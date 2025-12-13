using UnityEngine;
using System.Collections.Generic;
using IndicatorSystem.Core;
using IndicatorSystem.Controller;
using WeatherRadar.Weather3D;

namespace IndicatorSystem.Integration
{
    /// <summary>
    /// Bridge connecting Weather3DManager to the indicator system.
    /// Converts 3D weather cells to indicators for thunderstorms and severe weather.
    /// </summary>
    [AddComponentMenu("Indicator System/Weather3D Indicator Bridge")]
    public class Weather3DIndicatorBridge : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("References")]
        [Tooltip("Weather 3D manager to get data from. Auto-finds if null.")]
        [SerializeField] private Weather3DManager weather3DManager;
        
        [Tooltip("Indicator system controller. Auto-finds if null.")]
        [SerializeField] private IndicatorSystemController indicatorController;
        
        [Header("Indicator Settings")]
        [Tooltip("Minimum intensity to show indicator")]
        [Range(0f, 1f)]
        [SerializeField] private float minIntensity = 0.4f;
        
        [Tooltip("Maximum weather indicators to show")]
        [Range(1, 20)]
        [SerializeField] private int maxIndicators = 10;
        
        [Tooltip("Only show thunderstorm/severe cells")]
        [SerializeField] private bool thunderstormsOnly = false;
        
        [Header("Colors")]
        [SerializeField] private Color lightColor = new Color(0f, 0.8f, 0f, 1f);
        [SerializeField] private Color moderateColor = new Color(1f, 1f, 0f, 1f);
        [SerializeField] private Color heavyColor = new Color(1f, 0.5f, 0f, 1f);
        [SerializeField] private Color severeColor = new Color(1f, 0f, 0f, 1f);
        [SerializeField] private Color extremeColor = new Color(1f, 0f, 1f, 1f);
        
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;
        
        #endregion
        
        #region Private Fields
        
        private readonly List<Weather3DIndicatorTarget> _weatherTargets = new List<Weather3DIndicatorTarget>(20);
        private bool _isConnected;
        private int _targetIdCounter;
        
        #endregion
        
        #region Unity Lifecycle

        private void Awake()
        {
            AutoFindComponents();
        }
        
        private void OnEnable()
        {
            Connect();
        }
        
        private void OnDisable()
        {
            Disconnect();
            ClearIndicators();
        }
        
        #endregion
        
        #region Connection
        
        private void AutoFindComponents()
        {
            if (weather3DManager == null)
                weather3DManager = FindObjectOfType<Weather3DManager>();
            
            if (indicatorController == null)
                indicatorController = FindObjectOfType<IndicatorSystemController>();
            
            Log($"Found Weather3DManager: {weather3DManager != null}, IndicatorController: {indicatorController != null}");
        }
        
        private void Connect()
        {
            if (_isConnected || weather3DManager == null) return;
            
            weather3DManager.OnDataUpdated += OnWeatherDataUpdated;
            _isConnected = true;
            
            Log("Connected to Weather3DManager");
        }
        
        private void Disconnect()
        {
            if (!_isConnected || weather3DManager == null) return;
            
            weather3DManager.OnDataUpdated -= OnWeatherDataUpdated;
            _isConnected = false;
            
            Log("Disconnected from Weather3DManager");
        }
        
        #endregion
        
        #region Weather Data Processing
        
        private void OnWeatherDataUpdated(Weather3DData data)
        {
            if (data == null || indicatorController == null) return;
            
            UpdateIndicators(data);
        }
        
        private void UpdateIndicators(Weather3DData data)
        {
            _weatherTargets.Clear();
            
            if (data.weatherCells == null || data.weatherCells.Count == 0)
            {
                ClearIndicators();
                return;
            }
            
            // Filter and sort by intensity
            var filteredCells = new List<WeatherCell3D>();
            foreach (var cell in data.weatherCells)
            {
                if (cell.intensity < minIntensity) continue;
                
                if (thunderstormsOnly && cell.cellType != WeatherCellType.Thunderstorm)
                    continue;
                
                filteredCells.Add(cell);
            }
            
            // Sort by intensity (highest first)
            filteredCells.Sort((a, b) => b.intensity.CompareTo(a.intensity));
            
            // Take top N
            int count = Mathf.Min(filteredCells.Count, maxIndicators);
            
            for (int i = 0; i < count; i++)
            {
                var cell = filteredCells[i];
                var target = CreateIndicatorTarget(cell, data);
                _weatherTargets.Add(target);
                indicatorController.AddOrUpdateTarget(target);
            }
            
            Log($"Updated {_weatherTargets.Count} weather indicators from {data.weatherCells.Count} cells");
        }
        
        private Weather3DIndicatorTarget CreateIndicatorTarget(WeatherCell3D cell, Weather3DData data)
        {
            _targetIdCounter++;
            
            // Calculate relative position to aircraft
            Vector3 relativePos = cell.position - data.aircraftPosition;
            
            // Distance in NM (assuming position in meters)
            float distanceM = new Vector2(relativePos.x, relativePos.z).magnitude;
            float distanceNM = distanceM / 1852f;
            
            // Relative altitude
            float relativeAltFt = cell.altitude - data.aircraftAltitude;
            
            return new Weather3DIndicatorTarget
            {
                id = $"WX3D_{_targetIdCounter}",
                worldPosition = cell.position,
                displayColor = GetColorForIntensity(cell.intensity),
                priority = GetPriorityForCell(cell),
                label = GetLabelForCell(cell),
                distanceNM = distanceNM,
                relativeAltitudeFeet = relativeAltFt,
                intensity = cell.intensity,
                cellType = cell.cellType,
                hasLightning = cell.hasLightning,
                turbulenceLevel = cell.turbulenceLevel
            };
        }
        
        private Color GetColorForIntensity(float intensity)
        {
            if (intensity >= 0.8f) return extremeColor;
            if (intensity >= 0.6f) return severeColor;
            if (intensity >= 0.4f) return heavyColor;
            if (intensity >= 0.2f) return moderateColor;
            return lightColor;
        }
        
        private int GetPriorityForCell(WeatherCell3D cell)
        {
            // Higher priority for severe weather
            if (cell.cellType == WeatherCellType.Thunderstorm || cell.hasLightning)
                return 3;
            if (cell.intensity >= 0.6f)
                return 2;
            return 1;
        }
        
        private string GetLabelForCell(WeatherCell3D cell)
        {
            if (cell.cellType == WeatherCellType.Thunderstorm || cell.hasLightning)
                return "CB"; // Cumulonimbus
            
            if (cell.intensity >= 0.8f) return "EXT";
            if (cell.intensity >= 0.6f) return "SVR";
            if (cell.intensity >= 0.4f) return "HVY";
            if (cell.intensity >= 0.2f) return "MOD";
            return "LGT";
        }
        
        private void ClearIndicators()
        {
            // Remove previous targets from indicator system
            foreach (var target in _weatherTargets)
            {
                indicatorController?.RemoveTarget(target.Id);
            }
            _weatherTargets.Clear();
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
                Debug.Log($"[Weather3DIndicatorBridge] {message}");
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Force refresh of weather indicators.
        /// </summary>
        public void ForceUpdate()
        {
            if (weather3DManager != null && weather3DManager.CurrentData != null)
            {
                UpdateIndicators(weather3DManager.CurrentData);
            }
        }
        
        /// <summary>
        /// Reconnect to the Weather3D manager.
        /// </summary>
        public void Reconnect()
        {
            Disconnect();
            AutoFindComponents();
            Connect();
        }
        
        #endregion
    }
    
    /// <summary>
    /// Indicator target for 3D weather cells.
    /// </summary>
    public class Weather3DIndicatorTarget : IIndicatorTarget
    {
        public string id;
        public Vector3 worldPosition;
        public Color displayColor;
        public int priority;
        public string label;
        public float distanceNM;
        public float relativeAltitudeFeet;
        public float intensity;
        public WeatherCellType cellType;
        public bool hasLightning;
        public float turbulenceLevel;
        
        // IIndicatorTarget implementation
        public string Id => id;
        public Vector3 WorldPosition => worldPosition;
        public Color DisplayColor => displayColor;
        public int Priority => priority;
        public IndicatorType Type => IndicatorType.Weather;
        public string Label => label;
        public float DistanceNM => distanceNM;
        public float RelativeAltitudeFeet => relativeAltitudeFeet;
        public TrafficRadar.TrafficRadarDataManager.AircraftType AircraftType => 
            TrafficRadar.TrafficRadarDataManager.AircraftType.Unknown;
        public float Heading => 0f; // Weather cells don't have heading
    }
}
