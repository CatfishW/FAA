using System;
using System.Collections.Generic;
using UnityEngine;

namespace AircraftControl.Core
{
    /// <summary>
    /// Flight dynamics implementation for rotary-wing aircraft (helicopters).
    /// Implements realistic helicopter physics including:
    /// - Collective pitch control for vertical thrust
    /// - Cyclic pitch control for directional movement
    /// - Main rotor RPM management and spool up/down
    /// - Tail rotor anti-torque for yaw control
    /// - Ground effect
    /// - Translating tendency (drift)
    /// - Torque effects
    /// </summary>
    [Serializable]
    public class HelicopterDynamics : IFlightDynamics
    {
        #region Physical Constants

        // Earth's radius in meters (for position calculations)
        private const double EarthRadius = 6371000.0;

        // Conversion factors
        private const float KnotsToMps = 0.514444f;
        private const float MpsToKnots = 1.94384f;
        private const float FpmToMps = 0.00508f;

        #endregion

        #region Rotor Settings

        [Header("Rotor Configuration")]
        [Tooltip("Maximum main rotor RPM (100%)")]
        public float MaxMainRotorRpm = 400f;

        [Tooltip("Minimum main rotor RPM for flight (below this, no lift)")]
        public float MinFlightRotorRpm = 320f; // 80% of max

        [Tooltip("Time to spool up rotors from 0% to 100% (seconds)")]
        public float RotorSpoolUpTime = 8f;

        [Tooltip("Time to spool down rotors from 100% to 0% (seconds)")]
        public float RotorSpoolDownTime = 12f;

        [Tooltip("Rotor inertia (affects RPM stability)")]
        [Range(0.1f, 2f)]
        public float RotorInertia = 1f;

        #endregion

        #region Lift Settings

        [Header("Lift Characteristics")]
        [Tooltip("Maximum vertical climb rate in fpm at max collective")]
        public float MaxClimbRateFpm = 2000f;

        [Tooltip("Maximum descent rate in fpm at min collective")]
        public float MaxDescentRateFpm = -1500f;

        [Tooltip("Maximum forward speed in knots")]
        public float MaxForwardSpeedKnots = 150f;

        [Tooltip("Maximum sideways speed in knots")]
        public float MaxSidewaysSpeedKnots = 40f;

        [Tooltip("Maximum rearward speed in knots")]
        public float MaxRearwardSpeedKnots = 30f;

        [Tooltip("Maximum vertical speed in knots (pure climb/descent)")]
        public float MaxVerticalSpeedKnots = 30f;

        [Tooltip("Hover power required (% of max)")]
        [Range(0.3f, 0.9f)]
        public float HoverPowerRequired = 0.65f;

        #endregion

        #region Control Settings

        [Header("Control Response")]
        [Tooltip("Maximum pitch rate in degrees per second")]
        public float MaxPitchRate = 25f;

        [Tooltip("Maximum roll rate in degrees per second")]
        public float MaxRollRate = 35f;

        [Tooltip("Maximum yaw rate in degrees per second")]
        public float MaxYawRate = 45f;

        [Tooltip("Cyclic input to rotor disc tilt conversion factor")]
        public float CyclicToTiltFactor = 12f; // degrees of tilt per full cyclic input

        [Tooltip("Rate at which rotor disc tilts (degrees per second)")]
        public float RotorTiltRate = 30f;

        [Tooltip("Rate at which rotor disc returns to level (degrees per second)")]
        public float RotorLevelRate = 15f;

        [Tooltip("Pitch damping factor")]
        [Range(0f, 1f)]
        public float PitchDamping = 0.15f;

        [Tooltip("Roll damping factor")]
        [Range(0f, 1f)]
        public float RollDamping = 0.15f;

        [Tooltip("Yaw damping factor")]
        [Range(0f, 1f)]
        public float YawDamping = 0.2f;

        #endregion

        #region Ground Effect Settings

        [Header("Ground Effect")]
        [Tooltip("Height in meters where ground effect starts")]
        public float GroundEffectHeight = 15f; // ~50 feet

        [Tooltip("Height in meters where ground effect ends")]
        public float GroundEffectZeroHeight = 50f; // ~165 feet

        [Tooltip("Maximum power reduction from ground effect (0-1)")]
        [Range(0f, 0.5f)]
        public float MaxGroundEffectBenefit = 0.25f;

        #endregion

        #region Translational Response

