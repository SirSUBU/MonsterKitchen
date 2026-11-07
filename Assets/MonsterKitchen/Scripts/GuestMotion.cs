// Move a customer through the restaurant's entry and exit aisles.
// The route moves the body; the Animator only plays the walk, wave and cheer poses.
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen
{
    public sealed class GuestMotion : MonoBehaviour
    {
        public Animator Animator;
        public float WalkSpeed = 2.05f;
        public bool AtCounter { get; private set; }
        public bool IsDeparting { get; private set; }
        public int TicketId => ticket == null ? -1 : ticket.Id;
        public bool IsWalking { get; private set; }

        // Keep track of other guests so two people do not try to occupy the same aisle.
        static readonly List<GuestMotion> walkers = new List<GuestMotion>();
        OrderTicket ticket;
        Vector3[] route, departure;
        Renderer[] renderers;
        // This is the next point in the route. Delays separate arrivals and leave time for reactions.
        int waypoint;
        float entranceDelay, reactionRemaining;
        // Join the list used to check whether another guest is in the way.
        void OnEnable() => walkers.Add(this);
        // Stop other guests checking this one after it leaves or the scene closes.
        void OnDisable() => walkers.Remove(this);
        // Store this customer's ticket and both routes. A delayed arrival stays hidden at the door
        // until it is their turn to walk in.
        public void Initialize(OrderTicket order, Vector3[] incoming, Vector3[] outgoing, float delay)
        {
            ticket = order;
            route = incoming;
            departure = outgoing;
            entranceDelay = delay;
            renderers = GetComponentsInChildren<Renderer>();
            SetVisible(delay <= 0);
        }

        // Hide or show the model without disabling the route script that counts down its entrance.
        void SetVisible(bool value)
        {
            foreach (var renderer in renderers)
                if (renderer)
                    renderer.enabled = value;
        }

        // Handle the entrance delay, a short reaction, and then one step toward the next waypoint.
        // Reaching the final entry point makes the guest ready to serve and starts their patience clock.
        void Update()
        {
            var session = GameSession.Instance;
            bool advancing = session && (session.IsRunning || session.State == RunState.Won || session.State == RunState.Lost);
            if (Animator)
                Animator.speed = advancing ? 1 : 0;
            if (!advancing || route == null)
                return;
            if (entranceDelay > 0)
            {
                entranceDelay -= Time.deltaTime;
                if (entranceDelay <= 0)
                    SetVisible(true);
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
                if (IsDeparting)
                {
                    Destroy(gameObject);
                    return;
                }

                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, 180, 0), Time.deltaTime * 540);
                if (!AtCounter)
                {
                    AtCounter = true;
                    ticket.AtCounter = true;
                    if (Animator)
                        Animator.SetTrigger("Wave");
                }

                return;
            }

            Vector3 target = route[waypoint];
            Vector3 next = Vector3.MoveTowards(transform.position, target, WalkSpeed * Time.deltaTime);
            if (ShouldYield(next))
            {
                SetWalking(false);
                return;
            }

            Vector3 direction = target - transform.position;
            if (direction.sqrMagnitude > .0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 540);
            transform.position = next;
            SetWalking(true);
            if ((next - target).sqrMagnitude < .0001f)
                waypoint++;
        }

        // Wait when another guest is close and has priority. Let guests move apart freely,
        // otherwise two people could keep blocking each other forever.
        bool ShouldYield(Vector3 next)
        {
            foreach (var other in walkers)
            {
                if (!other || other == this || other.entranceDelay > 0)
                    continue;
                float now = (transform.position - other.transform.position).sqrMagnitude;
                float proposed = (next - other.transform.position).sqrMagnitude;
                // Always allow moving apart. Leavers and earlier arrivals get priority
                // across an aisle; an occupied serving position must stay unobstructed.
                if (proposed >= .85f * .85f || proposed >= now)
                    continue;
                if (other.AtCounter || other.reactionRemaining > 0 || (!IsDeparting && other.IsDeparting) || (IsDeparting == other.IsDeparting && other.TicketId < TicketId))
                    return true;
            }

            return false;
        }

        // Use the Speed parameter to blend between the Animator's idle and walk states.
        void SetWalking(bool value)
        {
            IsWalking = value;
            if (Animator)
                Animator.SetFloat("Speed", value ? .65f : 0);
        }

        // Switch to the exit route. A happy guest cheers before walking; an unhappy guest leaves sooner.
        // The ticket is no longer available for service once departure begins.
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
                if (happy)
                    Animator.SetTrigger("Cheer");
            }
        }
    }
}
