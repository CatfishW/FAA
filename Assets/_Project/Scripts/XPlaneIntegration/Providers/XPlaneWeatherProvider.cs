using System;
using UnityEngine;
using FAA.XPlaneIntegration;
using AviationUI;

namespace FAA.XPlaneIntegration.Providers
{
    /// <summary>
    /// Weather data provider that integrates X-Plane simulation weather into Unity's HUD display.
    /// 
    /// SUBSCRIBES TO X-PLANE WEATHER DATAREFS:
    /// - sim/weather/wind_speed_total[0] → WindSpeed (knots)
    /// - sim/weather/wind_direction_true[0] → WindDirection (degrees)
    /// - sim/weather/barometer[0] → BarometricPressure (inHg)
    /// - sim/weather/temperature_c[0] → Temperature (Celsius)
    /// - sim/weather/visibility_km[0] → Visibility (kilometers)
    /// - sim/weather/cloud_base[0] → CloudBase (meters MSL)
    /// 
    /// IMPORTANT LIMITATIONS:
    /// - X-Plane provides POINT weather data only (at aircraft position)
    /// - This is NOT volumetric/radar weather data
    /// - Does NOT replace NEXRAD or radar imagery pipelines
    /// - Complementary data for HUD display only
    /// - Weather is LOCAL to aircraft, not regional
    /// 
    /// USAGE:
    /// 1. Add component to a GameObject in the scene
    /// 2. Assign XPlaneUdpListener reference (or let it find one)
    /// 3. Assign AviationFlightDataProvider reference (or let it find one)
    /// 4. Enable "Enable X-Plane Weather" to activate
    /// 5. Provider will auto-subscribe to weather DataRefs on Start()
    /// </summary>
    public class XPlaneWeatherProvider : MonoBehaviour
    {
        #region Configuration

        [Header("X-Plane Weather Integration")]
        [Tooltip("Enable to receive weather data from X-Plane. Disable to use other weather sources.")]
        [SerializeField]
        private bool enableXPlaneWeather = true;

        [Tooltip("Update frequency for weather data requests (Hz). Higher = more CPU, lower = more lag.")]
        [Range(1f, 30f)]
        [SerializeField]
        private float updateFrequency = 5f;

        [Header("References")]
        [Tooltip("Reference to XPlaneUdpListener. Auto-finds if not assigned.")]
        [SerializeField]
        private XPlaneUdpListener udpListener;

        [Tooltip("Reference to AviationFlightDataProvider for injecting weather data. Auto-finds if not assigned.")]
        [SerializeField]
        private AviationFlightDataProvider flightDataProvider;

        [Header("Smoothing")]
        [Tooltip("Smoothing factor for weather values (0 = instant, 1 = no change). Lower = smoother.")]
        [Range(0f, 0.9f)]
        [SerializeField]
        private float smoothingFactor = 0.1f;

        [Header("Status")]
        [Tooltip("Shows current connection status")]
        [SerializeField]
        private bool isConnected;

        [Tooltip("Time of last weather data update")]
        [SerializeField]
        private float lastUpdateTime;

        #endregion

        #region Public Properties

        /// <summary>
        /// Enable or disable X-Plane weather integration
        /// </summary>
        public bool EnableXPlaneWeather
        {
            get => enableXPlaneWeather;
            set
            {
                if (enableXPlaneWeather != value)
                {
                    enableXPlaneWeather = value;
                    if (enableXPlaneWeather)
                    {
                        SubscribeToDataRefs();
                    }
                    else
                    {
                        UnsubscribeFromDataRefs();
                    }
                }
            }
        }

        /// <summary>
        /// Current connection status to X-Plane
        /// </summary>
        public bool IsConnected => isConnected;

        /// <summary>
        /// Timestamp of last successful weather data update
        /// </summary>
        public float LastUpdateTime => lastUpdateTime;

        /// <summary>
        /// Current weather data from X-Plane (for external access)
        /// </summary>
        public XPlaneWeatherData CurrentWeather => currentWeather;

        #endregion

        #region Private Fields

