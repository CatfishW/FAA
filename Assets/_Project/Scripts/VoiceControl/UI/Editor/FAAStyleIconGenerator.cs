using UnityEngine;
using UnityEditor;
using System.IO;

namespace VoiceControl.UI.Editor
{
    /// <summary>
    /// Generates FAA-styled professional aviation icons for the radial menu.
    /// Uses clean, minimal design with monochrome palette and clear symbology.
    /// </summary>
    public class FAAStyleIconGenerator : EditorWindow
    {
        private const string MENU_PATH = "Tools/Aviation/Voice Control/Generate FAA Icons";
        private const string OUTPUT_PATH = "Assets/Resources/VoiceControl/Icons";

        [MenuItem(MENU_PATH)]
        public static void ShowWindow()
        {
            GetWindow<FAAStyleIconGenerator>("FAA Icon Generator");
        }

        [MenuItem("Tools/Aviation/Voice Control/Generate FAA Icons (Auto)")]
        private static void GenerateAllIconsAuto()
        {
            var generator = ScriptableObject.CreateInstance<FAAStyleIconGenerator>();
            generator.GenerateAllIcons();
            Object.DestroyImmediate(generator);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("FAA-Styled Icon Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Generates clean, professional FAA-styled icons for the voice command radial menu. " +
                "Icons use minimal colors, high contrast, and aviation-standard symbology.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Generate All Icons", GUILayout.Height(40)))
            {
                GenerateAllIcons();
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Individual Icons:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Weather Radar"))
            {
                GenerateWeatherRadarIcon();
            }
            if (GUILayout.Button("Traffic Radar"))
            {
                GenerateTrafficRadarIcon();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Indicator System"))
            {
                GenerateIndicatorIcon();
            }
            if (GUILayout.Button("Symbology"))
            {
                GenerateSymbologyIcon();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Vision Briefing"))
            {
                GenerateVisionIcon();
            }
            if (GUILayout.Button("System"))
            {
                GenerateSystemIcon();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void GenerateAllIcons()
        {
            EnsureDirectoryExists();

            GenerateWeatherRadarIcon();
            GenerateTrafficRadarIcon();
            GenerateIndicatorIcon();
            GenerateSymbologyIcon();
            GenerateVisionIcon();
            GenerateSystemIcon();

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", "All FAA-styled icons generated successfully!", "OK");
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(OUTPUT_PATH))
            {
                Directory.CreateDirectory(OUTPUT_PATH);
            }
        }

        /// <summary>
        /// Generates a weather radar icon - circular display with sweep arc and precipitation
        /// </summary>
        private void GenerateWeatherRadarIcon()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            // Clear with transparent
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Color lineColor = new Color(0.9f, 0.9f, 0.95f, 1f); // Off-white
            Color accentColor = new Color(0.4f, 0.7f, 1f, 0.9f); // Light blue

            int center = size / 2;
            int radius = size * 3 / 8;

            // Draw outer circle (radar screen boundary)
            DrawCircle(pixels, size, center, center, radius, lineColor, 3f);

            // Draw crosshairs
            DrawLine(pixels, size, center, center - radius + 10, center, center + radius - 10, lineColor, 2f);
            DrawLine(pixels, size, center - radius + 10, center, center + radius - 10, center, lineColor, 2f);

            // Draw inner range rings
            DrawCircle(pixels, size, center, center, radius * 2 / 3, lineColor, 1.5f);
            DrawCircle(pixels, size, center, center, radius / 3, lineColor, 1.5f);

            // Draw sweep arc (90 degree arc)
            DrawArc(pixels, size, center, center, radius - 15, -45, 45, accentColor, 4f);

            // Draw precipitation blob (stylized)
            DrawPrecipitationBlob(pixels, size, center + 40, center - 35, 25, accentColor);

            SaveTexture(texture, pixels, "WeatherRadar");
        }

        /// <summary>
        /// Generates a traffic radar icon - aircraft symbols with surrounding traffic
        /// </summary>
        private void GenerateTrafficRadarIcon()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Color lineColor = new Color(0.9f, 0.9f, 0.95f, 1f);
            Color threatColor = new Color(1f, 0.5f, 0.3f, 0.9f); // Amber/red for traffic

            int center = size / 2;

            // Draw own aircraft (center, upward pointing)
            DrawAircraftSymbol(pixels, size, center, center, lineColor, 2.5f, true);

            // Draw traffic targets
            DrawTrafficTarget(pixels, size, center + 50, center - 40, threatColor, 2f);
            DrawTrafficTarget(pixels, size, center - 60, center + 30, threatColor, 2f);
            DrawTrafficTarget(pixels, size, center + 30, center + 55, threatColor, 2f);

            // Draw range rings
            DrawCircle(pixels, size, center, center, 80, lineColor, 1.5f);

            SaveTexture(texture, pixels, "TrafficRadar");
        }

        /// <summary>
        /// Generates an indicator system icon - gauge/altimeter style
        /// </summary>
        private void GenerateIndicatorIcon()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Color lineColor = new Color(0.9f, 0.9f, 0.95f, 1f);
            Color needleColor = new Color(0.3f, 0.8f, 0.5f, 0.95f); // Green

            int center = size / 2;
            int radius = size * 3 / 8;

            // Draw outer ring
            DrawCircle(pixels, size, center, center, radius, lineColor, 3f);

            // Draw tick marks (like altimeter)
            for (int i = 0; i < 12; i++)
            {
                float angle = (i * 30 - 90) * Mathf.Deg2Rad;
                int x1 = center + (int)((radius - 15) * Mathf.Cos(angle));
                int y1 = center + (int)((radius - 15) * Mathf.Sin(angle));
                int x2 = center + (int)(radius * Mathf.Cos(angle));
                int y2 = center + (int)(radius * Mathf.Sin(angle));
                DrawLine(pixels, size, x1, y1, x2, y2, lineColor, 2f);
            }

            // Draw minor ticks
            for (int i = 0; i < 60; i++)
            {
                if (i % 5 == 0) continue;
                float angle = (i * 6 - 90) * Mathf.Deg2Rad;
                int x1 = center + (int)((radius - 8) * Mathf.Cos(angle));
                int y1 = center + (int)((radius - 8) * Mathf.Sin(angle));
                int x2 = center + (int)(radius * Mathf.Cos(angle));
                int y2 = center + (int)(radius * Mathf.Sin(angle));
                DrawLine(pixels, size, x1, y1, x2, y2, lineColor, 1f);
            }

            // Draw needle (pointing at ~2 o'clock position)
            float needleAngle = 60 * Mathf.Deg2Rad;
            int nx = center + (int)((radius - 25) * Mathf.Cos(needleAngle));
            int ny = center + (int)((radius - 25) * Mathf.Sin(needleAngle));
            DrawLine(pixels, size, center, center, nx, ny, needleColor, 3f);

            // Draw center cap
            DrawFilledCircle(pixels, size, center, center, 8, lineColor);

            SaveTexture(texture, pixels, "IndicatorSystem");
        }

