using System;
using System.Collections.Generic;
using UnityEngine;

namespace FAA.XPlaneIntegration.Runtime
{
    /// <summary>Non-overlapping, world-aligned DEM quadtree. Key = south/west in 0.1°, span.</summary>
    public static class XPlaneTerrainLod
    {
        public static Dictionary<Vector3Int, int> Select(Vector2Int center, int nearRadius, int rootRadius)
        {
            var result = new Dictionary<Vector3Int, int>();
            int south = (int)Math.Floor(center.x / 8d) * 8;
            // Align from -180 degrees so the quadtree also tiles the date line.
            int west = (int)Math.Floor((center.y + 1800) / 8d) * 8 - 1800;
            int radius = Mathf.Clamp(rootRadius, 1, 3);
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
                Subdivide(new Vector3Int(south + y * 8, XPlaneTerrainTile.WrapLongitudeIndex(west + x * 8), 8),
                    center, Mathf.Clamp(nearRadius, 1, 3), result);
            return result;
        }

        private static void Subdivide(Vector3Int key, Vector2Int center, int nearRadius, Dictionary<Vector3Int, int> result)
        {
            if (key.x >= 850 || key.x + key.z <= -850) return;
            bool border = key.x < -850 || key.x + key.z > 850;
            float detailRadius = nearRadius + (key.z > 2 ? key.z * .5f : 0f);
            if (key.z > 1 && (border || Distance(center, key) < detailRadius))
            {
                int half = key.z / 2;
                for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                    Subdivide(new Vector3Int(key.x + y * half, XPlaneTerrainTile.WrapLongitudeIndex(key.y + x * half), half),
                        center, nearRadius, result);
                return;
            }
            result[key] = key.z == 1 ? 129 : key.z == 2 ? 65 : 33;
        }

        public static float Distance(Vector2Int center, Vector3Int tile)
        {
            float lat = center.x + .5f - tile.x;
            float lon = XPlaneTerrainTile.WrapLongitudeIndex(center.y - tile.y) + .5f;
            return Mathf.Max(Mathf.Max(0, -lat, lat - tile.z), Mathf.Max(0, -lon, lon - tile.z));
        }

        public static bool Contains(Vector3Int outer, Vector3Int inner)
        {
            int longitude = XPlaneTerrainTile.WrapLongitudeIndex(inner.y - outer.y);
            return inner.x >= outer.x && inner.x + inner.z <= outer.x + outer.z &&
                longitude >= 0 && longitude + inner.z <= outer.z;
        }

        // Retain the old parent until ALL replacing children have arrived. This
        // avoids blank squares and overlapping surface flicker while streaming.
        public static bool CanRetire(Vector3Int old, ICollection<Vector3Int> desired, ICollection<Vector3Int> loaded)
        {
            if (desired.Contains(old)) return false;
            foreach (var next in desired)
                if ((Contains(old, next) || Contains(next, old)) && !loaded.Contains(next)) return false;
            return true;
        }
    }
}
