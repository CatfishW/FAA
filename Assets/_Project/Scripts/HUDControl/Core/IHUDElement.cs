using AircraftControl.Core;

namespace HUDControl.Core
{
    /// <summary>
    /// Interface for all HUD elements. Defines contract for plug-in/out architecture.
    /// </summary>
    public interface IHUDElement
    {
        /// <summary>
        /// Unique identifier for this element
        /// </summary>
        string ElementId { get; }
        
        /// <summary>
        /// Whether this element is currently enabled and updating
        /// </summary>
        bool IsEnabled { get; }
        
        /// <summary>
        /// Initialize the element. Called once on startup.
        /// </summary>
        void Initialize();
        
        /// <summary>
        /// Update element display with new aircraft state
        /// </summary>
        /// <param name="state">Current aircraft state data</param>
        void UpdateElement(AircraftState state);
        
        /// <summary>
        /// Enable or disable this element
        /// </summary>
        void SetEnabled(bool enabled);
    }
}
