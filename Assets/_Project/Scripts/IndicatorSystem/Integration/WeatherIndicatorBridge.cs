using System.Collections.Generic;
using UnityEngine;
using IndicatorSystem.Core;
using IndicatorSystem.Controller;
using WeatherRadar;

namespace IndicatorSystem.Integration
{
    /// <summary>
    /// Bridge component connecting WeatherRadarProviderBase to the indicator system.
    /// Converts weather radar data to indicators for significant weather cells.
    /// 
    /// Low Coupling: Subscribes to events, no modification to WeatherRadar code.
    /// </summary>
    [AddComponentMenu("Indicator System/Weather Indicator Bridge")]
    public class WeatherIndicatorBridge : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("References")]
        [Tooltip("Weather radar provider to get data from. Auto-finds if null.")]
        [SerializeField] private WeatherRadarProviderBase weatherProvider;
        
        [Tooltip("Indicator system controller. Auto-finds if null.")]
        [SerializeField] private IndicatorSystemController indicatorController;
        
        [Header("Position Reference")]
        [Tooltip("Reference latitude for world position conversion")]
        [SerializeField] private double referenceLatitude = 33.6407;
        [Tooltip("Reference longitude for world position conversion")]  
        [SerializeField] private double referenceLongitude = -84.4277;
        [Tooltip("Reference altitude in meters")]
        [SerializeField] private float referenceAltitude = 313f;
        
        [Header("Weather Cell Detection")]
        [Tooltip("Minimum intensity (0-1) to show indicator")]
        [Range(0f, 1f)]
        [SerializeField] private float minIntensityThreshold = 0.3f;
        
        [Tooltip("Sample grid resolution for cell detection")]
        [Range(4, 32)]
        [SerializeField] private int sampleGridSize = 16;
        
        [Tooltip("Maximum weather indicators to show")]
        [Range(1, 20)]
        [SerializeField] private int maxWeatherIndicators = 10;
        
        [Header("Update Settings")]
        [Tooltip("How often to scan for weather cells (seconds)")]
        [Range(1f, 30f)]
        [SerializeField] private float updateInterval = 5f;
        
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;
        
        #endregion
        
        #region Private Fields
        
        private readonly List<WeatherIndicatorTarget> _weatherTargets = new List<WeatherIndicatorTarget>();
        private float _nextUpdateTime;
        private int _targetIdCounter;
        private bool _isConnected;
        private Texture2D _lastRadarTexture;
        
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
        }
        
        private void Update()
        {
            if (!_isConnected || weatherProvider == null)
                return;
            
            // Periodic update
            if (Time.time >= _nextUpdateTime)
            {
                UpdateWeatherIndicators();
                _nextUpdateTime = Time.time + updateInterval;
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Manually set the reference position.
        /// </summary>
        public void SetReferencePosition(double lat, double lon, float altMeters)
        {
            referenceLatitude = lat;
            referenceLongitude = lon;
            referenceAltitude = altMeters;
        }
        
        /// <summary>
        /// Force immediate weather indicator update.
        /// </summary>
        public void ForceUpdate()
        {
            UpdateWeatherIndicators();
        }
        
        /// <summary>
        /// Reconnect to the weather provider.
        /// </summary>
        public void Reconnect()
        {
            Disconnect();
            AutoFindComponents();
            Connect();
        }
        
        #endregion
        
        #region Private Methods
        
        private void AutoFindComponents()
        {
            if (weatherProvider == null)
            {
                weatherProvider = FindObjectOfType<WeatherRadarProviderBase>();
            }
            
            if (indicatorController == null)
            {
                indicatorController = FindObjectOfType<IndicatorSystemController>();
            }
            
            Log($"Found WeatherProvider: {weatherProvider != null}, IndicatorController: {indicatorController != null}");
        }
        
        private void Connect()
        {
            if (_isConnected || weatherProvider == null)
                return;
            
            // Subscribe to the correct event name
            weatherProvider.OnRadarDataUpdated += OnWeatherDataUpdated;
            _isConnected = true;
            
            Log("Connected to WeatherRadarProviderBase");
        }
        
        private void Disconnect()
        {
            if (!_isConnected || weatherProvider == null)
                return;
            
            weatherProvider.OnRadarDataUpdated -= OnWeatherDataUpdated;
            _isConnected = false;
            
            Log("Disconnected from WeatherRadarProviderBase");
        }
        
        private void OnWeatherDataUpdated(Texture2D radarTexture)
        {
            // Store the texture reference for use in updates
            _lastRadarTexture = radarTexture;
            
            // Update on data change
            UpdateWeatherIndicators();
        }
        
        private void UpdateWeatherIndicators()
        {
            if (indicatorController == null || weatherProvider == null)
                return;
            
            // Get reference position from weather provider
            referenceLatitude = weatherProvider.Latitude;
            referenceLongitude = weatherProvider.Longitude;
            referenceAltitude = weatherProvider.Altitude * 0.3048f; // FT to meters
            
            // Clear previous weather targets
            _weatherTargets.Clear();
            
            // Use the cached radar texture
            if (_lastRadarTexture == null)
            {
                Log("No radar texture available");
                return;
            }
            
            // Sample the radar texture for weather cells
            float rangeNM = weatherProvider.RangeNM;
            DetectWeatherCells(_lastRadarTexture, rangeNM);
            
            // Update indicator system
            foreach (var target in _weatherTargets)
            {
                indicatorController.AddOrUpdateTarget(target);
            }
            
            Log($"Updated {_weatherTargets.Count} weather indicators");
        }
        
        private void DetectWeatherCells(Texture2D texture, float rangeNM)
        {
            int width = texture.width;
            int height = texture.height;
            float cellSizeX = width / (float)sampleGridSize;
            float cellSizeY = height / (float)sampleGridSize;
            
            // Sample grid for significant weather
            var cells = new List<WeatherCell>();
            
            for (int gx = 0; gx < sampleGridSize; gx++)
            {
                for (int gy = 0; gy < sampleGridSize; gy++)
                {
                    int px = (int)(gx * cellSizeX + cellSizeX / 2);
                    int py = (int)(gy * cellSizeY + cellSizeY / 2);
                    
                    Color pixel = texture.GetPixel(px, py);
                    float intensity = GetWeatherIntensity(pixel);
                    
                    if (intensity >= minIntensityThreshold)
                    {
                        cells.Add(new WeatherCell
                        {
                            gridX = gx,
                            gridY = gy,
                            intensity = intensity,
                            color = pixel
                        });
                    }
                }
            }
            
            // Sort by intensity and take top N
            cells.Sort((a, b) => b.intensity.CompareTo(a.intensity));
            int count = Mathf.Min(cells.Count, maxWeatherIndicators);
            
            for (int i = 0; i < count; i++)
            {
                var cell = cells[i];
                _weatherTargets.Add(CreateWeatherTarget(cell, rangeNM));
            }
        }
        
        private float GetWeatherIntensity(Color pixel)
        {
            // Simple intensity based on red channel (typical weather color scale)
            // Adjust based on actual weather radar color mapping
            float r = pixel.r;
            float g = pixel.g;
            
            // Higher intensity for red/yellow returns
            if (r > 0.7f && g < 0.3f)
                return 1.0f; // Red - severe
            else if (r > 0.5f)
                return 0.7f; // Yellow/orange - moderate
            else if (g > 0.5f)
                return 0.4f; // Green - light
            else if (pixel.a > 0.1f && (r > 0.1f || g > 0.1f))
                return 0.3f; // Faint return
            
            return 0f;
        }
        
        private WeatherIndicatorTarget CreateWeatherTarget(WeatherCell cell, float rangeNM)
        {
            // Convert grid position to geographic offset
            float normalizedX = (cell.gridX / (float)sampleGridSize) * 2f - 1f; // -1 to 1
            float normalizedY = (cell.gridY / (float)sampleGridSize) * 2f - 1f; // -1 to 1
            
            // Calculate distance and bearing
            float distance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY) * rangeNM;
            float bearing = Mathf.Atan2(normalizedX, normalizedY) * Mathf.Rad2Deg;
            if (bearing < 0) bearing += 360f;
            
            // Convert to world position (simplified - assumes flat earth for short distances)
            float distanceMeters = distance * 1852f; // NM to meters
            float bearingRad = bearing * Mathf.Deg2Rad;
            
            Vector3 worldPos = new Vector3(
                distanceMeters * Mathf.Sin(bearingRad),
                0, // Weather at same altitude
                distanceMeters * Mathf.Cos(bearingRad)
            );
            
            // Get color based on intensity
            Color color = GetColorForIntensity(cell.intensity);
            
            _targetIdCounter++;
            
            return new WeatherIndicatorTarget
            {
                id = $"WX_{_targetIdCounter}",
                worldPosition = worldPos,
                displayColor = color,
                priority = cell.intensity > 0.7f ? 2 : 1,
                label = GetLabelForIntensity(cell.intensity),
                distanceNM = distance,
                relativeAltitudeFeet = 0,
                intensity = cell.intensity
            };
        }
        
        private Color GetColorForIntensity(float intensity)
        {
            if (indicatorController?.Settings != null)
            {
                var settings = indicatorController.Settings;
                if (intensity > 0.7f)
                    return settings.weatherHeavyColor;
                else if (intensity > 0.4f)
                    return settings.weatherModerateColor;
                else
                    return settings.weatherLightColor;
            }
            
            // Fallback colors
            if (intensity > 0.7f)
                return Color.red;
            else if (intensity > 0.4f)
                return Color.yellow;
            else
                return Color.green;
        }
        
        private string GetLabelForIntensity(float intensity)
        {
            if (intensity > 0.7f)
                return "HVY";
            else if (intensity > 0.4f)
                return "MOD";
            else
                return "LGT";
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[WeatherIndicatorBridge] {message}");
            }
        }
        
        #endregion
        
        #region Nested Types
        
        private struct WeatherCell
        {
            public int gridX;
            public int gridY;
            public float intensity;
            public Color color;
        }
        
        #endregion
    }
    
    /// <summary>
    /// Implementation of IIndicatorTarget for weather cells.
    /// </summary>
    public class WeatherIndicatorTarget : IIndicatorTarget
    {
        public string id;
        public Vector3 worldPosition;
        public Color displayColor;
        public int priority;
        public string label;
        public float distanceNM;
        public float relativeAltitudeFeet;
        public float intensity;
        
        // IIndicatorTarget implementation
        public string Id => id;
        public Vector3 WorldPosition => worldPosition;
        public Color DisplayColor => displayColor;
        public int Priority => priority;
        public IndicatorType Type => IndicatorType.Weather;
        public string Label => label;
        public float DistanceNM => distanceNM;
        public float RelativeAltitudeFeet => relativeAltitudeFeet;
        public TrafficRadar.TrafficRadarDataManager.AircraftType AircraftType => TrafficRadar.TrafficRadarDataManager.AircraftType.Unknown;
        public float Heading => 0f; // Weather doesn't have heading
    }
}

