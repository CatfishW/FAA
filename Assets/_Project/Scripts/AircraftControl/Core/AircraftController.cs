using System;
using UnityEngine;
using TrafficRadar.Core;
using FAA.Geo;

namespace AircraftControl.Core
{
    /// <summary>
    /// Main aircraft controller implementing FAA-standard flight controls.
    /// Provides keyboard input handling, physics-based movement, and position broadcasting.
    /// Also implements IOwnShipPositionProvider for radar integration.
    /// 
    /// Setup:
    /// 1. Add this component to your aircraft GameObject
    /// 2. Optionally assign GeoPosUnityPosProjectManager for geo coordinate conversion
    /// 3. Configure control sensitivities and flight characteristics
    /// </summary>
    [AddComponentMenu("Aircraft Control/Aircraft Controller")]
    public class AircraftController : MonoBehaviour, IAircraftController, IOwnShipPositionProvider
    {
        #region Inspector Settings

        [Header("Aircraft Type")]
        [Tooltip("Type of aircraft - determines control scheme and physics model")]
        [SerializeField] private AircraftType aircraftType = AircraftType.FixedWing;

        [Header("Initial Position")]
        [Tooltip("Starting latitude in decimal degrees")]
        [SerializeField] private double initialLatitude = 33.6407;

        [Tooltip("Starting longitude in decimal degrees")]
        [SerializeField] private double initialLongitude = -84.4277;

        [Tooltip("Starting altitude in feet")]
        [SerializeField] private float initialAltitudeFeet = 10000f;

        [Tooltip("Starting heading in degrees")]
        [SerializeField] private float initialHeading = 0f;

        [Header("Control Input Settings")]
        [Tooltip("Smoothing factor for control inputs (lower = smoother)")]
        [Range(0.01f, 1f)]
        [SerializeField] private float inputSmoothing = 0.1f;

        [Tooltip("Dead zone for control inputs")]
        [Range(0f, 0.2f)]
        [SerializeField] private float inputDeadzone = 0.05f;

        [Header("Keyboard Bindings - Fixed Wing")]
        [SerializeField] private KeyCode pitchUpKey = KeyCode.S;
        [SerializeField] private KeyCode pitchDownKey = KeyCode.W;
        [SerializeField] private KeyCode rollLeftKey = KeyCode.A;
        [SerializeField] private KeyCode rollRightKey = KeyCode.D;
        [SerializeField] private KeyCode yawLeftKey = KeyCode.Q;
        [SerializeField] private KeyCode yawRightKey = KeyCode.E;
        [SerializeField] private KeyCode throttleUpKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode throttleDownKey = KeyCode.LeftControl;

        [Header("Keyboard Bindings - Helicopter")]
        [SerializeField] private KeyCode collectiveUpKey = KeyCode.R;
        [SerializeField] private KeyCode collectiveDownKey = KeyCode.F;
        [SerializeField] private KeyCode cyclicForwardKey = KeyCode.W;
        [SerializeField] private KeyCode cyclicBackwardKey = KeyCode.S;
        [SerializeField] private KeyCode cyclicLeftKey = KeyCode.A;
        [SerializeField] private KeyCode cyclicRightKey = KeyCode.D;
        [SerializeField] private KeyCode pedalLeftKey = KeyCode.Q;
        [SerializeField] private KeyCode pedalRightKey = KeyCode.E;
        [SerializeField] private KeyCode rotorStartKey = KeyCode.T;

        [Header("Flight Dynamics - Fixed Wing")]
        [Tooltip("Maximum pitch rate in degrees per second")]
        [SerializeField] private float maxPitchRate = 15f;

        [Tooltip("Maximum roll rate in degrees per second")]
        [SerializeField] private float maxRollRate = 45f;

        [Tooltip("Maximum yaw rate in degrees per second")]
        [SerializeField] private float maxYawRate = 10f;

        [Tooltip("Maximum airspeed in knots")]
        [SerializeField] private float maxAirspeedKnots = 350f;

        [Tooltip("Minimum airspeed in knots")]
        [SerializeField] private float minAirspeedKnots = 60f;

