using System;
using UnityEngine;
using TrafficRadar.Core;
using TrafficRadar;
using AircraftControl.Core;
using FAA.XPlaneIntegration.Providers;
using XPlaneTrafficState = TrafficRadar.Core.AircraftState;

namespace FAA.XPlaneIntegration.Bridges
{
    /// <summary>
    /// Bridges X-Plane traffic data from XPlaneTrafficProvider to the TrafficRadar system.
    /// 
    /// This component coordinates between:
    /// - XPlaneTrafficProvider: Reads traffic from X-Plane multiplayer slots via DataRefs
    /// - TrafficRadarDataManager: Manages traffic data and fires onDataUpdated events
    /// - AircraftController: Provides own-ship position for radar reference
    /// - TrafficRadarController: Processes AircraftState list into RadarTarget list
    /// 
    /// Responsibilities:
    /// 1. Find and configure XPlaneTrafficProvider and TrafficRadarDataManager in scene
    /// 2. Sync own-ship position from AircraftController to TrafficRadarDataManager
    /// 3. Route X-Plane traffic data into TrafficRadar system
    /// 4. Handle coordination between X-Plane traffic and API-based traffic
    /// 
    /// Setup:
    /// 1. Add this component to a GameObject in your scene (e.g., "XPlaneIntegration")
    /// 2. Ensure XPlaneTrafficProvider, TrafficRadarDataManager, and AircraftController exist
    /// 3. Configure references in Inspector or let auto-find locate them
    /// 4. Enable "Sync Own-Ship Position" to update radar position from player aircraft
    /// 5. Enable "Use X-Plane Traffic Only" to disable API traffic when using X-Plane
    /// </summary>
    [AddComponentMenu("X-Plane Integration/Bridges/XPlane to Traffic Radar Bridge")]
    public class XPlaneToTrafficRadarBridge : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Component References")]
        [Tooltip("XPlaneTrafficProvider instance (auto-finds if not assigned)")]
        [SerializeField] private XPlaneTrafficProvider trafficProvider;

        [Tooltip("TrafficRadarDataManager instance (auto-finds if not assigned)")]
        [SerializeField] private TrafficRadarDataManager dataManager;

        [Tooltip("AircraftController for own-ship position (auto-finds if not assigned)")]
        [SerializeField] private AircraftController aircraftController;

        [Tooltip("TrafficRadarController instance (optional, for direct control)")]
        [SerializeField] private TrafficRadarController radarController;

        [Header("Position Sync Settings")]
        [Tooltip("Enable automatic own-ship position sync from AircraftController")]
        [SerializeField] private bool syncOwnShipPosition = true;

        [Tooltip("Minimum distance change (meters) to trigger position update")]
        [Range(0.1f, 100f)]
        [SerializeField] private float positionUpdateThreshold = 10f;

        [Tooltip("Minimum time between position updates (seconds)")]
        [Range(0.1f, 5f)]
        [SerializeField] private float positionUpdateInterval = 0.5f;

        [Header("Traffic Coordination")]
        [Tooltip("Disable API-based traffic when X-Plane traffic is available")]
        [SerializeField] private bool disableApiTrafficWhenXPlaneAvailable = true;

        [Tooltip("X-Plane traffic takes priority over API traffic")]
        [SerializeField] private bool prioritizeXPlaneTraffic = true;

        [Header("Geographic Filter")]
        [Tooltip("Automatically update radar range based on X-Plane traffic density")]
        [SerializeField] private bool autoUpdateRadarRange = true;

        [Tooltip("Base range in kilometers for traffic filtering")]
        [Range(10f, 500f)]
        [SerializeField] private float baseRangeKm = 100f;

        [Header("Debug")]
        [Tooltip("Enable verbose logging")]
        [SerializeField] private bool verboseLogging = false;

        #endregion

        #region Private Fields

        /// <summary>
        /// Last own-ship position for change detection
        /// </summary>
        private OwnShipPosition _lastOwnPosition;

        /// <summary>
        /// Last position update time for throttling
        /// </summary>
        private float _lastPositionUpdateTime;

        /// <summary>
        /// Whether X-Plane traffic provider is available and active
        /// </summary>
        private bool _isXPlaneTrafficAvailable;

        /// <summary>
        /// Cached AircraftState array for current frame
        /// </summary>
        private XPlaneTrafficState[] _cachedXPlaneTraffic;

        /// <summary>
        /// Internal reference to position provider interface
        /// </summary>
        private IOwnShipPositionProvider _ownShipPositionProvider;

        /// <summary>
        /// Event subscription flag to prevent duplicate subscriptions
        /// </summary>
        private bool _isSubscribed;

        #endregion

        #region Properties

        /// <summary>
        /// Whether the bridge is actively syncing traffic
        /// </summary>
        public bool IsBridgeActive { get; private set; }

