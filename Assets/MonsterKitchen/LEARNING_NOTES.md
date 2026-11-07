# How the code fits together

Start with `KitchenData`, then `GameSession`, `PlayerChef` and `KitchenStation`. Those four files explain most of the game. The other scripts add the visible food, guests, animation, sound and menus.

The comments in the source explain each method and the less obvious steps inside it. This guide is the larger picture, so you can decide which file to open first.

## A few Unity terms used here

- A **GameObject** is an object in the scene, such as the chef or a counter. Components give it behavior.
- A **MonoBehaviour** is a script component Unity can attach to a GameObject.
- **Awake** gets references ready when the object loads. **Start** runs before its first update. **Update** runs every frame. **OnDestroy** handles cleanup when the object goes away.
- A **Transform** stores position, rotation and scale. A **RectTransform** does that job for UI, with anchors for keeping things at screen edges.
- An **event** lets one script announce a change without knowing every script that needs to react. For example, resolving an order tells both the UI and the visible customer.

Public fields and fields marked `[SerializeField]` can be saved in a scene and edited in the Inspector. A property such as `Served { get; private set; }` lets other scripts read the total while leaving changes to GameSession.

## Following one plate

1. **PlayerChef.Update** reads movement and the interaction key. **SelectTarget** picks a nearby station whose trigger contains the chef.
2. **KitchenStation.Interact** sees that the target is a supply crate. If the hands are empty, it creates a **FoodItem** and calls **Initialize**.
3. **PlayerChef.Take** stores that portion in `Held`. **FoodItem.MoveTo** attaches it to the chef's carry anchor.
4. At a hotplate, the station calls **Release**, stores the returned portion in `Item`, and attaches it to its dish anchor.
5. The hotplate's **Update** calls **FoodItem.AddHeat**. The portion remembers its accumulated seconds and asks **KitchenRules.HeatAt** which state it should be in.
6. Picking it up transfers the same portion back to the chef. No new food is created, and its heat does not reset.
7. At the serving counter, **GameSession.TryServe** checks the waiting order. Only a successful result lets the station remove the held portion.
8. **GameSession.Resolve** updates the score and announces the result. **KitchenWorld** starts the customer's departure, and **KitchenUI** updates the slip or shows the ending.

The main idea is that one object represents the portion for its whole trip. Passing that object between owners is safer than deleting and recreating it every time it moves.

## Movement and collision

**PlayerChef.Move** turns WASD into directions along the camera's view of the floor. The camera is tilted, so its forward direction is flattened onto the floor before moving the chef. Diagonal input is clamped to keep diagonal walking from being faster.

Movement uses `CharacterController.Move`, which stops against the room's solid colliders. The requested distance is multiplied by `Time.deltaTime`, the seconds since the previous frame. This keeps movement reasonably consistent across frame rates.

Stations also have a separate trigger around their approach point. That trigger does not block the chef. **OnTriggerEnter** and **OnTriggerExit** tell the station whether it can be selected. A small preference for the current target keeps the highlight from jumping constantly between two nearby counters.

Moving a station in the scene also moves its children. Check its solid counter, `Interaction position`, trigger and `Dish slot` together if an interaction feels too far away.

## Holding, placing and rejecting food

The chef has one `Held` reference. A hotplate or resting counter has one `Item` reference. Check those before making a transfer:

```csharp
if (!Item && chef.Held)
{
    Item = chef.Release();
    Item.MoveTo(ItemAnchor);
}
```

An empty station can accept the chef's dish. A full station cannot silently replace its dish. Taking food uses the opposite check: the station has an item and the hands are empty.

Serving also checks first. If **TryServe** rejects the kind or doneness, the portion stays in the chef's hands. That is what makes recovery possible: a charred patty rejected by a vampire can still go to a tiefling who ordered it.

## Cooking and visual feedback

`HeatSeconds` belongs to the FoodItem. It counts only time spent on an active hotplate.

