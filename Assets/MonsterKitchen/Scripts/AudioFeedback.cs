// Short sounds stay with the current scene. Music has its own player so a scene change
// can fade between songs instead of cutting one off or starting a second copy.
using UnityEngine;

namespace MonsterKitchen
{
    public sealed class AudioFeedback : MonoBehaviour
    {
        public static AudioFeedback Instance { get; private set; }

        // These two arrays match by position: CueNames[0] uses Clips[0], and so on.
        public string[] CueNames;
        public AudioClip[] Clips;
        [Header("When the shop opens")]
        public AudioClip OpeningBell;
        [Header("Music for each part of the game")]
        public AudioClip TitleMusic;
        public AudioClip GameplayMusic;
        public AudioClip ResultsMusic;
        [Range(0f, 1f)] public float MusicVolume = .3f;

        AudioSource source;
        GameSession session;

        // This source only plays action and menu cues. There is no constant ambience loop.
        // Stove sizzling is still controlled by the stove while it is actually cooking.
        void Awake()
        {
            Instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.volume = .6f;
        }

        // GameSession sets its initial state in Awake. Waiting until Start means the music
        // can choose the correct screen, even if Kitchen is opened directly while testing.
        void Start()
        {
            session = GameSession.Instance;
            if (session)
                session.Changed += RefreshMusic;
            RefreshMusic();
        }

        // The title tune continues through the briefing. Jazz begins when service starts;
        // both ending screens use the results tune. Pause lowers the jazz without restarting it.
        void RefreshMusic()
        {
            RunState state = session ? session.State : RunState.Title;
            AudioClip clip = TitleMusic;
            if (state == RunState.Playing || state == RunState.Paused)
                clip = GameplayMusic;
            else if (state == RunState.Won || state == RunState.Lost)
                clip = ResultsMusic;

            float volume = state == RunState.Paused ? MusicVolume * .7f : MusicVolume;
            MusicPlayer.GetOrCreate().SetTrack(clip, volume);
        }

        // Choosing a level immediately loads its kitchen, which removes this scene's normal
        // effect source. Give the bell its own short-lived object so its tail is not cut off.
        // It plays once, follows the master volume, and removes itself after the clip finishes.
        public static void PlayOpeningBell()
        {
            if (!Instance || !Instance.OpeningBell)
                return;

            var bellObject = new GameObject("Shop opening bell");
            DontDestroyOnLoad(bellObject);
            var bell = bellObject.AddComponent<AudioSource>();
            bell.playOnAwake = false;
            bell.loop = false;
            bell.spatialBlend = 0;
            bell.volume = .6f;
            bell.clip = Instance.OpeningBell;
            bell.Play();
            Destroy(bellObject, bell.clip.length + .1f);
        }

        // A missing or incomplete cue list should stay silent rather than break an interaction.
        public static void Play(string cue)
        {
            if (!Instance || Instance.CueNames == null || Instance.Clips == null)
                return;
            for (int i = 0; i < Instance.CueNames.Length && i < Instance.Clips.Length; i++)
                if (Instance.CueNames[i] == cue && Instance.Clips[i])
                {
                    Instance.source.PlayOneShot(Instance.Clips[i]);
                    return;
                }
        }

        // The next scene gets new action sounds. The separate music object survives so its
        // outgoing song can finish fading and a shared title/briefing track can keep its place.
        void OnDestroy()
        {
            if (session)
                session.Changed -= RefreshMusic;
            if (Instance == this)
                Instance = null;
        }
    }
}
