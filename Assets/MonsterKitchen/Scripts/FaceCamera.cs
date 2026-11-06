using UnityEngine;
namespace MonsterKitchen
{
    // Separate file preserves Unity's serialized MonoScript reference in authored scenes.
    public sealed class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            if (Camera.main) transform.rotation = Camera.main.transform.rotation;
        }
    }
}
