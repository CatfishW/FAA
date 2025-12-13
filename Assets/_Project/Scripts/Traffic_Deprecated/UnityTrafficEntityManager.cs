using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using CompassNavigatorPro;

[AddComponentMenu("Air Traffic/Traffic Entity Manager")]
public class UnityTrafficEntityManager : MonoBehaviour
{
    #region Inspector Settings
    [Header("References")]
    [Tooltip("The traffic data manager to get aircraft data from")]
    [SerializeField] private TrafficDataManager trafficDataManager;

    [Tooltip("Prefab to instantiate for aircraft")]
    [SerializeField] private GameObject aircraftPrefab;

    [Tooltip("Transform of the player's aircraft/root to teleport near traffic (defaults to main camera if not set)")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("UI Slider to control maximum visible distance")]
    [SerializeField] private Slider maxDistanceSlider;

    [Header("Traffic POI References")]
    public CompassProPOI otherTraffic;
    public CompassProPOI proximateTraffic;
    public CompassProPOI intrudingTraffic;
    public CompassProPOI threatTraffic;

    [Header("Visualization Settings")]
    [Tooltip("Scale factor for aircraft model")]
    [SerializeField] private float aircraftScale = 1f;

    [Tooltip("Maximum distance (km) from reference point to display aircraft")]
    [Range(1f, 500f)]
    [SerializeField] private float maxVisibleDistanceKm = 100f;

    [Tooltip("Apply altitude to Y position")]
    [SerializeField] private bool useRealAltitude = true;

    [Tooltip("Altitude scale factor (to make altitude changes more visible)")]
    [SerializeField] private float altitudeScaleFactor = 0.1f;
    
    [Header("Icon Progression Settings (nm & scale)")]
    [Tooltip("Distance in nm where icon switches from diamond to aircraft sprite")]
    [SerializeField] private float mediumRangeNmDefault = 10f;
    [Tooltip("Distance in nm where icon switches from aircraft sprite to directional view")]
    [SerializeField] private float closeRangeNmDefault = 3f;
    [Tooltip("Scale multiplier when far (diamond)")]
    [SerializeField] private float farIconScaleDefault = 0.9f;
    [Tooltip("Scale multiplier when medium (aircraft type)")]
    [SerializeField] private float mediumIconScaleDefault = 1.0f;
    [Tooltip("Scale multiplier when close (directional)")]
    [SerializeField] private float closeIconScaleDefault = 1.15f;
    
    [Header("Update Settings")]
    [Tooltip("Smoothing time for position changes")]
    [Range(0, 2)]
    [SerializeField] private float positionSmoothTime = 0.5f;

    [Tooltip("Smoothing time for rotation changes")]
    [Range(0, 2)]
    [SerializeField] private float rotationSmoothTime = 0.5f;

    [Header("Filtering")]
    [Tooltip("Filter aircraft by type (leave empty to show all)")]
    [SerializeField] private List<TrafficDataManager.AircraftType> visibleTypes = new List<TrafficDataManager.AircraftType>();
    
    [Header("Reference Position")]
    [Tooltip("Use this transform's position as the reference point (overrides lat/long)")]
    [SerializeField] private bool useTransformAsReference = false;
    
    [Tooltip("Use TrafficGeoPositionUpdater as the reference source (overrides transform and lat/long)")]
    [SerializeField] private bool useGeoUpdaterAsReference = false;
    
    [Tooltip("TrafficGeoPositionUpdater to use as reference source")]
    [SerializeField] private TrafficGeoPositionUpdater geoPositionUpdater;

    [Tooltip("Reference latitude for visibility calculations")]
    [SerializeField] private float referenceLatitude = 0f;

    [Tooltip("Reference longitude for visibility calculations")]
    [SerializeField] private float referenceLongitude = 0f;
    [Tooltip("Reference altitude for visibility calculations (meters)")]
    [SerializeField] private float referenceAltitude = 0f; // New field added

    [Header("Prediction")]
    [Tooltip("Show prediction lines for aircraft trajectory")]
    [SerializeField] private bool showPredictions = false;
    
    [Tooltip("Minutes ahead to predict aircraft position")]
    [Range(1f, 15f)]
    [SerializeField] private float predictionTimeMinutes = 3f;
    #endregion

    [Header("Debug")]
    [Tooltip("Show debug info on instantiated entities")]
    [SerializeField] private bool showDebugInfo = false;
    
    [Tooltip("Display debug statistics GUI on screen")]
    [SerializeField] private bool showDebugGUI = false;
    
    [Tooltip("Show debug gizmos in scene view")]
    [SerializeField] private bool showDebugGizmos = false;
    
    [Tooltip("Log detailed debug information to console")]
    [SerializeField] private bool logDebugInfo = false;
    
    [Tooltip("Color for debug visualization")]
    [SerializeField] private Color debugColor = Color.cyan;
    
    [Tooltip("Draw reference range circle")]
    [SerializeField] private bool showRangeCircle = false;
    #region Private Variables
    // Dictionary to track instantiated aircraft by ICAO ID
    private Dictionary<string, GameObject> aircraftEntities = new Dictionary<string, GameObject>();
    
    // Cache for velocity tracking
    private Dictionary<string, Vector3> velocities = new Dictionary<string, Vector3>();
    private Dictionary<string, Quaternion> rotationVelocities = new Dictionary<string, Quaternion>();
    
    // Component caches
    private GeoPosUnityPosProjectManager projectionManager;
    private Transform referenceTransform;
    private enum ThreatLevel
    {
        Other,
        Proximate,
        Intruding,
        Threat
    }
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        InitializeComponents();
        
