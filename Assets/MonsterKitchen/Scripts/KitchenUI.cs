// The kitchen's briefing, order slips, pause pages and end-of-shift receipt.
// GameSession owns the rules. This script reads those rules and gives the player buttons and feedback.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace MonsterKitchen
{
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class KitchenUI : MonoBehaviour
    {
        // Shared colors keep paper, ink, cooked food and charred food consistent across the UI.
        static readonly Color Ink = Hex("49352B"), Paper = Hex("FFF9E8"), Line = Hex("D8C5A4"), Sage = Hex("687F50"), SageWash = Hex("DCE6C8"), Tomato = Hex("A64F3D"), TomatoWash = Hex("EBC1A5"), Muted = Hex("806C59"), Tape = Hex("D7C292");
        // These PlayerPrefs names are also used by CookbookMenu, so sound settings carry between scenes.
        const string VolumeKey = "MonsterKitchen.MasterVolume", MuteKey = "MonsterKitchen.Muted";
        // A menu row keeps its background, label and action together for both mouse and keyboard input.
        sealed class MenuEntry
        {
            public UnityEngine.UI.Image Background;
            public TMP_Text Label;
            public Action Action;
        }

        // References to one visible order slip. The matching OrderTicket contains the actual order data.
        sealed class TicketView
        {
            public RectTransform Root, Progress;
            public TMP_Text Customer, Food, State, Time;
            public UnityEngine.UI.Image StatePaper, Bar;
            public KitchenFoodGlyph Glyph;
        }

        GameSession session;
        Canvas canvas;
        TMP_FontAsset font;
        readonly Dictionary<GameObject, List<MenuEntry>> menus = new Dictionary<GameObject, List<MenuEntry>>();
        // There are three service slots. Each order uses the slip with the same slot number.
        readonly TicketView[] tickets = new TicketView[3];
        readonly OrderTicket[] orders = new OrderTicket[3];
        // Each view is a whole screen or menu page. Only the appropriate one is visible at a time.
        readonly List<RectTransform> views = new List<RectTransform>();
        RectTransform briefing, hud, pause, results, controls, audioPage, carry, guide, toast;
        TMP_Text served, complaints, score, prompt, heldFood, heldState, guideText, toastText, volumeText, pauseStats;
        TMP_Text resultTitle, resultServed, resultComplaints, resultWaste, resultScore, resultTime;
        KitchenFoodGlyph heldGlyph;
        UnityEngine.UI.Slider volumeSlider;
        List<MenuEntry> activeMenu;
        readonly MenuInputGate menuInput = new MenuInputGate();
        RunState lastState;
        // Controls and Sound are pages inside a menu. They do not change the game's Playing/Paused state.
        string subpage = "";
        bool hasState;
        // Session events request a refresh. The short toast timer and menu repeat timer use real time
        // so they keep working when cooking and customer patience are paused.
        bool refresh = true, muted, syncSlider;
        float volume, toastUntil, nextNav;
        int selected, lastDirection;
        // Build the Canvas, text and buttons once. The runtime then hides or updates existing elements.
        // A screen-space overlay keeps these panels readable regardless of the kitchen camera's angle.
        void Awake()
        {
            font = Resources.Load<TMP_FontAsset>("UI/KitchenRounded");
            volume = PlayerPrefs.GetFloat(VolumeKey, .75f);
            muted = PlayerPrefs.GetInt(MuteKey, 0) != 0;
            AudioListener.volume = muted ? 0 : volume;
            var root = new GameObject("Kitchen paper UI", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            if (!EventSystem.current)
            {
                var input = new GameObject("Kitchen pointer input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                input.GetComponent<EventSystem>().sendNavigationEvents = false;
                input.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            BuildBriefing();
            BuildHUD();
            BuildPause();
            BuildControls();
            BuildAudio();
            BuildResults();
            BuildToast();
            foreach (var view in views)
                view.gameObject.SetActive(false);
        }

        // Disconnect from the old session when leaving the kitchen.
        void OnDestroy()
        {
            if (session)
            {
                session.Changed -= Changed;
                session.Message -= ShowMessage;
            }
        }

        // Mark the displayed information as out of date. Returning to Title also hides the Canvas
        // immediately, before the scene load finishes, so the briefing cannot flash during the exit.
        void Changed()
        {
            refresh = true;
            // LoadScene finishes at a frame boundary. Hide this scene's UI now so
            // the old kitchen cannot show its briefing while the cookbook loads.
            if (session && session.State == RunState.Title)
            {
                canvas.enabled = false;
                activeMenu = null;
            }
        }

        // Notice state changes before reading menu input. That order matters: serving the last meal
        // changes Playing to Won during PlayerChef.Update, earlier in this same frame.
        // Arm the fresh-input check first, then show the result without treating that serving key as Retry.
        void Update()
        {
            if (!session && GameSession.Instance)
            {
                session = GameSession.Instance;
                session.Changed += Changed;
                session.Message += ShowMessage;
            }

            if (!session)
                return;
            if (!hasState || session.State != lastState)
            {
                hasState = true;
                lastState = session.State;
                subpage = "";
                selected = 0;
                lastDirection = 0;
                refresh = true;
                if (!session.IsRunning)
                    toastUntil = 0;
                // The key that serves guest eight belongs to gameplay, not Retry.
                // Give the result a brief moment to appear, then require a fresh press.
                menuInput.Reset(lastState == RunState.Won || lastState == RunState.Lost ? .35f : 0f);
            }

            if (refresh)
                RefreshScreen();
            menuInput.Update();
            HandleKeyboard();
            if (session.IsRunning)
                RefreshHUD();
            toast.gameObject.SetActive(session.IsRunning && Time.unscaledTime < toastUntil);
        }

        // Explain the goal and the two guest preferences before the shift starts its timers.
        void BuildBriefing()
        {
            briefing = View("Briefing");
            var sheet = Sheet(briefing, "Tonight's menu", 820, 720);
            Text(sheet, "Tonight's menu", 40, 48, 740, 75, 53, Ink, true, TextAlignmentOptions.Center);
            Text(sheet, "Serve 8 guests before 3 leave hungry.", 50, 145, 720, 45, 29, Muted, false, TextAlignmentOptions.Center);
            Preference(sheet, 80, "Vampire", "Cooked", false);
            Preference(sheet, 440, "Tiefling", "Charred", true);
            Rule(sheet, 70, 392, 680);
            Text(sheet, "Take food. Cook it. Serve the right guest.", 55, 420, 710, 42, 29, Ink, false, TextAlignmentOptions.Center);
            Text(sheet, "WASD / arrows to move   |   E / Space to interact", 50, 478, 720, 52, 25, Muted, false, TextAlignmentOptions.Center);
            Choice(sheet, briefing, "Open kitchen", 120, 560, 580, () => session.StartShift());
            Choice(sheet, briefing, "Back", 120, 635, 580, () => session.ReturnToTitle(), 29);
        }

        // Make one illustrated guest-preference card for the briefing.
        void Preference(RectTransform parent, float x, string guest, string state, bool charred)
        {
            var p = PaperAt(parent, guest, x, 212, 300, 150, charred ? TomatoWash : SageWash);
            Glyph(p, 20, 19, 104, 96, 0, charred);
            Text(p, guest, 132, 25, 155, 38, 29, Ink, true);
            Text(p, state, 132, 70, 155, 36, 27, charred ? Tomato : Sage);
        }

        // Put orders at the top, totals in the corners, and the current station action at the bottom.
        // Held-food information appears only when the chef is actually carrying a portion.
        void BuildHUD()
        {
            hud = View("HUD");
            var row = new GameObject("Order slips", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(hud, false);
            Place(row, 0, 1, 24, -20, 700, 230, new Vector2(0, 1));
            for (int i = 0; i < 3; i++)
                tickets[i] = BuildTicket(row, i);
            var tally = PaperAt(hud, "Shift tally", 0, 0, 260, 102, Paper);
            Place(tally, 0, 0, 24, 22, 260, 102, Vector2.zero);
            served = Text(tally, "0 / 8", 18, 8, 137, 48, 39, Ink, true);
            Text(tally, "served", 153, 20, 95, 30, 25, Muted);
            complaints = Text(tally, "0 / 3 left hungry", 18, 61, 235, 31, 23, Tomato);
            var tips = PaperAt(hud, "Till receipt", 0, 0, 215, 102, Paper);
            Place(tips, 1, 0, -24, 22, 215, 102, new Vector2(1, 0));
            Text(tips, "SCORE", 18, 10, 175, 26, 20, Muted, false, TextAlignmentOptions.Center);
            score = Text(tips, "0", 18, 37, 175, 50, 40, Ink, true, TextAlignmentOptions.Center);
            carry = PaperAt(hud, "Held plate", 0, 0, 210, 94, Paper);
            Place(carry, 0, 0, 300, 22, 210, 94, Vector2.zero);
            heldGlyph = Glyph(carry, 8, 13, 85, 66, 0, false);
            heldFood = Text(carry, "Patty", 100, 14, 102, 30, 25, Ink, true);
            heldState = Text(carry, "Raw", 100, 48, 102, 27, 21, Muted);
            var action = PaperAt(hud, "Station action", 0, 0, 680, 69, Paper);
            Place(action, .5f, 0, 30, 24, 680, 69, new Vector2(.5f, 0));
            var key = PaperAt(action, "E key", 12, 10, 49, 48, SageWash);
            Text(key, "E", 0, 0, 49, 48, 30, Ink, true, TextAlignmentOptions.Center);
            prompt = Text(action, "Find a station", 78, 7, 583, 55, 27, Ink);
            var pauseButton = PaperAt(hud, "Pause", 0, 0, 163, 51, Paper);
            Place(pauseButton, 1, 1, -24, -22, 163, 51, new Vector2(1, 1));
            Text(pauseButton, "Pause  Esc", 0, 0, 163, 51, 24, Ink, false, TextAlignmentOptions.Center);
            var button = pauseButton.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = pauseButton.GetComponent<UnityEngine.UI.Image>();
            button.onClick.AddListener(() => session.TogglePause());
            button.targetGraphic.raycastTarget = true;
            guide = PaperAt(hud, "Chef's note", 0, 0, 330, 145, Paper, true);
            Place(guide, 1, 1, -24, -95, 330, 145, new Vector2(1, 1));
            TapeTab(guide, 330);
            guideText = Text(guide, "", 20, 20, 290, 110, 25, Ink);
        }

        // Create one reusable paper slip. Its slot badge stays fixed while RefreshHUD changes
        // the guest, food, doneness and patience as orders arrive and leave.
        TicketView BuildTicket(RectTransform parent, int slot)
        {
            var root = PaperAt(parent, "Order " + (slot + 1), slot * 228, 0, 214, 225, Paper, true);
            root.localRotation = Quaternion.Euler(0, 0, slot == 1 ? -1 : 1);
            TapeTab(root, 214);
            var ticketView = new TicketView
            {
                Root = root
            };
            var badge = PaperAt(root, "Counter number", 10, 12, 35, 35, TomatoWash);
            Text(badge, (slot + 1).ToString(), 0, 0, 35, 35, 24, Ink, true, TextAlignmentOptions.Center);
            ticketView.Customer = Text(root, "Vampire", 52, 16, 150, 28, 22, Ink);
            ticketView.Glyph = Glyph(root, 38, 48, 140, 91, 0, false);
            ticketView.Food = Text(root, "Patty", 10, 132, 194, 33, 27, Ink, true, TextAlignmentOptions.Center);
            var state = PaperAt(root, "Doneness", 17, 170, 120, 30, SageWash);
            ticketView.StatePaper = state.GetComponent<UnityEngine.UI.Image>();
            ticketView.State = Text(state, "Cooked", 0, 0, 120, 30, 22, Ink, false, TextAlignmentOptions.Center);
            ticketView.Time = Text(root, "80s", 140, 171, 58, 27, 20, Muted, false, TextAlignmentOptions.Right);
            var track = Solid(root, "Patience track", 17, 207, 180, 5, Line);
            ticketView.Bar = Solid(track, "Patience", 0, 0, 180, 5, Sage).GetComponent<UnityEngine.UI.Image>();
            ticketView.Progress = ticketView.Bar.rectTransform;
            Stretch(ticketView.Progress, Vector2.zero, Vector2.one);
            return ticketView;
        }

        // The pause sheet sends Resume, Restart and Main menu choices to GameSession.
        // Controls and Sound only open another UI page; they do not unpause the kitchen.
        void BuildPause()
        {
            pause = View("Pause");
            var sheet = Sheet(pause, "Pause menu", 650, 830);
            Text(sheet, "Paused", 35, 57, 580, 90, 62, Ink, true, TextAlignmentOptions.Center);
            Rule(sheet, 90, 178, 470);
            Choice(sheet, pause, "Resume", 70, 235, 510, () => session.TogglePause(), 39);
            Choice(sheet, pause, "How to play", 70, 325, 510, () => OpenPage("Controls"), 39);
            Choice(sheet, pause, "Sound", 70, 415, 510, () => OpenPage("Audio"), 39);
            Choice(sheet, pause, "Restart", 70, 505, 510, () => session.RestartShift(), 39);
            Choice(sheet, pause, "Main menu", 70, 595, 510, () => session.ReturnToTitle(), 39);
            Rule(sheet, 90, 708, 470);
            pauseStats = Text(sheet, "", 40, 737, 570, 40, 24, Muted, false, TextAlignmentOptions.Center);
        }

        // Keep the controls and cooking rules available from Pause without restarting the game.
        void BuildControls()
        {
            controls = View("Controls");
            var sheet = Sheet(controls, "House rules", 850, 830);
            Text(sheet, "How to play", 40, 48, 770, 80, 53, Ink, true, TextAlignmentOptions.Center);
            Control(sheet, 177, "WASD / arrows", "Move");
            Control(sheet, 249, "E / Space", "Take, place or serve");
            Control(sheet, 321, "Esc", "Pause / go back");
            Control(sheet, 393, "M / - / +", "Mute / change volume");
            Rule(sheet, 55, 480, 740);
            Text(sheet, "Raw  >  Cooked  >  Charred  >  Ash", 45, 510, 760, 45, 28, Tomato, true, TextAlignmentOptions.Center);
            Text(sheet, "Pick food up to stop cooking.\nVampires want cooked food. Tieflings want charred.\nAsh goes in the bin.", 65, 578, 720, 110, 27, Ink, false, TextAlignmentOptions.Center);
            Choice(sheet, controls, "Back", 100, 735, 650, ClosePage);
        }

        // Draw a key label beside a short explanation of its action.
        void Control(RectTransform sheet, float y, string key, string meaning)
        {
            var cap = PaperAt(sheet, key, 55, y, 270, 54, SageWash);
            Text(cap, key, 0, 0, 270, 54, 26, Ink, false, TextAlignmentOptions.Center);
            Text(sheet, meaning, 355, y, 450, 54, 29, Ink);
        }

        // Build a master-volume slider and mute/back choices. The fill stretches inside its track,
        // and the handle travels inside an inset area so neither can spill outside the sheet.
        void BuildAudio()
        {
            audioPage = View("Audio");
            var sheet = Sheet(audioPage, "Sound menu", 720, 600);
            Text(sheet, "Sound", 40, 48, 640, 80, 55, Ink, true, TextAlignmentOptions.Center);
            volumeText = Text(sheet, "Volume: 75%", 40, 184, 640, 45, 32, Ink, false, TextAlignmentOptions.Center);
            var sliderRoot = new GameObject("Volume", typeof(RectTransform), typeof(UnityEngine.UI.Slider)).GetComponent<RectTransform>();
            sliderRoot.SetParent(sheet, false);
            Place(sliderRoot, 0, 1, 70, -270, 580, 46, new Vector2(0, 1));
            volumeSlider = sliderRoot.GetComponent<UnityEngine.UI.Slider>();
            volumeSlider.minValue = 0;
            volumeSlider.maxValue = 1;
            volumeSlider.navigation = new UnityEngine.UI.Navigation
            {
                mode = UnityEngine.UI.Navigation.Mode.None
            };
            var track = PaperAt(sliderRoot, "Volume track", 0, 17, 580, 12, Line);
            var fill = PaperAt(track, "Volume fill", 0, 0, 580, 12, Sage);
            Stretch(fill, Vector2.zero, Vector2.one);
            var handleArea = new GameObject("Handle travel", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(sliderRoot, false);
            Stretch(handleArea, Vector2.zero, Vector2.one);
            handleArea.offsetMin = new Vector2(16, 0);
            handleArea.offsetMax = new Vector2(-16, 0);
            var knob = PaperAt(handleArea, "Wooden knob", 0, 0, 32, 46, TomatoWash);
            Place(knob, .5f, .5f, 0, 0, 32, 0, new Vector2(.5f, .5f));
            knob.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            track.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            volumeSlider.fillRect = fill;
            volumeSlider.handleRect = knob;
            volumeSlider.targetGraphic = knob.GetComponent<UnityEngine.UI.Image>();
            volumeSlider.onValueChanged.AddListener(v =>
            {
                if (!syncSlider)
                    SetVolume(v);
            });
            Choice(sheet, audioPage, "Mute / unmute", 70, 375, 580, ToggleMute);
            Choice(sheet, audioPage, "Back", 70, 475, 580, ClosePage);
        }

        // Lay out the receipt over a plate and gingham cloth. RefreshScreen fills its totals from
        // the completed session. The result stays here until the player chooses another action.
        void BuildResults()
        {
            results = View("Results");
            // An original gingham placemat and bill-on-a-plate composition.
            for (int x = 0; x < 16; x++)
                for (int y = 0; y < 10; y++)
                {
                    Color cloth = x % 2 == 0 && y % 2 == 0 ? Hex("DDE5D5") : x % 2 != 0 && y % 2 != 0 ? Hex("82A39C") : Hex("B9CDC2");
                    var tile = Solid(results, "Gingham", 0, 0, 0, 0, cloth);
                    Stretch(tile, new Vector2(x / 16f, y / 10f), new Vector2((x + 1) / 16f, (y + 1) / 10f));
                }

            var plate = PaperAt(results, "Dinner plate", 0, 0, 960, 960, Hex("D9DCCD"));
            Place(plate, .5f, .5f, 0, 0, 960, 960, new Vector2(.5f, .5f));
            plate.GetComponent<KitchenPaperImage>().Corner = 480;
            var inside = PaperAt(plate, "Plate centre", 50, 50, 860, 860, Paper);
            inside.GetComponent<KitchenPaperImage>().Corner = 430;
            Cutlery(results, -510, false);
            Cutlery(results, 510, true);
            var receipt = PaperAt(results, "Shift receipt", 0, 0, 680, 914, Paper, true);
            Place(receipt, .5f, .5f, 0, 0, 680, 914, new Vector2(.5f, .5f));
            resultTitle = Text(receipt, "Shift complete", 30, 50, 620, 80, 49, Ink, true, TextAlignmentOptions.Center);
            resultTime = Text(receipt, "", 40, 145, 600, 38, 25, Muted, false, TextAlignmentOptions.Center);
            Rule(receipt, 60, 214, 560);
            Text(receipt, "Guests served", 60, 253, 370, 46, 30, Ink);
            resultServed = Text(receipt, "", 450, 253, 170, 46, 32, Sage, true, TextAlignmentOptions.Right);
            Text(receipt, "Left hungry", 60, 326, 370, 46, 30, Ink);
            resultComplaints = Text(receipt, "", 450, 326, 170, 46, 32, Tomato, true, TextAlignmentOptions.Right);
            Text(receipt, "Plates discarded", 60, 399, 370, 46, 30, Ink);
            resultWaste = Text(receipt, "", 450, 399, 170, 46, 32, Ink, true, TextAlignmentOptions.Right);
            Rule(receipt, 60, 484, 560);
            Text(receipt, "TOTAL", 60, 522, 270, 60, 34, Ink, true);
            resultScore = Text(receipt, "", 330, 508, 290, 84, 64, Ink, true, TextAlignmentOptions.Right);
            Rule(receipt, 60, 634, 560);
            Choice(receipt, results, "Another shift", 80, 692, 520, () => session.RestartShift());
            Choice(receipt, results, "Main menu", 80, 788, 520, () => session.ReturnToTitle());
        }

        // Simple rectangles make the fork or knife beside the results plate.
        void Cutlery(RectTransform parent, float x, bool knife)
        {
            var holder = new GameObject(knife ? "Knife" : "Fork", typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(parent, false);
            Place(holder, .5f, .5f, x, 0, 75, 660, new Vector2(.5f, .5f));
            holder.localRotation = Quaternion.Euler(0, 0, knife ? -8 : 8);
            var metal = Hex("DFE3D6");
            PaperAt(holder, "Handle", 25, 270, 26, 390, metal);
            if (knife)
                PaperAt(holder, "Blade", 9, 0, 43, 320, metal);
            else
            {
                PaperAt(holder, "Fork shoulder", 0, 155, 75, 135, metal);
                for (int i = 0; i < 4; i++)
                    PaperAt(holder, "Tine", i * 21, 0, 12, 185, metal);
            }
        }

        // Reserve one small paper note for short messages such as a wrong order or a burnt portion.
        void BuildToast()
        {
            toast = PaperAt((RectTransform)canvas.transform, "Order note", 0, 0, 380, 145, Paper, true);
            Place(toast, 1, 1, -24, -95, 380, 145, new Vector2(1, 1));
            TapeTab(toast, 380);
            toastText = Text(toast, "", 20, 20, 340, 108, 25, Ink);
            toast.gameObject.SetActive(false);
        }

        // Replace the note's text and restart its display timer. Gameplay decides what the message says;
        // this presenter only decides how it looks and how long it remains visible.
        public void ShowMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;
            toastText.text = message;
            toastUntil = Time.unscaledTime + 3.8f;
        }

        // Hide the previous view, then choose a view explicitly from the game state.
        // Title is deliberately not a briefing fallback: the cookbook scene handles that screen.
        void RefreshScreen()
        {
            refresh = false;
            foreach (var view in views)
                view.gameObject.SetActive(false);
            // Title belongs to CookbookMenu. It must never fall back to Briefing.
            canvas.enabled = session.State != RunState.Title;
            RectTransform active = null;
            if (session.State != RunState.Title)
            {
                if (subpage == "Controls")
                    active = controls;
                else if (subpage == "Audio")
                    active = audioPage;
                else
                    switch (session.State)
                    {
                        case RunState.Briefing:
                            active = briefing;
                            break;
                        case RunState.Playing:
                            active = hud;
                            break;
                        case RunState.Paused:
                            active = pause;
                            break;
                        case RunState.Won:
                        case RunState.Lost:
                            active = results;
                            break;
                    }
            }

            if (active == null)
            {
                activeMenu = null;
                return;
            }

            active.gameObject.SetActive(true);
            menus.TryGetValue(active.gameObject, out activeMenu);
            selected = activeMenu == null ? 0 : Mathf.Clamp(selected, 0, activeMenu.Count - 1);
            RefreshSelection();
            RefreshAudio();
            if (!session)
                return;
            pauseStats.text = session.Served + " / 8 served     " + session.Complaints + " / 3 left hungry";
            resultTitle.text = session.State == RunState.Won ? "Shift complete" : "Kitchen closed";
            resultTitle.color = session.State == RunState.Won ? Ink : Tomato;
            resultServed.text = session.Served + " / 8";
            resultComplaints.text = session.Complaints.ToString();
            resultWaste.text = session.Wasted.ToString();
            resultScore.text = session.Score.ToString();
            int seconds = Mathf.FloorToInt(session.Elapsed);
            resultTime.text = "Service time  " + seconds / 60 + ":" + (seconds % 60).ToString("00");
        }

        // Read the active orders and the chef's held portion without changing any game rules.
        // A guest still walking in shows dots instead of a running patience countdown.
        void RefreshHUD()
        {
            served.text = session.Served + " / 8";
            complaints.text = session.Complaints + " / 3 left hungry";
            score.text = session.Score.ToString();
            Array.Clear(orders, 0, orders.Length);
            foreach (var order in session.Orders)
                if (!order.Resolved && order.Slot >= 0 && order.Slot < 3)
                    orders[order.Slot] = order;
            for (int i = 0; i < 3; i++)
            {
                var ticketView = tickets[i];
                var order = orders[i];
                ticketView.Root.gameObject.SetActive(order != null);
                if (order == null)
                    continue;
                bool charred = order.Required == HeatState.Charred;
                ticketView.Customer.text = order.CustomerName;
                ticketView.Food.text = KitchenRules.FoodName(order.Food);
                ticketView.State.text = KitchenRules.StateName(order.Required);
                ticketView.StatePaper.color = charred ? TomatoWash : SageWash;
                ticketView.Time.text = order.AtCounter ? Mathf.CeilToInt(order.Remaining) + "s" : "...";
                ticketView.Progress.anchorMax = new Vector2(order.Patience01, 1);
                ticketView.Bar.color = order.Patience01 < .25f ? Tomato : Sage;
                ticketView.Glyph.SetFood(order.Food == FoodKind.Ham ? 1 : 0, charred, false);
            }

            var chef = PlayerChef.Instance;
            var food = chef ? chef.Held : null;
            prompt.text = chef && !string.IsNullOrEmpty(chef.CurrentPrompt) ? chef.CurrentPrompt.Replace("[E] ", "") : "Find a station";
            carry.gameObject.SetActive(food);
            if (food)
            {
                heldFood.text = KitchenRules.FoodName(food.Kind);
                heldState.text = KitchenRules.StateName(food.Heat);
                heldState.color = food.Heat == HeatState.Charred || food.Heat == HeatState.Ash ? Tomato : Sage;
                heldGlyph.SetFood(food.Kind == FoodKind.Ham ? 1 : 0, food.Heat == HeatState.Charred, food.Heat == HeatState.Ash);
            }

            guide.gameObject.SetActive(session.Served < 2 && Time.unscaledTime >= toastUntil);
            if (session.Served == 0)
            {
                FoodItem heating = null;
                foreach (var s in KitchenStation.All)
                    if (s.Type == StationType.Hotplate && s.Item)
                    {
                        heating = s.Item;
                        break;
                    }

                var current = food ? food : heating;
                // Check mistakes first, then guide the next step of this first order.
                if (current && current.Heat == HeatState.Ash)
                    guideText.text = "That one is ash. Bin it and take a fresh patty.";
                else if (current && current.Heat == HeatState.Charred)
                    guideText.text = "Too charred for this vampire. Take a fresh patty.";
                else if (food)
                    guideText.text = food.Heat == HeatState.Raw
                        ? "Place the patty on a hotplate."
                        : "Match your plate to a guest's order slip.";
                else if (heating)
                    guideText.text = heating.Heat == HeatState.Raw
                        ? "Let it cook. Pick it up when it says Cooked."
                        : "Your patty is ready to collect.";
                else
                    guideText.text = "First order: take a patty from the crate.";
            }
            else
                guideText.text = "The tiefling likes it charred. Leave the patty heating a little longer.";
        }

        // Escape pauses during play. Menus wait for fresh input before allowing navigation or confirmation.
        // This prevents the last E/Space serving press from immediately selecting Another shift.
        void HandleKeyboard()
        {
            if (session.State == RunState.Title)
                return;
            var keys = Keyboard.current;
            if (keys == null)
                return;
            if (keys.mKey.wasPressedThisFrame)
                ToggleMute();
            if (keys.minusKey.wasPressedThisFrame)
                SetVolume(volume - .1f);
            if (keys.equalsKey.wasPressedThisFrame)
                SetVolume(volume + .1f);
            if (!session.IsRunning && !menuInput.Ready)
                return;
            if (keys.escapeKey.wasPressedThisFrame)
            {
                AudioFeedback.Play("back");
                if (subpage != "")
                    ClosePage();
                else if (session.IsRunning || session.State == RunState.Paused)
                    session.TogglePause();
                else
                    session.ReturnToTitle();
                return;
            }

            if (session.IsRunning || activeMenu == null || activeMenu.Count == 0)
                return;
            int direction = keys.downArrowKey.isPressed || keys.sKey.isPressed ? 1 : keys.upArrowKey.isPressed || keys.wKey.isPressed ? -1 : 0;
            if (direction != 0 && (direction != lastDirection || Time.unscaledTime >= nextNav))
            {
                selected = (selected + direction + activeMenu.Count) % activeMenu.Count;
                nextNav = Time.unscaledTime + (lastDirection != direction ? .4f : .17f);
                RefreshSelection();
                AudioFeedback.Play("click");
            }

            lastDirection = direction;
            if (keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame || keys.eKey.wasPressedThisFrame)
            {
                AudioFeedback.Play("confirm");
                activeMenu[selected].Action();
            }
        }

        // Start a subpage at its first choice and wait for the opening key or click to finish.
        void OpenPage(string name)
        {
            subpage = name;
            selected = 0;
            refresh = true;
            menuInput.Reset();
        }

        // Return to the parent menu without changing the paused shift.
        void ClosePage()
        {
            subpage = "";
            selected = 0;
            refresh = true;
            menuInput.Reset();
        }

        // Clamp the new level to 0-100%. Raising it also clears mute.
        void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
            if (volume > 0)
                muted = false;
            SaveVolume();
        }

        // Mute without forgetting the volume the player chose.
        void ToggleMute()
        {
            muted = !muted;
            SaveVolume();
        }

        // Apply the setting to all audio and save it for the cookbook and future launches.
        void SaveVolume()
        {
            AudioListener.volume = muted ? 0 : volume;
            PlayerPrefs.SetFloat(VolumeKey, volume);
            PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            RefreshAudio();
        }

        // Move the slider to the stored value without firing its value-changed callback again.
        void RefreshAudio()
        {
            volumeText.text = muted ? "Muted" : "Volume: " + Mathf.RoundToInt(volume * 100) + "%";
            syncSlider = true;
            volumeSlider.SetValueWithoutNotify(volume);
            syncSlider = false;
        }

        // Make a full-Canvas container for one screen. Its children use their own corner or center anchors.
        RectTransform View(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            Stretch(rt, Vector2.zero, Vector2.one);
            views.Add(rt);
            return rt;
        }

        // Dim the kitchen behind a centered paper menu while keeping the menu itself bright.
        RectTransform Sheet(RectTransform parent, string name, float w, float h)
        {
            var shade = Solid(parent, "Dimmed kitchen", 0, 0, 0, 0, new Color(.12f, .09f, .07f, .48f));
            Stretch(shade, Vector2.zero, Vector2.one);
            shade.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var sheet = PaperAt(parent, name, 0, 0, w, h, Paper, true);
            Place(sheet, .5f, .5f, 0, 0, w, h, new Vector2(.5f, .5f));
            TapeTab(sheet, w);
            return sheet;
        }

        // Add the small tilted piece of tape that visually holds a note or menu in place.
        void TapeTab(RectTransform parent, float width)
        {
            var tape = PaperAt(parent, "Paper clip", width * .5f - 38, -7, 76, 20, Tape);
            tape.localRotation = Quaternion.Euler(0, 0, -3);
        }

        // Create one clickable option and add it to that page's keyboard navigation list.
        // Both paths use the same action, and pointer clicks respect the same input guard as keys.
        void Choice(RectTransform parent, RectTransform menu, string caption, float x, float y, float width, Action action, int size = 34)
        {
            if (!menus.TryGetValue(menu.gameObject, out var list))
            {
                list = new List<MenuEntry>();
                menus[menu.gameObject] = list;
            }

            int index = list.Count;
            var bg = PaperAt(parent, caption, x, y, width, 64, Color.clear);
            bg.GetComponent<KitchenPaperImage>().Corner = 27;
            var image = bg.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = true;
            var button = bg.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            button.navigation = new UnityEngine.UI.Navigation
            {
                mode = UnityEngine.UI.Navigation.Mode.None
            };
            var entry = new MenuEntry
            {
                Background = image,
                Label = Text(bg, caption, 12, 0, width - 24, 64, size, Ink, false, TextAlignmentOptions.Center),
                Action = action
            };
            list.Add(entry);
            button.onClick.AddListener(() =>
            {
                if (activeMenu != list || !menuInput.Ready)
                    return;
                selected = index;
                AudioFeedback.Play("confirm");
                action();
            });
            var trigger = bg.gameObject.AddComponent<EventTrigger>();
            var hover = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            hover.callback.AddListener(_ =>
            {
                if (activeMenu == list && Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > .01f)
                {
                    selected = index;
                    RefreshSelection();
                }
            });
            trigger.triggers.Add(hover);
        }

        // Highlight one option using the page's warm accent color.
        void RefreshSelection()
        {
            if (activeMenu == null)
                return;
            for (int i = 0; i < activeMenu.Count; i++)
            {
                activeMenu[i].Background.color = i == selected ? TomatoWash : Color.clear;
                activeMenu[i].Label.color = i == selected ? Tomato : Ink;
            }
        }

        // Create a TMP label using the bundled font. Text does not catch clicks meant for its button.
        TMP_Text Text(RectTransform parent, string value, float x, float y, float w, float h, int size, Color color, bool bold = false, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var go = new GameObject(value.Length > 30 ? value.Substring(0, 30) : value, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.text = value;
            t.color = color;
            t.alignment = alignment;
            t.verticalAlignment = VerticalAlignmentOptions.Middle;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = false;
            Place(t.rectTransform, 0, 1, x, -y, w, h, new Vector2(0, 1));
            return t;
        }

        // Create a paper rectangle with optional torn edges and a small shadow.
        static RectTransform PaperAt(RectTransform parent, string name, float x, float y, float w, float h, Color color, bool torn = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(KitchenPaperImage));
            go.transform.SetParent(parent, false);
            var p = go.GetComponent<KitchenPaperImage>();
            p.color = color;
            p.Torn = torn;
            p.raycastTarget = false;
            if (color.a > .5f && color == Paper)
            {
                var shadow = go.AddComponent<UnityEngine.UI.Shadow>();
                shadow.effectColor = new Color(.15f, .09f, .04f, .25f);
                shadow.effectDistance = new Vector2(4, -6);
            }

            var r = (RectTransform)go.transform;
            Place(r, 0, 1, x, -y, w, h, new Vector2(0, 1));
            return r;
        }

        // A plain filled rectangle for lines, patience bars and the tablecloth.
        static RectTransform Solid(RectTransform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<UnityEngine.UI.Image>().color = color;
            go.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var r = (RectTransform)go.transform;
            Place(r, 0, 1, x, -y, w, h, new Vector2(0, 1));
            return r;
        }

        // Draw a thin printed divider across a menu or receipt.
        static void Rule(RectTransform p, float x, float y, float w) => Solid(p, "Printed rule", x, y, w, 2, Line);
        // Place one of the plate illustrations inside a ticket or held-food display.
        static KitchenFoodGlyph Glyph(RectTransform parent, float x, float y, float w, float h, int food, bool charred)
        {
            var go = new GameObject("Plate illustration", typeof(RectTransform), typeof(CanvasRenderer), typeof(KitchenFoodGlyph));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, 0, 1, x, -y, w, h, new Vector2(0, 1));
            var glyph = go.GetComponent<KitchenFoodGlyph>();
            glyph.raycastTarget = false;
            glyph.SetFood(food, charred, false);
            return glyph;
        }

        // Anchor a UI rectangle at one point, then set its position, size and pivot.
        // For top-aligned elements, the callers pass negative Y to move down the screen.
        static void Place(RectTransform r, float ax, float ay, float x, float y, float w, float h, Vector2 pivot)
        {
            r.anchorMin = r.anchorMax = new Vector2(ax, ay);
            r.pivot = pivot;
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
        }

        // Fill the area between two anchor points. Zero offsets keep the rectangle against those edges.
        static void Stretch(RectTransform r, Vector2 min, Vector2 max)
        {
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        // Convert a six-digit color string into a Unity Color.
        static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var c);
            return c;
        }
    }
}
