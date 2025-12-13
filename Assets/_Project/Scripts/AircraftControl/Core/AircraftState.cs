using System;
using UnityEngine;

namespace AircraftControl.Core
{
    /// <summary>
    /// Centralized aircraft state data structure.
    /// Contains all flight parameters needed for HUD, radar, and control systems.
    /// </summary>
    [Serializable]
    public class AircraftState
    {
        #region Position Data
        
        [Header("Geographic Position")]
        [Tooltip("Latitude in decimal degrees")]
        public double Latitude;
        
        [Tooltip("Longitude in decimal degrees")]
        public double Longitude;
        
        [Tooltip("Altitude in meters MSL")]
        public float AltitudeMeters;
        
        /// <summary>
        /// Altitude in feet MSL (computed)
        /// </summary>
        public float AltitudeFeet => AltitudeMeters * 3.28084f;
        
        #endregion
        
        #region Attitude Data
        
        [Header("Aircraft Attitude")]
        [Tooltip("Pitch angle in degrees (-90 to 90, positive = nose up)")]
        [Range(-90f, 90f)]
        public float Pitch;
        
        [Tooltip("Roll/Bank angle in degrees (-180 to 180, positive = right wing down)")]
        [Range(-180f, 180f)]
        public float Roll;
        
        [Tooltip("Heading/Yaw in degrees (0-360, magnetic)")]
        [Range(0f, 360f)]
        public float Heading;
        
        #endregion
        
        #region Velocity Data
        
        [Header("Velocity")]
        [Tooltip("Indicated airspeed in knots")]
        public float IndicatedAirspeedKnots;
        
        [Tooltip("Ground speed in knots")]
        public float GroundSpeedKnots;
        
        [Tooltip("True airspeed in knots")]
        public float TrueAirspeedKnots;
        
        [Tooltip("Vertical speed in feet per minute")]
        public float VerticalSpeedFpm;
        
        /// <summary>
        /// Ground speed in meters per second (computed)
        /// </summary>
        public float GroundSpeedMps => GroundSpeedKnots * 0.514444f;
        
        /// <summary>
        /// Vertical speed in meters per second (computed)
        /// </summary>
        public float VerticalSpeedMps => VerticalSpeedFpm * 0.00508f;
        
        #endregion
        
        #region Control Inputs
        
        [Header("Control Inputs")]
        [Tooltip("Throttle position (0-100%)")]
        [Range(0f, 100f)]
        public float ThrottlePercent;
        
        [Tooltip("Elevator deflection (-1 to 1, positive = pitch up)")]
        [Range(-1f, 1f)]
        public float ElevatorInput;
        
        [Tooltip("Aileron deflection (-1 to 1, positive = roll right)")]
        [Range(-1f, 1f)]
        public float AileronInput;
        
        [Tooltip("Rudder deflection (-1 to 1, positive = yaw right)")]
        [Range(-1f, 1f)]
        public float RudderInput;
        
        #endregion
        
        #region Status Flags
        
        [Header("Status")]
        public bool IsOnGround;
        public bool GearDown = true;
        public bool AutopilotEngaged;
        
        #endregion
        
        #region Methods
        
        /// <summary>
        /// Create a deep copy of the aircraft state
        /// </summary>
        public AircraftState Clone()
        {
            return (AircraftState)MemberwiseClone();
        }
        
        /// <summary>
        /// Interpolate between two aircraft states
        /// </summary>
        public static AircraftState Lerp(AircraftState a, AircraftState b, float t)
        {
            return new AircraftState
            {
                Latitude = a.Latitude + (b.Latitude - a.Latitude) * t,
                Longitude = a.Longitude + (b.Longitude - a.Longitude) * t,
                AltitudeMeters = Mathf.Lerp(a.AltitudeMeters, b.AltitudeMeters, t),
                Pitch = Mathf.Lerp(a.Pitch, b.Pitch, t),
                Roll = Mathf.Lerp(a.Roll, b.Roll, t),
                Heading = Mathf.LerpAngle(a.Heading, b.Heading, t),
                IndicatedAirspeedKnots = Mathf.Lerp(a.IndicatedAirspeedKnots, b.IndicatedAirspeedKnots, t),
                GroundSpeedKnots = Mathf.Lerp(a.GroundSpeedKnots, b.GroundSpeedKnots, t),
                TrueAirspeedKnots = Mathf.Lerp(a.TrueAirspeedKnots, b.TrueAirspeedKnots, t),
                VerticalSpeedFpm = Mathf.Lerp(a.VerticalSpeedFpm, b.VerticalSpeedFpm, t),
                ThrottlePercent = Mathf.Lerp(a.ThrottlePercent, b.ThrottlePercent, t),
                ElevatorInput = Mathf.Lerp(a.ElevatorInput, b.ElevatorInput, t),
                AileronInput = Mathf.Lerp(a.AileronInput, b.AileronInput, t),
                RudderInput = Mathf.Lerp(a.RudderInput, b.RudderInput, t),
                IsOnGround = t < 0.5f ? a.IsOnGround : b.IsOnGround,
                GearDown = t < 0.5f ? a.GearDown : b.GearDown,
                AutopilotEngaged = t < 0.5f ? a.AutopilotEngaged : b.AutopilotEngaged
            };
        }
        
        /// <summary>
        /// Creates a default aircraft state
        /// </summary>
        public static AircraftState CreateDefault(double latitude = 33.6407, double longitude = -84.4277)
        {
            return new AircraftState
            {
                Latitude = latitude,
                Longitude = longitude,
                AltitudeMeters = 3048f, // 10,000 ft
                Pitch = 0f,
                Roll = 0f,
                Heading = 0f,
                IndicatedAirspeedKnots = 250f,
                GroundSpeedKnots = 250f,
                TrueAirspeedKnots = 260f,
                VerticalSpeedFpm = 0f,
                ThrottlePercent = 50f,
                IsOnGround = false,
                GearDown = false
            };
        }
        
        #endregion
    }
}
