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
        static HideFlags originalSettingsFlags;
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
            // Input System destroys its temporary HideAndDontSave settings when they are replaced.
            // Give the original a different lifetime flag while the test copy is active, so we
            // can put the same settings object back afterward. Restore its old flags too.
            originalSettingsFlags = originalSettings.hideFlags;
            originalSettings.hideFlags = HideFlags.DontUnloadUnusedAsset;
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
            originalSettings.hideFlags = originalSettingsFlags;
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
                    var chef = PlayerChef.Instance;
                    Require(!chef.Held && !chef.Animator.GetBool("Carrying") &&
                        chef.Animator.GetLayerWeight(chef.Animator.GetLayerIndex("Carry arms")) == 0,
                        "the final delivery releases the arm pose even after gameplay stops");
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

        // Follow the real session states while checking the music sources. Earlier orders are
        // resolved quickly to reach both endings. Seeking near each song's end checks looping
        // without waiting through several minutes of music for every test run.
        public static async Task<string> Music()
        {
            if (!Application.isPlaying)
                throw new Exception("Enter Play mode at the cookbook first.");
            checks = 0;
            LastResult = "Checking music and ambience removal";
            float originalMaster = AudioListener.volume;
            float savedVolume = PlayerPrefs.GetFloat("MonsterKitchen.MasterVolume", .75f);
            int savedMute = PlayerPrefs.GetInt("MonsterKitchen.Muted", 0);
            BeginInput();
            try
            {
                Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Title", "music check starts at the cookbook");
                await Task.Delay(800);
                var player = MusicPlayer.Instance;
                Require(player != null, "shared music player exists");

                async Task<AudioSource> CheckSong(string title)
                {
                    await Task.Delay(800);
                    Require(MusicPlayer.Instance && MusicPlayer.Instance.CurrentClip.name == title, "requested track is " + title);
                    var playing = MusicPlayer.Instance.GetComponents<AudioSource>().Where(a => a.isPlaying && a.volume > .001f).ToArray();
                    Require(playing.Length == 1 && playing[0].clip.name == title, "only the expected song remains after the fade");
                    Require(playing[0].loop && playing[0].spatialBlend == 0, "music loops in stereo screen space");
                    Require(UnityEngine.Object.FindObjectsByType<AudioSource>().All(a => !a.clip || !a.clip.name.StartsWith("ambience", StringComparison.OrdinalIgnoreCase)), "no constant ambience source remains");
                    return playing[0];
                }

                async Task CheckLoop(AudioSource song)
                {
                    song.timeSamples = song.clip.samples - (int)(song.clip.frequency * .12f);
                    await Task.Delay(450);
                    Require(song.isPlaying && song.timeSamples < song.clip.frequency, "song wraps and keeps playing: " + song.clip.name);
                }

                // Look during a transition as well as after it. Both songs should briefly
                // be audible, with the old one getting quieter and the new one getting louder.
                async Task CheckFade(string from, string to)
                {
                    await Task.Delay(180);
                    var sources = MusicPlayer.Instance.GetComponents<AudioSource>();
                    var outgoing = sources.Single(s => s.clip && s.clip.name == from);
                    var incoming = sources.Single(s => s.clip && s.clip.name == to);
                    float level = AudioFeedback.Instance.MusicVolume;
                    Require(outgoing.isPlaying && incoming.isPlaying && outgoing.volume > 0 && outgoing.volume < level && incoming.volume > 0 && incoming.volume < level, "songs overlap partway through the fade");
                    float oldLevel = outgoing.volume, newLevel = incoming.volume;
                    await Task.Delay(120);
                    Require(outgoing.volume < oldLevel && incoming.volume > newLevel, "both volumes move smoothly toward the next song");
                }

                var titleSong = await CheckSong("Lily-Paddling Down the Stream");
                await CheckLoop(titleSong);
                int before = titleSong.timeSamples;
                await Press(Key.Enter);
                Require(GameSession.Instance.State == RunState.Title, "Start cooking opens level select without leaving the cookbook");
                await Press(Key.Enter);
                Require(GameSession.Instance.State == RunState.Briefing, "kitchen waits at briefing");
                Require(MusicPlayer.Instance == player && titleSong.isPlaying && titleSong.timeSamples > before, "title song keeps its place across the scene load");
                var bell = UnityEngine.Object.FindObjectsByType<AudioSource>().SingleOrDefault(a => a.name == "Shop opening bell");
                Require(bell && bell.isPlaying && !bell.loop && bell.clip.name == "door-bell", "choosing a kitchen rings one bell that survives the scene change");
                before = bell.timeSamples;
                await Task.Delay(120);
                Require(bell.isPlaying && bell.timeSamples > before, "the bell keeps playing its tail in the kitchen");
                GameSession.Instance.BeginBriefing();
                Require(UnityEngine.Object.FindObjectsByType<AudioSource>().Count(a => a.name == "Shop opening bell") == 1, "an ignored second start does not ring another bell");
                await Task.Delay(950);
                Require(!bell, "the finished bell removes its temporary object");
                GameSession.Instance.StartShift();
                await CheckFade("Lily-Paddling Down the Stream", "Jazzy Shop");
                var jazz = await CheckSong("Jazzy Shop");
                before = jazz.timeSamples;
                await Task.Delay(350);
                Require(jazz.timeSamples > before, "session timer events do not restart the song");
                float normalVolume = jazz.volume;
                GameSession.Instance.TogglePause();
                before = jazz.timeSamples;
                await Task.Delay(800);
                Require(jazz.isPlaying && jazz.timeSamples > before && jazz.volume < normalVolume, "pause softens music while keeping its place");
                GameSession.Instance.TogglePause();
                await Task.Delay(800);
                Require(Mathf.Approximately(jazz.volume, normalVolume), "resume restores music level");
                bool wasMuted = AudioListener.volume == 0;
                await Press(Key.M);
                Require(wasMuted ? AudioListener.volume > 0 : AudioListener.volume == 0, "the existing mute key also controls music");
                await Press(Key.M);
                Require(wasMuted ? AudioListener.volume == 0 : AudioListener.volume > 0, "second mute press restores the previous sound state");
                await CheckLoop(jazz);

                // A half-filled cue list must not throw and interrupt a menu or station action.
                var clips = AudioFeedback.Instance.Clips;
                try
                {
                    AudioFeedback.Instance.Clips = null;
                    AudioFeedback.Play("pickup");
                }
                finally
                {
                    AudioFeedback.Instance.Clips = clips;
                }
                Require(true, "missing effect clips are safely ignored");

                var session = GameSession.Instance;
                int guard = 0;
                while (session.IsRunning && guard++ < 180)
                {
                    foreach (var order in session.Orders.ToArray())
                    {
                        order.AtCounter = true;
                        string reason;
                        session.TryServe(order.Slot, order.Food, order.Required, out reason);
                        if (!session.IsRunning)
                            break;
                    }
                    await Task.Delay(200);
                }
                Require(session.State == RunState.Won && session.Served == 8, "successful shift reaches the result screen");
                var resultSong = await CheckSong("Quircky Shop");
                await CheckLoop(resultSong);
                session.RestartShift();
                await CheckFade("Quircky Shop", "Jazzy Shop");
                await CheckSong("Jazzy Shop");

                guard = 0;
                while (session.IsRunning && guard++ < 100)
                {
                    foreach (var order in session.Orders)
                    {
                        order.AtCounter = true;
                        order.Remaining = .01f;
                    }
                    await Task.Delay(250);
                }
                Require(session.State == RunState.Lost, "missed orders reach the other ending");
                await CheckSong("Quircky Shop");
                session.ReturnToTitle();
                await CheckSong("Lily-Paddling Down the Stream");

                // Rapid scene changes interrupt fades. They must reuse the same two sources,
                // rather than leave extra persistent players or multiple copies of a song.
                for (int i = 0; i < 2; i++)
                {
                    GameSession.Instance.BeginBriefing();
                    await Task.Delay(120);
                    GameSession.Instance.StartShift();
                    await Task.Delay(120);
                    GameSession.Instance.ReturnToTitle();
                    await Task.Delay(120);
                }
                await CheckSong("Lily-Paddling Down the Stream");
                Require(UnityEngine.Object.FindObjectsByType<MusicPlayer>().Length == 1 && MusicPlayer.Instance == player, "scene changes keep exactly one music player");
                Require(MusicPlayer.Instance.GetComponents<AudioSource>().Length == 2, "crossfades keep exactly two music sources");
                Require(UnityEngine.Object.FindObjectsByType<AudioListener>().Length == 1, "only the current scene camera listens to audio");
                LastResult = checks + " music checks passed (tracks, loops, fades, opening bell, pause, mute, both endings, retry, scene changes and no ambience).";
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
                PlayerPrefs.SetFloat("MonsterKitchen.MasterVolume", savedVolume);
                PlayerPrefs.SetInt("MonsterKitchen.Muted", savedMute);
                AudioListener.volume = originalMaster;
            }
        }

        // Exercise the catalog through the same menu handlers used by mouse and keyboard.
        // extraScene can point at a temporary Editor test scene to prove that a second kitchen
        // loads by its own path. Scratch catalog entries exist only in memory and are removed.
        public static async Task<string> Levels(KitchenLevel extraScene = null)
        {
            if (!Application.isPlaying)
                throw new Exception("Enter Play mode at the cookbook first.");
            checks = 0;
            LastResult = "Checking level selection";
            var book = UnityEngine.Object.FindAnyObjectByType<CookbookMenu>();
            if (!book || !book.Levels)
                throw new Exception("The cookbook needs its saved level catalog.");
            var original = book.Levels;
            string originalPath = UnityEditor.AssetDatabase.GetAssetPath(original);
            string extraPath = extraScene ? UnityEditor.AssetDatabase.GetAssetPath(extraScene) : "";
            KitchenLevelCatalog scratch = null;
            var created = new System.Collections.Generic.List<KitchenLevel>();
            BeginInput();
            try
            {
                bool Has(string text) => UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>().Any(t => t.isActiveAndEnabled && t.text == text);
                UnityEngine.UI.Button[] Buttons() => UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().Where(b => b.isActiveAndEnabled).ToArray();
                async Task Click(string label)
                {
                    Buttons().Single(b => b.name == label).onClick.Invoke();
                    await Task.Delay(400);
                }

                int savedLevelCount = original.PublishedLevels().Count;
                var first = original.PublishedLevels().First();
                await Task.Delay(250);
                await Press(Key.Enter, 650);
                Require(Has("Choose a kitchen") && GameSession.Instance.State == RunState.Title, "holding Start opens level select without entering a kitchen");
                Require(!UnityEngine.Object.FindObjectsByType<AudioSource>().Any(a => a.name == "Shop opening bell"), "opening level select does not ring the door bell early");
                Require(Buttons().Any(b => b.name == "1. " + first.DisplayName) && Buttons().Any(b => b.name == "Back to the menu"), "the first kitchen and Back are available");
                await Press(Key.Escape);
                Require(Buttons().Any(b => b.name == "Start cooking"), "Escape returns from level select to the main book");
                await Press(Key.Enter);
                await Press(Key.Enter);
                Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == first.ScenePath, "the selected kitchen loads from its asset path");
                Require(GameSession.Instance.Level == first && GameSession.Instance.State == RunState.Briefing, "the kitchen receives its own level name and waits at briefing");
                GameSession.Instance.StartShift();
                GameSession.Instance.RestartShift();
                Require(GameSession.Instance.Level == first && GameSession.Instance.Served == 0, "retry keeps the current kitchen and starts a clean shift");
                GameSession.Instance.ReturnToTitle();
                await Task.Delay(500);

                // A single-scene load can unload assets held only by this async test's local
                // variables. Find the newly loaded catalog, and keep scratch data alive explicitly.
                original = UnityEditor.AssetDatabase.LoadAssetAtPath<KitchenLevelCatalog>(originalPath);
                first = original.PublishedLevels().First();
                extraScene = string.IsNullOrEmpty(extraPath) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<KitchenLevel>(extraPath);
                scratch = ScriptableObject.CreateInstance<KitchenLevelCatalog>();
                scratch.hideFlags = HideFlags.DontUnloadUnusedAsset;

                // More than three published entries must create a second page. A draft,
                // an empty slot and the same asset twice must not become extra menu choices.
                for (int i = 2; i <= 4; i++)
                {
                    var level = ScriptableObject.CreateInstance<KitchenLevel>();
                    level.DisplayName = "Test kitchen " + i;
                    level.ScenePath = first.ScenePath;
                    created.Add(level);
                }
                var missing = ScriptableObject.CreateInstance<KitchenLevel>();
                missing.DisplayName = "Unbuilt kitchen";
                missing.ScenePath = "Assets/MonsterKitchen/Scenes/NotBuilt.unity";
                created.Add(missing);
                var draft = ScriptableObject.CreateInstance<KitchenLevel>();
                draft.DisplayName = "Unpublished draft";
                draft.IsPublished = false;
                created.Add(draft);
                scratch.Levels = new[] { first, null, first, created[0], created[1], created[2], missing, extraScene, draft };
                book = UnityEngine.Object.FindAnyObjectByType<CookbookMenu>();
                book.Levels = scratch;
                await Click("Start cooking");
                Require(Has("Page 1 of 2"), "additional kitchens create more cookbook pages");
                Require(Buttons().Count(b => char.IsDigit(b.name[0])) == 3, "the first page fits three published kitchens");
                Require(!UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>().Any(t => t.isActiveAndEnabled && t.text.Contains("Unpublished draft")), "draft entries stay out of the menu");
                await Click("Next page");
                Require(Has("Page 2 of 2") && Buttons().Any(b => b.name == "4. Test kitchen 4"), "Next keeps the catalog order on the second page");
                Require(Has("5. Unbuilt kitchen (closed)") && !Buttons().Any(b => b.name.StartsWith("5.")), "a missing build scene is shown as closed and cannot be clicked");
                GameSession.Instance.BeginBriefing(missing);
                Require(GameSession.Instance.State == RunState.Title, "a missing scene cannot leave the menu in a half-started state");
                await Click("Previous page");
                Require(Has("Page 1 of 2"), "Previous returns to the first page");
                await Click("Back to the menu");

                // Empty data is a safe, navigable closed page rather than a null-reference error.
                scratch.Levels = null;
                await Click("Start cooking");
                Require(Has("The kitchen is closed for now.") && Buttons().Length == 1, "an empty catalog still offers Back");
                await Click("Back to the menu");
                book.Levels = null;
                await Click("Start cooking");
                Require(Has("The kitchen is closed for now.") && Buttons().Length == 1, "a missing catalog stays navigable too");
                await Click("Back to the menu");

                if (extraScene)
                {
                    scratch.Levels = new[] { first, extraScene };
                    book.Levels = scratch;
                    await Click("Start cooking");
                    await Click("2. " + extraScene.DisplayName);
                    Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == extraScene.ScenePath, "a new level loads a different scene without changing menu code");
                    Require(GameSession.Instance.Level == extraScene, "the second scene keeps its own level asset");
                    GameSession.Instance.StartShift();
                    GameSession.Instance.RestartShift();
                    Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == extraScene.ScenePath && GameSession.Instance.Level == extraScene, "retry stays in the second scene too");
                    GameSession.Instance.ReturnToTitle();
                    await Task.Delay(500);
                }
                book = UnityEngine.Object.FindAnyObjectByType<CookbookMenu>();
                original = UnityEditor.AssetDatabase.LoadAssetAtPath<KitchenLevelCatalog>(originalPath);
                book.Levels = original;
                Require(original.PublishedLevels().Count == savedLevelCount, "scratch levels never change the saved catalog");
                LastResult = checks + " level-select checks passed (input, catalog pages, drafts, missing scenes, retry and future scene loading).";
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
                if (GameSession.Instance && !GameSession.Instance.IsTitleScene)
                {
                    GameSession.Instance.ReturnToTitle();
                    await Task.Delay(400);
                }
                book = UnityEngine.Object.FindAnyObjectByType<CookbookMenu>();
                if (book)
                    book.Levels = UnityEditor.AssetDatabase.LoadAssetAtPath<KitchenLevelCatalog>(originalPath);
                foreach (var level in created)
                    UnityEngine.Object.Destroy(level);
                UnityEngine.Object.Destroy(scratch);
            }
        }

        // Use keyboard input to go from cookbook help through level select, briefing, play and pause.
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
                Require(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>().Any(t => t.text == "Choose a kitchen"), "Start cooking opens the level-select page");
                await Press(Key.Enter);
                await Task.Delay(600);
                Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Kitchen", "choosing the first kitchen loads its scene");
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

        // Compare the witch's actual arm bones before and after a transfer. Checking only the
        // Carrying flag missed the original bug: it was false while the arms still held their pose.
        public static async Task<string> ChefArms()
        {
            checks = 0;
            LastResult = "Checking chef arm poses";
            BeginInput();
            try
            {
                var session = GameSession.Instance;
                session.RestartShift();
                await Task.Delay(350);
                var chef = PlayerChef.Instance;
                var animator = chef.Animator;
                int layer = animator.GetLayerIndex("Carry arms");
                var arms = animator.GetComponentsInChildren<Transform>()
                    .Where(t => t.name == "upperarm.l" || t.name == "upperarm.r").ToArray();
                Require(layer >= 0 && arms.Length == 2, "chef has a carry layer and both arm bones");
                Quaternion[] Pose() => arms.Select(t => t.localRotation).ToArray();
                float Difference(Quaternion[] a, Quaternion[] b) => a.Zip(b, Quaternion.Angle).Max();
                var relaxed = Pose();
                Require(!chef.Held && animator.GetLayerWeight(layer) == 0, "empty hands start with the normal idle pose");
                var supply = KitchenStation.All.First(s => s.Type == StationType.Supply && s.SupplyFood == FoodKind.Patty);

                foreach (var destination in new[] { Find(StationType.Counter), Find(StationType.Hotplate), Find(StationType.Trash) })
                {
                    Require(supply.Interact(chef), "pick up food before testing " + destination.name);
                    await Task.Delay(350);
                    Require(animator.GetBool("Carrying") && Difference(relaxed, Pose()) > 15, "holding food raises the arms");
                    Require(destination.Interact(chef), "put food down at " + destination.name);
                    await Task.Delay(400);
                    Require(!chef.Held && !animator.GetBool("Carrying") && Difference(relaxed, Pose()) < 8,
                        "arms return to idle after " + destination.name);
                }

                // Walking should swing the empty arms, rather than leaving a frozen holding pose.
                held = new[] { Key.W };
                await Task.Delay(300);
                var walking = Pose();
                await Task.Delay(180);
                Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk") && Difference(walking, Pose()) > 1,
                    "empty-handed walking animates the arm bones");
                held = Array.Empty<Key>();
                await Task.Delay(300);
                Require(supply.Interact(chef), "pick up food for the pause check");
                await Task.Delay(350);
                session.TogglePause();
                await Task.Delay(80);
                var paused = Pose();
                await Task.Delay(250);
                Require(Difference(paused, Pose()) < .01f, "pause keeps the carrying pose still");
                session.TogglePause();
                Require(Find(StationType.Trash).Interact(chef), "discard after resuming");
                await Task.Delay(400);
                Require(Difference(relaxed, Pose()) < 8, "resuming and releasing food restores free arms");

                // Retry must clear the arm override immediately, even if the old shift was paused.
                supply.Interact(chef);
                await Task.Delay(350);
                session.TogglePause();
                session.RestartShift();
                Require(!chef.Held && !animator.GetBool("Carrying") && animator.GetLayerWeight(layer) == 0,
                    "retry immediately clears food and the carrying layer");
                await Task.Delay(350);
                Require(Difference(relaxed, Pose()) < 8, "retry settles back into the relaxed idle pose");
                LastResult = checks + " chef arm checks passed (idle, walking, pickup, counters, stove, bin, pause and retry).";
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
