using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class RadarVoiceAdapterVisibilityTests
    {
        [TestCase(
            "VoiceControl.Adapters.WeatherRadarVoiceAdapter, Assembly-CSharp",
            "XPlaneWeatherRadarCanvas")]
        [TestCase(
            "VoiceControl.Adapters.TrafficRadarVoiceAdapter, Assembly-CSharp",
            "XPlaneTrafficRadarCanvas")]
        public void PanelCommands_HideDedicatedCanvasWithoutStoppingItsGameObject(
            string adapterAssemblyName,
            string canvasName)
        {
            Type adapterType = Type.GetType(adapterAssemblyName);
            Assert.That(adapterType, Is.Not.Null);

            GameObject canvasObject = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas));
            GameObject legacyRoot = new GameObject("Inactive Legacy Radar Root");
            GameObject adapterObject = new GameObject("Radar Voice Adapter Test");

            try
            {
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                Component adapter = adapterObject.AddComponent(adapterType);
                SetPrivateField(adapterType, adapter, "radarRoot", legacyRoot);
                SetPrivateField(adapterType, adapter, "_resolvedRadarCanvas", canvas);

                Assert.That(Execute(adapterType, adapter, "hide_panel"), Is.True);
                CanvasGroup group = canvasObject.GetComponent<CanvasGroup>();
                Assert.That(group, Is.Not.Null);
                Assert.That(canvasObject.activeSelf, Is.True,
                    "Hiding must leave data providers and controllers running.");
                Assert.That(canvas.enabled, Is.False);
                Assert.That(group.alpha, Is.Zero);
                Assert.That(group.interactable, Is.False);
                Assert.That(group.blocksRaycasts, Is.False);
                Assert.That(legacyRoot.activeSelf, Is.True,
                    "A stale serialized legacy root must not own live radar visibility.");

                Assert.That(Execute(adapterType, adapter, "show_panel"), Is.True);
                Assert.That(canvasObject.activeSelf, Is.True);
                Assert.That(canvas.enabled, Is.True);
                Assert.That(group.alpha, Is.GreaterThan(0f));
                Assert.That(group.interactable, Is.True);
                Assert.That(group.blocksRaycasts, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(adapterObject);
                UnityEngine.Object.DestroyImmediate(legacyRoot);
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static bool Execute(Type adapterType, Component adapter, string commandName)
        {
            MethodInfo execute = adapterType.GetMethod("ExecuteCommand", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(execute, Is.Not.Null);
            return (bool)execute.Invoke(adapter, new object[]
            {
                commandName,
                new Dictionary<string, object>()
            });
        }

        private static void SetPrivateField(Type type, object target, string fieldName, object value)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Could not locate {type.Name}.{fieldName}.");
            field.SetValue(target, value);
        }
    }
}
