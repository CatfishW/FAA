using System;
using UnityEngine;
using AircraftControl.Core;
using TrafficRadar;
using TrafficRadar.Core;
using WeatherRadar;
using AircraftState = AircraftControl.Core.AircraftState;

namespace FAA.XPlaneIntegration.Bridges
{
    /// <summary>
    /// Bridge component that broadcasts X-Plane aircraft position to all radar systems.
    /// 
    /// This component:
    /// - Finds AircraftController and reads position from its State property
    /// - Broadcasts position changes to multiple radar systems:
    ///   * TrafficRadarDataManager.SetReferencePosition()
    ///   * WeatherRadarProviderBase.SetAircraftPosition()
    ///   * TrafficRadarController.SetOwnPosition()
    /// - Updates at configurable interval (default: 1Hz)
    /// - Only broadcasts when position changes significantly (threshold-based)
    /// 
    /// Position Change Detection:
    /// - Tracks last broadcast position (lat/lon/heading)
    /// - Only updates if lat/lon changed > threshold OR heading changed > 5 degrees
    /// - Prevents excessive updates to radar systems
    /// 
    /// Setup:
    /// 1. Add this component to a GameObject in your scene
    /// 2. Assign AircraftController reference (or leave null for auto-discovery)
    /// 3. Configure update interval and position thresholds
    /// 4. Enable/disable specific radar targets as needed
    /// 
    /// Usage:
    /// - Works with XPlaneAircraftProvider injecting data into AircraftController
    /// - Automatically discovers radar systems via FindObjectOfType
    /// - Can be configured at runtime via public methods
    /// </summary>
    [AddComponentMenu("X-Plane Integration/X-Plane Own Ship Position Bridge")]
    public class XPlaneOwnShipPositionBridge : MonoBehaviour
    {
        #region Inspector Settings

        [Header("Aircraft Source")]
        [Tooltip("AircraftController to get position from. If null, will auto-find on Awake.")]
        [SerializeField] private AircraftController aircraftController;

        [Header("Radar Targets")]
        [Tooltip("Traffic Radar DataManager to update. If null, will auto-find.")]
        [SerializeField] private TrafficRadarDataManager trafficRadarDataManager;

        [Tooltip("Traffic Radar Controller to update. If null, will auto-find.")]
        [SerializeField] private TrafficRadarController trafficRadarController;

        [Tooltip("Weather Radar Provider to update. If null, will auto-find.")]
        [SerializeField] private WeatherRadarProviderBase weatherRadarProvider;

        [Header("Update Settings")]
        [Tooltip("How often to check for position changes and broadcast (Hz). Default: 1Hz")]
        [Range(0.1f, 10f)]
        [SerializeField] private float updateRateHz = 1f;

        [Tooltip("Minimum position change to trigger broadcast (meters). Prevents excessive updates.")]
        [SerializeField] private float positionChangeThresholdMeters = 50f;

        [Tooltip("Minimum heading change to trigger broadcast (degrees). Default: 5 degrees.")]
        [Range(1f, 45f)]
        [SerializeField] private float headingChangeThresholdDegrees = 5f;

        [Tooltip("Always broadcast at update interval regardless of change (debug/testing)")]
        [SerializeField] private bool forcePeriodicBroadcast = false;

        [Header("Integration Options")]
        [Tooltip("Update Traffic Radar DataManager reference position")]
        [SerializeField] private bool updateTrafficDataManager = true;

        [Tooltip("Update Traffic Radar Controller own position")]
        [SerializeField] private bool updateTrafficController = true;

        [Tooltip("Update Weather Radar Provider aircraft position")]
        [SerializeField] private bool updateWeatherRadar = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        [Tooltip("Log every broadcast (verbose)")]
        [SerializeField] private bool verboseLogging = false;

        #endregion

        #region Private Fields

        private double _lastBroadcastLatitude;
        private double _lastBroadcastLongitude;
        private float _lastBroadcastHeading;
        private float _lastBroadcastTime;
        private float _updateInterval;
        private bool _isInitialized;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _updateInterval = 1f / updateRateHz;
            FindDependencies();
            _isInitialized = true;
        }

        private void Start()
        {
            ForceBroadcast();
        }

        private void Update()
        {
            if (!_isInitialized || aircraftController == null)
                return;

            if (Time.time - _lastBroadcastTime >= _updateInterval)
            {
                CheckAndBroadcastPosition();
            }
        }

        private void OnValidate()
        {
            _updateInterval = 1f / updateRateHz;
        }

        #endregion

        #region Initialization

