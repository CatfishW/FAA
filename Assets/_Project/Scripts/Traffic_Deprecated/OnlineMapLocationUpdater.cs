using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OnlineMaps;

/// <summary>
/// OnlineMapLocationUpdater integrates traffic data with OnlineMaps and UserLocation components.
/// Key Features:
/// - Updates map center position based on traffic data or specific aircraft
/// - Synchronizes GPS emulator location with traffic data
/// - Creates and manages 3D markers for aircraft on the map with proper heading rotation
/// - Updates User Location compass with own aircraft heading from HeadingHUD
/// - Provides fallback location options
/// - Configurable update intervals and marker limits
/// - Aircraft markers display callsign, altitude, speed, and heading information
/// - Automatic marker rotation based on aircraft heading with configurable offsets
/// - Event-driven architecture for HeadingHUD integration
/// </summary>


public class OnlineMapLocationUpdater : MonoBehaviour
{
     [Header("Components")]
        [SerializeField] private TrafficDataManager trafficDataManager;
        [SerializeField] private UserLocation userLocation;
        [SerializeField] private Map onlineMaps;
        [SerializeField] private Marker3DManager marker3DManager;
        [SerializeField] private HeadingHUD headingHUD;
        
        [Header("Location Update Settings")]
        [SerializeField] private bool useTrafficDataForLocation = true;
        [SerializeField] private string targetAircraftICAO = ""; // Leave empty to use reference location
        [SerializeField] private bool followSpecificAircraft = false;
        [SerializeField] private bool updateMapPosition = true;
        [SerializeField] private bool updateEmulatedLocation = true;
        [SerializeField] private float updateInterval = 1.0f;
        
        [Header("Aircraft Markers")]
        [SerializeField] private bool showAircraftMarkers = true;
        [SerializeField] private GameObject aircraftMarkerPrefab;
        [SerializeField] private float aircraftMarkerScale = 1.0f;
        [SerializeField] private int maxAircraftMarkers = 50;
        [SerializeField] private Vector3 aircraftMarkerRotationOffset = Vector3.zero; // Additional rotation offset for aircraft model orientation
        
        [Header("Own Aircraft Settings")]
        [SerializeField] private bool updateOwnAircraftHeading = true;
        [SerializeField] private bool useOwnAircraftForCompass = true;
        
        [Header("Fallback Settings")]
        [SerializeField] private bool useFallbackLocation = true;
        [SerializeField] private double fallbackLatitude = 39.8283; // Washington DC area
        [SerializeField] private double fallbackLongitude = -98.5795; // Center of US
        
        private float lastUpdateTime;
        private bool isInitialized = false;
        
        // Aircraft marker tracking
        private Dictionary<string, Marker3D> aircraftMarkers = new Dictionary<string, Marker3D>();
        
        private void Start()
        {
            InitializeComponents();
        }
        
        private void InitializeComponents()
        {
            // Auto-find components if not assigned
            if (trafficDataManager == null)
                trafficDataManager = FindObjectOfType<TrafficDataManager>();
            
            if (userLocation == null)
                userLocation = FindObjectOfType<UserLocation>();
            
            if (onlineMaps == null)
                onlineMaps = FindObjectOfType<Map>();
            
            if (marker3DManager == null)
                marker3DManager = FindObjectOfType<Marker3DManager>();
            
            if (headingHUD == null)
                headingHUD = FindObjectOfType<HeadingHUD>();
            
            // Validate components
            if (trafficDataManager == null)
            {
                Debug.LogError("[OnlinemapLocationUpdate] TrafficDataManager not found!");
                return;
            }
            
            if (userLocation == null)
            {
                Debug.LogError("[OnlinemapLocationUpdate] UserLocation not found!");
                return;
            }
            
            // Subscribe to traffic data updates
            trafficDataManager.onDataUpdated.AddListener(OnTrafficDataUpdated);
            
            // Subscribe to HeadingHUD changes if available
            if (headingHUD != null && useOwnAircraftForCompass)
            {
                headingHUD.OnHeadingChanged.AddListener(OnHeadingHUDChanged);
            }
            
            // Set up custom location provider for UserLocation
            if (useTrafficDataForLocation)
            {
                userLocation.OnGetLocation += GetCustomLocation;
            }
            
            isInitialized = true;
            Debug.Log("[OnlinemapLocationUpdate] Initialized successfully");
        }
        
