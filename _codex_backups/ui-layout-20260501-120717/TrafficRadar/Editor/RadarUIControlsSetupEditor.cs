using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;
using TrafficRadar;
using TrafficRadar.Core;
using TrafficRadar.Controls;
using WeatherRadar;

/// <summary>
/// Editor wizard for easy setup of Radar UI Controls.
/// Provides one-click creation of:
/// - TrafficRadarRangeUI (range selector with animated buttons)
/// - WeatherRadarClickHandler (control panel toggle on click)
/// </summary>
public class RadarUIControlsSetupEditor : EditorWindow
{
    #region Color Presets
    
    public enum ColorPreset
    {
        DefaultBlue,
        GreenRadar,
        AmberAviation,
        DarkMode,
        HighContrast
    }
    
    // Color preset definitions
    private static readonly Dictionary<ColorPreset, ColorPresetData> colorPresets = new Dictionary<ColorPreset, ColorPresetData>
    {
        { ColorPreset.DefaultBlue, new ColorPresetData {
            normal = new Color(0.12f, 0.18f, 0.25f, 0.95f),
            hover = new Color(0.18f, 0.28f, 0.38f, 1f),
            pressed = new Color(0.08f, 0.12f, 0.18f, 1f),
            active = new Color(0.15f, 0.45f, 0.65f, 1f),
            glow = new Color(0.3f, 0.7f, 1f, 0.4f),
            textNormal = new Color(0.7f, 0.85f, 0.95f, 1f),
            textActive = new Color(0.95f, 1f, 1f, 1f)
        }},
        { ColorPreset.GreenRadar, new ColorPresetData {
            normal = new Color(0.08f, 0.15f, 0.08f, 0.95f),
            hover = new Color(0.12f, 0.25f, 0.12f, 1f),
            pressed = new Color(0.05f, 0.10f, 0.05f, 1f),
            active = new Color(0.1f, 0.5f, 0.2f, 1f),
            glow = new Color(0.2f, 1f, 0.4f, 0.4f),
            textNormal = new Color(0.6f, 0.9f, 0.6f, 1f),
            textActive = new Color(0.8f, 1f, 0.8f, 1f)
        }},
        { ColorPreset.AmberAviation, new ColorPresetData {
            normal = new Color(0.18f, 0.12f, 0.05f, 0.95f),
            hover = new Color(0.28f, 0.18f, 0.08f, 1f),
            pressed = new Color(0.12f, 0.08f, 0.03f, 1f),
            active = new Color(0.6f, 0.4f, 0.1f, 1f),
            glow = new Color(1f, 0.7f, 0.2f, 0.4f),
            textNormal = new Color(0.95f, 0.8f, 0.5f, 1f),
            textActive = new Color(1f, 0.95f, 0.8f, 1f)
        }},
        { ColorPreset.DarkMode, new ColorPresetData {
            normal = new Color(0.08f, 0.08f, 0.1f, 0.98f),
            hover = new Color(0.15f, 0.15f, 0.18f, 1f),
            pressed = new Color(0.05f, 0.05f, 0.06f, 1f),
            active = new Color(0.25f, 0.25f, 0.35f, 1f),
            glow = new Color(0.5f, 0.5f, 0.7f, 0.3f),
            textNormal = new Color(0.7f, 0.7f, 0.75f, 1f),
            textActive = new Color(0.95f, 0.95f, 1f, 1f)
        }},
        { ColorPreset.HighContrast, new ColorPresetData {
            normal = new Color(0.02f, 0.02f, 0.02f, 0.98f),
            hover = new Color(0.1f, 0.1f, 0.1f, 1f),
            pressed = new Color(0.15f, 0.15f, 0.15f, 1f),
            active = new Color(0f, 0.6f, 1f, 1f),
            glow = new Color(0f, 0.8f, 1f, 0.6f),
            textNormal = new Color(1f, 1f, 1f, 1f),
            textActive = new Color(1f, 1f, 0.8f, 1f)
        }}
    };
    
    private struct ColorPresetData
    {
        public Color normal, hover, pressed, active, glow, textNormal, textActive;
    }
    
    #endregion
    
    #region Settings
    
    private bool createTrafficRangeUI = true;
    private bool createTrafficFilterUI = true;
    private bool createWeatherClickHandler = true;
    
    private Vector2 scrollPosition;
    
    // Traffic Range UI settings
    private KeyCode rangeUIToggleKey = KeyCode.R;
    private bool rangeUIStartVisible = true;
    private ColorPreset colorPreset = ColorPreset.DefaultBlue;
    
