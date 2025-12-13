using UnityEngine;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// ScriptableObject configuration for the 3D Weather Display System.
    /// Contains all visual and performance settings.
    /// </summary>
    [CreateAssetMenu(fileName = "Weather3DConfig", menuName = "Weather Radar/3D Weather Config")]
    public class Weather3DConfig : ScriptableObject
    {
        [Header("Grid Settings")]
        [Tooltip("Resolution of the 3D weather grid")]
        public Vector3Int gridResolution = new Vector3Int(64, 16, 64);
        
        [Tooltip("Coverage area in nautical miles")]
        [Range(20f, 320f)]
        public float coverageNM = 80f;
        
        [Tooltip("Maximum altitude in feet")]
        public float maxAltitudeFt = 50000f;

        [Header("Cloud Visualization")]
        [Tooltip("Maximum number of cloud particles")]
        [Range(100, 10000)]
        public int maxCloudParticles = 2000;
        
        [Tooltip("Cloud particle base size")]
        [Range(0.1f, 5f)]
        public float cloudParticleSize = 1.5f;
        
        [Tooltip("Cloud particle size variation")]
        [Range(0f, 2f)]
        public float cloudSizeVariation = 0.5f;
        
        [Tooltip("Cloud opacity")]
        [Range(0.1f, 1f)]
        public float cloudOpacity = 0.7f;
        
        [Tooltip("Enable cloud animation")]
        public bool animateClouds = true;
        
        [Tooltip("Cloud animation speed")]
        [Range(0.1f, 2f)]
        public float cloudAnimationSpeed = 0.5f;
        
        [Header("Enhanced Visual Effects")]
        [Tooltip("Enable emission/glow effects on weather cells")]
        public bool enableEmission = true;
        
        [Tooltip("Emission intensity multiplier")]
        [Range(0.1f, 2f)]
        public float emissionIntensity = 0.5f;
        
        [Tooltip("Soft edge falloff for cloud boundaries")]
        [Range(0f, 1f)]
        public float softEdgeFalloff = 0.3f;
        
        [Tooltip("Rim lighting intensity for depth perception")]
        [Range(0f, 1f)]
        public float rimLightIntensity = 0.4f;
        
        [Tooltip("Number of gradient color bands")]
        [Range(2, 8)]
        public int gradientQuality = 5;

        [Header("Precipitation Effects")]
        [Tooltip("Maximum precipitation particles")]
        [Range(100, 5000)]
        public int maxPrecipitationParticles = 1000;
        
        [Tooltip("Rain drop size")]
        [Range(0.01f, 0.5f)]
        public float rainDropSize = 0.1f;
        
        [Tooltip("Rain fall speed")]
        [Range(5f, 50f)]
        public float rainFallSpeed = 20f;
        
        [Tooltip("Snow flake size")]
        [Range(0.02f, 0.3f)]
        public float snowFlakeSize = 0.08f;
        
        [Tooltip("Snow fall speed")]
        [Range(1f, 10f)]
        public float snowFallSpeed = 3f;

        [Header("Thunderstorm Effects")]
        [Tooltip("Enable lightning effects")]
        public bool enableLightning = true;
        
        [Tooltip("Lightning flash duration")]
        [Range(0.05f, 0.5f)]
        public float lightningFlashDuration = 0.15f;
        
        [Tooltip("Minimum time between lightning strikes")]
        [Range(0.5f, 10f)]
        public float lightningMinInterval = 2f;
        
        [Tooltip("Maximum time between lightning strikes")]
        [Range(1f, 30f)]
        public float lightningMaxInterval = 8f;
        
        [Tooltip("Lightning bolt segments")]
        [Range(3, 20)]
        public int lightningSegments = 8;
        
        [Tooltip("Lightning bolt width")]
        [Range(0.1f, 2f)]
        public float lightningWidth = 0.3f;
        
        [Tooltip("Lightning color")]
        public Color lightningColor = new Color(0.9f, 0.95f, 1f, 1f);

        [Header("Turbulence Visualization")]
        [Tooltip("Turbulence zone opacity")]
        [Range(0.1f, 0.8f)]
        public float turbulenceOpacity = 0.4f;
        
        [Tooltip("Animate turbulence zones")]
        public bool animateTurbulence = true;
        
        [Tooltip("Turbulence animation frequency")]
        [Range(0.5f, 5f)]
        public float turbulenceAnimFrequency = 2f;

        [Header("Hazard Pillar Settings")]
        [Tooltip("Pillar ring count")]
        [Range(2, 10)]
        public int hazardPillarRings = 4;
        
        [Tooltip("Pillar pulse speed")]
        [Range(0.5f, 3f)]
        public float hazardPulseSpeed = 1.5f;
        
        [Tooltip("Pillar base width")]
        [Range(0.5f, 5f)]
        public float hazardPillarWidth = 2f;

        [Header("Intensity Colors")]
        [Tooltip("Light precipitation color")]
        public Color lightColor = new Color(0f, 0.8f, 0f, 0.6f);
        
        [Tooltip("Moderate precipitation color")]
        public Color moderateColor = new Color(1f, 1f, 0f, 0.7f);
        
        [Tooltip("Heavy precipitation color")]
        public Color heavyColor = new Color(1f, 0.5f, 0f, 0.8f);
        
        [Tooltip("Intense precipitation color")]
        public Color intenseColor = new Color(1f, 0f, 0f, 0.9f);
        
        [Tooltip("Extreme precipitation color")]
        public Color extremeColor = new Color(1f, 0f, 1f, 1f);

        [Header("Performance")]
        [Tooltip("Update rate for weather effects (Hz)")]
        [Range(1f, 30f)]
        public float effectUpdateRate = 10f;
        
        [Tooltip("LOD distance for reduced detail")]
        [Range(10f, 100f)]
        public float lodDistance = 50f;
        
        [Tooltip("Maximum visible range for effects")]
        [Range(50f, 500f)]
        public float maxVisibleRange = 200f;
        
        [Tooltip("Enable particle shadows")]
        public bool enableParticleShadows = false;
        
        [Tooltip("Cull particles outside camera frustum")]
        public bool frustumCulling = true;

        [Header("Display Options")]
        [Tooltip("Show vertical profile view")]
        public bool showVerticalProfile = true;
        
        [Tooltip("Profile view height")]
        [Range(100f, 500f)]
        public float profileViewHeight = 200f;
        
        [Tooltip("Show altitude labels")]
        public bool showAltitudeLabels = true;
        
        [Tooltip("Show distance rings")]
        public bool showDistanceRings = true;

        /// <summary>
        /// Get the color for a given intensity level (0-1)
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
        /// Get update interval based on effect update rate
        /// </summary>
        public float GetUpdateInterval()
        {
            return 1f / effectUpdateRate;
        }
    }
}
