using UnityEngine;
using UnityEditor;

namespace WeatherRadar.Weather3D.Editor
{
    /// <summary>
    /// Custom inspector for Weather3DManager with debugging tools
    /// and a "Generate Test Weather" button for easy testing.
    /// </summary>
    [CustomEditor(typeof(Weather3DManager))]
    public class Weather3DManagerInspector : UnityEditor.Editor
    {
        private Weather3DManager manager;
        private bool showDebugFoldout = true;
        private bool showTestControls = true;
        
        // Test weather parameters
        private int testCellCount = 5;
        private float testIntensity = 0.5f;
        private float testRadius = 50f;

        private void OnEnable()
        {
            manager = (Weather3DManager)target;
        }

        public override void OnInspectorGUI()
        {
            // Draw default inspector
            DrawDefaultInspector();
            
            EditorGUILayout.Space(10);
            
            // Status Section
            DrawStatusSection();
            
            EditorGUILayout.Space(10);
            
            // Test Controls
            DrawTestControls();
            
            EditorGUILayout.Space(10);
            
            // Debug Section
            DrawDebugSection();
        }

        private void DrawStatusSection()
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Config status
                var configProp = serializedObject.FindProperty("config");
                bool hasConfig = configProp.objectReferenceValue != null;
                EditorGUILayout.LabelField("Config:", hasConfig ? "✅ Assigned" : "❌ Missing");
                
                // Weather provider status
                var providerProp = serializedObject.FindProperty("weatherProvider");
                bool hasProvider = providerProp.objectReferenceValue != null;
                EditorGUILayout.LabelField("Weather Provider:", hasProvider ? "✅ Connected" : "❌ Not connected");
                
                // Renderers status
                var cloudProp = serializedObject.FindProperty("cloudRenderer");
                var precipProp = serializedObject.FindProperty("precipitationSystem");
                var stormProp = serializedObject.FindProperty("thunderstormRenderer");
                var turbProp = serializedObject.FindProperty("turbulenceIndicator");
                
                int connectedRenderers = 0;
                if (cloudProp.objectReferenceValue != null) connectedRenderers++;
                if (precipProp.objectReferenceValue != null) connectedRenderers++;
                if (stormProp.objectReferenceValue != null) connectedRenderers++;
                if (turbProp.objectReferenceValue != null) connectedRenderers++;
                
                EditorGUILayout.LabelField("Renderers:", $"{connectedRenderers}/4 connected");
                
                // Current data
                if (Application.isPlaying && manager.CurrentData != null)
                {
                    EditorGUILayout.LabelField("Weather Cells:", manager.CurrentData.weatherCells.Count.ToString());
                    EditorGUILayout.LabelField("Cloud Layers:", manager.CurrentData.cloudLayers.Count.ToString());
                    EditorGUILayout.LabelField("Data Age:", $"{manager.CurrentData.dataAge:F1}s");
                }
                else if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField("Data:", "No weather data received yet");
                }
                
