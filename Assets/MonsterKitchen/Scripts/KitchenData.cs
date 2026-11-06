using System;
using UnityEngine;

namespace MonsterKitchen
{
    public enum FoodKind { Patty, Ham }
    public enum HeatState { Raw, Cooked, Charred, Ash }
    public enum RunState { Title, Briefing, Playing, Paused, Won, Lost }

    /// <summary>A live order. Only GameSession should change its runtime fields.</summary>
    [Serializable]
    public sealed class OrderTicket
    {
        public int Id;
        public int Slot = -1;
        public FoodKind Food;
        public HeatState Required;
        public float Remaining;
        public float Duration;
        public bool Resolved;
        public bool Served;
        // True by default for rule-only simulations. The scene's customer route
        // holds it false until the guest actually reaches the serving position.
        public bool AtCounter = true;

        public string CustomerName => Required == HeatState.Charred ? "Tiefling" : "Vampire";
        public float Patience01 => Duration > 0f ? Mathf.Clamp01(Remaining / Duration) : 0f;
    }

    /// <summary>Cumulative time on a hotplate, not time since the last state change.</summary>
    public readonly struct HeatThresholds
    {
        public readonly float Cooked;
        public readonly float Charred;
        public readonly float Ash;

        public HeatThresholds(float cooked, float charred, float ash)
        {
            Cooked = cooked;
            Charred = charred;
            Ash = ash;
        }
    }

    public static class KitchenRules
    {
        // Ten resolved tickets must reach one of these outcomes: 8 served or 3 missed.
        // Keeping the three numbers together prevents an unwinnable finite deck.
        public const int OrderCount = 10;
        public const int ServesToWin = 8;
        public const int ComplaintsToLose = 3;
        public const int ServingSlots = 3;

        public static string FoodName(FoodKind kind)
        {
            switch (kind)
            {
                case FoodKind.Patty: return "Patty";
                case FoodKind.Ham: return "Ham";
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public static string StateName(HeatState state)
        {
            switch (state)
            {
                case HeatState.Raw: return "Raw";
                case HeatState.Cooked: return "Cooked";
                case HeatState.Charred: return "Charred";
                case HeatState.Ash: return "Ash";
                default: throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        public static HeatThresholds Thresholds(FoodKind kind)
        {
            switch (kind)
            {
                case FoodKind.Patty: return new HeatThresholds(7f, 12f, 19f);
                case FoodKind.Ham: return new HeatThresholds(11f, 17f, 25f);
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        /// <summary>
        /// Pure state lookup. Picking food up never changes these thresholds or its heat.
        /// A threshold belongs to the new state, so exact-boundary interactions are consistent.
        /// </summary>
        public static HeatState HeatAt(FoodKind kind, float accumulatedHeat)
        {
            HeatThresholds times = Thresholds(kind);
            if (accumulatedHeat >= times.Ash) return HeatState.Ash;
            if (accumulatedHeat >= times.Charred) return HeatState.Charred;
            if (accumulatedHeat >= times.Cooked) return HeatState.Cooked;
            return HeatState.Raw;
        }

        /// <summary>
        /// New objects per run; never reuse mutable tickets between retries.
        /// The first pair after the two lessons requests the same food in opposite states.
        /// </summary>
        public static OrderTicket[] CreateDeck()
        {
            FoodKind[] foods =
            {
                FoodKind.Patty, FoodKind.Patty,
                FoodKind.Patty, FoodKind.Patty,
                FoodKind.Ham, FoodKind.Ham,
                FoodKind.Patty, FoodKind.Ham,
                FoodKind.Patty, FoodKind.Ham
            };
            HeatState[] states =
            {
                HeatState.Cooked, HeatState.Charred,
                HeatState.Cooked, HeatState.Charred,
                HeatState.Cooked, HeatState.Charred,
                HeatState.Cooked, HeatState.Cooked,
                HeatState.Charred, HeatState.Charred
            };

            var deck = new OrderTicket[OrderCount];
            for (int i = 0; i < deck.Length; i++)
            {
                deck[i] = new OrderTicket { Id = i + 1, Food = foods[i], Required = states[i] };
            }
            return deck;
        }
    }
}
