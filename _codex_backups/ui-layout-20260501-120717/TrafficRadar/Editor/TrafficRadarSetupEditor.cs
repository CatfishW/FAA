using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using TrafficRadar;
using TrafficRadar.Core;

/// <summary>
/// One-click setup wizard for the Traffic Radar System.
/// Creates a complete FAA TCAS-compliant radar display with all components.
/// </summary>
public class TrafficRadarSetupEditor : EditorWindow
{
    #region Settings
    
    private bool createDataManager = true;
    private bool createUI = true;
    
    private string systemName = "Traffic Radar System";
    private double initialLatitude = 33.6407;
    private double initialLongitude = -84.4277;
    private float radiusKm = 150f;
    
    private Canvas targetCanvas;
    private Vector2 scrollPosition;
    
    #endregion
    
    [MenuItem("Tools/Traffic Radar/Setup Wizard")]
    public static void ShowWindow()
    {
        var window = GetWindow<TrafficRadarSetupEditor>("Traffic Radar Setup");
        window.minSize = new Vector2(420, 550);
        window.Show();
    }

    [MenuItem("Tools/Traffic Radar/One-Click Complete Setup")]
    public static void OneClickSetup()
    {
        var wizard = CreateInstance<TrafficRadarSetupEditor>();
        wizard.PerformSetup();
        DestroyImmediate(wizard);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        // Header
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Traffic Radar System Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This wizard creates a complete FAA TCAS-compliant Traffic Radar System with:\n" +
            "• Live aircraft data from Airplanes.live API\n" +
            "• Threat-level classification (RA/TA/Proximate/Other)\n" +
            "• Circular radar display with range rings\n" +
            "• FAA sectional chart tile background\n" +
            "• Auto-range adjustment for optimal viewing",
            MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        // Components Section
        EditorGUILayout.LabelField("Components to Create", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        
        createDataManager = EditorGUILayout.Toggle("Data Manager (API)", createDataManager);
        createUI = EditorGUILayout.Toggle("UI Display", createUI);
        
        EditorGUI.indentLevel--;
        
        EditorGUILayout.Space(10);
        
        // Position Settings
        EditorGUILayout.LabelField("Initial Position", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        
        initialLatitude = EditorGUILayout.DoubleField("Latitude", initialLatitude);
        initialLongitude = EditorGUILayout.DoubleField("Longitude", initialLongitude);
        radiusKm = EditorGUILayout.FloatField("Fetch Radius (km)", radiusKm);
        
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
        
        EditorGUILayout.Space(10);
        
        // Optional References
        EditorGUILayout.LabelField("Optional References", EditorStyles.boldLabel);
        targetCanvas = (Canvas)EditorGUILayout.ObjectField("Target Canvas", targetCanvas, typeof(Canvas), true);
        
        EditorGUILayout.Space(10);
        
        // Existing Components Detection
        DrawSceneStatus();
        
        EditorGUILayout.Space(10);
        
        // Setup Button
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            fixedHeight = 40
        };
        
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("🚀 Create Traffic Radar System", buttonStyle))
        {
            PerformSetup();
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.Space(10);
        
        // Threat Level Legend
        EditorGUILayout.LabelField("FAA TCAS Threat Level Colors", EditorStyles.boldLabel);
        DrawColorBox(new Color(0f, 1f, 1f, 0.8f), "Other Traffic (Cyan Diamond)");
        DrawColorBox(new Color(0f, 1f, 1f, 1f), "Proximate (Cyan Filled Diamond)");
        DrawColorBox(new Color(1f, 0.75f, 0f, 1f), "Traffic Advisory (Amber Circle)");
        DrawColorBox(new Color(1f, 0f, 0f, 1f), "Resolution Advisory (Red Square)");
        
        EditorGUILayout.EndScrollView();
    }

    private void DrawSceneStatus()
    {
        EditorGUILayout.LabelField("Scene Status", EditorStyles.boldLabel);
        
        var controller = FindObjectOfType<TrafficRadarController>();
        var dataManager = FindObjectOfType<TrafficRadarDataManager>();
        var display = FindObjectOfType<TrafficRadarDisplay>();
        var chartProvider = FindObjectOfType<FAASectionalChartProvider>();
        
        EditorGUILayout.BeginVertical("box");
        
        DrawStatusLine("TrafficRadarController", controller != null);
        DrawStatusLine("TrafficRadarDataManager", dataManager != null);
        DrawStatusLine("TrafficRadarDisplay", display != null);
        DrawStatusLine("FAASectionalChartProvider", chartProvider != null);
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawStatusLine(string name, bool exists)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(exists ? "✓" : "✗", GUILayout.Width(20));
        EditorGUILayout.LabelField(name);
        EditorGUILayout.LabelField(exists ? "Found" : "Not Found", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawColorBox(Color color, string label)
    {
        EditorGUILayout.BeginHorizontal();
        var rect = GUILayoutUtility.GetRect(20, 16);
        EditorGUI.DrawRect(rect, color);
        EditorGUILayout.LabelField(label);
        EditorGUILayout.EndHorizontal();
    }

    private void PerformSetup()
    {
        Undo.SetCurrentGroupName("Create Traffic Radar System");
        int undoGroup = Undo.GetCurrentGroup();
        
        // ========== CREATE SYSTEM ROOT ==========
        GameObject systemRoot = new GameObject(systemName);
        Undo.RegisterCreatedObjectUndo(systemRoot, "Create Traffic Radar System");
        
        // ========== DATA MANAGER ==========
        TrafficRadarDataManager dataManager = FindObjectOfType<TrafficRadarDataManager>();
        
        if (createDataManager && dataManager == null)
        {
            GameObject dmGO = new GameObject("Data Manager");
            dmGO.transform.SetParent(systemRoot.transform);
            dataManager = dmGO.AddComponent<TrafficRadarDataManager>();
            
            SerializedObject dmSO = new SerializedObject(dataManager);
            dmSO.FindProperty("radiusFilterKm").floatValue = radiusKm;
            dmSO.FindProperty("referenceLatitude").floatValue = (float)initialLatitude;
            dmSO.FindProperty("referenceLongitude").floatValue = (float)initialLongitude;
            dmSO.FindProperty("autoStartFetching").boolValue = true;
            dmSO.ApplyModifiedProperties();
            
            Debug.Log("[Traffic Radar Setup] Created TrafficRadarDataManager");
        }
        
        // ========== CONTROLLER ==========
        TrafficRadarController controller = systemRoot.AddComponent<TrafficRadarController>();
        
        SerializedObject ctrlSO = new SerializedObject(controller);
        ctrlSO.FindProperty("dataManager").objectReferenceValue = dataManager;
        ctrlSO.FindProperty("verboseLogging").boolValue = true;
        ctrlSO.FindProperty("autoRangeEnabled").boolValue = true;
        ctrlSO.ApplyModifiedProperties();
        
        Debug.Log("[Traffic Radar Setup] Created TrafficRadarController");
        
        // ========== UI SETUP ==========
        TrafficRadarDisplay display = null;
        FAASectionalChartProvider chartProvider = null;
        
        if (createUI)
        {
            // Find or create canvas
            Canvas canvas = targetCanvas;
            if (canvas == null)
                canvas = FindObjectOfType<Canvas>();
            
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("Radar Canvas");
                canvasGO.transform.SetParent(systemRoot.transform);
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }
            
            // Create display container
            GameObject displayContainer = new GameObject("Radar Display");
            displayContainer.transform.SetParent(canvas.transform);
            
            RectTransform displayRect = displayContainer.AddComponent<RectTransform>();
            displayRect.anchorMin = new Vector2(0, 0);
            displayRect.anchorMax = new Vector2(0, 0);
            displayRect.pivot = new Vector2(0, 0);
            displayRect.anchoredPosition = new Vector2(20, 20);
            displayRect.sizeDelta = new Vector2(350, 350);
            
            // Background
            Image bg = displayContainer.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.1f, 0.15f, 0.95f);
            displayContainer.AddComponent<Mask>().showMaskGraphic = true;
            
            // Chart background
            GameObject chartBgGO = new GameObject("Chart Background");
            chartBgGO.transform.SetParent(displayContainer.transform);
            RectTransform chartRect = chartBgGO.AddComponent<RectTransform>();
            chartRect.anchorMin = Vector2.zero;
            chartRect.anchorMax = Vector2.one;
            chartRect.sizeDelta = Vector2.zero;
            chartRect.anchoredPosition = Vector2.zero;
            RawImage chartImage = chartBgGO.AddComponent<RawImage>();
            chartImage.color = new Color(1, 1, 1, 0.4f);
            
            // Radar image
            GameObject radarImgGO = new GameObject("Radar Image");
            radarImgGO.transform.SetParent(displayContainer.transform);
            RectTransform radarRect = radarImgGO.AddComponent<RectTransform>();
            radarRect.anchorMin = Vector2.zero;
            radarRect.anchorMax = Vector2.one;
            radarRect.sizeDelta = Vector2.zero;
            radarRect.anchoredPosition = Vector2.zero;
            RawImage radarImage = radarImgGO.AddComponent<RawImage>();
            
            // Range label
            GameObject rangeLabelGO = new GameObject("Range Label");
            rangeLabelGO.transform.SetParent(displayContainer.transform);
            RectTransform labelRect = rangeLabelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0);
            labelRect.anchorMax = new Vector2(0.5f, 0);
            labelRect.pivot = new Vector2(0.5f, 0);
            labelRect.anchoredPosition = new Vector2(0, 5);
            labelRect.sizeDelta = new Vector2(80, 25);
            TextMeshProUGUI rangeLabel = rangeLabelGO.AddComponent<TextMeshProUGUI>();
            rangeLabel.text = "40 NM";
            rangeLabel.alignment = TextAlignmentOptions.Center;
            rangeLabel.fontSize = 14;
            rangeLabel.color = new Color(0.8f, 0.9f, 1f);
            
            // Compass labels
            string[] directions = { "N", "E", "S", "W" };
            Vector2[] positions = {
                new Vector2(0.5f, 0.95f),
                new Vector2(0.95f, 0.5f),
                new Vector2(0.5f, 0.05f),
                new Vector2(0.05f, 0.5f)
            };
            
            for (int i = 0; i < 4; i++)
            {
                GameObject compassLabel = new GameObject($"Compass_{directions[i]}");
                compassLabel.transform.SetParent(displayContainer.transform);
                RectTransform compassRect = compassLabel.AddComponent<RectTransform>();
                compassRect.anchorMin = positions[i];
                compassRect.anchorMax = positions[i];
                compassRect.sizeDelta = new Vector2(30, 20);
                compassRect.anchoredPosition = Vector2.zero;
                TextMeshProUGUI compassText = compassLabel.AddComponent<TextMeshProUGUI>();
                compassText.text = directions[i];
                compassText.alignment = TextAlignmentOptions.Center;
                compassText.fontSize = 16;
                compassText.fontStyle = FontStyles.Bold;
                compassText.color = new Color(0.7f, 0.8f, 0.9f);
            }
            
            // Add display component
            display = displayContainer.AddComponent<TrafficRadarDisplay>();
            
            SerializedObject displaySO = new SerializedObject(display);
            displaySO.FindProperty("radarImage").objectReferenceValue = radarImage;
            displaySO.FindProperty("chartBackgroundImage").objectReferenceValue = chartImage;
            displaySO.FindProperty("rangeLabel").objectReferenceValue = rangeLabel;
            displaySO.FindProperty("radarController").objectReferenceValue = controller;
            displaySO.ApplyModifiedProperties();
            
            // Create chart provider
            GameObject chartProviderGO = new GameObject("Chart Provider");
            chartProviderGO.transform.SetParent(systemRoot.transform);
            chartProvider = chartProviderGO.AddComponent<FAASectionalChartProvider>();
            
            // Update display with chart provider
            displaySO.FindProperty("chartProvider").objectReferenceValue = chartProvider;
            displaySO.ApplyModifiedProperties();
            
            Debug.Log("[Traffic Radar Setup] Created UI components");
        }
        
        // Update controller with display reference
        if (display != null)
        {
            SerializedObject ctrlSO2 = new SerializedObject(controller);
            ctrlSO2.FindProperty("radarDisplay").objectReferenceValue = display;
            ctrlSO2.ApplyModifiedProperties();
        }
        
        Undo.CollapseUndoOperations(undoGroup);
        
        // Select the created system
        Selection.activeGameObject = systemRoot;
        
        // Success dialog
        EditorUtility.DisplayDialog(
            "Traffic Radar System Created",
            "Complete Traffic Radar System has been created!\n\n" +
            "✓ TrafficRadarController\n" +
            (dataManager != null ? "✓ " : "⚠ ") + "TrafficRadarDataManager\n" +
            (display != null ? "✓ " : "⚠ ") + "TrafficRadarDisplay\n\n" +
            $"Position: {initialLatitude:F4}, {initialLongitude:F4}\n" +
            $"Fetch Radius: {radiusKm} km\n\n" +
            "Press Play to see the radar in action!",
            "OK");
        
        Debug.Log("<color=green>[Traffic Radar Setup]</color> Complete Traffic Radar System created successfully!");
    }
    
