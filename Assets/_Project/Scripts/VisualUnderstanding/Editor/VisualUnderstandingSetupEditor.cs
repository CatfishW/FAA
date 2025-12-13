#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using VisualUnderstanding.Core;
using VisualUnderstanding.Network;
using VisualUnderstanding.Analyzers;
using VisualUnderstanding.UI;

namespace VisualUnderstanding.Editor
{
    /// <summary>
    /// Editor wizard for easy setup of Visual Understanding System.
    /// Provides one-click creation of all required components.
    /// </summary>
    public class VisualUnderstandingSetupEditor : EditorWindow
    {
        // Window settings
        private Vector2 _scrollPosition;
        
        // Settings
        private string _serverUrl = "https://game.agaii.org/llm/v1";
        private string _modelName = "Qwen/Qwen3-VL-4B-Instruct-FP8";
        private string _apiKey = "empty";
        
        // Component checks
        private VisualAnalysisManager _existingManager;
        private VisionLLMClient _existingClient;
        private VisualBriefingPanel _existingPanel;
        private VisionSettings _existingSettings;
        
        // UI options
        private bool _createUI = true;
        private bool _createSettings = true;
        private string _panelPosition = "Bottom Left";
        private string[] _positionOptions = { "Bottom Left", "Bottom Right", "Top Left", "Top Right" };
        private int _positionIndex = 0;
        
        // Model auto-detection
        private bool _isFetchingModels;
        private string _modelFetchStatus = "";
        private string _lastFetchedUrl = "";
        
        // Styles
        private GUIStyle _headerStyle;
        private GUIStyle _sectionStyle;
        private bool _stylesInitialized;
        
        [MenuItem("Tools/Visual Understanding/Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<VisualUnderstandingSetupEditor>("Visual Understanding Setup");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }
        
        [MenuItem("Tools/Visual Understanding/Quick Setup")]
        public static void QuickSetup()
        {
            var window = GetWindow<VisualUnderstandingSetupEditor>();
            window.PerformSetup();
        }
        
        private void OnEnable()
        {
            CheckSceneComponents();
        }
        
        private void OnFocus()
        {
            CheckSceneComponents();
        }
        
        private void InitStyles()
        {
            if (_stylesInitialized) return;
            
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 5)
            };
            
            _sectionStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 5, 10)
            };
            
