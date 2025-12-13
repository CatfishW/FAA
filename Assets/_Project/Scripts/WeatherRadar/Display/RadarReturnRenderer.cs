using UnityEngine;
using UnityEngine.UI;

namespace WeatherRadar
{
    /// <summary>
    /// Optimized renderer for weather returns on the radar display.
    /// Uses Color32 buffers and progressive reveal based on sweep angle.
    /// 
    /// Performance Optimizations:
    /// - Uses SetPixels32 for batch operations
    /// - Progressive reveal instead of full texture updates
    /// - Efficient circular masking
    /// - Minimal allocations during update
    /// </summary>
    public class RadarReturnRenderer : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private RawImage returnDisplay;
        [SerializeField] private RectTransform displayRect;

        [Header("Fade Settings")]
        [SerializeField] private float persistenceDuration = 8f;
        [SerializeField] private bool enablePersistence = true;
        [SerializeField] private float fadeUpdateInterval = 0.1f;

        [Header("Performance")]
        [SerializeField] private bool useProgressiveReveal = true;
        [SerializeField] private float revealAngleBuffer = 5f;

        private WeatherRadarConfig config;
        private WeatherRadarDataProvider dataProvider;
        private Texture2D displayTexture;
        private Texture2D weatherSourceTexture;
        private Color32[] pixelBuffer;
        private float[] persistenceBuffer;
        private float[] intensityBuffer;
        private int textureSize = 512;
        private float lastSweepAngle;
        private float lastFadeUpdate;

        // Pre-calculated values
        private int centerX;
        private int centerY;
        private float radius;
        private float radiusSq;

        // Color lookup table for performance
        private Color32[] rainbowLUT;

        /// <summary>
        /// Initialize the return renderer
        /// </summary>
        public void Initialize(WeatherRadarConfig radarConfig, WeatherRadarDataProvider provider)
        {
            config = radarConfig;
            dataProvider = provider;

            if (config != null)
            {
                textureSize = config.textureResolution;
            }

            centerX = textureSize / 2;
            centerY = textureSize / 2;
            radius = textureSize / 2f;
            radiusSq = radius * radius;

            InitializeColorLUT();
            InitializeTextures();
            InitializeBuffers();
        }

        private void InitializeColorLUT()
        {
            // Pre-calculate 256 colors for intensity lookup
            rainbowLUT = new Color32[256];
            for (int i = 0; i < 256; i++)
            {
                float intensity = i / 255f;
                rainbowLUT[i] = GetPrecipColor(intensity);
            }
        }

        private Color32 GetPrecipColor(float intensity)
        {
            if (config != null)
            {
                return config.GetPrecipitationColor(intensity);
            }

            // Default NEXRAD-style colors
            if (intensity < 0.2f)
                return new Color32(0, (byte)(intensity * 5f * 200 + 55), 0, (byte)(intensity * 5f * 200));
            if (intensity < 0.4f)
                return new Color32((byte)((intensity - 0.2f) * 5f * 255), 255, 0, 220);
            if (intensity < 0.6f)
                return new Color32(255, (byte)(255 - (intensity - 0.4f) * 5f * 128), 0, 235);
            if (intensity < 0.8f)
                return new Color32(255, (byte)(127 - (intensity - 0.6f) * 5f * 127), 0, 250);
            return new Color32(255, 0, (byte)((intensity - 0.8f) * 5f * 255), 255);
        }

        private void InitializeTextures()
        {
            displayTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            displayTexture.filterMode = FilterMode.Bilinear;
            displayTexture.wrapMode = TextureWrapMode.Clamp;

            pixelBuffer = new Color32[textureSize * textureSize];
            ClearToBackground();
            ApplyBuffer();

            if (returnDisplay != null)
            {
                returnDisplay.texture = displayTexture;
            }
        }

        private void InitializeBuffers()
        {
            int bufferSize = textureSize * textureSize;
            persistenceBuffer = new float[bufferSize];
            intensityBuffer = new float[bufferSize];

            for (int i = 0; i < bufferSize; i++)
            {
                persistenceBuffer[i] = 0f;
                intensityBuffer[i] = 0f;
            }
        }

