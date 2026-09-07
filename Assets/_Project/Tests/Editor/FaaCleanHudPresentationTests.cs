using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization.Tests
{
    public class FaaCleanHudPresentationTests
    {
        private static Type Runtime(string name) => Type.GetType(name + ", Assembly-CSharp", true);

        [TestCase(-247f)]
        [TestCase(0f)]
        [TestCase(247f)]
        public void HeadingTargetCaptionStaysInsideTheTape(float cueX)
        {
            float offset = (float)Runtime("FAA.Customization.FaaHeadingTapeOverlay")
                .GetMethod("GetNavigationLabelOffset").Invoke(null, new object[] { cueX, 260f, 75f });
            Assert.That(Mathf.Abs(cueX + offset) + 75f, Is.LessThanOrEqualTo(260f));
        }

        [Test]
        public void TrafficMenu_CloseRequestIsImmediateEvenWhileItFades()
        {
            GameObject root = new GameObject("Traffic menu state", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                Type type = Runtime("FAA.Customization.TrafficRadarContextMenu");
                Component menu = root.AddComponent(type);
                type.GetField("_targetOpen", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu, true);
                type.GetField("_progress", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu, 1f);
                type.GetMethod("Close").Invoke(menu, new object[] { false });
                Assert.That(type.GetProperty("IsRequestedOpen").GetValue(menu), Is.False);
                Assert.That(type.GetProperty("IsOpen").GetValue(menu), Is.True,
                    "The fade can continue without keeping the companion bar open.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase("GetNavigationDeviation")]
        [TestCase("GetGlideslopeDeviation")]
        public void MissingDeviationDataDoesNotBecomeAnOnCourseZero(string methodName)
        {
            Type bridge = Runtime("FAA.XPlaneIntegration.Runtime.XPlane12ApiHudBridge");
            float result = (float)bridge.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { new System.Collections.Generic.Dictionary<string, float>(), float.NaN });
            Assert.That(float.IsNaN(result), Is.True);
        }

        [TestCase("LocalizerElement")]
        [TestCase("GlidescopeElement")]
        public void NavigationElement_InvalidOrStaleDataClearsGuidance(string elementName)
        {
            GameObject root = new GameObject("Guidance validity");
            root.SetActive(false);
            try
            {
                Type type = Runtime("HUDControl.Elements." + elementName);
                Component element = root.AddComponent(type);
                type.GetMethod("SetDeviation").Invoke(element, new object[] { 0f });
                Assert.That(type.GetProperty("HasDeviationData").GetValue(element), Is.True);
                type.GetMethod("SetDeviation").Invoke(element, new object[] { float.NaN });
                Assert.That(type.GetProperty("HasDeviationData").GetValue(element), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void RadarDrawer_HidesTheEntireBarIncludingItsPrimaryRow()
        {
            var owner = new GameObject("Inactive radar owner");
            owner.SetActive(false); // Don't bind to real scene radars during tests.
            var strip = new GameObject("Control strip", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                Type ownerType = Runtime("FAA.Customization.FaaRadarControlsOverlay");
                Component overlay = owner.AddComponent(ownerType);
                var primary = new GameObject("Primary", typeof(RectTransform), typeof(Image));
                primary.transform.SetParent(strip.transform);
                var secondary = new GameObject("Secondary", typeof(RectTransform));
                secondary.transform.SetParent(strip.transform);
                object drawer = ownerType.GetMethod("EnsureDrawer", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(overlay, new object[] { strip.GetComponent<RectTransform>() });
                CanvasGroup group = strip.GetComponent<CanvasGroup>();
                Assert.That(group.alpha, Is.Zero, "The primary bar must not remain visible at startup.");
                Assert.That(group.blocksRaycasts, Is.False);
                MethodInfo setVisible = drawer.GetType().GetMethod("SetVisible");
                setVisible.Invoke(drawer, new object[] { true, true });
                Assert.That(group.alpha, Is.EqualTo(1f));
                Assert.That(group.interactable, Is.True);
                setVisible.Invoke(drawer, new object[] { false, true });
                Assert.That(group.alpha, Is.Zero);
                Assert.That(group.interactable, Is.False);
                Assert.That(group.blocksRaycasts, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(strip);
            }
        }

        [TestCase(3840f, 2160f)]
        [TestCase(1920f, 1080f)]
        [TestCase(1280f, 720f)]
        [TestCase(640f, 480f)]
        public void WheelAndCommandColumn_FitInsideTheViewport(float width, float height)
        {
            Type menu = Runtime("VoiceControl.UI.UIToolkitRadialMenuAdvanced");
            float scale = (float)menu.GetMethod("CalculateMenuScale").Invoke(null, new object[] { width, height, 820f, 560f });
            Assert.That(scale * 820f, Is.LessThanOrEqualTo(width - 40f + .1f));
            Assert.That(scale * 560f, Is.LessThanOrEqualTo(height - 72f + .1f));
        }

        [Test]
        public void NavigationScale_WithoutTargetNeverShowsAnOnCourseDiamond()
        {
            GameObject root = new GameObject("Navigation scale", typeof(RectTransform));
            try
            {
                Type type = Runtime("FAA.Customization.FaaNavigationScaleGraphic");
                Component scale = root.AddComponent(type);
                type.GetMethod("Configure").Invoke(scale, new object[] { false, null, null, null });
                Assert.That((bool)type.GetField("hasGuidance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scale), Is.False);
                Transform detail = root.transform.Find("Detail");
                Graphic text = detail.GetComponent<Graphic>();
                Assert.That(text.GetType().GetProperty("text").GetValue(text), Is.EqualTo("NO TARGET"));
                Assert.That(((Graphic)scale).raycastTarget, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void NavigationScale_MapOnlyVerticalModeIsNotLabeledGlideslope()
        {
            GameObject root = new GameObject("Vertical map scale", typeof(RectTransform));
            try
            {
                Type type = Runtime("FAA.Customization.FaaNavigationScaleGraphic");
                Component scale = root.AddComponent(type);
                type.GetMethod("Configure").Invoke(scale, new object[] { true, null, null, null });
                Graphic text = root.transform.Find("Mode").GetComponent<Graphic>();
                Assert.That(text.GetType().GetProperty("text").GetValue(text), Is.EqualTo("ALONG TRACK"));
                Graphic ahead = root.transform.Find("Positive").GetComponent<Graphic>();
                Assert.That(ahead.GetType().GetProperty("text").GetValue(ahead), Is.EqualTo("AHEAD"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
