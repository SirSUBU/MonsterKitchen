// The shift's rules: which guests are waiting, how much patience they have, and when the game ends.
// This script keeps the score and state. It asks other scripts to update the world and UI through events.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterKitchen
{
    [DefaultExecutionOrder(-200)]
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        [Tooltip("Title previews wait at the menu; the Kitchen scene starts at its briefing.")]
        public bool IsTitleScene = true;
        // Times here are in seconds. The first two guests get extra time to teach the cooking states.
        // These fields can be changed on the GameSession component in the Inspector.
        [Header("Customer patience, in seconds")]
        [SerializeField, Min(1f)]
        private float teachingPatience = 80f;
        [SerializeField, Min(1f)]
        private float regularPatience = 52f;
        [SerializeField, Min(0f)]
        private float slowerFoodAllowance = 5f;
        [Header("Arrival pacing")]
        [SerializeField, Min(0.1f)]
        private float lessonTransitionDelay = 2f;
        [SerializeField, Min(0.1f)]
        private float arrivalInterval = 14f;
        [SerializeField, Min(0.1f)]
        private float emptyCounterDelay = 3f;
        [Header("Scoring")]
        [SerializeField, Min(0)]
        private int pointsPerServe = 100;
        [SerializeField, Min(0)]
        private int maximumPatienceBonus = 25;
        // Only guests still waiting belong here. The deck also contains orders that have not arrived yet.
        private readonly List<OrderTicket> activeOrders = new List<OrderTicket>(KitchenRules.ServingSlots);
        private ReadOnlyCollection<OrderTicket> orderView;
        private OrderTicket[] deck;
        private int nextOrder;
        private float arrivalRemaining;
        private bool arrivalPending;
        private float notificationRemaining;
        // These flags stop a second start, service or scene load from happening inside the first one.
        // For example, an event listener must not award points twice for the same meal.
        private bool resetting;
        private bool resolving;
        private bool loadingScene;
        // Increase this on every restart. A timer update can then notice that an event
        // started a new shift and stop touching the old shift's orders.
        private int runVersion;
        public RunState State { get; private set; } = RunState.Title;
        public int Served { get; private set; }
        public int Complaints { get; private set; }
        public int Score { get; private set; }
        public int Wasted { get; private set; }
        public float Elapsed { get; private set; }
        public int TotalIssued => nextOrder;
        // All moving/cooking systems use this property instead of each inventing
        // their own pause check. During a reset, cleanup must also count as stopped.
        public bool IsRunning => State == RunState.Playing && !resetting;
        public bool IsBriefing => State == RunState.Briefing;
        public IReadOnlyList<OrderTicket> Orders => orderView ?? (orderView = activeOrders.AsReadOnly());

        // Changed fires immediately for decisions and at 10 Hz for countdown displays.
        public event Action Changed;
        public event Action<OrderTicket> OrderArrived;
        public event Action<OrderTicket> OrderResolved;
        public event Action<string> Message;
        public event Action ResetRequested;
        // Set up the shared session for this scene. Title waits at the cookbook; Kitchen waits at its briefing.
        // If a duplicate session was accidentally added, keep the first one.
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

        // The next scene gets its own session. Do not leave a reference to this destroyed one.
        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // Keep Inspector values within sensible ranges, even if someone types a negative number.
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

        // The cookbook's Start cooking choice comes here. Load Kitchen without starting its timers yet.
        // The fallback also lets an isolated test scene show a briefing without the build scene list.
        public void BeginBriefing()
        {
            if (resetting || resolving || loadingScene || State != RunState.Title)
                return;
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

        // Open the kitchen from its briefing. Ignore a second start while a shift is already active.
        public void StartShift()
        {
            if (resetting || resolving || loadingScene || State == RunState.Playing || State == RunState.Paused)
                return;
            StartFreshShift();
        }

        // An explicit Retry or Restart choice starts over, even if the old shift was paused.
        public void RestartShift()
        {
            if (resetting || resolving || loadingScene)
                return;
            StartFreshShift();
        }

        // Reset the old run first, then issue the first teaching order and its short instruction.
        private void StartFreshShift()
        {
            ResetRun(RunState.Playing);
            IssueOne();
            Changed?.Invoke();
            ShowMessage("First order: cook a patty, then serve the vampire.");
        }

        // Stop this run and tell the UI it is leaving before loading the cookbook scene.
        // The kitchen UI must treat Title as hidden, not as another briefing.
        public void ReturnToTitle()
        {
            if (resetting || resolving || loadingScene)
                return;
            ResetRun(RunState.Title);
            Changed?.Invoke();
            if (!IsTitleScene && Application.CanStreamedLevelBeLoaded("Title"))
            {
                loadingScene = true;
                SceneManager.LoadScene("Title");
            }
        }

        // Only a running or paused shift can toggle. The ending and cookbook do not pause.
        // Gameplay scripts read IsRunning, so the UI can still respond while the kitchen is stopped.
        public void TogglePause()
        {
            if (resetting || resolving || loadingScene)
                return;
            if (State == RunState.Playing)
                State = RunState.Paused;
            else if (State == RunState.Paused)
                State = RunState.Playing;
            else
                return;
            Changed?.Invoke();
        }

        // Clear every counter and timer, then ask the world to remove old food and guests.
        // Resetting stays true until cleanup finishes, so clearing dishes cannot count as player waste.
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
            try
            {
                ResetRequested?.Invoke();
            }
            finally
            {
                resetting = false;
            }
        }

        // Unity calls Update each frame. Pass that frame's elapsed seconds to the shift clock.
        private void Update()
        {
            AdvanceTime(Time.deltaTime);
        }

        // Advance patience and arrivals only during play. The separate method also lets tests
        // try exact timer boundaries without waiting through an entire real-time shift.
        internal void AdvanceTime(float seconds)
        {
            if (!IsRunning || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
                return;
            int version = runVersion;
            bool wasScheduled = arrivalPending;
            Elapsed += seconds;
            // There is no i++ in the loop header because resolving an expired
            // order removes it. The next order then takes over the same index.
            for (int i = 0; i < activeOrders.Count;)
            {
                OrderTicket ticket = activeOrders[i];
                if (!ticket.AtCounter)
                {
                    i++;
                    continue;
                }

                ticket.Remaining = Mathf.Max(0f, ticket.Remaining - seconds);
                if (ticket.Remaining <= 0f)
                {
                    Resolve(ticket, false);
                    if (!IsRunning || runVersion != version)
                        return;
                // Resolve removes this entry, so the next entry now occupies this index.
                }
                else
                    i++;
            }

            // Do not charge a newly scheduled arrival for time before its predecessor left.
            AdvanceArrivals(wasScheduled ? seconds : 0f);
            if (!IsRunning || runVersion != version)
                return;
            notificationRemaining -= seconds;
            if (notificationRemaining <= 0f)
            {
                notificationRemaining = 0.1f;
                Changed?.Invoke();
            }
        }

        // Wait until the next guest is due and a service slot is free.
        // The first two orders are lessons; after them the kitchen starts overlapping orders.
        private void AdvanceArrivals(float seconds)
        {
            if (!arrivalPending || nextOrder >= KitchenRules.OrderCount)
                return;
            arrivalRemaining = Mathf.Max(0f, arrivalRemaining - seconds);
            if (arrivalRemaining > 0f || activeOrders.Count >= KitchenRules.ServingSlots)
                return;
            int version = runVersion;
            // The first real overlap is intentionally a same-food cooked/charred pair.
            bool introduceRecoveryPair = nextOrder == 2;
            IssueOne();
            if (!IsRunning || runVersion != version)
                return;
            if (introduceRecoveryPair)
                IssueOne();
            if (!IsRunning || runVersion != version)
                return;
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

        // Take the next order from the deck, give it a free counter number and set its patience.
        // OrderArrived tells KitchenWorld to create the matching customer.
        private void IssueOne()
        {
            if (!IsRunning || deck == null || nextOrder >= deck.Length)
                return;
            int slot = FindFreeSlot();
            if (slot < 0)
                return;
            OrderTicket ticket = deck[nextOrder++];
            ticket.Slot = slot;
            ticket.Duration = ticket.Id <= 2 ? teachingPatience : regularPatience;
            if (ticket.Id > 2 && ticket.Food == FoodKind.Ham)
                ticket.Duration += slowerFoodAllowance;
            ticket.Remaining = ticket.Duration;
            activeOrders.Add(ticket);
            // The world holds AtCounter false during the doorway walk. Guests
            // only spend patience once they can actually be served.
            OrderArrived?.Invoke(ticket);
        }

        // Return the first unused serving counter, or -1 when all three are occupied.
        private int FindFreeSlot()
        {
            for (int slot = 0; slot < KitchenRules.ServingSlots; slot++)
            {
                bool occupied = false;
                for (int i = 0; i < activeOrders.Count; i++)
                    if (activeOrders[i].Slot == slot)
                    {
                        occupied = true;
                        break;
                    }

                if (!occupied)
                    return slot;
            }

            return -1;
        }

        // Check the guest, arrival, patience, food kind and doneness before accepting a plate.
        // A failed check returns a message but leaves the chef's food available to recover or recook.
        public bool TryServe(int slot, FoodKind kind, HeatState heat, out string reason)
        {
            if (!IsRunning || resolving)
            {
                reason = State == RunState.Paused ? "The shift is paused." : "The counter is closed.";
                return false;
            }

            OrderTicket ticket = null;
            for (int i = 0; i < activeOrders.Count; i++)
                if (activeOrders[i].Slot == slot)
                {
                    ticket = activeOrders[i];
                    break;
                }

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
                reason = "Needs " + KitchenRules.StateName(ticket.Required).ToLowerInvariant() + " " + KitchenRules.FoodName(ticket.Food).ToLowerInvariant() + ".";
                ShowMessage(reason);
                return false;
            }

            reason = ticket.CustomerName + " served!";
            return Resolve(ticket, true);
        }

        // Finish one ticket exactly once. Update the totals before notifying the UI and customer.
        // Eight served guests means Won; three missed guests means Lost. Neither result starts a new shift.
        private bool Resolve(OrderTicket ticket, bool served)
        {
            if (!IsRunning || resolving || ticket.Resolved || !activeOrders.Contains(ticket))
                return false;
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
                else
                    Complaints++;
                if (Served >= KitchenRules.ServesToWin)
                    State = RunState.Won;
                else if (Complaints >= KitchenRules.ComplaintsToLose)
                    State = RunState.Lost;
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
                if (State == RunState.Won)
                    ShowMessage("Shift complete! Eight happy guests.");
                else if (State == RunState.Lost)
                    ShowMessage("Three complaints. Take a breath and try again.");
                else
                    ShowMessage(served ? ticket.CustomerName + ": delicious!" : ticket.CustomerName + " left unhappy.");
                return true;
            }
            finally
            {
                resolving = false;
            }
        }

        // The trash station calls this once after discarding a held dish.
        // Cleanup during a restart is not player waste and should not increase the number.
        public void RecordWaste()
        {
            if (!IsRunning)
                return;
            Wasted++;
            Changed?.Invoke();
        }

        // Send a short message to any listener. KitchenUI handles how long it stays on screen.
        public void ShowMessage(string text)
        {
            if (!string.IsNullOrEmpty(text))
                Message?.Invoke(text);
        }
    }
}
