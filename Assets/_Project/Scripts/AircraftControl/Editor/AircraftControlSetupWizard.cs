using UnityEngine;
using UnityEditor;
using AircraftControl.Core;
using AircraftControl.Camera;
using AircraftControl.Integration;

namespace AircraftControl.Editor
{
    /// <summary>
    /// Editor window for easy one-click setup of the Aircraft Control System.
    /// Access via menu: Tools > Aircraft Control > Setup Wizard
    /// </summary>
    public class AircraftControlSetupWizard : EditorWindow
    {
        #region Settings
        
        private bool createAircraftObject = true;
        private bool addCameraController = true;
        private bool addRadarBridge = true;
        private bool linkToMainCamera = true;
        
        private string aircraftName = "PlayerAircraft";
        private double initialLatitude = 33.6407;
        private double initialLongitude = -84.4277;
        private float initialAltitudeFeet = 10000f;
        private float initialHeading = 0f;
        
        private Vector2 scrollPosition;
        
        #endregion
        
        [MenuItem("Tools/Aircraft Control/Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<AircraftControlSetupWizard>("Aircraft Control Setup");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }
        
        [MenuItem("Tools/Aircraft Control/Quick Setup (One Click)")]
        public static void QuickSetup()
        {
            var wizard = CreateInstance<AircraftControlSetupWizard>();
            wizard.PerformSetup();
            DestroyImmediate(wizard);
        }
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            // Header
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Aircraft Control System Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This wizard will set up the complete aircraft control system including:\n" +
                "• Aircraft Controller (keyboard controls)\n" +
                "• Camera Controller (smooth mouse look)\n" +
                "• Radar Bridge (links to Weather & Traffic radar)", 
                MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // Components Section
            EditorGUILayout.LabelField("Components to Create", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            createAircraftObject = EditorGUILayout.Toggle("Aircraft Controller", createAircraftObject);
            
            EditorGUI.BeginDisabledGroup(!createAircraftObject);
            addCameraController = EditorGUILayout.Toggle("Camera Controller", addCameraController);
            addRadarBridge = EditorGUILayout.Toggle("Radar Bridge", addRadarBridge);
            linkToMainCamera = EditorGUILayout.Toggle("Link to Main Camera", linkToMainCamera);
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(10);
            
            // Aircraft Settings
            EditorGUILayout.LabelField("Aircraft Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            aircraftName = EditorGUILayout.TextField("Aircraft Name", aircraftName);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Initial Position", EditorStyles.miniLabel);
            initialLatitude = EditorGUILayout.DoubleField("Latitude", initialLatitude);
            initialLongitude = EditorGUILayout.DoubleField("Longitude", initialLongitude);
            initialAltitudeFeet = EditorGUILayout.FloatField("Altitude (ft)", initialAltitudeFeet);
            initialHeading = EditorGUILayout.FloatField("Heading (°)", initialHeading);
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(10);
            
            // Preset Locations
            EditorGUILayout.LabelField("Preset Locations", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Atlanta (KATL)"))
            {
                initialLatitude = 33.6407;
                initialLongitude = -84.4277;
            }
            if (GUILayout.Button("New York (JFK)"))
            {
                initialLatitude = 40.6413;
                initialLongitude = -73.7781;
            }
            if (GUILayout.Button("Los Angeles (LAX)"))
            {
                initialLatitude = 33.9416;
                initialLongitude = -118.4085;
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Chicago (ORD)"))
            {
                initialLatitude = 41.9742;
                initialLongitude = -87.9073;
            }
            if (GUILayout.Button("Dallas (DFW)"))
            {
                initialLatitude = 32.8998;
                initialLongitude = -97.0403;
            }
            if (GUILayout.Button("London (LHR)"))
            {
                initialLatitude = 51.4700;
                initialLongitude = -0.4543;
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(20);
            
            // Existing Components Detection
            DrawExistingComponentsInfo();
            
            EditorGUILayout.Space(10);
            
            // Setup Button
            EditorGUI.BeginDisabledGroup(!createAircraftObject);
            
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 14;
            buttonStyle.fontStyle = FontStyle.Bold;
            buttonStyle.fixedHeight = 40;
            
            if (GUILayout.Button("🚀 Setup Aircraft Control System", buttonStyle))
            {
                PerformSetup();
            }
            
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space(10);
            
            // Help
            EditorGUILayout.LabelField("Controls Reference", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Aircraft Controls:\n" +
                "  W/S - Pitch down/up\n" +
                "  A/D - Roll left/right\n" +
                "  Q/E - Yaw left/right\n" +
                "  Shift/Ctrl - Throttle up/down\n\n" +
                "Camera Controls:\n" +
                "  Right Mouse - Look around\n" +
                "  R - Reset view\n" +
                "  V - Cycle camera mode",
                MessageType.None);
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawExistingComponentsInfo()
        {
            EditorGUILayout.LabelField("Scene Status", EditorStyles.boldLabel);
            
            var existingAircraft = FindObjectOfType<AircraftController>();
            var existingCamera = FindObjectOfType<AircraftCameraController>();
            var existingBridge = FindObjectOfType<OwnAircraftRadarBridge>();
            var existingGeoManager = FindObjectOfType<GeoPosUnityPosProjectManager>();
            
            EditorGUILayout.BeginVertical("box");
            
            DrawStatusLine("AircraftController", existingAircraft != null);
            DrawStatusLine("AircraftCameraController", existingCamera != null);
            DrawStatusLine("OwnAircraftRadarBridge", existingBridge != null);
            DrawStatusLine("GeoPosUnityPosProjectManager", existingGeoManager != null);
            
            // Check for radar systems
            var trafficRadar = FindObjectOfType<TrafficRadar.Core.TrafficRadarController>();
            var weatherRadar = FindObjectOfType<WeatherRadar.WeatherRadarProviderBase>();
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Radar Systems:", EditorStyles.miniLabel);
            DrawStatusLine("TrafficRadarController", trafficRadar != null);
            DrawStatusLine("WeatherRadarProvider", weatherRadar != null);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawStatusLine(string name, bool exists)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(exists ? "✓" : "✗", GUILayout.Width(20));
            EditorGUILayout.LabelField(name);
            EditorGUILayout.LabelField(exists ? "Found" : "Not Found", 
                exists ? EditorStyles.miniLabel : EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }
        
        private void PerformSetup()
        {
            Undo.SetCurrentGroupName("Setup Aircraft Control System");
            int undoGroup = Undo.GetCurrentGroup();
            
            GameObject aircraftObj = null;
            AircraftController aircraftController = null;
            
            // Step 1: Create or find aircraft object
            if (createAircraftObject)
            {
                // Check if one already exists
                aircraftController = FindObjectOfType<AircraftController>();
                
                if (aircraftController == null)
                {
                    aircraftObj = new GameObject(aircraftName);
                    Undo.RegisterCreatedObjectUndo(aircraftObj, "Create Aircraft");
                    
                    aircraftController = aircraftObj.AddComponent<AircraftController>();
                    
                    // Set initial position via SerializedObject
                    SerializedObject so = new SerializedObject(aircraftController);
                    so.FindProperty("initialLatitude").doubleValue = initialLatitude;
                    so.FindProperty("initialLongitude").doubleValue = initialLongitude;
                    so.FindProperty("initialAltitudeFeet").floatValue = initialAltitudeFeet;
                    so.FindProperty("initialHeading").floatValue = initialHeading;
                    so.FindProperty("showDebugInfo").boolValue = true;
                    so.ApplyModifiedProperties();
                    
                    Debug.Log($"[AircraftControlSetup] Created AircraftController at {initialLatitude:F4}, {initialLongitude:F4}");
                }
                else
                {
                    aircraftObj = aircraftController.gameObject;
                    Debug.Log("[AircraftControlSetup] Using existing AircraftController");
                }
            }
            
            // Step 2: Setup camera controller
            if (addCameraController && aircraftController != null)
            {
                AircraftCameraController cameraController = FindObjectOfType<AircraftCameraController>();
                
                if (cameraController == null)
                {
                    UnityEngine.Camera mainCam = null;
                    
                    if (linkToMainCamera)
                    {
                        mainCam = UnityEngine.Camera.main;
                    }
                    
                    if (mainCam == null)
                    {
                        // Create a new camera
                        GameObject camObj = new GameObject("AircraftCamera");
                        Undo.RegisterCreatedObjectUndo(camObj, "Create Aircraft Camera");
                        
                        mainCam = camObj.AddComponent<UnityEngine.Camera>();
                        camObj.AddComponent<AudioListener>();
                        camObj.tag = "MainCamera";
                    }
                    
                    cameraController = mainCam.gameObject.GetComponent<AircraftCameraController>();
                    if (cameraController == null)
                    {
                        cameraController = mainCam.gameObject.AddComponent<AircraftCameraController>();
                        Undo.RegisterCreatedObjectUndo(cameraController, "Add Camera Controller");
                    }
                    
                    // Link to aircraft
                    SerializedObject camSo = new SerializedObject(cameraController);
                    camSo.FindProperty("aircraftTransform").objectReferenceValue = aircraftController.transform;
                    camSo.FindProperty("showDebugInfo").boolValue = true;
                    camSo.ApplyModifiedProperties();
                    
                    Debug.Log("[AircraftControlSetup] Created AircraftCameraController on " + mainCam.gameObject.name);
                }
                else
                {
                    Debug.Log("[AircraftControlSetup] Using existing AircraftCameraController");
                }
            }
            
            // Step 3: Setup radar bridge
            if (addRadarBridge && aircraftController != null)
            {
                OwnAircraftRadarBridge bridge = FindObjectOfType<OwnAircraftRadarBridge>();
                
                if (bridge == null)
                {
                    bridge = aircraftObj.AddComponent<OwnAircraftRadarBridge>();
                    Undo.RegisterCreatedObjectUndo(bridge, "Add Radar Bridge");
                    
                    SerializedObject bridgeSo = new SerializedObject(bridge);
                    bridgeSo.FindProperty("showDebugInfo").boolValue = true;
                    bridgeSo.ApplyModifiedProperties();
                    
                    Debug.Log("[AircraftControlSetup] Created OwnAircraftRadarBridge");
                }
                else
                {
                    Debug.Log("[AircraftControlSetup] Using existing OwnAircraftRadarBridge");
                }
            }
            
            // Step 4: Ensure GeoPosUnityPosProjectManager exists
            var geoManager = FindObjectOfType<GeoPosUnityPosProjectManager>();
            if (geoManager == null)
            {
                GameObject geoObj = new GameObject("GeoPosUnityPosProjectManager");
                Undo.RegisterCreatedObjectUndo(geoObj, "Create Geo Manager");
                
                geoManager = geoObj.AddComponent<GeoPosUnityPosProjectManager>();
                
                // Set origin to match aircraft position
                SerializedObject geoSo = new SerializedObject(geoManager);
                var defaultLatProp = geoSo.FindProperty("defaultLatitude");
                var defaultLonProp = geoSo.FindProperty("defaultLongitude");
                var originLatProp = geoSo.FindProperty("originLatitude");
                var originLonProp = geoSo.FindProperty("originLongitude");
                
                if (defaultLatProp != null) defaultLatProp.doubleValue = initialLatitude;
                if (defaultLonProp != null) defaultLonProp.doubleValue = initialLongitude;
                if (originLatProp != null) originLatProp.doubleValue = initialLatitude;
                if (originLonProp != null) originLonProp.doubleValue = initialLongitude;
                
                geoSo.ApplyModifiedProperties();
                
                Debug.Log("[AircraftControlSetup] Created GeoPosUnityPosProjectManager");
            }
            
            Undo.CollapseUndoOperations(undoGroup);
            
            // Select the aircraft
            if (aircraftObj != null)
            {
                Selection.activeGameObject = aircraftObj;
            }
            
            // Summary
            EditorUtility.DisplayDialog(
                "Aircraft Control Setup Complete",
                "The aircraft control system has been set up successfully!\n\n" +
                "Controls:\n" +
                "• W/S - Pitch\n" +
                "• A/D - Roll\n" +
                "• Q/E - Yaw\n" +
                "• Shift/Ctrl - Throttle\n" +
                "• Right Mouse - Look around\n" +
                "• R - Reset camera\n" +
                "• V - Cycle camera mode\n\n" +
                "Press Play to test!",
                "OK");
        }
    }
}
