# Adding the Cozy Cooking music

The requested track is [Cozy Cooking by OSTnull](https://audiomack.com/ostnull/song/cozy-cooking). No copy of that track is included in this project yet.

Audiomack's Download button saves an offline copy inside its app, not an audio file you can import into Unity. See [Audiomack's download explanation](https://audiomack.zendesk.com/hc/en-us/articles/360013834432-How-do-you-download-music-on-Audiomack). I checked the public song and artist pages and did not find a separate creator-provided file download or a game-use license there.

The next step is to obtain the track from the creator with permission to use it in the game, including an exported homework build or an itch.io upload if you plan to share one. A useful message to send yourself is:

> I would like to use Cozy Cooking as looping background music in my student game Monster Kitchen. Could you provide an MP3, OGG or WAV file and let me know the credit and license requirements for including it in a downloadable game?

You do not need a WAV specifically. [Unity supports MP3, OGG and WAV](https://docs.unity.com/en-us/engine/6000.0/manual/audio/files/compatibility), among other formats. Converting an MP3 to WAV does not restore audio quality or grant permission to use it.

Once you have the file:

1. Create an `Assets/MonsterKitchen/Audio/Music` folder in Unity's Project panel and drag the file into it. Keep the creator's license/permission and required credit alongside the project documentation.
2. Open `Assets/MonsterKitchen/Scenes/Title.unity`. Find the object with the **Audio Feedback** component and drag the imported clip into its **Music** field. Start with **Music Volume = 0.3**, then adjust by ear against the sound effects.
3. Repeat the assignment in `Assets/MonsterKitchen/Scenes/Kitchen.unity` and save both scenes. Music loops automatically and follows the game's existing master-volume and mute controls.
4. Play through a loop boundary to check for silence or a click. The current scene-based audio setup restarts the track when switching between the cookbook and kitchen; continuous cross-scene playback can be added later.

If you still need a WAV for another tool, a local FFmpeg installation can convert an authorized source file:

```powershell
ffmpeg -i "cozy-cooking.mp3" -c:a pcm_s16le "cozy-cooking.wav"
```

The built-in cafe ambience and sound effects remain available while the music field is empty.
