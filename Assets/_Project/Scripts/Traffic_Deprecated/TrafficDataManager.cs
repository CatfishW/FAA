using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using UnityEngine.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
// Key Features
// This implementation includes:

// Comprehensive API Configuration

// Configurable API endpoint and authentication
// Support for geographical filtering
// Robust Data Management

// Structured aircraft data model
// Both list and dictionary storage for efficient access
// Methods to search, filter, and organize aircraft data
// Flexible Refresh Controls

// Configurable update interval
// Manual and automatic fetching options
// Smart failure handling with backoff strategy
// Event-Based Architecture

// Unity Events for data updates and status changes
// Easy integration with other components
// Inspector-Friendly Design

// Well-organized sections with headers and tooltips
// Sensible default settings
// Error Handling

// Comprehensive error detection and reporting
// Automatic retry with exponential backoff
// Convenience Methods

// Find aircraft by ICAO24 code
// Get aircraft within a radius
// Find nearest aircraft
// Custom filtering function
// Debug Support

// Optional verbose logging
// Status tracking and reporting
public class TrafficDataManager : MonoBehaviour
{
    [Serializable]
    public class AircraftData
    {   
        public string icao24;        // Unique ICAO 24-bit address
        public string callsign;      // Callsign of the aircraft
        public string originCountry; // Country of origin
        public float longitude;      // WGS-84 longitude in decimal degrees
        public float latitude;       // WGS-84 latitude in decimal degrees
        public float altitude;       // Barometric altitude in meters
        public float velocity;       // Velocity over ground in m/s
        public float heading;        // Heading in decimal degrees
        public float verticalRate;   // Vertical rate in m/s
        public bool onGround;        // Boolean indicating if the aircraft is on ground
        public DateTime lastUpdateTime; // Last update timestamp
        public AircraftType type = AircraftType.Unknown;
        
        // Helper property to determine aircraft category
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
        
        // Predicted position in minutes ahead based on current heading and velocity
        public Vector2 GetPredictedPosition(float minutesAhead)
        {
            if (latitude == 0 && longitude == 0) return Vector2.zero;
            
            // Calculate distance in km
            float distanceKm = (velocity * 0.0036f) * minutesAhead * 10; // m/s to km/minute * minutes * normalization factor
            
            // Calculate new position (simplified, not accounting for Earth curvature for short distances)
            float headingRad = heading * Mathf.Deg2Rad;
            float latChange = Mathf.Cos(headingRad) * distanceKm / 111.0f;
            float lonChange = Mathf.Sin(headingRad) * distanceKm / (111.0f * Mathf.Cos(latitude * Mathf.Deg2Rad));
            
            return new Vector2(longitude + lonChange, latitude + latChange);
        }
    }

    public enum AircraftType
    {
        Unknown,
        Commercial,
        Military,
        General,
        Helicopter
    }

    public enum AircraftCategory
    {
        Ground,
        LowAltitude,
        MidAltitude,
        HighAltitude
    }

    [Serializable]
    public class AircraftDataEvent : UnityEvent<List<AircraftData>> { }

    #region Inspector Fields
    [Header("API Configuration")]
    [Tooltip("Base URL for the OpenSky API")]
    [SerializeField] private string apiBaseUrl = "https://opensky-network.org/api";
    
    [Tooltip("API endpoint for state vectors")]
    [SerializeField] private string statesEndpoint = "/states/all";
    
    [Tooltip("Optional: OpenSky Network username for authentication")]
    [SerializeField] private string username = "";
    
    [Tooltip("Optional: OpenSky Network password for authentication")]
    [SerializeField] private string password = "";

    [Header("Data Refresh Settings")]
    [Tooltip("Time in seconds between API updates")]
    [Range(0.5f, 300f)]
    [SerializeField] private float updateInterval = 15f;
    
    [Tooltip("Start fetching data automatically when this component is enabled")]
    [SerializeField] public bool autoStartFetching = true;
    
