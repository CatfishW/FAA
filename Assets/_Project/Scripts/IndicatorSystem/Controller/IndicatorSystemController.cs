using System.Collections.Generic;
using UnityEngine;
using IndicatorSystem.Core;
using IndicatorSystem.Display;

namespace IndicatorSystem.Controller
{
    /// <summary>
    /// Main coordinator for the indicator system.
    /// Manages indicator lifecycle and coordinates between data sources and display.
    /// 
    /// Design Principles:
    /// - High Cohesion: Focuses on coordinating indicator display
    /// - Low Coupling: Receives targets via interface, no direct radar dependencies
    /// </summary>
    [AddComponentMenu("Indicator System/Indicator System Controller")]
    public class IndicatorSystemController : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("Settings")]
        [Tooltip("Indicator settings asset. Created automatically if null.")]
        [SerializeField] private IndicatorSettings settings;
        
        [Header("Canvas")]
        [Tooltip("Target canvas for indicators. If null, creates its own overlay canvas.")]
        [SerializeField] private Canvas targetCanvas;
        
        [Header("Camera")]
        [Tooltip("Camera for screen-space calculations. Uses main camera if null.")]
        [SerializeField] private Camera targetCamera;
        
        [Header("Own Position")]
        [Tooltip("Reference latitude for position calculations")]
        [SerializeField] private double referenceLatitude = 33.6407;
        [Tooltip("Reference longitude for position calculations")]
        [SerializeField] private double referenceLongitude = -84.4277;
        [Tooltip("Reference altitude in meters")]
        [SerializeField] private float referenceAltitude = 313f;
        
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;
        
        #endregion
        
        #region Private Fields
        
        private IndicatorPool _pool;
        private readonly Dictionary<string, IIndicatorTarget> _targets = new Dictionary<string, IIndicatorTarget>();
        private readonly List<IndicatorData> _indicatorDataList = new List<IndicatorData>();
        private readonly HashSet<string> _activeIds = new HashSet<string>();
        private IndicatorEdgeConfig _edgeConfig;
        private bool _isInitialized;
        
        #endregion
        
        #region Properties
        
        /// <summary>Current number of active indicators</summary>
        public int ActiveIndicatorCount => _pool?.ActiveCount ?? 0;
        
        /// <summary>Settings asset in use</summary>
        public IndicatorSettings Settings => settings;
        
        /// <summary>Whether the system is initialized and ready</summary>
        public bool IsInitialized => _isInitialized;
        
        /// <summary>Target canvas for indicators</summary>
        public Canvas TargetCanvas => targetCanvas;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            Initialize();
        }
        
        private void OnEnable()
        {
            if (!_isInitialized)
                Initialize();
        }
        
        private void LateUpdate()
        {
            if (!_isInitialized || settings == null || !settings.enabled)
                return;
            
            UpdateIndicators();
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Initialize the indicator system.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;
            
            // Ensure settings exist
            if (settings == null)
            {
                settings = IndicatorSettings.CreateDefault();
                Log("Created default settings");
            }
            
            // Setup camera
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
            
            if (targetCamera == null)
            {
                Debug.LogError("[IndicatorSystem] No camera found! Assign a camera or ensure a MainCamera exists.");
                return;
            }
            
            // Create indicator pool
            if (targetCanvas != null)
            {
                // Use the assigned canvas
                _pool = IndicatorPool.CreateOnCanvas(targetCanvas, settings);
                Log($"Created pool on assigned canvas: {targetCanvas.name}");
            }
            else
            {
                // Create our own canvas
                _pool = IndicatorPool.CreateWithCanvas(transform, settings);
                Log("Created pool with new overlay canvas");
            }
            
            // Cache edge config
            _edgeConfig = settings.GetEdgeConfig();
            
            _isInitialized = true;
            Log("Indicator system initialized");
        }
        
        /// <summary>
        /// Set the target canvas at runtime.
        /// Note: This destroys and recreates the pool.
        /// </summary>
        public void SetTargetCanvas(Canvas canvas)
        {
            if (canvas == targetCanvas)
                return;
            
            targetCanvas = canvas;
            
            // Reinitialize pool if already running
            if (_isInitialized)
            {
                // Destroy old pool
                if (_pool != null)
                {
                    Destroy(_pool.gameObject);
                }
                
                // Create new pool on the canvas
                if (targetCanvas != null)
                {
                    _pool = IndicatorPool.CreateOnCanvas(targetCanvas, settings);
                }
                else
                {
                    _pool = IndicatorPool.CreateWithCanvas(transform, settings);
                }
                
                Log($"Recreated pool on canvas: {(targetCanvas != null ? targetCanvas.name : "new overlay")}");
            }
        }
        
        /// <summary>
        /// Update the reference position for calculations.
        /// </summary>
        public void SetReferencePosition(double lat, double lon, float altMeters)
        {
            referenceLatitude = lat;
            referenceLongitude = lon;
            referenceAltitude = altMeters;
        }
        
        /// <summary>
        /// Add or update a target for indication.
        /// </summary>
        /// <param name="target">Target to display indicator for</param>
        public void AddOrUpdateTarget(IIndicatorTarget target)
        {
            if (target == null || string.IsNullOrEmpty(target.Id))
                return;
            
            _targets[target.Id] = target;
        }
        
        /// <summary>
        /// Add or update multiple targets.
        /// </summary>
        public void AddOrUpdateTargets(IEnumerable<IIndicatorTarget> targets)
        {
            foreach (var target in targets)
            {
                AddOrUpdateTarget(target);
            }
        }
        
        /// <summary>
        /// Replace all targets with a new set.
        /// </summary>
        public void SetTargets(IEnumerable<IIndicatorTarget> targets)
        {
            _targets.Clear();
            AddOrUpdateTargets(targets);
        }
        
        /// <summary>
        /// Remove a target by ID.
        /// </summary>
        public void RemoveTarget(string id)
        {
            if (_targets.Remove(id))
            {
                _pool?.ReleaseIndicator(id);
            }
        }
        
        /// <summary>
        /// Clear all targets and indicators.
        /// </summary>
        public void ClearAll()
        {
            _targets.Clear();
            _pool?.ReleaseAll();
        }
        
        /// <summary>
        /// Force refresh of edge configuration from settings.
        /// </summary>
        public void RefreshSettings()
        {
            if (settings != null)
            {
                _edgeConfig = settings.GetEdgeConfig();
            }
        }
        
        /// <summary>
        /// Set the global opacity for all indicators via settings.
        /// </summary>
        public void SetGlobalOpacity(float alpha)
        {
            if (settings != null)
            {
                settings.globalOpacity = Mathf.Clamp01(alpha);
                Log($"Global opacity set to {alpha:F2}");
            }
        }
        
        /// <summary>
        /// Set the opacity for nearby indicators via settings.
        /// </summary>
        public void SetNearbyOpacity(float alpha, float distanceThresholdNM = -1f)
        {
            if (settings != null)
            {
                settings.nearbyOpacity = Mathf.Clamp01(alpha);
                settings.useProximityOpacity = true;
                
                if (distanceThresholdNM > 0f)
                {
                    settings.nearbyDistanceThresholdNM = distanceThresholdNM;
                }
                
                Log($"Nearby opacity set to {alpha:F2} within {settings.nearbyDistanceThresholdNM:F1} NM");
            }
        }
        
        /// <summary>
        /// Disable proximity-based opacity mode.
        /// </summary>
        public void DisableProximityOpacity()
        {
            if (settings != null)
            {
                settings.useProximityOpacity = false;
                Log("Proximity opacity disabled");
            }
        }
        
        /// <summary>
        /// Toggle proximity-based opacity mode.
        /// </summary>
        public void ToggleProximityOpacity()
        {
            if (settings != null)
            {
                settings.useProximityOpacity = !settings.useProximityOpacity;
                Log($"Proximity opacity: {(settings.useProximityOpacity ? "enabled" : "disabled")}");
            }
        }
        
        /// <summary>
        /// Show all indicators at full opacity.
        /// </summary>
        public void ShowAllIndicators()
        {
            SetGlobalOpacity(1f);
            DisableProximityOpacity();
            
            // Also ensure type visibility is enabled
            if (settings != null)
            {
                settings.showTrafficIndicators = true;
                settings.showWeatherIndicators = true;
                settings.showWaypointIndicators = true;
            }
            
            Log("All indicators shown");
        }
        
        /// <summary>
        /// Hide all indicators by setting opacity to 0.
        /// </summary>
        public void HideAllIndicators()
        {
            SetGlobalOpacity(0f);
            Log("All indicators hidden");
        }
        
        #endregion
        
        #region Private Methods
        
        private void UpdateIndicators()
        {
            if (_pool == null || targetCamera == null)
                return;
            
            _indicatorDataList.Clear();
            _activeIds.Clear();
            
            // Calculate indicator data for each target
            foreach (var kvp in _targets)
            {
                var target = kvp.Value;
                
                // Skip by type if disabled
                if (!ShouldShowType(target.Type))
                    continue;
                
                // Skip by distance
                if (target.DistanceNM < settings.minDisplayDistance)
                    continue;
                
                // Calculate screen-space data
                var data = ScreenIndicatorCalculator.CalculateIndicator(target, targetCamera, _edgeConfig);
                
                if (data.IsActive)
                {
                    _indicatorDataList.Add(data);
                    _activeIds.Add(data.Id);
                }
            }
            
            // Sort by priority (higher priority on top)
            _indicatorDataList.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            
            // Limit to max indicators
            int count = Mathf.Min(_indicatorDataList.Count, settings.maxIndicators);
            
            // Update active indicators
            for (int i = 0; i < count; i++)
            {
                var data = _indicatorDataList[i];
                
                // Pass the data to pool so it can select correct prefab
                var element = _pool.GetIndicator(data.Id, data);
                
                if (element != null)
                {
                    element.UpdateIndicator(data, settings);
                }
            }
            
            // Release indicators for targets no longer present
            _pool.ReleaseExcept(_activeIds);
        }
        
        private bool ShouldShowType(IndicatorType type)
        {
            switch (type)
            {
                case IndicatorType.Traffic:
                    return settings.showTrafficIndicators;
                case IndicatorType.Weather:
                    return settings.showWeatherIndicators;
                case IndicatorType.Waypoint:
                    return settings.showWaypointIndicators;
                default:
                    return true;
            }
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[IndicatorSystem] {message}");
            }
        }
        
        #endregion
        
        #region Context Menu
        
#if UNITY_EDITOR
        [ContextMenu("Debug: Log Status")]
        private void DebugLogStatus()
        {
            Debug.Log($"=== Indicator System Status ===");
            Debug.Log($"Initialized: {_isInitialized}");
            Debug.Log($"Targets: {_targets.Count}");
            Debug.Log($"Active Indicators: {_pool?.ActiveCount ?? 0}");
            Debug.Log($"Pool Available: {_pool?.AvailableCount ?? 0}");
            Debug.Log($"Reference Position: {referenceLatitude:F4}, {referenceLongitude:F4}");
            Debug.Log($"Target Canvas: {(targetCanvas != null ? targetCanvas.name : "Auto-created")}");
            Debug.Log($"Using Custom Prefabs: {(settings != null ? settings.useCustomPrefabs.ToString() : "N/A")}");
        }
        
        [ContextMenu("Debug: Clear All")]
        private void DebugClearAll()
        {
            ClearAll();
        }
        
        [ContextMenu("Debug: Reinitialize")]
        private void DebugReinitialize()
        {
            if (_pool != null)
            {
                Destroy(_pool.gameObject);
            }
            _isInitialized = false;
            _targets.Clear();
            Initialize();
        }
#endif
        
        #endregion
    }
}
