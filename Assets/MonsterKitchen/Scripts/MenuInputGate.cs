// Stop an input from doing one action in gameplay and another action on the next menu.
// For example, the E press that serves guest eight must not also choose Another shift.
using UnityEngine;
using UnityEngine.InputSystem;

namespace MonsterKitchen
{
    // A screen can open while the player is still holding the key that got them there.
    // Wait for that input to finish before letting it choose anything on the new screen.
    // Both the cookbook and the kitchen menus use this same small helper.
    public sealed class MenuInputGate
    {
        // Once the old input is released, the menu stays ready until another screen opens.
        public bool Ready { get; private set; }

        int openedFrame;
        float readyAfter;
        // Call this when entering a screen or changing pages. Results also use a short reading delay.
        // Use unscaled time so this still works while the gameplay clock is paused.
        public void Reset(float readingDelay = 0f)
        {
            Ready = false;
            openedFrame = Time.frameCount;
            readyAfter = Time.unscaledTime + readingDelay;
        }

        // Do not accept input on the opening frame. After that, wait for any confirm, back,
        // navigation key or left mouse button to be released before allowing a new choice.
        public void Update()
        {
            if (Ready || Time.frameCount <= openedFrame || Time.unscaledTime < readyAfter)
                return;
            // E and Space also serve meals. W/S also move the chef. None of those
            // held inputs should accidentally confirm or move a new menu selection.
            var keys = Keyboard.current;
            bool held = keys != null && (keys.eKey.isPressed || keys.spaceKey.isPressed || keys.enterKey.isPressed || keys.escapeKey.isPressed || keys.wKey.isPressed || keys.sKey.isPressed || keys.upArrowKey.isPressed || keys.downArrowKey.isPressed);
            held |= Mouse.current != null && Mouse.current.leftButton.isPressed;
            if (!held)
                Ready = true;
        }
    }
}