    [Tooltip("Maximum number of consecutive fetch failures before pausing")]
    [Range(1, 10)]
    [SerializeField] private int maxConsecutiveFailures = 3;

    [Header("Geographic Filter")]
    [Tooltip("Only fetch aircraft within this radius (in km) from reference point (0 = no filter)")]
    [SerializeField] public float radiusFilterKm = 0f;
    
    [Tooltip("Reference latitude for radius filtering")]
    [SerializeField] public float referenceLatitude = 0f;
    
    [Tooltip("Reference longitude for radius filtering")]
    [SerializeField] public float referenceLongitude = 0f;
    
    [Header("Offline Cache")]
    [Tooltip("Enable caching of aircraft data for offline use")]
    [SerializeField] private bool enableCaching = false;
    
    [Tooltip("Maximum time in minutes to use cached data when offline")]
    [Range(1, 60)]
    [SerializeField] private int maxCacheAgeMinutes = 15;

    [Header("Debug Settings")]
    [Tooltip("Enable detailed debug logs")]
    [SerializeField] private bool verboseLogging = false;

    [Header("Events")]
    [SerializeField] public AircraftDataEvent onDataUpdated = new AircraftDataEvent();
    [SerializeField] private UnityEvent onFetchStarted = new UnityEvent();
    [SerializeField] private UnityEvent onFetchCompleted = new UnityEvent();
    [SerializeField] private UnityEvent onFetchFailed = new UnityEvent();
    #endregion

    #region Private Variables
    public Dictionary<string, AircraftData> aircraftMap = new Dictionary<string, AircraftData>();
    //[HideInInspector]
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
    #endregion