    // Traffic Filter UI settings
    private KeyCode filterUIToggleKey = KeyCode.F;
    private bool filterUIStartVisible = true;
    
    // Traffic Click Handler settings
    private bool createTrafficClickHandler = true;
    private KeyCode trafficClickToggleKey = KeyCode.T;
    private bool trafficUIStartVisible = true;
    
    // Weather Click Handler settings
    private KeyCode panelToggleKey = KeyCode.P;
    private bool panelStartVisible = true;
    private WeatherRadarClickHandler.AnimationDirection slideDirection = WeatherRadarClickHandler.AnimationDirection.Right;
    private float animationDuration = 0.25f;
    private float slideDistance = 30f;
    
    #endregion
    
    [MenuItem("Tools/Radar UI Controls/Setup Wizard")]
    public static void ShowWindow()
    {
        var window = GetWindow<RadarUIControlsSetupEditor>("Radar UI Controls Setup");
        window.minSize = new Vector2(400, 500);
        window.Show();
    }

    [MenuItem("Tools/Radar UI Controls/One-Click Setup All")]
    public static void OneClickSetup()
    {
        var wizard = CreateInstance<RadarUIControlsSetupEditor>();
        wizard.PerformSetup();
        DestroyImmediate(wizard);
    }

    [MenuItem("Tools/Radar UI Controls/Add Traffic Range UI")]
    public static void AddTrafficRangeUI()
    {
        var wizard = CreateInstance<RadarUIControlsSetupEditor>();
        wizard.createWeatherClickHandler = false;
        wizard.PerformSetup();
        DestroyImmediate(wizard);
    }

