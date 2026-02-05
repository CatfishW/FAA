using UnityEngine;
using UnityEditor;

namespace WeatherVisualization3D.Editor
{
    /// <summary>
    /// Provides Scene view preview rendering for volumetric clouds
    /// Works in Edit mode without entering Play mode
    /// Compatible with URP and Built-in RP (SRP)
    /// </summary>
    [InitializeOnLoad]
    public class VolumetricCloudScenePreview
    {
        private static bool isRegistered = false;
        private static Material previewMaterial;
        private static Mesh cubeMesh;

        static VolumetricCloudScenePreview()
        {
            Register();
        }

        public static void Register()
        {
            if (isRegistered) return;

            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += OnEditorUpdate;
            isRegistered = true;
        }

        private static void OnEditorUpdate()
        {
            // Force Scene view repaint for animation
            if (Application.isPlaying) return;

            // Only repaint occasionally to save performance
            if (EditorApplication.timeSinceStartup % 0.1 < 0.05)
            {
                SceneView.RepaintAll();
            }
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (Application.isPlaying) return;

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
            var meshRenderer = volume.GetComponent<MeshRenderer>();
            if (meshRenderer == null || !meshRenderer.enabled) return;

            var material = meshRenderer.sharedMaterial;
            if (material == null) return;

            // Get volume bounds
            var sizeField = volume.GetType().GetField("_volumeSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Vector3 size = sizeField != null ? (Vector3)sizeField.GetValue(volume) : new Vector3(50000f, 15000f, 50000f);

            Vector3 center = volume.transform.position;
            Vector3 min = center - size * 0.5f;
            Vector3 max = center + size * 0.5f;

            // Update material properties
            material.SetVector("_VolumeMin", min);
            material.SetVector("_VolumeMax", max);

            // Set time for animation preview
            material.SetFloat("_Time.y", (float)EditorApplication.timeSinceStartup);

            // Draw bounds
            if (meshRenderer != null && Selection.activeGameObject == volume.gameObject)
            {
                Handles.color = new Color(0.3f, 0.7f, 1f, 0.5f);
                Handles.DrawWireCube(center, size);

                // Draw label
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                style.normal.textColor = new Color(0.3f, 0.7f, 1f);
                style.fontSize = 12;
                Handles.Label(center + Vector3.up * size.y * 0.55f,
                    $"☁ Cloud Volume\n{size.x / 1000:F0}km × {size.y / 1000:F0}km × {size.z / 1000:F0}km", style);
            }

            // Trigger render
            if (material.SetPass(0))
            {
                var meshFilter = volume.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    Graphics.DrawMeshNow(meshFilter.sharedMesh, volume.transform.localToWorldMatrix);
                }
            }
        }
    }
}
