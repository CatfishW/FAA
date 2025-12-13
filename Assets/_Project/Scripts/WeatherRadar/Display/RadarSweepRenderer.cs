using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using System;

namespace WeatherRadar
{
    /// <summary>
    /// High-performance radar sweep renderer using optimized pixel operations.
    /// Creates the classic rotating beam visual with smooth gradient trail.
    /// 
    /// Performance Optimizations:
    /// - Uses SetPixels32 instead of SetPixel for batch operations
    /// - Pre-calculates lookup tables for trig functions
    /// - Minimizes per-pixel allocations
    /// - Uses dirty flag to skip unnecessary updates
    /// </summary>
    public class RadarSweepRenderer : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private RawImage sweepImage;

        [Header("Sweep Settings")]
        [SerializeField] private float sweepSpeed = 180f;
        [SerializeField] private float trailLength = 45f;
        [SerializeField] private float lineWidth = 4f;

        [Header("Visual Settings")]
        [SerializeField] private Color32 sweepColor = new Color32(51, 255, 77, 230);
        [SerializeField] private Color32 trailEndColor = new Color32(26, 153, 51, 0);

        [Header("Performance")]
        [SerializeField] private int trailSteps = 24;
        [SerializeField] private float pixelStep = 1.5f;

        [Header("Glow Effect")]
        [SerializeField] private bool enableGlow = true;
        [SerializeField] private float glowWidth = 8f;
        [SerializeField] private byte glowAlpha = 102;

        private WeatherRadarConfig config;
        private RectTransform parentRect;
        private float currentAngle;
        private Texture2D sweepTexture;
        private int textureSize = 512;
        private float lastAngle;
        private Color32[] pixelBuffer;
        private bool isDirty = true;

        // Pre-calculated lookup tables
        private float[] sinTable;
        private float[] cosTable;
        private float[] trailIntensities;

        // Events
        public event Action OnSweepComplete;
        public event Action<float> OnSweepAngleChanged;

        public float CurrentAngle => currentAngle;
        public float SweepSpeed
        {
            get => sweepSpeed;
            set => sweepSpeed = value;
        }

        /// <summary>
        /// Initialize the sweep renderer
        /// </summary>
        public void Initialize(WeatherRadarConfig radarConfig, RectTransform parent)
        {
            config = radarConfig;
            parentRect = parent;

            if (config != null)
            {
                textureSize = config.textureResolution;
                sweepColor = config.sweepLineColor;
                lineWidth = config.sweepLineWidth;
                trailLength = config.sweepTrailLength;
            }

            InitializeLookupTables();
            InitializeSweepTexture();
        }

        private void InitializeLookupTables()
        {
            // Pre-calculate sin/cos for all degrees
            sinTable = new float[360];
            cosTable = new float[360];
            for (int i = 0; i < 360; i++)
            {
                float rad = (90f - i) * Mathf.Deg2Rad;
                sinTable[i] = Mathf.Sin(rad);
                cosTable[i] = Mathf.Cos(rad);
            }

            // Pre-calculate trail intensities with smooth falloff
            trailIntensities = new float[trailSteps + 1];
            for (int i = 0; i <= trailSteps; i++)
            {
                float t = (float)i / trailSteps;
                // Smooth cubic falloff
                trailIntensities[i] = 1f - (t * t * (3f - 2f * t));
            }
        }