        private XPlaneWeatherData currentWeather = new XPlaneWeatherData();
        private XPlaneWeatherData smoothedWeather = new XPlaneWeatherData();
        private bool isSubscribed;
        private float windSpeedSmoothed;
        private float windDirectionSmoothed;
        private float barometerSmoothed;
        private float temperatureSmoothed;
        private float visibilitySmoothed;
        private float cloudBaseSmoothed;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (udpListener == null)
            {
                udpListener = FindObjectOfType<XPlaneUdpListener>();
                if (udpListener == null)
                {
                    Debug.LogWarning("[XPlaneWeatherProvider] XPlaneUdpListener not found in scene. Create one or assign manually.");
                }
            }

            if (flightDataProvider == null)
            {
                flightDataProvider = FindObjectOfType<AviationFlightDataProvider>();
                if (flightDataProvider == null)
                {
                    Debug.LogWarning("[XPlaneWeatherProvider] AviationFlightDataProvider not found in scene. Create one or assign manually.");
                }
            }

            if (udpListener != null)
            {
                udpListener.OnDataReceived += OnWeatherDataReceived;
                udpListener.OnConnectionStateChanged += OnConnectionStateChanged;
            }
        }

        private void Start()
        {
            if (enableXPlaneWeather && udpListener != null)
            {
                SubscribeToDataRefs();
            }
        }

        private void Update()
        {
            if (!isConnected || !enableXPlaneWeather) return;

            udpListener?.ProcessQueuedData();
        }

        private void OnDestroy()
        {
            UnsubscribeFromDataRefs();

            try
            {
                if (udpListener != null)
                {
                    udpListener.OnDataReceived -= OnWeatherDataReceived;
                    udpListener.OnConnectionStateChanged -= OnConnectionStateChanged;
                }
            }
            catch (ObjectReferenceException)
            {
                // udpListener was destroyed first, ignore
            }
        }

        private void OnValidate()
        {
            updateFrequency = Mathf.Clamp(updateFrequency, 1f, 30f);
            smoothingFactor = Mathf.Clamp(smoothingFactor, 0f, 0.9f);
        }

        #endregion

        #region DataRef Subscription

        /// <summary>
        /// Subscribe to X-Plane weather DataRefs via RREF protocol.
        /// Called automatically on Start() if EnableXPlaneWeather is true.
        /// </summary>
        public void SubscribeToDataRefs()
        {
            if (udpListener == null || !udpListener.IsConnected)
            {
                Debug.LogWarning("[XPlaneWeatherProvider] Cannot subscribe: UDP listener not connected");
                return;
            }

            if (isSubscribed)
            {
                return;
            }

            udpListener.SendRrefRequest("sim/weather/aircraft/wind_speed_kt", (int)updateFrequency);
            udpListener.SendRrefRequest("sim/weather/aircraft/wind_direction_deg", (int)updateFrequency);
            udpListener.SendRrefRequest("sim/weather/aircraft/barometer_sealevel_inhg", (int)updateFrequency);
            udpListener.SendRrefRequest("sim/weather/aircraft/ambient_temperature_c", (int)updateFrequency);
            udpListener.SendRrefRequest("sim/weather/aircraft/visibility_reported_m", (int)updateFrequency);
            udpListener.SendRrefRequest("sim/weather/aircraft/cloud_base_msl_m", (int)updateFrequency);

            isSubscribed = true;
            Debug.Log("[XPlaneWeatherProvider] Subscribed to weather DataRefs");
        }

        /// <summary>
        /// Unsubscribe from X-Plane weather DataRefs.
        /// Called automatically on OnDestroy().
        /// </summary>
        public void UnsubscribeFromDataRefs()
        {
            if (udpListener == null || !isSubscribed)
            {
                return;
            }

            udpListener.SendRrefRequest("sim/weather/aircraft/wind_speed_kt", 0);
            udpListener.SendRrefRequest("sim/weather/aircraft/wind_direction_deg", 0);
            udpListener.SendRrefRequest("sim/weather/aircraft/barometer_sealevel_inhg", 0);
            udpListener.SendRrefRequest("sim/weather/aircraft/ambient_temperature_c", 0);
            udpListener.SendRrefRequest("sim/weather/aircraft/visibility_reported_m", 0);
            udpListener.SendRrefRequest("sim/weather/aircraft/cloud_base_msl_m", 0);

            isSubscribed = false;
            isConnected = false;
            Debug.Log("[XPlaneWeatherProvider] Unsubscribed from weather DataRefs");
        }

