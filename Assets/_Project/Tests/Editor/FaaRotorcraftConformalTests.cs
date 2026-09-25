using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class FaaRotorcraftConformalTests
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static Type MathType => Type.GetType("FAA.Customization.FaaRotorcraftCueMath, Assembly-CSharp", true);
        private static Type DataType => Type.GetType("AviationUI.AviationFlightData, Assembly-CSharp", true);
        private static Type BridgeType => Type.GetType("FAA.XPlaneIntegration.Runtime.XPlane12ApiHudBridge, Assembly-CSharp", true);
        private static object Call(string method, params object[] args) => MathType.GetMethod(method, Any).Invoke(null, args);
        private static T Field<T>(object obj, string field) => (T)obj.GetType().GetField(field, Any).GetValue(obj);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Any).SetValue(obj, value);

        [TestCase(0f, 0f, 1f)]
        [TestCase(90f, 1f, 0f)]
        [TestCase(180f, 0f, -1f)]
        [TestCase(270f, -1f, 0f)]
        public void TrueBearingUsesEastNorthAxes(float heading, float east, float north)
        {
            var ray = (Vector3)Call("EarthRay", 0f, heading);
            Assert.That(ray.x, Is.EqualTo(east).Within(.00001f));
            Assert.That(ray.z, Is.EqualTo(north).Within(.00001f));
        }

        [TestCase(0f, 0f, 10f)]
        [TestCase(90f, 10f, 0f)]
        [TestCase(180f, 0f, -10f)]
        [TestCase(270f, -10f, 0f)]
        public void HoverVelocityShowsForwardSidewaysAndRearwardFlight(float track, float right, float forward)
        {
            object[] args = { 10f, track, 0f, null };
            Assert.That((bool)Call("TryGroundVelocity", args), Is.True);
            var drift = (Vector2)Call("HoverVelocity", (Vector3)args[3], 0f);
            Assert.That(drift.x, Is.EqualTo(right).Within(.001f));
            Assert.That(drift.y, Is.EqualTo(forward).Within(.001f));
        }

        [Test]
        public void HoverVelocityRotatesIntoAircraftHeadingNotCameraLook()
        {
            object[] args = { 5f, 90f, -60f, null };
            Call("TryGroundVelocity", args);
            var drift = (Vector2)Call("HoverVelocity", (Vector3)args[3], 90f);
            Assert.That(drift.x, Is.EqualTo(0f).Within(.001f));
            Assert.That(drift.y, Is.EqualTo(5f).Within(.001f));
        }

        [TestCase(-1f, 0f, 0f, false)]
        [TestCase(float.NaN, 0f, 0f, false)]
        [TestCase(20f, float.NaN, 0f, false)]
        [TestCase(20f, 0f, float.PositiveInfinity, false)]
        [TestCase(0f, float.NaN, 100f, true)]
        public void InvalidVelocityNeverCreatesForwardFlight(float speed, float track, float vertical, bool expected)
        {
            object[] args = { speed, track, vertical, null };
            Assert.That((bool)Call("TryGroundVelocity", args), Is.EqualTo(expected));
        }

        [TestCase(4f, false, true)]
        [TestCase(6f, false, false)]
        [TestCase(6f, true, true)]
        [TestCase(8f, true, false)]
        [TestCase(-1f, true, false)]
        [TestCase(float.NaN, true, false)]
        public void HoverHysteresisAndInvalidInput(float speed, bool previous, bool expected) =>
            Assert.That((bool)Call("UseHover", speed, previous, 5f, 8f), Is.EqualTo(expected));

        [TestCase(true, 0.9f, true)]
        [TestCase(true, 1.1f, false)]
        [TestCase(false, 0f, false)]
        [TestCase(true, -1f, false)]
        [TestCase(true, float.NaN, false)]
        public void StaleOrUnavailableFeedIsNeverLive(bool healthy, float age, bool expected) =>
            Assert.That((bool)Call("Fresh", healthy, age, 1f), Is.EqualTo(expected));

        [TestCase(-100f, -15f)]
        [TestCase(100f, 10f)]
        [TestCase(-5.5f, -5.5f)]
        [TestCase(float.NaN, -3f)]
        public void SelectedFpaIsBoundedAndRejectsInvalidValues(float requested, float expected) =>
            Assert.That((float)Call("SelectFpa", -3f, requested), Is.EqualTo(expected));

        [Test]
        public void FlightPathAngleUsesActualGroundVelocity()
        {
            object[] args = { new Vector3(0, -1, 10), null };
            Assert.That((bool)Call("TryFlightPathAngle", args), Is.True);
            Assert.That((float)args[1], Is.EqualTo(-5.710593f).Within(.0001f));
        }

        [Test]
        public void AngularWorldReferenceIgnoresTranslationButRespondsToFinalHeadPose()
        {
            var go = new GameObject("Rotorcraft projection test", typeof(Camera));
            try
            {
                var camera = go.GetComponent<Camera>();
                camera.aspect = 16f / 9f;
                Vector3 direction = (Vector3)Call("EarthRay", -3f, 15f);
                object[] first = { camera, direction, null };
                Assert.That((bool)Call("TryProjectDirection", first), Is.True);
                camera.transform.position = new Vector3(200000, 5000, -200000);
                object[] moved = { camera, direction, null };
                Assert.That((bool)Call("TryProjectDirection", moved), Is.True);
                Assert.That((Vector2)moved[2], Is.EqualTo((Vector2)first[2]));
                camera.transform.rotation = Quaternion.Euler(0, 15, 0);
                object[] turned = { camera, direction, null };
                Assert.That((bool)Call("TryProjectDirection", turned), Is.True);
                Assert.That(((Vector2)turned[2]).x, Is.EqualTo(.5f).Within(.00001f));
                camera.transform.rotation = Quaternion.Euler(0, 180, 0);
                Assert.That((bool)Call("TryProjectDirection", new object[] { camera, direction, null }), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void AngularProjectionRejectsZeroInvalidOrthographicAndOffscreen()
        {
            var go = new GameObject("Projection invalid test", typeof(Camera));
            try
            {
                var camera = go.GetComponent<Camera>();
                Assert.That((bool)Call("TryProjectDirection", new object[] { camera, Vector3.zero, null }), Is.False);
                Assert.That((bool)Call("TryProjectDirection", new object[] { camera, new Vector3(float.NaN, 0, 1), null }), Is.False);
                Assert.That((bool)Call("InView", camera, new Vector3(100, 0, 1), .015f), Is.False);
                camera.orthographic = true;
                Assert.That((bool)Call("TryProjectDirection", new object[] { camera, Vector3.forward, null }), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static Dictionary<string, float> Sample() => new Dictionary<string, float>
        {
            ["sim/flightmodel/position/theta"] = 2f,
            ["sim/flightmodel/position/phi"] = -3f,
            ["sim/flightmodel/position/psi"] = 90f,
            ["sim/flightmodel/position/mag_psi"] = 80f,
            ["sim/flightmodel/position/hpath"] = 110f,
            ["sim/flightmodel/position/groundspeed"] = 10f,
            ["sim/flightmodel/position/vh_ind"] = -.5f,
            ["sim/flightmodel/position/y_agl"] = 100f
        };

        private static object Build(Dictionary<string, float> sample)
        {
            var go = new GameObject("Inactive telemetry fixture");
            go.SetActive(false); // Never start the bridge/network or affect the actual aircraft.
            try
            {
                var bridge = go.AddComponent(BridgeType);
                return BridgeType.GetMethod("BuildFlightData", Any).Invoke(bridge,
                    new object[] { sample, new Dictionary<string, float>(), new Dictionary<string, float>() });
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void BridgeSeparatesTrueGroundTrackFromMagneticHeading()
        {
            var data = Build(Sample());
            Assert.That(Field<float>(data, "track"), Is.EqualTo(110f));
            Assert.That(Field<float>(data, "magneticVariation"), Is.EqualTo(10f));
            Assert.That(Field<bool>(data, "attitudeValid"), Is.True);
            Assert.That(Field<bool>(data, "groundVelocityValid"), Is.True);
            Assert.That(Field<bool>(data, "altitudeAGLValid"), Is.True);
        }

        [Test]
        public void MissingTrackNeverFallsBackToHeading()
        {
            var sample = Sample(); sample.Remove("sim/flightmodel/position/hpath");
            var data = Build(sample);
            Assert.That(float.IsNaN(Field<float>(data, "track")), Is.True);
            Assert.That(Field<bool>(data, "groundVelocityValid"), Is.False);
            sample["sim/flightmodel/position/groundspeed"] = 0f;
            data = Build(sample);
            Assert.That(Field<bool>(data, "groundVelocityValid"), Is.True, "A measured stationary ground vector has no meaningful track.");
        }

        [Test]
        public void MissingChannelsAreNotFreshZeroMeasurements()
        {
            var data = Build(new Dictionary<string, float>());
            Assert.That(Field<bool>(data, "attitudeValid"), Is.False);
            Assert.That(Field<bool>(data, "groundVelocityValid"), Is.False);
            Assert.That(Field<bool>(data, "altitudeAGLValid"), Is.False);
            var sample = Sample(); sample["sim/flightmodel/position/y_agl"] = -1f;
            sample["sim/flightmodel/position/theta"] = float.PositiveInfinity;
            data = Build(sample);
            Assert.That(Field<bool>(data, "attitudeValid"), Is.False);
            Assert.That(Field<bool>(data, "altitudeAGLValid"), Is.False);
        }

        [Test]
        public void DataSmoothingRecoversFromMissingTrackAndInvalidatesImmediately()
        {
            var a = Activator.CreateInstance(DataType); var b = Activator.CreateInstance(DataType);
            Set(a, "track", float.NaN); Set(b, "track", 123f);
            Set(b, "attitudeValid", true); Set(b, "groundVelocityValid", true);
            var result = DataType.GetMethod("Lerp").Invoke(null, new[] { a, b, (object).1f });
            Assert.That(Field<float>(result, "track"), Is.EqualTo(123f));
            Assert.That(Field<bool>(result, "groundVelocityValid"), Is.True);
            Set(b, "groundVelocityValid", false);
            result = DataType.GetMethod("Lerp").Invoke(null, new[] { result, b, (object).01f });
            Assert.That(Field<bool>(result, "groundVelocityValid"), Is.False);
            DataType.GetMethod("Reset").Invoke(result, null);
            Assert.That(Field<bool>(result, "attitudeValid"), Is.False);
        }

        [Test]
        public void AnchorsFollowTheirSceneAndSelectionDoesNotInventCoordinates()
        {
            Type anchorType = Type.GetType("FAA.Customization.FaaRotorcraftCueAnchor, Assembly-CSharp", true);
            var parent = new GameObject("Test ground");
            var a = new GameObject("Test LZ"); var b = new GameObject("Test hover reference");
            try
            {
                a.transform.SetParent(parent.transform, false); b.transform.SetParent(parent.transform, false);
                var first = a.AddComponent(anchorType); var second = b.AddComponent(anchorType);
                // These are non-ExecuteAlways components. The direct EditMode
                // fixture must invoke their runtime registration lifecycle explicitly.
                anchorType.GetMethod("OnEnable", Any).Invoke(first, null);
                anchorType.GetMethod("OnEnable", Any).Invoke(second, null);
                Set(first, "kind", Enum.Parse(anchorType.GetNestedType("CueType"), "LandingArea"));
                Set(second, "kind", Enum.Parse(anchorType.GetNestedType("CueType"), "HoverReference"));
                anchorType.GetMethod("Select").Invoke(first, null);
                anchorType.GetMethod("Select").Invoke(second, null);
                Assert.That(Field<bool>(first, "selected"), Is.False);
                parent.transform.position = new Vector3(30, 2, -50);
                object[] args = { null };
                Assert.That((bool)anchorType.GetMethod("TryPosition").Invoke(second, args), Is.True);
                Assert.That((Vector3)args[0], Is.EqualTo(parent.transform.position));
                b.SetActive(false);
                Assert.That((bool)anchorType.GetMethod("TryPosition").Invoke(second, args), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }
    }
}
