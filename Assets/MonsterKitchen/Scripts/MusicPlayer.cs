// One music player for the whole game. Two sources let the next song fade in while
// the previous one fades out. Neither source shares a channel with cooking sounds.
using UnityEngine;

namespace MonsterKitchen
{
    public sealed class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance { get; private set; }
        public AudioClip CurrentClip { get; private set; }

        const float FadeSeconds = .65f;
        readonly AudioSource[] decks = new AudioSource[2];
        readonly float[] fromVolume = new float[2];
        readonly float[] toVolume = new float[2];
        float requestedVolume = -1f;
        float fadeTime = FadeSeconds;

        // Scene audio calls this instead of creating another music object on every load.
        public static MusicPlayer GetOrCreate()
        {
            if (!Instance)
                new GameObject("Restaurant music").AddComponent<MusicPlayer>();
            return Instance;
        }

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            for (int i = 0; i < decks.Length; i++)
            {
                decks[i] = gameObject.AddComponent<AudioSource>();
                decks[i].playOnAwake = false;
                decks[i].loop = true;
                decks[i].spatialBlend = 0;
                decks[i].volume = 0;
            }
        }

        // GameSession also sends Changed while its timer ticks. An unchanged request must
        // do nothing, or it would repeatedly restart the song and never finish its fade.
        public void SetTrack(AudioClip clip, float volume)
        {
            volume = Mathf.Clamp01(volume);
            if (clip == CurrentClip && Mathf.Approximately(volume, requestedVolume))
                return;

            int next = -1;
            if (clip)
            {
                // A quick pause, retry or return can ask for a song that is still fading out.
                // Reuse that playing source so the same song never plays twice over itself.
                for (int i = 0; i < decks.Length; i++)
                    if (decks[i].clip == clip && decks[i].isPlaying)
                        next = i;

                if (next < 0)
                {
                    next = decks[0].volume <= decks[1].volume ? 0 : 1;
                    decks[next].Stop();
                    decks[next].clip = clip;
                    decks[next].volume = 0;
                    decks[next].Play();
                }
            }

            // Start from the volumes that are audible now, not from the start of an old fade.
            // This also handles a menu change made halfway through the previous transition.
            for (int i = 0; i < decks.Length; i++)
            {
                fromVolume[i] = decks[i].volume;
                toVolume[i] = i == next ? volume : 0;
            }
            CurrentClip = clip;
            requestedVolume = volume;
            fadeTime = 0;
        }

        // Unscaled time lets fades finish even if gameplay is paused. The master volume
        // and mute controls still affect both songs through AudioListener.volume.
        void Update()
        {
            if (fadeTime >= FadeSeconds)
                return;
            fadeTime = Mathf.Min(FadeSeconds, fadeTime + Time.unscaledDeltaTime);
            float blend = Mathf.SmoothStep(0, 1, fadeTime / FadeSeconds);
            for (int i = 0; i < decks.Length; i++)
            {
                decks[i].volume = Mathf.Lerp(fromVolume[i], toVolume[i], blend);
                if (fadeTime >= FadeSeconds && toVolume[i] == 0)
                    decks[i].Stop();
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
