using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrafficDataDebugger : MonoBehaviour
{
    private TrafficDataManager m_trafficDataManager;
    
    [Header("Debug Settings")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool logUpdatesToConsole = false;
    [SerializeField] private bool drawAircraftGizmos = true;
    [SerializeField] private float gizmoSize = 1.0f;
    [SerializeField] private bool showTrajectories = false;
    [SerializeField] private float trajectoryMinutes = 5.0f;
    
    [Header("UI Settings")]
    [SerializeField] private int uiWidth = 500;
    [SerializeField] private int uiHeight = 700;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    [SerializeField] private GUISkin customSkin;
    
    [Header("Filters")]
    [SerializeField] private TrafficDataManager.AircraftCategory filterCategory = TrafficDataManager.AircraftCategory.MidAltitude;
    [SerializeField] private bool useFilterCategory = false;
    [SerializeField] private float filterRadiusKm = 100f;
    [SerializeField] private bool useFilterRadius = false;
    private Dictionary<string, Vector2> previousPositions = new Dictionary<string, Vector2>();
    private HashSet<string> changedAircraftIds = new HashSet<string>();
    
    // Colors for different aircraft types
    private readonly Color[] typeColors = new Color[] {
        Color.gray,        // Unknown
        Color.green,       // Commercial
        Color.red,         // Military
        Color.blue,        // General
        Color.yellow       // Helicopter
    };
    
    private Vector2 scrollPosition;
    private string selectedAircraftId = null;
    private TrafficDataManager.AircraftData selectedAircraft = null;
    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;
    private GUIStyle labelStyle;
    private GUIStyle boxStyle;
    private GUIStyle toggleStyle;
    private Rect windowRect;
    private bool initialized = false;
    
    private void Awake()
    {
        m_trafficDataManager = FindObjectOfType<TrafficDataManager>();
        if (m_trafficDataManager == null)
        {
            Debug.LogError("TrafficDataManager not found in the scene.");
        }
        InitializeStyles();
    }
    
    private void OnEnable()
    {
        if (m_trafficDataManager != null)
        {
            m_trafficDataManager.onDataUpdated.AddListener(OnTrafficDataUpdated);
        }
    }
    
    private void OnDisable()
    {
        if (m_trafficDataManager != null)
        {
            m_trafficDataManager.onDataUpdated.RemoveListener(OnTrafficDataUpdated);
        }
    }
    
    private void Start()
    {
        // Center the window
        windowRect = new Rect(
            (Screen.width - uiWidth) / 2,
            (Screen.height - uiHeight) / 2,
            uiWidth,
            uiHeight
        );
        
        // Force initial data fetch
        if (m_trafficDataManager != null && !m_trafficDataManager.IsActive)
        {
            m_trafficDataManager.FetchDataNow();
        }
    }
    
    private void InitializeStyles()
    {
        headerStyle = new GUIStyle(GUI.skin.box);
        headerStyle.fontSize = 16;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.alignment = TextAnchor.MiddleCenter;
        headerStyle.normal.textColor = Color.white;
        
        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 14;
        buttonStyle.padding = new RectOffset(10, 10, 6, 6);
        
        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 14;
        labelStyle.wordWrap = true;
        
        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.textColor = Color.white;
        boxStyle.fontSize = 14;
        boxStyle.alignment = TextAnchor.MiddleLeft;
        boxStyle.margin = new RectOffset(5, 5, 5, 5);
        boxStyle.padding = new RectOffset(10, 10, 10, 10);
        
        toggleStyle = new GUIStyle(GUI.skin.toggle);
        toggleStyle.fontSize = 14;
        
        initialized = true;
    }
    
    private void Update()
    {
        // Toggle debug UI with key press
        if (Input.GetKeyDown(toggleKey))
        {
            showDebugInfo = !showDebugInfo;
        }
    }
    
    private void OnTrafficDataUpdated(List<TrafficDataManager.AircraftData> aircraftList)
    {
        if (logUpdatesToConsole)
        {
            Debug.Log($"[TrafficDataDebugger] Received update with {aircraftList.Count} aircraft");
        }

        changedAircraftIds.Clear();

        foreach (var aircraft in aircraftList)
        {
            Vector2 currentPosition = new Vector2(aircraft.latitude, aircraft.longitude);

            if (previousPositions.TryGetValue(aircraft.icao24, out Vector2 previousPosition))
            {
                if (currentPosition != previousPosition)
                {
                    changedAircraftIds.Add(aircraft.icao24);
                }
            }

            previousPositions[aircraft.icao24] = currentPosition;
        }

        // Update selected aircraft if we have one
        if (!string.IsNullOrEmpty(selectedAircraftId))
        {
            selectedAircraft = m_trafficDataManager.GetAircraftByIcao(selectedAircraftId);
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!drawAircraftGizmos || m_trafficDataManager == null) return;
        
        var aircraftList = m_trafficDataManager.AircraftList;
        foreach (var aircraft in aircraftList)
        {
            if (ShouldShowAircraft(aircraft))
            {
                // Scale gizmo size based on altitude
                float scale = gizmoSize * (0.5f + aircraft.altitude / 10000f);
                
                // Convert lat/long to a local position (simplified)
                Vector3 position = ConvertLatLongToPosition(aircraft.latitude, aircraft.longitude, aircraft.altitude);
                
                // Set color based on type
                Gizmos.color = typeColors[(int)aircraft.type];
                
                // Draw aircraft
                Gizmos.DrawSphere(position, scale);
                
                // Draw heading line
                if (aircraft.velocity > 1.0f)
                {
                    Vector3 headingDir = Quaternion.Euler(0, aircraft.heading, 0) * Vector3.forward;
                    Gizmos.DrawLine(position, position + headingDir * scale * 3);
                }
                
                // Draw predicted trajectory
                if (showTrajectories)
                {
                    Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.5f);
                    Vector2 predictedPos = aircraft.GetPredictedPosition(trajectoryMinutes);
                    Vector3 targetPos = ConvertLatLongToPosition(predictedPos.y, predictedPos.x, aircraft.altitude);
                    Gizmos.DrawLine(position, targetPos);
                }
            }
        }
    }
    
    private void OnGUI()
    {
        if (!initialized) InitializeStyles();
        
        // Always show toggle button in the corner
        if (GUI.Button(new Rect(Screen.width - 150, 10, 130, 30), showDebugInfo ? "Hide Debug UI" : "Show Debug UI", buttonStyle))
        {
            showDebugInfo = !showDebugInfo;
        }
        
        if (!showDebugInfo || m_trafficDataManager == null) return;
        
        // Draw the main debug window
        windowRect = GUILayout.Window(0, windowRect, DrawDebugWindow, "Traffic Data Debugger");
    }
    
    private void DrawDebugWindow(int windowID)
    {
        GUILayout.Space(10);
        
        if (m_trafficDataManager.IsActive)
        {
            GUILayout.Label($"Status: Active, {m_trafficDataManager.AircraftCount} aircraft", labelStyle);
            GUILayout.Label($"Last Update: {m_trafficDataManager.LastSuccessfulFetch.ToShortTimeString()}", labelStyle);
        }
        else
        {
            GUILayout.Label("Status: Inactive", labelStyle);
        }
        
        if (GUILayout.Button(m_trafficDataManager.IsActive ? "Stop Fetching" : "Start Fetching", buttonStyle))
        {
            if (m_trafficDataManager.IsActive)
                m_trafficDataManager.StopFetching();
            else
                m_trafficDataManager.StartFetching();
        }
        
        if (GUILayout.Button("Fetch Data Now", buttonStyle))
        {
            m_trafficDataManager.FetchDataNow();
        }
        
        GUILayout.Space(10);
        GUILayout.Label("Filter Options:", headerStyle);
        
        useFilterCategory = GUILayout.Toggle(useFilterCategory, "Filter by Category", toggleStyle);
        if (useFilterCategory)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Category:", labelStyle, GUILayout.Width(80));
            if (GUILayout.Button(filterCategory.ToString(), buttonStyle))
            {
                // Cycle through categories
                filterCategory = (TrafficDataManager.AircraftCategory)(((int)filterCategory + 1) % 4);
            }
            GUILayout.EndHorizontal();
        }
        
        useFilterRadius = GUILayout.Toggle(useFilterRadius, "Filter by Radius", toggleStyle);
        if (useFilterRadius)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Radius (km):", labelStyle, GUILayout.Width(100));
            string radiusStr = GUILayout.TextField(filterRadiusKm.ToString(), GUILayout.Width(100));
            float.TryParse(radiusStr, out filterRadiusKm);
            GUILayout.EndHorizontal();
        }
        
        GUILayout.Space(10);
        GUILayout.Label("Aircraft List", headerStyle);
        
        // Check if we have aircraft data
        var aircraftList = m_trafficDataManager.AircraftList;
        if (aircraftList == null || aircraftList.Count == 0)
        {
            GUILayout.Box("No aircraft data available.\nTry clicking 'Fetch Data Now'.", boxStyle);
        }
        else
        {
            // Calculate how many visible aircraft after filtering
            int visibleCount = 0;
            foreach (var aircraft in aircraftList)
            {
                if (ShouldShowAircraft(aircraft))
                {
                    visibleCount++;
                }
            }
            
            if (visibleCount == 0)
            {
                GUILayout.Box("No aircraft match current filters.\nTry adjusting filter settings.", boxStyle);
            }
            else
            {
                GUILayout.Label($"Showing {visibleCount} of {aircraftList.Count} aircraft", labelStyle);
                
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(250));
                
                foreach (var aircraft in aircraftList)
                {
                    if (ShouldShowAircraft(aircraft))
                    {
                        string displayName = string.IsNullOrEmpty(aircraft.callsign) ? 
                            aircraft.icao24 : $"{aircraft.callsign} ({aircraft.icao24})";
                        
                        GUI.backgroundColor = (selectedAircraftId == aircraft.icao24) ? Color.green : Color.grey;
                        
                        if (GUILayout.Button(displayName, buttonStyle, GUILayout.Height(30)))
                        {
                            selectedAircraftId = aircraft.icao24;
                            selectedAircraft = aircraft;
                        }
                        
                        GUI.backgroundColor = Color.white;
                    }
                }
                
                GUILayout.EndScrollView();
            }
        }
        
        // Show details for selected aircraft
        if (selectedAircraft != null)
        {
            GUILayout.Space(10);
            GUILayout.Label($"Details for {selectedAircraft.callsign}", headerStyle);
            
            GUILayout.BeginVertical(boxStyle);
            
            GUILayout.Label($"ICAO24: {selectedAircraft.icao24}", labelStyle);
            GUILayout.Label($"Country: {selectedAircraft.originCountry}", labelStyle);
            GUILayout.Label($"Position: {selectedAircraft.latitude:F4}, {selectedAircraft.longitude:F4}", labelStyle);
            GUILayout.Label($"Altitude: {selectedAircraft.altitude:F0}m", labelStyle);
            GUILayout.Label($"Speed: {selectedAircraft.velocity:F0}m/s ({selectedAircraft.velocity * 3.6f:F0}km/h)", labelStyle);
            GUILayout.Label($"Heading: {selectedAircraft.heading:F0}°", labelStyle);
            GUILayout.Label($"Vertical Rate: {selectedAircraft.verticalRate:F1}m/s", labelStyle);
            GUILayout.Label($"On Ground: {selectedAircraft.onGround}", labelStyle);
            GUILayout.Label($"Type: {selectedAircraft.type}", labelStyle);
            GUILayout.Label($"Category: {selectedAircraft.Category}", labelStyle);
            
            GUILayout.EndVertical();
        }
        
        // Make window draggable
        GUI.DragWindow();
    }
    
    private bool ShouldShowAircraft(TrafficDataManager.AircraftData aircraft)
    {
        if (useFilterCategory && aircraft.Category != filterCategory)
            return false;
        
        if (useFilterRadius)
        {
            // Get origin position (you might want to customize this based on your scene)
            Vector3 origin = transform.position;
            float distance = CalculateDistanceKm(origin.z, origin.x, aircraft.latitude, aircraft.longitude);
            if (distance > filterRadiusKm)
                return false;
        }
        
        return true;
    }
    
    private Vector3 ConvertLatLongToPosition(float latitude, float longitude, float altitude)
    {
        // This is a simple conversion for visualization purposes
        // In a real application, you would use proper map projection
        float scale = 100.0f; // Scale factor to make visualization fit in scene
        return new Vector3(longitude * scale, altitude / 1000f, latitude * scale);
    }
    
    private float CalculateDistanceKm(float lat1, float lon1, float lat2, float lon2)
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
    
}