        private void Update()
        {
            if (!isInitialized || !useTrafficDataForLocation) return;
            
            // Update at specified interval
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                lastUpdateTime = Time.time;
                UpdateLocationFromTrafficData();
            }
        }
        
        private void OnTrafficDataUpdated(List<TrafficDataManager.AircraftData> aircraftList)
        {
            if (!useTrafficDataForLocation || !isInitialized) return;
            
            // Force an immediate location update when new traffic data arrives
            UpdateLocationFromTrafficData();
            
            // Update aircraft markers
            if (showAircraftMarkers && marker3DManager != null)
            {
                UpdateAircraftMarkers(aircraftList);
            }
        }
        
        private void OnHeadingHUDChanged(float newHeading)
        {
            if (useOwnAircraftForCompass && userLocation != null)
            {
                userLocation.emulatedCompass = newHeading;
                Debug.Log($"[OnlinemapLocationUpdate] HeadingHUD changed, updated User Location compass to: {newHeading:F1}°");
            }
        }
        
        private GeoPoint GetCustomLocation()
        {
            if (followSpecificAircraft && !string.IsNullOrEmpty(targetAircraftICAO))
            {
                // Follow a specific aircraft
                var aircraft = GetAircraftByICAO(targetAircraftICAO);
                if (aircraft != null)
                {
                    return new GeoPoint(aircraft.longitude, aircraft.latitude);
                }
            }
            
            // Use reference location from traffic data manager
            if (trafficDataManager != null)
            {
                return new GeoPoint(trafficDataManager.referenceLongitude, trafficDataManager.referenceLatitude);
            }
            
            // Fallback location
            if (useFallbackLocation)
            {
                return new GeoPoint(fallbackLongitude, fallbackLatitude);
            }
            
            return new GeoPoint(0, 0);
        }
        
        private void UpdateLocationFromTrafficData()
        {
            GeoPoint location = GetCustomLocation();
            
            // Update map position if enabled
            if (updateMapPosition && onlineMaps != null && location.longitude != 0 && location.latitude != 0)
            {
                onlineMaps.view.center = location;
            }
            
            // Update emulated location if enabled
            if (updateEmulatedLocation && userLocation != null && location.longitude != 0 && location.latitude != 0)
            {
                userLocation.emulatedLocation = location;
            }
            
            // Update User Location compass with heading from HeadingHUD
            if (useOwnAircraftForCompass && headingHUD != null && userLocation != null)
            {
                userLocation.emulatedCompass = headingHUD.currentHeading;
                if (Time.time - lastUpdateTime < 1.0f) // Only log occasionally to avoid spam
                {
                    Debug.Log($"[OnlinemapLocationUpdate] Updated User Location compass to: {headingHUD.currentHeading:F1}°");
                }
            }
        }
        
        private TrafficDataManager.AircraftData GetAircraftByICAO(string icao)
        {
            if (trafficDataManager == null || trafficDataManager.aircraftList == null) return null;
            
            foreach (var aircraft in trafficDataManager.aircraftList)
            {
                if (aircraft.icao24.Equals(icao, System.StringComparison.OrdinalIgnoreCase))
                {
                    return aircraft;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Updates aircraft markers on the map based on traffic data
        /// </summary>
        private void UpdateAircraftMarkers(List<TrafficDataManager.AircraftData> aircraftList)
        {
            if (marker3DManager == null) return;
            
            HashSet<string> currentAircraftICAOs = new HashSet<string>();
            int markersAdded = 0;
            
            // Update existing markers and add new ones
            foreach (var aircraft in aircraftList)
            {
                if (markersAdded >= maxAircraftMarkers) break;
                if (aircraft.latitude == 0 && aircraft.longitude == 0) continue;
                
                currentAircraftICAOs.Add(aircraft.icao24);
                
                if (aircraftMarkers.TryGetValue(aircraft.icao24, out Marker3D existingMarker))
                {
                    // Update existing marker position
                    existingMarker.location = new GeoPoint(aircraft.longitude, aircraft.latitude);
                    
                    // Update marker rotation based on aircraft heading
                    if (existingMarker.transform != null)
                    {
                        Quaternion headingRotation = Quaternion.Euler(aircraftMarkerRotationOffset.x, aircraft.heading + aircraftMarkerRotationOffset.y, aircraftMarkerRotationOffset.z);
                        existingMarker.transform.rotation = headingRotation;
                    }
                    
                    // Update marker label with aircraft info
                    string label = $"{aircraft.callsign}\n{aircraft.altitude:F0}ft\n{aircraft.velocity * 1.94384f:F0}kts\nHDG: {aircraft.heading:F0}°"; // Convert m/s to knots
                    existingMarker.label = label;
                }
                else
                {
                    // Create new marker for aircraft
                    if (aircraftMarkerPrefab != null)
                    {
                        Marker3D newMarker = marker3DManager.Create(
                            aircraft.longitude, 
                            aircraft.latitude, 
                            aircraftMarkerPrefab,
                            $"{aircraft.callsign}\n{aircraft.altitude:F0}ft\n{aircraft.velocity * 1.94384f:F0}kts\nHDG: {aircraft.heading:F0}°"
                        );
                        
                        newMarker.scale = aircraftMarkerScale;
                        
                        // Set initial rotation based on aircraft heading
                        if (newMarker.transform != null)
                        {
                            Quaternion headingRotation = Quaternion.Euler(aircraftMarkerRotationOffset.x, aircraft.heading + aircraftMarkerRotationOffset.y, aircraftMarkerRotationOffset.z);
                            newMarker.transform.rotation = headingRotation;
                        }
                        else
                        {
                            // If transform isn't available yet, set up a callback to apply rotation when it's ready
                            newMarker.OnInitComplete += (marker) => 
                            {
                                if ((marker as Marker3D).transform != null)
                                {
                                    Quaternion headingRotation = Quaternion.Euler(aircraftMarkerRotationOffset.x, aircraft.heading + aircraftMarkerRotationOffset.y, aircraftMarkerRotationOffset.z);
                                    (marker as Marker3D).transform.rotation = headingRotation;
                                }
                            };
                        }
                        
                        aircraftMarkers[aircraft.icao24] = newMarker;
                        markersAdded++;
                    }
                }
            }
            
            // Remove markers for aircraft that are no longer in the data
            List<string> markersToRemove = new List<string>();
            foreach (var kvp in aircraftMarkers)
            {
                if (!currentAircraftICAOs.Contains(kvp.Key))
                {
                    markersToRemove.Add(kvp.Key);
                }
            }
            
            foreach (string icao in markersToRemove)
            {
                if (aircraftMarkers.TryGetValue(icao, out Marker3D markerToRemove))
                {
                    marker3DManager.Remove(markerToRemove);
                    aircraftMarkers.Remove(icao);
                }
            }
        }
        
        /// <summary>
        /// Clears all aircraft markers from the map
        /// </summary>
        private void ClearAllAircraftMarkers()
        {
            if (marker3DManager == null) return;
            
            foreach (var kvp in aircraftMarkers)
            {
                marker3DManager.Remove(kvp.Value);
            }
            aircraftMarkers.Clear();
        }
        
        /// <summary>
        /// Set a specific aircraft to follow by ICAO code
        /// </summary>
        /// <param name="icao">ICAO code of the aircraft to follow</param>
        public void SetTargetAircraft(string icao)
        {
            targetAircraftICAO = icao;
            followSpecificAircraft = !string.IsNullOrEmpty(icao);
            Debug.Log($"[OnlinemapLocationUpdate] Target aircraft set to: {icao}");
        }
        
        /// <summary>
        /// Stop following specific aircraft and use reference location
        /// </summary>
        public void ClearTargetAircraft()
        {
            targetAircraftICAO = "";
            followSpecificAircraft = false;
            Debug.Log("[OnlinemapLocationUpdate] Cleared target aircraft, using reference location");
        }
        
        /// <summary>
        /// Toggle between using traffic data and GPS for location
        /// </summary>
        /// <param name="useTrafficData">True to use traffic data, false to use GPS</param>
        public void SetLocationSource(bool useTrafficData)
        {
            useTrafficDataForLocation = useTrafficData;
            
            if (userLocation != null)
            {
                if (useTrafficData)
                {
                    userLocation.OnGetLocation += GetCustomLocation;
                }
                else
                {
                    userLocation.OnGetLocation -= GetCustomLocation;
                }
            }
            
            Debug.Log($"[OnlinemapLocationUpdate] Location source set to: {(useTrafficData ? "Traffic Data" : "GPS")}");
        }
        
        /// <summary>
        /// Enable or disable aircraft markers on the map
        /// </summary>
        /// <param name="enabled">True to show aircraft markers, false to hide them</param>
        public void SetAircraftMarkersEnabled(bool enabled)
        {
            showAircraftMarkers = enabled;
            
            if (!enabled)
            {
                ClearAllAircraftMarkers();
            }
            
            Debug.Log($"[OnlinemapLocationUpdate] Aircraft markers: {(enabled ? "Enabled" : "Disabled")}");
        }
        
        /// <summary>
        /// Set the maximum number of aircraft markers to display
        /// </summary>
        /// <param name="maxMarkers">Maximum number of markers</param>
        public void SetMaxAircraftMarkers(int maxMarkers)
        {
            maxAircraftMarkers = Mathf.Max(0, maxMarkers);
            Debug.Log($"[OnlinemapLocationUpdate] Max aircraft markers set to: {maxAircraftMarkers}");
        }
        
        /// <summary>
        /// Enable or disable using own aircraft heading for User Location compass
        /// </summary>
        /// <param name="enabled">True to use own aircraft heading for compass, false to use default compass</param>
        public void SetUseOwnAircraftForCompass(bool enabled)
        {
            useOwnAircraftForCompass = enabled;
            Debug.Log($"[OnlinemapLocationUpdate] Use own aircraft for compass: {(enabled ? "Enabled" : "Disabled")}");
        }

        /// <summary>
        /// Manually update the User Location compass with a specific heading
        /// </summary>
        /// <param name="heading">Heading in degrees (0-360)</param>
        public void UpdateUserLocationCompass(float heading)
        {
            if (userLocation != null)
            {
                userLocation.emulatedCompass = heading;
                Debug.Log($"[OnlinemapLocationUpdate] Updated User Location compass to: {heading:F1}°");
            }
        }

        /// <summary>
        /// Set the rotation offset for aircraft markers (useful for correcting model orientation)
        /// </summary>
        /// <param name="rotationOffset">Rotation offset in degrees (X, Y, Z)</param>
        public void SetAircraftMarkerRotationOffset(Vector3 rotationOffset)
        {
            aircraftMarkerRotationOffset = rotationOffset;
            Debug.Log($"[OnlinemapLocationUpdate] Aircraft marker rotation offset set to: {rotationOffset}");
        }
        
        /// <summary>
        /// Get the current location being used
        /// </summary>
        /// <returns>Current geo location</returns>
        public GeoPoint GetCurrentLocation()
        {
            return GetCustomLocation();
        }
        
        private void OnDestroy()
        {
            // Clean up subscriptions
            if (trafficDataManager != null)
            {
                trafficDataManager.onDataUpdated.RemoveListener(OnTrafficDataUpdated);
            }
            
            if (headingHUD != null)
            {
                headingHUD.OnHeadingChanged.RemoveListener(OnHeadingHUDChanged);
            }
            
            if (userLocation != null)
            {
                userLocation.OnGetLocation -= GetCustomLocation;
            }
            
            // Clear all aircraft markers
            ClearAllAircraftMarkers();
        }
        
        private void OnValidate()
        {
            // Ensure update interval is reasonable
            if (updateInterval < 0.1f)
                updateInterval = 0.1f;
                
            // Ensure aircraft marker scale is reasonable
            if (aircraftMarkerScale < 0.1f)
                aircraftMarkerScale = 0.1f;
                
            // Ensure max aircraft markers is reasonable
            if (maxAircraftMarkers < 0)
                maxAircraftMarkers = 0;
        }
    
}
