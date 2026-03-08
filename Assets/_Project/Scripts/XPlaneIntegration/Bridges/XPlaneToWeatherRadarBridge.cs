using System;
using UnityEngine;
using AircraftControl.Core;
using FAA.XPlaneIntegration.Providers;
using WeatherRadar;

namespace FAA.XPlaneIntegration.Bridges
{
    /// <summary>
    /// Bridge component that connects XPlaneWeatherProvider to the WeatherRadar system.
    /// 
    /// This component synchronizes aircraft position data from X-Plane to the weather radar provider,
    /// ensuring the weather radar display reflects the current aircraft position, altitude, and heading.
    /// 
    /// FEATURES:
    /// - Auto-discovers XPlaneWeatherProvider and WeatherRadarProviderBase in scene
    /// - Syncs position (altitude, latitude, longitude, heading) from X-Plane to weather radar
    /// - Optionally triggers weather radar refresh when X-Plane weather updates
    /// - Handles null references gracefully with fallback discovery
    /// 
    /// USAGE:
    /// 1. Add component to a GameObject in the scene
    /// 2. Assign XPlaneWeatherProvider and WeatherRadarProviderBase references (or let auto-find)
    /// 3. Configure update settings (auto-refresh, update interval)
    /// 4. Component will automatically sync position on enable
    /// 
    /// INTEGRATION FLOW:
    /// X-Plane DataRefs → XPlaneWeatherProvider → [This Bridge] → WeatherRadarProviderBase → WeatherRadarPanel
    /// </summary>
    [AddComponentMenu("X-Plane Integration/Bridges/XPlane To Weather Radar Bridge")]
    public class XPlaneToWeatherRadarBridge : MonoBehaviour
    {
        #region Inspector Settings

        [Header("References")]
        [Tooltip("Reference to XPlaneWeatherProvider. Auto-finds if not assigned.")]
        [SerializeField]
        private XPlaneWeatherProvider xPlaneWeatherProvider;

        [Tooltip("Reference to WeatherRadarProviderBase. Auto-finds if not assigned.")]
        [SerializeField]
        private WeatherRadarProviderBase weatherRadarProvider;

        [Tooltip("Reference to AircraftController for own-ship position. Auto-finds if not assigned.")]
        [SerializeField]
        private AircraftController aircraftController;

        [Header("Position Sync Settings")]
        [Tooltip("Enable automatic position synchronization from X-Plane to weather radar")]
        [SerializeField]
        private bool enablePositionSync = true;

        [Tooltip("Minimum interval between position updates (seconds). Prevents excessive updates.")]
        [Range(0.1f, 5f)]
        [SerializeField]
        private float positionUpdateInterval = 0.5f;

        [Tooltip("Minimum altitude change to trigger update (feet)")]
        [SerializeField]
        private float altitudeChangeThreshold = 100f;

        [Tooltip("Minimum heading change to trigger update (degrees)")]
        [Range(1f, 45f)]
        [SerializeField]
        private float headingChangeThreshold = 5f;

        [Header("Weather Refresh Settings")]
        [Tooltip("Trigger weather radar refresh when X-Plane weather data updates")]
        [SerializeField]
        private bool autoRefreshOnWeatherUpdate = true;

        [Tooltip("Minimum interval between weather radar refreshes (seconds)")]
        [Range(1f, 30f)]
        [SerializeField]
        private float weatherRefreshInterval = 5f;

        [Header("Debug")]
        [Tooltip("Show debug information in console")]
        [SerializeField]
        private bool showDebugInfo = false;

        #endregion

        #region Private Fields

        private float _lastAltitude;
        private float _lastHeading;
        private float _lastPositionUpdateTime;
        private float _lastWeatherRefreshTime;
        private bool _isInitialized;
        private bool _wasConnected;

        #endregion

        #region Public Properties

        /// <summary>
        /// Enable or disable position synchronization
        /// </summary>
        public bool EnablePositionSync
        {
            get => enablePositionSync;
            set => enablePositionSync = value;
        }

        /// <summary>
        /// Enable or disable auto-refresh on weather update
        /// </summary>
        public bool AutoRefreshOnWeatherUpdate
        {
            get => autoRefreshOnWeatherUpdate;
            set => autoRefreshOnWeatherUpdate = value;
        }

        /// <summary>
        /// Current position update interval
        /// </summary>
        public float PositionUpdateInterval
        {
            get => positionUpdateInterval;
            set => positionUpdateInterval = Mathf.Max(0.1f, value);
        }

        /// <summary>
        /// Check if bridge is initialized and ready
        /// </summary>
        public bool IsInitialized => _isInitialized;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            FindDependencies();
        }

