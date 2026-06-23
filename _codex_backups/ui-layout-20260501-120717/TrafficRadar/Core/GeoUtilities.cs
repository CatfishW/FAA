using UnityEngine;

namespace TrafficRadar.Core
{
    /// <summary>
    /// Static utility class for geographic calculations.
    /// Consolidates duplicate calculations that were in multiple classes.
    /// </summary>
    public static class GeoUtilities
    {
        // Constants
        public const float EARTH_RADIUS_KM = 6371f;
        public const float NM_TO_KM = 1.852f;
        public const float KM_TO_NM = 0.539957f;
        public const float METERS_TO_FEET = 3.28084f;
        public const float FEET_TO_METERS = 0.3048f;
        public const float MPS_TO_KNOTS = 1.94384f;
        public const float KNOTS_TO_MPS = 0.514444f;

        /// <summary>
        /// Calculate distance between two points using Haversine formula.
        /// </summary>
        /// <returns>Distance in kilometers</returns>
        public static float CalculateDistanceKm(float lat1, float lon1, float lat2, float lon2)
        {
            float dLat = (lat2 - lat1) * Mathf.Deg2Rad;
            float dLon = (lon2 - lon1) * Mathf.Deg2Rad;

            float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                      Mathf.Cos(lat1 * Mathf.Deg2Rad) * Mathf.Cos(lat2 * Mathf.Deg2Rad) *
                      Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);

            float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));

            return EARTH_RADIUS_KM * c;
        }

        /// <summary>
        /// Calculate distance between two points using Haversine formula.
        /// </summary>
        /// <returns>Distance in kilometers</returns>
        public static float CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            return CalculateDistanceKm((float)lat1, (float)lon1, (float)lat2, (float)lon2);
        }

        /// <summary>
        /// Calculate distance between two points.
        /// </summary>
        /// <returns>Distance in nautical miles</returns>
        public static float CalculateDistanceNm(float lat1, float lon1, float lat2, float lon2)
        {
            return CalculateDistanceKm(lat1, lon1, lat2, lon2) * KM_TO_NM;
        }

        /// <summary>
        /// Calculate distance between two points.
        /// </summary>
        /// <returns>Distance in nautical miles</returns>
        public static float CalculateDistanceNm(double lat1, double lon1, double lat2, double lon2)
        {
            return CalculateDistanceKm(lat1, lon1, lat2, lon2) * KM_TO_NM;
        }

        /// <summary>
        /// Calculate bearing from point 1 to point 2.
        /// </summary>
        /// <returns>Bearing in degrees (0-360)</returns>
        public static float CalculateBearing(float lat1, float lon1, float lat2, float lon2)
        {
            float dLon = (lon2 - lon1) * Mathf.Deg2Rad;
            float lat1Rad = lat1 * Mathf.Deg2Rad;
            float lat2Rad = lat2 * Mathf.Deg2Rad;

            float y = Mathf.Sin(dLon) * Mathf.Cos(lat2Rad);
            float x = Mathf.Cos(lat1Rad) * Mathf.Sin(lat2Rad) -
                      Mathf.Sin(lat1Rad) * Mathf.Cos(lat2Rad) * Mathf.Cos(dLon);

            float bearing = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            return (bearing + 360f) % 360f;
        }

        /// <summary>
        /// Calculate bearing from point 1 to point 2.
        /// </summary>
        /// <returns>Bearing in degrees (0-360)</returns>
        public static float CalculateBearing(double lat1, double lon1, double lat2, double lon2)
        {
            return CalculateBearing((float)lat1, (float)lon1, (float)lat2, (float)lon2);
        }

        /// <summary>
        /// Calculate normalized radar position from distance and bearing.
        /// </summary>
        /// <param name="distanceNm">Distance in nautical miles</param>
        /// <param name="bearingDeg">Bearing in degrees</param>
        /// <param name="ownHeadingDeg">Own ship heading in degrees (for heading-up display)</param>
        /// <param name="rangeNm">Radar range in nautical miles</param>
        /// <returns>Normalized position (-1 to 1 on both axes)</returns>
        public static Vector2 CalculateRadarPosition(float distanceNm, float bearingDeg, float ownHeadingDeg, float rangeNm)
        {
            // Convert bearing relative to own heading (heading-up display)
            float relativeBearing = (bearingDeg - ownHeadingDeg + 360f) % 360f;
            float relBearingRad = relativeBearing * Mathf.Deg2Rad;

            // Normalize distance to radar range
            float normalizedDistance = Mathf.Clamp01(distanceNm / rangeNm);

            // Calculate x,y position (0 is up, clockwise)
            float x = normalizedDistance * Mathf.Sin(relBearingRad);
            float y = normalizedDistance * Mathf.Cos(relBearingRad);

            return new Vector2(x, y);
        }

        /// <summary>
        /// Convert meters to feet.
        /// </summary>
        public static float MetersToFeet(float meters)
        {
            return meters * METERS_TO_FEET;
        }

        /// <summary>
        /// Convert feet to meters.
        /// </summary>
        public static float FeetToMeters(float feet)
        {
            return feet * FEET_TO_METERS;
        }

        /// <summary>
        /// Convert meters per second to knots.
        /// </summary>
        public static float MpsToKnots(float mps)
        {
            return mps * MPS_TO_KNOTS;
        }

        /// <summary>
        /// Convert knots to meters per second.
        /// </summary>
        public static float KnotsToMps(float knots)
        {
            return knots * KNOTS_TO_MPS;
        }
    }
}
