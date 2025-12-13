using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using UnityEngine.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TrafficRadar
{
    /// <summary>
    /// Manages traffic data fetching from Airplanes.live API.
    /// Independent implementation for Traffic Radar System.
    /// </summary>
    public class TrafficRadarDataManager : MonoBehaviour
    {
        [Serializable]
        public class AircraftData
        {   
            public string icao24;
            public string callsign;
            public string originCountry;
            public float longitude;
            public float latitude;
            public float altitude;
            public float velocity;
            public float heading;
            public float verticalRate;
            public bool onGround;
            public DateTime lastUpdateTime;
            public AircraftType type = AircraftType.Unknown;
            
            public AircraftCategory Category 
            {
                get 
                {
                    if (altitude > 8000) return AircraftCategory.HighAltitude;
                    if (altitude > 3000) return AircraftCategory.MidAltitude;
                    if (onGround) return AircraftCategory.Ground;
                    return AircraftCategory.LowAltitude;
                }
            }
            
            public Vector2 GetPredictedPosition(float minutesAhead)
            {
                if (latitude == 0 && longitude == 0) return Vector2.zero;
                float distanceKm = (velocity * 0.0036f) * minutesAhead * 10;
                float headingRad = heading * Mathf.Deg2Rad;
                float latChange = Mathf.Cos(headingRad) * distanceKm / 111.0f;
                float lonChange = Mathf.Sin(headingRad) * distanceKm / (111.0f * Mathf.Cos(latitude * Mathf.Deg2Rad));
                return new Vector2(longitude + lonChange, latitude + latChange);
            }
        }

        public enum AircraftType { Unknown, Commercial, Military, General, Helicopter }
        public enum AircraftCategory { Ground, LowAltitude, MidAltitude, HighAltitude }

        [Serializable]
        public class AircraftDataEvent : UnityEvent<List<AircraftData>> { }

        #region Inspector Fields
        [Header("API Configuration")]
        [SerializeField] private string apiBaseUrl = "https://api.airplanes.live/v2";
        [SerializeField] private string endpointType = "/point";
        
        [Header("Data Refresh Settings")]
        [Range(0.5f, 300f)]
        [SerializeField] private float updateInterval = 5f;
        [SerializeField] public bool autoStartFetching = true;
        [Range(1, 10)]
        [SerializeField] private int maxConsecutiveFailures = 3;

        [Header("Geographic Filter")]
        [SerializeField] public float radiusFilterKm = 100f;
        [SerializeField] public float referenceLatitude = 33.6407f;
        [SerializeField] public float referenceLongitude = -84.4277f;
        
        [Header("Offline Cache")]
        [SerializeField] private bool enableCaching = false;
        [Range(1, 60)]
        [SerializeField] private int maxCacheAgeMinutes = 15;

        [Header("Debug Settings")]
        [SerializeField] private bool verboseLogging = false;

        [Header("Events")]
        [SerializeField] public AircraftDataEvent onDataUpdated = new AircraftDataEvent();
        [SerializeField] private UnityEvent onFetchStarted = new UnityEvent();
        [SerializeField] private UnityEvent onFetchCompleted = new UnityEvent();
        [SerializeField] private UnityEvent onFetchFailed = new UnityEvent();
        #endregion

        #region Private Variables
        public Dictionary<string, AircraftData> aircraftMap = new Dictionary<string, AircraftData>();
        public List<AircraftData> aircraftList = new List<AircraftData>();
        private Coroutine fetchRoutine;
        private int consecutiveFailures = 0;
        private bool isFetching = false;
        private DateTime lastSuccessfulFetch;
        private string lastErrorMessage = "";
        #endregion

        #region Public Properties
        public IReadOnlyList<AircraftData> AircraftList => aircraftList.AsReadOnly();
        public int AircraftCount => aircraftList.Count;
        public bool IsFetching => isFetching;
        public DateTime LastSuccessfulFetch => lastSuccessfulFetch;
        public string LastErrorMessage => lastErrorMessage;
        public bool IsActive => fetchRoutine != null;

        /// <summary>
        /// Radius filter in nautical miles (for UI). Converts to/from km internally.
        /// </summary>
        public float RadiusFilterNM
        {
            get => radiusFilterKm * 0.539957f;
            set
            {
                radiusFilterKm = value * 1.852f; // Convert NM to km
                Log($"Radius filter set to {value} NM ({radiusFilterKm:F1} km)");
            }
        }
        #endregion

        #region Unity Lifecycle Methods
        protected virtual void OnEnable()
        {
            Debug.Log($"[TrafficRadarDataManager] OnEnable - AutoStart: {autoStartFetching}, Pos: ({referenceLatitude:F4}, {referenceLongitude:F4}), Radius: {radiusFilterKm}km");
            
            if (enableCaching && LoadCachedData())
            {
                onDataUpdated?.Invoke(aircraftList);
            }
            
            if (autoStartFetching)
            {
                StartFetching();
            }
            else
            {
                Debug.LogWarning("[TrafficRadarDataManager] Auto-start fetching is DISABLED. Enable it in Inspector or call StartFetching().");
            }
        }

        protected virtual void OnDisable()
        {
            StopFetching();
            if (enableCaching)
            {
                SaveCachedData();
            }
        }
        #endregion

        #region Public Methods
        [ContextMenu("Start Fetching")]
        public virtual void StartFetching()
        {
            Debug.Log($"[TrafficRadarDataManager] StartFetching called");
            if (fetchRoutine != null)
            {
                StopCoroutine(fetchRoutine);
            }
            consecutiveFailures = 0;
            fetchRoutine = StartCoroutine(FetchDataRoutine());
        }

        [ContextMenu("Stop Fetching")]
        public virtual void StopFetching()
        {
            if (fetchRoutine != null)
            {
                StopCoroutine(fetchRoutine);
                fetchRoutine = null;
                isFetching = false;
                Log("Stopped fetching traffic data");
            }
        }

        [ContextMenu("Fetch Now")]
        public virtual void FetchDataNow()
        {
            StartCoroutine(FetchData());
        }

        public void ClearData()
        {
            aircraftMap.Clear();
            aircraftList.Clear();
        }

        public void SetGeographicFilter(float latitude, float longitude, float radiusKm)
        {
            referenceLatitude = latitude;
            referenceLongitude = longitude;
            radiusFilterKm = radiusKm;
            Log($"Geographic filter updated: center ({latitude}, {longitude}), radius {radiusKm}km");
        }

        public void SetReferencePosition(float latitude, float longitude)
        {
            referenceLatitude = latitude;
            referenceLongitude = longitude;
            Log($"Reference position updated: ({latitude}, {longitude}), radius unchanged at {radiusFilterKm}km");
        }

        public AircraftData GetAircraftByIcao(string icao24)
        {
            if (aircraftMap.TryGetValue(icao24.ToLower(), out AircraftData aircraft))
            {
                return aircraft;
            }
            return null;
        }

        public List<AircraftData> GetAircraftInRadius(float latitude, float longitude, float radiusKm)
        {
            List<AircraftData> result = new List<AircraftData>();
            foreach (var aircraft in aircraftList)
            {
                float distance = CalculateDistanceKm(latitude, longitude, aircraft.latitude, aircraft.longitude);
                if (distance <= radiusKm)
                {
                    result.Add(aircraft);
                }
            }
            return result;
        }
        
        public AircraftData GetNearestAircraft(float latitude, float longitude)
        {
            if (aircraftList.Count == 0) return null;
            AircraftData nearest = null;
            float minDistance = float.MaxValue;
            foreach (var aircraft in aircraftList)
            {
                float distance = CalculateDistanceKm(latitude, longitude, aircraft.latitude, aircraft.longitude);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = aircraft;
                }
            }
            return nearest;
        }

        public List<AircraftData> FindAircraft(Func<AircraftData, bool> predicate)
        {
            List<AircraftData> result = new List<AircraftData>();
            foreach (var aircraft in aircraftList)
            {
                if (predicate(aircraft))
                {
                    result.Add(aircraft);
                }
            }
            return result;
        }
        
        public Dictionary<string, string> GetStatusInfo()
        {
            Dictionary<string, string> info = new Dictionary<string, string>();
            info["Total Aircraft"] = aircraftList.Count.ToString();
            info["Last Update"] = lastSuccessfulFetch.ToString("HH:mm:ss");
            info["Update Interval"] = $"{updateInterval}s";
            info["Status"] = IsActive ? "Active" : "Inactive";
            if (radiusFilterKm > 0)
            {
                info["Filter"] = $"{radiusFilterKm}km around {referenceLatitude:F2},{referenceLongitude:F2}";
            }
            else
            {
                info["Filter"] = "None";
            }
            if (!string.IsNullOrEmpty(lastErrorMessage))
            {
                info["Last Error"] = lastErrorMessage;
            }
            return info;
        }
        #endregion

        #region Private Methods
        private IEnumerator FetchDataRoutine()
        {
            while (true)
            {
                yield return StartCoroutine(FetchData());
                
                if (consecutiveFailures > 0)
                {
                    float backoffTime = Mathf.Min(updateInterval * consecutiveFailures, 300f);
                    Log($"Backing off for {backoffTime}s after {consecutiveFailures} consecutive failures");
                    yield return new WaitForSeconds(backoffTime);
                    
                    if (consecutiveFailures >= maxConsecutiveFailures)
                    {
                        Log($"Maximum consecutive failures ({maxConsecutiveFailures}) reached. Pausing data fetching.");
                        fetchRoutine = null;
                        yield break;
                    }
                }
                else
                {
                    yield return new WaitForSeconds(updateInterval);
                }
            }
        }

        private IEnumerator FetchData()
        {
            if (isFetching)
            {
                Log("Skipping fetch - already in progress");
                yield break;
            }

            string url = BuildApiUrl();
            isFetching = true;
            onFetchStarted?.Invoke();
            Debug.Log($"[TrafficRadarDataManager] Fetching from: {url}");

            using (UnityWebRequest request = CreateApiRequest(url))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    HandleFetchError(request.error);
                }
                else
                {
                    try
                    {
                        ProcessResponse(request.downloadHandler.text);
                        consecutiveFailures = 0;
                        lastSuccessfulFetch = DateTime.Now;
                        
                        if (enableCaching)
                        {
                            SaveCachedData();
                        }
                        
                        Debug.Log($"[TrafficRadarDataManager] Fetched {aircraftList.Count} aircraft");
                        onFetchCompleted?.Invoke();
                        onDataUpdated?.Invoke(aircraftList);
                    }
                    catch (Exception e)
                    {
                        HandleFetchError($"Error processing response: {e.Message}");
                    }
                }
            }

            isFetching = false;
        }

        private void HandleFetchError(string error)
        {
            consecutiveFailures++;
            lastErrorMessage = error;
            Debug.LogWarning($"[TrafficRadarDataManager] Fetch error ({consecutiveFailures}): {error}");
            onFetchFailed?.Invoke();
        }

        private string BuildApiUrl()
        {
            float radiusNM = radiusFilterKm * 0.539957f;
            radiusNM = Mathf.Clamp(radiusNM, 1.0f, 250f);

            StringBuilder urlBuilder = new StringBuilder(apiBaseUrl);
            urlBuilder.Append(endpointType);
            urlBuilder.Append($"/{referenceLatitude:F4}");
            urlBuilder.Append($"/{referenceLongitude:F4}");
            urlBuilder.Append($"/{radiusNM:F1}");
            
            return urlBuilder.ToString();
        }

        protected virtual UnityWebRequest CreateApiRequest(string url)
        {
            UnityWebRequest request = UnityWebRequest.Get(url);
            return request;
        }

        private void ProcessResponse(string jsonResponse)
        {
            List<AircraftData> newAircraftList = new List<AircraftData>();
            Dictionary<string, AircraftData> newAircraftMap = new Dictionary<string, AircraftData>();
            
            try
            {
                JObject responseObj = JObject.Parse(jsonResponse);
                JArray aircraftArray = (JArray)responseObj["ac"];

                if (aircraftArray != null && aircraftArray.Count > 0)
                {
                    foreach (JObject acObj in aircraftArray)
                    {
                        AircraftData aircraft = new AircraftData();
                        
                        aircraft.icao24 = acObj["hex"]?.ToString().Trim().ToLower() ?? "";
                        aircraft.callsign = acObj["flight"]?.ToString().Trim() ?? acObj["r"]?.ToString().Trim() ?? "";
                        aircraft.originCountry = "";
                        
                        aircraft.latitude = acObj["lat"]?.Value<float>() ?? 0;
                        aircraft.longitude = acObj["lon"]?.Value<float>() ?? 0;
                        
                        string altStr = acObj["alt_baro"]?.ToString();
                        if (altStr == "ground")
                        {
                            aircraft.altitude = 0;
                            aircraft.onGround = true;
                        }
                        else if (float.TryParse(altStr, out float altFeet))
                        {
                            aircraft.altitude = altFeet * 0.3048f;
                            aircraft.onGround = false;
                        }
                        
                        aircraft.velocity = (acObj["gs"]?.Value<float>() ?? 0) * 0.514444f;
                        aircraft.heading = acObj["track"]?.Value<float>() ?? 0;
                        aircraft.verticalRate = (acObj["baro_rate"]?.Value<float>() ?? 0) * 0.00508f;
                        aircraft.lastUpdateTime = DateTime.Now;
                        
                        string desc = acObj["desc"]?.ToString() ?? "";
                        string category = acObj["category"]?.ToString() ?? "";
                        aircraft.type = DetermineAircraftType(desc, category);
                        
                        if (!string.IsNullOrEmpty(aircraft.icao24))
                        {
                            newAircraftMap[aircraft.icao24] = aircraft;
                            newAircraftList.Add(aircraft);
                        }
                    }
                }
                
                aircraftMap = newAircraftMap;
                aircraftList = newAircraftList;
                
                Log($"Processed {aircraftList.Count} aircraft from API response");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TrafficRadarDataManager] Error processing response: {e.Message}");
                throw;
            }
        }

        private AircraftType DetermineAircraftType(string description, string category)
        {
            string descLower = description.ToLower();
            
            if (descLower.Contains("helicopter") || descLower.Contains("heli"))
                return AircraftType.Helicopter;
            if (descLower.Contains("military") || descLower.Contains("f-") || descLower.Contains("c-17") || descLower.Contains("kc-"))
                return AircraftType.Military;
            if (category == "A5" || category == "A4" || category == "A3")
                return AircraftType.Commercial;
            if (category == "A1" || category == "A2" || descLower.Contains("cessna") || descLower.Contains("piper"))
                return AircraftType.General;
                
            return AircraftType.Unknown;
        }

        private bool LoadCachedData()
        {
            string cacheKey = $"TrafficRadarCache_{referenceLatitude:F2}_{referenceLongitude:F2}";
            if (PlayerPrefs.HasKey(cacheKey))
            {
                try
                {
                    string cachedJson = PlayerPrefs.GetString(cacheKey);
                    JObject cacheObj = JObject.Parse(cachedJson);
                    
                    long timestamp = cacheObj["timestamp"]?.Value<long>() ?? 0;
                    DateTime cacheTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
                    
                    if ((DateTime.Now - cacheTime).TotalMinutes <= maxCacheAgeMinutes)
                    {
                        JArray aircraftArray = (JArray)cacheObj["aircraft"];
                        if (aircraftArray != null)
                        {
                            aircraftList = aircraftArray.ToObject<List<AircraftData>>();
                            foreach (var ac in aircraftList)
                            {
                                aircraftMap[ac.icao24] = ac;
                            }
                            Log($"Loaded {aircraftList.Count} aircraft from cache");
                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[TrafficRadarDataManager] Failed to load cache: {e.Message}");
                }
            }
            return false;
        }

        private void SaveCachedData()
        {
            try
            {
                string cacheKey = $"TrafficRadarCache_{referenceLatitude:F2}_{referenceLongitude:F2}";
                JObject cacheObj = new JObject
                {
                    ["timestamp"] = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    ["aircraft"] = JArray.FromObject(aircraftList)
                };
                PlayerPrefs.SetString(cacheKey, cacheObj.ToString());
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TrafficRadarDataManager] Failed to save cache: {e.Message}");
            }
        }

        public float CalculateDistanceKm(float lat1, float lon1, float lat2, float lon2)
        {
            const float EarthRadiusKm = 6371.0f;
            float dLat = (lat2 - lat1) * Mathf.Deg2Rad;
            float dLon = (lon2 - lon1) * Mathf.Deg2Rad;
            float a = Mathf.Sin(dLat/2) * Mathf.Sin(dLat/2) +
                     Mathf.Cos(lat1 * Mathf.Deg2Rad) * Mathf.Cos(lat2 * Mathf.Deg2Rad) *
                     Mathf.Sin(dLon/2) * Mathf.Sin(dLon/2);
            float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1-a));
            return EarthRadiusKm * c;
        }

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[TrafficRadarDataManager] {message}");
            }
        }
        #endregion
    }
}