        [Header("Translational Response")]
        [Tooltip("Horizontal acceleration toward target speeds (m/s^2)")]
        public float HorizontalAcceleration = 10f;

        [Tooltip("Horizontal drag applied each second (0 = no drag)")]
        [Range(0f, 2f)]
        public float HorizontalDrag = 0.5f;

        [Tooltip("Extra drag applied when hovering with minimal cyclic input")]
        [Range(0f, 2f)]
        public float HoverStabilizationDrag = 0.8f;

        [Tooltip("Cyclic input magnitude below which hover stabilization applies")]
        [Range(0f, 0.3f)]
        public float HoverStabilizationInputThreshold = 0.12f;

        [Tooltip("Maximum ground speed (knots) for hover stabilization to apply")]
        public float HoverStabilizationMaxSpeedKnots = 15f;

        #endregion

        #region Translating Tendency

        [Header("Translating Tendency")]
        [Tooltip("Lateral drift tendency when hovering (knots, typically right drift in US helicopters)")]
        public float TranslatingTendencyKnots = 5f;

        [Tooltip("Direction of translating tendency (0 = right, 90 = backward, etc.)")]
        public float TranslatingTendencyDirection = 90f; // Right drift

        #endregion

        #region Torque Settings

        [Header("Torque Effects")]
        [Tooltip("Torque yaw effect at max collective")]
        public float MaxTorqueYawRate = 20f;

        [Tooltip("Direction of torque effect (1 = nose right, -1 = nose left)")]
        public float TorqueDirection = 1f; // Most helicopters yaw right with main rotor

        #endregion

        #region Autorotation Settings

        [Header("Autorotation")]
        [Tooltip("Minimum collective for autorotation (prevents rotor stall)")]
        [Range(-0.3f, 0f)]
        public float AutorotationMinCollective = -0.2f;

        [Tooltip("Descent rate during autorotation in fpm")]
        public float AutorotationDescentRateFpm = -1200f;

        #endregion

        #region IFlightDynamics Implementation

        public AircraftType AircraftType => AircraftType.Helicopter;

        // Current internal state
        private float _currentRotorRpm;
        private float _currentRotorDiscTilt;
        private float _currentRotorDiscTiltDirection;
        private float _smoothedCollective;
        private float _currentTorque;
        private bool _isEngineRunning;
        private float _forwardSpeedKnots;
        private float _lateralSpeedKnots;
        private float _groundTrackHeading;

        public void Initialize(AircraftState state)
        {
            state.AircraftType = AircraftType.Helicopter;

            // Initialize rotor state
            _currentRotorRpm = state.MainRotorRpm;
            _smoothedCollective = state.CollectiveInput;
            _currentRotorDiscTilt = state.RotorDiscTiltAngle;
            _currentRotorDiscTiltDirection = state.RotorDiscTiltDirection;

            // Determine if engine should be running
            _isEngineRunning = state.ThrottlePercent > 0.1f;

            // Validate initial state
            state.IsRotorSpooledUp = _currentRotorRpm >= MinFlightRotorRpm;
            state.IsInHover = state.GroundSpeedKnots < 5f;

            _forwardSpeedKnots = state.GroundSpeedKnots;
            _lateralSpeedKnots = 0f;
            _groundTrackHeading = state.Heading;
        }

        public void UpdatePhysics(AircraftState state, float deltaTime)
        {
            if (deltaTime <= 0f) return;

            // Update rotor RPM based on throttle
            UpdateRotorRpm(state, deltaTime);

            // Only proceed with flight physics if rotors are spinning
            if (_currentRotorRpm < 50f)
            {
                // Rotors stopped/stopping - just update position if moving
                if (state.GroundSpeedKnots > 0.1f)
                {
                    ApplyDrag(state, deltaTime);
                    UpdatePosition(state, deltaTime);
                }
                return;
            }

            // Calculate ground effect
            UpdateGroundEffect(state);

            // Update cyclic/rotor disc tilt
            UpdateRotorDiscTilt(state, deltaTime);

            // Update attitude based on cyclic and natural stability
            UpdateAttitude(state, deltaTime);

            // Update vertical motion based on collective and RPM
            UpdateVerticalMotion(state, deltaTime);

            // Update horizontal motion based on rotor disc tilt
            UpdateHorizontalMotion(state, deltaTime);

            // Update yaw based on tail rotor and torque
            UpdateYaw(state, deltaTime);

            // Update geographic position
            UpdatePosition(state, deltaTime);

            // Update state flags
            state.IsRotorSpooledUp = _currentRotorRpm >= MinFlightRotorRpm;
            state.IsInHover = state.GroundSpeedKnots < 5f && state.VerticalSpeedFpm < 100f;

            // Sync internal state to state object
            state.MainRotorRpm = _currentRotorRpm;
            state.TailRotorRpm = _currentRotorRpm * 3f; // Typical tail rotor ratio
            state.RotorDiscTiltAngle = _currentRotorDiscTilt;
            state.RotorDiscTiltDirection = _currentRotorDiscTiltDirection;
        }

