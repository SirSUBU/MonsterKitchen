// Editor helper for the bundled Fredoka font.
// The saved TMP font asset is used by the paper UI in both the Editor and the built game.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;

namespace MonsterKitchen.Editor
{
    public static class KitchenUIFontSetup
    {
        // Build an SDF atlas from the licensed TTF and add basic characters ahead of time.
        // Save the material and texture with the font asset so they do not disappear after an Editor restart.
        // If the font asset already exists, leave it alone.
        public static string Create()
        {
            const string root = "Assets/MonsterKitchen/Resources/UI/";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(root + "KitchenRounded.asset");
            if (existing)
                return "Kitchen rounded font already prepared";
            var source = AssetDatabase.LoadAssetAtPath<Font>(root + "Fredoka-Medium.ttf");
            if (!source)
                throw new System.Exception("Import the licensed Fredoka font first.");
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = "Kitchen Rounded";
            AssetDatabase.CreateAsset(font, root + "KitchenRounded.asset");
            string chars = "";
            for (int i = 32; i < 127; i++)
                chars += (char)i;
            font.TryAddCharacters(chars);
            font.material.name = "Kitchen Rounded Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures)
                if (texture && !AssetDatabase.Contains(texture))
                    AssetDatabase.AddObjectToAsset(texture, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return "Created bundled Fredoka SDF font and ASCII atlas";
        }
    }
}
#endif
