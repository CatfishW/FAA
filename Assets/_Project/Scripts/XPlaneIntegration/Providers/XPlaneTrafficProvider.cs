using System;
using System.Collections.Generic;
using UnityEngine;
using TrafficRadar;
using TrafficRadar.Core;
using FAA.XPlaneIntegration;

namespace FAA.XPlaneIntegration.Providers
{
    /// <summary>
    /// X-Plane traffic data provider - reads multiplayer/traffic slots from X-Plane DataRefs.
    /// Maps X-Plane traffic data to normalized TrafficTarget format and injects into TrafficRadarController.
    /// 
    /// READ-ONLY: This adapter only reads traffic from X-Plane. It does not inject traffic back.
    /// X-Plane provides up to 20 multiplayer slots via sim/multiplayer/position/[0-19] DataRefs.
    /// 
    /// DataRef mappings per slot:
    /// - sim/multiplayer/position/[n]/latitude → Lat
    /// - sim/multiplayer/position/[n]/longitude → Lon
    /// - sim/multiplayer/position/[n]/elevation → Altitude
    /// - sim/multiplayer/position/[n]/psi → Heading
    /// - sim/multiplayer/position/[n]/indicated_airspeed → Speed
    /// - sim/multiplayer/position/[n]/gear_position → OnGround (bool)
    /// </summary>
    public class XPlaneTrafficProvider : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Traffic Configuration")]
        [Tooltip("Enable X-Plane traffic data reading")]
        [SerializeField] private bool enableXPlaneTraffic = true;

        [Tooltip("Maximum number of traffic slots to monitor (X-Plane supports 1-19)")]
        [Range(1, 19)]
        [SerializeField] private int maxTrafficSlots = 10;

        [Tooltip("Update interval in seconds for traffic data polling")]
        [Range(0.1f, 5f)]
        [SerializeField] private float updateInterval = 0.5f;

        [Tooltip("Enable verbose logging for debugging")]
        [SerializeField] private bool verboseLogging = false;

        [Header("References")]
        [Tooltip("XPlaneUdpListener instance for UDP communication")]
        [SerializeField] private XPlaneUdpListener udpListener;

        [Tooltip("TrafficRadarController to inject traffic data into")]
        [SerializeField] private TrafficRadarController trafficRadarController;

        #endregion

        #region Private Fields

        /// <summary>
        /// Tracks active traffic targets by ICAO/callsign key
        /// </summary>
        private Dictionary<string, XPlaneTrafficSlot> _activeTrafficSlots = new Dictionary<string, XPlaneTrafficSlot>();

        /// <summary>
        /// Cached AircraftState array for current frame
        /// </summary>
        private List<AircraftState> _cachedAircraftStates = new List<AircraftState>();

        /// <summary>
        /// Last update time for throttled updates
        /// </summary>
        private float _lastUpdateTime;

        /// <summary>
        /// Internal UDP listener if not provided via inspector
        /// </summary>
        private XPlaneUdpListener _internalUdpListener;

        /// <summary>
        /// DataRef paths for traffic slots (dynamically built)
        /// </summary>
        private List<string> _dataRefPaths = new List<string>();

        /// <summary>
        /// Traffic slot index to DataRef value mapping
        /// </summary>
        private Dictionary<int, TrafficSlotData> _slotDataMap = new Dictionary<int, TrafficSlotData>();

        #endregion

        #region Properties

        /// <summary>
        /// Whether X-Plane traffic reading is enabled
        /// </summary>
        public bool EnableXPlaneTraffic
        {
            get => enableXPlaneTraffic;
            set
            {
                if (enableXPlaneTraffic != value)
                {
                    enableXPlaneTraffic = value;
                    if (value)
                    {
                        StartTrafficMonitoring();
                    }
                    else
                    {
                        StopTrafficMonitoring();
                    }
                }
            }
        }

        /// <summary>
        /// Maximum traffic slots to monitor
        /// </summary>
        public int MaxTrafficSlots
        {
            get => maxTrafficSlots;
            set => maxTrafficSlots = Mathf.Clamp(value, 1, 20);
        }

        /// <summary>
        /// Number of currently tracked traffic targets
        /// </summary>
        public int TrackedTrafficCount => _activeTrafficSlots.Count;

