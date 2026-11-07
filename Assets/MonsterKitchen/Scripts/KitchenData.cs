// Shared names, cooking thresholds and the order sequence.
// Start here when changing a recipe's cook time or the guests in a shift.
using System;
using UnityEngine;

namespace MonsterKitchen
{
    // The two ingredients currently available from the crates.
    public enum FoodKind
    {
        Patty,
        Ham
    }

    // Food moves in this order as it stays on a hotplate. Ash cannot be served.
    public enum HeatState
    {
        Raw,
        Cooked,
        Charred,
        Ash
    }

    // These are the game's screens and play states. Only Playing advances the shift.
    public enum RunState
    {
        Title,
        Briefing,
        Playing,
        Paused,
        Won,
        Lost
    }

    // One guest's request. GameSession changes the waiting time and result; GuestMotion
    // sets AtCounter when the visible guest reaches their numbered serving spot.
    [Serializable]
    public sealed class OrderTicket
    {
        // Id identifies the guest across their whole visit. Slot is the serving counter, numbered 0 to 2.
        // Duration is their starting patience; Remaining counts down only after arrival.
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

    // Each value is total time on a hotplate, not extra time after the previous state.
    // For example, a patty becomes charred at 12 total seconds, not 7 plus 12.
    public readonly struct HeatThresholds
    {
        public readonly float Cooked;
        public readonly float Charred;
        public readonly float Ash;
        // Keep the three boundaries together so the state lookup and progress feedback agree.
        public HeatThresholds(float cooked, float charred, float ash)
        {
            Cooked = cooked;
            Charred = charred;
            Ash = ash;
        }
    }

    public static class KitchenRules
    {
        // These recipes and win/loss rules are shared by kitchens. A new scene can change
        // its layout and GameSession's patience settings while keeping the same cooking rules.
        // Ten resolved orders guarantee an ending: at least eight served or at least three missed.
        // With only nine orders, seven served and two missed would leave the game unfinished.
        public const int OrderCount = 10;
        public const int ServesToWin = 8;
        public const int ComplaintsToLose = 3;
        public const int ServingSlots = 3;
        // Turn a food enum into the name shown in prompts and order slips.
        public static string FoodName(FoodKind kind)
        {
            switch (kind)
            {
                case FoodKind.Patty:
                    return "Patty";
                case FoodKind.Ham:
                    return "Ham";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        // Keep the written heat-state names consistent wherever they appear.
        public static string StateName(HeatState state)
        {
            switch (state)
            {
                case HeatState.Raw:
                    return "Raw";
                case HeatState.Cooked:
                    return "Cooked";
                case HeatState.Charred:
                    return "Charred";
                case HeatState.Ash:
                    return "Ash";
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        // Patties cook faster than ham. Change these seconds to adjust that timing.
        public static HeatThresholds Thresholds(FoodKind kind)
        {
            switch (kind)
            {
                case FoodKind.Patty:
                    return new HeatThresholds(7f, 12f, 19f);
                case FoodKind.Ham:
                    return new HeatThresholds(11f, 17f, 25f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        // Check the hottest state first in case a long frame crosses more than one threshold.
        // A dish picked up exactly at a boundary already belongs to the new state.
        public static HeatState HeatAt(FoodKind kind, float accumulatedHeat)
        {
            HeatThresholds times = Thresholds(kind);
            if (accumulatedHeat >= times.Ash)
                return HeatState.Ash;
            if (accumulatedHeat >= times.Charred)
                return HeatState.Charred;
            if (accumulatedHeat >= times.Cooked)
                return HeatState.Cooked;
            return HeatState.Raw;
        }

        // The first two guests teach cooked and charred patties. The next pair wants the same food
        // in different states, so an overcooked meal can still go to the other guest.
        // Create new ticket objects each run so an earlier shift's timers do not carry over.
        public static OrderTicket[] CreateDeck()
        {
            FoodKind[] foods =
            {
                FoodKind.Patty,
                FoodKind.Patty,
                FoodKind.Patty,
                FoodKind.Patty,
                FoodKind.Ham,
                FoodKind.Ham,
                FoodKind.Patty,
                FoodKind.Ham,
                FoodKind.Patty,
                FoodKind.Ham
            };
            HeatState[] states =
            {
                HeatState.Cooked,
                HeatState.Charred,
                HeatState.Cooked,
                HeatState.Charred,
                HeatState.Cooked,
                HeatState.Charred,
                HeatState.Cooked,
                HeatState.Cooked,
                HeatState.Charred,
                HeatState.Charred
            };
            var deck = new OrderTicket[OrderCount];
            for (int i = 0; i < deck.Length; i++)
            {
                deck[i] = new OrderTicket
                {
                    Id = i + 1,
                    Food = foods[i],
                    Required = states[i]
                };
            }

            return deck;
        }
    }
}
