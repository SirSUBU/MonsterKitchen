using UnityEngine;

namespace MonsterKitchen
{
    /// <summary>Scene references to licensed source assets. No runtime AssetDatabase lookups.</summary>
    public sealed class KitchenAssets : MonoBehaviour
    {
        public static KitchenAssets Instance { get; private set; }
        public GameObject PattyRaw, PattyCooked, PattyCharred;
        public GameObject HamRaw, HamCooked, HamCharred, Plate;
        public GameObject Vampire, Tiefling;
        public RuntimeAnimatorController GuestController;
        public Material AshMaterial, SteamMaterial, MintMaterial, CoralMaterial;
        void Awake() { Instance = this; }

        public GameObject FoodModel(FoodKind kind, HeatState heat)
        {
            if (kind == FoodKind.Patty)
                return heat == HeatState.Raw ? PattyRaw : heat == HeatState.Cooked ? PattyCooked : PattyCharred;
            return heat == HeatState.Raw ? HamRaw : heat == HeatState.Cooked ? HamCooked : HamCharred;
        }

        // Imported FBX files use different units. Normalize the visible bounds, retaining proportions.
        public static GameObject Visual(GameObject source, Transform parent, float size, bool byHeight = false)
        {
            if (!source) return null;
            GameObject instance = Instantiate(source, parent);
            instance.name = source.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            Bounds bounds = BoundsOf(instance);
            float extent = byHeight ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.z);
            if (extent > .0001f) instance.transform.localScale *= size / extent;
            bounds = BoundsOf(instance);
            // Keep the model's feet/base at its anchor, including meshes with an offset origin.
            Vector3 baseCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            instance.transform.position += parent.position - baseCenter;
            foreach (Collider c in instance.GetComponentsInChildren<Collider>()) Destroy(c);
            return instance;
        }

        public static Bounds BoundsOf(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        public static Color HeatColor(HeatState state)
        {
            switch (state)
            {
                case HeatState.Cooked: return new Color(.55f, .90f, .70f);
                case HeatState.Charred: return new Color(1f, .56f, .34f);
                case HeatState.Ash: return new Color(.64f, .67f, .68f);
                default: return new Color(1f, .83f, .55f);
            }
        }
    }
}