        [Tooltip("Rate of speed change in knots per second")]
        [SerializeField] private float speedChangeRate = 10f;

        [Tooltip("Climb rate per degree of pitch in fpm")]
        [SerializeField] private float climbRatePerPitchDegree = 100f;

        [Tooltip("Enable auto-level when no pitch input (returns to level flight)")]
        [SerializeField] private bool autoLevelPitch = true;

        [Tooltip("Enable auto-level when no roll input")]
        [SerializeField] private bool autoLevelRoll = true;

        [Tooltip("Auto-level rate in degrees per second")]
        [SerializeField] private float autoLevelRate = 10f;

        [Header("Flight Dynamics - Helicopter")]
        [Tooltip("Maximum vertical climb rate in fpm")]
        [SerializeField] private float helicopterMaxClimbRate = 2000f;

        [Tooltip("Maximum forward speed in knots")]
        [SerializeField] private float helicopterMaxForwardSpeed = 150f;

        [Tooltip("Rotor spool up time in seconds")]
        [SerializeField] private float rotorSpoolUpTime = 8f;

        [Tooltip("Hover power required (% of max)")]
        [Range(0.3f, 0.9f)]
        [SerializeField] private float hoverPowerRequired = 0.65f;

        [Header("Unity Integration")]
        [Tooltip("If true, updates transform position based on flight")]
        [SerializeField] private bool updateTransformPosition = true;

        [Tooltip("Reference to GeoPosUnityPosProjectManager for coordinate conversion")]
        [SerializeField] private FAA.Geo.GeoPosUnityPosProjectManager geoProjection;

        [Header("Position Broadcasting")]
        [Tooltip("Minimum position change to trigger event (meters)")]
        [SerializeField] private float positionChangeThreshold = 10f;

        [Tooltip("Minimum time between position broadcasts (seconds)")]
        [SerializeField] private float minBroadcastInterval = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        #endregion
        
        #region Private Fields

        private AircraftState _state;
        private bool _isEnabled = true;
        private bool _isUserControlled = true;

        // Flight dynamics strategy (Strategy pattern)
        private IFlightDynamics _flightDynamics;

        // Control input targets (before smoothing)
        private float _targetPitch;
        private float _targetRoll;
        private float _targetYaw;
        private float _targetThrottle;

        // Helicopter-specific input targets
        private float _targetCollective;
        private float _targetCyclicLongitudinal;
        private float _targetCyclicLateral;
        private float _targetTailRotor;

        // Smoothed inputs
        private float _smoothedPitch;
        private float _smoothedRoll;
        private float _smoothedYaw;
        private float _smoothedCollective;
        private float _smoothedCyclicLongitudinal;
        private float _smoothedCyclicLateral;
        private float _smoothedTailRotor;

        // Position tracking for events
        private Vector3 _lastBroadcastPosition;
        private float _lastBroadcastTime;

        // Cached OwnShipPosition for interface
        private OwnShipPosition _ownShipPosition;

        // Runtime configured flag
        private bool _isConfigured = false;

        #endregion
        
        #region IAircraftController Implementation

        public AircraftState State => _state;
        public bool IsEnabled => _isEnabled;
        public bool IsUserControlled => _isUserControlled;
        public AircraftType CurrentAircraftType => aircraftType;
        public IFlightDynamics FlightDynamics => _flightDynamics;

        public event Action<AircraftState> OnStateChanged;
        public event Action<double, double, float> OnPositionChanged;

        #endregion
        
        #region IOwnShipPositionProvider Implementation
        
        event Action<OwnShipPosition> IOwnShipPositionProvider.OnPositionChanged
        {
            add => _ownShipPositionChanged += value;
            remove => _ownShipPositionChanged -= value;
        }
        private event Action<OwnShipPosition> _ownShipPositionChanged;
        
        public OwnShipPosition CurrentPosition => _ownShipPosition;
        public bool IsValid => _state != null;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            InitializeFlightDynamics();
            InitializeState();
            FindDependencies();
        }

