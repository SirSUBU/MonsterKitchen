# Supplied assets staged for Monster Kitchen

Game models, animation and audio were selected from the user's Downloads archives. The UI also bundles the open-licensed Fredoka font described below. No baseline project code was copied.

| Source | Selected content | Permission evidence |
| --- | --- | --- |
| [KayKit Restaurant Bits](https://kaylousberg.itch.io/restaurant-bits) | All 144 FBX models from `Assets/fbx (unity)`, restaurant atlas | Included License.txt: CC0; credit appreciated |
| [KayKit Monthly Series 5](https://kaylousberg.itch.io/kaykit-series-5) | Witch, Vampire, Tiefling; five supplied texture variants | Included License.txt: CC0; credit appreciated |
| [KayKit Character Animations](https://kaylousberg.itch.io/kaykit-character-animations) | Rig_Medium General, MovementBasic, Tools, Simulation FBX | Included License.txt: CC0; credit appreciated |
| [Cozy Cafe SFX](https://aquumgifts.itch.io/cozy-cafe-sfx) | 43 short SFX, three separately supplied ambience loops | Included license permits personal/commercial projects and modification; no standalone asset redistribution; optional credit: Cozy Cafe SFX by Bramble & Byte |
| [Fredoka](https://github.com/google/fonts/tree/main/ofl/fredoka) | Medium-weight static TTF and a Unity TextMeshPro SDF atlas | Fredoka Project Authors, SIL Open Font License 1.1; license at `Resources/UI/Fredoka-OFL.txt` |

Keep the supplied license files and attribution with the project. A distributable game can use Cozy Cafe audio; do not publish this staging folder as a standalone downloadable asset pack.

## Baseline/reference status

- [KaganAyten/RestaurantGame3DUnity](https://github.com/KaganAyten/RestaurantGame3DUnity): previously inspected Unity 2020 project with an MIT code license. Reference for interaction and food-state patterns only; no source or third-party assets copied here. Its MIT code license is not treated as a blanket license for embedded third-party resources.
- [kwiruu/Undercooked-](https://github.com/kwiruu/Undercooked-): Java/libGDX reference rather than a Unity baseline. No source or assets copied.
- [Undercooked: Cook It Now](https://diego-sevilla.itch.io/undercooked-cookitnow): cookbook presentation reference. The book meshes and page UI here are original Unity work; no supplied reference textures were imported.
- Overcooked screenshots and the supplied 2.97-second kitchen-complete video: visual references for compact order tickets, a paper pause menu and a receipt on a plate. Original paper geometry, food glyphs, colors and typography were authored for this project.
- [Restaurant UI by Nielisson](https://nielisson.itch.io/restaurant-ui): visual reference only. The paid archive was not purchased or imported.
- `DisasterChef.rar`: archive inventory showed a compiled Windows game. It was not used as source code or imported into Unity.
- [Cozy Cooking by OSTnull](https://audiomack.com/ostnull/song/cozy-cooking): requested music reference. No track file or game-use license was found or imported. See `MUSIC_IMPORT.md` for the next steps.

## Import and validation

Copy ThirdParty to `Assets/MonsterKitchen/ThirdParty` and Editor/KitchenAssetSetup.cs to `Assets/MonsterKitchen/Editor`. Run `Monster Kitchen > Configure supplied assets` or call `MonsterKitchen.Editor.KitchenAssetSetup.Configure()`.

The helper generates explicit URP Lit atlas materials and generic-rig controllers under `Assets/MonsterKitchen/Generated`. Re-running it regenerates only its Chef and Customer controllers. It reports actual Unity model bounds, avatar validity and selected clips.

The source GLB metadata confirms matching Rig_Medium/root and arm bone names for all three characters. That is preliminary compatibility evidence; animation, orientation, root motion and carry masking must still be checked in Unity Play Mode. The FBX source units are respected, not guessed; use the reported Unity model bounds when placing or sizing objects.

Controller parameters: Speed (float; greater than 0.05 walks), Carrying (bool; optional arm layer on chef), Interact, Wave and Cheer (triggers). Runtime Animators set applyRootMotion=false. Customers use Walking_A, Idle_B, Waving and Cheering from the supplied pack. Live checks confirmed those walking, greeting and celebration states, facing, pause behavior and departure. The source rigs include handslot.l and handslot.r attachment transforms.

`asset-manifest.json` records every copied entry, archive filename, byte count and SHA-256. `stage_assets.py` is the reproducible selector and writes only beneath this staging directory.

The archive manifest covers the supplied game assets, not the later font import. Fredoka was obtained from the Google Fonts repository and instantiated at weight 500, width 100 using fontTools. `Editor/KitchenUIFontSetup.cs` creates its SDF font asset through Unity APIs. TextMeshPro Essential Resources were imported with Unity's official importer and include their own font license.
