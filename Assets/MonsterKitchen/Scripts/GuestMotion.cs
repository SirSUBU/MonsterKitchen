using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen
{
    /// <summary>
    /// Customers follow scene-authored aisles, never a straight line through furniture.
    /// Arrival and departure use different lanes; nearby walkers yield at crossings.
    /// The Animator supplies in-place motion while this component moves the root.
    /// </summary>
    public sealed class GuestMotion : MonoBehaviour
    {
        public Animator Animator;
        public float WalkSpeed = 2.05f;
        public bool AtCounter { get; private set; }
        public bool IsDeparting { get; private set; }
        public int TicketId => ticket == null ? -1 : ticket.Id;
        public bool IsWalking { get; private set; }
        static readonly List<GuestMotion> walkers = new List<GuestMotion>();
        OrderTicket ticket;
        Vector3[] route, departure;
        Renderer[] renderers;
        int waypoint;
        float entranceDelay, reactionRemaining;

        void OnEnable() => walkers.Add(this);
        void OnDisable() => walkers.Remove(this);

        public void Initialize(OrderTicket order, Vector3[] incoming, Vector3[] outgoing, float delay)
        {
            ticket = order;
            route = incoming;
            departure = outgoing;
            entranceDelay = delay;
            renderers = GetComponentsInChildren<Renderer>();
            SetVisible(delay <= 0);
        }

        void SetVisible(bool value)
        {
            foreach (var renderer in renderers) if (renderer) renderer.enabled = value;
        }

        void Update()
        {
            var session = GameSession.Instance;
            bool advancing = session && (session.IsRunning || session.State == RunState.Won || session.State == RunState.Lost);
            if (Animator) Animator.speed = advancing ? 1 : 0;
            if (!advancing || route == null) return;
            if (entranceDelay > 0)
            {
                entranceDelay -= Time.deltaTime;
                if (entranceDelay <= 0) SetVisible(true);
                return;
            }
            if (reactionRemaining > 0)
            {
                reactionRemaining -= Time.deltaTime;
                SetWalking(false);
                return;
            }
            if (waypoint >= route.Length)
            {
                SetWalking(false);
                if (IsDeparting) { Destroy(gameObject); return; }
                transform.rotation = Quaternion.RotateTowards(transform.rotation,Quaternion.Euler(0,180,0),Time.deltaTime*540);
                if (!AtCounter)
                {
                    AtCounter = true;
                    ticket.AtCounter = true;
                    if (Animator) Animator.SetTrigger("Wave");
                }
                return;
            }

            Vector3 target = route[waypoint];
            Vector3 next = Vector3.MoveTowards(transform.position,target,WalkSpeed*Time.deltaTime);
            if (ShouldYield(next)) { SetWalking(false); return; }
            Vector3 direction = target - transform.position;
            if (direction.sqrMagnitude > .0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),Time.deltaTime*540);
            transform.position = next;
            SetWalking(true);
            if ((next-target).sqrMagnitude < .0001f) waypoint++;
        }

        bool ShouldYield(Vector3 next)
        {
            foreach (var other in walkers)
            {
                if (!other || other == this || other.entranceDelay > 0) continue;
                float now = (transform.position-other.transform.position).sqrMagnitude;
                float proposed = (next-other.transform.position).sqrMagnitude;
                // Always allow moving apart. Leavers and earlier arrivals get priority
                // across an aisle; an occupied serving position must stay unobstructed.
                if (proposed >= .85f*.85f || proposed >= now) continue;
                if (other.AtCounter || other.reactionRemaining > 0 ||
                    (!IsDeparting && other.IsDeparting) || (IsDeparting == other.IsDeparting && other.TicketId < TicketId))
                    return true;
            }
            return false;
        }

        void SetWalking(bool value)
        {
            IsWalking = value;
            if (Animator) Animator.SetFloat("Speed",value ? .65f : 0);
        }

        public void Depart(bool happy)
        {
            IsDeparting = true;
            AtCounter = false;
            ticket.AtCounter = false;
            route = departure;
            waypoint = 0;
            reactionRemaining = happy ? 1.85f : .65f;
            SetWalking(false);
            if (Animator)
            {
                Animator.ResetTrigger("Wave");
                if (happy) Animator.SetTrigger("Cheer");
            }
        }
    }
}
