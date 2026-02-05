using UnityEngine;
using System;
using System.Collections.Generic;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Main orchestrator for the Volumetric Weather Visualization System.
    /// Manages data sources, renderers, and coordinates the visualization pipeline.
    /// Designed with high cohesion and low coupling using interfaces.
    /// </summary>
    [DisallowMultipleComponent]
    public class VolumetricWeatherManager : MonoBehaviour
    {
        #region Configuration

        [Header("Configuration")]
        [SerializeField] private WeatherVolumeConfig _config;

        [Header("Volume Settings")]
        [SerializeField] private Transform _volumeOrigin;
        [SerializeField] private float _worldScale = 1f;

        [Header("View Settings")]
        [SerializeField] private WeatherViewMode _viewMode = WeatherViewMode.Perspective3D;

        [Header("Layer Visibility")]
        [SerializeField] private bool _showVolumetricClouds = true;
        [SerializeField] private bool _showIntensityPillars = true;
        [SerializeField] private bool _showLightning = true;
        [SerializeField] private bool _showPrecipitation = true;

        [Header("Debug")]
        [SerializeField] private bool _debugMode = false;

        #endregion

        #region Private Fields

        // Data source (can be simulation, real API, etc.)
        private IWeatherDataSource _dataSource;

        // Renderers
        private List<IVolumetricRenderer> _renderers = new List<IVolumetricRenderer>();
        private List<IWeatherEffectRenderer> _effectRenderers = new List<IWeatherEffectRenderer>();

        // Current data
        private WeatherVolumeData _currentData;
        private WeatherDataMapper _dataMapper;

        // State
        private bool _isInitialized = false;
        private float _lastUpdateTime;

        #endregion

        #region Properties

        public WeatherVolumeConfig Config => _config;
        public WeatherVolumeData CurrentData => _currentData;
        public WeatherViewMode ViewMode => _viewMode;
        public bool IsInitialized => _isInitialized;

        public bool ShowVolumetricClouds
        {
            get => _showVolumetricClouds;
            set { _showVolumetricClouds = value; UpdateLayerVisibility(); }
        }

        public bool ShowIntensityPillars
        {
            get => _showIntensityPillars;
            set { _showIntensityPillars = value; UpdateLayerVisibility(); }
        }

        public bool ShowLightning
        {
            get => _showLightning;
            set { _showLightning = value; UpdateLayerVisibility(); }
        }

        public bool ShowPrecipitation
        {
            get => _showPrecipitation;
            set { _showPrecipitation = value; UpdateLayerVisibility(); }
        }

        #endregion

        #region Events

        public event Action<WeatherVolumeData> OnDataUpdated;
        public event Action<WeatherViewMode> OnViewModeChanged;
        public event Action OnInitialized;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_volumeOrigin == null)
                _volumeOrigin = transform;

            // Create default config if not assigned
            if (_config == null)
            {
                _config = WeatherVolumeConfig.CreateDefault();
                Debug.LogWarning("[VolumetricWeatherManager] No config assigned, using default");
            }
        }

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            SubscribeToDataSource();
        }

        private void OnDisable()
        {
            UnsubscribeFromDataSource();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Update()
        {
            if (!_isInitialized) return;

            // Update data age
            if (_currentData != null)
            {
                _currentData.LastUpdateTime = Time.time - _currentData.DataAge;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the weather manager
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;

            // Create data mapper
            _dataMapper = new WeatherDataMapper(_config);

            // Find and initialize renderers
            FindAndInitializeRenderers();

            // Apply initial visibility
            UpdateLayerVisibility();

            _isInitialized = true;

            if (_debugMode)
                Debug.Log("[VolumetricWeatherManager] Initialized successfully");

            OnInitialized?.Invoke();
        }

        private void FindAndInitializeRenderers()
        {
            // Find volumetric renderers
            var volumetricRenderers = GetComponentsInChildren<IVolumetricRenderer>(true);
            foreach (var renderer in volumetricRenderers)
            {
                renderer.Initialize(_config);
                _renderers.Add(renderer);
                
                if (_debugMode)
                    Debug.Log($"[VolumetricWeatherManager] Found renderer: {renderer.RendererName}");
            }

            // Find effect renderers
            var effectRenderers = GetComponentsInChildren<IWeatherEffectRenderer>(true);
            foreach (var effect in effectRenderers)
            {
                effect.Initialize(_config);
                _effectRenderers.Add(effect);
                
                if (_debugMode)
                    Debug.Log($"[VolumetricWeatherManager] Found effect: {effect.EffectName}");
            }
        }

        #endregion

        #region Data Source Management

        /// <summary>
        /// Set the weather data source
        /// </summary>
        public void SetDataSource(IWeatherDataSource dataSource)
        {
            UnsubscribeFromDataSource();
            _dataSource = dataSource;
            SubscribeToDataSource();

            if (_dataSource != null)
            {
                _dataSource.Initialize();
                
                if (_debugMode)
                    Debug.Log($"[VolumetricWeatherManager] Data source set: {_dataSource.SourceName}");
            }
        }

        /// <summary>
        /// Get the current data source
        /// </summary>
        public IWeatherDataSource GetDataSource()
        {
            return _dataSource;
        }

        private void SubscribeToDataSource()
        {
            if (_dataSource != null)
            {
                _dataSource.OnDataUpdated += HandleDataUpdated;
                _dataSource.OnStatusChanged += HandleStatusChanged;
            }
        }

        private void UnsubscribeFromDataSource()
        {
            if (_dataSource != null)
            {
                _dataSource.OnDataUpdated -= HandleDataUpdated;
                _dataSource.OnStatusChanged -= HandleStatusChanged;
            }
        }

        private void HandleDataUpdated(WeatherVolumeData data)
        {
            UpdateVisualization(data);
        }

        private void HandleStatusChanged(DataSourceStatus status)
        {
            if (_debugMode)
                Debug.Log($"[VolumetricWeatherManager] Data source status: {status}");
        }

        #endregion

        #region Visualization Updates

        /// <summary>
        /// Update visualization with new data
        /// </summary>
        public void UpdateVisualization(WeatherVolumeData data)
        {
            if (data == null) return;

            _currentData = data;
            _lastUpdateTime = Time.time;

            // Update all renderers
            foreach (var renderer in _renderers)
            {
                if (renderer.IsVisible)
                {
                    renderer.UpdateData(data);
                }
            }

            // Update all effect renderers
            foreach (var effect in _effectRenderers)
            {
                if (effect.IsActive)
                {
                    effect.UpdateEffect(data);
                }
            }

            OnDataUpdated?.Invoke(data);

            if (_debugMode)
            {
                var stats = data.CalculateStats();
                Debug.Log($"[VolumetricWeatherManager] Data updated - Cells: {stats.CellCount}, " +
                         $"Coverage: {stats.CoveragePercent:F1}%, Max: {stats.MaxDensity:F2}");
            }
        }

        /// <summary>
        /// Update visualization from a 2D radar texture
        /// </summary>
        public void UpdateFromRadarTexture(Texture2D radarTexture, Vector3 centerPosition, float rangeNM, float heading = 0f)
        {
            if (_dataMapper == null)
            {
                Debug.LogError("[VolumetricWeatherManager] Data mapper not initialized");
                return;
            }

            var volumeData = _dataMapper.ConvertRadarTexture(radarTexture, centerPosition, rangeNM, heading);
            if (volumeData != null)
            {
                UpdateVisualization(volumeData);
            }
        }

        /// <summary>
        /// Force refresh of all visualization
        /// </summary>
        public void RefreshVisualization()
        {
            if (_currentData != null)
            {
                UpdateVisualization(_currentData);
            }
        }

        /// <summary>
        /// Clear all visualization
        /// </summary>
        public void ClearVisualization()
        {
            foreach (var renderer in _renderers)
            {
                renderer.Cleanup();
            }

            foreach (var effect in _effectRenderers)
            {
                effect.Cleanup();
            }

            _currentData?.Dispose();
            _currentData = null;
        }

        #endregion

        #region View Mode Control

        /// <summary>
        /// Set the view mode
        /// </summary>
        public void SetViewMode(WeatherViewMode mode)
        {
            if (_viewMode == mode) return;

            _viewMode = mode;

            foreach (var renderer in _renderers)
            {
                renderer.SetViewMode(mode);
            }

            OnViewModeChanged?.Invoke(mode);

            if (_debugMode)
                Debug.Log($"[VolumetricWeatherManager] View mode: {mode}");
        }

        #endregion

        #region Layer Visibility

        private void UpdateLayerVisibility()
        {
            foreach (var renderer in _renderers)
            {
                if (renderer is ILayeredRenderer layered)
                {
                    layered.SetLayerVisible(RenderLayer.VolumetricClouds, _showVolumetricClouds);
                    layered.SetLayerVisible(RenderLayer.IntensityPillars, _showIntensityPillars);
                    layered.SetLayerVisible(RenderLayer.Lightning, _showLightning);
                    layered.SetLayerVisible(RenderLayer.Precipitation, _showPrecipitation);
                }
                else
                {
                    // Simple visibility toggle for non-layered renderers
                    renderer.IsVisible = _showVolumetricClouds;
                }
            }

            foreach (var effect in _effectRenderers)
            {
                // Match effect to appropriate layer
                if (effect.EffectName.Contains("Lightning"))
                    effect.IsActive = _showLightning;
                else if (effect.EffectName.Contains("Precipitation") || effect.EffectName.Contains("Rain") || effect.EffectName.Contains("Snow"))
                    effect.IsActive = _showPrecipitation;
                else
                    effect.IsActive = true;
            }
        }

        /// <summary>
        /// Set visibility of a specific layer
        /// </summary>
        public void SetLayerVisible(RenderLayer layer, bool visible)
        {
            switch (layer)
            {
                case RenderLayer.VolumetricClouds:
                    ShowVolumetricClouds = visible;
                    break;
                case RenderLayer.IntensityPillars:
                    ShowIntensityPillars = visible;
                    break;
                case RenderLayer.Lightning:
                    ShowLightning = visible;
                    break;
                case RenderLayer.Precipitation:
                    ShowPrecipitation = visible;
                    break;
            }
        }

        #endregion

        #region Renderer Management

        /// <summary>
        /// Register a renderer with the manager
        /// </summary>
        public void RegisterRenderer(IVolumetricRenderer renderer)
        {
            if (!_renderers.Contains(renderer))
            {
                renderer.Initialize(_config);
                _renderers.Add(renderer);
                
                if (_debugMode)
                    Debug.Log($"[VolumetricWeatherManager] Registered renderer: {renderer.RendererName}");
            }
        }

        /// <summary>
        /// Unregister a renderer
        /// </summary>
        public void UnregisterRenderer(IVolumetricRenderer renderer)
        {
            if (_renderers.Contains(renderer))
            {
                renderer.Cleanup();
                _renderers.Remove(renderer);
            }
        }

        /// <summary>
        /// Register an effect renderer
        /// </summary>
        public void RegisterEffect(IWeatherEffectRenderer effect)
        {
            if (!_effectRenderers.Contains(effect))
            {
                effect.Initialize(_config);
                _effectRenderers.Add(effect);
            }
        }

        /// <summary>
        /// Unregister an effect renderer
        /// </summary>
        public void UnregisterEffect(IWeatherEffectRenderer effect)
        {
            if (_effectRenderers.Contains(effect))
            {
                effect.Cleanup();
                _effectRenderers.Remove(effect);
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Convert world position to volume UV coordinates
        /// </summary>
        public Vector3 WorldToVolumeUV(Vector3 worldPos)
        {
            if (_currentData == null) return Vector3.zero;

            var voxel = _currentData.WorldToVoxel(worldPos);
            return new Vector3(
                voxel.x / (float)_currentData.Resolution.x,
                voxel.y / (float)_currentData.Resolution.y,
                voxel.z / (float)_currentData.Resolution.z
            );
        }

        /// <summary>
        /// Get weather intensity at a world position
        /// </summary>
        public float GetIntensityAtPosition(Vector3 worldPos)
        {
            if (_currentData == null) return 0f;

            var voxel = _currentData.WorldToVoxel(worldPos);
            return _currentData.GetDensity(voxel.x, voxel.y, voxel.z);
        }

        /// <summary>
        /// Get weather type at a world position
        /// </summary>
        public WeatherType GetWeatherTypeAtPosition(Vector3 worldPos)
        {
            if (_currentData == null) return WeatherType.Clear;

            var voxel = _currentData.WorldToVoxel(worldPos);
            return _currentData.GetWeatherType(voxel.x, voxel.y, voxel.z);
        }

        /// <summary>
        /// Get volume bounds in world space
        /// </summary>
        public Bounds GetVolumeBounds()
        {
            if (_currentData != null)
                return _currentData.WorldBounds;

            // Default bounds based on config
            float coverage = _config.coverageNM * WeatherVolumeData.METERS_PER_NM;
            float altitude = _config.maxAltitudeFt * WeatherVolumeData.METERS_PER_FT;
            return new Bounds(
                _volumeOrigin.position + Vector3.up * altitude * 0.5f,
                new Vector3(coverage * 2f, altitude, coverage * 2f)
            );
        }

        private void Cleanup()
        {
            ClearVisualization();
            UnsubscribeFromDataSource();
            _isInitialized = false;
        }

        #endregion

        #region Debug

        private void OnDrawGizmos()
        {
            if (!_debugMode) return;

            // Draw volume bounds
            Bounds bounds = GetVolumeBounds();
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            // Draw weather cells
            if (_currentData != null && _currentData.WeatherCells != null)
            {
                foreach (var cell in _currentData.WeatherCells)
                {
                    Gizmos.color = cell.GetIntensityColor();
                    Gizmos.DrawWireCube(cell.Position, cell.Size * 0.5f);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw grid
            if (_config != null && _debugMode)
            {
                Bounds bounds = GetVolumeBounds();
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);

                int gridLines = 8;
                float stepX = bounds.size.x / gridLines;
                float stepZ = bounds.size.z / gridLines;

                for (int i = 0; i <= gridLines; i++)
                {
                    Vector3 startX = bounds.min + Vector3.right * stepX * i;
                    Vector3 endX = startX + Vector3.forward * bounds.size.z;
                    Gizmos.DrawLine(startX, endX);

                    Vector3 startZ = bounds.min + Vector3.forward * stepZ * i;
                    Vector3 endZ = startZ + Vector3.right * bounds.size.x;
                    Gizmos.DrawLine(startZ, endZ);
                }
            }
        }

        #endregion
    }
}
