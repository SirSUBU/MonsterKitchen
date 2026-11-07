// One portion of food, including its plate, cooking time and current owner.
// Moving it between hands and counters keeps the same portion and saved heat.
using UnityEngine;

namespace MonsterKitchen
{
    public sealed class FoodItem : MonoBehaviour
    {
        public FoodKind Kind { get; private set; }
        public HeatState Heat { get; private set; }
        public float HeatSeconds { get; private set; }
        public Transform Owner { get; private set; }

        // Only this child changes when food cooks. The plate and FoodItem stay the same.
        GameObject visual;
        Transform foodAnchor;
        // A supply crate calls this once for a new portion. It starts raw at zero seconds.
        // The food sits slightly above the plate so the two models do not overlap.
        public void Initialize(FoodKind kind)
        {
            Kind = kind;
            Heat = HeatState.Raw;
            HeatSeconds = 0;
            name = KitchenRules.FoodName(kind) + " portion";
            var plateAnchor = new GameObject("Plate").transform;
            plateAnchor.SetParent(transform, false);
            KitchenAssets.Visual(KitchenAssets.Instance.Plate, plateAnchor, .78f);
            foodAnchor = new GameObject("Food visual").transform;
            foodAnchor.SetParent(transform, false);
            foodAnchor.localPosition = new Vector3(0, .08f, 0);
            RebuildVisual();
        }

        // Attach this portion to the chef's hands or a station's item anchor.
        // The local position is reset because each anchor already marks the right spot.
        public void MoveTo(Transform newOwner)
        {
            Owner = newOwner;
            transform.SetParent(newOwner, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        // Only a running hotplate adds heat. Carrying food does not call this method.
        // Swap the model and play a cue only when the food enters a different heat state.
        public void AddHeat(float delta)
        {
            if (Heat == HeatState.Ash || delta <= 0)
                return;
            HeatSeconds += delta;
            HeatState next = KitchenRules.HeatAt(Kind, HeatSeconds);
            if (next == Heat)
                return;
            Heat = next;
            RebuildVisual();
            AudioFeedback.Play(next == HeatState.Cooked ? "ready" : "burn");
            if (next == HeatState.Ash)
                GameSession.Instance.ShowMessage("Ash! Clear the hotplate and start a fresh portion.");
        }

        // Replace the food model to match its current state. Ash uses a few small lumps
        // instead of a usable meal, so the player can tell it needs to go in the bin.
        void RebuildVisual()
        {
            if (visual)
            {
                visual.SetActive(false);
                Destroy(visual);
            }

            if (Heat == HeatState.Ash)
            {
                visual = new GameObject("Unusable ash");
                visual.transform.SetParent(foodAnchor, false);
                for (int i = 0; i < 6; i++)
                {
                    var lump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    lump.name = "Ash fragment";
                    Destroy(lump.GetComponent<Collider>());
                    lump.transform.SetParent(visual.transform, false);
                    float angle = i * Mathf.PI / 3f;
                    lump.transform.localPosition = new Vector3(Mathf.Cos(angle) * .15f, .025f, Mathf.Sin(angle) * .13f);
                    lump.transform.localScale = new Vector3(.2f, .075f, .16f);
                    lump.GetComponent<Renderer>().sharedMaterial = KitchenAssets.Instance.AshMaterial;
                }
            }
            else
                visual = KitchenAssets.Visual(KitchenAssets.Instance.FoodModel(Kind, Heat), foodAnchor, .62f);
        }
    }
}
