using UnityEngine;

namespace WeatherRadar
{
    /// <summary>Shared projection for the dataref illustration, sweep and vector face.</summary>
    public static class XPlaneWeatherRadarGeometry
    {
        public const float Aspect = 724f / 512f;
        public const float OriginHeight = 0.09f;
        public const float Radius = 0.84f;
        public const float HalfAngle = 55f;

        public static float RangeAtRing(float rangeNM, int ring, int count = 4) =>
            Mathf.Max(0f, rangeNM) * Mathf.Clamp(ring, 0, Mathf.Max(1, count)) / Mathf.Max(1, count);

        public static Vector2 Point(Vector2 origin, float radius, float bearing)
        {
            float radians = bearing * Mathf.Deg2Rad;
            return origin + new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * radius;
        }
    }
}
