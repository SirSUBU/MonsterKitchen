#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;

namespace MonsterKitchen.Editor
{
    public static class KitchenUIFontSetup
    {
        public static string Create()
        {
            const string root="Assets/MonsterKitchen/Resources/UI/";
            var existing=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(root+"KitchenRounded.asset");
            if(existing)return "Kitchen rounded font already prepared";
            var source=AssetDatabase.LoadAssetAtPath<Font>(root+"Fredoka-Medium.ttf");
            if(!source)throw new System.Exception("Import the licensed Fredoka font first.");
            var font=TMP_FontAsset.CreateFontAsset(source,90,9,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            font.name="Kitchen Rounded";
            AssetDatabase.CreateAsset(font,root+"KitchenRounded.asset");
            string chars="";for(int i=32;i<127;i++)chars+=(char)i;
            font.TryAddCharacters(chars);
            font.material.name="Kitchen Rounded Material";
            AssetDatabase.AddObjectToAsset(font.material,font);
            foreach(var texture in font.atlasTextures)if(texture&&!AssetDatabase.Contains(texture))AssetDatabase.AddObjectToAsset(texture,font);
            EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
            return "Created bundled Fredoka SDF font and ASCII atlas";
        }
    }
}
#endif