        private void ClearToBackground()
        {
            Color32 bgColor = config != null 
                ? (Color32)config.backgroundColor 
                : new Color32(13, 13, 13, 255);

            for (int i = 0; i < pixelBuffer.Length; i++)
            {
                int x = i % textureSize;
                int y = i / textureSize;
                float dx = x - centerX;
                float dy = y - centerY;

                if (dx * dx + dy * dy <= radiusSq)
                {
                    pixelBuffer[i] = bgColor;
                }
                else
                {
                    pixelBuffer[i] = new Color32(0, 0, 0, 0);
                }
            }
        }

        private void ApplyBuffer()
        {
            displayTexture.SetPixels32(pixelBuffer);
            displayTexture.Apply(false);
        }

        private void OnDestroy()
        {
            if (displayTexture != null)
            {
                Destroy(displayTexture);
            }
        }

        private void Update()
        {
            if (!enablePersistence) return;

            // Throttled fade update for performance
            if (Time.time - lastFadeUpdate < fadeUpdateInterval) return;
            lastFadeUpdate = Time.time;

            UpdatePersistenceFade();
        }

        private void UpdatePersistenceFade()
        {
            float fadeAmount = fadeUpdateInterval / persistenceDuration;
            bool needsApply = false;

            Color32 bgColor = config != null 
                ? (Color32)config.backgroundColor 
                : new Color32(13, 13, 13, 255);

            for (int i = 0; i < persistenceBuffer.Length; i++)
            {
                if (persistenceBuffer[i] > 0)
                {
                    persistenceBuffer[i] -= fadeAmount;
                    if (persistenceBuffer[i] < 0) persistenceBuffer[i] = 0;

                    // Update pixel color based on new persistence
                    if (intensityBuffer[i] > 0)
                    {
                        int x = i % textureSize;
                        int y = i / textureSize;
                        float dx = x - centerX;
                        float dy = y - centerY;

                        if (dx * dx + dy * dy <= radiusSq)
                        {
                            if (persistenceBuffer[i] > 0.01f)
                            {
                                int lutIndex = Mathf.Clamp(Mathf.RoundToInt(intensityBuffer[i] * 255), 0, 255);
                                Color32 color = rainbowLUT[lutIndex];
                                color.a = (byte)(color.a * persistenceBuffer[i]);
                                pixelBuffer[i] = BlendColor32(bgColor, color);
                            }
                            else
                            {
                                pixelBuffer[i] = bgColor;
                                intensityBuffer[i] = 0;
                            }
                            needsApply = true;
                        }
                    }
                }
            }

            if (needsApply)
            {
                ApplyBuffer();
            }
        }

        /// <summary>
        /// Update with new weather data texture
        /// </summary>
        public void UpdateWeatherData(Texture2D sourceTexture)
        {
            weatherSourceTexture = sourceTexture;
            
            if (sourceTexture == null) return;

            // Full update - sample entire texture
            ProcessFullWeatherTexture();
        }

