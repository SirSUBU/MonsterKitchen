# Monster Kitchen

Saubdiel Avalos - sa604

Unity 6000.6.5f1, Universal 3D

Cook for a small restaurant full of monsters. Vampires want cooked food; tieflings want it charred. A patty that stayed on the stove too long might still be useful, but the guest who wanted it cooked is still waiting.

## Playing

Open the project in Unity and press Play. The cookbook opens first, even if the Kitchen scene is currently open in the Editor. Choose **Start cooking**, then **Open kitchen** after the briefing.

| Control | What it does |
| --- | --- |
| WASD or arrows | Move the chef |
| E or Space | Take, place, collect, serve or discard at the highlighted station |
| Escape | Pause, go back from a page, or leave the result screen |
| W/S or up/down | Choose a menu option |
| E, Space or Enter | Confirm the selected menu option |
| Mouse | Click menu options and drag the sound slider |
| M, minus, plus | Mute or change the master volume |

Serve **8 guests** before **3 leave hungry**. An order needs the right food and the right heat state. There is no automatic win for surviving a timer.

Picking up food stops its cooking. Setting it on a resting counter also keeps its heat where it was. Returning it to a hotplate continues from that time. A wrong delivery leaves the plate in your hands; ash has to go in the bin.

The receipt stays open when the shift ends. Release the last serving key, then choose **Another shift** or **Main menu** with a new press or click. That keeps the final delivery from accidentally restarting the game.

## Finding things in the project

- [LEARNING_NOTES.md](LEARNING_NOTES.md): a walkthrough of the scripts, including how a plate gets from a crate to a guest.
- [ASSIGNMENT_NOTES.md](ASSIGNMENT_NOTES.md): where the assignment requirements appear in the game and notes for the class GDD.
- [ASSET_SOURCES.md](ASSET_SOURCES.md): source links, credits and what was actually imported.
- [MUSIC_IMPORT.md](MUSIC_IMPORT.md): the current soundtrack, playback behavior and how to change a song.
- [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md): fixes, checks and build results.

The main scenes are [Title](Scenes/Title.unity) and [Kitchen](Scenes/Kitchen.unity). Cameras, props, counters and route markers are ordinary scene objects. You can move them in the Editor and save the scene.

The scripts in **Editor** are setup and test tools. The full scene builders replace their saved scenes when run, so they are not the way to keep a small manual placement change. The scripts in **Scripts** run the actual game.

## Small changes to try

| Change | Start here |
| --- | --- |
| Cooking times or the order sequence | `Scripts/KitchenData.cs` |
| Patience, arrival delays or scoring | The GameSession component on `00 - Game systems` in Kitchen |
| Walking speed | The PlayerChef component on `Witch chef` |
| Station position or approach point | The station and its `Interaction position` child in Kitchen |
| Customer routes | Markers inside `04 - Front of house` |
| Cookbook wording | `Scripts/CookbookMenu.cs` |
| Order slips, prompts or paper menus | `Scripts/KitchenUI.cs` |

Change one thing, play it, then make the next change. That makes it easier to tell which change helped or caused a problem.

## Credits

Game design: **Saubdiel Avalos**. The models and character animations are **KayKit by Kay Lousberg**. Action sounds are **Cozy Cafe SFX by Bramble & Byte**. Music is by **Fablefly Music** and **Tim Reichert**. The paper UI uses **Fredoka by the Fredoka Project Authors**.

The cookbook was inspired by **Undercooked: Cook It Now**. The supplied **Overcooked** references and **Restaurant UI by Nielisson** helped guide the paper menus and results presentation. Their artwork and game code were not imported. Full links and license notes are in the source list above.

The cookbook and briefing use Lily-Paddling Down the Stream, gameplay uses Jazzy Shop, and both endings use Quircky Shop. The constant ambience loop has been removed. Music credits and license links are also available inside the cookbook.
