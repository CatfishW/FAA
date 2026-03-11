using System;
using UnityEngine;
using AircraftControl.Core;
using AviationUI;
using FAA.XPlaneIntegration.Core;

namespace FAA.XPlaneIntegration.Providers
{
    /// <summary>
    /// Adapter that connects X-Plane UDP telemetry data to Unity's AircraftController system.
    /// Subscribes to XPlaneUdpListener events, maps data via XPlaneDataRefMapper, and injects
    /// into AircraftController without modifying core systems (adapter pattern).
    /// 
    /// Setup:
    /// 1. Add this component to a GameObject in your scene
    /// 2. Assign AircraftController reference (or leave null for auto-discovery)
    /// 3. Configure DataRef paths and update settings in Inspector
    /// 4. Enable X-Plane connection to start receiving telemetry
    /// 
    /// Notes:
    /// - Event-driven architecture (no main thread blocking)
    /// - Uses XPlaneUdpListener for UDP communication
    /// - Uses XPlaneDataRefMapper for unit conversions and data mapping
    /// - Follows same bridge pattern as OwnAircraftRadarBridge.cs
    /// </summary>
    [AddComponentMenu("X-Plane Integration/X-Plane Aircraft Provider")]
    public class XPlaneAircraftProvider : MonoBehaviour
    {
        #region Inspector Settings

        [Header("X-Plane Connection")]
        [Tooltip("Enable/disable X-Plane input processing")]
        [SerializeField] private bool enableXPlaneInput = true;

        [Tooltip("X-Plane IP address (leave empty for default 127.0.0.1)")]
        [SerializeField] private string xPlaneIpAddress = "127.0.0.1";

        [Tooltip("UDP port for X-Plane data (default: 49009)")]
        [SerializeField] private int udpPort = 49009;

        [Tooltip("Auto-connect on Start()")]
        [SerializeField] private bool autoConnectOnStart = true;

        [Header("Aircraft Target")]
        [Tooltip("AircraftController to update. If null, will try to find one.")]
        [SerializeField] private AircraftController aircraftController;

        [Tooltip("AviationFlightDataProvider to keep the Aviation UI in sync with X-Plane ownship data.")]
        [SerializeField] private AviationFlightDataProvider flightDataProvider;

        [Header("DataRef Configuration")]
        [Tooltip("Request pitch data from X-Plane")]
        [SerializeField] private bool requestPitch = true;

        [Tooltip("Request roll data from X-Plane")]
        [SerializeField] private bool requestRoll = true;

        [Tooltip("Request heading data from X-Plane")]
        [SerializeField] private bool requestHeading = true;

        [Tooltip("Request airspeed data from X-Plane")]
        [SerializeField] private bool requestAirspeed = true;

        [Tooltip("Request position data from X-Plane")]
        [SerializeField] private bool requestPosition = true;

        [Tooltip("Request altitude data from X-Plane")]
        [SerializeField] private bool requestAltitude = true;

        [Tooltip("Request vertical speed from X-Plane")]
        [SerializeField] private bool requestVerticalSpeed = true;

        [Tooltip("Request wind data from X-Plane")]
        [SerializeField] private bool requestWind = true;

        [Tooltip("DataRef update frequency in Hz (0 = use X-Plane default)")]
        [Range(0, 50)]
        [SerializeField] private int dataRefFrequency = 10;

        [Header("Update Settings")]
        [Tooltip("Minimum interval between position updates to AircraftController (seconds)")]
        [Range(0.01f, 1f)]
        [SerializeField] private float positionUpdateInterval = 0.1f;

        [Tooltip("Minimum position change to trigger update (meters)")]
        [SerializeField] private float positionChangeThreshold = 5f;

        [Tooltip("Smooth input values over time")]
        [SerializeField] private bool smoothInputs = true;

