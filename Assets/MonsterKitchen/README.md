**Monster Kitchen — playable prototype**

Unity 6000.6.5f1 / Universal 3D. Created for Saubdiel Avalos (sa604).

Press Unity's Play button. `KitchenPlayEntry` starts at `Assets/MonsterKitchen/Scenes/Title.unity`, even when you are editing the Kitchen scene. The physical cookbook is the main menu. Choose **Start cooking**; read the briefing, then open the kitchen.

Controls: WASD or arrows move; E or Space picks up, places, collects or serves; Escape pauses; M mutes. Menus support keyboard navigation and pointer selection. Volume settings persist between runs.

Serve eight customers before three leave unhappy. Vampires want cooked food; tieflings want charred food. Match both the food and state on each ticket. Redirecting a burnt dish completes only its compatible customer's order—the original cooked-food order remains open. Ash cannot be served. Heating stops when food leaves a hotplate and resumes from its saved time when returned.

The first two orders introduce cooked and charred food. A mixed pair follows, then overlapping orders for patties and ham. Each shift uses ten authored tickets. There is no timer-only victory.

Customers enter through the diner doorway and follow the front-of-house aisle to a numbered serving spot. Their patience starts on arrival; a ticket showing `...` belongs to a guest still walking in. Guests wave at the counter, celebrate a correct meal, and walk out through the doorway. The chef's workspace remains separated from the dining tables and waiting bench.

**Where to edit**

- `Scenes/Title.unity`: cookbook, table, props, camera and lighting.
- `Scenes/Kitchen.unity`: chef, counters, supplies, hotplates, serving positions and kitchen camera.
- `Scripts/KitchenData.cs`: cooking thresholds and authored order deck.
- `Scripts/GameSession.cs`: patience, spawning, scoring, pause and win/loss logic.
- `Scripts/PlayerChef.cs`: keyboard movement, targeting and hands.
- `Scripts/FoodItem.cs`: a particular dish's accumulated heat and visual state.
- `Scripts/KitchenStation.cs`: validated transfers, cooking, service and discard.
- `Scripts/KitchenWorld.cs`: customer arrivals, departures and reset.
- `Scripts/GuestMotion.cs`: customer waypoints, facing, walking, yielding and reactions.
- `Scripts/CookbookMenu.cs`: text and interactive options on the book's pages.
- `Scripts/KitchenUI.cs`: briefing, tickets, feedback, pause and results.
- `Scripts/KitchenPaperImage.cs`: rounded paper and torn receipt outlines drawn as uGUI geometry.
- `Scripts/KitchenFoodGlyph.cs`: small plate illustrations used by tickets and the held-food display.
- `Scripts/AudioFeedback.cs`: cue names mapped to supplied sounds.
- `Resources/UI`: bundled Fredoka font, its license and the generated TMP atlas.
- `Editor/KitchenPlayEntry.cs`: makes Editor Play begin at the cookbook.
- `Editor/KitchenUIFontSetup.cs`: regenerates the bundled TMP font asset if needed.
- `Editor/KitchenAssetSetup.cs`: URP materials, rig import and animation controllers.
- `Editor/KitchenSceneBuilder.cs`: reproducible kitchen authoring, including the customer area. Its full build also calls CookbookSceneBuilder for the title.
- `Editor/CookbookSceneBuilder.cs`: reproducible physical cookbook scene.
- `Editor/KitchenIntegrationChecks.cs`: Editor-only keyboard and gameplay checks; excluded from player builds.

Scene-builder menu commands regenerate owned scenes. Use them when deliberately rebuilding from code; ordinary camera/object edits can be made directly in the scene and saved.

**Validation**

The rules/session passed 130 assertions in a standalone test harness, including the new arrival/patience rules. Live checks cover keyboard movement, trigger targeting, food ownership, heating, wrong orders, charred recovery, ash discard, pause, restart, boundary collision and both outcomes. Schedule/outcome checks accelerate cooking and use the real acceptance API; they are not a substitute for a human balance/play-feel review. Current live results and standalone-build status are recorded in DEVELOPMENT_STATUS.md.

**Credits and sources**

The paper HUD uses **Fredoka by the Fredoka Project Authors**, under the SIL Open Font License 1.1. The original license is bundled in `Resources/UI/Fredoka-OFL.txt`. The supplied Overcooked screenshots/video and [Restaurant UI by Nielisson](https://nielisson.itch.io/restaurant-ui) informed the paper menus and receipt composition; their artwork was not imported.

Background music is optional. See **MUSIC_IMPORT.md** to obtain the requested OSTnull track and assign it in the Audio Feedback component. Unity accepts MP3 directly; no conversion to WAV is required. The requested track is not bundled yet.

KayKit Restaurant Bits, Series 5 characters and Character Animations by **Kay Lousberg** (CC0):

- https://kaylousberg.itch.io/restaurant-bits
- https://kaylousberg.itch.io/kaykit-series-5
- https://kaylousberg.itch.io/kaykit-character-animations

**Cozy Cafe SFX by Bramble & Byte**, published at https://aquumgifts.itch.io/cozy-cafe-sfx. The downloaded license permits game use and modification. Retain the original license; do not redistribute these sounds as a standalone asset pack.

Cooking/station code reference reviewed: https://github.com/KaganAyten/RestaurantGame3DUnity. The implemented gameplay is newly written for this game; the older project's source and settings were not copied into this Unity project.

Cookbook menu presentation reference: **Undercooked: Cook It Now** by Diego Sevilla, https://diego-sevilla.itch.io/undercooked-cookitnow. The supplied screenshot and GraphicalAssets archive were inspected. The book geometry, table pattern, page lettering and menu behavior here were authored in Unity; no executable code was extracted from UnderCooked_windows.zip, and no texture from GraphicalAssets.zip was imported into the game.

Additional original idea reference supplied by the designer: https://youtu.be/Pc62vgxWQ3g. The video was not independently viewed during implementation. The other supplied cooking-game links remain references; they were not imported as source projects.

The source asset ledger and hash manifest are alongside this file. The project is a first playable prototype with room for your feedback on character size, layout, pacing and presentation.
