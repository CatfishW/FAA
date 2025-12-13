using UnityEngine;
using System.Collections.Generic;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Converts 2D radar texture data into 3D volumetric weather data.
    /// Uses meteorological models to estimate altitude distribution.
    /// </summary>
    public static class Weather2DTo3DConverter
    {
        // Altitude distribution constants (based on typical precipitation profiles)
        private const float LIGHT_RAIN_BASE_ALT = 2000f;      // Feet
        private const float LIGHT_RAIN_TOP_ALT = 15000f;
        private const float MODERATE_RAIN_BASE_ALT = 1500f;
        private const float MODERATE_RAIN_TOP_ALT = 25000f;
        private const float HEAVY_RAIN_BASE_ALT = 1000f;
        private const float HEAVY_RAIN_TOP_ALT = 35000f;
        private const float THUNDERSTORM_BASE_ALT = 500f;
        private const float THUNDERSTORM_TOP_ALT = 50000f;

        /// <summary>
        /// Convert a 2D radar texture to 3D weather data
        /// </summary>
        /// <param name="radarTexture">Input 2D radar texture</param>
        /// <param name="config">3D weather configuration</param>
        /// <param name="aircraftPosition">Current aircraft world position</param>
        /// <param name="aircraftAltitude">Aircraft altitude in feet</param>
        /// <param name="rangeNM">Radar range in nautical miles</param>
        /// <returns>Populated Weather3DData object</returns>
        public static Weather3DData Convert(
            Texture2D radarTexture, 
            Weather3DConfig config,
            Vector3 aircraftPosition,
            float aircraftAltitude,
            float rangeNM)
        {
            if (radarTexture == null || config == null)
            {
                Debug.LogWarning("[Weather2DTo3DConverter] Null texture or config provided");
                return null;
            }

            var data = new Weather3DData
            {
                gridSize = config.gridResolution,
                coverageNM = rangeNM * 2f, // Radar covers +/- range
                maxAltitudeFt = config.maxAltitudeFt,
                aircraftPosition = aircraftPosition,
                aircraftAltitude = aircraftAltitude,
                lastUpdateTime = Time.time
            };
            
            data.InitializeGrid();

            // Sample the radar texture and build 3D grid
            int texWidth = radarTexture.width;
            int texHeight = radarTexture.height;
            Color[] pixels = radarTexture.GetPixels();

            // Track cells for grouping
            List<Vector2Int> significantCells = new List<Vector2Int>();

            // Map texture pixels to grid cells
            for (int gx = 0; gx < data.gridSize.x; gx++)
            {
                for (int gz = 0; gz < data.gridSize.z; gz++)
                {
                    // Map grid cell to texture coordinate
                    float u = gx / (float)(data.gridSize.x - 1);
                    float v = gz / (float)(data.gridSize.z - 1);
                    
                    int texX = Mathf.RoundToInt(u * (texWidth - 1));
                    int texY = Mathf.RoundToInt(v * (texHeight - 1));
                    int pixelIndex = texY * texWidth + texX;
                    
                    if (pixelIndex < 0 || pixelIndex >= pixels.Length)
                        continue;

                    Color pixel = pixels[pixelIndex];
                    float intensity = ExtractIntensityFromColor(pixel);
                    
                    if (intensity > 0.05f) // Minimum threshold
                    {
                        significantCells.Add(new Vector2Int(gx, gz));
                        
                        // Distribute intensity vertically based on precipitation strength
                        DistributeVertically(data, gx, gz, intensity);
                    }
                }
            }

            // Generate weather cells from grouped significant areas
            GenerateWeatherCells(data, significantCells, radarTexture, pixels, texWidth, texHeight);
            
            // Generate cloud layers
            GenerateCloudLayers(data);

            return data;
        }

        /// <summary>
        /// Extract intensity value from radar pixel color
        /// </summary>
        private static float ExtractIntensityFromColor(Color pixel)
        {
            // Skip transparent or near-transparent pixels
            if (pixel.a < 0.1f)
                return 0f;

            // Analyze RGB to determine intensity
            // Aviation weather radar typically uses:
            // Green = Light, Yellow = Moderate, Orange/Red = Heavy, Magenta = Extreme
            
            float greenScore = pixel.g - Mathf.Max(pixel.r, pixel.b) * 0.5f;
            float yellowScore = Mathf.Min(pixel.r, pixel.g) - pixel.b * 0.5f;
            float redScore = pixel.r - Mathf.Max(pixel.g, pixel.b) * 0.3f;
            float magentaScore = Mathf.Min(pixel.r, pixel.b) - pixel.g * 0.3f;

            // Calculate weighted intensity
            float intensity = 0f;
            
            if (magentaScore > 0.2f)
                intensity = 0.85f + magentaScore * 0.15f;
            else if (redScore > 0.3f)
                intensity = 0.6f + redScore * 0.25f;
            else if (yellowScore > 0.2f)
                intensity = 0.35f + yellowScore * 0.25f;
            else if (greenScore > 0.1f)
                intensity = 0.1f + greenScore * 0.25f;
            else
                intensity = (pixel.r + pixel.g + pixel.b) / 3f * 0.3f;

            return Mathf.Clamp01(intensity);
        }

        /// <summary>
        /// Distribute precipitation intensity vertically based on meteorological profiles
        /// </summary>
        private static void DistributeVertically(Weather3DData data, int gx, int gz, float intensity)
        {
            // Determine altitude range based on intensity
            float baseAlt, topAlt;
            GetAltitudeRange(intensity, out baseAlt, out topAlt);

            // Convert altitudes to grid indices
            int baseY = Mathf.RoundToInt(baseAlt / data.maxAltitudeFt * data.gridSize.y);
            int topY = Mathf.RoundToInt(topAlt / data.maxAltitudeFt * data.gridSize.y);
            
            baseY = Mathf.Clamp(baseY, 0, data.gridSize.y - 1);
            topY = Mathf.Clamp(topY, 0, data.gridSize.y - 1);

            // Apply vertical intensity profile (bell curve centered around middle altitude)
            for (int gy = baseY; gy <= topY; gy++)
            {
                float altitudeFactor = 1f - Mathf.Abs((gy - (baseY + topY) * 0.5f) / ((topY - baseY) * 0.5f + 1f));
                altitudeFactor = Mathf.Pow(altitudeFactor, 0.5f); // Soften the curve
                
                float cellIntensity = intensity * altitudeFactor;
                data.SetIntensityAt(gx, gy, gz, cellIntensity);
            }
        }

        /// <summary>
        /// Get altitude range for precipitation based on intensity
        /// </summary>
        private static void GetAltitudeRange(float intensity, out float baseAlt, out float topAlt)
        {
            if (intensity < 0.3f)
            {
                baseAlt = LIGHT_RAIN_BASE_ALT;
                topAlt = LIGHT_RAIN_TOP_ALT;
            }
            else if (intensity < 0.5f)
            {
                baseAlt = MODERATE_RAIN_BASE_ALT;
                topAlt = MODERATE_RAIN_TOP_ALT;
            }
            else if (intensity < 0.7f)
            {
                baseAlt = HEAVY_RAIN_BASE_ALT;
                topAlt = HEAVY_RAIN_TOP_ALT;
            }
            else
            {
                baseAlt = THUNDERSTORM_BASE_ALT;
                topAlt = THUNDERSTORM_TOP_ALT;
            }
        }

        /// <summary>
        /// Generate discrete weather cells from grouped radar returns
        /// </summary>
        private static void GenerateWeatherCells(
            Weather3DData data, 
            List<Vector2Int> significantCells,
            Texture2D texture,
            Color[] pixels,
            int texWidth,
            int texHeight)
        {
            if (significantCells.Count == 0) return;

            // Simple clustering: group adjacent cells
            HashSet<Vector2Int> processed = new HashSet<Vector2Int>();
            
            foreach (var cell in significantCells)
            {
                if (processed.Contains(cell)) continue;
                
                // Find connected cells (simple flood fill)
                List<Vector2Int> cluster = new List<Vector2Int>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(cell);
                
                while (queue.Count > 0 && cluster.Count < 100) // Limit cluster size
                {
                    var current = queue.Dequeue();
                    if (processed.Contains(current)) continue;
                    
                    processed.Add(current);
                    cluster.Add(current);
                    
                    // Check 4-connected neighbors
                    Vector2Int[] neighbors = {
                        current + Vector2Int.up,
                        current + Vector2Int.down,
                        current + Vector2Int.left,
                        current + Vector2Int.right
                    };
                    
                    foreach (var neighbor in neighbors)
                    {
                        if (significantCells.Contains(neighbor) && !processed.Contains(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
                
                if (cluster.Count >= 2) // Minimum cell size
                {
                    CreateWeatherCell(data, cluster, texture, pixels, texWidth, texHeight);
                }
            }
        }

        /// <summary>
        /// Create a weather cell from a cluster of grid points
        /// </summary>
        private static void CreateWeatherCell(
            Weather3DData data,
            List<Vector2Int> cluster,
            Texture2D texture,
            Color[] pixels,
            int texWidth,
            int texHeight)
        {
            // Calculate cluster bounds and average intensity
            Vector2 minPos = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 maxPos = new Vector2(float.MinValue, float.MinValue);
            float totalIntensity = 0f;
            float maxIntensity = 0f;

            foreach (var gridPos in cluster)
            {
                minPos.x = Mathf.Min(minPos.x, gridPos.x);
                minPos.y = Mathf.Min(minPos.y, gridPos.y);
                maxPos.x = Mathf.Max(maxPos.x, gridPos.x);
                maxPos.y = Mathf.Max(maxPos.y, gridPos.y);
                
                // Sample texture for this cell
                float u = gridPos.x / (float)(data.gridSize.x - 1);
                float v = gridPos.y / (float)(data.gridSize.z - 1);
                int texX = Mathf.RoundToInt(u * (texWidth - 1));
                int texY = Mathf.RoundToInt(v * (texHeight - 1));
                int pixelIndex = texY * texWidth + texX;
                
                if (pixelIndex >= 0 && pixelIndex < pixels.Length)
                {
                    float intensity = ExtractIntensityFromColor(pixels[pixelIndex]);
                    totalIntensity += intensity;
                    maxIntensity = Mathf.Max(maxIntensity, intensity);
                }
            }

            float avgIntensity = totalIntensity / cluster.Count;
            
            // Calculate world position (center of cluster)
            Vector2 centerGrid = (minPos + maxPos) * 0.5f;
            Vector3 worldPos = data.GridToWorld(
                Mathf.RoundToInt(centerGrid.x),
                data.gridSize.y / 2, // Middle altitude
                Mathf.RoundToInt(centerGrid.y)
            );

            // Determine cell type based on intensity
            WeatherCellType cellType = DetermineCellType(maxIntensity);
            
            // Get altitude range
            GetAltitudeRange(maxIntensity, out float baseAlt, out float topAlt);

            // Calculate cell size
            float sizeX = (maxPos.x - minPos.x + 1) / data.gridSize.x * data.coverageNM * 1852f;
            float sizeZ = (maxPos.y - minPos.y + 1) / data.gridSize.z * data.coverageNM * 1852f;
            float sizeY = (topAlt - baseAlt) * 0.3048f; // Convert feet to meters

            var weatherCell = new WeatherCell3D
            {
                position = worldPos,
                size = new Vector3(Mathf.Max(sizeX, 1000f), Mathf.Max(sizeY, 1000f), Mathf.Max(sizeZ, 1000f)),
                intensity = avgIntensity,
                cellType = cellType,
                altitude = baseAlt,
                topAltitude = topAlt,
                hasLightning = maxIntensity > 0.6f,
                turbulenceLevel = maxIntensity > 0.4f ? (maxIntensity - 0.4f) / 0.6f : 0f
            };

            data.weatherCells.Add(weatherCell);
        }

        /// <summary>
        /// Determine weather cell type based on intensity
        /// </summary>
        private static WeatherCellType DetermineCellType(float intensity)
        {
            if (intensity < 0.25f)
                return WeatherCellType.LightRain;
            else if (intensity < 0.45f)
                return WeatherCellType.ModerateRain;
            else if (intensity < 0.65f)
                return WeatherCellType.HeavyRain;
            else
                return WeatherCellType.Thunderstorm;
        }

        /// <summary>
        /// Generate cloud layers based on weather cells
        /// </summary>
        private static void GenerateCloudLayers(Weather3DData data)
        {
            // Always add a base cloud layer if there's any weather
            if (data.weatherCells.Count > 0)
            {
                // Find overall weather extent
                float minBase = float.MaxValue;
                float maxTop = float.MinValue;
                float avgCoverage = 0f;
                
                foreach (var cell in data.weatherCells)
                {
                    minBase = Mathf.Min(minBase, cell.altitude);
                    maxTop = Mathf.Max(maxTop, cell.topAltitude);
                    avgCoverage += cell.intensity;
                }
                avgCoverage /= data.weatherCells.Count;

                // Create main cloud layer
                data.cloudLayers.Add(new CloudLayer
                {
                    baseAltitude = minBase,
                    topAltitude = maxTop,
                    coverage = Mathf.Clamp01(avgCoverage * 1.5f),
                    layerType = maxTop > 30000f ? CloudLayerType.Cumulonimbus : CloudLayerType.Cumulus,
                    tintColor = Color.white
                });

                // Add secondary layers for thunderstorms
                bool hasThunderstorms = false;
                foreach (var cell in data.weatherCells)
                {
                    if (cell.cellType == WeatherCellType.Thunderstorm)
                    {
                        hasThunderstorms = true;
                        break;
                    }
                }

                if (hasThunderstorms)
                {
                    // Anvil layer at high altitude
                    data.cloudLayers.Add(new CloudLayer
                    {
                        baseAltitude = 35000f,
                        topAltitude = 45000f,
                        coverage = 0.6f,
                        layerType = CloudLayerType.Cirrus,
                        tintColor = new Color(0.9f, 0.9f, 0.95f, 0.8f)
                    });
                }
            }
        }
    }
}
