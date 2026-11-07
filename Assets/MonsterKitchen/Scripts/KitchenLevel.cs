// The cookbook's description of one playable kitchen.
// This asset points to a scene; the room, stations and customer routes still live in that scene.
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterKitchen
{
    [CreateAssetMenu(menuName = "Monster Kitchen/Level", fileName = "NewKitchen")]
    public sealed class KitchenLevel : ScriptableObject
    {
        public string DisplayName = "The little kitchen";
        [TextArea(2, 3)] public string Description = "Cook for eight hungry guests.";
        [Tooltip("Project path, such as Assets/MonsterKitchen/Scenes/Kitchen.unity.")]
        public string ScenePath = "";
        [Tooltip("Turn this off while a future kitchen is still being made.")]
        public bool IsPublished = true;

        // Resolve the path against this build's scene list. Loading by index avoids mixing up
        // two scenes with the same filename in different folders. Missing scenes stay closed.
        public bool TryGetSceneIndex(out int index)
        {
            index = -1;
            if (!IsPublished || string.IsNullOrWhiteSpace(ScenePath))
                return false;
            index = SceneUtility.GetBuildIndexByScenePath(ScenePath);
            return index >= 0 && Application.CanStreamedLevelBeLoaded(index);
        }

        // Paths pasted from Windows often use backslashes. Unity's asset paths use slashes.
        void OnValidate()
        {
            ScenePath = (ScenePath ?? "").Trim().Replace('\\', '/');
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? "Untitled kitchen" : DisplayName.Trim();
        }
    }
}
