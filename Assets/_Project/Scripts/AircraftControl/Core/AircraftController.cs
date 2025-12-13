using System;
using UnityEngine;
using TrafficRadar.Core;

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
        
        [Header("Initial Position")]
        [Tooltip("Starting latitude in decimal degrees")]
        [SerializeField] private double initialLatitude = 33.6407;
        
        [Tooltip("Starting longitude in decimal degrees")]
        [SerializeField] private double initialLongitude = -84.4277;
        
        [Tooltip("Starting altitude in feet")]
        [SerializeField] private float initialAltitudeFeet = 10000f;
        
        [Tooltip("Starting heading in degrees")]
        [SerializeField] private float initialHeading = 0f;
        
        [Header("Flight Characteristics")]
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
        
        [Header("Control Input Settings")]
        [Tooltip("Smoothing factor for control inputs (lower = smoother)")]
        [Range(0.01f, 1f)]
        [SerializeField] private float inputSmoothing = 0.1f;
        
        [Tooltip("Dead zone for control inputs")]
        [Range(0f, 0.2f)]
        [SerializeField] private float inputDeadzone = 0.05f;
        
        [Header("Keyboard Bindings")]
        [SerializeField] private KeyCode pitchUpKey = KeyCode.S;
        [SerializeField] private KeyCode pitchDownKey = KeyCode.W;
        [SerializeField] private KeyCode rollLeftKey = KeyCode.A;
        [SerializeField] private KeyCode rollRightKey = KeyCode.D;
        [SerializeField] private KeyCode yawLeftKey = KeyCode.Q;
        [SerializeField] private KeyCode yawRightKey = KeyCode.E;
        [SerializeField] private KeyCode throttleUpKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode throttleDownKey = KeyCode.LeftControl;
        
        [Header("Auto-Level")]
        [Tooltip("Enable auto-level when no pitch input (returns to level flight)")]
        [SerializeField] private bool autoLevelPitch = true;
        
        [Tooltip("Enable auto-level when no roll input")]
        [SerializeField] private bool autoLevelRoll = true;
        
        [Tooltip("Auto-level rate in degrees per second")]
        [SerializeField] private float autoLevelRate = 10f;
        
        [Header("Unity Integration")]
        [Tooltip("If true, updates transform position based on flight")]
        [SerializeField] private bool updateTransformPosition = true;
        
        [Tooltip("Reference to GeoPosUnityPosProjectManager for coordinate conversion")]
        [SerializeField] private GeoPosUnityPosProjectManager geoProjection;
        
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
        
        // Control input targets (before smoothing)
        private float _targetPitch;
        private float _targetRoll;
        private float _targetYaw;
        private float _targetThrottle;
        
        // Smoothed inputs
        private float _smoothedPitch;
        private float _smoothedRoll;
        private float _smoothedYaw;
        
        // Position tracking for events
        private Vector3 _lastBroadcastPosition;
        private float _lastBroadcastTime;
        
        // Cached OwnShipPosition for interface
        private OwnShipPosition _ownShipPosition;
        
        #endregion
        
        #region IAircraftController Implementation
        
        public AircraftState State => _state;
        public bool IsEnabled => _isEnabled;
        public bool IsUserControlled => _isUserControlled;
        
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
            InitializeState();
            FindDependencies();
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
            
            // Update aircraft physics
            UpdateFlightPhysics();
            
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
        
        #region Initialization
        
        private void InitializeState()
        {
            _state = new AircraftState
            {
                Latitude = initialLatitude,
                Longitude = initialLongitude,
                AltitudeMeters = initialAltitudeFeet / 3.28084f,
                Heading = initialHeading,
                Pitch = 0f,
                Roll = 0f,
                IndicatedAirspeedKnots = 200f,
                GroundSpeedKnots = 200f,
                TrueAirspeedKnots = 210f,
                ThrottlePercent = 50f
            };
            
            _targetThrottle = _state.ThrottlePercent / 100f;
            
            // Initialize OwnShipPosition
            UpdateOwnShipPosition();
        }
        
        private void FindDependencies()
        {
            if (geoProjection == null)
            {
                geoProjection = GeoPosUnityPosProjectManager.Instance;
            }
        }
        
        #endregion
        
        #region Input Processing
        
        private void ProcessKeyboardInput()
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
        
        private void SmoothInputs()
        {
            float smoothFactor = inputSmoothing * 60f * Time.deltaTime;
            
            _smoothedPitch = Mathf.Lerp(_smoothedPitch, _targetPitch, smoothFactor);
            _smoothedRoll = Mathf.Lerp(_smoothedRoll, _targetRoll, smoothFactor);
            _smoothedYaw = Mathf.Lerp(_smoothedYaw, _targetYaw, smoothFactor);
            
            // Apply deadzone
            if (Mathf.Abs(_smoothedPitch) < inputDeadzone) _smoothedPitch = 0f;
            if (Mathf.Abs(_smoothedRoll) < inputDeadzone) _smoothedRoll = 0f;
            if (Mathf.Abs(_smoothedYaw) < inputDeadzone) _smoothedYaw = 0f;
            
            // Update state with current inputs
            _state.ElevatorInput = _smoothedPitch;
            _state.AileronInput = _smoothedRoll;
            _state.RudderInput = _smoothedYaw;
            _state.ThrottlePercent = _targetThrottle * 100f;
        }
        
        #endregion
        
        #region Flight Physics
        
        private void UpdateFlightPhysics()
        {
            float dt = Time.deltaTime;
            
            // Update pitch
            float pitchChange = _smoothedPitch * maxPitchRate * dt;
            _state.Pitch = Mathf.Clamp(_state.Pitch + pitchChange, -80f, 80f);
            
            // Auto-level pitch if no input (return to level flight)
            if (autoLevelPitch && Mathf.Abs(_targetPitch) < 0.1f)
            {
                float levelAmount = autoLevelRate * dt;
                _state.Pitch = Mathf.MoveTowards(_state.Pitch, 0f, levelAmount);
            }
            
            // Update roll
            float rollChange = _smoothedRoll * maxRollRate * dt;
            _state.Roll = Mathf.Clamp(_state.Roll + rollChange, -89f, 89f);
            
            // Auto-level roll if no input
            if (autoLevelRoll && Mathf.Abs(_targetRoll) < 0.1f)
            {
                float levelAmount = autoLevelRate * dt;
                _state.Roll = Mathf.MoveTowards(_state.Roll, 0f, levelAmount);
            }
            
            // Turn rate based on bank angle (coordinated turn)
            float bankRadians = _state.Roll * Mathf.Deg2Rad;
            float turnRate = (_state.GroundSpeedKnots > 0) 
                ? (Mathf.Tan(bankRadians) * 1091f / _state.GroundSpeedKnots) 
                : 0f;
            
            // Add yaw input
            turnRate += _smoothedYaw * maxYawRate * dt;
            
            // Update heading
            _state.Heading = Mathf.Repeat(_state.Heading + turnRate * dt, 360f);
            
            // Update speed based on throttle
            float targetSpeed = Mathf.Lerp(minAirspeedKnots, maxAirspeedKnots, _targetThrottle);
            _state.IndicatedAirspeedKnots = Mathf.MoveTowards(
                _state.IndicatedAirspeedKnots, 
                targetSpeed, 
                speedChangeRate * dt
            );
            _state.GroundSpeedKnots = _state.IndicatedAirspeedKnots * 0.98f; // Simplified
            _state.TrueAirspeedKnots = _state.IndicatedAirspeedKnots * 1.02f; // Simplified
            
            // Calculate pitch in radians for proper velocity distribution
            float pitchRad = _state.Pitch * Mathf.Deg2Rad;
            
            // Vertical speed is the vertical component of airspeed based on pitch
            // Using TAS (in knots) converted to fpm: knots * 101.269 = fpm
            _state.VerticalSpeedFpm = _state.TrueAirspeedKnots * Mathf.Sin(pitchRad) * 101.269f;
            
            // Update altitude
            float altitudeChangeMeters = _state.VerticalSpeedMps * dt;
            _state.AltitudeMeters = Mathf.Max(0f, _state.AltitudeMeters + altitudeChangeMeters);
            
            // Ground speed is the horizontal component of airspeed
            // This is what we use for geographic position updates
            _state.GroundSpeedKnots = _state.TrueAirspeedKnots * Mathf.Cos(pitchRad);
            
            // Update position based on heading and ground speed
            UpdateGeographicPosition(dt);
            
            // Update OwnShipPosition struct
            UpdateOwnShipPosition();
        }
        
        private void UpdateGeographicPosition(float dt)
        {
            // Convert heading to radians
            float headingRad = _state.Heading * Mathf.Deg2Rad;
            
            // Speed in meters per second
            float speedMps = _state.GroundSpeedMps;
            
            // Distance traveled in this frame (meters)
            float distanceMeters = speedMps * dt;
            
            // Earth radius in meters
            const double EarthRadius = 6371000.0;
            
            // Calculate position change
            double latRad = _state.Latitude * Mathf.Deg2Rad;
            
            // North/South movement (latitude)
            double dLat = (distanceMeters * Mathf.Cos(headingRad)) / EarthRadius;
            
            // East/West movement (longitude, corrected for latitude)
            double dLon = (distanceMeters * Mathf.Sin(headingRad)) / (EarthRadius * Math.Cos(latRad));
            
            // Update coordinates
            _state.Latitude += dLat * Mathf.Rad2Deg;
            _state.Longitude += dLon * Mathf.Rad2Deg;
            
            // Clamp latitude
            _state.Latitude = Math.Max(-90.0, Math.Min(90.0, _state.Latitude));
            
            // Wrap longitude
            if (_state.Longitude > 180.0) _state.Longitude -= 360.0;
            if (_state.Longitude < -180.0) _state.Longitude += 360.0;
        }
        
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
        }
        
        public void SetPitch(float value)
        {
            _targetPitch = Mathf.Clamp(value, -1f, 1f);
        }
        
        public void SetRoll(float value)
        {
            _targetRoll = Mathf.Clamp(value, -1f, 1f);
        }
        
        public void SetYaw(float value)
        {
            _targetYaw = Mathf.Clamp(value, -1f, 1f);
        }
        
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
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("=== Aircraft Controller ===");
            GUILayout.Label($"Position: {_state.Latitude:F4}, {_state.Longitude:F4}");
            GUILayout.Label($"Altitude: {_state.AltitudeFeet:F0} ft");
            GUILayout.Label($"Heading: {_state.Heading:F1}°");
            GUILayout.Label($"Pitch: {_state.Pitch:F1}° | Roll: {_state.Roll:F1}°");
            GUILayout.Label($"Airspeed: {_state.IndicatedAirspeedKnots:F0} kts");
            GUILayout.Label($"VS: {_state.VerticalSpeedFpm:F0} fpm");
            GUILayout.Label($"Throttle: {_state.ThrottlePercent:F0}%");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
