using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class FaaChartZoomTests
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static Type Provider => Type.GetType("TrafficRadar.FAASectionalChartProvider, TrafficRadar", true);
        private static Type Display => Type.GetType("TrafficRadar.TrafficRadarDisplay, TrafficRadar", true);
        private static Rect Uv(float latitude, float longitude, float range, int zoom, int x, int y) =>
            (Rect)Provider.GetMethod("CalculateChartUvRect").Invoke(null, new object[] { latitude, longitude, range, zoom, x, y });

        [Test]
        public void RangeWithinSameTileLevel_ContinuouslyZoomsAroundSameOwnship()
        {
            var wide = Uv(34.2f, -77.8f, 20, 9, 145, 204);
            var close = Uv(34.2f, -77.8f, 13.333333f, 9, 145, 204);
            Assert.That(close.width, Is.EqualTo(wide.width * 2f / 3f).Within(.0001));
            Assert.That(close.center.x, Is.EqualTo(wide.center.x).Within(.0001));
            Assert.That(close.center.y, Is.EqualTo(wide.center.y).Within(.0001));
        }

        [Test]
        public void OwnshipOffsetWithinTile_IsNotForcedToMosaicCentre()
        {
            var center = Uv(0, 0, 10, 9, 256, 256);
            Assert.That(center.center.x, Is.EqualTo(1f / 3f).Within(.0001));
            Assert.That(center.center.y, Is.EqualTo(2f / 3f).Within(.0001));
            var northeast = Uv(.1f, .1f, 10, 9, 256, 256);
            Assert.That(northeast.center.x, Is.GreaterThan(center.center.x));
            Assert.That(northeast.center.y, Is.GreaterThan(center.center.y));
        }

        [Test]
        public void DatelineWrap_UsesAdjacentTileNotWholeWorldJump()
        {
            var before = Uv(20, 179.999f, 5, 9, 511, 226);
            var after = Uv(20, -179.999f, 5, 9, 511, 226);
            Assert.That(Mathf.Abs(before.center.x - after.center.x), Is.LessThan(.002f));
        }

        [Test]
        public void FullscreenPanMargin_DoesNotChangeVisibleGeographicScale()
        {
            var normal = Uv(34, -78, 10, 10, 290, 409);
            var expanded = Uv(34, -78, 24, 10, 290, 409);
            Assert.That(expanded.width / 2.4f, Is.EqualTo(normal.width).Within(.0001));
        }

        [Test]
        public void NativeTileLevelChanges_PreserveMetersPerScreenUnit()
        {
            var low = Uv(34, -78, 5, 10, 290, 409);
            var high = Uv(34, -78, 5, 11, 580, 819);
            Assert.That(high.width, Is.EqualTo(low.width * 2f).Within(.0001));
        }

        [Test]
        public void NorthTileRow_IsCompositedAboveCentre_NotMirroredSouth()
        {
            var go = new GameObject("Chart row test"); go.SetActive(false);
            var north = new Texture2D(2, 2); var composite = new Texture2D(9, 9);
            try
            {
                var provider = go.AddComponent(Provider);
                Provider.GetField("currentCompositeTexture", Any).SetValue(provider, composite);
                Provider.GetField("compositeSize", Any).SetValue(provider, 9);
                north.SetPixels(new[] {Color.red, Color.red, Color.red, Color.red}); north.Apply();
                Provider.GetMethod("CompositeTiles", Any).Invoke(provider, new object[] {
                    new List<Texture2D> { north }, new List<(int, int)> { (0, -1) } });
                Assert.That(composite.GetPixel(4, 7).r, Is.GreaterThan(.95f));
                Assert.That(composite.GetPixel(4, 1).r, Is.LessThan(.5f));
                Provider.GetField("currentCompositeTexture", Any).SetValue(provider, null);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(north); UnityEngine.Object.DestroyImmediate(composite); }
        }

        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(90f)]
        [TestCase(135f)]
        [TestCase(180f)]
        [TestCase(270f)]
        [TestCase(359f)]
        public void TrackUp_ChartNorthAndTicksAgreeWithCompassAndForwardBearing(float heading)
        {
            var go = new GameObject("Map rotation test", typeof(RectTransform)); go.SetActive(false);
            try
            {
                var display = go.AddComponent(Display);
                var chart = new GameObject("Chart", typeof(RectTransform), typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
                chart.transform.SetParent(go.transform, false);
                var ticks = new GameObject("Ticks", typeof(RectTransform)).GetComponent<RectTransform>();
                ticks.SetParent(go.transform, false);
                var labels = new RectTransform[4];
                for (int i = 0; i < 4; i++)
                {
                    labels[i] = new GameObject("Compass " + i, typeof(RectTransform)).GetComponent<RectTransform>();
                    labels[i].SetParent(go.transform, false);
                }
                Display.GetField("chartBackgroundImage", Any).SetValue(display, chart);
                Display.GetField("compassTicksContainer", Any).SetValue(display, ticks);
                Display.GetField("_compassLabelRects", Any).SetValue(display, labels);
                Display.GetField("_currentHeadingRotation", Any).SetValue(display, -heading);
                Display.GetMethod("ApplyHeadingPresentation", Any).Invoke(display, null);
                var northOnChart = chart.rectTransform.localRotation * Vector3.up;
                Assert.That(Vector2.Dot(northOnChart, labels[0].anchoredPosition.normalized), Is.EqualTo(1f).Within(.0001));
                Assert.That(Vector3.Dot(northOnChart, ticks.localRotation * Vector3.up), Is.EqualTo(1f).Within(.0001));
                Vector3 ahead = new Vector3(Mathf.Sin(heading * Mathf.Deg2Rad), Mathf.Cos(heading * Mathf.Deg2Rad), 0);
                Assert.That(Vector3.Dot(chart.rectTransform.localRotation * ahead, Vector3.up), Is.EqualTo(1f).Within(.0001));
                Display.GetMethod("ResetHeadingPresentation", Any).Invoke(display, null);
                Assert.That(Vector3.Dot(chart.rectTransform.localRotation * Vector3.up, Vector3.up), Is.EqualTo(1f).Within(.0001));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(180f)]
        [TestCase(270f)]
        public void ForwardAircraftMotion_MovesGroundBehindOwnship_NotAhead(float heading)
        {
            const float lat = 34f, lon = -78f;
            float radians = heading * Mathf.Deg2Rad;
            var before = Uv(lat, lon, 10, 10, 290, 409);
            var after = Uv(lat + .01f * Mathf.Cos(radians), lon + .01f * Mathf.Sin(radians) / Mathf.Cos(lat * Mathf.Deg2Rad), 10, 10, 290, 409);
            Vector2 groundMotion = before.center - after.center;
            Vector3 screenMotion = Quaternion.Euler(0, 0, heading) * groundMotion;
            Assert.That(screenMotion.y, Is.LessThan(0));
            Assert.That(Mathf.Abs(screenMotion.x), Is.LessThan(Mathf.Abs(screenMotion.y) * .005f));
        }

        [TestCase(1f)]
        [TestCase(2f)]
        public void ChartDrag_FollowsPointerInScaledCanvasUnits(float scale)
        {
            var canvasGo = new GameObject("Map drag canvas", typeof(RectTransform), typeof(Canvas));
            var go = new GameObject("Map drag test", typeof(RectTransform)); go.SetActive(false);
            go.transform.SetParent(canvasGo.transform, false);
            try
            {
                canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(400, 400); rect.localScale = Vector3.one * scale;
                var display = go.AddComponent(Display);
                Display.GetField("rectTransform", Any).SetValue(display, rect);
                Display.GetField("enableMapPanning", Any).SetValue(display, true);
                var surfaceType = Type.GetType("FAA.Customization.FaaRadarInteractionSurface, Assembly-CSharp", true);
                var surface = go.AddComponent(surfaceType);
                surfaceType.GetField("_trafficDisplay", Any).SetValue(surface, display);
                surfaceType.GetField("_interactionEnabled", Any).SetValue(surface, true);
                surfaceType.GetField("_dragging", Any).SetValue(surface, true);
                Canvas.ForceUpdateCanvases();
                var origin = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(Vector3.zero));
                var pointer = new UnityEngine.EventSystems.PointerEventData(null) { position = origin + new Vector2(40, 20), delta = new Vector2(40, 20) };
                surfaceType.GetMethod("OnDrag").Invoke(surface, new object[] { pointer });
                var pan = (Vector2)Display.GetProperty("MapPan").GetValue(display);
                Assert.That(pan.x, Is.EqualTo(40f / scale).Within(.01));
                Assert.That(pan.y, Is.EqualTo(20f / scale).Within(.01));
            }
            finally { UnityEngine.Object.DestroyImmediate(canvasGo); }
        }
    }
}
