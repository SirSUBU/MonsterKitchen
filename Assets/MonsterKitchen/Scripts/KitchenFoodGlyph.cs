// Small plate pictures for the order slips and held-food display.
// These use simple UI geometry, so the pictures resize with their RectTransform.
using UnityEngine;

namespace MonsterKitchen
{
    public sealed class KitchenFoodGlyph : UnityEngine.UI.MaskableGraphic
    {
        // 0 draws a patty and 1 draws ham. The extra flags select charred food or unusable ash.
        private int kind;
        private bool charred, ash;
        // Ask Unity to redraw only when the food or its appearance changes.
        public void SetFood(int food, bool isCharred, bool isAsh)
        {
            if (kind == food && charred == isCharred && ash == isAsh)
                return;
            kind = food;
            charred = isCharred;
            ash = isAsh;
            SetVerticesDirty();
        }

        // Draw the plate first, then the food on top. Ham gets a bone and patties get grill marks.
        // The dimensions come from this UI element's rectangle, not a fixed screen resolution.
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            Vector2 center = bounds.center;
            float w = bounds.width, h = bounds.height;
            Disc(mesh, center + Vector2.down * h * 0.04f, w * 0.47f, h * 0.36f, new Color(0.62f, 0.72f, 0.69f));
            Disc(mesh, center, w * 0.47f, h * 0.36f, new Color(0.96f, 0.96f, 0.85f));
            Disc(mesh, center, w * 0.37f, h * 0.27f, new Color(0.82f, 0.87f, 0.78f));
            Color meat = ash ? new Color(0.38f, 0.39f, 0.38f) : charred ? new Color(0.22f, 0.16f, 0.15f) : kind == 0 ? new Color(0.57f, 0.27f, 0.16f) : new Color(0.82f, 0.38f, 0.35f);
            Disc(mesh, center + Vector2.up * h * 0.025f, w * (ash ? 0.26f : 0.31f), h * (ash ? 0.12f : 0.225f), meat);
            if (ash)
                return;
            if (kind == 1)
            {
                Disc(mesh, center + new Vector2(w * 0.08f, h * 0.015f), w * 0.075f, h * 0.08f, new Color(1, 0.88f, 0.7f));
                Disc(mesh, center + new Vector2(w * 0.08f, h * 0.015f), w * 0.03f, h * 0.035f, new Color(0.68f, 0.37f, 0.27f));
            }
            else
            {
                Color marks = charred ? new Color(0.59f, 0.33f, 0.23f) : new Color(0.86f, 0.52f, 0.27f);
                for (int i = -1; i <= 1; i++)
                    Quad(mesh, center + new Vector2(i * w * 0.13f, h * 0.03f), new Vector2(w * 0.025f, h * 0.24f), marks);
            }
        }

        // Make an ellipse from triangles sharing one center point. Different horizontal and vertical
        // radii give the plate its tilted shape.
        private static void Disc(UnityEngine.UI.VertexHelper mesh, Vector2 center, float rx, float ry, Color color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(center, color, Vector2.zero);
            const int segments = 24;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry), color, Vector2.zero);
                if (i > 0)
                    mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        // Two triangles make one rectangular grill mark.
        private static void Quad(UnityEngine.UI.VertexHelper mesh, Vector2 center, Vector2 size, Color color)
        {
            int start = mesh.currentVertCount;
            Vector2 half = size * 0.5f;
            mesh.AddVert(center + new Vector2(-half.x, -half.y), color, Vector2.zero);
            mesh.AddVert(center + new Vector2(-half.x, half.y), color, Vector2.zero);
            mesh.AddVert(center + new Vector2(half.x, half.y), color, Vector2.zero);
            mesh.AddVert(center + new Vector2(half.x, -half.y), color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
