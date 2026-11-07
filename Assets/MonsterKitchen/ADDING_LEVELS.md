# Adding another kitchen later

The jam version still has one level: **The little kitchen**. Start cooking opens the cookbook's level-select page, and choosing that kitchen opens its briefing. The door bell rings when the kitchen is chosen. The menu can list more kitchens without being rewritten.

## What to edit

| Item | What it does |
| --- | --- |
| `Levels/LittleKitchen.asset` | The first kitchen's name, short description and scene path |
| `Levels/KitchenLevels.asset` | The ordered list shown in the cookbook |
| `Scenes/Kitchen.unity` | The actual room, stations, chef, camera and customer routes |
| `GameSession.Level` in a kitchen scene | Identifies that scene's level and supplies its briefing title |

The two level asset types are ScriptableObjects. That just means they are saved data files you can select and edit in the Inspector. They are not objects that need to be placed in the room.

## Make a second kitchen

1. Duplicate `Scenes/Kitchen.unity` and give the copy its own name, such as `GardenKitchen.unity`. Open the copy and change its layout. Keep the scene's game systems, camera and chef. Adjust the station triggers and customer route markers along with the furniture.
2. In the Project window, use **Create > Monster Kitchen > Level**. Save the new asset in `Assets/MonsterKitchen/Levels`. Give it a display name and a short description. Set **Scene Path** to its project path, for example `Assets/MonsterKitchen/Scenes/GardenKitchen.unity`. Turn **Is Published** off while it is unfinished.
3. In the new scene, find the GameSession component on `00 - Game systems`. Assign the new asset to **Level**, and keep **Is Title Scene** off. Save the scene. This makes the briefing use the correct kitchen name. Patience, arrival spacing and scoring settings on that same component belong to this scene, so they can differ from the original kitchen.
4. Open `Levels/KitchenLevels.asset` and add the new asset to its **Levels** array. The array order becomes the menu order. An unpublished entry stays hidden, so it can sit in the catalog while you work on it.
5. When it is ready, turn **Is Published** on and use **MonsterKitchen > Update level build scenes**. This keeps Title first and adds published kitchen scenes without removing other build entries. Check the scene list in Build Profiles before exporting.
6. Press Play, choose the new kitchen and try its briefing, service, pause, restart and both endings. Check that customers can reach all three serving spots and leave through the door. Restart should keep the same kitchen; Main menu should return to the cookbook.

The book shows up to three kitchens per page. Previous and Next appear only when needed. There is no fixed maximum number of menu entries, and you do not need to add fake locked levels to reserve space. Empty slots and duplicate asset references are ignored. A published entry whose scene is missing from the build list shows as closed, with no enter button.

## What remains shared

All kitchens currently use the same recipes, ten-order sequence, eight-served goal, three-complaint limit and three serving slots from `KitchenData.cs`. A new room can change its layout and pacing while keeping those rules. Different recipes or goals would be a separate gameplay change; the level selector does not invent them automatically.

The existing music setup can be copied with the scene. Title music continues through the selector and briefing, jazz starts with service, and results use Quircky Shop. If a later kitchen should use different music, assign its AudioFeedback clips and update the credits.

Use **Update level build scenes** when adding a new level. **Configure level selection** reconnects the original Title and Kitchen scenes to their default assets. **Build playable scenes** recreates the original room and cookbook, so it is not needed for an ordinary level edit. The builders preserve the level catalog and additional build-scene entries.

## Where the code lives

`KitchenLevel` stores one menu entry and checks whether its scene can load. `KitchenLevelCatalog` returns the published entries in order. `CookbookMenu.ShowLevels` turns those entries into page text and buttons. `GameSession.BeginBriefing` loads the chosen scene only after that scene passes the check.

Retry does not reload a hard-coded Kitchen scene. It resets the chef, plates, guests and totals in the scene already being played. That is why a future level can keep its own layout and settings when the player retries.
