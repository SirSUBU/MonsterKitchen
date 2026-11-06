#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MonsterKitchen.Editor
{
    /// <summary>Play always opens the cookbook, even while editing the Kitchen scene.</summary>
    [InitializeOnLoad]
    public static class KitchenPlayEntry
    {
        static KitchenPlayEntry() => EditorApplication.delayCall += Configure;
        [MenuItem("MonsterKitchen/Start Play at cookbook")]
        public static void Configure()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            var title=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/MonsterKitchen/Scenes/Title.unity");
            if(title) EditorSceneManager.playModeStartScene=title;
        }
    }
}
#endif