        /// <summary>
        /// Generates a symbology icon - aircraft symbol with waypoints
        /// </summary>
        private void GenerateSymbologyIcon()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Color lineColor = new Color(0.9f, 0.9f, 0.95f, 1f);
            Color accentColor = new Color(0.8f, 0.4f, 0.9f, 0.9f); // Purple

            int center = size / 2;

            // Draw helicopter/aircraft symbol (simplified)
            // Main body
            DrawLine(pixels, size, center, center - 40, center, center + 40, lineColor, 3f);
            // Rotor
            DrawLine(pixels, size, center - 45, center - 35, center + 45, center - 35, lineColor, 2.5f);
            // Tail
            DrawLine(pixels, size, center, center + 20, center + 35, center + 50, lineColor, 2.5f);

            // Draw waypoint symbols
            DrawDiamond(pixels, size, center - 60, center - 50, 12, accentColor);
            DrawDiamond(pixels, size, center + 70, center + 40, 12, accentColor);
            DrawDiamond(pixels, size, center - 40, center + 70, 12, accentColor);

            SaveTexture(texture, pixels, "Symbology");
        }

        /// <summary>
        /// Generates a vision briefing icon - eye/visibility symbol
        /// </summary>
        private void GenerateVisionIcon()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Color lineColor = new Color(0.9f, 0.9f, 0.95f, 1f);
            Color accentColor = new Color(1f, 0.8f, 0.3f, 0.9f); // Amber

