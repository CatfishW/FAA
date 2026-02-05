using UnityEngine;
using UnityEditor;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Quick action menu items for Weather Visualization 3D system.
    /// Provides one-click actions for common tasks.
    /// </summary>
    public static class WeatherQuickActions
    {
        #region Quick Setup

        [MenuItem("Tools/Weather Visualization/Quick Setup/Add RainViewer Real-Time Radar", false, 0)]
        public static void AddRainViewerProvider()
        {
            var manager = Object.FindObjectOfType<VolumetricWeatherManager>();
            if (manager == null)
            {
                if (!EditorUtility.DisplayDialog("No Weather System",
                    "No VolumetricWeatherManager found in scene.\n\n" +
                    "Create a weather system first, then add RainViewer provider.",
                    "Create System", "Cancel"))
                {
                    return;
                }
                CreateCompleteSystem();
                manager = Object.FindObjectOfType<VolumetricWeatherManager>();
            }

            // Add RainViewer provider to the manager's game object
            var provider = manager.gameObject.GetComponent<RainViewer3DProvider>();
            if (provider != null)
            {
                EditorUtility.DisplayDialog("Already Exists",
                    "RainViewer3DProvider already exists on the weather system.",
                    "OK");
                return;
            }

            provider = manager.gameObject.AddComponent<RainViewer3DProvider>();
            provider.SetPosition(39.7392f, -104.9903f, 5000f); // Default: Denver
            provider.SetRange(160f);

            // Set as data source
            manager.SetDataSource(provider);

            Selection.activeGameObject = manager.gameObject;
            Debug.Log("[WeatherQuickActions] Added RainViewer3DProvider to weather system. Press Play to start receiving real-time radar data.");
        }

        [MenuItem("Tools/Weather Visualization/Quick Setup/Create Complete System", false, 1)]
        public static void CreateCompleteSystem()
        {
            if (FindExistingSystem())
            {
                if (!EditorUtility.DisplayDialog("System Exists",
                    "A weather system already exists in the scene.\n\n" +
                    "Do you want to replace it with a new one?",
                    "Replace", "Cancel"))
                {
                    return;
                }
                RemoveExistingSystem();
            }

            VolumetricWeatherSetupWizard.CreateFullSetup(
                ScenarioType.ThunderstormCells,
                true, true, true, true);
        }

        [MenuItem("Tools/Weather Visualization/Quick Setup/Add to Selected Object", false, 1)]
        public static void AddToSelectedObject()
        {
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("No Selection",
                    "Please select a GameObject to add weather components to.",
                    "OK");
                return;
            }

            var target = Selection.activeGameObject;
            
            if (target.GetComponent<VolumetricWeatherManager>() == null)
                target.AddComponent<VolumetricWeatherManager>();
            
            EditorUtility.DisplayDialog("Success",
                $"Added VolumetricWeatherManager to {target.name}",
                "OK");
        }

        [MenuItem("Tools/Weather Visualization/Quick Setup/Create Minimal (Clouds Only)", false, 10)]
        public static void CreateMinimalSystem()
        {
            VolumetricWeatherSetupWizard.CreateFullSetup(
                ScenarioType.ScatteredShowers,
                true, false, false, false);
        }

        [MenuItem("Tools/Weather Visualization/Quick Setup/Create With Pillars", false, 11)]
        public static void CreateWithPillars()
        {
            VolumetricWeatherSetupWizard.CreateFullSetup(
                ScenarioType.ThunderstormCells,
                true, true, false, false);
        }

        [MenuItem("Tools/Weather Visualization/Quick Setup/Create Full Storm", false, 12)]
        public static void CreateFullStorm()
        {
            VolumetricWeatherSetupWizard.CreateFullSetup(
                ScenarioType.Supercell,
                true, true, true, true);
        }

        #endregion

        #region Scenarios

        [MenuItem("Tools/Weather Visualization/Scenarios/Scattered Showers", false, 50)]
        public static void SetScatteredShowers()
        {
            var sim = Object.FindObjectOfType<WeatherSimulator>();
            if (sim != null)
            {
                sim.SetScenarioByType(ScenarioType.ScatteredShowers);
                Debug.Log("[WeatherQuickActions] Set scenario: Scattered Showers");
            }
            else
            {
                EditorUtility.DisplayDialog("No Simulator",
                    "No WeatherSimulator found in scene.\nCreate a weather system first.",
                    "OK");
            }
        }

        [MenuItem("Tools/Weather Visualization/Scenarios/Thunderstorm Cells", false, 51)]
        public static void SetThunderstormCells()
        {
            var sim = Object.FindObjectOfType<WeatherSimulator>();
            if (sim != null)
            {
                sim.SetScenarioByType(ScenarioType.ThunderstormCells);
                Debug.Log("[WeatherQuickActions] Set scenario: Thunderstorm Cells");
            }
        }

        [MenuItem("Tools/Weather Visualization/Scenarios/Squall Line", false, 52)]
        public static void SetSquallLine()
        {
            var sim = Object.FindObjectOfType<WeatherSimulator>();
            if (sim != null)
            {
                sim.SetScenarioByType(ScenarioType.SquallLine);
                Debug.Log("[WeatherQuickActions] Set scenario: Squall Line");
            }
        }

        [MenuItem("Tools/Weather Visualization/Scenarios/Supercell", false, 53)]
        public static void SetSupercell()
        {
            var sim = Object.FindObjectOfType<WeatherSimulator>();
            if (sim != null)
            {
                sim.SetScenarioByType(ScenarioType.Supercell);
                Debug.Log("[WeatherQuickActions] Set scenario: Supercell");
            }
        }

        // Validation for scenario menu items
        [MenuItem("Tools/Weather Visualization/Scenarios/Scattered Showers", true)]
        [MenuItem("Tools/Weather Visualization/Scenarios/Thunderstorm Cells", true)]
        [MenuItem("Tools/Weather Visualization/Scenarios/Squall Line", true)]
        [MenuItem("Tools/Weather Visualization/Scenarios/Supercell", true)]
        public static bool ValidateScenarioMenuItem()
        {
            return Application.isPlaying && Object.FindObjectOfType<WeatherSimulator>() != null;
        }

        #endregion

        #region Visibility

        [MenuItem("Tools/Weather Visualization/Visibility/Toggle All On", false, 100)]
        public static void ToggleAllOn()
        {
            var manager = Object.FindObjectOfType<VolumetricWeatherManager>();
            if (manager != null)
            {
                manager.ShowVolumetricClouds = true;
                manager.ShowIntensityPillars = true;
                manager.ShowLightning = true;
                manager.ShowPrecipitation = true;
            }
        }

        [MenuItem("Tools/Weather Visualization/Visibility/Toggle All Off", false, 101)]
        public static void ToggleAllOff()
        {
            var manager = Object.FindObjectOfType<VolumetricWeatherManager>();
            if (manager != null)
            {
                manager.ShowVolumetricClouds = false;
                manager.ShowIntensityPillars = false;
                manager.ShowLightning = false;
                manager.ShowPrecipitation = false;
            }
        }

        [MenuItem("Tools/Weather Visualization/Visibility/Clouds Only", false, 102)]
        public static void CloudsOnly()
        {
            var manager = Object.FindObjectOfType<VolumetricWeatherManager>();
            if (manager != null)
            {
                manager.ShowVolumetricClouds = true;
                manager.ShowIntensityPillars = false;
                manager.ShowLightning = false;
                manager.ShowPrecipitation = false;
            }
        }

        [MenuItem("Tools/Weather Visualization/Visibility/Pillars Only", false, 103)]
        public static void PillarsOnly()
        {
            var manager = Object.FindObjectOfType<VolumetricWeatherManager>();
            if (manager != null)
            {
                manager.ShowVolumetricClouds = false;
                manager.ShowIntensityPillars = true;
                manager.ShowLightning = false;
                manager.ShowPrecipitation = false;
            }
        }

        #endregion

        #region Debug

        [MenuItem("Tools/Weather Visualization/Debug/Log System Status", false, 150)]
        public static void LogSystemStatus()
        {
            var manager = Object.FindObjectOfType<VolumetricWeatherManager>();
            var simulator = Object.FindObjectOfType<WeatherSimulator>();
            var cloudVolume = Object.FindObjectOfType<VolumetricCloudVolume>();
            var pillarRenderer = Object.FindObjectOfType<IntensityPillarRenderer>();
            var lightning = Object.FindObjectOfType<VolumetricLightning>();
            var precipitation = Object.FindObjectOfType<PrecipitationVFX>();

            Debug.Log("=== Weather Visualization 3D System Status ===");
            Debug.Log($"VolumetricWeatherManager: {(manager != null ? "✓ Found" : "✗ Missing")}");
            Debug.Log($"WeatherSimulator: {(simulator != null ? "✓ Found" : "✗ Missing")}");
            Debug.Log($"VolumetricCloudVolume: {(cloudVolume != null ? "✓ Found" : "✗ Missing")}");
            Debug.Log($"IntensityPillarRenderer: {(pillarRenderer != null ? "✓ Found" : "✗ Missing")}");
            Debug.Log($"VolumetricLightning: {(lightning != null ? "✓ Found" : "✗ Missing")}");
            Debug.Log($"PrecipitationVFX: {(precipitation != null ? "✓ Found" : "✗ Missing")}");

            if (Application.isPlaying && simulator != null)
            {
                var stats = simulator.GetStats();
                Debug.Log($"--- Runtime Stats ---");
                Debug.Log($"Scenario: {stats.ScenarioName}");
                Debug.Log($"Active Cells: {stats.ActiveCellCount}");
                Debug.Log($"Sim Time: {stats.SimulationTime:F1}s");
                Debug.Log($"Time Scale: {stats.TimeScale:F1}x");
                Debug.Log($"Running: {stats.IsRunning}");
            }
        }

        [MenuItem("Tools/Weather Visualization/Debug/Focus Scene View on Weather", false, 151)]
        public static void FocusOnWeather()
        {
            var root = GameObject.Find("WeatherVisualization3D");
            if (root != null)
            {
                Selection.activeGameObject = root;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
            else
            {
                var manager = Object.FindObjectOfType<VolumetricWeatherManager>();
                if (manager != null)
                {
                    Selection.activeGameObject = manager.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }
        }

        [MenuItem("Tools/Weather Visualization/Debug/Create Debug Camera", false, 152)]
        public static void CreateDebugCamera()
        {
            var existing = GameObject.Find("WeatherDebugCamera");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var camObj = new GameObject("WeatherDebugCamera");
            var camera = camObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 10f;
            camera.farClipPlane = 500000f;
            camera.fieldOfView = 60f;

            // Position above looking down
            camObj.transform.position = new Vector3(0, 30000f, -50000f);
            camObj.transform.LookAt(Vector3.up * 15000f);

            camObj.AddComponent<FreeFlyCamera>();

            Selection.activeGameObject = camObj;
            Debug.Log("[WeatherQuickActions] Created debug camera with FreeFly controls");
        }

        [MenuItem("Tools/Weather Visualization/Debug/Log RainViewer Status", false, 153)]
        public static void LogRainViewerStatus()
        {
            var provider = Object.FindObjectOfType<RainViewer3DProvider>();
            if (provider != null)
            {
                Debug.Log("=== RainViewer 3D Provider Status ===");
                Debug.Log($"Status: {provider.Status}");
                Debug.Log($"Data Valid: {provider.IsDataValid}");
                Debug.Log($"Last Timestamp: {provider.LastRadarTimestamp}");
                Debug.Log($"Cache: {provider.GetCacheStats()}");
            }
            else
            {
                Debug.Log("[WeatherQuickActions] No RainViewer3DProvider found in scene.");
            }
        }

        #endregion

        #region Shader Enhancement

        [MenuItem("Tools/Weather Visualization/Clouds/Use Enhanced Shader", false, 175)]
        public static void EnableEnhancedShader()
        {
            var volumes = Object.FindObjectsOfType<VolumetricCloudVolume>();
            foreach (var volume in volumes)
            {
                var field = volume.GetType().GetField("_useEnhancedShader",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(volume, true);
                    var initMethod = volume.GetType().GetMethod("InitializeMaterial",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    initMethod?.Invoke(volume, null);
                }
            }
            Debug.Log($"[WeatherQuickActions] Enabled enhanced shader on {volumes.Length} cloud volume(s)");
        }

        [MenuItem("Tools/Weather Visualization/Clouds/Use Original Shader", false, 176)]
        public static void DisableEnhancedShader()
        {
            var volumes = Object.FindObjectsOfType<VolumetricCloudVolume>();
            foreach (var volume in volumes)
            {
                var field = volume.GetType().GetField("_useEnhancedShader",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(volume, false);
                    var initMethod = volume.GetType().GetMethod("InitializeMaterial",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    initMethod?.Invoke(volume, null);
                }
            }
            Debug.Log($"[WeatherQuickActions] Switched to original shader on {volumes.Length} cloud volume(s)");
        }

        #endregion

        #region Prefabs

        [MenuItem("Tools/Weather Visualization/Prefabs/Create All Prefabs", false, 180)]
        public static void CreateAllPrefabsMenu()
        {
            WeatherPrefabFactory.CreateAllPrefabs();
        }

        [MenuItem("Tools/Weather Visualization/Prefabs/Create Missing Prefabs Only", false, 181)]
        public static void CreateMissingPrefabsMenu()
        {
            WeatherPrefabFactory.CreateMissingPrefabsOnly();
        }

        [MenuItem("Tools/Weather Visualization/Prefabs/Select Prefab Registry", false, 190)]
        public static void SelectPrefabRegistry()
        {
            var registry = WeatherPrefabRegistry.GetOrCreate();
            if (registry != null)
            {
                Selection.activeObject = registry;
            }
            else
            {
                EditorUtility.DisplayDialog("No Registry",
                    "No prefab registry found. Create prefabs first.",
                    "OK");
            }
        }

        #endregion

        #region Cleanup

        [MenuItem("Tools/Weather Visualization/Remove All Weather Objects", false, 200)]
        public static void RemoveAllWeatherObjects()
        {
            if (!EditorUtility.DisplayDialog("Remove Weather System",
                "This will remove all weather visualization objects from the scene.\n\n" +
                "This action cannot be undone. Continue?",
                "Remove", "Cancel"))
            {
                return;
            }

            RemoveExistingSystem();
            Debug.Log("[WeatherQuickActions] Removed all weather objects");
        }

        #endregion

        #region Helpers

        private static bool FindExistingSystem()
        {
            return Object.FindObjectOfType<VolumetricWeatherManager>() != null ||
                   Object.FindObjectOfType<WeatherSimulator>() != null ||
                   GameObject.Find("WeatherVisualization3D") != null;
        }

        private static void RemoveExistingSystem()
        {
            // Remove root object
            var root = GameObject.Find("WeatherVisualization3D");
            if (root != null)
                Object.DestroyImmediate(root);

            // Remove any orphaned components
            var managers = Object.FindObjectsOfType<VolumetricWeatherManager>();
            foreach (var m in managers)
                Object.DestroyImmediate(m.gameObject);

            var simulators = Object.FindObjectsOfType<WeatherSimulator>();
            foreach (var s in simulators)
                Object.DestroyImmediate(s.gameObject);

            var clouds = Object.FindObjectsOfType<VolumetricCloudVolume>();
            foreach (var c in clouds)
                Object.DestroyImmediate(c.gameObject);

            var pillars = Object.FindObjectsOfType<IntensityPillarRenderer>();
            foreach (var p in pillars)
                Object.DestroyImmediate(p.gameObject);

            var lightning = Object.FindObjectsOfType<VolumetricLightning>();
            foreach (var l in lightning)
                Object.DestroyImmediate(l.gameObject);

            var precip = Object.FindObjectsOfType<PrecipitationVFX>();
            foreach (var p in precip)
                Object.DestroyImmediate(p.gameObject);
        }

        #endregion
    }
}
