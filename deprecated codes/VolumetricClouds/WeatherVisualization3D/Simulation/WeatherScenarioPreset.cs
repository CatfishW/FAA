using UnityEngine;
using System;

namespace WeatherVisualization3D
{
    /// <summary>
    /// ScriptableObject defining weather scenario configurations.
    /// Includes presets for common aviation weather scenarios.
    /// </summary>
    [CreateAssetMenu(fileName = "WeatherScenario", menuName = "Weather Visualization/Scenario Preset")]
    public class WeatherScenarioPreset : ScriptableObject
    {
        [Header("Scenario Info")]
        [Tooltip("Display name for this scenario")]
        public string scenarioName = "Custom Scenario";
        
        [TextArea(2, 4)]
        [Tooltip("Description of weather conditions")]
        public string description = "Custom weather scenario";
        
        [Tooltip("Scenario type classification")]
        public ScenarioType scenarioType = ScenarioType.ScatteredShowers;

        [Header("Storm Cell Configuration")]
        [Tooltip("Number of storm cells to generate")]
        [Range(1, 20)]
        public int cellCount = 5;
        
        [Tooltip("Spawn area size in world units")]
        public Vector2 spawnAreaSize = new Vector2(50000f, 50000f);
        
        [Tooltip("Minimum distance between cell centers")]
        [Range(1000f, 20000f)]
        public float minimumCellSpacing = 5000f;

        [Header("Cell Size Ranges")]
        [Tooltip("Minimum cell radius in world units")]
        [Range(500f, 10000f)]
        public float minCellRadius = 2000f;
        
        [Tooltip("Maximum cell radius in world units")]
        [Range(1000f, 30000f)]
        public float maxCellRadius = 8000f;
        
        [Tooltip("Cell radius variation over lifetime (multiplier)")]
        [Range(0f, 1f)]
        public float radiusVariation = 0.3f;

        [Header("Intensity Distribution")]
        [Tooltip("Probability weights for each intensity level [Light, Moderate, Heavy, Extreme]")]
        public float[] intensityWeights = new float[] { 0.3f, 0.4f, 0.2f, 0.1f };
        
        [Tooltip("Whether cells can change intensity over time")]
        public bool dynamicIntensity = true;
        
        [Tooltip("Rate of intensity change (0 = static, 1 = rapid)")]
        [Range(0f, 1f)]
        public float intensityChangeRate = 0.3f;

        [Header("Altitude Configuration")]
        [Tooltip("Base cloud altitude in feet MSL")]
        [Range(1000f, 15000f)]
        public float baseAltitudeFeet = 5000f;
        
        [Tooltip("Maximum cloud top altitude in feet MSL")]
        [Range(10000f, 60000f)]
        public float maxTopAltitudeFeet = 45000f;
        
        [Tooltip("Altitude variation range in feet")]
        [Range(0f, 10000f)]
        public float altitudeVariation = 3000f;

        [Header("Cell Lifecycle")]
        [Tooltip("Minimum cell lifetime in seconds")]
        [Range(30f, 600f)]
        public float minLifetime = 120f;
        
        [Tooltip("Maximum cell lifetime in seconds")]
        [Range(60f, 1800f)]
        public float maxLifetime = 600f;
        
        [Tooltip("Growth phase duration (fraction of lifetime)")]
        [Range(0.1f, 0.5f)]
        public float growthPhaseFraction = 0.25f;
        
        [Tooltip("Mature phase duration (fraction of lifetime)")]
        [Range(0.2f, 0.6f)]
        public float maturePhaseFraction = 0.5f;

        [Header("Movement")]
        [Tooltip("Average cell movement speed in knots")]
        [Range(0f, 100f)]
        public float averageSpeedKnots = 25f;
        
        [Tooltip("Speed variation range in knots")]
        [Range(0f, 30f)]
        public float speedVariation = 10f;
        
        [Tooltip("Primary movement direction in degrees (0 = North)")]
        [Range(0f, 360f)]
        public float primaryDirection = 270f;
        
        [Tooltip("Direction variation in degrees")]
        [Range(0f, 90f)]
        public float directionVariation = 30f;

        [Header("Special Effects")]
        [Tooltip("Enable lightning effects")]
        public bool enableLightning = true;
        
        [Tooltip("Lightning frequency (flashes per minute for intense cells)")]
        [Range(0f, 60f)]
        public float lightningFrequency = 12f;
        
        [Tooltip("Enable precipitation visualization")]
        public bool enablePrecipitation = true;
        
        [Tooltip("Enable turbulence indication")]
        public bool enableTurbulence = true;

        [Header("Squall Line Settings (if applicable)")]
        [Tooltip("Squall line orientation in degrees (0 = North-South)")]
        [Range(0f, 180f)]
        public float squallLineOrientation = 45f;
        
        [Tooltip("Squall line length in world units")]
        [Range(10000f, 100000f)]
        public float squallLineLength = 50000f;
        
        [Tooltip("Squall line width (depth) in world units")]
        [Range(5000f, 30000f)]
        public float squallLineWidth = 15000f;

        /// <summary>
        /// Get a random intensity level based on configured weights
        /// </summary>
        public IntensityLevel GetRandomIntensity()
        {
            float total = 0f;
            foreach (float w in intensityWeights)
                total += w;

            float roll = UnityEngine.Random.value * total;
            float cumulative = 0f;

            for (int i = 0; i < intensityWeights.Length && i < 4; i++)
            {
                cumulative += intensityWeights[i];
                if (roll <= cumulative)
                    return (IntensityLevel)(i + 1);
            }

            return IntensityLevel.Moderate;
        }