        private void Start()
        {
            if (ValidateDependencies())
            {
                _isInitialized = true;
                LogStatus("Bridge initialized successfully");
            }
            else
            {
                LogWarning("Bridge initialization incomplete - some dependencies missing");
            }
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            if (!_isInitialized || !enablePositionSync) return;

            if (xPlaneWeatherProvider != null)
            {
                bool isConnected = xPlaneWeatherProvider.IsConnected;

                if (isConnected != _wasConnected)
                {
                    _wasConnected = isConnected;
                    if (isConnected)
                    {
                        LogStatus("X-Plane connected - initiating position sync");
                        ForceSyncPosition();
                    }
                    else
                    {
                        LogStatus("X-Plane disconnected");
                    }
                }

                if (isConnected && Time.time - _lastPositionUpdateTime >= positionUpdateInterval)
                {
                    UpdatePositionFromXPlane();
                }
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Find all required dependencies via Inspector or scene search
        /// </summary>
        private void FindDependencies()
        {
            if (xPlaneWeatherProvider == null)
            {
                xPlaneWeatherProvider = FindObjectOfType<XPlaneWeatherProvider>();
                if (xPlaneWeatherProvider == null)
                {
                    LogWarning("XPlaneWeatherProvider not found in scene. Assign manually or create one.");
                }
            }

            if (weatherRadarProvider == null)
            {
                weatherRadarProvider = FindObjectOfType<WeatherRadarProviderBase>();
                if (weatherRadarProvider == null)
                {
                    LogWarning("WeatherRadarProviderBase not found in scene. Assign manually or create one.");
                }
            }

            if (aircraftController == null)
            {
                aircraftController = FindObjectOfType<AircraftController>();
                if (aircraftController == null)
                {
                    LogWarning("AircraftController not found in scene. Position sync requires a valid aircraft source.");
                }
            }

            if (showDebugInfo)
            {
                LogStatus($"Dependencies found:");
                LogStatus($"  - XPlaneWeatherProvider: {(xPlaneWeatherProvider != null ? "✓" : "✗")}");
                LogStatus($"  - WeatherRadarProvider: {(weatherRadarProvider != null ? "✓" : "✗")}");
                LogStatus($"  - AircraftController: {(aircraftController != null ? "✓" : "✗")}");
            }
        }

        /// <summary>
        /// Validate that required dependencies are available
        /// </summary>
        private bool ValidateDependencies()
        {
            if (xPlaneWeatherProvider == null)
            {
                LogError("No XPlaneWeatherProvider found! Bridge cannot function without X-Plane weather source.");
                return false;
            }

            if (weatherRadarProvider == null)
            {
                LogError("No WeatherRadarProvider found! Bridge cannot function without weather radar target.");
                return false;
            }

            if (aircraftController == null)
            {
                LogError("No AircraftController found! Bridge cannot function without own-ship position source.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Subscribe to XPlaneWeatherProvider events
        /// </summary>
        private void SubscribeToEvents()
        {
            if (xPlaneWeatherProvider != null)
            {
                _wasConnected = xPlaneWeatherProvider.IsConnected;
            }
        }

        /// <summary>
        /// Unsubscribe from all events
        /// </summary>
        private void UnsubscribeFromEvents()
        {
        }

        #endregion

        #region Position Synchronization

        /// <summary>
        /// Update weather radar position from X-Plane data
        /// Called periodically in Update() when connected
        /// </summary>
        private void UpdatePositionFromXPlane()
        {
            if (xPlaneWeatherProvider == null || weatherRadarProvider == null) return;

            if (aircraftController == null || !aircraftController.IsValid)
            {
                LogWarning("Cannot sync position: AircraftController not available or invalid");
                return;
            }

            var state = aircraftController.State;

            float altitudeChange = Mathf.Abs(state.AltitudeFeet - _lastAltitude);
            float headingChange = Mathf.DeltaAngle(_lastHeading, state.Heading);

            bool shouldUpdate = altitudeChange >= altitudeChangeThreshold ||
                               Mathf.Abs(headingChange) >= headingChangeThreshold ||
                               Time.time - _lastPositionUpdateTime >= positionUpdateInterval;

            if (shouldUpdate)
            {
                weatherRadarProvider.SetAircraftPosition(
                    state.AltitudeFeet,
                    (float)state.Latitude,
                    (float)state.Longitude,
                    state.Heading
                );

                _lastAltitude = state.AltitudeFeet;
                _lastHeading = state.Heading;
                _lastPositionUpdateTime = Time.time;

                LogDebug($"Position synced: ALT {state.AltitudeFeet:F0}ft, LAT {state.Latitude:F4}, LON {state.Longitude:F4}, HDG {state.Heading:F0}°");

                if (autoRefreshOnWeatherUpdate &&
                    Time.time - _lastWeatherRefreshTime >= weatherRefreshInterval)
                {
                    TriggerWeatherRadarRefresh();
                }
            }
        }

        /// <summary>
        /// Force immediate position synchronization
        /// </summary>
        public void ForceSyncPosition()
        {
            _lastPositionUpdateTime = 0;
            UpdatePositionFromXPlane();
        }

        #endregion

        #region Weather Radar Refresh

        /// <summary>
        /// Trigger a refresh of the weather radar data
        /// Called when X-Plane weather updates (if autoRefreshOnWeatherUpdate is enabled)
        /// </summary>
        private void TriggerWeatherRadarRefresh()
        {
            if (weatherRadarProvider == null) return;

            try
            {
                weatherRadarProvider.RefreshData();
                _lastWeatherRefreshTime = Time.time;

                LogDebug("Weather radar refresh triggered");
            }
            catch (Exception e)
            {
                LogError($"Failed to refresh weather radar: {e.Message}");
            }
        }

        /// <summary>
        /// Manually trigger weather radar refresh
        /// </summary>
        public void RefreshWeatherRadar()
        {
            TriggerWeatherRadarRefresh();
        }

        #endregion

        #region Helper Methods

        public void SetAircraftController(AircraftController controller)
        {
            aircraftController = controller;
        }

        /// <summary>
        /// Set the XPlaneWeatherProvider reference
        /// </summary>
        public void SetXPlaneWeatherProvider(XPlaneWeatherProvider provider)
        {
            xPlaneWeatherProvider = provider;
            if (provider != null)
            {
                _wasConnected = provider.IsConnected;
            }
        }

        /// <summary>
        /// Set the WeatherRadarProviderBase reference
        /// </summary>
        public void SetWeatherRadarProvider(WeatherRadarProviderBase provider)
        {
            weatherRadarProvider = provider;
        }

        #endregion

        #region Debug Logging

        private void LogStatus(string message)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[XPlaneToWeatherRadarBridge] {message}", this);
            }
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"[XPlaneToWeatherRadarBridge] {message}", this);
        }

        private void LogError(string message)
        {
            Debug.LogError($"[XPlaneToWeatherRadarBridge] {message}", this);
        }

        private void LogDebug(string message)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[XPlaneToWeatherRadarBridge] {message}", this);
            }
        }

