using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TrafficRadar.Core
{
    /// <summary>
    /// Main controller for the Traffic Radar system.
    /// Acts as a facade/mediator between data sources, processing, and display.
    /// 
    /// Design principles:
    /// - High Cohesion: Focuses only on coordinating radar components
    /// - Low Coupling: Communicates via interfaces and events
    /// - Dependency Injection: Components can be swapped via inspector or runtime
    /// </summary>
    [AddComponentMenu("Traffic Radar/Traffic Radar Controller")]
    public class TrafficRadarController : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("Data Source")]
        [Tooltip("TrafficRadarDataManager component providing aircraft data")]
        [SerializeField] private TrafficRadarDataManager dataManager;
        
        [Header("Display")]
        [Tooltip("TrafficRadarDisplay component for rendering")]
        [SerializeField] private TrafficRadarDisplay radarDisplay;
        
        [Header("Radar Settings")]
        [Tooltip("Radar range in nautical miles")]
        [SerializeField] private float rangeNM = 40f;
        
        [Tooltip("Available range options")]
        [SerializeField] private float[] rangeOptionsNM = { 10f, 20f, 40f, 80f, 150f };
        
        [Tooltip("Maximum targets to display")]
        [SerializeField] private int maxTargets = 50;
        
        [Header("Threat Thresholds")]
        [SerializeField] private ThreatThresholds threatThresholds = new ThreatThresholds();
        
        [Header("Update Settings")]
        [Tooltip("How often to process and update display (Hz)")]
        [SerializeField] private float updateRate = 2f;
        
        [Header("Auto-Range Settings")]
        [Tooltip("Automatically adjust radar range to include nearby aircraft")]
        [SerializeField] private bool autoRangeEnabled = true;
        
        [Tooltip("Minimum range for auto-range in NM")]
        [SerializeField] private float autoRangeMinNM = 10f;
        
        [Tooltip("Also update data manager's fetch radius when position changes (may override manual radius)")]
        [SerializeField] private bool syncFetchRadiusWithRange = false;
        
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;
        
        [Header("Events")]
        public UnityEvent<int> OnTargetCountChanged;
        public UnityEvent<ThreatLevel> OnHighestThreatChanged;
        public UnityEvent<IReadOnlyList<RadarTarget>> OnTargetsUpdated;
        
        #endregion
        
        #region Private Fields
        
        private RadarDataProcessor _processor;
        private List<AircraftState> _cachedAircraftStates = new List<AircraftState>();
        private OwnShipPosition _currentOwnPosition;
        private float _nextUpdateTime;
        private int _lastTargetCount;
        private ThreatLevel _lastHighestThreat = ThreatLevel.OtherTraffic;
        private IReadOnlyList<RadarTarget> _currentTargets;
        
        #endregion
        
        #region Properties
        
        public float RangeNM
        {
            get => rangeNM;
            set
            {
                rangeNM = Mathf.Max(1f, value);
                if (_processor != null)
                    _processor.RangeNM = rangeNM;
                // Update the display's range
                if (radarDisplay != null)
                    radarDisplay.RangeNM = rangeNM;
                
                Log($"Range set to {rangeNM} NM");
            }
        }
        
        public IReadOnlyList<RadarTarget> CurrentTargets => _currentTargets;
        public int TargetCount => _currentTargets?.Count ?? 0;
        public ThreatLevel HighestThreat => _lastHighestThreat;
        public OwnShipPosition OwnPosition => _currentOwnPosition;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            AutoFindComponents();
            InitializeProcessor();
        }
        
        private void OnEnable()
        {
            SubscribeToEvents();
        }
        
        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }
        
        private void Start()
        {
            // Initialize own position from position updater or use default
            UpdateOwnPosition();
            
            // Set initial range
            RangeNM = rangeNM;
            
            // Force initial data fetch and processing
            if (dataManager != null && dataManager.AircraftCount > 0)
            {
                ProcessCurrentData();
            }
        }
        
        private void Update()
        {
            // Periodic processing
            if (Time.time >= _nextUpdateTime)
            {
                ProcessCurrentData();
                _nextUpdateTime = Time.time + (1f / updateRate);
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Cycle through available range options
        /// </summary>
        public void CycleRange()
        {
            int currentIndex = FindCurrentRangeIndex();
            currentIndex = (currentIndex + 1) % rangeOptionsNM.Length;
            RangeNM = rangeOptionsNM[currentIndex];
        }
        
        /// <summary>
        /// Increase range to next higher option
        /// </summary>
        public void IncreaseRange()
        {
            int currentIndex = FindCurrentRangeIndex();
            if (currentIndex < rangeOptionsNM.Length - 1)
            {
                RangeNM = rangeOptionsNM[currentIndex + 1];
            }
        }
        
        /// <summary>
        /// Decrease range to next lower option
        /// </summary>
        public void DecreaseRange()
        {
            int currentIndex = FindCurrentRangeIndex();
            if (currentIndex > 0)
            {
                RangeNM = rangeOptionsNM[currentIndex - 1];
            }
        }
        
        /// <summary>
        /// Force immediate data refresh
        /// </summary>
        public void RefreshData()
        {
            if (dataManager != null)
            {
                dataManager.FetchDataNow();
            }
        }
        
        /// <summary>
        /// Set own-ship position manually
        /// </summary>
        public void SetOwnPosition(double lat, double lon, float altMeters, float heading)
        {
            _currentOwnPosition = new OwnShipPosition
            {
                Latitude = lat,
                Longitude = lon,
                AltitudeMeters = altMeters,
                HeadingDegrees = heading
            };
            
            // Update data manager's geographic filter position (not radius unless syncFetchRadiusWithRange is enabled)
            if (dataManager != null)
            {
                if (syncFetchRadiusWithRange)
                {
                    // Auto-calculate radius based on range
                    float radiusKm = rangeNM * 1.852f * 1.5f; // 50% buffer
                    dataManager.SetGeographicFilter((float)lat, (float)lon, radiusKm);
                }
                else
                {
                    // Only update position, preserve user's manual radius setting
                    dataManager.SetReferencePosition((float)lat, (float)lon);
                }
            }
            
            ProcessCurrentData();
        }
        
        /// <summary>
        /// Find the optimal range to display all nearby aircraft
        /// </summary>
        public void AutoAdjustRange()
        {
            if (_currentTargets == null || _currentTargets.Count == 0)
            {
                RangeNM = autoRangeMinNM;
                return;
            }
            
            // Find the farthest target
            float maxDistance = 0f;
            foreach (var target in _currentTargets)
            {
                if (target.DistanceNM > maxDistance)
                    maxDistance = target.DistanceNM;
            }
            
            // Find the best range option
            float newRange = autoRangeMinNM;
            foreach (float option in rangeOptionsNM)
            {
                if (option >= maxDistance * 1.2f) // 20% margin
                {
                    newRange = option;
                    break;
                }
                newRange = option; // Use largest if none fit
            }
            
            RangeNM = Mathf.Max(newRange, autoRangeMinNM);
        }
        
        #endregion
        
        #region Private Methods
        
        private void AutoFindComponents()
        {
            if (dataManager == null)
                dataManager = FindObjectOfType<TrafficRadarDataManager>();
            
            if (radarDisplay == null)
                radarDisplay = FindObjectOfType<TrafficRadarDisplay>();
            
            // Log what was found
            Log($"Components found - DataManager: {dataManager != null}, Display: {radarDisplay != null}");
        }
        
        private void InitializeProcessor()
        {
            _processor = new RadarDataProcessor(threatThresholds)
            {
                RangeNM = rangeNM,
                MaxTargets = maxTargets
            };
        }
        
        private void SubscribeToEvents()
        {
            if (dataManager != null)
            {
                dataManager.onDataUpdated.AddListener(OnDataManagerUpdated);
            }
        }
        
        private void UnsubscribeFromEvents()
        {
            if (dataManager != null)
            {
                dataManager.onDataUpdated.RemoveListener(OnDataManagerUpdated);
            }
        }
        
        private void OnDataManagerUpdated(List<TrafficRadarDataManager.AircraftData> aircraftList)
        {
            Log($"Received {aircraftList.Count} aircraft from data manager");
            
            // Convert to AircraftState list
            _cachedAircraftStates.Clear();
            foreach (var aircraft in aircraftList)
            {
                _cachedAircraftStates.Add(new AircraftState
                {
                    Icao24 = aircraft.icao24,
                    Callsign = aircraft.callsign,
                    Latitude = aircraft.latitude,
                    Longitude = aircraft.longitude,
                    AltitudeMeters = aircraft.altitude,
                    Heading = aircraft.heading,
                    VelocityMps = aircraft.velocity,
                    VerticalRateMps = aircraft.verticalRate,
                    OnGround = aircraft.onGround,
                    LastUpdate = aircraft.lastUpdateTime
                });
            }
            
            // Update own position and process immediately
            UpdateOwnPosition();
            ProcessCurrentData();
            
            // Auto-adjust range if enabled and we have aircraft but none in range
            if (autoRangeEnabled && _cachedAircraftStates.Count > 0 && (_currentTargets == null || _currentTargets.Count == 0))
            {
                Log("No targets in range - auto-adjusting range...");
                AutoAdjustRangeToIncludeAircraft();
            }
        }
        
        private void UpdateOwnPosition()
        {
            // Use data manager's reference position
            // The OwnAircraftRadarBridge will call SetOwnPosition() to update dynamically
            if (dataManager != null && _currentOwnPosition.Latitude == 0 && _currentOwnPosition.Longitude == 0)
            {
                _currentOwnPosition = new OwnShipPosition
                {
                    Latitude = dataManager.referenceLatitude,
                    Longitude = dataManager.referenceLongitude,
                    AltitudeMeters = 313, // Default altitude (~1000 ft)
                    HeadingDegrees = 0
                };
                Log($"Using data manager reference position: {dataManager.referenceLatitude:F4}, {dataManager.referenceLongitude:F4}");
            }
        }
        
        private void ProcessCurrentData()
        {
            if (_processor == null)
            {
                InitializeProcessor();
            }
            
            // Use cached aircraft states if available, otherwise try to get from data manager
            if (_cachedAircraftStates.Count == 0 && dataManager != null && dataManager.AircraftCount > 0)
            {
                foreach (var aircraft in dataManager.AircraftList)
                {
                    _cachedAircraftStates.Add(new AircraftState
                    {
                        Icao24 = aircraft.icao24,
                        Callsign = aircraft.callsign,
                        Latitude = aircraft.latitude,
                        Longitude = aircraft.longitude,
                        AltitudeMeters = aircraft.altitude,
                        Heading = aircraft.heading,
                        VelocityMps = aircraft.velocity,
                        VerticalRateMps = aircraft.verticalRate,
                        OnGround = aircraft.onGround,
                        LastUpdate = aircraft.lastUpdateTime
                    });
                }
            }
            
            // Process aircraft into radar targets
            _currentTargets = _processor.ProcessAircraft(_cachedAircraftStates, _currentOwnPosition);
            
            // Log processing results
            if (_cachedAircraftStates.Count > 0)
            {
                Log($"Processed {_cachedAircraftStates.Count} aircraft -> {_currentTargets.Count} targets in range ({rangeNM} NM)");
                
                if (_currentTargets.Count == 0 && _cachedAircraftStates.Count > 0)
                {
                    // Calculate distance to nearest aircraft for debugging
                    float nearestDist = float.MaxValue;
                    string nearestCallsign = "";
                    foreach (var ac in _cachedAircraftStates)
                    {
                        float dist = CalculateDistanceNM(_currentOwnPosition.Latitude, _currentOwnPosition.Longitude,
                                                         ac.Latitude, ac.Longitude);
                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            nearestCallsign = ac.Callsign;
                        }
                    }
                    Log($"WARNING: No targets in range! Own pos: {_currentOwnPosition.Latitude:F4}, {_currentOwnPosition.Longitude:F4}. Nearest aircraft '{nearestCallsign}' at {nearestDist:F1} NM");
                }
            }
            
            // Update display
            UpdateDisplay();
            
            // Check for count/threat changes
            CheckForChanges();
        }
        
        private void AutoAdjustRangeToIncludeAircraft()
        {
            if (_cachedAircraftStates.Count == 0)
                return;
            
            // Find nearest aircraft
            float nearestDist = float.MaxValue;
            foreach (var ac in _cachedAircraftStates)
            {
                float dist = CalculateDistanceNM(_currentOwnPosition.Latitude, _currentOwnPosition.Longitude,
                                                 ac.Latitude, ac.Longitude);
                if (dist < nearestDist)
                    nearestDist = dist;
            }
            
            // Find appropriate range
            float targetRange = nearestDist * 1.5f; // 50% margin
            foreach (float option in rangeOptionsNM)
            {
                if (option >= targetRange)
                {
                    RangeNM = option;
                    Log($"Auto-adjusted range to {option} NM to include aircraft at {nearestDist:F1} NM");
                    ProcessCurrentData(); // Re-process with new range
                    return;
                }
            }
            
            // Use maximum range
            RangeNM = rangeOptionsNM[rangeOptionsNM.Length - 1];
            Log($"Auto-adjusted to maximum range {RangeNM} NM");
            ProcessCurrentData();
        }
        
        private float CalculateDistanceNM(double lat1, double lon1, double lat2, double lon2)
        {
            const float EarthRadiusKm = 6371f;
            const float KmToNm = 0.539957f;
            
            float dLat = (float)(lat2 - lat1) * Mathf.Deg2Rad;
            float dLon = (float)(lon2 - lon1) * Mathf.Deg2Rad;
            
            float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                      Mathf.Cos((float)lat1 * Mathf.Deg2Rad) * Mathf.Cos((float)lat2 * Mathf.Deg2Rad) *
                      Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
            
            float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
            
            return EarthRadiusKm * c * KmToNm;
        }
        
        private void UpdateDisplay()
        {
            if (radarDisplay == null || _currentTargets == null)
                return;
            
            // Convert RadarTarget to RadarTrafficTarget for compatibility with existing display
            var displayTargets = new List<RadarTrafficTarget>();
            foreach (var target in _currentTargets)
            {
                displayTargets.Add(new RadarTrafficTarget
                {
                    icao24 = target.Icao24,
                    callsign = target.Callsign,
                    latitude = (float)target.Latitude,
                    longitude = (float)target.Longitude,
                    altitudeFt = target.AltitudeFeet,
                    heading = target.Heading,
                    groundSpeedKts = target.GroundSpeedKnots,
                    verticalRateFpm = target.VerticalRateFpm,
                    distanceNM = target.DistanceNM,
                    bearingDeg = target.BearingDegrees,
                    relativeAltitudeFt = target.RelativeAltitudeFeet,
                    threatLevel = target.ThreatLevel,
                    radarPosition = target.RadarPosition
                });
            }
            
            // Invoke the targets updated event for the display to pick up
            // The display listens to the provider, so we need to update via provider or directly
            // For now, we'll fire our own event that can be subscribed to
            OnTargetsUpdated?.Invoke(_currentTargets);
        }
        
        private void CheckForChanges()
        {
            if (_currentTargets == null)
                return;
            
            // Check target count change
            int currentCount = _currentTargets.Count;
            if (currentCount != _lastTargetCount)
            {
                _lastTargetCount = currentCount;
                OnTargetCountChanged?.Invoke(currentCount);
            }
            
            // Check highest threat change
            ThreatLevel highest = ThreatLevel.OtherTraffic;
            foreach (var target in _currentTargets)
            {
                if (target.ThreatLevel > highest)
                    highest = target.ThreatLevel;
            }
            
            if (highest != _lastHighestThreat)
            {
                _lastHighestThreat = highest;
                OnHighestThreatChanged?.Invoke(highest);
            }
        }
        
        private int FindCurrentRangeIndex()
        {
            for (int i = 0; i < rangeOptionsNM.Length; i++)
            {
                if (Mathf.Approximately(rangeOptionsNM[i], rangeNM))
                    return i;
            }
            return 0;
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[TrafficRadarController] {message}");
            }
        }
        
        #endregion
        
        #region Debug
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure range is within options
            if (rangeOptionsNM != null && rangeOptionsNM.Length > 0)
            {
                System.Array.Sort(rangeOptionsNM);
            }
        }
        
        [ContextMenu("Debug: Log Status")]
        private void DebugLogStatus()
        {
            Debug.Log($"=== TrafficRadarController Status ===");
            Debug.Log($"Own Position: {_currentOwnPosition.Latitude:F4}, {_currentOwnPosition.Longitude:F4}");
            Debug.Log($"Range: {rangeNM} NM");
            Debug.Log($"Cached Aircraft: {_cachedAircraftStates.Count}");
            Debug.Log($"Targets in Range: {_currentTargets?.Count ?? 0}");
            Debug.Log($"Data Manager Aircraft: {dataManager?.AircraftCount ?? 0}");
            
            if (_cachedAircraftStates.Count > 0)
            {
                Debug.Log($"First aircraft: {_cachedAircraftStates[0].Callsign} at {_cachedAircraftStates[0].Latitude:F4}, {_cachedAircraftStates[0].Longitude:F4}");
            }
        }
        
        [ContextMenu("Debug: Force Auto-Range")]
        private void DebugForceAutoRange()
        {
            AutoAdjustRangeToIncludeAircraft();
        }
#endif
        
        #endregion
    }
}
