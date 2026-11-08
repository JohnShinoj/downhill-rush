using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SkiGame.EditorTools
{
    /// <summary>
    /// One-click scene setup. Creates the settings assets and builds Assets/Scenes/SkiGame.unity
    /// with every GameObject, component and reference wired up.
    ///
    /// Runs automatically the first time the project is opened (when the scene does not exist yet).
    /// Menu: Tools > Ski Game > Rebuild Scene   (recreate the scene, keeps your settings assets)
    ///       Tools > Ski Game > Open Game Scene
    ///       Tools > Ski Game > Fix Input Handling
    /// </summary>
    [InitializeOnLoad]
    public static class SkiGameSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SkiGame.unity";
        private const string SettingsFolder = "Assets/Settings";

        static SkiGameSceneBuilder()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        private static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureInputHandling(false);
            if (!File.Exists(ScenePath))
            {
                Debug.Log("[SkiGame] First run: building the game scene...");
                BuildScene();
            }
        }

        [MenuItem("Tools/Ski Game/Open Game Scene", priority = 0)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) { BuildScene(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Ski Game/Fix Input Handling", priority = 20)]
        public static void FixInputHandlingMenu() => EnsureInputHandling(true);

        [MenuItem("Tools/Ski Game/Rebuild Scene", priority = 1)]
        public static void BuildScene()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "Settings");

            // Make sure the settings assets exist on disk (kept if they already exist).
            LoadOrCreate<SkierSettings>("SkierSettings");
            LoadOrCreate<SlopeSettings>("SlopeSettings");
            LoadOrCreate<SpawnTable>("SpawnTable");
            LoadOrCreate<GameRulesSettings>("GameRulesSettings");
            LoadOrCreate<DifficultySettings>("DifficultySettings");
            LoadOrCreate<VisualSettings>("VisualSettings");
            EnsureSkyboxMaterial();
            AssetDatabase.SaveAssets();

            // ---------- Scene ----------
            // NOTE: NewScene unloads unused assets, so the settings are (re)loaded AFTER it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var skier = LoadOrCreate<SkierSettings>("SkierSettings");
            var slopeSettings = LoadOrCreate<SlopeSettings>("SlopeSettings");
            var spawnTable = LoadOrCreate<SpawnTable>("SpawnTable");
            var rules = LoadOrCreate<GameRulesSettings>("GameRulesSettings");
            var difficulty = LoadOrCreate<DifficultySettings>("DifficultySettings");
            var visualSettings = LoadOrCreate<VisualSettings>("VisualSettings");
            var skyMaterial = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);

            // Lighting rig: warm sun + cool fill + sky/fog/ambient/mountains
            var lightingGo = new GameObject("Lighting");
            var rig = lightingGo.AddComponent<LightingRig>();
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(lightingGo.transform, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            var fillGo = new GameObject("Fill Light");
            fillGo.transform.SetParent(lightingGo.transform, false);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;

            // World
            var world = new GameObject("World");
            var slope = world.AddComponent<SlopeGenerator>();
            var spawner = world.AddComponent<ContentSpawner>();

            // Player (gameplay root) + swappable visual child
            var playerGo = new GameObject("Player");
            var controller = playerGo.AddComponent<PlayerController>();
            var keyboard = playerGo.AddComponent<KeyboardInput>();
            var visualGo = new GameObject("SkierVisual (Primitive)");
            visualGo.transform.SetParent(playerGo.transform, false);
            var visual = visualGo.AddComponent<PrimitiveSkierVisual>();

            // Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1600f;
            cam.allowHDR = true;
            cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();
            var skiCam = camGo.AddComponent<SkiCamera>();
            var post = camGo.AddComponent<SkiPostEffects>();
            camGo.transform.position = new Vector3(0f, 4f, -7f);
            camGo.transform.rotation = Quaternion.Euler(18f, 0f, 0f);

            // Game systems + UI
            var gameGo = new GameObject("Game");
            var gameManager = gameGo.AddComponent<GameManager>();
            var score = gameGo.AddComponent<ScoreManager>();
            var difficultyManager = gameGo.AddComponent<DifficultyManager>();
            var hud = gameGo.AddComponent<GameHUD>();
            var debugHud = gameGo.AddComponent<DebugInputHUD>();
            var shaderLibrary = gameGo.AddComponent<ShaderLibrary>();
            var effects = gameGo.AddComponent<GameEffects>();
            var audio = gameGo.AddComponent<GameAudio>();

            // ---------- Wire references ----------
            SetAsset(controller, "settings", "SkierSettings");
            SetAsset(controller, "rules", "GameRulesSettings");
            Set(controller, "slope", slope);
            Set(controller, "inputSource", keyboard);
            Set(controller, "visualComponent", visual);

            SetAsset(slope, "settings", "SlopeSettings");
            Set(slope, "player", playerGo.transform);

            Set(spawner, "slope", slope);
            SetAsset(spawner, "spawnTable", "SpawnTable");
            SetAsset(spawner, "difficulty", "DifficultySettings");
            SetAsset(spawner, "skierSettings", "SkierSettings");
            SetAsset(spawner, "rules", "GameRulesSettings");

            Set(skiCam, "target", playerGo.transform);
            Set(skiCam, "player", controller);
            Set(skiCam, "slope", slope);

            SetAsset(gameManager, "rules", "GameRulesSettings");
            Set(gameManager, "player", controller);
            Set(gameManager, "slope", slope);
            Set(gameManager, "score", score);

            SetAsset(score, "rules", "GameRulesSettings");
            Set(score, "player", controller);

            SetAsset(difficultyManager, "settings", "DifficultySettings");
            Set(difficultyManager, "player", controller);

            Set(hud, "game", gameManager);
            Set(hud, "score", score);
            Set(hud, "player", controller);
            Set(hud, "difficulty", difficultyManager);
            Set(hud, "slope", slope);

            Set(debugHud, "player", controller);

            Set(rig, "settings", visualSettings);
            Set(rig, "sun", sun);
            Set(rig, "fill", fill);
            Set(rig, "targetCamera", cam);
            Set(rig, "skyboxMaterial", skyMaterial);
            Set(rig, "customSkyShader", AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/SkiSky.shader"));

            Set(post, "shader", AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/SkiPostFX.shader"));
            Set(post, "player", controller);

            Set(shaderLibrary, "particleAlpha", AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/SkiParticle.shader"));
            Set(shaderLibrary, "particleAdditive", AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/SkiParticleAdditive.shader"));
            Set(shaderLibrary, "backdrop", AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/SkiBackdrop.shader"));
            Set(shaderLibrary, "snow", AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/SkiSnow.shader"));
            Set(shaderLibrary, "cloud", AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/SkiCloud.shader"));

            Set(effects, "player", controller);
            Set(effects, "targetCamera", cam);
            Set(effects, "visualSettings", visualSettings);
            Set(audio, "player", controller);

            // Preview the lighting in the editor too (it is re-applied at runtime).
            rig.Apply();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = playerGo;
            Debug.Log("[SkiGame v5] Scene ready: " + ScenePath + "  -> press Play.");
        }

        private const string SkyboxPath = SettingsFolder + "/SkiSky.mat";

        /// <summary>Procedural sky material saved as an asset so it is included in builds.</summary>
        private static void EnsureSkyboxMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
            if (mat != null) return;
            Shader proc = Shader.Find("Skybox/Procedural");
            if (proc == null) return;
            mat = new Material(proc) { name = "SkiSky" };
            AssetDatabase.CreateAsset(mat, SkyboxPath);
        }

        // ------------------------------------------------------------------ helpers
        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError($"[SkiGame] Field '{field}' not found on {target.GetType().Name}.");
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Assigns a settings asset, loading it from disk right now (NewScene can unload earlier instances).</summary>
        private static void SetAsset(Object target, string field, string assetName)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(SettingsFolder + "/" + assetName + ".asset");
            if (asset == null) Debug.LogError("[SkiGame] Missing settings asset: " + assetName);
            Set(target, field, asset);
        }

        private static T LoadOrCreate<T>(string name) where T : ScriptableObject
        {
            string path = SettingsFolder + "/" + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }

        /// <summary>
        /// KeyboardInput uses the old Input Manager. If the project is set to the new
        /// Input System only, switch to "Both" (needs an editor restart).
        /// </summary>
        private static void EnsureInputHandling(bool fromMenu)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            SerializedProperty p = so.FindProperty("activeInputHandler");
            if (p == null) return;

            if (p.intValue == 1) // 0 = old, 1 = new, 2 = both
            {
                p.intValue = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                if (EditorUtility.DisplayDialog("Ski Game",
                        "Active Input Handling was set to 'Both' so the keyboard controls (old Input Manager) work.\n\n" +
                        "Unity must restart for this to take effect. Restart now?", "Restart", "Later"))
                {
                    EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                }
            }
            else if (fromMenu)
            {
                EditorUtility.DisplayDialog("Ski Game", "Input handling is already compatible (old Input Manager enabled).", "OK");
            }
        }
    }
}
