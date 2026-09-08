using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization.Tests
{
    public class FaaPitchLadderTests
    {
        private static Type Graphic => Type.GetType("FAA.Customization.FaaPitchLadderGraphic, Assembly-CSharp");

        [TestCase(-10f)]
        [TestCase(-5f)]
        [TestCase(-2.5f)]
        [TestCase(0f)]
        [TestCase(2.5f)]
        [TestCase(5f)]
        [TestCase(10f)]
        public void PitchMarks_FollowAngularProjectionNotTextureHeight(float mark)
        {
            var focal = Focal(60, 1920, 1080, 1);
            Assert.That(Project(mark, 0, 0, 0, focal, out var point), Is.True);
            Assert.That(point.x, Is.EqualTo(0).Within(.001));
            Assert.That(point.y, Is.EqualTo(focal.y * Mathf.Tan(mark * Mathf.Deg2Rad)).Within(.002));
            Assert.That(Mathf.Atan2(point.y, focal.y) * Mathf.Rad2Deg, Is.EqualTo(mark).Within(.001));
        }

        [TestCase(-20f)]
        [TestCase(-5f)]
        [TestCase(5f)]
        [TestCase(20f)]
        public void NoseUp_PutsHorizonBelowBoresight_AndMatchingPitchAtCenter(float pitch)
        {
            var focal = Focal(60, 1920, 1080, 1);
            Project(0, 0, pitch, 0, focal, out var horizon);
            Project(pitch, 0, pitch, 0, focal, out var matchingMark);
            Assert.That(horizon.y, Is.EqualTo(-focal.y * Mathf.Tan(pitch * Mathf.Deg2Rad)).Within(.002));
            Assert.That(matchingMark.magnitude, Is.LessThan(.002));
        }

        [TestCase(-60f)]
        [TestCase(-30f)]
        [TestCase(30f)]
        [TestCase(60f)]
        public void RightBank_RotatesTheHorizonCounterclockwise_WithoutClamping(float roll)
        {
            var focal = Focal(60, 1920, 1080, 1);
            Project(0, -5, 0, roll, focal, out var left);
            Project(0, 5, 0, roll, focal, out var right);
            float angle = Mathf.Atan2(right.y - left.y, right.x - left.x) * Mathf.Rad2Deg;
            Assert.That(angle, Is.EqualTo(roll).Within(.001));
        }

        [TestCase(40f)]
        [TestCase(60f)]
        [TestCase(90f)]
        public void FiveDegreeSpacing_TracksFieldOfView(float fov)
        {
            var focal = Focal(fov, 1920, 1080, 1);
            Project(5, 0, 0, 0, focal, out var point);
            float expected = 540 * Mathf.Tan(5 * Mathf.Deg2Rad) / Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
            Assert.That(point.y, Is.EqualTo(expected).Within(.002));
        }

        [Test]
        public void FourKAndFullHd_KeepIdenticalAngularSpacingInReferenceUnits()
        {
            var fullHd = Focal(60, 1920, 1080, 1);
            var fourK = Focal(60, 3840, 2160, 2);
            Assert.That(Vector2.Distance(fullHd, fourK), Is.LessThan(.001));
            Project(5, 0, 0, 0, fullHd, out var a);
            Project(5, 0, 0, 0, fourK, out var b);
            Assert.That(Vector2.Distance(a, b), Is.LessThan(.001));
        }

        [Test]
        public void WideViewportAndCanvasScale_DoNotStretchPitchOrBank()
        {
            var normal = Focal(60, 1920, 1080, 1);
            var wide = Focal(60, 2560, 1080, 1);
            Assert.That(Vector2.Distance(normal, wide), Is.LessThan(.001));
            var scaled = (Vector2)Graphic.GetMethod("ProjectionFocalLengths").Invoke(null,
                new object[] { Matrix4x4.Perspective(60, 16f / 9f, .1f, 100), new Vector2(1920, 1080), new Vector2(2, 4) });
            Assert.That(scaled.x, Is.EqualTo(normal.x / 2).Within(.001));
            Assert.That(scaled.y, Is.EqualTo(normal.y / 4).Within(.001));
        }

        [TestCase(100f, 0f, 0f)]
        [TestCase(0f, 180f, 0f)]
        [TestCase(float.NaN, 0f, 0f)]
        [TestCase(0f, 0f, float.PositiveInfinity)]
        public void InvalidOrBehindAircraft_DoesNotProduceAFalseClampedCue(float angle, float bearing, float pitch) =>
            Assert.That(Project(angle, bearing, pitch, 0, Vector2.one * 900, out _), Is.False);

        [Test]
        public void NumberedAndSmallAttitudeMarks_HaveExplicitDegreeIntervals()
        {
            Assert.That(Graphic.GetField("MajorIntervalDegrees").GetRawConstantValue(), Is.EqualTo(5f));
            Assert.That(Graphic.GetField("MinorIntervalDegrees").GetRawConstantValue(), Is.EqualTo(2.5f));
        }

        [Test]
        public void VectorMesh_HasSharpCoreAndCoverageFringe_WithoutSpriteOrBloom()
        {
            var host = new GameObject("Vector pitch test", typeof(RectTransform));
            host.SetActive(false);
            try
            {
                var graphic = host.AddComponent(Graphic) as MaskableGraphic;
                Assert.That(graphic, Is.Not.Null);
                Assert.That(host.GetComponent<Image>(), Is.Null);
                Assert.That(host.GetComponent<RawImage>(), Is.Null);
                Graphic.GetField("hasAttitude", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(graphic, true);
                Graphic.GetField("focal", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(graphic, Vector2.one * 935);
                using var vertices = new VertexHelper();
                Graphic.GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(graphic, new object[] { vertices });
                Assert.That(vertices.currentVertCount, Is.GreaterThan(100));
                UIVertex vertex = default;
                vertices.PopulateUIVertex(ref vertex, 0);
                Assert.That(vertex.color.a, Is.EqualTo(0));
                vertices.PopulateUIVertex(ref vertex, 2);
                Assert.That(vertex.color.a, Is.GreaterThan(200));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static Vector2 Focal(float fov, float width, float height, float scale) =>
            (Vector2)Graphic.GetMethod("ProjectionFocalLengths").Invoke(null,
                new object[] { Matrix4x4.Perspective(fov, width / height, .1f, 100), new Vector2(width, height), Vector2.one * scale });

        private static bool Project(float elevation, float bearing, float pitch, float roll, Vector2 focal, out Vector2 point)
        {
            object[] args = { elevation, bearing, pitch, roll, focal, Vector2.zero };
            bool valid = (bool)Graphic.GetMethod("ProjectDirection").Invoke(null, args);
            point = (Vector2)args[5];
            return valid;
        }
    }
}