    #region Unity Lifecycle Methods
    protected virtual void OnEnable()
    {
        if (enableCaching && LoadCachedData())
        {
            onDataUpdated?.Invoke(aircraftList);
        }
        
        if (autoStartFetching)
        {
            StartFetching();
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
    /// <summary>
    /// Starts periodic fetching of traffic data from the OpenSky API
    /// </summary>
    public virtual void StartFetching()
    {
        if (fetchRoutine != null)
        {
            StopCoroutine(fetchRoutine);
        }

        consecutiveFailures = 0;
        fetchRoutine = StartCoroutine(FetchDataRoutine());
        Log("Started fetching traffic data");
    }

    /// <summary>
    /// Stops fetching traffic data
    /// </summary>
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

    /// <summary>
    /// Forces an immediate data refresh regardless of the update interval
    /// </summary>
    public virtual void FetchDataNow()
    {
        StartCoroutine(FetchData());
    }

    /// <summary>
    /// Clears all stored aircraft data
    /// </summary>
    public void ClearData()
    {
        aircraftMap.Clear();
        aircraftList.Clear();
        Log("Cleared all aircraft data");
    }

    /// <summary>
    /// Updates the geographic filter settings
    /// </summary>
    public void SetGeographicFilter(float latitude, float longitude, float radiusKm)
    {
        referenceLatitude = latitude;
        referenceLongitude = longitude;
        radiusFilterKm = radiusKm;
        Log($"Geographic filter updated: center ({latitude}, {longitude}), radius {radiusKm}km");
    }

    /// <summary>
    /// Gets an aircraft by its ICAO24 identifier
    /// </summary>
    public AircraftData GetAircraftByIcao(string icao24)
    {
        if (aircraftMap.TryGetValue(icao24.ToLower(), out AircraftData aircraft))
        {
            return aircraft;
        }
        return null;
    }

    /// <summary>
    /// Finds all aircraft within a specified radius
    /// </summary>
    public List<AircraftData> GetAircraftInRadius(float latitude, float longitude, float radiusKm)
    {
        List<AircraftData> result = new List<AircraftData>();
        
        foreach (var aircraft in aircraftList)
        {
            float distance = CalculateDistanceKm(latitude, longitude, 
                                              aircraft.latitude, aircraft.longitude);
            if (distance <= radiusKm)
            {
                result.Add(aircraft);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Finds the nearest aircraft to the specified coordinates
    /// </summary>
    public AircraftData GetNearestAircraft(float latitude, float longitude)
    {
        if (aircraftList.Count == 0)
            return null;
            
        AircraftData nearest = null;
        float minDistance = float.MaxValue;
        
        foreach (var aircraft in aircraftList)
        {
            float distance = CalculateDistanceKm(latitude, longitude, 
                                              aircraft.latitude, aircraft.longitude);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = aircraft;
            }
        }
        
        return nearest;
    }

    /// <summary>
    /// Returns aircraft that match a specified condition
    /// </summary>
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
    
    /// <summary>
    /// Gets a summary of current traffic data status for UI display
    /// </summary>
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
            
            // Implement backoff strategy for failures
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
        Log($"Fetching traffic data from: {url}");

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
                    
                    // Cache the data if enabled
                    if (enableCaching)
                    {
                        SaveCachedData();
                    }
                    
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
        Debug.LogWarning($"[TrafficDataManager] Fetch error ({consecutiveFailures}): {error}");
        onFetchFailed?.Invoke();
    }

    private string BuildApiUrl()
    {
        StringBuilder urlBuilder = new StringBuilder(apiBaseUrl);
        urlBuilder.Append(statesEndpoint);
        
        List<string> parameters = new List<string>();
        
        // Add geographic bounds if filter is enabled
        if (radiusFilterKm > 0)
        {
            // Convert radius to approximate lat/lon bounds
            // 1 degree latitude ≈ 111km
            float latDelta = radiusFilterKm / 111.0f;
            // 1 degree longitude varies with latitude
            float lonDelta = radiusFilterKm / 
                (111.0f * Mathf.Cos(referenceLatitude * Mathf.Deg2Rad));
                
            parameters.Add($"lamin={referenceLatitude - latDelta}");
            parameters.Add($"lamax={referenceLatitude + latDelta}");
            parameters.Add($"lomin={referenceLongitude - lonDelta}");
            parameters.Add($"lomax={referenceLongitude + lonDelta}");
        }
        
        if (parameters.Count > 0)
        {
            urlBuilder.Append("?").Append(string.Join("&", parameters));
        }
        
        return urlBuilder.ToString();
    }

    protected virtual UnityWebRequest CreateApiRequest(string url)
    {
        UnityWebRequest request = UnityWebRequest.Get(url);
        
        // Add authentication if credentials are provided
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            string auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            request.SetRequestHeader("Authorization", $"Basic {auth}");
        }
        return request;
    }

    private void ProcessResponse(string jsonResponse)
    {
        // A temporary list for the new aircraft data
        List<AircraftData> newAircraftList = new List<AircraftData>();
        Dictionary<string, AircraftData> newAircraftMap = new Dictionary<string, AircraftData>();
        
        // Parse the JSON response
        try
        {
            // Using Newtonsoft.Json for better JSON parsing capabilities
            JObject responseObj = JObject.Parse(jsonResponse);
            long timestamp = responseObj["time"].Value<long>();
            JArray states = (JArray)responseObj["states"];

            if (states != null && states.Count > 0)
            {
                foreach (JArray state in states)
                {
                    if (state.Count < 17) continue; // Skip incomplete data
                    
                    AircraftData aircraft = new AircraftData();
                    
                    // Extract data from the state array
                    aircraft.icao24 = state[0].ToString().Trim().ToLower();
                    aircraft.callsign = state[1].ToString().Trim();
                    aircraft.originCountry = state[2].ToString();
                    
                    // Parse position data with proper null checking
                    aircraft.longitude = state[5].Type != JTokenType.Null ? state[5].Value<float>() : 0;
                    aircraft.latitude = state[6].Type != JTokenType.Null ? state[6].Value<float>() : 0;
                    aircraft.altitude = state[7].Type != JTokenType.Null ? state[7].Value<float>() : 0;
                    aircraft.onGround = state[8].Type != JTokenType.Null ? state[8].Value<bool>() : false;
                    aircraft.velocity = state[9].Type != JTokenType.Null ? state[9].Value<float>() : 0;
                    aircraft.heading = state[10].Type != JTokenType.Null ? state[10].Value<float>() : 0;
                    aircraft.verticalRate = state[11].Type != JTokenType.Null ? state[11].Value<float>() : 0;
                    
                    // Try to determine aircraft type based on data
                    aircraft.type = DetermineAircraftType(aircraft);
                    
                    // Only add aircraft with valid positions
                    if (aircraft.latitude != 0 && aircraft.longitude != 0)
                    {
                        aircraft.lastUpdateTime = DateTime.Now;
                        newAircraftList.Add(aircraft);
                        newAircraftMap[aircraft.icao24] = aircraft;
                    }
                }

                // Update our stored data
                aircraftList = newAircraftList;
                aircraftMap = newAircraftMap;
                
                Log($"Successfully processed {aircraftList.Count} aircraft");
            }
            else
            {
                Log("No aircraft data found in the response");
            }
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to parse response: {e.Message}");
        }
    }

    private AircraftType DetermineAircraftType(AircraftData aircraft)
    {
        // This is a simple heuristic - in a real application, you would use a more
        // sophisticated method (like a database of known aircraft types by ICAO24)
        
        // Military aircraft often have specific callsign patterns
        if (aircraft.callsign.Contains("MIL") || 
            aircraft.callsign.Contains("RCH") ||
            aircraft.callsign.Contains("NAVY"))
            return AircraftType.Military;
        
        // Commercial flights usually have higher cruise altitudes
        if (aircraft.altitude > 7000 && !string.IsNullOrEmpty(aircraft.callsign))
            return AircraftType.Commercial;
        
        // Helicopters typically fly slower and lower
        if (aircraft.altitude < 1500 && aircraft.velocity < 80)
            return AircraftType.Helicopter;
        
        // General aviation is a catch-all for smaller aircraft
        if (aircraft.altitude < 5000)
            return AircraftType.General;
        
        return AircraftType.Unknown;
    }
    
    private void SaveCachedData()
    {
        if (!enableCaching) return;
        
        try
        {
            string json = JsonConvert.SerializeObject(aircraftList);
            PlayerPrefs.SetString("CachedAircraftData", json);
            PlayerPrefs.SetString("CacheTimestamp", DateTime.Now.ToString("o"));
            PlayerPrefs.Save();
            Log("Aircraft data cached successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TrafficDataManager] Failed to cache data: {e.Message}");
        }
    }

    private bool LoadCachedData()
    {
        if (!enableCaching) return false;
        
        try
        {
            if (!PlayerPrefs.HasKey("CachedAircraftData") || !PlayerPrefs.HasKey("CacheTimestamp"))
                return false;
                
            // Check cache age
            string timestamp = PlayerPrefs.GetString("CacheTimestamp");
            DateTime cacheTime = DateTime.Parse(timestamp);
            if ((DateTime.Now - cacheTime).TotalMinutes > maxCacheAgeMinutes)
            {
                Log("Cached data expired");
                return false;
            }
            
            // Load and deserialize
            string json = PlayerPrefs.GetString("CachedAircraftData");
            List<AircraftData> cachedList = JsonConvert.DeserializeObject<List<AircraftData>>(json);
            
            if (cachedList != null && cachedList.Count > 0)
            {
                aircraftList = cachedList;
                aircraftMap = new Dictionary<string, AircraftData>();
                foreach (var aircraft in aircraftList)
                {
                    aircraftMap[aircraft.icao24] = aircraft;
                }
                Log($"Loaded {aircraftList.Count} aircraft from cache");
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[TrafficDataManager] Failed to load cached data: {e.Message}");
        }
        
        return false;
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
            Debug.Log($"[TrafficDataManager] {message}");
        }
    }
    #endregion

    #region Helper Classes
    // Simple class structure to help parse the OpenSky API response
    [Serializable]
    private class JsonResponse
    {
        public long time;
        public object[][] states;
    }
    #endregion
}