        public void Reset(AircraftState state)
        {
            _currentRotorRpm = 0f;
            _smoothedCollective = 0f;
            _currentRotorDiscTilt = 0f;
            _currentRotorDiscTiltDirection = 0f;
            _currentTorque = 0f;
            _isEngineRunning = false;
            _forwardSpeedKnots = 0f;
            _lateralSpeedKnots = 0f;
            _groundTrackHeading = state.Heading;

            state.Pitch = 0f;
            state.Roll = 0f;
            state.Heading = state.Heading;
            state.IndicatedAirspeedKnots = 0f;
            state.GroundSpeedKnots = 0f;
            state.TrueAirspeedKnots = 0f;
            state.VerticalSpeedFpm = 0f;
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
            state.ThrottlePercent = 0f;
        }

        public IReadOnlyList<string> GetRequiredInputNames()
        {
            return new[]
            {
                "Collective",           // Vertical thrust
                "CyclicLongitudinal",   // Forward/aft cyclic (pitch)
                "CyclicLateral",        // Left/right cyclic (roll)
                "TailRotor",            // Pedals/yaw
                "Throttle"              // Rotor RPM control
            };
        }

        public bool ValidateState(AircraftState state)
        {
            return state.MainRotorRpm >= 0f &&
                   state.MainRotorRpm <= 100f &&
                   state.CollectiveInput >= -1f &&
                   state.CollectiveInput <= 1f;
        }

        #endregion

        #region Physics Update Methods

        private void UpdateRotorRpm(AircraftState state, float dt)
        {
            _isEngineRunning = state.ThrottlePercent > 0.05f;

            float targetRpm = _isEngineRunning ? MaxMainRotorRpm : 0f;

            // Determine spool rate based on direction
            float spoolRate;
            if (targetRpm > _currentRotorRpm)
            {
                // Spooling up
                spoolRate = MaxMainRotorRpm / RotorSpoolUpTime;
            }
            else if (targetRpm < _currentRotorRpm)
            {
                // Spooling down
                spoolRate = MaxMainRotorRpm / RotorSpoolDownTime;
            }
            else
            {
                return;
            }

            // Apply spool rate with inertia
            float rpmChange = spoolRate * dt / RotorInertia;
            _currentRotorRpm = Mathf.MoveTowards(_currentRotorRpm, targetRpm, rpmChange);

            // In autorotation (engine off, in descent), rotors may maintain RPM
            if (!_isEngineRunning && state.VerticalSpeedFpm < -500f)
            {
                // Maintain some RPM from airflow through rotors
                float autorotationRpm = MinFlightRotorRpm * 0.7f;
                _currentRotorRpm = Mathf.Max(_currentRotorRpm, autorotationRpm);
            }

            // Clamp to valid range
            _currentRotorRpm = Mathf.Clamp(_currentRotorRpm, 0f, MaxMainRotorRpm);

            // Update state
            state.MainRotorRpm = (_currentRotorRpm / MaxMainRotorRpm) * 100f; // Store as percentage
        }

        private void UpdateGroundEffect(AircraftState state)
        {
            float altitude = state.AltitudeMeters;

            if (altitude >= GroundEffectZeroHeight)
            {
                state.GroundEffectFactor = 0f;
            }
            else if (altitude <= GroundEffectHeight)
            {
                state.GroundEffectFactor = 1f;
            }
            else
            {
                // Linear interpolation between heights
                float t = (GroundEffectZeroHeight - altitude) / (GroundEffectZeroHeight - GroundEffectHeight);
                state.GroundEffectFactor = Mathf.Clamp01(t);
            }
        }

