using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class XPlaneEngineHudBindingTests
    {
        [Test]
        public void TorquePointers_UsePersistentVerticalXPlaneTargets()
        {
            Type elementType = Type.GetType("HUDControl.Elements.TorquePanelElement, Assembly-CSharp");
            Assert.That(elementType, Is.Not.Null);

            GameObject root = new GameObject("Torque Test", typeof(RectTransform));
            GameObject leftObject = new GameObject("Left", typeof(RectTransform));
            GameObject rightObject = new GameObject("Right", typeof(RectTransform));
            try
            {
                RectTransform left = leftObject.GetComponent<RectTransform>();
                RectTransform right = rightObject.GetComponent<RectTransform>();
                left.SetParent(root.transform, false);
                right.SetParent(root.transform, false);
                left.anchoredPosition = new Vector2(-0.027f, 0.004f);
                right.anchoredPosition = new Vector2(0.0606f, 0.004f);

                Component element = root.AddComponent(elementType);
                elementType.GetMethod("ConfigurePointers")?.Invoke(element, new object[] { left, right, null });
                elementType.GetMethod("Initialize")?.Invoke(element, null);
                elementType.GetMethod("SetTorque")?.Invoke(element, new object[] { 60f, 90f });

                Assert.That(left.anchoredPosition.x, Is.EqualTo(-0.027f).Within(0.0001f));
                Assert.That(right.anchoredPosition.x, Is.EqualTo(0.0606f).Within(0.0001f));
                Assert.That(left.anchoredPosition.y, Is.EqualTo(0.124f).Within(0.0001f));
                Assert.That(right.anchoredPosition.y, Is.EqualTo(0.184f).Within(0.0001f));

                object state = Activator.CreateInstance(Type.GetType("AircraftControl.Core.AircraftState, AircraftControl"));
                elementType.GetMethod("UpdateElement")?.Invoke(element, new[] { state });
                Assert.That((float)elementType.GetMethod("GetTargetTorqueL")?.Invoke(element, null), Is.EqualTo(60f));
                Assert.That((float)elementType.GetMethod("GetDisplayedTorqueL")?.Invoke(element, null), Is.EqualTo(60f).Within(0.01f));

                elementType.GetMethod("SetTorque")?.Invoke(element, new object[] { 100f, 30f });
                Assert.That((float)elementType.GetMethod("GetTargetTorqueL")?.Invoke(element, null), Is.EqualTo(100f));
                Assert.That((float)elementType.GetMethod("GetDisplayedTorqueL")?.Invoke(element, null), Is.EqualTo(60f),
                    "Later X-Plane samples must become animation targets instead of snapping the bar.");
                Assert.That(left.anchoredPosition.y, Is.EqualTo(0.124f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NrN2Pointers_MoveIndependentlyAlongAuthoredBars()
        {
            Type elementType = Type.GetType("HUDControl.Elements.NRIndicatorElement, Assembly-CSharp");
            Assert.That(elementType, Is.Not.Null);

            GameObject root = new GameObject("NR Test", typeof(RectTransform));
            GameObject centerObject = new GameObject("Center", typeof(RectTransform));
            GameObject leftObject = new GameObject("Left", typeof(RectTransform));
            GameObject rightObject = new GameObject("Right", typeof(RectTransform));
            try
            {
                RectTransform center = centerObject.GetComponent<RectTransform>();
                RectTransform left = leftObject.GetComponent<RectTransform>();
                RectTransform right = rightObject.GetComponent<RectTransform>();
                center.SetParent(root.transform, false);
                left.SetParent(root.transform, false);
                right.SetParent(root.transform, false);
                center.anchoredPosition = new Vector2(0f, 0.03f);
                left.anchoredPosition = new Vector2(-0.11f, 0.03f);
                right.anchoredPosition = new Vector2(0.11f, 0.03f);

                Component element = root.AddComponent(elementType);
                elementType.GetMethod("ConfigurePointers")?.Invoke(element, new object[] { center, left, right, null });
                elementType.GetMethod("Initialize")?.Invoke(element, null);
                elementType.GetMethod("SetRPM")?.Invoke(element, new object[] { 55f, 82.5f, 110f });

                Assert.That(center.anchoredPosition.y, Is.EqualTo(0.15f).Within(0.0001f));
                Assert.That(left.anchoredPosition.y, Is.EqualTo(0.21f).Within(0.0001f));
                Assert.That(right.anchoredPosition.y, Is.EqualTo(0.27f).Within(0.0001f));
                Assert.That((float)elementType.GetMethod("GetDisplayedRPML")?.Invoke(element, null), Is.EqualTo(82.5f));
                Assert.That((float)elementType.GetMethod("GetDisplayedRPMR")?.Invoke(element, null), Is.EqualTo(110f));

                elementType.GetMethod("SetRPM")?.Invoke(element, new object[] { 100f, 95f, 88f });
                Assert.That((float)elementType.GetMethod("GetDisplayedRPML")?.Invoke(element, null), Is.EqualTo(82.5f),
                    "N2 must interpolate between live samples rather than jump to each poll result.");
                Assert.That((float)elementType.GetMethod("GetDisplayedRPMR")?.Invoke(element, null), Is.EqualTo(110f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EngineConversions_UseRatedTorqueAndAircraftPropRedline()
        {
            Type bridgeType = Type.GetType("FAA.XPlaneIntegration.Runtime.XPlane12ApiHudBridge, Assembly-CSharp");
            Assert.That(bridgeType, Is.Not.Null);

            var systems = new Dictionary<string, float>
            {
                ["sim/flightmodel/engine/ENGN_driv_TRQ[0]"] = 600f,
                ["sim/flightmodel/engine/POINT_max_TRQ[0]"] = 1000f,
                ["sim/cockpit2/engine/indicators/prop_speed_rpm[0]"] = 200f * 60f / (2f * Mathf.PI),
                ["sim/aircraft/controls/acf_RSC_redline_prp"] = 200f
            };

            MethodInfo torque = bridgeType.GetMethod("TryCalculateTorquePercent", BindingFlags.Public | BindingFlags.Static);
            MethodInfo rotor = bridgeType.GetMethod("TryCalculateRotorNrPercent", BindingFlags.Public | BindingFlags.Static);
            Assert.That(torque, Is.Not.Null);
            Assert.That(rotor, Is.Not.Null);

            object[] torqueArguments = { systems, 0, 0f };
            Assert.That((bool)torque.Invoke(null, torqueArguments), Is.True);
            Assert.That((float)torqueArguments[2], Is.EqualTo(60f).Within(0.001f));

            object[] rotorArguments = { systems, 0, 0f };
            Assert.That((bool)rotor.Invoke(null, rotorArguments), Is.True);
            Assert.That((float)rotorArguments[2], Is.EqualTo(100f).Within(0.001f));

            systems["sim/flightmodel/engine/POINT_max_TRQ[0]"] = 0f;
            torqueArguments = new object[] { systems, 0, 0f };
            Assert.That((bool)torque.Invoke(null, torqueArguments), Is.False,
                "A missing/zero rated torque must not be replaced with throttle or false live zero.");
        }

        [Test]
        public void EnginePointers_ClearOnUnknownTopologyOrStaleFeed()
        {
            Type torqueType = Type.GetType("HUDControl.Elements.TorquePanelElement, Assembly-CSharp");
            Type rpmType = Type.GetType("HUDControl.Elements.NRIndicatorElement, Assembly-CSharp");
            Assert.That(torqueType, Is.Not.Null);
            Assert.That(rpmType, Is.Not.Null);

            GameObject root = new GameObject("Engine Pointer Availability Test", typeof(RectTransform));
            RectTransform torqueLeft = NewPointer("Torque Left", root.transform);
            RectTransform torqueRight = NewPointer("Torque Right", root.transform);
            RectTransform rpmCenter = NewPointer("RPM Center", root.transform);
            RectTransform rpmLeft = NewPointer("RPM Left", root.transform);
            RectTransform rpmRight = NewPointer("RPM Right", root.transform);
            try
            {
                Component torque = root.AddComponent(torqueType);
                torqueType.GetMethod("ConfigurePointers")?.Invoke(torque, new object[] { torqueLeft, torqueRight, null });
                torqueType.GetMethod("SetTorque")?.Invoke(torque, new object[] { 55f, 65f });
                torqueType.GetMethod("SetEngineCount")?.Invoke(torque, new object[] { 1 });
                Assert.That(torqueLeft.gameObject.activeSelf, Is.True);
                Assert.That(torqueRight.gameObject.activeSelf, Is.False,
                    "A single-engine aircraft must not retain a ghost right pointer.");
                torqueType.GetMethod("ClearExternalData")?.Invoke(torque, null);
                Assert.That(torqueLeft.gameObject.activeSelf, Is.False);

                Component rpm = root.AddComponent(rpmType);
                rpmType.GetMethod("ConfigurePointers")?.Invoke(rpm, new object[] { rpmCenter, rpmLeft, rpmRight, null });
                rpmType.GetMethod("SetRPM")?.Invoke(rpm, new object[] { 100f, 88f, 89f });
                rpmType.GetMethod("SetEngineCount")?.Invoke(rpm, new object[] { 1 });
                Assert.That(rpmLeft.gameObject.activeSelf, Is.True);
                Assert.That(rpmRight.gameObject.activeSelf, Is.False);
                rpmType.GetMethod("SetEngineCount")?.Invoke(rpm, new object[] { 0 });
                Assert.That(rpmCenter.gameObject.activeSelf, Is.False);
                Assert.That(rpmLeft.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static RectTransform NewPointer(string name, Transform parent)
        {
            GameObject pointer = new GameObject(name, typeof(RectTransform));
            RectTransform rect = pointer.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
