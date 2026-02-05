using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Preview window for weather visualization effects.
    /// Allows configuring and visualizing weather without entering Play mode.
    /// </summary>
    public class WeatherPreviewWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private bool autoRefresh = true;
        private float refreshInterval = 0.5f;
        private double lastRefreshTime;

        // Preview toggles
        private bool showCloudVolume = true;
        private bool showIntensityPillars = true;
        private bool showPrecipitation = true;
        private bool showLightning = true;
        private bool showStormCells = true;

        // Preview settings
        private int pillarPreviewCount = 5;
        private float previewScale = 1f;
        private Color cloudColor = new Color(0.3f, 0.7f, 1f, 0.5f);
        private Color pillarColor = new Color(1f, 0.5f, 0f, 0.5f);

        // Storm cell preview data
        private List<PreviewStormCell> previewCells = new List<PreviewStormCell>();
        private bool regenerateCells = true;

        private class PreviewStormCell
        {
            public Vector3 position;
            public float intensity;
            public float radius;
            public float height;
            public WeatherType type;
        }

        [MenuItem("Tools/Weather Visualization/Preview/Preview Window", false, 401)]
        public static void ShowWindow()
        {
            var window = GetWindow<WeatherPreviewWindow>();
            window.titleContent = new GUIContent("Weather Preview");
            window.minSize = new Vector2(350, 500);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            GeneratePreviewCells();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawGlobalSettings();
            EditorGUILayout.Space(10);

            DrawVisibilityToggles();
            EditorGUILayout.Space(10);

            DrawStormCellPreview();
            EditorGUILayout.Space(10);

            DrawActions();
            EditorGUILayout.Space(10);

            DrawStats();

            EditorGUILayout.EndScrollView();

            // Auto refresh
            if (autoRefresh && EditorApplication.timeSinceStartup - lastRefreshTime > refreshInterval)
            {
                lastRefreshTime = EditorApplication.timeSinceStartup;
                SceneView.RepaintAll();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };

            EditorGUILayout.LabelField("Weather Preview", titleStyle);
            EditorGUILayout.LabelField("Visualize effects in Scene view without Play mode", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawGlobalSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Global Settings", EditorStyles.boldLabel);

            autoRefresh = EditorGUILayout.Toggle("Auto Refresh", autoRefresh);
            if (autoRefresh)
            {
                refreshInterval = EditorGUILayout.Slider("Refresh Interval", refreshInterval, 0.1f, 2f);
            }

            previewScale = EditorGUILayout.Slider("Preview Scale", previewScale, 0.1f, 2f);

            EditorGUILayout.EndVertical();
        }

        private void DrawVisibilityToggles()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Visibility", EditorStyles.boldLabel);

            showCloudVolume = EditorGUILayout.ToggleLeft("Cloud Volume Bounds", showCloudVolume);
            showStormCells = EditorGUILayout.ToggleLeft("Storm Cells", showStormCells);
            showIntensityPillars = EditorGUILayout.ToggleLeft("Intensity Pillars", showIntensityPillars);
            showPrecipitation = EditorGUILayout.ToggleLeft("Precipitation Areas", showPrecipitation);
            showLightning = EditorGUILayout.ToggleLeft("Lightning Zones", showLightning);

            EditorGUILayout.EndVertical();
        }

        private void DrawStormCellPreview()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Storm Cell Preview", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Generate preview storm cells to visualize how the system will look with data.",
                MessageType.Info);

            regenerateCells = EditorGUILayout.Toggle("Regenerate on Change", regenerateCells);
            pillarPreviewCount = EditorGUILayout.IntSlider("Cell Count", pillarPreviewCount, 1, 20);

            cloudColor = EditorGUILayout.ColorField("Cloud Color", cloudColor);
            pillarColor = EditorGUILayout.ColorField("Pillar Color", pillarColor);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Cells"))
            {
                GeneratePreviewCells();
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Clear Cells"))
            {
                previewCells.Clear();
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            // Cell list
            if (previewCells.Count > 0)
            {
                EditorGUILayout.LabelField($"Active Cells: {previewCells.Count}", EditorStyles.boldLabel);
                foreach (var cell in previewCells)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Pos: {cell.position}", GUILayout.Width(150));
                    EditorGUILayout.LabelField($"Intensity: {cell.intensity:P0}");
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Scene View"))
            {
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Focus on Weather"))
            {
                FocusOnWeather();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Create Weather System", GUILayout.Height(30)))
            {
                VolumetricWeatherSetupWizard.ShowWindow();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStats()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Scene Status", EditorStyles.boldLabel);

            var manager = FindObjectOfType<VolumetricWeatherManager>();
            var simulator = FindObjectOfType<WeatherSimulator>();
            var volumes = FindObjectsOfType<VolumetricCloudVolume>();
            var pillars = FindObjectsOfType<IntensityPillarRenderer>();
            var lightning = FindObjectsOfType<VolumetricLightning>();
            var precipitation = FindObjectsOfType<PrecipitationVFX>();

            EditorGUILayout.LabelField($"Weather Manager: {(manager != null ? "✓" : "✗")}");
            EditorGUILayout.LabelField($"Simulator: {(simulator != null ? "✓" : "✗")}");
            EditorGUILayout.LabelField($"Cloud Volumes: {volumes.Length}");
            EditorGUILayout.LabelField($"Pillar Renderers: {pillars.Length}");
            EditorGUILayout.LabelField($"Lightning Effects: {lightning.Length}");
            EditorGUILayout.LabelField($"Precipitation FX: {precipitation.Length}");

            EditorGUILayout.EndVertical();
        }

        private void GeneratePreviewCells()
        {
            previewCells.Clear();

            Vector3 origin = GetWeatherOrigin();
            float areaSize = 50000f;

            for (int i = 0; i < pillarPreviewCount; i++)
            {
                float angle = (i / (float)pillarPreviewCount) * Mathf.PI * 2f;
                float distance = Random.Range(5000f, areaSize * 0.4f);

                Vector3 pos = origin + new Vector3(
                    Mathf.Cos(angle) * distance,
                    0,
                    Mathf.Sin(angle) * distance
                );

                float intensity = Random.value;
                float radius = Mathf.Lerp(2000f, 8000f, intensity);
                float height = Mathf.Lerp(10000f, 40000f, intensity);

                previewCells.Add(new PreviewStormCell
                {
                    position = pos,
                    intensity = intensity,
                    radius = radius,
                    height = height,
                    type = GetWeatherTypeFromIntensity(intensity)
                });
            }
        }

        private Vector3 GetWeatherOrigin()
        {
            var manager = FindObjectOfType<VolumetricWeatherManager>();
            if (manager != null)
                return manager.transform.position;

            var volume = FindObjectOfType<VolumetricCloudVolume>();
            if (volume != null)
                return volume.transform.position;

            return Vector3.zero;
        }

        private WeatherType GetWeatherTypeFromIntensity(float intensity)
        {
            if (intensity < 0.3f) return WeatherType.LightRain;
            if (intensity < 0.6f) return WeatherType.ModerateRain;
            if (intensity < 0.8f) return WeatherType.HeavyRain;
            return WeatherType.Thunderstorm;
        }

        private void FocusOnWeather()
        {
            var manager = FindObjectOfType<VolumetricWeatherManager>();
            if (manager != null)
            {
                Selection.activeGameObject = manager.gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
                return;
            }

            var volume = FindObjectOfType<VolumetricCloudVolume>();
            if (volume != null)
            {
                Selection.activeGameObject = volume.gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!showCloudVolume && !showIntensityPillars && !showPrecipitation && !showLightning && !showStormCells)
                return;

            // Draw based on toggles
            if (showCloudVolume)
                DrawCloudVolumePreviews();

            if (showStormCells)
                DrawStormCellPreviews();

            if (showIntensityPillars)
                DrawIntensityPillarPreviews();

            if (showPrecipitation)
                DrawPrecipitationPreviews();

            if (showLightning)
                DrawLightningPreviews();
        }

        private void DrawCloudVolumePreviews()
        {
            var volumes = FindObjectsOfType<VolumetricCloudVolume>();
            foreach (var volume in volumes)
            {
                if (volume == null) continue;

                Vector3 center = volume.transform.position;
                Vector3 size = GetVolumeSize(volume) * previewScale;

                // Draw wireframe
                Handles.color = cloudColor;
                Handles.DrawWireCube(center, size);

                // Draw filled
                Color fillColor = cloudColor;
                fillColor.a = 0.05f;
                Handles.color = fillColor;
                DrawWireCubeFilled(center, size);

                // Draw label
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                style.normal.textColor = cloudColor;
                Handles.Label(center + Vector3.up * size.y * 0.55f, "Cloud Volume", style);
            }
        }

        private void DrawStormCellPreviews()
        {
            foreach (var cell in previewCells)
            {
                // Draw cell area
                Color color = GetIntensityColor(cell.intensity);
                color.a = 0.3f;

                Handles.color = color;
                Handles.DrawWireDisc(cell.position, Vector3.up, cell.radius);

                // Draw height indicator
                Handles.DrawLine(cell.position, cell.position + Vector3.up * cell.height);
                Handles.DrawWireDisc(cell.position + Vector3.up * cell.height, Vector3.up, cell.radius * 0.7f);

                // Draw label
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                style.normal.textColor = color;
                style.fontSize = 10;
                Handles.Label(cell.position + Vector3.up * cell.height, $"{cell.type} ({cell.intensity:P0})", style);
            }
        }

        private void DrawIntensityPillarPreviews()
        {
            foreach (var cell in previewCells)
            {
                if (cell.intensity < 0.2f) continue;

                Color color = GetIntensityColor(cell.intensity);
                float radius = cell.radius * 0.3f;

                // Draw pillar
                Handles.color = color;
                DrawWireCylinder(cell.position, radius, cell.height);

                // Draw glow
                color.a = 0.1f;
                Handles.color = color;
                DrawWireCylinder(cell.position, radius * 1.3f, cell.height);
            }
        }

        private void DrawPrecipitationPreviews()
        {
            foreach (var cell in previewCells)
            {
                if (cell.intensity < 0.3f) continue;

                Vector3 area = new Vector3(cell.radius * 2f, 2000f, cell.radius * 2f);
                Vector3 center = cell.position + Vector3.up * area.y * 0.5f;

                Handles.color = new Color(0.5f, 0.7f, 0.9f, 0.2f);
                DrawWireCubeFilled(center, area);

                Handles.color = new Color(0.5f, 0.7f, 0.9f, 0.4f);
                Handles.DrawWireCube(center, area);
            }
        }

        private void DrawLightningPreviews()
        {
            foreach (var cell in previewCells)
            {
                if (cell.intensity < 0.6f) continue;

                // Draw strike zone
                Handles.color = new Color(1f, 1f, 0.3f, 0.1f);
                Handles.DrawSolidDisc(cell.position, Vector3.up, cell.radius * 1.5f);

                Handles.color = new Color(1f, 1f, 0.3f, 0.4f);
                Handles.DrawWireDisc(cell.position, Vector3.up, cell.radius * 1.5f);

                // Draw sample bolt
                DrawLightningBolt(cell.position + new Vector3(0, cell.height * 0.8f, 0), cell.height * 0.6f);
            }
        }

        private Color GetIntensityColor(float intensity)
        {
            if (intensity < 0.3f)
                return new Color(0f, 1f, 0f, 0.5f);
            if (intensity < 0.6f)
                return new Color(1f, 1f, 0f, 0.5f);
            if (intensity < 0.8f)
                return new Color(1f, 0.5f, 0f, 0.5f);
            return new Color(1f, 0f, 0.5f, 0.5f);
        }

        private Vector3 GetVolumeSize(VolumetricCloudVolume volume)
        {
            var field = volume.GetType().GetField("volumeSize",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (field != null)
                return (Vector3)field.GetValue(volume);

            return new Vector3(50000f, 50000f, 50000f);
        }

        private void DrawWireCubeFilled(Vector3 center, Vector3 size)
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

            Handles.DrawAAConvexPolygon(vertices[0], vertices[1], vertices[2], vertices[3]);
            Handles.DrawAAConvexPolygon(vertices[4], vertices[7], vertices[6], vertices[5]);
        }

        private void DrawWireCylinder(Vector3 baseCenter, float radius, float height)
        {
            int segments = 16;
            float angleStep = 360f / segments;

            Vector3 topCenter = baseCenter + Vector3.up * height;

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

        private void DrawLightningBolt(Vector3 start, float length)
        {
            Handles.color = new Color(0.9f, 0.95f, 1f, 0.8f);

            Vector3 current = start;
            int segments = 6;
            float segmentLength = length / segments;

            for (int i = 0; i < segments; i++)
            {
                Vector3 next = current + new Vector3(
                    Random.Range(-300f, 300f),
                    -segmentLength,
                    Random.Range(-300f, 300f)
                );

                Handles.DrawLine(current, next, 2f);
                current = next;
            }
        }
    }
}
