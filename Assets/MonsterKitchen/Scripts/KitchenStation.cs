using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen
{
    public enum StationType { Supply, Hotplate, Counter, Trash, Serve }

    public sealed class KitchenStation : MonoBehaviour
    {
        public static readonly List<KitchenStation> All = new List<KitchenStation>();
        public StationType Type;
        public FoodKind SupplyFood;
        public int ServiceSlot;
        public string DisplayName;
        public Transform ItemAnchor, ReachPoint;
        public float ReachRadius = 1.3f;
        public GameObject Highlight;
        public TextMesh StatusLabel;
        public Transform ProgressFill;
        public ParticleSystem Steam;
        public AudioSource Sizzle;
        public FoodItem Item { get; private set; }
        bool highlighted;
        bool playerInside;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Update()
        {
            bool running = GameSession.Instance && GameSession.Instance.IsRunning;
            bool heating = running && Type == StationType.Hotplate && Item && Item.Heat != HeatState.Ash;
            if (heating) Item.AddHeat(Time.deltaTime);
            if (Sizzle)
            {
                if (heating && !Sizzle.isPlaying) Sizzle.Play();
                if (!heating && Sizzle.isPlaying) Sizzle.Stop();
            }
            if (Steam)
            {
                bool visible = heating && Item.Heat != HeatState.Raw;
                if (visible && !Steam.isPlaying) Steam.Play();
                if (!visible && Steam.isPlaying) Steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (visible)
                {
                    var main = Steam.main;
                    main.startColor = Item.Heat == HeatState.Charred ? new Color(.24f,.23f,.28f,.6f) : new Color(.95f,.87f,.68f,.45f);
                }
            }
            if (StatusLabel)
            {
                if (Type == StationType.Hotplate && Item)
                {
                    StatusLabel.text = KitchenRules.StateName(Item.Heat).ToUpperInvariant();
                    StatusLabel.color = KitchenAssets.HeatColor(Item.Heat);
                }
                else { StatusLabel.text = DisplayName; StatusLabel.color = highlighted ? new Color(.64f,1,.80f) : new Color(1,.91f,.72f); }
            }
            if (ProgressFill)
            {
                ProgressFill.parent.gameObject.SetActive(Type == StationType.Hotplate && Item);
                if (Item)
                {
                    float duration = Item.Kind == FoodKind.Patty ? 19 : 25;
                    float p = Mathf.Clamp01(Item.HeatSeconds / duration);
                    ProgressFill.localScale = new Vector3(Mathf.Max(.01f,p),1,1);
                    ProgressFill.localPosition = new Vector3((p-1)*.5f,0,0);
                    var renderer = ProgressFill.GetComponentInChildren<Renderer>();
                    if (renderer) renderer.material.color = KitchenAssets.HeatColor(Item.Heat);
                }
            }
        }

        public string Prompt(PlayerChef chef)
        {
            if (Type == StationType.Supply) return chef.Held ? "Hands full - place your dish first" : "[E] Take " + KitchenRules.FoodName(SupplyFood);
            if (Type == StationType.Trash) return chef.Held ? "[E] Discard " + KitchenRules.FoodName(chef.Held.Kind) : "Trash - bring an unwanted dish";
            if (Type == StationType.Serve)
            {
                OrderTicket order = Ticket();
                if (order == null) return "Service " + (ServiceSlot+1) + " - waiting for a guest";
                if (!order.AtCounter) return order.CustomerName + " is walking to service " + (ServiceSlot+1);
                return chef.Held ? "[E] Serve " + order.CustomerName : order.CustomerName + " needs " + KitchenRules.StateName(order.Required) + " " + KitchenRules.FoodName(order.Food);
            }
            if (Item) return chef.Held ? "Occupied - place your held dish elsewhere" : "[E] Collect " + KitchenRules.StateName(Item.Heat) + " " + KitchenRules.FoodName(Item.Kind);
            return chef.Held ? "[E] " + (Type == StationType.Hotplate ? "Heat dish" : "Place dish") : DisplayName + " - bring an ingredient";
        }

        OrderTicket Ticket()
        {
            if (!GameSession.Instance) return null;
            foreach (var ticket in GameSession.Instance.Orders) if (ticket.Slot == ServiceSlot && !ticket.Resolved) return ticket;
            return null;
        }

        public bool Interact(PlayerChef chef)
        {
            if (!GameSession.Instance || !GameSession.Instance.IsRunning) return false;
            switch (Type)
            {
                case StationType.Supply:
                    if (chef.Held) return false;
                    var portion = new GameObject("Fresh portion").AddComponent<FoodItem>();
                    portion.Initialize(SupplyFood);
                    if (!chef.Take(portion)) { Destroy(portion.gameObject); return false; }
                    AudioFeedback.Play("pickup");
                    return true;
                case StationType.Trash:
                    if (!chef.Held) return false;
                    Destroy(chef.Release().gameObject);
                    GameSession.Instance.RecordWaste();
                    AudioFeedback.Play("place");
                    return true;
                case StationType.Serve:
                    if (!chef.Held || Ticket() == null) return false;
                    // Validate before consuming. Wrong meals remain recoverable in the player's hands.
                    FoodItem offered = chef.Held;
                    string reason;
                    if (!GameSession.Instance.TryServe(ServiceSlot,offered.Kind,offered.Heat,out reason))
                    { GameSession.Instance.ShowMessage(reason); AudioFeedback.Play("error"); return false; }
                    Destroy(chef.Release().gameObject);
                    AudioFeedback.Play("serve");
                    return true;
                default:
                    if (Item && !chef.Held)
                    {
                        if (!chef.Take(Item)) return false;
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

        public void SetHighlighted(bool value)
        {
            highlighted = value;
            if (Highlight) Highlight.SetActive(value);
        }

        // Trigger entry is a visible NPC/item collision response, independent of confirming a transfer.
        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerChef>()) { playerInside = true; }
        }
        void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerChef>()) { playerInside = false; }
        }
        public bool PlayerInside => playerInside;

        public void ResetStation()
        {
            if (Item) { Destroy(Item.gameObject); Item = null; }
            if (Sizzle) Sizzle.Stop();
            if (Steam) Steam.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            SetHighlighted(false);
        }
    }
}
