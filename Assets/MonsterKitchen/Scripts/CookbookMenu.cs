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
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            buttons.Clear();
            labels.Clear();
            selected = 0;
            if (page == "Menu")
            {
                AddOption("Start cooking", 220, () => GameSession.Instance.BeginBriefing());
                AddOption("How to play", 110, () => Show("Help"));
                AddOption("Sound", 0, () => Show("Sound"));
                AddOption("Credits", -110, () => Show("Credits"));
                AddOption("Close the kitchen", -220, Quit);
            }
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
            else
            {
                TextAt(rightPage, "Made with", new Vector2(0, 390), new Vector2(700, 75), 50, true, FontStyle.Italic);
                TextAt(rightPage, "Game design: Saubdiel Avalos\n\nArt & animation: Kay Lousberg / KayKit\n\nCafe sounds: Bramble & Byte\n\nUI font: Fredoka Project Authors\n\nMenu inspiration: Undercooked\nUI references: Overcooked / Nielisson", new Vector2(0, 55), new Vector2(700, 500), 30, true);
                AddOption("Back to the menu", -340, () => Show("Menu"));
            }

            RefreshSelection();
        }

        // Create a clickable row and save it for keyboard navigation. Mouse and keyboard both
        // invoke the same button action, so they cannot drift into different menu behavior.
        void AddOption(string text, float y, Action action)
        {
            int index = buttons.Count;
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(rightPage, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(720, 90);
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
                AudioFeedback.Play("confirm");
                action();
            });
            var label = TextAt(rect, text, Vector2.zero, new Vector2(700, 86), 52, true);
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
        // E, Space or Enter activates it. Escape goes back to the main page.
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
                if (page != "Menu")
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
