# Music and playback settings

The soundtrack is now imported and connected in both scenes.

| Part of the game | Track | Creator |
| --- | --- | --- |
| Cookbook and pre-shift briefing | Lily-Paddling Down the Stream | Fablefly Music |
| Playing and paused gameplay | Jazzy Shop | Tim Reichert |
| Both ending screens | Quircky Shop | Tim Reichert |

The files are in `Audio/Music`. The original pack spells the last title **Quircky Shop**, so the filename and credit use that spelling. The cookbook lists the tracks and artists as plain text. [CREDITS.txt](Audio/Music/CREDITS.txt) keeps the full creator links and licenses and is also included beside the Windows build.

## How playback works

`AudioFeedback` reads the current GameSession state and requests its song. `MusicPlayer` keeps two AudioSources on a separate object that survives scene changes. One song fades out as the other fades in over 0.65 seconds. Repeated requests for the same song do nothing, so timer updates cannot restart it.

The title music keeps playing through level select and when the kitchen briefing opens. Starting service changes to jazz. Pause lowers the jazz to 70% of its normal level and keeps its place. Resume brings it back up. The existing volume slider and M key affect the music and effects together.

All three songs loop for as long as their screen is open. Reaching the end of a recording does not change scenes or leave the game silent; Unity returns to the start of that song.

**Start cooking** opens the level-select page. Choosing a kitchen rings `door-bell.wav` from the supplied Cozy Cafe SFX pack instead of the regular menu confirmation sound. The bell has a temporary source that survives the scene load, plays once, then removes itself. The bell uses the same master volume and mute setting as the rest of the game. It does not delay loading the kitchen.

The constant cafe ambience has been removed. Action sounds and stove sizzling still play when those actions happen.

## Changing a song later

1. Import the replacement audio and record its creator and license in the credits.
2. Open Title and Kitchen. On each scene's AudioFeedback component, assign **Title Music**, **Gameplay Music** and **Results Music**. The normal **Music Volume** is 0.3.
3. Save both scenes and check the title, briefing, gameplay, pause, ending and retry.
4. If using the scene builders later, update the filenames in `Editor/KitchenMusicSetup.cs` too. The builders call its Assign method so they keep the soundtrack connected.

The supplied WAV files are unchanged. Unity imports stereo Vorbis playback copies at quality 0.85, preserves the 44.1 kHz sample rate and streams them. `KitchenMusicSetup.Configure` applies those settings and assigns the clips without rebuilding the room.

The shop music uses [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/), so it is for noncommercial use. Lily uses [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Keep the credits and links when distributing the game.
