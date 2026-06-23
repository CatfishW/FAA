using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrafficRadar.Core
{
    /// <summary>
    /// Interface for radar display renderers.
    /// Allows different display implementations (texture-based, UI-based, 3D, etc.)
    /// </summary>
    public interface IRadarDisplayRenderer
    {
        /// <summary>
        /// Update the display with new radar targets
        /// </summary>
        void UpdateTargets(IReadOnlyList<RadarTarget> targets);
        
        /// <summary>
        /// Set the radar range in nautical miles
        /// </summary>
        float RangeNM { get; set; }
        
        /// <summary>
        /// Set own-ship heading for heading-up mode
        /// </summary>
        float OwnHeadingDegrees { get; set; }
        
        /// <summary>
        /// Clear all targets from display
        /// </summary>
        void ClearDisplay();
    }
    
    /// <summary>
    /// Processed radar target ready for display.
    /// Contains all calculated values needed for rendering.
    /// </summary>
    [System.Serializable]
    public struct RadarTarget
    {
        // Identity
        public string Icao24;
        public string Callsign;
        
        // Raw position
        public double Latitude;
        public double Longitude;
        public float AltitudeFeet;
        public float Heading;
        public float GroundSpeedKnots;
        public float VerticalRateFpm;
        
        // Calculated relative values
        public float DistanceNM;
        public float BearingDegrees;
        public float RelativeAltitudeFeet;
        
        // Radar display position (normalized -1 to 1)
        public Vector2 RadarPosition;
        
        // Threat classification
        public ThreatLevel ThreatLevel;
        
        // Aircraft type for prefab selection
        public TrafficRadarDataManager.AircraftType AircraftType;
        
        // Tracking
        public bool IsTracked;
        public float TimeSinceUpdate;
    }
}
