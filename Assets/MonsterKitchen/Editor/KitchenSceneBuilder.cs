// Editor tool that creates the kitchen's saved scene objects and connects their components.
// The coordinates here describe the floor, counters, props and customer aisles.
// A full build replaces the owned scenes. It is not needed for normal Play mode or small scene edits.
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

namespace MonsterKitchen.Editor
{
    public static class KitchenSceneBuilder
    {
        const string Root = "Assets/MonsterKitchen";
        static Material teal, dark, cream, gold, wood, tileA, tileB, mint, coral, ash, steam, diningTile;
        static Transform architecture, furniture, details;
        // Set up the supplied assets, build Kitchen and the cookbook, and put them in the build scene list.
        // Title is first so a standalone game begins at the cookbook.
        [MenuItem("MonsterKitchen/Build playable scenes")]
        public static string Build()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play mode before authoring scenes.");
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Prefabs");
            AssetDatabase.Refresh();
            string assetReport = KitchenAssetSetup.Configure();
            MakeMaterials();
            BuildScene(false);
            CookbookSceneBuilder.Build();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(Root + "/Scenes/Title.unity", true),
                new EditorBuildSettingsScene(Root + "/Scenes/Kitchen.unity", true)
            };
            PlayerSettings.companyName = "Saubdiel Avalos";
            PlayerSettings.productName = "Monster Kitchen";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            QualitySettings.vSyncCount = 1;
            AssetDatabase.SaveAssets();
            return "Created Title and Kitchen scenes. " + assetReport;
        }

        // Create the room and its camera, then add the shared systems, workstations, chef and customer area.
        // These become ordinary saved scene objects that can also be inspected and moved in Unity.
        static void BuildScene(bool titleScene)
        {
            var previous = EditorSceneManager.GetActiveScene();
            if (previous.isDirty && !string.IsNullOrEmpty(previous.path))
                EditorSceneManager.SaveScene(previous);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = titleScene ? "Title" : "Kitchen";
            architecture = new GameObject("01 - Kitchen architecture").transform;
            furniture = new GameObject("02 - Workstations").transform;
            details = new GameObject("03 - Restaurant details").transform;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ColorHex("B9D5D2");
            RenderSettings.ambientEquatorColor = ColorHex("8FA6A8");
            RenderSettings.ambientGroundColor = ColorHex("4A5058");
            RenderSettings.ambientIntensity = 1;
            RenderSettings.fog = false;
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9.1f;
            camera.transform.position = new Vector3(4.6f, 19.5f, -17.8f);
            camera.transform.LookAt(new Vector3(.45f, .4f, 1.5f));
            if (titleScene)
                camera.transform.position -= camera.transform.right * 3.9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ColorHex("142D35");
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 80;
            cameraObject.AddComponent<AudioListener>();
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var light = new GameObject("Warm afternoon light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(49, -32, 0);
            light.color = new Color(1, .91f, .77f);
            light.intensity = 1.8f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = .55f;
            light.shadowBias = .04f;
            var fill = new GameObject("Cool room fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(35, 150, 0);
            fill.color = new Color(.61f, .80f, .86f);
            fill.intensity = .45f;
            fill.shadows = LightShadows.None;
            // Keep the rule, audio, world and UI components together in the hierarchy.
            // The references assigned here are saved with the scene.
            var systems = new GameObject("00 - Game systems");
            var assets = systems.AddComponent<KitchenAssets>();
            assets.PattyRaw = Model("food_ingredient_burger_uncooked");
            assets.PattyCooked = Model("food_ingredient_burger_cooked");
            assets.PattyCharred = Model("food_ingredient_burger_trash");
            assets.HamRaw = Model("food_ingredient_ham");
            assets.HamCooked = Model("food_ingredient_ham_cooked");
            assets.HamCharred = Model("food_ingredient_ham_trash");
            assets.Plate = Model("plate");
            assets.Vampire = Model("Vampire");
            assets.Tiefling = Model("Tiefling");
            assets.GuestController = KitchenAssetSetup.CustomerController;
            assets.AshMaterial = ash;
            assets.SteamMaterial = steam;
            assets.MintMaterial = mint;
            assets.CoralMaterial = coral;
            systems.AddComponent<GameSession>().IsTitleScene = titleScene;
            var sound = systems.AddComponent<AudioFeedback>();
            sound.CueNames = new[]
            {
                "pickup",
                "place",
                "ready",
                "burn",
                "order",
                "serve",
                "unhappy",
                "click",
                "confirm",
                "back",
                "error"
            };
            string[] soundFiles =
            {
                "cup-place",
                "plate-place",
                "oven-timer",
                "oven-close",
                "new-order",
                "serve-dish",
                "customer-unhappy",
                "ui-click",
                "ui-confirm",
                "ui-back",
                "ui-error"
            };
            sound.Clips = soundFiles.Select(Audio).ToArray();
            sound.Ambience = Audio("ambience-kitchen");
            systems.AddComponent<KitchenUI>();
            var world = systems.AddComponent<KitchenWorld>();
            world.GameCamera = camera;
            // The chef keeps the same compact workspace. Three extra rows belong
            // to the diner: counter waiting spots, two aisles, then dining tables.
            Cube("Floating diner foundation", new Vector3(0, -.38f, 1.5f), new Vector3(14.8f, .7f, 14.6f), dark, architecture, true);
            Cube("Brass foundation trim", new Vector3(0, -.045f, 1.5f), new Vector3(14.45f, .09f, 14.25f), gold, architecture, false);
            for (int x = 0; x < 14; x++)
                for (int z = 0; z < 14; z++)
                    Cube("Floor tile " + x + "-" + z, new Vector3(x - 6.5f, .015f, z - 5), new Vector3(.98f, .045f, .98f), (x + z) % 2 == 0 ? (z >= 9 ? diningTile : tileA) : tileB, architecture, false);
            Cube("Walkable floor", new Vector3(0, -.02f, 1.5f), new Vector3(14, .08f, 14), tileA, architecture, true);
            Cube("Back wall", new Vector3(0, 1.1f, 8.5f), new Vector3(14.4f, 2.2f, .25f), teal, architecture, true);
            Cube("Back wall cap", new Vector3(0, 2.24f, 8.5f), new Vector3(14.5f, .12f, .36f), wood, architecture, false);
            Cube("Left wall", new Vector3(-7.1f, .8f, 1.5f), new Vector3(.25f, 1.6f, 14.2f), teal, architecture, true);
            // Leave a real opening in the east wall for the entrance and exit.
            Cube("Right kitchen wall", new Vector3(7.1f, .35f, -.65f), new Vector3(.25f, .7f, 9.9f), teal, architecture, true);
            Cube("Right diner wall", new Vector3(7.1f, .65f, 7.85f), new Vector3(.25f, 1.3f, 1.3f), teal, architecture, true);
            Cube("Front low wall", new Vector3(0, .22f, -5.6f), new Vector3(14.4f, .44f, .25f), teal, architecture, true);
            // Invisible upper boundaries retain the chef even where the scenic wall is low.
            Boundary("South boundary", new Vector3(0, 1, -5.75f), new Vector3(14.5f, 2, .2f));
            Boundary("East kitchen boundary", new Vector3(7.25f, 1, -1.5f), new Vector3(.2f, 2, 8));
            // The serving rail separates customer motion from the chef's work area.
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 2.6f;
                var station = Station("SERVICE " + (i + 1), StationType.Serve, new Vector3(x, 0, 2.6f), "kitchencounter_straight_A", 2.05f, new Vector3(0, 0, -1.1f));
                station.ServiceSlot = i;
                station.ItemAnchor.localPosition = new Vector3(0, 1.05f, 0);
                Visual("plate", station.transform, new Vector3(.55f, 1.05f, 0), .48f);
            }

            Cube("Customer rail left", new Vector3(-5.4f, .6f, 2.6f), new Vector3(3.25f, 1.2f, .8f), teal, furniture, true);
            Cube("Customer rail right", new Vector3(5.4f, .6f, 2.6f), new Vector3(3.25f, 1.2f, .8f), teal, furniture, true);
            Cube("Rail gap left", new Vector3(-1.3f, .5f, 2.6f), new Vector3(.6f, 1, .7f), wood, furniture, true);
            Cube("Rail gap right", new Vector3(1.3f, .5f, 2.6f), new Vector3(.6f, 1, .7f), wood, furniture, true);
            world.GuestSpots = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var point = new GameObject("Guest position " + (i + 1)).transform;
                point.SetParent(details, false);
                point.position = new Vector3((i - 1) * 2.6f, 0, 3.95f);
                world.GuestSpots[i] = point;
                Cube("Guest waiting mat " + (i + 1), point.position + Vector3.up * .07f, new Vector3(1.3f, .04f, .8f), i % 2 == 0 ? coral : mint, details, false);
            }

            BuildCustomerArea(world);
            // Ingredients are on the left, cooking is on the right, and spare
            // counters sit in the middle. Crossing the room is part of managing orders.
            var patty = Station("PATTIES", StationType.Supply, new Vector3(-5.65f, 0, .45f), "kitchencounter_straight_A", 1.8f, new Vector3(1.05f, 0, 0));
            patty.SupplyFood = FoodKind.Patty;
            Visual("crate_steak", patty.transform, new Vector3(0, 1.05f, 0), 1.1f);
            var ham = Station("HAM", StationType.Supply, new Vector3(-5.65f, 0, -2.1f), "kitchencounter_straight_A", 1.8f, new Vector3(1.05f, 0, 0));
            ham.SupplyFood = FoodKind.Ham;
            Visual("crate_ham", ham.transform, new Vector3(0, 1.05f, 0), 1.1f);
            var stoveA = Station("HOTPLATE A", StationType.Hotplate, new Vector3(5.4f, 0, .45f), "stove_single", 1.8f, new Vector3(-1.1f, 0, 0));
            var stoveB = Station("HOTPLATE B", StationType.Hotplate, new Vector3(5.4f, 0, -2.1f), "stove_single", 1.8f, new Vector3(-1.1f, 0, 0));
            DecorateStove(stoveA);
            DecorateStove(stoveB);
            Station("REST 1", StationType.Counter, new Vector3(-.95f, 0, -.8f), "kitchentable_A", 1.65f, new Vector3(0, 0, -1.0f));
            Station("REST 2", StationType.Counter, new Vector3(.95f, 0, -.8f), "kitchentable_A", 1.65f, new Vector3(0, 0, -1.0f));
            var bin = Station("DISCARD", StationType.Trash, new Vector3(-5.7f, 0, -4.3f), null, 1.2f, new Vector3(1, 0, 0));
            Cube("Teal discard bin", new Vector3(-5.7f, .5f, -4.3f), new Vector3(1.05f, 1, .9f), teal, furniture, false);
            Cube("Bin opening", new Vector3(-5.7f, 1.015f, -4.3f), new Vector3(.88f, .05f, .73f), dark, furniture, false);
            Cube("Bin rim", new Vector3(-5.7f, 1.02f, -4.65f), new Vector3(1.1f, .1f, .12f), gold, furniture, false);
            // Utilities stay in the kitchen; diners no longer walk behind a fridge.
            var fridge = Visual("fridge_A_decorated", details, new Vector3(-3.1f, 0, -4.55f), 2.05f, true, 0);
            AddSolidDecoration(fridge);
            var sink = Visual("kitchencounter_sink", details, new Vector3(5.3f, 0, -4.25f), 1.55f);
            AddSolidDecoration(sink);
            Visual("dishrack_plates", details, new Vector3(5.3f, .82f, -4.25f), .88f);
            Visual("extractorhood", details, new Vector3(6.65f, 2.6f, .5f), 1.5f, false, -90);
            Visual("jar_A_large", details, new Vector3(-4.3f, 1.22f, 2.6f), .35f);
            Visual("jar_B_large", details, new Vector3(-4.9f, 1.22f, 2.6f), .4f);
            Visual("menu", details, new Vector3(4.7f, 1.2f, 2.65f), .5f);
            Sign("Monster Kitchen", new Vector3(0, 1.48f, 8.25f), 5.5f, .86f);
            for (int i = 0; i < 11; i++)
            {
                float x = -5 + i;
                Cube("Awning stripe " + i, new Vector3(x, 2.75f, 8.0f), new Vector3(.99f, .18f, .9f), i % 2 == 0 ? cream : coral, details, false);
            }

            for (int i = 0; i < 3; i++)
            {
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lamp.name = "Warm pendant " + i;
                lamp.transform.SetParent(details, false);
                lamp.transform.position = new Vector3((i - 1) * 3.6f, 3.25f, 7.85f);
                lamp.transform.localScale = new Vector3(.35f, .3f, .35f);
                UnityEngine.Object.DestroyImmediate(lamp.GetComponent<Collider>());
                lamp.GetComponent<Renderer>().sharedMaterial = gold;
                Cube("Pendant wire " + i, new Vector3((i - 1) * 3.6f, 3.6f, 7.85f), new Vector3(.025f, .5f, .025f), dark, details, false);
            }

            // Put movement/collision on the root and the animated model beneath it.
            // The carrying anchor follows the root without giving the plate loose physics.
            var chefRoot = new GameObject("Witch chef");
            chefRoot.transform.position = new Vector3(0, .08f, -3.4f);
            var chefVisual = Visual("Witch", chefRoot.transform, Vector3.zero, 1.85f, true);
            var controller = chefRoot.AddComponent<CharacterController>();
            controller.height = 1.65f;
            controller.radius = .31f;
            controller.center = new Vector3(0, .85f, 0);
            controller.stepOffset = .15f;
            controller.skinWidth = .035f;
            controller.slopeLimit = 45;
            var chef = chefRoot.AddComponent<PlayerChef>();
            chef.VisualRoot = chefVisual.transform;
            chef.Animator = chefVisual.GetComponent<Animator>();
            if (chef.Animator)
            {
                chef.Animator.runtimeAnimatorController = KitchenAssetSetup.ChefController;
                chef.Animator.applyRootMotion = false;
            }

            var hands = new GameObject("Held dish anchor").transform;
            hands.SetParent(chefRoot.transform, false);
            hands.localPosition = new Vector3(0, 1.0f, .58f);
            chef.CarryAnchor = hands;
            var shadow = Cylinder("Chef ground marker", new Vector3(0, .085f, -3.4f), new Vector3(.75f, .012f, .75f), mint, chefRoot.transform);
            shadow.transform.localPosition = new Vector3(0, .01f, 0);
            PrefabUtility.SaveAsPrefabAsset(chefRoot, Root + "/Prefabs/WitchChef.prefab");
            if (titleScene)
            {
                Visual("Vampire", details, new Vector3(-1.4f, 0, 3.9f), 1.8f, true, 180);
                Visual("Tiefling", details, new Vector3(1.4f, 0, 3.9f), 1.8f, true, 180);
            }

            // Use the same lettering pass as an existing scene, after food kinds and slots are assigned.
            if (!titleScene)
                UpdateRestaurantLettering();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, Root + "/Scenes/" + scene.name + ".unity");
        }

        // Place the dining furniture and doorway, then mark separate entry and exit aisles.
        // KitchenWorld reads these route markers when it creates a customer.
        static void BuildCustomerArea(KitchenWorld world)
        {
            var area = new GameObject("04 - Front of house").transform;
            world.Entrance = RoutePoint("Entrance - outside", new Vector3(9.25f, 0, 5.05f), area);
            world.ArrivalAisle = RoutePoint("Arrival aisle", new Vector3(5.1f, 0, 5.05f), area);
            world.DepartureAisle = RoutePoint("Departure aisle", new Vector3(5.1f, 0, 6.35f), area);
            world.Exit = RoutePoint("Exit - outside", new Vector3(9.25f, 0, 6.35f), area);
            Cube("Front doorstep", new Vector3(8.15f, -.18f, 5.75f), new Vector3(2.7f, .35f, 3.1f), dark, area, false);
            Cube("Welcome mat", new Vector3(7.85f, .035f, 5.75f), new Vector3(1.8f, .035f, 2.5f), coral, area, false);
            Cube("Entrance jamb near", new Vector3(7.1f, 1.4f, 4.3f), new Vector3(.32f, 2.8f, .3f), wood, area, false);
            Cube("Entrance jamb far", new Vector3(7.1f, 1.4f, 7.2f), new Vector3(.32f, 2.8f, .3f), wood, area, false);
            Cube("Entrance lintel", new Vector3(7.1f, 2.85f, 5.75f), new Vector3(.38f, .25f, 3.2f), wood, area, false);
            var welcome = Label("Open", new Vector3(7.1f, 3.25f, 5.75f), .14f, ColorHex("49352B"), area);
            welcome.transform.parent.name = "Door sign";
            BackLabel(welcome, 1.15f);
            // Table and chair footprints stay north of both walking lanes.
            foreach (float x in new[]
            {
                -4.6f,
                0f,
                4.25f
            }

            )
            {
                Visual("table_round_A_small_decorated", area, new Vector3(x, 0, 7.55f), 1.15f);
                Visual("chair_A", area, new Vector3(x - .9f, 0, 7.55f), .85f, true, 90);
                Visual("chair_A", area, new Vector3(x + .9f, 0, 7.55f), .85f, true, -90);
            }

            // A low bench makes the left corner read as a waiting area.
            Cube("Waiting bench seat", new Vector3(-6.2f, .5f, 5.65f), new Vector3(.7f, .18f, 2.6f), coral, area, false);
            Cube("Waiting bench back", new Vector3(-6.6f, .85f, 5.65f), new Vector3(.16f, .8f, 2.6f), wood, area, false);
            for (int i = 0; i < 2; i++)
                Cube("Bench leg", new Vector3(-6.2f, .22f, 4.65f + i * 2), new Vector3(.55f, .44f, .16f), wood, area, false);
        }

        // Create a named position marker. It is not a visible prop; it supplies a waypoint to the route.
        static Transform RoutePoint(string name, Vector3 position, Transform parent)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.position = position;
            return point;
        }

        // Fit a simple BoxCollider around a large decorative object such as the fridge.
        // Keep it separate from the imported mesh so collision stays simple and predictable.
        static void AddSolidDecoration(GameObject model)
        {
            var bounds = KitchenAssets.BoundsOf(model);
            var collider = new GameObject(model.name + " collision");
            collider.transform.SetParent(furniture, false);
            collider.transform.position = bounds.center;
            collider.AddComponent<BoxCollider>().size = bounds.size;
        }

        // Cream paper and warm ink match the order slips. Dimensions are given in world units,
        // then converted to UI units because the little Canvas is scaled to one hundredth size.
        static void BackLabel(TMP_Text label, float width, float height = .52f, bool round = false)
        {
            var canvas = (RectTransform)label.transform.parent;
            canvas.sizeDelta = new Vector2(width * 100, height * 100);
            float corner = round ? height * 50 : 12;
            TagPaper(canvas, "Soft shadow", new Vector2(1, -3), canvas.sizeDelta, ColorHex("806C59"), corner);
            TagPaper(canvas, "Paper edge", Vector2.zero, canvas.sizeDelta, ColorHex("C7AB88"), corner);
            TagPaper(canvas, "Cream paper", Vector2.zero, canvas.sizeDelta - Vector2.one * 3, ColorHex("FFF9E8"), corner - 1);
            label.transform.SetAsLastSibling();
        }

        // Reuse a tag's existing shapes when restyling it, so running the tool twice does not add copies.
        static void TagPaper(RectTransform parent, string name, Vector2 position, Vector2 size, Color color, float corner)
        {
            var child = parent.Find(name) as RectTransform;
            if (!child)
            {
                child = new GameObject(name, typeof(RectTransform), typeof(KitchenPaperImage)).GetComponent<RectTransform>();
                child.SetParent(parent, false);
            }
            child.anchorMin = child.anchorMax = child.pivot = new Vector2(.5f, .5f);
            child.anchoredPosition = position;
            child.sizeDelta = size;
            var paper = child.GetComponent<KitchenPaperImage>();
            paper.Corner = corner;
            paper.color = color;
            paper.raycastTarget = false;
            child.SetAsLastSibling();
        }

        // Create both a solid counter and a separate interaction trigger around its approach point.
        // The solid box blocks walking; the trigger tells PlayerChef this station is within reach.
        // Also connect the dish anchor, highlight and label used by KitchenStation.
        static KitchenStation Station(string name, StationType type, Vector3 position, string model, float width, Vector3 approach)
        {
            var go = new GameObject(name);
            go.transform.SetParent(furniture, false);
            go.transform.position = position;
            if (model != null)
                Visual(model, go.transform, Vector3.zero, width);
            var solid = new GameObject("Solid counter");
            solid.transform.SetParent(go.transform, false);
            var box = solid.AddComponent<BoxCollider>();
            box.center = new Vector3(0, .55f, 0);
            box.size = new Vector3(width * .93f, 1.1f, type == StationType.Serve ? .95f : width * .8f);
            var station = go.AddComponent<KitchenStation>();
            station.Type = type;
            station.DisplayName = name;
            station.ItemAnchor = new GameObject("Dish slot").transform;
            station.ItemAnchor.SetParent(go.transform, false);
            station.ItemAnchor.localPosition = new Vector3(0, 1.08f, 0);
            station.ReachPoint = new GameObject("Interaction position").transform;
            station.ReachPoint.SetParent(go.transform, false);
            station.ReachPoint.localPosition = approach;
            station.ReachRadius = 1.35f;
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = station.ReachRadius;
            trigger.center = approach + Vector3.up * .65f;
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var highlight = Cube("Interaction highlight", position + new Vector3(0, .08f, 0), new Vector3(width + .12f, .035f, width * .8f + .12f), mint, go.transform, false);
            station.Highlight = highlight;
            highlight.SetActive(false);
            station.StatusLabel = Label(name, position + new Vector3(0, 1.75f, 0), .14f, cream.color, go.transform);
            BackLabel(station.StatusLabel, 1.65f);
            return station;
        }

        // Position the plate above the actual stove surface, then attach its heat bar, steam and sizzle.
        // KitchenStation controls these effects during play; this method only sets them up.
        static void DecorateStove(KitchenStation station)
        {
            // Keep the plate above the supplied stove model's actual upper surface.
            Renderer[] renderers = station.GetComponentsInChildren<Renderer>();
            float top = renderers.Where(r => !r.transform.IsChildOf(station.StatusLabel.transform.parent)).Max(r => r.bounds.max.y);
            station.ItemAnchor.position = new Vector3(station.transform.position.x, Mathf.Max(1.05f, top + .04f), station.transform.position.z);
            station.StatusLabel.transform.parent.position = station.ItemAnchor.position + Vector3.up * .75f;
            var bar = new GameObject("Heat progress");
            bar.transform.SetParent(station.transform, false);
            bar.transform.position = station.ItemAnchor.position + Vector3.up * .48f;
            bar.AddComponent<FaceCamera>();
            Cube("Progress track", bar.transform.position, new Vector3(1, .085f, .02f), dark, bar.transform, false);
            var fill = Cube("Progress fill", bar.transform.position, new Vector3(1, .06f, .025f), gold, bar.transform, false);
            // A separate scaling transform keeps the geometry's intended thickness.
            var fillPivot = new GameObject("Progress amount").transform;
            fillPivot.SetParent(bar.transform, false);
            fill.transform.SetParent(fillPivot, true);
            station.ProgressFill = fillPivot;
            bar.SetActive(false);
            var steamObject = new GameObject("Cooking steam");
            steamObject.transform.SetParent(station.ItemAnchor, false);
            steamObject.transform.localPosition = Vector3.up * .15f;
            steamObject.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var particles = steamObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = 1.2f;
            main.startSpeed = .5f;
            main.startSize = .17f;
            main.maxParticles = 20;
            var emission = particles.emission;
            emission.rateOverTime = 6;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = .18f;
            shape.angle = 12;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(.5f, 0), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = steam;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            station.Steam = particles;
            var sizzle = station.gameObject.AddComponent<AudioSource>();
            sizzle.clip = Audio("sizzle");
            sizzle.loop = true;
            sizzle.playOnAwake = false;
            sizzle.spatialBlend = 0;
            sizzle.volume = .09f;
            station.Sizzle = sizzle;
        }

        // Use the supplied-model lookup shared with the asset setup tool.
        static GameObject Model(string name) => KitchenAssetSetup.Model(name);
        // Place an imported prefab under a separate positioning anchor. Normalize its visible size
        // without changing the source asset, then rotate the anchor to face the work area.
        static GameObject Visual(string model, Transform parent, Vector3 localPosition, float size, bool byHeight = false, float yaw = 0)
        {
            GameObject source = Model(model);
            if (!source)
                throw new Exception("Missing model: " + model);
            var anchor = new GameObject(model + " display").transform;
            anchor.SetParent(parent, false);
            anchor.localPosition = localPosition;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source, anchor);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var b = KitchenAssets.BoundsOf(go);
            float dimension = byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            if (dimension > .0001f)
                go.transform.localScale *= size / dimension;
            b = KitchenAssets.BoundsOf(go);
            go.transform.position += anchor.position - new Vector3(b.center.x, b.min.y, b.center.z);
            anchor.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (var c in go.GetComponentsInChildren<Collider>())
                UnityEngine.Object.DestroyImmediate(c);
            return go;
        }

        // Find a cafe sound by its exact filename, without its extension.
        static AudioClip Audio(string name)
        {
            return AssetDatabase.FindAssets(name + " t:AudioClip", new[] { Root + "/ThirdParty/CozyCafeSFX" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileNameWithoutExtension(p) == name).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).FirstOrDefault();
        }

        // Create a box-shaped scene piece. Keep its collider only when it should block movement.
        static GameObject Cube(string name, Vector3 position, Vector3 size, Material material, Transform parent, bool collision)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision)
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // Create a small decorative cylinder without a collider.
        static GameObject Cylinder(string name, Vector3 position, Vector3 size, Material material, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // Add an invisible solid box where the chef must not leave the room.
        static void Boundary(string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(architecture, false);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = size;
        }

        // Put rounded TMP lettering on a small world-space Canvas. It belongs to the station,
        // so moving that station in the Editor also moves its tag. It never intercepts clicks.
        static TMP_Text Label(string text, Vector3 position, float size, Color color, Transform parent)
        {
            var go = new GameObject(text + " tag", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * .01f;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 52);
            var label = new GameObject("Lettering", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(go.transform, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8, 2);
            label.rectTransform.offsetMax = new Vector2(-8, -2);
            label.font = Resources.Load<TMP_FontAsset>("UI/KitchenRounded");
            label.text = text;
            label.fontSize = size * 260;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = color;
            label.raycastTarget = false;
            go.AddComponent<FaceCamera>();
            if (Camera.main)
                go.transform.rotation = Camera.main.transform.rotation;
            return label;
        }

        // Unlike a station label, this sign stays attached to its wall instead of facing the camera.
        static void Sign(string text, Vector3 position, float width, float height)
        {
            var label = Label(text, position, .22f, ColorHex("49352B"), details);
            var canvas = label.transform.parent;
            canvas.name = "Restaurant sign";
            BackLabel(label, width, height);
            UnityEngine.Object.DestroyImmediate(canvas.GetComponent<FaceCamera>());
            canvas.rotation = Quaternion.identity;
        }

        // Update only the restaurant's lettering. Counters, colliders, routes and props keep
        // their saved positions. This is also called after building a brand-new Kitchen scene.
        public static string UpdateRestaurantLettering()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.name != "Kitchen")
                throw new InvalidOperationException("Open Kitchen outside Play mode before updating its lettering.");
            var stations = UnityEngine.Object.FindObjectsByType<KitchenStation>();
            foreach (var station in stations)
            {
                var old = station.GetComponentInChildren<TextMesh>();
                if (!station.StatusLabel)
                {
                    Vector3 position = old ? old.transform.position : station.transform.position + Vector3.up * 1.75f;
                    station.StatusLabel = Label("", position, .14f, ColorHex("49352B"), station.transform);
                }
                // Connect the new label before removing the old text object and its dark backing.
                if (old)
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
                // The objects keep their descriptive Inspector names. These shorter captions
                // are the words the player sees, with enough room for the longest cooking state.
                float width;
                switch (station.Type)
                {
                    case StationType.Serve:
                        station.DisplayName = (station.ServiceSlot + 1).ToString();
                        width = .64f;
                        break;
                    case StationType.Supply:
                        station.DisplayName = station.SupplyFood == FoodKind.Ham ? "Ham" : "Patties";
                        width = station.SupplyFood == FoodKind.Ham ? 1.05f : 1.5f;
                        break;
                    case StationType.Hotplate:
                        station.DisplayName = "Stove";
                        width = 1.65f;
                        break;
                    case StationType.Counter:
                        station.DisplayName = "Counter";
                        width = 1.65f;
                        break;
                    default:
                        station.DisplayName = "Bin";
                        width = 1.05f;
                        break;
                }
                var label = station.StatusLabel;
                label.text = station.DisplayName;
                label.color = ColorHex("49352B");
                label.fontSize = station.Type == StationType.Serve ? 40 : 36;
                label.transform.parent.name = "Station tag";
                bool number = station.Type == StationType.Serve;
                BackLabel(label, width, number ? .64f : .52f, number);
                if (number)
                    label.transform.parent.position = station.transform.position + new Vector3(0, 1.42f, -.47f);
                EditorUtility.SetDirty(station);
            }

            // The numbered badges replace the duplicate SERVICE labels and counter numbers.
            // A single wall sign replaces the old title/tagline pair.
            var oldSigns = UnityEngine.Object.FindObjectsByType<TextMesh>().Where(t =>
                t.text == "OPEN" || t.text == "MONSTER KITCHEN" || t.text == "GOOD FOOD. STRANGE TASTES."
                || t.name.StartsWith("Serving position ", StringComparison.Ordinal)).ToArray();
            foreach (var old in oldSigns)
            {
                if (old.text == "OPEN")
                {
                    var open = Label("Open", old.transform.position, .14f, ColorHex("49352B"), old.transform.parent);
                    open.transform.parent.name = "Door sign";
                    BackLabel(open, 1.15f);
                }
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>().Where(t => t.name == "Menu sign backing").ToArray())
                UnityEngine.Object.DestroyImmediate(t.gameObject);
            details = GameObject.Find("03 - Restaurant details").transform;
            if (!details.Find("Restaurant sign"))
                Sign("Monster Kitchen", new Vector3(0, 1.48f, 8.25f), 5.5f, .86f);
            EditorSceneManager.MarkSceneDirty(scene);
            return "Updated " + stations.Length + " station tags and restaurant signs.";
        }

        // Turn a written color value into a Unity Color.
        static Color ColorHex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        // Reuse a saved material when possible so rebuilding does not leave duplicate materials.
        static Material Material(string name, string hex, float smooth = .2f)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = ColorHex(hex);
            material.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Define the shared room colors and the transparent particle material used for stove steam.
        static void MakeMaterials()
        {
            teal = Material("Diner teal", "386468");
            dark = Material("Deep ink", "142D35");
            cream = Material("Warm cream", "FFF1D6");
            gold = Material("Honey brass", "DCAE64", .35f);
            wood = Material("Walnut trim", "6D493A");
            tileA = Material("Sage tile", "C0C8B2");
            tileB = Material("Cream tile", "DFDDCA");
            diningTile = Material("Diner terracotta tile", "C99879");
            mint = Material("Interaction mint", "A4DFBB");
            coral = Material("Coral accents", "DD866B");
            ash = Material("Cold ash", "393D42");
            steam = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Steam.mat");
            if (!steam)
            {
                steam = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                steam.SetFloat("_Surface", 1);
                steam.SetFloat("_Blend", 0);
                steam.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha);
                steam.SetFloat("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                steam.SetFloat("_ZWrite", 0);
                steam.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                steam.renderQueue = 3000;
                AssetDatabase.CreateAsset(steam, Root + "/Materials/Steam.mat");
            }
        }
    }
}
