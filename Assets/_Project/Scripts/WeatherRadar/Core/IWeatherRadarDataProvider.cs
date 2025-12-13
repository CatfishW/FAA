using System;
using UnityEngine;

namespace WeatherRadar
{
    /// <summary>
    /// Interface for weather radar data providers.
    /// Enables loose coupling between data sources and the display system.
    /// </summary>
    public interface IWeatherRadarDataProvider
    {
        /// <summary>
        /// Event fired when new radar data is available
        /// </summary>
        event Action<Texture2D> OnRadarDataUpdated;

        /// <summary>
        /// Event fired when the data provider status changes
        /// </summary>
        event Action<ProviderStatus> OnStatusChanged;

        /// <summary>
        /// Current provider status
        /// </summary>
        ProviderStatus Status { get; }

        /// <summary>
        /// Provider name for display
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Is the provider currently active and providing data
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Set aircraft position for data fetching
        /// </summary>
        void SetPosition(float latitude, float longitude);

        /// <summary>
        /// Set aircraft heading for data orientation
        /// </summary>
        void SetHeading(float headingDegrees);

        /// <summary>
        /// Set the radar range
        /// </summary>
        void SetRange(float rangeNM);

        /// <summary>
        /// Set antenna tilt angle
        /// </summary>
        void SetTilt(float tiltDegrees);

        /// <summary>
        /// Set gain offset
        /// </summary>
        void SetGain(float gainDB);

        /// <summary>
        /// Start the data provider
        /// </summary>
        void Activate();

        /// <summary>
        /// Stop the data provider
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Force an immediate data refresh
        /// </summary>
        void RefreshData();
    }
}
