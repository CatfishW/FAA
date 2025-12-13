using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace WeatherRadar
{
    /// <summary>
    /// Optimized renderer for range rings and compass tick marks.
    /// Uses Color32 buffer and batch operations for performance.
    /// </summary>
    public class RangeRingsRenderer : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private RawImage ringsDisplay;
        [SerializeField] private RectTransform displayRect;

        [Header("Labels")]
        [SerializeField] private Transform labelsContainer;
        [SerializeField] private GameObject rangeLabelPrefab;
        [SerializeField] private List<TextMeshProUGUI> rangeLabels = new List<TextMeshProUGUI>();

        [Header("Compass Ticks")]
        [SerializeField] private bool showCompassTicks = true;
        [SerializeField] private int majorTickCount = 12;
        [SerializeField] private int minorTickCount = 36;

        private WeatherRadarConfig config;
        private WeatherRadarDataProvider dataProvider;
        private Texture2D ringsTexture;
        private Color32[] pixelBuffer;
        private int textureSize = 512;
        private float currentRange = 40f;
        private bool needsRedraw = true;

        // Pre-calculated values
        private float[] sinTable;
        private float[] cosTable;

        /// <summary>
        /// Initialize the range rings renderer
        /// </summary>
        public void Initialize(WeatherRadarConfig radarConfig, WeatherRadarDataProvider provider)
        {
            config = radarConfig;
            dataProvider = provider;

            if (config != null)
            {
                textureSize = config.textureResolution;
            }

            if (dataProvider != null)
            {
                currentRange = dataProvider.RadarData.currentRange;
            }

            InitializeLookupTables();
            InitializeTexture();
            GenerateRings();
        }

        private void InitializeLookupTables()
        {
            // Pre-calculate sin/cos for 360 degrees
            sinTable = new float[360];
            cosTable = new float[360];
            for (int i = 0; i < 360; i++)
            {
                float rad = i * Mathf.Deg2Rad;
                sinTable[i] = Mathf.Sin(rad);
                cosTable[i] = Mathf.Cos(rad);
            }
        }

        private void InitializeTexture()
        {
            ringsTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            ringsTexture.filterMode = FilterMode.Bilinear;
            ringsTexture.wrapMode = TextureWrapMode.Clamp;

            pixelBuffer = new Color32[textureSize * textureSize];
            ClearBuffer();

            if (ringsDisplay != null)
            {
                ringsDisplay.texture = ringsTexture;
            }
        }

        private void ClearBuffer()
        {
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixelBuffer.Length; i++)
            {
                pixelBuffer[i] = clear;
            }
        }

        private void ApplyBuffer()
        {
            ringsTexture.SetPixels32(pixelBuffer);
            ringsTexture.Apply(false);
        }

        private void OnDestroy()
        {
            if (ringsTexture != null)
            {
                Destroy(ringsTexture);
            }
        }

        /// <summary>
        /// Called when range changes
        /// </summary>
        public void OnRangeChanged(float newRange)
        {
            currentRange = newRange;
            UpdateLabels();
            // Range rings don't need redraw - just labels update
        }

        /// <summary>
        /// Regenerate the range rings texture
        /// </summary>
        public void GenerateRings()
        {
            if (ringsTexture == null || pixelBuffer == null) return;

            ClearBuffer();

            int centerX = textureSize / 2;
            int centerY = textureSize / 2;
            float maxRadius = textureSize / 2f - 2;

            Color32 ringColor = config != null 
                ? (Color32)config.rangeRingColor 
                : new Color32(128, 128, 128, 153);
            float lineWidth = config != null ? config.rangeRingWidth : 1f;
            int ringCount = config != null ? config.rangeRingCount : 4;

            // Draw range rings
            for (int ring = 1; ring <= ringCount; ring++)
            {
                float ringRadius = (ring / (float)ringCount) * maxRadius;
                DrawCircleOptimized(centerX, centerY, ringRadius, ringColor, lineWidth);
            }

            // Draw compass ticks
            if (showCompassTicks)
            {
                DrawCompassTicksOptimized(centerX, centerY, maxRadius);
            }

            // Draw heading line (center vertical line to top)
            Color32 headingColor = config != null 
                ? (Color32)config.headingLineColor 
                : new Color32(255, 255, 255, 204);
            DrawLineOptimized(centerX, centerY, centerX, centerY + (int)maxRadius, headingColor, 2f);

            ApplyBuffer();
            UpdateLabels();
        }

        private void DrawCircleOptimized(int cx, int cy, float radius, Color32 color, float lineWidth)
        {
            int halfWidth = Mathf.CeilToInt(lineWidth / 2f);
            float radiusSq = radius * radius;
            float innerRadiusSq = (radius - lineWidth) * (radius - lineWidth);
            float outerRadiusSq = (radius + lineWidth) * (radius + lineWidth);

            // Draw using anti-aliased circle algorithm
            int iRadius = Mathf.CeilToInt(radius + lineWidth);
            
            for (int dx = -iRadius; dx <= iRadius; dx++)
            {
                for (int dy = -iRadius; dy <= iRadius; dy++)
                {
                    float distSq = dx * dx + dy * dy;
                    
                    // Check if within ring bounds
                    if (distSq < innerRadiusSq || distSq > outerRadiusSq) continue;

                    float dist = Mathf.Sqrt(distSq);
                    float distFromRing = Mathf.Abs(dist - radius);
                    
                    if (distFromRing > lineWidth / 2f) continue;

                    int px = cx + dx;
                    int py = cy + dy;

                    if (px < 0 || px >= textureSize || py < 0 || py >= textureSize) continue;

                    // Anti-alias based on distance from ring center
                    float alpha = 1f - (distFromRing / (lineWidth / 2f));
                    alpha = Mathf.Clamp01(alpha);

                    int pixelIndex = py * textureSize + px;
                    byte newAlpha = (byte)(color.a * alpha);
                    
                    if (newAlpha > pixelBuffer[pixelIndex].a)
                    {
                        pixelBuffer[pixelIndex] = new Color32(color.r, color.g, color.b, newAlpha);
                    }
                }
            }
        }

        private void DrawLineOptimized(int x0, int y0, int x1, int y1, Color32 color, float width)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            int halfWidth = Mathf.CeilToInt(width / 2f);

            while (true)
            {
                // Draw with width
                for (int wx = -halfWidth; wx <= halfWidth; wx++)
                {
                    for (int wy = -halfWidth; wy <= halfWidth; wy++)
                    {
                        int px = x0 + wx;
                        int py = y0 + wy;

                        if (px >= 0 && px < textureSize && py >= 0 && py < textureSize)
                        {
                            float dist = Mathf.Sqrt(wx * wx + wy * wy);
                            if (dist <= width / 2f)
                            {
                                float alpha = 1f - (dist / (width / 2f));
                                int pixelIndex = py * textureSize + px;
                                byte newAlpha = (byte)(color.a * alpha);
                                
                                if (newAlpha > pixelBuffer[pixelIndex].a)
                                {
                                    pixelBuffer[pixelIndex] = new Color32(color.r, color.g, color.b, newAlpha);
                                }
                            }
                        }
                    }
                }

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private void DrawCompassTicksOptimized(int cx, int cy, float radius)
        {
            Color32 tickColor = config != null 
                ? (Color32)config.compassTickColor 
                : new Color32(204, 204, 204, 153);

            // Major ticks (every 30 degrees)
            for (int i = 0; i < majorTickCount; i++)
            {
                int angle = (i * 360) / majorTickCount;
                float innerRadius = radius * 0.93f;

                int x1 = cx + Mathf.RoundToInt(sinTable[angle] * innerRadius);
                int y1 = cy + Mathf.RoundToInt(cosTable[angle] * innerRadius);
                int x2 = cx + Mathf.RoundToInt(sinTable[angle] * radius);
                int y2 = cy + Mathf.RoundToInt(cosTable[angle] * radius);

                DrawLineOptimized(x1, y1, x2, y2, tickColor, 2f);
            }

            // Minor ticks (every 10 degrees)
            Color32 minorColor = new Color32(tickColor.r, tickColor.g, tickColor.b, (byte)(tickColor.a / 2));
            int ticksPerMajor = minorTickCount / majorTickCount;
            
            for (int i = 0; i < minorTickCount; i++)
            {
                if (i % ticksPerMajor == 0) continue; // Skip major tick positions

                int angle = (i * 360) / minorTickCount;
                float innerRadius = radius * 0.96f;

                int x1 = cx + Mathf.RoundToInt(sinTable[angle] * innerRadius);
                int y1 = cy + Mathf.RoundToInt(cosTable[angle] * innerRadius);
                int x2 = cx + Mathf.RoundToInt(sinTable[angle] * radius);
                int y2 = cy + Mathf.RoundToInt(cosTable[angle] * radius);

                DrawLineOptimized(x1, y1, x2, y2, minorColor, 1f);
            }
        }

        private void UpdateLabels()
        {
            if (labelsContainer == null) return;

            int ringCount = config != null ? config.rangeRingCount : 4;
            float ringSpacing = currentRange / ringCount;

            // Ensure we have enough labels
            while (rangeLabels.Count < ringCount)
            {
                if (rangeLabelPrefab != null)
                {
                    GameObject labelObj = Instantiate(rangeLabelPrefab, labelsContainer);
                    TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
                    if (label != null)
                    {
                        rangeLabels.Add(label);
                    }
                }
                else
                {
                    // Create default label
                    GameObject labelObj = new GameObject($"RangeLabel_{rangeLabels.Count}");
                    labelObj.transform.SetParent(labelsContainer, false);
                    
                    RectTransform rect = labelObj.AddComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(40, 20);
                    
                    TextMeshProUGUI tmp = labelObj.AddComponent<TextMeshProUGUI>();
                    tmp.fontSize = 10;
                    tmp.alignment = TextAlignmentOptions.Left;
                    tmp.color = new Color(0.7f, 0.9f, 0.7f, 0.8f);
                    
                    rangeLabels.Add(tmp);
                }
            }

            // Update label text and positions
            float maxRadius = displayRect != null ? displayRect.rect.width / 2f : 150f;

            for (int i = 0; i < rangeLabels.Count && i < ringCount; i++)
            {
                float rangeValue = ringSpacing * (i + 1);
                rangeLabels[i].text = $"{rangeValue:0}";

                // Position label at ring position (offset to the right)
                float ringRadius = ((i + 1) / (float)ringCount) * maxRadius;
                RectTransform labelRect = rangeLabels[i].GetComponent<RectTransform>();
                if (labelRect != null)
                {
                    labelRect.anchoredPosition = new Vector2(ringRadius + 5, 2);
                }

                rangeLabels[i].gameObject.SetActive(true);
            }

            // Hide extra labels
            for (int i = ringCount; i < rangeLabels.Count; i++)
            {
                rangeLabels[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Set visibility
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (ringsDisplay != null)
            {
                ringsDisplay.gameObject.SetActive(visible);
            }

            if (labelsContainer != null)
            {
                labelsContainer.gameObject.SetActive(visible);
            }
        }
    }
}