        private void InitializeFlightDynamics()
        {
            if (_flightDynamics != null && _flightDynamics.AircraftType == aircraftType) return;

            if (aircraftType == AircraftType.Helicopter)
            {
                _flightDynamics = new HelicopterDynamics
                {
                    MaxClimbRateFpm = helicopterMaxClimbRate,
                    MaxForwardSpeedKnots = helicopterMaxForwardSpeed,
                    RotorSpoolUpTime = rotorSpoolUpTime,
                    HoverPowerRequired = hoverPowerRequired
                };
            }
            else
            {
                _flightDynamics = new FixedWingDynamics
                {
                    MaxPitchRate = maxPitchRate,
                    MaxRollRate = maxRollRate,
                    MaxYawRate = maxYawRate,
                    MaxAirspeedKnots = maxAirspeedKnots,
                    MinAirspeedKnots = minAirspeedKnots,
                    SpeedChangeRate = speedChangeRate,
                    ClimbRatePerPitchDegree = climbRatePerPitchDegree,
                    AutoLevelPitch = autoLevelPitch,
                    AutoLevelRoll = autoLevelRoll,
                    AutoLevelRate = autoLevelRate
                };
            }

            _isConfigured = true;
        }
        
        private void Start()
        {
            // Set initial Unity position if projection manager available
            if (updateTransformPosition && geoProjection != null)
            {
                transform.position = geoProjection.GeoToUnityPosition(
                    _state.Latitude, 
                    _state.Longitude, 
                    _state.AltitudeMeters
                );
            }
            
            // Initialize broadcast tracking
            _lastBroadcastPosition = transform.position;
            _lastBroadcastTime = Time.time;
            
            // Initial position broadcast
            BroadcastPosition();
        }
        
        private void Update()
        {
            if (!_isEnabled) return;

            // Process keyboard input
            if (_isUserControlled)
            {
                ProcessKeyboardInput();
            }

            // Smooth control inputs
            SmoothInputs();

            // Apply smoothed inputs to state
            ApplyInputsToState();

            // Update aircraft physics using strategy
            _flightDynamics?.UpdatePhysics(_state, Time.deltaTime);

            // Update OwnShipPosition after physics update
            UpdateOwnShipPosition();

            // Update Unity transform
            if (updateTransformPosition)
            {
                UpdateTransformFromState();
            }

            // Check for position broadcast
            CheckPositionBroadcast();

            // Fire state changed event
            OnStateChanged?.Invoke(_state);
        }

        #endregion

        #region Aircraft Type Management

        /// <summary>
        /// Change the aircraft type at runtime
        /// </summary>
        public void SetAircraftType(AircraftType newType)
        {
            if (aircraftType == newType) return;

            aircraftType = newType;

            // Reset state for new aircraft type
            _state = AircraftState.CreateDefault(newType, _state.Latitude, _state.Longitude);

            // Reinitialize flight dynamics
            InitializeFlightDynamics();
            _flightDynamics.Initialize(_state);

            // Reset inputs
            ResetInputs();

            // Update transform
            if (updateTransformPosition)
            {
                UpdateTransformFromState();
            }

            if (showDebugInfo)
            {
                Debug.Log($"[AircraftController] Switched to {newType}");
            }
        }

        /// <summary>
        /// Get the current flight dynamics configuration
        /// </summary>
        public T GetFlightDynamics<T>() where T : class, IFlightDynamics
        {
            return _flightDynamics as T;
        }

        private void ResetInputs()
        {
            _targetPitch = 0f;
            _targetRoll = 0f;
            _targetYaw = 0f;
            _targetThrottle = aircraftType == AircraftType.Helicopter ? 0f : 0.5f;
            _targetCollective = 0f;
            _targetCyclicLongitudinal = 0f;
            _targetCyclicLateral = 0f;
            _targetTailRotor = 0f;

            _smoothedPitch = 0f;
            _smoothedRoll = 0f;
            _smoothedYaw = 0f;
            _smoothedCollective = 0f;
            _smoothedCyclicLongitudinal = 0f;
            _smoothedCyclicLateral = 0f;
            _smoothedTailRotor = 0f;
        }

        #endregion

        #region Initialization
        
