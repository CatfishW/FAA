using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace WeatherRadar.Editor
{
    /// <summary>
    /// Editor tool for easy Weather Radar setup in Unity scenes.
    /// Creates complete radar system with one click.
    /// </summary>
    public class WeatherRadarSetupEditor : EditorWindow
    {
        private enum ProviderType
        {
            Simulated,
            IEM_NEXRAD,  // Recommended: Real-time NEXRAD from Iowa Environmental Mesonet
            NOAA,
            MQTT
        }

        private ProviderType selectedProvider = ProviderType.Simulated;
        private int textureResolution = 512;
        private float defaultRange = 40f;
        private float sweepCycleDuration = 4f;
        private bool createControlPanel = true;
        private Canvas targetCanvas;
        private WeatherRadarConfig configAsset;
        private int displaySize = 300;
        
        // Cached TMP font for text creation
        private static TMP_FontAsset cachedFont;

        [MenuItem("Tools/Aviation/Create Weather Radar System")]
        public static void ShowWindow()
        {
            GetWindow<WeatherRadarSetupEditor>("Weather Radar Setup");
        }
        
        private static TMP_FontAsset GetDefaultFont()
        {
            if (cachedFont == null)
            {
                // Try TMP Settings default font first
                if (TMP_Settings.defaultFontAsset != null)
                {
                    cachedFont = TMP_Settings.defaultFontAsset;
                }
                
                // Fallback: Try to load LiberationSans SDF
                if (cachedFont == null)
                {
                    cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                }
                
                // Fallback: find any TMP font in project
                if (cachedFont == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        cachedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                    }
                }
                
                if (cachedFont == null)
                {
                    Debug.LogWarning("[WeatherRadarSetup] No TMP font found. Please import TMP Essentials.");
                }
            }
            return cachedFont;
        }
        
        // Helper to safely set font on TMP text
        private static void SetTMPFont(TextMeshProUGUI tmp)
        {
            var font = GetDefaultFont();
            if (font != null)
            {
                tmp.font = font;
            }
            // If font is null, TMP will use its internal default
        }

        private void OnGUI()
        {
            GUILayout.Label("Weather Radar System Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This tool creates a complete weather radar system with display, controls, and data provider.",
                MessageType.Info);
            EditorGUILayout.Space();

            // Configuration
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            configAsset = (WeatherRadarConfig)EditorGUILayout.ObjectField(
                "Config Asset", configAsset, typeof(WeatherRadarConfig), false);

            if (configAsset == null)
            {
                if (GUILayout.Button("Create New Config Asset"))
                {
                    CreateConfigAsset();
                }
            }

            EditorGUILayout.Space();

            // Settings
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            textureResolution = EditorGUILayout.IntPopup("Resolution", textureResolution, 
                new string[] { "256", "512", "1024" }, 
                new int[] { 256, 512, 1024 });
            displaySize = EditorGUILayout.IntSlider("Display Size", displaySize, 200, 600);
            defaultRange = EditorGUILayout.FloatField("Default Range (NM)", defaultRange);
            sweepCycleDuration = EditorGUILayout.Slider("Sweep Cycle (sec)", sweepCycleDuration, 2f, 10f);
            selectedProvider = (ProviderType)EditorGUILayout.EnumPopup("Data Provider", selectedProvider);
            
            // Provider help text
            switch (selectedProvider)
            {
                case ProviderType.IEM_NEXRAD:
                    EditorGUILayout.HelpBox(
                        "RECOMMENDED: Real-time NEXRAD data from Iowa Environmental Mesonet. " +
                        "Updates every ~5 min. Free for non-commercial use.", MessageType.Info);
                    break;
                case ProviderType.NOAA:
                    EditorGUILayout.HelpBox(
                        "NOAA nowCOAST MRMS radar. Updates every 4 min. " +
                        "May have higher latency.", MessageType.Info);
                    break;
                case ProviderType.Simulated:
                    EditorGUILayout.HelpBox(
                        "Procedural noise-based simulation. No network required.", MessageType.Info);
                    break;
            }
            
            createControlPanel = EditorGUILayout.Toggle("Create Control Panel", createControlPanel);

            EditorGUILayout.Space();

            // Target Canvas
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            targetCanvas = (Canvas)EditorGUILayout.ObjectField("Target Canvas", targetCanvas, typeof(Canvas), true);

            EditorGUILayout.Space();

            // Create Button
            EditorGUI.BeginDisabledGroup(configAsset == null);
            if (GUILayout.Button("Create Weather Radar System", GUILayout.Height(30)))
            {
                CreateRadarSystem();
            }
            EditorGUI.EndDisabledGroup();

            if (configAsset == null)
            {
                EditorGUILayout.HelpBox("Create or assign a config asset to continue.", MessageType.Warning);
            }
        }

        private void CreateConfigAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Weather Radar Config",
                "WeatherRadarConfig",
                "asset",
                "Select location for config asset");

            if (!string.IsNullOrEmpty(path))
            {
                WeatherRadarConfig config = ScriptableObject.CreateInstance<WeatherRadarConfig>();
                config.textureResolution = textureResolution;
                
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                configAsset = config;
                Debug.Log($"Created weather radar config at: {path}");
            }
        }

        private void CreateRadarSystem()
        {
            // Find or create canvas
            if (targetCanvas == null)
            {
                targetCanvas = FindObjectOfType<Canvas>();
                if (targetCanvas == null)
                {
                    GameObject canvasObj = new GameObject("WeatherRadarCanvas");
                    targetCanvas = canvasObj.AddComponent<Canvas>();
                    targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasObj.AddComponent<CanvasScaler>();
                    canvasObj.AddComponent<GraphicRaycaster>();
                    Undo.RegisterCreatedObjectUndo(canvasObj, "Create Weather Radar Canvas");
                }
            }

            // Create main container
            GameObject radarRoot = new GameObject("WeatherRadarSystem");
            radarRoot.transform.SetParent(targetCanvas.transform, false);
            RectTransform rootRect = radarRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0, 0);
            rootRect.anchorMax = new Vector2(0, 0);
            rootRect.pivot = new Vector2(0, 0);
            rootRect.anchoredPosition = new Vector2(50, 50);
            rootRect.sizeDelta = new Vector2(displaySize + 120, displaySize + 20);
            Undo.RegisterCreatedObjectUndo(radarRoot, "Create Weather Radar");

            // Add data provider
            WeatherRadarDataProvider dataProvider = radarRoot.AddComponent<WeatherRadarDataProvider>();

            // Create radar panel with all components properly wired
            WeatherRadarPanel panel = CreateRadarPanel(radarRoot.transform, dataProvider);

            // Create weather provider and wire to panel
            WeatherRadarProviderBase weatherProvider = CreateWeatherProvider(radarRoot);
            
            // Wire up the panel references using SerializedObject
            SerializedObject panelSO = new SerializedObject(panel);
            panelSO.FindProperty("config").objectReferenceValue = configAsset;
            panelSO.FindProperty("dataProvider").objectReferenceValue = dataProvider;
            panelSO.FindProperty("weatherProvider").objectReferenceValue = weatherProvider;
            panelSO.FindProperty("sweepCycleDuration").floatValue = sweepCycleDuration;
            panelSO.ApplyModifiedPropertiesWithoutUndo();

            // Create control panel
            if (createControlPanel)
            {
                CreateControlPanel(radarRoot.transform, dataProvider);
            }

            // Select the created object
            Selection.activeGameObject = radarRoot;

            Debug.Log("Weather Radar System created successfully!");
        }

        private WeatherRadarPanel CreateRadarPanel(Transform parent, WeatherRadarDataProvider dataProvider)
        {
            // Main panel container
            GameObject panelObj = new GameObject("RadarPanel");
            panelObj.transform.SetParent(parent, false);
            
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0.5f);
            panelRect.anchorMax = new Vector2(0, 0.5f);
            panelRect.pivot = new Vector2(0, 0.5f);
            panelRect.sizeDelta = new Vector2(displaySize, displaySize);
            panelRect.anchoredPosition = new Vector2(10, 0);

            CanvasGroup canvasGroup = panelObj.AddComponent<CanvasGroup>();
            WeatherRadarPanel panel = panelObj.AddComponent<WeatherRadarPanel>();

            // Background - dark radar screen
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(panelObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.02f, 0.05f, 0.02f, 1f); // Dark green tint

            // Create radar display area
            int radarSize = displaySize - 20; // margin
            
            // Radar Returns Display (weather data)
            GameObject returnObj = new GameObject("RadarReturns");
            returnObj.transform.SetParent(panelObj.transform, false);
            RectTransform returnRect = returnObj.AddComponent<RectTransform>();
            returnRect.anchorMin = new Vector2(0.5f, 0.5f);
            returnRect.anchorMax = new Vector2(0.5f, 0.5f);
            returnRect.sizeDelta = new Vector2(radarSize, radarSize);
            returnRect.anchoredPosition = Vector2.zero;
            RawImage returnImage = returnObj.AddComponent<RawImage>();
            returnImage.color = Color.white;
            RadarReturnRenderer returnRenderer = returnObj.AddComponent<RadarReturnRenderer>();
            
            // Wire return renderer
            SerializedObject returnSO = new SerializedObject(returnRenderer);
            returnSO.FindProperty("returnDisplay").objectReferenceValue = returnImage;
            returnSO.FindProperty("displayRect").objectReferenceValue = returnRect;
            returnSO.ApplyModifiedPropertiesWithoutUndo();

            // Range Rings Display  
            GameObject ringsObj = new GameObject("RangeRings");
            ringsObj.transform.SetParent(panelObj.transform, false);
            RectTransform ringsRect = ringsObj.AddComponent<RectTransform>();
            ringsRect.anchorMin = new Vector2(0.5f, 0.5f);
            ringsRect.anchorMax = new Vector2(0.5f, 0.5f);
            ringsRect.sizeDelta = new Vector2(radarSize, radarSize);
            ringsRect.anchoredPosition = Vector2.zero;
            RawImage ringsImage = ringsObj.AddComponent<RawImage>();
            ringsImage.color = Color.white;
            RangeRingsRenderer ringsRenderer = ringsObj.AddComponent<RangeRingsRenderer>();

            // Wire rings renderer
            SerializedObject ringsSO = new SerializedObject(ringsRenderer);
            ringsSO.FindProperty("ringsDisplay").objectReferenceValue = ringsImage;
            ringsSO.FindProperty("displayRect").objectReferenceValue = ringsRect;
            ringsSO.ApplyModifiedPropertiesWithoutUndo();

            // Sweep Line Display
            GameObject sweepObj = new GameObject("SweepLine");
            sweepObj.transform.SetParent(panelObj.transform, false);
            RectTransform sweepRect = sweepObj.AddComponent<RectTransform>();
            sweepRect.anchorMin = new Vector2(0.5f, 0.5f);
            sweepRect.anchorMax = new Vector2(0.5f, 0.5f);
            sweepRect.sizeDelta = new Vector2(radarSize, radarSize);
            sweepRect.anchoredPosition = Vector2.zero;
            RawImage sweepImage = sweepObj.AddComponent<RawImage>();
            sweepImage.color = Color.white;
            RadarSweepRenderer sweepRenderer = sweepObj.AddComponent<RadarSweepRenderer>();

            // Wire sweep renderer
            SerializedObject sweepSO = new SerializedObject(sweepRenderer);
            sweepSO.FindProperty("sweepImage").objectReferenceValue = sweepImage;
            sweepSO.ApplyModifiedPropertiesWithoutUndo();

            // Waypoint Overlay
            GameObject waypointObj = new GameObject("WaypointOverlay");
            waypointObj.transform.SetParent(panelObj.transform, false);
            RectTransform waypointRect = waypointObj.AddComponent<RectTransform>();
            waypointRect.anchorMin = new Vector2(0.5f, 0.5f);
            waypointRect.anchorMax = new Vector2(0.5f, 0.5f);
            waypointRect.sizeDelta = new Vector2(radarSize, radarSize);
            waypointRect.anchoredPosition = Vector2.zero;
            WaypointOverlayRenderer waypointRenderer = waypointObj.AddComponent<WaypointOverlayRenderer>();

            // Wire waypoint renderer
            SerializedObject waypointSO = new SerializedObject(waypointRenderer);
            waypointSO.FindProperty("displayRect").objectReferenceValue = waypointRect;
            waypointSO.ApplyModifiedPropertiesWithoutUndo();

            // Create info labels
            CreateRadarLabels(panelObj.transform, radarSize, panel);

            // Wire all renderers to panel
            SerializedObject panelSO = new SerializedObject(panel);
            panelSO.FindProperty("sweepRenderer").objectReferenceValue = sweepRenderer;
            panelSO.FindProperty("returnRenderer").objectReferenceValue = returnRenderer;
            panelSO.FindProperty("rangeRingsRenderer").objectReferenceValue = ringsRenderer;
            panelSO.FindProperty("waypointRenderer").objectReferenceValue = waypointRenderer;
            panelSO.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            panelSO.FindProperty("panelRect").objectReferenceValue = panelRect;
            panelSO.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private void CreateRadarLabels(Transform parent, int radarSize, WeatherRadarPanel panel)
        {
            float halfSize = radarSize / 2f;

            // Mode label (top center, inside radar)
            TMP_Text modeLabel = CreateTMPLabel(parent, "ModeLabel", "WX",
                new Vector2(0, halfSize - 25), TextAlignmentOptions.Center);

            // Range label (bottom center, below radar)
            TMP_Text rangeLabel = CreateTMPLabel(parent, "RangeLabel", "40nm",
                new Vector2(0, -halfSize - 20), TextAlignmentOptions.Center);

            // Tilt label (inside radar, bottom-right area)
            TMP_Text tiltLabel = CreateTMPLabel(parent, "TiltLabel", "TLT +0.0°",
                new Vector2(halfSize - 50, -halfSize + 35), TextAlignmentOptions.Right);

            // Wire labels to panel
            SerializedObject panelSO = new SerializedObject(panel);
            panelSO.FindProperty("modeLabel").objectReferenceValue = modeLabel;
            panelSO.FindProperty("rangeLabel").objectReferenceValue = rangeLabel;
            panelSO.FindProperty("tiltLabel").objectReferenceValue = tiltLabel;
            panelSO.ApplyModifiedPropertiesWithoutUndo();
        }

        private TMP_Text CreateTMPLabel(Transform parent, string name, string content, Vector2 position, TextAlignmentOptions alignment)
        {
            GameObject labelObj = new GameObject(name);
            labelObj.transform.SetParent(parent, false);

            RectTransform rect = labelObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(80, 40);

            TextMeshProUGUI text = labelObj.AddComponent<TextMeshProUGUI>();
            SetTMPFont(text);
            text.text = content;
            text.fontSize = 14;
            text.alignment = alignment;
            text.color = new Color(0.8f, 1f, 0.8f, 1f);

            return text;
        }

        private WeatherRadarProviderBase CreateWeatherProvider(GameObject parent)
        {
            switch (selectedProvider)
            {
                case ProviderType.Simulated:
                    return parent.AddComponent<SimulatedWeatherProvider>();
                case ProviderType.IEM_NEXRAD:
                    return parent.AddComponent<IEMWeatherProvider>();
                case ProviderType.NOAA:
                    return parent.AddComponent<NOAAWeatherProvider>();
                case ProviderType.MQTT:
                    return parent.AddComponent<MQTTWeatherProvider>();
                default:
                    return parent.AddComponent<SimulatedWeatherProvider>();
            }
        }

        private void CreateControlPanel(Transform parent, WeatherRadarDataProvider dataProvider)
        {
            // Create control panel - cleaner vertical layout
            GameObject controlsObj = new GameObject("ControlPanel");
            controlsObj.transform.SetParent(parent, false);

            RectTransform controlsRect = controlsObj.AddComponent<RectTransform>();
            // Position at right side with proper gap
            controlsRect.anchorMin = new Vector2(1, 0.5f);
            controlsRect.anchorMax = new Vector2(1, 0.5f);
            controlsRect.pivot = new Vector2(0, 0.5f);
            controlsRect.anchoredPosition = new Vector2(15, 0);
            controlsRect.sizeDelta = new Vector2(140, 280);

            // Dark background with subtle border
            Image controlsBg = controlsObj.AddComponent<Image>();
            controlsBg.color = new Color(0.02f, 0.04f, 0.02f, 0.95f);
            
            // Add outline effect via second image
            GameObject border = new GameObject("Border");
            border.transform.SetParent(controlsObj.transform, false);
            RectTransform borderRect = border.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero;
            borderRect.offsetMin = new Vector2(-1, -1);
            borderRect.offsetMax = new Vector2(1, 1);
            Image borderImg = border.AddComponent<Image>();
            borderImg.color = new Color(0.2f, 0.4f, 0.2f, 0.6f);
            border.transform.SetAsFirstSibling();

            // Vertical layout
            VerticalLayoutGroup layout = controlsObj.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 10, 10);
            layout.spacing = 4;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // === RANGE SECTION ===
            CreateSectionHeader(controlsObj.transform, "RANGE");
            GameObject rangeRow = CreateControlRow(controlsObj.transform, "RangeRow", 32);
            Button rangeDownBtn = CreateControlButton(rangeRow.transform, "RangeDown", "−", 30);
            TMP_Text rangeText = CreateValueDisplay(rangeRow.transform, "RangeValue", "40nm", 50);
            Button rangeUpBtn = CreateControlButton(rangeRow.transform, "RangeUp", "+", 30);

            // === TILT SECTION ===
            CreateSectionHeader(controlsObj.transform, "TILT");
            GameObject tiltRow = CreateControlRow(controlsObj.transform, "TiltRow", 32);
            Button tiltDownBtn = CreateControlButton(tiltRow.transform, "TiltDown", "▼", 30);
            TMP_Text tiltText = CreateValueDisplay(tiltRow.transform, "TiltValue", "+0.0°", 50);
            Button tiltUpBtn = CreateControlButton(tiltRow.transform, "TiltUp", "▲", 30);

            // Separator
            CreateSeparator(controlsObj.transform);

            // === MODE SECTION ===
            CreateSectionHeader(controlsObj.transform, "MODE");
            Button wxBtn = CreateModeButton(controlsObj.transform, "WXMode", "WX");
            Button wxTBtn = CreateModeButton(controlsObj.transform, "WXTMode", "WX+T");
            Button turbBtn = CreateModeButton(controlsObj.transform, "TURBMode", "TURB");

            // Add control panel component and wire ALL references
            RadarControlPanel controlPanel = controlsObj.AddComponent<RadarControlPanel>();

            SerializedObject controlSO = new SerializedObject(controlPanel);
            controlSO.FindProperty("dataProvider").objectReferenceValue = dataProvider;
            
            // Range controls
            controlSO.FindProperty("rangeUpButton").objectReferenceValue = rangeUpBtn;
            controlSO.FindProperty("rangeDownButton").objectReferenceValue = rangeDownBtn;
            controlSO.FindProperty("rangeValueText").objectReferenceValue = rangeText;
            
            // Tilt controls
            controlSO.FindProperty("tiltUpButton").objectReferenceValue = tiltUpBtn;
            controlSO.FindProperty("tiltDownButton").objectReferenceValue = tiltDownBtn;
            controlSO.FindProperty("tiltValueText").objectReferenceValue = tiltText;
            
            // Mode buttons
            controlSO.FindProperty("wxModeButton").objectReferenceValue = wxBtn;
            controlSO.FindProperty("wxTModeButton").objectReferenceValue = wxTBtn;
            controlSO.FindProperty("turbModeButton").objectReferenceValue = turbBtn;
            
            controlSO.ApplyModifiedPropertiesWithoutUndo();
        }

        private void CreateSectionHeader(Transform parent, string text)
        {
            GameObject headerObj = new GameObject(text + "Header");
            headerObj.transform.SetParent(parent, false);

            RectTransform rect = headerObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(124, 18);

            // Background for section
            Image bg = headerObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.2f, 0.1f, 0.5f);
            bg.raycastTarget = false;

            // Text must be on child object (Image and TMP are both Graphic - can't be on same GameObject)
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(headerObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            SetTMPFont(tmp);
            tmp.text = text;
            tmp.fontSize = 11;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.6f, 0.8f, 0.6f, 1f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
        }

        private void CreateSeparator(Transform parent)
        {
            GameObject sep = new GameObject("Separator");
            sep.transform.SetParent(parent, false);
            RectTransform rect = sep.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(80, 1);
            Image img = sep.AddComponent<Image>();
            img.color = new Color(0.3f, 0.5f, 0.3f, 0.4f);
            img.raycastTarget = false;
            // Separator line
        }

        private GameObject CreateControlRow(Transform parent, string name, float height)
        {
            GameObject rowObj = new GameObject(name);
            rowObj.transform.SetParent(parent, false);

            RectTransform rect = rowObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(124, height);

            HorizontalLayoutGroup layout = rowObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            return rowObj;
        }

        private Button CreateControlButton(Transform parent, string name, string label, float size)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent, false);

            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size);

            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.15f, 0.25f, 0.15f, 1f);

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.15f, 0.25f, 0.15f, 1f);
            colors.highlightedColor = new Color(0.25f, 0.45f, 0.25f, 1f);
            colors.pressedColor = new Color(0.08f, 0.15f, 0.08f, 1f);
            colors.selectedColor = new Color(0.2f, 0.35f, 0.2f, 1f);
            button.colors = colors;

            // Label child
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(buttonObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = labelObj.AddComponent<TextMeshProUGUI>();
            SetTMPFont(text);
            text.text = label;
            text.fontSize = 16;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.7f, 1f, 0.7f, 1f);
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;

            return button;
        }

        private TMP_Text CreateValueDisplay(Transform parent, string name, string content, float width)
        {
            GameObject containerObj = new GameObject(name);
            containerObj.transform.SetParent(parent, false);

            RectTransform rect = containerObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, 28);

            // Background - set raycastTarget to false so it doesn't block sibling button clicks
            Image bg = containerObj.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.1f, 0.05f, 0.8f);
            bg.raycastTarget = false;

            // Text must be on child (Image and TMP are both Graphic)
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(containerObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            SetTMPFont(text);
            text.text = content;
            text.fontSize = 13;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.9f, 1f, 0.9f, 1f);
            text.raycastTarget = false;

            return text;
        }

        private Button CreateModeButton(Transform parent, string name, string label)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent, false);

            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(124, 28);

            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.12f, 0.2f, 0.12f, 1f);

            Button button = buttonObj.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.12f, 0.2f, 0.12f, 1f);
            colors.highlightedColor = new Color(0.2f, 0.4f, 0.2f, 1f);
            colors.pressedColor = new Color(0.08f, 0.12f, 0.08f, 1f);
            colors.selectedColor = new Color(0.25f, 0.5f, 0.25f, 1f);
            button.colors = colors;
            button.targetGraphic = image;

            // Label child
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(buttonObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = labelObj.AddComponent<TextMeshProUGUI>();
            SetTMPFont(text);
            text.text = label;
            text.fontSize = 12;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.75f, 0.95f, 0.75f, 1f);
            text.raycastTarget = false;

            return button;
        }

        [MenuItem("Tools/Aviation/Create Weather Radar Config")]
        public static void CreateConfigAssetMenu()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Weather Radar Config",
                "WeatherRadarConfig",
                "asset",
                "Select location for config asset");

            if (!string.IsNullOrEmpty(path))
            {
                WeatherRadarConfig config = ScriptableObject.CreateInstance<WeatherRadarConfig>();
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = config;
                Debug.Log($"Created weather radar config at: {path}");
            }
        }
    }
}
