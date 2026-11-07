# Adding the background music

The chosen track is [Cozy Cooking by OSTnull](https://audiomack.com/ostnull/song/cozy-cooking). It is not in the project yet. The game currently uses cafe ambience and action sounds from the supplied Cozy Cafe pack.

Audiomack's Download option makes a copy for offline listening inside its app; it does not give you a normal audio file to drag into Unity. The public track and artist pages did not provide a separate file download or game-use license when checked. [Audiomack explains its offline downloads here.](https://audiomack.zendesk.com/hc/en-us/articles/360013834432-How-do-you-download-music-on-Audiomack)

Ask the creator for an MP3, OGG or WAV and permission to include it in a downloadable student game. Also ask what credit they want. A WAV is optional: [Unity accepts all three formats](https://docs.unity.com/en-us/engine/6000.0/manual/audio/files/compatibility). Converting an MP3 to WAV will not improve the original sound quality.

## Once you have the file

1. Create `Assets/MonsterKitchen/Audio/Music` in Unity's Project panel and drag the audio file into it. Add the creator's credit and permission to the source notes.
2. Open `Scenes/Title.unity`. Find the object with the **Audio Feedback** component and drag the clip into **Music**. Start with **Music Volume** at **0.3**.
3. Open `Scenes/Kitchen.unity` and assign the same clip to its Audio Feedback component. Save both scenes.
4. Press Play and listen against the cooking sounds. The music loops automatically and uses the existing volume and mute controls. Check the end of the loop for a gap or click.

Each scene has its own audio player, so the track currently starts over when switching between the cookbook and kitchen. Leaving Music empty is fine; the existing sounds still work.

If another tool specifically needs a WAV, FFmpeg can convert a source file you already have:

```powershell
ffmpeg -i "cozy-cooking.mp3" -c:a pcm_s16le "cozy-cooking.wav"
```

This conversion is not needed just to import the track into Unity.