        /// <summary>
        /// Whether the provider is actively monitoring traffic
        /// </summary>
        public bool IsMonitoring { get; private set; }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            BuildDataRefPaths();
            InitializeUdpListener();
        }

        private void OnEnable()
        {
            if (enableXPlaneTraffic)
            {
                StartTrafficMonitoring();
            }
        }

        private void OnDisable()
        {
            StopTrafficMonitoring();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Update()
        {
            if (!enableXPlaneTraffic || !IsMonitoring)
                return;

            if (Time.time - _lastUpdateTime < updateInterval)
                return;

            _lastUpdateTime = Time.time;

            CleanupStaleTrafficSlots();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Builds DataRef paths for all traffic slots
        /// </summary>
        private void BuildDataRefPaths()
        {
            _dataRefPaths.Clear();
            _slotDataMap.Clear();

            for (int i = 1; i <= maxTrafficSlots; i++)
            {
                _dataRefPaths.Add($"sim/multiplayer/position/plane{i}_lat");
                _dataRefPaths.Add($"sim/multiplayer/position/plane{i}_lon");
                _dataRefPaths.Add($"sim/multiplayer/position/plane{i}_el");
                _dataRefPaths.Add($"sim/multiplayer/position/plane{i}_psi");
                _dataRefPaths.Add($"sim/multiplayer/position/plane{i}_the");
                _dataRefPaths.Add($"sim/multiplayer/position/plane{i}_phi");
                _dataRefPaths.Add($"sim/multiplayer/position/plane{i}_v_x");
                _dataRefPaths.Add($"sim/multiplayer/position/plane{i}_v_y");
                _dataRefPaths.Add($"sim/multiplayer/position/plane{i}_v_z");
                _dataRefPaths.Add($"sim/multiplayer/position/plane{i}_gear_deploy");
                _dataRefPaths.Add($"sim/multiplayer/position/plane{i}_flap_ratio");

                _slotDataMap[i] = new TrafficSlotData();
            }

            Log($"Built {_dataRefPaths.Count} DataRef paths for {maxTrafficSlots} traffic slots (XP12 format)");
        }

        /// <summary>
        /// Initializes UDP listener (uses provided reference or creates internal)
        /// </summary>
        private void InitializeUdpListener()
        {
            if (udpListener != null)
            {
                Log("Using provided XPlaneUdpListener instance");
                return;
            }

            _internalUdpListener = new XPlaneUdpListener();
            udpListener = _internalUdpListener;
            Log("Created internal XPlaneUdpListener");
        }

        #endregion

        #region Traffic Monitoring

        /// <summary>
        /// Starts monitoring X-Plane traffic data
        /// </summary>
        public void StartTrafficMonitoring()
        {
            if (IsMonitoring)
            {
                Log("Traffic monitoring already active");
                return;
            }

            if (udpListener == null)
            {
                Debug.LogError("[XPlaneTrafficProvider] UDP listener not available");
                return;
            }

            udpListener.OnDataReceived += OnUdpDataReceived;

            if (!udpListener.IsConnected)
            {
                udpListener.Connect();
            }

            SubscribeToTrafficDataRefs();

            IsMonitoring = true;
            Log($"Started monitoring {maxTrafficSlots} traffic slots");
        }

        /// <summary>
        /// Stops monitoring X-Plane traffic data
        /// </summary>
        public void StopTrafficMonitoring()
        {
            if (!IsMonitoring)
                return;

            if (udpListener != null)
            {
                udpListener.OnDataReceived -= OnUdpDataReceived;
            }

            UnsubscribeFromTrafficDataRefs();

            _activeTrafficSlots.Clear();
            _cachedAircraftStates.Clear();

            IsMonitoring = false;
            Log("Stopped traffic monitoring");
        }

        /// <summary>
        /// Subscribes to all traffic DataRefs with specified frequency
        /// </summary>
        private void SubscribeToTrafficDataRefs()
        {
            if (udpListener == null || !udpListener.IsConnected)
            {
                Debug.LogWarning("[XPlaneTrafficProvider] Cannot subscribe: UDP not connected");
                return;
            }

            float frequency = Mathf.FloorToInt(1f / updateInterval);
            frequency = Mathf.Clamp(frequency, 1, 30);

            foreach (var dataRef in _dataRefPaths)
            {
                udpListener.SendRrefRequest(dataRef, (int)frequency);
            }

            Log($"Subscribed to {_dataRefPaths.Count} DataRefs @ {frequency}Hz");
        }

        /// <summary>
        /// Unsubscribes from all traffic DataRefs
        /// </summary>
        private void UnsubscribeFromTrafficDataRefs()
        {
            if (udpListener == null)
                return;

            foreach (var dataRef in _dataRefPaths)
            {
                udpListener.SendRrefRequest(dataRef, 0);
            }

            Log("Unsubscribed from all traffic DataRefs");
        }

        #endregion

        #region Data Processing

        private void OnUdpDataReceived(Dictionary<string, float> data)
        {
            if (data == null || data.Count == 0 || !enableXPlaneTraffic)
                return;

            ParseTrafficSlotData(data);
            MapToAircraftStates();
            InjectTrafficData();

            if (_cachedAircraftStates.Count > 0)
            {
                OnTrafficDataReceived?.Invoke(_cachedAircraftStates.ToArray());
            }
        }

        private void CleanupStaleTrafficSlots()
        {
            if (_activeTrafficSlots.Count == 0)
            {
                return;
            }

            const float staleTimeoutSeconds = 10f;
            var slotsToRemove = new List<string>();

            foreach (var kvp in _activeTrafficSlots)
            {
                if (Time.time - kvp.Value.LastUpdateTime > staleTimeoutSeconds)
                {
                    slotsToRemove.Add(kvp.Key);
                }
            }

            foreach (var slotKey in slotsToRemove)
            {
                _activeTrafficSlots.Remove(slotKey);
            }
        }

        /// <summary>
         /// Parses raw DataRef data into traffic slot structures
         /// </summary>
        private void ParseTrafficSlotData(Dictionary<string, float> data)
        {
            for (int slotNumber = 1; slotNumber <= maxTrafficSlots; slotNumber++)
            {
                if (!_slotDataMap.TryGetValue(slotNumber, out var slotData))
                {
                    continue;
                }

                slotData.Latitude = GetDataRefValue(data, $"sim/multiplayer/position/plane{slotNumber}_lat");
                slotData.Longitude = GetDataRefValue(data, $"sim/multiplayer/position/plane{slotNumber}_lon");
                slotData.AltitudeMeters = GetDataRefValue(data, $"sim/multiplayer/position/plane{slotNumber}_el");
                slotData.Heading = GetDataRefValue(data, $"sim/multiplayer/position/plane{slotNumber}_psi");
                slotData.Pitch = GetDataRefValue(data, $"sim/multiplayer/position/plane{slotNumber}_the");
                slotData.Roll = GetDataRefValue(data, $"sim/multiplayer/position/plane{slotNumber}_phi");
                slotData.VelocityX = GetDataRefValue(data, $"sim/multiplayer/position/plane{slotNumber}_v_x");
                slotData.VelocityY = GetDataRefValue(data, $"sim/multiplayer/position/plane{slotNumber}_v_y");
                slotData.VelocityZ = GetDataRefValue(data, $"sim/multiplayer/position/plane{slotNumber}_v_z");
                slotData.GearPosition = GetDataRefValue(data, $"sim/multiplayer/position/plane{slotNumber}_gear_deploy");
                slotData.FlapRatio = GetDataRefValue(data, $"sim/multiplayer/position/plane{slotNumber}_flap_ratio");

                slotData.HasValidData = Mathf.Abs(slotData.Latitude) > 0.001f &&
                                        Mathf.Abs(slotData.Longitude) > 0.001f;

                _slotDataMap[slotNumber] = slotData;
            }
        }

        private static float GetDataRefValue(Dictionary<string, float> data, string dataRefPath)
        {
            return data.TryGetValue(dataRefPath, out float value) ? value : 0f;
        }

        /// <summary>
        /// Maps parsed slot data to AircraftState array
        /// </summary>
        private void MapToAircraftStates()
        {
            _cachedAircraftStates.Clear();
            var slotsToRemove = new List<string>();

            for (int i = 1; i <= maxTrafficSlots; i++)
            {
                if (!_slotDataMap.TryGetValue(i, out var slotData))
                {
                    continue;
                }

                if (!slotData.HasValidData)
                {
                    string invalidSlotKey = $"slot_{i}";
                    if (_activeTrafficSlots.ContainsKey(invalidSlotKey))
                    {
                        slotsToRemove.Add(invalidSlotKey);
                    }
                    continue;
                }

                string slotKey = $"slot_{i}";
                string callsign = $"MP{i:D2}";

                if (!_activeTrafficSlots.TryGetValue(slotKey, out var trafficSlot))
                {
                    trafficSlot = new XPlaneTrafficSlot
                    {
                        SlotIndex = i,
                        Key = slotKey,
                        FirstSeenTime = Time.time
                    };
                    _activeTrafficSlots[slotKey] = trafficSlot;
                    Log($"New traffic detected in slot {i}");
                }

                trafficSlot.LastUpdateTime = Time.time;
                trafficSlot.Data = slotData;

                float groundSpeedMps = new Vector3(slotData.VelocityX, slotData.VelocityY, slotData.VelocityZ).magnitude;
                
                var aircraftState = new AircraftState
                {
                    Icao24 = $"XPL{i:D4}",
                    Callsign = callsign,
                    Latitude = slotData.Latitude,
                    Longitude = slotData.Longitude,
                    AltitudeMeters = slotData.AltitudeMeters,
                    Heading = slotData.Heading,
                    VelocityMps = groundSpeedMps,
                    VerticalRateMps = slotData.VelocityY,
                    OnGround = slotData.GearPosition > 0.5f,
                    LastUpdate = DateTime.Now
                };

                _cachedAircraftStates.Add(aircraftState);
            }

            foreach (var slotKey in slotsToRemove)
            {
                _activeTrafficSlots.Remove(slotKey);
                Log($"Traffic removed from {slotKey}");
            }

            if (verboseLogging)
            {
                Log($"Mapped {_cachedAircraftStates.Count} active traffic slots");
            }
        }

        /// <summary>
        /// Injects traffic data into TrafficRadarController
        /// </summary>
        private void InjectTrafficData()
        {
            if (trafficRadarController == null)
            {
                trafficRadarController = FindObjectOfType<TrafficRadarController>();
                if (trafficRadarController == null)
                {
                    Debug.LogWarning("[XPlaneTrafficProvider] TrafficRadarController not found");
                    return;
                }
            }

            if (_cachedAircraftStates.Count == 0)
                return;

            var dataManager = trafficRadarController.GetComponentInChildren<TrafficRadarDataManager>();
            if (dataManager == null)
            {
                Debug.LogWarning("[XPlaneTrafficProvider] TrafficRadarDataManager not found");
                return;
            }

            dataManager.aircraftMap.Clear();
            dataManager.aircraftList.Clear();

            foreach (var state in _cachedAircraftStates)
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
                    icao24 = state.Icao24,
                    callsign = state.Callsign,
                    latitude = (float)state.Latitude,
                    longitude = (float)state.Longitude,
                    altitude = state.AltitudeMeters,
                    velocity = state.VelocityMps,
                    heading = state.Heading,
                    verticalRate = state.VerticalRateMps,
                    onGround = state.OnGround,
                    lastUpdateTime = state.LastUpdate
                };

                dataManager.aircraftMap[state.Icao24.ToLower()] = aircraftData;
                dataManager.aircraftList.Add(aircraftData);
            }

            dataManager.onDataUpdated?.Invoke(dataManager.aircraftList);
            Log($"Injected {_cachedAircraftStates.Count} X-Plane traffic targets into TrafficRadarDataManager");
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Cleans up resources
        /// </summary>
        private void Cleanup()
        {
            StopTrafficMonitoring();

            if (_internalUdpListener != null)
            {
                _internalUdpListener.Dispose();
                _internalUdpListener = null;
            }

            _activeTrafficSlots.Clear();
            _cachedAircraftStates.Clear();
            _slotDataMap.Clear();
        }

        #endregion

        #region Events

        /// <summary>
        /// Fired when new traffic data is received and mapped
        /// </summary>
        public event Action<AircraftState[]> OnTrafficDataReceived;

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

        public void SetUdpListener(XPlaneUdpListener listener)
        {
            if (udpListener != null)
            {
                udpListener.OnDataReceived -= OnUdpDataReceived;
            }

            if (_internalUdpListener != null && _internalUdpListener != listener)
            {
                _internalUdpListener.Dispose();
                _internalUdpListener = null;
            }

            udpListener = listener;

            if (udpListener != null)
            {
                udpListener.OnDataReceived += OnUdpDataReceived;

                if (enableXPlaneTraffic && IsMonitoring)
                {
                    SubscribeToTrafficDataRefs();
                }
            }
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Data structure for a single traffic slot
        /// </summary>
        [Serializable]
        private struct TrafficSlotData
        {
            public float Latitude;
            public float Longitude;
            public float AltitudeMeters;
            public float Heading;
            public float Pitch;
            public float Roll;
            public float VelocityX;
            public float VelocityY;
            public float VelocityZ;
            public float GearPosition;
            public float FlapRatio;
            public bool HasValidData;
        }

        /// <summary>
        /// Tracks state of an individual traffic target
        /// </summary>
        private class XPlaneTrafficSlot
        {
            public int SlotIndex;
            public string Key;
            public float FirstSeenTime;
            public float LastUpdateTime;
            public TrafficSlotData Data;
        }

        #endregion

        #region Debug

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[XPlaneTrafficProvider] {message}");
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Log Status")]
        private void DebugLogStatus()
        {
            Debug.Log("=== XPlaneTrafficProvider Status ===");
            Debug.Log($"Enabled: {enableXPlaneTraffic}");
            Debug.Log($"Monitoring: {IsMonitoring}");
            Debug.Log($"Max Slots: {maxTrafficSlots}");
            Debug.Log($"Active Traffic: {_activeTrafficSlots.Count}");
            Debug.Log($"UDP Connected: {udpListener?.IsConnected ?? false}");

            if (_cachedAircraftStates.Count > 0)
            {
                Debug.Log($"First aircraft: {_cachedAircraftStates[0].Callsign} at {_cachedAircraftStates[0].Latitude:F4}, {_cachedAircraftStates[0].Longitude:F4}");
            }
        }

        [ContextMenu("Debug: Force Refresh")]
        private void DebugForceRefresh()
        {
            udpListener?.ProcessQueuedData();
            CleanupStaleTrafficSlots();
            Log("Manual refresh requested");
        }
#endif

        #endregion
    }
}
