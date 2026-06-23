using UnityEngine;
using System.Collections.Generic;

namespace Weather3D
{
    /// <summary>
    /// Weather data structure for 3D visualization.
    /// </summary>
    public class Weather3DData
    {
        public Vector3 AircraftPosition;
        public float AircraftAltitude;
        public float AircraftHeading;
        public float DataAge;
        public float CoverageRangeNM;
        public float MaxAltitudeFt;

        public List<WeatherCell3D> WeatherCells = new List<WeatherCell3D>();
        public List<StormCell3D> StormCells = new List<StormCell3D>();

        public Weather3DData()
        {
            WeatherCells = new List<WeatherCell3D>();
            StormCells = new List<StormCell3D>();
        }
    }

    /// <summary>
    /// Represents a single 3D weather cell.
    /// </summary>
    public class WeatherCell3D
    {
        public Vector3 Position;
        public Vector3 Size;
        public float Intensity;
        public WeatherCellType CellType;
        public float BaseAltitude;
        public float TopAltitude;

        public Color GetIntensityColor()
        {
            // Standard weather radar color mapping
            if (Intensity < 0.2f) return new Color(0.1f, 0.5f, 0.1f, 0.6f);      // Light - Green
            if (Intensity < 0.4f) return new Color(0.9f, 0.9f, 0.1f, 0.7f);      // Moderate - Yellow
            if (Intensity < 0.6f) return new Color(0.9f, 0.5f, 0.1f, 0.8f);      // Heavy - Orange
            if (Intensity < 0.8f) return new Color(0.9f, 0.1f, 0.1f, 0.9f);      // Severe - Red
            return new Color(0.8f, 0.1f, 0.8f, 1f);                              // Extreme - Magenta
        }
    }

    /// <summary>
    /// Represents a storm cell with lightning.
    /// </summary>
    public class StormCell3D
    {
        public Vector3 Position;
        public float Intensity;
        public bool HasLightning;
        public float LightningFrequency;
        public float TurbulenceLevel;
    }

    public enum WeatherCellType
    {
        LightRain,
        ModerateRain,
        HeavyRain,
        Thunderstorm,
        Hail
    }
}
