using UnityEngine;
using TrafficRadar;

namespace IndicatorSystem.Core
{
    /// <summary>
    /// Type of indicator to display.
    /// </summary>
    public enum IndicatorType
    {
        Traffic,
        Weather,
        Waypoint,
        Custom
    }

    /// <summary>
    /// Screen visibility state for an indicator.
    /// </summary>
    public enum IndicatorVisibility
    {
        /// <summary>Target is visible within screen bounds</summary>
        OnScreen,
        /// <summary>Target is outside screen bounds</summary>
        OffScreen,
        /// <summary>Target is behind the camera</summary>
        Behind,
        /// <summary>Target is too far to display</summary>
        OutOfRange
    }

    /// <summary>
    /// Processed indicator data ready for display.
    /// Contains all calculated screen-space information.
    /// </summary>
    [System.Serializable]
    public struct IndicatorData
    {
        /// <summary>Unique identifier matching the source target</summary>
        public string Id;
        
        /// <summary>Screen-space position (in pixels or normalized)</summary>
        public Vector2 ScreenPosition;
        
        /// <summary>Rotation angle for off-screen arrow (degrees)</summary>
        public float ArrowRotation;
        
        /// <summary>Current visibility state</summary>
        public IndicatorVisibility Visibility;
        
        /// <summary>Type of indicator</summary>
        public IndicatorType Type;
        
        /// <summary>Display color</summary>
        public Color Color;
        
        /// <summary>Priority for layering</summary>
        public int Priority;
        
        /// <summary>Label text to display</summary>
        public string Label;
        
        /// <summary>Distance in nautical miles</summary>
        public float DistanceNM;
        
        /// <summary>Relative altitude in feet</summary>
        public float RelativeAltitudeFeet;
        
        /// <summary>Whether the indicator is currently active</summary>
        public bool IsActive;
        
        /// <summary>World position of the target</summary>
        public Vector3 WorldPosition;
        
        /// <summary>Aircraft type for traffic indicators (for prefab selection)</summary>
        public TrafficRadarDataManager.AircraftType AircraftType;
        
        /// <summary>Aircraft heading in degrees (0-360)</summary>
        public float Heading;
        
        /// <summary>Bearing from own aircraft to target in degrees</summary>
        public float BearingFromOwn;
    }

    /// <summary>
    /// Configuration for indicator edge clamping and display.
    /// </summary>
    [System.Serializable]
    public struct IndicatorEdgeConfig
    {
        [Tooltip("Padding from screen edges in pixels")]
        public float EdgePadding;
        
        [Tooltip("Size of indicator in pixels")]
        public float IndicatorSize;
        
        [Tooltip("Whether to show distance label")]
        public bool ShowDistanceLabel;
        
        [Tooltip("Whether to show altitude indicator (above/below arrows)")]
        public bool ShowAltitudeIndicator;
        
        [Tooltip("Maximum distance to show indicators (NM)")]
        public float MaxDisplayDistance;

        public static IndicatorEdgeConfig Default => new IndicatorEdgeConfig
        {
            EdgePadding = 50f,
            IndicatorSize = 40f,
            ShowDistanceLabel = true,
            ShowAltitudeIndicator = true,
            MaxDisplayDistance = 100f
        };
    }
}
