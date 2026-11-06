using UnityEngine;

namespace MonsterKitchen
{
    /// <summary>A particular portion. Its heat travels with it between hands and stations.</summary>
    public sealed class FoodItem : MonoBehaviour
    {
        public FoodKind Kind { get; private set; }
        public HeatState Heat { get; private set; }
        public float HeatSeconds { get; private set; }
        public Transform Owner { get; private set; }
        GameObject visual;
        Transform foodAnchor;

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

        public void MoveTo(Transform newOwner)
        {
            Owner = newOwner;
            transform.SetParent(newOwner, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        public void AddHeat(float delta)
        {
            if (Heat == HeatState.Ash || delta <= 0) return;
            HeatSeconds += delta;
            HeatState next = KitchenRules.HeatAt(Kind, HeatSeconds);
            if (next == Heat) return;
            Heat = next;
            RebuildVisual();
            AudioFeedback.Play(next == HeatState.Cooked ? "ready" : "burn");
            if (next == HeatState.Ash) GameSession.Instance.ShowMessage("Ash! Clear the hotplate and start a fresh portion.");
        }

        void RebuildVisual()
        {
            if (visual) { visual.SetActive(false); Destroy(visual); }
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
            else visual = KitchenAssets.Visual(KitchenAssets.Instance.FoodModel(Kind, Heat), foodAnchor, .62f);
        }
    }
}