        private void InitializeState()
        {
            // Ensure flight dynamics is initialized
            if (_flightDynamics == null)
            {
                InitializeFlightDynamics();
            }

            // Create state based on aircraft type
            _state = AircraftState.CreateDefault(aircraftType, initialLatitude, initialLongitude);

            // Apply initial settings
            _state.AltitudeMeters = initialAltitudeFeet / 3.28084f;
            _state.Heading = initialHeading;

            // Initialize input targets
            _targetThrottle = aircraftType == AircraftType.Helicopter ? 0f : 0.5f;
            _targetCollective = 0f;
            _targetCyclicLongitudinal = 0f;
            _targetCyclicLateral = 0f;
            _targetTailRotor = 0f;

            // Initialize flight dynamics
            _flightDynamics?.Initialize(_state);

            // Initialize OwnShipPosition
            UpdateOwnShipPosition();
        }
        
        private void FindDependencies()
        {
            if (geoProjection == null)
            {
                geoProjection = FAA.Geo.GeoPosUnityPosProjectManager.Instance;
            }
        }
        
        #endregion
        
        #region Input Processing
        
        private void ProcessKeyboardInput()
        {
            if (aircraftType == AircraftType.Helicopter)
            {
                ProcessHelicopterInput();
            }
            else
            {
                ProcessFixedWingInput();
            }
        }

        private void ProcessFixedWingInput()
        {
            // Pitch: W = nose down (negative), S = nose up (positive)
            float pitchInput = 0f;
            if (Input.GetKey(pitchUpKey)) pitchInput = 1f;
            else if (Input.GetKey(pitchDownKey)) pitchInput = -1f;
            _targetPitch = pitchInput;

            // Roll: A = left (negative), D = right (positive)
            float rollInput = 0f;
            if (Input.GetKey(rollRightKey)) rollInput = 1f;
            else if (Input.GetKey(rollLeftKey)) rollInput = -1f;
            _targetRoll = rollInput;

            // Yaw: Q = left (negative), E = right (positive)
            float yawInput = 0f;
            if (Input.GetKey(yawRightKey)) yawInput = 1f;
            else if (Input.GetKey(yawLeftKey)) yawInput = -1f;
            _targetYaw = yawInput;

            // Throttle: Shift = increase, Ctrl = decrease
            if (Input.GetKey(throttleUpKey))
            {
                _targetThrottle = Mathf.Min(1f, _targetThrottle + Time.deltaTime * 0.5f);
            }
            else if (Input.GetKey(throttleDownKey))
            {
                _targetThrottle = Mathf.Max(0f, _targetThrottle - Time.deltaTime * 0.5f);
            }
        }

        private void ProcessHelicopterInput()
        {
            // Cyclic Longitudinal (Forward/Aft): W = forward (pitch down), S = backward (pitch up)
            float cyclicLongInput = 0f;
            if (Input.GetKey(cyclicForwardKey)) cyclicLongInput = 1f;
            else if (Input.GetKey(cyclicBackwardKey)) cyclicLongInput = -1f;
            _targetCyclicLongitudinal = cyclicLongInput;

            // Cyclic Lateral (Left/Right): D = right, A = left
            float cyclicLatInput = 0f;
            if (Input.GetKey(cyclicRightKey)) cyclicLatInput = 1f;
            else if (Input.GetKey(cyclicLeftKey)) cyclicLatInput = -1f;
            _targetCyclicLateral = cyclicLatInput;

            // Tail Rotor (Yaw): E = right, Q = left
            float pedalInput = 0f;
            if (Input.GetKey(pedalRightKey)) pedalInput = 1f;
            else if (Input.GetKey(pedalLeftKey)) pedalInput = -1f;
            _targetTailRotor = pedalInput;

            // Collective: R = increase, F = decrease
            if (Input.GetKey(collectiveUpKey))
            {
                _targetCollective = Mathf.Min(1f, _targetCollective + Time.deltaTime * 0.8f);
            }
            else if (Input.GetKey(collectiveDownKey))
            {
                _targetCollective = Mathf.Max(-1f, _targetCollective - Time.deltaTime * 0.8f);
            }

            // Throttle (Rotor RPM): Shift = increase, Ctrl = decrease
            if (Input.GetKey(throttleUpKey))
            {
                _targetThrottle = Mathf.Min(1f, _targetThrottle + Time.deltaTime * 0.3f);
            }
            else if (Input.GetKey(throttleDownKey))
            {
                _targetThrottle = Mathf.Max(0f, _targetThrottle - Time.deltaTime * 0.3f);
            }

            // Quick rotor start/stop: T key toggles throttle
            if (Input.GetKeyDown(rotorStartKey))
            {
                _targetThrottle = _targetThrottle > 0.5f ? 0f : 1f;
            }
        }
        
