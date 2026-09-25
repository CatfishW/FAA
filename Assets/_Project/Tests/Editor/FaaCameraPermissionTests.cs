using System;
using NUnit.Framework;

namespace FAA.Customization.Tests
{
    // Pure policy tests. They do not query TCC, request consent or activate a camera.
    public sealed class FaaCameraPermissionTests
    {
        private static Type Permission => Type.GetType("FAA.Customization.FaaMacCameraPermission, Assembly-CSharp", true);
        private static Type State => Type.GetType("FAA.Customization.FaaCameraPermissionState, Assembly-CSharp", true);
        [TestCase(-1, false)] [TestCase(0, false)] [TestCase(1, false)] [TestCase(2, false)]
        [TestCase(3, true)] [TestCase(4, true)] [TestCase(99, false)]
        public void CaptureRequiresAnAuthorizedOrNonMacState(int state, bool expected)
        {
            Assert.That((bool)Permission.GetMethod("CanCapture").Invoke(null, new[] { Enum.ToObject(State, state) }), Is.EqualTo(expected));
        }
        [TestCase(0, "not requested")] [TestCase(1, "restricted")] [TestCase(2, "denied")]
        [TestCase(3, "ALLOWED")] [TestCase(-1, "unavailable")]
        public void PermissionMessageDoesNotConfuseDeniedPendingAndAllowed(int state, string expected)
        {
            string text = (string)Permission.GetMethod("Description").Invoke(null, new[] { Enum.ToObject(State, state) });
            Assert.That(text, Does.Contain(expected));
        }
    }
}
