using UnityEngine;
using System.Collections.Generic;

namespace WeatherRadar
{
    /// <summary>
    /// Optimized simulated weather data provider using Perlin noise.
    /// Generates procedural weather patterns for testing and demonstration.
    /// 
    /// Performance Optimizations:
    /// - Uses Color32 buffer and SetPixels32
    /// - Pre-calculated color lookup table
    /// - Efficient circular masking
    /// </summary>
    public class SimulatedWeatherProvider : WeatherRadarProviderBase
    {
        [Header("Weather Simulation")]
        [SerializeField] private float noiseScale = 0.02f;
        [SerializeField] private float noiseSpeed = 0.1f;
        [SerializeField] private float intensityThreshold = 0.3f;

        [Header("Storm Cells")]
        [SerializeField] private bool generateStormCells = true;
        [SerializeField] private int maxStormCells = 5;
        [SerializeField] [Range(0.1f, 1f)] private float stormIntensity = 0.7f;
        [SerializeField] private float cellMinRadius = 20f;
        [SerializeField] private float cellMaxRadius = 80f;

        [Header("Movement")]
        [SerializeField] private float cellMoveSpeed = 5f;
        [SerializeField] private Vector2 windDirection = new Vector2(1f, 0.5f);

        public override string ProviderName => "Simulated Weather";

        private float noiseOffsetX;
        private float noiseOffsetY;
        private List<StormCell> stormCells = new List<StormCell>();
        private float lastCellUpdateTime;
        private Color32[] pixelBuffer;
        private Color32[] colorLUT;

        // Pre-calculated values
        private int centerX;
        private int centerY;
        private float radius;
        private float radiusSq;

        private class StormCell
        {
            public Vector2 position;
            public float radius;
            public float intensity;
            public float lifetime;
            public float age;
        }

        protected override void Awake()
        {
            base.Awake();

            noiseOffsetX = Random.Range(0f, 1000f);
            noiseOffsetY = Random.Range(0f, 1000f);

            if (generateStormCells)
            {
                InitializeStormCells();
            }
        }

        protected override void InitializeTexture()
        {
            base.InitializeTexture();

            centerX = textureSize / 2;
            centerY = textureSize / 2;
            radius = textureSize / 2f;
            radiusSq = radius * radius;

            pixelBuffer = new Color32[textureSize * textureSize];
            InitializeColorLUT();
        }

        private void InitializeColorLUT()
        {
            // Pre-calculate 256 colors for fast lookup
            colorLUT = new Color32[256];
            for (int i = 0; i < 256; i++)
            {
                float intensity = i / 255f;
                colorLUT[i] = GetWeatherColor32(intensity);
            }
        }

        private Color32 GetWeatherColor32(float intensity)
        {
            if (intensity <= 0f)
                return new Color32(0, 0, 0, 0);

            // Standard weather radar color scale
            if (intensity < 0.25f)
            {
                // Green (light rain)
                float t = intensity / 0.25f;
                return new Color32(0, (byte)(77 + 128 * t), 0, (byte)(t * 255));
            }
            else if (intensity < 0.5f)
            {
                // Green to Yellow
                float t = (intensity - 0.25f) / 0.25f;
                return new Color32((byte)(t * 255), 204, 0, 255);
            }
            else if (intensity < 0.75f)
            {
                // Yellow to Orange
                float t = (intensity - 0.5f) / 0.25f;
                return new Color32(255, (byte)(204 - 77 * t), 0, 255);
            }
            else
            {
                // Orange to Red
                float t = (intensity - 0.75f) / 0.25f;
                return new Color32(255, (byte)(127 - 127 * t), 0, 255);
            }
        }

        protected override void Update()
        {
            base.Update();

            // Update noise offset for movement
            noiseOffsetX += noiseSpeed * Time.deltaTime * windDirection.x;
            noiseOffsetY += noiseSpeed * Time.deltaTime * windDirection.y;

            // Update storm cells
            if (generateStormCells)
            {
                UpdateStormCells();
            }
        }

        private void InitializeStormCells()
        {
            stormCells.Clear();

            int numCells = Random.Range(2, maxStormCells + 1);
            for (int i = 0; i < numCells; i++)
            {
                CreateNewStormCell();
            }
        }

        private void CreateNewStormCell()
        {
            // Position within radar range
            float posRange = textureSize * 0.4f;
            Vector2 position = new Vector2(
                Random.Range(-posRange, posRange),
                Random.Range(-posRange, posRange)
            );

            StormCell cell = new StormCell
            {
                position = position,
                radius = Random.Range(cellMinRadius, cellMaxRadius),
                intensity = Random.Range(0.3f, stormIntensity),
                lifetime = Random.Range(30f, 120f),
                age = 0f
            };

            stormCells.Add(cell);
        }

