using System;

namespace AircraftControl.Core
{
    /// <summary>
    /// Interface for aircraft controllers.
    /// Provides abstraction for different aircraft control implementations.
    /// </summary>
    public interface IAircraftController
    {
        /// <summary>
        /// Current aircraft state (read-only)
        /// </summary>
        AircraftState State { get; }
        
        /// <summary>
        /// Whether the controller is currently enabled and accepting input
        /// </summary>
        bool IsEnabled { get; }
        
        /// <summary>
        /// Whether the aircraft is currently being controlled by user input
        /// </summary>
        bool IsUserControlled { get; }
        
        /// <summary>
        /// Event fired when aircraft state changes
        /// </summary>
        event Action<AircraftState> OnStateChanged;
        
        /// <summary>
        /// Event fired when geographic position changes significantly
        /// </summary>
        event Action<double, double, float> OnPositionChanged;
        
        /// <summary>
        /// Set throttle input (0-1)
        /// </summary>
        void SetThrottle(float value);
        
        /// <summary>
        /// Set pitch input (-1 to 1, positive = pitch up)
        /// </summary>
        void SetPitch(float value);
        
        /// <summary>
        /// Set roll input (-1 to 1, positive = roll right)
        /// </summary>
        void SetRoll(float value);
        
        /// <summary>
        /// Set yaw input (-1 to 1, positive = yaw right)
        /// </summary>
        void SetYaw(float value);
        
        /// <summary>
        /// Enable or disable user control
        /// </summary>
        void SetControlEnabled(bool enabled);
        
        /// <summary>
        /// Reset aircraft to default state
        /// </summary>
        void ResetToDefault();
    }
}
