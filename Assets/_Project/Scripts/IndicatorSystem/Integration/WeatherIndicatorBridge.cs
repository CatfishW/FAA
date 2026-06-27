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
        [SerializeField] private float minIntensityThreshold = 0.18f;
        
        [Tooltip("Sample grid resolution for cell detection")]
        [Range(4, 32)]
        [SerializeField] private int sampleGridSize = 24;
        
        [Tooltip("Maximum weather indicators to show")]
        [Range(1, 20)]
        [SerializeField] private int maxWeatherIndicators = 10;
        
        [Header("Update Settings")]
        [Tooltip("How often to scan for weather cells (seconds)")]
        [Range(0.25f, 30f)]
        [SerializeField] private float updateInterval = 1.5f;

        [Tooltip("Remove weather indicators when the X-Plane EFIS weather radar is off.")]
        [SerializeField] private bool requirePoweredRadar = true;

        [Tooltip("Create a stable X-Plane weather indicator from EFIS weather state when no precipitation return pixels are present.")]
        [SerializeField] private bool showPoweredRadarFallback = false;

        [Tooltip("Fallback indicator distance in nautical miles when EFIS weather is on but the source texture has no active cells.")]
        [Range(2f, 80f)]
        [SerializeField] private float poweredRadarFallbackDistanceNM = 12f;

        [Tooltip("Fallback indicator bearing relative to aircraft heading. Positive values place it to the right.")]
        [Range(-180f, 180f)]
        [SerializeField] private float poweredRadarFallbackRelativeBearing = 35f;

        [Tooltip("Raise weather indicators above the camera horizon so the on/off-screen icon is visible while flying above terrain.")]
        [Range(-20f, 20f)]
        [SerializeField] private float indicatorVerticalOffsetMeters = 6f;

        [Tooltip("World reference transform. If omitted, the bridge uses the main camera, then its own transform.")]
        [SerializeField] private Transform positionReference;

        [Tooltip("Treat weather texture cell positions as heading-up X-Plane radar bearings relative to ownship.")]
        [SerializeField] private bool useRadarRelativeScreenProjection = true;
        
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;
        
        #endregion
        
        #region Private Fields
        
        private readonly List<WeatherIndicatorTarget> _weatherTargets = new List<WeatherIndicatorTarget>();
        private float _nextUpdateTime;
        private bool _isConnected;
        private Texture2D _lastRadarTexture;
        private XPlaneOriginalWeatherRadarDisplay _originalDisplay;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            AutoFindComponents();
        }
        
        private void OnEnable()
        {
            AutoFindComponents();
            Connect();
        }
        
        private void OnDisable()
        {
            Disconnect();
        }
        
        private void Update()
        {
            if (weatherProvider == null || indicatorController == null)
            {
                AutoFindComponents();
                Connect();
            }

            if (weatherProvider == null || indicatorController == null)
                return;

            if (!_isConnected)
            {
                Connect();
            }
            
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
                weatherProvider = FindAnyObjectByType<WeatherRadarProviderBase>();
            }

            if (_originalDisplay == null)
            {
                _originalDisplay = FindAnyObjectByType<XPlaneOriginalWeatherRadarDisplay>();
            }

            if (positionReference == null && Camera.main != null)
            {
                positionReference = Camera.main.transform;
            }
            
            if (indicatorController == null)
            {
                indicatorController = FindAnyObjectByType<IndicatorSystemController>();
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

            if (_lastRadarTexture == null && _originalDisplay != null)
            {
                _lastRadarTexture = _originalDisplay.CurrentTexture as Texture2D;
            }

            bool hasFreshOriginalTexture = _originalDisplay != null && _originalDisplay.HasUsableTexture;
            bool hasPoweredRadarState = _originalDisplay != null && _originalDisplay.HasRadarPowerState;
            bool isPoweredRadarOn = hasPoweredRadarState
                ? _originalDisplay.IsRadarPowered || hasFreshOriginalTexture
                : weatherProvider.Status != ProviderStatus.Inactive;

            if (requirePoweredRadar && !isPoweredRadarOn)
            {
                ClearWeatherIndicators();
                return;
            }
            
            // Get reference position from weather provider
            referenceLatitude = weatherProvider.Latitude;
            referenceLongitude = weatherProvider.Longitude;
            referenceAltitude = weatherProvider.Altitude * 0.3048f; // FT to meters
            indicatorController.SetReferencePosition(referenceLatitude, referenceLongitude, referenceAltitude);
            
            // Clear previous weather targets
            _weatherTargets.Clear();
            
            // Use the cached radar texture
            if (_lastRadarTexture == null)
            {
                if (showPoweredRadarFallback && isPoweredRadarOn)
                {
                    _weatherTargets.Add(CreatePoweredRadarFallbackTarget(weatherProvider.RangeNM));
                    indicatorController.SetTargetsForType(IndicatorType.Weather, _weatherTargets);
                }
                else
                {
                    ClearWeatherIndicators();
                }

                Log("No radar texture available");
                return;
            }
            
            // Sample the radar texture for weather cells
            float rangeNM = weatherProvider.RangeNM;
            DetectWeatherCells(_lastRadarTexture, rangeNM);

            if (_weatherTargets.Count == 0 && showPoweredRadarFallback && isPoweredRadarOn)
            {
                _weatherTargets.Add(CreatePoweredRadarFallbackTarget(rangeNM));
            }
            
            // Replace only weather targets; traffic indicators are managed by their own bridge.
            indicatorController.SetTargetsForType(IndicatorType.Weather, _weatherTargets);
            
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
                    if (!IsInsideRadarScope(px, py, width, height))
                    {
                        continue;
                    }

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
            float r = pixel.r;
            float g = pixel.g;
            float b = pixel.b;
            float max = Mathf.Max(r, g, b);
            float min = Mathf.Min(r, g, b);
            float saturation = max - min;

            if (pixel.a <= 0.08f || max <= 0.16f || saturation <= 0.08f)
            {
                return 0f;
            }
            
            // Higher intensity for red/yellow returns
            if (r > 0.7f && g < 0.35f && b < 0.45f)
                return 1.0f; // Red - severe
            else if (r > 0.58f && g > 0.35f && b < 0.45f)
                return 0.7f; // Yellow/orange - moderate
            else if (g > 0.48f && r < 0.55f)
                return 0.4f; // Green - light
            
            return 0f;
        }

        private bool IsInsideRadarScope(int px, int py, int width, int height)
        {
            float centerX = (width - 1) * 0.5f;
            float centerY = (height - 1) * 0.5f;
            float radius = Mathf.Min(width, height) * 0.48f;
            float dx = px - centerX;
            float dy = py - centerY;
            return dx * dx + dy * dy <= radius * radius;
        }
        
        private WeatherIndicatorTarget CreateWeatherTarget(WeatherCell cell, float rangeNM)
        {
            // Convert grid position to geographic offset
            float normalizedX = ((cell.gridX + 0.5f) / sampleGridSize) * 2f - 1f; // -1 to 1
            float normalizedY = ((cell.gridY + 0.5f) / sampleGridSize) * 2f - 1f; // -1 to 1
            
            // X-Plane weather radar textures are heading-up: 0 is ahead, positive is right.
            float distance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY) * rangeNM;
            float relativeBearing = Mathf.Atan2(normalizedX, normalizedY) * Mathf.Rad2Deg;
            
            float distanceMeters = distance * 1852f; // NM to meters
            Vector3 worldPos = BuildWorldPosition(relativeBearing, distanceMeters);
            
            // Get color based on intensity
            Color color = GetColorForIntensity(cell.intensity);
            
            return new WeatherIndicatorTarget
            {
                id = $"WX_{cell.gridX:00}_{cell.gridY:00}",
                worldPosition = worldPos,
                displayColor = color,
                priority = cell.intensity > 0.7f ? 2 : 1,
                label = GetLabelForIntensity(cell.intensity),
                distanceNM = distance,
                relativeAltitudeFeet = 0,
                intensity = cell.intensity
            };
        }

        private WeatherIndicatorTarget CreatePoweredRadarFallbackTarget(float rangeNM)
        {
            float distanceNM = Mathf.Clamp(poweredRadarFallbackDistanceNM, 2f, Mathf.Max(2f, rangeNM));
            float distanceMeters = distanceNM * 1852f;
            float intensity = 0.36f;

            return new WeatherIndicatorTarget
            {
                id = "WX_POWERED_RADAR",
                worldPosition = BuildWorldPosition(poweredRadarFallbackRelativeBearing, distanceMeters),
                displayColor = GetColorForIntensity(intensity),
                priority = 1,
                label = _originalDisplay != null && _originalDisplay.RadarMode >= 0
                    ? $"WX M{_originalDisplay.RadarMode}"
                    : "WX ON",
                distanceNM = distanceNM,
                relativeAltitudeFeet = 0,
                intensity = intensity
            };
        }

        private Vector3 BuildWorldPosition(float relativeBearingDegrees, float distanceMeters)
        {
            if (useRadarRelativeScreenProjection)
            {
                return ScreenIndicatorCalculator.RadarRelativeMetersToWorldPosition(
                    distanceMeters,
                    relativeBearingDegrees,
                    indicatorVerticalOffsetMeters,
                    GetPositionReference());
            }

            float absoluteBearing = Mathf.Repeat(
                (weatherProvider != null ? weatherProvider.Heading : 0f) + relativeBearingDegrees,
                360f);
            float bearingRad = absoluteBearing * Mathf.Deg2Rad;
            Transform reference = GetPositionReference();
            Vector3 origin = reference != null ? reference.position : Vector3.zero;

            return origin + new Vector3(
                distanceMeters * Mathf.Sin(bearingRad),
                indicatorVerticalOffsetMeters,
                distanceMeters * Mathf.Cos(bearingRad)
            );
        }

        private Transform GetPositionReference()
        {
            if (positionReference != null)
            {
                return positionReference;
            }

            Camera camera = Camera.main ?? FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                positionReference = camera.transform;
                return positionReference;
            }

            return transform;
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

        private void ClearWeatherIndicators()
        {
            _weatherTargets.Clear();
            indicatorController?.SetTargetsForType(IndicatorType.Weather, _weatherTargets);
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
