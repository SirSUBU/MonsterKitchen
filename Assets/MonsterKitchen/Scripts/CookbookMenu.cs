// The title menu is lettering placed on the 3D cookbook's pages.
// The book model stays in the scene; switching pages only changes the text and menu choices.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MonsterKitchen
{
    public sealed class CookbookMenu : MonoBehaviour
    {
        // Book is the physical prop. MenuCamera is the camera used to click its world-space buttons.
        public Transform Book;
        public Camera MenuCamera;
        // The catalog owns the level list. The book only presents it, so adding another
        // kitchen does not require another hard-coded menu option or a new menu script.
        public KitchenLevelCatalog Levels;
        const int LevelsPerPage = 3;
        int levelPage;
        // Buttons and labels use the same index. selected tracks the option highlighted by keyboard or mouse.
        readonly List<Button> buttons = new List<Button>();
        readonly List<Text> labels = new List<Text>();
        Font body, serif;
        RectTransform rightPage;
        Text volumeLabel;
        int selected;
        string page = "Menu";
        int heldNavigation;
        readonly MenuInputGate menuInput = new MenuInputGate();
        readonly Color ink = new Color(.25f, .17f, .13f), red = new Color(.62f, .22f, .14f);
        // Create a world-space Canvas just above the pages, then fill the first menu.
        // The small Canvas scale converts its pixel-like text positions into the book's world dimensions.
        void Start()
        {
            body = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            serif = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman", "Arial" }, 48);
            var go = new GameObject("Cookbook page lettering", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            go.transform.SetParent(Book, false);
            go.transform.localPosition = new Vector3(0, .52f, 0);
            go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            go.transform.localScale = Vector3.one * .006f;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = MenuCamera;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(1660, 1080);
            TextAt(rect, "Monster\nKitchen", new Vector2(-425, 40), new Vector2(730, 310), 120, true, FontStyle.Bold);
            var right = new GameObject("Right page", typeof(RectTransform));
            right.transform.SetParent(rect, false);
            rightPage = (RectTransform)right.transform;
            rightPage.sizeDelta = new Vector2(760, 950);
            rightPage.anchoredPosition = new Vector2(430, 0);
            if (EventSystem.current == null)
            {
                var eventSystem = new GameObject("Book pointer input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem.GetComponent<EventSystem>().sendNavigationEvents = false;
                eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            AudioListener.volume = PlayerPrefs.GetInt("MonsterKitchen.Muted", 0) != 0 ? 0 : PlayerPrefs.GetFloat("MonsterKitchen.MasterVolume", .75f);
            Show("Menu");
        }

        // Remove the previous page's options and build the requested page in their place.
        // Wait for a fresh input so the key that opened this page cannot immediately choose its first item.
        void Show(string next)
        {
            menuInput.Reset();
            page = next;
            foreach (Transform child in rightPage)
            {
                // Destroy waits until the end of this frame. Hide the old row now so it
                // cannot still receive a click while the next page's rows are being made.
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            buttons.Clear();
            labels.Clear();
            selected = 0;
            if (page == "Menu")
            {
                AddOption("Start cooking", 220, () =>
                {
                    levelPage = 0;
                    Show("Levels");
                });
                AddOption("How to play", 110, () => Show("Help"));
                AddOption("Sound", 0, () => Show("Sound"));
                AddOption("Credits", -110, () => Show("Credits"));
                AddOption("Close the kitchen", -220, Quit);
            }
            else if (page == "Levels")
                ShowLevels();
            else if (page == "Help")
            {
                TextAt(rightPage, "The house rules", new Vector2(0, 390), new Vector2(700, 75), 50, true, FontStyle.Italic);
                TextAt(rightPage, "WASD / arrows : move\nE / Space : take, place, serve\nEsc : pause", new Vector2(0, 240), new Vector2(700, 145), 33, false);
                TextAt(rightPage, "Feed eight guests before\nthree leave unhappy.\n\nCharred food pleases a tiefling.\nVampires want theirs cooked.\n\nPick up food to stop heating.\nAsh belongs in the bin.", new Vector2(0, -35), new Vector2(700, 350), 33, true);
                AddOption("Back to the menu", -340, () => Show("Menu"));
            }
            else if (page == "Sound")
            {
                TextAt(rightPage, "Kitchen sounds", new Vector2(0, 390), new Vector2(700, 75), 50, true, FontStyle.Italic);
                volumeLabel = TextAt(rightPage, "", new Vector2(0, 240), new Vector2(700, 75), 38, true);
                AddOption("A little louder  +", 110, () => Volume(.1f));
                AddOption("A little quieter  -", 10, () => Volume(-.1f));
                AddOption("Mute / unmute", -90, ToggleMute);
                AddOption("Back to the menu", -270, () => Show("Menu"));
                UpdateVolume();
            }
            else if (page == "MusicCredits")
            {
                // Keep these as plain credits, with one way back to the book.
                // Full source and license details stay in the credit file included with the game.
                TextAt(rightPage, "Music credits", new Vector2(0, 390), new Vector2(700, 75), 50, true, FontStyle.Italic);
                TextAt(rightPage, "Lily-Paddling Down the Stream", new Vector2(0, 235), new Vector2(700, 85), 34, true);
                TextAt(rightPage, "Fablefly Music", new Vector2(0, 155), new Vector2(700, 65), 40, true);
                TextAt(rightPage, "Jazzy Shop / Quircky Shop", new Vector2(0, -5), new Vector2(700, 75), 34, true);
                TextAt(rightPage, "Tim Reichert", new Vector2(0, -85), new Vector2(700, 65), 40, true);
                AddOption("Back to credits", -340, () => Show("Credits"), 46, 76);
            }
            else
            {
                TextAt(rightPage, "Made with", new Vector2(0, 390), new Vector2(700, 75), 50, true, FontStyle.Italic);
                TextAt(rightPage, "Game design: Saubdiel Avalos\n\nArt & animation: Kay Lousberg / KayKit\n\nCafe sounds: Bramble & Byte\n\nUI font: Fredoka Project Authors\n\nMenu inspiration: Undercooked\nUI references: Overcooked / Nielisson", new Vector2(0, 90), new Vector2(700, 440), 30, true);
                AddOption("Music credits", -225, () => Show("MusicCredits"), 44, 76);
                AddOption("Back to the menu", -340, () => Show("Menu"));
            }

            RefreshSelection();
        }

        // Keep the first release to one real kitchen. More published assets fill the page,
        // then get Previous/Next choices automatically; there are no pretend locked levels.
        void ShowLevels()
        {
            var entries = Levels ? Levels.PublishedLevels() : new List<KitchenLevel>();
            int pageCount = Mathf.Max(1, (entries.Count + LevelsPerPage - 1) / LevelsPerPage);
            levelPage = Mathf.Clamp(levelPage, 0, pageCount - 1);
            TextAt(rightPage, "Choose a kitchen", new Vector2(0, 390), new Vector2(700, 75), 50, true, FontStyle.Italic);
            if (pageCount > 1)
                TextAt(rightPage, "Page " + (levelPage + 1) + " of " + pageCount, new Vector2(0, 325), new Vector2(700, 40), 26, true);
            if (entries.Count == 0)
                TextAt(rightPage, "The kitchen is closed for now.", new Vector2(0, 90), new Vector2(700, 100), 36, true);

            int first = levelPage * LevelsPerPage;
            int count = Mathf.Min(LevelsPerPage, entries.Count - first);
            float top = count == 1 ? 155 : count == 2 ? 210 : 240;
            for (int row = 0; row < count; row++)
            {
                var level = entries[first + row];
                float y = top - row * 175;
                string title = (first + row + 1) + ". " + level.DisplayName;
                if (level.TryGetSceneIndex(out _))
                    AddOption(title, y, () => GameSession.Instance.BeginBriefing(level), 44, 80, playConfirm: false);
                else
                    TextAt(rightPage, title + " (closed)", new Vector2(0, y), new Vector2(700, 80), 36, true);
                var description = TextAt(rightPage, level.Description ?? "", new Vector2(0, y - 70), new Vector2(700, 65), 29, true);
                // A future description should stay inside its own row, even if it is too long.
                description.verticalOverflow = VerticalWrapMode.Truncate;
            }

            if (levelPage > 0)
                AddOption("Previous page", -270, () => ChangeLevelPage(-1), 32, 55);
            if (levelPage + 1 < pageCount)
                AddOption("Next page", -330, () => ChangeLevelPage(1), 32, 55);
            AddOption("Back to the menu", pageCount > 1 ? -410 : -340, () => Show("Menu"), 44, 70);
        }

        // Rebuilding only the lettering also resets the input gate. Holding Enter on Next
        // cannot accidentally enter the first kitchen on the newly displayed page.
        void ChangeLevelPage(int direction)
        {
            levelPage += direction;
            Show("Levels");
        }

        // Create a clickable row and save it for keyboard navigation. Mouse and keyboard both
        // invoke the same button action, so they cannot drift into different menu behavior.
        // Entering a kitchen uses the door bell instead of the usual confirmation click.
        void AddOption(string text, float y, Action action, int fontSize = 52, float rowHeight = 90, bool playConfirm = true)
        {
            int index = buttons.Count;
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(rightPage, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(720, rowHeight);
            rect.anchoredPosition = new Vector2(0, y);
            var image = go.GetComponent<Image>();
            image.color = Color.clear;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (!menuInput.Ready)
                    return;
                if (playConfirm)
                    AudioFeedback.Play("confirm");
                action();
            });
            var label = TextAt(rect, text, Vector2.zero, new Vector2(700, rowHeight - 4), fontSize, true);
            labels.Add(label);
            buttons.Add(button);
            var eventTrigger = go.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            entry.callback.AddListener(_ =>
            {
                if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > .01f)
                {
                    selected = index;
                    RefreshSelection();
                }
            });
            eventTrigger.triggers.Add(entry);
        }

        // Place centered text on a page. Book text uses the serif font; control labels use the simpler font.
        Text TextAt(RectTransform parent, string value, Vector2 pos, Vector2 size, int fontSize, bool bookFont, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(value.Length > 30 ? value.Substring(0, 30) : value, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = bookFont ? serif : body;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = ink;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // Use the same color and bold highlight whether the choice came from keyboard movement or hovering.
        void RefreshSelection()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].GetComponent<Image>().color = i == selected ? new Color(.65f, .42f, .19f, .13f) : Color.clear;
                labels[i].color = i == selected ? red : ink;
                labels[i].fontStyle = i == selected ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        // Read menu keys only after the opening input has been released. W/S or arrows choose a row;
        // E, Space or Enter activates it. Escape leaves level select without loading a scene;
        // music credits first go back to the main credits, then to the main page.
        void Update()
        {
            menuInput.Update();
            if (!menuInput.Ready)
                return;
            var keyboard = Keyboard.current;
            if (keyboard == null || buttons.Count == 0)
                return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (page == "MusicCredits")
                    Show("Credits");
                else if (page != "Menu")
                    Show("Menu");
                return;
            }

            int direction = (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) ? 1 : (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) ? -1 : 0;
            if (direction != 0 && direction != heldNavigation)
            {
                selected = (selected + direction + buttons.Count) % buttons.Count;
                RefreshSelection();
                AudioFeedback.Play("click");
            }

            heldNavigation = direction;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
                buttons[selected].onClick.Invoke();
            if (keyboard.equalsKey.wasPressedThisFrame)
                Volume(.1f);
            if (keyboard.minusKey.wasPressedThisFrame)
                Volume(-.1f);
            if (keyboard.mKey.wasPressedThisFrame)
                ToggleMute();
        }

        // Save volume between runs and unmute when the player deliberately changes it.
        void Volume(float amount)
        {
            float v = Mathf.Clamp01(PlayerPrefs.GetFloat("MonsterKitchen.MasterVolume", .75f) + amount);
            PlayerPrefs.SetFloat("MonsterKitchen.MasterVolume", v);
            PlayerPrefs.SetInt("MonsterKitchen.Muted", 0);
            PlayerPrefs.Save();
            AudioListener.volume = v;
            UpdateVolume();
        }

        // Remember the chosen volume while muting, so unmute brings it back.
        void ToggleMute()
        {
            bool muted = PlayerPrefs.GetInt("MonsterKitchen.Muted", 0) == 0;
            PlayerPrefs.SetInt("MonsterKitchen.Muted", muted ? 1 : 0);
            PlayerPrefs.Save();
            AudioListener.volume = muted ? 0 : PlayerPrefs.GetFloat("MonsterKitchen.MasterVolume", .75f);
            UpdateVolume();
        }

        // Refresh the sound page's label when that page exists.
        void UpdateVolume()
        {
            if (volumeLabel)
                volumeLabel.text = AudioListener.volume <= 0 ? "Shh... muted." : "Volume : " + Mathf.RoundToInt(AudioListener.volume * 100) + "%";
        }

        // Stop Play mode while testing in Unity. In a built game, close the application instead.
        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