    [MenuItem("Tools/Traffic Radar/Validate Setup")]
    public static void ValidateSetup()
    {
        string report = "Traffic Radar Validation Report:\n\n";
        bool valid = true;
        
        var controller = FindObjectOfType<TrafficRadarController>();
        var dataManager = FindObjectOfType<TrafficRadarDataManager>();
        var display = FindObjectOfType<TrafficRadarDisplay>();
        var chartProvider = FindObjectOfType<FAASectionalChartProvider>();
        
        report += CheckComponent("TrafficRadarController", controller, ref valid);
        report += CheckComponent("TrafficRadarDataManager", dataManager, ref valid, true);
        report += CheckComponent("TrafficRadarDisplay", display, ref valid);
        report += CheckComponent("FAASectionalChartProvider", chartProvider, ref valid, true);
        
        report += "\n" + (valid ? "✓ Core components found" : "⚠ Some components missing");
        
        Debug.Log(report);
        EditorUtility.DisplayDialog("Validation Result", report, "OK");
    }
    
    private static string CheckComponent(string name, Object component, ref bool valid, bool optional = false)
    {
        if (component != null)
            return $"✓ {name}: Found\n";
        else if (optional)
            return $"⚠ {name}: Not found (optional)\n";
        else
        {
            valid = false;
            return $"✗ {name}: MISSING\n";
        }
    }
}
