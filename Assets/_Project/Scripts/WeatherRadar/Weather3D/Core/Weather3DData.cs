using UnityEngine;
using System;
using System.Collections.Generic;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Represents a 3D weather cell with position, size, and intensity data.
    /// </summary>
    [Serializable]
    public class WeatherCell3D
    {
        public Vector3 position;           // World position
        public Vector3 size;               // Cell dimensions
        public float intensity;            // 0-1 normalized intensity
        public WeatherCellType cellType;   // Type of weather
        public float altitude;             // Base altitude in feet
        public float topAltitude;          // Top altitude in feet
        public bool hasLightning;          // Lightning present
        public float turbulenceLevel;      // 0-1 turbulence intensity
        
        public Color GetIntensityColor()
        {
            // Aviation standard color scheme
            if (intensity < 0.2f)
                return new Color(0f, 0.8f, 0f, 0.6f);      // Green - Light
            else if (intensity < 0.4f)
                return new Color(1f, 1f, 0f, 0.7f);        // Yellow - Moderate
            else if (intensity < 0.6f)
                return new Color(1f, 0.5f, 0f, 0.8f);      // Orange - Heavy
            else if (intensity < 0.8f)
                return new Color(1f, 0f, 0f, 0.9f);        // Red - Intense
            else
                return new Color(1f, 0f, 1f, 1f);          // Magenta - Extreme
        }
    }

    /// <summary>
    /// Types of weather cells for visualization
    /// </summary>
    public enum WeatherCellType
    {
        LightRain,
        ModerateRain,
        HeavyRain,
        Thunderstorm,
        Snow,
        MixedPrecipitation,
        Hail
    }

    /// <summary>
    /// Represents a cloud layer at a specific altitude range
    /// </summary>
    [Serializable]
    public class CloudLayer
    {
        public float baseAltitude;         // Feet MSL
        public float topAltitude;          // Feet MSL
        public float coverage;             // 0-1 coverage amount
        public CloudLayerType layerType;   // Type of cloud layer
        public Color tintColor;            // Color tint for the layer
    }

    /// <summary>
    /// Types of cloud layers
    /// </summary>
    public enum CloudLayerType
    {
        Cirrus,          // High, wispy
        Altocumulus,     // Medium, puffy
        Stratus,         // Low, flat
        Cumulus,         // Building, puffy
        Cumulonimbus,    // Towering, thunderstorm
        Nimbostratus     // Rain-producing, dark
    }

    /// <summary>
    /// 3D weather data container - central data structure for 3D visualization
    /// </summary>
    [Serializable]
    public class Weather3DData
    {
        [Header("Voxel Grid")]
        [Tooltip("3D grid of precipitation intensity values (0-1)")]
        public float[,,] intensityGrid;
        
        [Tooltip("Grid dimensions")]
        public Vector3Int gridSize = new Vector3Int(64, 16, 64);
        
        [Tooltip("World size covered by the grid in nautical miles")]
        public float coverageNM = 80f;
        
        [Tooltip("Maximum altitude in feet")]
        public float maxAltitudeFt = 50000f;

        [Header("Weather Cells")]
        [Tooltip("List of identified weather cells")]
        public List<WeatherCell3D> weatherCells = new List<WeatherCell3D>();
        
        [Header("Cloud Layers")]
        [Tooltip("Cloud layers at different altitudes")]
        public List<CloudLayer> cloudLayers = new List<CloudLayer>();

        [Header("Aircraft Reference")]
        public Vector3 aircraftPosition;
        public float aircraftHeading;
        public float aircraftAltitude;

        [Header("Time Data")]
        public float lastUpdateTime;
        public float dataAge;

        /// <summary>
        /// Initialize the intensity grid
        /// </summary>
        public void InitializeGrid()
        {
            intensityGrid = new float[gridSize.x, gridSize.y, gridSize.z];
        }

        /// <summary>
        /// Get intensity at a specific grid position
        /// </summary>
        public float GetIntensityAt(int x, int y, int z)
        {
            if (intensityGrid == null) return 0f;
            if (x < 0 || x >= gridSize.x || y < 0 || y >= gridSize.y || z < 0 || z >= gridSize.z)
                return 0f;
            return intensityGrid[x, y, z];
        }

        /// <summary>
        /// Set intensity at a specific grid position
        /// </summary>
        public void SetIntensityAt(int x, int y, int z, float intensity)
        {
            if (intensityGrid == null) InitializeGrid();
            if (x < 0 || x >= gridSize.x || y < 0 || y >= gridSize.y || z < 0 || z >= gridSize.z)
                return;
            intensityGrid[x, y, z] = Mathf.Clamp01(intensity);
        }

        /// <summary>
        /// Convert world position to grid coordinates
        /// </summary>
        public Vector3Int WorldToGrid(Vector3 worldPos)
        {
            float halfCoverage = coverageNM * 0.5f;
            float nmToUnits = 1852f; // 1 NM = 1852 meters
            
            // Relative position from aircraft
            Vector3 relPos = worldPos - aircraftPosition;
            
            // Convert to grid coordinates
            int x = Mathf.RoundToInt((relPos.x / (halfCoverage * nmToUnits) + 1f) * 0.5f * gridSize.x);
            int y = Mathf.RoundToInt(relPos.y / maxAltitudeFt * gridSize.y);
            int z = Mathf.RoundToInt((relPos.z / (halfCoverage * nmToUnits) + 1f) * 0.5f * gridSize.z);
            
            return new Vector3Int(
                Mathf.Clamp(x, 0, gridSize.x - 1),
                Mathf.Clamp(y, 0, gridSize.y - 1),
                Mathf.Clamp(z, 0, gridSize.z - 1)
            );
        }

        /// <summary>
        /// Convert grid coordinates to world position
        /// </summary>
        public Vector3 GridToWorld(int x, int y, int z)
        {
            float halfCoverage = coverageNM * 0.5f;
            float nmToUnits = 1852f;
            
            float worldX = (x / (float)gridSize.x * 2f - 1f) * halfCoverage * nmToUnits + aircraftPosition.x;
            float worldY = y / (float)gridSize.y * maxAltitudeFt;
            float worldZ = (z / (float)gridSize.z * 2f - 1f) * halfCoverage * nmToUnits + aircraftPosition.z;
            
            return new Vector3(worldX, worldY, worldZ);
        }

        /// <summary>
        /// Clear all data
        /// </summary>
        public void Clear()
        {
            if (intensityGrid != null)
            {
                Array.Clear(intensityGrid, 0, intensityGrid.Length);
            }
            weatherCells.Clear();
            cloudLayers.Clear();
        }

        /// <summary>
        /// Create a deep copy
        /// </summary>
        public Weather3DData Clone()
        {
            var clone = new Weather3DData
            {
                gridSize = gridSize,
                coverageNM = coverageNM,
                maxAltitudeFt = maxAltitudeFt,
                aircraftPosition = aircraftPosition,
                aircraftHeading = aircraftHeading,
                aircraftAltitude = aircraftAltitude,
                lastUpdateTime = lastUpdateTime,
                dataAge = dataAge
            };
            
            clone.InitializeGrid();
            if (intensityGrid != null)
            {
                Array.Copy(intensityGrid, clone.intensityGrid, intensityGrid.Length);
            }
            
            clone.weatherCells = new List<WeatherCell3D>(weatherCells);
            clone.cloudLayers = new List<CloudLayer>(cloudLayers);
            
            return clone;
        }
    }
}
