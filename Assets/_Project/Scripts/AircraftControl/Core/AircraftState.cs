using System;
using UnityEngine;

namespace AircraftControl.Core
{
    /// <summary>
    /// Centralized aircraft state data structure.
    /// Contains all flight parameters needed for HUD, radar, and control systems.
    /// Supports both fixed-wing and rotary-wing (helicopter) aircraft.
    /// </summary>
    [Serializable]
    public class AircraftState
    {
        #region Aircraft Type

        [Header("Aircraft Type")]
        [Tooltip("Type of aircraft - affects control scheme and physics")]
        public AircraftType AircraftType = AircraftType.FixedWing;

        #endregion

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

        #region Helicopter-Specific State

        [Header("Helicopter Systems")]
        [Tooltip("Main rotor RPM (0-100% of max)")]
        [Range(0f, 100f)]
        public float MainRotorRpm;

        [Tooltip("Tail rotor RPM (0-100% of max)")]
        [Range(0f, 100f)]
        public float TailRotorRpm;

        [Tooltip("Collective pitch input (-1 to 1, positive = more lift)")]
        [Range(-1f, 1f)]
        public float CollectiveInput;

        [Tooltip("Cyclic longitudinal input (-1 to 1, positive = pitch nose down/forward)")]
        [Range(-1f, 1f)]
        public float CyclicLongitudinalInput;

        [Tooltip("Cyclic lateral input (-1 to 1, positive = roll right)")]
        [Range(-1f, 1f)]
        public float CyclicLateralInput;

        [Tooltip("Tail rotor collective/pitch input (-1 to 1, positive = yaw right)")]
        [Range(-1f, 1f)]
        public float TailRotorInput;

        [Tooltip("Current ground effect factor (0-1, 1 = maximum ground effect)")]
        [Range(0f, 1f)]
        public float GroundEffectFactor;

        [Tooltip("Current rotor disc tilt angle in degrees (combined cyclic result)")]
        public float RotorDiscTiltAngle;

        [Tooltip("Direction of rotor disc tilt in degrees (0 = forward, 90 = right)")]
        public float RotorDiscTiltDirection;

        [Tooltip("Engine/rotor spooled up and ready (above 80% RPM)")]
        public bool IsRotorSpooledUp;

        [Tooltip("Helicopter is in hover mode (ground speed < 5 knots)")]
        public bool IsInHover;

        #endregion

        #region Status Flags
        
        [Header("Status")]
        public bool IsOnGround;
        public bool GearDown = true;
        public bool AutopilotEngaged;

        [Header("X-Plane Control Systems")]
        [Range(0f, 1f)]
        public float FlapsRatio;

        [Range(0f, 1f)]
        public float SpeedbrakeRatio;

        [Range(0f, 1f)]
        public float ParkingBrakeRatio;

        [Range(0f, 1f)]
        public float LeftBrakeRatio;

        [Range(0f, 1f)]
        public float RightBrakeRatio;

        [Range(-1f, 1f)]
        public float ElevatorTrim;

        [Range(-1f, 1f)]
        public float AileronTrim;

        [Range(-1f, 1f)]
        public float RudderTrim;

        public int AutopilotMode;
        
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
        /// Create a deep copy suitable for a specific aircraft type
        /// </summary>
        public AircraftState Clone(AircraftType targetType)
        {
            var cloned = Clone();
            cloned.AircraftType = targetType;
            return cloned;
        }
        
