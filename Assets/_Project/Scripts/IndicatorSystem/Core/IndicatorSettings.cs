using UnityEngine;

namespace IndicatorSystem.Core
{
    /// <summary>
    /// ScriptableObject for global indicator system settings.
    /// Create via Assets > Create > Indicator System > Settings.
    /// </summary>
    [CreateAssetMenu(fileName = "IndicatorSettings", menuName = "Indicator System/Settings")]
    public class IndicatorSettings : ScriptableObject
    {
        [Header("General Settings")]
        [Tooltip("Enable/disable the entire indicator system")]
        public bool enabled = true;
        
        [Tooltip("Maximum number of indicators to show at once")]
        [Range(1, 100)]
        public int maxIndicators = 50;

        [Header("Edge Display")]
        [Tooltip("Padding from screen edges in pixels")]
        [Range(20f, 200f)]
        public float edgePadding = 50f;
        
        [Header("Size Configuration")]
        [Tooltip("Global scale multiplier for all indicators")]
        [Range(0.5f, 5f)]
        public float globalScale = 1.5f;
        
        [Tooltip("Size of indicator symbol/icon in pixels")]
        [Range(30f, 300f)]
        public float indicatorSize = 80f;
        
        [Tooltip("Size of off-screen arrows in pixels")]
        [Range(20f, 200f)]
        public float arrowSize = 60f;
        
        [Header("Symbol Rotation")]
        [Tooltip("Rotate symbol based on aircraft heading")]
        public bool rotateSymbolByHeading = true;
        
        [Tooltip("Rotation offset (degrees) - adjusts for sprite orientation")]
        [Range(-180f, 180f)]
        public float rotationOffset = 0f;
        
        [Tooltip("Limit rotation to prevent invisible angles (e.g., side view)")]
        public bool limitRotationAngles = false;
        
        [Tooltip("Minimum visible angle (degrees from straight-on)")]
        [Range(0f, 45f)]
        public float minVisibleAngle = 15f;
        
        [Tooltip("Enable 3D perspective rotation (X/Y rotation based on view angle)")]
        public bool enable3DRotation = true;
        
        [Tooltip("Maximum X tilt angle for perspective (degrees) - pitch effect")]
        [Range(0f, 60f)]
        public float maxPerspectiveTiltX = 35f;
        
        [Tooltip("Maximum Y tilt angle for perspective (degrees) - bank effect")]
        [Range(0f, 60f)]
        public float maxPerspectiveTiltY = 30f;
        
        [Header("Font Sizes")]
        [Tooltip("Font size for callsign/label text")]
        [Range(8f, 48f)]
        public float labelFontSize = 16f;
        
        [Tooltip("Font size for distance display")]
        [Range(8f, 40f)]
        public float distanceFontSize = 14f;
        
        [Tooltip("Font size for altitude display")]
        [Range(8f, 40f)]
        public float altitudeFontSize = 13f;

        [Header("Distance Display")]
        [Tooltip("Show distance labels on indicators")]
        public bool showDistanceLabels = true;
        
        [Tooltip("Maximum distance to display indicators (NM)")]
        [Range(10f, 500f)]
        public float maxDisplayDistance = 100f;
        
        [Tooltip("Minimum distance to display indicators (NM) - closer targets hidden")]
        [Range(0f, 10f)]
        public float minDisplayDistance = 0f;

        [Header("Distance-Based Scaling")]
        [Tooltip("Enable scaling indicators based on distance")]
        public bool enableDistanceScaling = true;
        
        [Tooltip("Distance at which indicator is at maximum scale (NM)")]
        [Range(0f, 20f)]
        public float closeDistanceNM = 5f;
        
        [Tooltip("Distance at which indicator is at minimum scale (NM)")]
        [Range(10f, 100f)]
        public float farDistanceNM = 40f;
        
        [Tooltip("Scale multiplier for close aircraft")]
        [Range(0.5f, 3f)]
        public float closeDistanceScale = 1.5f;
        
        [Tooltip("Scale multiplier for far aircraft")]
        [Range(0.2f, 1f)]
        public float farDistanceScale = 0.6f;

