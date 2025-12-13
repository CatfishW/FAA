using UnityEngine;

namespace TrafficRadar
{
    /// <summary>
    /// FAA TCAS-compliant threat level classification for aircraft traffic.
    /// Based on Traffic Collision Avoidance System (TCAS II) standards.
    /// </summary>
    public enum ThreatLevel
    {
        /// <summary>Non-threatening traffic in the area (cyan unfilled diamond)</summary>
        OtherTraffic,
        
        /// <summary>Traffic within observation radius but not a threat (cyan filled diamond)</summary>
        Proximate,
        
        /// <summary>Potential conflict, ~35-48 seconds to impact (amber filled circle)</summary>
        TrafficAdvisory,
        
        /// <summary>Collision risk, ~15-35 seconds to impact (red filled square)</summary>
        ResolutionAdvisory
    }

    /// <summary>
    /// FAA-standard colors and symbology for TCAS threat levels.
    /// </summary>
    public static class ThreatLevelConfig
    {
        // FAA TCAS Standard Colors
        public static readonly Color OtherTrafficColor = new Color(0f, 1f, 1f, 0.8f);      // Cyan
        public static readonly Color ProximateColor = new Color(0f, 1f, 1f, 1f);           // Cyan (filled)
        public static readonly Color TrafficAdvisoryColor = new Color(1f, 0.75f, 0f, 1f);  // Amber
        public static readonly Color ResolutionAdvisoryColor = new Color(1f, 0f, 0f, 1f);  // Red

        // Own aircraft color
        public static readonly Color OwnAircraftColor = new Color(1f, 0f, 0f, 1f);         // Red

        /// <summary>
        /// Gets the color for a given threat level.
        /// </summary>
        public static Color GetColor(ThreatLevel level)
        {
            switch (level)
            {
                case ThreatLevel.ResolutionAdvisory:
                    return ResolutionAdvisoryColor;
                case ThreatLevel.TrafficAdvisory:
                    return TrafficAdvisoryColor;
                case ThreatLevel.Proximate:
                    return ProximateColor;
                case ThreatLevel.OtherTraffic:
                default:
                    return OtherTrafficColor;
            }
        }

        /// <summary>
        /// Gets the symbol type for a given threat level.
        /// </summary>
        public static SymbolType GetSymbolType(ThreatLevel level)
        {
            switch (level)
            {
                case ThreatLevel.ResolutionAdvisory:
                    return SymbolType.FilledSquare;
                case ThreatLevel.TrafficAdvisory:
                    return SymbolType.FilledCircle;
                case ThreatLevel.Proximate:
                    return SymbolType.FilledDiamond;
                case ThreatLevel.OtherTraffic:
                default:
                    return SymbolType.UnfilledDiamond;
            }
        }
    }

    /// <summary>
    /// Symbol types for aircraft display on traffic radar.
    /// </summary>
    public enum SymbolType
    {
        UnfilledDiamond,  // Other traffic
        FilledDiamond,    // Proximate
        FilledCircle,     // Traffic Advisory
        FilledSquare      // Resolution Advisory
    }

    /// <summary>
    /// Configurable thresholds for threat level determination.
    /// Based on TCAS-style criteria.
    /// </summary>
    [System.Serializable]
    public class ThreatThresholds
    {
        [Header("Resolution Advisory (Red)")]
        [Tooltip("Maximum distance in nautical miles for RA")]
        public float raDistanceNM = 1.0f;
        [Tooltip("Maximum altitude difference in feet for RA")]
        public float raAltitudeFt = 300f;

        [Header("Traffic Advisory (Amber)")]
        [Tooltip("Maximum distance in nautical miles for TA")]
        public float taDistanceNM = 3.0f;
        [Tooltip("Maximum altitude difference in feet for TA")]
        public float taAltitudeFt = 500f;

        [Header("Proximate (Cyan Filled)")]
        [Tooltip("Maximum distance in nautical miles for Proximate")]
        public float proximateDistanceNM = 6.0f;
        [Tooltip("Maximum altitude difference in feet for Proximate")]
        public float proximateAltitudeFt = 1200f;

        /// <summary>
        /// Determines the threat level based on distance and altitude difference.
        /// </summary>
        /// <param name="distanceNM">Distance in nautical miles</param>
        /// <param name="altitudeDiffFt">Altitude difference in feet</param>
        /// <returns>The appropriate threat level</returns>
        public ThreatLevel DetermineThreatLevel(float distanceNM, float altitudeDiffFt)
        {
            if (distanceNM <= raDistanceNM && altitudeDiffFt <= raAltitudeFt)
            {
                return ThreatLevel.ResolutionAdvisory;
            }
            else if (distanceNM <= taDistanceNM && altitudeDiffFt <= taAltitudeFt)
            {
                return ThreatLevel.TrafficAdvisory;
            }
            else if (distanceNM <= proximateDistanceNM && altitudeDiffFt <= proximateAltitudeFt)
            {
                return ThreatLevel.Proximate;
            }
            else
            {
                return ThreatLevel.OtherTraffic;
            }
        }
    }

    /// <summary>
    /// Data structure for a traffic target on the radar.
    /// </summary>
    [System.Serializable]
    public class RadarTrafficTarget
    {
        public string icao24;
        public string callsign;
        public float latitude;
        public float longitude;
        public float altitudeFt;
        public float heading;
        public float groundSpeedKts;
        public float verticalRateFpm;
        
        // Calculated fields
        public float distanceNM;
        public float bearingDeg;
        public float relativeAltitudeFt;
        public ThreatLevel threatLevel;
        
        // Radar display position (normalized -1 to 1)
        public Vector2 radarPosition;
    }
}
