using UnityEngine;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Registry of prefabs used by the weather visualization system.
    /// Assign prefabs in this ScriptableObject and reference them in setup scripts.
    /// </summary>
    [CreateAssetMenu(fileName = "WeatherPrefabRegistry", menuName = "Weather/Weather Prefab Registry")]
    public class WeatherPrefabRegistry : ScriptableObject
    {
        [Header("Core System Prefabs")]
        [Tooltip("Root weather system prefab with VolumetricWeatherManager")]
        public GameObject weatherSystemRoot;

        [Tooltip("Weather simulator prefab")]
        public GameObject weatherSimulator;

        [Header("Renderer Prefabs")]
        [Tooltip("Volumetric cloud volume renderer prefab")]
        public GameObject volumetricCloudVolume;

        [Tooltip("Intensity pillar renderer prefab")]
        public GameObject intensityPillarRenderer;

        [Header("Effect Prefabs")]
        [Tooltip("Lightning effect controller prefab")]
        public GameObject volumetricLightning;

        [Tooltip("Lightning bolt prefab (for individual bolts)")]
        public GameObject lightningBolt;

        [Tooltip("Precipitation effects prefab")]
        public GameObject precipitationVFX;

        [Tooltip("Rain particle system prefab")]
        public GameObject rainParticles;

        [Tooltip("Snow particle system prefab")]
        public GameObject snowParticles;

        /// <summary>
        /// Get the default registry path
        /// </summary>
        public static string DefaultPath => "Assets/_Project/ScriptableObjects/WeatherVisualization/WeatherPrefabRegistry.asset";

        /// <summary>
        /// Load or create the default registry
        /// </summary>
        public static WeatherPrefabRegistry GetOrCreate()
        {
            var registry = Resources.Load<WeatherPrefabRegistry>("WeatherPrefabRegistry");
            if (registry != null) return registry;

            #if UNITY_EDITOR
            // Try to load from default path
            registry = UnityEditor.AssetDatabase.LoadAssetAtPath<WeatherPrefabRegistry>(DefaultPath);
            if (registry != null) return registry;
            #endif

            return null;
        }

        /// <summary>
        /// Check if all required prefabs are assigned
        /// </summary>
        public bool IsComplete()
        {
            return weatherSystemRoot != null &&
                   weatherSimulator != null &&
                   volumetricCloudVolume != null &&
                   intensityPillarRenderer != null &&
                   volumetricLightning != null &&
                   precipitationVFX != null;
        }

        /// <summary>
        /// Get list of missing prefab assignments
        /// </summary>
        public string[] GetMissingPrefabs()
        {
            var missing = new System.Collections.Generic.List<string>();

            if (weatherSystemRoot == null) missing.Add("Weather System Root");
            if (weatherSimulator == null) missing.Add("Weather Simulator");
            if (volumetricCloudVolume == null) missing.Add("Volumetric Cloud Volume");
            if (intensityPillarRenderer == null) missing.Add("Intensity Pillar Renderer");
            if (volumetricLightning == null) missing.Add("Volumetric Lightning");
            if (precipitationVFX == null) missing.Add("Precipitation VFX");

            return missing.ToArray();
        }
    }
}
