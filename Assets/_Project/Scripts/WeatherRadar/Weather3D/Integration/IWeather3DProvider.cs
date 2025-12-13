using System;
using UnityEngine;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Interface for 3D weather data providers.
    /// Implement this interface to create custom 3D weather data sources.
    /// </summary>
    public interface IWeather3DProvider
    {
        /// <summary>
        /// Name of this provider for display purposes
        /// </summary>
        string ProviderName { get; }
        
        /// <summary>
        /// Current operational status
        /// </summary>
        Weather3DProviderStatus Status { get; }
        
        /// <summary>
        /// Check if provider is currently active and providing data
        /// </summary>
        bool IsActive { get; }
        
        /// <summary>
        /// Current 3D weather data
        /// </summary>
        Weather3DData CurrentData { get; }
        
        /// <summary>
        /// Event fired when new 3D weather data is available
        /// </summary>
        event Action<Weather3DData> OnDataUpdated;
        
        /// <summary>
        /// Event fired when provider status changes
        /// </summary>
        event Action<Weather3DProviderStatus> OnStatusChanged;
        
        /// <summary>
        /// Set the aircraft position for centered weather data
        /// </summary>
        void SetAircraftPosition(Vector3 position, float heading, float altitude);
        
        /// <summary>
        /// Set the coverage range in nautical miles
        /// </summary>
        void SetRange(float rangeNM);
        
        /// <summary>
        /// Activate the provider
        /// </summary>
        void Activate();
        
        /// <summary>
        /// Deactivate the provider
        /// </summary>
        void Deactivate();
        
        /// <summary>
        /// Request immediate data refresh
        /// </summary>
        void RefreshData();
    }

    /// <summary>
    /// Status of a 3D weather data provider
    /// </summary>
    public enum Weather3DProviderStatus
    {
        Inactive,
        Connecting,
        Active,
        Error,
        NoData
    }
}