        [Tooltip("Smoothing factor (lower = smoother, higher = more responsive)")]
        [Range(0.01f, 1f)]
        [SerializeField] private float inputSmoothingFactor = 0.2f;

        [Header("Fallback Settings")]
        [Tooltip("Keep last known position if connection lost")]
        [SerializeField] private bool keepLastPositionOnDisconnect = true;

        [Tooltip("Disable user control when X-Plane data is active")]
        [SerializeField] private bool disableUserControlWhenActive = true;

        [Header("Debug")]
        [Tooltip("Show debug information in console")]
        [SerializeField] private bool showDebugInfo = false;

        [Tooltip("Log every data update (verbose)")]
        [SerializeField] private bool verboseLogging = false;

        #endregion

        #region Public State Properties

        /// <summary>
        /// Whether currently connected to X-Plane
        /// </summary>
        public bool IsConnected => _udpListener != null && _udpListener.IsConnected;

        /// <summary>
        /// Time of last successful data update
        /// </summary>
        public float LastUpdateTime => _lastUpdateTime;

        /// <summary>
        /// Current error state description (empty if no error)
        /// </summary>
        public string ErrorState => _errorState;

        /// <summary>
        /// Whether X-Plane input is enabled
        /// </summary>
        public bool IsEnabled => enableXPlaneInput;

        /// <summary>
        /// Last received flight data from X-Plane
        /// </summary>
        public AviationFlightData LastFlightData => _lastFlightData;

        #endregion

        #region Private Fields

        private XPlaneUdpListener _udpListener;
        private bool _ownsUdpListener;
        private AviationFlightData _lastFlightData;
        private AviationFlightData _smoothedFlightData;
        private float _lastUpdateTime;
        private float _lastPositionUpdateTime;
        private double _lastLatitude;
        private double _lastLongitude;
        private float _lastAltitude;
        private string _errorState = string.Empty;
        private bool _isInitialized;
        private bool _hasReceivedData;
        private System.Collections.Generic.Dictionary<string, float> _lastRawDataRefs;

        // Smoothed values for gradual transitions
        private float _smoothedPitch;
        private float _smoothedRoll;
        private float _smoothedHeading;
        private float _smoothedAirspeed;
        private float _smoothedAltitude;
        private float _smoothedVerticalSpeed;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeUdpListener();
            FindAircraftController();
            FindFlightDataProvider();
            _isInitialized = true;
        }

        private void Start()
        {
            if (autoConnectOnStart && enableXPlaneInput)
            {
                ConnectToXPlane();
            }

            if (_smoothedFlightData == null)
            {
                _smoothedFlightData = new AviationFlightData();
            }
        }

        private void Update()
        {
            if (!enableXPlaneInput || !_isInitialized)
            {
                return;
            }

            if (_udpListener != null && IsConnected)
            {
                _udpListener.ProcessQueuedData();
            }

            if (_hasReceivedData && Time.time - _lastUpdateTime > 5f)
            {
                HandleConnectionTimeout();
            }
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            CancelInvoke();
            UnsubscribeFromEvents();
            if (enableXPlaneInput)
            {
                DisconnectFromXPlane();
            }
        }

        private void OnDestroy()
        {
            CancelInvoke();
            Cleanup();
        }

        #endregion

        #region Initialization

        private void InitializeUdpListener()
        {
            if (_udpListener != null)
            {
                return;
            }

            _udpListener = new XPlaneUdpListener(xPlaneIpAddress, udpPort);
            _ownsUdpListener = true;
        }

        private void FindAircraftController()
        {
            if (aircraftController == null)
            {
                aircraftController = FindFirstObjectByType<AircraftController>();
            }

            if (aircraftController == null)
            {
                Debug.LogWarning("[XPlaneAircraftProvider] No AircraftController found! Data will not be applied until one is assigned.");
            }
            else
            {
                LogDebug($"Found AircraftController: {aircraftController.name}");
            }
        }