        // Subscribe to traffic data events
        if (trafficDataManager != null)
        {
            trafficDataManager.onDataUpdated.AddListener(OnAircraftDataUpdated);
        }
        
        // Subscribe to geo position updater events
        if (useGeoUpdaterAsReference && geoPositionUpdater != null)
        {
            geoPositionUpdater.onPositionUpdated.AddListener(OnGeoPositionUpdated);
        }
        
        // Subscribe to slider events
        InitializeSlider();
    }

    private void OnDisable()
    {
        // Unsubscribe from events
        if (trafficDataManager != null)
        {
            trafficDataManager.onDataUpdated.RemoveListener(OnAircraftDataUpdated);
        }
        
        if (geoPositionUpdater != null)
        {
            geoPositionUpdater.onPositionUpdated.RemoveListener(OnGeoPositionUpdated);
        }
        
        // Unsubscribe from slider events
        if (maxDistanceSlider != null)
        {
            maxDistanceSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }
    }

    private void Start()
    {
        // Force an initial update if data is already available
        if (trafficDataManager != null && trafficDataManager.AircraftCount > 0)
        {
            OnAircraftDataUpdated(trafficDataManager.AircraftList.ToList());
        }
    }

    private void OnDestroy()
    {
        // Clean up all instantiated aircraft
        ClearAllAircraft();
    }
    #endregion

    #region Core Methods
    /// <summary>
    /// Determines the threat level of an aircraft based on distance and other criteria
    /// </summary>
    private ThreatLevel GetAircraftThreatLevel(TrafficDataManager.AircraftData aircraft)
    {
        // Calculate distance to reference position
        float distanceKm = CalculateDistance(aircraft);
        
        // Altitude difference in meters
        float altitudeDifference = Mathf.Abs(aircraft.altitude - (useGeoUpdaterAsReference && geoPositionUpdater != null ? 
                                (float)geoPositionUpdater.GetCurrentAltitude() : 0f));
        
        // Simple classification based on distance and altitude
        // These thresholds should be customizable via the inspector
        if (distanceKm < 1.0f && altitudeDifference < 300)
        {
            return ThreatLevel.Threat;
        }
        else if (distanceKm < 3.0f && altitudeDifference < 500)
        {
            return ThreatLevel.Intruding;
        }
        else if (distanceKm < 10.0f && altitudeDifference < 1000)
        {
            return ThreatLevel.Proximate;
        }
        else
        {
            return ThreatLevel.Other;
        }
    }
    /// <summary>
    /// Create a POI for the aircraft based on its threat level
    /// </summary>
    private void CreateOrUpdatePOI(GameObject entity, TrafficDataManager.AircraftData aircraft)
    {
        // Find or create POI component
        AircraftPOIComponent poiComponent = entity.GetComponent<AircraftPOIComponent>();
        if (poiComponent == null)
        {
            poiComponent = entity.AddComponent<AircraftPOIComponent>();
        }
        // Apply manager defaults to icon progression config
        poiComponent.ConfigureIconProgression(
            mediumRangeNmDefault,
            closeRangeNmDefault,
            farIconScaleDefault,
            mediumIconScaleDefault,
            closeIconScaleDefault
        );
        
        // Determine threat level
        ThreatLevel threatLevel = GetAircraftThreatLevel(aircraft);
        
        // Select the appropriate POI prefab
        CompassProPOI poiPrefab = null;
        switch (threatLevel)
        {
            case ThreatLevel.Threat:
                poiPrefab = threatTraffic;
                break;
            case ThreatLevel.Intruding:
                poiPrefab = intrudingTraffic;
                break;
            case ThreatLevel.Proximate:
                poiPrefab = proximateTraffic;
                break;
            case ThreatLevel.Other:
                poiPrefab = otherTraffic;
                break;
        }
        
        // Only proceed if we have a valid prefab
        if (poiPrefab != null)
        {
            // Update the POI component with the new prefab and aircraft data
            poiComponent.UpdatePOI(poiPrefab, aircraft, threatLevel.ToString(), referenceAltitude);
        }
        else if (logDebugInfo)
        {
            Debug.LogWarning($"[TrafficEntityManager] No POI prefab assigned for threat level {threatLevel}");
        }
    }
    /// <summary>
    /// Update reference position from TrafficGeoPositionUpdater
    /// </summary>
    private void OnGeoPositionUpdated(double latitude, double longitude, double altitude)
    {
        if (useGeoUpdaterAsReference)
        {
            referenceLatitude = (float)latitude;
            referenceLongitude = (float)longitude;
            referenceAltitude = (float)altitude;//(float)geoPositionUpdater.GetCurrentAltitude(); // Update reference altitude if using geo updater
            
            if (logDebugInfo)
            {
                Debug.Log($"[TrafficEntityManager] Updated reference position from GeoPositionUpdater: Lat: {referenceLatitude:F6}, Lon: {referenceLongitude:F6}");
            }
        }
    }
    /// <summary>
    /// Process updated aircraft data from the traffic manager
    /// </summary>
    private void OnAircraftDataUpdated(List<TrafficDataManager.AircraftData> aircraftList)
    {
        // Update reference point if using transform
        if (useTransformAsReference && referenceTransform != null)
        {
            var geoPos = projectionManager.UnityPositionToGeo(referenceTransform.position);
            referenceLatitude = (float)geoPos.latitude;
            referenceLongitude = (float)geoPos.longitude;
        }
        // If using geo updater, position is already being updated via events
        else if (useGeoUpdaterAsReference && geoPositionUpdater != null)
        {
            // Get the latest position in case we missed an event
            var geoPos = geoPositionUpdater.GetCurrentGeoPosition();
            referenceLatitude = (float)geoPos.latitude;
            referenceLongitude = (float)geoPos.longitude;
        }
        if (aircraftPrefab == null)
        {
            Debug.LogWarning("[TrafficEntityManager] Aircraft prefab not assigned!");
            return;
        }
        //log aircraftlist
        if (logDebugInfo)
        {
            foreach (var aircraft in aircraftList)
            {
                Debug.Log($"[TrafficEntityManager] Aircraft: {aircraft.callsign} ({aircraft.icao24}) - Type: {aircraft.type}");
            }
        }

        HashSet<string> currentIcaos = new HashSet<string>();
        
        // Update reference point if using transform
        if (useTransformAsReference && referenceTransform != null)
        {
            var geoPos = projectionManager.UnityPositionToGeo(referenceTransform.position);
            referenceLatitude = (float)geoPos.latitude;
            referenceLongitude = (float)geoPos.longitude;
        }

        foreach (var aircraft in aircraftList)
        {
            // Apply filters
            if (!ShouldShowAircraft(aircraft)){
                //Debug.Log($"[TrafficEntityManager] Skipping aircraft {aircraft.callsign} ({aircraft.icao24}) due to filtering.");
                continue;
            }

            currentIcaos.Add(aircraft.icao24);

            // Update or create aircraft
            if (aircraftEntities.TryGetValue(aircraft.icao24, out GameObject entity))
            {
                UpdateAircraftEntity(entity, aircraft);
            }
            else
            {
                CreateAircraftEntity(aircraft);
            }
        }

        // Remove aircraft that no longer exist in the data
        foreach (var icao in aircraftEntities.Keys.ToList())
        {
            if (!currentIcaos.Contains(icao))
            {
                RemoveAircraftEntity(icao);
            }
        }
    }

    /// <summary>
    /// Create a new aircraft game object
    /// </summary>
    private void CreateAircraftEntity(TrafficDataManager.AircraftData aircraft)
    {
        // Instantiate the aircraft prefab
        GameObject entity = Instantiate(aircraftPrefab, Vector3.zero, Quaternion.identity);
        entity.name = $"Aircraft_{aircraft.callsign}_{aircraft.icao24}";
        entity.transform.localScale = Vector3.one * aircraftScale;

        // Set the initial position and rotation
        Vector3 unityPos = GetUnityPositionForAircraft(aircraft);
        entity.transform.position = unityPos;
        entity.transform.rotation = GetUnityRotationForAircraft(aircraft);

        // Set parent to "TrafficEntities"
        GameObject parentObject = GameObject.Find("TrafficEntities");
        if (parentObject == null)
        {
            parentObject = new GameObject("TrafficEntities");
        }
        entity.transform.SetParent(parentObject.transform);

        // Add to tracking dictionary
        aircraftEntities[aircraft.icao24] = entity;
        velocities[aircraft.icao24] = Vector3.zero;
        rotationVelocities[aircraft.icao24] = Quaternion.identity;

        // Attach aircraft data component if it exists
        var dataComponent = entity.GetComponent<AircraftEntityComponent>();
        if (dataComponent == null && showDebugInfo)
        {
            dataComponent = entity.AddComponent<AircraftEntityComponent>();
        }

        if (dataComponent != null)
        {
            dataComponent.UpdateAircraftData(aircraft);
        }
        
        // Create or update POI for this aircraft
        CreateOrUpdatePOI(entity, aircraft);

        // Create prediction line if enabled
        if (showPredictions)
        {
            CreateOrUpdatePredictionLine(entity, aircraft);
        }
    }

    /// <summary>
    /// Update an existing aircraft game object
    /// </summary>
    private void UpdateAircraftEntity(GameObject entity, TrafficDataManager.AircraftData aircraft)
    {
        // Calculate target position and rotation
        Vector3 targetPosition = GetUnityPositionForAircraft(aircraft);
        Quaternion targetRotation = GetUnityRotationForAircraft(aircraft);

        // Apply smoothing
        Vector3 velocity = velocities[aircraft.icao24];
        entity.transform.position = Vector3.SmoothDamp(entity.transform.position, targetPosition, ref velocity, positionSmoothTime);
        velocities[aircraft.icao24] = velocity;
        
        // We can use simple slerp for rotation
        entity.transform.rotation = Quaternion.Slerp(entity.transform.rotation, targetRotation, Time.deltaTime / rotationSmoothTime);

       // Update aircraft data component if it exists
        var dataComponent = entity.GetComponent<AircraftEntityComponent>();
        if (dataComponent != null)
        {
            dataComponent.UpdateAircraftData(aircraft);
        }
        
        // Update POI for this aircraft
        CreateOrUpdatePOI(entity, aircraft);
        
        // Update prediction line if enabled
        if (showPredictions)
        {
            CreateOrUpdatePredictionLine(entity, aircraft);
        }
    }

    /// <summary>
    /// Remove an aircraft entity by ICAO
    /// </summary>
    private void RemoveAircraftEntity(string icao24)
    {
        if (aircraftEntities.TryGetValue(icao24, out GameObject entity))
        {
            Destroy(entity);
            aircraftEntities.Remove(icao24);
            velocities.Remove(icao24);
            rotationVelocities.Remove(icao24);
        }
    }

    /// <summary>
    /// Calculate Unity world position for aircraft
    /// </summary>
    private Vector3 GetUnityPositionForAircraft(TrafficDataManager.AircraftData aircraft)
    {
        // Convert geographic position to Unity position
        float altitude = useRealAltitude ? aircraft.altitude * altitudeScaleFactor : 0;
        return projectionManager.GeoToUnityPosition(aircraft.latitude, aircraft.longitude, altitude);
    }

    /// <summary>
    /// Calculate Unity rotation for aircraft
    /// </summary>
    private Quaternion GetUnityRotationForAircraft(TrafficDataManager.AircraftData aircraft)
    {
        // Calculate rotation based on heading
        // In Unity, 0 degrees is along the Z axis, but geographic heading is clockwise from north
        float yRotation = aircraft.heading;
        
        // Create a rotation that first applies pitch based on vertical rate, then heading
        float pitch = Mathf.Clamp(aircraft.verticalRate * 5f, -30f, 30f);
        
        return Quaternion.Euler(pitch, yRotation, 0);
    }
    
    /// <summary>
    /// Create or update the prediction line for an aircraft
    /// </summary>
    private void CreateOrUpdatePredictionLine(GameObject entity, TrafficDataManager.AircraftData aircraft)
    {
        LineRenderer lineRenderer = entity.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = entity.AddComponent<LineRenderer>();
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.05f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.white;
            lineRenderer.endColor = new Color(1, 1, 1, 0.2f);
        }

        // Calculate predicted position
        Vector2 predictedGeoPos = aircraft.GetPredictedPosition(predictionTimeMinutes);
        Vector3 currentPos = entity.transform.position;
        Vector3 predictedPos = projectionManager.GeoToUnityPosition(predictedGeoPos.y, predictedGeoPos.x, 
                                                               useRealAltitude ? aircraft.altitude * altitudeScaleFactor : 0);
        
        // Set the line points
        lineRenderer.positionCount = 2;
        lineRenderer.SetPositions(new Vector3[] { currentPos, predictedPos });
    }
    
    /// <summary>
    /// Determine if an aircraft should be displayed based on filters
    /// </summary>
    private bool ShouldShowAircraft(TrafficDataManager.AircraftData aircraft)
    {
        // Check type filter
        if (visibleTypes.Count > 0 && !visibleTypes.Contains(aircraft.type)){
            Debug.Log($"[TrafficEntityManager] Aircraft type {aircraft.type} is not in the visible types list. Skipping...");
            return false;   
        }
        
        // Check distance filter
        if (maxVisibleDistanceKm > 0)
        {
            float distance = CalculateDistance(aircraft);
            if (distance > maxVisibleDistanceKm){
                Debug.Log($"[TrafficEntityManager] Aircraft {aircraft.callsign} ({aircraft.icao24}) is beyond the max visible distance of {maxVisibleDistanceKm} km (actual distance: {distance:F2} km). Skipping...");
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Calculate the distance from reference point to aircraft
    /// </summary>
    private float CalculateDistance(TrafficDataManager.AircraftData aircraft)
    {
        // Use the traffic data manager's distance calculation
        float horizontalDistance = (float)projectionManager.CalculateDistance(
            referenceLatitude, referenceLongitude, aircraft.latitude, aircraft.longitude) / 1000f; // Convert to km
        
        // Calculate altitude difference
        float altitudeDifference = Mathf.Abs(aircraft.altitude - referenceAltitude);

        // Combine horizontal and vertical distances
        return Mathf.Sqrt(horizontalDistance * horizontalDistance + (altitudeDifference / 1000f) * (altitudeDifference / 1000f));
    }

    /// <summary>
    /// Clear all aircraft entities
    /// </summary>
    public void ClearAllAircraft()
    {
        foreach (var entity in aircraftEntities.Values)
        {
            Destroy(entity);
        }
        
        aircraftEntities.Clear();
        velocities.Clear();
        rotationVelocities.Clear();
    }
    #endregion

    #region Initialization and Utility Methods
    /// <summary>
    /// Initialize required components and references
    /// </summary>
    private void InitializeComponents()
    {
        // Find traffic data manager if not set
        if (trafficDataManager == null)
        {
            trafficDataManager = FindObjectOfType<TrafficDataManager>();
            if (trafficDataManager == null)
            {
                Debug.LogWarning("[TrafficEntityManager] No TrafficDataManager found in the scene!");
            }
        }

        // Get projection manager
        projectionManager = FindObjectOfType<GeoPosUnityPosProjectManager>();
        if (projectionManager == null)
        {
            Debug.LogWarning("[TrafficEntityManager] No GeoPosUnityPosProjectManager found in the scene!");
        }

        // Cache reference transform if needed
        if (useTransformAsReference)
        {
            referenceTransform = transform;
        }
        // Default player transform if not assigned
        if (playerTransform == null && Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }
        // Find traffic geo position updater if not set
        if (useGeoUpdaterAsReference && geoPositionUpdater == null)
        {
            geoPositionUpdater = FindObjectOfType<TrafficGeoPositionUpdater>();
            if (geoPositionUpdater == null)
            {
                Debug.LogWarning("[TrafficEntityManager] useGeoUpdaterAsReference is true but no TrafficGeoPositionUpdater found in the scene!");
            }
            else
            {
                // Subscribe to position updates
                geoPositionUpdater.onPositionUpdated.AddListener(OnGeoPositionUpdated);
                
                // Initialize with current position
                var geoPos = geoPositionUpdater.GetCurrentGeoPosition();
                referenceLatitude = (float)geoPos.latitude;
                referenceLongitude = (float)geoPos.longitude;
            }
        }

        // Cache reference transform if needed
        if (useTransformAsReference)
        {
            referenceTransform = transform;
        }
    }

    /// <summary>
    /// Initialize the UI slider for distance control
    /// </summary>
    private void InitializeSlider()
    {
        if (maxDistanceSlider != null)
        {
            // Set slider range
            maxDistanceSlider.minValue = 1f;
            maxDistanceSlider.maxValue = 100f;
            maxDistanceSlider.value = maxVisibleDistanceKm;
            
            // Subscribe to value changes
            maxDistanceSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }
    }

    /// <summary>
    /// Handle slider value changes
    /// </summary>
    private void OnSliderValueChanged(float value)
    {
        MaxVisibleDistanceKm = value;
    }

    /// <summary>
    /// Set visible aircraft types
    /// </summary>
    public void SetVisibleTypes(List<TrafficDataManager.AircraftType> types)
    {
        visibleTypes = types;
        // Update visibility for existing aircraft
        if (trafficDataManager != null && trafficDataManager.AircraftCount > 0)
        {
            OnAircraftDataUpdated(trafficDataManager.AircraftList.ToList());
        }
    }
    /// <summary>
    /// Set reference position mode
    /// </summary>
    public void SetReferenceMode(bool useTransform, bool useGeoUpdater)
    {
        // Unsubscribe from current geoPositionUpdater if changing mode
        if (useGeoUpdaterAsReference && !useGeoUpdater && geoPositionUpdater != null)
        {
            geoPositionUpdater.onPositionUpdated.RemoveListener(OnGeoPositionUpdated);
        }
        
        useTransformAsReference = useTransform;
        useGeoUpdaterAsReference = useGeoUpdater;
        
        // If both are true, prioritize geoUpdater
        if (useTransformAsReference && useGeoUpdaterAsReference)
        {
            useTransformAsReference = false;
            Debug.LogWarning("[TrafficEntityManager] Both reference modes enabled. Using GeoPositionUpdater as priority.");
        }
        
        // Resubscribe if needed
        if (useGeoUpdaterAsReference && geoPositionUpdater != null)
        {
            geoPositionUpdater.onPositionUpdated.AddListener(OnGeoPositionUpdated);
            
            // Initialize with current position
            var geoPos = geoPositionUpdater.GetCurrentGeoPosition();
            referenceLatitude = (float)geoPos.latitude;
            referenceLongitude = (float)geoPos.longitude;
        }
        
        // Reinitialize references
        InitializeComponents();
    }

    /// <summary>
    /// Set the TrafficGeoPositionUpdater to use
    /// </summary>
    public void SetGeoPositionUpdater(TrafficGeoPositionUpdater updater)
    {
        // Unsubscribe from old updater
        if (geoPositionUpdater != null)
        {
            geoPositionUpdater.onPositionUpdated.RemoveListener(OnGeoPositionUpdated);
        }
        
        geoPositionUpdater = updater;
        
        // Subscribe to new updater
        if (useGeoUpdaterAsReference && geoPositionUpdater != null)
        {
            geoPositionUpdater.onPositionUpdated.AddListener(OnGeoPositionUpdated);
            
            // Initialize with current position
            var geoPos = geoPositionUpdater.GetCurrentGeoPosition();
            referenceLatitude = (float)geoPos.latitude;
            referenceLongitude = (float)geoPos.longitude;
        }
    }


    /// <summary>
    /// Public property to control maximum visible distance with slider support
    /// </summary>
    public float MaxVisibleDistanceKm
    {
        get { return maxVisibleDistanceKm; }
        set 
        { 
            maxVisibleDistanceKm = Mathf.Clamp(value, 1f, 500f);
            
            // Update slider if it exists and value is different
            if (maxDistanceSlider != null && !Mathf.Approximately(maxDistanceSlider.value, maxVisibleDistanceKm))
            {
                maxDistanceSlider.value = maxVisibleDistanceKm;
            }
            
            // Update visibility for existing aircraft when distance changes
            if (trafficDataManager != null && trafficDataManager.AircraftCount > 0)
            {
                OnAircraftDataUpdated(trafficDataManager.AircraftList.ToList());
            }
        }
    }

    /// <summary>
    /// Set maximum visible distance
    /// </summary>
    public void SetMaxVisibleDistance(float distanceKm)
    {
        MaxVisibleDistanceKm = distanceKm;
    }

    /// <summary>
    /// Set reference position for distance calculations
    /// </summary>
    public void SetReferencePosition(float latitude, float longitude)
    {
        useTransformAsReference = false;
        referenceLatitude = latitude;
        referenceLongitude = longitude;
    }
    /// <summary>
    /// Set reference altitude for visibility calculations
    /// </summary>
    public void SetReferenceAltitude(float altitude)
    {
        referenceAltitude = altitude;
    }
    #endregion

    #region Teleportation Utilities
    /// <summary>
    /// Teleports the configured player transform near the specified aircraft ICAO.
    /// </summary>
    /// <param name="icao24">Target aircraft ICAO24 (lower/upper case accepted)</param>
    /// <param name="offsetMeters">Distance offset in meters from target position</param>
    /// <param name="bearingFromTargetDeg">Bearing from target in degrees (0=north, 90=east)</param>
    /// <returns>True if teleport succeeded</returns>
    public bool TeleportPlayerNearAircraft(string icao24, float offsetMeters = 500f, float bearingFromTargetDeg = 0f)
    {
        if (trafficDataManager == null || projectionManager == null)
        {
            Debug.LogWarning("[TrafficEntityManager] Cannot teleport: missing managers.");
            return false;
        }
        if (playerTransform == null)
        {
            Debug.LogWarning("[TrafficEntityManager] Cannot teleport: playerTransform not set.");
            return false;
        }

        var aircraft = trafficDataManager.GetAircraftByIcao(icao24.ToLower());
        if (aircraft == null)
        {
            Debug.LogWarning($"[TrafficEntityManager] Teleport failed: aircraft with ICAO {icao24} not found.");
            return false;
        }

        // Convert target geo to Unity position
        float yAlt = useRealAltitude ? aircraft.altitude * altitudeScaleFactor : 0f;
        Vector3 targetPos = projectionManager.GeoToUnityPosition(aircraft.latitude, aircraft.longitude, yAlt);

        // Compute planar offset from bearing
        float rad = bearingFromTargetDeg * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * offsetMeters;

        Vector3 newPos = targetPos + offset;
        playerTransform.position = newPos;

        // Optionally face the target aircraft
        Vector3 lookDir = (targetPos - newPos); lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            playerTransform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        }

        // Update transform-based reference immediately if used
        if (useTransformAsReference)
        {
            referenceTransform = playerTransform;
        }
        return true;
    }
    #endregion
}

/// <summary>
/// Optional component to store and display aircraft data on entities
/// </summary>
public class AircraftEntityComponent : MonoBehaviour
{
    public TrafficDataManager.AircraftData aircraftData;
    private TextMesh labelText;

    public void UpdateAircraftData(TrafficDataManager.AircraftData data)
    {
        aircraftData = data;
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (labelText == null)
        {
            // Create a text label if needed
            GameObject label = new GameObject("Label");
            label.transform.SetParent(transform);
            label.transform.localPosition = Vector3.up * 2f;
            label.transform.localRotation = Quaternion.identity;
            
            labelText = label.AddComponent<TextMesh>();
            labelText.alignment = TextAlignment.Center;
            labelText.anchor = TextAnchor.MiddleCenter;
            labelText.fontSize = 48;
            labelText.characterSize = 0.05f;
        }

        // Update label text with aircraft info
        labelText.text = $"{aircraftData.callsign}\n{aircraftData.altitude:F0}m\n{aircraftData.velocity:F0}m/s";
    }

    private void LateUpdate()
    {
        // Make the label face the camera
        if (labelText != null && Camera.main != null)
        {
            labelText.transform.rotation = Quaternion.LookRotation(
                labelText.transform.position - Camera.main.transform.position);
        }
    }
    
}
/// <summary>
/// Component to manage POI for aircraft entities
/// </summary>
public class AircraftPOIComponent : MonoBehaviour
{
    private CompassProPOI currentPOI;
    private string currentThreatLevel;
    private TrafficDataManager.AircraftData latestAircraftData;

    // Distance thresholds (in nautical miles) for icon progression
    [SerializeField] private float mediumRangeNm = 10f;
    [SerializeField] private float closeRangeNm = 3f;

    // Optional scale adjustments by stage
    [SerializeField] private float farIconScale = 0.9f;
    [SerializeField] private float mediumIconScale = 1.0f;
    [SerializeField] private float closeIconScale = 1.15f;


    [Header("Sprite Overrides (Inspector)")]
    [Tooltip("If set, overrides sprite loaded from Resources for diamond icon")]
    [SerializeField] private Sprite overrideDiamond;
    [Tooltip("If set, overrides sprite loaded from Resources for jet icon")]
    [SerializeField] private Sprite overrideJet;
    [Tooltip("If set, overrides sprite loaded from Resources for helicopter icon")]
    [SerializeField] private Sprite overrideHelicopter;
    [Tooltip("If set, overrides sprite loaded from Resources for drone icon")]
    [SerializeField] private Sprite overrideDrone;
    [Tooltip("If set, overrides sprite loaded from Resources for small plane icon")]
    [SerializeField] private Sprite overrideSmallPlane;
    [Tooltip("If set, overrides sprite loaded from Resources for commercial plane icon")]
    [SerializeField] private Sprite overrideCommercial;

    [Header("Directional Sprite Overrides (Inspector)")]
    [SerializeField] private Sprite dirUpwardRight;
    [SerializeField] private Sprite dirUpwardRight2;
    [SerializeField] private Sprite dirUpwardLeft;
    [SerializeField] private Sprite dirUpwardClimb;
    [SerializeField] private Sprite dirDownwardTowards;
    [SerializeField] private Sprite dirDownwardLeftFacingTowards;
    [SerializeField] private Sprite dirDownturnRight;

    // Cached sprites loaded from Resources/AIRCRAFT SPRITES
    private bool spritesLoaded;
    private Sprite spriteDiamond;
    private Sprite spriteJet;
    private Sprite spriteHelicopter;
    private Sprite spriteDrone;
    private Sprite spriteSmallPlane;
    private Sprite spriteCommercial;

    // Directional sprites (optional; falls back gracefully if not found)
    private readonly Dictionary<string, Sprite> directionalSprites = new Dictionary<string, Sprite>();

    private enum DisplayStage { Far, Medium, Close }
    private DisplayStage lastStage = (DisplayStage)(-1);

    private void EnsureSpritesLoaded()
    {
        if (spritesLoaded) return;
        spritesLoaded = true;

        // Base icons - prefer Inspector overrides, else Resources
        spriteDiamond = overrideDiamond != null ? overrideDiamond : Resources.Load<Sprite>("AIRCRAFT SPRITES/DiamondSprite");
        spriteJet = overrideJet != null ? overrideJet : Resources.Load<Sprite>("AIRCRAFT SPRITES/JetSprite");
        spriteHelicopter = overrideHelicopter != null ? overrideHelicopter : Resources.Load<Sprite>("AIRCRAFT SPRITES/HelicopterSprite");
        spriteDrone = overrideDrone != null ? overrideDrone : Resources.Load<Sprite>("AIRCRAFT SPRITES/DroneSprite");
        spriteSmallPlane = overrideSmallPlane != null ? overrideSmallPlane : Resources.Load<Sprite>("AIRCRAFT SPRITES/SmallPlaneSprite");
        spriteCommercial = overrideCommercial != null ? overrideCommercial : Resources.Load<Sprite>("AIRCRAFT SPRITES/CommercialPlaneSprite");

        // Directional sprites - prefer Inspector overrides first
        if (dirUpwardRight != null) directionalSprites["UpwardRight"] = dirUpwardRight;
        if (dirUpwardRight2 != null) directionalSprites["UpwardRight2"] = dirUpwardRight2;
        if (dirUpwardLeft != null) directionalSprites["UpwardLeft"] = dirUpwardLeft;
        if (dirUpwardClimb != null) directionalSprites["UpwardClimb"] = dirUpwardClimb;
        if (dirDownwardTowards != null) directionalSprites["DownwardTowards"] = dirDownwardTowards;
        if (dirDownwardLeftFacingTowards != null) directionalSprites["DownwardLeftFacingTowards"] = dirDownwardLeftFacingTowards;
        if (dirDownturnRight != null) directionalSprites["DownturnRight"] = dirDownturnRight;

        // For any not provided, try loading from Resources
        TryLoadDirectional("UpwardRight");
        TryLoadDirectional("UpwardRight2");
        TryLoadDirectional("UpwardLeft");
        TryLoadDirectional("UpwardClimb");
        TryLoadDirectional("DownwardTowards");
        TryLoadDirectional("DownwardLeftFacingTowards");
        TryLoadDirectional("DownturnRight");
    }

    private void TryLoadDirectional(string name)
    {
        var s = Resources.Load<Sprite>("AIRCRAFT SPRITES/" + name);
        if (s != null && !directionalSprites.ContainsKey(name)) directionalSprites.Add(name, s);
    }
    
    /// <summary>
    /// Update or create the POI for this aircraft
    /// </summary>
    public void UpdatePOI(CompassProPOI poiPrefab, TrafficDataManager.AircraftData aircraft, string threatLevel, float referenceAltitude)
    {
        // If threat level changed or no POI yet, create/update POI
        if (currentPOI == null || currentThreatLevel != threatLevel)
        {
            // If we had a different POI before, destroy it
            if (currentPOI != null)
            {
                Destroy(currentPOI.gameObject);
                currentPOI = null;
            }
            
            // Instantiate the new POI
            if (poiPrefab != null)
            {
                currentPOI = Instantiate(poiPrefab, transform);
                currentPOI.id = Random.Range(100, 999999);
                currentPOI.name = $"POI_{aircraft.callsign}_{threatLevel}";
                // Configure the POI
                currentPOI.title = aircraft.callsign;
                // Store relative altitude (aircraft altitude - reference altitude)
                currentPOI.altitude = aircraft.altitude - referenceAltitude;
                // Store heading from aircraft data
                currentPOI.heading = aircraft.heading;
                currentPOI.iconShowDistance = true;
                currentPOI.miniMapShowRotation = true;
                
                // Store the current threat level
                currentThreatLevel = threatLevel;
                currentPOI.gameObject.SetActive(true); // Ensure the POI is active
            }
        }
        
        // Update POI data if needed
        if (currentPOI != null)
        {
            // Update relative altitude from aircraft data (aircraft altitude - reference altitude)
            currentPOI.altitude = aircraft.altitude - referenceAltitude;
            // Update heading from aircraft data
            currentPOI.heading = aircraft.heading;
            // Update title to include callsign
            currentPOI.title = $"{aircraft.callsign}";
            latestAircraftData = aircraft;
            UpdateDynamicIcon();
        }
    }
    
    private void Update()
    {
        if (currentPOI == null) return;
        UpdateDynamicIcon();
    }

    private void UpdateDynamicIcon()
    {
        EnsureSpritesLoaded();
        if (currentPOI == null) return;

        // distanceToFollow is in meters; convert to nautical miles
        float distanceNm = currentPOI.distanceToFollow / 1852f;

        DisplayStage stage = DisplayStage.Far;
        if (distanceNm <= closeRangeNm) stage = DisplayStage.Close;
        else if (distanceNm <= mediumRangeNm) stage = DisplayStage.Medium;

        // Always apply sprite to ensure UI reflects updates even if POI UI instantiated after stage change
        Sprite target = GetSpriteForStage(stage);
        ApplySpriteToPOI(target, stage);

        // Track stage for any future stage-specific logic
        lastStage = stage;

        // Do not modify transform rotation here; rotation is handled by UnityTrafficEntityManager smoothing
    }

    private Sprite GetSpriteForStage(DisplayStage stage)
    {
        if (stage == DisplayStage.Far)
        {
            // Always prefer the diamond icon for far distance
            if (spriteDiamond != null) return spriteDiamond;
            return currentPOI.iconNonVisited;
        }
        if (stage == DisplayStage.Medium)
        {
            return GetMediumTypeSprite();
        }
        return GetSpriteForCloseDirectional();
    }

    private Sprite GetMediumTypeSprite()
    {
        if (latestAircraftData == null)
        {
            return spriteDiamond != null ? spriteDiamond : currentPOI.iconNonVisited;
        }

        // Drone heuristic: low altitude + speed or UAV hint in callsign
        if (spriteDrone != null && (latestAircraftData.altitude < 500f && latestAircraftData.velocity < 55f ||
            (!string.IsNullOrEmpty(latestAircraftData.callsign) && latestAircraftData.callsign.ToUpper().Contains("UAV"))))
        {
            return spriteDrone;
        }

        switch (latestAircraftData.type)
        {
            case TrafficDataManager.AircraftType.Helicopter:
                return spriteHelicopter ?? spriteSmallPlane ?? spriteDiamond;
            case TrafficDataManager.AircraftType.Commercial:
                return spriteCommercial ?? spriteJet ?? spriteSmallPlane ?? spriteDiamond;
            case TrafficDataManager.AircraftType.Military:
                return spriteJet ?? spriteCommercial ?? spriteSmallPlane ?? spriteDiamond;
            case TrafficDataManager.AircraftType.General:
                return spriteSmallPlane ?? spriteJet ?? spriteDiamond;
            default:
                return spriteDiamond ?? currentPOI.iconNonVisited;
        }
    }

    private Sprite GetSpriteForCloseDirectional()
    {
        if (latestAircraftData == null)
        {
            return GetMediumTypeSprite();
        }

        Transform cam = CompassPro.instance != null ? CompassPro.instance.cameraMain?.transform : null;
        if (cam == null)
        {
            return GetMediumTypeSprite();
        }

        Vector3 aircraftPos = transform.position;
        Vector3 pilotPos = cam.position;
        Vector3 toPilot = (pilotPos - aircraftPos);
        toPilot.y = 0f;
        if (toPilot.sqrMagnitude < 0.0001f) return GetMediumTypeSprite();
        toPilot.Normalize();

        Vector3 headingDir = Quaternion.Euler(0f, latestAircraftData.heading, 0f) * Vector3.forward;
        headingDir.y = 0f;
        headingDir.Normalize();

        float angleToPilot = Vector3.Angle(headingDir, toPilot); // 0 -> towards
        float signedSide = Vector3.SignedAngle(headingDir, toPilot, Vector3.up); // +left (CCW), -right (CW)
        bool isTowards = angleToPilot < 45f;
        bool isAway = angleToPilot > 135f;
        // Revert left/right mapping to match sprite orientation observed in runtime
        bool isRight = signedSide > 0f;

        float vr = latestAircraftData.verticalRate;
        const float verticalThreshold = 0.5f; // m/s
        bool climbing = vr > verticalThreshold;
        bool descending = vr < -verticalThreshold;

        string[] candidates;
        if (isTowards)
        {
            if (climbing)
            {
                candidates = new string[] { "UpwardTowards", "UpwardClimb", isRight ? "UpwardRight" : "UpwardLeft" };
            }
            else if (descending)
            {
                candidates = new string[] { "DownwardTowards", isRight ? "DownturnRight" : "DownwardLeftFacingTowards" };
            }
            else
            {
                candidates = new string[] { isRight ? "UpwardRight" : "UpwardLeft" };
            }
        }
        else if (isAway)/*  */
        {
            if (climbing)
            {
                candidates = new string[] { "UpwardClimb", isRight ? "UpwardRight2" : "UpwardLeft" };
            }
            else if (descending)
            {
                candidates = new string[] { isRight ? "DownturnRight" : "DownwardLeftFacingTowards", "DownwardTowards" };
            }
            else
            {
                candidates = new string[] { isRight ? "UpwardRight2" : "UpwardLeft" };
            }
        }
        else
        {
            if (climbing)
            {
                candidates = new string[] { isRight ? "UpwardRight" : "UpwardLeft", "UpwardClimb" };
            }
            else if (descending)
            {
                candidates = new string[] { isRight ? "DownturnRight" : "DownwardLeftFacingTowards", "DownwardTowards" };
            }
            else
            {
                candidates = new string[] { isRight ? "UpwardRight" : "UpwardLeft" };
            }
        }

        foreach (var key in candidates)
        {
            if (directionalSprites.TryGetValue(key, out var sp) && sp != null)
                return sp;
        }

        return GetMediumTypeSprite();
    }

    private void ApplySpriteToPOI(Sprite sprite, DisplayStage stage)
    {
        if (currentPOI == null || sprite == null) return;

        currentPOI.iconNonVisited = sprite;
        currentPOI.iconVisited = sprite;

        if (currentPOI.compassIconImage != null) currentPOI.compassIconImage.sprite = sprite;
        if (currentPOI.miniMapIconImage != null) currentPOI.miniMapIconImage.sprite = sprite;
        if (currentPOI.indicatorImage != null) currentPOI.indicatorImage.sprite = sprite;

        // Dynamic scale: base by stage, modulated by camera distance so icons are readable but not blocking
        float stageScale = stage == DisplayStage.Close ? closeIconScale : (stage == DisplayStage.Medium ? mediumIconScale : farIconScale);
        float dynamicMultiplier = 1f;
        var compass = CompassNavigatorPro.CompassPro.instance;
        if (compass != null && compass.cameraMain != null)
        {
            // Map distance (meters) to a comfortable readable range
            // Near clamp at 100m => 1.2x, mid at 1nm => 1.0x, far at 10nm => 0.8x
            float d = Mathf.Max(1f, currentPOI.distanceToFollow);
            float dNm = d / 1852f;
            float t = Mathf.InverseLerp(0.1f, 10f, dNm);
            dynamicMultiplier = Mathf.Lerp(1.2f, 0.8f, t);
        }
        currentPOI.iconScale = stageScale * dynamicMultiplier;
    }
    
    private void OnDestroy()
    {
        // Clean up POI when component is destroyed
        if (currentPOI != null)
        {           
            Destroy(currentPOI.gameObject);
        }
    }

    public void ConfigureIconProgression(float mediumRangeNm, float closeRangeNm, float farScale, float mediumScale, float closeScale)
    {
        this.mediumRangeNm = Mathf.Max(0.1f, mediumRangeNm);
        this.closeRangeNm = Mathf.Max(0.05f, closeRangeNm);
        this.farIconScale = Mathf.Max(0.1f, farScale);
        this.mediumIconScale = Mathf.Max(0.1f, mediumScale);
        this.closeIconScale = Mathf.Max(0.1f, closeScale);
    }
}
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     