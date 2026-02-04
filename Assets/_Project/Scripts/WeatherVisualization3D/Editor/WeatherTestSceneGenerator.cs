using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Generator for complete weather visualization test scenes.
    /// Creates a ready-to-run scene with all components configured for testing.
    /// </summary>
    public class WeatherTestSceneGenerator : EditorWindow
    {
        #region Settings
        
        private string sceneName = "WeatherVisualization_TestScene";
        private ScenarioType initialScenario = ScenarioType.ThunderstormCells;
        private bool createSkybox = true;
        private bool createFloor = true;
        private bool createCamera = true;
        private bool createLighting = true;
        private bool createDebugUI = true;
        private bool openSceneAfterCreate = true;
        
        private Vector3 volumeSize = new Vector3(100000f, 50000f, 100000f);
        private Vector3Int resolution = new Vector3Int(64, 32, 64);
        
        private enum CameraType
        {
            FreeFly,
            Cockpit,
            Orbital
        }
        private CameraType cameraType = CameraType.FreeFly;
        
        #endregion

        [MenuItem("Tools/Weather Visualization/Generate Test Scene", false, 50)]
        public static void ShowWindow()
        {
            var window = GetWindow<WeatherTestSceneGenerator>();
            window.titleContent = new GUIContent("Weather Test Scene Generator");
            window.minSize = new Vector2(450, 550);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(10);
            
            DrawSceneSettings();
            EditorGUILayout.Space(10);
            
            DrawWeatherSettings();
            EditorGUILayout.Space(10);
            
            DrawEnvironmentSettings();
            EditorGUILayout.Space(10);
            
            DrawCameraSettings();
            EditorGUILayout.Space(20);
            
            DrawGenerateButton();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            
            EditorGUILayout.LabelField("🌩️ Weather Test Scene Generator", titleStyle);
            EditorGUILayout.LabelField("Create a complete test scene with one click", EditorStyles.centeredGreyMiniLabel);
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.HelpBox(
                "This tool creates a new scene with all weather visualization components pre-configured. " +
                "Perfect for testing and demonstrating the volumetric weather system.",
                MessageType.Info
            );
            
            EditorGUILayout.EndVertical();
        }

        private void DrawSceneSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Scene Settings", EditorStyles.boldLabel);
            
            sceneName = EditorGUILayout.TextField("Scene Name", sceneName);
            openSceneAfterCreate = EditorGUILayout.Toggle("Open After Create", openSceneAfterCreate);
            
            EditorGUILayout.EndVertical();
        }

        private void DrawWeatherSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Weather Configuration", EditorStyles.boldLabel);
            
            initialScenario = (ScenarioType)EditorGUILayout.EnumPopup("Initial Scenario", initialScenario);
            
            EditorGUILayout.Space(5);
            
            volumeSize = EditorGUILayout.Vector3Field("Volume Size (meters)", volumeSize);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Resolution", GUILayout.Width(80));
            resolution.x = EditorGUILayout.IntField(resolution.x);
            resolution.y = EditorGUILayout.IntField(resolution.y);
            resolution.z = EditorGUILayout.IntField(resolution.z);
            EditorGUILayout.EndHorizontal();
            
            // Resolution presets
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Presets:", GUILayout.Width(60));
            if (GUILayout.Button("Low", EditorStyles.miniButtonLeft))
                resolution = new Vector3Int(32, 16, 32);
            if (GUILayout.Button("Medium", EditorStyles.miniButtonMid))
                resolution = new Vector3Int(64, 32, 64);
            if (GUILayout.Button("High", EditorStyles.miniButtonRight))
                resolution = new Vector3Int(128, 64, 128);
            EditorGUILayout.EndHorizontal();
            
            // Memory estimate
            float memoryMB = (resolution.x * resolution.y * resolution.z * 4f * 2f) / (1024f * 1024f);
            EditorGUILayout.HelpBox($"Estimated Memory: {memoryMB:F1} MB", MessageType.None);
            
            EditorGUILayout.EndVertical();
        }

        private void DrawEnvironmentSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Environment", EditorStyles.boldLabel);
            
            createSkybox = EditorGUILayout.Toggle("Create Skybox", createSkybox);
            createFloor = EditorGUILayout.Toggle("Create Reference Floor", createFloor);
            createLighting = EditorGUILayout.Toggle("Configure Lighting", createLighting);
            createDebugUI = EditorGUILayout.Toggle("Create Debug UI", createDebugUI);
            
            EditorGUILayout.EndVertical();
        }

        private void DrawCameraSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);
            
            createCamera = EditorGUILayout.Toggle("Create Camera", createCamera);
            
            if (createCamera)
            {
                cameraType = (CameraType)EditorGUILayout.EnumPopup("Camera Type", cameraType);
                
                string cameraDesc = cameraType switch
                {
                    CameraType.FreeFly => "WASD + Mouse to fly freely through the scene",
                    CameraType.Cockpit => "Simulated cockpit view with weather radar perspective",
                    CameraType.Orbital => "Orbit camera around the weather volume center",
                    _ => ""
                };
                
                EditorGUILayout.HelpBox(cameraDesc, MessageType.None);
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawGenerateButton()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            if (GUILayout.Button("🎬 Generate Test Scene", GUILayout.Width(200), GUILayout.Height(35)))
            {
                GenerateTestScene();
            }
            GUI.backgroundColor = Color.white;
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void GenerateTestScene()
        {
            // Check for unsaved changes
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            
            // Create new scene
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            
            try
            {
                // Create lighting
                if (createLighting)
                {
                    CreateLighting();
                }
                
                // Create camera
                if (createCamera)
                {
                    CreateCamera();
                }
                
                // Create floor
                if (createFloor)
                {
                    CreateFloor();
                }
                
                // Create weather system
                CreateWeatherSystem();
                
                // Create debug UI
                if (createDebugUI)
                {
                    CreateDebugUI();
                }
                
                // Save scene
                string scenePath = $"Assets/_Project/Scenes/{sceneName}.unity";
                EnsureDirectoryExists(scenePath);
                EditorSceneManager.SaveScene(newScene, scenePath);
                
                Debug.Log($"[WeatherTestSceneGenerator] Created test scene at: {scenePath}");
                
                EditorUtility.DisplayDialog("Success", 
                    $"Test scene '{sceneName}' created successfully!\n\n" +
                    "Press Play to start the simulation.\n\n" +
                    "Controls:\n" +
                    "- WASD: Move camera\n" +
                    "- Mouse: Look around\n" +
                    "- Shift: Move faster\n" +
                    "- Space/Ctrl: Up/Down",
                    "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WeatherTestSceneGenerator] Failed to generate scene: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to generate scene:\n{ex.Message}", "OK");
            }
        }

        private void EnsureDirectoryExists(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private void CreateLighting()
        {
            // Directional light (Sun)
            GameObject sunObj = new GameObject("Directional Light (Sun)");
            var light = sunObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            sunObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            
            // Set as sun reference
            RenderSettings.sun = light;
            
            // Ambient settings
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            
            // Fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.00001f;
            RenderSettings.fogColor = new Color(0.6f, 0.7f, 0.8f);
            
            // Skybox
            if (createSkybox)
            {
                var skyboxMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/WeatherVisualization/SkyboxMaterial.mat");
                if (skyboxMat == null)
                {
                    // Create procedural skybox
                    CreateProceduralSkybox();
                }
                else
                {
                    RenderSettings.skybox = skyboxMat;
                }
            }
        }

        private void CreateProceduralSkybox()
        {
            // Create a simple procedural skybox material
            Shader skyboxShader = Shader.Find("Skybox/Procedural");
            if (skyboxShader != null)
            {
                Material skyMat = new Material(skyboxShader);
                skyMat.name = "ProceduralSkybox";
                skyMat.SetFloat("_SunSize", 0.04f);
                skyMat.SetFloat("_SunSizeConvergence", 5f);
                skyMat.SetFloat("_AtmosphereThickness", 1.0f);
                skyMat.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f));
                skyMat.SetColor("_GroundColor", new Color(0.369f, 0.349f, 0.341f));
                skyMat.SetFloat("_Exposure", 1.3f);
                
                // Save the material
                string matPath = "Assets/_Project/Materials/WeatherVisualization/ProceduralSkybox.mat";
                EnsureDirectoryExists(matPath);
                AssetDatabase.CreateAsset(skyMat, matPath);
                
                RenderSettings.skybox = skyMat;
            }
        }

        private void CreateCamera()
        {
            GameObject cameraObj = new GameObject("Main Camera");
            var camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.2f, 0.3f, 0.5f);
            camera.nearClipPlane = 10f;
            camera.farClipPlane = 500000f;
            camera.fieldOfView = 60f;
            camera.tag = "MainCamera";
            
            // Position camera
            float viewHeight = volumeSize.y * 0.3f;
            float viewDistance = volumeSize.x * 0.5f;
            cameraObj.transform.position = new Vector3(-viewDistance * 0.3f, viewHeight, -viewDistance * 0.3f);
            cameraObj.transform.LookAt(Vector3.zero + Vector3.up * viewHeight * 0.5f);
            
            // Add audio listener
            cameraObj.AddComponent<AudioListener>();
            
            // Add camera controller based on type
            switch (cameraType)
            {
                case CameraType.FreeFly:
                    cameraObj.AddComponent<FreeFlyCamera>();
                    break;
                case CameraType.Orbital:
                    var orbital = cameraObj.AddComponent<OrbitalTestCamera>();
                    orbital.targetPoint = Vector3.up * volumeSize.y * 0.25f;
                    orbital.distance = volumeSize.x * 0.5f;
                    break;
                case CameraType.Cockpit:
                    cameraObj.AddComponent<FreeFlyCamera>();
                    // Position for cockpit view
                    cameraObj.transform.position = new Vector3(0, viewHeight * 0.6f, -viewDistance * 0.8f);
                    cameraObj.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
                    break;
            }
        }

        private void CreateFloor()
        {
            // Create a large reference floor/terrain
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "ReferenceFloor";
            floor.transform.position = Vector3.zero;
            float floorScale = volumeSize.x / 10f;
            floor.transform.localScale = new Vector3(floorScale, 1f, floorScale);
            
            // Create floor material
            var floorMat = new Material(Shader.Find("Standard"));
            floorMat.name = "FloorMaterial";
            floorMat.color = new Color(0.3f, 0.4f, 0.25f);
            floorMat.SetFloat("_Metallic", 0f);
            floorMat.SetFloat("_Glossiness", 0.2f);
            
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            
            // Remove collider for better performance
            DestroyImmediate(floor.GetComponent<Collider>());
        }

        private void CreateWeatherSystem()
        {
            // Create root object
            GameObject root = new GameObject("WeatherVisualization3D");
            
            // Create and configure manager
            var manager = root.AddComponent<VolumetricWeatherManager>();
            
            // Create config
            WeatherVolumeConfig config = ScriptableObject.CreateInstance<WeatherVolumeConfig>();
            config.volumeResolution = resolution;
            config.coverageNM = volumeSize.x / 3704f; // Convert meters to NM
            config.maxAltitudeFt = volumeSize.y * 3.28084f; // Convert to feet
            
            string configPath = "Assets/_Project/ScriptableObjects/WeatherVisualization/TestSceneConfig.asset";
            EnsureDirectoryExists(configPath);
            configPath = AssetDatabase.GenerateUniqueAssetPath(configPath);
            AssetDatabase.CreateAsset(config, configPath);
            
            // Assign config via serialized property
            var so = new SerializedObject(manager);
            so.FindProperty("_config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            
            // Create simulator
            GameObject simObj = new GameObject("WeatherSimulator");
            simObj.transform.SetParent(root.transform);
            var simulator = simObj.AddComponent<WeatherSimulator>();
            
            // Configure simulator via serialized object
            var simSO = new SerializedObject(simulator);
            simSO.FindProperty("defaultScenarioType").enumValueIndex = (int)initialScenario;
            simSO.FindProperty("volumeResolution").vector3IntValue = resolution;
            simSO.FindProperty("volumeWorldSize").vector3Value = volumeSize;
            simSO.FindProperty("volumeOrigin").vector3Value = new Vector3(-volumeSize.x * 0.5f, 0, -volumeSize.z * 0.5f);
            simSO.FindProperty("showDebugInfo").boolValue = true;
            simSO.FindProperty("drawCellGizmos").boolValue = true;
            simSO.ApplyModifiedPropertiesWithoutUndo();
            
            // Create cloud renderer
            GameObject cloudObj = new GameObject("VolumetricCloudVolume");
            cloudObj.transform.SetParent(root.transform);
            cloudObj.AddComponent<VolumetricCloudVolume>();
            
            // Create pillar renderer
            GameObject pillarObj = new GameObject("IntensityPillarRenderer");
            pillarObj.transform.SetParent(root.transform);
            var pillarRenderer = pillarObj.AddComponent<IntensityPillarRenderer>();
            
            // Link pillar renderer to simulator
            var pillarSO = new SerializedObject(pillarRenderer);
            pillarSO.FindProperty("weatherSimulator").objectReferenceValue = simulator;
            pillarSO.ApplyModifiedPropertiesWithoutUndo();
            
            // Create lightning effect
            GameObject lightningObj = new GameObject("VolumetricLightning");
            lightningObj.transform.SetParent(root.transform);
            var lightning = lightningObj.AddComponent<VolumetricLightning>();
            
            var lightningSO = new SerializedObject(lightning);
            lightningSO.FindProperty("weatherSimulator").objectReferenceValue = simulator;
            lightningSO.ApplyModifiedPropertiesWithoutUndo();
            
            // Create precipitation effect
            GameObject precipObj = new GameObject("PrecipitationVFX");
            precipObj.transform.SetParent(root.transform);
            var precip = precipObj.AddComponent<PrecipitationVFX>();
            
            var precipSO = new SerializedObject(precip);
            precipSO.FindProperty("weatherSimulator").objectReferenceValue = simulator;
            precipSO.ApplyModifiedPropertiesWithoutUndo();
        }

        private void CreateDebugUI()
        {
            // Check if EventSystem exists
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            
            // Create Canvas
            GameObject canvasObj = new GameObject("WeatherDebugUI");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            // Add weather debug panel
            canvasObj.AddComponent<WeatherDebugPanel>();
        }
    }
}

