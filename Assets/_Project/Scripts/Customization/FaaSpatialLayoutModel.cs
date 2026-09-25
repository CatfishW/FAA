using System;
using System.Collections.Generic;
using UnityEngine;

namespace FAA.Customization
{
    [Serializable]
    public sealed class FaaSpatialLayoutEntry
    {
        public string id;
        public float yaw;
        public float elevation = -25f;
        public float distance = 1.2f;
        public float scale = 1f;
        public FaaSpatialLayoutEntry Copy() => (FaaSpatialLayoutEntry)MemberwiseClone();
    }

    [Serializable]
    public sealed class FaaSpatialLayoutProfile
    {
        public int version = 1;
        public List<FaaSpatialLayoutEntry> entries = new();
    }

    /// <summary>Research UI ergonomics bounds, not aircraft operating limits or optical calibration.</summary>
    public static class FaaSpatialLayoutMath
    {
        public const float MinScale = .4f, MaxScale = 1.6f;
        public const float MinDistance = .55f, MaxDistance = 3f;
        public static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        public static bool Finite(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z);
        public static float Scale(float value) => Finite(value) ? Mathf.Clamp(value, MinScale, MaxScale) : 1f;

        public static Vector3 Position(FaaSpatialLayoutEntry p)
        {
            float yaw = p.yaw * Mathf.Deg2Rad, elevation = p.elevation * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(yaw) * Mathf.Cos(elevation), Mathf.Sin(elevation),
                Mathf.Cos(yaw) * Mathf.Cos(elevation)) * p.distance;
        }

        public static bool SetPosition(FaaSpatialLayoutEntry p, Vector3 position)
        {
            if (p == null || !Finite(position) || position.sqrMagnitude < .0001f) return false;
            p.yaw = Mathf.Atan2(position.x, position.z) * Mathf.Rad2Deg;
            p.elevation = Mathf.Clamp(Mathf.Atan2(position.y, new Vector2(position.x, position.z).magnitude) * Mathf.Rad2Deg, -80f, 80f);
            p.distance = Mathf.Clamp(position.magnitude, MinDistance, MaxDistance);
            return true;
        }

        public static bool Sanitize(FaaSpatialLayoutEntry p)
        {
            if (p == null || string.IsNullOrEmpty(p.id) || p.id.Length > 80 || !Finite(p.yaw) ||
                !Finite(p.elevation) || !Finite(p.distance) || !Finite(p.scale)) return false;
            p.yaw = Mathf.Repeat(p.yaw + 180f, 360f) - 180f;
            p.elevation = Mathf.Clamp(p.elevation, -80f, 80f);
            p.distance = Mathf.Clamp(p.distance, MinDistance, MaxDistance);
            p.scale = Scale(p.scale);
            return true;
        }

        public static bool TryReadProfile(string json, out FaaSpatialLayoutProfile profile)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > 32768) return false;
            try
            {
                var candidate = JsonUtility.FromJson<FaaSpatialLayoutProfile>(json);
                if (candidate == null || candidate.version != 1 || candidate.entries == null || candidate.entries.Count > 64) return false;
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in candidate.entries)
                    if (!Sanitize(item) || !ids.Add(item.id)) return false;
                profile = candidate;
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        public static bool TryResize(float baselineScale, float baselineSeparation, float separation, out float result)
        {
            result = Scale(baselineScale);
            if (!Finite(baselineSeparation) || !Finite(separation) || baselineSeparation < .04f || separation < .01f) return false;
            result = Scale(baselineScale * separation / baselineSeparation);
            return true;
        }

        public static bool Pinched(float strength, bool wasPinched) =>
            Finite(strength) && strength >= (wasPinched ? .65f : .85f);
    }
}
