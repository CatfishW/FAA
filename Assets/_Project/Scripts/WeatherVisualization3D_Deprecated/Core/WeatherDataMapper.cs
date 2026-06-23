using UnityEngine;
using System;
using System.Collections.Generic;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Maps 2D radar/weather data to 3D volumetric representation.
    /// Handles conversion of 2D intensity data into properly extruded 3D volumes
    /// using meteorological altitude profiles.
    /// </summary>
    public class WeatherDataMapper
    {
        #region Configuration

        private WeatherVolumeConfig _config;
        private Vector3Int _resolution;

        #endregion

        #region Altitude Profiles

        // Meteorological altitude profiles for different precipitation types
        private static readonly AltitudeProfile[] _altitudeProfiles = new AltitudeProfile[]
        {
            new AltitudeProfile(WeatherType.Clear, 0, 0, 0),
            new AltitudeProfile(WeatherType.LightRain, 2000f, 15000f, 0.3f),
            new AltitudeProfile(WeatherType.ModerateRain, 1500f, 25000f, 0.5f),
            new AltitudeProfile(WeatherType.HeavyRain, 1000f, 35000f, 0.7f),
            new AltitudeProfile(WeatherType.Thunderstorm, 500f, 50000f, 0.85f),
            new AltitudeProfile(WeatherType.SevereThunderstorm, 300f, 55000f, 1.0f),
            new AltitudeProfile(WeatherType.LightSnow, 3000f, 12000f, 0.25f),
            new AltitudeProfile(WeatherType.ModerateSnow, 2000f, 18000f, 0.4f),
            new AltitudeProfile(WeatherType.HeavySnow, 1500f, 22000f, 0.55f),
            new AltitudeProfile(WeatherType.MixedPrecipitation, 1500f, 20000f, 0.5f),
            new AltitudeProfile(WeatherType.Hail, 1000f, 45000f, 0.9f),
        };

        private struct AltitudeProfile
        {
            public WeatherType Type;
            public float BaseAltitudeFt;
            public float TopAltitudeFt;
            public float IntensityThreshold;

            public AltitudeProfile(WeatherType type, float baseAlt, float topAlt, float threshold)
            {
                Type = type;
                BaseAltitudeFt = baseAlt;
                TopAltitudeFt = topAlt;
                IntensityThreshold = threshold;
            }
        }

        #endregion

        #region Construction

        public WeatherDataMapper(WeatherVolumeConfig config)
        {
            _config = config;
            _resolution = config.volumeResolution;
        }

        #endregion

        #region 2D to 3D Conversion

        /// <summary>
        /// Convert a 2D radar texture to 3D volume data
        /// </summary>
        public WeatherVolumeData ConvertRadarTexture(
            Texture2D radarTexture,
            Vector3 centerPosition,
            float rangeNM,
            float heading = 0f)
        {
            if (radarTexture == null)
            {
                Debug.LogWarning("[WeatherDataMapper] Null radar texture provided");
                return null;
            }

            var volumeData = new WeatherVolumeData(_resolution);
            volumeData.CoverageNM = rangeNM;
            volumeData.MaxAltitudeFt = _config.maxAltitudeFt;
            volumeData.MinAltitudeFt = _config.minAltitudeFt;
            volumeData.CenterPosition = centerPosition;
            volumeData.Heading = heading;
            volumeData.DataSource = "RadarTexture";
            volumeData.IsSimulated = false;
            volumeData.LastUpdateTime = Time.time;

            // Calculate world bounds
            float coverageMeters = rangeNM * WeatherVolumeData.METERS_PER_NM;
            float altitudeMeters = _config.maxAltitudeFt * WeatherVolumeData.METERS_PER_FT;
            volumeData.WorldBounds = new Bounds(
                centerPosition + Vector3.up * altitudeMeters * 0.5f,
                new Vector3(coverageMeters * 2f, altitudeMeters, coverageMeters * 2f)
            );

            // Get radar pixels
            Color[] pixels = radarTexture.GetPixels();
            int texWidth = radarTexture.width;
            int texHeight = radarTexture.height;

            // Track significant cells for weather cell generation
            List<Vector2Int> significantCells = new List<Vector2Int>();

            // Map texture to volume (XZ plane, then extrude to Y)
            for (int vx = 0; vx < _resolution.x; vx++)
            {
                for (int vz = 0; vz < _resolution.z; vz++)
                {
                    // Map volume coordinate to texture coordinate
                    float u = vx / (float)(_resolution.x - 1);
                    float v = vz / (float)(_resolution.z - 1);

                    int texX = Mathf.RoundToInt(u * (texWidth - 1));
                    int texY = Mathf.RoundToInt(v * (texHeight - 1));
                    int pixelIndex = texY * texWidth + texX;

                    if (pixelIndex < 0 || pixelIndex >= pixels.Length)
                        continue;

                    Color pixel = pixels[pixelIndex];
                    float intensity = ExtractIntensityFromColor(pixel);

                    if (intensity > 0.05f)
                    {
                        significantCells.Add(new Vector2Int(vx, vz));
                        
                        // Extrude vertically based on intensity
                        ExtrudeVertically(volumeData, vx, vz, intensity, pixel);
                    }
                }
            }

            // Generate discrete weather cells
            GenerateWeatherCells(volumeData, significantCells, radarTexture);

            // Generate cloud layers
            GenerateCloudLayers(volumeData);

            // Update the 3D texture
            volumeData.UpdateDensityTexture();

            return volumeData;
        }

        /// <summary>
        /// Extract intensity value (0-1) from radar pixel color
        /// Uses aviation weather radar color scale
        /// </summary>
        private float ExtractIntensityFromColor(Color pixel)
        {
            if (pixel.a < 0.1f)
                return 0f;

            // Aviation radar color analysis
            // Green = Light, Yellow = Moderate, Orange = Heavy, Red = Intense, Magenta = Extreme
            
            float r = pixel.r;
            float g = pixel.g;
            float b = pixel.b;

            // Score each color range
            float greenScore = g - Mathf.Max(r, b) * 0.5f;
            float yellowScore = Mathf.Min(r, g) - b * 0.5f;
            float orangeScore = r - g * 0.8f - b * 0.5f;
            float redScore = r - Mathf.Max(g, b) * 0.3f;
            float magentaScore = Mathf.Min(r, b) - g * 0.3f;

            // Calculate weighted intensity
            float intensity;
            
            if (magentaScore > 0.2f)
                intensity = 0.85f + Mathf.Clamp01(magentaScore) * 0.15f;
            else if (redScore > 0.3f)
                intensity = 0.65f + Mathf.Clamp01(redScore) * 0.2f;
            else if (orangeScore > 0.2f)
                intensity = 0.45f + Mathf.Clamp01(orangeScore) * 0.2f;
            else if (yellowScore > 0.2f)
                intensity = 0.25f + Mathf.Clamp01(yellowScore) * 0.2f;
            else if (greenScore > 0.1f)
                intensity = 0.05f + Mathf.Clamp01(greenScore) * 0.2f;
            else
                intensity = (r + g + b) / 3f * 0.25f;

            return Mathf.Clamp01(intensity);
        }

        /// <summary>
        /// Extrude 2D intensity vertically into the volume using meteorological profiles
        /// </summary>
        private void ExtrudeVertically(WeatherVolumeData data, int vx, int vz, float intensity, Color pixel)
        {
            // Get altitude range based on intensity
            GetAltitudeRange(intensity, out float baseAltFt, out float topAltFt);

            // Apply height extrusion multiplier
            if (_config.enableHeightExtrusion)
            {
                topAltFt = Mathf.Min(topAltFt * _config.heightExtrusionMultiplier, _config.maxAltitudeFt);
            }

            // Convert altitudes to Y indices
            int baseY = data.AltitudeToVoxelY(baseAltFt);
            int topY = data.AltitudeToVoxelY(topAltFt);

            // Determine weather type from intensity
            WeatherType weatherType = IntensityToWeatherType(intensity);

            // Vertical distribution profile (bell curve centered around mid-altitude)
            for (int vy = baseY; vy <= topY; vy++)
            {
                // Calculate height factor for bell curve distribution
                float normalizedHeight = (vy - baseY) / (float)Mathf.Max(1, topY - baseY);
                float heightFactor = CalculateVerticalProfile(normalizedHeight, intensity);

                float voxelDensity = intensity * heightFactor;
                
                // Set the data
                data.SetDensity(vx, vy, vz, voxelDensity);
                data.SetWeatherType(vx, vy, vz, weatherType);
                
                // Turbulence increases with intensity
                if (intensity > 0.4f)
                {
                    float turbulence = (intensity - 0.4f) / 0.6f * heightFactor;
                    data.SetTurbulence(vx, vy, vz, turbulence);
                }
            }
        }

        /// <summary>
        /// Calculate vertical density profile based on normalized height and intensity
        /// </summary>
        private float CalculateVerticalProfile(float normalizedHeight, float intensity)
        {
            // Use AnimationCurve from config if available
            if (_config.heightDensityCurve != null && _config.heightDensityCurve.keys.Length > 0)
            {
                return _config.heightDensityCurve.Evaluate(normalizedHeight);
            }

            // Default: bell curve with peak at 30% height (cloud base denser than top)
            float peakHeight = 0.3f;
            float spread = 0.5f + intensity * 0.3f; // Higher intensity = wider spread

            float dist = Mathf.Abs(normalizedHeight - peakHeight);
            float factor = Mathf.Exp(-dist * dist / (spread * spread));

            // Ensure some density at base
            float baseFactor = Mathf.Max(0.2f, 1f - normalizedHeight * 0.5f);

            return Mathf.Lerp(baseFactor, factor, 0.7f);
        }

        /// <summary>
        /// Get altitude range based on intensity level
        /// </summary>
        private void GetAltitudeRange(float intensity, out float baseAlt, out float topAlt)
        {
            if (intensity < 0.3f)
            {
                baseAlt = _config.lightPrecipMinHeight;
                topAlt = _config.lightPrecipMaxHeight;
            }
            else if (intensity < 0.5f)
            {
                baseAlt = _config.moderatePrecipMinHeight;
                topAlt = _config.moderatePrecipMaxHeight;
            }
            else if (intensity < 0.7f)
            {
                baseAlt = _config.heavyPrecipMinHeight;
                topAlt = _config.heavyPrecipMaxHeight;
            }
            else
            {
                baseAlt = _config.thunderstormMinHeight;
                topAlt = _config.thunderstormMaxHeight;
            }
        }

        /// <summary>
        /// Convert intensity to weather type
        /// </summary>
        private WeatherType IntensityToWeatherType(float intensity)
        {
            if (intensity < 0.2f)
                return WeatherType.LightRain;
            else if (intensity < 0.4f)
                return WeatherType.ModerateRain;
            else if (intensity < 0.6f)
                return WeatherType.HeavyRain;
            else if (intensity < 0.8f)
                return WeatherType.Thunderstorm;
            else
                return WeatherType.SevereThunderstorm;
        }

        #endregion

        #region Weather Cell Generation

        /// <summary>
        /// Generate discrete weather cells from clusters of significant voxels
        /// </summary>
        private void GenerateWeatherCells(WeatherVolumeData data, List<Vector2Int> significantCells, Texture2D texture)
        {
            if (significantCells.Count == 0) return;

            HashSet<Vector2Int> processed = new HashSet<Vector2Int>();
            Color[] pixels = texture.GetPixels();
            int texWidth = texture.width;
            int texHeight = texture.height;

            foreach (var cell in significantCells)
            {
                if (processed.Contains(cell)) continue;

                // Flood fill to find connected cells
                List<Vector2Int> cluster = new List<Vector2Int>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(cell);

                while (queue.Count > 0 && cluster.Count < 200)
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

                // Create weather cell from cluster
                if (cluster.Count >= 3)
                {
                    CreateWeatherCellFromCluster(data, cluster, pixels, texWidth, texHeight);
                }
            }
        }

        /// <summary>
        /// Create a WeatherCellInfo from a cluster of voxel coordinates
        /// </summary>
        private void CreateWeatherCellFromCluster(
            WeatherVolumeData data,
            List<Vector2Int> cluster,
            Color[] pixels,
            int texWidth,
            int texHeight)
        {
            // Calculate bounds and intensity
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

                // Sample texture
                float u = gridPos.x / (float)(_resolution.x - 1);
                float v = gridPos.y / (float)(_resolution.z - 1);
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

            // Calculate world position
            Vector2 centerGrid = (minPos + maxPos) * 0.5f;
            Vector3 worldPos = data.VoxelToWorld(
                Mathf.RoundToInt(centerGrid.x),
                _resolution.y / 2,
                Mathf.RoundToInt(centerGrid.y)
            );

            // Get altitude range
            GetAltitudeRange(maxIntensity, out float baseAlt, out float topAlt);

            // Calculate size
            float coverageMeters = data.CoverageNM * WeatherVolumeData.METERS_PER_NM * 2f;
            float sizeX = (maxPos.x - minPos.x + 1) / _resolution.x * coverageMeters;
            float sizeZ = (maxPos.y - minPos.y + 1) / _resolution.z * coverageMeters;
            float sizeY = (topAlt - baseAlt) * WeatherVolumeData.METERS_PER_FT;

            var cellInfo = new WeatherCellInfo
            {
                Position = worldPos,
                Size = new Vector3(Mathf.Max(sizeX, 1000f), Mathf.Max(sizeY, 1000f), Mathf.Max(sizeZ, 1000f)),
                Intensity = avgIntensity,
                Type = IntensityToWeatherType(maxIntensity),
                BaseAltitudeFt = baseAlt,
                TopAltitudeFt = topAlt,
                HasLightning = maxIntensity > 0.6f,
                TurbulenceLevel = maxIntensity > 0.4f ? (maxIntensity - 0.4f) / 0.6f : 0f
            };

            data.WeatherCells.Add(cellInfo);
        }

        /// <summary>
        /// Generate cloud layers based on weather cells
        /// </summary>
        private void GenerateCloudLayers(WeatherVolumeData data)
        {
            if (data.WeatherCells.Count == 0) return;

            // Find overall extent
            float minBase = float.MaxValue;
            float maxTop = float.MinValue;
            float totalCoverage = 0f;

            foreach (var cell in data.WeatherCells)
            {
                minBase = Mathf.Min(minBase, cell.BaseAltitudeFt);
                maxTop = Mathf.Max(maxTop, cell.TopAltitudeFt);
                totalCoverage += cell.Intensity;
            }

            float avgCoverage = totalCoverage / data.WeatherCells.Count;

            // Main cloud layer
            data.CloudLayers.Add(new CloudLayerInfo
            {
                BaseAltitudeFt = minBase,
                TopAltitudeFt = maxTop,
                Coverage = Mathf.Clamp01(avgCoverage * 1.5f),
                Type = maxTop > 30000f ? CloudLayerType.Cumulonimbus : CloudLayerType.Cumulus,
                TintColor = Color.white
            });

            // Add anvil layer for severe storms
            bool hasSevere = false;
            foreach (var cell in data.WeatherCells)
            {
                if (cell.Type == WeatherType.Thunderstorm || cell.Type == WeatherType.SevereThunderstorm)
                {
                    hasSevere = true;
                    break;
                }
            }

            if (hasSevere && maxTop > 35000f)
            {
                data.CloudLayers.Add(new CloudLayerInfo
                {
                    BaseAltitudeFt = 35000f,
                    TopAltitudeFt = Mathf.Min(50000f, maxTop + 5000f),
                    Coverage = 0.5f,
                    Type = CloudLayerType.Cirrus,
                    TintColor = new Color(0.95f, 0.95f, 1f, 0.7f)
                });
            }
        }

        #endregion

        #region Direct 3D Data Generation

        /// <summary>
        /// Generate 3D volume directly from weather cell definitions
        /// Used by simulation system
        /// </summary>
        public void GenerateFromCells(WeatherVolumeData data, List<WeatherCellInfo> cells)
        {
            data.Clear();
            data.LastUpdateTime = Time.time;

            foreach (var cell in cells)
            {
                AddCellToVolume(data, cell);
            }

            // Copy cells to data
            data.WeatherCells.AddRange(cells);
            GenerateCloudLayers(data);
            data.UpdateDensityTexture();
        }

        /// <summary>
        /// Add a single weather cell to the volume
        /// </summary>
        private void AddCellToVolume(WeatherVolumeData data, WeatherCellInfo cell)
        {
            // Calculate voxel bounds of the cell
            Vector3 cellMin = cell.Position - cell.Size * 0.5f;
            Vector3 cellMax = cell.Position + cell.Size * 0.5f;

            Vector3Int voxelMin = data.WorldToVoxel(cellMin);
            Vector3Int voxelMax = data.WorldToVoxel(cellMax);

            // Fill voxels within the cell
            for (int x = voxelMin.x; x <= voxelMax.x; x++)
            {
                for (int y = voxelMin.y; y <= voxelMax.y; y++)
                {
                    for (int z = voxelMin.z; z <= voxelMax.z; z++)
                    {
                        if (!data.IsValidCoordinate(x, y, z)) continue;

                        Vector3 voxelWorld = data.VoxelToWorld(x, y, z);
                        Vector3 localPos = voxelWorld - cell.Position;

                        // Ellipsoid distance
                        float3 normalizedPos = new float3(
                            localPos.x / (cell.Size.x * 0.5f),
                            localPos.y / (cell.Size.y * 0.5f),
                            localPos.z / (cell.Size.z * 0.5f)
                        );
                        float dist = Mathf.Sqrt(normalizedPos.x * normalizedPos.x + 
                                               normalizedPos.y * normalizedPos.y + 
                                               normalizedPos.z * normalizedPos.z);

                        if (dist > 1f) continue;

                        // Density falls off from center
                        float falloff = 1f - Mathf.Pow(dist, 0.7f);
                        float density = cell.Intensity * falloff;

                        // Vertical profile adjustment
                        float normalizedHeight = (y - voxelMin.y) / (float)Mathf.Max(1, voxelMax.y - voxelMin.y);
                        float verticalFactor = CalculateVerticalProfile(normalizedHeight, cell.Intensity);
                        density *= verticalFactor;

                        // Combine with existing (max blend)
                        float existing = data.GetDensity(x, y, z);
                        data.SetDensity(x, y, z, Mathf.Max(existing, density));
                        data.SetWeatherType(x, y, z, cell.Type);
                        data.SetTurbulence(x, y, z, Mathf.Max(data.GetTurbulence(x, y, z), cell.TurbulenceLevel * falloff));
                    }
                }
            }
        }

        // Helper struct for vector operations in shader-like code
        private struct float3
        {
            public float x, y, z;
            public float3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        }

        #endregion
    }
}
