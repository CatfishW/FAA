using System;
using UnityEngine;

namespace TrafficRadar.Core
{
    /// <summary>
    /// Interface for own-ship position providers.
    /// Decouples the radar system from specific position sources.
    /// </summary>
    public interface IOwnShipPositionProvider
    {
        /// <summary>
        /// Event fired when position changes significantly
        /// </summary>
        event Action<OwnShipPosition> OnPositionChanged;
        
        /// <summary>
        /// Get the current position
        /// </summary>
        OwnShipPosition CurrentPosition { get; }
        
        /// <summary>
        /// Whether position data is valid/available
        /// </summary>
        bool IsValid { get; }
    }
    
    /// <summary>
    /// Own-ship position data structure
    /// </summary>
    [System.Serializable]
    public struct OwnShipPosition
    {
        public double Latitude;
        public double Longitude;
        public float AltitudeMeters;
        public float HeadingDegrees;
        public float GroundSpeedMps;
        
        // Computed properties
        public float AltitudeFeet => AltitudeMeters * 3.28084f;
        public float GroundSpeedKnots => GroundSpeedMps * 1.94384f;
        
        public static OwnShipPosition Default => new OwnShipPosition
        {
            Latitude = 33.6407,  // KATL
            Longitude = -84.4277,
            AltitudeMeters = 313,
            HeadingDegrees = 0,
            GroundSpeedMps = 0
        };
    }
}