| Food | Cooked at | Charred at | Ash at |
| --- | --- | --- | --- |
| Patty | 7 seconds | 12 seconds | 19 seconds |
| Ham | 11 seconds | 17 seconds | 25 seconds |

These are total times. A patty becomes charred at 12 seconds total, not 12 more seconds after becoming cooked.

**HeatAt** checks ash, charred and cooked in that order. That also handles a long update that crosses more than one boundary. **RebuildVisual** changes the imported food model when the state changes. The stove updates its label, progress bar, steam or smoke, and sizzle separately.

To change cooking speed, edit **KitchenRules.Thresholds**. The current stove progress bar also uses the food's ash time in **KitchenStation.Update**, so update that duration too if you change when ash begins.

## Orders, patience and the theme

**KitchenRules.CreateDeck** creates ten fresh tickets for each shift. The first two introduce cooked and charred patties. A mixed pair follows, then the game overlaps patty and ham orders.

An order has an `Id` for the guest and a `Slot` for the serving counter. Slots are numbered 0-2 in code and shown as 1-3 to the player. `Remaining` is the guest's current patience; `Duration` is its starting amount.

**GameSession.IssueOne** assigns a free slot and announces the arrival. **GuestMotion** sets `AtCounter` only when the customer reaches that slot. Patience does not run while they walk in, and they cannot be served from the doorway.

**Resolve** removes one active ticket and records one outcome. Eight successful serves wins; three missed guests loses. Ten tickets are enough to guarantee one of those outcomes. A guest who leaves does not count as served.

The recovery moment does not erase the original order. If the vampire wanted cooked food and the tiefling accepts the charred plate, only the tiefling's ticket is completed. The vampire is still waiting for a replacement.

## Customers and animation

**KitchenWorld.OnArrived** creates the correct model and gives it entry and exit routes built from the scene markers. **GuestMotion.Update** moves toward one waypoint at a time. Separate lanes keep departing customers out of the cooking area.

The Animator uses the supplied KayKit clips. Its `Speed` parameter chooses idle or walking; `Wave` greets the chef and `Cheer` reacts to a successful meal. Root motion is disabled because the route script already controls the character's position.

The chef uses a similar idle/walk setup. A carrying layer uses an arm mask, which means the arms can hold a plate while the legs keep walking. **KitchenAssetSetup.CreateArmsMask** and **CreateController** show how that is assembled in the Editor.

## Screens, pause and restart

The normal flow is:

`Title -> Briefing -> Playing -> Won or Lost`

Pause temporarily changes `Playing` to `Paused`. It does not create another shift. The movement, cooking, patience and arrival code all check `GameSession.IsRunning`. UI uses unscaled time so menus stay responsive while gameplay is stopped.

**RestartShift** clears the old counters and asks **KitchenWorld.ResetWorld** to remove old guests and dishes. It then issues a fresh first order. **ReturnToTitle** leaves the kitchen and loads the cookbook.

Two details matter here:

- **KitchenUI** hides its Canvas immediately on Title. Previously, its fallback selected the briefing for any unhandled state, which briefly showed the start notice while leaving.
- **MenuInputGate** waits for the key or mouse button that opened a screen to be released. Previously, the last E/Space press could serve guest eight and select Another shift in the same frame. The result now has a short opening delay and requires a new choice.

## Textboxes, order slips and buttons

**KitchenUI.ShowMessage** is the temporary note presenter. It sets the label and an expiry time. Gameplay can send a message without drawing UI:

```csharp
GameSession.Instance.ShowMessage("Take the plate off the hotplate.");
```

To reuse that approach in another game, keep the text element, expiry timer and ShowMessage method together. Have the new game's rules call it or send an event. Replace this game's `IsRunning` display check with the conditions that make sense in the new game.

Order slips work differently: **RefreshHUD** reads live tickets every frame. The slip does not own the countdown or decide when an order finishes. It just displays the current values.

