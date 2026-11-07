// Make Unity's Play button begin at the cookbook, regardless of which scene is being edited.
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MonsterKitchen.Editor
{
    [InitializeOnLoad]
    public static class KitchenPlayEntry
    {
        // Wait until the Editor has finished loading scripts before looking up the Title scene.
        static KitchenPlayEntry() => EditorApplication.delayCall += Configure;
        // Set the Editor's starting scene without changing the scene currently open for editing.
        // This also has a menu command in case the setting needs to be restored manually.
        [MenuItem("MonsterKitchen/Start Play at cookbook")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            var title = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/MonsterKitchen/Scenes/Title.unity");
            if (title)
                EditorSceneManager.playModeStartScene = title;
        }
    }
}
#endif
