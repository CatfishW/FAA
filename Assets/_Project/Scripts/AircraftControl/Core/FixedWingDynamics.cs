using System;
using System.Collections.Generic;
using UnityEngine;

namespace AircraftControl.Core
{
    /// <summary>
    /// Flight dynamics implementation for fixed-wing aircraft (airplanes).
    /// Implements realistic but simplified fixed-wing aerodynamics including:
    /// - Pitch/roll/yaw control via control surfaces
    /// - Airspeed management with throttle
    /// - Coordinated turns based on bank angle
    /// - Auto-leveling behavior
    /// </summary>
    [Serializable]
    public class FixedWingDynamics : IFlightDynamics
    {
        #region Settings

        [Tooltip("Maximum pitch rate in degrees per second")]
        public float MaxPitchRate = 15f;

        [Tooltip("Maximum roll rate in degrees per second")]
        public float MaxRollRate = 45f;

        [Tooltip("Maximum yaw rate in degrees per second")]
        public float MaxYawRate = 10f;

        [Tooltip("Maximum airspeed in knots")]
        public float MaxAirspeedKnots = 350f;

        [Tooltip("Minimum airspeed in knots (stall speed)")]
        public float MinAirspeedKnots = 60f;

        [Tooltip("Rate of speed change in knots per second")]
        public float SpeedChangeRate = 10f;

        [Tooltip("Climb rate per degree of pitch in fpm")]
        public float ClimbRatePerPitchDegree = 100f;

        [Tooltip("Enable auto-level when no pitch input")]
        public bool AutoLevelPitch = true;

        [Tooltip("Enable auto-level when no roll input")]
        public bool AutoLevelRoll = true;

        [Tooltip("Auto-level rate in degrees per second")]
        public float AutoLevelRate = 10f;

        [Tooltip("Pitch damping factor (0-1, higher = more damping)")]
        [Range(0f, 1f)]
        public float PitchDamping = 0.1f;

        [Tooltip("Roll damping factor (0-1, higher = more damping)")]
        [Range(0f, 1f)]
        public float RollDamping = 0.1f;

        #endregion

        #region IFlightDynamics Implementation

        public AircraftType AircraftType => AircraftType.FixedWing;

        public void Initialize(AircraftState state)
        {
            // Ensure state is valid for fixed-wing
            state.AircraftType = AircraftType.FixedWing;

            // Initialize airspeed if not set
            if (state.IndicatedAirspeedKnots < MinAirspeedKnots)
            {
                state.IndicatedAirspeedKnots = (MinAirspeedKnots + MaxAirspeedKnots) * 0.5f;
                state.GroundSpeedKnots = state.IndicatedAirspeedKnots;
                state.TrueAirspeedKnots = state.IndicatedAirspeedKnots * 1.02f;
            }
        }

        public void UpdatePhysics(AircraftState state, float deltaTime)
        {
            if (deltaTime <= 0f) return;

            // Update attitude based on control inputs
            UpdateAttitude(state, deltaTime);

            // Update airspeed based on throttle
            UpdateAirspeed(state, deltaTime);

            // Update vertical motion based on pitch and airspeed
            UpdateVerticalMotion(state, deltaTime);

            // Update position based on heading and ground speed
            UpdatePosition(state, deltaTime);
        }

        public void Reset(AircraftState state)
        {
            state.Pitch = 0f;
            state.Roll = 0f;
            state.Heading = state.Heading; // Preserve heading
            state.IndicatedAirspeedKnots = (MinAirspeedKnots + MaxAirspeedKnots) * 0.5f;
            state.GroundSpeedKnots = state.IndicatedAirspeedKnots;
            state.TrueAirspeedKnots = state.IndicatedAirspeedKnots * 1.02f;
            state.VerticalSpeedFpm = 0f;
            state.ElevatorInput = 0f;
            state.AileronInput = 0f;
            state.RudderInput = 0f;
            state.ThrottlePercent = 50f;
        }

        public IReadOnlyList<string> GetRequiredInputNames()
        {
            return new[]
            {
                "Elevator",      // Pitch control
                "Aileron",       // Roll control
                "Rudder",        // Yaw control
                "Throttle"       // Speed control
            };
        }

        public bool ValidateState(AircraftState state)
        {
            // Fixed-wing requires non-negative airspeed
            return state.IndicatedAirspeedKnots >= 0f &&
                   state.GroundSpeedKnots >= 0f &&
                   state.TrueAirspeedKnots >= 0f;
        }

        #endregion

        #region Physics Update Methods

        private void UpdateAttitude(AircraftState state, float dt)
        {
            // Update pitch based on elevator input
            float pitchChange = state.ElevatorInput * MaxPitchRate * dt;
            state.Pitch = Mathf.Clamp(state.Pitch + pitchChange, -80f, 80f);

            // Auto-level pitch if no input
            if (AutoLevelPitch && Mathf.Abs(state.ElevatorInput) < 0.01f)
            {
                float levelAmount = AutoLevelRate * dt;
                state.Pitch = Mathf.MoveTowards(state.Pitch, 0f, levelAmount);
            }

            // Apply pitch damping (tendency to return to trimmed state)
            if (Mathf.Abs(state.ElevatorInput) < 0.01f)
            {
                state.Pitch *= (1f - PitchDamping * dt);
            }

            // Update roll based on aileron input
            float rollChange = state.AileronInput * MaxRollRate * dt;
            state.Roll = Mathf.Clamp(state.Roll + rollChange, -89f, 89f);

            // Auto-level roll if no input
            if (AutoLevelRoll && Mathf.Abs(state.AileronInput) < 0.01f)
            {
                float levelAmount = AutoLevelRate * dt;
                state.Roll = Mathf.MoveTowards(state.Roll, 0f, levelAmount);
            }

            // Apply roll damping
            if (Mathf.Abs(state.AileronInput) < 0.01f)
            {
                state.Roll *= (1f - RollDamping * dt);
            }

            // Calculate turn rate based on bank angle (coordinated turn formula)
            // Standard aviation formula: turn rate = (g * tan(bank)) / speed
            // Simplified: turnRate = 1091 * tan(bank) / speed (in knots)
            float bankRadians = state.Roll * Mathf.Deg2Rad;
            float turnRateFromBank = (state.GroundSpeedKnots > 10f)
                ? (Mathf.Tan(bankRadians) * 1091f / state.GroundSpeedKnots)
                : 0f;

            // Add direct yaw input
            float turnRateFromRudder = state.RudderInput * MaxYawRate;

            // Total turn rate (degrees per second)
            float totalTurnRate = turnRateFromBank + turnRateFromRudder;

            // Update heading
            state.Heading = Mathf.Repeat(state.Heading + totalTurnRate * dt, 360f);
        }

