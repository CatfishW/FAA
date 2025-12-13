namespace WeatherRadar
{
    /// <summary>
    /// Status of a weather data provider
    /// </summary>
    public enum ProviderStatus
    {
        Inactive,       // Provider is not running
        Connecting,     // Attempting to connect to data source
        Active,         // Actively providing data
        Error,          // Error state
        NoData          // Connected but no data available
    }
}