    [MenuItem("Tools/Radar UI Controls/Add Weather Click Handler")]
    public static void AddWeatherClickHandler()
    {
        var wizard = CreateInstance<RadarUIControlsSetupEditor>();
        wizard.createTrafficRangeUI = false;
        wizard.PerformSetup();
        DestroyImmediate(wizard);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        // Header
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Radar UI Controls Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This wizard sets up UI controls for radar systems:\n\n" +
            "• Traffic Radar Range UI - Animated range selector buttons\n" +
            "• Weather Radar Click Handler - Click radar to toggle control panel",
            MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        // Components to Create
        EditorGUILayout.LabelField("Components to Create", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        
        createTrafficRangeUI = EditorGUILayout.Toggle("Traffic Range UI", createTrafficRangeUI);
        createTrafficFilterUI = EditorGUILayout.Toggle("Traffic Filter UI", createTrafficFilterUI);
        createTrafficClickHandler = EditorGUILayout.Toggle("Traffic Click Handler", createTrafficClickHandler);
        createWeatherClickHandler = EditorGUILayout.Toggle("Weather Click Handler", createWeatherClickHandler);
        
        EditorGUI.indentLevel--;
        
        EditorGUILayout.Space(10);
        
        // Traffic Range UI Settings
        if (createTrafficRangeUI)
        {
            EditorGUILayout.LabelField("Traffic Range UI Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            
            rangeUIStartVisible = EditorGUILayout.Toggle("Start Visible", rangeUIStartVisible);
            rangeUIToggleKey = (KeyCode)EditorGUILayout.EnumPopup("Toggle Key", rangeUIToggleKey);
            colorPreset = (ColorPreset)EditorGUILayout.EnumPopup("Color Theme", colorPreset);
            
            // Show color preview
            if (colorPresets.TryGetValue(colorPreset, out var preset))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Preview:", GUILayout.Width(60));
                DrawColorPreview(preset.normal, "Normal");
                DrawColorPreview(preset.hover, "Hover");
                DrawColorPreview(preset.active, "Active");
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.Space(5);
        
        // Traffic Filter UI Settings
        if (createTrafficFilterUI)
        {
            EditorGUILayout.LabelField("Traffic Filter UI Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            
            filterUIStartVisible = EditorGUILayout.Toggle("Start Visible", filterUIStartVisible);
            filterUIToggleKey = (KeyCode)EditorGUILayout.EnumPopup("Toggle Key", filterUIToggleKey);
            
            EditorGUILayout.HelpBox("Filter UI positioned on top of radar display.\nControls data fetch radius in nautical miles.", MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.Space(5);
        
        // Traffic Click Handler Settings
        if (createTrafficClickHandler)
        {
            EditorGUILayout.LabelField("Traffic Click Handler Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            
            trafficUIStartVisible = EditorGUILayout.Toggle("UI Start Visible", trafficUIStartVisible);
            trafficClickToggleKey = (KeyCode)EditorGUILayout.EnumPopup("Toggle Key", trafficClickToggleKey);
            
            EditorGUILayout.HelpBox("Click on traffic radar display to toggle Range UI and Filter UI visibility.", MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.Space(5);
        
        // Weather Click Handler Settings
        if (createWeatherClickHandler)
        {
            EditorGUILayout.LabelField("Weather Click Handler Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            
            panelStartVisible = EditorGUILayout.Toggle("Panel Start Visible", panelStartVisible);
            panelToggleKey = (KeyCode)EditorGUILayout.EnumPopup("Toggle Key", panelToggleKey);
            slideDirection = (WeatherRadarClickHandler.AnimationDirection)EditorGUILayout.EnumPopup("Slide Direction", slideDirection);
            animationDuration = EditorGUILayout.Slider("Animation Duration", animationDuration, 0.1f, 1f);
            slideDistance = EditorGUILayout.Slider("Slide Distance", slideDistance, 10f, 100f);
            
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.Space(10);
        
        // Scene Status
        DrawSceneStatus();
        
        EditorGUILayout.Space(10);
        
        // Setup Button
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            fixedHeight = 40
        };
        
        GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
        if (GUILayout.Button("🎛️ Setup Radar UI Controls", buttonStyle))
        {
            PerformSetup();
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.Space(10);
        
        // Quick Actions
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select Traffic Radar"))
        {
            var controller = FindObjectOfType<TrafficRadarController>();
            if (controller != null) Selection.activeGameObject = controller.gameObject;
        }
        if (GUILayout.Button("Select Weather Radar"))
        {
            var panel = FindObjectOfType<WeatherRadarPanel>();
            if (panel != null) Selection.activeGameObject = panel.gameObject;
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndScrollView();
    }

    private void DrawSceneStatus()
    {
        EditorGUILayout.LabelField("Scene Status", EditorStyles.boldLabel);
        
        var trafficController = FindObjectOfType<TrafficRadarController>();
        var trafficRangeUI = FindObjectOfType<TrafficRadarRangeUI>();
        var weatherPanel = FindObjectOfType<WeatherRadarPanel>();
        var controlPanel = FindObjectOfType<RadarControlPanel>();
        var clickHandler = FindObjectOfType<WeatherRadarClickHandler>();
        
        EditorGUILayout.BeginVertical("box");
        
        EditorGUILayout.LabelField("Traffic Radar", EditorStyles.miniLabel);
        DrawStatusLine("TrafficRadarController", trafficController != null);
        DrawStatusLine("TrafficRadarRangeUI", trafficRangeUI != null);
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.LabelField("Weather Radar", EditorStyles.miniLabel);
        DrawStatusLine("WeatherRadarPanel", weatherPanel != null);
        DrawStatusLine("RadarControlPanel", controlPanel != null);
        DrawStatusLine("WeatherRadarClickHandler", clickHandler != null);
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawStatusLine(string name, bool exists)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(exists ? "✓" : "○", GUILayout.Width(20));
        EditorGUILayout.LabelField(name);
        EditorGUILayout.LabelField(exists ? "Found" : "Not Found", 
            exists ? EditorStyles.miniLabel : EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawColorPreview(Color color, string tooltip)
    {
        var rect = GUILayoutUtility.GetRect(30, 16);
        EditorGUI.DrawRect(rect, color);
        if (rect.Contains(Event.current.mousePosition))
        {
            GUI.Label(new Rect(rect.x, rect.y - 18, 80, 16), tooltip, EditorStyles.miniLabel);
        }
    }

    private void PerformSetup()
    {
        Undo.SetCurrentGroupName("Setup Radar UI Controls");
        int undoGroup = Undo.GetCurrentGroup();
        
        bool trafficUICreated = false;
        bool weatherHandlerCreated = false;
        
        // ========== TRAFFIC RANGE UI ==========
        if (createTrafficRangeUI)
        {
            var existingRangeUI = FindObjectOfType<TrafficRadarRangeUI>();
            
            if (existingRangeUI != null)
            {
                Debug.Log("[Radar UI Setup] TrafficRadarRangeUI already exists, skipping creation.");
            }
            else
            {
                var controller = FindObjectOfType<TrafficRadarController>();
                
                if (controller != null)
                {
                    // Find the radar display to parent the UI to
                    var display = FindObjectOfType<TrafficRadarDisplay>();
                    Transform parent = display != null ? display.transform : controller.transform;
                    
                    // Check if parent has a Canvas (required for UI)
                    Canvas canvas = parent.GetComponentInParent<Canvas>();
                    if (canvas == null)
                    {
                        // Create UI on the display's canvas if it exists
                        canvas = FindObjectOfType<Canvas>();
                    }
                    
                    if (canvas != null)
                    {
                        GameObject rangeUIGO = new GameObject("Traffic Range UI");
                        rangeUIGO.transform.SetParent(canvas.transform, false);
                        Undo.RegisterCreatedObjectUndo(rangeUIGO, "Create Traffic Range UI");
                        
                        // Position at bottom center
                        RectTransform rect = rangeUIGO.AddComponent<RectTransform>();
                        rect.anchorMin = new Vector2(0.5f, 0);
                        rect.anchorMax = new Vector2(0.5f, 0);
                        rect.pivot = new Vector2(0.5f, 0);
                        rect.anchoredPosition = new Vector2(0, 20);
                        rect.sizeDelta = new Vector2(300, 60);
                        
                        // Add CanvasGroup
                        rangeUIGO.AddComponent<CanvasGroup>();
                        
                        // Add component
                        var rangeUI = rangeUIGO.AddComponent<TrafficRadarRangeUI>();
                        
                        // Configure via SerializedObject
                        SerializedObject so = new SerializedObject(rangeUI);
                        so.FindProperty("radarController").objectReferenceValue = controller;
                        so.FindProperty("startVisible").boolValue = rangeUIStartVisible;
                        so.FindProperty("toggleKey").intValue = (int)rangeUIToggleKey;
                        
                        // Apply color preset
                        if (colorPresets.TryGetValue(colorPreset, out var preset))
                        {
                            so.FindProperty("normalColor").colorValue = preset.normal;
                            so.FindProperty("hoverColor").colorValue = preset.hover;
                            so.FindProperty("pressedColor").colorValue = preset.pressed;
                            so.FindProperty("activeColor").colorValue = preset.active;
                            so.FindProperty("glowColor").colorValue = preset.glow;
                            so.FindProperty("textNormalColor").colorValue = preset.textNormal;
                            so.FindProperty("textActiveColor").colorValue = preset.textActive;
                        }
                        
                        so.ApplyModifiedProperties();
                        
                        trafficUICreated = true;
                        Debug.Log("<color=green>[Radar UI Setup]</color> Created TrafficRadarRangeUI");
                    }
                    else
                    {
                        Debug.LogWarning("[Radar UI Setup] No Canvas found! Create a Canvas first.");
                    }
                }
                else
                {
                    Debug.LogWarning("[Radar UI Setup] No TrafficRadarController found! Run Traffic Radar Setup first.");
                }
            }
        }
        
        // ========== TRAFFIC FILTER UI ==========
        if (createTrafficFilterUI)
        {
            var existingFilterUI = FindObjectOfType<TrafficRadarFilterUI>();
            
            if (existingFilterUI != null)
            {
                Debug.Log("[Radar UI Setup] TrafficRadarFilterUI already exists, skipping creation.");
            }
            else
            {
                var display = FindObjectOfType<TrafficRadarDisplay>();
                var dataManager = FindObjectOfType<TrafficRadarDataManager>();
                
                if (display != null)
                {
                    // Create filter UI as child of display
                    GameObject filterUIGO = new GameObject("Traffic Filter UI");
                    filterUIGO.transform.SetParent(display.transform, false);
                    Undo.RegisterCreatedObjectUndo(filterUIGO, "Create Traffic Filter UI");
                    
                    // Position at top of display
                    RectTransform rect = filterUIGO.AddComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.pivot = new Vector2(0.5f, 1);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(0, 80);
                    
                    filterUIGO.AddComponent<CanvasGroup>();
                    
                    var filterUI = filterUIGO.AddComponent<TrafficRadarFilterUI>();
                    
                    SerializedObject so = new SerializedObject(filterUI);
                    so.FindProperty("dataManager").objectReferenceValue = dataManager;
                    so.FindProperty("startVisible").boolValue = filterUIStartVisible;
                    so.FindProperty("toggleKey").intValue = (int)filterUIToggleKey;
                    so.ApplyModifiedProperties();
                    
                    Debug.Log("<color=green>[Radar UI Setup]</color> Created TrafficRadarFilterUI on top of display");
                }
                else
                {
                    Debug.LogWarning("[Radar UI Setup] No TrafficRadarDisplay found! Run Traffic Radar Setup first.");
                }
            }
        }
        
        // ========== TRAFFIC CLICK HANDLER ==========
        if (createTrafficClickHandler)
        {
            var existingClickHandler = FindObjectOfType<TrafficRadarClickHandler>();
            
            if (existingClickHandler != null)
            {
                Debug.Log("[Radar UI Setup] TrafficRadarClickHandler already exists, skipping creation.");
            }
            else
            {
                var display = FindObjectOfType<TrafficRadarDisplay>();
                
                if (display != null)
                {
                    // Add click handler to the display
                    var clickHandler = Undo.AddComponent<TrafficRadarClickHandler>(display.gameObject);
                    
                    // Auto-find range and filter UIs
                    var rangeUI = FindObjectOfType<TrafficRadarRangeUI>();
                    var filterUI = FindObjectOfType<TrafficRadarFilterUI>();
                    
                    SerializedObject so = new SerializedObject(clickHandler);
                    so.FindProperty("rangeUI").objectReferenceValue = rangeUI;
                    so.FindProperty("filterUI").objectReferenceValue = filterUI;
                    so.FindProperty("startUIVisible").boolValue = trafficUIStartVisible;
                    so.FindProperty("toggleKey").intValue = (int)trafficClickToggleKey;
                    so.ApplyModifiedProperties();
                    
                    // Ensure display has raycast target for click detection
                    if (display.GetComponent<UnityEngine.UI.Image>() == null && 
                        display.GetComponent<UnityEngine.UI.RawImage>() == null)
                    {
                        var img = Undo.AddComponent<UnityEngine.UI.Image>(display.gameObject);
                        img.color = Color.clear;
                    }
                    
                    Debug.Log("<color=green>[Radar UI Setup]</color> Created TrafficRadarClickHandler");
                }
                else
                {
                    Debug.LogWarning("[Radar UI Setup] No TrafficRadarDisplay found! Run Traffic Radar Setup first.");
                }
            }
        }
        
        // ========== WEATHER CLICK HANDLER ==========
        if (createWeatherClickHandler)
        {
            var existingHandler = FindObjectOfType<WeatherRadarClickHandler>();
            
            if (existingHandler != null)
            {
                Debug.Log("[Radar UI Setup] WeatherRadarClickHandler already exists, skipping creation.");
            }
            else
            {
                var weatherPanel = FindObjectOfType<WeatherRadarPanel>();
                var controlPanel = FindObjectOfType<RadarControlPanel>();
                
                if (weatherPanel != null)
                {
                    // Add click handler to the weather panel
                    var handler = Undo.AddComponent<WeatherRadarClickHandler>(weatherPanel.gameObject);
                    
                    // Configure via SerializedObject
                    SerializedObject so = new SerializedObject(handler);
                    so.FindProperty("controlPanel").objectReferenceValue = controlPanel;
                    so.FindProperty("startPanelVisible").boolValue = panelStartVisible;
                    so.FindProperty("toggleKey").intValue = (int)panelToggleKey;
                    so.FindProperty("slideDirection").enumValueIndex = (int)slideDirection;
                    so.FindProperty("animationDuration").floatValue = animationDuration;
                    so.FindProperty("slideDistance").floatValue = slideDistance;
                    so.ApplyModifiedProperties();
                    
                    // Ensure weather panel has an Image for click detection
                    if (weatherPanel.GetComponent<Image>() == null && weatherPanel.GetComponent<RawImage>() == null)
                    {
                        var img = Undo.AddComponent<Image>(weatherPanel.gameObject);
                        img.color = Color.clear; // Invisible but raycast-able
                    }
                    
                    weatherHandlerCreated = true;
                    Debug.Log("<color=green>[Radar UI Setup]</color> Created WeatherRadarClickHandler");
                }
                else
                {
                    Debug.LogWarning("[Radar UI Setup] No WeatherRadarPanel found! Setup Weather Radar first.");
                }
            }
        }
        
        Undo.CollapseUndoOperations(undoGroup);
        
        // Show result
        string message = "Radar UI Controls Setup Complete!\n\n";
        
        if (createTrafficRangeUI)
            message += trafficUICreated ? "✓ TrafficRadarRangeUI created\n" : "○ TrafficRadarRangeUI (already exists or failed)\n";
        
        if (createWeatherClickHandler)
            message += weatherHandlerCreated ? "✓ WeatherRadarClickHandler created\n" : "○ WeatherRadarClickHandler (already exists or failed)\n";
        
        message += "\nPress Play to test the controls!";
        
        if (trafficUICreated) message += $"\n• Press '{rangeUIToggleKey}' to toggle range UI";
        if (weatherHandlerCreated) message += $"\n• Press '{panelToggleKey}' or click radar to toggle panel";
        
        EditorUtility.DisplayDialog("Setup Complete", message, "OK");
    }
}