        #endregion

        #region Data Processing

        /// <summary>
        /// Handle incoming weather data from X-Plane UDP listener.
        /// Maps X-Plane DataRef values to AviationFlightData fields.
        /// 
        /// DATAREF MAPPING:
        /// - sim/weather/wind_speed_total[0] → WindSpeed (knots)
        /// - sim/weather/wind_direction_true[0] → WindDirection (degrees)
        /// - sim/weather/barometer[0] → BarometricPressure (inHg)
        /// - sim/weather/temperature_c[0] → Temperature (Celsius, stored for reference)
        /// - sim/weather/visibility_km[0] → Visibility (kilometers)
        /// - sim/weather/cloud_base[0] → CloudBase (meters MSL)
        /// </summary>
        /// <param name="dataRefValues">Dictionary of DataRef values from X-Plane</param>
        private void OnWeatherDataReceived(Dictionary<string, float> dataRefValues)
        {
            if (dataRefValues == null || dataRefValues.Count == 0)
            {
                return;
            }

            currentWeather.WindSpeed = GetWeatherValue(dataRefValues, XPlaneDataRefMapper.DataRef_WindSpeed, 0f);
            currentWeather.WindDirection = GetWeatherValue(dataRefValues, XPlaneDataRefMapper.DataRef_WindDirection, 0f);
            currentWeather.BarometricPressure = GetWeatherValue(dataRefValues, XPlaneDataRefMapper.DataRef_Pressure, 29.92f);
            currentWeather.Temperature = GetWeatherValue(dataRefValues, XPlaneDataRefMapper.DataRef_Temperature, 15f);
            currentWeather.Visibility = GetWeatherValue(dataRefValues, "sim/weather/aircraft/visibility_reported_m", 10000f);
            currentWeather.CloudBase = GetWeatherValue(dataRefValues, "sim/weather/aircraft/cloud_base_msl_m[0]", 3000f);
            currentWeather.LastUpdate = Time.time;

            ApplySmoothing();
            InjectWeatherData();

            lastUpdateTime = Time.time;
        }

        private static float GetWeatherValue(Dictionary<string, float> data, string key, float defaultValue)
        {
            return data.TryGetValue(key, out float value) ? value : defaultValue;
        }

        /// <summary>
        /// Apply exponential smoothing to weather values for stable HUD display.
        /// </summary>
        private void ApplySmoothing()
        {
            float smooth = smoothingFactor;

            windSpeedSmoothed = Mathf.Lerp(windSpeedSmoothed, currentWeather.WindSpeed, 1f - smooth);
            windDirectionSmoothed = Mathf.LerpAngle(windDirectionSmoothed, currentWeather.WindDirection, 1f - smooth);
            barometerSmoothed = Mathf.Lerp(barometerSmoothed, currentWeather.BarometricPressure, 1f - smooth);
            temperatureSmoothed = Mathf.Lerp(temperatureSmoothed, currentWeather.Temperature, 1f - smooth);
            visibilitySmoothed = Mathf.Lerp(visibilitySmoothed, currentWeather.Visibility, 1f - smooth);
            cloudBaseSmoothed = Mathf.Lerp(cloudBaseSmoothed, currentWeather.CloudBase, 1f - smooth);

            smoothedWeather.WindSpeed = windSpeedSmoothed;
            smoothedWeather.WindDirection = windDirectionSmoothed;
            smoothedWeather.BarometricPressure = barometerSmoothed;
            smoothedWeather.Temperature = temperatureSmoothed;
            smoothedWeather.Visibility = visibilitySmoothed;
            smoothedWeather.CloudBase = cloudBaseSmoothed;
        }