        /// <summary>
        /// Interpolate between two aircraft states
        /// </summary>
        public static AircraftState Lerp(AircraftState a, AircraftState b, float t)
        {
            return new AircraftState
            {
                // Aircraft type
                AircraftType = t < 0.5f ? a.AircraftType : b.AircraftType,

                // Position
                Latitude = a.Latitude + (b.Latitude - a.Latitude) * t,
                Longitude = a.Longitude + (b.Longitude - a.Longitude) * t,
                AltitudeMeters = Mathf.Lerp(a.AltitudeMeters, b.AltitudeMeters, t),

                // Attitude
                Pitch = Mathf.Lerp(a.Pitch, b.Pitch, t),
                Roll = Mathf.Lerp(a.Roll, b.Roll, t),
                Heading = Mathf.Repeat(Mathf.LerpAngle(a.Heading, b.Heading, t), 360f),

                // Velocity
                IndicatedAirspeedKnots = Mathf.Lerp(a.IndicatedAirspeedKnots, b.IndicatedAirspeedKnots, t),
                GroundSpeedKnots = Mathf.Lerp(a.GroundSpeedKnots, b.GroundSpeedKnots, t),
                TrueAirspeedKnots = Mathf.Lerp(a.TrueAirspeedKnots, b.TrueAirspeedKnots, t),
                VerticalSpeedFpm = Mathf.Lerp(a.VerticalSpeedFpm, b.VerticalSpeedFpm, t),

                // Control inputs (fixed-wing)
                ThrottlePercent = Mathf.Lerp(a.ThrottlePercent, b.ThrottlePercent, t),
                ElevatorInput = Mathf.Lerp(a.ElevatorInput, b.ElevatorInput, t),
                AileronInput = Mathf.Lerp(a.AileronInput, b.AileronInput, t),
                RudderInput = Mathf.Lerp(a.RudderInput, b.RudderInput, t),

                // Helicopter systems
                MainRotorRpm = Mathf.Lerp(a.MainRotorRpm, b.MainRotorRpm, t),
                TailRotorRpm = Mathf.Lerp(a.TailRotorRpm, b.TailRotorRpm, t),
                CollectiveInput = Mathf.Lerp(a.CollectiveInput, b.CollectiveInput, t),
                CyclicLongitudinalInput = Mathf.Lerp(a.CyclicLongitudinalInput, b.CyclicLongitudinalInput, t),
                CyclicLateralInput = Mathf.Lerp(a.CyclicLateralInput, b.CyclicLateralInput, t),
                TailRotorInput = Mathf.Lerp(a.TailRotorInput, b.TailRotorInput, t),
                GroundEffectFactor = Mathf.Lerp(a.GroundEffectFactor, b.GroundEffectFactor, t),
                RotorDiscTiltAngle = Mathf.Lerp(a.RotorDiscTiltAngle, b.RotorDiscTiltAngle, t),
                RotorDiscTiltDirection = Mathf.LerpAngle(a.RotorDiscTiltDirection, b.RotorDiscTiltDirection, t),
                IsRotorSpooledUp = t < 0.5f ? a.IsRotorSpooledUp : b.IsRotorSpooledUp,
                IsInHover = t < 0.5f ? a.IsInHover : b.IsInHover,

                // Status
                IsOnGround = t < 0.5f ? a.IsOnGround : b.IsOnGround,
                GearDown = t < 0.5f ? a.GearDown : b.GearDown,
                AutopilotEngaged = t < 0.5f ? a.AutopilotEngaged : b.AutopilotEngaged,

                FlapsRatio = Mathf.Lerp(a.FlapsRatio, b.FlapsRatio, t),
                SpeedbrakeRatio = Mathf.Lerp(a.SpeedbrakeRatio, b.SpeedbrakeRatio, t),
                ParkingBrakeRatio = Mathf.Lerp(a.ParkingBrakeRatio, b.ParkingBrakeRatio, t),
                LeftBrakeRatio = Mathf.Lerp(a.LeftBrakeRatio, b.LeftBrakeRatio, t),
                RightBrakeRatio = Mathf.Lerp(a.RightBrakeRatio, b.RightBrakeRatio, t),
                ElevatorTrim = Mathf.Lerp(a.ElevatorTrim, b.ElevatorTrim, t),
                AileronTrim = Mathf.Lerp(a.AileronTrim, b.AileronTrim, t),
                RudderTrim = Mathf.Lerp(a.RudderTrim, b.RudderTrim, t),
                AutopilotMode = t < 0.5f ? a.AutopilotMode : b.AutopilotMode
            };
        }
        
        /// <summary>
        /// Creates a default aircraft state for fixed-wing aircraft
        /// </summary>
        public static AircraftState CreateDefault(double latitude = 33.6407, double longitude = -84.4277)
        {
            return CreateDefault(AircraftType.FixedWing, latitude, longitude);
        }

        /// <summary>
        /// Creates a default aircraft state for the specified aircraft type
        /// </summary>
        public static AircraftState CreateDefault(AircraftType type, double latitude = 33.6407, double longitude = -84.4277)
        {
            var state = new AircraftState
            {
                AircraftType = type,
                Latitude = latitude,
                Longitude = longitude,
                Pitch = 0f,
                Roll = 0f,
                Heading = 0f,
                VerticalSpeedFpm = 0f,
                IsOnGround = false,
                GearDown = false
            };

            if (type == AircraftType.Helicopter)
            {
                // Helicopter defaults - typically start on ground with rotors stopped
                state.AltitudeMeters = 304.8f; // 1,000 ft (or 0 for ground start)
                state.IndicatedAirspeedKnots = 0f;
                state.GroundSpeedKnots = 0f;
                state.TrueAirspeedKnots = 0f;
                state.ThrottlePercent = 0f;
                state.MainRotorRpm = 0f;
                state.TailRotorRpm = 0f;
                state.CollectiveInput = 0f;
                state.CyclicLongitudinalInput = 0f;
                state.CyclicLateralInput = 0f;
                state.TailRotorInput = 0f;
                state.GroundEffectFactor = 0f;
                state.RotorDiscTiltAngle = 0f;
                state.RotorDiscTiltDirection = 0f;
                state.IsRotorSpooledUp = false;
                state.IsInHover = true;
                state.FlapsRatio = 0f;
                state.SpeedbrakeRatio = 0f;
                state.ParkingBrakeRatio = 1f;
                state.LeftBrakeRatio = 0f;
                state.RightBrakeRatio = 0f;
                state.ElevatorTrim = 0f;
                state.AileronTrim = 0f;
                state.RudderTrim = 0f;
                state.AutopilotMode = 0;
            }
            else
            {
                // Fixed-wing defaults
                state.AltitudeMeters = 3048f; // 10,000 ft
                state.IndicatedAirspeedKnots = 250f;
                state.GroundSpeedKnots = 250f;
                state.TrueAirspeedKnots = 260f;
                state.ThrottlePercent = 50f;
                state.GearDown = false;
                state.FlapsRatio = 0f;
                state.SpeedbrakeRatio = 0f;
                state.ParkingBrakeRatio = 0f;
                state.LeftBrakeRatio = 0f;
                state.RightBrakeRatio = 0f;
                state.ElevatorTrim = 0f;
                state.AileronTrim = 0f;
                state.RudderTrim = 0f;
                state.AutopilotMode = 0;
            }

            return state;
        }
        
        #endregion
    }
}
