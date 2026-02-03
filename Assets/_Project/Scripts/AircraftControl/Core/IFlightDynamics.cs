using System.Collections.Generic;

namespace AircraftControl.Core
{
    /// <summary>
    /// Interface for aircraft flight dynamics implementations.
    /// Implements the Strategy pattern to allow different physics models
    /// for different aircraft types (fixed-wing, helicopter, etc.)
    /// </summary>
    public interface IFlightDynamics
    {
        /// <summary>
        /// The type of aircraft this dynamics model represents
        /// </summary>
        AircraftType AircraftType { get; }

        /// <summary>
        /// Initialize the dynamics model with the given aircraft state
        /// </summary>
        /// <param name="state">The aircraft state to initialize with</param>
        void Initialize(AircraftState state);

        /// <summary>
        /// Update the aircraft physics based on current state and inputs
        /// </summary>
        /// <param name="state">Current aircraft state (modified in-place)</param>
        /// <param name="deltaTime">Time step in seconds</param>
        void UpdatePhysics(AircraftState state, float deltaTime);

        /// <summary>
        /// Reset the dynamics model to initial conditions
        /// </summary>
        /// <param name="state">The aircraft state to reset</param>
        void Reset(AircraftState state);

        /// <summary>
        /// Get the names of required input axes for this aircraft type
        /// </summary>
        /// <returns>List of required input names</returns>
        IReadOnlyList<string> GetRequiredInputNames();

        /// <summary>
        /// Validate that the state contains required inputs for this aircraft type
        /// </summary>
        /// <param name="state">The aircraft state to validate</param>
        /// <returns>True if state is valid for this dynamics model</returns>
        bool ValidateState(AircraftState state);
    }
}