        private void UpdateAirspeed(AircraftState state, float dt)
        {
            // Calculate target airspeed based on throttle position
            float targetSpeed = Mathf.Lerp(MinAirspeedKnots, MaxAirspeedKnots,
                state.ThrottlePercent / 100f);

            // Smoothly interpolate towards target speed
            state.IndicatedAirspeedKnots = Mathf.MoveTowards(
                state.IndicatedAirspeedKnots,
                targetSpeed,
                SpeedChangeRate * dt
            );

            // Calculate ground speed and true airspeed
            // Ground speed is affected by pitch (climb/descent)
            float pitchRad = state.Pitch * Mathf.Deg2Rad;
            state.GroundSpeedKnots = state.IndicatedAirspeedKnots * Mathf.Cos(pitchRad);

            // True airspeed is slightly higher than indicated (simplified ISA model)
            // At altitude, TAS is higher than IAS
            float altitudeFactor = 1f + (state.AltitudeMeters / 10000f) * 0.02f;
            state.TrueAirspeedKnots = state.IndicatedAirspeedKnots * altitudeFactor;
        }

        private void UpdateVerticalMotion(AircraftState state, float dt)
        {
            // Calculate vertical speed based on pitch and airspeed
            // VSI = TAS * sin(pitch) * conversion factor
            // Conversion: knots to fpm = knots * 101.269
            float pitchRad = state.Pitch * Mathf.Deg2Rad;
            float targetVsfpm = state.TrueAirspeedKnots * Mathf.Sin(pitchRad) * 101.269f;

            // Alternative calculation using climb rate per degree of pitch
            // This gives more predictable behavior
            float climbRateFromPitch = state.Pitch * ClimbRatePerPitchDegree;

            // Blend between the two methods based on pitch angle
            // Use sine-based at steeper angles, pitch-based at shallow angles
            float blendFactor = Mathf.Abs(state.Pitch) / 30f;
            blendFactor = Mathf.Clamp01(blendFactor);

            state.VerticalSpeedFpm = Mathf.Lerp(climbRateFromPitch, targetVsfpm, blendFactor);

            // Update altitude
            float altitudeChangeMeters = state.VerticalSpeedMps * dt;
            state.AltitudeMeters = Mathf.Max(0f, state.AltitudeMeters + altitudeChangeMeters);
        }

        private void UpdatePosition(AircraftState state, float dt)
        {
            // Speed in meters per second
            float speedMps = state.GroundSpeedMps;

            // Distance traveled in this frame
            float distanceMeters = speedMps * dt;

            if (distanceMeters < 0.001f) return;

            // Earth radius in meters
            const double EarthRadius = 6371000.0;

            // Convert heading to radians
            float headingRad = state.Heading * Mathf.Deg2Rad;

            // Current latitude in radians
            double latRad = state.Latitude * Mathf.Deg2Rad;

            // Calculate position changes
            double dLat = (distanceMeters * Mathf.Cos(headingRad)) / EarthRadius;
            double dLon = (distanceMeters * Mathf.Sin(headingRad)) / (EarthRadius * Math.Cos(latRad));

            // Update coordinates
            state.Latitude += dLat * Mathf.Rad2Deg;
            state.Longitude += dLon * Mathf.Rad2Deg;

            // Clamp latitude
            state.Latitude = Math.Max(-90.0, Math.Min(90.0, state.Latitude));

            // Wrap longitude
            if (state.Longitude > 180.0) state.Longitude -= 360.0;
            if (state.Longitude < -180.0) state.Longitude += 360.0;
        }

        #endregion

        #region Configuration Methods

        /// <summary>
        /// Configure the dynamics model from an existing aircraft controller's settings
        /// </summary>
        public void ConfigureFromController(
            float maxPitchRate,
            float maxRollRate,
            float maxYawRate,
            float maxAirspeed,
            float minAirspeed,
            float speedChangeRate,
            float climbRatePerPitch,
            bool autoLevelPitch,
            bool autoLevelRoll,
            float autoLevelRate)
        {
            MaxPitchRate = maxPitchRate;
            MaxRollRate = maxRollRate;
            MaxYawRate = maxYawRate;
            MaxAirspeedKnots = maxAirspeed;
            MinAirspeedKnots = minAirspeed;
            SpeedChangeRate = speedChangeRate;
            ClimbRatePerPitchDegree = climbRatePerPitch;
            AutoLevelPitch = autoLevelPitch;
            AutoLevelRoll = autoLevelRoll;
            AutoLevelRate = autoLevelRate;
        }

        #endregion
    }
}