        #endregion

        #region Editor Support

#if UNITY_EDITOR
        /// <summary>
        /// Editor context menu: Find all dependencies
        /// </summary>
        [ContextMenu("Find All Dependencies")]
        private void EditorFindDependencies()
        {
            FindDependencies();
            Debug.Log("[XPlaneToWeatherRadarBridge] Dependency search complete", this);
        }

        /// <summary>
        /// Editor context menu: Force position sync
        /// </summary>
        [ContextMenu("Force Position Sync")]
        private void EditorForceSync()
        {
            ForceSyncPosition();
            Debug.Log("[XPlaneToWeatherRadarBridge] Position sync triggered", this);
        }

        /// <summary>
        /// Editor context menu: Refresh weather radar
        /// </summary>
        [ContextMenu("Refresh Weather Radar")]
        private void EditorRefreshRadar()
        {
            RefreshWeatherRadar();
            Debug.Log("[XPlaneToWeatherRadarBridge] Weather radar refresh triggered", this);
        }

        /// <summary>
        /// Editor context menu: Log current status
        /// </summary>
        [ContextMenu("Log Current Status")]
        private void EditorLogStatus()
        {
            Debug.Log("=== XPlaneToWeatherRadarBridge Status ===", this);
            Debug.Log($"Initialized: {_isInitialized}", this);
            Debug.Log($"XPlaneWeatherProvider: {(xPlaneWeatherProvider != null ? "Connected" : "Not Found")}", this);
            Debug.Log($"WeatherRadarProvider: {(weatherRadarProvider != null ? "Connected" : "Not Found")}", this);

            if (xPlaneWeatherProvider != null)
            {
                Debug.Log($"X-Plane Connected: {xPlaneWeatherProvider.IsConnected}", this);
                Debug.Log($"Last Weather Update: {xPlaneWeatherProvider.LastUpdateTime:F2}s", this);
            }

            if (weatherRadarProvider != null)
            {
                Debug.Log($"Radar Status: {weatherRadarProvider.Status}", this);
                Debug.Log($"Radar Altitude: {weatherRadarProvider.Altitude:F0}ft", this);
                Debug.Log($"Radar Position: {weatherRadarProvider.Latitude:F4}, {weatherRadarProvider.Longitude:F4}", this);
                Debug.Log($"Radar Heading: {weatherRadarProvider.Heading:F0}°", this);
            }
        }
#endif

        #endregion
    }
}
