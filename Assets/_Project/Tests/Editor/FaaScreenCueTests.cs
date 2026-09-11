using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class FaaScreenCueTests
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static Type Runtime(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(Type type, object instance, string method, params object[] args) => type.GetMethod(method, Any).Invoke(instance, args);
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Any).SetValue(instance, value);
        private static T Get<T>(object instance, string name) => (T)instance.GetType().GetField(name, Any).GetValue(instance);

        [TestCase(1279f, "REL +1,300 FT")]
        [TestCase(-2301f, "REL -2,300 FT")]
        [TestCase(0f, "REL 0 FT")]
        [TestCase(float.NaN, "REL — FT")]
        [TestCase(float.PositiveInfinity, "REL — FT")]
        public void CueAltitude_IsExplicitSignedFeet(float feet, string expected) =>
            Assert.That(Call(Runtime("IndicatorSystem.Display.PilotIndicatorCue"), null, "FormatRelativeAltitude", feet), Is.EqualTo(expected));

        [TestCase(0f)]
        [TestCase(30f)]
        [TestCase(-30f)]
        public void WeatherFan_InversePreservesBearingAndRange(float bearing)
        {
            const float aspect = 724f / 512f;
            float angle = bearing * Mathf.Deg2Rad;
            var point = new Vector2(.5f + .84f * .5f * Mathf.Sin(angle) / aspect, .09f + .84f * .5f * Mathf.Cos(angle));
            object[] args = { point, aspect, 0f, 0f };
            Assert.That(Call(Runtime("IndicatorSystem.Integration.WeatherIndicatorBridge"), null, "TryGetFanCoordinates", args), Is.True);
            Assert.That((float)args[2], Is.EqualTo(.5f).Within(.001));
            Assert.That((float)args[3], Is.EqualTo(bearing).Within(.001));
        }

        [TestCase(.5f, 0f)]
        [TestCase(0f, .1f)]
        [TestCase(.5f, 1f)]
        public void WeatherFan_RejectsOutsideSectorAndOwnship(float x, float y)
        {
            object[] args = { new Vector2(x, y), 724f / 512f, 0f, 0f };
            Assert.That(Call(Runtime("IndicatorSystem.Integration.WeatherIndicatorBridge"), null, "TryGetFanCoordinates", args), Is.False);
        }

        [TestCase(0f, 0f, 100f, "OnScreen", 700f, 400f)]
        [TestCase(1000f, 0f, 100f, "OffScreen", 1150f, 400f)]
        [TestCase(0f, 0f, -100f, "Behind", 700f, 150f)]
        public void Projection_UsesCameraViewportAndKeepsAftCueAtEdge(float x, float y, float z, string visibility, float sx, float sy)
        {
            var go = new GameObject("Cue projection test", typeof(Camera));
            try
            {
                var camera = go.GetComponent<Camera>();
                camera.pixelRect = new Rect(200, 100, 1000, 600);
                var target = Activator.CreateInstance(Runtime("IndicatorSystem.Integration.TrafficIndicatorTarget"));
                Set(target, "id", "TEST"); Set(target, "worldPosition", new Vector3(x, y, z)); Set(target, "distanceNM", 1f);
                var config = Activator.CreateInstance(Runtime("IndicatorSystem.Core.IndicatorEdgeConfig"));
                Set(config, "EdgePadding", 50f); Set(config, "MaxDisplayDistance", 80f);
                var data = Call(Runtime("IndicatorSystem.Core.ScreenIndicatorCalculator"), null, "CalculateIndicator", target, camera, config);
                Assert.That(Get<object>(data, "Visibility").ToString(), Is.EqualTo(visibility));
                Assert.That(Get<Vector2>(data, "ScreenPosition").x, Is.EqualTo(sx).Within(.1));
                Assert.That(Get<Vector2>(data, "ScreenPosition").y, Is.EqualTo(sy).Within(.1));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void AbsoluteBearing_IsIndependentOfHeadOrientation()
        {
            var point = (Vector3)Call(Runtime("IndicatorSystem.Core.ScreenIndicatorCalculator"), null,
                "RadarBearingToWorldPosition", 1f, 90f, 1000f, new Vector3(10, 20, 30));
            Assert.That(point.x, Is.EqualTo(1862).Within(.01));
            Assert.That(point.y, Is.EqualTo(324.8f).Within(.01));
            Assert.That(point.z, Is.EqualTo(30).Within(.01));
        }

        [Test]
        public void GenericPool_RecyclesAcrossMoreThanItsMaximumLifetimeTargets()
        {
            var go = new GameObject("Cue pool test", typeof(RectTransform));
            var settings = ScriptableObject.CreateInstance(Runtime("IndicatorSystem.Core.IndicatorSettings"));
            try
            {
                Set(settings, "useCustomPrefabs", false);
                var type = Runtime("IndicatorSystem.Display.IndicatorPool");
                var pool = go.AddComponent(type);
                Call(type, pool, "Initialize", settings, go.GetComponent<RectTransform>());
                var get = type.GetMethod("GetIndicator", new[] { typeof(string) });
                for (int i = 0; i < 300; i++)
                {
                    string id = "target-" + i;
                    Assert.That(get.Invoke(pool, new object[] { id }), Is.Not.Null, "Pool lost reusable instances at target " + i);
                    Call(type, pool, "ReleaseIndicator", id);
                }
                Assert.That(type.GetProperty("TotalCreated").GetValue(pool), Is.EqualTo(10));
                Assert.That(type.GetProperty("ActiveCount").GetValue(pool), Is.EqualTo(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void MarkerRange_DoesNotInheritOrChangeRadarZoom()
        {
            var go = new GameObject("Independent range test"); go.SetActive(false);
            try
            {
                var type = Type.GetType("TrafficRadar.Core.TrafficRadarController, TrafficRadar", true);
                var controller = go.AddComponent(type);
                type.GetProperty("RangeNM").SetValue(controller, 5f);
                var stateType = Type.GetType("TrafficRadar.Core.AircraftState, TrafficRadar", true);
                var state = Activator.CreateInstance(stateType);
                Set(state, "Icao24", "INDEPENDENT"); Set(state, "Latitude", 35.2); Set(state, "Longitude", -80.0);
                Set(state, "LastUpdate", DateTime.UtcNow);
                var cache = Get<IList>(controller, "_cachedAircraftStates"); cache.Add(state);
                Call(type, controller, "SetOwnPosition", 35.0, -80.0, 2000f, 0f);
                var targets = (IList)Call(type, controller, "GetIndicatorTargets", 80f);
                Assert.That(targets.Count, Is.EqualTo(1));
                Assert.That(type.GetProperty("TargetCount").GetValue(controller), Is.EqualTo(0));
                Assert.That(type.GetProperty("RangeNM").GetValue(controller), Is.EqualTo(5f));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(0f, .8f)]
        [TestCase(90f, .8f)]
        [TestCase(180f, .8f)]
        [TestCase(270f, .8f)]
        [TestCase(0f, 0f)]
        public void SimulatedRain_PaintsReturnsButCloudAloneDoesNot(float heading, float rain)
        {
            var bridge = Runtime("FAA.XPlaneIntegration.Runtime.XPlane12ApiHudBridge");
            var metrics = Activator.CreateInstance(bridge.GetNestedType("StreamWeatherMetrics", BindingFlags.NonPublic));
            Set(metrics, "Precipitation", rain); Set(metrics, "CloudCoverage", 1f); Set(metrics, "Intensity", .9f);
            var data = Activator.CreateInstance(Runtime("AviationUI.AviationFlightData")); Set(data, "heading", heading);
            const int width = 512, height = 362;
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(8, 22, 31, 200);
            int originX = width / 2, originY = Mathf.RoundToInt(height * .09f);
            Call(bridge, null, "DrawModernWeatherReturns", pixels, width, height, originX, originY, height * .84f, 55f, data, metrics);
            int returns = 0;
            foreach (var pixel in pixels) if (pixel.g > 60 && pixel.g > pixel.r * 1.35f && pixel.b < pixel.g * .65f) returns++;
            if (rain > 0) Assert.That(returns, Is.GreaterThan(20), "Rain must be visible, not just a live-data status");
            else Assert.That(returns, Is.Zero, "Dry overcast must not fabricate precipitation");
        }
    }
}
