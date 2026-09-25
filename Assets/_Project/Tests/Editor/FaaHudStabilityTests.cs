using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class FaaHudStabilityTests
    {
        private static Type Hud => Type.GetType("FAA.Customization.FaaConformalHudController, Assembly-CSharp", true);
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static void Set(object obj, string field, object value) => Hud.GetField(field, Any).SetValue(obj, value);
        private static bool Project(Camera camera, Quaternion reference, out Vector2 pixel, out Vector2 up)
        {
            object[] args = { camera, reference, null, null };
            bool valid = (bool)Hud.GetMethod("TryProjectReference").Invoke(null, args);
            pixel = (Vector2)args[2]; up = (Vector2)args[3];
            return valid;
        }

        [TestCase(0f, 0f, 0f)]
        [TestCase(15f, 359f, -30f)]
        [TestCase(-20f, 160f, 50f)]
        public void TranslationAndOriginRebaseCannotMoveCollimatedReference(float pitch, float yaw, float roll)
        {
            var go = new GameObject("Angular HUD camera", typeof(Camera));
            try
            {
                var camera = go.GetComponent<Camera>();
                camera.pixelRect = new Rect(80, 30, 1920, 1080);
                Quaternion reference = Quaternion.Euler(pitch, yaw, roll);
                camera.transform.rotation = reference * Quaternion.Euler(5, 20, 3);
                Assert.That(Project(camera, reference, out Vector2 expected, out Vector2 up), Is.True);
                foreach (Vector3 position in new[] { new Vector3(42, -25, 100), new Vector3(200000, 8000, -200000), Vector3.zero })
                {
                    camera.transform.position = position;
                    Assert.That(Project(camera, reference, out Vector2 actual, out Vector2 actualUp), Is.True);
                    Assert.That(actual, Is.EqualTo(expected));
                    Assert.That(actualUp, Is.EqualTo(up));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(1920f, 1080f, 25f)]
        [TestCase(1200f, 1200f, -30f)]
        [TestCase(3840f, 2160f, 2f)]
        public void AngularProjectionMatchesPerspectiveAndViewport(float width, float height, float look)
        {
            var go = new GameObject("Projection agreement", typeof(Camera));
            try
            {
                var camera = go.GetComponent<Camera>();
                camera.pixelRect = new Rect(100, 40, width, height);
                camera.aspect = width / height;
                Quaternion reference = Quaternion.Euler(-10, 179, 25);
                camera.transform.rotation = reference * Quaternion.Euler(5, look, -5);
                Assert.That(Project(camera, reference, out Vector2 pixel, out _), Is.True);
                Vector2 expected = camera.WorldToScreenPoint(reference * Vector3.forward * 1000f);
                Assert.That(Vector2.Distance(pixel, expected), Is.LessThan(.002f));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void TemporalAntialiasingJitterDoesNotMoveOverlay()
        {
            var go = new GameObject("Unjittered projection", typeof(Camera));
            try
            {
                var camera = go.GetComponent<Camera>();
                camera.pixelRect = new Rect(0, 0, 1920, 1080);
                Matrix4x4 original = camera.projectionMatrix;
                Project(camera, Quaternion.identity, out Vector2 before, out _);
                Matrix4x4 jittered = original; jittered.m02 += .002f; jittered.m12 -= .001f;
                camera.projectionMatrix = jittered;
                camera.nonJitteredProjectionMatrix = original;
                Project(camera, Quaternion.identity, out Vector2 after, out _);
                Assert.That(after, Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void RenderRefreshUsesFinalCameraPoseAndDoesNotFreezeBehindView()
        {
            var cameraGo = new GameObject("Final pose camera", typeof(Camera));
            var aircraftGo = new GameObject("Final pose aircraft");
            var canvasGo = new GameObject("Final pose canvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var rootGo = new GameObject("Test symbology", typeof(RectTransform));
            try
            {
                var camera = cameraGo.GetComponent<Camera>(); camera.pixelRect = new Rect(0, 0, 1920, 1080);
                var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                var root = (RectTransform)rootGo.transform; root.SetParent(canvasGo.transform, false);
                var hud = canvasGo.AddComponent(Hud);
                Set(hud, "aircraftTransform", aircraftGo.transform); Set(hud, "projectionCamera", camera);
                Set(hud, "_screenCanvas", canvas); Set(hud, "_screenHudRoot", root);
                Set(hud, "_headFixedRootCaptured", true); Set(hud, "_headFixedLocalRotation", Quaternion.identity);
                MethodInfo refresh = Hud.GetMethod("RefreshRenderProjection", Any);
                foreach (float yaw in new[] { 15f, -25f, 35f, 0f })
                {
                    camera.transform.rotation = Quaternion.Euler(0, yaw, 0);
                    aircraftGo.transform.position += new Vector3(10, 20, -40); // packet-stepped position
                    refresh.Invoke(hud, null);
                    Project(camera, Quaternion.identity, out Vector2 pixel, out _);
                    Assert.That(Vector2.Distance(root.anchoredPosition, pixel - camera.pixelRect.center), Is.LessThan(.001f));
                }
                camera.transform.rotation = Quaternion.Euler(0, 180, 0);
                refresh.Invoke(hud, null);
                Assert.That(Mathf.Abs(root.anchoredPosition.x), Is.GreaterThan(3840));
                camera.transform.rotation = Quaternion.identity;
                refresh.Invoke(hud, null);
                Assert.That(root.anchoredPosition.magnitude, Is.LessThan(.001f));
                ((Behaviour)hud).enabled = false;
                Assert.That(root.anchoredPosition, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasGo); UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(aircraftGo);
            }
        }

        [Test]
        public void HudLateUpdateRunsAfterCameraFinalPose()
        {
            Type camera = Type.GetType("AircraftControl.Camera.AircraftCameraController, AircraftControl", true);
            int cameraOrder = camera.GetCustomAttribute<DefaultExecutionOrder>().order;
            Assert.That(Hud.GetCustomAttribute<DefaultExecutionOrder>().order, Is.GreaterThan(cameraOrder));
        }
    }
}
