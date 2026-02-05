using UnityEngine;
using UnityEditor;
using WeatherVisualization3D;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Editor wizard for setting up the Volumetric Weather Visualization system.
    /// </summary>
    public class VolumetricWeatherSetupWizard : EditorWindow
    {
        #region Fields
        
        private ScenarioType selectedScenario = ScenarioType.ThunderstormCells;
        private bool createCloudRenderer = true;
        private bool createPillarRenderer = true;
        private bool createLightningEffect = true;
        private bool createPrecipitationEffect = true;
        private bool createConfig = true;
        
        private Vector3 volumeSize = new Vector3(50000f, 50000f, 50000f);
        private Vector3Int resolution = new Vector3Int(64, 32, 64);
        
        private string configPath = "Assets/_Project/ScriptableObjects/WeatherVisualization";
        
        private Vector2 scrollPosition;
        
        #endregion

        #region Menu Items
        
        [MenuItem("Tools/Weather Visualization/Setup Wizard", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<VolumetricWeatherSetupWizard>();
            window.titleContent = new GUIContent("Weather Setup Wizard");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }
        
        [MenuItem("Tools/Weather Visualization/Create Default Setup", false, 101)]
        public static void QuickSetup()
        {
            CreateFullSetup(ScenarioType.ThunderstormCells, true, true, true, true);
        }
        
        [MenuItem("Tools/Weather Visualization/Documentation", false, 200)]
        public static void OpenDocumentation()
        {
            // Open the README file in the project
            string readmePath = "Assets/_Project/Scripts/WeatherVisualization3D/README.md";
            var readme = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(readmePath);
            if (readme != null)
            {
                UnityEditor.AssetDatabase.OpenAsset(readme);
            }
            else
            {
                // Fallback to GitHub repo
                Application.OpenURL("https://github.com/CatfishW/FAA/tree/master/Assets/_Project/Scripts/WeatherVisualization3D");
            }
        }
        
        #endregion

        #region GUI
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            DrawHeader();
            EditorGUILayout.Space(10);
            
            DrawScenarioSection();
            EditorGUILayout.Space(10);
            
            DrawComponentsSection();
            EditorGUILayout.Space(10);
            
            DrawVolumeSettingsSection();
            EditorGUILayout.Space(10);
            
            DrawConfigSection();
            EditorGUILayout.Space(20);
            
            DrawCreateButton();
            EditorGUILayout.Space(10);
            
            DrawExistingSetupSection();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            
            EditorGUILayout.LabelField("Volumetric Weather Visualization", titleStyle);
            EditorGUILayout.LabelField("Setup Wizard", EditorStyles.centeredGreyMiniLabel);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "This wizard will create a complete weather visualization setup in your scene. " +
                "Select the components you want and configure the settings below.",
                MessageType.Info
            );
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawScenarioSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Weather Scenario", EditorStyles.boldLabel);
            
            selectedScenario = (ScenarioType)EditorGUILayout.EnumPopup("Scenario Type", selectedScenario);
            
            // Show scenario description
            string description = selectedScenario switch
            {
                ScenarioType.ScatteredShowers => "Isolated precipitation cells with light to moderate intensity.",
                ScenarioType.ThunderstormCells => "Active thunderstorm cells with moderate to extreme intensity.",
                ScenarioType.SquallLine => "Organized line of severe thunderstorms.",
                ScenarioType.Supercell => "Isolated supercell with extreme intensity.",
                _ => "Custom weather scenario."
            };
            
            EditorGUILayout.HelpBox(description, MessageType.None);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawComponentsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Components to Create", EditorStyles.boldLabel);
            
            createCloudRenderer = EditorGUILayout.ToggleLeft(
                new GUIContent("Volumetric Cloud Renderer", "Raymarched volumetric clouds"),
                createCloudRenderer
            );
            
            createPillarRenderer = EditorGUILayout.ToggleLeft(
                new GUIContent("Intensity Pillar Renderer", "Vertical pillars showing storm height/intensity"),
                createPillarRenderer
            );
            
            createLightningEffect = EditorGUILayout.ToggleLeft(
                new GUIContent("Lightning Effects", "Procedural lightning bolts"),
                createLightningEffect
            );
            
            createPrecipitationEffect = EditorGUILayout.ToggleLeft(
                new GUIContent("Precipitation Effects", "Rain and snow particles"),
                createPrecipitationEffect
            );
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawVolumeSettingsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Volume Settings", EditorStyles.boldLabel);
            
            volumeSize = EditorGUILayout.Vector3Field("World Size", volumeSize);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Resolution", GUILayout.Width(100));
            resolution.x = EditorGUILayout.IntField(resolution.x);
            resolution.y = EditorGUILayout.IntField(resolution.y);
            resolution.z = EditorGUILayout.IntField(resolution.z);
            EditorGUILayout.EndHorizontal();
            
            // Quick resolution presets
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Presets:", GUILayout.Width(100));
            if (GUILayout.Button("Low (32³)", EditorStyles.miniButtonLeft))
                resolution = new Vector3Int(32, 16, 32);
            if (GUILayout.Button("Medium (64³)", EditorStyles.miniButtonMid))
                resolution = new Vector3Int(64, 32, 64);
            if (GUILayout.Button("High (128³)", EditorStyles.miniButtonRight))
                resolution = new Vector3Int(128, 64, 128);
            EditorGUILayout.EndHorizontal();
            
            // Show estimated memory
            float memoryMB = (resolution.x * resolution.y * resolution.z * 4f * 3f) / (1024f * 1024f);
            EditorGUILayout.HelpBox($"Estimated texture memory: {memoryMB:F1} MB", MessageType.None);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawConfigSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            
            createConfig = EditorGUILayout.ToggleLeft("Create Config ScriptableObject", createConfig);
            
            if (createConfig)
            {
                EditorGUILayout.BeginHorizontal();
                configPath = EditorGUILayout.TextField("Config Path", configPath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string path = EditorUtility.SaveFolderPanel("Select Config Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        configPath = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawCreateButton()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Create Weather System", GUILayout.Width(200), GUILayout.Height(30)))
            {
                CreateSetup();
            }
            GUI.backgroundColor = Color.white;
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawExistingSetupSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Existing Setup", EditorStyles.boldLabel);
            
            // Find existing components
            var manager = FindObjectOfType<VolumetricWeatherManager>();
            var simulator = FindObjectOfType<WeatherSimulator>();
            
            if (manager != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Manager found:", manager.gameObject.name);
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeGameObject = manager.gameObject;
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("No VolumetricWeatherManager in scene", EditorStyles.miniLabel);
            }
            
            if (simulator != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Simulator found:", simulator.gameObject.name);
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeGameObject = simulator.gameObject;
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.Space(5);
            
            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("Remove Existing Setup"))
            {
                if (EditorUtility.DisplayDialog("Remove Weather Setup",
                    "Are you sure you want to remove all weather visualization components from the scene?",
                    "Remove", "Cancel"))
                {
                    RemoveExistingSetup();
                }
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndVertical();
        }
        
        #endregion

        #region Setup Logic
        
        private void CreateSetup()
        {
            CreateFullSetup(
                selectedScenario,
                createCloudRenderer,
                createPillarRenderer,
                createLightningEffect,
                createPrecipitationEffect
            );
        }
        
        public static void CreateFullSetup(
            ScenarioType scenario,
            bool clouds,
            bool pillars,
            bool lightning,
            bool precipitation)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Weather System");
            int undoGroup = Undo.GetCurrentGroup();

            // Ensure prefabs exist
            var registry = WeatherPrefabRegistry.GetOrCreate();
            if (registry == null || !registry.IsComplete())
            {
                if (EditorUtility.DisplayDialog("Missing Prefabs",
                    "Weather prefabs not found or incomplete.\n\nCreate prefabs now?",
                    "Create Prefabs", "Cancel"))
                {
                    WeatherPrefabFactory.CreateMissingPrefabsOnly();
                    registry = WeatherPrefabRegistry.GetOrCreate();
                }

                if (registry == null || !registry.IsComplete())
                {
                    Debug.LogError("[WeatherSetupWizard] Cannot create weather system - prefabs missing.");
                    Undo.RevertAllDownToGroup(undoGroup);
                    return;
                }
            }

            // Create config asset
            WeatherVolumeConfig config = CreateConfig();

            // Use prefab factory to instantiate the system
            GameObject root = WeatherPrefabFactory.InstantiateWeatherSystem(clouds, pillars, lightning, precipitation);

            if (root == null)
            {
                Debug.LogError("[WeatherSetupWizard] Failed to instantiate weather system from prefabs.");
                Undo.RevertAllDownToGroup(undoGroup);
                return;
            }

            // Set scenario on simulator
            var simulator = root.GetComponentInChildren<WeatherSimulator>();
            if (simulator != null)
            {
                var scenarioField = simulator.GetType().GetField("defaultScenarioType",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (scenarioField != null)
                {
                    scenarioField.SetValue(simulator, scenario);
                }
            }

            // Set config on manager
            var manager = root.GetComponent<VolumetricWeatherManager>();
            if (manager != null && config != null)
            {
                var configField = manager.GetType().GetField("_config",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                configField?.SetValue(manager, config);
            }

            Undo.CollapseUndoOperations(undoGroup);

            Selection.activeGameObject = root;

            Debug.Log($"[WeatherSetupWizard] Created weather system with scenario: {scenario}");
            EditorUtility.DisplayDialog("Success",
                "Weather visualization system created successfully!\n\n" +
                "Press Play to see the simulation in action.",
                "OK");
        }
        
        private static WeatherVolumeConfig CreateConfig()
        {
            string folderPath = "Assets/_Project/ScriptableObjects/WeatherVisualization";
            
            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string[] parts = folderPath.Split('/');
                string currentPath = parts[0];
                
                for (int i = 1; i < parts.Length; i++)
                {
                    string nextPath = currentPath + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                    }
                    currentPath = nextPath;
                }
            }
            
            // Create config asset
            WeatherVolumeConfig config = ScriptableObject.CreateInstance<WeatherVolumeConfig>();
            
            string assetPath = folderPath + "/DefaultWeatherConfig.asset";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[WeatherSetupWizard] Created config at: {assetPath}");
            
            return config;
        }
        
        private void RemoveExistingSetup()
        {
            // Find and remove all weather components
            var managers = FindObjectsOfType<VolumetricWeatherManager>();
            var simulators = FindObjectsOfType<WeatherSimulator>();
            var cloudRenderers = FindObjectsOfType<VolumetricCloudVolume>();
            var pillarRenderers = FindObjectsOfType<IntensityPillarRenderer>();
            var lightningEffects = FindObjectsOfType<VolumetricLightning>();
            var precipEffects = FindObjectsOfType<PrecipitationVFX>();
            
            int count = 0;
            
            foreach (var m in managers) { DestroyImmediate(m.gameObject); count++; }
            foreach (var s in simulators) { DestroyImmediate(s.gameObject); count++; }
            foreach (var c in cloudRenderers) { DestroyImmediate(c.gameObject); count++; }
            foreach (var p in pillarRenderers) { DestroyImmediate(p.gameObject); count++; }
            foreach (var l in lightningEffects) { DestroyImmediate(l.gameObject); count++; }
            foreach (var p in precipEffects) { DestroyImmediate(p.gameObject); count++; }
            
            // Also look for root object
            var root = GameObject.Find("WeatherVisualization3D");
            if (root != null)
            {
                DestroyImmediate(root);
                count++;
            }
            
            Debug.Log($"[WeatherSetupWizard] Removed {count} weather objects from scene");
        }
        
        #endregion
    }
}
