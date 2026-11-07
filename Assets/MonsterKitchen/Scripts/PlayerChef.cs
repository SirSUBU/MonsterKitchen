// Keyboard movement, station targeting and the dish in the chef's hands.
// KitchenStation handles the actual station action after this script chooses a target.
using UnityEngine;
using UnityEngine.InputSystem;

namespace MonsterKitchen
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerChef : MonoBehaviour
    {
        public static PlayerChef Instance { get; private set; }

        // The plate follows CarryAnchor. VisualRoot and Animator belong to the imported chef model.
        public Transform CarryAnchor;
        public Transform VisualRoot;
        public Animator Animator;
        // Walking speed in Unity units per second. The movement code also smooths starts and turns.
        public float MoveSpeed = 4.6f;
        public FoodItem Held { get; private set; }
        public KitchenStation Target { get; private set; }
        public string CurrentPrompt => Target ? Target.Prompt(this) : "Find a station.  WASD / ARROWS to move";
        public int Transfers { get; private set; }

        CharacterController controller;
        // Remember the scene's starting pose so Restart returns the chef to the same place.
        Vector3 initialPosition;
        Quaternion initialRotation;
        float interactCooldown;
        Vector3 velocity;
        int carryLayer = -1;
        const float CarryBlendSeconds = .12f;
        // Save the CharacterController and the starting pose once when the scene loads.
        void Awake()
        {
            Instance = this;
            controller = GetComponent<CharacterController>();
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            if (Animator)
                carryLayer = Animator.GetLayerIndex("Carry arms");
            RefreshCarryPose(true);
        }

        // Read movement every frame while the shift is running. E/Space use a fresh key press
        // and a short cooldown, so holding the key does not repeatedly move the same plate.
        void Update()
        {
            var session = GameSession.Instance;
            bool playing = session && session.IsRunning;
            bool paused = session && session.State == RunState.Paused;
            if (Animator)
            {
                Animator.speed = paused ? 0 : 1;
                // Keep the hands in sync on the result screen too. The last delivery can
                // finish the shift, which means the movement code below will no longer run.
                if (!paused)
                    RefreshCarryPose();
            }
            if (!playing)
            {
                if (Animator)
                    Animator.SetFloat("Speed", 0);
                SetTarget(null);
                return;
            }

            Keyboard keys = Keyboard.current;
            Vector2 move = Vector2.zero;
            if (keys != null)
            {
                move.x = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0);
                move.y = (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0) - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0);
            }

            Move(move, Time.deltaTime);
            SelectTarget();
            interactCooldown -= Time.deltaTime;
            if (keys != null && (keys.eKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame) && interactCooldown <= 0)
                Interact();
        }

        // Turn WASD into directions along the camera's view of the floor, then use CharacterController.Move.
        // Clamp diagonal input so moving in two directions is not faster. The small downward motion
        // keeps the controller grounded while solid walls and counters stop horizontal movement.
        public void Move(Vector2 input, float delta)
        {
            input = Vector2.ClampMagnitude(input, 1);
            // Remove the camera's vertical tilt. Movement should follow the floor,
            // not try to move the chef up into the camera's viewing direction.
            Camera camera = Camera.main;
            Vector3 forward = camera ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = camera ? Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized : Vector3.right;
            Vector3 direction = forward * input.y + right * input.x;
            velocity = Vector3.MoveTowards(velocity, direction * MoveSpeed, delta * 30);
            // Multiplying by the frame's seconds gives roughly the same travel
            // distance on a fast computer and a slower one.
            controller.Move((velocity + Vector3.down * 5) * delta);
            if (direction.sqrMagnitude > .01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), delta * 16);
            if (Animator)
                Animator.SetFloat("Speed", velocity.magnitude / MoveSpeed, .08f, delta);
        }

        // An empty animation state does not necessarily let go of the last pose it played.
        // Fade the arm layer to zero when the hands are empty, so idle/walk can move the arms
        // again. While carrying, this layer takes over just the arms; the legs still walk.
        // Reset snaps to empty hands immediately. Normal pickups and deliveries blend gently.
        void RefreshCarryPose(bool snap = false)
        {
            if (!Animator)
                return;
            bool carrying = Held != null;
            Animator.SetBool("Carrying", carrying);
            if (carryLayer < 0)
                return;
            float target = carrying ? 1f : 0f;
            float weight = snap ? target : Mathf.MoveTowards(
                Animator.GetLayerWeight(carryLayer), target, Time.deltaTime / CarryBlendSeconds);
            Animator.SetLayerWeight(carryLayer, weight);
        }

        // Consider only stations whose reach trigger contains the chef. Prefer the closest one
        // and slightly favor the current target so prompts do not flicker between neighboring counters.
        void SelectTarget()
        {
            KitchenStation best = null;
            float bestScore = float.MaxValue;
            foreach (KitchenStation station in KitchenStation.All)
            {
                if (!station || !station.isActiveAndEnabled || !station.PlayerInside)
                    continue;
                Vector3 difference = station.ReachPoint.position - transform.position;
                difference.y = 0;
                float distance = difference.magnitude;
                if (distance > station.ReachRadius)
                    continue;
                // Facing is a preference, not a precision aiming requirement at a kitchen counter.
                float facing = Vector3.Dot(transform.forward, difference.normalized);
                float score = distance + (1 - facing) * .16f;
                if (station == Target)
                    score -= .12f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = station;
                }
            }

            SetTarget(best);
        }

        // Switch the visible highlight along with the selected station.
        void SetTarget(KitchenStation station)
        {
            if (Target == station)
                return;
            if (Target)
                Target.SetHighlighted(false);
            Target = station;
            if (Target)
                Target.SetHighlighted(true);
        }

        // Ask the selected station to act. Play the short hand animation only if the action succeeds.
        public bool Interact()
        {
            if (!GameSession.Instance || !GameSession.Instance.IsRunning || !Target)
                return false;
            interactCooldown = .16f;
            bool result = Target.Interact(this);
            if (result && Animator)
                Animator.SetTrigger("Interact");
            return result;
        }

        // Accept one portion only when the hands are empty, then attach it to the carry anchor.
        public bool Take(FoodItem food)
        {
            if (Held || !food)
                return false;
            Held = food;
            food.MoveTo(CarryAnchor);
            Transfers++;
            return true;
        }

        // Give the current portion back to the station code and clear the hands first.
        // The caller decides whether to place, serve or discard that returned portion.
        public FoodItem Release()
        {
            FoodItem food = Held;
            Held = null;
            Transfers++;
            return food;
        }

        // Clear held food, targeting and movement before returning to the scene's starting pose.
        // Temporarily disable the CharacterController so it does not fight the position reset.
        public void ResetChef()
        {
            if (Held)
            {
                Destroy(Held.gameObject);
                Held = null;
            }

            SetTarget(null);
            controller.enabled = false;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            controller.enabled = true;
            velocity = Vector3.zero;
            Transfers = 0;
            interactCooldown = .2f;
            if (Animator)
            {
                RefreshCarryPose(true);
                Animator.SetFloat("Speed", 0);
            }
        }

        // A helper for the Editor checks to approach a station quickly. Normal movement uses Move.
        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            velocity = Vector3.zero;
        }

        // Clear the shared chef reference when this scene closes.
        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