        [Header("Trails / Trajectories")]
        [Tooltip("Show trails behind moving indicators")]
        public bool showTrails = true;
        
        [Tooltip("Maximum number of trail points")]
        [Range(5, 50)]
        public int trailPointCount = 20;
        
        [Tooltip("Time between trail point samples (seconds)")]
        [Range(0.1f, 2f)]
        public float trailSampleInterval = 0.5f;
        
        [Tooltip("Trail fade duration (seconds)")]
        [Range(1f, 30f)]
        public float trailFadeDuration = 10f;
        
        [Tooltip("Trail line width")]
        [Range(1f, 10f)]
        public float trailWidth = 3f;
        
        [Tooltip("Trail color (alpha controls base opacity)")]
        public Color trailColor = new Color(0f, 1f, 1f, 0.6f);

        [Header("Altitude Display")]
        [Tooltip("Show relative altitude on indicators")]
        public bool showAltitudeIndicators = true;
        
        [Tooltip("Altitude display format: as text like +5, -3, 0 (in thousands of feet)")]
        public bool altitudeInThousands = true;
        
        [Tooltip("Altitude threshold for showing value (feet) - below this shows 0")]
        [Range(100f, 2000f)]
        public float altitudeThreshold = 500f;

        [Header("Navigation Lights")]
        [Tooltip("Show navigation lights on traffic indicators")]
        public bool showNavigationLights = true;
        
        [Tooltip("Size of navigation lights in pixels")]
        [Range(4f, 20f)]
        public float navLightSize = 8f;
        
        [Tooltip("Port (left) light color - standard is red")]
        public Color portLightColor = new Color(1f, 0f, 0f, 1f);
        
        [Tooltip("Starboard (right) light color - standard is green")]
        public Color starboardLightColor = new Color(0f, 1f, 0f, 1f);
        
        [Tooltip("Tail light color - standard is white")]
        public Color tailLightColor = new Color(1f, 1f, 1f, 0.9f);
        
        [Tooltip("Light intensity/glow")]
        [Range(0.5f, 2f)]
        public float navLightIntensity = 1.2f;
        
        [Tooltip("Enable blinking/strobe effect")]
        public bool blinkNavLights = false;
        
        [Tooltip("Blink rate in Hz")]
        [Range(0.5f, 3f)]
        public float navLightBlinkRate = 1f;

        [Header("Animation")]
        [Tooltip("Smooth indicator movement")]
        public bool smoothMovement = true;
        
        [Tooltip("Movement smoothing speed")]
        [Range(1f, 20f)]
        public float smoothSpeed = 10f;
        
        [Tooltip("Pulse effect on high-priority indicators")]
        public bool pulseHighPriority = true;
        
        [Tooltip("Pulse frequency in Hz")]
        [Range(0.5f, 5f)]
        public float pulseFrequency = 2f;

        [Header("Traffic Indicator Colors")]
        [Tooltip("Color for normal traffic (OtherTraffic/Proximate)")]
        public Color trafficNormalColor = new Color(0f, 1f, 1f, 1f); // Cyan
        
        [Tooltip("Color for Traffic Advisory")]
        public Color trafficAdvisoryColor = new Color(1f, 0.75f, 0f, 1f); // Amber
        
        [Tooltip("Color for Resolution Advisory")]
        public Color trafficResolutionColor = new Color(1f, 0f, 0f, 1f); // Red

        [Header("Weather Indicator Colors")]
        [Tooltip("Color for light precipitation")]
        public Color weatherLightColor = new Color(0f, 0.8f, 0f, 1f); // Green
        
        [Tooltip("Color for moderate precipitation")]
        public Color weatherModerateColor = new Color(1f, 1f, 0f, 1f); // Yellow
        
        [Tooltip("Color for heavy precipitation")]
        public Color weatherHeavyColor = new Color(1f, 0f, 0f, 1f); // Red

        [Header("Waypoint Indicator Colors")]
        [Tooltip("Color for waypoint indicators")]
        public Color waypointColor = new Color(1f, 0f, 1f, 1f); // Magenta

        [Header("Type-Specific Settings")]
        [Tooltip("Show traffic indicators")]
        public bool showTrafficIndicators = true;
        
