using UnityEngine;
using System;
using System.Collections.Generic;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Core data container for 3D volumetric weather visualization.
    /// Contains 3D textures and metadata for raymarching shaders.
    /// Designed for high performance and accurate data representation.
    /// </summary>
    [Serializable]
    public class WeatherVolumeData : IDisposable
    {
        #region Volume Configuration

        /// <summary>
        /// Resolution of the 3D volume (X = width, Y = height/altitude, Z = depth)
        /// </summary>
        public Vector3Int Resolution { get; private set; }

        /// <summary>
        /// World-space bounds of the weather volume
        /// </summary>
        public Bounds WorldBounds { get; set; }

        /// <summary>
        /// Coverage range in nautical miles (radius from center)
        /// </summary>
        public float CoverageNM { get; set; } = 80f;

        /// <summary>
        /// Maximum altitude in feet
        /// </summary>
        public float MaxAltitudeFt { get; set; } = 50000f;

        /// <summary>
        /// Minimum altitude in feet
        /// </summary>
        public float MinAltitudeFt { get; set; } = 0f;

        #endregion

        #region Volume Textures

        /// <summary>
        /// 3D texture containing weather intensity/density values (0-1)
        /// R channel = intensity, G channel = weather type encoded, B = turbulence, A = lightning probability
        /// </summary>
        public Texture3D DensityVolume { get; private set; }

        /// <summary>
        /// Raw density data array for CPU-side manipulation
        /// </summary>
        public float[,,] DensityData { get; private set; }

        /// <summary>
        /// Weather type data array (encoded as WeatherType enum value / 255)
        /// </summary>
        public float[,,] TypeData { get; private set; }

        /// <summary>
        /// Turbulence intensity data array
        /// </summary>
        public float[,,] TurbulenceData { get; private set; }

        #endregion

        #region Weather Cells

        /// <summary>
        /// List of identified weather cells for effects and analysis
        /// </summary>
        public List<WeatherCellInfo> WeatherCells { get; private set; } = new List<WeatherCellInfo>();

        /// <summary>
        /// List of cloud layers at different altitudes
        /// </summary>
        public List<CloudLayerInfo> CloudLayers { get; private set; } = new List<CloudLayerInfo>();

        #endregion

        #region Reference Position

        /// <summary>
        /// Center position of the volume in world space
        /// </summary>
        public Vector3 CenterPosition { get; set; }

        /// <summary>
        /// Latitude of center position (degrees)
        /// </summary>
        public float CenterLatitude { get; set; }

        /// <summary>
        /// Longitude of center position (degrees)
        /// </summary>
        public float CenterLongitude { get; set; }

        /// <summary>
        /// Reference heading in degrees (0 = North)
        /// </summary>
        public float Heading { get; set; }

        /// <summary>
        /// Reference altitude in feet
        /// </summary>
        public float AltitudeFt { get; set; }

        #endregion

        #region Metadata

        /// <summary>
        /// Time when data was last updated
        /// </summary>
        public float LastUpdateTime { get; set; }

        /// <summary>
        /// Age of the data in seconds
        /// </summary>
        public float DataAge => Time.time - LastUpdateTime;

        /// <summary>
        /// Source of this data
        /// </summary>
        public string DataSource { get; set; } = "Unknown";

        /// <summary>
        /// Whether this data is from simulation (vs real data)
        /// </summary>
        public bool IsSimulated { get; set; }

        #endregion

        #region Constants

        /// <summary>Meters per nautical mile</summary>
        public const float METERS_PER_NM = 1852f;

        /// <summary>Meters per foot</summary>
        public const float METERS_PER_FT = 0.3048f;

        /// <summary>Feet per meter</summary>
        public const float FT_PER_METER = 3.28084f;

        #endregion

        private bool _isDisposed = false;

        #region Construction

        /// <summary>
        /// Create a new WeatherVolumeData with specified resolution
        /// </summary>
        public WeatherVolumeData(Vector3Int resolution)
        {
            Resolution = resolution;
            InitializeArrays();
        }

        /// <summary>
        /// Create with default resolution (64x32x64)
        /// </summary>
        public WeatherVolumeData() : this(new Vector3Int(64, 32, 64))
        {
        }
        
        /// <summary>
        /// Create with explicit resolution and world bounds for simulation
        /// </summary>
        public WeatherVolumeData(int resX, int resY, int resZ, Vector3 worldOrigin, Vector3 worldSize)
            : this(new Vector3Int(resX, resY, resZ))
        {
            CenterPosition = worldOrigin + worldSize * 0.5f;
            
            // Calculate coverage from world size (assumes XZ are equal for circular coverage)
            float coverageMeters = Mathf.Max(worldSize.x, worldSize.z) * 0.5f;
            CoverageNM = coverageMeters / METERS_PER_NM;
            
            // Calculate altitude range from world size Y
            MinAltitudeFt = worldOrigin.y * FT_PER_METER;
            MaxAltitudeFt = (worldOrigin.y + worldSize.y) * FT_PER_METER;
            
            WorldBounds = new Bounds(CenterPosition, worldSize);
            IsSimulated = true;
            DataSource = "Simulation";
        }

        private void InitializeArrays()
        {
            DensityData = new float[Resolution.x, Resolution.y, Resolution.z];
            TypeData = new float[Resolution.x, Resolution.y, Resolution.z];
            TurbulenceData = new float[Resolution.x, Resolution.y, Resolution.z];
        }

        #endregion

        #region Data Access

        /// <summary>
        /// Get density at specific voxel coordinates
        /// </summary>
        public float GetDensity(int x, int y, int z)
        {
            if (!IsValidCoordinate(x, y, z)) return 0f;
            return DensityData[x, y, z];
        }

        /// <summary>
        /// Set density at specific voxel coordinates
        /// </summary>
        public void SetDensity(int x, int y, int z, float density)
        {
            if (!IsValidCoordinate(x, y, z)) return;
            DensityData[x, y, z] = Mathf.Clamp01(density);
        }

        /// <summary>
        /// Get weather type at specific voxel coordinates
        /// </summary>
        public WeatherType GetWeatherType(int x, int y, int z)
        {
            if (!IsValidCoordinate(x, y, z)) return WeatherType.Clear;
            return (WeatherType)Mathf.RoundToInt(TypeData[x, y, z] * 255f);
        }

        /// <summary>
        /// Set weather type at specific voxel coordinates
        /// </summary>
        public void SetWeatherType(int x, int y, int z, WeatherType type)
        {
            if (!IsValidCoordinate(x, y, z)) return;
            TypeData[x, y, z] = (float)type / 255f;
        }

        /// <summary>
        /// Get turbulence at specific voxel coordinates
        /// </summary>
        public float GetTurbulence(int x, int y, int z)
        {
            if (!IsValidCoordinate(x, y, z)) return 0f;
            return TurbulenceData[x, y, z];
        }

        /// <summary>
        /// Set turbulence at specific voxel coordinates
        /// </summary>
        public void SetTurbulence(int x, int y, int z, float turbulence)
        {
            if (!IsValidCoordinate(x, y, z)) return;
            TurbulenceData[x, y, z] = Mathf.Clamp01(turbulence);
        }

        /// <summary>
        /// Check if coordinate is valid
        /// </summary>
        public bool IsValidCoordinate(int x, int y, int z)
        {
            return x >= 0 && x < Resolution.x &&
                   y >= 0 && y < Resolution.y &&
                   z >= 0 && z < Resolution.z;
        }

        #endregion

        #region Coordinate Conversion

        /// <summary>
        /// Convert world position to voxel coordinates
        /// </summary>
        public Vector3Int WorldToVoxel(Vector3 worldPos)
        {
            Vector3 localPos = worldPos - CenterPosition;
            float halfCoverageMeters = CoverageNM * METERS_PER_NM;
            float altitudeMeters = MaxAltitudeFt * METERS_PER_FT;

            // Normalize to 0-1 range
            float nx = (localPos.x / halfCoverageMeters + 1f) * 0.5f;
            float ny = localPos.y / altitudeMeters;
            float nz = (localPos.z / halfCoverageMeters + 1f) * 0.5f;

            return new Vector3Int(
                Mathf.Clamp(Mathf.RoundToInt(nx * Resolution.x), 0, Resolution.x - 1),
                Mathf.Clamp(Mathf.RoundToInt(ny * Resolution.y), 0, Resolution.y - 1),
                Mathf.Clamp(Mathf.RoundToInt(nz * Resolution.z), 0, Resolution.z - 1)
            );
        }

        /// <summary>
        /// Convert voxel coordinates to world position
        /// </summary>
        public Vector3 VoxelToWorld(int x, int y, int z)
        {
            float halfCoverageMeters = CoverageNM * METERS_PER_NM;
            float altitudeMeters = MaxAltitudeFt * METERS_PER_FT;

            // Normalize coordinates
            float nx = x / (float)Resolution.x;
            float ny = y / (float)Resolution.y;
            float nz = z / (float)Resolution.z;

            return new Vector3(
                (nx * 2f - 1f) * halfCoverageMeters + CenterPosition.x,
                ny * altitudeMeters + CenterPosition.y,
                (nz * 2f - 1f) * halfCoverageMeters + CenterPosition.z
            );
        }

        /// <summary>
        /// Convert normalized UV coordinates to voxel coordinates
        /// </summary>
        public Vector3Int UVToVoxel(Vector3 uv)
        {
            return new Vector3Int(
                Mathf.Clamp(Mathf.RoundToInt(uv.x * Resolution.x), 0, Resolution.x - 1),
                Mathf.Clamp(Mathf.RoundToInt(uv.y * Resolution.y), 0, Resolution.y - 1),
                Mathf.Clamp(Mathf.RoundToInt(uv.z * Resolution.z), 0, Resolution.z - 1)
            );
        }

        /// <summary>
        /// Convert altitude in feet to Y voxel coordinate
        /// </summary>
        public int AltitudeToVoxelY(float altitudeFt)
        {
            float normalizedAlt = (altitudeFt - MinAltitudeFt) / (MaxAltitudeFt - MinAltitudeFt);
            return Mathf.Clamp(Mathf.RoundToInt(normalizedAlt * Resolution.y), 0, Resolution.y - 1);
        }

        /// <summary>
        /// Convert Y voxel coordinate to altitude in feet
        /// </summary>
        public float VoxelYToAltitude(int y)
        {
            float normalizedAlt = y / (float)Resolution.y;
            return MinAltitudeFt + normalizedAlt * (MaxAltitudeFt - MinAltitudeFt);
        }

        #endregion

        #region Texture Generation

        /// <summary>
        /// Generate or update the 3D density texture from data arrays
        /// </summary>
        public void UpdateDensityTexture()
        {
            if (DensityVolume == null || 
                DensityVolume.width != Resolution.x || 
                DensityVolume.height != Resolution.y || 
                DensityVolume.depth != Resolution.z)
            {
                if (DensityVolume != null)
                {
                    UnityEngine.Object.Destroy(DensityVolume);
                }
                
                DensityVolume = new Texture3D(Resolution.x, Resolution.y, Resolution.z, TextureFormat.RGBA32, false);
                DensityVolume.wrapMode = TextureWrapMode.Clamp;
                DensityVolume.filterMode = FilterMode.Trilinear;
                DensityVolume.name = "WeatherDensityVolume";
            }

            // Create color array for texture
            Color[] colors = new Color[Resolution.x * Resolution.y * Resolution.z];
            int index = 0;

            for (int z = 0; z < Resolution.z; z++)
            {
                for (int y = 0; y < Resolution.y; y++)
                {
                    for (int x = 0; x < Resolution.x; x++)
                    {
                        colors[index++] = new Color(
                            DensityData[x, y, z],      // R = density
                            TypeData[x, y, z],          // G = weather type
                            TurbulenceData[x, y, z],    // B = turbulence
                            DensityData[x, y, z] > 0.6f ? 1f : 0f  // A = lightning probability
                        );
                    }
                }
            }

            DensityVolume.SetPixels(colors);
            DensityVolume.Apply();
        }
        
        /// <summary>
        /// Alias for UpdateDensityTexture() - updates all volume textures
        /// </summary>
        public void UpdateTextures()
        {
            UpdateDensityTexture();
        }

        #endregion

        #region Data Operations

        /// <summary>
        /// Clear all data to zero
        /// </summary>
        public void Clear()
        {
            Array.Clear(DensityData, 0, DensityData.Length);
            Array.Clear(TypeData, 0, TypeData.Length);
            Array.Clear(TurbulenceData, 0, TurbulenceData.Length);
            WeatherCells.Clear();
            CloudLayers.Clear();
        }

        /// <summary>
        /// Create a deep copy of this data
        /// </summary>
        public WeatherVolumeData Clone()
        {
            var clone = new WeatherVolumeData(Resolution)
            {
                WorldBounds = WorldBounds,
                CoverageNM = CoverageNM,
                MaxAltitudeFt = MaxAltitudeFt,
                MinAltitudeFt = MinAltitudeFt,
                CenterPosition = CenterPosition,
                CenterLatitude = CenterLatitude,
                CenterLongitude = CenterLongitude,
                Heading = Heading,
                AltitudeFt = AltitudeFt,
                LastUpdateTime = LastUpdateTime,
                DataSource = DataSource,
                IsSimulated = IsSimulated
            };

            Array.Copy(DensityData, clone.DensityData, DensityData.Length);
            Array.Copy(TypeData, clone.TypeData, TypeData.Length);
            Array.Copy(TurbulenceData, clone.TurbulenceData, TurbulenceData.Length);
            clone.WeatherCells.AddRange(WeatherCells);
            clone.CloudLayers.AddRange(CloudLayers);

            return clone;
        }

        /// <summary>
        /// Calculate statistics about the current data
        /// </summary>
        public WeatherVolumeStats CalculateStats()
        {
            var stats = new WeatherVolumeStats();
            float totalDensity = 0f;
            int nonZeroCount = 0;

            for (int z = 0; z < Resolution.z; z++)
            {
                for (int y = 0; y < Resolution.y; y++)
                {
                    for (int x = 0; x < Resolution.x; x++)
                    {
                        float density = DensityData[x, y, z];
                        if (density > 0.01f)
                        {
                            nonZeroCount++;
                            totalDensity += density;
                            stats.MaxDensity = Mathf.Max(stats.MaxDensity, density);
                        }
                    }
                }
            }

            stats.AverageDensity = nonZeroCount > 0 ? totalDensity / nonZeroCount : 0f;
            stats.CoveragePercent = (float)nonZeroCount / (Resolution.x * Resolution.y * Resolution.z) * 100f;
            stats.CellCount = WeatherCells.Count;

            return stats;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed) return;

            if (DensityVolume != null)
            {
                UnityEngine.Object.Destroy(DensityVolume);
                DensityVolume = null;
            }

            DensityData = null;
            TypeData = null;
            TurbulenceData = null;
            WeatherCells = null;
            CloudLayers = null;

            _isDisposed = true;
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Types of weather for classification
    /// </summary>
    public enum WeatherType : byte
    {
        Clear = 0,
        LightRain = 1,
        ModerateRain = 2,
        HeavyRain = 3,
        Thunderstorm = 4,
        SevereThunderstorm = 5,
        LightSnow = 6,
        ModerateSnow = 7,
        HeavySnow = 8,
        MixedPrecipitation = 9,
        Hail = 10,
        Fog = 11,
        Mist = 12
    }

    /// <summary>
    /// Intensity level classification
    /// </summary>
    public enum IntensityLevel
    {
        None = 0,
        Light = 1,      // Green on radar
        Moderate = 2,   // Yellow on radar
        Heavy = 3,      // Orange on radar
        Intense = 4,    // Red on radar
        Extreme = 5     // Magenta on radar
    }

    /// <summary>
    /// Information about a discrete weather cell
    /// </summary>
    [Serializable]
    public class WeatherCellInfo
    {
        public Vector3 Position;
        public Vector3 Size;
        public float Intensity;
        public WeatherType Type;
        public float BaseAltitudeFt;
        public float TopAltitudeFt;
        public bool HasLightning;
        public float TurbulenceLevel;
        public Vector3 MovementVector;

        public IntensityLevel GetIntensityLevel()
        {
            if (Intensity < 0.2f) return IntensityLevel.Light;
            if (Intensity < 0.4f) return IntensityLevel.Moderate;
            if (Intensity < 0.6f) return IntensityLevel.Heavy;
            if (Intensity < 0.8f) return IntensityLevel.Intense;
            return IntensityLevel.Extreme;
        }

        public Color GetIntensityColor()
        {
            if (Intensity < 0.2f) return new Color(0f, 0.8f, 0f, 0.6f);      // Green
            if (Intensity < 0.4f) return new Color(1f, 1f, 0f, 0.7f);        // Yellow
            if (Intensity < 0.6f) return new Color(1f, 0.5f, 0f, 0.8f);      // Orange
            if (Intensity < 0.8f) return new Color(1f, 0f, 0f, 0.9f);        // Red
            return new Color(1f, 0f, 1f, 1f);                                 // Magenta
        }
    }

    /// <summary>
    /// Information about a cloud layer
    /// </summary>
    [Serializable]
    public class CloudLayerInfo
    {
        public float BaseAltitudeFt;
        public float TopAltitudeFt;
        public float Coverage;
        public CloudLayerType Type;
        public Color TintColor;
    }

    /// <summary>
    /// Types of cloud layers
    /// </summary>
    public enum CloudLayerType
    {
        Cirrus,
        Altocumulus,
        Stratus,
        Cumulus,
        Cumulonimbus,
        Nimbostratus
    }

    /// <summary>
    /// Statistics about weather volume data
    /// </summary>
    public struct WeatherVolumeStats
    {
        public float MaxDensity;
        public float AverageDensity;
        public float CoveragePercent;
        public int CellCount;
    }

    #endregion
}
