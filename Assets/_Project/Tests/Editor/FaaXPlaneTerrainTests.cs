using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class FaaXPlaneTerrainTests
    {
        private static Type TileType => Type.GetType("FAA.XPlaneIntegration.Runtime.XPlaneTerrainTile, Assembly-CSharp", true);
        private static Type GeoType => Type.GetType("FAA.Geo.GeoPosUnityPosProjectManager, FAA.Geo", true);
        private static Type LodType => Type.GetType("FAA.XPlaneIntegration.Runtime.XPlaneTerrainLod, Assembly-CSharp", true);
        private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method).Invoke(obj, args);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field).SetValue(obj, value);
        private static object Tile(int lat = 337, int lon = -829)
        {
            object tile = Activator.CreateInstance(TileType);
            Set(tile, "schema_version", 1); Set(tile, "source_kind", "xplane_dsf_elevation"); Set(tile, "status", "ready");
            Set(tile, "altitude_datum", "MSL_m"); Set(tile, "row_order", "south_to_north"); Set(tile, "column_order", "west_to_east");
            Set(tile, "lat_index", lat); Set(tile, "lon_index", lon); Set(tile, "tile_degrees", .1d);
            Set(tile, "south", lat / 10d); Set(tile, "west", lon / 10d); Set(tile, "resolution", 33);
            var heights = new float[33 * 33];
            for (int i = 0; i < heights.Length; i++) heights[i] = 100 + i / 33 * 2 + i % 33;
            Set(tile, "heights_m", heights);
            var sourceType = Type.GetType("FAA.XPlaneIntegration.Runtime.XPlaneTerrainSource, Assembly-CSharp", true);
            var source = Activator.CreateInstance(sourceType);
            Set(source, "name", "synthetic-test.dsf"); Set(source, "revision", "test-only"); Set(source, "width", 1201); Set(source, "height", 1201);
            var sources = Array.CreateInstance(sourceType, 1); sources.SetValue(source, 0); Set(tile, "sources", sources);
            return tile;
        }

        [Test]
        public void ValidatedContractRequiresUnitsOrderingAndGeographicIdentity()
        {
            object tile = Tile();
            Assert.That(Call(tile, "Validate", 337, -829, 33, null), Is.True);
            Assert.That(Call(tile, "Validate", 337, -828, 33, null), Is.False);
            foreach (string field in new[] { "source_kind", "altitude_datum", "row_order", "column_order", "status" })
            {
                object fresh = Tile(); Set(fresh, field, "wrong");
                Assert.That(Call(fresh, "Validate", 337, -829, 33, null), Is.False, field);
            }
        }

        [Test]
        public void MissingNonfiniteAndOversizeDataAreRejected()
        {
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -32768f, 90000f })
            {
                object tile = Tile(); var heights = (float[])TileType.GetField("heights_m").GetValue(tile);
                heights[0] = invalid;
                Assert.That(Call(tile, "Validate", 337, -829, 33, null), Is.False);
            }
            object missing = Tile(); Set(missing, "heights_m", new float[5]);
            Assert.That(Call(missing, "Validate", 337, -829, 33, null), Is.False);
            missing = Tile(); Set(missing, "sources", null);
            Assert.That(Call(missing, "Validate", 337, -829, 33, null), Is.False);
            missing = Tile(); Set(missing, "south", double.NaN);
            Assert.That(Call(missing, "Validate", 337, -829, 33, null), Is.False);
        }

        [TestCase(-.01, -.01, -1, -1)]
        [TestCase(33.72, -82.86, 337, -829)]
        [TestCase(0, 180, 0, -1800)]
        public void TileSelectionUsesFloorAndLongitudeWrap(double lat, double lon, int x, int y)
        {
            Assert.That(TileType.GetMethod("Key").Invoke(null, new object[] { lat, lon }), Is.EqualTo(new Vector2Int(x, y)));
        }

        [Test]
        public void NeighborTilesHaveIdenticalGeographicEdges()
        {
            object left = Tile(), right = Tile(337, -828), north = Tile(338, -829);
            Assert.That(Call(left, "Longitude", 32), Is.EqualTo(Call(right, "Longitude", 0)));
            Assert.That(Call(left, "Latitude", 32), Is.EqualTo(Call(north, "Latitude", 0)));
        }

        [Test]
        public void MeshFacesUpAndUsesSameMslFrameAsAircraft()
        {
            var go = new GameObject("Terrain projection test");
            Mesh mesh = null;
            try
            {
                Component geo = go.AddComponent(GeoType);
                Call(geo, "SetOrigin", 33.7d, -82.9d, 1000f);
                mesh = (Mesh)Call(Tile(), "BuildMesh", geo);
                Assert.That(mesh.vertexCount, Is.EqualTo(1089));
                Assert.That(mesh.triangles.Length, Is.EqualTo(32 * 32 * 6));
                Vector3 anchor = (Vector3)Call(geo, "GeoToUnityPosition", 33.7d, -82.9d, 0f);
                Vector3 ownship = (Vector3)Call(geo, "GeoToUnityPosition", 33.7d, -82.9d, 1100f);
                Assert.That(ownship.y - (anchor + mesh.vertices[0]).y, Is.EqualTo(1000f).Within(.001f));
                Assert.That(mesh.vertices[32].x, Is.GreaterThan(mesh.vertices[0].x));
                Assert.That(mesh.vertices[32 * 33].z, Is.GreaterThan(mesh.vertices[0].z));
                Assert.That(mesh.normals[100].y, Is.GreaterThan(.99f));
            }
            finally { if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void OriginRebaseDoesNotChangeAircraftTerrainSeparation()
        {
            var go = new GameObject("Terrain rebase test");
            Mesh before = null, after = null;
            try
            {
                Component geo = go.AddComponent(GeoType);
                object tile = Tile();
                Call(geo, "SetOrigin", 33.7d, -82.9d, 1000f);
                before = (Mesh)Call(tile, "BuildMesh", geo);
                Vector3 ground = before.vertices[0] + (Vector3)Call(geo, "GeoToUnityPosition", 33.7d, -82.9d, 0f);
                Vector3 aircraft = (Vector3)Call(geo, "GeoToUnityPosition", 33.7d, -82.9d, 1100f);
                Call(geo, "SetOrigin", 33.8d, -82.8d, 2000f);
                after = (Mesh)Call(tile, "BuildMesh", geo);
                Vector3 newGround = after.vertices[0] + (Vector3)Call(geo, "GeoToUnityPosition", 33.7d, -82.9d, 0f);
                Vector3 newAircraft = (Vector3)Call(geo, "GeoToUnityPosition", 33.7d, -82.9d, 1100f);
                Assert.That(Vector3.Distance(aircraft - ground, newAircraft - newGround), Is.LessThan(.001f));
            }
            finally
            {
                if (before != null) UnityEngine.Object.DestroyImmediate(before);
                if (after != null) UnityEngine.Object.DestroyImmediate(after);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DatelineCrossingRemainsInLocalAircraftFrame()
        {
            var go = new GameObject("Terrain dateline test");
            try
            {
                Component geo = go.AddComponent(GeoType);
                Call(geo, "SetOrigin", 0d, 179.95d, 1000f);
                Vector3 across = (Vector3)Call(geo, "GeoToUnityPosition", 0d, -179.95d, 1000f);
                Assert.That(across.x, Is.InRange(11000f, 11200f));
                Assert.That(across.z, Is.Zero);
                Assert.That(across.y, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void VersionedDistantTileKeepsUnitsAndExactGeographicEdges()
        {
            object distant = Tile(330, -830); Set(distant, "schema_version", 2); Set(distant, "span", 4); Set(distant, "tile_degrees", .4d);
            Assert.That(Call(distant, "Validate", 330, -830, 33, null), Is.True);
            Assert.That(Call(distant, "Latitude", 32), Is.EqualTo(33.4d));
            Assert.That(Call(distant, "Longitude", 32), Is.EqualTo(-82.6d));
            Set(distant, "schema_version", 1);
            Assert.That(Call(distant, "Validate", 330, -830, 33, null), Is.False);
            Set(distant, "schema_version", 2); Set(distant, "span", 3);
            Assert.That(Call(distant, "Validate", 330, -830, 33, null), Is.False);
        }

        [Test]
        public void LodSkirtsHideCracksWithoutChangingAnySurfaceHeight()
        {
            var go = new GameObject("Terrain skirt test"); Mesh plain = null, render = null;
            try
            {
                var geo = go.AddComponent(GeoType); Call(geo, "SetOrigin", 33.7d, -82.9d, 1000f);
                object tile = Tile(); plain = (Mesh)Call(tile, "BuildMesh", geo);
                render = (Mesh)Call(tile, "BuildRenderMesh", geo, 200f);
                Assert.That(render.vertexCount, Is.EqualTo(1089 + 33 * 8));
                Assert.That(render.triangles.Length, Is.EqualTo(32 * 32 * 6 + 32 * 24));
                for (int i = 0; i < plain.vertexCount; i++) Assert.That(render.vertices[i], Is.EqualTo(plain.vertices[i]));
                Assert.That(render.vertices[1089].y - render.vertices[1090].y, Is.EqualTo(200f));
                Assert.That(render.normals[100].y, Is.GreaterThan(.99f));
                Assert.That(render.normals[1089].z, Is.LessThan(-.9f), "South skirt faces out, not inward");
            }
            finally
            {
                if (plain != null) UnityEngine.Object.DestroyImmediate(plain);
                if (render != null) UnityEngine.Object.DestroyImmediate(render);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [TestCase(337, -829)]
        [TestCase(-1, -1)]
        [TestCase(0, 1799)]
        [TestCase(847, -1799)]
        public void AdaptiveCoverageHasNoOverlapsAndNearDetailIsPreserved(int lat, int lon)
        {
            var selected = (Dictionary<Vector3Int, int>)LodType.GetMethod("Select").Invoke(null, new object[] { new Vector2Int(lat, lon), 2, 2 });
            var keys = selected.Keys.ToArray();
            Assert.That(keys.Length, Is.LessThan(400));
            Assert.That(selected.Sum(kv => kv.Value * kv.Value + kv.Value * 8), Is.LessThan(1200000));
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.That(keys[i].x, Is.GreaterThanOrEqualTo(-850));
                Assert.That(keys[i].x + keys[i].z, Is.LessThanOrEqualTo(850));
                for (int j = i + 1; j < keys.Length; j++)
                {
                    Assert.That(Contains(keys[i], keys[j]) || Contains(keys[j], keys[i]), Is.False, "LOD leaves overlap");
                }
            }
            int south = (int)Math.Floor(lat / 8d) * 8 - 16;
            int west = (int)Math.Floor((lon + 1800) / 8d) * 8 - 1800 - 16;
            for (int y = south; y < south + 40; y++)
            for (int x = west; x < west + 40; x++)
            {
                if (y < -850 || y >= 850) continue;
                int wrapped = (int)TileType.GetMethod("WrapLongitudeIndex").Invoke(null, new object[] { x });
                Assert.That(keys.Count(key => Contains(key, new Vector3Int(y, wrapped, 1))), Is.EqualTo(1), "Coverage gap");
            }
            var own = keys.Single(key => Contains(key, new Vector3Int(lat, lon, 1)));
            Assert.That(own.z, Is.EqualTo(1)); Assert.That(selected[own], Is.EqualTo(129));
            Assert.That(keys.Any(key => key.z == 8), Is.True);
        }

        private static bool Contains(Vector3Int outer, Vector3Int inner) =>
            (bool)LodType.GetMethod("Contains").Invoke(null, new object[] { outer, inner });

        [Test]
        public void ParentIsRetainedUntilEveryReplacementIsReady()
        {
            var parent = new Vector3Int(336, -832, 2);
            var children = new List<Vector3Int> { new Vector3Int(336, -832, 1), new Vector3Int(336, -831, 1),
                new Vector3Int(337, -832, 1), new Vector3Int(337, -831, 1) };
            var loaded = new List<Vector3Int>(children.Take(3)) { parent };
            var retire = LodType.GetMethod("CanRetire");
            Assert.That(retire.Invoke(null, new object[] { parent, children, loaded }), Is.False);
            loaded.Add(children[3]);
            Assert.That(retire.Invoke(null, new object[] { parent, children, loaded }), Is.True);
            Assert.That(retire.Invoke(null, new object[] { parent, new List<Vector3Int> { parent }, loaded }), Is.False);
            Assert.That(retire.Invoke(null, new object[] { children[0], new List<Vector3Int> { parent }, loaded }), Is.True);
        }
    }
}