        /// <summary>
        /// Number of X-Plane traffic targets currently tracked
        /// </summary>
        public int XPlaneTrafficCount => _cachedXPlaneTraffic?.Length ?? 0;

        /// <summary>
        /// Whether own-ship position sync is enabled
        /// </summary>
        public bool IsPositionSyncEnabled => syncOwnShipPosition;

        /// <summary>
        /// Current own-ship position
        /// </summary>
        public OwnShipPosition CurrentOwnPosition => _lastOwnPosition;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            FindComponents();
            InitializeBridge();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
            StartBridge();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
            StopBridge();
        }

        private void Update()
        {
            if (!IsBridgeActive)
                return;

            if (syncOwnShipPosition)
            {
                SyncOwnShipPosition();
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Finds all required components via inspector or auto-discovery
        /// </summary>
        private void FindComponents()
        {
            if (trafficProvider == null)
            {
                trafficProvider = FindFirstObjectByType<XPlaneTrafficProvider>();
                if (trafficProvider != null)
                {
                    Log("Auto-found XPlaneTrafficProvider");
                }
            }

            if (dataManager == null)
            {
                dataManager = FindFirstObjectByType<TrafficRadarDataManager>();
                if (dataManager != null)
                {
                    Log("Auto-found TrafficRadarDataManager");
                }
            }

            if (aircraftController == null)
            {
                aircraftController = FindFirstObjectByType<AircraftController>();
                if (aircraftController != null)
                {
                    Log("Auto-found AircraftController");
                }
            }

            if (radarController == null)
            {
                radarController = FindFirstObjectByType<TrafficRadarController>();
                if (radarController != null)
                {
                    Log("Auto-found TrafficRadarController");
                }
            }

            if (aircraftController != null)
            {
                _ownShipPositionProvider = aircraftController as IOwnShipPositionProvider;
            }
        }

        /// <summary>
        /// Initializes bridge configuration
        /// </summary>
        private void InitializeBridge()
        {
            if (dataManager == null)
            {
                Debug.LogError("[XPlaneToTrafficRadarBridge] TrafficRadarDataManager not found - bridge cannot function");
                enabled = false;
                return;
            }

            _lastOwnPosition = OwnShipPosition.Default;
            _lastPositionUpdateTime = 0f;

            Log("Bridge initialized");
        }

        #endregion

        #region Bridge Lifecycle

        /// <summary>
        /// Starts the bridge and enables traffic syncing
        /// </summary>
        private void StartBridge()
        {
            if (dataManager == null)
            {
                Debug.LogWarning("[XPlaneToTrafficRadarBridge] Cannot start - DataManager missing");
                return;
            }

            ConfigureDataManager();

            if (disableApiTrafficWhenXPlaneAvailable && trafficProvider != null)
            {
                dataManager.StopFetching();
                Log("Disabled API traffic fetching (X-Plane traffic enabled)");
            }

            IsBridgeActive = true;
            Log("Bridge started");
        }

        /// <summary>
        /// Stops the bridge and disables traffic syncing
        /// </summary>
        private void StopBridge()
        {
            IsBridgeActive = false;

            if (disableApiTrafficWhenXPlaneAvailable && dataManager != null)
            {
                dataManager.StartFetching();
                Log("Re-enabled API traffic fetching");
            }

            Log("Bridge stopped");
        }

        /// <summary>
        /// Configures TrafficRadarDataManager to accept X-Plane traffic data
        /// </summary>
        private void ConfigureDataManager()
        {
            if (dataManager == null) return;

            dataManager.SetGeographicFilter(
                (float)_lastOwnPosition.Latitude,
                (float)_lastOwnPosition.Longitude,
                baseRangeKm
            );

            Log($"Configured DataManager with range {baseRangeKm}km");
        }

        #endregion

        #region Event Subscription

        /// <summary>
        /// Subscribes to relevant events from connected components
        /// </summary>
        private void SubscribeToEvents()
        {
            if (_isSubscribed) return;

            if (trafficProvider != null)
            {
                trafficProvider.OnTrafficDataReceived += OnXPlaneTrafficReceived;
                Log("Subscribed to XPlaneTrafficProvider events");
            }

            if (_ownShipPositionProvider != null && syncOwnShipPosition)
            {
                _ownShipPositionProvider.OnPositionChanged += OnOwnShipPositionChanged;
                Log("Subscribed to own-ship position updates");
            }

            _isSubscribed = true;
        }

        /// <summary>
        /// Unsubscribes from all events
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (!_isSubscribed) return;

            if (trafficProvider != null)
            {
                trafficProvider.OnTrafficDataReceived -= OnXPlaneTrafficReceived;
            }

            if (_ownShipPositionProvider != null)
            {
                _ownShipPositionProvider.OnPositionChanged -= OnOwnShipPositionChanged;
            }

            _isSubscribed = false;
        }

        #endregion

        #region Position Sync

        /// <summary>
        /// Syncs own-ship position from AircraftController to TrafficRadarDataManager
        /// </summary>
        private void SyncOwnShipPosition()
        {
            if (_ownShipPositionProvider == null || !_ownShipPositionProvider.IsValid)
                return;

            if (Time.time - _lastPositionUpdateTime < positionUpdateInterval)
                return;

            var currentPosition = _ownShipPositionProvider.CurrentPosition;

            float distanceMoved = CalculateDistance(
                _lastOwnPosition.Latitude,
                _lastOwnPosition.Longitude,
                currentPosition.Latitude,
                currentPosition.Longitude
            );

            if (distanceMoved < positionUpdateThreshold)
                return;

            UpdateOwnPosition(currentPosition);
        }

        /// <summary>
        /// Handles own-ship position change events
        /// </summary>
        private void OnOwnShipPositionChanged(OwnShipPosition newPosition)
        {
            UpdateOwnPosition(newPosition);
        }

        /// <summary>
        /// Updates own-ship position in TrafficRadarDataManager
        /// </summary>
        private void UpdateOwnPosition(OwnShipPosition newPosition)
        {
            _lastOwnPosition = newPosition;
            _lastPositionUpdateTime = Time.time;

            dataManager?.SetReferencePosition(
                (float)newPosition.Latitude,
                (float)newPosition.Longitude
            );

            if (radarController != null)
            {
                radarController.SetOwnPosition(
                    newPosition.Latitude,
                    newPosition.Longitude,
                    newPosition.AltitudeMeters,
                    newPosition.HeadingDegrees
                );
            }

            Log($"Own-ship position updated: {newPosition.Latitude:F4}, {newPosition.Longitude:F4}");
        }

        #endregion

        #region Traffic Data Handling

        /// <summary>
        /// Handles X-Plane traffic data received event
        /// </summary>
        private void OnXPlaneTrafficReceived(XPlaneTrafficState[] trafficData)
        {
            if (trafficData == null || trafficData.Length == 0)
            {
                _cachedXPlaneTraffic = null;
                _isXPlaneTrafficAvailable = false;
                return;
            }

            _cachedXPlaneTraffic = trafficData;
            _isXPlaneTrafficAvailable = true;

            if (verboseLogging)
            {
                Log($"Received {trafficData.Length} X-Plane traffic targets");
            }

            InjectTrafficIntoRadar(trafficData);
        }

        /// <summary>
        /// Injects X-Plane traffic data into TrafficRadarDataManager
        /// </summary>
        private void InjectTrafficIntoRadar(XPlaneTrafficState[] trafficData)
        {
            if (dataManager == null)
            {
                Debug.LogWarning("[XPlaneToTrafficRadarBridge] Cannot inject traffic - DataManager missing");
                return;
            }

            var aircraftDataList = new System.Collections.Generic.List<TrafficRadarDataManager.AircraftData>();
            dataManager.aircraftMap.Clear();
            dataManager.aircraftList.Clear();

            foreach (var state in trafficData)
            {
                float distanceKm = CalculateDistanceKm(
                    dataManager.referenceLatitude,
                    dataManager.referenceLongitude,
                    (float)state.Latitude,
                    (float)state.Longitude);

                if (distanceKm > dataManager.radiusFilterKm)
                {
                    continue;
                }

                var aircraftData = new TrafficRadarDataManager.AircraftData
                {
                    icao24 = state.Icao24?.ToLower() ?? "",
                    callsign = state.Callsign ?? "",
                    originCountry = "",
                    latitude = (float)state.Latitude,
                    longitude = (float)state.Longitude,
                    altitude = state.AltitudeMeters,
                    velocity = state.VelocityMps,
                    heading = state.Heading,
                    verticalRate = state.VerticalRateMps,
                    onGround = state.OnGround,
                    lastUpdateTime = state.LastUpdate,
                    type = TrafficRadarDataManager.AircraftType.Unknown
                };

                dataManager.aircraftMap[aircraftData.icao24] = aircraftData;
                dataManager.aircraftList.Add(aircraftData);
                aircraftDataList.Add(aircraftData);
            }

            dataManager.onDataUpdated?.Invoke(aircraftDataList);

            if (verboseLogging)
            {
                Log($"Injected {aircraftDataList.Count} X-Plane traffic targets into TrafficRadar");
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Calculates distance between two geographic points in meters
        /// </summary>
        private float CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const float EarthRadiusKm = 6371.0f;

            float dLat = (float)(lat2 - lat1) * Mathf.Deg2Rad;
            float dLon = (float)(lon2 - lon1) * Mathf.Deg2Rad;

            float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                      Mathf.Cos((float)lat1 * Mathf.Deg2Rad) * Mathf.Cos((float)lat2 * Mathf.Deg2Rad) *
                      Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);

            float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));

            return EarthRadiusKm * c * 1000f;
        }

        private static float CalculateDistanceKm(float lat1, float lon1, float lat2, float lon2)
        {
            const float earthRadiusKm = 6371f;

            float dLat = (lat2 - lat1) * Mathf.Deg2Rad;
            float dLon = (lon2 - lon1) * Mathf.Deg2Rad;

            float a = Mathf.Sin(dLat / 2f) * Mathf.Sin(dLat / 2f) +
                      Mathf.Cos(lat1 * Mathf.Deg2Rad) * Mathf.Cos(lat2 * Mathf.Deg2Rad) *
                      Mathf.Sin(dLon / 2f) * Mathf.Sin(dLon / 2f);

            float c = 2f * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1f - a));
            return earthRadiusKm * c;
        }

        /// <summary>
        /// Logs message if verbose logging is enabled
        /// </summary>
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[XPlaneToTrafficRadarBridge] {message}");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Manually set own-ship position
        /// </summary>
        public void SetOwnPosition(double latitude, double longitude, float altitudeMeters, float heading)
        {
            var position = new OwnShipPosition
            {
                Latitude = latitude,
                Longitude = longitude,
                AltitudeMeters = altitudeMeters,
                HeadingDegrees = heading,
                GroundSpeedMps = 0
            };
            UpdateOwnPosition(position);
        }

        /// <summary>
        /// Set the geographic filter range
        /// </summary>
        public void SetFilterRange(float rangeKm)
        {
            baseRangeKm = Mathf.Max(10f, rangeKm);
            dataManager?.SetGeographicFilter(
                (float)_lastOwnPosition.Latitude,
                (float)_lastOwnPosition.Longitude,
                baseRangeKm
            );
        }

        /// <summary>
        /// Force refresh of X-Plane traffic data
        /// </summary>
        public void RefreshXPlaneTraffic()
        {
            if (trafficProvider != null && trafficProvider.IsMonitoring)
            {
                Log("X-Plane traffic refresh requested");
            }
        }

        /// <summary>
        /// Toggle API traffic on/off
        /// </summary>
        public void SetApiTrafficEnabled(bool enabled)
        {
            if (dataManager == null) return;

            if (enabled)
            {
                dataManager.StartFetching();
            }
            else
            {
                dataManager.StopFetching();
            }
        }

        #endregion

        #region Debug