        private void UpdateStormCells()
        {
            float deltaTime = Time.deltaTime;

            for (int i = stormCells.Count - 1; i >= 0; i--)
            {
                var cell = stormCells[i];

                // Move cell with wind
                cell.position += windDirection.normalized * cellMoveSpeed * deltaTime;
                cell.age += deltaTime;

                // Fade intensity near end of life
                float lifeRatio = cell.age / cell.lifetime;
                if (lifeRatio > 0.7f)
                {
                    cell.intensity *= 1f - (lifeRatio - 0.7f) / 0.3f;
                }

                // Remove if expired or too far away
                float distFromCenter = cell.position.magnitude;
                if (cell.age >= cell.lifetime || distFromCenter > textureSize * 0.7f)
                {
                    stormCells.RemoveAt(i);
                }
            }

            // Spawn new cells occasionally
            if (stormCells.Count < maxStormCells && Time.time - lastCellUpdateTime > 10f)
            {
                lastCellUpdateTime = Time.time;
                if (Random.value > 0.5f)
                {
                    CreateNewStormCell();
                }
            }
        }

        protected override void GenerateRadarData()
        {
            if (radarTexture == null || pixelBuffer == null) return;

            float gainMultiplier = 1f + (gainDB / 16f);
            float tiltFactor = 1f - Mathf.Abs(tiltDegrees) / 30f;

            for (int y = 0; y < textureSize; y++)
            {
                int rowOffset = y * textureSize;
                float dy = y - centerY;
                float dySq = dy * dy;

                for (int x = 0; x < textureSize; x++)
                {
                    float dx = x - centerX;
                    float distSq = dx * dx + dySq;

                    if (distSq > radiusSq)
                    {
                        pixelBuffer[rowOffset + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // Calculate weather intensity at this pixel
                    float intensity = CalculateWeatherIntensityOptimized(x, y, dx, dy, distSq);

                    // Apply gain offset
                    intensity *= gainMultiplier;

                    // Distance-based fade
                    float distRatio = Mathf.Sqrt(distSq) / radius;
                    intensity *= 1f - (distRatio * 0.1f * tiltFactor);

                    intensity = Mathf.Clamp01(intensity);

                    // Convert to color using LUT
                    int lutIndex = Mathf.RoundToInt(intensity * 255);
                    pixelBuffer[rowOffset + x] = colorLUT[lutIndex];
                }
            }

            // Apply buffer to texture
            radarTexture.SetPixels32(pixelBuffer);
            radarTexture.Apply(false);
            NotifyDataUpdated();
        }

        private float CalculateWeatherIntensityOptimized(int x, int y, float dx, float dy, float distSq)
        {
            float intensity = 0f;

            // Base noise layer
            float noiseX = (x + noiseOffsetX) * noiseScale;
            float noiseY = (y + noiseOffsetY) * noiseScale;
            float baseNoise = Mathf.PerlinNoise(noiseX, noiseY);

            // Second octave for detail
            float detailNoise = Mathf.PerlinNoise(noiseX * 2f, noiseY * 2f) * 0.5f;

            intensity = (baseNoise + detailNoise) / 1.5f;

            // Apply threshold
            if (intensity < intensityThreshold)
            {
                intensity = 0f;
            }
            else
            {
                intensity = (intensity - intensityThreshold) / (1f - intensityThreshold);
            }

            // Add storm cells
            if (generateStormCells && stormCells.Count > 0)
            {
                Vector2 pixelPos = new Vector2(dx, dy);

                foreach (var cell in stormCells)
                {
                    float cellDistSq = (pixelPos - cell.position).sqrMagnitude;
                    float cellRadiusSq = cell.radius * cell.radius;

                    if (cellDistSq < cellRadiusSq)
                    {
                        float distToCell = Mathf.Sqrt(cellDistSq);
                        float falloff = 1f - (distToCell / cell.radius);
                        falloff = Mathf.Sqrt(falloff); // Sharper falloff

                        // Add cell noise for texture
                        float cellNoise = Mathf.PerlinNoise(
                            (x + noiseOffsetX * 2f) * noiseScale * 3f,
                            (y + noiseOffsetY * 2f) * noiseScale * 3f
                        );

                        float cellIntensity = cell.intensity * falloff * (0.7f + cellNoise * 0.3f);
                        intensity = Mathf.Max(intensity, cellIntensity);
                    }
                }
            }

            return intensity;
        }

        /// <summary>
        /// Manually trigger a new storm cell
        /// </summary>
        public void SpawnStormCell()
        {
            CreateNewStormCell();
        }

        /// <summary>
        /// Clear all storm cells
        /// </summary>
        public void ClearStormCells()
        {
            stormCells.Clear();
        }

        /// <summary>
        /// Set wind direction for weather movement
        /// </summary>
        public void SetWindDirection(Vector2 direction)
        {
            windDirection = direction.normalized;
        }

        /// <summary>
        /// Set noise parameters
        /// </summary>
        public void SetNoiseParameters(float scale, float speed, float threshold)
        {
            noiseScale = scale;
            noiseSpeed = speed;
            intensityThreshold = threshold;
        }
    }
}
