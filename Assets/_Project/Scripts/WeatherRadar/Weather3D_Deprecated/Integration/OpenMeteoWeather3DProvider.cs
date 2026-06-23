using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Weather data provider that fetches real-time weather data from Open-Meteo API.
    /// Open-Meteo provides free weather data including pressure-level (altitude) information.
    /// No API key is required for non-commercial use.
    /// 
    /// Implements IWeather3DProvider for seamless integration with Weather3DManager.
    /// </summary>
    public class OpenMeteoWeather3DProvider : MonoBehaviour, IWeather3DProvider
    {
        #region Inspector Fields
        
        [Header("Location Settings")]
        [SerializeField] private float latitude = 40.7128f;  // Default: New York City
        [SerializeField] private float longitude = -74.0060f;
        [SerializeField] private bool useAircraftPosition = true;
        
        [Header("Data Settings")]
        [SerializeField] private float refreshIntervalSeconds = 300f; // 5 minutes
        [SerializeField] private float rangeNM = 80f;
        [SerializeField] private bool includePressureLevels = true;
        
        [Header("Conversion Settings")]
        [SerializeField] private float worldScaleMetersPerUnit = 100f;
        [SerializeField] private float altitudeScaleFactor = 0.01f; // Feet to world units
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool logApiResponses = false;
        
        #endregion
        
        #region Private Fields
        
        private Weather3DData currentData;
        private Weather3DProviderStatus status = Weather3DProviderStatus.Inactive;
        private bool isActive = false;
        
        private Vector3 aircraftPosition;
        private float aircraftHeading;
        private float aircraftAltitude;
        
        private Coroutine refreshCoroutine;
        private float lastRefreshTime;
        
        private OpenMeteoResponse lastApiResponse;
        
        #endregion
        
        #region IWeather3DProvider Implementation
        
        public string ProviderName => "Open-Meteo";
        
        public Weather3DProviderStatus Status => status;
        
        public bool IsActive => isActive;
        
        public Weather3DData CurrentData => currentData;
        
        public event Action<Weather3DData> OnDataUpdated;
        public event Action<Weather3DProviderStatus> OnStatusChanged;
        
        public void SetAircraftPosition(Vector3 position, float heading, float altitude)
        {
            aircraftPosition = position;
            aircraftHeading = heading;
            aircraftAltitude = altitude;
            
            if (useAircraftPosition)
            {
                // In a real implementation, you would convert world position to lat/lon
                // For now, we use the Inspector-set coordinates
            }
        }
        
        public void SetRange(float newRangeNM)
        {
            rangeNM = newRangeNM;
        }
        
        public void Activate()
        {
            if (isActive) return;
            
            isActive = true;
            SetStatus(Weather3DProviderStatus.Connecting);
            
            // Initialize data structure
            currentData = new Weather3DData
            {
                gridSize = new Vector3Int(32, 8, 32),
                coverageNM = rangeNM,
                maxAltitudeFt = 45000f
            };
            currentData.InitializeGrid();
            
            // Start refresh coroutine
            if (refreshCoroutine != null)
            {
                StopCoroutine(refreshCoroutine);
            }
            refreshCoroutine = StartCoroutine(RefreshDataRoutine());
            
            if (debugMode)
            {
                Debug.Log($"[OpenMeteoProvider] Activated - Location: {latitude:F4}, {longitude:F4}");
            }
        }
        
        public void Deactivate()
        {
            isActive = false;
            
            if (refreshCoroutine != null)
            {
                StopCoroutine(refreshCoroutine);
                refreshCoroutine = null;
            }
            
            SetStatus(Weather3DProviderStatus.Inactive);
            
            if (debugMode)
            {
                Debug.Log("[OpenMeteoProvider] Deactivated");
            }
        }
        
        public void RefreshData()
        {
            if (!isActive) return;
            StartCoroutine(FetchWeatherData());
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            currentData = new Weather3DData();
        }
        
        private void OnEnable()
        {
            // Auto-activate if set
        }
        
        private void OnDisable()
        {
            Deactivate();
        }
        
        #endregion
        
        #region Data Fetching
        
        private IEnumerator RefreshDataRoutine()
        {
            while (isActive)
            {
                yield return FetchWeatherData();
                yield return new WaitForSeconds(refreshIntervalSeconds);
            }
        }
        
        private IEnumerator FetchWeatherData()
        {
            SetStatus(Weather3DProviderStatus.Connecting);
            
            string url = OpenMeteoUtils.BuildForecastUrl(latitude, longitude, includePressureLevels);
            
            if (debugMode)
            {
                Debug.Log($"[OpenMeteoProvider] Fetching: {url}");
            }
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 30;
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    
                    if (logApiResponses)
                    {
                        Debug.Log($"[OpenMeteoProvider] Response: {json.Substring(0, Mathf.Min(500, json.Length))}...");
                    }
                    
                    try
                    {
                        lastApiResponse = JsonUtility.FromJson<OpenMeteoResponse>(json);
                        ProcessApiResponse(lastApiResponse);
                        SetStatus(Weather3DProviderStatus.Active);
                        lastRefreshTime = Time.time;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[OpenMeteoProvider] Parse error: {e.Message}");
                        SetStatus(Weather3DProviderStatus.Error);
                    }
                }
                else
                {
                    Debug.LogError($"[OpenMeteoProvider] Request failed: {request.error}");
                    SetStatus(Weather3DProviderStatus.Error);
                }
            }
        }
        
        #endregion
        
        #region Data Processing
        
        private void ProcessApiResponse(OpenMeteoResponse response)
        {
            if (response == null || response.hourly == null) 
            {
                SetStatus(Weather3DProviderStatus.NoData);
                return;
            }
            
            // Clear previous data
            currentData.Clear();
            currentData.aircraftPosition = aircraftPosition;
            currentData.aircraftHeading = aircraftHeading;
            currentData.aircraftAltitude = aircraftAltitude;
            currentData.lastUpdateTime = Time.time;
            
            // Find current hour index
            int currentHourIndex = GetCurrentHourIndex(response.hourly.time);
            if (currentHourIndex < 0)
            {
                currentHourIndex = 0;
            }
            
            // Process current weather
            if (response.current != null)
            {
                ProcessCurrentWeather(response.current);
            }
            else if (response.hourly.weather_code != null && currentHourIndex < response.hourly.weather_code.Length)
            {
                // Use hourly data if current not available
                ProcessHourlyWeather(response.hourly, currentHourIndex);
            }
            
            // Process pressure level data for altitude layers
            if (includePressureLevels)
            {
                ProcessPressureLevelData(response.hourly, currentHourIndex);
            }
            
            // Notify subscribers
            currentData.dataAge = 0f;
            OnDataUpdated?.Invoke(currentData);
            
            if (debugMode)
            {
                Debug.Log($"[OpenMeteoProvider] Processed data - Cells: {currentData.weatherCells.Count}, Layers: {currentData.cloudLayers.Count}");
            }
        }
        
        private int GetCurrentHourIndex(string[] times)
        {
            if (times == null || times.Length == 0) return -1;
            
            DateTime now = DateTime.UtcNow;
            
            for (int i = 0; i < times.Length; i++)
            {
                if (DateTime.TryParse(times[i], out DateTime t))
                {
                    if (t >= now) return Mathf.Max(0, i - 1);
                }
            }
            
            return times.Length - 1;
        }
        
        private void ProcessCurrentWeather(OpenMeteoCurrentData current)
        {
            var codeInfo = OpenMeteoUtils.InterpretWeatherCode(current.weather_code);
            
            if (codeInfo.cellType.HasValue)
            {
                // Create a weather cell for current conditions
                WeatherCell3D cell = new WeatherCell3D
                {
                    position = aircraftPosition,
                    size = new Vector3(rangeNM * 1852f * 0.5f, 10000f * altitudeScaleFactor, rangeNM * 1852f * 0.5f),
                    intensity = codeInfo.intensity,
                    cellType = codeInfo.cellType.Value,
                    altitude = 0f,
                    topAltitude = 25000f,
                    hasLightning = codeInfo.hasLightning,
                    turbulenceLevel = codeInfo.turbulenceLevel
                };
                
                currentData.weatherCells.Add(cell);
            }
            
            // Add cloud layer based on coverage
            if (current.cloud_cover > 10f)
            {
                CloudLayer layer = new CloudLayer
                {
                    baseAltitude = 5000f,
                    topAltitude = 15000f,
                    coverage = current.cloud_cover / 100f,
                    layerType = GetCloudLayerType(current.cloud_cover, current.weather_code),
                    tintColor = GetCloudTint(current.weather_code)
                };
                
                currentData.cloudLayers.Add(layer);
            }
        }
        
        private void ProcessHourlyWeather(OpenMeteoHourlyData hourly, int index)
        {
            if (hourly.weather_code == null || index >= hourly.weather_code.Length) return;
            
            int code = hourly.weather_code[index];
            var codeInfo = OpenMeteoUtils.InterpretWeatherCode(code);
            
            float cloudCover = hourly.cloud_cover != null && index < hourly.cloud_cover.Length 
                ? hourly.cloud_cover[index] : 50f;
            
            if (codeInfo.cellType.HasValue)
            {
                WeatherCell3D cell = new WeatherCell3D
                {
                    position = aircraftPosition,
                    size = new Vector3(rangeNM * 1852f * 0.3f, 15000f * altitudeScaleFactor, rangeNM * 1852f * 0.3f),
                    intensity = codeInfo.intensity,
                    cellType = codeInfo.cellType.Value,
                    altitude = 2000f,
                    topAltitude = 30000f,
                    hasLightning = codeInfo.hasLightning,
                    turbulenceLevel = codeInfo.turbulenceLevel
                };
                
                currentData.weatherCells.Add(cell);
            }
            
            // Cloud layers from low/mid/high coverage
            AddCloudLayersFromCoverage(hourly, index);
        }
        
        private void AddCloudLayersFromCoverage(OpenMeteoHourlyData hourly, int index)
        {
            // Low clouds (stratus/cumulus)
            if (hourly.cloud_cover_low != null && index < hourly.cloud_cover_low.Length && hourly.cloud_cover_low[index] > 10f)
            {
                currentData.cloudLayers.Add(new CloudLayer
                {
                    baseAltitude = 2000f,
                    topAltitude = 6500f,
                    coverage = hourly.cloud_cover_low[index] / 100f,
                    layerType = CloudLayerType.Stratus,
                    tintColor = new Color(0.9f, 0.9f, 0.95f, 0.6f)
                });
            }
            
            // Mid-level clouds (altocumulus)
            if (hourly.cloud_cover_mid != null && index < hourly.cloud_cover_mid.Length && hourly.cloud_cover_mid[index] > 10f)
            {
                currentData.cloudLayers.Add(new CloudLayer
                {
                    baseAltitude = 6500f,
                    topAltitude = 20000f,
                    coverage = hourly.cloud_cover_mid[index] / 100f,
                    layerType = CloudLayerType.Altocumulus,
                    tintColor = new Color(0.85f, 0.85f, 0.9f, 0.5f)
                });
            }
            
            // High clouds (cirrus)
            if (hourly.cloud_cover_high != null && index < hourly.cloud_cover_high.Length && hourly.cloud_cover_high[index] > 10f)
            {
                currentData.cloudLayers.Add(new CloudLayer
                {
                    baseAltitude = 20000f,
                    topAltitude = 40000f,
                    coverage = hourly.cloud_cover_high[index] / 100f,
                    layerType = CloudLayerType.Cirrus,
                    tintColor = new Color(0.95f, 0.95f, 1f, 0.3f)
                });
            }
        }
        
        private void ProcessPressureLevelData(OpenMeteoHourlyData hourly, int index)
        {
            // Process each pressure level for altitude-specific weather
            foreach (int level in OpenMeteoUtils.PressureLevels)
            {
                float altitude = OpenMeteoUtils.GetAltitudeForPressure(level);
                float cloudCover = GetPressureLevelCloudCover(hourly, level, index);
                float humidity = GetPressureLevelHumidity(hourly, level, index);
                
                // Create weather cells at altitude if significant cloud cover
                if (cloudCover > 30f)
                {
                    float nextAltitude = GetNextPressureLevelAltitude(level);
                    float layerThickness = (nextAltitude - altitude) * altitudeScaleFactor;
                    
                    // Estimate intensity from humidity and coverage
                    float intensity = (cloudCover / 100f * 0.5f) + (humidity / 100f * 0.3f);
                    
                    WeatherCell3D cell = new WeatherCell3D
                    {
                        position = aircraftPosition + Vector3.up * (altitude * altitudeScaleFactor),
                        size = new Vector3(rangeNM * 500f, layerThickness, rangeNM * 500f),
                        intensity = Mathf.Clamp01(intensity),
                        cellType = intensity > 0.5f ? WeatherCellType.ModerateRain : WeatherCellType.LightRain,
                        altitude = altitude,
                        topAltitude = nextAltitude,
                        turbulenceLevel = EstimateTurbulenceAtLevel(hourly, level, index)
                    };
                    
                    currentData.weatherCells.Add(cell);
                }
            }
        }
        
        private float GetPressureLevelCloudCover(OpenMeteoHourlyData hourly, int level, int index)
        {
            float[] data = level switch
            {
                1000 => hourly.cloud_cover_1000hPa,
                850 => hourly.cloud_cover_850hPa,
                700 => hourly.cloud_cover_700hPa,
                500 => hourly.cloud_cover_500hPa,
                300 => hourly.cloud_cover_300hPa,
                _ => null
            };
            
            return data != null && index < data.Length ? data[index] : 0f;
        }
        
        private float GetPressureLevelHumidity(OpenMeteoHourlyData hourly, int level, int index)
        {
            float[] data = level switch
            {
                1000 => hourly.relative_humidity_1000hPa,
                850 => hourly.relative_humidity_850hPa,
                700 => hourly.relative_humidity_700hPa,
                500 => hourly.relative_humidity_500hPa,
                300 => hourly.relative_humidity_300hPa,
                _ => null
            };
            
            return data != null && index < data.Length ? data[index] : 0f;
        }
        
        private float GetNextPressureLevelAltitude(int currentLevel)
        {
            int[] levels = OpenMeteoUtils.PressureLevels;
            for (int i = 0; i < levels.Length - 1; i++)
            {
                if (levels[i] == currentLevel)
                {
                    return OpenMeteoUtils.GetAltitudeForPressure(levels[i + 1]);
                }
            }
            return 45000f; // Default ceiling
        }
        
        private float EstimateTurbulenceAtLevel(OpenMeteoHourlyData hourly, int level, int index)
        {
            // Simple turbulence estimation based on humidity gradient
            float humidity = GetPressureLevelHumidity(hourly, level, index);
            float cloudCover = GetPressureLevelCloudCover(hourly, level, index);
            
            // Higher cloud cover and humidity variance indicates turbulence
            float turbulence = (cloudCover / 100f * 0.3f) + (humidity > 80 ? 0.2f : 0f);
            
            return Mathf.Clamp01(turbulence);
        }
        
        private CloudLayerType GetCloudLayerType(float coverage, int weatherCode)
        {
            if (weatherCode >= 95) return CloudLayerType.Cumulonimbus;
            if (weatherCode >= 80) return CloudLayerType.Cumulus;
            if (weatherCode >= 61) return CloudLayerType.Nimbostratus;
            if (coverage > 80) return CloudLayerType.Stratus;
            if (coverage > 50) return CloudLayerType.Altocumulus;
            return CloudLayerType.Cirrus;
        }
        
        private Color GetCloudTint(int weatherCode)
        {
            if (weatherCode >= 95) return new Color(0.3f, 0.3f, 0.4f, 0.9f); // Thunderstorm - dark
            if (weatherCode >= 80) return new Color(0.5f, 0.5f, 0.55f, 0.7f); // Showers - gray
            if (weatherCode >= 61) return new Color(0.6f, 0.6f, 0.65f, 0.6f); // Rain - medium gray
            if (weatherCode >= 71) return new Color(0.9f, 0.92f, 0.95f, 0.5f); // Snow - white
            return new Color(0.8f, 0.8f, 0.85f, 0.4f); // Default - light gray
        }
        
        #endregion
        
        #region Utility Methods
        
        private void SetStatus(Weather3DProviderStatus newStatus)
        {
            if (status != newStatus)
            {
                status = newStatus;
                OnStatusChanged?.Invoke(status);
                
                if (debugMode)
                {
                    Debug.Log($"[OpenMeteoProvider] Status: {status}");
                }
            }
        }
        
        /// <summary>
        /// Set location coordinates manually
        /// </summary>
        public void SetLocation(float lat, float lon)
        {
            latitude = lat;
            longitude = lon;
            
            if (isActive)
            {
                RefreshData();
            }
        }
        
        /// <summary>
        /// Get the last API response for debugging
        /// </summary>
        public OpenMeteoResponse GetLastResponse()
        {
            return lastApiResponse;
        }
        
        #endregion
        
        #region Editor Support
        
        private void OnValidate()
        {
            latitude = Mathf.Clamp(latitude, -90f, 90f);
            longitude = Mathf.Clamp(longitude, -180f, 180f);
            refreshIntervalSeconds = Mathf.Max(60f, refreshIntervalSeconds); // Minimum 1 minute
            rangeNM = Mathf.Clamp(rangeNM, 10f, 320f);
        }
        
        #endregion
    }
}