        private void SmoothInputs()
        {
            float smoothFactor = inputSmoothing * 60f * Time.deltaTime;

            if (aircraftType == AircraftType.Helicopter)
            {
                // Smooth helicopter inputs
                _smoothedCollective = Mathf.Lerp(_smoothedCollective, _targetCollective, smoothFactor * 0.8f);
                _smoothedCyclicLongitudinal = Mathf.Lerp(_smoothedCyclicLongitudinal, _targetCyclicLongitudinal, smoothFactor);
                _smoothedCyclicLateral = Mathf.Lerp(_smoothedCyclicLateral, _targetCyclicLateral, smoothFactor);
                _smoothedTailRotor = Mathf.Lerp(_smoothedTailRotor, _targetTailRotor, smoothFactor);

                // Apply deadzone
                if (Mathf.Abs(_smoothedCollective) < inputDeadzone) _smoothedCollective = 0f;
                if (Mathf.Abs(_smoothedCyclicLongitudinal) < inputDeadzone) _smoothedCyclicLongitudinal = 0f;
                if (Mathf.Abs(_smoothedCyclicLateral) < inputDeadzone) _smoothedCyclicLateral = 0f;
                if (Mathf.Abs(_smoothedTailRotor) < inputDeadzone) _smoothedTailRotor = 0f;
            }
            else
            {
                // Smooth fixed-wing inputs
                _smoothedPitch = Mathf.Lerp(_smoothedPitch, _targetPitch, smoothFactor);
                _smoothedRoll = Mathf.Lerp(_smoothedRoll, _targetRoll, smoothFactor);
                _smoothedYaw = Mathf.Lerp(_smoothedYaw, _targetYaw, smoothFactor);

                // Apply deadzone
                if (Mathf.Abs(_smoothedPitch) < inputDeadzone) _smoothedPitch = 0f;
                if (Mathf.Abs(_smoothedRoll) < inputDeadzone) _smoothedRoll = 0f;
                if (Mathf.Abs(_smoothedYaw) < inputDeadzone) _smoothedYaw = 0f;
            }
        }

        private void ApplyInputsToState()
        {
            if (aircraftType == AircraftType.Helicopter)
            {
                // Apply helicopter inputs to state
                _state.CollectiveInput = _smoothedCollective;
                _state.CyclicLongitudinalInput = _smoothedCyclicLongitudinal;
                _state.CyclicLateralInput = _smoothedCyclicLateral;
                _state.TailRotorInput = _smoothedTailRotor;
                _state.ThrottlePercent = _targetThrottle * 100f;

                // Also map to fixed-wing style inputs for compatibility
                _state.ElevatorInput = _smoothedCyclicLongitudinal;
                _state.AileronInput = _smoothedCyclicLateral;
                _state.RudderInput = _smoothedTailRotor;
            }
            else
            {
                // Apply fixed-wing inputs to state
                _state.ElevatorInput = _smoothedPitch;
                _state.AileronInput = _smoothedRoll;
                _state.RudderInput = _smoothedYaw;
                _state.ThrottlePercent = _targetThrottle * 100f;

                // Clear helicopter-specific inputs
                _state.CollectiveInput = 0f;
                _state.CyclicLongitudinalInput = _smoothedPitch;
                _state.CyclicLateralInput = _smoothedRoll;
                _state.TailRotorInput = _smoothedYaw;
            }
        }
        
        #endregion
        
        #region Transform and Position Updates

