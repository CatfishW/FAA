using System;
using UnityEngine;
using AircraftControl.Core;
using TrafficRadar.Core;
using WeatherRadar;

namespace AircraftControl.Integration
{
    /// <summary>
    /// Bridge component that links aircraft position to all radar systems.
    /// Subscribes to AircraftController position updates and broadcasts to:
    /// - Traffic Radar (via TrafficRadarController)
    /// - Weather Radar (via WeatherRadarProviderBase)
    /// 
    /// Setup:
    /// 1. Add this component to the same GameObject as AircraftController, or separately
    /// 2. Assign references (or leave null for auto-discovery)
    /// 3. Configure update settings
    /// </summary>
    [AddComponentMenu("Aircraft Control/Own Aircraft Radar Bridge")]
    public class OwnAircraftRadarBridge : MonoBehaviour
    {
        #region Inspector Settings
        
        [Header("Aircraft Source")]
        [Tooltip("AircraftController to get position from. If null, will try to find one.")]
        [SerializeField] private AircraftController aircraftController;
        
        [Header("Radar Targets")]
        [Tooltip("Traffic Radar Controller to update. If null, will try to find one.")]
        [SerializeField] private TrafficRadarController trafficRadarController;
        
        [Tooltip("Weather Radar Provider to update. If null, will try to find one.")]
        [SerializeField] private WeatherRadarProviderBase weatherRadarProvider;
        
        [Header("Update Settings")]
        [Tooltip("Minimum interval between radar updates (seconds)")]
        [Range(0.1f, 5f)]
        [SerializeField] private float updateInterval = 0.5f;
        
        [Tooltip("Minimum position change to trigger update (meters)")]
        [SerializeField] private float positionChangeThreshold = 50f;
        
        [Tooltip("Always update at interval regardless of position change")]
        [SerializeField] private bool forcePeriodicUpdate = true;
        
        [Header("Integration Options")]
        [Tooltip("Update Traffic Radar position")]
        [SerializeField] private bool updateTrafficRadar = true;
        
        [Tooltip("Update Weather Radar position")]
        [SerializeField] private bool updateWeatherRadar = true;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        
        #endregion
        
        #region Private Fields
        
        private double _lastLatitude;
        private double _lastLongitude;
        private float _lastAltitude;
        private float _lastUpdateTime;
        private bool _isInitialized;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            FindDependencies();
        }
        
        private void OnEnable()
        {
            SubscribeToAircraftEvents();
        }
        
        private void OnDisable()
        {
            UnsubscribeFromAircraftEvents();
        }
        
        private void Start()
        {
            if (ValidateDependencies())
            {
                _isInitialized = true;
                // Force initial update
                ForceUpdate();
            }
        }
        
        private void Update()
        {
            if (!_isInitialized) return;
            
            // Check for periodic update
            if (forcePeriodicUpdate && Time.time - _lastUpdateTime >= updateInterval)
            {
                UpdateRadarSystems();
            }
        }
        
        #endregion
        
        #region Initialization
        
        private void FindDependencies()
        {
            // Find AircraftController
            if (aircraftController == null)
            {
                aircraftController = GetComponent<AircraftController>();
                if (aircraftController == null)
                {
                    aircraftController = FindObjectOfType<AircraftController>();
                }
            }
            
            // Find Traffic Radar Controller
            if (trafficRadarController == null && updateTrafficRadar)
            {
                trafficRadarController = FindObjectOfType<TrafficRadarController>();
            }
            
            // Find Weather Radar Provider
            if (weatherRadarProvider == null && updateWeatherRadar)
            {
                weatherRadarProvider = FindObjectOfType<WeatherRadarProviderBase>();
            }
            
            LogDependencyStatus();
        }
        
        private bool ValidateDependencies()
        {
            if (aircraftController == null)
            {
                Debug.LogError("[OwnAircraftRadarBridge] No AircraftController found! Cannot bridge position.");
                return false;
            }
            
            bool hasAnyRadar = trafficRadarController != null || weatherRadarProvider != null;
            
            if (!hasAnyRadar)
            {
                Debug.LogWarning("[OwnAircraftRadarBridge] No radar systems found. Bridge will be inactive.");
                return false;
            }
            
            return true;
        }
        
        private void LogDependencyStatus()
        {
            if (showDebugInfo)
            {
                Debug.Log($"[OwnAircraftRadarBridge] Dependencies found:");
                Debug.Log($"  - AircraftController: {(aircraftController != null ? "✓" : "✗")}");
                Debug.Log($"  - TrafficRadarController: {(trafficRadarController != null ? "✓" : "✗")}");
                Debug.Log($"  - WeatherRadarProvider: {(weatherRadarProvider != null ? "✓" : "✗")}");
            }
        }
        
        #endregion
        
        #region Event Handling
        
        private void SubscribeToAircraftEvents()
        {
            if (aircraftController != null)
            {
                aircraftController.OnPositionChanged += OnAircraftPositionChanged;
            }
        }
        