**Choice** creates a button and stores the same action for keyboard navigation. **RefreshScreen** decides which page is visible. **KitchenPaperImage** draws the paper outlines, and **KitchenFoodGlyph** draws the small plate illustrations. The bundled TMP font keeps the paper UI consistent across machines.

The cookbook uses a world-space Canvas above the 3D pages. **CookbookMenu.Show** replaces the page lettering and options; **CookbookSceneBuilder** created the saved book meshes and props.

The restaurant tags also use small world-space Canvases and the same rounded font. Each station holds a **StatusLabel** reference to its TMP text. **KitchenStation.Update** changes that text to Raw, Cooked, Charred or Ash while food is on a stove; otherwise it shows the station's short name. The number badges match the order slips' counter numbers. These labels do not receive mouse clicks.

**KitchenSceneBuilder.UpdateRestaurantLettering** restyles these tags without rebuilding the kitchen. The cream backing uses KitchenPaperImage, just like the menus. The stove tags have extra width for Charred. The wall sign stays fixed to the restaurant, while **FaceCamera** keeps station tags facing the player. You can edit the saved tag Canvases directly in the Kitchen scene.

## Sound

**AudioFeedback.Play("pickup")** looks up a named cue and plays its clip once. It checks both cue arrays before reading them, so an incomplete sound setup stays silent instead of throwing an error. The stove owns its sizzle source because it needs to start and stop with cooking. There is no constant ambience loop.

**AudioFeedback.PlayOpeningBell** is a small exception to scene-owned effects. Start cooking loads a new scene right away, so its bell would be cut off if it used the old menu's source. This method creates a temporary object, keeps it alive across the load, plays the bell once, and removes the object after the recording finishes. `GameSession.BeginBriefing` calls it only after accepting a new start, so a repeated start request cannot ring it twice. The cookbook skips its normal confirmation click for this option.

**AudioFeedback.RefreshMusic** chooses a song from the session state. **MusicPlayer** lives on a separate object that survives scene changes. It has two AudioSources: the outgoing song fades down while the incoming song fades up. The same clip is never started twice, and repeated timer notifications do not restart its fade.

The fade uses unscaled time so it works while gameplay is stopped. Pausing lowers the current song without stopping it. The title song continues through the briefing; gameplay and results switch to their assigned songs. This separation is useful in another game too: keep short effects with scene objects, and give continuous music its own lifetime.

Master volume and mute use `AudioListener.volume`. Both menu systems save the same PlayerPrefs keys, so returning to the cookbook keeps the chosen settings. See MUSIC_IMPORT.md for the current tracks, import settings and how to replace one.

## The Editor files

These tools help create or verify the project. They are not part of the built player's gameplay loop.

| File | Use |
| --- | --- |
| KitchenSceneBuilder | Build and connect the kitchen's saved scene objects |
| CookbookSceneBuilder | Build the book, table, props and title camera |
| KitchenAssetSetup | Prepare materials, rigs, clips and animation controllers |
| KitchenUIFontSetup | Create the saved TMP font atlas |
| KitchenMusicSetup | Import the three songs and assign their roles in both scenes |
| KitchenPlayEntry | Start Editor Play at Title even when editing Kitchen |
| KitchenIntegrationChecks | Run keyboard, cooking, route and menu checks against the live game |

Do not run a scene builder just to apply a code edit. Unity recompiles edited C# files itself. A full builder replaces its scene and can replace manual placements that were not added back to the builder.

## Testing a change

For a quick manual pass: start at the book, take food, put it on a stove, pause, resume, serve the right guest, try a wrong delivery, restart, and return to the title. Check both endings when changing order or state logic.

The Editor checks use a temporary virtual keyboard and restore the normal input settings afterward. Some earlier orders are resolved quickly to reach the behavior under test. In particular, **Transitions** uses real E and Space presses for the final meal, because calling the service API alone would miss input accidentally carrying into the result menu.
