using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace WeatherVisualization3D.Editor
{
    /// <summary>
    /// Easy-to-use editor window for setting up and previewing volumetric clouds
    /// without entering play mode.
    /// </summary>
    public class VolumetricCloudSetupWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private bool showSetupSection = true;
        private bool showPreviewSection = true;
        private bool showSettingsSection = true;

        // Status tracking
        private bool noiseTexturesExist = false;
        private bool cloudMaterialExists = false;
        private bool cloudVolumeExists = false;
        private string statusMessage = "";
        private MessageType statusType = MessageType.None;

        // Preview settings
        private bool autoRefreshPreview = true;
        private float previewUpdateInterval = 0.1f;
        private double lastPreviewUpdate;

        // Selected cloud volume
        private VolumetricCloudVolume selectedCloudVolume;
        private Material cloudMaterial;
        private Texture3D worleyNoise128;
        private Texture3D erosionNoise32;
        private Texture3D perlinNoise32;

        // Quick settings
        private float cloudDensity = 0.5f;
        private float shapeScale = 3f;
        private float erosionScale = 50f;
        private int raymarchSteps = 48;
        private float windSpeed = 10f;
        private bool showBounds = true;

        [MenuItem("Weather/Volumetric Clouds Setup", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<VolumetricCloudSetupWindow>("Cloud Setup");
            window.minSize = new Vector2(350, 500);
            window.Show();
        }

        [MenuItem("GameObject/Weather/Volumetric Cloud Volume", priority = 10)]
        public static void CreateCloudVolumeMenu()
        {
            ShowWindow();
            var window = GetWindow<VolumetricCloudSetupWindow>();
            window.OneClickSetup();
        }

        private void OnEnable()
        {
            CheckExistingAssets();
            FindSelectedCloudVolume();
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnEditorUpdate()
        {
            if (autoRefreshPreview && selectedCloudVolume != null)
            {
                if (EditorApplication.timeSinceStartup - lastPreviewUpdate > previewUpdateInterval)
                {
                    lastPreviewUpdate = EditorApplication.timeSinceStartup;
                    EditorApplication.QueuePlayerLoopUpdate();
                    SceneView.RepaintAll();
                }
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // Header
            EditorGUILayout.Space(10);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.largeLabel);
            headerStyle.fontSize = 18;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            EditorGUILayout.LabelField("☁ Volumetric Clouds Setup", headerStyle);
            EditorGUILayout.Space(5);

            // Render Pipeline Indicator
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Render Pipeline:", EditorStyles.boldLabel, GUILayout.Width(110));
            if (IsUsingSRP())
            {
                GUI.color = new Color(0.4f, 0.8f, 1f);
                EditorGUILayout.LabelField("SRP (URP/HDRP) Detected", EditorStyles.boldLabel);
            }
            else
            {
                GUI.color = new Color(1f, 0.8f, 0.4f);
                EditorGUILayout.LabelField("Built-in Render Pipeline", EditorStyles.boldLabel);
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Status message
            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
                EditorGUILayout.Space(10);
            }

            // ===== SETUP SECTION =====
            showSetupSection = EditorGUILayout.BeginFoldoutHeaderGroup(showSetupSection, "📦 Setup", EditorStyles.foldoutHeader);
            if (showSetupSection)
            {
                EditorGUILayout.Space(5);

                // Step 1: Generate Noise Textures
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Step 1: Generate 3D Noise Textures", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                GUI.color = noiseTexturesExist ? Color.green : Color.white;
                if (GUILayout.Button(noiseTexturesExist ? "✓ Noise Textures Generated" : "Generate Noise Textures", GUILayout.Height(30)))
                {
                    GenerateNoiseTextures();
                }
                GUI.color = Color.white;

                if (noiseTexturesExist && GUILayout.Button("Regenerate", GUILayout.Width(80), GUILayout.Height(30)))
                {
                    GenerateNoiseTextures();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox("Creates 3D Worley and Perlin noise textures for realistic cloud shapes.", MessageType.Info);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(10);

                // Step 2: Create Material
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Step 2: Create Cloud Material", EditorStyles.boldLabel);

                GUI.color = cloudMaterialExists ? Color.green : Color.white;
                if (GUILayout.Button(cloudMaterialExists ? "✓ Material Created" : "Create Cloud Material", GUILayout.Height(30)))
                {
                    CreateCloudMaterial();
                }
                GUI.color = Color.white;

                EditorGUILayout.HelpBox("Creates a material using the WeatherCloudVolume shader with 3D textures assigned.", MessageType.Info);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(10);

                // Step 3: Create Cloud Volume
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Step 3: Create Cloud Volume", EditorStyles.boldLabel);

                GUI.color = cloudVolumeExists ? Color.green : Color.white;
                if (GUILayout.Button(cloudVolumeExists ? "✓ Cloud Volume Created" : "Create Cloud Volume in Scene", GUILayout.Height(30)))
                {
                    CreateCloudVolume();
                }
                GUI.color = Color.white;

                if (cloudVolumeExists && GUILayout.Button("Select Existing Cloud Volume"))
                {
                    SelectExistingCloudVolume();
                }

                EditorGUILayout.HelpBox("Creates a GameObject with VolumetricCloudVolume component ready to render.", MessageType.Info);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);

                // One-click setup
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Quick Setup", EditorStyles.boldLabel);
                if (GUILayout.Button("🚀 One-Click Full Setup", GUILayout.Height(35)))
                {
                    OneClickSetup();
                }
                EditorGUILayout.HelpBox("Runs all setup steps automatically.", MessageType.Info);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(10);

            // ===== PREVIEW SECTION =====
            showPreviewSection = EditorGUILayout.BeginFoldoutHeaderGroup(showPreviewSection, "👁 Scene View Preview", EditorStyles.foldoutHeader);
            if (showPreviewSection)
            {
                EditorGUILayout.Space(5);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Auto refresh toggle
                autoRefreshPreview = EditorGUILayout.Toggle("Auto Refresh Preview", autoRefreshPreview);
                if (autoRefreshPreview)
                {
                    previewUpdateInterval = EditorGUILayout.Slider("Update Interval", previewUpdateInterval, 0.05f, 1f);
                }

                EditorGUILayout.Space(10);

                // Selected cloud volume info
                EditorGUILayout.LabelField("Selected Cloud Volume:", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                selectedCloudVolume = EditorGUILayout.ObjectField(selectedCloudVolume, typeof(VolumetricCloudVolume), true) as VolumetricCloudVolume;
                if (GUILayout.Button("Find in Scene", GUILayout.Width(100)))
                {
                    FindAndSelectCloudVolume();
                }
                EditorGUILayout.EndHorizontal();

                if (selectedCloudVolume == null)
                {
                    EditorGUILayout.HelpBox("No cloud volume selected. Create one or select an existing GameObject with VolumetricCloudVolume component.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.Space(10);

                    // Quick adjustments
                    EditorGUILayout.LabelField("Quick Adjustments:", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();

                    cloudDensity = EditorGUILayout.Slider("Cloud Density", cloudDensity, 0f, 2f);
                    shapeScale = EditorGUILayout.Slider("Shape Scale", shapeScale, 0.1f, 20f);
                    erosionScale = EditorGUILayout.Slider("Erosion Scale", erosionScale, 1f, 200f);
                    raymarchSteps = EditorGUILayout.IntSlider("Raymarch Steps", raymarchSteps, 16, 96);
                    windSpeed = EditorGUILayout.Slider("Wind Speed", windSpeed, 0f, 100f);
                    showBounds = EditorGUILayout.Toggle("Show Volume Bounds", showBounds);

                    if (EditorGUI.EndChangeCheck())
                    {
                        ApplyQuickSettings();
                    }

                    EditorGUILayout.Space(10);

                    // Force update button
                    if (GUILayout.Button("Force Refresh Preview"))
                    {
                        ForceRefreshPreview();
                    }

                    EditorGUILayout.Space(5);

                    // Open in inspector
                    if (GUILayout.Button("Open in Inspector"))
                    {
                        Selection.activeObject = selectedCloudVolume;
                    }
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(10);

            // ===== SETTINGS SECTION =====
            showSettingsSection = EditorGUILayout.BeginFoldoutHeaderGroup(showSettingsSection, "⚙ Advanced Settings", EditorStyles.foldoutHeader);
            if (showSettingsSection)
            {
                EditorGUILayout.Space(5);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField("Noise Texture Resolution:", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Higher resolution = better quality but slower generation", MessageType.Info);

                EditorGUILayout.Space(10);

                if (GUILayout.Button("Open Noise Texture Generator"))
                {
                    OpenNoiseTextureGenerator();
                }

                EditorGUILayout.Space(10);

                if (GUILayout.Button("Import External Noise Textures"))
                {
                    ImportExternalNoiseTextures();
                }

                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(10);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField("Troubleshooting:", EditorStyles.boldLabel);

                if (GUILayout.Button("Force Reinitialize Material"))
                {
                    ForceReinitializeMaterial();
                }

                if (GUILayout.Button("Clear All Cloud Data"))
                {
                    ClearAllCloudData();
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(20);

            // Footer
            EditorGUILayout.LabelField("Volumetric Clouds for URP", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField("Based on Unity HDRP Volumetric Clouds", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndScrollView();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (showBounds && selectedCloudVolume != null)
            {
                DrawCloudVolumeBounds(selectedCloudVolume);
            }
        }

        private void DrawCloudVolumeBounds(VolumetricCloudVolume volume)
        {
            if (volume == null) return;

            Vector3 center = volume.transform.position;
            Vector3 size = volume.GetType().GetField("_volumeSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(volume) as Vector3?
                ?? new Vector3(50000f, 15000f, 50000f);

            // Draw wireframe bounds
            Handles.color = new Color(0.3f, 0.7f, 1f, 0.5f);
            Handles.DrawWireCube(center, size);

            // Draw label
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = new Color(0.3f, 0.7f, 1f);
            style.fontSize = 12;
            Handles.Label(center + Vector3.up * size.y * 0.55f,
                $"☁ Cloud Volume\n{size.x / 1000:F0}km × {size.y / 1000:F0}km × {size.z / 1000:F0}km", style);
        }

        #region Setup Methods

        private void CheckExistingAssets()
        {
            // Check for noise textures
            worleyNoise128 = AssetDatabase.LoadAssetAtPath<Texture3D>(
                "Assets/_Project/Textures/CloudNoise/WorleyNoise128_Generated.asset");
            erosionNoise32 = AssetDatabase.LoadAssetAtPath<Texture3D>(
                "Assets/_Project/Textures/CloudNoise/ErosionNoise32_Generated.asset");
            perlinNoise32 = AssetDatabase.LoadAssetAtPath<Texture3D>(
                "Assets/_Project/Textures/CloudNoise/PerlinNoise32_Generated.asset");

            noiseTexturesExist = (worleyNoise128 != null && erosionNoise32 != null && perlinNoise32 != null);

            // Check for material
            cloudMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/VolumetricCloudWeatherMaterial.mat");
            cloudMaterialExists = (cloudMaterial != null);

            // Check for cloud volume in scene
            cloudVolumeExists = (FindObjectOfType<VolumetricCloudVolume>() != null);
        }

        private void FindSelectedCloudVolume()
        {
            if (Selection.activeGameObject != null)
            {
                selectedCloudVolume = Selection.activeGameObject.GetComponent<VolumetricCloudVolume>();
            }

            if (selectedCloudVolume == null)
            {
                selectedCloudVolume = FindObjectOfType<VolumetricCloudVolume>();
            }
        }

        private void GenerateNoiseTextures()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Generating Noise Textures", "Creating 3D Worley noise...", 0f);

                // Create generator
                var generatorGO = new GameObject("TempNoiseGenerator");
                var generator = generatorGO.AddComponent<CloudNoiseTextureGenerator>();
                generator.textureSize = 64;
                generator.autoGenerateOnStart = false;

                // Generate textures
                generator.GenerateAllTextures();

                // Save to disk
                string path = "Assets/_Project/Textures/CloudNoise/Generated/";
                System.IO.Directory.CreateDirectory(path);

                if (generator.worleyNoise128 != null)
                {
                    AssetDatabase.CreateAsset(generator.worleyNoise128, path + "WorleyNoise128_Generated.asset");
                    worleyNoise128 = generator.worleyNoise128;
                }
                if (generator.erosionNoise32 != null)
                {
                    AssetDatabase.CreateAsset(generator.erosionNoise32, path + "ErosionNoise32_Generated.asset");
                    erosionNoise32 = generator.erosionNoise32;
                }
                if (generator.perlinNoise32 != null)
                {
                    AssetDatabase.CreateAsset(generator.perlinNoise32, path + "PerlinNoise32_Generated.asset");
                    perlinNoise32 = generator.perlinNoise32;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Cleanup
                DestroyImmediate(generatorGO);

                noiseTexturesExist = true;
                statusMessage = "✓ Noise textures generated successfully!";
                statusType = MessageType.Info;

                EditorUtility.ClearProgressBar();
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                statusMessage = $"✗ Failed to generate noise textures: {e.Message}";
                statusType = MessageType.Error;
                Debug.LogException(e);
            }
        }

        private void CreateCloudMaterial()
        {
            try
            {
                // Detect render pipeline and choose appropriate shader
                Shader shader = GetAppropriateShader();

                if (shader == null)
                {
                    statusMessage = "✗ Cloud shader not found! Make sure shader files exist and compile without errors.";
                    statusType = MessageType.Error;
                    return;
                }

                Debug.Log($"[VolumetricCloudSetup] Using shader: {shader.name}");

                // Create material
                cloudMaterial = new Material(shader);
                cloudMaterial.name = "VolumetricCloudWeatherMaterial";

                // Assign noise textures if available
                if (worleyNoise128 != null)
                    cloudMaterial.SetTexture("_WorleyNoise", worleyNoise128);
                if (erosionNoise32 != null)
                    cloudMaterial.SetTexture("_ErosionNoise", erosionNoise32);
                if (perlinNoise32 != null)
                    cloudMaterial.SetTexture("_PerlinNoise", perlinNoise32);

                // Default settings
                cloudMaterial.SetFloat("_DensityMultiplier", 0.5f);
                cloudMaterial.SetFloat("_ShapeScale", 3f);
                cloudMaterial.SetFloat("_ErosionScale", 50f);
                cloudMaterial.SetInt("_RaymarchSteps", 48);
                cloudMaterial.SetFloat("_StepSize", 500f);
                cloudMaterial.SetFloat("_LightAbsorption", 0.3f);
                cloudMaterial.SetFloat("_SunIntensity", 1.5f);

                // Save
                string materialPath = "Assets/_Project/Materials/VolumetricCloudWeatherMaterial.mat";
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(materialPath));
                AssetDatabase.CreateAsset(cloudMaterial, materialPath);
                AssetDatabase.SaveAssets();

                cloudMaterialExists = true;
                statusMessage = "✓ Cloud material created successfully!";
                statusType = MessageType.Info;

                // Ping in project window
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = cloudMaterial;
            }
            catch (System.Exception e)
            {
                statusMessage = $"✗ Failed to create material: {e.Message}";
                statusType = MessageType.Error;
                Debug.LogException(e);
            }
        }

        private void CreateCloudVolume()
        {
            try
            {
                // Create GameObject
                GameObject cloudGO = new GameObject("VolumetricCloudVolume");
                cloudGO.transform.position = Vector3.zero;

                // Add required components
                var meshFilter = cloudGO.AddComponent<MeshFilter>();
                var meshRenderer = cloudGO.AddComponent<MeshRenderer>();
                var cloudVolume = cloudGO.AddComponent<VolumetricCloudVolume>();

                // Create cube mesh
                Mesh cubeMesh = CreateCubeMesh();
                meshFilter.sharedMesh = cubeMesh;

                // Assign material
                if (cloudMaterial == null)
                {
                    cloudMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                        "Assets/_Project/Materials/VolumetricCloudWeatherMaterial.mat");
                }

                if (cloudMaterial != null)
                {
                    meshRenderer.sharedMaterial = cloudMaterial;
                }
                else
                {
                    statusMessage = "⚠ Cloud volume created but no material found. Create material first.";
                    statusType = MessageType.Warning;
                }

                // Configure cloud volume
                var config = ScriptableObject.CreateInstance<WeatherVolumeConfig>();
                config.name = "CloudVolumeConfig_Temp";
                cloudVolume.Initialize(config);

                // Select in scene
                Selection.activeGameObject = cloudGO;
                SceneView.FrameLastActiveSceneView();

                selectedCloudVolume = cloudVolume;
                cloudVolumeExists = true;

                if (statusType != MessageType.Warning)
                {
                    statusMessage = "✓ Cloud volume created and ready for preview!";
                    statusType = MessageType.Info;
                }

                // Mark scene dirty
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            catch (System.Exception e)
            {
                statusMessage = $"✗ Failed to create cloud volume: {e.Message}";
                statusType = MessageType.Error;
                Debug.LogException(e);
            }
        }

        private void OneClickSetup()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Full Setup", "Checking existing assets...", 0f);

                if (!noiseTexturesExist)
                {
                    EditorUtility.DisplayProgressBar("Full Setup", "Generating noise textures...", 0.25f);
                    GenerateNoiseTextures();
                }

                if (!cloudMaterialExists)
                {
                    EditorUtility.DisplayProgressBar("Full Setup", "Creating material...", 0.5f);
                    CreateCloudMaterial();
                }

                if (!cloudVolumeExists)
                {
                    EditorUtility.DisplayProgressBar("Full Setup", "Creating cloud volume...", 0.75f);
                    CreateCloudVolume();
                }

                EditorUtility.ClearProgressBar();

                statusMessage = "✓ Full setup complete! You can now view clouds in Scene view.";
                statusType = MessageType.Info;

                // Force refresh
                ForceRefreshPreview();
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                statusMessage = $"✗ Setup failed: {e.Message}";
                statusType = MessageType.Error;
                Debug.LogException(e);
            }
        }

        #endregion

        #region Preview Methods

        private void FindAndSelectCloudVolume()
        {
            var volumes = FindObjectsOfType<VolumetricCloudVolume>();
            if (volumes.Length > 0)
            {
                selectedCloudVolume = volumes[0];
                Selection.activeGameObject = selectedCloudVolume.gameObject;
                statusMessage = $"✓ Found and selected {volumes.Length} cloud volume(s)";
                statusType = MessageType.Info;
            }
            else
            {
                statusMessage = "✗ No cloud volumes found in scene. Create one first.";
                statusType = MessageType.Warning;
            }
        }

        private void SelectExistingCloudVolume()
        {
            FindAndSelectCloudVolume();
        }

        private void ApplyQuickSettings()
        {
            if (selectedCloudVolume == null) return;

            // Get the material
            var renderer = selectedCloudVolume.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            Material mat = renderer.sharedMaterial;
            if (mat == null) return;

            // Apply settings
            mat.SetFloat("_DensityMultiplier", cloudDensity);
            mat.SetFloat("_ShapeScale", shapeScale);
            mat.SetFloat("_ErosionScale", erosionScale);
            mat.SetInt("_RaymarchSteps", raymarchSteps);
            mat.SetFloat("_WindSpeed", windSpeed);

            // Trigger update
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private void ForceRefreshPreview()
        {
            if (selectedCloudVolume != null)
            {
                // Force material update
                var renderer = selectedCloudVolume.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    renderer.sharedMaterial.SetFloat("_Seed", Random.value * 1000);
                }
            }

            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();

            statusMessage = "✓ Preview refreshed";
            statusType = MessageType.Info;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Detects the current render pipeline and returns the appropriate shader
        /// </summary>
        private Shader GetAppropriateShader()
        {
            Shader shader = null;

            // Check for SRP (URP/HDRP)
            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null)
            {
                // SRP detected - try SRP shader first
                shader = Shader.Find("WeatherVisualization3D/WeatherCloudVolumeSRP");

                if (shader == null)
                {
                    Debug.LogWarning("[VolumetricCloudSetup] SRP shader not found, falling back to standard shader.");
                }
            }

            // Fall back to standard shader (Built-in RP or SRP fallback)
            if (shader == null)
            {
                shader = Shader.Find("WeatherVisualization3D/WeatherCloudVolume");
            }

            // Last resort fallback
            if (shader == null)
            {
                shader = Shader.Find("WeatherVisualization3D/VolumetricCloudWeatherURP");
            }

            return shader;
        }

        /// <summary>
        /// Returns true if the project is using SRP (URP/HDRP)
        /// </summary>
        private bool IsUsingSRP()
        {
            return UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null;
        }

        private Mesh CreateCubeMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "CloudVolumeCube";

            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
            };

            int[] triangles = new int[]
            {
                0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4, 2, 3, 7, 2, 7, 6,
                0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            return mesh;
        }

        private void OpenNoiseTextureGenerator()
        {
            if (selectedCloudVolume != null)
            {
                var generator = selectedCloudVolume.GetComponent<CloudNoiseTextureGenerator>();
                if (generator == null)
                {
                    generator = selectedCloudVolume.gameObject.AddComponent<CloudNoiseTextureGenerator>();
                }
                Selection.activeObject = generator;
            }
            else
            {
                statusMessage = "⚠ Select a cloud volume first to attach the generator.";
                statusType = MessageType.Warning;
            }
        }

        private void ImportExternalNoiseTextures()
        {
            string path = EditorUtility.OpenFilePanel("Select Noise Texture", "", "png");
            if (!string.IsNullOrEmpty(path))
            {
                statusMessage = $"✓ Selected: {System.IO.Path.GetFileName(path)}. Use the importer to convert to 3D texture.";
                statusType = MessageType.Info;
            }
        }

        private void ForceReinitializeMaterial()
        {
            if (selectedCloudVolume != null)
            {
                var renderer = selectedCloudVolume.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = null;
                    renderer.sharedMaterial = cloudMaterial;
                }

                selectedCloudVolume.Refresh();
                statusMessage = "✓ Material reinitialized";
                statusType = MessageType.Info;
            }
        }

        private void ClearAllCloudData()
        {
            if (EditorUtility.DisplayDialog("Clear All Cloud Data",
                "This will remove all generated textures and materials. Continue?", "Yes", "No"))
            {
                worleyNoise128 = null;
                erosionNoise32 = null;
                perlinNoise32 = null;
                cloudMaterial = null;
                noiseTexturesExist = false;
                cloudMaterialExists = false;

                statusMessage = "✓ All cloud data cleared";
                statusType = MessageType.Info;
            }
        }

        #endregion
    }
}
