# Level select and code review — 6 November 2026

Start cooking now opens a level-select page inside the cookbook. The jam version lists one playable level, The little kitchen. Choosing it rings the door bell and opens its briefing; turning pages does not start the shift or restart the title music.

The menu reads KitchenLevel assets from a KitchenLevelCatalog. More published entries create extra pages automatically. Drafts, empty slots and duplicates are skipped, and a missing build scene is shown as closed. GameSession loads the chosen scene by its full path instead of assuming that every level is named Kitchen. Retry resets the scene already being played. The scene builders keep the catalog and additional build entries when rebuilding the original scenes.

[ADDING_LEVELS.md](ADDING_LEVELS.md) walks through making a new scene and level asset, connecting the scene's GameSession, and updating the build list. Recipe rules and the current win/loss goals remain shared; individual scenes can have their own room layouts and pacing.

The comments in all 26 project-owned C# scripts were reviewed. The new level scripts explain how their data, menu choices and scene loading fit together. Extra explanations cover page input, scene startup, customer route offsets, paper geometry and the cookbook mesh. The customer route arrays were split into named incoming and outgoing routes. The stove progress bar now reads its duration from the recipe thresholds, so changing a recipe later will also update its bar. There are no XML summary tags.

**197 live Editor checks passed:** 20 for level selection, 55 for music, 7 for cookbook navigation, 60 for gameplay, 25 for the paper UI, 12 for customer routes and animations, and 18 for transitions. This includes the earlier eighth-serve restart and briefing-flash bugs. The checks use real game components and input, with some setup steps speeding up cooking and orders; they are not a full natural-speed standalone playthrough.

A temporary second scene confirmed that choosing another kitchen loads that scene, uses its own briefing name and stays there on retry. The test scene was removed afterward, and the release catalog and build list were checked again: one playable kitchen, plus Title. The build-list helper also preserved the extra scene while it existed. Neither saved scene has missing scripts, and both have their level references assigned.

The selector was visually checked at 1600 x 900, and a UI raycast confirmed its kitchen button receives clicks. It uses the existing cookbook lettering and paper. The local preview is `Screenshots/level-select.png`; the complete check summary is `Screenshots/level-integration-suite.txt`.

Windows build `build_dca29c4c50a5` succeeded in about 14 seconds with **0 errors** and the same 499 package shader warnings as before. The updated player is at `Builds/Windows/MonsterKitchen.exe`. The Editor is left outside Play mode at Title. The build contains only Title and the existing kitchen.

## Previous revision: shop opening and plain credits

Start cooking now rings the supplied cafe door bell instead of the regular menu confirmation click. The bell survives the change from cookbook to kitchen, finishes its short tail, and removes its temporary object. A repeated start request cannot play another bell once the briefing has opened. Other menu and cooking sounds work as before.

The music credits now show only track names and artists, with one Back option. There are no website or license buttons on that page. The complete credits, source links and license details stay in the project's credit file and beside the Windows build.

The existing music fades and loops are still used. The title tune continues through the briefing; starting service fades to jazz, and both endings fade to Quircky Shop. A track that reaches its end loops until the game requests another one.

**54 live audio checks passed.** These include the actual Enter press on Start cooking, the bell continuing after the scene load, rejection of a repeated start, and removal of the finished bell object. The checks also inspect the middle of music fades, verify all three loop points, and cover pause, mute, both endings, retry and repeated scene changes. There were no runtime console warnings or errors.

The new credits page was visually checked at 1600 x 900. It has only text credits and a Back button; both Back actions return to the main cookbook. The local capture is `Screenshots/music-credits-plain.png`, and the check result is in `Screenshots/opening-music-checks.txt`. As in the previous revision, setup steps seek near song endings and resolve orders quickly. These are targeted Editor checks, not a full natural-speed standalone playthrough.

Windows build `build_2b5190b6767c` succeeded in about 12 seconds with **0 errors** and the same 499 package shader warnings as before. The updated player is at `Builds/Windows/MonsterKitchen.exe`. The Editor is left outside Play mode at Title.

## Previous revision: soundtrack and ambience

The cookbook and briefing now play Lily-Paddling Down the Stream by Fablefly Music. Jazzy Shop by Tim Reichert starts when service begins, and his Quircky Shop plays on both ending screens. The constant kitchen ambience has been removed. Cooking, serving, pickup and menu sounds still play when those actions happen.

Music uses two sources on one object that survives scene changes. This lets songs fade into each other without an abrupt cut. Pausing lowers the jazz a little and keeps its place; resuming brings it back up. The existing volume and mute controls affect music too. Asking for the same song again does not restart it, even though the session sends updates while its timer runs. An incomplete sound-effect list also stays silent instead of causing a null-reference error.

The original WAV files are unchanged. Unity imports them as streaming stereo Vorbis. The creator's pack ReadMe and a source manifest are kept with the music. The cookbook has a separate music credits page with artist and license links, and the written guides now explain these settings. The shop tracks use CC BY-NC 4.0, so they are for noncommercial use; Lily uses CC BY 4.0.

## Checks for this revision

- **46 music checks passed:** the correct track on each screen, all three loop points, pause/resume continuity, mute and volume restoration, both endings, retry, rapid scene changes, one music player, one listener, and no constant ambience source.
- The missing sound-effect array case was checked too. The game continued without throwing an exception.
- The credits and music credits pages were visually checked at 1600 x 900. Back navigation returned to the credits and then to the cookbook menu.
- The title source produced a nonzero audio output signal. Source playback and loop positions were checked in the Editor; this was not a full listening pass through every recording.
- The live music checks finished with no console errors or warnings. The existing gameplay rules, customer routes and kitchen layout were not changed.

