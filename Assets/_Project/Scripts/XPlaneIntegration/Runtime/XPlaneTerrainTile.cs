using System;
using FAA.Geo;
using UnityEngine;

namespace FAA.XPlaneIntegration.Runtime
{
    [Serializable]
    public sealed class XPlaneTerrainSource
    {
        public string name;
        public string revision;
        public int width;
        public int height;
        public bool post_centric;
    }

    /// <summary>Versioned, geographically fixed elevation tile, not a camera-following surface.</summary>
    [Serializable]
    public sealed class XPlaneTerrainTile
    {
        public int schema_version;
        public string source_kind;
        public string status;
        public string altitude_datum;
        public string row_order;
        public string column_order;
        public int lat_index;
        public int lon_index;
        public double tile_degrees;
        public double south;
        public double west;
        public int resolution;
        public int span = 1;
        public string generated_utc;
        public XPlaneTerrainSource[] sources;
        public float[] heights_m;

        public bool Validate(int expectedLat, int expectedLon, int expectedResolution, out string error)
        {
            error = "Invalid terrain response";
            if ((schema_version != 1 && schema_version != 2) || (schema_version == 1 && Span != 1) ||
                source_kind != "xplane_dsf_elevation" || status != "ready" ||
                altitude_datum != "MSL_m" || row_order != "south_to_north" || column_order != "west_to_east") return false;
            if (lat_index != expectedLat || lon_index != expectedLon || resolution != expectedResolution ||
                lat_index < -850 || lat_index >= 850 || lon_index < -1800 || lon_index >= 1800 ||
                (resolution != 33 && resolution != 65 && resolution != 129)) return false;
            if ((Span != 1 && Span != 2 && Span != 4 && Span != 8) || lat_index + Span > 850 ||
                !Finite(tile_degrees) || Math.Abs(tile_degrees - Span / 10d) > 1e-10 ||
                !Finite(south) || !Finite(west) || Math.Abs(south - lat_index / 10d) > 1e-10 ||
                Math.Abs(west - lon_index / 10d) > 1e-10) return false;
            if (sources == null || sources.Length == 0 || heights_m == null || heights_m.Length != resolution * resolution) return false;
            foreach (var source in sources)
                if (source == null || string.IsNullOrWhiteSpace(source.name) || string.IsNullOrWhiteSpace(source.revision) ||
                    source.width < 2 || source.height < 2) return false;
            foreach (float value in heights_m)
                if (!Finite(value) || value < -12000f || value > 12000f) return false;
            error = string.Empty;
            return true;
        }

        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static Vector2Int Key(double latitude, double longitude)
        {
            double wrappedLongitude = ((longitude + 180d) % 360d + 360d) % 360d - 180d;
            return new Vector2Int((int)Math.Floor(latitude * 10d), (int)Math.Floor(wrappedLongitude * 10d));
        }

        public static int WrapLongitudeIndex(int index) => ((index + 1800) % 3600 + 3600) % 3600 - 1800;

        // Old schema-1 services omit span. Schema 2 requires it explicitly.
        public int Span => schema_version == 1 && span == 0 ? 1 : span;
        public double Latitude(int row) => (lat_index * (resolution - 1) + row * Span) / (10d * (resolution - 1));
        public double Longitude(int column) => (lon_index * (resolution - 1) + column * Span) / (10d * (resolution - 1));

        public Mesh BuildMesh(GeoPosUnityPosProjectManager projection) => BuildRenderMesh(projection, 0f);

        public Mesh BuildRenderMesh(GeoPosUnityPosProjectManager projection, float skirtDepthMeters)
        {
            if (!Validate(lat_index, lon_index, resolution, out string error)) throw new ArgumentException(error);
            Vector3 anchor = projection.GeoToUnityPosition(south, west, 0f);
            bool skirts = skirtDepthMeters > 0f;
            var vertices = new Vector3[heights_m.Length + (skirts ? resolution * 8 : 0)];
            var colors = new Color[vertices.Length];
            for (int row = 0; row < resolution; row++)
            for (int column = 0; column < resolution; column++)
            {
                int index = row * resolution + column;
                vertices[index] = projection.GeoToUnityPosition(Latitude(row), Longitude(column), heights_m[index]) - anchor;
                // Neutral synthetic elevation palette, NOT land use or a clearance warning.
                colors[index] = ElevationColor(heights_m[index]);
            }
            var triangles = new int[(resolution - 1) * (resolution - 1) * 6 + (skirts ? (resolution - 1) * 24 : 0)];
            int cursor = 0;
            for (int row = 0; row < resolution - 1; row++)
            for (int column = 0; column < resolution - 1; column++)
            {
                int sw = row * resolution + column;
                triangles[cursor++] = sw; triangles[cursor++] = sw + resolution; triangles[cursor++] = sw + 1;
                triangles[cursor++] = sw + 1; triangles[cursor++] = sw + resolution; triangles[cursor++] = sw + resolution + 1;
            }
            if (skirts)
            {
                // Separate top vertices keep skirt normals out of the surface.
                // These hide LOD T-junctions; source surface elevations never change.
                float depth = Mathf.Abs(projection.GeoToUnityPosition(south, west, -skirtDepthMeters).y - anchor.y);
                for (int side = 0; side < 4; side++)
                for (int i = 0; i < resolution; i++)
                {
                    int edge = side == 0 ? i : side == 1 ? i * resolution + resolution - 1 :
                        side == 2 ? resolution * resolution - 1 - i : (resolution - 1 - i) * resolution;
                    int top = heights_m.Length + (side * resolution + i) * 2;
                    vertices[top] = vertices[edge]; vertices[top + 1] = vertices[edge] - Vector3.up * depth;
                    colors[top] = colors[top + 1] = colors[edge];
                    if (i == resolution - 1) continue;
                    triangles[cursor++] = top; triangles[cursor++] = top + 2; triangles[cursor++] = top + 1;
                    triangles[cursor++] = top + 2; triangles[cursor++] = top + 3; triangles[cursor++] = top + 1;
                }
            }
            var mesh = new Mesh { name = $"X-Plane DEM {lat_index}/{lon_index}", vertices = vertices, colors = colors, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Color ElevationColor(float meters)
        {
            Color low = new Color(.22f, .30f, .22f);
            Color hill = new Color(.42f, .43f, .30f);
            Color rock = new Color(.57f, .54f, .47f);
            if (meters < 1200f) return Color.Lerp(low, hill, Mathf.Clamp01(meters / 1200f));
            return Color.Lerp(hill, rock, Mathf.Clamp01((meters - 1200f) / 2800f));
        }
    }
}
