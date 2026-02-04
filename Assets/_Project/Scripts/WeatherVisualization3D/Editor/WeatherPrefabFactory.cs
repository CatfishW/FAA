using UnityEngine;
using UnityEditor;
using System.IO;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Factory for creating weather visualization prefabs.
    /// Creates properly configured prefabs for all weather system components.
    /// </summary>
    public static class WeatherPrefabFactory
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs/WeatherVisualization";
        private const string RegistryFolder = "Assets/_Project/ScriptableObjects/WeatherVisualization";

        #region Menu Items

        [MenuItem("Tools/Weather Visualization/Prefabs/Create All Prefabs", false, 300)]
        public static void CreateAllPrefabs()
        {
            if (!Directory.Exists(PrefabFolder))
            {
                Directory.CreateDirectory(PrefabFolder);
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Weather Prefabs");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                // Create all prefabs
                CreateWeatherSystemRootPrefab();
                CreateWeatherSimulatorPrefab();
                CreateVolumetricCloudVolumePrefab();
                CreateIntensityPillarRendererPrefab();
                CreateVolumetricLightningPrefab();
                CreatePrecipitationVFXPrefab();

                // Create or update registry
                CreatePrefabRegistry();

                Undo.CollapseUndoOperations(undoGroup);

                EditorUtility.DisplayDialog("Success",
                    "All weather prefabs created successfully!\n\n" +
                    $"Location: {PrefabFolder}",
                    "OK");
            }
            catch (System.Exception e)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogError($"[WeatherPrefabFactory] Failed to create prefabs: {e.Message}");
                EditorUtility.DisplayDialog("Error",
                    $"Failed to create prefabs: {e.Message}",
                    "OK");
            }
        }

        [MenuItem("Tools/Weather Visualization/Prefabs/Create Missing Prefabs Only", false, 301)]
        public static void CreateMissingPrefabsOnly()
        {
            if (!Directory.Exists(PrefabFolder))
            {
                Directory.CreateDirectory(PrefabFolder);
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Missing Weather Prefabs");

            int created = 0;

            if (!AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/WeatherSystemRoot.prefab"))
            {
                CreateWeatherSystemRootPrefab();
                created++;
            }
            if (!AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/WeatherSimulator.prefab"))
            {
                CreateWeatherSimulatorPrefab();
                created++;
            }
            if (!AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/VolumetricCloudVolume.prefab"))
            {
                CreateVolumetricCloudVolumePrefab();
                created++;
            }
            if (!AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/IntensityPillarRenderer.prefab"))
            {
                CreateIntensityPillarRendererPrefab();
                created++;
            }
            if (!AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/VolumetricLightning.prefab"))
            {
                CreateVolumetricLightningPrefab();
                created++;
            }
            if (!AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/PrecipitationVFX.prefab"))
            {
                CreatePrecipitationVFXPrefab();
                created++;
            }

            CreatePrefabRegistry();

            EditorUtility.DisplayDialog("Complete",
                created > 0
                    ? $"Created {created} missing prefab(s)."
                    : "All prefabs already exist.",
                "OK");
        }

        #endregion

        #region Prefab Creation Methods

        public static GameObject CreateWeatherSystemRootPrefab()
        {
            string path = $"{PrefabFolder}/WeatherSystemRoot.prefab";

            // Check if prefab already exists
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            // Create GameObject
            GameObject go = new GameObject("WeatherSystemRoot");
            go.AddComponent<VolumetricWeatherManager>();

            // Create prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            Debug.Log($"[WeatherPrefabFactory] Created prefab: {path}");
            return prefab;
        }

        public static GameObject CreateWeatherSimulatorPrefab()
        {
            string path = $"{PrefabFolder}/WeatherSimulator.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject go = new GameObject("WeatherSimulator");
            go.AddComponent<WeatherSimulator>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            Debug.Log($"[WeatherPrefabFactory] Created prefab: {path}");
            return prefab;
        }

        public static GameObject CreateVolumetricCloudVolumePrefab()
        {
            string path = $"{PrefabFolder}/VolumetricCloudVolume.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject go = new GameObject("VolumetricCloudVolume");

            // Add mesh filter and renderer
            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            var meshRenderer = go.AddComponent<MeshRenderer>();

            // Add volumetric cloud volume component
            go.AddComponent<VolumetricCloudVolume>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            Debug.Log($"[WeatherPrefabFactory] Created prefab: {path}");
            return prefab;
        }

        public static GameObject CreateIntensityPillarRendererPrefab()
        {
            string path = $"{PrefabFolder}/IntensityPillarRenderer.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject go = new GameObject("IntensityPillarRenderer");
            var renderer = go.AddComponent<IntensityPillarRenderer>();

            // Try to find and assign the pillar material
            var pillarMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/Weather/IntensityPillar.mat");
            if (pillarMaterial != null)
            {
                var materialField = renderer.GetType().GetField("pillarMaterial",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                materialField?.SetValue(renderer, pillarMaterial);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            Debug.Log($"[WeatherPrefabFactory] Created prefab: {path}");
            return prefab;
        }

        public static GameObject CreateVolumetricLightningPrefab()
        {
            string path = $"{PrefabFolder}/VolumetricLightning.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject go = new GameObject("VolumetricLightning");
            go.AddComponent<VolumetricLightning>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            Debug.Log($"[WeatherPrefabFactory] Created prefab: {path}");
            return prefab;
        }

        public static GameObject CreatePrecipitationVFXPrefab()
        {
            string path = $"{PrefabFolder}/PrecipitationVFX.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject go = new GameObject("PrecipitationVFX");
            go.AddComponent<PrecipitationVFX>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            Debug.Log($"[WeatherPrefabFactory] Created prefab: {path}");
            return prefab;
        }

        public static WeatherPrefabRegistry CreatePrefabRegistry()
        {
            // Ensure folder exists
            if (!Directory.Exists(RegistryFolder))
            {
                Directory.CreateDirectory(RegistryFolder);
            }

            string path = $"{RegistryFolder}/WeatherPrefabRegistry.asset";

            // Check if registry already exists
            var existing = AssetDatabase.LoadAssetAtPath<WeatherPrefabRegistry>(path);
            if (existing != null)
            {
                // Update references
                UpdateRegistryReferences(existing);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }

            // Create new registry
            WeatherPrefabRegistry registry = ScriptableObject.CreateInstance<WeatherPrefabRegistry>();
            UpdateRegistryReferences(registry);

            AssetDatabase.CreateAsset(registry, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[WeatherPrefabFactory] Created registry: {path}");
            return registry;
        }

        private static void UpdateRegistryReferences(WeatherPrefabRegistry registry)
        {
            registry.weatherSystemRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabFolder}/WeatherSystemRoot.prefab");
            registry.weatherSimulator = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabFolder}/WeatherSimulator.prefab");
            registry.volumetricCloudVolume = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabFolder}/VolumetricCloudVolume.prefab");
            registry.intensityPillarRenderer = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabFolder}/IntensityPillarRenderer.prefab");
            registry.volumetricLightning = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabFolder}/VolumetricLightning.prefab");
            registry.precipitationVFX = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabFolder}/PrecipitationVFX.prefab");
            registry.lightningBolt = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabFolder}/LightningBolt.prefab");
        }

        #endregion

        #region Utility

        /// <summary>
        /// Instantiate a weather system from prefabs
        /// </summary>
        public static GameObject InstantiateWeatherSystem(
            bool clouds = true,
            bool pillars = true,
            bool lightning = true,
            bool precipitation = true)
        {
            var registry = WeatherPrefabRegistry.GetOrCreate();
            if (registry == null)
            {
                Debug.LogError("[WeatherPrefabFactory] No prefab registry found. Create prefabs first.");
                return null;
            }

            if (!registry.IsComplete())
            {
                Debug.LogError("[WeatherPrefabFactory] Prefab registry is incomplete. Create missing prefabs.");
                return null;
            }

            // Instantiate root
            GameObject root = Object.Instantiate(registry.weatherSystemRoot);
            root.name = "WeatherVisualization3D";
            Undo.RegisterCreatedObjectUndo(root, "Create Weather System");

            // Get manager
            var manager = root.GetComponent<VolumetricWeatherManager>();

            // Create simulator
            GameObject simObj = Object.Instantiate(registry.weatherSimulator, root.transform);
            simObj.name = "WeatherSimulator";
            var simulator = simObj.GetComponent<WeatherSimulator>();

            // Create renderers
            if (clouds && registry.volumetricCloudVolume != null)
            {
                GameObject cloudObj = Object.Instantiate(registry.volumetricCloudVolume, root.transform);
                cloudObj.name = "VolumetricCloudVolume";
            }

            if (pillars && registry.intensityPillarRenderer != null)
            {
                GameObject pillarObj = Object.Instantiate(registry.intensityPillarRenderer, root.transform);
                pillarObj.name = "IntensityPillarRenderer";
                var pillarRenderer = pillarObj.GetComponent<IntensityPillarRenderer>();

                // Link to simulator
                var simField = pillarRenderer.GetType().GetField("weatherSimulator",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                simField?.SetValue(pillarRenderer, simulator);
            }

            if (lightning && registry.volumetricLightning != null)
            {
                GameObject lightningObj = Object.Instantiate(registry.volumetricLightning, root.transform);
                lightningObj.name = "VolumetricLightning";
                var lightningEffect = lightningObj.GetComponent<VolumetricLightning>();

                var simField = lightningEffect.GetType().GetField("weatherSimulator",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                simField?.SetValue(lightningEffect, simulator);
            }

            if (precipitation && registry.precipitationVFX != null)
            {
                GameObject precipObj = Object.Instantiate(registry.precipitationVFX, root.transform);
                precipObj.name = "PrecipitationVFX";
                var precipEffect = precipObj.GetComponent<PrecipitationVFX>();

                var simField = precipEffect.GetType().GetField("weatherSimulator",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                simField?.SetValue(precipEffect, simulator);
            }

            return root;
        }

        #endregion
    }
}