#if UNITY_EDITOR
        [ContextMenu("Debug: Log Status")]
        private void DebugLogStatus()
        {
            Debug.Log("=== XPlaneToTrafficRadarBridge Status ===");
            Debug.Log($"Active: {IsBridgeActive}");
            Debug.Log($"X-Plane Traffic Available: {_isXPlaneTrafficAvailable}");
            Debug.Log($"X-Plane Traffic Count: {XPlaneTrafficCount}");
            Debug.Log($"Position Sync Enabled: {syncOwnShipPosition}");
            Debug.Log($"Components:");
            Debug.Log($"  - TrafficProvider: {trafficProvider != null}");
            Debug.Log($"  - DataManager: {dataManager != null}");
            Debug.Log($"  - AircraftController: {aircraftController != null}");
            Debug.Log($"  - RadarController: {radarController != null}");

            if (_ownShipPositionProvider != null && _ownShipPositionProvider.IsValid)
            {
                var pos = _ownShipPositionProvider.CurrentPosition;
                Debug.Log($"Own-ship Position: {pos.Latitude:F4}, {pos.Longitude:F4}, {pos.AltitudeFeet:F0}ft");
            }
        }

        [ContextMenu("Debug: Force Position Sync")]
        private void DebugForcePositionSync()
        {
            if (_ownShipPositionProvider != null && _ownShipPositionProvider.IsValid)
            {
                UpdateOwnPosition(_ownShipPositionProvider.CurrentPosition);
            }
        }

        [ContextMenu("Debug: Inject Test Traffic")]
        private void DebugInjectTestTraffic()
        {
            var testTraffic = new XPlaneTrafficState[]
            {
                new XPlaneTrafficState
                {
                    Icao24 = "TEST01",
                    Callsign = "TEST1",
                    Latitude = _lastOwnPosition.Latitude + 0.01,
                    Longitude = _lastOwnPosition.Longitude + 0.01,
                    AltitudeMeters = _lastOwnPosition.AltitudeMeters + 1000,
                    Heading = 90f,
                    VelocityMps = 100f,
                    VerticalRateMps = 0f,
                    OnGround = false,
                    LastUpdate = DateTime.Now
                }
            };
            InjectTrafficIntoRadar(testTraffic);
        }
#endif

        #endregion
    }
}
