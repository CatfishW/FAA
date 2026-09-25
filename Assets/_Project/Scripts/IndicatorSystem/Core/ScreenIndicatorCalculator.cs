using UnityEngine;

namespace IndicatorSystem.Core
{
    /// <summary>
    /// Static utility for calculating screen-space indicator positioning.
    /// High cohesion: Single responsibility for geometric calculations.
    /// </summary>
    public static class ScreenIndicatorCalculator
    {
        private const float NauticalMileToMeters = 1852f;
        private const float FeetToMeters = 0.3048f;
        private const float LowerBandEligibleHeightRatio = 0.58f;
        private const float LowerBandTargetHeightRatio = 0.42f;

        /// <summary>
        /// Calculate indicator data from a world position.
        /// </summary>
        /// <param name="worldPosition">Target position in world space</param>
        /// <param name="camera">Camera to use for screen projection</param>
        /// <param name="edgeConfig">Edge clamping configuration</param>
        /// <returns>Calculated indicator data with screen position and visibility</returns>
        public static IndicatorData CalculateIndicator(
            IIndicatorTarget target,
            Camera camera,
            IndicatorEdgeConfig edgeConfig)
        {
            var data = new IndicatorData
            {
                Id = target.Id,
                Type = target.Type,
                Color = target.DisplayColor,
                Priority = target.Priority,
                Label = target.Label,
                DistanceNM = target.DistanceNM,
                RelativeAltitudeFeet = target.RelativeAltitudeFeet,
                WorldPosition = target.WorldPosition,
                AircraftType = target.AircraftType,
                Heading = target.Heading,
                IsActive = true
            };

            // Check distance limit
            if (target.DistanceNM > edgeConfig.MaxDisplayDistance)
            {
                data.Visibility = IndicatorVisibility.OutOfRange;
                data.IsActive = false;
                return data;
            }

            // Get viewport position (0-1 range)
            Vector3 viewportPos = camera.WorldToViewportPoint(target.WorldPosition);
            
            // Check if behind camera
            if (viewportPos.z < 0)
            {
                data.Visibility = IndicatorVisibility.Behind;
                // Flip position for behind-camera targets
                viewportPos.x = 1f - viewportPos.x;
                viewportPos.y = 1f - viewportPos.y;
            }

            // Convert to screen space
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            Vector2 screenPos = new Vector2(
                viewportPos.x * screenWidth,
                viewportPos.y * screenHeight
            );

            // Determine if on-screen (within padded bounds)
            float padding = edgeConfig.EdgePadding;
            bool isOnScreen = viewportPos.z > 0 &&
                              screenPos.x >= padding &&
                              screenPos.x <= screenWidth - padding &&
                              screenPos.y >= padding &&
                              screenPos.y <= screenHeight - padding;

            if (isOnScreen)
            {
                data.Visibility = IndicatorVisibility.OnScreen;
                data.ScreenPosition = screenPos;
                data.ArrowRotation = 0f;
            }
            else
            {
                // Off-screen: clamp to edge and calculate arrow rotation
                if (data.Visibility != IndicatorVisibility.Behind)
                    data.Visibility = IndicatorVisibility.OffScreen;
                
                Vector2 clampedPos = ClampToScreenEdge(screenPos, screenWidth, screenHeight, padding);
                clampedPos = ApplyLowerBandBias(target.Type, screenPos, clampedPos, screenWidth, screenHeight, padding);
                data.ScreenPosition = clampedPos;
                
                // Calculate arrow rotation pointing toward target
                Vector2 screenCenter = new Vector2(screenWidth / 2f, screenHeight / 2f);
                Vector2 direction = (screenPos - screenCenter).normalized;
                data.ArrowRotation = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            }

            return data;
        }

        /// <summary>
        /// Clamp a screen position to the screen edge with padding.
        /// </summary>
        private static Vector2 ClampToScreenEdge(Vector2 screenPos, float width, float height, float padding)
        {
            Vector2 center = new Vector2(width / 2f, height / 2f);
            Vector2 direction = screenPos - center;
            
            if (direction.sqrMagnitude < 0.001f)
                return center;

            direction.Normalize();

            // Calculate intersection with screen bounds
            float minX = padding;
            float maxX = width - padding;
            float minY = padding;
            float maxY = height - padding;

            // Find the scalar to reach each edge
            float tX = direction.x != 0 
                ? (direction.x > 0 ? (maxX - center.x) / direction.x : (minX - center.x) / direction.x) 
                : float.MaxValue;
            float tY = direction.y != 0 
                ? (direction.y > 0 ? (maxY - center.y) / direction.y : (minY - center.y) / direction.y) 
                : float.MaxValue;

            // Use the smaller scalar (first intersection)
            float t = Mathf.Min(Mathf.Abs(tX), Mathf.Abs(tY));
            
            return center + direction * t;
        }

