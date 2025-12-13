using UnityEngine;

namespace AviationUI
{
    /// <summary>
    /// ScriptableObject configuration for the Aviation UI system.
    /// Holds all customizable settings for colors, sizing, and visibility.
    /// </summary>
    [CreateAssetMenu(fileName = "AviationUIConfig", menuName = "Aviation UI/Configuration", order = 1)]
    public class AviationUIConfig : ScriptableObject
    {
        [Header("Primary Colors")]
        [Tooltip("Main HUD color for normal conditions")]
        public Color primaryColor = new Color(0.2f, 1f, 0.4f, 1f); // Green
        
        [Tooltip("Warning color for caution conditions")]
        public Color warningColor = new Color(1f, 0.92f, 0.016f, 1f); // Yellow
        
        [Tooltip("Danger color for critical conditions")]
        public Color dangerColor = new Color(1f, 0.2f, 0.2f, 1f); // Red
        
        [Tooltip("Background color for boxes and panels")]
        public Color backgroundColor = new Color(0f, 0f, 0f, 0.5f);
        
        [Tooltip("Text color")]
        public Color textColor = Color.white;

        [Header("UI Sizing")]
        [Tooltip("Overall scale multiplier for the entire UI")]
        [Range(0.5f, 2f)]
        public float uiScale = 1f;
        
        [Tooltip("Font size for primary displays")]
        public int primaryFontSize = 72;
        
        [Tooltip("Font size for secondary displays")]
        public int secondaryFontSize = 48;
        
        [Tooltip("Font size for labels")]
        public int labelFontSize = 32;

        [Header("Panel Visibility")]
        public bool showHeadingTape = true;
        public bool showAttitudeIndicator = true;
        public bool showAirspeedTape = true;
        public bool showAltitudeTape = true;
        public bool showVerticalSpeed = true;
        public bool showEngineGauges = true;
        public bool showCompassRose = true;
        public bool showRadar = true;
        public bool showModeButtons = true;

        [Header("Thresholds")]
        [Tooltip("Airspeed at which warning color begins")]
        public float airspeedWarningThreshold = 140f;
        
        [Tooltip("Airspeed at which danger color begins")]
        public float airspeedDangerThreshold = 160f;
        
        [Tooltip("Altitude at which warning color begins (MSL)")]
        public float altitudeWarningThreshold = 10000f;
        
        [Tooltip("Altitude at which danger color begins (MSL)")]
        public float altitudeDangerThreshold = 19000f;
        
        [Tooltip("Vertical speed at which warning begins (ft/min)")]
        public float verticalSpeedWarningThreshold = 1000f;
        
        [Tooltip("Vertical speed at which danger begins (ft/min)")]
        public float verticalSpeedDangerThreshold = 2500f;
        
        [Tooltip("Engine torque percentage at which warning begins")]
        public float torqueWarningThreshold = 100f;
        
        [Tooltip("Engine torque percentage at which danger begins")]
        public float torqueDangerThreshold = 110f;

        [Header("Animation")]
        [Tooltip("Smoothing factor for value transitions")]
        [Range(0.01f, 1f)]
        public float smoothingFactor = 0.1f;
        
        [Tooltip("Enable animations")]
        public bool enableAnimations = true;

        [Header("Layout Preset")]
        public LayoutPreset layoutPreset = LayoutPreset.Helicopter;
        
        /// <summary>
        /// Gets a color based on value and thresholds (green -> yellow -> red)
        /// </summary>
        public Color GetThresholdColor(float value, float warningThreshold, float dangerThreshold)
        {
            if (value >= dangerThreshold)
            {
                return dangerColor;
            }
            else if (value >= warningThreshold)
            {
                float t = (value - warningThreshold) / (dangerThreshold - warningThreshold);
                return Color.Lerp(warningColor, dangerColor, t);
            }
            else if (value >= warningThreshold * 0.5f)
            {
                float t = (value - warningThreshold * 0.5f) / (warningThreshold * 0.5f);
                return Color.Lerp(primaryColor, warningColor, t);
            }
            return primaryColor;
        }

        /// <summary>
        /// Creates a default configuration
        /// </summary>
        public static AviationUIConfig CreateDefault()
        {
            var config = CreateInstance<AviationUIConfig>();
            // Default values are set in field declarations
            return config;
        }
    }

    /// <summary>
    /// Layout presets for different aircraft types
    /// </summary>
    public enum LayoutPreset
    {
        Helicopter,
        FixedWing,
        VTOL,
        Custom
    }
}
