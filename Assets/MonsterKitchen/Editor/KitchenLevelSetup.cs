// Create the first level's metadata and connect it to the existing scenes.
// This does not rebuild the kitchen, move any props or overwrite a catalog that already exists.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MonsterKitchen.Editor
{
    public static class KitchenLevelSetup
    {
        public const string FirstLevelPath = "Assets/MonsterKitchen/Levels/LittleKitchen.asset";
        public const string CatalogPath = "Assets/MonsterKitchen/Levels/KitchenLevels.asset";
        const string TitlePath = "Assets/MonsterKitchen/Scenes/Title.unity";
        const string KitchenPath = "Assets/MonsterKitchen/Scenes/Kitchen.unity";

        // The scene builders use this too. Only create missing assets: a later build must not
        // erase new kitchens, renamed menu entries or a deliberately reordered catalog.
        public static KitchenLevelCatalog EnsureDefaults()
        {
            Directory.CreateDirectory("Assets/MonsterKitchen/Levels");
            AssetDatabase.Refresh();
            var first = AssetDatabase.LoadAssetAtPath<KitchenLevel>(FirstLevelPath);
            if (!first)
            {
                first = ScriptableObject.CreateInstance<KitchenLevel>();
                first.DisplayName = "The little kitchen";
                first.Description = "Cook for eight hungry guests.";
                first.ScenePath = KitchenPath;
                AssetDatabase.CreateAsset(first, FirstLevelPath);
            }
            var catalog = AssetDatabase.LoadAssetAtPath<KitchenLevelCatalog>(CatalogPath);
            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<KitchenLevelCatalog>();
                catalog.Levels = new[] { first };
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            return catalog;
        }

        // Connect the two original scenes without replacing their objects. Refuse to switch
        // scenes if any open scene has unsaved work, then restore the original editing setup.
        [MenuItem("MonsterKitchen/Configure level selection")]
        public static string Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before configuring levels.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before configuring levels.");
            var catalog = EnsureDefaults();
            var first = AssetDatabase.LoadAssetAtPath<KitchenLevel>(FirstLevelPath);
            try
            {
                foreach (string path in new[] { TitlePath, KitchenPath })
                {
                    var scene = EditorSceneManager.OpenScene(path);
                    var session = UnityEngine.Object.FindAnyObjectByType<GameSession>();
                    if (!session)
                        throw new InvalidOperationException("Missing GameSession in " + path);
                    session.Level = first;
                    EditorUtility.SetDirty(session);
                    var book = UnityEngine.Object.FindAnyObjectByType<CookbookMenu>();
                    if (book)
                    {
                        book.Levels = catalog;
                        EditorUtility.SetDirty(book);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                SyncBuildScenes();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            return "Connected the cookbook and first kitchen to the level catalog.";
        }

        // Put Title first and enable published level scenes. Preserve other build entries,
        // including future scenes, instead of resetting the list to just Title and Kitchen.
        [MenuItem("MonsterKitchen/Update level build scenes")]
        public static void SyncBuildScenes()
        {
            var catalog = EnsureDefaults();
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(TitlePath, true)
            };
            foreach (var existing in EditorBuildSettings.scenes)
                if (existing.path != TitlePath && !scenes.Exists(s => s.path == existing.path))
                    scenes.Add(new EditorBuildSettingsScene(existing.path, existing.enabled));
            foreach (var level in catalog.PublishedLevels())
            {
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(level.ScenePath))
                    throw new InvalidOperationException("Choose a saved scene for " + level.DisplayName + ".");
                var entry = scenes.Find(s => s.path == level.ScenePath);
                if (entry != null)
                    entry.enabled = true;
                else
                    scenes.Add(new EditorBuildSettingsScene(level.ScenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