        private static Vector2 ApplyLowerBandBias(
            IndicatorType type,
            Vector2 sourceScreenPos,
            Vector2 clampedPos,
            float width,
            float height,
            float padding)
        {
            if (type != IndicatorType.Weather && type != IndicatorType.Traffic)
            {
                return clampedPos;
            }

            bool sideClamped = clampedPos.x <= padding + 1f || clampedPos.x >= width - padding - 1f;
            bool lowerHalfTarget = sourceScreenPos.y <= height * LowerBandEligibleHeightRatio ||
                                   clampedPos.y <= height * LowerBandEligibleHeightRatio;
            if (!sideClamped || !lowerHalfTarget)
            {
                return clampedPos;
            }

            float lowerBandY = Mathf.Clamp(
                height * LowerBandTargetHeightRatio,
                padding + 18f,
                height - padding - 18f);
            clampedPos.y = Mathf.Min(clampedPos.y, lowerBandY);
            return clampedPos;
        }

        /// <summary>
        /// Convert geographic coordinates to Unity world position.
        /// Uses a simple local tangent plane approximation around a reference point.
        /// </summary>
        /// <param name="latitude">Target latitude in degrees</param>
        /// <param name="longitude">Target longitude in degrees</param>
        /// <param name="altitudeMeters">Target altitude in meters MSL</param>
        /// <param name="refLatitude">Reference latitude (own position)</param>
        /// <param name="refLongitude">Reference longitude (own position)</param>
        /// <param name="refAltitude">Reference altitude in meters</param>
        /// <param name="metersPerUnit">Meters per Unity unit (default 1)</param>
        /// <returns>World position in Unity coordinates</returns>
        public static Vector3 GeoToWorldPosition(
            double latitude, double longitude, float altitudeMeters,
            double refLatitude, double refLongitude, float refAltitude,
            float metersPerUnit = 1f)
        {
            // Earth's radius in meters
            const double EarthRadius = 6371000.0;
            
            // Convert to radians
            double latRad = latitude * Mathf.Deg2Rad;
            double lonRad = longitude * Mathf.Deg2Rad;
            double refLatRad = refLatitude * Mathf.Deg2Rad;
            double refLonRad = refLongitude * Mathf.Deg2Rad;
            
            // Calculate differences
            double dLat = latRad - refLatRad;
            double dLon = lonRad - refLonRad;
            
            // Convert to meters using small angle approximation
            double northMeters = dLat * EarthRadius;
            double eastMeters = dLon * EarthRadius * System.Math.Cos(refLatRad);
            double upMeters = altitudeMeters - refAltitude;
            
            // Convert to Unity coordinates (X=East, Y=Up, Z=North)
            return new Vector3(
                (float)(eastMeters / metersPerUnit),
                (float)(upMeters / metersPerUnit),
                (float)(northMeters / metersPerUnit)
            );
        }

        /// <summary>
        /// Convert a heading-up radar bearing/range into a world point relative to the headset/camera.
        /// This keeps screen-edge cues aligned with X-Plane radar symbology even when the aircraft turns.
        /// </summary>
        public static Vector3 RadarRelativeToWorldPosition(
            float distanceNM,
            float relativeBearingDegrees,
            float relativeAltitudeFeet,
            Transform reference)
        {
            return RadarRelativeMetersToWorldPosition(
                Mathf.Max(0f, distanceNM) * NauticalMileToMeters,
                relativeBearingDegrees,
                relativeAltitudeFeet * FeetToMeters,
                reference);
        }

        /// <summary>
        /// Convert a heading-up radar bearing/range into a world point relative to the headset/camera.
        /// </summary>
        public static Vector3 RadarRelativeMetersToWorldPosition(
            float distanceMeters,
            float relativeBearingDegrees,
            float verticalOffsetMeters,
            Transform reference)
        {
            Vector3 origin = reference != null ? reference.position : Vector3.zero;
            Vector3 forward = reference != null ? reference.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            Vector3 right = reference != null ? reference.right : Vector3.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.Cross(Vector3.up, forward);
            }
            right.Normalize();

            float bearingRad = relativeBearingDegrees * Mathf.Deg2Rad;
            float sideMeters = distanceMeters * Mathf.Sin(bearingRad);
            float forwardMeters = distanceMeters * Mathf.Cos(bearingRad);

            return origin + right * sideMeters + forward * forwardMeters + Vector3.up * verticalOffsetMeters;
        }

        /// <summary>
        /// Return a signed heading-up bearing where 0 is ahead, +90 is right, and -90 is left.
        /// </summary>
        public static float CalculateRelativeBearing(float absoluteBearingDegrees, float ownHeadingDegrees)
        {
            return Mathf.DeltaAngle(ownHeadingDegrees, absoluteBearingDegrees);
        }

        /// <summary>
        /// Calculate bearing from reference to target position.
        /// </summary>
        public static float CalculateBearing(double lat1, double lon1, double lat2, double lon2)
        {
            float dLon = (float)(lon2 - lon1) * Mathf.Deg2Rad;
            float lat1Rad = (float)lat1 * Mathf.Deg2Rad;
            float lat2Rad = (float)lat2 * Mathf.Deg2Rad;

            float y = Mathf.Sin(dLon) * Mathf.Cos(lat2Rad);
            float x = Mathf.Cos(lat1Rad) * Mathf.Sin(lat2Rad) -
                      Mathf.Sin(lat1Rad) * Mathf.Cos(lat2Rad) * Mathf.Cos(dLon);

            float bearing = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            return (bearing + 360f) % 360f;
        }
    }
}
