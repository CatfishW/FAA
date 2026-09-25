using UnityEngine;

namespace FAA.Customization
{
    /// <summary>Pure geometry for the research rotorcraft display. Unity axes: east X, up Y, north Z.</summary>
    public static class FaaRotorcraftCueMath
    {
        public const float KnotsToMetersPerSecond = 0.514444444f;
        public const float FeetPerMinuteToMetersPerSecond = 0.00508f;

        public static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        public static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);

        public static Vector3 EarthRay(float elevationDegrees, float trueBearingDegrees)
        {
            if (!Finite(elevationDegrees) || !Finite(trueBearingDegrees)) return Vector3.zero;
            float e = elevationDegrees * Mathf.Deg2Rad;
            float b = trueBearingDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(b) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(b) * Mathf.Cos(e));
        }

        /// <summary>Uses ground track, never magnetic heading or indicated airspeed. Zero speed needs no track.</summary>
        public static bool TryGroundVelocity(float groundSpeedKnots, float trueTrackDegrees,
            float verticalSpeedFeetPerMinute, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (!Finite(groundSpeedKnots) || groundSpeedKnots < 0f || !Finite(verticalSpeedFeetPerMinute)) return false;
            if (groundSpeedKnots > 0.01f && !Finite(trueTrackDegrees)) return false;
            if (groundSpeedKnots > 0.01f)
                velocity = EarthRay(0f, trueTrackDegrees) * (groundSpeedKnots * KnotsToMetersPerSecond);
            velocity.y = verticalSpeedFeetPerMinute * FeetPerMinuteToMetersPerSecond;
            return Finite(velocity);
        }

        public static bool TryFlightPathAngle(Vector3 velocity, out float degrees)
        {
            degrees = 0f;
            if (!Finite(velocity) || velocity.sqrMagnitude < 0.000001f) return false;
            degrees = Mathf.Atan2(velocity.y, new Vector2(velocity.x, velocity.z).magnitude) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>Hysteresis avoids mode flicker: enter at 5 kt, leave at 8 kt. Invalid speed is not hover.</summary>
        public static bool UseHover(float knots, bool wasHover, float enter = 5f, float exit = 8f) =>
            Finite(knots) && knots >= 0f && knots < (wasHover ? Mathf.Max(enter, exit) : enter);

        /// <summary>Plan-view instrument units: right X, forward Y, in knots. Not a conformal world vector.</summary>
        public static Vector2 HoverVelocity(Vector3 groundVelocity, float trueHeadingDegrees)
        {
            if (!Finite(groundVelocity) || !Finite(trueHeadingDegrees)) return Vector2.zero;
            Vector3 bodyHorizontal = Quaternion.AngleAxis(-trueHeadingDegrees, Vector3.up) * groundVelocity;
            return new Vector2(bodyHorizontal.x, bodyHorizontal.z) / KnotsToMetersPerSecond;
        }

        /// <summary>Stable screen observation for tests/status only; native stereo rendering projects the real mesh per eye.</summary>
        public static bool TryProjectDirection(Camera view, Vector3 worldDirection, out Vector2 viewport)
        {
            viewport = Vector2.zero;
            if (view == null || view.orthographic || !Finite(worldDirection) || worldDirection.sqrMagnitude < .000001f) return false;
            Vector3 local = Quaternion.Inverse(view.transform.rotation) * worldDirection;
            if (local.z <= .0001f) return false;
            Vector4 clip = view.nonJitteredProjectionMatrix * new Vector4(local.x, local.y, -local.z, 0f);
            if (!Finite(clip.w) || Mathf.Abs(clip.w) < .000001f) return false;
            viewport = new Vector2(clip.x / clip.w * .5f + .5f, clip.y / clip.w * .5f + .5f);
            return Finite(viewport.x) && Finite(viewport.y);
        }

        public static bool InView(Camera view, Vector3 direction, float margin = .015f) =>
            TryProjectDirection(view, direction, out Vector2 p) &&
            p.x >= margin && p.x <= 1f - margin && p.y >= margin && p.y <= 1f - margin;

        public static bool Fresh(bool healthy, float packetAge, float maximumAge) =>
            healthy && Finite(packetAge) && packetAge >= 0f && packetAge <= maximumAge;

        public static float SelectFpa(float current, float requested) =>
            Finite(requested) ? Mathf.Clamp(requested, -15f, 10f) : current;
    }
}
