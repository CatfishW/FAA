using System;
using System.Collections.Generic;

namespace TrafficRadar.Core
{
    /// <summary>
    /// Interface for traffic data sources (API, mock, file-based, etc.)
    /// This abstraction allows for easy testing and swapping of data sources.
    /// </summary>
    public interface ITrafficDataSource
    {
        /// <summary>
        /// Event fired when new aircraft data is available
        /// </summary>
        event Action<IReadOnlyList<AircraftState>> OnDataReceived;
        
        /// <summary>
        /// Event fired when a fetch error occurs
        /// </summary>
        event Action<string> OnFetchError;
        
        /// <summary>
        /// Whether the data source is currently fetching
        /// </summary>
        bool IsFetching { get; }
        
        /// <summary>
        /// Whether the data source is actively polling
        /// </summary>
        bool IsActive { get; }
        
        /// <summary>
        /// Number of aircraft currently tracked
        /// </summary>
        int AircraftCount { get; }
        
        /// <summary>
        /// Start fetching data
        /// </summary>
        void StartFetching();
        
        /// <summary>
        /// Stop fetching data
        /// </summary>
        void StopFetching();
        
        /// <summary>
        /// Force an immediate fetch
        /// </summary>
        void FetchNow();
        
        /// <summary>
        /// Set the geographic center for data fetching
        /// </summary>
        void SetGeographicCenter(double latitude, double longitude, float radiusKm);
    }
    
    /// <summary>
    /// Immutable aircraft state data.
    /// Uses a struct for performance with radar systems handling many aircraft.
    /// </summary>
    public struct AircraftState
    {
        public string Icao24;
        public string Callsign;
        public double Latitude;
        public double Longitude;
        public float AltitudeMeters;
        public float Heading;
        public float VelocityMps;
        public float VerticalRateMps;
        public bool OnGround;
        public DateTime LastUpdate;
        
        // Computed properties
        public float AltitudeFeet => AltitudeMeters * 3.28084f;
        public float VelocityKnots => VelocityMps * 1.94384f;
        public float VerticalRateFpm => VerticalRateMps * 196.85f;
    }
}