        private void InjectWeatherData()
        {
            if (flightDataProvider == null)
            {
                return;
            }

            var flightData = flightDataProvider.FlightData;
            if (flightData == null)
            {
                return;
            }

            flightData.windSpeed = smoothedWeather.WindSpeed;
            flightData.windDirection = smoothedWeather.WindDirection;
            flightData.barometricSetting = smoothedWeather.BarometricPressure;

            if (verboseLogging)
            {
                Debug.Log($"[XPlaneWeatherProvider] Weather injected: Wind {smoothedWeather.WindDirection:F0}°@{smoothedWeather.WindSpeed:F1}kt, " +
                          $"Baro {smoothedWeather.BarometricPressure:F2}inHg, Vis {smoothedWeather.Visibility:F1}km, " +
                          $"Cloud Base {smoothedWeather.CloudBase:F0}m");
            }
        }

        #endregion

        #region Connection Handling

        /// <summary>
        /// Handle X-Plane UDP connection state changes.
        /// </summary>
        /// <param name="state">New connection state</param>
        private void OnConnectionStateChanged(XPlaneUdpListener.ConnectionState state)
        {
            isConnected = state == XPlaneUdpListener.ConnectionState.Connected;

            if (isConnected && enableXPlaneWeather)
            {
                if (!isSubscribed)
                {
                    SubscribeToDataRefs();
                }
                Debug.Log("[XPlaneWeatherProvider] X-Plane connected");
            }
            else if (!isConnected)
            {
                Debug.LogWarning("[XPlaneWeatherProvider] X-Plane disconnected");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Set the X-Plane UDP listener reference.
        /// </summary>
        public void SetUdpListener(XPlaneUdpListener listener)
        {
            if (udpListener != null)
            {
                udpListener.OnDataReceived -= OnWeatherDataReceived;
                udpListener.OnConnectionStateChanged -= OnConnectionStateChanged;
            }

            udpListener = listener;

            if (udpListener != null)
            {
                udpListener.OnDataReceived += OnWeatherDataReceived;
                udpListener.OnConnectionStateChanged += OnConnectionStateChanged;

                if (enableXPlaneWeather && udpListener.IsConnected)
                {
                    SubscribeToDataRefs();
                }
            }
        }

        /// <summary>
        /// Set the aviation flight data provider reference.
        /// </summary>
        public void SetFlightDataProvider(AviationFlightDataProvider provider)
        {
            flightDataProvider = provider;
        }

        /// <summary>
        /// Force refresh weather data (for debugging).
        /// </summary>
        public void RefreshWeather()
        {
            if (udpListener != null && isConnected)
            {
                UnsubscribeFromDataRefs();
                SubscribeToDataRefs();
            }
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Container for X-Plane weather data.
        /// Used for internal tracking and external access.
        /// </summary>
        [Serializable]
        public class XPlaneWeatherData
        {
            [Tooltip("Wind speed in knots")]
            public float WindSpeed;

            [Tooltip("Wind direction in degrees (true)")]
            [Range(0f, 360f)]
            public float WindDirection;

            [Tooltip("Barometric pressure in inches Hg")]
            public float BarometricPressure;

            [Tooltip("Temperature in Celsius")]
            public float Temperature;

            [Tooltip("Visibility in kilometers")]
            public float Visibility;

            [Tooltip("Cloud base in meters MSL")]
            public float CloudBase;

            [Tooltip("Last update timestamp")]
            public float LastUpdate;

            /// <summary>
            /// Create a copy of the weather data
            /// </summary>
            public XPlaneWeatherData Clone()
            {
                return (XPlaneWeatherData)MemberwiseClone();
            }

            /// <summary>
            /// Convert to string for debugging
            /// </summary>
            public override string ToString()
            {
                return $"Wind: {WindDirection:F0}°@{WindSpeed:F1}kt | Baro: {BarometricPressure:F2}inHg | " +
                       $"Temp: {Temperature:F1}°C | Vis: {Visibility:F1}km | Cloud Base: {CloudBase:F0}m";
            }
        }

        #endregion
    }
}
