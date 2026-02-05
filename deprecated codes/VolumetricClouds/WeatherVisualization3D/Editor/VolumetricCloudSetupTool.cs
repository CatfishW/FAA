using UnityEngine;
using UnityEditor;

namespace WeatherVisualization3D.Editor
{
    /// <summary>
    /// Comprehensive setup tool for volumetric clouds
    /// </summary>
    public class VolumetricCloudSetupTool : EditorWindow
    {
        private bool createTestTexture = true;
        private bool setupMaterial = true;
        private bool createPrefab = true;
        
        [MenuItem("Tools/Weather Visualization/Volumetric Cloud Setup")]
        public static void ShowWindow()
        {
            GetWindow<VolumetricCloudSetupTool>("Volumetric Cloud Setup");
        }
        
        void OnGUI()
        {
            GUILayout.Label("Volumetric Cloud Setup Tool", EditorStyles.boldLabel);
            
            createTestTexture = EditorGUILayout.Toggle("Create Test 3D Texture", createTestTexture);
            setupMaterial = EditorGUILayout.Toggle("Setup Material", setupMaterial);
            createPrefab = EditorGUILayout.Toggle("Create Prefab", createPrefab);
            
            GUILayout.Space(20);
            
            if (GUILayout.Button("Setup Everything", GUILayout.Height(40)))
            {
                SetupEverything();
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Debug Current Scene"))
            {
                DebugCurrentSetup();
            }
        }
        
        void SetupEverything()
        {
            if (createTestTexture)
            {
                CreateTest3DTexture();
            }
            
            if (setupMaterial)
            {
                SetupCloudMaterial();
            }
            
            if (createPrefab)
            {
                CreateCloudPrefab();
            }
            
            EditorUtility.DisplayDialog("Setup Complete", 
                "Volumetric cloud setup finished. Check console for details.", "OK");
        }
        
        void CreateTest3DTexture()
        {
            Debug.Log("[VolumetricCloudSetup] Creating test 3D texture...");
            
            // Create a simple 3D texture with some noise
            int size = 64;
            Texture3D texture = new Texture3D(size, size, size, TextureFormat.RGBA32, false);
            texture.name = "TestDensityVolume";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            
            Color[] colors = new Color[size * size * size];
            
            // Create some test cloud patterns
            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int index = z * size * size + y * size + x;
                        
                        // Create a few cloud blobs
                        float density = 0;
                        
                        // Blob 1 - center
                        float d1 = Vector3.Distance(new Vector3(x, y, z), new Vector3(size * 0.5f, size * 0.6f, size * 0.5f));
                        if (d1 < size * 0.15f)
                            density = Mathf.Max(density, 1f - d1 / (size * 0.15f));
                        
                        // Blob 2 - offset
                        float d2 = Vector3.Distance(new Vector3(x, y, z), new Vector3(size * 0.3f, size * 0.5f, size * 0.7f));
                        if (d2 < size * 0.1f)
                            density = Mathf.Max(density, 1f - d2 / (size * 0.1f));
                        
                        // Blob 3 - another offset
                        float d3 = Vector3.Distance(new Vector3(x, y, z), new Vector3(size * 0.7f, size * 0.7f, size * 0.3f));
                        if (d3 < size * 0.12f)
                            density = Mathf.Max(density, 1f - d3 / (size * 0.12f));
                        
                        colors[index] = new Color(density, 0.1f, 0, density > 0.1f ? 1f : 0f);
                    }
                }
            }
            
            texture.SetPixels(colors);
            texture.Apply();
            
