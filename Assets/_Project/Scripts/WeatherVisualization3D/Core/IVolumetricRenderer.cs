using UnityEngine;
using System;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Interface for volumetric weather renderers.
    /// Provides abstraction for different rendering implementations.
    /// Enables loose coupling between data and visualization.
    /// </summary>
    public interface IVolumetricRenderer
    {
        /// <summary>
        /// Name of the renderer for identification
        /// </summary>
        string RendererName { get; }

        /// <summary>
        /// Whether the renderer is currently visible
        /// </summary>
        bool IsVisible { get; set; }

        /// <summary>
        /// Whether the renderer is initialized and ready
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Current render quality level (0-1)
        /// </summary>
        float QualityLevel { get; set; }

        /// <summary>
        /// Initialize the renderer with configuration
        /// </summary>
        void Initialize(WeatherVolumeConfig config);

        /// <summary>
        /// Update the renderer with new weather data
        /// </summary>
        void UpdateData(WeatherVolumeData data);

        /// <summary>
        /// Set the view mode for rendering
        /// </summary>
        void SetViewMode(WeatherViewMode mode);

        /// <summary>
        /// Clean up renderer resources
        /// </summary>
        void Cleanup();

        /// <summary>
        /// Force a visual refresh
        /// </summary>
        void Refresh();
    }

    /// <summary>
    /// Interface for renderers that support layer visibility control
    /// </summary>
    public interface ILayeredRenderer : IVolumetricRenderer
    {
        /// <summary>
        /// Set visibility of a specific render layer
        /// </summary>
        void SetLayerVisible(RenderLayer layer, bool visible);

        /// <summary>
        /// Check if a specific layer is visible
        /// </summary>
        bool IsLayerVisible(RenderLayer layer);
    }

    /// <summary>
    /// Available render layers for weather visualization
    /// </summary>
    [Flags]
    public enum RenderLayer
    {
        None = 0,
        // VolumetricClouds = 1 << 0, // Removed - no volumetric clouds
        IntensityPillars = 1 << 1,
        CellBoundaries = 1 << 2,
        Lightning = 1 << 3,
        Precipitation = 1 << 4,
        Turbulence = 1 << 5,
        Labels = 1 << 6,
        DistanceRings = 1 << 7,
        AltitudeMarkers = 1 << 8,
        All = ~0
    }

    /// <summary>
    /// View modes for weather visualization
    /// </summary>
    public enum WeatherViewMode
    {
        /// <summary>Full 3D perspective view (default)</summary>
        Perspective3D,
        /// <summary>Top-down plan view like traditional radar</summary>
        PlanView,
        /// <summary>Side profile/vertical cross-section view</summary>
        ProfileView,
        /// <summary>Cockpit perspective with volumetric extrusion</summary>
        CockpitView
    }

    /// <summary>
    /// Interface for effect renderers (lightning, precipitation, etc.)
    /// </summary>
    public interface IWeatherEffectRenderer
    {
        /// <summary>
        /// Name of the effect
        /// </summary>
        string EffectName { get; }

        /// <summary>
        /// Whether the effect is active
        /// </summary>
        bool IsActive { get; set; }

        /// <summary>
        /// Intensity multiplier (0-1)
        /// </summary>
        float IntensityMultiplier { get; set; }

        /// <summary>
        /// Initialize the effect renderer
        /// </summary>
        void Initialize(WeatherVolumeConfig config);

        /// <summary>
        /// Update effect based on weather data
        /// </summary>
        void UpdateEffect(WeatherVolumeData data);

        /// <summary>
        /// Clean up effect resources
        /// </summary>
        void Cleanup();
    }
}