These checks use the real game components. Some setup steps resolve or expire orders through the session code to reach both endings quickly, and the loop checks seek near each track's end. They do not replace a natural-speed playthrough. Local captures and the check result are in `Screenshots/music-credits.png`, `Screenshots/credits-with-music-link.png` and `Screenshots/music-checks.txt`.

Windows build `build_0a87ad042101` succeeded in about 20 seconds with **0 errors**. Unity reported the same 499 package shader warnings as the previous build. The player is at `Builds/Windows/MonsterKitchen.exe`, with `MUSIC-CREDITS.txt` and the creator's pack ReadMe beside it. This build was compiled successfully; the playback checks above were done in the Editor, not in a separate standalone playthrough.

The Editor is left outside Play mode with Title open. No scene rebuild is needed to use the soundtrack. The project's existing unrelated local changes are not part of this revision.

## Previous revision: restaurant lettering

The restaurant now uses the menus' rounded Fredoka lettering. Cream station tags replace the dark, all-caps labels. The three serving counters have number badges that match the order slips. Equipment uses short names: Patties, Ham, Stove, Counter and Bin.

The wall has one Monster Kitchen sign, without the old tagline. It sits below the awning so its lettering is fully visible. The entrance uses a matching Open sign. Cooking labels use dark ink, green for Cooked, and terracotta for Charred or Ash.

The saved scene has ten connected TMP station labels, no remaining legacy TextMesh labels, and no missing scripts. The room's colliders and their positions were checked before and after the lettering update and are unchanged. Raw, Cooked, Charred and Ash were checked against their displayed text and available label space. The widescreen preview was inspected in Play mode, with no live console errors or warnings during that check.

The local preview is `Screenshots/restaurant-lettering-preview.png`. Cooking states were set up for this screenshot so both Cooked and Charred can be seen together. This was a focused lettering check; the previous revision's full gameplay checks are recorded below.

Windows build `build_13729d3e4014` succeeded in about 15 seconds with 0 errors. Unity reported the same 499 package shader warnings as the preceding build. The updated player is at `Builds/Windows/MonsterKitchen.exe`; the visual checks above were done in the Editor.

## Previous revision: ending and title fixes

## Leaving the kitchen and finishing a shift

Returning to the cookbook briefly showed the starting notice because the kitchen UI used its briefing screen as a fallback for the Title state. It now hides the whole kitchen Canvas as soon as the session returns to Title. Title is handled explicitly, so it cannot fall through to the briefing.

Serving guest eight did reach the win state, but the menu read that same E or Space press as **Another shift**. That made the ending disappear immediately. The menu now waits for the old input to be released and for a fresh choice. The receipt also gets a short opening delay. The cookbook uses the same input check so holding Enter while leaving the kitchen cannot immediately start it again.

## Code and documentation

All 21 game and Editor scripts now use ordinary comments. The XML summary tags are gone, and dense one-line methods have been split into readable blocks. Comments explain the main functions, food ownership, timers, state changes, movement, collision, animations and the setup tools.

[LEARNING_NOTES.md](LEARNING_NOTES.md) follows the main systems with examples, including how to reuse the textbox pattern. [ASSIGNMENT_NOTES.md](ASSIGNMENT_NOTES.md) connects the finished features to the professor's requirements and gathers facts for the class GDD. It is not a replacement for the provided GDD or reflection forms. The controls, credits and music instructions have also been updated.

The kitchen layout and UI artwork were not redesigned in this pass.

## Checks for this revision

- **60 gameplay checks passed:** movement, station triggers, dish transfers, heating, wrong deliveries, recovery, disposal, pause, restart, room boundaries, winning and losing.
- **6 cookbook navigation checks passed:** help, back, entering Kitchen, its briefing, starting service and pausing.
- **25 paper menu checks passed:** controls, sound, mute, restart, HUD pause, resume and the completed-shift receipt.
- **18 transition checks passed:** finishing with E and Space, keeping the receipt open after release, deliberately retrying, hiding the outgoing kitchen UI and staying at the cookbook while Enter is held.
- Both ending screenshots were inspected. They show the completed receipt with eight served guests.
- The live checks finished with no console errors or warnings. All 21 own C# files were checked for leftover XML documentation tags, and the local documentation links were checked too.

The checks use real keyboard input for movement and menu actions. To reach later scenarios promptly, some setup steps accelerate cooking or resolve earlier orders through the serving code. The final E and Space deliveries go through the actual chef interaction, including holding each key down. These checks cover behavior; they are not a full natural-speed difficulty playthrough.

The earlier version's 130 session/rule checks and 12 customer animation/route checks also passed. The rules, routes and supplied animations were not changed in this pass.

The two ending captures are saved locally as `Screenshots/win-final-e.png` and `Screenshots/win-final-space.png`.

## Windows build

Build `build_6335934661cf` succeeded in about 17 seconds with **0 errors**. The updated player is at `Builds/Windows/MonsterKitchen.exe`.

Unity reported 499 warnings, the same count as the previous build. These include package shader warnings about stripped debug shaders. The live checks and screenshots above were taken in the Editor; this new standalone build was compiled but was not separately played through.

Open the project and press Play to begin at the cookbook. The Editor is left outside Play mode with Title open. The scene builders are optional tools that replace their saved scenes, so they should not be run just to apply a script edit.

The source project and its guides are tracked in the private Git repository. Builds, screenshots, Unity caches and recovery scenes stay local. Existing unrelated local changes were left out of this revision.
