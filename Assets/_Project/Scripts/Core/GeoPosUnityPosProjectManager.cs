using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Principal;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages the projection between geographic positions (latitude/longitude) and Unity world positions.
/// </summary>
public class GeoPosUnityPosProjectManager : MonoBehaviour
{
    #region Singleton
    public static GeoPosUnityPosProjectManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializeProjection();
    }
    #endregion
    
    #region Projection Parameters
    [Header("Default Position")]
    [Tooltip("Default latitude in degrees")]
    [SerializeField] private double defaultLatitude = 0.0;
    
    [Tooltip("Default longitude in degrees")]
    [SerializeField] private double defaultLongitude = 0.0;
    
    [Tooltip("Default altitude in meters")]
    [SerializeField] private float defaultAltitude = 0.0f;
    
    [Header("Origin Reference")]
    [Tooltip("Origin latitude for projection (degrees)")]
    [SerializeField] private double originLatitude = 0.0;
    
    [Tooltip("Origin longitude for projection (degrees)")]
    [SerializeField] private double originLongitude = 0.0;
    
    [Tooltip("Origin altitude for projection (meters)")]
    [SerializeField] private float originAltitude = 0.0f;
    
    [Header("Scale Settings")]
    [Tooltip("Scale factor for converting geographic to Unity coordinates (meters per degree)")]
    [SerializeField] private float scaleFactor = 111000f; // Approx. meters per degree at equator
    
    [Tooltip("Unity units per meter")]
    [SerializeField] private float unitsPerMeter = 1.0f;
    
    [Header("Altitude Settings")]
    [Tooltip("Vertical exaggeration factor for altitude")]
    [SerializeField] private float altitudeExaggeration = 1.0f;
    
    [Tooltip("Reference system for altitude (0 = sea level)")]
    [SerializeField] private AltitudeReferenceSystem altitudeReference = AltitudeReferenceSystem.SeaLevel;
    
    [Header("Projection Type")]
    [SerializeField] private ProjectionType projectionType = ProjectionType.Equirectangular;
    
    // Earth model constants
    private const double EARTH_RADIUS = 6371000.0; // in meters
    private const double DEG_TO_RAD = Math.PI / 180.0;
    private const double RAD_TO_DEG = 180.0 / Math.PI;
    
    // Event for when projection parameters change
    public event Action OnProjectionParametersChanged;
    
    public enum ProjectionType
    {
        Equirectangular,
        Mercator,
        LocalTangentPlane,
        Identity
    }
    
    public enum AltitudeReferenceSystem
    {
        SeaLevel,
        Terrain,
        Custom
    }
    #endregion
    
    #region Public Methods
    /// <summary>
    /// Sets the default geographic position (latitude/longitude/altitude)
    /// </summary>
    public void SetDefaultPosition(double latitude, double longitude, float altitude = 0.0f)
    {
        defaultLatitude = latitude;
        defaultLongitude = longitude;
        defaultAltitude = altitude;
    }
    
    /// <summary>
    /// Gets the current default geographic position
    /// </summary>
    public (double latitude, double longitude, float altitude) GetDefaultPosition()
    {
        return (defaultLatitude, defaultLongitude, defaultAltitude);
    }
    
    /// <summary>
    /// Sets the origin for the projection
    /// </summary>
    public void SetOrigin(double latitude, double longitude, float altitude = 0.0f)
    {
        originLatitude = latitude;
        originLongitude = longitude;
        originAltitude = altitude;
        OnProjectionParametersChanged?.Invoke();
    }
    
    /// <summary>
    /// Gets the current origin for the projection
    /// </summary>
    public (double latitude, double longitude, float altitude) GetOrigin()
    {
        return (originLatitude, originLongitude, originAltitude);
    }
    
    /// <summary>
    /// Sets the altitude reference system and parameters
    /// </summary>
    public void SetAltitudeParameters(AltitudeReferenceSystem reference, float exaggeration = 1.0f)
    {
        altitudeReference = reference;
        altitudeExaggeration = exaggeration;
        OnProjectionParametersChanged?.Invoke();
    }
    
    /// <summary>
    /// Projects geographic coordinates (lat/long) to Unity world position
    /// </summary>
    public Vector3 GeoToUnityPosition(double latitude, double longitude, float altitude = 0f)
    {
        switch (projectionType)
        {
            case ProjectionType.Equirectangular:
                return GeoToUnityEquirectangular(latitude, longitude, altitude);
            case ProjectionType.Mercator:
                return GeoToUnityMercator(latitude, longitude, altitude);
            case ProjectionType.LocalTangentPlane:
                return GeoToUnityLTP(latitude, longitude, altitude);
            case ProjectionType.Identity:
                return GeoToUnityIdentity(latitude, longitude, altitude);
            default:
                return GeoToUnityEquirectangular(latitude, longitude, altitude);
        }
    }
    
    /// <summary>
    /// Projects Unity world position to geographic coordinates (lat/long)
    /// </summary>
    public (double latitude, double longitude, float altitude) UnityPositionToGeo(Vector3 position)
    {
        switch (projectionType)
        {
            case ProjectionType.Equirectangular:
                return UnityToGeoEquirectangular(position);
            case ProjectionType.Mercator:
                return UnityToGeoMercator(position);
            case ProjectionType.LocalTangentPlane:
                return UnityToGeoLTP(position);
      
            default:
                return UnityToGeoEquirectangular(position);
        }
    }
    
    /// <summary>
    /// Calculates distance between two geographic points in meters
    /// </summary>
    public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        // Haversine formula for distance calculation
        double dLat = (lat2 - lat1) * DEG_TO_RAD;
        double dLon = (lon2 - lon1) * DEG_TO_RAD;
        
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                  Math.Cos(lat1 * DEG_TO_RAD) * Math.Cos(lat2 * DEG_TO_RAD) *
                  Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
                  
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return EARTH_RADIUS * c; // Distance in meters
    }
    
    /// <summary>
    /// Calculates 3D distance between two geographic points including altitude in meters
    /// </summary>
    public double Calculate3DDistance(double lat1, double lon1, float alt1, double lat2, double lon2, float alt2)
    {
        // Get 2D surface distance
        double surfaceDistance = CalculateDistance(lat1, lon1, lat2, lon2);
        
        // Calculate altitude difference
        double altDifference = alt2 - alt1;
        
        // Use Pythagorean theorem for 3D distance
        return Math.Sqrt(surfaceDistance * surfaceDistance + altDifference * altDifference);
    }
    
    /// <summary>
    /// Calculates bearing between two geographic points in degrees
    /// </summary>
    public double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
    {
        lat1 *= DEG_TO_RAD;
        lon1 *= DEG_TO_RAD;
        lat2 *= DEG_TO_RAD;
        lon2 *= DEG_TO_RAD;
        
        double dLon = lon2 - lon1;
        
        double y = Math.Sin(dLon) * Math.Cos(lat2);
        double x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
        
        double bearing = Math.Atan2(y, x);
        bearing = bearing * RAD_TO_DEG;
        bearing = (bearing + 360) % 360; // Normalize to 0-360
        
        return bearing;
    }
    
    /// <summary>
    /// Sets the projection type to use
    /// </summary>
    public void SetProjectionType(ProjectionType type)
    {
        if (projectionType != type)
        {
            projectionType = type;
            OnProjectionParametersChanged?.Invoke();
        }
    }
    
    /// <summary>
    /// Sets the scale factors for the projection
    /// </summary>
    public void SetScaleFactors(float metersPerDegree, float unityUnitsPerMeter)
    {
        scaleFactor = metersPerDegree;
        unitsPerMeter = unityUnitsPerMeter;
        OnProjectionParametersChanged?.Invoke();
    }
    
    /// <summary>
    /// Converts an altitude value from one reference system to another
    /// </summary>
    public float ConvertAltitude(float altitude, AltitudeReferenceSystem fromSystem, AltitudeReferenceSystem toSystem)
    {
        // Implementation depends on available terrain data or custom offsets
        // This is a simplified version
        if (fromSystem == toSystem)
            return altitude;
            
        // Implement conversion between different reference systems
        // This would need terrain data or more sophisticated handling
        
        return altitude; // Default implementation just returns the original altitude
    }
    #endregion
    
    #region Private Methods
    private void InitializeProjection()
    {
        // Copy default values to origin if not set
        if (originLatitude == 0.0 && originLongitude == 0.0)
        {
            originLatitude = defaultLatitude;
            originLongitude = defaultLongitude;
            originAltitude = defaultAltitude;
        }
        
        Debug.Log($"Geo Projection initialized with origin: ({originLatitude}, {originLongitude}, {originAltitude})");
    }
    
    // Process altitude based on reference system and exaggeration factor
    private float ProcessAltitude(float rawAltitude)
    {
        float relativeAltitude = rawAltitude - originAltitude;
        return relativeAltitude * altitudeExaggeration;
    }
    
    // Reverse altitude processing
    private float ReverseAltitudeProcess(float unityAltitude)
    {
        float relativeAltitude = unityAltitude / altitudeExaggeration;
        return relativeAltitude + originAltitude;
    }
    
    // Equirectangular projection (simple)
    private Vector3 GeoToUnityEquirectangular(double latitude, double longitude, float altitude)
    {
        float x = (float)((longitude - originLongitude) * Math.Cos(originLatitude * DEG_TO_RAD) * scaleFactor * unitsPerMeter);
        float z = (float)((latitude - originLatitude) * scaleFactor * unitsPerMeter);
        float y = ProcessAltitude(altitude) * unitsPerMeter;
        
        return new Vector3(x, y, z);
    }
    private Vector3 GeoToUnityIdentity(double latitude, double longitude, float altitude)
    {
        float x = (float)((longitude - originLongitude) * Math.Cos(originLatitude * DEG_TO_RAD) * scaleFactor * unitsPerMeter);
        float z = (float)((latitude - originLatitude) * scaleFactor * unitsPerMeter);
        float y = altitude;

        return new Vector3(x, y, z);
    }
    
    private (double latitude, double longitude, float altitude) UnityToGeoEquirectangular(Vector3 position)
    {
        double latitude = originLatitude + (position.z / (scaleFactor * unitsPerMeter));
        double longitude = originLongitude + (position.x / (scaleFactor * unitsPerMeter * Math.Cos(originLatitude * DEG_TO_RAD)));
        float altitude = ReverseAltitudeProcess(position.y / unitsPerMeter);
        
        return (latitude, longitude, altitude);
    }
    
    // Mercator projection
    private Vector3 GeoToUnityMercator(double latitude, double longitude, float altitude)
    {
        // Limit latitude range for Mercator projection
        latitude = Math.Max(Math.Min(latitude, 85.0), -85.0);
        
        float x = (float)((longitude - originLongitude) * scaleFactor * unitsPerMeter);
        
        // Mercator formula: y = ln(tan(π/4 + φ/2))
        double latRad = latitude * DEG_TO_RAD;
        double originLatRad = originLatitude * DEG_TO_RAD;
        float z = (float)(Math.Log(Math.Tan(Math.PI / 4 + latRad / 2)) - 
                         Math.Log(Math.Tan(Math.PI / 4 + originLatRad / 2))) * 
                         scaleFactor * unitsPerMeter;
        
        float y = ProcessAltitude(altitude) * unitsPerMeter;
        
        return new Vector3(x, y, z);
    }
    
    private (double latitude, double longitude, float altitude) UnityToGeoMercator(Vector3 position)
    {
        double longitude = originLongitude + (position.x / (scaleFactor * unitsPerMeter));
        
        // Inverse Mercator formula: φ = 2 * atan(e^y) - π/2
        double originLatRad = originLatitude * DEG_TO_RAD;
        double mercN = Math.Tan(Math.PI / 4 + originLatRad / 2);
        double latRad = 2 * Math.Atan(mercN * Math.Exp(position.z / (scaleFactor * unitsPerMeter))) - Math.PI / 2;
        double latitude = latRad * RAD_TO_DEG;
        
        float altitude = ReverseAltitudeProcess(position.y / unitsPerMeter);
        
        return (latitude, longitude, altitude);
    }
    
    // Local Tangent Plane (North-East-Down or East-North-Up) projection
    private Vector3 GeoToUnityLTP(double latitude, double longitude, float altitude)
    {
        // Convert to radians
        double lat = latitude * DEG_TO_RAD;
        double lon = longitude * DEG_TO_RAD;
        double lat0 = originLatitude * DEG_TO_RAD;
        double lon0 = originLongitude * DEG_TO_RAD;
        
        // Calculate distance from origin
        double dLat = lat - lat0;
        double dLon = lon - lon0;
        
        // Calculate East-North coordinates
        double east = EARTH_RADIUS * dLon * Math.Cos(lat0);
        double north = EARTH_RADIUS * dLat;
        
        // Convert to Unity coordinates (East -> X, North -> Z, Up -> Y)
        float x = (float)(east * unitsPerMeter);
        float z = (float)(north * unitsPerMeter);
        float y = ProcessAltitude(altitude) * unitsPerMeter;
        
        return new Vector3(x, y, z);
    }
    
    private (double latitude, double longitude, float altitude) UnityToGeoLTP(Vector3 position)
    {
        // Convert from Unity coordinates
        double east = position.x / unitsPerMeter;
        double north = position.z / unitsPerMeter;
        float altitude = ReverseAltitudeProcess(position.y / unitsPerMeter);
        
        // Convert to radians
        double lat0 = originLatitude * DEG_TO_RAD;
        double lon0 = originLongitude * DEG_TO_RAD;
        
        // Calculate lat/long from East-North coordinates
        double dLat = north / EARTH_RADIUS;
        double dLon = east / (EARTH_RADIUS * Math.Cos(lat0));
        
        double lat = lat0 + dLat;
        double lon = lon0 + dLon;
        
        // Convert back to degrees
        double latitude = lat * RAD_TO_DEG;
        double longitude = lon * RAD_TO_DEG;
        
        return (latitude, longitude, altitude);
    }
    #endregion
}