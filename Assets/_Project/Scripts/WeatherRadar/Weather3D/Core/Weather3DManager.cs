using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Main controller for the 3D Weather Visualization System.
    /// Manages all visualization sub-systems and coordinates data flow.
    /// </summary>
    public class Weather3DManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private Weather3DConfig config;
        
        [Header("Weather Data Source")]
        [SerializeField] private WeatherRadarProviderBase weatherProvider;
        
        [Header("Visualization Components")]
        [SerializeField] private VolumetricCloudRenderer cloudRenderer;
        [SerializeField] private PrecipitationSystem precipitationSystem;
        [SerializeField] private ThunderstormCellRenderer thunderstormRenderer;
        [SerializeField] private TurbulenceIndicator turbulenceIndicator;
        
        [Header("Display Settings")]
        [SerializeField] private Transform displayOrigin;
        [SerializeField] private float displayScale = 0.001f; // World scale factor
        
        [Header("View Mode")]
        [SerializeField] private Weather3DViewMode viewMode = Weather3DViewMode.Perspective3D;
        
        [Header("Layer Visibility")]
        [SerializeField] private bool showClouds = true;
        [SerializeField] private bool showPrecipitation = true;
        [SerializeField] private bool showLightning = true;
        [SerializeField] private bool showTurbulence = true;
        [SerializeField] private bool showHazardPillars = true;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // Current weather data
        private Weather3DData currentData;
        private Texture2D lastRadarTexture;
        private bool isInitialized = false;
        private float lastUpdateTime;

        // Events
        public event Action<Weather3DData> OnDataUpdated;
        public event Action<Weather3DViewMode> OnViewModeChanged;

        #region Public Properties

        public Weather3DConfig Config => config;
        public Weather3DData CurrentData => currentData;
        public Weather3DViewMode ViewMode => viewMode;
        
        public bool ShowClouds
        {
            get => showClouds;
            set
            {
                showClouds = value;
                UpdateLayerVisibility();
            }
        }
        
        public bool ShowPrecipitation
        {
            get => showPrecipitation;
            set
            {
                showPrecipitation = value;
                UpdateLayerVisibility();
            }
        }
        
        public bool ShowLightning
        {
            get => showLightning;
            set
            {
                showLightning = value;
                UpdateLayerVisibility();
            }
        }
        
        public bool ShowTurbulence
        {
            get => showTurbulence;
            set
            {
                showTurbulence = value;
                UpdateLayerVisibility();
            }
        }
        
        public bool ShowHazardPillars
        {
            get => showHazardPillars;
            set
            {
                showHazardPillars = value;
                UpdateLayerVisibility();
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (displayOrigin == null)
            {
                displayOrigin = transform;
            }
            
            // Create default config if not assigned
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<Weather3DConfig>();
            }
        }

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            SubscribeToWeatherProvider();
        }

        private void OnDisable()
        {
            UnsubscribeFromWeatherProvider();
        }

        private void Update()
        {
            if (!isInitialized) return;
            
            // Update data age
            if (currentData != null)
            {
                currentData.dataAge = Time.time - currentData.lastUpdateTime;
            }
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            // Auto-find components if not assigned
            AutoFindComponents();
            
            // Initialize sub-systems
            InitializeSubSystems();
            
            // Apply initial layer visibility
            UpdateLayerVisibility();
            
            isInitialized = true;
            
            if (debugMode)
            {
                Debug.Log("[Weather3DManager] Initialized successfully");
            }
        }

        private void AutoFindComponents()
        {
            if (weatherProvider == null)
            {
                weatherProvider = FindObjectOfType<WeatherRadarProviderBase>();
            }
            
            if (cloudRenderer == null)
            {
                cloudRenderer = GetComponentInChildren<VolumetricCloudRenderer>();
            }
            
            if (precipitationSystem == null)
            {
                precipitationSystem = GetComponentInChildren<PrecipitationSystem>();
            }
            
            if (thunderstormRenderer == null)
            {
                thunderstormRenderer = GetComponentInChildren<ThunderstormCellRenderer>();
            }
            
            if (turbulenceIndicator == null)
            {
                turbulenceIndicator = GetComponentInChildren<TurbulenceIndicator>();
            }
        }

        private void InitializeSubSystems()
        {
            if (cloudRenderer != null)
            {
                cloudRenderer.Initialize(config, this);
            }
            
            if (precipitationSystem != null)
            {
                precipitationSystem.Initialize(config, this);
            }
            
            if (thunderstormRenderer != null)
            {
                thunderstormRenderer.Initialize(config, this);
            }
            
            if (turbulenceIndicator != null)
            {
                turbulenceIndicator.Initialize(config, this);
            }
        }

        #endregion

        #region Weather Provider Integration

        private void SubscribeToWeatherProvider()
        {
            if (weatherProvider != null)
            {
                weatherProvider.OnRadarDataUpdated += OnRadarDataReceived;
                
                if (debugMode)
                {
                    Debug.Log($"[Weather3DManager] Subscribed to {weatherProvider.ProviderName}");
                }
            }
        }

        private void UnsubscribeFromWeatherProvider()
        {
            if (weatherProvider != null)
            {
                weatherProvider.OnRadarDataUpdated -= OnRadarDataReceived;
            }
        }

        private void OnRadarDataReceived(Texture2D radarTexture)
        {
            if (radarTexture == null) return;
            
            lastRadarTexture = radarTexture;
            ProcessRadarData(radarTexture);
        }

        private void ProcessRadarData(Texture2D radarTexture)
        {
            if (config == null || weatherProvider == null) return;
            
            // Get aircraft position from provider
            Vector3 aircraftPos = new Vector3(
                weatherProvider.Longitude * 111320f, // Approximate degrees to meters
                weatherProvider.Altitude * 0.3048f,   // Feet to meters
                weatherProvider.Latitude * 110540f
            );
            
            // Convert 2D radar to 3D data
            currentData = Weather2DTo3DConverter.Convert(
                radarTexture,
                config,
                aircraftPos,
                weatherProvider.Altitude,
                weatherProvider.RangeNM
            );
            
            if (currentData != null)
            {
                currentData.aircraftHeading = weatherProvider.Heading;
                lastUpdateTime = Time.time;
                
                // Update all visualization systems
                UpdateVisualization();
                
                // Notify listeners
                OnDataUpdated?.Invoke(currentData);
                
                if (debugMode)
                {
                    Debug.Log($"[Weather3DManager] Processed radar data: {currentData.weatherCells.Count} cells, {currentData.cloudLayers.Count} layers");
                }
            }
        }

        #endregion

        #region Visualization Updates

        private void UpdateVisualization()
        {
            if (currentData == null) return;
            
            // Update cloud visualization
            if (cloudRenderer != null && showClouds)
            {
                cloudRenderer.UpdateClouds(currentData);
            }
            
            // Update precipitation
            if (precipitationSystem != null && showPrecipitation)
            {
                precipitationSystem.UpdatePrecipitation(currentData);
            }
            
            // Update thunderstorm effects
            if (thunderstormRenderer != null && (showLightning || showHazardPillars))
            {
                thunderstormRenderer.UpdateThunderstorms(currentData);
            }
            
            // Update turbulence indicators
            if (turbulenceIndicator != null && showTurbulence)
            {
                turbulenceIndicator.UpdateTurbulence(currentData);
            }
        }

        private void UpdateLayerVisibility()
        {
            if (cloudRenderer != null)
            {
                cloudRenderer.SetVisible(showClouds);
            }
            
            if (precipitationSystem != null)
            {
                precipitationSystem.SetVisible(showPrecipitation);
            }
            
            if (thunderstormRenderer != null)
            {
                thunderstormRenderer.SetLightningVisible(showLightning);
                thunderstormRenderer.SetPillarsVisible(showHazardPillars);
            }
            
            if (turbulenceIndicator != null)
            {
                turbulenceIndicator.SetVisible(showTurbulence);
            }
        }

        #endregion

        #region View Mode Control

        /// <summary>
        /// Set the display view mode
        /// </summary>
        public void SetViewMode(Weather3DViewMode mode)
        {
            if (viewMode == mode) return;
            
            viewMode = mode;
            ApplyViewMode();
            OnViewModeChanged?.Invoke(mode);
            
            if (debugMode)
            {
                Debug.Log($"[Weather3DManager] View mode changed to: {mode}");
            }
        }

        private void ApplyViewMode()
        {
            // Notify all renderers of view mode change
            if (cloudRenderer != null)
            {
                cloudRenderer.SetViewMode(viewMode);
            }
            
            if (precipitationSystem != null)
            {
                precipitationSystem.SetViewMode(viewMode);
            }
            
            if (thunderstormRenderer != null)
            {
                thunderstormRenderer.SetViewMode(viewMode);
            }
            
            if (turbulenceIndicator != null)
            {
                turbulenceIndicator.SetViewMode(viewMode);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the weather data provider
        /// </summary>
        public void SetWeatherProvider(WeatherRadarProviderBase provider)
        {
            UnsubscribeFromWeatherProvider();
            weatherProvider = provider;
            SubscribeToWeatherProvider();
        }

        /// <summary>
        /// Force refresh of the visualization
        /// </summary>
        public void RefreshVisualization()
        {
            if (lastRadarTexture != null)
            {
                ProcessRadarData(lastRadarTexture);
            }
        }

        /// <summary>
        /// Clear all weather visualization
        /// </summary>
        public void ClearVisualization()
        {
            if (cloudRenderer != null)
            {
                cloudRenderer.Clear();
            }
            
            if (precipitationSystem != null)
            {
                precipitationSystem.Clear();
            }
            
            if (thunderstormRenderer != null)
            {
                thunderstormRenderer.Clear();
            }
            
            if (turbulenceIndicator != null)
            {
                turbulenceIndicator.Clear();
            }
            
            currentData = null;
        }

        /// <summary>
        /// Convert world position to display position
        /// </summary>
        public Vector3 WorldToDisplay(Vector3 worldPos)
        {
            if (displayOrigin == null) return worldPos * displayScale;
            
            Vector3 relative = worldPos - currentData?.aircraftPosition ?? Vector3.zero;
            return displayOrigin.position + relative * displayScale;
        }

        /// <summary>
        /// Get intensity at a specific altitude
        /// </summary>
        public float GetIntensityAtAltitude(float altitudeFt)
        {
            if (currentData == null || currentData.intensityGrid == null)
                return 0f;
            
            int y = Mathf.RoundToInt(altitudeFt / currentData.maxAltitudeFt * currentData.gridSize.y);
            y = Mathf.Clamp(y, 0, currentData.gridSize.y - 1);
            
            float maxIntensity = 0f;
            for (int x = 0; x < currentData.gridSize.x; x++)
            {
                for (int z = 0; z < currentData.gridSize.z; z++)
                {
                    maxIntensity = Mathf.Max(maxIntensity, currentData.GetIntensityAt(x, y, z));
                }
            }
            
            return maxIntensity;
        }

        #endregion

        #region Debug

        private void OnDrawGizmos()
        {
            if (!debugMode || currentData == null) return;
            
            // Draw weather cells
            foreach (var cell in currentData.weatherCells)
            {
                Gizmos.color = cell.GetIntensityColor();
                Vector3 displayPos = WorldToDisplay(cell.position);
                Gizmos.DrawWireCube(displayPos, cell.size * displayScale);
            }
        }

        #endregion
    }

    /// <summary>
    /// Available view modes for 3D weather display
    /// </summary>
    public enum Weather3DViewMode
    {
        PlanView,        // Top-down view
        ProfileView,     // Side/vertical profile view
        Perspective3D    // Full 3D perspective
    }
}