            int center = size / 2;

            // Draw eye shape (ellipse outline)
            DrawEllipse(pixels, size, center, center, 70, 40, lineColor, 3f);

            // Draw iris (circle)
            DrawCircle(pixels, size, center, center, 25, lineColor, 2.5f);

            // Draw pupil (filled circle)
            DrawFilledCircle(pixels, size, center, center, 12, accentColor);

            // Draw visibility rays/lines emanating from eye
            for (int i = 0; i < 6; i++)
            {
                float angle = (i * 60 - 30) * Mathf.Deg2Rad;
                int x1 = center + (int)(85 * Mathf.Cos(angle));
                int y1 = center + (int)(50 * Mathf.Sin(angle));
                int x2 = center + (int)(110 * Mathf.Cos(angle));
                int y2 = center + (int)(65 * Mathf.Sin(angle));
                DrawLine(pixels, size, x1, y1, x2, y2, lineColor, 2f);
            }

            SaveTexture(texture, pixels, "VisionBriefing");
        }

        /// <summary>
        /// Generates a system/settings icon - gear/network symbol
        /// </summary>
        private void GenerateSystemIcon()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Color lineColor = new Color(0.9f, 0.9f, 0.95f, 1f);
            Color accentColor = new Color(0.6f, 0.6f, 0.7f, 0.9f); // Gray-blue

            int center = size / 2;
            int outerRadius = 60;
            int innerRadius = 40;

            // Draw gear teeth
            int teeth = 8;
            for (int i = 0; i < teeth; i++)
            {
                float angle1 = ((i * 360f / teeth) - 10) * Mathf.Deg2Rad;
                float angle2 = ((i * 360f / teeth) + 10) * Mathf.Deg2Rad;

                int x1 = center + (int)(innerRadius * Mathf.Cos(angle1));
                int y1 = center + (int)(innerRadius * Mathf.Sin(angle1));
                int x2 = center + (int)(outerRadius * Mathf.Cos(angle1));
                int y2 = center + (int)(outerRadius * Mathf.Sin(angle1));
                int x3 = center + (int)(outerRadius * Mathf.Cos(angle2));
                int y3 = center + (int)(outerRadius * Mathf.Sin(angle2));
                int x4 = center + (int)(innerRadius * Mathf.Cos(angle2));
                int y4 = center + (int)(innerRadius * Mathf.Sin(angle2));

                DrawLine(pixels, size, x1, y1, x2, y2, lineColor, 3f);
                DrawLine(pixels, size, x2, y2, x3, y3, lineColor, 3f);
                DrawLine(pixels, size, x3, y3, x4, y4, lineColor, 3f);
            }

            // Draw inner circle
            DrawCircle(pixels, size, center, center, innerRadius - 5, lineColor, 2.5f);

            // Draw center hub
            DrawFilledCircle(pixels, size, center, center, 15, accentColor);

            SaveTexture(texture, pixels, "System");
        }

        #region Drawing Helpers

        private void DrawLine(Color[] pixels, int size, int x1, int y1, int x2, int y2, Color color, float thickness)
        {
            int dx = Mathf.Abs(x2 - x1);
            int dy = Mathf.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;

            int halfThick = Mathf.Max(1, (int)(thickness / 2));

            while (true)
            {
                for (int ox = -halfThick; ox <= halfThick; ox++)
                {
                    for (int oy = -halfThick; oy <= halfThick; oy++)
                    {
                        int px = x1 + ox;
                        int py = y1 + oy;
                        if (px >= 0 && px < size && py >= 0 && py < size)
                        {
                            float dist = Mathf.Sqrt(ox * ox + oy * oy);
                            float alpha = Mathf.Clamp01(1 - dist / halfThick);
                            if (alpha > 0)
                            {
                                int idx = py * size + px;
                                pixels[idx] = Color.Lerp(pixels[idx], color, alpha * color.a);
                            }
                        }
                    }
                }

                if (x1 == x2 && y1 == y2) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x1 += sx; }
                if (e2 < dx) { err += dx; y1 += sy; }
            }
        }

        private void DrawCircle(Color[] pixels, int size, int cx, int cy, int radius, Color color, float thickness)
        {
            int halfThick = Mathf.Max(1, (int)(thickness / 2));

            for (int angle = 0; angle < 360; angle++)
            {
                float rad = angle * Mathf.Deg2Rad;
                int x = cx + (int)(radius * Mathf.Cos(rad));
                int y = cy + (int)(radius * Mathf.Sin(rad));

                for (int ox = -halfThick; ox <= halfThick; ox++)
                {
                    for (int oy = -halfThick; oy <= halfThick; oy++)
                    {
                        int px = x + ox;
                        int py = y + oy;
                        if (px >= 0 && px < size && py >= 0 && py < size)
                        {
                            float dist = Mathf.Sqrt(ox * ox + oy * oy);
                            float alpha = Mathf.Clamp01(1 - dist / halfThick);
                            if (alpha > 0)
                            {
                                int idx = py * size + px;
                                pixels[idx] = Color.Lerp(pixels[idx], color, alpha * color.a);
                            }
                        }
                    }
                }
            }
        }

        private void DrawFilledCircle(Color[] pixels, int size, int cx, int cy, int radius, Color color)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        int px = cx + x;
                        int py = cy + y;
                        if (px >= 0 && px < size && py >= 0 && py < size)
                        {
                            pixels[py * size + px] = color;
                        }
                    }
                }
            }
        }

        private void DrawEllipse(Color[] pixels, int size, int cx, int cy, int rx, int ry, Color color, float thickness)
        {
            int halfThick = Mathf.Max(1, (int)(thickness / 2));

            for (int angle = 0; angle < 360; angle++)
            {
                float rad = angle * Mathf.Deg2Rad;
                int x = cx + (int)(rx * Mathf.Cos(rad));
                int y = cy + (int)(ry * Mathf.Sin(rad));

                for (int ox = -halfThick; ox <= halfThick; ox++)
                {
                    for (int oy = -halfThick; oy <= halfThick; oy++)
                    {
                        int px = x + ox;
                        int py = y + oy;
                        if (px >= 0 && px < size && py >= 0 && py < size)
                        {
                            float dist = Mathf.Sqrt(ox * ox + oy * oy);
                            float alpha = Mathf.Clamp01(1 - dist / halfThick);
                            if (alpha > 0)
                            {
                                int idx = py * size + px;
                                pixels[idx] = Color.Lerp(pixels[idx], color, alpha * color.a);
                            }
                        }
                    }
                }
            }
        }

        private void DrawArc(Color[] pixels, int size, int cx, int cy, int radius, float startAngle, float endAngle, Color color, float thickness)
        {
            int halfThick = Mathf.Max(1, (int)(thickness / 2));

            for (float angle = startAngle; angle <= endAngle; angle += 0.5f)
            {
                float rad = angle * Mathf.Deg2Rad;
                int x = cx + (int)(radius * Mathf.Cos(rad));
                int y = cy + (int)(radius * Mathf.Sin(rad));

                for (int ox = -halfThick; ox <= halfThick; ox++)
                {
                    for (int oy = -halfThick; oy <= halfThick; oy++)
                    {
                        int px = x + ox;
                        int py = y + oy;
                        if (px >= 0 && px < size && py >= 0 && py < size)
                        {
                            float dist = Mathf.Sqrt(ox * ox + oy * oy);
                            float alpha = Mathf.Clamp01(1 - dist / halfThick);
                            if (alpha > 0)
                            {
                                int idx = py * size + px;
                                pixels[idx] = Color.Lerp(pixels[idx], color, alpha * color.a);
                            }
                        }
                    }
                }
            }
        }

        private void DrawPrecipitationBlob(Color[] pixels, int size, int cx, int cy, int radius, Color color)
        {
            // Draw a soft, irregular blob shape
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    float dist = Mathf.Sqrt(x * x + y * y);
                    float noise = Mathf.PerlinNoise((cx + x) * 0.1f, (cy + y) * 0.1f);
                    float threshold = radius * (0.6f + noise * 0.4f);

                    if (dist < threshold)
                    {
                        int px = cx + x;
                        int py = cy + y;
                        if (px >= 0 && px < size && py >= 0 && py < size)
                        {
                            float alpha = Mathf.Clamp01(1 - dist / threshold);
                            int idx = py * size + px;
                            pixels[idx] = Color.Lerp(pixels[idx], color, alpha * color.a * 0.7f);
                        }
                    }
                }
            }
        }

        private void DrawAircraftSymbol(Color[] pixels, int size, int cx, int cy, Color color, float thickness, bool isOwnship)
        {
            int wingSpan = 35;
            int fuselageLength = 45;

            // Fuselage (vertical line)
            DrawLine(pixels, size, cx, cy - fuselageLength / 2, cx, cy + fuselageLength / 2, color, thickness);

            // Wings (horizontal line)
            DrawLine(pixels, size, cx - wingSpan / 2, cy - 5, cx + wingSpan / 2, cy - 5, color, thickness);

            // Tail (small horizontal line at bottom)
            DrawLine(pixels, size, cx - 12, cy + fuselageLength / 2 - 8, cx + 12, cy + fuselageLength / 2 - 8, color, thickness);

            if (isOwnship)
            {
                // Add a circle around ownship
                DrawCircle(pixels, size, cx, cy, wingSpan / 2 + 10, color, thickness);
            }
        }

        private void DrawTrafficTarget(Color[] pixels, int size, int cx, int cy, Color color, float thickness)
        {
            // Draw a simple diamond shape for traffic
            int s = 8;
            DrawLine(pixels, size, cx, cy - s, cx + s, cy, color, thickness);
            DrawLine(pixels, size, cx + s, cy, cx, cy + s, color, thickness);
            DrawLine(pixels, size, cx, cy + s, cx - s, cy, color, thickness);
            DrawLine(pixels, size, cx - s, cy, cx, cy - s, color, thickness);
        }

        private void DrawDiamond(Color[] pixels, int size, int cx, int cy, int s, Color color)
        {
            DrawLine(pixels, size, cx, cy - s, cx + s, cy, color, 2f);
            DrawLine(pixels, size, cx + s, cy, cx, cy + s, color, 2f);
            DrawLine(pixels, size, cx, cy + s, cx - s, cy, color, 2f);
            DrawLine(pixels, size, cx - s, cy, cx, cy - s, color, 2f);
        }

        private void SaveTexture(Texture2D texture, Color[] pixels, string name)
        {
            texture.SetPixels(pixels);
            texture.Apply();

            string path = Path.Combine(OUTPUT_PATH, name + ".png");
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(path, pngData);

            // Configure import settings
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 100;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Object.DestroyImmediate(texture);
        }

        #endregion
    }
}
