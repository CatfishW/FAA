using UnityEngine;
using UnityEditor;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Scene view renderer for volumetric clouds - handles the actual drawing in Scene view
    /// </summary>
    [InitializeOnLoad]
    public static class CloudVolumeSceneRenderer
    {
        private static bool isRegistered = false;

        static CloudVolumeSceneRenderer()
        {
            Register();
        }

        public static void Register()
        {
            if (isRegistered) return;

            SceneView.duringSceneGui += OnSceneGUI;
            isRegistered = true;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            // Find all cloud volumes
            var volumes = Object.FindObjectsOfType<VolumetricCloudVolume>();

            foreach (var volume in volumes)
            {
                if (volume == null) continue;

                DrawCloudVolumeInSceneView(volume, sceneView);
            }
        }

        private static void DrawCloudVolumeInSceneView(VolumetricCloudVolume volume, SceneView sceneView)
        {
            var meshFilter = volume.GetComponent<MeshFilter>();
            var meshRenderer = volume.GetComponent<MeshRenderer>();

            if (meshFilter == null || meshFilter.sharedMesh == null) return;
            if (meshRenderer == null || !meshRenderer.enabled) return;

            var material = meshRenderer.sharedMaterial;
            if (material == null) return;

            // Get or generate preview texture
            var texture = GetOrCreatePreviewTexture(volume, material);
            if (texture == null) return;

            // Get volume bounds
            var sizeField = volume.GetType().GetField("_volumeSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Vector3 size = sizeField != null ? (Vector3)sizeField.GetValue(volume) : new Vector3(50000f, 15000f, 50000f);

            Vector3 center = volume.transform.position;
            Vector3 min = center - size * 0.5f;
            Vector3 max = center + size * 0.5f;

            // Set material properties
            material.SetTexture("_DensityVolume", texture);
            material.SetVector("_VolumeMin", min);
            material.SetVector("_VolumeMax", max);
            material.SetFloat("_AlphaScale", 3f);

            // Draw wireframe bounds
            Handles.color = new Color(0.3f, 0.7f, 1f, 0.3f);
            Handles.DrawWireCube(center, size);

            // Draw the mesh
            Matrix4x4 matrix = volume.transform.localToWorldMatrix;

            // Save state
            var prevMatrix = Handles.matrix;
            Handles.matrix = matrix;

            // Try to render with material
            if (material.SetPass(0))
            {
                Graphics.DrawMeshNow(meshFilter.sharedMesh, matrix);
            }
            else
            {
                // Fallback: draw wireframe with density-based coloring
                DrawFallbackVisualization(center, size, texture);
            }

            // Restore state
            Handles.matrix = prevMatrix;

            // Draw label
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = new Color(0.3f, 0.7f, 1f);
            style.fontSize = 11;
            Handles.Label(center + Vector3.up * size.y * 0.55f, $"Cloud Volume\n{size.x/1000:F0}km x {size.y/1000:F0}km x {size.z/1000:F0}km", style);
        }

        private static Texture3D GetOrCreatePreviewTexture(VolumetricCloudVolume volume, Material material)
        {
            // Check if material already has a valid texture
            var existingTex = material.GetTexture("_DensityVolume") as Texture3D;
            if (existingTex != null)
            {
                return existingTex;
            }

            // Generate new texture
            var texture = GenerateProceduralCloudTexture();
            material.SetTexture("_DensityVolume", texture);

            return texture;
        }

        private static Texture3D GenerateProceduralCloudTexture()
        {
            int size = 64;
            Texture3D tex = new Texture3D(size, size, size, TextureFormat.RGBA32, false);
            tex.name = "ProceduralCloudDensity";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size * size];

            // Generate storm cells
            Vector3[] centers = new Vector3[]
            {
                new Vector3(size * 0.3f, size * 0.4f, size * 0.3f),
                new Vector3(size * 0.7f, size * 0.5f, size * 0.6f),
                new Vector3(size * 0.5f, size * 0.6f, size * 0.5f)
            };

            float[] radii = new float[] { size * 0.15f, size * 0.12f, size * 0.1f };
            float[] intensities = new float[] { 0.8f, 0.9f, 0.95f };

            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int idx = x + y * size + z * size * size;
                        Vector3 pos = new Vector3(x, y, z);

                        float density = 0f;

                        // Sample each storm cell
                        for (int c = 0; c < centers.Length; c++)
                        {
                            float dist = Vector3.Distance(pos, centers[c]);
                            if (dist < radii[c])
                            {
                                float t = dist / radii[c];
                                float falloff = 1f - t * t;

                                // Height falloff
                                float heightFactor = 1f - Mathf.Abs(y - centers[c].y) / (radii[c] * 2f);
                                heightFactor = Mathf.Max(0, heightFactor);

                                // Noise
                                float noise = Mathf.PerlinNoise(x * 0.1f + c * 10, z * 0.1f) *
                                              Mathf.PerlinNoise(y * 0.1f, x * 0.1f + c * 5);
                                noise = noise * 0.5f + 0.5f;

                                float cellDensity = intensities[c] * falloff * heightFactor * noise;
                                density = Mathf.Max(density, cellDensity);
                            }
                        }

                        // Boost density
                        density = Mathf.Clamp01(density * 2.5f);

                        pixels[idx] = new Color(density, density, density, 1f);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Debug.Log($"[CloudVolumeSceneRenderer] Generated procedural cloud texture with {size}x{size}x{size} resolution");

            return tex;
        }

        private static void DrawFallbackVisualization(Vector3 center, Vector3 size, Texture3D texture)
        {
            // Draw as a semi-transparent box
            Handles.color = new Color(0.5f, 0.7f, 0.9f, 0.1f);

            Vector3 half = size * 0.5f;
            Vector3[] corners = new Vector3[8];
            corners[0] = center + new Vector3(-half.x, -half.y, -half.z);
            corners[1] = center + new Vector3(half.x, -half.y, -half.z);
            corners[2] = center + new Vector3(half.x, -half.y, half.z);
            corners[3] = center + new Vector3(-half.x, -half.y, half.z);
            corners[4] = center + new Vector3(-half.x, half.y, -half.z);
            corners[5] = center + new Vector3(half.x, half.y, -half.z);
            corners[6] = center + new Vector3(half.x, half.y, half.z);
            corners[7] = center + new Vector3(-half.x, half.y, half.z);

            // Draw faces
            Handles.DrawAAConvexPolygon(corners[0], corners[1], corners[5], corners[4]);
            Handles.DrawAAConvexPolygon(corners[1], corners[2], corners[6], corners[5]);
            Handles.DrawAAConvexPolygon(corners[2], corners[3], corners[7], corners[6]);
            Handles.DrawAAConvexPolygon(corners[3], corners[0], corners[4], corners[7]);
            Handles.DrawAAConvexPolygon(corners[4], corners[5], corners[6], corners[7]);
            Handles.DrawAAConvexPolygon(corners[0], corners[3], corners[2], corners[1]);
        }
    }
}