        private void InitializeSweepTexture()
        {
            sweepTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            sweepTexture.filterMode = FilterMode.Bilinear;
            sweepTexture.wrapMode = TextureWrapMode.Clamp;
            
            // Initialize pixel buffer
            pixelBuffer = new Color32[textureSize * textureSize];
            ClearBuffer();
            ApplyBuffer();

            if (sweepImage != null)
            {
                sweepImage.texture = sweepTexture;
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
            sweepTexture.SetPixels32(pixelBuffer);
            sweepTexture.Apply(false);
        }

        private void OnDestroy()
        {
            if (sweepTexture != null)
            {
                Destroy(sweepTexture);
            }
        }

        /// <summary>
        /// Update the sweep line position
        /// </summary>
        public void UpdateSweep(float angle)
        {
            lastAngle = currentAngle;
            currentAngle = angle % 360f;
            if (currentAngle < 0) currentAngle += 360f;

            // Check for sweep completion
            if (lastAngle > 350f && currentAngle < 10f)
            {
                OnSweepComplete?.Invoke();
            }

            OnSweepAngleChanged?.Invoke(currentAngle);
            isDirty = true;
        }

        /// <summary>
        /// Advance sweep by delta time
        /// </summary>
        public void AdvanceSweep(float deltaTime)
        {
            float newAngle = currentAngle + sweepSpeed * deltaTime;
            UpdateSweep(newAngle);
        }

        private void LateUpdate()
        {
            if (isDirty)
            {
                DrawSweepOptimized();
                isDirty = false;
            }
        }

        private void DrawSweepOptimized()
        {
            if (sweepTexture == null || pixelBuffer == null) return;

            // Clear buffer
            ClearBuffer();

            int centerX = textureSize / 2;
            int centerY = textureSize / 2;
            float radius = textureSize / 2f - 2f;

            // Draw trail (from back to front)
            for (int step = trailSteps; step >= 0; step--)
            {
                float t = (float)step / trailSteps;
                float trailAngle = currentAngle - (t * trailLength);
                if (trailAngle < 0) trailAngle += 360f;

                float intensity = trailIntensities[step];
                if (intensity < 0.02f) continue;

                // Interpolate color
                byte r = (byte)Mathf.Lerp(sweepColor.r, trailEndColor.r, t);
                byte g = (byte)Mathf.Lerp(sweepColor.g, trailEndColor.g, t);
                byte b = (byte)Mathf.Lerp(sweepColor.b, trailEndColor.b, t);
                byte a = (byte)(sweepColor.a * intensity);

                Color32 lineColor = new Color32(r, g, b, a);
                float currentWidth = Mathf.Lerp(lineWidth, lineWidth * 0.3f, t);

                DrawRadialLineOptimized(centerX, centerY, radius, trailAngle, lineColor, currentWidth);

                // Add glow for main sweep area
                if (enableGlow && step < trailSteps / 4)
                {
                    byte glowA = (byte)(glowAlpha * intensity * (1f - (float)step / (trailSteps / 4)));
                    Color32 glowColor = new Color32(r, g, b, glowA);
                    DrawRadialLineOptimized(centerX, centerY, radius, trailAngle, glowColor, glowWidth);
                }
            }

            // Draw main sweep line (brightest)
            DrawRadialLineOptimized(centerX, centerY, radius, currentAngle, sweepColor, lineWidth);

            // Draw glow on main line
            if (enableGlow)
            {
                Color32 glowColor = new Color32(sweepColor.r, sweepColor.g, sweepColor.b, glowAlpha);
                DrawRadialLineOptimized(centerX, centerY, radius, currentAngle, glowColor, glowWidth);
            }

            ApplyBuffer();
        }

        private void DrawRadialLineOptimized(int cx, int cy, float radius, float angleDeg, Color32 color, float width)
        {
            int angleIndex = Mathf.RoundToInt(angleDeg) % 360;
            if (angleIndex < 0) angleIndex += 360;

            float dirX = cosTable[angleIndex];
            float dirY = sinTable[angleIndex];
            float perpX = -dirY;
            float perpY = dirX;

            float halfWidth = width / 2f;
            float radiusSq = radius * radius;

            for (float r = 0; r < radius; r += pixelStep)
            {
                // Edge fade
                float edgeFade = r > radius - 10f ? (radius - r) / 10f : 1f;
                if (edgeFade <= 0) continue;

                float baseX = cx + dirX * r;
                float baseY = cy + dirY * r;

                for (float w = -halfWidth; w <= halfWidth; w += pixelStep)
                {
                    float x = baseX + perpX * w;
                    float y = baseY + perpY * w;

                    int px = Mathf.RoundToInt(x);
                    int py = Mathf.RoundToInt(y);

                    if (px < 0 || px >= textureSize || py < 0 || py >= textureSize) continue;

                    // Width fade
                    float widthFade = 1f - Mathf.Abs(w) / halfWidth;
                    widthFade = Mathf.Sqrt(widthFade); // Smoother falloff

                    int pixelIndex = py * textureSize + px;
                    
                    // Alpha blend
                    byte newAlpha = (byte)(color.a * widthFade * edgeFade);
                    if (newAlpha > pixelBuffer[pixelIndex].a)
                    {
                        pixelBuffer[pixelIndex] = new Color32(color.r, color.g, color.b, newAlpha);
                    }
                }
            }
        }

        /// <summary>
        /// Set sweep visibility
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (sweepImage != null)
            {
                sweepImage.enabled = visible;
            }
        }

        /// <summary>
        /// Set sweep color
        /// </summary>
        public void SetColor(Color color)
        {
            sweepColor = color;
        }

        /// <summary>
        /// Set trail length in degrees
        /// </summary>
        public void SetTrailLength(float degrees)
        {
            trailLength = Mathf.Clamp(degrees, 10f, 90f);
        }
    }
}
