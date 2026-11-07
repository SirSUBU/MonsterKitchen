// Editor setup for the supplied KayKit models, animation clips and cafe audio.
// It prepares URP materials and animation controllers inside this project's Generated folder.
// It does not download assets or run during gameplay.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MonsterKitchen.Editor
{
    public static class KitchenAssetSetup
    {
        // Keep generated files inside MonsterKitchen so this setup does not alter unrelated project assets.
        public const string Root = "Assets/MonsterKitchen";
        public const string ThirdParty = Root + "/ThirdParty";
        public const string Generated = Root + "/Generated";
        public const string ChefControllerPath = Generated + "/Animation/Chef.controller";
        public const string CustomerControllerPath = Generated + "/Animation/Customer.controller";
        public static RuntimeAnimatorController ChefController => AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ChefControllerPath);
        public static RuntimeAnimatorController CustomerController => AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CustomerControllerPath);

        // Expose the setup command in Unity's menu and print its report in the Console.
        [MenuItem("Monster Kitchen/Configure supplied assets")]
        public static void ConfigureMenu() => Debug.Log(Configure());
        // Import textures and character rigs before building the animation controllers that refer to them.
        // The report lists missing clips or unusual model sizes to check before using the scene builder.
        public static string Configure()
        {
            if (!AssetDatabase.IsValidFolder(ThirdParty))
                throw new DirectoryNotFoundException("Copy staged ThirdParty into " + ThirdParty + " first.");
            var report = new StringBuilder("Monster Kitchen asset setup\n");
            EnsureFolder(Generated + "/Materials");
            EnsureFolder(Generated + "/Animation");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP/Lit is unavailable. Open this project with Universal 3D/URP configured.");
            var paths = AssetDatabase.FindAssets("", new[] { ThirdParty }).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p).ToArray();
            foreach (var path in paths.Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                ConfigureTexture(path);
            var restaurant = MaterialFor("Restaurant", shader, ThirdParty + "/KayKitRestaurant/Textures/restaurantbits_texture.png");
            var witch = MaterialFor("Witch", shader, ThirdParty + "/KayKitSeries5/Witch/witch_texture_A.png");
            var vampire = MaterialFor("Vampire", shader, ThirdParty + "/KayKitSeries5/Vampire/vampire_texture.png");
            var tiefling = MaterialFor("Tiefling", shader, ThirdParty + "/KayKitSeries5/Tiefling/tiefling_texture.png");
            var models = paths.Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)).ToArray();
            // Character avatars must exist before animation/controller inspection.
            foreach (var path in models.Where(p => !p.Contains("KayKitAnimations/")))
            {
                var character = path.Contains("KayKitSeries5/");
                var material = restaurant;
                if (character)
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    material = name == "Witch" ? witch : name == "Vampire" ? vampire : tiefling;
                }

                ConfigureModel(path, character, false, material);
            }

            foreach (var path in models.Where(p => p.Contains("KayKitAnimations/")))
                ConfigureModel(path, true, true, null);
            var audioPaths = paths.Where(p => p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)).ToArray();
            foreach (var path in audioPaths)
                ConfigureAudio(path);
            var idle = Clip("Idle_A");
            var walk = Clip("Walking_A");
            var interact = Clip("Interact") ?? Clip("PickUp");
            var holding = Clip("Holding_A");
            var cheer = Clip("Cheering");
            var mask = CreateArmsMask(Model("Witch"), report);
            CreateController(ChefControllerPath, idle, walk, interact, cheer, holding, mask);
            CreateController(CustomerControllerPath, Clip("Idle_B") ?? idle, walk, interact, cheer, null, null);
            AssetDatabase.SaveAssets();
            report.AppendLine($"Configured {models.Length} FBX files, {audioPaths.Length} audio clips and four URP atlas materials.");
            report.AppendLine("Chef parameters: Speed (float), Carrying (bool), Interact (trigger), Cheer (trigger).");
            report.AppendLine("Customer parameters: Speed (float), Carrying (bool), Interact (trigger), Cheer (trigger).");
            foreach (var label in new[]
            {
                "Idle_A",
                "Walking_A",
                "Interact",
                "Holding_A",
                "Cheering"
            }

            )
            {
                var clip = Clip(label);
                report.AppendLine(label + ": " + (clip ? $"{clip.length:0.###}s, {AssetDatabase.GetAssetPath(clip)}" : "MISSING"));
            }

            foreach (var name in new[]
            {
                "Witch",
                "Vampire",
                "Tiefling",
                "kitchencounter_straight_A",
                "food_ingredient_burger_uncooked",
                "plate"
            }

            )
            {
                var model = Model(name);
                if (!model)
                {
                    report.AppendLine(name + ": MISSING");
                    continue;
                }

                var bounds = ModelBounds(model);
                var animator = model.GetComponentInChildren<Animator>(true);
                report.AppendLine($"{name}: local bounds center={bounds.center}, size={bounds.size}" + (animator ? $", avatar={(animator.avatar ? animator.avatar.name : "none")}, valid={(animator.avatar && animator.avatar.isValid)}" : ""));
            }

            report.AppendLine("Play Mode checks remain: idle/walk/carry/interact on Witch; idle/cheer on both customers; no root drift, missing materials or bone snapping.");
            return report.ToString();
        }

        // Find a model by its exact filename. If two files share that name, stop rather than quietly
        // choosing the wrong character or prop.
        public static GameObject Model(string name)
        {
            var matches = AssetDatabase.FindAssets("t:Model", new[] { ThirdParty }).Select(AssetDatabase.GUIDToAssetPath).Where(p => string.Equals(Path.GetFileNameWithoutExtension(p), name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length > 1)
                throw new InvalidOperationException("Duplicate model basename: " + name);
            return matches.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<GameObject>(matches[0]);
        }

        // Find a sound by filename in the supplied cafe pack.
        public static AudioClip Sound(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ThirdParty + "/CozyCafeSFX" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }

            return null;
        }

        // Animations are stored inside the imported FBX files. Ignore Unity's preview clips and strip
        // the source file prefix so names such as Walking_A can be looked up consistently.
        public static AnimationClip Clip(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ThirdParty + "/KayKitAnimations" }))
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<AnimationClip>())
                {
                    if (clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                        continue;
                    var simple = clip.name.Split('|').Last();
                    if (string.Equals(simple, name, StringComparison.OrdinalIgnoreCase))
                        return clip;
                }

            return null;
        }

        // Measure the imported mesh in the model's own coordinates, including all child renderers.
        // Transform the eight corners of each mesh's bounds before combining them.
        public static Bounds ModelBounds(GameObject model)
        {
            var bounds = new Bounds();
            var found = false;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skinned)
                    mesh = skinned.sharedMesh;
                else if (renderer.TryGetComponent<MeshFilter>(out var filter))
                    mesh = filter.sharedMesh;
                if (!mesh)
                    continue;
                var local = mesh.bounds;
                var matrix = model.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                for (var x = -1; x <= 1; x += 2)
                    for (var y = -1; y <= 1; y += 2)
                        for (var z = -1; z <= 1; z += 2)
                        {
                            var point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z)));
                            if (!found)
                            {
                                bounds = new Bounds(point, Vector3.zero);
                                found = true;
                            }
                            else
                                bounds.Encapsulate(point);
                        }
            }

            return bounds;
        }

        // Create missing parent folders first, then the requested generated-asset folder.
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // Import the color atlases as regular 3D textures with mipmaps for the angled camera view.
        private static void ConfigureTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // Use the supplied atlas as the URP material's base map. Reuse the saved material on later runs.
        private static Material MaterialFor(string name, Shader shader, string texturePath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (!texture)
                throw new FileNotFoundException("Missing atlas: " + texturePath);
            var path = Generated + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_Surface", 0f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Props use no animation rig; characters and animation files use the matching generic rig.
        // Keep the bone hierarchy available for carrying poses, and lock root movement in the clips
        // because PlayerChef and GuestMotion already move the characters through the scene.
        private static void ConfigureModel(string path, bool rigged, bool animation, Material material)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importCameras = false;
            importer.importLights = false;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.preserveHierarchy = true;
            importer.importAnimation = animation;
            importer.animationType = rigged ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            if (rigged)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.motionNodeName = "root";
                importer.optimizeGameObjects = false;
            }

            importer.materialImportMode = animation ? ModelImporterMaterialImportMode.None : ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            if (animation)
            {
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    var label = clip.name.Split('|').Last();
                    clip.loopTime = label.StartsWith("Idle", StringComparison.Ordinal) || label.StartsWith("Walking", StringComparison.Ordinal) || label.StartsWith("Running", StringComparison.Ordinal) || label.StartsWith("Holding", StringComparison.Ordinal) || label.StartsWith("Working", StringComparison.Ordinal) || label == "Chopping";
                    clip.loopPose = clip.loopTime;
                    clip.lockRootRotation = true;
                    clip.lockRootPositionXZ = true;
                    clip.lockRootHeightY = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionXZ = true;
                    clip.keepOriginalPositionY = true;
                }

                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
            else if (material)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var materialNames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => m.name).Concat(model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m).Select(m => m.name)).Distinct().ToArray();
                foreach (var sourceName in materialNames)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName), material);
                importer.SaveAndReimport();
            }
        }

        // Short effects use decompressed playback. Longer ambience streams to use less memory.
        // The pack's ambience files are retained as source assets but are not played by the game.
        private static void ConfigureAudio(string path)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var ambience = path.Contains("/Ambience/");
            importer.forceToMono = !ambience;
            importer.loadInBackground = ambience;
            var sample = importer.defaultSampleSettings;
            sample.loadType = ambience ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            sample.compressionFormat = AudioCompressionFormat.Vorbis;
            sample.quality = 0.8f;
            importer.defaultSampleSettings = sample;
            importer.SaveAndReimport();
        }

        // Include the left and right arm bone branches in an AvatarMask. This lets the chef hold a plate
        // with their upper body while the legs continue using the walking animation.
        private static AvatarMask CreateArmsMask(GameObject model, StringBuilder report)
        {
            if (!model)
                return null;
            var paths = new List<string>
            {
                ""
            };
            foreach (var transform in model.GetComponentsInChildren<Transform>(true))
                if (transform != model.transform)
                    paths.Add(AnimationUtility.CalculateTransformPath(transform, model.transform));
            var path = Generated + "/Animation/CarryArms.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (!mask)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }

            mask.transformCount = paths.Count;
            var active = 0;
            for (var i = 0; i < paths.Count; i++)
            {
                var lower = paths[i].ToLowerInvariant();
                var arm = lower.Split('/').Any(part => part == "upperarm.l" || part == "upperarm.r");
                mask.SetTransformPath(i, paths[i]);
                mask.SetTransformActive(i, arm);
                if (arm)
                    active++;
            }

            for (var i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            EditorUtility.SetDirty(mask);
            report.AppendLine("Carry mask: " + active + " generic arm transforms; confirm the hold pose during Play Mode.");
            return active > 0 ? mask : null;
        }

        // Create idle/walk states and the interaction, wave and cheer triggers.
        // The chef also gets a masked carrying layer. Rebuild the contents but keep the controller's
        // saved asset identity, so the scene's references do not break.
        private static void CreateController(string path, AnimationClip idle, AnimationClip walk, AnimationClip interact, AnimationClip cheer, AnimationClip holding, AvatarMask mask)
        {
            // Regenerate the owned state machines while preserving the controller
            // asset/GUID so scene and prefab references survive another setup run.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller)
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            else
            {
                controller.layers = Array.Empty<AnimatorControllerLayer>();
                controller.parameters = Array.Empty<AnimatorControllerParameter>();
                foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (subAsset && subAsset != controller)
                        UnityEngine.Object.DestroyImmediate(subAsset, true);
                var baseMachine = new AnimatorStateMachine
                {
                    name = "Base Layer"
                };
                AssetDatabase.AddObjectToAsset(baseMachine, controller);
                controller.AddLayer(new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f, blendingMode = AnimatorLayerBlendingMode.Override, stateMachine = baseMachine });
            }

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Carrying", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Interact", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Cheer", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Wave", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var idleState = State(machine, "Idle", idle);
            machine.defaultState = idleState;
            if (walk)
            {
                var walking = State(machine, "Walk", walk);
                Transition(idleState, walking, AnimatorConditionMode.Greater, "Speed", 0.05f);
                Transition(walking, idleState, AnimatorConditionMode.Less, "Speed", 0.05f);
            }

            TriggerState(machine, idleState, "Interact", interact);
            TriggerState(machine, idleState, "Cheer", cheer);
            TriggerState(machine, idleState, "Wave", Clip("Waving"));
            if (holding && mask)
            {
                var carryMachine = new AnimatorStateMachine
                {
                    name = "Carry arms"
                };
                AssetDatabase.AddObjectToAsset(carryMachine, controller);
                var empty = State(carryMachine, "Free hands", null);
                var carry = State(carryMachine, "Carry", holding);
                carryMachine.defaultState = empty;
                Transition(empty, carry, AnimatorConditionMode.If, "Carrying", 0);
                Transition(carry, empty, AnimatorConditionMode.IfNot, "Carrying", 0);
                controller.AddLayer(new AnimatorControllerLayer { name = "Carry arms", defaultWeight = 1f, avatarMask = mask, blendingMode = AnimatorLayerBlendingMode.Override, stateMachine = carryMachine });
            }

            EditorUtility.SetDirty(controller);
        }

        // Add one named animation state and assign its clip.
        private static AnimatorState State(AnimatorStateMachine machine, string name, AnimationClip motion)
        {
            var state = machine.AddState(name);
            state.motion = motion;
            state.writeDefaultValues = false;
            return state;
        }

        // Connect two states with a parameter condition, such as Speed being above the walking threshold.
        private static void Transition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, string parameter, float threshold)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.12f;
            transition.hasFixedDuration = true;
            transition.AddCondition(mode, threshold, parameter);
        }

        // Play a short reaction when its trigger fires, then return to idle when the clip finishes.
        private static void TriggerState(AnimatorStateMachine machine, AnimatorState idle, string parameter, AnimationClip clip)
        {
            if (!clip)
                return;
            var state = State(machine, parameter, clip);
            var enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.canTransitionToSelf = false;
            enter.duration = 0.08f;
            enter.hasFixedDuration = true;
            enter.AddCondition(AnimatorConditionMode.If, 0, parameter);
            var exit = state.AddTransition(idle);
            exit.hasExitTime = true;
            exit.exitTime = 0.95f;
            exit.duration = 0.1f;
            exit.hasFixedDuration = true;
        }
    }
}
#endif
