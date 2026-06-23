using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Data models for Open-Meteo API response parsing.
    /// Open-Meteo provides free weather data including pressure-level variables.
    /// </summary>
    
    #region API Response Models
    
    /// <summary>
    /// Root response from Open-Meteo API
    /// </summary>
    [Serializable]
    public class OpenMeteoResponse
    {
        public float latitude;
        public float longitude;
        public float elevation;
        public float generationtime_ms;
        public int utc_offset_seconds;
        public string timezone;
        public string timezone_abbreviation;
        public OpenMeteoHourlyData hourly;
        public OpenMeteoCurrentData current;
    }
    
    /// <summary>
    /// Hourly weather data from Open-Meteo
    /// </summary>
    [Serializable]
    public class OpenMeteoHourlyData
    {
        public string[] time;
        
        // Surface level data
        public float[] temperature_2m;
        public float[] relative_humidity_2m;
        public float[] precipitation;
        public float[] precipitation_probability;
        public int[] weather_code;
        public float[] cloud_cover;
        public float[] cloud_cover_low;
        public float[] cloud_cover_mid;
        public float[] cloud_cover_high;
        public float[] wind_speed_10m;
        public float[] wind_direction_10m;
        
        // Pressure level data (altitude-specific)
        public float[] temperature_1000hPa;  // ~110m
        public float[] temperature_850hPa;   // ~1.5km
        public float[] temperature_700hPa;   // ~3km
        public float[] temperature_500hPa;   // ~5.6km
        public float[] temperature_300hPa;   // ~9.2km
        
        public float[] relative_humidity_1000hPa;
        public float[] relative_humidity_850hPa;
        public float[] relative_humidity_700hPa;
        public float[] relative_humidity_500hPa;
        public float[] relative_humidity_300hPa;
        
        public float[] cloud_cover_1000hPa;
        public float[] cloud_cover_850hPa;
        public float[] cloud_cover_700hPa;
        public float[] cloud_cover_500hPa;
        public float[] cloud_cover_300hPa;
        
        public float[] geopotential_height_1000hPa;
        public float[] geopotential_height_850hPa;
        public float[] geopotential_height_700hPa;
        public float[] geopotential_height_500hPa;
        public float[] geopotential_height_300hPa;
    }
    
    /// <summary>
    /// Current weather data from Open-Meteo
    /// </summary>
    [Serializable]
    public class OpenMeteoCurrentData
    {
        public string time;
        public float temperature_2m;
        public float relative_humidity_2m;
        public float precipitation;
        public int weather_code;
        public float cloud_cover;
        public float wind_speed_10m;
        public float wind_direction_10m;
    }
    
    #endregion
    
    #region Utility Classes
    
    /// <summary>
    /// Utility class for converting pressure levels to altitude and interpreting weather codes
    /// </summary>
    public static class OpenMeteoUtils
    {
        /// <summary>
        /// Standard pressure levels available from Open-Meteo (in hPa)
        /// </summary>
        public static readonly int[] PressureLevels = { 1000, 850, 700, 500, 300 };
        
        /// <summary>
        /// Approximate altitudes for pressure levels in feet MSL
        /// </summary>
        public static readonly Dictionary<int, float> PressureToAltitudeFt = new Dictionary<int, float>
        {
            { 1000, 360f },    // ~110m = 360ft
            { 850, 4920f },    // ~1.5km = 4920ft
            { 700, 9840f },    // ~3km = 9840ft
            { 500, 18370f },   // ~5.6km = 18370ft
            { 300, 30180f }   // ~9.2km = 30180ft
        };
        
        /// <summary>
        /// Get approximate altitude in feet for a pressure level
        /// </summary>
        public static float GetAltitudeForPressure(int hPa)
        {
            if (PressureToAltitudeFt.TryGetValue(hPa, out float altitude))
            {
                return altitude;
            }
            
            // Interpolate using barometric formula approximation
            // Each 1000m decrease in altitude roughly doubles pressure
            return Mathf.Pow(1013.25f / hPa, 0.19f) * 44330f * 3.28084f; // Convert to feet
        }
        
        /// <summary>
        /// Interpret WMO weather code and return weather characteristics
        /// </summary>
        public static WeatherCodeInfo InterpretWeatherCode(int code)
        {
            var info = new WeatherCodeInfo { code = code };
            
            switch (code)
            {
                // Clear
                case 0:
                    info.description = "Clear sky";
                    info.cellType = null; // No precipitation
                    info.intensity = 0f;
                    break;
                    
                // Clouds
                case 1:
                case 2:
                case 3:
                    info.description = "Partly to mostly cloudy";
                    info.cellType = null;
                    info.intensity = code * 0.1f;
                    break;
                    
                // Fog/mist
                case 45:
                case 48:
                    info.description = "Fog";
                    info.cellType = null;
                    info.intensity = 0.1f;
                    info.hasTurbulence = true;
                    info.turbulenceLevel = 0.2f;
                    break;
                    
                // Drizzle
                case 51:
                    info.description = "Light drizzle";
                    info.cellType = WeatherCellType.LightRain;
                    info.intensity = 0.1f;
                    break;
                case 53:
                    info.description = "Moderate drizzle";
                    info.cellType = WeatherCellType.LightRain;
                    info.intensity = 0.15f;
                    break;
                case 55:
                    info.description = "Dense drizzle";
                    info.cellType = WeatherCellType.ModerateRain;
                    info.intensity = 0.2f;
                    break;
                    
                // Freezing drizzle
                case 56:
                case 57:
                    info.description = "Freezing drizzle";
                    info.cellType = WeatherCellType.MixedPrecipitation;
                    info.intensity = 0.25f;
                    info.hasTurbulence = true;
                    info.turbulenceLevel = 0.3f;
                    break;
                    
                // Rain
                case 61:
                    info.description = "Slight rain";
                    info.cellType = WeatherCellType.LightRain;
                    info.intensity = 0.2f;
                    break;
                case 63:
                    info.description = "Moderate rain";
                    info.cellType = WeatherCellType.ModerateRain;
                    info.intensity = 0.4f;
                    break;
                case 65:
                    info.description = "Heavy rain";
                    info.cellType = WeatherCellType.HeavyRain;
                    info.intensity = 0.6f;
                    info.hasTurbulence = true;
                    info.turbulenceLevel = 0.4f;
                    break;
                    
                // Freezing rain
                case 66:
                case 67:
                    info.description = "Freezing rain";
                    info.cellType = WeatherCellType.MixedPrecipitation;
                    info.intensity = 0.5f;
                    info.hasTurbulence = true;
                    info.turbulenceLevel = 0.5f;
                    break;
                    
                // Snow
                case 71:
                    info.description = "Slight snow";
                    info.cellType = WeatherCellType.Snow;
                    info.intensity = 0.2f;
                    break;
                case 73:
                    info.description = "Moderate snow";
                    info.cellType = WeatherCellType.Snow;
                    info.intensity = 0.4f;
                    info.hasTurbulence = true;
                    info.turbulenceLevel = 0.25f;
                    break;
                case 75:
                    info.description = "Heavy snow";
                    info.cellType = WeatherCellType.Snow;
                    info.intensity = 0.6f;
                    info.hasTurbulence = true;
                    info.turbulenceLevel = 0.4f;
                    break;
                    
                // Snow grains
                case 77:
                    info.description = "Snow grains";
                    info.cellType = WeatherCellType.Snow;
                    info.intensity = 0.3f;
                    break;
                    
                // Rain showers
                case 80:
                    info.description = "Slight rain showers";
                    info.cellType = WeatherCellType.LightRain;
                    info.intensity = 0.25f;
                    info.hasTurbulence = true;
                    info.turbulenceLevel = 0.2f;
                    break;
                case 81:
                    info.description = "Moderate rain showers";
                    info.cellType = WeatherCellType.ModerateRain;
                    info.intensity = 0.45f;
                    info.hasTurbulence = true;
                    info.turbulenceLevel = 0.35f;
                    break;
                case 82:
                    info.description = "Violent rain showers";
                    info.cellType = WeatherCellType.HeavyRain;
                    info.intensity = 0.7f;
                    info.hasTurbulence = true;
                    info.turbulenceLevel = 0.6f;
                    break;
                    
                // Snow showers
                case 85:
                case 86:
                    info.description = "Snow showers";
                    info.cellType = WeatherCellType.Snow;
                    info.intensity = code == 85 ? 0.3f : 0.5f;
                    info.hasTurbulence = true;
                    info.turbulenceLevel = 0.35f;
                    break;
                    
                // Thunderstorms
                case 95:
                    info.description = "Thunderstorm";
                    info.cellType = WeatherCellType.Thunderstorm;
                    info.intensity = 0.75f;
                    info.hasLightning = true;
                    info.hasTurbulence = true;
                    info.turbulenceLevel = 0.7f;
                    break;
                case 96:
                case 99:
                    info.description = "Thunderstorm with hail";
                    info.cellType = WeatherCellType.Thunderstorm;
                    info.intensity = code == 96 ? 0.85f : 0.95f;
                    info.hasLightning = true;
                    info.hasTurbulence = true;
                    info.turbulenceLevel = 0.85f;
                    info.hasHail = true;
                    break;
                    
                default:
                    info.description = "Unknown";
                    info.cellType = null;
                    info.intensity = 0f;
                    break;
            }
            
            return info;
        }
        
        /// <summary>
        /// Calculate turbulence intensity from humidity gradient and temperature lapse rate
        /// </summary>
        public static float EstimateTurbulence(float humidityLow, float humidityHigh, float tempLow, float tempHigh, float altitudeDiffFt)
        {
            // Steeper humidity gradients and temperature inversions indicate turbulence
            float humidityGradient = Mathf.Abs(humidityHigh - humidityLow) / 100f;
            float tempLapseRate = (tempLow - tempHigh) / (altitudeDiffFt / 1000f); // per 1000ft
            
            // Normal lapse rate is about 2°C per 1000ft
            float lapseDeviation = Mathf.Abs(tempLapseRate - 2f) / 5f;
            
            return Mathf.Clamp01((humidityGradient * 0.5f + lapseDeviation * 0.5f));
        }
        
        /// <summary>
        /// Build API URL for Open-Meteo forecast
        /// </summary>
        public static string BuildForecastUrl(float latitude, float longitude, bool includePressureLevels = true)
        {
            string baseUrl = "https://api.open-meteo.com/v1/forecast";
            
            // Basic hourly variables
            string hourlyVars = "temperature_2m,relative_humidity_2m,precipitation,precipitation_probability," +
                               "weather_code,cloud_cover,cloud_cover_low,cloud_cover_mid,cloud_cover_high," +
                               "wind_speed_10m,wind_direction_10m";
            
            // Add pressure level variables if requested
            if (includePressureLevels)
            {
                string pressureVars = "";
                foreach (int level in PressureLevels)
                {
                    pressureVars += $",temperature_{level}hPa,relative_humidity_{level}hPa,cloud_cover_{level}hPa,geopotential_height_{level}hPa";
                }
                hourlyVars += pressureVars;
            }
            
            // Current weather
            string currentVars = "temperature_2m,relative_humidity_2m,precipitation,weather_code,cloud_cover,wind_speed_10m,wind_direction_10m";
            
            return $"{baseUrl}?latitude={latitude:F4}&longitude={longitude:F4}" +
                   $"&hourly={hourlyVars}" +
                   $"&current={currentVars}" +
                   $"&forecast_days=1" +
                   $"&timezone=auto";
        }
    }
    
    /// <summary>
    /// Interpreted weather code information
    /// </summary>
    public class WeatherCodeInfo
    {
        public int code;
        public string description;
        public WeatherCellType? cellType;
        public float intensity;
        public bool hasLightning;
        public bool hasTurbulence;
        public float turbulenceLevel;
        public bool hasHail;
    }
    
    #endregion
}
