# Notes for the Unity assignment

Saubdiel Avalos - sa604

These notes describe what is currently in the game. They can be used when filling the professor's GDD template; they do not replace that template or the personal reflection form.

## Theme: Task Failed Successfully

The basic idea is that a cooking mistake can still help someone else. A vampire wants a cooked patty. If it stays on the hotplate too long, a tiefling who wants charred food can accept it instead. The vampire's order stays open, so the mistake still costs time and the player needs a replacement.

The player can also make charred food on purpose. The game checks what was served, not whether the mistake was intentional. Ash is the limit: it cannot be recovered as a meal and must be discarded.

## Where the requirements appear

| Requirement | In this game | Where to look |
| --- | --- | --- |
| Keyboard or mouse play | Keyboard movement/actions and keyboard or mouse menus | PlayerChef, KitchenUI, CookbookMenu |
| Theme | A charred dish can satisfy a different guest while the original order stays open | KitchenRules.CreateDeck, GameSession.TryServe |
| Character visual states | Idle, walking, carrying and interaction on the animated 3D chef | PlayerChef, KitchenAssetSetup |
| Map collision | Solid walls and counters stop the chef | CharacterController, KitchenSceneBuilder.Boundary/Station |
| Interactive collision | Station reach triggers select a target and show its action | KitchenStation.OnTriggerEnter/Exit, PlayerChef.SelectTarget |
| Continuous movement | Held keys move the chef each frame | PlayerChef.Update/Move |
| Sound | Pickup, placement, cooking, service and menu cues, with separate title, gameplay and results music | AudioFeedback, KitchenStation.Update |
| Action beyond movement | Taking, placing, collecting, serving and discarding food | KitchenStation.Interact |
| Level design | Supplies, hotplates, resting space and serving counters require crossing and managing the room | Kitchen scene, KitchenSceneBuilder |
| Visual design | Matching KayKit models, physical cookbook, paper orders and receipt results | Title/Kitchen scenes, KitchenUI |
| Code documentation | Plain comments explain the functions, state flow, transfers, timers and setup tools | Scripts and Editor folders, LEARNING_NOTES.md |

This project uses 3D character models instead of a 2D sprite sheet. The visual-state requirement is represented by readable skeletal animation. The project uses Unity 6, following the clarification that the version listed in the assignment was a suggestion.

## Keep these details in the GDD

- The win condition is eight served guests; the loss condition is three guests leaving hungry.
- The game has one playable kitchen and a fixed ten-order sequence. The cookbook includes level select, with an editable catalog for adding new kitchen scenes after the jam. The submitted version is still one complete shift.
- Explain the recovery example above and why the first customer's order does not disappear.
- Include the external links from ASSET_SOURCES.md. Distinguish imported assets from games used only as references.
- The soundtrack uses Lily-Paddling Down the Stream by Fablefly Music and Jazzy Shop / Quircky Shop by Tim Reichert. Include the track, creator and license links from the credits. The two shop tracks have a noncommercial license.
- Add the final downloadable project link when preparing the submission. A private GitHub repository alone is not automatically accessible to a grader.

The professor's code guidance asks for meaningful names and enough comments for another student to understand the approach. The comments here explain the purpose of a section, the data it uses, and the less obvious cases instead of repeating every line in English.

Source: the supplied **Unity Game Jam Quest FA26.pdf**, especially pages 3-6. The final GDD and reflection should use the professor's provided forms and the finished game's actual behavior.
