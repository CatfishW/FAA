using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class GeoPositionUpdateEvent : UnityEvent<double, double,double> { }

/// <summary>
/// Updates TrafficDataManager's geographic filter based on player aircraft position.
/// Attach this to the player-controlled aircraft.
/// </summary>
public class TrafficGeoPositionUpdater : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TrafficDataManager to update. If null, will try to find one in the scene.")]
    [SerializeField] private TrafficDataManager trafficDataManager;

    [Header("Update Settings")]
    [Tooltip("How often to update the geographic position (in seconds)")]
    [Range(1f, 60f)]
    [SerializeField] private float updateInterval = 5f;
    
    [Tooltip("Radius in kilometers to fetch traffic data around the aircraft")]
    [Range(10f, 500f)]
    [SerializeField] private float geographicFilterRadius = 100f;
    
    [Tooltip("Minimum distance the aircraft must move (in Unity units) to trigger an update")]
    [SerializeField] private float minDistanceForUpdate = 5f;
    
    [Tooltip("Automatically fetch new data when position updates")]
    [SerializeField] private bool fetchDataOnUpdate = false;

    [Header("Debug")]
    [Tooltip("Show debug information in console")]
    [SerializeField] private bool showDebugInfo = false;
    
    [Tooltip("Visualize the geographic radius in the scene")]
    [SerializeField] private bool visualizeRadius = false;
    
    [Tooltip("Color for radius visualization")]
    [SerializeField] private Color visualizationColor = new Color(0, 1, 0, 0.2f);

    [Header("Events")]
    [SerializeField] public GeoPositionUpdateEvent onPositionUpdated = new GeoPositionUpdateEvent();

    // Private variables
    private float timer = 0f;
    private Vector3 lastPosition;
    private double currentLatitude;
    private double currentLongitude;
    private double currentAltitude; 
    private void Start()
    {
        // Find TrafficDataManager if not assigned
        if (trafficDataManager == null)
        {
            trafficDataManager = FindObjectOfType<TrafficDataManager>();
            if (trafficDataManager == null)
            {
                Debug.LogError("[TrafficGeoPositionUpdater] No TrafficDataManager found in the scene.");
                enabled = false;
                return;
            }
        }

        // Initialize with current position
        lastPosition = transform.position;
        UpdateGeographicPosition();
    }

    private void Update()
    {
        // Check if position has changed significantly or if timer has elapsed
        if (Vector3.Distance(transform.position, lastPosition) > minDistanceForUpdate || timer >= updateInterval)
        {
            UpdateGeographicPosition();
            timer = 0f;
        }
        
        timer += Time.deltaTime;
    }

    /// <summary>
    /// Updates the geographic position based on the current Unity position
    /// </summary>
    public void UpdateGeographicPosition()
    {
        lastPosition = transform.position;
        
        // Convert current Unity position to geographic coordinates
        var geoCoordinates = GeoPosUnityPosProjectManager.Instance.UnityPositionToGeo(transform.position);
        currentLatitude = geoCoordinates.latitude;
        currentLongitude = geoCoordinates.longitude;
        currentAltitude = geoCoordinates.altitude; // Updated to use the proper altitude from geo coordinates
        
        // Update the traffic data manager's geographic filter
        trafficDataManager.SetGeographicFilter((float)currentLatitude, (float)currentLongitude, geographicFilterRadius);
        
        if (showDebugInfo)
        {
            Debug.Log($"[TrafficGeoPositionUpdater] Updated position: Lat: {currentLatitude:F6}, Long: {currentLongitude:F6}, Alt: {currentAltitude:F1}m, Filter radius: {geographicFilterRadius}km");
        }
        
        // Trigger the position update event
        onPositionUpdated.Invoke(currentLatitude, currentLongitude,currentAltitude);
        
        // Optionally fetch data immediately
        if (fetchDataOnUpdate)
        {
            trafficDataManager.FetchDataNow();
        }
    }

    /// <summary>
    /// Sets the geographic filter radius (in km)
    /// </summary>
    public void SetGeographicFilterRadius(float radiusKm)
    {
        geographicFilterRadius = radiusKm;
        // Update with the new radius
        UpdateGeographicPosition();
    }

    /// <summary>
    /// Gets the current geographic position
    /// </summary>
    public (double latitude, double longitude) GetCurrentGeoPosition()
    {
        return (currentLatitude, currentLongitude);
    }
    /// <summary>
    /// Gets the current altitude in meters
    /// </summary>
    public double GetCurrentAltitude()
    {
        return currentAltitude;
    }


    private void OnDrawGizmos()
    {
        if (!visualizeRadius || !Application.isPlaying) return;
        
        // Draw a circle representing the filter radius
        Vector3 position = transform.position;
        position.y = 0; // Draw at ground level
        
        // Approximate the radius in Unity units (this is an approximation)
        float radiusUnity = geographicFilterRadius * 1000 * GeoPosUnityPosProjectManager.Instance.GeoToUnityPosition(0.001, 0, 0).magnitude / 111.0f;
        
        // Draw circle
        Gizmos.color = visualizationColor;
        DrawCircle(position, radiusUnity, 64);
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angle = 0f;
        float angleStep = 2f * Mathf.PI / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 0; i <= segments; i++)
        {
            angle += angleStep;
            Vector3 newPoint = center + new Vector3(radius * Mathf.Cos(angle), 0, radius * Mathf.Sin(angle));
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}