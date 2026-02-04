using UnityEngine;
using UnityEditor;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Simple preview renderer for volumetric clouds - guarantees visibility in Scene view
    /// </summary>
    [InitializeOnLoad]
    public class CloudPreviewRenderer
    {
        private static Texture3D previewTexture;
        private static Material previewMaterial;

        static CloudPreviewRenderer()
        {
            EditorApplication.delayCall += () => {
                SceneView.duringSceneGui += OnSceneGUI;
            };
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            // Find all cloud volumes
            var volumes = Object.FindObjectsOfType<VolumetricCloudVolume>();

            foreach (var volume in volumes)
            {
                if (volume == null) continue;
                DrawCloudPreview(volume);
            }
        }

        private static void DrawCloudPreview(VolumetricCloudVolume volume)
        {
            // Get volume size
            var sizeField = volume.GetType().GetField("_volumeSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Vector3 size = sizeField != null ? (Vector3)sizeField.GetValue(volume) : new Vector3(50000f, 15000f, 50000f);

            Vector3 center = volume.transform.position;

            // Initialize preview texture if needed
            if (previewTexture == null)
            {
                previewTexture = GeneratePreviewTexture();
            }

            // Initialize material if needed
            if (previewMaterial == null)
            {
                previewMaterial = CreatePreviewMaterial();
            }

            // Update material properties
            previewMaterial.SetTexture("_DensityTex", previewTexture);
            previewMaterial.SetVector("_VolumeMin", new Vector4(center.x - size.x/2, center.y - size.y/2, center.z - size.z/2, 0));
            previewMaterial.SetVector("_VolumeMax", new Vector4(center.x + size.x/2, center.y + size.y/2, center.z + size.z/2, 0));

            // Draw wireframe
            Handles.color = new Color(0.3f, 0.7f, 1f, 0.5f);
            Handles.DrawWireCube(center, size);

            // Draw the volume with preview material
            bool materialRendered = false;
            if (previewMaterial != null && previewMaterial.SetPass(0))
            {
                try
                {
                    Graphics.DrawMeshNow(GetCubeMesh(), Matrix4x4.TRS(center, Quaternion.identity, size));
                    materialRendered = true;
                }
                catch { }
            }

            // If material rendering failed, use fallback
            if (!materialRendered)
            {
                DrawCloudVoxels(center, size);
            }

            // Draw label
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = new Color(0.3f, 0.7f, 1f, 1f);
            style.fontSize = 12;
            Handles.Label(center + Vector3.up * size.y * 0.55f, $"Cloud Volume\n{size.x/1000:F0}km x {size.y/1000:F0}km x {size.z/1000:F0}km", style);
        }

        private static Texture3D GeneratePreviewTexture()
        {
            int size = 32;
            Texture3D tex = new Texture3D(size, size, size, TextureFormat.RGBA32, false);
            tex.name = "CloudPreviewTexture";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size * size];

            // Simple storm cell generation
            Vector3 center1 = new Vector3(size * 0.3f, size * 0.4f, size * 0.3f);
            Vector3 center2 = new Vector3(size * 0.7f, size * 0.5f, size * 0.6f);
            float radius = size * 0.15f;

            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int idx = x + y * size + z * size * size;
                        Vector3 pos = new Vector3(x, y, z);

                        float density = 0f;

                        // Cell 1
                        float dist1 = Vector3.Distance(pos, center1);
                        if (dist1 < radius)
                        {
                            density = Mathf.Max(density, (1f - dist1/radius) * 0.8f);
                        }

                        // Cell 2
                        float dist2 = Vector3.Distance(pos, center2);
                        if (dist2 < radius)
                        {
                            density = Mathf.Max(density, (1f - dist2/radius) * 0.9f);
                        }

                        // Add some noise
                        density *= 0.7f + 0.3f * Mathf.PerlinNoise(x * 0.1f, z * 0.1f);

                        pixels[idx] = new Color(density, density, density, 1f);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            return tex;
        }

        private static Material CreatePreviewMaterial()
        {
            string shaderCode = @"
            Shader ""WeatherVisualization3D/CloudPreview""
            {
                Properties
                {
                    _DensityTex(""Density"", 3D) = ""white"" {}
                    _VolumeMin(""Min"", Vector) = (0,0,0,0)
                    _VolumeMax(""Max"", Vector) = (1,1,1,0)
                }
                SubShader
                {
                    Tags { ""RenderType""=""Transparent"" ""Queue""=""Transparent+100"" }
                    Cull Off
                    ZWrite Off
                    Blend SrcAlpha OneMinusSrcAlpha

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

                        sampler3D _DensityTex;
                        float3 _VolumeMin;
                        float3 _VolumeMax;

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

                            float density = tex3D(_DensityTex, uvw).r;

                            // Aviation weather colors
                            fixed3 color;
                            if (density < 0.2)
                                color = fixed3(0.2, 0.9, 0.2);      // Light - Green
                            else if (density < 0.4)
                                color = fixed3(1.0, 0.95, 0.1);     // Moderate - Yellow
                            else if (density < 0.6)
                                color = fixed3(1.0, 0.5, 0.1);      // Heavy - Orange
                            else if (density < 0.8)
                                color = fixed3(1.0, 0.15, 0.15);    // Intense - Red
                            else
                                color = fixed3(1.0, 0.1, 0.8);      // Extreme - Magenta

                            return fixed4(color, density * 0.5);
                        }
                        ENDCG
                    }
                }
            }";

            // Create shader asset
            string shaderPath = "Assets/_Project/Shaders/CloudPreview.shader";
            System.IO.File.WriteAllText(shaderPath, shaderCode);
            AssetDatabase.ImportAsset(shaderPath);

            Shader shader = Shader.Find("WeatherVisualization3D/CloudPreview");
            if (shader == null)
            {
                Debug.LogError("[CloudPreviewRenderer] Failed to create preview shader");
                return null;
            }

            Material mat = new Material(shader);
            mat.name = "CloudPreviewMaterial";
            return mat;
        }

        private static void DrawCloudVoxels(Vector3 center, Vector3 size)
        {
            // Generate sample points for cloud visualization
            int samples = 100;
            float voxelSize = Mathf.Min(size.x, size.y, size.z) * 0.02f;

            for (int i = 0; i < samples; i++)
            {
                // Pseudo-random positions within volume
                float nx = Mathf.Sin(i * 1.618f) * 0.5f + 0.5f;
                float ny = Mathf.Sin(i * 2.618f) * 0.5f + 0.5f;
                float nz = Mathf.Sin(i * 4.236f) * 0.5f + 0.5f;

                // Cluster around certain areas (storm cells)
                Vector3 cell1 = new Vector3(0.3f, 0.4f, 0.3f);
                Vector3 cell2 = new Vector3(0.7f, 0.5f, 0.6f);

                float dist1 = Vector3.Distance(new Vector3(nx, ny, nz), cell1);
                float dist2 = Vector3.Distance(new Vector3(nx, ny, nz), cell2);

                float density = Mathf.Max(
                    Mathf.Clamp01(1f - dist1 * 3f),
                    Mathf.Clamp01(1f - dist2 * 3f)
                );

                if (density > 0.1f)
                {
                    Vector3 pos = center + new Vector3(
                        (nx - 0.5f) * size.x,
                        (ny - 0.5f) * size.y,
                        (nz - 0.5f) * size.z
                    );

                    // Color based on density
                    Color color;
                    if (density < 0.3f)
                        color = new Color(0.2f, 0.9f, 0.2f, 0.3f); // Green
                    else if (density < 0.5f)
                        color = new Color(1f, 0.95f, 0.1f, 0.4f); // Yellow
                    else if (density < 0.7f)
                        color = new Color(1f, 0.5f, 0.1f, 0.5f); // Orange
                    else
                        color = new Color(1f, 0.15f, 0.15f, 0.6f); // Red

                    Handles.color = color;

                    // Draw sphere at this position
                    float drawSize = voxelSize * (0.5f + density);
                    Handles.SphereHandleCap(0, pos, Quaternion.identity, drawSize, EventType.Repaint);
                }
            }
        }

        private static Mesh GetCubeMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "PreviewCube";

            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f)
            };

            int[] triangles = new int[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                2, 3, 7, 2, 7, 6,
                0, 4, 7, 0, 7, 3,
                1, 2, 6, 1, 6, 5
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