        private void ProcessFullWeatherTexture()
        {
            if (weatherSourceTexture == null) return;

            Color32 bgColor = config != null 
                ? (Color32)config.backgroundColor 
                : new Color32(13, 13, 13, 255);

            int sourceWidth = weatherSourceTexture.width;
            int sourceHeight = weatherSourceTexture.height;
            Color32[] sourcePixels = weatherSourceTexture.GetPixels32();

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;

                    if (dx * dx + dy * dy > radiusSq) continue;

                    int i = y * textureSize + x;

                    // Sample from source texture
                    int sourceX = Mathf.FloorToInt((float)x / textureSize * sourceWidth);
                    int sourceY = Mathf.FloorToInt((float)y / textureSize * sourceHeight);
                    sourceX = Mathf.Clamp(sourceX, 0, sourceWidth - 1);
                    sourceY = Mathf.Clamp(sourceY, 0, sourceHeight - 1);

                    Color32 sourceColor = sourcePixels[sourceY * sourceWidth + sourceX];

                    // Convert color to intensity
                    float intensity = GetIntensityFromColor32(sourceColor);

                    if (intensity > 0.05f)
                    {
                        intensityBuffer[i] = intensity;
                        persistenceBuffer[i] = 1f;

                        int lutIndex = Mathf.Clamp(Mathf.RoundToInt(intensity * 255), 0, 255);
                        pixelBuffer[i] = BlendColor32(bgColor, rainbowLUT[lutIndex]);
                    }
                }
            }

            ApplyBuffer();
        }

        /// <summary>
        /// Update weather returns based on sweep angle (progressive reveal)
        /// </summary>
        public void UpdateSweepReveal(float sweepAngle, Texture2D sourceTexture)
        {
            if (sourceTexture == null) return;
            if (!useProgressiveReveal)
            {
                UpdateWeatherData(sourceTexture);
                return;
            }

            float deltaAngle = sweepAngle - lastSweepAngle;
            if (deltaAngle < 0) deltaAngle += 360f;
            if (deltaAngle > 180f) deltaAngle = 0; // Handle wrap-around

            lastSweepAngle = sweepAngle;

            Color32 bgColor = config != null 
                ? (Color32)config.backgroundColor 
                : new Color32(13, 13, 13, 255);

            int sourceWidth = sourceTexture.width;
            int sourceHeight = sourceTexture.height;
            Color32[] sourcePixels = sourceTexture.GetPixels32();

            bool needsApply = false;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float distSq = dx * dx + dy * dy;

                    if (distSq > radiusSq) continue;

                    // Calculate pixel angle
                    float pixelAngle = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
                    if (pixelAngle < 0) pixelAngle += 360f;

                    // Check if this pixel is in the just-swept sector
                    float angleDiff = sweepAngle - pixelAngle;
                    if (angleDiff < 0) angleDiff += 360f;

                    if (angleDiff >= 0 && angleDiff < deltaAngle + revealAngleBuffer)
                    {
                        int i = y * textureSize + x;

                        // Sample source texture at this position
                        int sourceX = Mathf.FloorToInt((float)x / textureSize * sourceWidth);
                        int sourceY = Mathf.FloorToInt((float)y / textureSize * sourceHeight);
                        sourceX = Mathf.Clamp(sourceX, 0, sourceWidth - 1);
                        sourceY = Mathf.Clamp(sourceY, 0, sourceHeight - 1);

                        Color32 sourceColor = sourcePixels[sourceY * sourceWidth + sourceX];
                        float intensity = GetIntensityFromColor32(sourceColor);

                        if (intensity > 0.05f)
                        {
                            intensityBuffer[i] = intensity;
                            persistenceBuffer[i] = 1f;

                            int lutIndex = Mathf.Clamp(Mathf.RoundToInt(intensity * 255), 0, 255);
                            pixelBuffer[i] = BlendColor32(bgColor, rainbowLUT[lutIndex]);
                            needsApply = true;
                        }
                    }
                }
            }

            if (needsApply)
            {
                ApplyBuffer();
            }
        }

        private float GetIntensityFromColor32(Color32 color)
        {
            if (color.a < 25) return 0f;

            // Map color to intensity based on common radar color schemes
            float r = color.r / 255f;
            float g = color.g / 255f;
            float b = color.b / 255f;

            // Magenta/Pink = extreme
            if (r > 0.7f && b > 0.5f && g < 0.5f) return 1f;
            // Red = very heavy
            if (r > 0.7f && g < 0.4f && b < 0.4f) return 0.85f;
            // Orange = heavy
            if (r > 0.7f && g > 0.3f && g < 0.7f && b < 0.3f) return 0.65f;
            // Yellow = moderate
            if (r > 0.7f && g > 0.7f && b < 0.3f) return 0.45f;
            // Green = light
            if (g > 0.5f && r < 0.5f && b < 0.5f) return 0.25f;
            // Light green = very light
            if (g > 0.3f) return g * 0.5f;

            return Mathf.Max(r, g, b) * (color.a / 255f);
        }

        private Color32 BlendColor32(Color32 bg, Color32 fg)
        {
            if (fg.a == 0) return bg;
            if (fg.a == 255) return fg;

            float alpha = fg.a / 255f;
            float invAlpha = 1f - alpha;

            return new Color32(
                (byte)(fg.r * alpha + bg.r * invAlpha),
                (byte)(fg.g * alpha + bg.g * invAlpha),
                (byte)(fg.b * alpha + bg.b * invAlpha),
                255
            );
        }

        /// <summary>
        /// Clear all weather returns
        /// </summary>
        public void Clear()
        {
            InitializeBuffers();
            ClearToBackground();
            ApplyBuffer();
        }

        /// <summary>
        /// Set persistence enabled
        /// </summary>
        public void SetPersistence(bool enabled, float duration = 8f)
        {
            enablePersistence = enabled;
            persistenceDuration = duration;
        }
    }
}
