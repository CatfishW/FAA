using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization.Tests
{
    public class FaaRadarPresentationTests
    {
        private static Type Presentation => Type.GetType("FAA.Customization.FaaRadarPresentation, Assembly-CSharp");

        [Test]
        public void ContentOpacity_HasARealNativeCanvasGroup()
        {
            var go = new GameObject("Radar Content Test", typeof(RectTransform));
            try
            {
                var group = (CanvasGroup)Presentation.GetMethod("EnsureContentGroup").Invoke(null, new object[] { go });
                Assert.That(go.GetComponent<CanvasGroup>() != null, Is.True);
                group.alpha = 0f;
                Assert.That(go.GetComponent<CanvasGroup>().alpha, Is.Zero);
                Assert.That(Presentation.GetMethod("EnsureContentGroup").Invoke(null, new object[] { go }), Is.SameAs(group));
                Assert.That(go.GetComponents<CanvasGroup>().Length, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(220f, 220f)]
        [TestCase(360f, 360f)]
        [TestCase(560f, 560f)]
        [TestCase(360f, 400f)]
        public void CompactChart_FillsResizedCircularScope(float width, float height)
        {
            var type = Type.GetType("TrafficRadar.TrafficRadarDisplay, TrafficRadar");
            var result = (Vector2)type.GetMethod("CalculateCompactChartSize").Invoke(null, new object[] { new Vector2(width, height) });
            Assert.That(result, Is.EqualTo(Vector2.one * Mathf.Min(width, height)));
        }

        [TestCase(false, true, true, true, false, false, "DisplayOff")]
        [TestCase(false, true, false, false, false, false, "DisplayOff")]
        [TestCase(true, false, false, false, false, false, "Preview")]
        [TestCase(true, true, false, false, false, false, "Waiting")]
        [TestCase(true, true, false, true, false, false, "Stale")]
        [TestCase(true, true, false, true, true, true, "Stale")]
        [TestCase(true, true, true, true, true, false, "Standby")]
        [TestCase(true, true, true, true, false, true, "RadarOff")]
        [TestCase(true, true, true, true, false, false, "Live")]
        public void Status_DistinguishesVisibilityFreshnessAndPower(bool on, bool playing,
            bool fresh, bool previous, bool standby, bool powerOff, string expected)
        {
            Assert.That(Presentation.GetMethod("ResolveState").Invoke(null,
                new object[] { on, playing, fresh, previous, standby, powerOff }).ToString(), Is.EqualTo(expected));
        }

        [TestCase(160f, 1, 40f)]
        [TestCase(160f, 2, 80f)]
        [TestCase(160f, 3, 120f)]
        [TestCase(40f, 2, 20f)]
        [TestCase(5f, 1, 1.25f)]
        public void WeatherRanges_TrackSelectedRange(float range, int ring, float expected)
        {
            var type = Type.GetType("WeatherRadar.XPlaneWeatherRadarGeometry, WeatherRadar");
            Assert.That(type.GetMethod("RangeAtRing").Invoke(null, new object[] { range, ring, 4 }), Is.EqualTo(expected));
        }

        [Test]
        public void WeatherSector_EntireArcFitsTexture()
        {
            var type = Type.GetType("WeatherRadar.XPlaneWeatherRadarGeometry, WeatherRadar");
            float aspect = (float)type.GetField("Aspect").GetRawConstantValue();
            float radius = (float)type.GetField("Radius").GetRawConstantValue();
            float origin = (float)type.GetField("OriginHeight").GetRawConstantValue();
            float halfAngle = (float)type.GetField("HalfAngle").GetRawConstantValue();
            Assert.That(radius + origin, Is.LessThan(1f));
            Assert.That(Mathf.Sin(halfAngle * Mathf.Deg2Rad) * radius, Is.LessThan(aspect * .5f));
        }

        [TestCase(220f)]
        [TestCase(360f)]
        [TestCase(800f)]
        public void RadarChrome_OnlyPowerSwitchInterceptsClicksAndFooterDoesNotOverlap(float width)
        {
            var go = new GameObject("Radar Chrome Test", typeof(RectTransform));
            try
            {
                var rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(width, width);
                var presentation = go.AddComponent(Presentation);
                Presentation.GetMethod("SetDisplayEnabled").Invoke(presentation, new object[] { false });
                Assert.That(Presentation.GetProperty("IsDisplayOn").GetValue(presentation), Is.False);
                var button = go.GetComponentInChildren<Button>();
                Assert.That(button, Is.Not.Null);
                button.onClick.Invoke();
                Assert.That(Presentation.GetProperty("IsDisplayOn").GetValue(presentation), Is.True);
                foreach (var graphic in go.GetComponentsInChildren<Graphic>(true))
                    Assert.That(graphic.raycastTarget, Is.EqualTo(graphic == button.targetGraphic));
                var footer = rect.Find("Radar Status Footer");
                var range = footer.Find("Range") as RectTransform;
                var mode = footer.Find("Mode") as RectTransform;
                float footerWidth = ((RectTransform)footer).rect.width;
                Assert.That(range.rect.width + mode.rect.width + 8f, Is.LessThanOrEqualTo(footerWidth));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void WeatherFace_GeneratesCrispGeometryWithoutRasterText()
        {
            var go = new GameObject("Weather Face Test", typeof(RectTransform));
            try
            {
                var type = Type.GetType("WeatherRadar.XPlaneWeatherRadarFace, WeatherRadar");
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(320f, 226f);
                var face = go.AddComponent(type);
                Assert.That(go.GetComponent<CanvasRenderer>(), Is.Not.Null,
                    "A vector graphic needs a CanvasRenderer, not only generated vertices.");
                using var vertices = new VertexHelper();
                type.GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(face, new object[] { vertices });
                Assert.That(vertices.currentVertCount, Is.GreaterThan(1000));
                Assert.That(((Graphic)face).raycastTarget, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
