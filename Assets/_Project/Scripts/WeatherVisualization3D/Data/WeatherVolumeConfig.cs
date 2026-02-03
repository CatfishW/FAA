using UnityEngine;
using System;

namespace WeatherVisualization3D
{
    /// <summary>
    /// ScriptableObject configuration for the Volumetric Weather Visualization System.
    /// Contains all visual, rendering, and performance settings.
    /// </summary>
    [CreateAssetMenu(fileName = "WeatherVolumeConfig", menuName = "Weather Visualization 3D/Volume Config")]
    public class WeatherVolumeConfig : ScriptableObject
    {
        #region Volume Settings

        [Header("Volume Settings")]
        [Tooltip("Resolution of the 3D weather volume (X, Y, Z). Y is altitude.")]
        public Vector3Int volumeResolution = new Vector3Int(64, 32, 64);

        [Tooltip("Coverage area radius in nautical miles")]
        [Range(10f, 320f)]
        public float coverageNM = 80f;

        [Tooltip("Maximum altitude in feet")]
        [Range(10000f, 60000f)]
        public float maxAltitudeFt = 50000f;

        [Tooltip("Minimum altitude in feet")]
        [Range(0f, 5000f)]
        public float minAltitudeFt = 0f;

        #endregion

        #region Raymarching Settings

        [Header("Raymarching Quality")]
        [Tooltip("Number of raymarching steps (higher = better quality, lower performance)")]
        [Range(32, 256)]
        public int raymarchSteps = 128;

        [Tooltip("Step size multiplier for raymarching")]
        [Range(0.5f, 2f)]
        public float stepSizeMultiplier = 1f;

        [Tooltip("Jitter amount for reducing banding artifacts")]
        [Range(0f, 1f)]
        public float jitterAmount = 0.5f;

        [Tooltip("Early ray termination threshold")]
        [Range(0.9f, 0.99f)]
        public float earlyTerminationThreshold = 0.95f;

        #endregion

        #region Cloud Appearance

        [Header("Cloud Appearance")]
        [Tooltip("Base cloud density multiplier")]
        [Range(0.1f, 5f)]
        public float cloudDensity = 1.5f;

        [Tooltip("Cloud detail scale (higher = finer detail)")]
        [Range(0.5f, 10f)]
        public float detailScale = 3f;

        [Tooltip("Cloud detail strength")]
        [Range(0f, 1f)]
        public float detailStrength = 0.5f;

        [Tooltip("Height-based density falloff")]
        public AnimationCurve heightDensityCurve = AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f);

        [Tooltip("Edge softness for cloud boundaries")]
        [Range(0f, 1f)]
        public float edgeSoftness = 0.3f;

        [Tooltip("Enable animated cloud turbulence")]
        public bool animateClouds = true;

        [Tooltip("Cloud animation speed")]
        [Range(0.01f, 1f)]
        public float animationSpeed = 0.1f;

        #endregion

        #region Lighting

        [Header("Cloud Lighting")]
        [Tooltip("Ambient light color for clouds")]
        public Color ambientColor = new Color(0.4f, 0.45f, 0.5f, 1f);

        [Tooltip("Sun/directional light color")]
        public Color sunColor = new Color(1f, 0.95f, 0.9f, 1f);

        [Tooltip("Light absorption coefficient")]
        [Range(0.1f, 2f)]
        public float lightAbsorption = 0.8f;

        [Tooltip("Forward scattering amount (silver lining effect)")]
        [Range(0f, 1f)]
        public float forwardScattering = 0.7f;

        [Tooltip("Multi-scattering approximation strength")]
        [Range(0f, 1f)]
        public float multiScatterStrength = 0.5f;

        [Tooltip("Enable self-shadowing")]
        public bool selfShadowing = true;

        [Tooltip("Shadow step count")]
        [Range(2, 16)]
        public int shadowSteps = 6;

        #endregion

        #region Intensity Colors