        private void UnsubscribeFromAircraftEvents()
        {
            if (aircraftController != null)
            {
                aircraftController.OnPositionChanged -= OnAircraftPositionChanged;
            }
        }
        
        private void OnAircraftPositionChanged(double latitude, double longitude, float altitude)
        {
            // Check if position has changed enough
            float distanceChange = CalculateDistanceMeters(_lastLatitude, _lastLongitude, latitude, longitude);
            float altitudeChange = Mathf.Abs(altitude - _lastAltitude);
            
            if (distanceChange >= positionChangeThreshold || altitudeChange >= 30f)
            {
                UpdateRadarSystems();
            }
        }
        
        #endregion
        
        #region Radar Updates
        
        private void UpdateRadarSystems()
        {
            if (aircraftController == null) return;
            
            var state = aircraftController.State;
            
            _lastLatitude = state.Latitude;
            _lastLongitude = state.Longitude;
            _lastAltitude = state.AltitudeMeters;
            _lastUpdateTime = Time.time;
            
            // Update Traffic Radar
            if (updateTrafficRadar)
            {
                UpdateTrafficRadar(state);
            }
            
            // Update Weather Radar
            if (updateWeatherRadar)
            {
                UpdateWeatherRadar(state);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[OwnAircraftRadarBridge] Position updated: {state.Latitude:F4}, {state.Longitude:F4}, {state.AltitudeFeet:F0}ft, Hdg: {state.Heading:F0}°");
            }
        }
        
        private void UpdateTrafficRadar(AircraftControl.Core.AircraftState state)
        {
            if (trafficRadarController == null) return;
            
            try
            {
                trafficRadarController.SetOwnPosition(
                    state.Latitude,
                    state.Longitude,
                    state.AltitudeMeters,
                    state.Heading
                );
                
                if (showDebugInfo)
                {
                    Debug.Log($"[OwnAircraftRadarBridge] Traffic Radar updated");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[OwnAircraftRadarBridge] Failed to update Traffic Radar: {e.Message}");
            }
        }
        
        private void UpdateWeatherRadar(AircraftControl.Core.AircraftState state)
        {
            if (weatherRadarProvider == null) return;
            
            try
            {
                weatherRadarProvider.SetAircraftPosition(
                    state.AltitudeFeet,  // Weather radar uses feet
                    (float)state.Latitude,
                    (float)state.Longitude,
                    state.Heading
                );
                
                if (showDebugInfo)
                {
                    Debug.Log($"[OwnAircraftRadarBridge] Weather Radar updated");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[OwnAircraftRadarBridge] Failed to update Weather Radar: {e.Message}");
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Force an immediate update to all radar systems
        /// </summary>
        public void ForceUpdate()
        {
            UpdateRadarSystems();
        }
        
        /// <summary>
        /// Set the aircraft controller reference
        /// </summary>
        public void SetAircraftController(AircraftController controller)
        {
            UnsubscribeFromAircraftEvents();
            aircraftController = controller;
            SubscribeToAircraftEvents();
            
            if (controller != null)
            {
                ForceUpdate();
            }
        }
        
        /// <summary>
        /// Set the traffic radar controller reference
        /// </summary>
        public void SetTrafficRadarController(TrafficRadarController controller)
        {
            trafficRadarController = controller;
        }
        
        /// <summary>
        /// Set the weather radar provider reference
        /// </summary>
        public void SetWeatherRadarProvider(WeatherRadarProviderBase provider)
        {
            weatherRadarProvider = provider;
        }
        
        /// <summary>
        /// Enable or disable traffic radar updates
        /// </summary>
        public void SetTrafficRadarEnabled(bool enabled)
        {
            updateTrafficRadar = enabled;
        }
        
        /// <summary>
        /// Enable or disable weather radar updates
        /// </summary>
        public void SetWeatherRadarEnabled(bool enabled)
        {
            updateWeatherRadar = enabled;
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
        
        #endregion
        
        #region Debug
        
#if UNITY_EDITOR
        [ContextMenu("Find All Dependencies")]
        private void EditorFindDependencies()
        {
            FindDependencies();
        }
        
        [ContextMenu("Force Update Radars")]
        private void EditorForceUpdate()
        {
            ForceUpdate();
        }
        
        [ContextMenu("Log Current Status")]
        private void EditorLogStatus()
        {
            if (aircraftController != null)
            {
                var state = aircraftController.State;
                Debug.Log($"=== OwnAircraftRadarBridge Status ===");
                Debug.Log($"Aircraft Position: {state.Latitude:F4}, {state.Longitude:F4}");
                Debug.Log($"Altitude: {state.AltitudeFeet:F0} ft | Heading: {state.Heading:F0}°");
                Debug.Log($"Traffic Radar: {(trafficRadarController != null ? "Connected" : "Not Found")}");
                Debug.Log($"Weather Radar: {(weatherRadarProvider != null ? "Connected" : "Not Found")}");
            }
            else
            {
                Debug.Log("No AircraftController connected");
            }
        }
#endif
        
        #endregion
    }
}