        [Tooltip("Show weather indicators")]
        public bool showWeatherIndicators = true;
        
        [Tooltip("Show waypoint indicators")]
        public bool showWaypointIndicators = true;

        [Header("Transparency Settings")]
        [Tooltip("Global opacity for all indicators (0 = invisible, 1 = fully visible)")]
        [Range(0f, 1f)]
        public float globalOpacity = 1f;
        
        [Tooltip("Opacity for indicators within the nearby distance threshold")]
        [Range(0f, 1f)]
        public float nearbyOpacity = 1f;
        
        [Tooltip("Distance threshold for nearby indicators (in nautical miles)")]
        [Range(0f, 50f)]
        public float nearbyDistanceThresholdNM = 10f;
        
        [Tooltip("Use proximity-based opacity (nearby indicators use nearbyOpacity)")]
        public bool useProximityOpacity = false;

        [Header("Custom Prefabs by Aircraft Type")]
        [Tooltip("Use custom prefabs for different aircraft types")]
        public bool useCustomPrefabs = false;
        
        [Tooltip("Default indicator prefab (used if no type-specific prefab assigned)")]
        public GameObject defaultIndicatorPrefab;
        
        [Tooltip("Commercial aircraft indicator prefab")]
        public GameObject commercialPrefab;
        
        [Tooltip("Military aircraft indicator prefab")]
        public GameObject militaryPrefab;
        
        [Tooltip("General aviation aircraft indicator prefab")]
        public GameObject generalPrefab;
        
        [Tooltip("Helicopter indicator prefab")]
        public GameObject helicopterPrefab;
        
        [Tooltip("Unknown aircraft indicator prefab")]
        public GameObject unknownPrefab;

        /// <summary>
        /// Get the edge configuration struct for calculator use.
        /// </summary>
        public IndicatorEdgeConfig GetEdgeConfig()
        {
            return new IndicatorEdgeConfig
            {
                EdgePadding = edgePadding,
                IndicatorSize = indicatorSize,
                ShowDistanceLabel = showDistanceLabels,
                ShowAltitudeIndicator = showAltitudeIndicators,
                MaxDisplayDistance = maxDisplayDistance
            };
        }

        /// <summary>
        /// Get color for a specific indicator type and threat level (traffic only).
        /// </summary>
        public Color GetColorForTraffic(TrafficRadar.ThreatLevel threatLevel)
        {
            switch (threatLevel)
            {
                case TrafficRadar.ThreatLevel.ResolutionAdvisory:
                    return trafficResolutionColor;
                case TrafficRadar.ThreatLevel.TrafficAdvisory:
                    return trafficAdvisoryColor;
                default:
                    return trafficNormalColor;
            }
        }
        
        /// <summary>
        /// Get the prefab for a specific aircraft type.
        /// Returns null if useCustomPrefabs is false or no prefab is assigned.
        /// </summary>
        public GameObject GetPrefabForAircraftType(TrafficRadar.TrafficRadarDataManager.AircraftType type)
        {
            if (!useCustomPrefabs)
                return null;
            
            switch (type)
            {
                case TrafficRadar.TrafficRadarDataManager.AircraftType.Commercial:
                    return commercialPrefab != null ? commercialPrefab : defaultIndicatorPrefab;
                case TrafficRadar.TrafficRadarDataManager.AircraftType.Military:
                    return militaryPrefab != null ? militaryPrefab : defaultIndicatorPrefab;
                case TrafficRadar.TrafficRadarDataManager.AircraftType.General:
                    return generalPrefab != null ? generalPrefab : defaultIndicatorPrefab;
                case TrafficRadar.TrafficRadarDataManager.AircraftType.Helicopter:
                    return helicopterPrefab != null ? helicopterPrefab : defaultIndicatorPrefab;
                default:
                    return unknownPrefab != null ? unknownPrefab : defaultIndicatorPrefab;
            }
        }

        /// <summary>
        /// Create default settings instance.
        /// </summary>
        public static IndicatorSettings CreateDefault()
        {
            var settings = CreateInstance<IndicatorSettings>();
            settings.name = "IndicatorSettings";
            return settings;
        }
    }
}