        [Header("Weather Intensity Colors (Aviation Standard)")]
        [Tooltip("Light precipitation color (Green)")]
        public Color lightColor = new Color(0f, 0.85f, 0f, 0.7f);

        [Tooltip("Moderate precipitation color (Yellow)")]
        public Color moderateColor = new Color(1f, 1f, 0f, 0.8f);

        [Tooltip("Heavy precipitation color (Orange)")]
        public Color heavyColor = new Color(1f, 0.55f, 0f, 0.85f);

        [Tooltip("Intense precipitation color (Red)")]
        public Color intenseColor = new Color(1f, 0f, 0f, 0.9f);

        [Tooltip("Extreme precipitation color (Magenta)")]
        public Color extremeColor = new Color(1f, 0f, 1f, 1f);

        [Tooltip("Storm core glow color")]
        public Color stormCoreColor = new Color(1f, 0.3f, 0.3f, 1f);

        #endregion

        #region Height Extrusion

        [Header("Volumetric Extrusion")]
        [Tooltip("Enable intensity-based height extrusion")]
        public bool enableHeightExtrusion = true;

        [Tooltip("Height extrusion multiplier")]
        [Range(0.5f, 3f)]
        public float heightExtrusionMultiplier = 1.5f;

        [Tooltip("Minimum height for light precipitation (feet)")]
        public float lightPrecipMinHeight = 2000f;

        [Tooltip("Maximum height for light precipitation (feet)")]
        public float lightPrecipMaxHeight = 15000f;

        [Tooltip("Minimum height for moderate precipitation (feet)")]
        public float moderatePrecipMinHeight = 1500f;

        [Tooltip("Maximum height for moderate precipitation (feet)")]
        public float moderatePrecipMaxHeight = 25000f;

        [Tooltip("Minimum height for heavy precipitation (feet)")]
        public float heavyPrecipMinHeight = 1000f;

        [Tooltip("Maximum height for heavy precipitation (feet)")]
        public float heavyPrecipMaxHeight = 35000f;

        [Tooltip("Minimum height for thunderstorm (feet)")]
        public float thunderstormMinHeight = 500f;

        [Tooltip("Maximum height for thunderstorm (feet)")]
        public float thunderstormMaxHeight = 50000f;

        #endregion

        #region Intensity Pillars

        [Header("Intensity Pillars")]
        [Tooltip("Enable vertical intensity pillars")]
        public bool showIntensityPillars = true;

        [Tooltip("Pillar opacity")]
        [Range(0.1f, 1f)]
        public float pillarOpacity = 0.6f;

        [Tooltip("Pillar width in world units")]
        [Range(100f, 2000f)]
        public float pillarWidth = 500f;

        [Tooltip("Animate pillars with pulsing effect")]
        public bool animatePillars = true;

        [Tooltip("Pillar pulse speed")]
        [Range(0.5f, 3f)]
        public float pillarPulseSpeed = 1.2f;

        #endregion

        #region Lightning Effects

        [Header("Lightning Effects")]
        [Tooltip("Enable lightning visualization")]
        public bool enableLightning = true;

        [Tooltip("Lightning color")]
        public Color lightningColor = new Color(0.95f, 0.95f, 1f, 1f);

        [Tooltip("Lightning flash intensity")]
        [Range(1f, 5f)]
        public float lightningIntensity = 2f;

        [Tooltip("Lightning flash duration in seconds")]
        [Range(0.05f, 0.3f)]
        public float lightningFlashDuration = 0.1f;

        [Tooltip("Minimum interval between strikes in seconds")]
        [Range(0.5f, 10f)]
        public float lightningMinInterval = 1.5f;

        [Tooltip("Maximum interval between strikes in seconds")]
        [Range(1f, 20f)]
        public float lightningMaxInterval = 6f;

        [Tooltip("Lightning bolt width")]
        [Range(10f, 100f)]
        public float lightningWidth = 30f;

        [Tooltip("Number of lightning bolt segments")]
        [Range(4, 20)]
        public int lightningSegments = 10;

        #endregion

        #region Precipitation Effects

