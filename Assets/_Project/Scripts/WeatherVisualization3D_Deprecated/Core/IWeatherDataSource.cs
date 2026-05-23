using UnityEngine;
using System;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Interface for weather data sources.
    /// Provides abstraction for different data providers (simulation, real API, recorded data).
    /// This enables loose coupling between data sources and visualization systems.
    /// </summary>
    public interface IWeatherDataSource
    {
        /// <summary>
        /// Name of the data source for identification
        /// </summary>
        string SourceName { get; }

        /// <summary>
        /// Current status of the data source
        /// </summary>
        DataSourceStatus Status { get; }

        /// <summary>
        /// Whether the data source is currently providing valid data
        /// </summary>
        bool IsDataValid { get; }

        /// <summary>
        /// The current weather volume data
        /// </summary>
        WeatherVolumeData CurrentData { get; }

        /// <summary>
        /// Event fired when new weather data is available
        /// </summary>
        event Action<WeatherVolumeData> OnDataUpdated;

        /// <summary>
        /// Event fired when data source status changes
        /// </summary>
        event Action<DataSourceStatus> OnStatusChanged;

        /// <summary>
        /// Initialize the data source
        /// </summary>
        void Initialize();

        /// <summary>
        /// Start providing data updates
        /// </summary>
        void StartUpdates();

        /// <summary>
        /// Stop providing data updates
        /// </summary>
        void StopUpdates();

        /// <summary>
        /// Force an immediate data refresh
        /// </summary>
        void ForceRefresh();

        /// <summary>
        /// Set the geographic center of the data (latitude, longitude, altitude in feet)
        /// </summary>
        void SetPosition(float latitude, float longitude, float altitudeFt);

        /// <summary>
        /// Set the coverage range in nautical miles
        /// </summary>
        void SetRange(float rangeNM);

        /// <summary>
        /// Set the heading/orientation in degrees
        /// </summary>
        void SetHeading(float headingDegrees);
    }

    /// <summary>
    /// Status of a weather data source
    /// </summary>
    public enum DataSourceStatus
    {
        /// <summary>Not initialized</summary>
        Uninitialized,
        /// <summary>Initializing/connecting</summary>
        Initializing,
        /// <summary>Ready and providing data</summary>
        Active,
        /// <summary>Paused but can resume</summary>
        Paused,
        /// <summary>Error state - check logs</summary>
        Error,
        /// <summary>No data available in current region</summary>
        NoData,
        /// <summary>Disposed/shutdown</summary>
        Disposed
    }

    /// <summary>
    /// Interface for data sources that support 2D radar texture input
    /// </summary>
    public interface IRadarTextureSource : IWeatherDataSource
    {
        /// <summary>
        /// Get the current 2D radar texture
        /// </summary>
        Texture2D RadarTexture { get; }

        /// <summary>
        /// Event fired when radar texture is updated
        /// </summary>
        event Action<Texture2D> OnRadarTextureUpdated;
    }

    /// <summary>
    /// Interface for data sources that support simulation control
    /// </summary>
    public interface ISimulationDataSource : IWeatherDataSource
    {
        /// <summary>
        /// Current simulation time scale (1.0 = real-time)
        /// </summary>
        float TimeScale { get; set; }

        /// <summary>
        /// Whether simulation is paused
        /// </summary>
        bool IsPaused { get; set; }

        /// <summary>
        /// Step simulation forward by specified seconds
        /// </summary>
        void StepSimulation(float seconds);

        /// <summary>
        /// Reset simulation to initial state
        /// </summary>
        void ResetSimulation();

        /// <summary>
        /// Load a specific weather scenario
        /// </summary>
        void LoadScenario(WeatherScenarioPreset scenario);
    }
}
