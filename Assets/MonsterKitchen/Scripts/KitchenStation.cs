// The common behavior for ingredient crates, hotplates, resting counters, the bin and serving counters.
// PlayerChef chooses a reachable station; this script decides what E or Space does there.
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace MonsterKitchen
{
    // The station type selects a branch in Interact. Each station still uses the same reach trigger.
    public enum StationType
    {
        Supply,
        Hotplate,
        Counter,
        Trash,
        Serve
    }

    public sealed class KitchenStation : MonoBehaviour
    {
        public static readonly List<KitchenStation> All = new List<KitchenStation>();
        public StationType Type;
        public FoodKind SupplyFood;
        public int ServiceSlot;
        public string DisplayName;
        // ItemAnchor is where a plate rests. ReachPoint is where the chef should stand to interact.
        public Transform ItemAnchor, ReachPoint;
        public float ReachRadius = 1.3f;
        public GameObject Highlight;
        // The scene uses the same rounded TMP font as the paper menus.
        public TMP_Text StatusLabel;
        public Transform ProgressFill;
        public ParticleSystem Steam;
        public AudioSource Sizzle;
        // A work surface holds at most one portion. The chef's Held reference owns a carried portion.
        public FoodItem Item { get; private set; }

        bool highlighted;
        bool playerInside;
        // Register the station so PlayerChef can find it without searching the scene every frame.
        void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);
        }

        // Remove it from the list when the scene unloads or this station is disabled.
        void OnDisable()
        {
            All.Remove(this);
        }

        // A running hotplate heats its item and updates the sizzle, steam, label and progress bar.
        // Resting counters never add heat. Pause stops cooking and the stove effects.
        void Update()
        {
            bool running = GameSession.Instance && GameSession.Instance.IsRunning;
            bool heating = running && Type == StationType.Hotplate && Item && Item.Heat != HeatState.Ash;
            if (heating)
                Item.AddHeat(Time.deltaTime);
            if (Sizzle)
            {
                if (heating && !Sizzle.isPlaying)
                    Sizzle.Play();
                if (!heating && Sizzle.isPlaying)
                    Sizzle.Stop();
            }

            if (Steam)
            {
                bool visible = heating && Item.Heat != HeatState.Raw;
                if (visible && !Steam.isPlaying)
                    Steam.Play();
                if (!visible && Steam.isPlaying)
                    Steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (visible)
                {
                    var main = Steam.main;
                    main.startColor = Item.Heat == HeatState.Charred ? new Color(.24f, .23f, .28f, .6f) : new Color(.95f, .87f, .68f, .45f);
                }
            }

            if (StatusLabel)
            {
                if (Type == StationType.Hotplate && Item)
                {
                    StatusLabel.text = KitchenRules.StateName(Item.Heat);
                    // Dark ink stays readable on the cream station tag. Keep the progress bar's
                    // brighter food colors separate from the lettering.
                    StatusLabel.color = Item.Heat == HeatState.Cooked ? new Color(.32f, .42f, .23f)
                        : Item.Heat == HeatState.Charred || Item.Heat == HeatState.Ash ? new Color(.65f, .31f, .24f)
                        : new Color(.29f, .21f, .17f);
                }
                else
                {
                    StatusLabel.text = DisplayName;
                    StatusLabel.color = highlighted ? new Color(.32f, .42f, .23f) : new Color(.29f, .21f, .17f);
                }
            }

            if (ProgressFill)
            {
                ProgressFill.parent.gameObject.SetActive(Type == StationType.Hotplate && Item);
                if (Item)
                {
                    // Use the recipe's ash threshold too, so editing a cooking time does not
                    // leave the visible bar on the old timing. The offset keeps its left edge still.
                    float duration = KitchenRules.Thresholds(Item.Kind).Ash;
                    float p = Mathf.Clamp01(Item.HeatSeconds / duration);
                    ProgressFill.localScale = new Vector3(Mathf.Max(.01f, p), 1, 1);
                    ProgressFill.localPosition = new Vector3((p - 1) * .5f, 0, 0);
                    var renderer = ProgressFill.GetComponentInChildren<Renderer>();
                    if (renderer)
                        renderer.material.color = KitchenAssets.HeatColor(Item.Heat);
                }
            }
        }

        // Describe what pressing the interaction key would do right now.
        // The prompt changes with the station's contents, the chef's hands and the waiting guest.
        public string Prompt(PlayerChef chef)
        {
            if (Type == StationType.Supply)
                return chef.Held ? "Hands full - place your dish first" : "[E] Take " + KitchenRules.FoodName(SupplyFood);
            if (Type == StationType.Trash)
                return chef.Held ? "[E] Discard " + KitchenRules.FoodName(chef.Held.Kind) : "Trash - bring an unwanted dish";
            if (Type == StationType.Serve)
            {
                OrderTicket order = Ticket();
                if (order == null)
                    return "Service " + (ServiceSlot + 1) + " - waiting for a guest";
                if (!order.AtCounter)
                    return order.CustomerName + " is walking to service " + (ServiceSlot + 1);
                return chef.Held ? "[E] Serve " + order.CustomerName : order.CustomerName + " needs " + KitchenRules.StateName(order.Required) + " " + KitchenRules.FoodName(order.Food);
            }

            if (Item)
                return chef.Held ? "Occupied - place your held dish elsewhere" : "[E] Collect " + KitchenRules.StateName(Item.Heat) + " " + KitchenRules.FoodName(Item.Kind);
            return chef.Held ? "[E] " + (Type == StationType.Hotplate ? "Heat dish" : "Place dish") : DisplayName + " - bring an ingredient";
        }

        // Find the unresolved order assigned to this serving counter, if there is one.
        OrderTicket Ticket()
        {
            if (!GameSession.Instance)
                return null;
            foreach (var ticket in GameSession.Instance.Orders)
                if (ticket.Slot == ServiceSlot && !ticket.Resolved)
                    return ticket;
            return null;
        }

        // Check the destination before moving or deleting anything. Supplies create a new portion;
        // counters transfer it; serving validates it; the bin discards it. Full hands or an occupied
        // surface leave both portions where they were instead of replacing one.
        public bool Interact(PlayerChef chef)
        {
            if (!GameSession.Instance || !GameSession.Instance.IsRunning)
                return false;
            switch (Type)
            {
                case StationType.Supply:
                    // Only create a portion after confirming that the hands are empty.
                    if (chef.Held)
                        return false;
                    var portion = new GameObject("Fresh portion").AddComponent<FoodItem>();
                    portion.Initialize(SupplyFood);
                    if (!chef.Take(portion))
                    {
                        Destroy(portion.gameObject);
                        return false;
                    }

                    AudioFeedback.Play("pickup");
                    return true;
                case StationType.Trash:
                    // Release the reference before destroying the dish it points to.
                    if (!chef.Held)
                        return false;
                    Destroy(chef.Release().gameObject);
                    GameSession.Instance.RecordWaste();
                    AudioFeedback.Play("place");
                    return true;
                case StationType.Serve:
                    if (!chef.Held || Ticket() == null)
                        return false;
                    // Validate before consuming. Wrong meals remain recoverable in the player's hands.
                    FoodItem offered = chef.Held;
                    string reason;
                    if (!GameSession.Instance.TryServe(ServiceSlot, offered.Kind, offered.Heat, out reason))
                    {
                        GameSession.Instance.ShowMessage(reason);
                        AudioFeedback.Play("error");
                        return false;
                    }

                    Destroy(chef.Release().gameObject);
                    AudioFeedback.Play("serve");
                    return true;
                default:
                    // Resting counters and hotplates share these two transfers.
                    // Heating is separate, in Update, so putting food down stays simple.
                    if (Item && !chef.Held)
                    {
                        if (!chef.Take(Item))
                            return false;
                        Item = null;
                        AudioFeedback.Play("pickup");
                        return true;
                    }

                    if (!Item && chef.Held)
                    {
                        Item = chef.Release();
                        Item.MoveTo(ItemAnchor);
                        AudioFeedback.Play("place");
                        return true;
                    }

                    return false;
            }
        }

        // PlayerChef calls this for the chosen target, so only that station looks selected.
        public void SetHighlighted(bool value)
        {
            highlighted = value;
            if (Highlight)
                Highlight.SetActive(value);
        }

        // Entering the reach trigger makes this station a targeting candidate.
        // The player still needs to press E or Space to move a dish.
        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerChef>())
            {
                playerInside = true;
            }
        }

        // Leaving the trigger removes this station as a nearby target.
        void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerChef>())
            {
                playerInside = false;
            }
        }

        public bool PlayerInside => playerInside;

        // Remove a leftover dish and stop its effects when a fresh shift begins.
        public void ResetStation()
        {
            if (Item)
            {
                Destroy(Item.gameObject);
                Item = null;
            }

            if (Sizzle)
                Sizzle.Stop();
            if (Steam)
                Steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetHighlighted(false);
        }
    }
}