        private void FindFlightDataProvider()
        {
            if (flightDataProvider == null)
            {
                flightDataProvider = FindFirstObjectByType<AviationFlightDataProvider>();
            }

            if (flightDataProvider == null)
            {
                Debug.LogWarning("[XPlaneAircraftProvider] No AviationFlightDataProvider found. Aviation UI will not receive ownship updates from X-Plane.");
            }
        }

        private void SubscribeToEvents()
        {
            if (_udpListener != null)
            {
                _udpListener.OnDataReceived += OnDataReceived;
                _udpListener.OnConnectionStateChanged += OnConnectionStateChanged;
                _udpListener.OnError += OnError;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_udpListener != null)
            {
                _udpListener.OnDataReceived -= OnDataReceived;
                _udpListener.OnConnectionStateChanged -= OnConnectionStateChanged;
                _udpListener.OnError -= OnError;
            }
        }

        #endregion

        #region Connection Management

        /// <summary>
        /// Connect to X-Plane and start receiving data
        /// </summary>
        public void ConnectToXPlane()
        {
            if (_udpListener == null)
            {
                InitializeUdpListener();
            }

            _errorState = string.Empty;

            if (_ownsUdpListener)
            {
                _udpListener.Connect(xPlaneIpAddress);
            }

            if (IsConnected)
            {
                RequestDataRefs();
            }
        }

        /// <summary>
        /// Disconnect from X-Plane
        /// </summary>
        public void DisconnectFromXPlane()
        {
            if (_udpListener != null && _ownsUdpListener)
            {
                _udpListener.Disconnect();
                LogDebug("Disconnected from X-Plane");
            }
        }

        /// <summary>
        /// Reconnect to X-Plane (disconnect then connect)
        /// </summary>
        public void ReconnectToXPlane()
        {
            DisconnectFromXPlane();
            Invoke(nameof(ConnectToXPlane), 0.5f);
        }

