using UnityEngine;
using System;
using System.Collections.Generic;

namespace WeatherRadar
{
    /// <summary>
    /// Centralized container for all weather radar state.
    /// Single source of truth for radar display data.
    /// </summary>
    [Serializable]
    public class WeatherRadarData
    {
        [Header("Radar Returns")]
        [Tooltip("Current weather return texture")]
        public Texture2D radarReturns;
        
        [Tooltip("Raw reflectivity data (0-1 range)")]
        public float[,] reflectivityData;

        [Header("Scan State")]
        [Tooltip("Current sweep angle in degrees (0-360)")]
        [Range(0f, 360f)]
        public float sweepAngle;
        
        [Tooltip("Is the radar actively scanning")]
        public bool isScanning = true;
        
        [Tooltip("Scan direction (1 = clockwise, -1 = counter-clockwise)")]
        public int scanDirection = 1;

        [Header("Range Settings")]
        [Tooltip("Current range in nautical miles")]
        public float currentRange = 40f;
        
        [Tooltip("Available range options")]
        public static readonly float[] RangeOptions = { 5f, 10f, 20f, 40f, 80f, 160f, 320f };

        [Header("Antenna Settings")]
        [Tooltip("Antenna tilt angle in degrees (-15 to +15)")]
        [Range(-15f, 15f)]
        public float tiltAngle = 0f;
        
        [Tooltip("Gain offset in dB (-8 to +8)")]
        [Range(-8f, 8f)]
        public float gainOffset = 0f;

        [Header("Mode")]
        [Tooltip("Current radar mode")]
        public RadarMode currentMode = RadarMode.WX;

        [Header("Aircraft State")]
        [Tooltip("Aircraft heading in degrees")]
        [Range(0f, 360f)]
        public float heading = 0f;
        
        [Tooltip("Aircraft latitude")]
        public float latitude;
        
        [Tooltip("Aircraft longitude")]
        public float longitude;
        
        [Tooltip("Aircraft altitude in feet")]
        public float altitude;

        [Header("Navigation Overlay")]
        [Tooltip("Waypoints to display on radar")]
        public List<RadarWaypointData> waypoints = new List<RadarWaypointData>();
        
        [Tooltip("Show waypoint overlay")]
        public bool showWaypoints = true;

        [Header("Display Options")]
        [Tooltip("Show range rings")]
        public bool showRangeRings = true;
        
        [Tooltip("Show heading indicator")]
        public bool showHeadingIndicator = true;

        /// <summary>
        /// Get the current range index
        /// </summary>
        public int GetRangeIndex()
        {
            for (int i = 0; i < RangeOptions.Length; i++)
            {
                if (Mathf.Approximately(currentRange, RangeOptions[i]))
                    return i;
            }
            return 3; // Default to 40nm
        }

        /// <summary>
        /// Set range by index
        /// </summary>
        public void SetRangeByIndex(int index)
        {
            if (index >= 0 && index < RangeOptions.Length)
            {
                currentRange = RangeOptions[index];
            }
        }

        /// <summary>
        /// Increase range to next available option
        /// </summary>
        public void IncreaseRange()
        {
            int currentIndex = GetRangeIndex();
            if (currentIndex < RangeOptions.Length - 1)
            {
                currentRange = RangeOptions[currentIndex + 1];
            }
        }

        /// <summary>
        /// Decrease range to previous available option
        /// </summary>
        public void DecreaseRange()
        {
            int currentIndex = GetRangeIndex();
            if (currentIndex > 0)
            {
                currentRange = RangeOptions[currentIndex - 1];
            }
        }

        /// <summary>
        /// Create a copy of the radar data
        /// </summary>
        public WeatherRadarData Clone()
        {
            var clone = (WeatherRadarData)MemberwiseClone();
            clone.waypoints = new List<RadarWaypointData>(waypoints);
            return clone;
        }

        /// <summary>
        /// Reset to default values
        /// </summary>
        public void Reset()
        {
            sweepAngle = 0f;
            isScanning = true;
            scanDirection = 1;
            currentRange = 40f;
            tiltAngle = 0f;
            gainOffset = 0f;
            currentMode = RadarMode.WX;
            heading = 0f;
            showWaypoints = true;
            showRangeRings = true;
            showHeadingIndicator = true;
            waypoints.Clear();
        }
    }
}
