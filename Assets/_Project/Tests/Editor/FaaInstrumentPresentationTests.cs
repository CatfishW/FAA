using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class FaaInstrumentPresentationTests
    {
        private static Type Engine => Type.GetType("FAA.Customization.FaaEngineInstrumentGraphic, Assembly-CSharp");
        private static Type Traffic => Type.GetType("TrafficRadar.RadarTrafficOverlay, TrafficRadar");
        private static object Call(Type type, string method, params object[] args) => type.GetMethod(method).Invoke(null, args);

        [TestCase(1280f, "+13")]
        [TestCase(-1280f, "−13")]
        [TestCase(0f, "+00")]
        [TestCase(-20f, "−00")]
        [TestCase(10000f, "+100")]
        [TestCase(float.NaN, "—")]
        [TestCase(float.PositiveInfinity, "—")]
        public void RelativeAltitude_IsSignedHundredsOfFeet(float value, string expected) =>
            Assert.That(Call(Traffic, "FormatRelativeAltitude", value), Is.EqualTo(expected));

        [TestCase(499f, 0)]
        [TestCase(-499f, 0)]
        [TestCase(500f, 1)]
        [TestCase(-500f, -1)]
        [TestCase(float.NaN, 0)]
        public void TrafficTrend_HasExplicit500FpmThreshold(float value, int expected) =>
            Assert.That(Call(Traffic, "VerticalTrend", value), Is.EqualTo(expected));

        [TestCase(3f, 4f, true)]
        [TestCase(14f, 2f, false)]
        [TestCase(0f, 16f, false)]
        [TestCase(float.NaN, 0f, false)]
        public void StaleTraffic_IsNeverPresentedAsLive(float sampleAge, float elapsed, bool expected) =>
            Assert.That(Call(Traffic, "IsFresh", sampleAge, elapsed), Is.EqualTo(expected));

        [Test]
        public void TrafficMotion_IsFrameRateIndependentAndNeverOvershoots()
        {
            var start = Vector2.zero; var target = new Vector2(10, 20);
            var oneStep = (Vector2)Call(Traffic, "SmoothPosition", start, target, .1f);
            var twoSteps = (Vector2)Call(Traffic, "SmoothPosition", start, target, .05f);
            twoSteps = (Vector2)Call(Traffic, "SmoothPosition", twoSteps, target, .05f);
            Assert.That(Vector2.Distance(oneStep, twoSteps), Is.LessThan(.001));
            Assert.That(oneStep.magnitude, Is.LessThan(target.magnitude));
            Assert.That(oneStep.magnitude, Is.GreaterThan(0));
        }

        [Test]
        public void TagBounds_RespectTheWholeCircularFootprint()
        {
            Assert.That(Call(Traffic, "InsideScope", new Rect(-20, -10, 40, 20), 100f), Is.True);
            Assert.That(Call(Traffic, "InsideScope", new Rect(70, 70, 20, 20), 100f), Is.False);
        }

        [Test]
        public void TrafficAge_UsesTheSameTimeBasisForLocalAndUtcSources()
        {
            var type = Type.GetType("TrafficRadar.Core.RadarDataProcessor, TrafficRadar");
            var now = new DateTime(2026, 9, 7, 23, 0, 0, DateTimeKind.Utc);
            var sample = now.AddSeconds(-45);
            Assert.That(Call(type, "CalculateSampleAgeSeconds", sample, now), Is.EqualTo(45f));
            Assert.That(Call(type, "CalculateSampleAgeSeconds", sample.ToLocalTime(), now), Is.EqualTo(45f));
            Assert.That(Call(type, "CalculateSampleAgeSeconds", now.AddSeconds(1), now), Is.EqualTo(0f));
        }

        [TestCase(1920f, 1080f, 818f)]
        [TestCase(1280f, 720f, 458f)]
        [TestCase(2560f, 1080f, 818f)]
        public void FullMap_ReservesTheFinalToolbarOnceAboveTheMap(float width, float height, float expectedDiameter)
        {
            var type = Type.GetType("TrafficRadar.TrafficRadarDisplay, TrafficRadar");
            var rect = (Rect)Call(type, "CalculateFocusMapRect", new Vector2(width, height), 42f, 178f);
            Assert.That(rect.width, Is.EqualTo(expectedDiameter));
            Assert.That(rect.height, Is.EqualTo(expectedDiameter));
            Assert.That(rect.yMin, Is.EqualTo(-height * .5f + 42f));
            Assert.That(rect.yMax + 178, Is.LessThanOrEqualTo(height * .5f - 42f));
        }

        [TestCase("FAA.Customization.FaaEngineInstrumentGraphic, Assembly-CSharp")]
        [TestCase("TrafficRadar.RadarTrafficOverlay, TrafficRadar")]
        public void VectorLayers_RequireARealCanvasRenderer(string typeName)
        {
            var go = new GameObject("Vector renderer regression", typeof(RectTransform));
            go.SetActive(false);
            try
            {
                go.AddComponent(Type.GetType(typeName));
                Assert.That(go.GetComponent<CanvasRenderer>(), Is.Not.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(true, true, 0f, true)]
        [TestCase(false, true, 89f, false)]
        [TestCase(true, false, 89f, false)]
        [TestCase(true, true, float.NaN, false)]
        public void EngineValues_RequireBothFreshFeedAndFieldValidity(bool healthy, bool valid, float value, bool expected) =>
            Assert.That(Call(Engine, "IsUsable", healthy, valid, value), Is.EqualTo(expected));

        [TestCase(89.24f, true, "89.2")]
        [TestCase(0f, true, "0.0")]
        [TestCase(0f, false, "—")]
        public void EngineDigits_DoNotTurnMissingDataIntoZero(float value, bool valid, string expected) =>
            Assert.That(Call(Engine, "FormatPercent", value, valid), Is.EqualTo(expected));

        [TestCase(-126f, true, "-130")]
        [TestCase(0f, true, "0")]
        [TestCase(1234f, true, "+1230")]
        [TestCase(3200f, true, "+3200")]
        [TestCase(0f, false, "—")]
        public void VerticalSpeed_IsSignedAndDoesNotClampTheNumericValue(float value, bool valid, string expected) =>
            Assert.That(Call(Engine, "FormatVerticalSpeed", value, valid), Is.EqualTo(expected));

        [TestCase(2000f, 84f)]
        [TestCase(-2000f, -84f)]
        [TestCase(4000f, 84f)]
        [TestCase(0f, 0f)]
        public void VerticalSpeedScale_IsLinearWithExplicitOffScaleGeometry(float value, float expected) =>
            Assert.That(Call(Engine, "VerticalSpeedPosition", value), Is.EqualTo(expected));

        [Test]
        public void SvgIcons_AreBakedVectorGeometryCenteredInACommon24PixelViewBox()
        {
            var type = Type.GetType("FAA.Customization.FaaSvgIconLibrary, Assembly-CSharp");
            var library = Resources.Load("HudIcons/FaaRadarIconLibrary", type);
            Assert.That(library, Is.Not.Null);
            var entries = (Array)type.GetField("entries").GetValue(library);
            Assert.That(entries.Length, Is.EqualTo(7));
            foreach (var entry in entries)
            {
                var vertices = (Vector2[])entry.GetType().GetField("vertices").GetValue(entry);
                var triangles = (int[])entry.GetType().GetField("triangles").GetValue(entry);
                Assert.That(vertices.Length, Is.GreaterThan(20));
                Assert.That(triangles.Length % 3, Is.Zero);
                Assert.That(triangles.Length, Is.GreaterThan(0));
                foreach (var v in vertices)
                {
                    Assert.That(v.x, Is.InRange(-12f, 12f));
                    Assert.That(v.y, Is.InRange(-12f, 12f));
                }
                foreach (int index in triangles) Assert.That(index, Is.InRange(0, vertices.Length - 1));
            }
        }
    }
}
