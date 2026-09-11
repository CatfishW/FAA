using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class FaaViewAlignmentTests
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static Type Runtime(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(Type t, object o, string method, params object[] args) => t.GetMethod(method, Any).Invoke(o, args);
        private static void Set(object o, string name, object value) => o.GetType().GetField(name, Any).SetValue(o, value);
        private static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Any).GetValue(o);
        private static Type View => Type.GetType("AircraftControl.Camera.AircraftCameraController, AircraftControl", true);
        private static Type Projection => Runtime("IndicatorSystem.Core.ScreenIndicatorCalculator");
        private static Type Display => Type.GetType("TrafficRadar.TrafficRadarDisplay, TrafficRadar", true);

        [TestCase(0f, 1f)]
        [TestCase(.4f, .5f)]
        [TestCase(.8f, 0f)]
        [TestCase(4f, 0f)]
        public void LookReturn_IsTimedAndEndsExactlyForward(float elapsed, float fraction)
        {
            Vector2 offset = (Vector2)Call(View, null, "ReturnLookOffset", new Vector2(-30, 140), elapsed, .8f);
            Assert.That(Vector2.Distance(offset, new Vector2(-30, 140) * fraction), Is.LessThan(.001f));
        }

        [TestCase(0, true, true, false)]
        [TestCase(1, true, true, false)]
        [TestCase(2, true, true, true)]
        [TestCase(2, false, true, false)]
        [TestCase(2, true, false, false)]
        public void CameraOwnership_OnlyReplacesDesktopSimulatorPose(int mode, bool pointer, bool controller, bool expected)
        {
            var xr = Runtime("FAA.Headset.XR3HeadsetCompatibility");
            var value = Enum.ToObject(xr.GetNestedType("ActivationMode"), mode);
            Assert.That(Call(xr, null, "UseDesktopAircraftView", value, pointer, controller), Is.EqualTo(expected));
        }

        [Test]
        public void UiDrag_DoesNotBecomeLookWhenPointerLeavesPanel()
        {
            var go = new GameObject("View input test");
            try
            {
                var view = go.AddComponent(View);
                Call(View, view, "ProcessLookInput", true, new Vector2(30, 10), true, .016f);
                Call(View, view, "ProcessLookInput", true, new Vector2(30, 10), false, .016f);
                Assert.That(Get<float>(view, "_currentYaw"), Is.Zero);
                Call(View, view, "ProcessLookInput", false, Vector2.zero, false, .016f);
                Call(View, view, "ProcessLookInput", true, new Vector2(30, 10), false, .016f);
                Assert.That(Get<float>(view, "_currentYaw"), Is.EqualTo(90f));
                Call(View, view, "ProcessLookInput", false, Vector2.zero, false, .8f);
                Assert.That(Get<float>(view, "_currentYaw"), Is.Zero);
                Assert.That(Get<float>(view, "_currentPitch"), Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Return_FollowsCurrentAircraftHeading_NotInitialWorldHeading()
        {
            var go = new GameObject("View turn test");
            var own = new GameObject("Test ownship");
            try
            {
                var view = go.AddComponent(View);
                Call(View, view, "SetTarget", own.transform);
                own.transform.rotation = Quaternion.Euler(0, 350, 0);
                Call(View, view, "ProcessLookInput", true, new Vector2(30, 10), false, .016f);
                own.transform.rotation = Quaternion.Euler(0, 10, 0);
                Call(View, view, "ProcessLookInput", false, Vector2.zero, false, .8f);
                Call(View, view, "UpdateCockpitCamera");
                Assert.That(Quaternion.Angle(go.transform.rotation, own.transform.rotation), Is.LessThan(.001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(own); }
        }

        [TestCase(0f, 0f)]
        [TestCase(90f, 0f)]
        [TestCase(180f, 0f)]
        [TestCase(270f, 0f)]
        [TestCase(85f, -35f)]
        [TestCase(350f, 25f)]
        [TestCase(20f, 160f)]
        public void MapAndScreen_ShareBearingInEveryQuadrant(float heading, float relativeBearing)
        {
            var go = new GameObject("Map and view projection", typeof(Camera));
            try
            {
                var camera = go.GetComponent<Camera>();
                camera.pixelRect = new Rect(100, 40, 1200, 800);
                camera.fieldOfView = 60;
                camera.aspect = 1.5f;
                camera.transform.rotation = Quaternion.Euler(0, heading, 0);
                float bearing = heading + relativeBearing;
                Vector2 map = (Vector2)Call(Display, null, "CalculateTargetDisplayPosition", 10f, bearing, 20f, heading, true);
                Assert.That(Mathf.DeltaAngle(relativeBearing, Mathf.Atan2(map.x, map.y) * Mathf.Rad2Deg), Is.EqualTo(0f).Within(.001));
                Vector3 world = (Vector3)Call(Projection, null, "RadarBearingToWorldPosition", 10f, bearing, 1000f, Vector3.zero);
                var target = Activator.CreateInstance(Runtime("IndicatorSystem.Integration.TrafficIndicatorTarget"));
                Set(target, "id", "SAME-ID"); Set(target, "worldPosition", world); Set(target, "distanceNM", 10f);
                var config = Activator.CreateInstance(Runtime("IndicatorSystem.Core.IndicatorEdgeConfig"));
                Set(config, "EdgePadding", 50f); Set(config, "MaxDisplayDistance", 80f);
                var data = Call(Projection, null, "CalculateIndicator", target, camera, config);
                Vector2 screen = Get<Vector2>(data, "ScreenPosition");
                if (Mathf.Abs(relativeBearing) > .1f)
                    Assert.That(Mathf.Sign(screen.x - 700), Is.EqualTo(Mathf.Sign(map.x)), "Map and screen disagree left/right");
                Assert.That(Get<object>(data, "Visibility").ToString(), Is.EqualTo(Mathf.Abs(relativeBearing) > 90 ? "Behind" : "OnScreen"));
                Assert.That(screen.y, Is.GreaterThan(440), "Positive relative altitude must project above the level view");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(250f)]
        public void NorthUp_DoesNotRotateTargetsWithAircraft(float heading)
        {
            var north = (Vector2)Call(Display, null, "CalculateTargetDisplayPosition", 10f, 0f, 20f, heading, false);
            Assert.That(Vector2.Distance(north, new Vector2(0, .5f)), Is.LessThan(.0001f));
            var close = (Vector2)Call(Display, null, "CalculateTargetDisplayPosition", 10f, 0f, 10f, heading, false);
            Assert.That(Vector2.Distance(close, north * 2), Is.LessThan(.0001f));
        }

        [Test]
        public void OwnshipTranslation_UpdatesCueOriginWithoutRotatingBearing()
        {
            var own = new GameObject("Cue origin test");
            try
            {
                var target = Activator.CreateInstance(Runtime("IndicatorSystem.Integration.TrafficIndicatorTarget"));
                Set(target, "projectionOrigin", own.transform);
                Set(target, "worldOffset", new Vector3(1852, 304.8f, 0));
                own.transform.SetPositionAndRotation(new Vector3(10000, 200, -10000), Quaternion.Euler(0, 180, 0));
                var world = (Vector3)target.GetType().GetProperty("WorldPosition").GetValue(target);
                Assert.That(Vector3.Distance(world - own.transform.position, new Vector3(1852, 304.8f, 0)), Is.LessThan(.001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(own); }
        }

        [Test]
        public void MapAndCue_UseTheSameOwnshipSampleBetweenProcessingTicks()
        {
            var go = new GameObject("Same traffic sample test"); go.SetActive(false);
            try
            {
                var type = Type.GetType("TrafficRadar.Core.TrafficRadarController, TrafficRadar", true);
                var controller = go.AddComponent(type);
                var stateType = Type.GetType("TrafficRadar.Core.AircraftState, TrafficRadar", true);
                var state = Activator.CreateInstance(stateType);
                Set(state, "Icao24", "MATCH"); Set(state, "Latitude", 35.1); Set(state, "Longitude", -80.0);
                Set(state, "AltitudeMeters", 2300f); Set(state, "LastUpdate", DateTime.UtcNow);
                ((System.Collections.IList)Get<object>(controller, "_cachedAircraftStates")).Add(state);
                Call(type, controller, "SetOwnPosition", 35.0, -80.0, 2000f, 85f);
                Call(type, controller, "ProcessCurrentData");
                var map = (System.Collections.IList)type.GetProperty("CurrentTargets").GetValue(controller);
                Call(type, controller, "SetOwnPosition", 35.01, -80.01, 2200f, 100f);
                var cues = (System.Collections.IList)Call(type, controller, "GetIndicatorTargets", 80f);
                foreach (var field in new[] { "DistanceNM", "BearingDegrees", "RelativeAltitudeFeet" })
                    Assert.That(Get<float>(cues[0], field), Is.EqualTo(Get<float>(map[0], field)).Within(.0001), field);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void TargetInsideViewInset_StaysAtActualPixelNotAnOffscreenArrow()
        {
            var go = new GameObject("Visible edge cue", typeof(Camera));
            try
            {
                var camera = go.GetComponent<Camera>();
                camera.pixelRect = new Rect(0, 0, 1200, 800);
                var target = Activator.CreateInstance(Runtime("IndicatorSystem.Integration.TrafficIndicatorTarget"));
                Set(target, "worldPosition", camera.ViewportToWorldPoint(new Vector3(.98f, .5f, 1000)));
                Set(target, "distanceNM", 1f);
                var config = Activator.CreateInstance(Runtime("IndicatorSystem.Core.IndicatorEdgeConfig"));
                Set(config, "EdgePadding", 80f); Set(config, "MaxDisplayDistance", 80f);
                var data = Call(Projection, null, "CalculateIndicator", target, camera, config);
                Assert.That(Get<object>(data, "Visibility").ToString(), Is.EqualTo("OnScreen"));
                Assert.That(Get<Vector2>(data, "ScreenPosition").x, Is.EqualTo(1176).Within(.1f));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
