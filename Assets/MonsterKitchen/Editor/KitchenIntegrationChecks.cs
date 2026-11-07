// Checks that run against the live game in Unity's Editor. They are not included in a player build.
// Most checks use the same components and key events as a player. Some setup steps accelerate heat
// or resolve earlier orders so each check can reach the behavior it is meant to test.
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MonsterKitchen.Editor
{
    public static class KitchenIntegrationChecks
    {
        // Read this after starting an async check to see whether it is still running, passed or failed.
        public static string LastResult = "Not run";
        static int checks;
        static Key[] held = Array.Empty<Key>();
        static Keyboard testKeyboard, originalKeyboard;
        static InputSettings originalSettings, testSettings;
        // Stop at the first failed expectation and include its description in the error.
        static void Require(bool value, string message)
        {
            if (!value)
                throw new Exception("CHECK FAILED: " + message);
            checks++;
        }

        // Send the held test keys on each normal input update, just like a key held across several frames.
        static void FeedKeyboard()
        {
            if (InputState.currentUpdateType == InputUpdateType.Dynamic && testKeyboard != null)
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(held));
        }

        // Create a temporary virtual keyboard and copy the input settings for the test.
        // The original device and settings are kept so they can be restored afterward.
        static void BeginInput()
        {
            originalKeyboard = Keyboard.current;
            originalSettings = InputSystem.settings;
            testSettings = UnityEngine.Object.Instantiate(originalSettings);
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = testSettings;
            testKeyboard = InputSystem.AddDevice<Keyboard>();
            testKeyboard.MakeCurrent();
            InputSystem.onBeforeUpdate += FeedKeyboard;
        }

        // Always release test input and restore the real keyboard, even when a check throws an error.
        static void EndInput()
        {
            held = Array.Empty<Key>();
            InputSystem.onBeforeUpdate -= FeedKeyboard;
            if (testKeyboard != null)
                InputSystem.RemoveDevice(testKeyboard);
            testKeyboard = null;
            if (originalKeyboard != null && originalKeyboard.added)
                originalKeyboard.MakeCurrent();
            InputSystem.settings = originalSettings;
            if (testSettings)
                UnityEngine.Object.Destroy(testSettings);
        }

        // Hold a key for a short period, then release it. The waits allow Unity's Update loop to process both.
        static async Task Press(Key key, int milliseconds = 140)
        {
            held = new[]
            {
                key
            };
            await Task.Delay(milliseconds);
            held = Array.Empty<Key>();
            await Task.Delay(100);
        }

        // Find a station of the requested type, and the requested slot for a serving counter.
        static KitchenStation Find(StationType type, int slot = 0) => KitchenStation.All.First(s => s.Type == type && (type != StationType.Serve || s.ServiceSlot == slot));
        // Place the chef at a station's approach point and wait for physics to report trigger entry.
        // The action itself still goes through the game's selected target and interaction code.
        static async Task Approach(KitchenStation station)
        {
            if (station.Type == StationType.Serve)
                await WaitForGuests();
            PlayerChef.Instance.Teleport(station.ReachPoint.position + Vector3.up * .08f);
            await Task.Delay(180);
            Require(station.PlayerInside, "station collision trigger detects chef: " + station.name);
            Require(PlayerChef.Instance.Target == station, "correct station target: " + station.name);
        }

        // Wait for the real walking routes to finish before attempting service.
        // A limit turns a stuck route into a useful test failure rather than waiting forever.
        static async Task WaitForGuests()
        {
            int guard = 0;
            while (GameSession.Instance.Orders.Any(t => !t.AtCounter) && guard++ < 150)
                await Task.Delay(100);
            Require(GameSession.Instance.Orders.All(t => t.AtCounter), "guests reach the serving counter through the doorway");
        }

        // Exercise movement, pickup, heating, recovery, rejection, disposal, pause, reset and both endings.
        // Later schedule checks use the serving API directly; Transitions covers the actual final key press.
        public static async Task<string> Run()
        {
            if (!Application.isPlaying)
                throw new Exception("Enter Play mode in Kitchen first.");
            checks = 0;
            LastResult = "Running";
            BeginInput();
            try
            {
                var session = GameSession.Instance;
                var chef = PlayerChef.Instance;
                session.RestartShift();
                await Task.Delay(160);
                Vector3 start = chef.transform.position;
                await Press(Key.D, 350);
                Require(Vector3.Distance(start, chef.transform.position) > .7f, "continuous keyboard movement");
                var supply = KitchenStation.All.First(s => s.Type == StationType.Supply && s.SupplyFood == FoodKind.Patty);
                var stove = Find(StationType.Hotplate);
                await Approach(supply);
                await Press(Key.E);
                Require(chef.Held && chef.Held.Kind == FoodKind.Patty, "keyboard ingredient pickup");
                var portion = chef.Held;
                await Press(Key.E);
                Require(chef.Held == portion, "full hands do not duplicate ingredients");
                await Approach(stove);
                await Press(Key.E);
                Require(stove.Item == portion && !chef.Held, "atomic hands-to-hotplate transfer");
                float beforeHeat = portion.HeatSeconds;
                await Task.Delay(450);
                Require(portion.HeatSeconds > beforeHeat + .2f, "hotplate advances naturally");
                await Press(Key.Escape);
                Require(session.State == RunState.Paused, "keyboard pause");
                float frozenHeat = portion.HeatSeconds, frozenPatience = session.Orders[0].Remaining;
                await Task.Delay(350);
                Require(Mathf.Approximately(frozenHeat, portion.HeatSeconds) && Mathf.Approximately(frozenPatience, session.Orders[0].Remaining), "pause freezes cooking and patience");
                await Press(Key.Escape);
                Require(session.IsRunning, "keyboard resume");
                portion.AddHeat(7.1f - portion.HeatSeconds);
                await Press(Key.E);
                Require(chef.Held == portion && portion.Heat == HeatState.Cooked && !stove.Item, "collect cooked food");
                frozenHeat = portion.HeatSeconds;
                await Task.Delay(250);
                Require(portion.HeatSeconds == frozenHeat, "carrying stops heat");
                await Approach(Find(StationType.Serve, session.Orders[0].Slot));
                await Press(Key.E);
                Require(session.Served == 1 && !chef.Held, "successful real service consumes one dish");
                await Task.Delay(2200);
                Require(session.Orders.Count == 1 && session.Orders[0].Required == HeatState.Charred, "second teaching order requests charred food");
                await Approach(supply);
                await Press(Key.E);
                var wrong = chef.Held;
                await Approach(Find(StationType.Serve, session.Orders[0].Slot));
                await Press(Key.E);
                Require(chef.Held == wrong && session.Served == 1, "wrong delivery preserves food and ticket");
                wrong.AddHeat(12.2f);
                await Press(Key.E);
                Require(session.Served == 2, "charred order accepted");
                await Task.Delay(2200);
                Require(session.Orders.Count == 2, "first mixed pair overlaps");
                var cookedTicket = session.Orders.First(t => t.Required == HeatState.Cooked);
                var charredTicket = session.Orders.First(t => t.Required == HeatState.Charred);
                await Approach(supply);
                await Press(Key.E);
                chef.Held.AddHeat(12.1f);
                await Approach(Find(StationType.Serve, charredTicket.Slot));
                await Press(Key.E);
                Require(session.Orders.Contains(cookedTicket) && session.Served == 3, "salvage leaves original cooked order pending");
                await Approach(supply);
                await Press(Key.E);
                chef.Held.AddHeat(30);
                await Approach(Find(StationType.Serve, cookedTicket.Slot));
                await Press(Key.E);
                Require(chef.Held && chef.Held.Heat == HeatState.Ash, "ash is rejected");
                await Approach(Find(StationType.Trash));
                await Press(Key.E);
                Require(!chef.Held && session.Wasted == 1, "discard frees hands and records waste");
                // Boundary checks exercise CharacterController collision against authored walls.
                chef.Teleport(new Vector3(6.5f, .08f, -3.3f));
                for (int i = 0; i < 50; i++)
                    chef.Move(new Vector2(1, 0), .03f);
                Require(chef.transform.position.x < 7.2f, "east boundary blocks movement");
                session.RestartShift();
                await Task.Delay(100);
                Require(session.Served == 0 && session.Complaints == 0 && session.Wasted == 0 && !chef.Held && KitchenStation.All.All(s => !s.Item), "restart resets runtime state");
                // Resolve remaining order schedules quickly, still through the actual acceptance API.
                int guard = 0;
                while (session.IsRunning && guard++ < 30)
                {
                    await WaitForGuests();
                    foreach (var ticket in session.Orders.ToArray())
                    {
                        string reason;
                        session.TryServe(ticket.Slot, ticket.Food, ticket.Required, out reason);
                        if (!session.IsRunning)
                            break;
                    }

                    await Task.Delay(2200);
                }

                Require(session.State == RunState.Won && session.Served == 8, "complete authored shift reaches win");
                session.RestartShift();
                guard = 0;
                while (session.IsRunning && guard++ < 8)
                {
                    await WaitForGuests();
                    foreach (var ticket in session.Orders)
                        ticket.Remaining = .01f;
                    await Task.Delay(2200);
                }

                Require(session.State == RunState.Lost && session.Complaints == 3 && session.Served == 0, "waiting reaches loss, never win");
                session.RestartShift();
                session.TogglePause();
                LastResult = checks + " Unity integration checks passed (keyboard, collisions, ownership, cooking, recovery, pause, restart, win and loss).";
                Debug.Log(LastResult);
                return LastResult;
            }
            catch (Exception e)
            {
                LastResult = e.Message;
                Debug.LogError(e);
                throw;
            }
            finally
            {
                EndInput();
            }
        }

        // Navigate the controls and sound pages, try the pause/restart choices, and inspect a completed receipt.
        public static async Task<string> PaperUI()
        {
            checks = 0;
            LastResult = "Checking paper UI";
            BeginInput();
            bool Has(string text) => UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>().Any(t => t.text == text && t.gameObject.activeInHierarchy);
            try
            {
                var session = GameSession.Instance;
                session.RestartShift();
                await Task.Delay(180);
                await Press(Key.Escape);
                Require(Has("Paused"), "pause sheet appears");
                await Press(Key.S);
                await Press(Key.Enter);
                Require(Has("How to play") && Has("Take, place or serve"), "pause opens controls");
                await Press(Key.Escape);
                Require(Has("Paused"), "controls return to paused shift");
                await Press(Key.S);
                await Press(Key.S);
                await Press(Key.Enter);
                Require(Has("Sound"), "pause opens sound page");
                var slider = UnityEngine.Object.FindAnyObjectByType<UnityEngine.UI.Slider>();
                float oldVolume = slider.value;
                slider.value = oldVolume > .5f ? .25f : .75f;
                await Task.Delay(100);
                Require(Mathf.Approximately(AudioListener.volume, slider.value), "volume slider changes playback");
                slider.value = oldVolume;
                await Press(Key.M);
                Require(AudioListener.volume == 0, "mute key silences audio");
                await Press(Key.M);
                Require(Mathf.Approximately(AudioListener.volume, oldVolume), "unmute restores volume");
                await Press(Key.Escape);
                Require(Has("Paused"), "sound returns to pause");
                await Press(Key.S);
                await Press(Key.S);
                await Press(Key.S);
                await Press(Key.Enter);
                Require(session.IsRunning && session.Served == 0 && session.Complaints == 0, "restart choice begins a clean shift");
                var pauseButton = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().First(b => b.name == "Pause");
                pauseButton.onClick.Invoke();
                await Task.Delay(100);
                Require(session.State == RunState.Paused, "HUD pause button is wired");
                await Press(Key.Enter);
                Require(session.IsRunning, "resume choice returns to game");
                int guard = 0;
                while (session.IsRunning && guard++ < 30)
                {
                    await WaitForGuests();
                    foreach (var ticket in session.Orders.ToArray())
                    {
                        string reason;
                        session.TryServe(ticket.Slot, ticket.Food, ticket.Required, out reason);
                        if (!session.IsRunning)
                            break;
                    }

                    await Task.Delay(2200);
                }

                Require(session.State == RunState.Won && Has("Shift complete"), "successful shift displays receipt");
                Require(Has(session.Score.ToString()) && Has("8 / 8"), "receipt shows actual score and served count");
                Require(UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>().All(t => t.font && t.font.atlasTexture), "visible text uses bundled font and atlas");
                LastResult = checks + " paper UI checks passed (controls, sound, restart, pause, resume and completed-shift receipt).";
                Debug.Log(LastResult);
                return LastResult;
            }
            catch (Exception e)
            {
                LastResult = e.Message;
                Debug.LogError(e);
                throw;
            }
            finally
            {
                EndInput();
            }
        }

        // Serve guest eight using real E and Space key events, including holding each key down.
        // Earlier orders are fast-forwarded only to set up that last interaction. The checks require the
        // receipt to stay open, a fresh Retry choice to work, and title return to hide the outgoing UI.
        public static async Task<string> Transitions()
        {
            checks = 0;
            LastResult = "Checking gameplay/menu transitions";
            BeginInput();
            try
            {
                foreach (Key finishKey in new[]
                {
                    Key.E,
                    Key.Space
                }

                )
                {
                    var session = GameSession.Instance;
                    session.RestartShift();
                    int guard = 0;
                    while ((session.Served < 7 || session.Orders.Count == 0) && guard++ < 180)
                    {
                        foreach (var order in session.Orders.ToArray())
                        {
                            if (session.Served >= 7)
                                break;
                            order.AtCounter = true;
                            string reason;
                            session.TryServe(order.Slot, order.Food, order.Required, out reason);
                        }

                        await Task.Delay(200);
                    }

                    Require(session.Served == 7 && session.Orders.Count > 0, "prepare the last meal for " + finishKey);
                    var finalOrder = session.Orders[0];
                    finalOrder.AtCounter = true;
                    var portion = new GameObject("Transition test meal").AddComponent<FoodItem>();
                    portion.Initialize(finalOrder.Food);
                    var heat = KitchenRules.Thresholds(finalOrder.Food);
                    portion.AddHeat((finalOrder.Required == HeatState.Cooked ? heat.Cooked : heat.Charred) + .1f);
                    Require(PlayerChef.Instance.Take(portion), "chef holds the last requested meal");
                    var counter = Find(StationType.Serve, finalOrder.Slot);
                    PlayerChef.Instance.Teleport(counter.ReachPoint.position + Vector3.up * .08f);
                    await Task.Delay(200);
                    Require(PlayerChef.Instance.Target == counter, "last serving counter is selected");
                    await Press(finishKey, 650);
                    Require(session.State == RunState.Won && session.Served == 8, "last " + finishKey + " press stays on the ending instead of restarting");
                    Require(UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>().Any(t => t.text == "Shift complete" && t.gameObject.activeInHierarchy), "ending receipt is visible");
                    await Task.Delay(450);
                    Require(session.State == RunState.Won, "ending waits after the serving key is released");
                    // Capture the actual keyboard-delivered ending before choosing Retry.
                    ScreenCapture.CaptureScreenshot("Screenshots/win-final-" + finishKey.ToString().ToLowerInvariant() + ".png");
                    await Task.Delay(200);
                    await Press(Key.Enter);
                    Require(session.IsRunning && session.Served == 0, "a new Enter press deliberately starts another shift");
                }

                await Press(Key.Escape);
                var exiting = GameSession.Instance;
                var kitchenCanvas = UnityEngine.Object.FindObjectsByType<Canvas>().First(c => c.name == "Kitchen paper UI");
                bool exitObserved = false, kitchenHiddenOnExit = false;
                void ObserveExit()
                {
                    if (exiting.State != RunState.Title)
                        return;
                    exitObserved = true;
                    kitchenHiddenOnExit = !kitchenCanvas.enabled;
                }

                exiting.Changed += ObserveExit;
                for (int i = 0; i < 4; i++)
                    await Press(Key.S);
                await Press(Key.Enter, 650);
                Require(exitObserved && kitchenHiddenOnExit, "kitchen UI hides immediately on return to title");
                Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Title", "pause menu loads the cookbook");
                Require(GameSession.Instance.State == RunState.Title, "held confirm key does not reopen the briefing");
                await Task.Delay(250);
                Require(GameSession.Instance.State == RunState.Title, "cookbook stays open after release");
                LastResult = checks + " transition checks passed (E, Space, ending, deliberate retry and title return).";
                Debug.Log(LastResult);
                return LastResult;
            }
            catch (Exception e)
            {
                LastResult = e.Message;
                Debug.LogError(e);
                throw;
            }
            finally
            {
                EndInput();
            }
        }

        // Use keyboard input to go from cookbook help to the kitchen briefing, play and pause.
        public static async Task<string> Menu()
        {
            checks = 0;
            LastResult = "Running cookbook menu checks";
            BeginInput();
            try
            {
                Require(UnityEngine.Object.FindAnyObjectByType<CookbookMenu>() != null, "cookbook menu is present");
                await Task.Delay(200);
                await Press(Key.S);
                await Press(Key.Enter);
                Require(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>().Any(t => t.text == "The house rules"), "keyboard opens book instructions");
                await Press(Key.Escape);
                await Press(Key.Enter);
                await Task.Delay(600);
                Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Kitchen", "book Start loads Kitchen");
                Require(GameSession.Instance.State == RunState.Briefing, "arrives at briefing before time starts");
                await Press(Key.Enter);
                Require(GameSession.Instance.IsRunning, "keyboard starts gameplay from briefing");
                await Press(Key.Escape);
                Require(GameSession.Instance.State == RunState.Paused, "keyboard pauses after menu transition");
                LastResult = checks + " cookbook navigation checks passed.";
                return LastResult;
            }
            catch (Exception e)
            {
                LastResult = e.Message;
                Debug.LogError(e);
                throw;
            }
            finally
            {
                EndInput();
            }
        }

        // Check that guests walk in before they can be served, react to service, and leave through the door.
        // The route is sampled along the way to catch shortcuts through the kitchen furniture.
        public static async Task<string> CustomerRoutes()
        {
            checks = 0;
            LastResult = "Checking customer routes and animation";
            try
            {
                var session = GameSession.Instance;
                session.RestartShift();
                await Task.Delay(120);
                var ticket = session.Orders[0];
                var guest = UnityEngine.Object.FindObjectsByType<GuestMotion>().First(g => g.TicketId == ticket.Id);
                Require(!ticket.AtCounter, "walking guest cannot be served yet");
                float patience = ticket.Remaining;
                string reason;
                Require(!session.TryServe(ticket.Slot, ticket.Food, ticket.Required, out reason), "serving waits for physical customer arrival");
                await Task.Delay(300);
                Require(ticket.Remaining == patience, "doorway walk does not spend patience");
                Require(guest.IsWalking && guest.Animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"), "walk animation plays while entering");
                Vector3 pausedPosition = guest.transform.position;
                session.TogglePause();
                await Task.Delay(250);
                Require(Vector3.Distance(pausedPosition, guest.transform.position) < .001f, "pause freezes customer movement");
                session.TogglePause();
                await WaitForGuests();
                await Task.Delay(250);
                Require(guest.Animator.GetCurrentAnimatorStateInfo(0).IsName("Wave"), "customer greets the chef on arrival");
                Require(Vector3.Dot(guest.transform.forward, Vector3.back) > .99f, "waiting guest faces the serving counter");
                Require(session.TryServe(ticket.Slot, ticket.Food, ticket.Required, out reason), "arrived customer accepts the requested meal");
                await Task.Delay(250);
                Require(guest.Animator.GetCurrentAnimatorStateInfo(0).IsName("Cheer"), "served customer celebrates before leaving");
                bool usedDoor = false, safeRoute = true, walkedOut = false;
                int guard = 0;
                while (guest && guard++ < 140)
                {
                    Vector3 p = guest.transform.position;
                    safeRoute &= p.z >= 3.5f && (p.x < 6.9f || (p.z > 4.4f && p.z < 7.1f));
                    usedDoor |= p.x > 7.3f;
                    walkedOut |= guest.IsWalking;
                    await Task.Delay(100);
                }

                Require(safeRoute, "exit stays in the customer aisle and doorway, clear of kitchen appliances");
                Require(usedDoor && walkedOut && !guest, "customer walks through exit and despawns outside");
                session.RestartShift();
                session.TogglePause();
                LastResult = checks + " customer route and animation checks passed.";
                Debug.Log(LastResult);
                return LastResult;
            }
            catch (Exception e)
            {
                LastResult = e.Message;
                Debug.LogError(e);
                throw;
            }
        }
    }
}