        [Header("Precipitation Effects")]
        [Tooltip("Enable precipitation particles")]
        public bool enablePrecipitation = true;

        [Tooltip("Maximum precipitation particles")]
        [Range(100, 5000)]
        public int maxPrecipitationParticles = 2000;

        [Tooltip("Rain drop size")]
        [Range(0.01f, 0.2f)]
        public float rainDropSize = 0.05f;

        [Tooltip("Rain fall speed")]
        [Range(5f, 40f)]
        public float rainFallSpeed = 25f;

        [Tooltip("Rain streaking length")]
        [Range(1f, 5f)]
        public float rainStreakLength = 2f;

        [Tooltip("Snow flake size")]
        [Range(0.02f, 0.2f)]
        public float snowFlakeSize = 0.08f;

        [Tooltip("Snow fall speed")]
        [Range(1f, 8f)]
        public float snowFallSpeed = 3f;

        #endregion

        #region Performance

        [Header("Performance")]
        [Tooltip("Update rate for effects (updates per second)")]
        [Range(5f, 60f)]
        public float effectUpdateRate = 30f;

        [Tooltip("LOD distance - reduce quality beyond this distance")]
        [Range(10000f, 100000f)]
        public float lodDistance = 50000f;

        [Tooltip("Enable frustum culling for effects")]
        public bool frustumCulling = true;

        [Tooltip("Max visible range for effects")]
        [Range(50000f, 300000f)]
        public float maxVisibleRange = 150000f;

        #endregion

        #region Debug

        [Header("Debug")]
        [Tooltip("Show volume bounds gizmo")]
        public bool showVolumeBounds = false;

        [Tooltip("Show intensity heatmap overlay")]
        public bool showHeatmap = false;

        [Tooltip("Enable debug logging")]
        public bool debugLogging = false;

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get color for intensity level (0-1)
        /// </summary>
        public Color GetIntensityColor(float intensity)
        {
            if (intensity < 0.2f)
                return Color.Lerp(Color.clear, lightColor, intensity / 0.2f);
            else if (intensity < 0.4f)
                return Color.Lerp(lightColor, moderateColor, (intensity - 0.2f) / 0.2f);
            else if (intensity < 0.6f)
                return Color.Lerp(moderateColor, heavyColor, (intensity - 0.4f) / 0.2f);
            else if (intensity < 0.8f)
                return Color.Lerp(heavyColor, intenseColor, (intensity - 0.6f) / 0.2f);
            else
                return Color.Lerp(intenseColor, extremeColor, (intensity - 0.8f) / 0.2f);
        }

        /// <summary>
        /// Get altitude range for intensity level
        /// </summary>
        public void GetAltitudeRange(float intensity, out float minAlt, out float maxAlt)
        {
            if (intensity < 0.3f)
            {
                minAlt = lightPrecipMinHeight;
                maxAlt = lightPrecipMaxHeight;
            }
            else if (intensity < 0.5f)
            {
                minAlt = moderatePrecipMinHeight;
                maxAlt = moderatePrecipMaxHeight;
            }
            else if (intensity < 0.7f)
            {
                minAlt = heavyPrecipMinHeight;
                maxAlt = heavyPrecipMaxHeight;
            }
            else
            {
                minAlt = thunderstormMinHeight;
                maxAlt = thunderstormMaxHeight;
            }

            // Apply extrusion multiplier
            if (enableHeightExtrusion)
            {
                maxAlt *= heightExtrusionMultiplier;
                maxAlt = Mathf.Min(maxAlt, maxAltitudeFt);
            }
        }

        /// <summary>
        /// Get update interval based on effect update rate
        /// </summary>
        public float GetUpdateInterval()
        {
            return 1f / effectUpdateRate;
        }

        /// <summary>
        /// Create default configuration
        /// </summary>
        public static WeatherVolumeConfig CreateDefault()
        {
            var config = CreateInstance<WeatherVolumeConfig>();
            config.name = "DefaultWeatherVolumeConfig";
            return config;
        }

        #endregion
    }
}
