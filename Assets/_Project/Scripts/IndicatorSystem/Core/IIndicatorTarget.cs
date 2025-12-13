using UnityEngine;
using TrafficRadar;

namespace IndicatorSystem.Core
{
    /// <summary>
    /// Interface defining what any target must provide for indicator display.
    /// Enables unified handling of traffic, weather, and waypoint targets.
    /// </summary>
    public interface IIndicatorTarget
    {
        /// <summary>Unique identifier for the target</summary>
        string Id { get; }
        
        /// <summary>World-space position of the target</summary>
        Vector3 WorldPosition { get; }
        
        /// <summary>Color to display for this indicator</summary>
        Color DisplayColor { get; }
        
        /// <summary>Priority level (higher = more important, shown on top)</summary>
        int Priority { get; }
        
        /// <summary>Type of indicator to show</summary>
        IndicatorType Type { get; }
        
        /// <summary>Optional label text (e.g., callsign, distance)</summary>
        string Label { get; }
        
        /// <summary>Distance from own position in nautical miles</summary>
        float DistanceNM { get; }
        
        /// <summary>Relative altitude difference in feet (positive = above)</summary>
        float RelativeAltitudeFeet { get; }
        
        /// <summary>Aircraft type for traffic indicators (used for prefab selection)</summary>
        TrafficRadarDataManager.AircraftType AircraftType { get; }
        
        /// <summary>Aircraft heading in degrees (0-360, for navigation light display)</summary>
        float Heading { get; }
    }
}
