**How the main features work**

The source is divided by responsibility so that the cooking rule does not also need to know how a menu button is drawn.

**Movement and collisions.** PlayerChef reads the keyboard every Update. It converts the two-dimensional input into the camera's directions projected onto the floor, normalizes diagonals, and calls CharacterController.Move. Walls and counters block this motion. Nearby station trigger volumes register when the chef enters or leaves; targeting chooses one station and displays its action.

**Pickup and placement.** PlayerChef holds at most one FoodItem. KitchenStation holds at most one. Before transferring, the station verifies that the destination can accept the dish. Only then does it clear one reference, set the other and reparent the visual. A failed action changes neither reference. This is a reusable inventory/container pattern.

**Cooking.** FoodItem stores accumulated HeatSeconds. Only an occupied, active hotplate calls AddHeat(deltaTime). KitchenRules.HeatAt maps the accumulated time to Raw, Cooked, Charred or Ash. Picking the dish up does not reset its timer. RebuildVisual chooses the matching imported model or ash representation when its state changes.

**Customers and orders.** GameSession creates fresh OrderTicket objects from an authored deck. Each active ticket has a free service slot and its own patience clock. TryServe checks food kind and state. It resolves the matching ticket exactly once, updates counters and checks the ending. Different desired heat states create the recovery mechanic without guessing whether the player made a mistake intentionally.

**Customer walking routes.** KitchenWorld turns the scene's entrance, two aisles, service spots and exit into waypoint lists. GuestMotion moves toward one point at a time with MoveTowards, turns toward its travel direction, and advances to the next point when it arrives. This is sufficient for three fixed service positions; the scene does not need a navigation mesh. The incoming and outgoing lanes differ so a departing guest does not walk through the kitchen. A small proximity check makes later guests yield at crossings. Arrivals are spaced at the doorway so two simultaneously issued tickets do not produce overlapping characters.

**Connecting a visual action to game rules.** A ticket's AtCounter flag stays false during the entrance walk. GameSession does not reduce that guest's patience or accept a meal until GuestMotion reaches the final waypoint and sets it true. A successful service raises OrderResolved; the world starts a celebration and then the exit route. The order system never needs to know individual animation bones or route coordinates.

**Pause and restart.** Every gameplay subsystem checks GameSession.IsRunning. Pausing stops movement, cooking, patience and arrivals while UI stays responsive. ResetRequested asks the world to clear held food, station contents and customers before the session issues a new first ticket.

**Textboxes and UI.** KitchenUI.ShowMessage(text) is a general temporary message presenter. Rules provide text; the UI decides its appearance and display duration. The same separation works for dialogue, warnings or instructions in other games. Order cards read live tickets but do not complete orders themselves.

**Paper UI and reusable menus.** KitchenUI builds a screen-space uGUI Canvas. CanvasScaler keeps the layout proportional, while RectTransform anchors keep tickets in the top-left and counters in the bottom corners. KitchenPaperImage overrides Image.OnPopulateMesh to draw a rounded or torn paper outline. TextMeshPro uses the bundled Fredoka atlas, so the UI does not depend on an installed system font. Choice creates a selectable label and wires mouse clicks and keyboard activation to the same Action. Changing presentation does not change the order or cooking rules.

**Editor startup.** KitchenPlayEntry sets EditorSceneManager.playModeStartScene to Title. You can leave the kitchen open for editing and still press Play to test the normal cookbook entry point. The build scene list also places Title first.

**Music and sound.** AudioFeedback keeps effects, cafe ambience and optional music on separate AudioSources. Music is a looping two-dimensional source. Master volume and mute use AudioListener.volume, so they affect all three. The Music field stays empty until an authorized track file is supplied; the import steps are in MUSIC_IMPORT.md.

**The cookbook.** CookbookSceneBuilder creates real paper meshes, cover, rings, tabletop and props. CookbookMenu creates a World Space Canvas above the pages. Buttons are physically aligned to the paper, so Unity's camera gives them the same perspective as the book. Keyboard and mouse actions call the same functions. Changing the menu page only replaces its lettering and options.

**Animation.** KitchenAssetSetup imports the matching Medium rig and builds locomotion/interaction controllers. PlayerChef supplies Speed and Carrying parameters and an Interact trigger. An upper-body mask allows a holding pose while legs continue walking. The carried food uses a stable attachment point instead of loose physics.

Customers use Walking_A, Idle_B, Waving and Cheering from the supplied KayKit pack. Root motion is disabled: the route owns position, while the clip animates the skeleton in place. Speed chooses idle or walk. Wave and Cheer are triggers with automatic transitions back to idle. The departure delay allows the full celebration to finish before movement resumes.

**Useful first experiments.** Change one cooking threshold, move one counter in the scene, change an order in the deck, and change one message. Play after each change. This makes the effect of each system easier to see than changing movement, timing and layout at once.