        private void FindDependencies()
        {
            if (aircraftController == null)
            {
                aircraftController = FindObjectOfType<AircraftController>();
                if (aircraftController == null)
                {
                    Debug.LogError("[XPlaneOwnShipPositionBridge] No AircraftController found! Bridge cannot function without an aircraft source.");
                    return;
                }
                LogDebug($"Auto-found AircraftController: {aircraftController.name}");
            }

            if (trafficRadarDataManager == null && updateTrafficDataManager)
            {
                trafficRadarDataManager = FindObjectOfType<TrafficRadarDataManager>();
                if (trafficRadarDataManager != null)
                {
                    LogDebug($"Auto-found TrafficRadarDataManager: {trafficRadarDataManager.name}");
                }
            }

            if (trafficRadarController == null && updateTrafficController)
            {
                trafficRadarController = FindObjectOfType<TrafficRadarController>();
                if (trafficRadarController != null)
                {
                    LogDebug($"Auto-found TrafficRadarController: {trafficRadarController.name}");
                }
            }

            if (weatherRadarProvider == null && updateWeatherRadar)
            {
                weatherRadarProvider = FindObjectOfType<WeatherRadarProviderBase>();
                if (weatherRadarProvider != null)
                {
                    LogDebug($"Auto-found WeatherRadarProvider: {weatherRadarProvider.name}");
                }
            }

            LogDependencyStatus();
        }

        /// <summary>
        /// Log the status of found dependencies
        /// </summary>
        private void LogDependencyStatus()
        {
            if (!showDebugInfo) return;

            Debug.Log("[XPlaneOwnShipPositionBridge] Dependencies:");
            Debug.Log($"  - AircraftController: {(aircraftController != null ? "✓" : "✗")}");
            Debug.Log($"  - TrafficRadarDataManager: {(trafficRadarDataManager != null ? "✓" : "✗")}");
            Debug.Log($"  - TrafficRadarController: {(trafficRadarController != null ? "✓" : "✗")}");
            Debug.Log($"  - WeatherRadarProvider: {(weatherRadarProvider != null ? "✓" : "✗")}");
        }

        #endregion

        #region Position Broadcasting

        private void CheckAndBroadcastPosition()
        {
            if (aircraftController == null || !aircraftController.IsValid)
                return;

            var state = aircraftController.State;

            if (!forcePeriodicBroadcast && !ShouldBroadcast(state))
            {
                return;
            }

            BroadcastPosition(state);
        }

        private bool ShouldBroadcast(AircraftState state)
        {
            float distanceChange = CalculateDistanceMeters(
                _lastBroadcastLatitude,
                _lastBroadcastLongitude,
                state.Latitude,
                state.Longitude
            );

            if (distanceChange >= positionChangeThresholdMeters)
            {
                if (verboseLogging)
                {
                    Debug.Log($"[XPlaneOwnShipPositionBridge] Position change: {distanceChange:F1}m >= {positionChangeThresholdMeters}m threshold");
                }
                return true;
            }

            float headingChange = Mathf.Abs(Mathf.DeltaAngle(_lastBroadcastHeading, state.Heading));

            if (headingChange >= headingChangeThresholdDegrees)
            {
                if (verboseLogging)
                {
                    Debug.Log($"[XPlaneOwnShipPositionBridge] Heading change: {headingChange:F1}° >= {headingChangeThresholdDegrees}° threshold");
                }
                return true;
            }

            return false;
        }

        private void BroadcastPosition(AircraftState state)
        {
            _lastBroadcastLatitude = state.Latitude;
            _lastBroadcastLongitude = state.Longitude;
            _lastBroadcastHeading = state.Heading;
            _lastBroadcastTime = Time.time;

            bool anyUpdated = false;

            if (updateTrafficDataManager && trafficRadarDataManager != null)
            {
                UpdateTrafficRadarDataManager(state);
                anyUpdated = true;
            }

            if (updateTrafficController && trafficRadarController != null)
            {
                UpdateTrafficRadarController(state);
                anyUpdated = true;
            }

            if (updateWeatherRadar && weatherRadarProvider != null)
            {
                UpdateWeatherRadarProvider(state);
                anyUpdated = true;
            }

            if (verboseLogging || (showDebugInfo && anyUpdated))
            {
                Debug.Log($"[XPlaneOwnShipPositionBridge] Broadcast: {state.Latitude:F4}, {state.Longitude:F4}, " +
                         $"{state.AltitudeFeet:F0}ft, Hdg: {state.Heading:F0}°");
            }
        }