        private void UpdateRotorDiscTilt(AircraftState state, float dt)
        {
            // Calculate target tilt from cyclic inputs
            float targetTiltMagnitude = Mathf.Sqrt(
                state.CyclicLongitudinalInput * state.CyclicLongitudinalInput +
                state.CyclicLateralInput * state.CyclicLateralInput
            );
            targetTiltMagnitude = Mathf.Clamp01(targetTiltMagnitude) * CyclicToTiltFactor;

            // Calculate target direction
            float targetTiltDirection = Mathf.Atan2(state.CyclicLateralInput, state.CyclicLongitudinalInput) * Mathf.Rad2Deg;

            // Smoothly transition to target tilt
            float tiltChangeRate = (targetTiltMagnitude > _currentRotorDiscTilt) ? RotorTiltRate : RotorLevelRate;

            // Update tilt magnitude
            _currentRotorDiscTilt = Mathf.MoveTowards(_currentRotorDiscTilt, targetTiltMagnitude, tiltChangeRate * dt);

            // Update tilt direction (handle wraparound)
            float dirDiff = Mathf.DeltaAngle(_currentRotorDiscTiltDirection, targetTiltDirection);
            _currentRotorDiscTiltDirection = Mathf.MoveTowardsAngle(_currentRotorDiscTiltDirection, targetTiltDirection, RotorTiltRate * dt);

            // Store in state
            state.RotorDiscTiltAngle = _currentRotorDiscTilt;
            state.RotorDiscTiltDirection = _currentRotorDiscTiltDirection;
        }

        private void UpdateAttitude(AircraftState state, float dt)
        {
            // Helicopter attitude follows the rotor disc tilt with some lag and limits

            // Calculate target pitch based on longitudinal cyclic
            float targetPitch = state.CyclicLongitudinalInput * 30f; // Max 30 degree pitch

            // Calculate target roll based on lateral cyclic
            float targetRoll = -state.CyclicLateralInput * 30f; // Max 30 degree roll

            // Smoothly interpolate towards target attitude
            float pitchRate = MaxPitchRate * (_currentRotorRpm / MaxMainRotorRpm);
            float rollRate = MaxRollRate * (_currentRotorRpm / MaxMainRotorRpm);

            state.Pitch = Mathf.MoveTowards(state.Pitch, targetPitch, pitchRate * dt);
            state.Roll = Mathf.MoveTowards(state.Roll, targetRoll, rollRate * dt);

            // Apply damping when no cyclic input
            if (Mathf.Abs(state.CyclicLongitudinalInput) < 0.01f)
            {
                state.Pitch *= (1f - PitchDamping * dt);
            }
            if (Mathf.Abs(state.CyclicLateralInput) < 0.01f)
            {
                state.Roll *= (1f - RollDamping * dt);
            }

            // Clamp to safe limits
            state.Pitch = Mathf.Clamp(state.Pitch, -45f, 45f);
            state.Roll = Mathf.Clamp(state.Roll, -60f, 60f);
        }

        private void UpdateVerticalMotion(AircraftState state, float dt)
        {
            // Smooth collective input
            _smoothedCollective = Mathf.Lerp(_smoothedCollective, state.CollectiveInput, 5f * dt);

            // Calculate available lift based on RPM
            float rpmFactor = (_currentRotorRpm / MaxMainRotorRpm);
            float rpmFactorSquared = rpmFactor * rpmFactor; // Lift is proportional to RPM squared

            if (rpmFactorSquared < 0.25f)
            {
                // Not enough RPM for significant lift
                state.VerticalSpeedFpm = Mathf.Lerp(state.VerticalSpeedFpm, -1000f, dt);
                return;
            }

            // Calculate power required for hover
            float hoverCollective = HoverPowerRequired;

            // Apply ground effect benefit
            float groundEffectBonus = state.GroundEffectFactor * MaxGroundEffectBenefit;
            hoverCollective -= groundEffectBonus;

            // Calculate vertical speed based on collective relative to hover
            float collectiveDelta = _smoothedCollective - hoverCollective;

            // Determine target vertical speed
            float targetVerticalSpeed;
            if (collectiveDelta > 0)
            {
                // Climb - more collective = more climb
                targetVerticalSpeed = collectiveDelta / (1f - hoverCollective) * MaxClimbRateFpm;
            }
            else
            {
                // Descent - less collective = more descent
                targetVerticalSpeed = collectiveDelta / hoverCollective * Mathf.Abs(MaxDescentRateFpm);
            }

            // Scale by RPM squared (less lift at lower RPM)
            targetVerticalSpeed *= rpmFactorSquared;

            // Special case: autorotation (engine off, descending)
            if (!_isEngineRunning && state.VerticalSpeedFpm < -200f)
            {
                // In autorotation, collective controls descent rate
                targetVerticalSpeed = Mathf.Lerp(
                    AutorotationDescentRateFpm * 0.5f, // Min collective
                    AutorotationDescentRateFpm * 1.5f, // Max collective
                    (_smoothedCollective + 1f) * 0.5f
                );
            }

            // Smoothly transition to target vertical speed
            state.VerticalSpeedFpm = Mathf.Lerp(state.VerticalSpeedFpm, targetVerticalSpeed, 2f * dt);

            // Update altitude
            float altitudeChangeMeters = state.VerticalSpeedMps * dt;
            state.AltitudeMeters = Mathf.Max(0f, state.AltitudeMeters + altitudeChangeMeters);
        }

