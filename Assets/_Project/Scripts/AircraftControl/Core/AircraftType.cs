namespace AircraftControl.Core
{
    /// <summary>
    /// Enumeration of supported aircraft types.
    /// Used to differentiate control schemes and flight physics.
    /// </summary>
    public enum AircraftType
    {
        /// <summary>
        /// Fixed-wing aircraft (airplanes) using conventional control surfaces
        /// </summary>
        FixedWing = 0,

        /// <summary>
        /// Rotary-wing aircraft (helicopters) using main and tail rotors
        /// </summary>
        Helicopter = 1
    }
}
