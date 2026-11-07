// Connect the order rules to visible customers and scene objects.
// GameSession announces an arrival or result; this script creates the guest or sends them home.
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen
{
    public sealed class KitchenWorld : MonoBehaviour
    {
        // These transforms are placed in the scene. Their positions define the serving spots and aisles.
        public Transform[] GuestSpots;
        [Header("Front-of-house walking routes")]
        public Transform Entrance;
        public Transform ArrivalAisle;
        public Transform DepartureAisle;
        public Transform Exit;
        public Camera GameCamera;
        // Use the ticket Id as the key so resolving an order finds the correct visible customer.
        readonly Dictionary<int, GuestMotion> guests = new Dictionary<int, GuestMotion>();
        GameSession session;
        float nextEntranceTime;
        // Listen for session events, then create any order that existed before this component started.
        void Start()
        {
            session = GameSession.Instance;
            session.OrderArrived += OnArrived;
            session.OrderResolved += OnResolved;
            session.ResetRequested += ResetWorld;
            foreach (var ticket in session.Orders)
                OnArrived(ticket);
        }

        // Unsubscribe when leaving the scene so the old world cannot respond to later session events.
        void OnDestroy()
        {
            if (!session)
                return;
            session.OrderArrived -= OnArrived;
            session.OrderResolved -= OnResolved;
            session.ResetRequested -= ResetWorld;
        }

        // Create the right monster, assign its walk/idle/reaction controller, and build its route.
        // The guest enters and leaves on different sides of the aisle to keep them out of the appliances.
        void OnArrived(OrderTicket ticket)
        {
            var root = new GameObject(ticket.CustomerName + " - ticket " + ticket.Id);
            root.transform.SetParent(transform, false);
            Vector3 target = GuestSpots[ticket.Slot].position;
            root.transform.position = Entrance.position;
            ticket.AtCounter = false;
            var prefab = ticket.Required == HeatState.Charred ? KitchenAssets.Instance.Tiefling : KitchenAssets.Instance.Vampire;
            var visual = KitchenAssets.Visual(prefab, root.transform, 1.8f, true);
            var animator = visual ? visual.GetComponent<Animator>() : null;
            if (animator && KitchenAssets.Instance.GuestController)
            {
                animator.runtimeAnimatorController = KitchenAssets.Instance.GuestController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            root.transform.rotation = Quaternion.Euler(0, -90, 0);
            var motion = root.AddComponent<GuestMotion>();
            motion.Animator = animator;
            // Two guests may be issued together. Space their entrances instead of
            // creating both bodies at the same doorway on the same frame.
            float wait = Mathf.Max(0, nextEntranceTime - session.Elapsed);
            nextEntranceTime = session.Elapsed + wait + 1.1f;
            // Approach from the right of the counter, then leave on its left. The two
            // offset points keep incoming and outgoing guests from cutting through each other.
            var incoming = new[]
            {
                ArrivalAisle.position,
                new Vector3(target.x + .65f, 0, ArrivalAisle.position.z),
                new Vector3(target.x + .65f, 0, target.z),
                target
            };
            var outgoing = new[]
            {
                new Vector3(target.x - .65f, 0, target.z),
                new Vector3(target.x - .65f, 0, DepartureAisle.position.z),
                DepartureAisle.position,
                Exit.position
            };
            motion.Initialize(ticket, incoming, outgoing, wait);
            guests[ticket.Id] = motion;
            AudioFeedback.Play("order");
        }

        // Let the matching customer react and leave. Remove them from the ticket lookup now;
        // GuestMotion destroys their model after they finish the exit route.
        void OnResolved(OrderTicket ticket)
        {
            if (guests.TryGetValue(ticket.Id, out GuestMotion guest))
            {
                if (guest)
                    guest.Depart(ticket.Served);
                guests.Remove(ticket.Id);
            }

            if (!ticket.Served)
                AudioFeedback.Play("unhappy");
        }

        // Clear guests, plates and occupied stations before resetting the chef's position.
        // This gives Retry the same starting conditions as the first run.
        void ResetWorld()
        {
            foreach (var guest in GetComponentsInChildren<GuestMotion>())
                Destroy(guest.gameObject);
            guests.Clear();
            nextEntranceTime = 0;
            foreach (var station in KitchenStation.All)
                if (station)
                    station.ResetStation();
            if (PlayerChef.Instance)
                PlayerChef.Instance.ResetChef();
        }
    }
}
