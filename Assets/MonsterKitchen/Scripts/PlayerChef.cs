using UnityEngine;
using UnityEngine.InputSystem;

namespace MonsterKitchen
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerChef : MonoBehaviour
    {
        public static PlayerChef Instance { get; private set; }
        public Transform CarryAnchor;
        public Transform VisualRoot;
        public Animator Animator;
        public float MoveSpeed = 4.6f;
        public FoodItem Held { get; private set; }
        public KitchenStation Target { get; private set; }
        public string CurrentPrompt => Target ? Target.Prompt(this) : "Find a station.  WASD / ARROWS to move";
        public int Transfers { get; private set; }
        CharacterController controller;
        Vector3 initialPosition;
        Quaternion initialRotation;
        float interactCooldown;
        Vector3 velocity;

        void Awake()
        {
            Instance = this;
            controller = GetComponent<CharacterController>();
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        void Update()
        {
            var session = GameSession.Instance;
            bool playing = session && session.IsRunning;
            if (Animator) Animator.speed = session && session.State == RunState.Paused ? 0 : 1;
            if (!playing)
            {
                if (Animator) Animator.SetFloat("Speed", 0);
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

        // Movement follows the camera's floor-plane axes, so W always moves toward the screen's top.
        public void Move(Vector2 input, float delta)
        {
            input = Vector2.ClampMagnitude(input, 1);
            Camera camera = Camera.main;
            Vector3 forward = camera ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = camera ? Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized : Vector3.right;
            Vector3 direction = forward * input.y + right * input.x;
            velocity = Vector3.MoveTowards(velocity, direction * MoveSpeed, delta * 30);
            controller.Move((velocity + Vector3.down * 5) * delta);
            if (direction.sqrMagnitude > .01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), delta * 16);
            if (Animator)
            {
                Animator.SetFloat("Speed", velocity.magnitude / MoveSpeed, .08f, delta);
                Animator.SetBool("Carrying", Held != null);
            }
        }

        void SelectTarget()
        {
            KitchenStation best = null;
            float bestScore = float.MaxValue;
            foreach (KitchenStation station in KitchenStation.All)
            {
                if (!station || !station.isActiveAndEnabled || !station.PlayerInside) continue;
                Vector3 difference = station.ReachPoint.position - transform.position;
                difference.y = 0;
                float distance = difference.magnitude;
                if (distance > station.ReachRadius) continue;
                // Facing is a preference, not a precision aiming requirement at a kitchen counter.
                float facing = Vector3.Dot(transform.forward, difference.normalized);
                float score = distance + (1 - facing) * .16f;
                if (station == Target) score -= .12f;
                if (score < bestScore) { bestScore = score; best = station; }
            }
            SetTarget(best);
        }

        void SetTarget(KitchenStation station)
        {
            if (Target == station) return;
            if (Target) Target.SetHighlighted(false);
            Target = station;
            if (Target) Target.SetHighlighted(true);
        }

        public bool Interact()
        {
            if (!GameSession.Instance || !GameSession.Instance.IsRunning || !Target) return false;
            interactCooldown = .16f;
            bool result = Target.Interact(this);
            if (result && Animator) Animator.SetTrigger("Interact");
            return result;
        }

        // Stations validate capacity before calling these methods. Ownership changes once per press.
        public bool Take(FoodItem food)
        {
            if (Held || !food) return false;
            Held = food;
            food.MoveTo(CarryAnchor);
            Transfers++;
            return true;
        }

        public FoodItem Release()
        {
            FoodItem food = Held;
            Held = null;
            Transfers++;
            return food;
        }

        public void ResetChef()
        {
            if (Held) { Destroy(Held.gameObject); Held = null; }
            SetTarget(null);
            controller.enabled = false;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            controller.enabled = true;
            velocity = Vector3.zero;
            Transfers = 0;
            interactCooldown = .2f;
            if (Animator) { Animator.SetBool("Carrying", false); Animator.SetFloat("Speed", 0); }
        }

        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            velocity = Vector3.zero;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
