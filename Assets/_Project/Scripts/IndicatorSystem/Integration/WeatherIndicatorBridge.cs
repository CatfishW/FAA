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
        private FAA.XPlaneIntegration.Runtime.XPlane12ApiHudBridge _feed;
        private float _lastTextureTime = -100f;
        private readonly StableWeatherCueSelector _cueSelector = new StableWeatherCueSelector();
        private bool _hasCueReference;
        private double _cueReferenceLatitude, _cueReferenceLongitude;
        private Vector2 _cueAircraftOffsetNM;
        private float _scannedRange = float.NaN, _scannedGain = float.NaN;
        public string SourceStatus { get; private set; } = "WAITING FOR DATA";
        public int TargetCount => _weatherTargets.Count;
        public bool IsIllustrative => _lastRadarTexture != null &&
            _lastRadarTexture.name.StartsWith("FAAProceduralWeatherRadar", System.StringComparison.Ordinal);
        
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
            bool controlsChanged = !Mathf.Approximately(_scannedRange, weatherProvider.RangeNM) ||
                !Mathf.Approximately(_scannedGain, weatherProvider.GainDB);
            if (Time.unscaledTime >= _nextUpdateTime || controlsChanged)
            {
                UpdateWeatherIndicators();
                _nextUpdateTime = Time.unscaledTime + Mathf.Max(1f, updateInterval);
                _scannedRange = weatherProvider.RangeNM;
                _scannedGain = weatherProvider.GainDB;
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
            if (_feed == null)
                _feed = FindAnyObjectByType<FAA.XPlaneIntegration.Runtime.XPlane12ApiHudBridge>();
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
            if (_isConnected && weatherProvider != null)
                weatherProvider.OnRadarDataUpdated -= OnWeatherDataUpdated;
            _isConnected = false;
            ClearWeatherIndicators();
            SourceStatus = "DISCONNECTED";
            
            Log("Disconnected from WeatherRadarProviderBase");
        }
        
        private void OnWeatherDataUpdated(Texture2D radarTexture)
        {
            _lastTextureTime = Time.unscaledTime;
            // Store the texture reference for use in updates
            _lastRadarTexture = radarTexture;
            
            // Process on the bounded scan cadence, not once here and again in Update.
            // Live texture delivery can be much faster than a readable cue refresh.
        }
        
        private void UpdateWeatherIndicators()
        {
            if (indicatorController == null || weatherProvider == null)
                return;

            if (_lastRadarTexture == null && _originalDisplay != null)
            {
                _lastRadarTexture = _originalDisplay.CurrentTexture as Texture2D;
            }

            bool hasFreshOriginalTexture = _originalDisplay != null && _originalDisplay.HasFreshTexture;
            bool hasPoweredRadarState = _originalDisplay != null && _originalDisplay.HasRadarPowerState;
            bool isPoweredRadarOn = hasPoweredRadarState
                ? _originalDisplay.IsRadarPowered
                : weatherProvider.Status != ProviderStatus.Inactive;

            bool hasFreshData = _originalDisplay != null ? hasFreshOriginalTexture :
                _lastRadarTexture != null && Time.unscaledTime - _lastTextureTime <= 5f;
            if ((_feed != null && !_feed.IsFeedHealthy) || !hasFreshData)
            {
                SourceStatus = "WAITING FOR DATA";
                ClearWeatherIndicators();
                return;
            }

            if (requirePoweredRadar && !isPoweredRadarOn)
            {
                SourceStatus = "RADAR POWER OFF";
                ClearWeatherIndicators();
                return;
            }
            
            // Get reference position from weather provider
            referenceLatitude = weatherProvider.Latitude;
            referenceLongitude = weatherProvider.Longitude;
            referenceAltitude = weatherProvider.Altitude * 0.3048f; // FT to meters
            indicatorController.SetReferencePosition(referenceLatitude, referenceLongitude, referenceAltitude);
            if (!_hasCueReference)
            {
                _cueReferenceLatitude = referenceLatitude;
                _cueReferenceLongitude = referenceLongitude;
                _hasCueReference = true;
            }
            _cueAircraftOffsetNM = new Vector2(
                Mathf.DeltaAngle((float)_cueReferenceLongitude, (float)referenceLongitude) * 60f * Mathf.Cos((float)_cueReferenceLatitude * Mathf.Deg2Rad),
                (float)(referenceLatitude - _cueReferenceLatitude) * 60f);
            
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
            var cells = new List<WeatherCueSample>();
            
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
                        cells.Add(CreateWeatherSample(gx, gy, intensity, rangeNM));
                    }
                }
            }
            
            float cellNM = rangeNM * 2f / Mathf.Max(4, sampleGridSize);
            foreach (var cell in _cueSelector.Select(cells, cellNM * 1.6f, cellNM * 3.8f, maxWeatherIndicators))
                _weatherTargets.Add(CreateWeatherTarget(cell));
            SourceStatus = _weatherTargets.Count > 0
                ? (IsIllustrative ? "SIM WX · ILLUSTRATIVE" : "RADAR RETURNS") : "NO RAIN RETURNS";
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
            else if (g > 0.26f && g > r * 1.35f && b < g * 0.65f)
                return 0.4f; // Green - light
            
            return 0f;
        }

        private bool IsInsideRadarScope(int px, int py, int width, int height)
        {
            if (IsIllustrative)
            {
                return TryGetFanCoordinates(new Vector2(px / (float)width, py / (float)height),
                    width / (float)height, out _, out _);
            }
            float centerX = (width - 1) * 0.5f;
            float centerY = (height - 1) * 0.5f;
            float radius = Mathf.Min(width, height) * 0.48f;
            float dx = px - centerX;
            float dy = py - centerY;
            return dx * dx + dy * dy <= radius * radius;
        }
        
        private WeatherCueSample CreateWeatherSample(int gridX, int gridY, float intensity, float rangeNM)
        {
            // Convert grid position to geographic offset
            float normalizedX = ((gridX + 0.5f) / sampleGridSize) * 2f - 1f; // -1 to 1
            float normalizedY = ((gridY + 0.5f) / sampleGridSize) * 2f - 1f; // -1 to 1
            
            // X-Plane weather radar textures are heading-up: 0 is ahead, positive is right.
            float distance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY) * rangeNM;
            float relativeBearing = Mathf.Atan2(normalizedX, normalizedY) * Mathf.Rad2Deg;
            if (IsIllustrative)
            {
                TryGetFanCoordinates(new Vector2((gridX + 0.5f) / sampleGridSize,
                    (gridY + 0.5f) / sampleGridSize), _lastRadarTexture.width / (float)_lastRadarTexture.height,
                    out float rangeFraction, out relativeBearing);
                distance = rangeFraction * rangeNM;
            }
            
            float bearing = (weatherProvider.Heading + relativeBearing) * Mathf.Deg2Rad;
            return new WeatherCueSample { PositionNM = _cueAircraftOffsetNM +
                new Vector2(Mathf.Sin(bearing), Mathf.Cos(bearing)) * distance, Intensity = intensity };
        }

        private WeatherIndicatorTarget CreateWeatherTarget(WeatherCueSample cell)
        {
            Vector2 offset = cell.PositionNM - _cueAircraftOffsetNM;
            float distance = offset.magnitude;
            float relativeBearing = Mathf.DeltaAngle(weatherProvider.Heading, Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg);
            float distanceMeters = distance * 1852f;
            Vector3 worldPos = BuildWorldPosition(relativeBearing, distanceMeters);
            
            // Get color based on intensity
            Color color = GetColorForIntensity(cell.Intensity);
            
            return new WeatherIndicatorTarget
            {
                id = cell.Id,
                worldPosition = worldPos,
                displayColor = color,
                priority = cell.Intensity > 0.7f ? 2 : 1,
                label = IsIllustrative ? "SIM WX" : "WX " + GetLabelForIntensity(cell.Intensity),
                distanceNM = distance,
                relativeAltitudeFeet = 0,
                intensity = cell.Intensity,
                illustrative = IsIllustrative
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
                return ScreenIndicatorCalculator.RadarBearingToWorldPosition(
                    distanceMeters / 1852f,
                    (weatherProvider != null ? weatherProvider.Heading : 0f) + relativeBearingDegrees,
                    indicatorVerticalOffsetMeters / 0.3048f,
                    GetPositionReference().position);
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

        /// <summary>Inverse of the shared fan geometry; coordinates are texture-normalized.</summary>
        public static bool TryGetFanCoordinates(Vector2 point, float aspect, out float rangeFraction, out float bearing)
        {
            float x = (point.x - 0.5f) * aspect;
            float y = point.y - XPlaneWeatherRadarGeometry.OriginHeight;
            rangeFraction = new Vector2(x, y).magnitude / XPlaneWeatherRadarGeometry.Radius;
            bearing = Mathf.Atan2(x, y) * Mathf.Rad2Deg;
            return rangeFraction >= 0.10f && rangeFraction <= 0.98f && y >= 0f &&
                Mathf.Abs(bearing) < XPlaneWeatherRadarGeometry.HalfAngle - 2f;
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
            _cueSelector.Clear();
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
        
    }
    
    /// <summary>
    /// Implementation of IIndicatorTarget for weather cells.
    /// </summary>
    public class WeatherIndicatorTarget : IIndicatorTarget, IWeatherIndicatorTarget
    {
        public string id;
        public Vector3 worldPosition;
        public Color displayColor;
        public int priority;
        public string label;
        public float distanceNM;
        public float relativeAltitudeFeet;
        public float intensity;
        public bool illustrative;
        public bool IsIllustrative => illustrative;
        public WeatherCueKind WeatherKind => intensity > .7f ? WeatherCueKind.RainHeavy :
            intensity > .4f ? WeatherCueKind.RainModerate : intensity > 0 ? WeatherCueKind.RainLight : WeatherCueKind.Return;
        
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