        private void UpdateTrafficRadarDataManager(AircraftState state)
        {
            try
            {
                trafficRadarDataManager.SetReferencePosition(
                    (float)state.Latitude,
                    (float)state.Longitude
                );

                if (verboseLogging)
                {
                    Debug.Log($"[XPlaneOwnShipPositionBridge] TrafficRadarDataManager updated");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[XPlaneOwnShipPositionBridge] Failed to update TrafficRadarDataManager: {e.Message}");
            }
        }

        private void UpdateTrafficRadarController(AircraftState state)
        {
            try
            {
                trafficRadarController.SetOwnPosition(
                    state.Latitude,
                    state.Longitude,
                    state.AltitudeMeters,
                    state.Heading
                );

                if (verboseLogging)
                {
                    Debug.Log($"[XPlaneOwnShipPositionBridge] TrafficRadarController updated");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[XPlaneOwnShipPositionBridge] Failed to update TrafficRadarController: {e.Message}");
            }
        }

        private void UpdateWeatherRadarProvider(AircraftState state)
        {
            try
            {
                weatherRadarProvider.SetAircraftPosition(
                    state.AltitudeFeet,
                    (float)state.Latitude,
                    (float)state.Longitude,
                    state.Heading
                );

                if (verboseLogging)
                {
                    Debug.Log($"[XPlaneOwnShipPositionBridge] WeatherRadarProvider updated");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[XPlaneOwnShipPositionBridge] Failed to update WeatherRadarProvider: {e.Message}");
            }
        }

        #endregion

        #region Public Methods

        public void ForceBroadcast()
        {
            if (aircraftController == null || !aircraftController.IsValid)
            {
                Debug.LogWarning("[XPlaneOwnShipPositionBridge] Cannot force broadcast - no valid AircraftController");
                return;
            }

            BroadcastPosition(aircraftController.State);
        }

        public void SetAircraftController(AircraftController controller)
        {
            aircraftController = controller;
            if (controller != null)
            {
                LogDebug($"AircraftController set: {controller.name}");
                ForceBroadcast();
            }
        }

        public void SetTrafficRadarDataManager(TrafficRadarDataManager dataManager)
        {
            trafficRadarDataManager = dataManager;
        }

        public void SetTrafficRadarController(TrafficRadarController controller)
        {
            trafficRadarController = controller;
        }

        public void SetWeatherRadarProvider(WeatherRadarProviderBase provider)
        {
            weatherRadarProvider = provider;
        }

        public void SetTrafficDataManagerEnabled(bool enabled)
        {
            updateTrafficDataManager = enabled;
        }

        public void SetTrafficControllerEnabled(bool enabled)
        {
            updateTrafficController = enabled;
        }

        public void SetWeatherRadarEnabled(bool enabled)
        {
            updateWeatherRadar = enabled;
        }

        public void SetUpdateRate(float rateHz)
        {
            updateRateHz = Mathf.Max(0.1f, rateHz);
            _updateInterval = 1f / updateRateHz;
        }

        public void SetPositionThreshold(float meters)
        {
            positionChangeThresholdMeters = Mathf.Max(0f, meters);
        }

        public void SetHeadingThreshold(float degrees)
        {
            headingChangeThresholdDegrees = Mathf.Clamp(degrees, 1f, 45f);
        }

        #endregion

        #region Utility

        private float CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const float EarthRadiusMeters = 6371000f;

            float dLat = (float)(lat2 - lat1) * Mathf.Deg2Rad;
            float dLon = (float)(lon2 - lon1) * Mathf.Deg2Rad;

            float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                      Mathf.Cos((float)lat1 * Mathf.Deg2Rad) * Mathf.Cos((float)lat2 * Mathf.Deg2Rad) *
                      Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);

            float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));

            return EarthRadiusMeters * c;
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private void LogDebug(string message)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[XPlaneOwnShipPositionBridge] {message}");
            }
        }

        #endregion

        #region Debug

#if UNITY_EDITOR
        [ContextMenu("Find All Dependencies")]
        private void EditorFindDependencies()
        {
            FindDependencies();
        }

        [ContextMenu("Force Broadcast")]
        private void EditorForceBroadcast()
        {
            ForceBroadcast();
        }

        [ContextMenu("Log Current Status")]
        private void EditorLogStatus()
        {
            Debug.Log("=== XPlaneOwnShipPositionBridge Status ===");
            Debug.Log($"Update Rate: {updateRateHz} Hz (interval: {_updateInterval:F2}s)");
            Debug.Log($"Position Threshold: {positionChangeThresholdMeters}m");
            Debug.Log($"Heading Threshold: {headingChangeThresholdDegrees}°");
            Debug.Log($"Last Broadcast: {Time.time - _lastBroadcastTime:F2}s ago");

            if (aircraftController != null && aircraftController.IsValid)
            {
                var state = aircraftController.State;
                Debug.Log($"Aircraft Position: {state.Latitude:F4}, {state.Longitude:F4}");
                Debug.Log($"Altitude: {state.AltitudeFeet:F0} ft | Heading: {state.Heading:F0}°");
            }
            else
            {
                Debug.Log("AircraftController: Not connected or invalid");
            }

            Debug.Log($"TrafficRadarDataManager: {(trafficRadarDataManager != null ? "Connected" : "Not Found")}");
            Debug.Log($"TrafficRadarController: {(trafficRadarController != null ? "Connected" : "Not Found")}");
            Debug.Log($"WeatherRadarProvider: {(weatherRadarProvider != null ? "Connected" : "Not Found")}");
        }
#endif

        #endregion
    }
}
