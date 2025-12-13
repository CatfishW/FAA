using UnityEngine;

namespace WeatherRadar
{
    /// <summary>
    /// ScriptableObject configuration for Weather Radar display and behavior.
    /// Allows designers to customize radar appearance without code changes.
    /// </summary>
    [CreateAssetMenu(fileName = "WeatherRadarConfig", menuName = "Aviation/Weather Radar Config")]
    public class WeatherRadarConfig : ScriptableObject
    {
        [Header("Display Settings")]
        [Tooltip("Base resolution of radar texture")]
        public int textureResolution = 512;
        
        [Tooltip("UI scale multiplier")]
        [Range(0.5f, 2f)]
        public float uiScale = 1f;

        [Header("Sweep Settings")]
        [Tooltip("Sweep rotation speed in degrees per second")]
        [Range(30f, 360f)]
        public float sweepSpeed = 180f;
        
        [Tooltip("Sweep line width in pixels")]
        [Range(1f, 10f)]
        public float sweepLineWidth = 3f;
        
        [Tooltip("Sweep line color")]
        public Color sweepLineColor = new Color(0f, 1f, 0f, 0.8f);
        
        [Tooltip("Sweep fade trail length in degrees")]
        [Range(0f, 90f)]
        public float sweepTrailLength = 30f;

        [Header("Weather Colors")]
        [Tooltip("Color for light precipitation (0-20 dBZ)")]
        public Color lightPrecipColor = new Color(0f, 0.8f, 0f, 1f);  // Green
        
        [Tooltip("Color for moderate precipitation (20-40 dBZ)")]
        public Color moderatePrecipColor = new Color(1f, 1f, 0f, 1f);  // Yellow
        
        [Tooltip("Color for heavy precipitation (40-50 dBZ)")]
        public Color heavyPrecipColor = new Color(1f, 0.5f, 0f, 1f);  // Orange
        
        [Tooltip("Color for extreme precipitation (50+ dBZ)")]
        public Color extremePrecipColor = new Color(1f, 0f, 0f, 1f);  // Red
        
        [Tooltip("Color for turbulence indication")]
        public Color turbulenceColor = new Color(1f, 0f, 1f, 1f);  // Magenta

        [Header("Range Ring Settings")]
        [Tooltip("Ring color")]
        public Color rangeRingColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        
        [Tooltip("Ring line width")]
        [Range(0.5f, 3f)]
        public float rangeRingWidth = 1f;
        
        [Tooltip("Number of range rings to display")]
        [Range(1, 8)]
        public int rangeRingCount = 4;
        
        [Tooltip("Show range labels")]
        public bool showRangeLabels = true;

        [Header("Heading Indicator")]
        [Tooltip("Heading line color")]
        public Color headingLineColor = new Color(1f, 1f, 1f, 0.8f);
        
        [Tooltip("Compass tick color")]
        public Color compassTickColor = new Color(0.8f, 0.8f, 0.8f, 0.6f);

        [Header("Waypoint Display")]
        [Tooltip("Waypoint marker color")]
        public Color waypointColor = new Color(0f, 1f, 1f, 1f);  // Cyan
        
        [Tooltip("VOR marker color")]
        public Color vorColor = new Color(0.8f, 0.8f, 1f, 1f);  // Light blue
        
        [Tooltip("Airport marker color")]
        public Color airportColor = new Color(1f, 1f, 1f, 1f);  // White
        
        [Tooltip("Waypoint label font size")]
        [Range(8, 16)]
        public int waypointFontSize = 10;

        [Header("Background")]
        [Tooltip("Radar background color")]
        public Color backgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f);
        
        [Tooltip("Ground clutter color (for MAP mode)")]
        public Color groundClutterColor = new Color(0.3f, 0.2f, 0f, 0.5f);

        [Header("Antenna Settings")]
        [Tooltip("Minimum tilt angle")]
        public float minTilt = -15f;
        
        [Tooltip("Maximum tilt angle")]
        public float maxTilt = 15f;
        
        [Tooltip("Tilt step increment")]
        public float tiltStep = 0.5f;
        
        [Tooltip("Minimum gain")]
        public float minGain = -8f;
        
        [Tooltip("Maximum gain")]
        public float maxGain = 8f;

        [Header("Performance")]
        [Tooltip("Legacy: Update timing is now controlled by WeatherRadarPanel.sweepCycleDuration")]
        [Range(0.5f, 10f)]
        public float updateInterval = 4f;
        
        [Tooltip("Enable smooth transitions")]
        public bool smoothTransitions = true;
        
        [Tooltip("Transition smoothing factor")]
        [Range(0.01f, 0.5f)]
        public float smoothingFactor = 0.1f;

        [Header("Animation")]
        [Tooltip("Enable animations")]
        public bool enableAnimations = true;
        
        [Tooltip("Fade duration for visibility changes")]
        [Range(0.1f, 1f)]
        public float fadeAnimationDuration = 0.3f;

        /// <summary>
        /// Get precipitation color based on intensity (0-1 normalized)
        /// </summary>
        public Color GetPrecipitationColor(float intensity)
        {
            if (intensity < 0.25f)
                return Color.Lerp(Color.clear, lightPrecipColor, intensity * 4f);
            else if (intensity < 0.5f)
                return Color.Lerp(lightPrecipColor, moderatePrecipColor, (intensity - 0.25f) * 4f);
            else if (intensity < 0.75f)
                return Color.Lerp(moderatePrecipColor, heavyPrecipColor, (intensity - 0.5f) * 4f);
            else
                return Color.Lerp(heavyPrecipColor, extremePrecipColor, (intensity - 0.75f) * 4f);
        }

        /// <summary>
        /// Get color based on dBZ reflectivity value
        /// </summary>
        public Color GetColorFromDBZ(float dbz)
        {
            if (dbz < 15f) return Color.clear;
            if (dbz < 30f) return lightPrecipColor;
            if (dbz < 40f) return moderatePrecipColor;
            if (dbz < 50f) return heavyPrecipColor;
            return extremePrecipColor;
        }

        /// <summary>
        /// Get range ring spacing for current range
        /// </summary>
        public float GetRingSpacing(float totalRange)
        {
            return totalRange / rangeRingCount;
        }

        /// <summary>
        /// Convert world distance to radar screen distance
        /// </summary>
        public float WorldToRadarDistance(float worldDistanceNM, float currentRange, float radarRadius)
        {
            return (worldDistanceNM / currentRange) * radarRadius;
        }

        /// <summary>
        /// Convert radar screen distance to world distance
        /// </summary>
        public float RadarToWorldDistance(float radarDistance, float currentRange, float radarRadius)
        {
            return (radarDistance / radarRadius) * currentRange;
        }
    }
}
