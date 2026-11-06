using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen
{
    public sealed class KitchenWorld : MonoBehaviour
    {
        public Transform[] GuestSpots;
        [Header("Front-of-house walking routes")]
        public Transform Entrance;
        public Transform ArrivalAisle;
        public Transform DepartureAisle;
        public Transform Exit;
        public Camera GameCamera;
        readonly Dictionary<int, GuestMotion> guests = new Dictionary<int, GuestMotion>();
        GameSession session;
        float nextEntranceTime;

        void Start()
        {
            session = GameSession.Instance;
            session.OrderArrived += OnArrived;
            session.OrderResolved += OnResolved;
            session.ResetRequested += ResetWorld;
            foreach (var ticket in session.Orders) OnArrived(ticket);
        }
        void OnDestroy()
        {
            if (!session) return;
            session.OrderArrived -= OnArrived;
            session.OrderResolved -= OnResolved;
            session.ResetRequested -= ResetWorld;
        }

        void OnArrived(OrderTicket ticket)
        {
            var root = new GameObject(ticket.CustomerName + " - ticket " + ticket.Id);
            root.transform.SetParent(transform, false);
            Vector3 target = GuestSpots[ticket.Slot].position;
            root.transform.position = Entrance.position;
            ticket.AtCounter = false;
            var prefab = ticket.Required == HeatState.Charred ? KitchenAssets.Instance.Tiefling : KitchenAssets.Instance.Vampire;
            var visual = KitchenAssets.Visual(prefab,root.transform,1.8f,true);
            var animator = visual ? visual.GetComponent<Animator>() : null;
            if (animator && KitchenAssets.Instance.GuestController)
            {
                animator.runtimeAnimatorController = KitchenAssets.Instance.GuestController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            root.transform.rotation = Quaternion.Euler(0,-90,0);
            var motion = root.AddComponent<GuestMotion>();
            motion.Animator = animator;
            // Two guests may be issued together. Space their entrances instead of
            // creating both bodies at the same doorway on the same frame.
            float wait = Mathf.Max(0,nextEntranceTime-session.Elapsed);
            nextEntranceTime = session.Elapsed + wait + 1.1f;
            motion.Initialize(ticket, new[] {
                ArrivalAisle.position,
                new Vector3(target.x+.65f,0,ArrivalAisle.position.z),
                new Vector3(target.x+.65f,0,target.z), target
            }, new[] {
                new Vector3(target.x-.65f,0,target.z),
                new Vector3(target.x-.65f,0,DepartureAisle.position.z),
                DepartureAisle.position, Exit.position
            }, wait);
            guests[ticket.Id] = motion;
            AudioFeedback.Play("order");
        }
        void OnResolved(OrderTicket ticket)
        {
            if (guests.TryGetValue(ticket.Id,out GuestMotion guest))
            {
                if (guest) guest.Depart(ticket.Served);
                guests.Remove(ticket.Id);
            }
            if (!ticket.Served) AudioFeedback.Play("unhappy");
        }
        void ResetWorld()
        {
            foreach (var guest in GetComponentsInChildren<GuestMotion>()) Destroy(guest.gameObject);
            guests.Clear();
            nextEntranceTime = 0;
            foreach (var station in KitchenStation.All) if (station) station.ResetStation();
            if (PlayerChef.Instance) PlayerChef.Instance.ResetChef();
        }
    }

}