            _stylesInitialized = true;
        }
        
        private void OnGUI()
        {
            InitStyles();
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            // Title
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Visual Understanding System", _headerStyle);
            EditorGUILayout.LabelField("Vision LLM-powered sectional chart and weather radar analysis", EditorStyles.miniLabel);
            EditorGUILayout.Space(10);
            
            // Scene status
            DrawSceneStatus();
            
            // Server configuration
            DrawServerConfig();
            
            // UI configuration
            DrawUIConfig();
            
            // Setup button
            DrawSetupButton();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void CheckSceneComponents()
        {
            _existingManager = FindObjectOfType<VisualAnalysisManager>();
            _existingClient = FindObjectOfType<VisionLLMClient>();
            _existingPanel = FindObjectOfType<VisualBriefingPanel>();
            _existingSettings = AssetDatabase.LoadAssetAtPath<VisionSettings>("Assets/_Project/Scripts/VisualUnderstanding/Settings/VisionSettings.asset");
        }
        
        private void DrawSceneStatus()
        {
            EditorGUILayout.BeginVertical(_sectionStyle);
            EditorGUILayout.LabelField("Scene Status", EditorStyles.boldLabel);
            
            DrawStatusLine("Analysis Manager", _existingManager != null);
            DrawStatusLine("Vision LLM Client", _existingClient != null);
            DrawStatusLine("Briefing Panel", _existingPanel != null);
            DrawStatusLine("Vision Settings Asset", _existingSettings != null);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawStatusLine(string label, bool exists)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(150));
            EditorGUILayout.LabelField(exists ? "✓ Found" : "✗ Not found", 
                exists ? EditorStyles.boldLabel : EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawServerConfig()
        {
            EditorGUILayout.BeginVertical(_sectionStyle);
            EditorGUILayout.LabelField("Server Configuration", EditorStyles.boldLabel);
            
            // Server URL with auto-fetch on change
            EditorGUI.BeginChangeCheck();
            _serverUrl = EditorGUILayout.TextField("Server URL", _serverUrl);
            if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(_serverUrl) && _serverUrl != _lastFetchedUrl)
            {
                FetchAndAutoFillModel();
            }
            
            // Model field - editable with auto-detect option
            EditorGUILayout.BeginHorizontal();
            _modelName = EditorGUILayout.TextField("Model Name", _modelName);
            GUI.enabled = !_isFetchingModels && !string.IsNullOrEmpty(_serverUrl);
            if (GUILayout.Button(_isFetchingModels ? "..." : "Detect", GUILayout.Width(55)))
            {
                FetchAndAutoFillModel();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            if (!string.IsNullOrEmpty(_modelFetchStatus))
            {
                EditorGUILayout.HelpBox(_modelFetchStatus, string.IsNullOrEmpty(_modelName) ? MessageType.Warning : MessageType.Info);
            }
            
            _apiKey = EditorGUILayout.TextField("API Key", _apiKey);
            
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Test Connection"))
            {
                TestConnection();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawUIConfig()
        {
            EditorGUILayout.BeginVertical(_sectionStyle);
            EditorGUILayout.LabelField("UI Configuration", EditorStyles.boldLabel);
            
            _createUI = EditorGUILayout.Toggle("Create UI Panel", _createUI);
            
            if (_createUI)
            {
                EditorGUI.indentLevel++;
                _positionIndex = EditorGUILayout.Popup("Panel Position", _positionIndex, _positionOptions);
                EditorGUI.indentLevel--;
            }
            
            _createSettings = EditorGUILayout.Toggle("Create Settings Asset", _createSettings);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSetupButton()
        {
            EditorGUILayout.Space(10);
            
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Setup Visual Understanding System", GUILayout.Height(40)))
            {
                PerformSetup();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.Space(5);
            
            if (_existingManager != null)
            {
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("Remove Existing Setup"))
                {
                    if (EditorUtility.DisplayDialog("Remove Visual Understanding",
                        "This will remove all Visual Understanding components from the scene. Continue?",
                        "Yes", "Cancel"))
                    {
                        RemoveExistingSetup();
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }
        
        private void TestConnection()
        {
            EditorUtility.DisplayProgressBar("Testing Connection", "Connecting to vision LLM server...", 0.5f);
            
            // Simple HTTP test
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = System.TimeSpan.FromSeconds(10);
                    var response = client.GetAsync($"{_serverUrl.TrimEnd('/')}/models").Result;
                    
                    EditorUtility.ClearProgressBar();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        EditorUtility.DisplayDialog("Connection Test", 
                            $"✓ Successfully connected to {_serverUrl}", "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Connection Test",
                            $"⚠ Server responded with status: {response.StatusCode}\nThe server may still work for vision requests.", "OK");
                    }
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Connection Test",
                    $"✗ Failed to connect:\n{e.Message}", "OK");
            }
        }
        
        private void FetchAndAutoFillModel()
        {
            if (string.IsNullOrEmpty(_serverUrl)) return;
            
            _isFetchingModels = true;
            _modelFetchStatus = "Detecting model...";
            _lastFetchedUrl = _serverUrl;
            
            // Normalize URL - remove /v1 suffix since FetchAvailableModels adds it
            string baseUrl = _serverUrl.TrimEnd('/');
            if (baseUrl.EndsWith("/v1"))
            {
                baseUrl = baseUrl.Substring(0, baseUrl.Length - 3);
            }
            
            EditorApplication.delayCall += () =>
            {
                VoiceControl.Network.LLMClient.FetchAvailableModels(baseUrl, (models, error) =>
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        _modelFetchStatus = error;
                    }
                    else if (models != null && models.Count > 0)
                    {
                        _modelName = models[0];
                        _modelFetchStatus = $"Detected: {_modelName}";
                    }
                    else
                    {
                        _modelFetchStatus = "No models found";
                    }
                    _isFetchingModels = false;
                    Repaint();
                });
            };
        }
        
        private void PerformSetup()
        {
            Undo.SetCurrentGroupName("Setup Visual Understanding System");
            int undoGroup = Undo.GetCurrentGroup();
            
            // Create or find settings
            VisionSettings settings = CreateOrFindSettings();
            
            // Create main manager object
            GameObject managerGO;
            if (_existingManager != null)
            {
                managerGO = _existingManager.gameObject;
            }
            else
            {
                managerGO = new GameObject("Visual Understanding Manager");
                Undo.RegisterCreatedObjectUndo(managerGO, "Create Manager");
            }
            
            // Add components
            var manager = managerGO.GetComponent<VisualAnalysisManager>() ?? managerGO.AddComponent<VisualAnalysisManager>();
            var visionClient = managerGO.GetComponent<VisionLLMClient>() ?? managerGO.AddComponent<VisionLLMClient>();
            var sectionalAnalyzer = managerGO.GetComponent<SectionalChartAnalyzer>() ?? managerGO.AddComponent<SectionalChartAnalyzer>();
            var weatherAnalyzer = managerGO.GetComponent<WeatherRadarAnalyzer>() ?? managerGO.AddComponent<WeatherRadarAnalyzer>();
            
            // Add voice control adapter
            var voiceAdapter = managerGO.GetComponent<VoiceControl.Adapters.VisionVoiceAdapter>() 
                ?? managerGO.AddComponent<VoiceControl.Adapters.VisionVoiceAdapter>();
            
            // Configure settings via serialized properties
            ConfigureManager(manager, settings, visionClient, sectionalAnalyzer, weatherAnalyzer);
            ConfigureVisionClient(visionClient, settings);
            ConfigureAnalyzer(sectionalAnalyzer, settings, visionClient);
            ConfigureAnalyzer(weatherAnalyzer, settings, visionClient);
            
            // Create UI if requested
            if (_createUI)
            {
                CreateUI(settings);
            }
            
            Undo.CollapseUndoOperations(undoGroup);
            
            CheckSceneComponents();
            
            EditorUtility.DisplayDialog("Setup Complete",
                "Visual Understanding System has been set up successfully!\n\n" +
                "• VisualAnalysisManager added to scene\n" +
                "• VisionLLMClient configured\n" +
                (_createUI ? "• UI Panel created\n• Weather Radar button (WX) created\n• Sectional Chart button (SEC) created\n" : "") +
                "\nPress the WX or SEC buttons to trigger vision analysis.",
                "OK");
        }
        
        private VisionSettings CreateOrFindSettings()
        {
            if (_existingSettings != null)
            {
                return _existingSettings;
            }
            
            if (!_createSettings)
            {
                return null;
            }
            
            // Create settings directory
            string settingsDir = "Assets/_Project/Scripts/VisualUnderstanding/Settings";
            if (!AssetDatabase.IsValidFolder(settingsDir))
            {
                System.IO.Directory.CreateDirectory(settingsDir);
                AssetDatabase.Refresh();
            }
            
            // Create settings asset
            var settings = CreateInstance<VisionSettings>();
            settings.serverUrl = _serverUrl;
            settings.modelName = _modelName;
            settings.apiKey = _apiKey;
            
            AssetDatabase.CreateAsset(settings, $"{settingsDir}/VisionSettings.asset");
            AssetDatabase.SaveAssets();
            
            _existingSettings = settings;
            return settings;
        }
        
        private void ConfigureManager(VisualAnalysisManager manager, VisionSettings settings, 
            VisionLLMClient client, SectionalChartAnalyzer sectional, WeatherRadarAnalyzer weather)
        {
            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("visionClient").objectReferenceValue = client;
            so.FindProperty("sectionalAnalyzer").objectReferenceValue = sectional;
            so.FindProperty("weatherRadarAnalyzer").objectReferenceValue = weather;
            so.ApplyModifiedProperties();
        }
        
        private void ConfigureVisionClient(VisionLLMClient client, VisionSettings settings)
        {
            SerializedObject so = new SerializedObject(client);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.ApplyModifiedProperties();
        }
        
        private void ConfigureAnalyzer(SectionalChartAnalyzer analyzer, VisionSettings settings, VisionLLMClient client)
        {
            SerializedObject so = new SerializedObject(analyzer);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("visionClient").objectReferenceValue = client;
            so.ApplyModifiedProperties();
        }
        
        private void ConfigureAnalyzer(WeatherRadarAnalyzer analyzer, VisionSettings settings, VisionLLMClient client)
        {
            SerializedObject so = new SerializedObject(analyzer);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("visionClient").objectReferenceValue = client;
            so.ApplyModifiedProperties();
        }
        
        private void CreateUI(VisionSettings settings)
        {
            // Find or create canvas
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasGO = new GameObject("Visual Understanding Canvas");
                Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }
            
            // Create panel
            var panelGO = new GameObject("Visual Briefing Panel");
            Undo.RegisterCreatedObjectUndo(panelGO, "Create Panel");
            panelGO.transform.SetParent(canvas.transform, false);
            
            var panelRect = panelGO.AddComponent<RectTransform>();
            var panelImage = panelGO.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            
            var panel = panelGO.AddComponent<VisualBriefingPanel>();
            var canvasGroup = panelGO.AddComponent<CanvasGroup>();
            
            // Position based on selection
            SetPanelPosition(panelRect, _positionIndex);
            
            // Initial minimum size - panel will expand as text grows
            panelRect.sizeDelta = new Vector2(350, 150);
            
            // Add VerticalLayoutGroup for automatic child arrangement
            var panelLayout = panelGO.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(15, 15, 15, 15);
            panelLayout.spacing = 8;
            panelLayout.childAlignment = TextAnchor.UpperLeft;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;
            
            // Add ContentSizeFitter to make panel expand with content
            var panelSizeFitter = panelGO.AddComponent<ContentSizeFitter>();
            panelSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            panelSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // Add LayoutElement to constrain min/max size
            var panelLayoutElement = panelGO.AddComponent<LayoutElement>();
            panelLayoutElement.minWidth = 350;
            panelLayoutElement.minHeight = 150;
            panelLayoutElement.preferredWidth = 350;
            
            // Create header
            var headerGO = CreateTextObject("Header", panelGO.transform, "Visual Briefing", 18);
            var headerLayoutElement = headerGO.AddComponent<LayoutElement>();
            headerLayoutElement.minHeight = 30;
            headerLayoutElement.preferredHeight = 30;
            
            // Create status text
            var statusGO = CreateTextObject("Status", panelGO.transform, "Ready", 12);
            var statusLayoutElement = statusGO.AddComponent<LayoutElement>();
            statusLayoutElement.minHeight = 20;
            statusLayoutElement.preferredHeight = 20;
            var statusTMP = statusGO.GetComponent<TMP_Text>();
            statusTMP.color = new Color(0.7f, 0.7f, 0.7f);
            
            // Create summary text directly in the panel (not nested container)
            var summaryTextGO = CreateTextObject("Summary Text", panelGO.transform, "", 14);
            var summaryTextTMP = summaryTextGO.GetComponent<TextMeshProUGUI>();
            summaryTextTMP.enableWordWrapping = true;
            summaryTextTMP.overflowMode = TextOverflowModes.Overflow;
            
            // Add ContentSizeFitter to summary text so it expands with content
            var summarySizeFitter = summaryTextGO.AddComponent<ContentSizeFitter>();
            summarySizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            summarySizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // Add LayoutElement to summary for proper layout participation
            var summaryLayoutElement = summaryTextGO.AddComponent<LayoutElement>();
            summaryLayoutElement.minHeight = 50;
            summaryLayoutElement.flexibleHeight = 1;
            summaryLayoutElement.flexibleWidth = 0;
            
            var animatedText = summaryTextGO.AddComponent<AnimatedTMPText>();
            
            // Create findings container
            var findingsGO = new GameObject("Findings Container");
            findingsGO.transform.SetParent(panelGO.transform, false);
            var findingsLayoutElement = findingsGO.AddComponent<LayoutElement>();
            findingsLayoutElement.minHeight = 0;
            findingsLayoutElement.flexibleHeight = 0;
            
            var findingsRect = findingsGO.GetComponent<RectTransform>();
            if (findingsRect == null) findingsRect = findingsGO.AddComponent<RectTransform>();
            
            var verticalLayout = findingsGO.AddComponent<VerticalLayoutGroup>();
            verticalLayout.spacing = 5;
            verticalLayout.childControlHeight = false;
            verticalLayout.childControlWidth = true;
            verticalLayout.childForceExpandHeight = false;
            
            var findingsSizeFitter = findingsGO.AddComponent<ContentSizeFitter>();
            findingsSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            findingsSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // Create loading indicator
            var loadingGO = new GameObject("Loading Indicator");
            loadingGO.transform.SetParent(panelGO.transform, false);
            var loadingRect = loadingGO.AddComponent<RectTransform>();
            loadingRect.anchorMin = new Vector2(0.5f, 0.5f);
            loadingRect.anchorMax = new Vector2(0.5f, 0.5f);
            loadingRect.sizeDelta = new Vector2(50, 50);
            
            var loadingImage = loadingGO.AddComponent<Image>();
            loadingImage.color = new Color(0.4f, 0.8f, 1f);
            loadingGO.SetActive(false);
            
            // Create image preview (positioned to the left of the panel)
            var previewGO = new GameObject("Image Preview");
            previewGO.transform.SetParent(panelGO.transform.parent, false);
            var previewRect = previewGO.AddComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0, 0.5f);
            previewRect.anchorMax = new Vector2(0, 0.5f);
            previewRect.pivot = new Vector2(1, 0.5f);
            previewRect.anchoredPosition = new Vector2(-10, 0); // To the left of panel
            previewRect.sizeDelta = new Vector2(150, 150);
            
            var rawImage = previewGO.AddComponent<RawImage>();
            rawImage.color = new Color(1f, 1f, 1f, 0f); // Start transparent
            previewGO.SetActive(false);
            
            // Add rounded corners effect via mask
            var previewMask = previewGO.AddComponent<UnityEngine.UI.Outline>();
            previewMask.effectColor = new Color(0.2f, 0.4f, 0.6f, 0.8f);
            previewMask.effectDistance = new Vector2(2, -2);
            
            // Configure panel component
            SerializedObject so = new SerializedObject(panel);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("headerText").objectReferenceValue = headerGO.GetComponent<TMP_Text>();
            so.FindProperty("statusText").objectReferenceValue = statusGO.GetComponent<TMP_Text>();
            so.FindProperty("summaryText").objectReferenceValue = summaryTextGO.GetComponent<TMP_Text>();
            so.FindProperty("animatedSummary").objectReferenceValue = animatedText;
            so.FindProperty("findingsContainer").objectReferenceValue = findingsGO.transform;
            so.FindProperty("loadingIndicator").objectReferenceValue = loadingGO;
            so.FindProperty("imagePreview").objectReferenceValue = rawImage;
            so.ApplyModifiedProperties();
            
            // Create trigger buttons
            CreateTriggerButtons(canvas, settings);
            
            // Create briefing item prefab
            CreateBriefingItemPrefab();
        }
        
        private GameObject CreateTextObject(string name, Transform parent, string text, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            
            var rect = go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            
            return go;
        }
        
        private void SetPanelPosition(RectTransform rect, int positionIndex)
        {
            float margin = 20f;
            
            // Pivot Y is always 1 (top) so panel expands downward
            switch (positionIndex)
            {
                case 0: // Bottom Left - anchor at top-left, positioned near bottom
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(0, 0);
                    rect.pivot = new Vector2(0, 1); // Top-left pivot, expands down
                    rect.anchoredPosition = new Vector2(margin, margin + 150); // Start higher since pivot is at top
                    break;
                case 1: // Bottom Right - anchor at top-right, positioned near bottom
                    rect.anchorMin = new Vector2(1, 0);
                    rect.anchorMax = new Vector2(1, 0);
                    rect.pivot = new Vector2(1, 1); // Top-right pivot, expands down
                    rect.anchoredPosition = new Vector2(-margin, margin + 150);
                    break;
                case 2: // Top Left
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.pivot = new Vector2(0, 1); // Top-left pivot, expands down
                    rect.anchoredPosition = new Vector2(margin, -margin);
                    break;
                case 3: // Top Right
                    rect.anchorMin = new Vector2(1, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.pivot = new Vector2(1, 1); // Top-right pivot, expands down
                    rect.anchoredPosition = new Vector2(-margin, -margin);
                    break;
            }
        }
        
        private void CreateTriggerButtons(Canvas canvas, VisionSettings settings)
        {
            // Create button container
            var containerGO = new GameObject("Analysis Trigger Buttons");
            Undo.RegisterCreatedObjectUndo(containerGO, "Create Trigger Buttons");
            containerGO.transform.SetParent(canvas.transform, false);
            
            var containerRect = containerGO.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(1, 0.5f);
            containerRect.anchorMax = new Vector2(1, 0.5f);
            containerRect.pivot = new Vector2(1, 0.5f);
            containerRect.anchoredPosition = new Vector2(-20, 0);
            containerRect.sizeDelta = new Vector2(60, 140);
            
            var verticalLayout = containerGO.AddComponent<VerticalLayoutGroup>();
            verticalLayout.spacing = 10;
            verticalLayout.childAlignment = TextAnchor.MiddleCenter;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = false;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;
            
            // Create Weather Radar button
            CreateTriggerButton(containerGO.transform, "Weather Radar", VisualAnalysisType.WeatherRadar, 
                new Color(0.2f, 0.5f, 0.8f, 1f), "☁", settings);
            
            // Create Sectional Chart button
            CreateTriggerButton(containerGO.transform, "Sectional Chart", VisualAnalysisType.SectionalChart, 
                new Color(0.4f, 0.6f, 0.3f, 1f), "🗺", settings);
        }
        
        private void CreateTriggerButton(Transform parent, string label, VisualAnalysisType type, Color color, string icon, VisionSettings settings)
        {
            var buttonGO = new GameObject($"{label} Button");
            buttonGO.transform.SetParent(parent, false);
            
            var rect = buttonGO.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(60, 60);
            
            var image = buttonGO.AddComponent<Image>();
            image.color = color;
            
            // Round corners effect via simple masking
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1;
            
            var button = buttonGO.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = new Color(color.r + 0.1f, color.g + 0.1f, color.b + 0.1f, 1f);
            colors.pressedColor = new Color(color.r - 0.1f, color.g - 0.1f, color.b - 0.1f, 1f);
            button.colors = colors;
            
            var triggerButton = buttonGO.AddComponent<AnalysisTriggerButton>();
            var canvasGroup = buttonGO.AddComponent<CanvasGroup>();
            
            // Configure button component
            SerializedObject so = new SerializedObject(triggerButton);
            so.FindProperty("analysisType").enumValueIndex = (int)type;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("buttonImage").objectReferenceValue = image;
            so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("normalColor").colorValue = color;
            so.FindProperty("processingColor").colorValue = new Color(0.4f, 0.8f, 1f, 1f);
            so.FindProperty("successColor").colorValue = new Color(0.3f, 0.8f, 0.4f, 1f);
            so.FindProperty("errorColor").colorValue = new Color(1f, 0.3f, 0.3f, 1f);
            so.ApplyModifiedProperties();
            
            // Create icon text
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(buttonGO.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(5, 20);
            iconRect.offsetMax = new Vector2(-5, -5);
            
            var iconTMP = iconGO.AddComponent<TextMeshProUGUI>();
            iconTMP.text = icon;
            iconTMP.fontSize = 24;
            iconTMP.color = Color.white;
            iconTMP.alignment = TextAlignmentOptions.Center;
            
            // Create label text
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(buttonGO.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(1, 0);
            labelRect.pivot = new Vector2(0.5f, 0);
            labelRect.anchoredPosition = new Vector2(0, 5);
            labelRect.sizeDelta = new Vector2(0, 15);
            
            var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
            labelTMP.text = type == VisualAnalysisType.WeatherRadar ? "WX" : "SEC";
            labelTMP.fontSize = 10;
            labelTMP.color = Color.white;
            labelTMP.alignment = TextAlignmentOptions.Center;
            labelTMP.fontStyle = FontStyles.Bold;
            
            // Link label to button
            SerializedObject so2 = new SerializedObject(triggerButton);
            so2.FindProperty("labelText").objectReferenceValue = labelTMP;
            so2.ApplyModifiedProperties();
        }
        
        private void CreateBriefingItemPrefab()
        {
            string prefabDir = "Assets/_Project/Scripts/VisualUnderstanding/Prefabs";
            string prefabPath = $"{prefabDir}/BriefingItem.prefab";
            
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                return; // Already exists
            }
            
            if (!AssetDatabase.IsValidFolder(prefabDir))
            {
                System.IO.Directory.CreateDirectory(prefabDir);
                AssetDatabase.Refresh();
            }
            
            // Create prefab
            var itemGO = new GameObject("BriefingItem");
            
            var rect = itemGO.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 30);
            
            var layout = itemGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5;
            layout.padding = new RectOffset(5, 5, 2, 2);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            
            var bg = itemGO.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);
            
            var itemUI = itemGO.AddComponent<BriefingItemUI>();
            var canvasGroup = itemGO.AddComponent<CanvasGroup>();
            
            // Priority indicator
            var indicatorGO = new GameObject("Priority Indicator");
            indicatorGO.transform.SetParent(itemGO.transform, false);
            var indicatorRect = indicatorGO.AddComponent<RectTransform>();
            indicatorRect.sizeDelta = new Vector2(4, 26);
            var indicatorImage = indicatorGO.AddComponent<Image>();
            indicatorImage.color = new Color(0.4f, 0.8f, 1f);
            
            // Title text
            var titleGO = CreateTextObject("Title", itemGO.transform, "Finding", 13);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(300, 26);
            
            // Configure component references
            SerializedObject so = new SerializedObject(itemUI);
            so.FindProperty("priorityIndicator").objectReferenceValue = indicatorImage;
            so.FindProperty("titleText").objectReferenceValue = titleGO.GetComponent<TMP_Text>();
            so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            so.ApplyModifiedProperties();
            
            // Save as prefab
            PrefabUtility.SaveAsPrefabAsset(itemGO, prefabPath);
            DestroyImmediate(itemGO);
            
            // Assign prefab to panel
            var panel = FindObjectOfType<VisualBriefingPanel>();
            if (panel != null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                SerializedObject panelSO = new SerializedObject(panel);
                panelSO.FindProperty("briefingItemPrefab").objectReferenceValue = prefab;
                panelSO.ApplyModifiedProperties();
            }
        }
        
        private void RemoveExistingSetup()
        {
            if (_existingManager != null)
            {
                Undo.DestroyObjectImmediate(_existingManager.gameObject);
            }
            
            if (_existingPanel != null)
            {
                Undo.DestroyObjectImmediate(_existingPanel.gameObject);
            }
            
            CheckSceneComponents();
        }
        
        /// <summary>
        /// Menu option to upgrade an existing panel to expandable layout
        /// </summary>
        [MenuItem("Tools/Visual Understanding/Upgrade Panel to Expandable")]
        public static void UpgradeExistingPanelToExpandable()
        {
            var panel = FindObjectOfType<VisualBriefingPanel>();
            if (panel == null)
            {
                EditorUtility.DisplayDialog("Upgrade Panel", 
                    "No Visual Briefing Panel found in the scene. Please run the Setup Wizard first.", "OK");
                return;
            }
            
            Undo.SetCurrentGroupName("Upgrade Panel to Expandable");
            int undoGroup = Undo.GetCurrentGroup();
            
            var panelGO = panel.gameObject;
            var panelRect = panelGO.GetComponent<RectTransform>();
            
            // Adjust pivot so panel expands downward from top (pivot Y = 1)
            Vector2 currentPivot = panelRect.pivot;
            if (Mathf.Abs(currentPivot.y) < 0.5f) // If pivot is at bottom
            {
                // Move pivot to top while keeping position visually the same
                float panelHeight = panelRect.sizeDelta.y;
                Vector2 newPivot = new Vector2(currentPivot.x, 1f);
                Vector2 posOffset = new Vector2(0, panelHeight);
                
                Undo.RecordObject(panelRect, "Adjust Pivot");
                panelRect.pivot = newPivot;
                panelRect.anchoredPosition += posOffset;
            }
            
            // Add or configure VerticalLayoutGroup
            var panelLayout = panelGO.GetComponent<VerticalLayoutGroup>();
            if (panelLayout == null)
            {
                panelLayout = Undo.AddComponent<VerticalLayoutGroup>(panelGO);
            }
            panelLayout.padding = new RectOffset(15, 15, 15, 15);
            panelLayout.spacing = 8;
            panelLayout.childAlignment = TextAnchor.UpperLeft;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;
            
            // Add ContentSizeFitter to panel
            var panelSizeFitter = panelGO.GetComponent<ContentSizeFitter>();
            if (panelSizeFitter == null)
            {
                panelSizeFitter = Undo.AddComponent<ContentSizeFitter>(panelGO);
            }
            panelSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            panelSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // Add LayoutElement to panel for min size
            var panelLayoutElement = panelGO.GetComponent<LayoutElement>();
            if (panelLayoutElement == null)
            {
                panelLayoutElement = Undo.AddComponent<LayoutElement>(panelGO);
            }
            panelLayoutElement.minWidth = 350;
            panelLayoutElement.minHeight = 150;
            panelLayoutElement.preferredWidth = 350;
            
            // Find and configure summary text
            var summaryTextTMP = panel.transform.Find("Summary Text")?.GetComponent<TextMeshProUGUI>();
            if (summaryTextTMP == null)
            {
                // Try nested structure
                summaryTextTMP = panel.transform.Find("Summary/Summary Text")?.GetComponent<TextMeshProUGUI>();
            }
            
            if (summaryTextTMP != null)
            {
                var summaryGO = summaryTextTMP.gameObject;
                
                // Enable word wrapping and overflow
                summaryTextTMP.enableWordWrapping = true;
                summaryTextTMP.overflowMode = TextOverflowModes.Overflow;
                
                // Add ContentSizeFitter to summary
                var summarySizeFitter = summaryGO.GetComponent<ContentSizeFitter>();
                if (summarySizeFitter == null)
                {
                    summarySizeFitter = Undo.AddComponent<ContentSizeFitter>(summaryGO);
                }
                summarySizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                summarySizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                
                // Add LayoutElement to summary
                var summaryLayoutElement = summaryGO.GetComponent<LayoutElement>();
                if (summaryLayoutElement == null)
                {
                    summaryLayoutElement = Undo.AddComponent<LayoutElement>(summaryGO);
                }
                summaryLayoutElement.minHeight = 50;
                summaryLayoutElement.flexibleHeight = 1;
                summaryLayoutElement.flexibleWidth = 0;
                
                // If summary was nested in a container, move it directly under panel
                if (summaryGO.transform.parent != panel.transform)
                {
                    var oldParent = summaryGO.transform.parent;
                    summaryGO.transform.SetParent(panel.transform, false);
                    summaryGO.transform.SetSiblingIndex(2); // After header and status
                    
                    // Remove the old empty container
                    if (oldParent != null && oldParent.childCount == 0)
                    {
                        Undo.DestroyObjectImmediate(oldParent.gameObject);
                    }
                }
                
                // Wire up the serialized properties
                SerializedObject so = new SerializedObject(panel);
                so.FindProperty("panelSizeFitter").objectReferenceValue = panelSizeFitter;
                so.FindProperty("summarySizeFitter").objectReferenceValue = summarySizeFitter;
                so.FindProperty("summaryLayoutElement").objectReferenceValue = summaryLayoutElement;
                so.ApplyModifiedProperties();
            }
            
            // Configure header with LayoutElement
            var headerTMP = panel.transform.Find("Header")?.GetComponent<TMP_Text>();
            if (headerTMP != null)
            {
                var headerLayoutElement = headerTMP.gameObject.GetComponent<LayoutElement>();
                if (headerLayoutElement == null)
                {
                    headerLayoutElement = Undo.AddComponent<LayoutElement>(headerTMP.gameObject);
                }
                headerLayoutElement.minHeight = 30;
                headerLayoutElement.preferredHeight = 30;
            }
            
            // Configure status with LayoutElement
            var statusTMP = panel.transform.Find("Status")?.GetComponent<TMP_Text>();
            if (statusTMP != null)
            {
                var statusLayoutElement = statusTMP.gameObject.GetComponent<LayoutElement>();
                if (statusLayoutElement == null)
                {
                    statusLayoutElement = Undo.AddComponent<LayoutElement>(statusTMP.gameObject);
                }
                statusLayoutElement.minHeight = 20;
                statusLayoutElement.preferredHeight = 20;
            }
            
            // Configure findings container
            var findingsContainer = panel.transform.Find("Findings Container");
            if (findingsContainer != null)
            {
                var findingsLayoutElement = findingsContainer.GetComponent<LayoutElement>();
                if (findingsLayoutElement == null)
                {
                    findingsLayoutElement = Undo.AddComponent<LayoutElement>(findingsContainer.gameObject);
                }
                findingsLayoutElement.minHeight = 0;
                findingsLayoutElement.flexibleHeight = 0;
                
                var findingsSizeFitter = findingsContainer.GetComponent<ContentSizeFitter>();
                if (findingsSizeFitter == null)
                {
                    findingsSizeFitter = Undo.AddComponent<ContentSizeFitter>(findingsContainer.gameObject);
                }
                findingsSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                findingsSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            
            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.SetDirty(panel);
            
            EditorUtility.DisplayDialog("Upgrade Complete",
                "The Visual Briefing Panel has been upgraded to expand with streaming text.\n\n" +
                "The panel will now dynamically resize as text content grows, with a minimum height of 150 pixels.",
                "OK");
        }
    }
}
#endif
