using System;
using UnityEngine;

namespace WeatherRadar
{
    /// <summary>
    /// Parameters for each radar mode
    /// </summary>
    [Serializable]
    public class ModeParameters
    {
        [Header("Display Options")]
        public bool showWeatherReturns = true;
        public bool showTurbulence = false;
        public bool showGroundClutter = false;
        public bool scanActive = true;

        [Header("Scan Settings")]
        public float sweepSpeed = 180f;

        [Header("Gain Settings")]
        public bool useCustomGain = false;
        public float defaultGain = 0f;

        [Header("Color Overrides")]
        public bool useCustomColors = false;
        public Color precipitationColorOverride = Color.green;

        // Default mode configurations
        public static ModeParameters DefaultWX => new ModeParameters
        {
            showWeatherReturns = true,
            showTurbulence = false,
            showGroundClutter = false,
            scanActive = true,
            sweepSpeed = 180f
        };

        public static ModeParameters DefaultWXT => new ModeParameters
        {
            showWeatherReturns = true,
            showTurbulence = true,
            showGroundClutter = false,
            scanActive = true,
            sweepSpeed = 180f
        };

        public static ModeParameters DefaultTURB => new ModeParameters
        {
            showWeatherReturns = false,
            showTurbulence = true,
            showGroundClutter = false,
            scanActive = true,
            sweepSpeed = 120f,
            useCustomGain = true,
            defaultGain = 4f
        };

        public static ModeParameters DefaultMAP => new ModeParameters
        {
            showWeatherReturns = false,
            showTurbulence = false,
            showGroundClutter = true,
            scanActive = true,
            sweepSpeed = 180f
        };

        public static ModeParameters DefaultSTBY => new ModeParameters
        {
            showWeatherReturns = false,
            showTurbulence = false,
            showGroundClutter = false,
            scanActive = false,
            sweepSpeed = 0f
        };
    }
}
