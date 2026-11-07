# Credits and source notes

This list separates the assets used in the project from the games used as references. The gameplay scripts were written for Monster Kitchen; no baseline game's source code was copied into this project.

## Assets in the game

| Creator and source | What is included | License and credit |
| --- | --- | --- |
| Kay Lousberg — [KayKit Restaurant Bits](https://kaylousberg.itch.io/restaurant-bits) | 144 restaurant FBX models and their texture atlas | CC0, according to the included License.txt. Credit: KayKit by Kay Lousberg. |
| Kay Lousberg — [KayKit Monthly Series 5](https://kaylousberg.itch.io/kaykit-series-5) | Witch, Vampire and Tiefling models, with five supplied texture variants | CC0, according to the included License.txt. |
| Kay Lousberg — [KayKit Character Animations](https://kaylousberg.itch.io/kaykit-character-animations) | Rig_Medium General, MovementBasic, Tools and Simulation animation files | CC0, according to the included License.txt. |
| Bramble & Byte — [Cozy Cafe SFX](https://aquumgifts.itch.io/cozy-cafe-sfx) | 43 short sounds and three supplied ambience loops | The included license allows use and modification in personal or commercial games. It does not allow distributing the sounds as a separate asset pack. Credit: Cozy Cafe SFX by Bramble & Byte. |
| Fredoka Project Authors — [Fredoka](https://github.com/google/fonts/tree/main/ofl/fredoka) | A medium-weight font and its TextMeshPro font atlas | SIL Open Font License 1.1. The license is saved in `Resources/UI/Fredoka-OFL.txt`. |

Keep the included licenses with the project. The source sounds belong inside the game project, not in a separate public sound download.

## Games and art used as references

- [Undercooked: Cook It Now](https://diego-sevilla.itch.io/undercooked-cookitnow) helped guide the cookbook menu. Monster Kitchen's book and page UI were built in Unity; the reference game's textures were not imported.
- The supplied **Overcooked** screenshots and short completion video helped guide the order slips, paper pause menu and end-of-shift receipt. The paper shapes and food illustrations in this project are drawn by `KitchenPaperImage` and `KitchenFoodGlyph`.
- [Restaurant UI by Nielisson](https://nielisson.itch.io/restaurant-ui) was a visual reference. Its paid asset pack has not been purchased or imported.
- [RestaurantGame3DUnity by KaganAyten](https://github.com/KaganAyten/RestaurantGame3DUnity) was inspected for interaction and food-state ideas. It is an older Unity project with an MIT code license. No code or assets were copied; that code license also does not automatically cover every third-party asset in the repository.
- [Undercooked- by kwiruu](https://github.com/kwiruu/Undercooked-) was another mechanics reference. It uses Java/libGDX, and no source or assets were imported.
- The supplied `DisasterChef.rar` contained a compiled Windows game. It was not used as editable source or imported into Unity.

The requested music is [Cozy Cooking by OSTnull](https://audiomack.com/ostnull/song/cozy-cooking). It is not included yet because an importable file and permission for game use have not been supplied. [MUSIC_IMPORT.md](MUSIC_IMPORT.md) explains how to add it when those are available.

## How the imported assets are connected

The supplied models, textures and sounds are under `ThirdParty`. `asset-manifest.json` lists the original archive entries, file sizes and checksums so the imported files can be traced back to their packs. It covers the supplied archives; the later Fredoka import is recorded separately above.

`Editor/KitchenAssetSetup.cs` assigns URP materials, sets up the character rigs and builds the Chef and Customer animation controllers under `Generated`. If the setup needs to be rebuilt, use **Monster Kitchen > Configure supplied assets**. This recreates those generated controllers, so keep any manual controller changes before running it again.

The controllers use **Speed** for walking, **Carrying** for the chef's arm pose, and **Interact**, **Wave** and **Cheer** as animation triggers. Movement comes from the gameplay scripts, so root motion is disabled. The customer animations have been checked in Play mode: walking to the counter, greeting, celebrating, pausing and leaving.

`Editor/KitchenUIFontSetup.cs` builds Fredoka's TextMeshPro font asset. The imported TextMeshPro Essential Resources include their own font license. See [LEARNING_NOTES.md](LEARNING_NOTES.md) for how the runtime scripts use these models, animations and UI assets.