        private void UpdateTransformFromState()
        {
            if (geoProjection != null)
            {
                // Convert geo position to Unity position
                Vector3 newPos = geoProjection.GeoToUnityPosition(
                    _state.Latitude,
                    _state.Longitude,
                    _state.AltitudeMeters
                );
                transform.position = newPos;
            }
            
            // Update rotation
            transform.rotation = Quaternion.Euler(_state.Pitch, _state.Heading, -_state.Roll);
        }
        
        private void UpdateOwnShipPosition()
        {
            _ownShipPosition = new OwnShipPosition
            {
                Latitude = _state.Latitude,
                Longitude = _state.Longitude,
                AltitudeMeters = _state.AltitudeMeters,
                HeadingDegrees = _state.Heading,
                GroundSpeedMps = _state.GroundSpeedMps
            };
        }
        
        #endregion
        
        #region Position Broadcasting
        
        private void CheckPositionBroadcast()
        {
            // Check time interval
            if (Time.time - _lastBroadcastTime < minBroadcastInterval)
                return;
            
            // Check position change threshold
            float distance = Vector3.Distance(transform.position, _lastBroadcastPosition);
            if (distance < positionChangeThreshold && Time.time - _lastBroadcastTime < 2f)
                return;
            
            BroadcastPosition();
        }
        
        private void BroadcastPosition()
        {
            _lastBroadcastPosition = transform.position;
            _lastBroadcastTime = Time.time;
            
            // Fire position changed event
            OnPositionChanged?.Invoke(_state.Latitude, _state.Longitude, _state.AltitudeMeters);
            
            // Fire IOwnShipPositionProvider event
            _ownShipPositionChanged?.Invoke(_ownShipPosition);
            
            if (showDebugInfo)
            {
                Debug.Log($"[AircraftController] Position broadcast: {_state.Latitude:F4}, {_state.Longitude:F4}, {_state.AltitudeFeet:F0}ft");
            }
        }
        
        #endregion
        
        #region Public Control Methods
        
        public void SetThrottle(float value)
        {
            _targetThrottle = Mathf.Clamp01(value);
            if (_state != null)
            {
                _state.ThrottlePercent = _targetThrottle * 100f;
            }
        }
        
        public void SetPitch(float value)
        {
            _targetPitch = Mathf.Clamp(value, -1f, 1f);
            _smoothedPitch = _targetPitch;
            if (_state != null)
            {
                _state.ElevatorInput = _targetPitch;
                if (aircraftType == AircraftType.Helicopter)
                {
                    _state.CyclicLongitudinalInput = _targetPitch;
                }
            }
        }
        
        public void SetRoll(float value)
        {
            _targetRoll = Mathf.Clamp(value, -1f, 1f);
            _smoothedRoll = _targetRoll;
            if (_state != null)
            {
                _state.AileronInput = _targetRoll;
                if (aircraftType == AircraftType.Helicopter)
                {
                    _state.CyclicLateralInput = _targetRoll;
                }
            }
        }
        
        public void SetYaw(float value)
        {
            _targetYaw = Mathf.Clamp(value, -1f, 1f);
            _smoothedYaw = _targetYaw;
            if (_state != null)
            {
                _state.RudderInput = _targetYaw;
                if (aircraftType == AircraftType.Helicopter)
                {
                    _state.TailRotorInput = _targetYaw;
                }
            }
        }

        #region Helicopter Control Methods

        /// <summary>
        /// Set collective pitch input for helicopters (-1 to 1)
        /// </summary>
        public void SetCollective(float value)
        {
            _targetCollective = Mathf.Clamp(value, -1f, 1f);
            _smoothedCollective = _targetCollective;
            if (_state != null)
            {
                _state.CollectiveInput = _targetCollective;
            }
        }

        /// <summary>
        /// Set cyclic longitudinal input for helicopters (-1 to 1)
        /// Positive = forward (pitch down), Negative = backward (pitch up)
        /// </summary>
        public void SetCyclicLongitudinal(float value)
        {
            _targetCyclicLongitudinal = Mathf.Clamp(value, -1f, 1f);
            _smoothedCyclicLongitudinal = _targetCyclicLongitudinal;
            if (_state != null)
            {
                _state.CyclicLongitudinalInput = _targetCyclicLongitudinal;
            }
        }

