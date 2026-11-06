# Monster Kitchen — UI revision, 6 November 2026

## What changed

The kitchen's existing models, lighting, layout and customer area are preserved. The decorative fridge now faces the chef's work area.

- The cookbook's front page contains the game title and menu choices. Extra taglines, keyboard instructions and the service heading were removed.
- Editor Play starts at the cookbook even if Kitchen or SampleScene is open. The build also starts with Title.
- The HUD uses compact paper order slips with large food illustrations, heat-state labels and patience bars. Served/complaint totals, held food and score occupy the bottom corners without a full-width status banner.
- Pause, controls, sound and briefing use cream paper, warm ink and restrained sage/terracotta accents. The bundled Fredoka font keeps the text consistent across machines.
- Results appear as a receipt on a plate over a gingham tablecloth. Values come from the actual completed shift.
- The optional Music field accepts an imported, authorized audio file. The requested OSTnull track is not included; MUSIC_IMPORT.md explains the available route and Unity's supported formats.

## Verification for this revision

- **60 live Unity gameplay checks passed:** keyboard movement, station triggers, food ownership, cooking, wrong deliveries, charred recovery, ash disposal, pause, restart, arrival gating, walls and both win/loss outcomes.
- **6 cookbook navigation checks passed:** instructions, back, starting the Kitchen scene, briefing, starting service and pausing.
- **25 paper-UI checks passed:** controls and sound navigation, volume and mute, restart, HUD pause button, resume, a real completed-shift receipt and bundled font/atlas references.
- The sound slider received a subsequent layout correction. **8 focused geometry checks passed** for fill and handle bounds at 0%, 25%, 75% and 100%; the corrected sound page was visually inspected.
- Main UI captures were inspected at 1600 x 900 from the QHD Game view. A real 1024 x 768 Game view was also used to check controls, sound and the held-food HUD. The original Game view setting was restored afterward.
- The saved Kitchen scene has zero missing scripts. The saved fridge yaw is 0 degrees, and the Editor start scene is Title.
- No gameplay/UI exceptions or warnings appeared during the final live UI check.

The prior revision's 130 rule/session assertions and 12 customer-route/animation checks also passed. Those systems were not changed by this UI pass. The live gameplay checks accelerate heat and resolve some orders through the real service API; they are not a full natural-speed difficulty playthrough.

## Windows build

Build `build_627f2d57544f` **succeeded** in approximately 21 seconds, with **0 errors**, at `Builds/Windows/MonsterKitchen.exe`.

Unity reported 499 package shader warnings, including inference/Sentis shaders and stripped debug shaders. No custom gameplay compilation errors were reported. This revision was visually and interactively verified in the Unity Editor; the new standalone player was built but not separately played through.

Useful local captures in the ignored Screenshots folder:

- cookbook-minimal.png
- kitchen-paper-hud.png
- pause-paper.png
- briefing-paper.png
- controls-paper-1024x768.png
- hud-held-1024x768.png
- sound-paper.png
- shift-receipt.png

## Editing and source control

The Editor is left outside Play mode with Title open. Press Play to begin at the cookbook. Scene objects remain directly editable. The scene builders are optional authoring tools: running a full builder deliberately replaces its owned scenes, so do not use it to preserve unrecorded manual scene edits.

Source, imported assets, bundled licenses, fonts and required project settings belong in the project's private Git repository. Local screenshots, recovery scenes, caches and builds are ignored. Existing unrelated local changes are left untouched.

Earlier source/scenes were backed up in the Codex workspace before the UI pass. The game is ready for the designer's next presentation and play-feel review.
