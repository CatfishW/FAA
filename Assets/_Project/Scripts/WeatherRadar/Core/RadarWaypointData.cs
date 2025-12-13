using System;

namespace WeatherRadar
{
    /// <summary>
    /// Waypoint data for overlay display
    /// </summary>
    [Serializable]
    public class RadarWaypointData
    {
        public string identifier;
        public float bearing;      // Bearing from aircraft in degrees
        public float distance;     // Distance from aircraft in NM
        public WaypointType type;
    }

    /// <summary>
    /// Types of navigation waypoints
    /// </summary>
    public enum WaypointType
    {
        FIX,
        VOR,
        NDB,
        Airport,
        Intersection
    }
}
