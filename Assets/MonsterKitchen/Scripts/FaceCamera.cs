// Keep a world label facing the game camera so it is readable above a station.
using UnityEngine;

namespace MonsterKitchen
{
    // Attach this to floating station labels and heat bars.
    public sealed class FaceCamera : MonoBehaviour
    {
        // Run after the normal Update calls, once that frame's camera and objects have finished moving.
        void LateUpdate()
        {
            if (Camera.main)
                transform.rotation = Camera.main.transform.rotation;
        }
    }
}
