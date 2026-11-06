using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterKitchen
{
    /// <summary>
    /// Owns the shift's clock, orders and result. Scene objects own food and visuals.
    /// Timers are gated by State; we deliberately leave Unity's global time scale alone.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        [Tooltip("Title previews wait at the menu; the Kitchen scene starts at its briefing.")]
        public bool IsTitleScene = true;

        [Header("Customer patience, in seconds")]
        [SerializeField, Min(1f)] private float teachingPatience = 80f;
        [SerializeField, Min(1f)] private float regularPatience = 52f;
        [SerializeField, Min(0f)] private float slowerFoodAllowance = 5f;
        [Header("Arrival pacing")]
        [SerializeField, Min(0.1f)] private float lessonTransitionDelay = 2f;
        [SerializeField, Min(0.1f)] private float arrivalInterval = 14f;
        [SerializeField, Min(0.1f)] private float emptyCounterDelay = 3f;
        [Header("Scoring")]
        [SerializeField, Min(0)] private int pointsPerServe = 100;
        [SerializeField, Min(0)] private int maximumPatienceBonus = 25;

        private readonly List<OrderTicket> activeOrders = new List<OrderTicket>(KitchenRules.ServingSlots);
        private ReadOnlyCollection<OrderTicket> orderView;
        private OrderTicket[] deck;
        private int nextOrder;
        private float arrivalRemaining;
        private bool arrivalPending;
        private float notificationRemaining;
        private bool resetting;
        private bool resolving;
        private bool loadingScene;
        private int runVersion;

        public RunState State { get; private set; } = RunState.Title;
        public int Served { get; private set; }
        public int Complaints { get; private set; }
        public int Score { get; private set; }
        public int Wasted { get; private set; }
        public float Elapsed { get; private set; }
        public int TotalIssued => nextOrder;
        public bool IsRunning => State == RunState.Playing && !resetting;
        public bool IsBriefing => State == RunState.Briefing;

        /// <summary>Active tickets only. Resolved tickets are supplied to OrderResolved.</summary>
        public IReadOnlyList<OrderTicket> Orders => orderView ?? (orderView = activeOrders.AsReadOnly());

        // Changed fires immediately for decisions and at 10 Hz for countdown displays.
        public event Action Changed;
        public event Action<OrderTicket> OrderArrived;
        public event Action<OrderTicket> OrderResolved;
        public event Action<string> Message;
        public event Action ResetRequested;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                Destroy(this);
                return;
            }
            Instance = this;
            State = IsTitleScene ? RunState.Title : RunState.Briefing;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnValidate()
        {
            teachingPatience = Mathf.Max(1f, teachingPatience);
            regularPatience = Mathf.Max(1f, regularPatience);
            slowerFoodAllowance = Mathf.Max(0f, slowerFoodAllowance);
            lessonTransitionDelay = Mathf.Max(0.1f, lessonTransitionDelay);
            arrivalInterval = Mathf.Max(0.1f, arrivalInterval);
            emptyCounterDelay = Mathf.Max(0.1f, emptyCounterDelay);
            pointsPerServe = Mathf.Max(0, pointsPerServe);
            maximumPatienceBonus = Mathf.Max(0, maximumPatienceBonus);
        }

        public void BeginBriefing()
        {
            if (resetting || resolving || loadingScene || State != RunState.Title) return;
            if (IsTitleScene && Application.CanStreamedLevelBeLoaded("Kitchen"))
            {
                loadingScene = true;
                SceneManager.LoadScene("Kitchen");
                return;
            }
            // Also works in an isolated scene before the build's scene list is configured.
            State = RunState.Briefing;
            Changed?.Invoke();
        }

        public void StartShift()
        {
            if (resetting || resolving || loadingScene || State == RunState.Playing || State == RunState.Paused) return;
            StartFreshShift();
        }

        public void RestartShift()
        {
            if (resetting || resolving || loadingScene) return;
            StartFreshShift();
        }

        private void StartFreshShift()
        {
            ResetRun(RunState.Playing);
            IssueOne();
            Changed?.Invoke();
            ShowMessage("First order: cook a patty, then serve the vampire.");
        }

        public void ReturnToTitle()
        {
            if (resetting || resolving || loadingScene) return;
            ResetRun(RunState.Title);
            Changed?.Invoke();
            if (!IsTitleScene && Application.CanStreamedLevelBeLoaded("Title"))
            {
                loadingScene = true;
                SceneManager.LoadScene("Title");
            }
        }

        public void TogglePause()
        {
            if (resetting || resolving || loadingScene) return;
            if (State == RunState.Playing) State = RunState.Paused;
            else if (State == RunState.Paused) State = RunState.Playing;
            else return;
            Changed?.Invoke();
        }

        private void ResetRun(RunState nextState)
        {
            // Notify the world exactly once per start/retry/title reset, before issuing orders.
            // IsRunning is false during cleanup, so disposing old food cannot count as new waste.
            resetting = true;
            runVersion++;
            State = nextState;
            Served = Complaints = Score = Wasted = 0;
            Elapsed = 0f;
            activeOrders.Clear();
            deck = KitchenRules.CreateDeck();
            nextOrder = 0;
            arrivalRemaining = 0f;
            arrivalPending = false;
            notificationRemaining = 0f;
            try { ResetRequested?.Invoke(); }
            finally { resetting = false; }
        }

        private void Update()
        {
            AdvanceTime(Time.deltaTime);
        }

        /// <summary>One authoritative time step; separated from Update for deterministic checks.</summary>
        internal void AdvanceTime(float seconds)
        {
            if (!IsRunning || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            int version = runVersion;
            bool wasScheduled = arrivalPending;
            Elapsed += seconds;

            for (int i = 0; i < activeOrders.Count;)
            {
                OrderTicket ticket = activeOrders[i];
                if (!ticket.AtCounter) { i++; continue; }
                ticket.Remaining = Mathf.Max(0f, ticket.Remaining - seconds);
                if (ticket.Remaining <= 0f)
                {
                    Resolve(ticket, false);
                    if (!IsRunning || runVersion != version) return;
                    // Resolve removes this entry, so the next entry now occupies this index.
                }
                else i++;
            }

            // Do not charge a newly scheduled arrival for time before its predecessor left.
            AdvanceArrivals(wasScheduled ? seconds : 0f);
            if (!IsRunning || runVersion != version) return;
            notificationRemaining -= seconds;
            if (notificationRemaining <= 0f)
            {
                notificationRemaining = 0.1f;
                Changed?.Invoke();
            }
        }

        private void AdvanceArrivals(float seconds)
        {
            if (!arrivalPending || nextOrder >= KitchenRules.OrderCount) return;
            arrivalRemaining = Mathf.Max(0f, arrivalRemaining - seconds);
            if (arrivalRemaining > 0f || activeOrders.Count >= KitchenRules.ServingSlots) return;

            int version = runVersion;
            // The first real overlap is intentionally a same-food cooked/charred pair.
            bool introduceRecoveryPair = nextOrder == 2;
            IssueOne();
            if (!IsRunning || runVersion != version) return;
            if (introduceRecoveryPair) IssueOne();
            if (!IsRunning || runVersion != version) return;

            arrivalPending = nextOrder >= 4 && nextOrder < KitchenRules.OrderCount;
            arrivalRemaining = arrivalInterval;
            Changed?.Invoke();
            if (introduceRecoveryPair)
                ShowMessage("Both guests want patties. A charred one can still feed the tiefling.");
            else if (nextOrder == 2)
                ShowMessage("The tiefling wants CHARRED food. Leave a patty heating past cooked.");
            else if (nextOrder == 5)
                ShowMessage("Ham takes longer to cook. Watch each hotplate's indicator.");
        }

        private void IssueOne()
        {
            if (!IsRunning || deck == null || nextOrder >= deck.Length) return;
            int slot = FindFreeSlot();
            if (slot < 0) return;

            OrderTicket ticket = deck[nextOrder++];
            ticket.Slot = slot;
            ticket.Duration = ticket.Id <= 2 ? teachingPatience : regularPatience;
            if (ticket.Id > 2 && ticket.Food == FoodKind.Ham) ticket.Duration += slowerFoodAllowance;
            ticket.Remaining = ticket.Duration;
            activeOrders.Add(ticket);
            // The world holds AtCounter false during the doorway walk. Guests
            // only spend patience once they can actually be served.
            OrderArrived?.Invoke(ticket);
        }

        private int FindFreeSlot()
        {
            for (int slot = 0; slot < KitchenRules.ServingSlots; slot++)
            {
                bool occupied = false;
                for (int i = 0; i < activeOrders.Count; i++)
                    if (activeOrders[i].Slot == slot) { occupied = true; break; }
                if (!occupied) return slot;
            }
            return -1;
        }

        public bool TryServe(int slot, FoodKind kind, HeatState heat, out string reason)
        {
            if (!IsRunning || resolving)
            {
                reason = State == RunState.Paused ? "The shift is paused." : "The counter is closed.";
                return false;
            }

            OrderTicket ticket = null;
            for (int i = 0; i < activeOrders.Count; i++)
                if (activeOrders[i].Slot == slot) { ticket = activeOrders[i]; break; }

            if (ticket == null || ticket.Resolved)
            {
                reason = "No customer is waiting here.";
                return false;
            }
            if (!ticket.AtCounter)
            {
                reason = "Your guest is walking to the counter.";
                return false;
            }
            if (ticket.Remaining <= 0f)
            {
                reason = "This customer has already left.";
                Resolve(ticket, false);
                return false;
            }
            if (kind != ticket.Food || heat != ticket.Required)
            {
                reason = "Needs " + KitchenRules.StateName(ticket.Required).ToLowerInvariant()
                    + " " + KitchenRules.FoodName(ticket.Food).ToLowerInvariant() + ".";
                ShowMessage(reason);
                return false;
            }

            reason = ticket.CustomerName + " served!";
            return Resolve(ticket, true);
        }

        private bool Resolve(OrderTicket ticket, bool served)
        {
            if (!IsRunning || resolving || ticket.Resolved || !activeOrders.Contains(ticket)) return false;
            resolving = true;
            try
            {
                // Commit before notifying: a listener cannot award this ticket a second outcome.
                ticket.Resolved = true;
                ticket.Served = served;
                activeOrders.Remove(ticket);
                if (served)
                {
                    Served++;
                    Score += pointsPerServe + Mathf.RoundToInt(maximumPatienceBonus * ticket.Patience01);
                }
                else Complaints++;

                if (Served >= KitchenRules.ServesToWin) State = RunState.Won;
                else if (Complaints >= KitchenRules.ComplaintsToLose) State = RunState.Lost;

                if (IsRunning && nextOrder < KitchenRules.OrderCount)
                {
                    if (nextOrder <= 2 && activeOrders.Count == 0)
                    {
                        arrivalPending = true;
                        arrivalRemaining = lessonTransitionDelay;
                    }
                    else if (activeOrders.Count == 0)
                    {
                        arrivalPending = true;
                        arrivalRemaining = Mathf.Min(arrivalRemaining, emptyCounterDelay);
                    }
                }

                OrderResolved?.Invoke(ticket);
                Changed?.Invoke();
                if (State == RunState.Won) ShowMessage("Shift complete! Eight happy guests.");
                else if (State == RunState.Lost) ShowMessage("Three complaints. Take a breath and try again.");
                else ShowMessage(served ? ticket.CustomerName + ": delicious!" : ticket.CustomerName + " left unhappy.");
                return true;
            }
            finally { resolving = false; }
        }

        /// <summary>The owner calls this once per wasted item, not once per effect/animation.</summary>
        public void RecordWaste()
        {
            if (!IsRunning) return;
            Wasted++;
            Changed?.Invoke();
        }

        public void ShowMessage(string text)
        {
            if (!string.IsNullOrEmpty(text)) Message?.Invoke(text);
        }
    }
}
