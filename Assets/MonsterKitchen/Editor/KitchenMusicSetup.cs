// Assign the supplied songs without rebuilding either scene. The scene builders also call
// Assign so a later rebuild keeps the same music choices.
using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MonsterKitchen.Editor
{
    public static class KitchenMusicSetup
    {
        const string Root = "Assets/MonsterKitchen/Audio/Music/";
        static readonly string[] Files =
        {
            "Lily-Paddling Down the Stream.wav",
            "Jazzy Shop.wav",
            "Quircky Shop.wav"
        };

        // Stream the long stereo recordings instead of decoding the entire songs into memory.
        // Keep their original sample rate, and prepare their small playback buffers on load.
        public static string Configure()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play mode before assigning scene music.");
            var previous = EditorSceneManager.GetActiveScene();
            if (previous.isDirty)
                throw new InvalidOperationException("Save the open scene before assigning music.");
            string previousPath = previous.path;

            foreach (string file in Files)
            {
                var importer = AssetImporter.GetAtPath(Root + file) as AudioImporter;
                if (!importer)
                    throw new InvalidOperationException("Import the supplied recording first: " + file);
                importer.forceToMono = false;
                importer.loadInBackground = true;
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = .85f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }

            foreach (string name in new[] { "Title", "Kitchen" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/MonsterKitchen/Scenes/" + name + ".unity");
                var audio = UnityEngine.Object.FindAnyObjectByType<AudioFeedback>();
                if (!audio)
                    throw new InvalidOperationException(name + " is missing its AudioFeedback component.");
                Assign(audio);
                EditorUtility.SetDirty(audio);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(previousPath))
                EditorSceneManager.OpenScene(previousPath);
            return "Assigned title, gameplay and results music in both scenes. Constant ambience removed.";
        }

        // Both scenes know all three songs. This lets the music survive a scene change
        // while AudioFeedback picks the right song from the new session's state.
        public static void Assign(AudioFeedback audio)
        {
            audio.TitleMusic = Load(Files[0]);
            audio.GameplayMusic = Load(Files[1]);
            audio.ResultsMusic = Load(Files[2]);
            audio.MusicVolume = .3f;
        }

        static AudioClip Load(string file)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + file);
            if (!clip)
                throw new InvalidOperationException("Missing music: " + file);
            return clip;
        }
    }
}
