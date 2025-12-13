using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrafficRadar.Core
{
    /// <summary>
    /// Core radar processing service.
    /// Transforms raw aircraft data into displayable radar targets.
    /// High cohesion: Single responsibility of converting aircraft states to radar targets.
    /// Low coupling: Depends only on interfaces and data structures.
    /// </summary>
    public class RadarDataProcessor
    {
        // Constants
        private const float NM_TO_KM = 1.852f;
        private const float KM_TO_NM = 0.539957f;
        private const float METERS_TO_FEET = 3.28084f;
        
        private readonly ThreatThresholds _thresholds;
        private float _rangeNM = 20f;
        private int _maxTargets = 50;
        
        // Reusable list to avoid allocations
        private readonly List<RadarTarget> _processedTargets = new List<RadarTarget>();
        
        public RadarDataProcessor(ThreatThresholds thresholds = null)
        {
            _thresholds = thresholds ?? new ThreatThresholds();
        }
        
        /// <summary>
        /// Current radar range in nautical miles
        /// </summary>
        public float RangeNM
        {
            get => _rangeNM;
            set => _rangeNM = Mathf.Max(1f, value);
        }
        
        /// <summary>
        /// Maximum number of targets to process
        /// </summary>
        public int MaxTargets
        {
            get => _maxTargets;
            set => _maxTargets = Mathf.Max(1, value);
        }
        
        /// <summary>
        /// Process aircraft states into radar targets relative to own-ship position.
        /// </summary>
        /// <param name="aircraftStates">Raw aircraft state data</param>
        /// <param name="ownPosition">Own-ship position</param>
        /// <returns>Processed radar targets within range, sorted by threat level</returns>
        public IReadOnlyList<RadarTarget> ProcessAircraft(
            IReadOnlyList<AircraftState> aircraftStates,
            OwnShipPosition ownPosition)
        {
            _processedTargets.Clear();
            
            if (aircraftStates == null || aircraftStates.Count == 0)
            {
                return _processedTargets;
            }
            
            float ownLat = (float)ownPosition.Latitude;
            float ownLon = (float)ownPosition.Longitude;
            float ownAltFt = ownPosition.AltitudeFeet;
            float ownHeading = ownPosition.HeadingDegrees;
            
            foreach (var aircraft in aircraftStates)
            {
                // Skip aircraft without valid position
                if (aircraft.Latitude == 0 && aircraft.Longitude == 0)
                    continue;
                
                // Calculate distance
                float distanceKm = CalculateDistanceKm(
                    ownLat, ownLon,
                    (float)aircraft.Latitude, (float)aircraft.Longitude);
                float distanceNM = distanceKm * KM_TO_NM;
                
                // Skip if beyond radar range
                if (distanceNM > _rangeNM)
                    continue;
                
                // Calculate bearing
                float bearing = CalculateBearing(
                    ownLat, ownLon,
                    (float)aircraft.Latitude, (float)aircraft.Longitude);
                
                // Calculate altitude difference
                float altDiffFt = Mathf.Abs(aircraft.AltitudeFeet - ownAltFt);
                
                // Create radar target
                var target = new RadarTarget
                {
                    Icao24 = aircraft.Icao24,
                    Callsign = aircraft.Callsign,
                    Latitude = aircraft.Latitude,
                    Longitude = aircraft.Longitude,
                    AltitudeFeet = aircraft.AltitudeFeet,
                    Heading = aircraft.Heading,
                    GroundSpeedKnots = aircraft.VelocityKnots,
                    VerticalRateFpm = aircraft.VerticalRateFpm,
                    DistanceNM = distanceNM,
                    BearingDegrees = bearing,
                    RelativeAltitudeFeet = aircraft.AltitudeFeet - ownAltFt,
                    ThreatLevel = _thresholds.DetermineThreatLevel(distanceNM, altDiffFt),
                    RadarPosition = CalculateRadarPosition(distanceNM, bearing, ownHeading),
                    TimeSinceUpdate = (float)(DateTime.Now - aircraft.LastUpdate).TotalSeconds
                };
                
                _processedTargets.Add(target);
                
                if (_processedTargets.Count >= _maxTargets)
                    break;
            }
            
            // Sort by threat level (highest first)
            _processedTargets.Sort((a, b) => b.ThreatLevel.CompareTo(a.ThreatLevel));
            
            return _processedTargets;
        }
        
        /// <summary>
        /// Calculate distance between two points using Haversine formula
        /// </summary>
        private float CalculateDistanceKm(float lat1, float lon1, float lat2, float lon2)
        {
            const float EarthRadiusKm = 6371f;
            
            float dLat = (lat2 - lat1) * Mathf.Deg2Rad;
            float dLon = (lon2 - lon1) * Mathf.Deg2Rad;
            
            float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                      Mathf.Cos(lat1 * Mathf.Deg2Rad) * Mathf.Cos(lat2 * Mathf.Deg2Rad) *
                      Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
            
            float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
            
            return EarthRadiusKm * c;
        }
        
        /// <summary>
        /// Calculate bearing from point 1 to point 2
        /// </summary>
        private float CalculateBearing(float lat1, float lon1, float lat2, float lon2)
        {
            float dLon = (lon2 - lon1) * Mathf.Deg2Rad;
            float lat1Rad = lat1 * Mathf.Deg2Rad;
            float lat2Rad = lat2 * Mathf.Deg2Rad;

            float y = Mathf.Sin(dLon) * Mathf.Cos(lat2Rad);
            float x = Mathf.Cos(lat1Rad) * Mathf.Sin(lat2Rad) -
                      Mathf.Sin(lat1Rad) * Mathf.Cos(lat2Rad) * Mathf.Cos(dLon);

            float bearing = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            return (bearing + 360f) % 360f;
        }
        
        /// <summary>
        /// Calculate normalized radar position for display
        /// </summary>
        private Vector2 CalculateRadarPosition(float distanceNM, float bearingDeg, float ownHeadingDeg)
        {
            // Convert bearing relative to own heading (heading-up display)
            float relativeBearing = (bearingDeg - ownHeadingDeg + 360f) % 360f;
            float relBearingRad = relativeBearing * Mathf.Deg2Rad;

            // Normalize distance to radar range
            float normalizedDistance = Mathf.Clamp01(distanceNM / _rangeNM);

            // Calculate x,y position (0 is up, clockwise)
            float x = normalizedDistance * Mathf.Sin(relBearingRad);
            float y = normalizedDistance * Mathf.Cos(relBearingRad);

            return new Vector2(x, y);
        }
    }
}
