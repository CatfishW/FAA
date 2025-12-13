using UnityEngine;
using UnityEditor;
using System.IO;

namespace WeatherRadar.Weather3D.Editor
{
    /// <summary>
    /// Setup wizard for the 3D Weather Information Display System.
    /// Provides a one-click setup for the complete system.
    /// </summary>
    public class Weather3DSetupWizard : EditorWindow
    {
        // Setup options
        private bool createConfig = true;
        private bool createManager = true;
        private bool createRenderers = true;
        private bool createUI = true;
        private bool autoLinkRadarProvider = true;
        
        // References
        private WeatherRadarProviderBase existingRadarProvider;
        private Transform parentTransform;
        
        // Scroll position
        private Vector2 scrollPos;
        
        // Styles
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private GUIStyle successStyle;
        
        // Status
        private string statusMessage = "";
        private MessageType statusType = MessageType.None;

        [MenuItem("Tools/Weather Radar/3D Weather Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<Weather3DSetupWizard>("3D Weather Setup");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        private void OnEnable()
        {
            // Find existing radar provider
            existingRadarProvider = FindObjectOfType<WeatherRadarProviderBase>();
        }

        private void InitStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter,
                    margin = new RectOffset(0, 0, 10, 10)
                };
            }
            
            if (sectionStyle == null)
            {
                sectionStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 10, 10),
                    margin = new RectOffset(5, 5, 5, 5)
                };
            }
            
            if (successStyle == null)
            {
                successStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = Color.green },
                    fontStyle = FontStyle.Bold
                };
            }
        }

        private void OnGUI()
        {
            InitStyles();
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            // Header
            GUILayout.Space(10);
            EditorGUILayout.LabelField("🌩️ 3D Weather System Setup Wizard", headerStyle);
            GUILayout.Space(10);
            
            // Description
            EditorGUILayout.HelpBox(
                "This wizard will set up the complete 3D Weather Information Display System.\n\n" +
                "It will create all necessary GameObjects, components, and a configuration asset.",
                MessageType.Info);
            
            GUILayout.Space(10);
            
            // Existing Radar Provider Section
            DrawRadarProviderSection();
            
            GUILayout.Space(10);
            
            // Setup Options Section
            DrawSetupOptionsSection();
            
            GUILayout.Space(10);
            
            // Parent Transform Section
            DrawParentSection();
            
            GUILayout.Space(20);
            
            // Setup Button
            DrawSetupButton();
            
            // Status Message
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }
            
            GUILayout.Space(20);
            
            // Quick Actions
            DrawQuickActions();
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawRadarProviderSection()
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.LabelField("📡 Existing 2D Radar Provider", EditorStyles.boldLabel);
            
            GUILayout.Space(5);
            
            if (existingRadarProvider != null)
            {
                EditorGUILayout.LabelField("Found:", existingRadarProvider.ProviderName, successStyle);
                autoLinkRadarProvider = EditorGUILayout.Toggle("Auto-link to 3D System", autoLinkRadarProvider);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No existing WeatherRadarProviderBase found in scene.\n" +
                    "The 3D system will work but won't have data until a provider is set.",
                    MessageType.Warning);
                
                if (GUILayout.Button("Refresh Search"))
                {
                    existingRadarProvider = FindObjectOfType<WeatherRadarProviderBase>();
                }
            }
            
            existingRadarProvider = (WeatherRadarProviderBase)EditorGUILayout.ObjectField(
                "Override Provider",
                existingRadarProvider,
                typeof(WeatherRadarProviderBase),
                true);
            
            EditorGUILayout.EndVertical();
        }

        private void DrawSetupOptionsSection()
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.LabelField("⚙️ Setup Options", EditorStyles.boldLabel);
            
            GUILayout.Space(5);
            
            createConfig = EditorGUILayout.Toggle(
                new GUIContent("Create Config Asset", "Creates a Weather3DConfig ScriptableObject"),
                createConfig);
            
            createManager = EditorGUILayout.Toggle(
                new GUIContent("Create Manager", "Creates the Weather3DManager and Bridge"),
                createManager);
            
            createRenderers = EditorGUILayout.Toggle(
                new GUIContent("Create All Renderers", "Creates Cloud, Precipitation, Thunderstorm, and Turbulence renderers"),
                createRenderers);
            
            createUI = EditorGUILayout.Toggle(
                new GUIContent("Create Control Panel", "Creates a basic UI control panel"),
                createUI);
            
            EditorGUILayout.EndVertical();
        }

        private void DrawParentSection()
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.LabelField("📍 Parent Transform", EditorStyles.boldLabel);
            
            GUILayout.Space(5);
            
            parentTransform = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Parent", "Optional parent transform for the system"),
                parentTransform,
                typeof(Transform),
                true);
            
            EditorGUILayout.HelpBox(
                "Leave empty to create at scene root, or select a parent object.",
                MessageType.None);
            
            EditorGUILayout.EndVertical();
        }

        private void DrawSetupButton()
        {
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            
            if (GUILayout.Button("🚀 Setup Complete 3D Weather System", GUILayout.Height(40)))
            {
                PerformSetup();
            }
            
            GUI.backgroundColor = Color.white;
        }

        private void DrawQuickActions()
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.LabelField("⚡ Quick Actions", EditorStyles.boldLabel);
            
            GUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Select Manager"))
            {
                var manager = FindObjectOfType<Weather3DManager>();
                if (manager != null)
                {
                    Selection.activeGameObject = manager.gameObject;
                }
                else
                {
                    ShowNotification(new GUIContent("No Weather3DManager found"));
                }
            }
            
            if (GUILayout.Button("Create Config Only"))
            {
                CreateConfigAsset();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Find All Components"))
            {
                FindAllComponents();
            }
            
            if (GUILayout.Button("Cleanup System"))
            {
                if (EditorUtility.DisplayDialog("Cleanup 3D Weather System",
                    "This will remove all Weather3D components from the scene. Are you sure?",
                    "Yes, Cleanup", "Cancel"))
                {
                    CleanupSystem();
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        private void PerformSetup()
        {
            statusMessage = "";
            statusType = MessageType.Info;
            
            try
            {
                Undo.SetCurrentGroupName("Setup 3D Weather System");
                int undoGroup = Undo.GetCurrentGroup();
                
                Weather3DConfig config = null;
                
                // Step 1: Create config asset
                if (createConfig)
                {
                    config = CreateConfigAsset();
                }
                else
                {
                    config = FindExistingConfig();
                }
                
                // Step 2: Create main system GameObject
                GameObject systemRoot = null;
                Weather3DManager manager = null;
                Weather3DRadarBridge bridge = null;
                
                if (createManager)
                {
                    systemRoot = new GameObject("Weather3D_System");
                    Undo.RegisterCreatedObjectUndo(systemRoot, "Create Weather3D System");
                    
                    if (parentTransform != null)
                    {
                        systemRoot.transform.SetParent(parentTransform);
                    }
                    
                    manager = systemRoot.AddComponent<Weather3DManager>();
                    bridge = systemRoot.AddComponent<Weather3DRadarBridge>();
                    
                    // Assign config via serialized property
                    if (config != null)
                    {
                        var so = new SerializedObject(manager);
                        var configProp = so.FindProperty("config");
                        if (configProp != null)
                        {
                            configProp.objectReferenceValue = config;
                            so.ApplyModifiedProperties();
                        }
                    }
                    
                    // Link radar provider
                    if (autoLinkRadarProvider && existingRadarProvider != null)
                    {
                        var so = new SerializedObject(bridge);
                        var providerProp = so.FindProperty("radarProvider");
                        if (providerProp != null)
                        {
                            providerProp.objectReferenceValue = existingRadarProvider;
                            so.ApplyModifiedProperties();
                        }
                    }
                }
                
                // Step 3: Create renderers
                if (createRenderers && systemRoot != null)
                {
                    CreateRenderers(systemRoot, manager, config);
                }
                
                // Step 4: Create UI
                if (createUI && systemRoot != null)
                {
                    CreateUIPanel(systemRoot, manager);
                }
                
                Undo.CollapseUndoOperations(undoGroup);
                
                // Select the created system
                if (systemRoot != null)
                {
                    Selection.activeGameObject = systemRoot;
                }
                
                statusMessage = "✅ 3D Weather System setup complete!\n\n" +
                               "Created:\n" +
                               (createConfig ? "• Weather3DConfig asset\n" : "") +
                               (createManager ? "• Weather3DManager\n• Weather3DRadarBridge\n" : "") +
                               (createRenderers ? "• VolumetricCloudRenderer\n• PrecipitationSystem\n• ThunderstormCellRenderer\n• TurbulenceIndicator\n" : "") +
                               (createUI ? "• Weather3DControlPanel\n" : "");
                statusType = MessageType.Info;
                
                Debug.Log("[Weather3DSetupWizard] Setup complete!");
            }
            catch (System.Exception e)
            {
                statusMessage = $"❌ Setup failed: {e.Message}";
                statusType = MessageType.Error;
                Debug.LogError($"[Weather3DSetupWizard] Setup failed: {e}");
            }
        }

        private Weather3DConfig CreateConfigAsset()
        {
            // Check if config already exists
            string[] guids = AssetDatabase.FindAssets("t:Weather3DConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var existing = AssetDatabase.LoadAssetAtPath<Weather3DConfig>(path);
                if (existing != null)
                {
                    Debug.Log($"[Weather3DSetupWizard] Using existing config: {path}");
                    return existing;
                }
            }
            
            // Create new config
            var config = ScriptableObject.CreateInstance<Weather3DConfig>();
            
            string folderPath = "Assets/_Project/Data";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }
            
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/Weather3DConfig.asset");
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[Weather3DSetupWizard] Created config asset: {assetPath}");
            
            return config;
        }

        private Weather3DConfig FindExistingConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:Weather3DConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<Weather3DConfig>(path);
            }
            return null;
        }

        private void CreateRenderers(GameObject root, Weather3DManager manager, Weather3DConfig config)
        {
            // Create Cloud Renderer
            GameObject cloudObj = new GameObject("CloudRenderer");
            cloudObj.transform.SetParent(root.transform);
            cloudObj.transform.localPosition = Vector3.zero;
            var cloudRenderer = cloudObj.AddComponent<VolumetricCloudRenderer>();
            Undo.RegisterCreatedObjectUndo(cloudObj, "Create Cloud Renderer");
            
            // Create Precipitation System
            GameObject precipObj = new GameObject("PrecipitationRenderer");
            precipObj.transform.SetParent(root.transform);
            precipObj.transform.localPosition = Vector3.zero;
            var precipSystem = precipObj.AddComponent<PrecipitationSystem>();
            Undo.RegisterCreatedObjectUndo(precipObj, "Create Precipitation System");
            
            // Create Thunderstorm Renderer
            GameObject stormObj = new GameObject("ThunderstormRenderer");
            stormObj.transform.SetParent(root.transform);
            stormObj.transform.localPosition = Vector3.zero;
            var stormRenderer = stormObj.AddComponent<ThunderstormCellRenderer>();
            Undo.RegisterCreatedObjectUndo(stormObj, "Create Thunderstorm Renderer");
            
            // Create Turbulence Indicator
            GameObject turbObj = new GameObject("TurbulenceRenderer");
            turbObj.transform.SetParent(root.transform);
            turbObj.transform.localPosition = Vector3.zero;
            var turbIndicator = turbObj.AddComponent<TurbulenceIndicator>();
            Undo.RegisterCreatedObjectUndo(turbObj, "Create Turbulence Indicator");
            
            // Link to manager via serialized properties
            if (manager != null)
            {
                var so = new SerializedObject(manager);
                
                var cloudProp = so.FindProperty("cloudRenderer");
                if (cloudProp != null) cloudProp.objectReferenceValue = cloudRenderer;
                
                var precipProp = so.FindProperty("precipitationSystem");
                if (precipProp != null) precipProp.objectReferenceValue = precipSystem;
                
                var stormProp = so.FindProperty("thunderstormRenderer");
                if (stormProp != null) stormProp.objectReferenceValue = stormRenderer;
                
                var turbProp = so.FindProperty("turbulenceIndicator");
                if (turbProp != null) turbProp.objectReferenceValue = turbIndicator;
                
                so.ApplyModifiedProperties();
            }
        }

        private void CreateUIPanel(GameObject root, Weather3DManager manager)
        {
            // Create UI container
            GameObject uiObj = new GameObject("Weather3D_UI");
            uiObj.transform.SetParent(root.transform);
            uiObj.transform.localPosition = Vector3.zero;
            
            var controlPanel = uiObj.AddComponent<Weather3DControlPanel>();
            Undo.RegisterCreatedObjectUndo(uiObj, "Create Control Panel");
            
            // Link to manager
            if (manager != null)
            {
                var so = new SerializedObject(controlPanel);
                var managerProp = so.FindProperty("weather3DManager");
                if (managerProp != null)
                {
                    managerProp.objectReferenceValue = manager;
                    so.ApplyModifiedProperties();
                }
            }
        }

        private void FindAllComponents()
        {
            var manager = FindObjectOfType<Weather3DManager>();
            var bridge = FindObjectOfType<Weather3DRadarBridge>();
            var cloudRenderer = FindObjectOfType<VolumetricCloudRenderer>();
            var precipSystem = FindObjectOfType<PrecipitationSystem>();
            var stormRenderer = FindObjectOfType<ThunderstormCellRenderer>();
            var turbIndicator = FindObjectOfType<TurbulenceIndicator>();
            var controlPanel = FindObjectOfType<Weather3DControlPanel>();
            
            string report = "3D Weather Components Found:\n\n";
            report += $"• Weather3DManager: {(manager != null ? "✅" : "❌")}\n";
            report += $"• Weather3DRadarBridge: {(bridge != null ? "✅" : "❌")}\n";
            report += $"• VolumetricCloudRenderer: {(cloudRenderer != null ? "✅" : "❌")}\n";
            report += $"• PrecipitationSystem: {(precipSystem != null ? "✅" : "❌")}\n";
            report += $"• ThunderstormCellRenderer: {(stormRenderer != null ? "✅" : "❌")}\n";
            report += $"• TurbulenceIndicator: {(turbIndicator != null ? "✅" : "❌")}\n";
            report += $"• Weather3DControlPanel: {(controlPanel != null ? "✅" : "❌")}\n";
            
            statusMessage = report;
            statusType = MessageType.Info;
        }

        private void CleanupSystem()
        {
            // Find and destroy all Weather3D components
            var managers = FindObjectsOfType<Weather3DManager>();
            var bridges = FindObjectsOfType<Weather3DRadarBridge>();
            var clouds = FindObjectsOfType<VolumetricCloudRenderer>();
            var precips = FindObjectsOfType<PrecipitationSystem>();
            var storms = FindObjectsOfType<ThunderstormCellRenderer>();
            var turbs = FindObjectsOfType<TurbulenceIndicator>();
            var panels = FindObjectsOfType<Weather3DControlPanel>();
            
            int count = 0;
            
            foreach (var m in managers) { Undo.DestroyObjectImmediate(m.gameObject); count++; }
            foreach (var b in bridges) { if (b != null && b.gameObject != null) Undo.DestroyObjectImmediate(b.gameObject); count++; }
            foreach (var c in clouds) { if (c != null && c.gameObject != null) Undo.DestroyObjectImmediate(c.gameObject); count++; }
            foreach (var p in precips) { if (p != null && p.gameObject != null) Undo.DestroyObjectImmediate(p.gameObject); count++; }
            foreach (var s in storms) { if (s != null && s.gameObject != null) Undo.DestroyObjectImmediate(s.gameObject); count++; }
            foreach (var t in turbs) { if (t != null && t.gameObject != null) Undo.DestroyObjectImmediate(t.gameObject); count++; }
            foreach (var p in panels) { if (p != null && p.gameObject != null) Undo.DestroyObjectImmediate(p.gameObject); count++; }
            
            statusMessage = $"Cleanup complete. Removed {count} Weather3D objects.";
            statusType = MessageType.Info;
            
            Debug.Log($"[Weather3DSetupWizard] Cleanup complete. Removed {count} objects.");
        }
    }
}
