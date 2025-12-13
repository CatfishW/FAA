#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using VoiceControl.Core;
using VoiceControl.Network;
using VoiceControl.Manager;
using VoiceControl.UI;
using VoiceControl.Adapters;

namespace VoiceControl.Editor
{
    /// <summary>
    /// Editor wizard for easy setup of Voice Control System.
    /// Provides one-click creation of all required components.
    /// </summary>
    public class VoiceControlSetupEditor : EditorWindow
    {
        #region Styles
        
        private GUIStyle _headerStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _statusOkStyle;
        private GUIStyle _statusErrorStyle;
        
        #endregion
        
        #region Settings
        
        private string _sttServerUrl = "http://localhost:25567";
        private string _llmServerUrl = "http://localhost:25565";
        private string _llmModelName = "qwen3-30b-a3b-instruct";
        private string _llmApiKey = "";
        
        private Color _primaryColor = new Color(0.2f, 0.6f, 1f, 1f);
        private Color _secondaryColor = new Color(0.1f, 0.12f, 0.15f, 0.95f);
        private Color _recordingColor = new Color(1f, 0.3f, 0.3f, 1f);
        
        private VoiceUIPosition _uiPosition = VoiceUIPosition.BottomRight;
        private bool _createAdapters = true;
        private bool _createTestConnectivity = true;
        
        #endregion
        
        #region State
        
        private Vector2 _scrollPosition;
        private bool _isTestingConnectivity;
        private string _sttStatus = "";
        private string _llmStatus = "";
        private bool _sttOk;
        private bool _llmOk;
        
        // Scene components
        private VoiceControlManager _existingManager;
        private VoiceCommandRegistry _existingRegistry;
        private VoiceControlUI _existingUI;
        private bool _checkedScene;
        
        // Model auto-detection
        private bool _isFetchingModels;
        private string _modelFetchStatus = "";
        private string _lastFetchedUrl = "";
        
        #endregion
        
        [MenuItem("Tools/Aviation/Voice Control Setup", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<VoiceControlSetupEditor>("Voice Control Setup");
            window.minSize = new Vector2(450, 600);
            window.Show();
        }
        
        [MenuItem("Tools/Aviation/Voice Control Setup - Quick Setup", priority = 101)]
        public static void QuickSetup()
        {
            var window = GetWindow<VoiceControlSetupEditor>("Voice Control Setup");
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
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    margin = new RectOffset(0, 0, 10, 5)
                };
            }
            