            // Save as asset
            string path = "Assets/_Project/Textures/WeatherVisualization/TestDensityVolume.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(texture, path);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[VolumetricCloudSetup] Created test 3D texture at: {path}");
            
            // Assign to material
            AssignTextureToMaterial(texture);
        }
        
        void AssignTextureToMaterial(Texture3D texture)
        {
            string matPath = "Assets/_Project/Materials/WeatherVisualization/VolumetricCloud.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            
            if (mat != null)
            {
                mat.SetTexture("_DensityVolume", texture);
                EditorUtility.SetDirty(mat);
                Debug.Log("[VolumetricCloudSetup] Assigned texture to material");
            }
        }
        
        void SetupCloudMaterial()
        {
            Debug.Log("[VolumetricCloudSetup] Setting up cloud material...");
            
            string matPath = "Assets/_Project/Materials/WeatherVisualization/VolumetricCloud.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            
            if (mat == null)
            {
                Debug.LogError($"[VolumetricCloudSetup] Material not found at: {matPath}");
                return;
            }
            
            // Set default values
            mat.SetInt("_RaymarchSteps", 64);
            mat.SetFloat("_StepSize", 100f);
            mat.SetFloat("_CloudDensity", 1f);
            mat.SetFloat("_JitterAmount", 0.5f);
            mat.SetFloat("_EarlyTerminationThreshold", 0.95f);
            
            // Set default colors (aviation standard)
            mat.SetColor("_LightColor_Weather", new Color(0.2f, 0.9f, 0.2f, 0.7f));
            mat.SetColor("_ModerateColor", new Color(0.95f, 0.9f, 0.2f, 0.8f));
            mat.SetColor("_HeavyColor", new Color(1f, 0.5f, 0.1f, 0.85f));
            mat.SetColor("_IntenseColor", new Color(0.95f, 0.15f, 0.1f, 0.9f));
            mat.SetColor("_ExtremeColor", new Color(0.95f, 0.2f, 0.8f, 1f));
            
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            
            Debug.Log("[VolumetricCloudSetup] Material setup complete");
        }
        
        void CreateCloudPrefab()
        {
            Debug.Log("[VolumetricCloudSetup] Creating cloud prefab...");
            
            // Create GameObject
            GameObject go = new GameObject("VolumetricCloudVolume");
            
            // Add MeshFilter with cube
            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            
            // Add MeshRenderer
            var meshRenderer = go.AddComponent<MeshRenderer>();
            string matPath = "Assets/_Project/Materials/WeatherVisualization/VolumetricCloud.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                meshRenderer.sharedMaterial = mat;
            }
            
            // Add VolumetricCloudVolume component
            var cloudVolume = go.AddComponent<VolumetricCloudVolume>();
            
            // Position it appropriately
            go.transform.position = new Vector3(0, 7500, 0);
            go.transform.localScale = new Vector3(150000, 15000, 150000);
            
            // Save as prefab
            string prefabPath = "Assets/_Project/Prefabs/WeatherVisualization/VolumetricCloudVolume.prefab";
            prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            
            DestroyImmediate(go);
            
            Debug.Log($"[VolumetricCloudSetup] Created prefab at: {prefabPath}");
        }
        
        void DebugCurrentSetup()
        {
            Debug.Log("=== Volumetric Cloud Debug Report ===");
            
            // Find all VolumetricCloudVolume objects
            var volumes = FindObjectsOfType<VolumetricCloudVolume>();
            Debug.Log($"Found {volumes.Length} VolumetricCloudVolume components");
            
            foreach (var vol in volumes)
            {
                Debug.Log($"\nObject: {vol.gameObject.name}");
                
                var renderer = vol.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    Debug.LogError("  - No MeshRenderer found!");
                    continue;
                }
                
                Debug.Log($"  - MeshRenderer: {(renderer.enabled ? "ENABLED" : "DISABLED")}");
                
                var mat = renderer.sharedMaterial;
                if (mat == null)
                {
                    Debug.LogError("  - No material assigned!");
                    continue;
                }
                
                Debug.Log($"  - Material: {mat.name}");
                Debug.Log($"  - Shader: {mat.shader?.name}");
                
                var texture = mat.GetTexture("_DensityVolume");
                if (texture == null)
                {
                    Debug.LogError("  - Density Volume texture is NULL!");
                }
                else
                {
                    Debug.Log($"  - Density Volume: {texture.name} ({texture.dimension})");
                }
            }
            
            // Check materials in project
            string matPath = "Assets/_Project/Materials/WeatherVisualization/VolumetricCloud.mat";
            Material cloudMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (cloudMat != null)
            {
                Debug.Log("\nProject Material Check:");
                Debug.Log($"  - VolumetricCloud.mat exists");
                var tex = cloudMat.GetTexture("_DensityVolume");
                Debug.Log($"  - Has density texture: {tex != null}");
            }
            
            Debug.Log("====================================");
        }
    }
}
