using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Provides Scene view preview/gizmos for weather visualization components.
    /// Allows visualizing weather effects in Edit mode without entering Play mode.
    /// </summary>
    [InitializeOnLoad]
    public static class VolumetricWeatherPreview
    {
        private static bool previewEnabled = true;
        private static Dictionary<MonoBehaviour, PreviewState> previewStates = new Dictionary<MonoBehaviour, PreviewState>();

        private class PreviewState
        {
            public bool showVolume = true;
            public bool showCells = true;
            public bool showPillars = true;
            public bool showPrecipitation = true;
            public Color previewColor = Color.cyan;
        }

        static VolumetricWeatherPreview()
        {
            // Register for Scene view updates
            SceneView.duringSceneGui += OnSceneGUI;

            // Load preferences
            previewEnabled = EditorPrefs.GetBool("WeatherPreview_Enabled", true);
        }

        [MenuItem("Tools/Weather Visualization/Preview/Toggle Scene View Preview", false, 400)]
        public static void TogglePreview()
        {
            previewEnabled = !previewEnabled;
            EditorPrefs.SetBool("WeatherPreview_Enabled", previewEnabled);
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/Weather Visualization/Preview/Toggle Scene View Preview", true)]
        public static bool TogglePreviewValidate()
        {
            Menu.SetChecked("Tools/Weather Visualization/Preview/Toggle Scene View Preview", previewEnabled);
            return true;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!previewEnabled) return;

            // Draw previews for all weather components
            DrawVolumetricCloudVolumePreviews();
            DrawIntensityPillarPreviews();
            DrawPrecipitationPreviews();
            DrawLightningPreviews();
        }

        #region Volumetric Cloud Volume Preview

        private static void DrawVolumetricCloudVolumePreviews()
        {
            var volumes = Object.FindObjectsOfType<VolumetricCloudVolume>();
            foreach (var volume in volumes)
            {
                if (volume == null) continue;

                DrawVolumeBounds(volume);
            }
        }

        private static void DrawVolumeBounds(VolumetricCloudVolume volume)
        {
            Vector3 center = volume.transform.position;
            Vector3 size = GetVolumeSize(volume);

            // Draw wireframe box
            Handles.color = new Color(0.3f, 0.7f, 1f, 0.5f);
            Handles.DrawWireCube(center, size);

            // Draw filled semi-transparent box
            Handles.color = new Color(0.3f, 0.7f, 1f, 0.05f);
            DrawWireCubeFilled(center, size);

            // Draw labels
            DrawVolumeLabel(center, "Cloud Volume", size);

            // Draw corner handles for editing
            DrawVolumeHandles(volume, center, size);
        }

        private static Vector3 GetVolumeSize(VolumetricCloudVolume volume)
        {
            // Try to get size from the component via reflection
            var sizeField = volume.GetType().GetField("volumeSize",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (sizeField != null)
            {
                return (Vector3)sizeField.GetValue(volume);
            }

            // Default size
            return new Vector3(50000f, 50000f, 50000f);
        }

        private static void DrawVolumeHandles(VolumetricCloudVolume volume, Vector3 center, Vector3 size)
        {
            EditorGUI.BeginChangeCheck();

            Vector3 newCenter = Handles.PositionHandle(center, Quaternion.identity);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(volume.transform, "Move Cloud Volume");
                volume.transform.position = newCenter;
            }
        }

        #endregion

        #region Intensity Pillar Preview

        private static void DrawIntensityPillarPreviews()
        {
            var renderers = Object.FindObjectsOfType<IntensityPillarRenderer>();
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                // Get preview cells from the renderer
                var previewCells = GetPreviewCells(renderer);

                foreach (var cell in previewCells)
                {
                    DrawPillarPreview(cell.position, cell.intensity, cell.height);
                }
            }
        }

        private static List<PreviewCell> GetPreviewCells(IntensityPillarRenderer renderer)
        {
            var cells = new List<PreviewCell>();

            // Try to get cells from the renderer
            var cellsField = renderer.GetType().GetField("pillarInstances",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (cellsField != null)
            {
                var instances = cellsField.GetValue(renderer) as System.Collections.IList;
                if (instances != null)
                {
                    foreach (var instance in instances)
                    {
                        // Extract data from pillar instance
                        var positionProp = instance.GetType().GetField("position");
                        var intensityProp = instance.GetType().GetField("intensity");

                        if (positionProp != null && intensityProp != null)
                        {
                            cells.Add(new PreviewCell
                            {
                                position = (Vector3)positionProp.GetValue(instance),
                                intensity = (float)intensityProp.GetValue(instance),
                                height = 10000f
                            });
                        }
                    }
                }
            }

            // If no cells, show preview pillars at sample positions
            if (cells.Count == 0)
            {
                Vector3 origin = renderer.transform.position;
                cells.Add(new PreviewCell { position = origin + new Vector3(-5000, 0, -5000), intensity = 0.3f, height = 15000f });
                cells.Add(new PreviewCell { position = origin + new Vector3(5000, 0, -5000), intensity = 0.6f, height = 25000f });
                cells.Add(new PreviewCell { position = origin + new Vector3(0, 0, 5000), intensity = 0.9f, height = 40000f });
            }

            return cells;
        }

        private static void DrawPillarPreview(Vector3 position, float intensity, float height)
        {
            // Color based on intensity
            Color color;
            if (intensity < 0.3f)
                color = new Color(0f, 1f, 0f, 0.3f); // Green - Light
            else if (intensity < 0.6f)
                color = new Color(1f, 1f, 0f, 0.4f); // Yellow - Moderate
            else if (intensity < 0.8f)
                color = new Color(1f, 0.5f, 0f, 0.5f); // Orange - Heavy
            else
                color = new Color(1f, 0f, 0.5f, 0.6f); // Magenta - Extreme

            float radius = Mathf.Lerp(500f, 2000f, intensity);

            // Draw pillar cylinder
            Handles.color = color;
            DrawWireCylinder(position, radius, height);

            // Draw glow
            Handles.color = new Color(color.r, color.g, color.b, 0.1f);
            DrawWireCylinder(position, radius * 1.5f, height);

            // Draw label
            DrawLabel(position + Vector3.up * height * 0.5f, $"{intensity:P0} Intensity", color);
        }

        #endregion

        #region Precipitation Preview

        private static void DrawPrecipitationPreviews()
        {
            var effects = Object.FindObjectsOfType<PrecipitationVFX>();
            foreach (var effect in effects)
            {
                if (effect == null) continue;

                Vector3 center = effect.transform.position;
                Vector3 size = GetPrecipitationArea(effect);

                // Draw precipitation area
                Handles.color = new Color(0.5f, 0.7f, 0.9f, 0.2f);
                DrawWireCubeFilled(center + Vector3.up * size.y * 0.5f, size);

                Handles.color = new Color(0.5f, 0.7f, 0.9f, 0.5f);
                Handles.DrawWireCube(center + Vector3.up * size.y * 0.5f, size);

                // Draw precipitation particles preview
                DrawPrecipitationParticles(center, size);

                DrawLabel(center + Vector3.up * size.y, "Precipitation Area", Color.cyan);
            }
        }

        private static Vector3 GetPrecipitationArea(PrecipitationVFX effect)
        {
            var areaField = effect.GetType().GetField("spawnAreaSize",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (areaField != null)
            {
                return (Vector3)areaField.GetValue(effect);
            }

            return new Vector3(5000f, 2000f, 5000f);
        }

        private static void DrawPrecipitationParticles(Vector3 center, Vector3 size)
        {
            // Draw sample particles
            int particleCount = 20;
            Handles.color = new Color(0.7f, 0.8f, 1f, 0.6f);

            for (int i = 0; i < particleCount; i++)
            {
                float x = Mathf.Sin(i * 0.5f + Time.realtimeSinceStartup * 0.1f) * size.x * 0.4f;
                float z = Mathf.Cos(i * 0.7f + Time.realtimeSinceStartup * 0.15f) * size.z * 0.4f;
                float y = (i % 5) * size.y * 0.2f;

                Vector3 pos = center + new Vector3(x, y + size.y * 0.5f, z);
                Handles.DrawSolidDisc(pos, Vector3.up, 30f);
            }
        }

        #endregion

        #region Lightning Preview

        private static void DrawLightningPreviews()
        {
            var lightningEffects = Object.FindObjectsOfType<VolumetricLightning>();
            foreach (var effect in lightningEffects)
            {
                if (effect == null) continue;

                Vector3 center = effect.transform.position;

                // Draw strike radius
                Handles.color = new Color(1f, 1f, 0.3f, 0.1f);
                Handles.DrawSolidDisc(center, Vector3.up, 15000f);

                Handles.color = new Color(1f, 1f, 0.3f, 0.3f);
                Handles.DrawWireDisc(center, Vector3.up, 15000f);

                // Draw sample lightning bolt
                DrawSampleLightningBolt(center + new Vector3(2000, 10000, 2000), 8000f);

                DrawLabel(center + Vector3.up * 1500f, "Lightning Strike Zone", Color.yellow);
            }
        }

        private static void DrawSampleLightningBolt(Vector3 start, float length)
        {
            Handles.color = new Color(0.9f, 0.95f, 1f, 0.8f);

            Vector3 current = start;
            int segments = 8;
            float segmentLength = length / segments;

            for (int i = 0; i < segments; i++)
            {
                Vector3 next = current + new Vector3(
                    RandomOffset(500f),
                    -segmentLength,
                    RandomOffset(500f)
                );

                Handles.DrawLine(current, next, 3f);

                // Draw glow
                Handles.color = new Color(0.6f, 0.7f, 1f, 0.2f);
                Handles.DrawLine(current, next, 15f);
                Handles.color = new Color(0.9f, 0.95f, 1f, 0.8f);

                current = next;
            }
        }

        #endregion

        #region Utility Drawing Methods

        private static void DrawWireCubeFilled(Vector3 center, Vector3 size)
        {
            Vector3 half = size * 0.5f;

            Vector3[] vertices = new Vector3[8];
            vertices[0] = center + new Vector3(-half.x, -half.y, -half.z);
            vertices[1] = center + new Vector3(half.x, -half.y, -half.z);
            vertices[2] = center + new Vector3(half.x, -half.y, half.z);
            vertices[3] = center + new Vector3(-half.x, -half.y, half.z);
            vertices[4] = center + new Vector3(-half.x, half.y, -half.z);
            vertices[5] = center + new Vector3(half.x, half.y, -half.z);
            vertices[6] = center + new Vector3(half.x, half.y, half.z);
            vertices[7] = center + new Vector3(-half.x, half.y, half.z);

            // Draw faces with transparent quads
            Handles.DrawAAConvexPolygon(vertices[0], vertices[1], vertices[2], vertices[3]); // Bottom
            Handles.DrawAAConvexPolygon(vertices[4], vertices[7], vertices[6], vertices[5]); // Top
            Handles.DrawAAConvexPolygon(vertices[0], vertices[4], vertices[5], vertices[1]); // Front
            Handles.DrawAAConvexPolygon(vertices[2], vertices[6], vertices[7], vertices[3]); // Back
            Handles.DrawAAConvexPolygon(vertices[0], vertices[3], vertices[7], vertices[4]); // Left
            Handles.DrawAAConvexPolygon(vertices[1], vertices[5], vertices[6], vertices[2]); // Right
        }

        private static void DrawWireCylinder(Vector3 baseCenter, float radius, float height)
        {
            int segments = 32;
            float angleStep = 360f / segments;

            Vector3 topCenter = baseCenter + Vector3.up * height;

            // Draw bottom and top circles
            Vector3 prevBottom = baseCenter + new Vector3(radius, 0, 0);
            Vector3 prevTop = topCenter + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);

                Vector3 nextBottom = baseCenter + offset;
                Vector3 nextTop = topCenter + offset;

                Handles.DrawLine(prevBottom, nextBottom);
                Handles.DrawLine(prevTop, nextTop);
                Handles.DrawLine(nextBottom, nextTop);

                prevBottom = nextBottom;
                prevTop = nextTop;
            }
        }

        private static void DrawVolumeLabel(Vector3 position, string text, Vector3 size)
        {
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = new Color(0.3f, 0.7f, 1f, 1f);
            style.fontSize = 11;

            Handles.Label(position + Vector3.up * size.y * 0.55f, text, style);

            // Draw dimensions
            style.fontSize = 9;
            style.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            string dims = $"{size.x/1000:F0}km x {size.y/1000:F0}km x {size.z/1000:F0}km";
            Handles.Label(position + Vector3.up * size.y * 0.5f, dims, style);
        }

        private static void DrawLabel(Vector3 position, string text, Color color)
        {
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = color;
            style.fontSize = 10;

            Handles.Label(position, text, style);
        }

        private static float RandomOffset(float max)
        {
            return (Random.value - 0.5f) * 2f * max;
        }

        private struct PreviewCell
        {
            public Vector3 position;
            public float intensity;
            public float height;
        }

        #endregion
    }
}
