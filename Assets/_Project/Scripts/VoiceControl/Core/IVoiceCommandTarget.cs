using System.Collections.Generic;

namespace VoiceControl.Core
{
    /// <summary>
    /// Interface for components that can receive voice commands.
    /// Implement this interface on adapter components that bridge
    /// between the voice control system and existing controllers.
    /// 
    /// Design: High cohesion - each target handles its own command set.
    /// Low coupling - voice system only knows about this interface.
    /// </summary>
    public interface IVoiceCommandTarget
    {
        /// <summary>
        /// Unique identifier for this target (e.g., "weather_radar", "traffic_radar")
        /// </summary>
        string TargetId { get; }
        
        /// <summary>
        /// Human-readable name for UI display
        /// </summary>
        string DisplayName { get; }
        
        /// <summary>
        /// Get all available commands this target can handle
        /// </summary>
        VoiceCommandInfo[] GetAvailableCommands();
        
        /// <summary>
        /// Execute a command by name with parameters
        /// </summary>
        /// <param name="commandName">Name of the command to execute</param>
        /// <param name="parameters">Parameters for the command</param>
        /// <returns>True if command executed successfully</returns>
        bool ExecuteCommand(string commandName, Dictionary<string, object> parameters);
    }
}