                // Auto-find button
                if (!hasConfig || !hasProvider || connectedRenderers < 4)
                {
                    EditorGUILayout.Space(5);
                    if (GUILayout.Button("Auto-Find & Connect Components"))
                    {
                        AutoConnectComponents();
                    }
                }
            }
        }

        private void DrawTestControls()
        {
            showTestControls = EditorGUILayout.Foldout(showTestControls, "Test Controls", true);
            
            if (showTestControls)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.HelpBox(
                        "Generate test weather to verify the system works without needing real radar data.",
                        MessageType.Info);
                    
                    testCellCount = EditorGUILayout.IntSlider("Cell Count", testCellCount, 1, 20);
                    testIntensity = EditorGUILayout.Slider("Intensity", testIntensity, 0.1f, 1f);
                    testRadius = EditorGUILayout.Slider("Radius", testRadius, 10f, 200f);
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                    if (GUILayout.Button("Generate Test Weather", GUILayout.Height(30)))
                    {
                        GenerateTestWeather();
                    }
                    GUI.backgroundColor = Color.white;
                    
                    GUI.backgroundColor = new Color(0.8f, 0.4f, 0.4f);
                    if (GUILayout.Button("Clear", GUILayout.Height(30), GUILayout.Width(60)))
                    {
                        ClearWeather();
                    }
                    GUI.backgroundColor = Color.white;
                    
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void DrawDebugSection()
        {
            showDebugFoldout = EditorGUILayout.Foldout(showDebugFoldout, "Debug", true);
            
            if (showDebugFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    if (GUILayout.Button("Log Component Status"))
                    {
                        LogComponentStatus();
                    }
                    
                    if (GUILayout.Button("Force Refresh Visualization"))
                    {
                        if (Application.isPlaying)
                        {
                            manager.RefreshVisualization();
                        }
                        else
                        {
                            Debug.LogWarning("Must be in Play mode to refresh visualization");
                        }
                    }
                    
                    if (GUILayout.Button("Open Setup Wizard"))
                    {
                        Weather3DSetupWizard.ShowWindow();
                    }
                }
            }
        }

        private void AutoConnectComponents()
        {
            Undo.RecordObject(manager, "Auto-Connect Weather3D Components");
            
            serializedObject.Update();
            
            // Find config
            var configProp = serializedObject.FindProperty("config");
            if (configProp.objectReferenceValue == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:Weather3DConfig");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    configProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Weather3DConfig>(path);
                    Debug.Log($"[Weather3D] Found and assigned config: {path}");
                }
                else
                {
                    Debug.LogWarning("[Weather3D] No Weather3DConfig found. Create one via Tools > Weather Radar > 3D Weather Setup Wizard");
                }
            }
            
            // Find weather provider
            var providerProp = serializedObject.FindProperty("weatherProvider");
            if (providerProp.objectReferenceValue == null)
            {
                var provider = FindObjectOfType<WeatherRadarProviderBase>();
                if (provider != null)
                {
                    providerProp.objectReferenceValue = provider;
                    Debug.Log($"[Weather3D] Found and assigned provider: {provider.ProviderName}");
                }
            }
            
            // Find renderers in children
            var cloudProp = serializedObject.FindProperty("cloudRenderer");
            if (cloudProp.objectReferenceValue == null)
            {
                cloudProp.objectReferenceValue = manager.GetComponentInChildren<VolumetricCloudRenderer>();
            }
            
            var precipProp = serializedObject.FindProperty("precipitationSystem");
            if (precipProp.objectReferenceValue == null)
            {
                precipProp.objectReferenceValue = manager.GetComponentInChildren<PrecipitationSystem>();
            }
            
            var stormProp = serializedObject.FindProperty("thunderstormRenderer");
            if (stormProp.objectReferenceValue == null)
            {
                stormProp.objectReferenceValue = manager.GetComponentInChildren<ThunderstormCellRenderer>();
            }
            
            var turbProp = serializedObject.FindProperty("turbulenceIndicator");
            if (turbProp.objectReferenceValue == null)
            {
                turbProp.objectReferenceValue = manager.GetComponentInChildren<TurbulenceIndicator>();
            }
            
            serializedObject.ApplyModifiedProperties();
            
            Debug.Log("[Weather3D] Auto-connect complete!");
        }

        private void GenerateTestWeather()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Weather3D] Must be in Play mode to generate test weather");
                return;
            }
            
            // Create test weather data
            var config = manager.Config;
            if (config == null)
            {
                Debug.LogError("[Weather3D] No config assigned! Use Auto-Connect first.");
                return;
            }
            
            var testData = new Weather3DData
            {
                gridSize = config.gridResolution,
                coverageNM = 80f,
                maxAltitudeFt = config.maxAltitudeFt,
                aircraftPosition = manager.transform.position, // Use manager's position
                aircraftAltitude = 10000f,
                aircraftHeading = 0f,
                lastUpdateTime = Time.time
            };
            
            testData.InitializeGrid();
            
            // Generate random weather cells around the manager's position
            for (int i = 0; i < testCellCount; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float distance = Random.Range(10f, testRadius);
                
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * distance,
                    Random.Range(5f, 30f), // Small vertical offset
                    Mathf.Sin(angle) * distance
                );
                
                float cellIntensity = testIntensity * Random.Range(0.5f, 1.5f);
                cellIntensity = Mathf.Clamp01(cellIntensity);
                
                var cell = new WeatherCell3D
                {
                    position = manager.transform.position + offset,
                    size = new Vector3(
                        Random.Range(5f, 20f),
                        Random.Range(10f, 40f),
                        Random.Range(5f, 20f)
                    ),
                    intensity = cellIntensity,
                    cellType = cellIntensity > 0.6f ? WeatherCellType.Thunderstorm : 
                              cellIntensity > 0.4f ? WeatherCellType.HeavyRain :
                              cellIntensity > 0.2f ? WeatherCellType.ModerateRain : WeatherCellType.LightRain,
                    altitude = 2000f,
                    topAltitude = 35000f,
                    hasLightning = cellIntensity > 0.5f,
                    turbulenceLevel = cellIntensity * 0.8f
                };
                
                testData.weatherCells.Add(cell);
            }
            
            // Add a cloud layer
            testData.cloudLayers.Add(new CloudLayer
            {
                baseAltitude = 5000f,
                topAltitude = 25000f,
                coverage = 0.6f,
                layerType = CloudLayerType.Cumulus,
                tintColor = Color.white
            });
            
            // Manually trigger update on renderers
            var cloudRenderer = manager.GetComponentInChildren<VolumetricCloudRenderer>();
            var precipSystem = manager.GetComponentInChildren<PrecipitationSystem>();
            var stormRenderer = manager.GetComponentInChildren<ThunderstormCellRenderer>();
            var turbIndicator = manager.GetComponentInChildren<TurbulenceIndicator>();
            
            if (cloudRenderer != null)
            {
                cloudRenderer.Initialize(config, manager);
                cloudRenderer.UpdateClouds(testData);
            }
            
            if (precipSystem != null)
            {
                precipSystem.Initialize(config, manager);
                precipSystem.UpdatePrecipitation(testData);
            }
            
            if (stormRenderer != null)
            {
                stormRenderer.Initialize(config, manager);
                stormRenderer.UpdateThunderstorms(testData);
            }
            
            if (turbIndicator != null)
            {
                turbIndicator.Initialize(config, manager);
                turbIndicator.UpdateTurbulence(testData);
            }
            
            Debug.Log($"[Weather3D] Generated {testCellCount} test weather cells around {manager.transform.position}");
        }

        private void ClearWeather()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Weather3D] Must be in Play mode to clear weather");
                return;
            }
            
            manager.ClearVisualization();
            Debug.Log("[Weather3D] Cleared all weather visualization");
        }

        private void LogComponentStatus()
        {
            Debug.Log("=== Weather3D Component Status ===");
            
            var configProp = serializedObject.FindProperty("config");
            Debug.Log($"Config: {(configProp.objectReferenceValue != null ? configProp.objectReferenceValue.name : "MISSING")}");
            
            var providerProp = serializedObject.FindProperty("weatherProvider");
            var provider = providerProp.objectReferenceValue as WeatherRadarProviderBase;
            Debug.Log($"Weather Provider: {(provider != null ? provider.ProviderName : "MISSING")}");
            
            var cloud = manager.GetComponentInChildren<VolumetricCloudRenderer>();
            Debug.Log($"Cloud Renderer: {(cloud != null ? "✓ Found" : "✗ Missing")}");
            
            var precip = manager.GetComponentInChildren<PrecipitationSystem>();
            Debug.Log($"Precipitation System: {(precip != null ? "✓ Found" : "✗ Missing")}");
            
            var storm = manager.GetComponentInChildren<ThunderstormCellRenderer>();
            Debug.Log($"Thunderstorm Renderer: {(storm != null ? "✓ Found" : "✗ Missing")}");
            
            var turb = manager.GetComponentInChildren<TurbulenceIndicator>();
            Debug.Log($"Turbulence Indicator: {(turb != null ? "✓ Found" : "✗ Missing")}");
            
            Debug.Log("===================================");
        }
    }
}