        private void UpdateHorizontalMotion(AircraftState state, float dt)
        {
            float rpmFactor = _currentRotorRpm / MaxMainRotorRpm;
            float tiltRad = _currentRotorDiscTilt * Mathf.Deg2Rad;

            // Convert collective to a positive lift factor for horizontal thrust.
            float liftFactor = Mathf.Clamp01((_smoothedCollective - AutorotationMinCollective) / (1f - AutorotationMinCollective));
            float thrustFactor = rpmFactor * rpmFactor * liftFactor;

            float targetForwardSpeed = 0f;
            float targetLateralSpeed = 0f;

            if (tiltRad > 0.001f && thrustFactor > 0f)
            {
                float tiltDirRad = _currentRotorDiscTiltDirection * Mathf.Deg2Rad;
                float forwardComponent = Mathf.Cos(tiltDirRad) * Mathf.Sin(tiltRad);
                float lateralComponent = Mathf.Sin(tiltDirRad) * Mathf.Sin(tiltRad);

                if (Mathf.Abs(forwardComponent) > 0.001f)
                {
                    float maxSpeed = forwardComponent > 0 ? MaxForwardSpeedKnots : MaxRearwardSpeedKnots;
                    targetForwardSpeed = forwardComponent * maxSpeed * thrustFactor;
                }

                if (Mathf.Abs(lateralComponent) > 0.001f)
                {
                    targetLateralSpeed = lateralComponent * MaxSidewaysSpeedKnots * thrustFactor;
                }
            }

            float currentSpeedKnots = Mathf.Sqrt(_forwardSpeedKnots * _forwardSpeedKnots + _lateralSpeedKnots * _lateralSpeedKnots);

            // Add translating tendency when in hover
            if (currentSpeedKnots < 10f && _currentRotorRpm > MinFlightRotorRpm)
            {
                float ttDirRad = TranslatingTendencyDirection * Mathf.Deg2Rad;
                targetForwardSpeed += Mathf.Cos(ttDirRad) * TranslatingTendencyKnots * rpmFactor;
                targetLateralSpeed += Mathf.Sin(ttDirRad) * TranslatingTendencyKnots * rpmFactor;
            }

            // Apply acceleration towards target speeds
            float acceleration = HorizontalAcceleration * rpmFactor * rpmFactor; // m/s^2
            float currentFwdMps = _forwardSpeedKnots * KnotsToMps;
            float currentLatMps = _lateralSpeedKnots * KnotsToMps;
            float targetFwdMps = targetForwardSpeed * KnotsToMps;
            float targetLatMps = targetLateralSpeed * KnotsToMps;

            currentFwdMps = Mathf.MoveTowards(currentFwdMps, targetFwdMps, acceleration * dt);
            currentLatMps = Mathf.MoveTowards(currentLatMps, targetLatMps, acceleration * dt);

            // Apply base drag
            float dragFactor = Mathf.Clamp01(1f - HorizontalDrag * dt);
            currentFwdMps *= dragFactor;
            currentLatMps *= dragFactor;

            // Extra stabilization when hovering with minimal cyclic input
            float inputMag = Mathf.Max(Mathf.Abs(state.CyclicLongitudinalInput), Mathf.Abs(state.CyclicLateralInput));
            if (HoverStabilizationDrag > 0f && inputMag < HoverStabilizationInputThreshold)
            {
                float speedKnots = Mathf.Sqrt(currentFwdMps * currentFwdMps + currentLatMps * currentLatMps) * MpsToKnots;
                if (speedKnots < HoverStabilizationMaxSpeedKnots)
                {
                    float hoverDragFactor = Mathf.Clamp01(1f - HoverStabilizationDrag * dt);
                    currentFwdMps *= hoverDragFactor;
                    currentLatMps *= hoverDragFactor;
                }
            }

            // Convert back to knots
            _forwardSpeedKnots = currentFwdMps * MpsToKnots;
            _lateralSpeedKnots = currentLatMps * MpsToKnots;

            // Calculate total ground speed
            state.GroundSpeedKnots = Mathf.Sqrt(_forwardSpeedKnots * _forwardSpeedKnots + _lateralSpeedKnots * _lateralSpeedKnots);

            // Update airspeed (add vertical component)
            float verticalSpeedKnots = state.VerticalSpeedFpm * 0.0098747f; // fpm to knots
            state.TrueAirspeedKnots = Mathf.Sqrt(
                state.GroundSpeedKnots * state.GroundSpeedKnots +
                verticalSpeedKnots * verticalSpeedKnots
            );

            // Indicated airspeed is slightly less than true (simplified)
            state.IndicatedAirspeedKnots = state.TrueAirspeedKnots * 0.98f;

            UpdateGroundTrackHeading(state);
        }

