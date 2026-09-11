using UnityEngine;

namespace WeatherRadar
{
    /// <summary>
    /// An illustrative, north-aligned weather field sampled in nautical miles.
    /// Point weather data cannot supply measured spatial reflectivity. This model
    /// only makes SIM WX zoom/gain consistent; it is not weather-avoidance data.
    /// </summary>
    public static class XPlaneSimWeatherField
    {
        public static Vector2 SamplePositionNM(Vector2 scopeOffset, float rangeNM, float headingDegrees, Vector2 aircraftOffsetNM)
        {
            float range = IsFinite(rangeNM) && rangeNM > 0f ? Mathf.Clamp(rangeNM, 5f, 320f) : 80f;
            float heading = (IsFinite(headingDegrees) ? headingDegrees : 0f) * Mathf.Deg2Rad;
            float sin = Mathf.Sin(heading), cos = Mathf.Cos(heading);
            Vector2 local = scopeOffset * range;
            return aircraftOffsetNM + new Vector2(local.x * cos + local.y * sin, local.y * cos - local.x * sin);
        }

        public static float ApplyGain(float signal, float gainDb)
        {
            if (!IsFinite(signal) || signal <= 0f) return 0f;
            float gain = IsFinite(gainDb) ? Mathf.Clamp(gainDb, -8f, 8f) : 0f;
            // Linear echo amplitude: +6 dB approximately doubles the signal.
            return signal * Mathf.Pow(10f, gain / 20f);
        }

        public static float SampleSignal(Vector2 positionNM, float precipitation, float turbulence)
        {
            if (!IsFinite(precipitation) || precipitation <= 0.025f) return 0f;
            float intensity = Mathf.Clamp01(precipitation);
            float east = positionNM.x, north = positionNM.y;
            // Fixed physical scales and seeds. Range and heading must not reseed
            // the cells, and a return must keep its range/bearing when zoomed.
            float broad = Mathf.PerlinNoise(east / 28f + 31.7f, north / 28f + 53.1f);
            float cells = Mathf.PerlinNoise(east / 7f + 17.3f, north / 7f + 29.9f);
            float fine = Mathf.PerlinNoise(east / 1.8f + 61.2f, north / 1.8f + 11.4f);
            float filaments = Mathf.PerlinNoise(east / 18f + 73.4f, north / 4f + 23.6f);
            float signal = Mask(0.30f, 0.76f, broad) * 0.58f + Mask(0.36f, 0.78f, cells) * 0.30f +
                Mask(0.44f, 0.84f, filaments) * 0.12f;
            signal += intensity * 0.10f;
            signal += Mathf.Clamp01(turbulence) * Mathf.Max(0f, fine - 0.58f) * 0.14f;
            signal *= Mathf.Lerp(0.70f, 1.34f, intensity);
            return fine < 0.18f || (fine > 0.92f && signal < 0.96f) ? 0f : signal;
        }

        private static float Mask(float lower, float upper, float value) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lower, upper, value));
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