            if (_sectionStyle == null)
            {
                _sectionStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 10, 10),
                    margin = new RectOffset(0, 0, 5, 5)
                };
            }
            
            if (_statusOkStyle == null)
            {
                _statusOkStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = new Color(0.3f, 0.9f, 0.4f) }
                };
            }
            
            if (_statusErrorStyle == null)
            {
                _statusErrorStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = new Color(1f, 0.4f, 0.4f) }
                };
            }
        }
        
        private void OnGUI()
        {
            InitStyles();
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            // Title
            EditorGUILayout.Space(10);
            GUILayout.Label("Voice Control System Setup", _headerStyle);
            EditorGUILayout.HelpBox(
                "This wizard helps you set up the voice control system for aviation radar controls.\n" +
                "The system uses remote STT (Speech-to-Text) and LLM servers for voice command processing.",
                MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // Scene Status
            DrawSceneStatus();
            
            EditorGUILayout.Space(10);
            
            // Server Configuration
            DrawServerConfig();
            
            EditorGUILayout.Space(10);
            
            // UI Configuration
            DrawUIConfig();
            
            EditorGUILayout.Space(10);
            
            // Component Options
            DrawComponentOptions();
            
            EditorGUILayout.Space(20);
            
            // Setup Button
            DrawSetupButton();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void CheckSceneComponents()
        {
            _existingManager = FindObjectOfType<VoiceControlManager>();
            _existingRegistry = FindObjectOfType<VoiceCommandRegistry>();
            _existingUI = FindObjectOfType<VoiceControlUI>();
            _checkedScene = true;
        }
        
        private void DrawSceneStatus()
        {
            GUILayout.Label("Current Scene Status", _headerStyle);
            
            EditorGUILayout.BeginVertical(_sectionStyle);
            
            DrawStatusLine("VoiceControlManager", _existingManager != null);
            DrawStatusLine("VoiceCommandRegistry", _existingRegistry != null);
            DrawStatusLine("VoiceControlUI", _existingUI != null);
            DrawStatusLine("WeatherRadarVoiceAdapter", FindObjectOfType<WeatherRadarVoiceAdapter>() != null);
            DrawStatusLine("TrafficRadarVoiceAdapter", FindObjectOfType<TrafficRadarVoiceAdapter>() != null);
            DrawStatusLine("IndicatorSystemVoiceAdapter", FindObjectOfType<IndicatorSystemVoiceAdapter>() != null);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawStatusLine(string label, bool exists)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(200));
            GUILayout.Label(exists ? "✓ Found" : "✗ Not Found", exists ? _statusOkStyle : _statusErrorStyle);
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawServerConfig()
        {
            GUILayout.Label("Server Configuration", _headerStyle);
            
            EditorGUILayout.BeginVertical(_sectionStyle);
            
            EditorGUILayout.LabelField("Speech-to-Text Server (Whisper.cpp)", EditorStyles.boldLabel);
            _sttServerUrl = EditorGUILayout.TextField("STT Server URL", _sttServerUrl);
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("LLM Server (OpenAI-compatible)", EditorStyles.boldLabel);
            
            // LLM URL with auto-fetch on change
            EditorGUI.BeginChangeCheck();
            _llmServerUrl = EditorGUILayout.TextField("LLM Server URL", _llmServerUrl);
            if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(_llmServerUrl) && _llmServerUrl != _lastFetchedUrl)
            {
                FetchAndAutoFillModel();
            }
            
            // Show detected model (read-only)
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Model (auto-detected)", _llmModelName);
            EditorGUI.EndDisabledGroup();
            GUI.enabled = !_isFetchingModels && !string.IsNullOrEmpty(_llmServerUrl);
            if (GUILayout.Button(_isFetchingModels ? "..." : "Detect", GUILayout.Width(55)))
            {
                FetchAndAutoFillModel();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            if (!string.IsNullOrEmpty(_modelFetchStatus))
            {
                EditorGUILayout.HelpBox(_modelFetchStatus, string.IsNullOrEmpty(_llmModelName) ? MessageType.Warning : MessageType.Info);
            }
            
            _llmApiKey = EditorGUILayout.TextField("API Key (optional)", _llmApiKey);
            
            EditorGUILayout.Space(10);
            
            // Connectivity Test
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = !_isTestingConnectivity;
            if (GUILayout.Button("Test Connectivity", GUILayout.Height(25)))
            {
                TestConnectivity();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            if (!string.IsNullOrEmpty(_sttStatus))
            {
                EditorGUILayout.LabelField("STT:", _sttStatus, _sttOk ? _statusOkStyle : _statusErrorStyle);
            }
            if (!string.IsNullOrEmpty(_llmStatus))
            {
                EditorGUILayout.LabelField("LLM:", _llmStatus, _llmOk ? _statusOkStyle : _statusErrorStyle);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawUIConfig()
        {
            GUILayout.Label("UI Configuration", _headerStyle);
            
            EditorGUILayout.BeginVertical(_sectionStyle);
            
            _uiPosition = (VoiceUIPosition)EditorGUILayout.EnumPopup("UI Position", _uiPosition);
            
            EditorGUILayout.Space(5);
            
            _primaryColor = EditorGUILayout.ColorField("Primary Color", _primaryColor);
            _secondaryColor = EditorGUILayout.ColorField("Background Color", _secondaryColor);
            _recordingColor = EditorGUILayout.ColorField("Recording Color", _recordingColor);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawComponentOptions()
        {
            GUILayout.Label("Component Options", _headerStyle);
            
            EditorGUILayout.BeginVertical(_sectionStyle);
            
            _createAdapters = EditorGUILayout.Toggle("Create Voice Adapters", _createAdapters);
            EditorGUILayout.HelpBox(
                "Creates adapters for WeatherRadar, TrafficRadar, and IndicatorSystem if found in the scene.",
                MessageType.None);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSetupButton()
        {
            EditorGUILayout.BeginVertical(_sectionStyle);
            
            bool hasExisting = _existingManager != null || _existingRegistry != null || _existingUI != null;
            
            if (hasExisting)
            {
                EditorGUILayout.HelpBox(
                    "Voice Control components already exist in the scene. Setup will skip existing components.",
                    MessageType.Warning);
            }
            
            GUI.backgroundColor = new Color(0.3f, 0.7f, 0.4f);
            if (GUILayout.Button("Setup Voice Control System", GUILayout.Height(40)))
            {
                PerformSetup();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndVertical();
        }
        
        private void TestConnectivity()
        {
            _isTestingConnectivity = true;
            _sttStatus = "Testing...";
            _llmStatus = "Testing...";
            
            // We can't use async in Editor easily, so we'll do synchronous HTTP requests
            EditorApplication.delayCall += () =>
            {
                // Test STT
                try
                {
                    using (var client = new System.Net.WebClient())
                    {
                        client.DownloadString(_sttServerUrl.TrimEnd('/') + "/healthz");
                        _sttOk = true;
                        _sttStatus = "Connected";
                    }
                }
                catch (System.Exception e)
                {
                    _sttOk = false;
                    _sttStatus = $"Failed: {e.Message}";
                }
                
                // Test LLM
                try
                {
                    using (var client = new System.Net.WebClient())
                    {
                        client.DownloadString(_llmServerUrl.TrimEnd('/') + "/health");
                        _llmOk = true;
                        _llmStatus = "Connected";
                    }
                }
                catch (System.Exception e)
                {
                    _llmOk = false;
                    _llmStatus = $"Failed: {e.Message}";
                }
                
                _isTestingConnectivity = false;
                Repaint();
            };
        }
        
        private void FetchAndAutoFillModel()
        {
            if (string.IsNullOrEmpty(_llmServerUrl)) return;
            
            _isFetchingModels = true;
            _modelFetchStatus = "Detecting model...";
            _lastFetchedUrl = _llmServerUrl;
            
            EditorApplication.delayCall += () =>
            {
                LLMClient.FetchAvailableModels(_llmServerUrl, (models, error) =>
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        _modelFetchStatus = error;
                    }
                    else if (models != null && models.Count > 0)
                    {
                        _llmModelName = models[0];
                        _modelFetchStatus = $"Detected: {_llmModelName}";
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
            Undo.SetCurrentGroupName("Setup Voice Control System");
            int undoGroup = Undo.GetCurrentGroup();
            
            try
            {
                // 1. Create or find VoiceControl root object
                GameObject voiceControlRoot = GameObject.Find("VoiceControlSystem");
                if (voiceControlRoot == null)
                {
                    voiceControlRoot = new GameObject("VoiceControlSystem");
                    Undo.RegisterCreatedObjectUndo(voiceControlRoot, "Create VoiceControlSystem");
                }
                
                // 2. Create Settings asset if needed
                VoiceControlSettings settings = CreateOrFindSettings();
                ConfigureSettings(settings);
                
                // 3. Add VoiceCommandRegistry
                VoiceCommandRegistry registry = FindObjectOfType<VoiceCommandRegistry>();
                if (registry == null)
                {
                    registry = voiceControlRoot.AddComponent<VoiceCommandRegistry>();
                    Undo.RegisterCreatedObjectUndo(registry, "Create VoiceCommandRegistry");
                }
                
                // 4. Add VoiceControlManager
                VoiceControlManager manager = FindObjectOfType<VoiceControlManager>();
                if (manager == null)
                {
                    manager = voiceControlRoot.AddComponent<VoiceControlManager>();
                    Undo.RegisterCreatedObjectUndo(manager, "Create VoiceControlManager");
                    
                    // Set settings via SerializedObject
                    var so = new SerializedObject(manager);
                    so.FindProperty("settings").objectReferenceValue = settings;
                    so.ApplyModifiedProperties();
                }
                
                // 5. Add STTClient and LLMClient
                if (voiceControlRoot.GetComponent<STTClient>() == null)
                {
                    var stt = voiceControlRoot.AddComponent<STTClient>();
                    SerializedObject sttSO = new SerializedObject(stt);
                    sttSO.FindProperty("serverUrl").stringValue = _sttServerUrl;
                    sttSO.ApplyModifiedProperties();
                }
                
                if (voiceControlRoot.GetComponent<LLMClient>() == null)
                {
                    var llm = voiceControlRoot.AddComponent<LLMClient>();
                    SerializedObject llmSO = new SerializedObject(llm);
                    llmSO.FindProperty("serverUrl").stringValue = _llmServerUrl;
                    llmSO.FindProperty("modelName").stringValue = _llmModelName;
                    if (!string.IsNullOrEmpty(_llmApiKey))
                        llmSO.FindProperty("apiKey").stringValue = _llmApiKey;
                    llmSO.ApplyModifiedProperties();
                }
                
                // 6. Add VoiceCommandExecutor
                if (voiceControlRoot.GetComponent<VoiceCommandExecutor>() == null)
                {
                    voiceControlRoot.AddComponent<VoiceCommandExecutor>();
                }
                
                // 7. Create Voice Adapters
                if (_createAdapters)
                {
                    CreateVoiceAdapters(voiceControlRoot);
                }
                
                // 8. Create UI Canvas and components
                CreateVoiceUI(voiceControlRoot, settings);
                
                Undo.CollapseUndoOperations(undoGroup);
                
                CheckSceneComponents();
                
                EditorUtility.DisplayDialog("Voice Control Setup",
                    "Voice Control System has been set up successfully!\n\n" +
                    "Components created:\n" +
                    "- VoiceControlManager\n" +
                    "- VoiceCommandRegistry\n" +
                    "- STTClient & LLMClient\n" +
                    "- Voice Adapters (if targets found)\n" +
                    "- Voice Control UI\n\n" +
                    "You can now use the toggle buttons to control voice input.",
                    "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Voice Control Setup failed: {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog("Setup Error", $"Setup failed: {e.Message}", "OK");
            }
        }
        
        private VoiceControlSettings CreateOrFindSettings()
        {
            // Search for existing settings
            string[] guids = AssetDatabase.FindAssets("t:VoiceControlSettings");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<VoiceControlSettings>(path);
            }
            
            // Create new settings asset
            VoiceControlSettings settings = ScriptableObject.CreateInstance<VoiceControlSettings>();
            
            string settingsPath = "Assets/_Project/Scripts/VoiceControl/Settings";
            if (!AssetDatabase.IsValidFolder(settingsPath))
            {
                System.IO.Directory.CreateDirectory(Application.dataPath + "/../" + settingsPath);
                AssetDatabase.Refresh();
            }
            
            string assetPath = settingsPath + "/VoiceControlSettings.asset";
            AssetDatabase.CreateAsset(settings, assetPath);
            AssetDatabase.SaveAssets();
            
            return settings;
        }
        
        private void ConfigureSettings(VoiceControlSettings settings)
        {
            SerializedObject so = new SerializedObject(settings);
            
            so.FindProperty("sttServerUrl").stringValue = _sttServerUrl;
            so.FindProperty("llmServerUrl").stringValue = _llmServerUrl;
            so.FindProperty("llmModelName").stringValue = _llmModelName;
            so.FindProperty("llmApiKey").stringValue = _llmApiKey;
            so.FindProperty("primaryColor").colorValue = _primaryColor;
            so.FindProperty("secondaryColor").colorValue = _secondaryColor;
            so.FindProperty("recordingColor").colorValue = _recordingColor;
            so.FindProperty("uiPosition").enumValueIndex = (int)_uiPosition;
            
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
        }
        
        private void CreateVoiceAdapters(GameObject root)
        {
            // Weather Radar Adapter
            if (FindObjectOfType<WeatherRadarVoiceAdapter>() == null)
            {
                var weatherPanel = FindObjectOfType<WeatherRadar.RadarControlPanel>();
                if (weatherPanel != null)
                {
                    var adapter = root.AddComponent<WeatherRadarVoiceAdapter>();
                    Debug.Log("Created WeatherRadarVoiceAdapter");
                }
            }
            
            // Traffic Radar Adapter
            if (FindObjectOfType<TrafficRadarVoiceAdapter>() == null)
            {
                var trafficController = FindObjectOfType<TrafficRadar.Core.TrafficRadarController>();
                if (trafficController != null)
                {
                    var adapter = root.AddComponent<TrafficRadarVoiceAdapter>();
                    Debug.Log("Created TrafficRadarVoiceAdapter");
                }
            }
            
            // Indicator System Adapter
            if (FindObjectOfType<IndicatorSystemVoiceAdapter>() == null)
            {
                var indicatorController = FindObjectOfType<IndicatorSystem.Controller.IndicatorSystemController>();
                if (indicatorController != null)
                {
                    var adapter = root.AddComponent<IndicatorSystemVoiceAdapter>();
                    Debug.Log("Created IndicatorSystemVoiceAdapter");
                }
            }
        }
        
        private void CreateVoiceUI(GameObject root, VoiceControlSettings settings)
        {
            // Find or create Canvas
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("VoiceControlCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
            }
            
            // Check if VoiceControlUI already exists
            if (FindObjectOfType<VoiceControlUI>() != null)
            {
                return;
            }
            
            // Create Voice Panel - LARGER AND MORE VISUALLY APPEALING
            GameObject panelObj = new GameObject("VoiceControlPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            SetAnchorForPosition(panelRect, _uiPosition);
            panelRect.sizeDelta = new Vector2(480, 280); // Larger panel
            
            // Background with better styling
            Image panelBg = panelObj.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.1f, 0.14f, 0.98f); // Darker, more opaque
            
            // Add border/outline effect via child
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(panelObj.transform, false);
            RectTransform borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2, -2);
            borderRect.offsetMax = new Vector2(2, 2);
            borderRect.SetAsFirstSibling();
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(_primaryColor.r, _primaryColor.g, _primaryColor.b, 0.4f);
            
            // CanvasGroup for animations
            CanvasGroup panelCG = panelObj.AddComponent<CanvasGroup>();
            
            // VoiceControlUI component
            VoiceControlUI voiceUI = panelObj.AddComponent<VoiceControlUI>();
            
            // Header with title
            GameObject headerObj = new GameObject("Header");
            headerObj.transform.SetParent(panelObj.transform, false);
            RectTransform headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0, 50);
            Image headerBg = headerObj.AddComponent<Image>();
            headerBg.color = new Color(0.05f, 0.07f, 0.1f, 1f);
            
            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(headerObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(20, 0);
            titleRect.offsetMax = new Vector2(-20, 0);
            TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "VOICE CONTROL";
            titleText.fontSize = 20;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.color = _primaryColor;
            
            // Status indicator (right side of header)
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(headerObj.transform, false);
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 0);
            statusRect.anchorMax = new Vector2(1, 1);
            statusRect.offsetMin = new Vector2(0, 0);
            statusRect.offsetMax = new Vector2(-20, 0);
            TMP_Text statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "● Ready";
            statusText.fontSize = 18;
            statusText.alignment = TextAlignmentOptions.Right;
            statusText.color = new Color(0.4f, 0.9f, 0.5f, 1f); // Green for ready
            
            // Main content area
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(panelObj.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0.25f);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.offsetMin = new Vector2(20, 0);
            contentRect.offsetMax = new Vector2(-20, -55);
            
            // Transcription Text - larger and more prominent
            GameObject transcriptObj = new GameObject("TranscriptionText");
            transcriptObj.transform.SetParent(contentObj.transform, false);
            RectTransform transcriptRect = transcriptObj.AddComponent<RectTransform>();
            transcriptRect.anchorMin = Vector2.zero;
            transcriptRect.anchorMax = Vector2.one;
            transcriptRect.offsetMin = Vector2.zero;
            transcriptRect.offsetMax = Vector2.zero;
            TMP_Text transcriptText = transcriptObj.AddComponent<TextMeshProUGUI>();
            transcriptText.text = "Press and hold the microphone button to speak...";
            transcriptText.fontSize = 22;
            transcriptText.alignment = TextAlignmentOptions.Center;
            transcriptText.color = new Color(0.85f, 0.85f, 0.9f, 1f);
            transcriptText.fontStyle = FontStyles.Italic;
            
            // Bottom bar with level indicator
            GameObject bottomBar = new GameObject("BottomBar");
            bottomBar.transform.SetParent(panelObj.transform, false);
            RectTransform bottomRect = bottomBar.AddComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0, 0);
            bottomRect.anchorMax = new Vector2(1, 0.25f);
            bottomRect.offsetMin = Vector2.zero;
            bottomRect.offsetMax = Vector2.zero;
            Image bottomBg = bottomBar.AddComponent<Image>();
            bottomBg.color = new Color(0.05f, 0.07f, 0.1f, 0.8f);
            
            // Voice Level Indicator - centered and larger
            GameObject levelObj = new GameObject("VoiceLevelIndicator");
            levelObj.transform.SetParent(bottomBar.transform, false);
            RectTransform levelRect = levelObj.AddComponent<RectTransform>();
            levelRect.anchorMin = new Vector2(0.5f, 0.5f);
            levelRect.anchorMax = new Vector2(0.5f, 0.5f);
            levelRect.pivot = new Vector2(0.5f, 0.5f);
            levelRect.anchoredPosition = Vector2.zero;
            levelRect.sizeDelta = new Vector2(200, 50);
            VoiceLevelIndicator levelIndicator = levelObj.AddComponent<VoiceLevelIndicator>();
            
            // Command Feedback - overlay on content
            GameObject feedbackObj = new GameObject("CommandFeedback");
            feedbackObj.transform.SetParent(contentObj.transform, false);
            RectTransform feedbackRect = feedbackObj.AddComponent<RectTransform>();
            feedbackRect.anchorMin = Vector2.zero;
            feedbackRect.anchorMax = Vector2.one;
            feedbackRect.offsetMin = Vector2.zero;
            feedbackRect.offsetMax = Vector2.zero;
            VoiceCommandFeedback feedback = feedbackObj.AddComponent<VoiceCommandFeedback>();
            CanvasGroup feedbackCG = feedbackObj.AddComponent<CanvasGroup>();
            feedbackCG.alpha = 0;
            
            GameObject feedbackTextObj = new GameObject("CommandText");
            feedbackTextObj.transform.SetParent(feedbackObj.transform, false);
            RectTransform fbTextRect = feedbackTextObj.AddComponent<RectTransform>();
            fbTextRect.anchorMin = Vector2.zero;
            fbTextRect.anchorMax = Vector2.one;
            fbTextRect.offsetMin = Vector2.zero;
            fbTextRect.offsetMax = Vector2.zero;
            TMP_Text fbText = feedbackTextObj.AddComponent<TextMeshProUGUI>();
            fbText.fontSize = 20;
            fbText.alignment = TextAlignmentOptions.Center;
            fbText.color = _primaryColor;
            
            // Set references via SerializedObject
            SerializedObject uiSO = new SerializedObject(voiceUI);
            uiSO.FindProperty("panelTransform").objectReferenceValue = panelRect;
            uiSO.FindProperty("canvasGroup").objectReferenceValue = panelCG;
            uiSO.FindProperty("backgroundImage").objectReferenceValue = panelBg;
            uiSO.FindProperty("statusText").objectReferenceValue = statusText;
            uiSO.FindProperty("transcriptionText").objectReferenceValue = transcriptText;
            uiSO.FindProperty("levelIndicator").objectReferenceValue = levelIndicator;
            uiSO.FindProperty("commandFeedback").objectReferenceValue = feedback;
            uiSO.FindProperty("settings").objectReferenceValue = settings;
            uiSO.ApplyModifiedProperties();
            
            // Set feedback text reference
            SerializedObject fbSO = new SerializedObject(feedback);
            fbSO.FindProperty("commandText").objectReferenceValue = fbText;
            fbSO.FindProperty("settings").objectReferenceValue = settings;
            fbSO.ApplyModifiedProperties();
            
            // Create Toggle Buttons
            CreateToggleButtons(canvas.transform, panelObj, settings);
            
            Undo.RegisterCreatedObjectUndo(panelObj, "Create Voice Panel");
        }
        
        private void CreateToggleButtons(Transform parent, GameObject targetPanel, VoiceControlSettings settings)
        {
            float buttonSize = 70f; // Larger buttons
            float buttonSpacing = 15f;
            
            // PTT Button (main button, more prominent)
            GameObject pttObj = CreateButtonObject("VoicePTTButton", parent, buttonSize);
            RectTransform pttRect = pttObj.GetComponent<RectTransform>();
            pttRect.anchorMin = new Vector2(1, 0);
            pttRect.anchorMax = new Vector2(1, 0);
            pttRect.pivot = new Vector2(1, 0);
            pttRect.anchoredPosition = new Vector2(-25, 25);
            
            // Make PTT button more visible with accent color border
            Image pttBg = pttObj.GetComponent<Image>();
            pttBg.color = new Color(0.12f, 0.14f, 0.2f, 0.95f);
            
            // Add glow effect via child
            GameObject pttGlow = new GameObject("Glow");
            pttGlow.transform.SetParent(pttObj.transform, false);
            RectTransform pttGlowRect = pttGlow.AddComponent<RectTransform>();
            pttGlowRect.anchorMin = Vector2.zero;
            pttGlowRect.anchorMax = Vector2.one;
            pttGlowRect.offsetMin = new Vector2(-4, -4);
            pttGlowRect.offsetMax = new Vector2(4, 4);
            pttGlowRect.SetAsFirstSibling();
            Image pttGlowImg = pttGlow.AddComponent<Image>();
            pttGlowImg.color = new Color(_primaryColor.r, _primaryColor.g, _primaryColor.b, 0.3f);
            
            VoiceToggleButton ptt = pttObj.AddComponent<VoiceToggleButton>();
            SerializedObject pttSO = new SerializedObject(ptt);
            pttSO.FindProperty("mode").enumValueIndex = 1; // PushToTalk
            pttSO.FindProperty("voiceUI").objectReferenceValue = targetPanel.GetComponent<VoiceControlUI>();
            pttSO.FindProperty("settings").objectReferenceValue = settings;
            pttSO.ApplyModifiedProperties();
            
            // UI Toggle Button (smaller, secondary)
            float smallBtnSize = 50f;
            GameObject uiToggleObj = CreateButtonObject("VoiceUIToggle", parent, smallBtnSize);
            RectTransform uiToggleRect = uiToggleObj.GetComponent<RectTransform>();
            uiToggleRect.anchorMin = new Vector2(1, 0);
            uiToggleRect.anchorMax = new Vector2(1, 0);
            uiToggleRect.pivot = new Vector2(1, 0);
            uiToggleRect.anchoredPosition = new Vector2(-25 - buttonSize - buttonSpacing, 35);
            
            VoiceToggleButton uiToggle = uiToggleObj.AddComponent<VoiceToggleButton>();
            SerializedObject uiToggleSO = new SerializedObject(uiToggle);
            uiToggleSO.FindProperty("mode").enumValueIndex = 0; // ToggleUI
            uiToggleSO.FindProperty("voiceUI").objectReferenceValue = targetPanel.GetComponent<VoiceControlUI>();
            uiToggleSO.FindProperty("settings").objectReferenceValue = settings;
            uiToggleSO.ApplyModifiedProperties();
            
            // Add icon labels with larger text
            AddButtonLabel(pttObj, "🎤", 32);
            AddButtonLabel(uiToggleObj, "☰", 24);
        }
        
        private GameObject CreateButtonObject(string name, Transform parent, float size)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size);
            
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            
            return btnObj;
        }
        
        private void AddButtonLabel(GameObject btnObj, string label, int fontSize = 24)
        {
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            
            RectTransform rect = labelObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            TMP_Text text = labelObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
        }
        
        private void SetAnchorForPosition(RectTransform rect, VoiceUIPosition position)
        {
            Vector2 anchor;
            Vector2 pivot;
            Vector2 offset;
            
            switch (position)
            {
                case VoiceUIPosition.TopLeft:
                    anchor = new Vector2(0, 1);
                    pivot = new Vector2(0, 1);
                    offset = new Vector2(20, -100);
                    break;
                case VoiceUIPosition.TopCenter:
                    anchor = new Vector2(0.5f, 1);
                    pivot = new Vector2(0.5f, 1);
                    offset = new Vector2(0, -100);
                    break;
                case VoiceUIPosition.TopRight:
                    anchor = new Vector2(1, 1);
                    pivot = new Vector2(1, 1);
                    offset = new Vector2(-20, -100);
                    break;
                case VoiceUIPosition.BottomLeft:
                    anchor = new Vector2(0, 0);
                    pivot = new Vector2(0, 0);
                    offset = new Vector2(20, 100);
                    break;
                case VoiceUIPosition.BottomCenter:
                    anchor = new Vector2(0.5f, 0);
                    pivot = new Vector2(0.5f, 0);
                    offset = new Vector2(0, 100);
                    break;
                case VoiceUIPosition.BottomRight:
                default:
                    anchor = new Vector2(1, 0);
                    pivot = new Vector2(1, 0);
                    offset = new Vector2(-20, 100);
                    break;
            }
            
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
        }
    }
}
#endif
