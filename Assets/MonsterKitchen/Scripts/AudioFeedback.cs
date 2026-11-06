using UnityEngine;

namespace MonsterKitchen
{
    /// <summary>Small cue vocabulary keeps cooking rules independent from concrete WAV files.</summary>
    public sealed class AudioFeedback : MonoBehaviour
    {
        public static AudioFeedback Instance { get; private set; }
        public string[] CueNames;
        public AudioClip[] Clips;
        public AudioClip Ambience;
        [Tooltip("Optional licensed music file. Drag an imported MP3, OGG or WAV here.")]
        public AudioClip Music;
        [Range(0f,1f)] public float MusicVolume = .3f;
        AudioSource source, ambienceSource, musicSource;
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
        public static void Play(string cue)
        {
            if (!Instance || Instance.CueNames == null) return;
            for (int i=0;i<Instance.CueNames.Length && i<Instance.Clips.Length;i++)
                if (Instance.CueNames[i] == cue && Instance.Clips[i])
                { Instance.source.PlayOneShot(Instance.Clips[i]); return; }
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
