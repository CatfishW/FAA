using UnityEngine;
using UnityEditor;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Custom editor for VolumetricCloudVolume that renders clouds in Scene view.
    /// </summary>
    [CustomEditor(typeof(VolumetricCloudVolume))]
    public class VolumetricCloudVolumeEditor : UnityEditor.Editor
    {
        private VolumetricCloudVolume _volume;
        private Material _previewMaterial;
        private bool _showDebugSettings = false;
        private bool _autoRegenerate = true;
        private Texture3D _previewDensityTexture;
        private bool _showInEditMode = true;

        private void OnEnable()
        {
            _volume = target as VolumetricCloudVolume;

            // Register for Scene view updates
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            if (_previewDensityTexture != null)
            {
                DestroyImmediate(_previewDensityTexture);
                _previewDensityTexture = null;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(5);

            // Main properties
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_cloudMaterial"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_cloudShader"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_useEnhancedShader"));

            EditorGUILayout.Space(10);

            // Volume bounds
            EditorGUILayout.LabelField("Volume Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_volumeSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_volumeOffset"));

            EditorGUILayout.Space(10);

            // Quality
            EditorGUILayout.LabelField("Quality", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_qualityLevel"));

            EditorGUILayout.Space(10);

            // Shader controls
            EditorGUILayout.LabelField("Shader Controls", EditorStyles.boldLabel);

            if (GUILayout.Button("Recreate Material"))
            {
                RecreateMaterial();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Enhanced"))
            {
                SetEnhancedShader(true);
            }
            if (GUILayout.Button("Use Original"))
            {
                SetEnhancedShader(false);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Edit Mode Preview
            EditorGUILayout.LabelField("Edit Mode Preview", EditorStyles.boldLabel);
            _showInEditMode = EditorGUILayout.Toggle("Show in Edit Mode", _showInEditMode);

            if (_showInEditMode)
            {
                if (_previewDensityTexture == null)
                {
                    EditorGUILayout.HelpBox(
                        "Click 'Generate Preview Texture' to create cloud data for viewing in Scene view without entering Play mode.",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Texture Status:", EditorStyles.boldLabel, GUILayout.Width(100));
                    EditorGUILayout.LabelField($"{_previewDensityTexture.width}x{_previewDensityTexture.height}x{_previewDensityTexture.depth}", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();

                    // Show texture preview
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    EditorGUILayout.LabelField("Channel Preview:", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("R = Base Density | G = Detail | B = Erosion | A = Intensity", EditorStyles.miniLabel);

                    // Display the texture (will show one slice)
                    Rect texRect = GUILayoutUtility.GetAspectRect(1.0f);
                    EditorGUI.DrawPreviewTexture(texRect, _previewDensityTexture);
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Generate Preview Texture", GUILayout.Height(30)))
                {
                    ForceRegenerateTexture();
                }

                GUI.enabled = _previewDensityTexture != null;
                if (GUILayout.Button("Save to Asset", GUILayout.Height(30)))
                {
                    SaveCurrentTextureToAsset();
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // Quick visualization buttons
                EditorGUILayout.LabelField("Visualize Channels:", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Show Red (Density)"))
                {
                    VisualizeChannel(0);
                }
                if (GUILayout.Button("Show Green (Detail)"))
                {
                    VisualizeChannel(1);
                }
                if (GUILayout.Button("Show Blue (Erosion)"))
                {
                    VisualizeChannel(2);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);

            // Debug settings
            _showDebugSettings = EditorGUILayout.Foldout(_showDebugSettings, "Debug Settings");
            if (_showDebugSettings)
            {
                EditorGUI.indentLevel++;
                _autoRegenerate = EditorGUILayout.Toggle("Auto Regenerate", _autoRegenerate);

                if (GUILayout.Button("Force Scene View Refresh"))
                {
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("Debug: Test Solid Color"))
                {
                    var meshRenderer = _volume.GetComponent<MeshRenderer>();
                    if (meshRenderer != null && meshRenderer.sharedMaterial != null)
                    {
                        meshRenderer.sharedMaterial.SetFloat("_DebugNoise", 1);
                    }
                }

                if (GUILayout.Button("Debug: Reset Shader Flags"))
                {
                    var meshRenderer = _volume.GetComponent<MeshRenderer>();
                    if (meshRenderer != null && meshRenderer.sharedMaterial != null)
                    {
                        meshRenderer.sharedMaterial.SetFloat("_DebugNoise", 0);
                        meshRenderer.sharedMaterial.SetFloat("_DebugGradient", 0);
                        meshRenderer.sharedMaterial.SetFloat("_DebugLighting", 0);
                    }
                }

                if (GUILayout.Button("Debug: Log Material State"))
                {
                    LogMaterialState();
                }

                if (GUILayout.Button("Debug: Test Simple Material"))
                {
                    TestSimpleMaterial();
                }

                if (GUILayout.Button("Debug: Force Renderer On"))
                {
                    var meshRenderer = _volume.GetComponent<MeshRenderer>();
                    if (meshRenderer != null)
                    {
                        meshRenderer.enabled = true;
                        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        meshRenderer.receiveShadows = false;
                        Debug.Log("[VolumetricCloudVolumeEditor] MeshRenderer enabled");
                    }
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Quick Fixes:", EditorStyles.boldLabel);

                if (GUILayout.Button("Fix: Use Unlit Shader"))
                {
                    ApplyUnlitShader();
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Status
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            var renderer = _volume.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                EditorGUILayout.LabelField("Active Shader:", renderer.sharedMaterial.shader?.name ?? "None");
                EditorGUILayout.LabelField("Material:", renderer.sharedMaterial.name);

                // Check for density texture
                if (renderer.sharedMaterial.HasProperty("_DensityVolume"))
                {
                    var tex = renderer.sharedMaterial.GetTexture("_DensityVolume");
                    if (tex == null)
                    {
                        EditorGUILayout.Space(5);
                        EditorGUILayout.HelpBox(
                            "No Density Volume texture assigned!\n\n" +
                            "The clouds need weather data to render.\n" +
                            "1. Enter Play mode to generate data, or\n" +
                            "2. Use WeatherPreviewWindow to preview without Play mode",
                            MessageType.Warning);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Density Texture:", tex.name);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No material assigned! Click 'Recreate Material'.", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_volume == null) return;

            // Draw the clouds in Scene view
            DrawCloudsInSceneView(sceneView);

            // Draw handles for editing
            DrawEditingHandles();
        }

        private void DrawCloudsInSceneView(SceneView sceneView)
        {
            if (!_showInEditMode) return;

            var meshFilter = _volume.GetComponent<MeshFilter>();
            var meshRenderer = _volume.GetComponent<MeshRenderer>();

            if (meshFilter == null || meshRenderer == null) return;
            if (meshFilter.sharedMesh == null) return;

            var material = meshRenderer.sharedMaterial;
            if (material == null || material.shader == null) return;

            // Don't render if mesh renderer is disabled
            if (!meshRenderer.enabled) return;

            // Ensure preview texture exists
            EnsurePreviewTexture(material);

            // Get volume bounds for debugging
            var size = _volume.GetType().GetField("_volumeSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_volume) as Vector3?
                ?? new Vector3(50000f, 50000f, 50000f);

            // Draw wireframe bounds to show where clouds should be
            Handles.color = new Color(0.3f, 0.7f, 1f, 0.3f);
            Handles.DrawWireCube(_volume.transform.position, size);

            // Try to draw with material - but this often fails in Scene view for complex shaders
            // So we'll also draw a preview using simple cubes
            Matrix4x4 matrix = _volume.transform.localToWorldMatrix;

            // Save current state
            var prevMatrix = Handles.matrix;

            Handles.matrix = matrix;

            // Attempt material-based rendering
            bool materialRendered = false;
            if (material.SetPass(0))
            {
                try
                {
                    Graphics.DrawMeshNow(meshFilter.sharedMesh, matrix);
                    materialRendered = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[VolumetricCloudVolumeEditor] Material render failed: {e.Message}");
                }
            }

            // If material didn't render, show fallback preview
            if (!materialRendered || _showDebugSettings)
            {
                DrawFallbackCloudPreview(size);
            }

            // Restore state
            Handles.matrix = prevMatrix;
        }

        private void DrawFallbackCloudPreview(Vector3 size)
        {
            // Draw sample points from the 3D texture as small cubes
            if (_previewDensityTexture == null) return;

            int sampleCount = 500; // Number of sample points
            int texSize = _previewDensityTexture.width;
            Color[] pixels = _previewDensityTexture.GetPixels();

            System.Random rng = new System.Random(12345); // Fixed seed for consistency

            for (int i = 0; i < sampleCount; i++)
            {
                // Random position in volume
                int x = rng.Next(texSize);
                int y = rng.Next(texSize);
                int z = rng.Next(texSize);

                int idx = x + y * texSize + z * texSize * texSize;
                if (idx >= pixels.Length) continue;

                float density = pixels[idx].r;

                // Only draw if density is significant
                if (density > 0.2f)
                {
                    // Map to world position
                    Vector3 localPos = new Vector3(
                        (x / (float)texSize - 0.5f) * size.x,
                        (y / (float)texSize - 0.5f) * size.y,
                        (z / (float)texSize - 0.5f) * size.z
                    );

                    Vector3 worldPos = _volume.transform.position + localPos;

                    // Color based on density
                    Color cloudColor;
                    if (density < 0.4f)
                        cloudColor = new Color(0.8f, 0.9f, 1f, density * 0.3f); // Light
                    else if (density < 0.6f)
                        cloudColor = new Color(0.6f, 0.7f, 0.9f, density * 0.4f); // Moderate
                    else if (density < 0.8f)
                        cloudColor = new Color(0.4f, 0.5f, 0.7f, density * 0.5f); // Heavy
                    else
                        cloudColor = new Color(0.3f, 0.35f, 0.5f, density * 0.6f); // Intense

                    Handles.color = cloudColor;

                    // Draw small cube - size based on density
                    float cubeSize = Mathf.Lerp(200f, 800f, density);
                    Handles.DrawSolidRectangleWithOutline(
                        new Rect(worldPos.x - cubeSize/2, worldPos.z - cubeSize/2, cubeSize, cubeSize),
                        cloudColor,
                        Color.clear
                    );
                }
            }
        }

        private void EnsurePreviewTexture(Material material)
        {
            // Check if material already has a valid texture
            var existingTex = material.GetTexture("_DensityVolume") as Texture3D;
            if (existingTex != null && _previewDensityTexture == null)
            {
                _previewDensityTexture = existingTex;
                return;
            }

            // Create preview texture if needed
            if (_previewDensityTexture == null)
            {
                _previewDensityTexture = GeneratePreviewDensityTexture();
                material.SetTexture("_DensityVolume", _previewDensityTexture);

                // Set default volume bounds
                var size = _volume.GetType().GetField("_volumeSize",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_volume) as Vector3?
                    ?? new Vector3(50000f, 50000f, 50000f);

                Vector3 min = _volume.transform.position - size * 0.5f;
                Vector3 max = _volume.transform.position + size * 0.5f;

                material.SetVector("_VolumeMin", min);
                material.SetVector("_VolumeMax", max);
                material.SetVector("_VolumeSize", size);
                material.SetVector("_VolumeCenter", _volume.transform.position);

                // Apply other default settings
                ApplyDefaultSettings(material, WeatherVolumeConfig.CreateDefault());

                Debug.Log("[VolumetricCloudVolumeEditor] Generated and applied preview 3D texture");
            }
        }

        public void ForceRegenerateTexture()
        {
            if (_previewDensityTexture != null)
            {
                DestroyImmediate(_previewDensityTexture);
                _previewDensityTexture = null;
            }

            var meshRenderer = _volume.GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                meshRenderer.sharedMaterial.SetTexture("_DensityVolume", null);
                EnsurePreviewTexture(meshRenderer.sharedMaterial);
            }

            SceneView.RepaintAll();
        }

        private Texture3D GeneratePreviewDensityTexture()
        {
            int size = 128; // Increased resolution for better quality
            Debug.Log("[VolumetricCloudVolumeEditor] Generating preview 3D texture with Perlin-Worley FBM noise...");

            Texture3D tex = new Texture3D(size, size, size, TextureFormat.RGBA32, true);
            tex.name = "PreviewDensityVolume_Procedural";
            tex.wrapMode = TextureWrapMode.Repeat; // Repeat for seamless tiling
            tex.filterMode = FilterMode.Trilinear; // Better quality filtering
            tex.anisoLevel = 1;

            Color[] pixels = new Color[size * size * size];

            int nonZeroPixels = 0;
            float maxDensityFound = 0f;

            // Cloud parameters
            float cloudBaseHeight = size * 0.2f;
            float cloudTopHeight = size * 0.7f;
            float coverage = 0.35f; // Overall cloud coverage

            // Storm cell centers for structured clouds
            var stormCenters = new Vector3[]
            {
                new Vector3(size * 0.3f, size * 0.35f, size * 0.4f),
                new Vector3(size * 0.7f, size * 0.4f, size * 0.6f),
                new Vector3(size * 0.5f, size * 0.45f, size * 0.3f),
                new Vector3(size * 0.2f, size * 0.3f, size * 0.7f),
                new Vector3(size * 0.8f, size * 0.35f, size * 0.2f)
            };

            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int idx = x + y * size + z * size * size;

                        // Normalized position [0, 1]
                        Vector3 uvw = new Vector3(
                            x / (float)size,
                            y / (float)size,
                            z / (float)size
                        );

                        // ===== LAYER 1: Large-scale cloud structure (Perlin FBM) =====
                        float largeScale = PerlinFBM(uvw, 3, 2.0f, 0.5f);

                        // ===== LAYER 2: Medium detail (Perlin-Worley hybrid) =====
                        float mediumDetail = PerlinWorleyNoise(uvw, 4.0f);

                        // ===== LAYER 3: Fine erosion (High frequency Perlin) =====
                        float erosion = PerlinFBM(uvw * 8f + Vector3.one * 100f, 2, 4.0f, 0.5f);

                        // ===== Height-based density gradient =====
                        float heightGradient = HeightBasedGradient(y, size, cloudBaseHeight, cloudTopHeight);

                        // ===== Coverage mask from storm centers =====
                        float coverageMask = 0f;
                        float stormIntensity = 0f;
                        foreach (var center in stormCenters)
                        {
                            Vector3 centerUVW = new Vector3(
                                center.x / size,
                                center.y / size,
                                center.z / size
                            );
                            float dist = Vector3.Distance(uvw, centerUVW);
                            float radius = 0.25f;
                            if (dist < radius)
                            {
                                float falloff = 1f - (dist / radius);
                                falloff = falloff * falloff * (3f - 2f * falloff); // Smoothstep
                                coverageMask = Mathf.Max(coverageMask, falloff);
                                stormIntensity = Mathf.Max(stormIntensity, falloff);
                            }
                        }

                        // Also add some general coverage
                        coverageMask = Mathf.Max(coverageMask, largeScale * coverage);

                        // ===== Combine layers =====
                        // Base density from large scale + coverage mask
                        float baseDensity = largeScale * coverageMask * heightGradient;

                        // Apply medium detail (erodes edges)
                        baseDensity = baseDensity - (1f - mediumDetail) * 0.3f;

                        // Apply fine erosion
                        baseDensity = baseDensity - (1f - erosion) * 0.15f;

                        // Remap and boost for visibility
                        baseDensity = Mathf.Clamp01(baseDensity * 3.0f);

                        if (baseDensity > 0.01f)
                        {
                            nonZeroPixels++;
                            maxDensityFound = Mathf.Max(maxDensityFound, baseDensity);
                        }

                        // Channel packing for industry-standard format:
                        // R = Base density (large scale structure)
                        // G = Detail noise (medium frequency)
                        // B = Erosion noise (high frequency)
                        // A = Height/Intensity mask
                        pixels[idx] = new Color(
                            baseDensity,
                            mediumDetail,
                            erosion,
                            stormIntensity
                        );
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: false);

            // Save as asset so it displays in inspector
            SaveTextureAsAsset(tex);

            Debug.Log($"[VolumetricCloudVolumeEditor] Generated 3D texture: {nonZeroPixels} cloud pixels ({100f * nonZeroPixels / pixels.Length:F1}%), max density: {maxDensityFound:F2}");

            return tex;
        }

        // Perlin FBM (Fractal Brownian Motion) - multiple octaves of noise
        private float PerlinFBM(Vector3 uvw, int octaves, float frequency, float amplitudeDecay)
        {
            float result = 0f;
            float amplitude = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                Vector3 samplePos = uvw * frequency + new Vector3(i * 10f, i * 20f, i * 30f);

                // Sample 3D Perlin by combining 2D samples
                float noise = Mathf.PerlinNoise(samplePos.x, samplePos.y) *
                             Mathf.PerlinNoise(samplePos.y, samplePos.z) *
                             Mathf.PerlinNoise(samplePos.z, samplePos.x);
                noise = Mathf.Pow(noise, 0.5f); // Gamma correction

                result += noise * amplitude;
                maxValue += amplitude;
                amplitude *= amplitudeDecay;
                frequency *= 2f;
            }

            return result / maxValue;
        }

        // Perlin-Worley hybrid noise (simulates billowy cloud shapes)
        private float PerlinWorleyNoise(Vector3 uvw, float frequency)
        {
            Vector3 samplePos = uvw * frequency;

            // Perlin for smooth variation
            float perlin = Mathf.PerlinNoise(samplePos.x, samplePos.y) *
                          Mathf.PerlinNoise(samplePos.y, samplePos.z);

            // Worley-like cellular pattern (simulated with offset Perlins)
            float worley = WorleyNoise(samplePos);

            // Perlin modulated by inverted Worley for billowy effect
            return perlin * (1f - worley * 0.7f);
        }

        // Simulated Worley noise using distance to feature points
        private float WorleyNoise(Vector3 pos)
        {
            Vector3 cell = new Vector3(Mathf.Floor(pos.x), Mathf.Floor(pos.y), Mathf.Floor(pos.z));
            float minDist = float.MaxValue;

            // Check neighboring cells
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        Vector3 neighborCell = cell + new Vector3(x, y, z);
                        Vector3 featurePoint = neighborCell + RandomInCell(neighborCell);
                        float dist = Vector3.Distance(pos, featurePoint);
                        minDist = Mathf.Min(minDist, dist);
                    }
                }
            }

            return Mathf.Clamp01(minDist);
        }

        // Deterministic random point in cell
        private Vector3 RandomInCell(Vector3 cell)
        {
            float hash = Mathf.Sin(cell.x * 12.9898f + cell.y * 78.233f + cell.z * 43.123f) * 43758.5453f;
            hash = hash - Mathf.Floor(hash);

            float hash2 = Mathf.Sin(cell.x * 43.123f + cell.y * 12.9898f + cell.z * 78.233f) * 43758.5453f;
            hash2 = hash2 - Mathf.Floor(hash2);

            float hash3 = Mathf.Sin(cell.x * 78.233f + cell.y * 43.123f + cell.z * 12.9898f) * 43758.5453f;
            hash3 = hash3 - Mathf.Floor(hash3);

            return new Vector3(hash, hash2, hash3);
        }

        // Height-based gradient for realistic cloud layering
        private float HeightBasedGradient(float y, int size, float baseHeight, float topHeight)
        {
            float heightRatio = y / size;

            // Below cloud base
            if (heightRatio < baseHeight / size)
            {
                float t = heightRatio / (baseHeight / size);
                return Mathf.Lerp(0f, 0.3f, t * t); // Gradual increase
            }
            // Within cloud layer
            else if (heightRatio < topHeight / size)
            {
                return 1f; // Full density in cloud layer
            }
            // Above cloud top
            else
            {
                float t = (heightRatio - topHeight / size) / (1f - topHeight / size);
                return Mathf.Lerp(0.5f, 0f, t); // Gradual decrease
            }
        }

        // Save the texture as an asset file so it displays in inspector
        private void SaveTextureAsAsset(Texture3D tex)
        {
            string path = "Assets/_Project/Textures/WeatherVisualization/Generated/";
            string fileName = "PreviewDensityVolume_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".asset";

            try
            {
                // Ensure directory exists
                if (!System.IO.Directory.Exists(path))
                {
                    System.IO.Directory.CreateDirectory(path);
                }

                string fullPath = path + fileName;
                AssetDatabase.CreateAsset(tex, fullPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[VolumetricCloudVolumeEditor] Saved 3D texture to: {fullPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[VolumetricCloudVolumeEditor] Could not save texture asset: {e.Message}");
            }
        }

        private void DrawEditingHandles()
        {
            // Draw wireframe bounds
            var volumeSize = _volume.GetType().GetField("_volumeSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_volume) as Vector3?;

            if (volumeSize.HasValue)
            {
                Handles.color = new Color(0.3f, 0.7f, 1f, 0.3f);
                Handles.DrawWireCube(Vector3.zero, Vector3.one);

                // Draw size label
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                style.normal.textColor = new Color(0.3f, 0.7f, 1f);
                style.fontSize = 11;
                Handles.Label(Vector3.up * 0.6f, $"Cloud Volume\n{volumeSize.Value.x / 1000:F0}km x {volumeSize.Value.y / 1000:F0}km x {volumeSize.Value.z / 1000:F0}km", style);
            }

            // Position handle
            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = Handles.PositionHandle(_volume.transform.position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_volume.transform, "Move Cloud Volume");
                _volume.transform.position = newPosition;
            }
        }

        private void RecreateMaterial()
        {
            var meshRenderer = _volume.GetComponent<MeshRenderer>();
            if (meshRenderer == null) return;

            // Destroy old material
            if (meshRenderer.sharedMaterial != null)
            {
                DestroyImmediate(meshRenderer.sharedMaterial, true);
            }

            // Get shader
            bool useEnhanced = serializedObject.FindProperty("_useEnhancedShader").boolValue;
            string shaderName = useEnhanced
                ? "WeatherVisualization3D/VolumetricCloudEnhanced"
                : "WeatherVisualization3D/VolumetricCloud";

            Shader shader = Shader.Find(shaderName);
            if (shader == null && useEnhanced)
            {
                Debug.LogWarning("Enhanced shader not found, falling back to original");
                shader = Shader.Find("WeatherVisualization3D/VolumetricCloud");
            }

            if (shader == null)
            {
                Debug.LogError($"Could not find shader: {shaderName}");
                return;
            }

            // Create new material
            Material material = new Material(shader);
            material.name = "VolumetricCloudMaterial_Editor";
            meshRenderer.material = material;

            // Initialize with default config if available
            var config = WeatherVolumeConfig.CreateDefault();
            ApplyDefaultSettings(material, config);

            Debug.Log($"[VolumetricCloudVolumeEditor] Created material with shader: {shader.name}");
            SceneView.RepaintAll();
        }

        private void SetEnhancedShader(bool enhanced)
        {
            var prop = serializedObject.FindProperty("_useEnhancedShader");
            prop.boolValue = enhanced;
            serializedObject.ApplyModifiedProperties();
            RecreateMaterial();
        }

        private void ApplyDefaultSettings(Material material, WeatherVolumeConfig config)
        {
            if (material == null || config == null) return;

            // Basic settings
            material.SetInteger("_RaymarchSteps", 128);
            material.SetFloat("_StepSize", 100f);
            material.SetFloat("_JitterAmount", 0.3f);
            material.SetFloat("_CloudDensity", 1.5f);
            material.SetFloat("_DetailScale", 3f);
            material.SetFloat("_DetailStrength", 0.5f);

            // Lighting
            material.SetVector("_LightDir", new Vector3(0.5f, 1f, 0.3f));
            material.SetColor("_LightColor", Color.white);
            material.SetColor("_AmbientColor", new Color(0.4f, 0.45f, 0.5f));
            material.SetFloat("_LightAbsorption", 0.8f);

            // Enhanced shader settings
            if (material.HasProperty("_ShapeScale"))
            {
                material.SetFloat("_ShapeScale", config.shapeScale);
                material.SetFloat("_ErosionScale", config.erosionScale);
                material.SetFloat("_ShapeStrength", config.shapeStrength);
                material.SetFloat("_ErosionStrength", config.erosionStrength);
                material.SetFloat("_CloudBaseHeight", config.cloudBaseHeight);
                material.SetFloat("_CloudTopHeight", config.cloudTopHeight);
                material.SetFloat("_BaseSoftness", config.baseSoftness);
                material.SetFloat("_TopSoftness", config.topSoftness);
                material.SetFloat("_AnvilAmount", config.anvilAmount);
                material.SetFloat("_WindSpeed", config.windSpeed);
                material.SetVector("_WindDirection", config.windDirection);
                material.SetFloat("_SilverLining", config.silverLining);
                material.SetFloat("_ColorBlend", config.colorBlend);
            }

            // Weather colors
            material.SetColor("_LightColor_Weather", config.lightColor);
            material.SetColor("_ModerateColor", config.moderateColor);
            material.SetColor("_HeavyColor", config.heavyColor);
            material.SetColor("_IntenseColor", config.intenseColor);
            material.SetColor("_ExtremeColor", config.extremeColor);
            material.SetColor("_StormCoreColor", config.stormCoreColor);
        }

        private void LogMaterialState()
        {
            var meshRenderer = _volume.GetComponent<MeshRenderer>();
            if (meshRenderer == null || meshRenderer.sharedMaterial == null)
            {
                Debug.LogError("[VolumetricCloudVolumeEditor] No material found!");
                return;
            }

            var mat = meshRenderer.sharedMaterial;
            Debug.Log("=== Material State ===");
            Debug.Log($"Shader: {mat.shader?.name}");
            Debug.Log($"Material: {mat.name}");

            if (mat.HasProperty("_DensityVolume"))
            {
                var tex = mat.GetTexture("_DensityVolume");
                Debug.Log($"_DensityVolume: {(tex != null ? tex.name : "NULL")}");
            }

            if (mat.HasProperty("_VolumeMin"))
            {
                Debug.Log($"_VolumeMin: {mat.GetVector("_VolumeMin")}");
            }

            if (mat.HasProperty("_VolumeMax"))
            {
                Debug.Log($"_VolumeMax: {mat.GetVector("_VolumeMax")}");
            }

            if (mat.HasProperty("_VolumeSize"))
            {
                Debug.Log($"_VolumeSize: {mat.GetVector("_VolumeSize")}");
            }

            // Test SetPass
            bool setPassResult = mat.SetPass(0);
            Debug.Log($"SetPass(0) result: {setPassResult}");
        }

        private void TestSimpleMaterial()
        {
            var meshRenderer = _volume.GetComponent<MeshRenderer>();
            if (meshRenderer == null) return;

            // Create a simple colored material to test if the mesh is visible at all
            var testMat = new Material(Shader.Find("Standard"));
            testMat.color = new Color(0.5f, 0.7f, 1f, 0.5f);
            testMat.SetFloat("_Mode", 3); // Transparent mode
            testMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            testMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            testMat.SetInt("_ZWrite", 0);
            testMat.DisableKeyword("_ALPHATEST_ON");
            testMat.EnableKeyword("_ALPHABLEND_ON");
            testMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            testMat.renderQueue = 3000;

            meshRenderer.material = testMat;

            Debug.Log("[VolumetricCloudVolumeEditor] Applied test material. If you see a blue cube, the mesh is rendering correctly.");
        }

        private void ApplyUnlitShader()
        {
            var meshRenderer = _volume.GetComponent<MeshRenderer>();
            if (meshRenderer == null) return;

            // Create a simple unlit shader that just shows the 3D texture density
            string shaderCode = @"
            Shader ""WeatherVisualization3D/SimpleCloudPreview""
            {
                Properties
                {
                    _DensityVolume(""Density Volume"", 3D) = ""white"" {}
                    _VolumeMin(""Volume Min"", Vector) = (0,0,0,0)
                    _VolumeMax(""Volume Max"", Vector) = (1,1,1,0)
                    _AlphaScale(""Alpha Scale"", Range(0,10)) = 2
                }
                SubShader
                {
                    Tags { ""RenderType""=""Transparent"" ""Queue""=""Transparent"" }
                    LOD 100

                    Pass
                    {
                        CGPROGRAM
                        #pragma vertex vert
                        #pragma fragment frag
                        #include ""UnityCG.cginc""

                        struct appdata
                        {
                            float4 vertex : POSITION;
                        };

                        struct v2f
                        {
                            float4 vertex : SV_POSITION;
                            float3 worldPos : TEXCOORD0;
                        };

                        sampler3D _DensityVolume;
                        float3 _VolumeMin;
                        float3 _VolumeMax;
                        float _AlphaScale;

                        v2f vert(appdata v)
                        {
                            v2f o;
                            o.vertex = UnityObjectToClipPos(v.vertex);
                            o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                            return o;
                        }

                        fixed4 frag(v2f i) : SV_Target
                        {
                            float3 uvw = (i.worldPos - _VolumeMin) / (_VolumeMax - _VolumeMin);
                            if (any(uvw < 0) || any(uvw > 1))
                                return fixed4(0,0,0,0);

                            float density = tex3D(_DensityVolume, uvw).r;
                            float alpha = saturate(density * _AlphaScale);

                            // Color based on density
                            fixed3 color = lerp(fixed3(0.8,0.9,1), fixed3(0.3,0.4,0.6), density);

                            return fixed4(color, alpha);
                        }
                        ENDCG
                    }
                }
            }";

            // Create the shader asset
            string shaderPath = "Assets/_Project/Shaders/SimpleCloudPreview.shader";
            System.IO.File.WriteAllText(shaderPath, shaderCode);
            AssetDatabase.ImportAsset(shaderPath);

            // Create material
            var shader = Shader.Find("WeatherVisualization3D/SimpleCloudPreview");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetTexture("_DensityVolume", _previewDensityTexture);

                // Set bounds
                var size = _volume.GetType().GetField("_volumeSize",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_volume) as Vector3?
                    ?? new Vector3(50000f, 50000f, 50000f);

                mat.SetVector("_VolumeMin", _volume.transform.position - size * 0.5f);
                mat.SetVector("_VolumeMax", _volume.transform.position + size * 0.5f);
                mat.SetFloat("_AlphaScale", 3);

                meshRenderer.material = mat;
                Debug.Log("[VolumetricCloudVolumeEditor] Applied simple preview shader");
            }
            else
            {
                Debug.LogError("[VolumetricCloudVolumeEditor] Failed to create preview shader");
            }
        }

        // Save current preview texture to asset file
        private void SaveCurrentTextureToAsset()
        {
            if (_previewDensityTexture == null)
            {
                Debug.LogWarning("[VolumetricCloudVolumeEditor] No preview texture to save");
                return;
            }

            string path = "Assets/_Project/Textures/WeatherVisualization/Generated/";
            string fileName = "CloudDensityVolume_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".asset";

            try
            {
                // Ensure directory exists
                if (!System.IO.Directory.Exists(path))
                {
                    System.IO.Directory.CreateDirectory(path);
                }

                string fullPath = path + fileName;

                // Create a copy to save (since the original might be temporary)
                Texture3D savedTex = new Texture3D(
                    _previewDensityTexture.width,
                    _previewDensityTexture.height,
                    _previewDensityTexture.depth,
                    _previewDensityTexture.format,
                    true
                );
                savedTex.name = "CloudDensityVolume_Saved";
                savedTex.wrapMode = _previewDensityTexture.wrapMode;
                savedTex.filterMode = _previewDensityTexture.filterMode;
                savedTex.SetPixels(_previewDensityTexture.GetPixels());
                savedTex.Apply(true, false);

                AssetDatabase.CreateAsset(savedTex, fullPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Ping the new asset in Project window
                var asset = AssetDatabase.LoadAssetAtPath<Texture3D>(fullPath);
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;

                Debug.Log($"[VolumetricCloudVolumeEditor] Saved 3D texture to: {fullPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VolumetricCloudVolumeEditor] Failed to save texture: {e.Message}");
            }
        }

        // Visualize a specific channel of the 3D texture
        private void VisualizeChannel(int channelIndex)
        {
            if (_previewDensityTexture == null)
            {
                Debug.LogWarning("[VolumetricCloudVolumeEditor] Generate texture first");
                return;
            }

            int size = _previewDensityTexture.width;
            Color[] originalPixels = _previewDensityTexture.GetPixels();
            Color[] visualizedPixels = new Color[originalPixels.Length];

            for (int i = 0; i < originalPixels.Length; i++)
            {
                float value = 0f;
                switch (channelIndex)
                {
                    case 0: value = originalPixels[i].r; break;
                    case 1: value = originalPixels[i].g; break;
                    case 2: value = originalPixels[i].b; break;
                    case 3: value = originalPixels[i].a; break;
                }

                // Create a heatmap visualization
                Color heatmapColor;
                if (value < 0.25f)
                    heatmapColor = Color.Lerp(Color.black, Color.blue, value * 4f);
                else if (value < 0.5f)
                    heatmapColor = Color.Lerp(Color.blue, Color.green, (value - 0.25f) * 4f);
                else if (value < 0.75f)
                    heatmapColor = Color.Lerp(Color.green, Color.yellow, (value - 0.5f) * 4f);
                else
                    heatmapColor = Color.Lerp(Color.yellow, Color.red, (value - 0.75f) * 4f);

                visualizedPixels[i] = heatmapColor;
            }

            // Create temporary visualization texture
            Texture3D vizTex = new Texture3D(size, size, size, TextureFormat.RGBA32, false);
            vizTex.name = $"Channel{channelIndex}_Visualization";
            vizTex.wrapMode = TextureWrapMode.Clamp;
            vizTex.filterMode = FilterMode.Bilinear;
            vizTex.SetPixels(visualizedPixels);
            vizTex.Apply();

            // Display in inspector
            Selection.activeObject = vizTex;

            string[] channelNames = { "Red (Density)", "Green (Detail)", "Blue (Erosion)", "Alpha (Intensity)" };
            Debug.Log($"[VolumetricCloudVolumeEditor] Visualizing {channelNames[channelIndex]} channel");
        }
    }
}