        /// <summary>
        /// Set cyclic lateral input for helicopters (-1 to 1)
        /// Positive = right (roll right), Negative = left (roll left)
        /// </summary>
        public void SetCyclicLateral(float value)
        {
            _targetCyclicLateral = Mathf.Clamp(value, -1f, 1f);
            _smoothedCyclicLateral = _targetCyclicLateral;
            if (_state != null)
            {
                _state.CyclicLateralInput = _targetCyclicLateral;
            }
        }

        /// <summary>
        /// Set tail rotor input (pedals) for helicopters (-1 to 1)
        /// Positive = yaw right, Negative = yaw left
        /// </summary>
        public void SetTailRotor(float value)
        {
            _targetTailRotor = Mathf.Clamp(value, -1f, 1f);
            _smoothedTailRotor = _targetTailRotor;
            if (_state != null)
            {
                _state.TailRotorInput = _targetTailRotor;
            }
        }

        /// <summary>
        /// Set all helicopter controls at once
        /// </summary>
        public void SetHelicopterControls(float collective, float cyclicLongitudinal, float cyclicLateral, float tailRotor)
        {
            SetCollective(collective);
            SetCyclicLongitudinal(cyclicLongitudinal);
            SetCyclicLateral(cyclicLateral);
            SetTailRotor(tailRotor);
        }

        #endregion

        public void SetControlEnabled(bool enabled)
        {
            _isEnabled = enabled;
        }
        
        public void SetUserControlled(bool userControlled)
        {
            _isUserControlled = userControlled;
        }
        
        public void ResetToDefault()
        {
            InitializeState();
            if (updateTransformPosition && geoProjection != null)
            {
                transform.position = geoProjection.GeoToUnityPosition(
                    _state.Latitude,
                    _state.Longitude,
                    _state.AltitudeMeters
                );
            }
            BroadcastPosition();
        }
        
        /// <summary>
        /// Set aircraft position directly (for external systems)
        /// </summary>
        public void SetPosition(double latitude, double longitude, float altitudeMeters, float heading)
        {
            _state.Latitude = latitude;
            _state.Longitude = longitude;
            _state.AltitudeMeters = altitudeMeters;
            _state.Heading = heading;
            
            UpdateOwnShipPosition();
            BroadcastPosition();
        }
        
        #endregion
        
        #region Debug
        
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 350, 450));
            GUILayout.BeginVertical("box");

            GUILayout.Label($"=== {aircraftType} Controller ===");
            GUILayout.Label($"Position: {_state.Latitude:F4}, {_state.Longitude:F4}");
            GUILayout.Label($"Altitude: {_state.AltitudeFeet:F0} ft");
            GUILayout.Label($"Heading: {_state.Heading:F1}°");
            GUILayout.Label($"Pitch: {_state.Pitch:F1}° | Roll: {_state.Roll:F1}°");
            GUILayout.Label($"Airspeed: {_state.IndicatedAirspeedKnots:F0} kts");
            GUILayout.Label($"VS: {_state.VerticalSpeedFpm:F0} fpm");
            GUILayout.Label($"Throttle: {_state.ThrottlePercent:F0}%");

            if (aircraftType == AircraftType.Helicopter)
            {
                GUILayout.Space(10);
                GUILayout.Label("--- Helicopter Systems ---");
                GUILayout.Label($"Rotor RPM: {_state.MainRotorRpm:F0}% {(_state.IsRotorSpooledUp ? "(Ready)" : "(Spooling)")}");
                GUILayout.Label($"Collective: {_state.CollectiveInput:F2}");
                GUILayout.Label($"Cyclic Fwd/Aft: {_state.CyclicLongitudinalInput:F2}");
                GUILayout.Label($"Cyclic L/R: {_state.CyclicLateralInput:F2}");
                GUILayout.Label($"Pedals: {_state.TailRotorInput:F2}");
                GUILayout.Label($"Ground Effect: {_state.GroundEffectFactor:P0}");
                GUILayout.Label($"Hover: {(_state.IsInHover ? "Yes" : "No")}");
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