        private void RequestDataRefs()
        {
            if (!IsConnected)
            {
                return;
            }

            int frequency = dataRefFrequency > 0 ? dataRefFrequency : 10;

            if (requestPitch)
            {
                _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_Pitch, frequency);
            }
            if (requestRoll)
            {
                _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_Roll, frequency);
            }
            if (requestHeading)
            {
                _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_Heading, frequency);
            }

            if (requestAirspeed)
            {
                _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_IAS, frequency);
                _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_TAS, frequency);
                _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_GS, frequency);
            }

            if (requestPosition)
            {
                _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_Latitude, frequency);
                _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_Longitude, frequency);
            }

            if (requestAltitude)
            {
                _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_Elevation, frequency);
                _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_AGL, frequency);
            }

            if (requestVerticalSpeed)
            {
                _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_VerticalSpeed, frequency);
            }

            _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_AutopilotMode, frequency);
            _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_GearHandleDown, frequency);
            _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_GearDeployRatio, frequency);
            _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_FlapsRatio, frequency);
            _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_SpeedbrakeRatio, frequency);
            _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_ParkingBrakeRatio, frequency);
            _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_LeftBrakeRatio, frequency);
            _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_RightBrakeRatio, frequency);
            _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_ElevatorTrim, frequency);
            _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_AileronTrim, frequency);
            _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_RudderTrim, frequency);

            if (requestWind)
            {
                _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_WindSpeed, frequency);
                _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_WindDirection, frequency);
                _udpListener.SendRrefRequest(XPlaneDataRefMapper.DataRef_Pressure, frequency);
            }

            LogDebug($"Requested DataRefs @ {frequency}Hz");
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Called when UDP data is received from X-Plane
        /// </summary>
        /// <param name="dataRefs">Dictionary of DataRef paths to values</param>
        private void OnDataReceived(System.Collections.Generic.Dictionary<string, float> dataRefs)
        {
            if (dataRefs == null || dataRefs.Count == 0)
            {
                return;
            }

            // Map X-Plane data to AviationFlightData
            _lastFlightData = XPlaneDataRefMapper.Map(dataRefs);
            _lastRawDataRefs = new System.Collections.Generic.Dictionary<string, float>(dataRefs);
            _lastUpdateTime = Time.time;
            _hasReceivedData = true;
            _errorState = string.Empty;

            // Apply smoothing if enabled
            if (smoothInputs)
            {
                ApplySmoothing();
            }
            else
            {
                _smoothedFlightData = _lastFlightData.Clone();
            }

            // Inject data into AircraftController
            InjectDataIntoAircraftController();

            if (verboseLogging)
            {
                LogDebug($"Data received: Pitch={_smoothedFlightData.pitch:F1}, Roll={_smoothedFlightData.roll:F1}, " +
                        $"Hdg={_smoothedFlightData.heading:F0}, IAS={_smoothedFlightData.indicatedAirspeed:F0}kts, " +
                        $"Alt={_smoothedFlightData.altitudeMSL:F0}ft");
            }
        }

        /// <summary>
        /// Called when connection state changes
        /// </summary>
        private void OnConnectionStateChanged(XPlaneUdpListener.ConnectionState state)
        {
            LogDebug($"Connection state changed: {state}");

            switch (state)
            {
                case XPlaneUdpListener.ConnectionState.Connected:
                    _errorState = string.Empty;
                    RequestDataRefs();
                    break;

                case XPlaneUdpListener.ConnectionState.Error:
                    _errorState = "Connection error - check X-Plane is running and UDP port is correct";
                    Debug.LogError($"[XPlaneAircraftProvider] Connection error. {_errorState}");
                    break;

                case XPlaneUdpListener.ConnectionState.Disconnected:
                    if (keepLastPositionOnDisconnect)
                    {
                        LogDebug("Disconnected - keeping last known position");
                    }
                    break;
            }
        }

        /// <summary>
        /// Called when an error occurs
        /// </summary>
        private void OnError(string errorMessage)
        {
            _errorState = errorMessage;
            Debug.LogError($"[XPlaneAircraftProvider] Error: {errorMessage}");
        }

        private void HandleConnectionTimeout()
        {
            if (!string.IsNullOrEmpty(_errorState))
            {
                return; // Already have an error state
            }

            _errorState = "Connection timeout - no data received from X-Plane";
            Debug.LogWarning($"[XPlaneAircraftProvider] {_errorState}");
        }

        #endregion

        #region Data Processing

        private void ApplySmoothing()
        {
            if (_lastFlightData == null || _smoothedFlightData == null)
            {
                return;
            }

            float t = inputSmoothingFactor;

            _smoothedPitch = Mathf.Lerp(_smoothedPitch, _lastFlightData.pitch, t);
            _smoothedRoll = Mathf.Lerp(_smoothedRoll, _lastFlightData.roll, t);
            _smoothedHeading = Mathf.LerpAngle(_smoothedHeading, _lastFlightData.heading, t);
            _smoothedAirspeed = Mathf.Lerp(_smoothedAirspeed, _lastFlightData.indicatedAirspeed, t);
            _smoothedAltitude = Mathf.Lerp(_smoothedAltitude, _lastFlightData.altitudeMSL, t);
            _smoothedVerticalSpeed = Mathf.Lerp(_smoothedVerticalSpeed, _lastFlightData.verticalSpeed, t);

            _smoothedFlightData.pitch = _smoothedPitch;
            _smoothedFlightData.roll = _smoothedRoll;
            _smoothedFlightData.heading = _smoothedHeading;
            _smoothedFlightData.indicatedAirspeed = _smoothedAirspeed;
            _smoothedFlightData.altitudeMSL = _smoothedAltitude;
            _smoothedFlightData.verticalSpeed = _smoothedVerticalSpeed;

            _smoothedFlightData.groundSpeed = _lastFlightData.groundSpeed;
            _smoothedFlightData.trueAirspeed = _lastFlightData.trueAirspeed;
            _smoothedFlightData.altitudeAGL = _lastFlightData.altitudeAGL;
            _smoothedFlightData.barometricSetting = _lastFlightData.barometricSetting;
            _smoothedFlightData.windDirection = _lastFlightData.windDirection;
            _smoothedFlightData.windSpeed = _lastFlightData.windSpeed;
            _smoothedFlightData.gpsValid = _lastFlightData.gpsValid;
            _smoothedFlightData.ilsValid = _lastFlightData.ilsValid;
            _smoothedFlightData.autopilotEngaged = _lastFlightData.autopilotEngaged;
        }

        #endregion

        #region Aircraft Controller Injection

        private void InjectDataIntoAircraftController()
        {
            if (aircraftController == null || _smoothedFlightData == null)
            {
                return;
            }

            bool shouldUpdatePosition = ShouldUpdatePosition();

            if (shouldUpdatePosition)
            {
                UpdateAircraftPosition();
            }

            aircraftController.State.Pitch = _smoothedFlightData.pitch;
            aircraftController.State.Roll = _smoothedFlightData.roll;
            aircraftController.State.Heading = _smoothedFlightData.heading;
            aircraftController.State.IndicatedAirspeedKnots = _smoothedFlightData.indicatedAirspeed;
            aircraftController.State.VerticalSpeedFpm = _smoothedFlightData.verticalSpeed;
            aircraftController.State.GroundSpeedKnots = _smoothedFlightData.groundSpeed;
            aircraftController.State.TrueAirspeedKnots = _smoothedFlightData.trueAirspeed;

            var sourceDataRefs = _lastRawDataRefs;
            aircraftController.State.AutopilotMode = Mathf.RoundToInt(
                XPlaneDataRefMapper.GetDataRef(sourceDataRefs, XPlaneDataRefMapper.DataRef_AutopilotMode, 0f));
            aircraftController.State.AutopilotEngaged = aircraftController.State.AutopilotMode >= 2;

            float gearDeployRatio = XPlaneDataRefMapper.GetDataRef(
                sourceDataRefs,
                XPlaneDataRefMapper.DataRef_GearDeployRatio,
                -1f);
            if (gearDeployRatio >= 0f)
            {
                aircraftController.State.GearDown = XPlaneDataRefMapper.ClampRatio01(gearDeployRatio) > 0.5f;
            }
            else
            {
                aircraftController.State.GearDown =
                    XPlaneDataRefMapper.GetDataRef(sourceDataRefs, XPlaneDataRefMapper.DataRef_GearHandleDown, 1f) > 0.5f;
            }

            aircraftController.State.FlapsRatio = XPlaneDataRefMapper.ClampRatio01(
                XPlaneDataRefMapper.GetDataRef(sourceDataRefs, XPlaneDataRefMapper.DataRef_FlapsRatio, aircraftController.State.FlapsRatio));
            aircraftController.State.SpeedbrakeRatio = XPlaneDataRefMapper.ClampRatio01(
                XPlaneDataRefMapper.GetDataRef(sourceDataRefs, XPlaneDataRefMapper.DataRef_SpeedbrakeRatio, aircraftController.State.SpeedbrakeRatio));
            aircraftController.State.ParkingBrakeRatio = XPlaneDataRefMapper.ClampRatio01(
                XPlaneDataRefMapper.GetDataRef(sourceDataRefs, XPlaneDataRefMapper.DataRef_ParkingBrakeRatio, aircraftController.State.ParkingBrakeRatio));
            aircraftController.State.LeftBrakeRatio = XPlaneDataRefMapper.ClampRatio01(
                XPlaneDataRefMapper.GetDataRef(sourceDataRefs, XPlaneDataRefMapper.DataRef_LeftBrakeRatio, aircraftController.State.LeftBrakeRatio));
            aircraftController.State.RightBrakeRatio = XPlaneDataRefMapper.ClampRatio01(
                XPlaneDataRefMapper.GetDataRef(sourceDataRefs, XPlaneDataRefMapper.DataRef_RightBrakeRatio, aircraftController.State.RightBrakeRatio));
            aircraftController.State.ElevatorTrim = XPlaneDataRefMapper.ClampTrim(
                XPlaneDataRefMapper.GetDataRef(sourceDataRefs, XPlaneDataRefMapper.DataRef_ElevatorTrim, aircraftController.State.ElevatorTrim));
            aircraftController.State.AileronTrim = XPlaneDataRefMapper.ClampTrim(
                XPlaneDataRefMapper.GetDataRef(sourceDataRefs, XPlaneDataRefMapper.DataRef_AileronTrim, aircraftController.State.AileronTrim));
            aircraftController.State.RudderTrim = XPlaneDataRefMapper.ClampTrim(
                XPlaneDataRefMapper.GetDataRef(sourceDataRefs, XPlaneDataRefMapper.DataRef_RudderTrim, aircraftController.State.RudderTrim));

            if (disableUserControlWhenActive && _hasReceivedData)
            {
                if (aircraftController.IsUserControlled)
                {
                    aircraftController.SetUserControlled(false);
                }
            }

            PublishFlightDataToHud();
        }

        private bool ShouldUpdatePosition()
        {
            if (_lastPositionUpdateTime == 0f || _smoothedFlightData == null)
            {
                return true;
            }

            float altitudeChange = Mathf.Abs(_smoothedFlightData.altitudeMSL - _lastAltitude);
            bool significantChange = altitudeChange >= positionChangeThreshold;
            bool timeElapsed = Time.time - _lastPositionUpdateTime >= positionUpdateInterval;

            return timeElapsed || significantChange;
        }

        private void UpdateAircraftPosition()
        {
            if (_lastFlightData == null || _smoothedFlightData == null || aircraftController == null)
            {
                return;
            }

            var state = aircraftController.State;
            double latitude = XPlaneDataRefMapper.GetDataRef(_lastRawDataRefs, XPlaneDataRefMapper.DataRef_Latitude, (float)state.Latitude);
            double longitude = XPlaneDataRefMapper.GetDataRef(_lastRawDataRefs, XPlaneDataRefMapper.DataRef_Longitude, (float)state.Longitude);
            float altitudeMeters = _smoothedFlightData.altitudeMSL / 3.28084f;
            float heading = _smoothedFlightData.heading;

            aircraftController.SetPosition(latitude, longitude, altitudeMeters, heading);

            _lastLatitude = latitude;
            _lastLongitude = longitude;
            _lastAltitude = _smoothedFlightData.altitudeMSL;
            _lastPositionUpdateTime = Time.time;

            LogDebug($"Position updated: Lat={latitude:F6}, Lon={longitude:F6}, Alt={_smoothedFlightData.altitudeMSL:F0}ft, Hdg={heading:F0}°");
        }

        private void PublishFlightDataToHud()
        {
            if (flightDataProvider == null || aircraftController == null)
            {
                return;
            }

            var state = aircraftController.State;
            var existingData = flightDataProvider.FlightData;
            var hudFlightData = existingData != null ? existingData.Clone() : new AviationFlightData();

            hudFlightData.pitch = state.Pitch;
            hudFlightData.roll = state.Roll;
            hudFlightData.heading = state.Heading;
            hudFlightData.indicatedAirspeed = state.IndicatedAirspeedKnots;
            hudFlightData.trueAirspeed = state.TrueAirspeedKnots;
            hudFlightData.groundSpeed = state.GroundSpeedKnots;
            hudFlightData.altitudeMSL = state.AltitudeFeet;
            hudFlightData.altitudeAGL = _smoothedFlightData != null ? _smoothedFlightData.altitudeAGL : hudFlightData.altitudeAGL;
            hudFlightData.verticalSpeed = state.VerticalSpeedFpm;
            hudFlightData.autopilotEngaged = state.AutopilotEngaged;

            flightDataProvider.UpdateFlightData(hudFlightData);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the AircraftController reference at runtime
        /// </summary>
        public void SetAircraftController(AircraftController controller)
        {
            aircraftController = controller;
            if (controller != null)
            {
                LogDebug($"AircraftController set: {controller.name}");
            }
        }

        public void SetFlightDataProvider(AviationFlightDataProvider provider)
        {
            flightDataProvider = provider;
        }

        public void SetUdpListener(XPlaneUdpListener listener)
        {
            if (ReferenceEquals(_udpListener, listener))
            {
                return;
            }

            UnsubscribeFromEvents();

            if (_udpListener != null && _ownsUdpListener)
            {
                _udpListener.Dispose();
            }

            _udpListener = listener;
            _ownsUdpListener = false;

            SubscribeToEvents();

            if (_udpListener != null && _udpListener.IsConnected && enableXPlaneInput)
            {
                RequestDataRefs();
            }
        }

        /// <summary>
        /// Enable or disable X-Plane input processing
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            enableXPlaneInput = enabled;

            if (!enabled)
            {
                DisconnectFromXPlane();
            }
            else if (_isInitialized)
            {
                ConnectToXPlane();
            }
        }

        /// <summary>
        /// Force an immediate position update to AircraftController
        /// </summary>
        public void ForcePositionUpdate()
        {
            _lastPositionUpdateTime = 0f;
            InjectDataIntoAircraftController();
        }

        /// <summary>
        /// Clear the last known position (useful when re-centering)
        /// </summary>
        public void ClearLastPosition()
        {
            _lastLatitude = 0;
            _lastLongitude = 0;
            _lastAltitude = 0;
            _lastPositionUpdateTime = 0;
        }

        #endregion

        #region Cleanup

        private void Cleanup()
        {
            UnsubscribeFromEvents();

            if (_udpListener != null && _ownsUdpListener)
            {
                _udpListener.Dispose();
            }

            _udpListener = null;

            LogDebug("XPlaneAircraftProvider cleaned up");
        }

        #endregion

        #region Debug

        [System.Diagnostics.Conditional("DEBUG")]
        private void LogDebug(string message)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[XPlaneAircraftProvider] {message}");
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Connect to X-Plane")]
        private void EditorConnect()
        {
            ConnectToXPlane();
        }

        [ContextMenu("Disconnect from X-Plane")]
        private void EditorDisconnect()
        {
            DisconnectFromXPlane();
        }

        [ContextMenu("Force Position Update")]
        private void EditorForceUpdate()
        {
            ForcePositionUpdate();
        }

        [ContextMenu("Log Status")]
        private void EditorLogStatus()
        {
            Debug.Log("=== XPlaneAircraftProvider Status ===");
            Debug.Log($"Enabled: {enableXPlaneInput}");
            Debug.Log($"Connected: {IsConnected}");
            Debug.Log($"AircraftController: {(aircraftController != null ? aircraftController.name : "None")}");
            Debug.Log($"Last Update: {LastUpdateTime:F2}s ago");
            Debug.Log($"Error State: {(string.IsNullOrEmpty(ErrorState) ? "None" : ErrorState)}");

            if (_lastFlightData != null)
            {
                Debug.Log($"Last Pitch: {_lastFlightData.pitch:F1}°");
                Debug.Log($"Last Roll: {_lastFlightData.roll:F1}°");
                Debug.Log($"Last Heading: {_lastFlightData.heading:F0}°");
                Debug.Log($"Last Altitude: {_lastFlightData.altitudeMSL:F0}ft");
                Debug.Log($"Last Airspeed: {_lastFlightData.indicatedAirspeed:F0}kts");
            }
        }
#endif

        #endregion
    }
}