        /// <summary>
        /// Get a random cell radius within configured range
        /// </summary>
        public float GetRandomRadius()
        {
            return UnityEngine.Random.Range(minCellRadius, maxCellRadius);
        }

        /// <summary>
        /// Get a random lifetime within configured range
        /// </summary>
        public float GetRandomLifetime()
        {
            return UnityEngine.Random.Range(minLifetime, maxLifetime);
        }

        /// <summary>
        /// Get a random movement velocity based on configuration
        /// </summary>
        public Vector2 GetRandomVelocity()
        {
            float speed = averageSpeedKnots + UnityEngine.Random.Range(-speedVariation, speedVariation);
            float direction = primaryDirection + UnityEngine.Random.Range(-directionVariation, directionVariation);
            float radians = (direction + 180f) * Mathf.Deg2Rad;
            float speedUnitsPerSec = speed * 0.514444f;
            
            return new Vector2(
                Mathf.Sin(radians) * speedUnitsPerSec,
                Mathf.Cos(radians) * speedUnitsPerSec
            );
        }

        /// <summary>
        /// Get a random base altitude in world units
        /// </summary>
        public float GetRandomBaseAltitude()
        {
            return baseAltitudeFeet + UnityEngine.Random.Range(-altitudeVariation * 0.5f, altitudeVariation * 0.5f);
        }

        /// <summary>
        /// Get the maximum top altitude based on intensity
        /// </summary>
        public float GetTopAltitudeForIntensity(IntensityLevel intensity)
        {
            float fraction = intensity switch
            {
                IntensityLevel.Light => 0.4f,
                IntensityLevel.Moderate => 0.6f,
                IntensityLevel.Heavy => 0.85f,
                IntensityLevel.Extreme => 1.0f,
                _ => 0.3f
            };

            float baseTop = baseAltitudeFeet + (maxTopAltitudeFeet - baseAltitudeFeet) * fraction;
            return baseTop + UnityEngine.Random.Range(-altitudeVariation, altitudeVariation);
        }

        #region Static Preset Generators
        
        public static WeatherScenarioPreset CreateScatteredShowers()
        {
            var preset = CreateInstance<WeatherScenarioPreset>();
            preset.scenarioName = "Scattered Showers";
            preset.description = "Isolated precipitation cells with light to moderate intensity.";
            preset.scenarioType = ScenarioType.ScatteredShowers;
            preset.cellCount = 8;
            preset.spawnAreaSize = new Vector2(60000f, 60000f);
            preset.minimumCellSpacing = 8000f;
            preset.minCellRadius = 1500f;
            preset.maxCellRadius = 5000f;
            preset.intensityWeights = new float[] { 0.5f, 0.35f, 0.12f, 0.03f };
            preset.baseAltitudeFeet = 4000f;
            preset.maxTopAltitudeFeet = 25000f;
            preset.enableLightning = false;
            return preset;
        }

        public static WeatherScenarioPreset CreateThunderstormCells()
        {
            var preset = CreateInstance<WeatherScenarioPreset>();
            preset.scenarioName = "Thunderstorm Cells";
            preset.description = "Active thunderstorm cells with significant vertical development.";
            preset.scenarioType = ScenarioType.ThunderstormCells;
            preset.cellCount = 5;
            preset.minCellRadius = 3000f;
            preset.maxCellRadius = 12000f;
            preset.intensityWeights = new float[] { 0.1f, 0.25f, 0.4f, 0.25f };
            preset.baseAltitudeFeet = 3000f;
            preset.maxTopAltitudeFeet = 45000f;
            preset.enableLightning = true;
            preset.lightningFrequency = 20f;
            return preset;
        }

        public static WeatherScenarioPreset CreateSquallLine()
        {
            var preset = CreateInstance<WeatherScenarioPreset>();
            preset.scenarioName = "Squall Line";
            preset.description = "Organized line of severe thunderstorms.";
            preset.scenarioType = ScenarioType.SquallLine;
            preset.cellCount = 12;
            preset.spawnAreaSize = new Vector2(60000f, 20000f);
            preset.minimumCellSpacing = 4000f;
            preset.intensityWeights = new float[] { 0.05f, 0.15f, 0.45f, 0.35f };
            preset.baseAltitudeFeet = 2500f;
            preset.maxTopAltitudeFeet = 50000f;
            preset.averageSpeedKnots = 45f;
            preset.enableLightning = true;
            preset.lightningFrequency = 30f;
            return preset;
        }

        public static WeatherScenarioPreset CreateSupercell()
        {
            var preset = CreateInstance<WeatherScenarioPreset>();
            preset.scenarioName = "Supercell";
            preset.description = "Isolated supercell with extreme intensity.";
            preset.scenarioType = ScenarioType.Supercell;
            preset.cellCount = 2;
            preset.minimumCellSpacing = 20000f;
            preset.minCellRadius = 8000f;
            preset.maxCellRadius = 20000f;
            preset.intensityWeights = new float[] { 0f, 0.05f, 0.35f, 0.6f };
            preset.baseAltitudeFeet = 2000f;
            preset.maxTopAltitudeFeet = 55000f;
            preset.enableLightning = true;
            preset.lightningFrequency = 40f;
            return preset;
        }

        #endregion
    }

    /// <summary>
    /// Classification of weather scenario types
    /// </summary>
    public enum ScenarioType
    {
        Custom = 0,
        ScatteredShowers = 1,
        ThunderstormCells = 2,
        SquallLine = 3,
        Supercell = 4,
        FrontalSystem = 5,
        TropicalSystem = 6
    }
    
    // Note: IntensityLevel enum is defined in WeatherVolumeData.cs
}
