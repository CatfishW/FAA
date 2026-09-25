using UnityEngine;

namespace FAA.Customization
{
    /// <summary>Protects the forward cockpit viewing cone using the WHOLE utility panel, not its centre.
    /// User-interface clearance, not an aircraft operational limit. Does not track current head look.</summary>
    public static class FaaPeripheralPanelLayout
    {
        public const float ForwardHalfAngle = 60f;
        public const float ClearanceMargin = 3f;
        public const float DefaultYaw = 90f;
        public const float DefaultElevation = -8f;
        public const float DefaultDistance = 1.5f;

        public static float AngularRadius(float width, float height, float scale, float distance)
        {
            float radius = .5f * Mathf.Sqrt(width * width + height * height) * Mathf.Abs(scale);
            return Mathf.Atan2(radius, Mathf.Max(FaaSpatialLayoutMath.MinDistance, distance)) * Mathf.Rad2Deg;
        }
        public static bool Protect(FaaSpatialLayoutEntry entry, float width, float height, float fallbackSide)
        {
            if (entry == null || !FaaSpatialLayoutMath.Finite(width) || !FaaSpatialLayoutMath.Finite(height) || width <= 0 || height <= 0) return false;
            float yaw = entry.yaw, elevation = entry.elevation, distance = entry.distance, scale = entry.scale;
            if (!FaaSpatialLayoutMath.Sanitize(entry))
            {
                entry.yaw = Mathf.Sign(fallbackSide) * DefaultYaw; entry.elevation = DefaultElevation;
                entry.distance = DefaultDistance; entry.scale = 1f;
            }
            float needed = ForwardHalfAngle + ClearanceMargin + AngularRadius(width, height, entry.scale, entry.distance);
            // A very large/close group cannot clear the cone by yaw alone at an extreme
            // elevation. Reduce that elevation before solving, rather than clamping acos
            // to 180 degrees and silently leaving a panel corner in the forward cone.
            float maximumElevation = Mathf.Max(0f,180f-needed-.1f);
            entry.elevation = Mathf.Clamp(entry.elevation,-maximumElevation,maximumElevation);
            float cosElevation = Mathf.Max(.0001f, Mathf.Cos(entry.elevation * Mathf.Deg2Rad));
            float minimumYaw = Mathf.Acos(Mathf.Clamp(Mathf.Cos(needed * Mathf.Deg2Rad) / cosElevation, -1f, 1f)) * Mathf.Rad2Deg;
            float side = Mathf.Abs(entry.yaw) < .01f ? (fallbackSide < 0 ? -1 : 1) : Mathf.Sign(entry.yaw);
            if (Mathf.Abs(entry.yaw) < minimumYaw) entry.yaw = side * minimumYaw;
            return !Mathf.Approximately(yaw,entry.yaw) || !Mathf.Approximately(elevation,entry.elevation) ||
                !Mathf.Approximately(distance,entry.distance) || !Mathf.Approximately(scale,entry.scale);
        }
    }
}
