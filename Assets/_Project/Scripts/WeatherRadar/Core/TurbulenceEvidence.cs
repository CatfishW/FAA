using System;
using System.Collections.Generic;
using System.Globalization;

namespace WeatherRadar
{
    /// <summary>Regional simulator settings are not a spatial turbulence radar scan.</summary>
    public static class TurbulenceEvidence
    {
        public static bool IsTurbulenceMode(RadarMode mode) => mode == RadarMode.TURB || mode == RadarMode.WX_T;
        public static bool ShouldDrawRain(RadarMode mode) => mode != RadarMode.TURB && mode != RadarMode.STBY;

        public static bool TryRegionalMaximum(IDictionary<string, float> weather, out float value, out int samples)
            => TryLayerMaximum(weather, "region", out value, out samples);

        public static bool TryAircraftMaximum(IDictionary<string, float> weather, out float value, out int samples)
            => TryLayerMaximum(weather, "aircraft", out value, out samples);

        private static bool TryLayerMaximum(IDictionary<string, float> weather, string scope, out float value, out int samples)
        {
            value = 0; samples = 0;
            if (weather == null) return false;
            for (int i = 0; i < 13; i++)
                if (weather.TryGetValue("sim/weather/" + scope + "/turbulence[" + i + "]", out float sample) &&
                    !float.IsNaN(sample) && !float.IsInfinity(sample) && sample >= 0 && sample <= 10)
                { samples++; value = Math.Max(value, sample); }
            // Partial/missing arrays cannot be called a known regional maximum.
            return samples == 13;
        }

        public static string Explanation(IDictionary<string, float> weather, bool fresh)
        {
            if (!fresh) return "Telemetry stale · no turbulence scan";
            if (!TryRegionalMaximum(weather, out float value, out _))
                return "No turbulence samples · not a clear indication";
            string region = value.ToString("0.0", CultureInfo.InvariantCulture);
            string local = TryAircraftMaximum(weather, out float aircraft, out _) ?
                "\nLocal layers max " + aircraft.ToString("0.0", CultureInfo.InvariantCulture) + "/10" : "";
            return "Region " + region + "/10" + local + " · no spatial scan";
        }
    }
}