        private void UpdateYaw(AircraftState state, float dt)
        {
            // Calculate torque effect (opposite to main rotor rotation)
            // More collective = more torque
            float torqueEffect = _smoothedCollective * MaxTorqueYawRate * TorqueDirection;

            // Tail rotor counters torque
            // Positive tail rotor input = nose right (counter typical main rotor torque)
            float tailRotorEffect = state.TailRotorInput * MaxYawRate;

            // Calculate net yaw rate
            float targetYawRate = tailRotorEffect - torqueEffect;

            // Scale by RPM (no yaw control without rotor)
            float rpmFactor = _currentRotorRpm / MaxMainRotorRpm;
            targetYawRate *= rpmFactor * rpmFactor;

            // Apply damping
            targetYawRate *= (1f - YawDamping);

            // Update heading
            state.Heading = Mathf.Repeat(state.Heading + targetYawRate * dt, 360f);
        }

        private void UpdatePosition(AircraftState state, float dt)
        {
            // Speed in meters per second
            float speedMps = state.GroundSpeedMps;

            if (speedMps < 0.001f) return;

            // Distance traveled in this frame
            float distanceMeters = speedMps * dt;

            // Convert heading to radians
            float headingRad = _groundTrackHeading * Mathf.Deg2Rad;

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

        private void ApplyDrag(AircraftState state, float dt)
        {
            // Simple drag when rotors are not providing thrust
            float dragFactor = Mathf.Clamp01(1f - HorizontalDrag * dt);
            _forwardSpeedKnots *= dragFactor;
            _lateralSpeedKnots *= dragFactor;
            state.GroundSpeedKnots = Mathf.Sqrt(_forwardSpeedKnots * _forwardSpeedKnots + _lateralSpeedKnots * _lateralSpeedKnots);
            state.TrueAirspeedKnots = state.GroundSpeedKnots;
            state.IndicatedAirspeedKnots = state.GroundSpeedKnots;
            UpdateGroundTrackHeading(state);
        }

        #endregion

        #region Track Helpers

        private void UpdateGroundTrackHeading(AircraftState state)
        {
            float headingRad = state.Heading * Mathf.Deg2Rad;
            float northComponent = _forwardSpeedKnots * Mathf.Cos(headingRad) - _lateralSpeedKnots * Mathf.Sin(headingRad);
            float eastComponent = _forwardSpeedKnots * Mathf.Sin(headingRad) + _lateralSpeedKnots * Mathf.Cos(headingRad);

            if (Mathf.Abs(northComponent) < 0.001f && Mathf.Abs(eastComponent) < 0.001f)
            {
                _groundTrackHeading = state.Heading;
                return;
            }

            _groundTrackHeading = Mathf.Repeat(Mathf.Atan2(eastComponent, northComponent) * Mathf.Rad2Deg, 360f);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Check if the helicopter has enough rotor RPM for flight
        /// </summary>
        public bool IsRotorSpooledUp()
        {
            return _currentRotorRpm >= MinFlightRotorRpm;
        }

        /// <summary>
        /// Get the current rotor RPM
        /// </summary>
        public float GetCurrentRotorRpm()
        {
            return _currentRotorRpm;
        }

        /// <summary>
        /// Emergency rotor start (for testing)
        /// </summary>
        public void SetRotorRpm(float rpm)
        {
            _currentRotorRpm = Mathf.Clamp(rpm, 0f, MaxMainRotorRpm);
        }

        #endregion
    }
}
