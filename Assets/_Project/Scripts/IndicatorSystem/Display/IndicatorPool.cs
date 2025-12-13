using System.Collections.Generic;
using UnityEngine;
using IndicatorSystem.Core;
using TrafficRadar;

namespace IndicatorSystem.Display
{
    /// <summary>
    /// Object pool for efficient indicator management.
    /// Supports multiple prefabs based on aircraft type.
    /// </summary>
    public class IndicatorPool : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("Pool Settings")]
        [Tooltip("Initial pool size")]
        [SerializeField] private int initialPoolSize = 10;
        
        [Tooltip("Maximum pool size (auto-expands up to this)")]
        [SerializeField] private int maxPoolSize = 100;
        
        [Tooltip("Parent transform for pooled indicators")]
        [SerializeField] private RectTransform poolContainer;
        
        [Header("Prefabs")]
        [Tooltip("Default prefab (used when no type-specific prefab)")]
        [SerializeField] private GameObject defaultPrefab;
        
        #endregion
        
        #region Private Fields
        
        private IndicatorSettings _settings;
        
        // Separate pools per aircraft type for efficiency
        private readonly Dictionary<TrafficRadarDataManager.AircraftType, Queue<IndicatorElement>> _typedPools = 
            new Dictionary<TrafficRadarDataManager.AircraftType, Queue<IndicatorElement>>();
        
        // Generic pool for non-traffic indicators
        private readonly Queue<IndicatorElement> _genericPool = new Queue<IndicatorElement>();
        
        private readonly Dictionary<string, IndicatorElement> _activeIndicators = new Dictionary<string, IndicatorElement>();
        private readonly Dictionary<string, TrafficRadarDataManager.AircraftType> _activeTypes = 
            new Dictionary<string, TrafficRadarDataManager.AircraftType>();
        
        private int _totalCreated;
        
        #endregion
        
        #region Properties
        
        public int ActiveCount => _activeIndicators.Count;
        public int AvailableCount => CountAvailableInPools();
        public int TotalCreated => _totalCreated;
        public RectTransform Container => poolContainer;
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Initialize the pool with settings.
        /// </summary>
        public void Initialize(IndicatorSettings settings, RectTransform container = null)
        {
            _settings = settings;
            
            if (container != null)
            {
                poolContainer = container;
            }
            else if (poolContainer == null)
            {
                poolContainer = GetComponent<RectTransform>();
            }
            
            // Initialize typed pools
            foreach (TrafficRadarDataManager.AircraftType type in 
                     System.Enum.GetValues(typeof(TrafficRadarDataManager.AircraftType)))
            {
                if (!_typedPools.ContainsKey(type))
                {
                    _typedPools[type] = new Queue<IndicatorElement>();
                }
            }
            
            // Prewarm with default prefab only
            PrewarmPool();
        }
        
        /// <summary>
        /// Get or create an indicator for the given ID and data.
        /// Uses aircraft type to select appropriate prefab.
        /// </summary>
        public IndicatorElement GetIndicator(string id, IndicatorData data)
        {
            // Check if already active
            if (_activeIndicators.TryGetValue(id, out var existing))
            {
                return existing;
            }
            
            IndicatorElement indicator;
            var aircraftType = data.AircraftType;
            bool isTraffic = data.Type == IndicatorType.Traffic;
            
            if (isTraffic && _settings != null && _settings.useCustomPrefabs)
            {
                // Try to get from typed pool
                if (_typedPools.TryGetValue(aircraftType, out var typedPool) && typedPool.Count > 0)
                {
                    indicator = typedPool.Dequeue();
                }
                else if (_totalCreated < maxPoolSize)
                {
                    // Create new with correct prefab
                    indicator = CreateIndicatorForType(aircraftType);
                }
                else
                {
                    Debug.LogWarning($"[IndicatorPool] Pool exhausted! Max: {maxPoolSize}");
                    return null;
                }
                
                _activeTypes[id] = aircraftType;
            }
            else
            {
                // Use generic pool for non-traffic or when custom prefabs disabled
                if (_genericPool.Count > 0)
                {
                    indicator = _genericPool.Dequeue();
                }
                else if (_totalCreated < maxPoolSize)
                {
                    indicator = CreateDefaultIndicator();
                }
                else
                {
                    Debug.LogWarning($"[IndicatorPool] Pool exhausted! Max: {maxPoolSize}");
                    return null;
                }
                
                _activeTypes[id] = TrafficRadarDataManager.AircraftType.Unknown;
            }
            
            indicator.SetVisible(true);
            _activeIndicators[id] = indicator;
            
            return indicator;
        }
        
        /// <summary>
        /// Simplified get for backward compatibility (no type info).
        /// </summary>
        public IndicatorElement GetIndicator(string id)
        {
            if (_activeIndicators.TryGetValue(id, out var existing))
            {
                return existing;
            }
            
            IndicatorElement indicator;
            if (_genericPool.Count > 0)
            {
                indicator = _genericPool.Dequeue();
            }
            else if (_totalCreated < maxPoolSize)
            {
                indicator = CreateDefaultIndicator();
            }
            else
            {
                return null;
            }
            
            indicator.SetVisible(true);
            _activeIndicators[id] = indicator;
            _activeTypes[id] = TrafficRadarDataManager.AircraftType.Unknown;
            
            return indicator;
        }
        
        /// <summary>
        /// Return an indicator to the pool.
        /// </summary>
        public void ReleaseIndicator(string id)
        {
            if (_activeIndicators.TryGetValue(id, out var indicator))
            {
                indicator.Reset();
                
                // Return to correct pool
                if (_activeTypes.TryGetValue(id, out var type))
                {
                    if (_typedPools.TryGetValue(type, out var pool))
                    {
                        pool.Enqueue(indicator);
                    }
                    else
                    {
                        _genericPool.Enqueue(indicator);
                    }
                    _activeTypes.Remove(id);
                }
                else
                {
                    _genericPool.Enqueue(indicator);
                }
                
                _activeIndicators.Remove(id);
            }
        }
        
        /// <summary>
        /// Return all active indicators to the pool.
        /// </summary>
        public void ReleaseAll()
        {
            foreach (var kvp in _activeIndicators)
            {
                kvp.Value.Reset();
                
                if (_activeTypes.TryGetValue(kvp.Key, out var type) && 
                    _typedPools.TryGetValue(type, out var pool))
                {
                    pool.Enqueue(kvp.Value);
                }
                else
                {
                    _genericPool.Enqueue(kvp.Value);
                }
            }
            
            _activeIndicators.Clear();
            _activeTypes.Clear();
        }
        
        /// <summary>
        /// Release indicators not in the provided set of IDs.
        /// </summary>
        public void ReleaseExcept(HashSet<string> activeIds)
        {
            var toRelease = new List<string>();
            
            foreach (var kvp in _activeIndicators)
            {
                if (!activeIds.Contains(kvp.Key))
                {
                    toRelease.Add(kvp.Key);
                }
            }
            
            foreach (var id in toRelease)
            {
                ReleaseIndicator(id);
            }
        }
        
        /// <summary>
        /// Get all currently active indicators.
        /// </summary>
        public IEnumerable<IndicatorElement> GetActiveIndicators()
        {
            return _activeIndicators.Values;
        }
        
        /// <summary>
        /// Check if an indicator with the given ID is active.
        /// </summary>
        public bool IsActive(string id)
        {
            return _activeIndicators.ContainsKey(id);
        }
        
        #endregion
        
        #region Private Methods
        
        private void PrewarmPool()
        {
            // Just prewarm with some default indicators
            for (int i = 0; i < initialPoolSize; i++)
            {
                var indicator = CreateDefaultIndicator();
                indicator.Reset();
                _genericPool.Enqueue(indicator);
            }
        }
        
        private IndicatorElement CreateIndicatorForType(TrafficRadarDataManager.AircraftType type)
        {
            GameObject prefab = null;
            
            if (_settings != null)
            {
                prefab = _settings.GetPrefabForAircraftType(type);
            }
            
            return CreateFromPrefab(prefab, GetPrefabName(type));
        }
        
        private IndicatorElement CreateDefaultIndicator()
        {
            GameObject prefab = defaultPrefab;
            
            if (prefab == null && _settings != null)
            {
                prefab = _settings.defaultIndicatorPrefab;
            }
            
            return CreateFromPrefab(prefab, "Indicator");
        }
        
        private IndicatorElement CreateFromPrefab(GameObject prefab, string nameSuffix)
        {
            IndicatorElement indicator;
            
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, poolContainer);
                indicator = instance.GetComponent<IndicatorElement>();
                
                if (indicator == null)
                {
                    indicator = instance.AddComponent<IndicatorElement>();
                }
            }
            else
            {
                indicator = IndicatorElement.CreateDefault(poolContainer);
            }
            
            indicator.Initialize();
            indicator.name = $"{nameSuffix}_{_totalCreated}";
            _totalCreated++;
            
            return indicator;
        }
        
        private string GetPrefabName(TrafficRadarDataManager.AircraftType type)
        {
            switch (type)
            {
                case TrafficRadarDataManager.AircraftType.Commercial: return "Commercial";
                case TrafficRadarDataManager.AircraftType.Military: return "Military";
                case TrafficRadarDataManager.AircraftType.General: return "General";
                case TrafficRadarDataManager.AircraftType.Helicopter: return "Helicopter";
                default: return "Unknown";
            }
        }
        
        private int CountAvailableInPools()
        {
            int count = _genericPool.Count;
            foreach (var pool in _typedPools.Values)
            {
                count += pool.Count;
            }
            return count;
        }
        
        #endregion
        
        #region Static Setup
        
        /// <summary>
        /// Create a pool attached to an existing canvas.
        /// </summary>
        public static IndicatorPool CreateOnCanvas(Canvas targetCanvas, IndicatorSettings settings)
        {
            if (targetCanvas == null)
            {
                Debug.LogError("[IndicatorPool] Target canvas is null!");
                return null;
            }
            
            // Create pool container
            GameObject poolObj = new GameObject("[Indicator Pool]");
            poolObj.transform.SetParent(targetCanvas.transform, false);
            
            RectTransform rt = poolObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            
            IndicatorPool pool = poolObj.AddComponent<IndicatorPool>();
            pool.Initialize(settings, rt);
            
            return pool;
        }
        
        /// <summary>
        /// Create a pool with its own canvas (legacy method).
        /// </summary>
        public static IndicatorPool CreateWithCanvas(Transform parent, IndicatorSettings settings = null)
        {
            // Find or create canvas
            GameObject canvasObj = new GameObject("IndicatorCanvas");
            canvasObj.transform.SetParent(parent, false);
            
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            
            UnityEngine.UI.CanvasScaler scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            // Create pool container
            GameObject poolObj = new GameObject("[Indicator Pool]");
            poolObj.transform.SetParent(canvasObj.transform, false);
            
            RectTransform rt = poolObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            
            IndicatorPool pool = poolObj.AddComponent<IndicatorPool>();
            pool.Initialize(settings, rt);
            
            return pool;
        }
        
        #endregion
    }
}
