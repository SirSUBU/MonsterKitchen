// Sounds for the kitchen and cookbook.
// Other scripts ask for a cue like pickup or serve. This file finds and plays its clip.
using UnityEngine;

namespace MonsterKitchen
{
    public sealed class AudioFeedback : MonoBehaviour
    {
        public static AudioFeedback Instance { get; private set; }

        // These two arrays match by position: CueNames[0] uses Clips[0], and so on.
        // The scene builder fills them with the supplied cafe sounds.
        public string[] CueNames;
        public AudioClip[] Clips;
        public AudioClip Ambience;
        [Tooltip("Optional licensed music file. Drag an imported MP3, OGG or WAV here.")]
        public AudioClip Music;
        [Range(0f, 1f)]
        public float MusicVolume = .3f;
        AudioSource source, ambienceSource, musicSource;
        // Keep effects, ambience and music on separate sources so a pickup sound
        // does not interrupt a loop. Empty music means the game just uses cafe ambience.
        void Awake()
        {
            Instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0;
            source.volume = .6f;
            if (Ambience)
            {
                ambienceSource = gameObject.AddComponent<AudioSource>();
                ambienceSource.clip = Ambience;
                ambienceSource.loop = true;
                ambienceSource.volume = .12f;
                ambienceSource.Play();
            }

            if (Music)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.clip = Music;
                musicSource.loop = true;
                musicSource.spatialBlend = 0;
                musicSource.volume = MusicVolume;
                musicSource.Play();
            }
        }

        // Look up a short cue name and play it once. A missing cue stays silent.
        public static void Play(string cue)
        {
            if (!Instance || Instance.CueNames == null)
                return;
            for (int i = 0; i < Instance.CueNames.Length && i < Instance.Clips.Length; i++)
                if (Instance.CueNames[i] == cue && Instance.Clips[i])
                {
                    Instance.source.PlayOneShot(Instance.Clips[i]);
                    return;
                }
        }

        // Clear the shared reference when this scene closes so the next scene can replace it.
        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
