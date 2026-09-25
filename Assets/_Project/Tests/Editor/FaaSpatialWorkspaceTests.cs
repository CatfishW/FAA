using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class FaaSpatialWorkspaceTests
    {
        private static Type MathType => Type.GetType("FAA.Customization.FaaSpatialLayoutMath, Assembly-CSharp", true);
        private static Type EntryType => Type.GetType("FAA.Customization.FaaSpatialLayoutEntry, Assembly-CSharp", true);
        private static object Call(string name, params object[] args) => MathType.GetMethod(name).Invoke(null, args);
        private static object Entry(float yaw = 0f, float elevation = 0f, float distance = 1.2f, float scale = 1f)
        {
            var entry = Activator.CreateInstance(EntryType);
            EntryType.GetField("id").SetValue(entry, "traffic");
            EntryType.GetField("yaw").SetValue(entry, yaw);
            EntryType.GetField("elevation").SetValue(entry, elevation);
            EntryType.GetField("distance").SetValue(entry, distance);
            EntryType.GetField("scale").SetValue(entry, scale);
            return entry;
        }
        private static float Field(object entry, string name) => (float)EntryType.GetField(name).GetValue(entry);

        [TestCase(0f, 0f, 1.2f)]
        [TestCase(90f, 1.2f, 0f)]
        [TestCase(180f, 0f, -1.2f)]
        [TestCase(-90f, -1.2f, 0f)]
        public void PanelsReachAllFourCockpitDirections(float yaw, float x, float z)
        {
            var position = (Vector3)Call("Position", Entry(yaw));
            Assert.That(position.x, Is.EqualTo(x).Within(.0001));
            Assert.That(position.z, Is.EqualTo(z).Within(.0001));
        }

        [TestCase(-75f, -60f, .8f)]
        [TestCase(175f, 35f, 2.4f)]
        [TestCase(-179f, 70f, 1.5f)]
        public void SpatialLayoutRoundTripsWithoutScreenClamping(float yaw, float elevation, float distance)
        {
            var input = Entry(yaw, elevation, distance);
            var position = (Vector3)Call("Position", input);
            var restored = Entry();
            Assert.That((bool)Call("SetPosition", restored, position), Is.True);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw, Field(restored, "yaw"))), Is.LessThan(.0001));
            Assert.That(Field(restored, "elevation"), Is.EqualTo(elevation).Within(.0001));
            Assert.That(Field(restored, "distance"), Is.EqualTo(distance).Within(.0001));
        }

        [TestCase(-10f, .4f)]
        [TestCase(0f, .4f)]
        [TestCase(.72f, .72f)]
        [TestCase(1.3f, 1.3f)]
        [TestCase(20f, 1.6f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void ScaleIsFiniteBoundedAndNonzero(float input, float expected) =>
            Assert.That((float)Call("Scale", input), Is.EqualTo(expected));

        [TestCase(.84f, false, false)]
        [TestCase(.85f, false, true)]
        [TestCase(.7f, true, true)]
        [TestCase(.64f, true, false)]
        [TestCase(float.NaN, true, false)]
        public void PinchHysteresisAvoidsThresholdChatter(float strength, bool previous, bool expected) =>
            Assert.That((bool)Call("Pinched", strength, previous), Is.EqualTo(expected));

        [TestCase(.2f, .4f, true, 1.6f)]
        [TestCase(.2f, .1f, true, .5f)]
        [TestCase(.0f, .2f, false, 1f)]
        [TestCase(.02f, .2f, false, 1f)]
        [TestCase(.2f, float.NaN, false, 1f)]
        public void TwoHandResizeRejectsDegenerateTracking(float baseline, float current, bool expected, float expectedScale)
        {
            object[] args = { 1f, baseline, current, null };
            Assert.That((bool)Call("TryResize", args), Is.EqualTo(expected));
            Assert.That((float)args[3], Is.EqualTo(expectedScale).Within(.0001f));
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("{\"version\":2,\"entries\":[]}")]
        [TestCase("{\"version\":1,\"entries\":[{\"id\":\"x\"},{\"id\":\"x\"}]}")]
        [TestCase("{\"version\":1,\"entries\":[{\"id\":\"\"}]}")]
        public void InvalidPersistedProfilesAreIgnored(string json)
        {
            object[] args = { json, null };
            Assert.That((bool)Call("TryReadProfile", args), Is.False);
            Assert.That(args[1], Is.Null);
        }

        [Test]
        public void ProfileRestoresSpatialPoseAndPerModuleSize()
        {
            const string json = "{\"version\":1,\"entries\":[{\"id\":\"traffic\",\"yaw\":165,\"elevation\":-25,\"distance\":1.5,\"scale\":0.8},{\"id\":\"airspeed\",\"scale\":0.6}]}";
            object[] args = { json, null };
            Assert.That((bool)Call("TryReadProfile", args), Is.True);
            string exported = JsonUtility.ToJson(args[1]);
            Assert.That(exported, Does.Contain("traffic"));
            Assert.That(exported, Does.Contain("airspeed"));
            Assert.That(exported, Does.Contain("165"));
        }

        [Test]
        public void NaNPositionCannotEraseLastUsablePanelPose()
        {
            var entry = Entry(45, -20, 1.2f);
            Assert.That((bool)Call("SetPosition", entry, new Vector3(float.NaN, 0, 0)), Is.False);
            Assert.That((bool)Call("SetPosition", entry, Vector3.zero), Is.False);
            Assert.That(Field(entry, "yaw"), Is.EqualTo(45f));
        }

        [Test]
        public void ExcessiveDistanceAndPitchAreBoundedButYawIsNotHeadLimited()
        {
            var entry = Entry(530, -90, 100, .1f);
            Assert.That((bool)Call("Sanitize", entry), Is.True);
            Assert.That(Field(entry, "yaw"), Is.EqualTo(170f));
            Assert.That(Field(entry, "elevation"), Is.EqualTo(-80f));
            Assert.That(Field(entry, "distance"), Is.EqualTo(3f));
            Assert.That(Field(entry, "scale"), Is.EqualTo(.4f));
        }

        [Test]
        public void SeatSpaceFollowsAircraftNotIndependentHeadLook()
        {
            var seat = new GameObject("Workspace geometry fixture");
            var head = new GameObject("Independent head fixture");
            try
            {
                var local = (Vector3)Call("Position", Entry(110, -25));
                seat.transform.SetPositionAndRotation(new Vector3(50, 12, 30), Quaternion.Euler(5, 30, 8));
                Vector3 expected = seat.transform.TransformPoint(local);
                head.transform.rotation = Quaternion.Euler(0, 120, 0);
                Assert.That(seat.transform.TransformPoint(local), Is.EqualTo(expected));
                seat.transform.position += Vector3.right * 500;
                Assert.That(Vector3.Distance(seat.transform.TransformPoint(local), expected + Vector3.right * 500), Is.LessThan(.0001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(seat); UnityEngine.Object.DestroyImmediate(head); }
        }

        [Test]
        public void NativeSeatStaysInTrackingSpaceDuringHeadTranslationAndRigMovement()
        {
            Type type = Type.GetType("FAA.Customization.FaaTrackedSeatReference, Assembly-CSharp", true);
            var origin = new GameObject("XR tracking origin fixture");
            var head = new GameObject("Tracked head fixture");
            var seat = new GameObject("Stable seat fixture");
            try
            {
                head.transform.SetParent(origin.transform, false);
                head.transform.localPosition = new Vector3(0, 1.4f, 0);
                head.transform.localRotation = Quaternion.Euler(5, 35, 0);
                object reference = Activator.CreateInstance(type);
                type.GetMethod("Capture").Invoke(reference, new object[] { head.transform });
                type.GetMethod("Apply").Invoke(reference, new object[] { seat.transform });
                Vector3 position = seat.transform.position;
                Quaternion rotation = seat.transform.rotation;
                head.transform.localPosition += new Vector3(.3f, -.2f, .1f);
                head.transform.localRotation = Quaternion.Euler(20, 140, 10);
                type.GetMethod("Apply").Invoke(reference, new object[] { seat.transform });
                Assert.That(seat.transform.position, Is.EqualTo(position));
                Assert.That(Quaternion.Angle(seat.transform.rotation, rotation), Is.LessThan(.001f));
                origin.transform.position = new Vector3(400, -20, 30);
                type.GetMethod("Apply").Invoke(reference, new object[] { seat.transform });
                Assert.That(Vector3.Distance(seat.transform.position, position + origin.transform.position), Is.LessThan(.001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(origin); UnityEngine.Object.DestroyImmediate(seat); }
        }
    }
}
