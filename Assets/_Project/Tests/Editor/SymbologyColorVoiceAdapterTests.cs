using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class SymbologyColorVoiceAdapterTests
    {
        [Test]
        public void HudVisibility_HidesCompleteFlightHudRoots_AndLeavesIndependentUiVisible()
        {
            Type adapterType = Type.GetType("VoiceControl.Adapters.SymbologyColorVoiceAdapter, Assembly-CSharp");
            Assert.That(adapterType, Is.Not.Null);

            GameObject adapterObject = new GameObject("Symbology Adapter Test");
            GameObject mainHud = CreateCanvas("FAASymbologyCanvas");
            GameObject headingTape = CreateCanvas("FAAHeadingTapeCanvas");
            GameObject weather = CreateCanvas("XPlaneWeatherRadarCanvas");
            GameObject traffic = CreateCanvas("XPlaneTrafficRadarCanvas");
            GameObject indicator = CreateCanvas("IndicatorCanvas");
            GameObject menu = CreateCanvas("HUD Radial Menu Canvas");

            try
            {
                CanvasGroup authoredMainGroup = mainHud.AddComponent<CanvasGroup>();
                authoredMainGroup.alpha = 0.72f;

                Component adapter = adapterObject.AddComponent(adapterType);
                MethodInfo cache = GetPrivateMethod(adapterType, "CacheHudVisibilityTargets");
                MethodInfo apply = GetPrivateMethod(adapterType, "ApplyCachedHudVisibility");
                cache.Invoke(adapter, new object[]
                {
                    new[]
                    {
                        mainHud.GetComponent<Canvas>(),
                        headingTape.GetComponent<Canvas>(),
                        weather.GetComponent<Canvas>(),
                        traffic.GetComponent<Canvas>(),
                        indicator.GetComponent<Canvas>(),
                        menu.GetComponent<Canvas>()
                    }
                });

                apply.Invoke(adapter, new object[] { false });

                Assert.That(mainHud.activeSelf, Is.True, "HUD data/update objects must remain active.");
                Assert.That(mainHud.GetComponent<Canvas>().enabled, Is.True);
                Assert.That(authoredMainGroup.alpha, Is.Zero);
                Assert.That(authoredMainGroup.interactable, Is.False);
                Assert.That(authoredMainGroup.blocksRaycasts, Is.False);
                Assert.That(headingTape.GetComponent<CanvasGroup>(), Is.Not.Null);
                Assert.That(headingTape.GetComponent<CanvasGroup>().alpha, Is.Zero);
                Assert.That(headingTape.GetComponent<CanvasGroup>().interactable, Is.False);
                Assert.That(headingTape.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);

                Assert.That(weather.GetComponent<CanvasGroup>(), Is.Null);
                Assert.That(traffic.GetComponent<CanvasGroup>(), Is.Null);
                Assert.That(indicator.GetComponent<CanvasGroup>(), Is.Null);
                Assert.That(menu.GetComponent<CanvasGroup>(), Is.Null);

                apply.Invoke(adapter, new object[] { true });

                Assert.That(authoredMainGroup.alpha, Is.EqualTo(0.72f).Within(0.001f));
                Assert.That(authoredMainGroup.interactable, Is.True);
                Assert.That(authoredMainGroup.blocksRaycasts, Is.True);
                Assert.That(headingTape.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(adapterObject);
                UnityEngine.Object.DestroyImmediate(mainHud);
                UnityEngine.Object.DestroyImmediate(headingTape);
                UnityEngine.Object.DestroyImmediate(weather);
                UnityEngine.Object.DestroyImmediate(traffic);
                UnityEngine.Object.DestroyImmediate(indicator);
                UnityEngine.Object.DestroyImmediate(menu);
            }
        }

        [Test]
        public void HudVisibility_ReleasesCachedRoot_WhenItBecomesAnExcludedRadarCanvas()
        {
            Type adapterType = Type.GetType("VoiceControl.Adapters.SymbologyColorVoiceAdapter, Assembly-CSharp");
            Assert.That(adapterType, Is.Not.Null);

            GameObject adapterObject = new GameObject("Symbology Adapter Cache Test");
            GameObject mainHud = CreateCanvas("FAASymbologyCanvas");

            try
            {
                Component adapter = adapterObject.AddComponent(adapterType);
                MethodInfo cache = GetPrivateMethod(adapterType, "CacheHudVisibilityTargets");
                MethodInfo apply = GetPrivateMethod(adapterType, "ApplyCachedHudVisibility");
                MethodInfo prune = GetPrivateMethod(adapterType, "PruneHudVisibilityTargets");
                FieldInfo rootsVisible = adapterType.GetField(
                    "_hudRootsVisible",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(rootsVisible, Is.Not.Null);

                cache.Invoke(adapter, new object[] { new[] { mainHud.GetComponent<Canvas>() } });
                rootsVisible.SetValue(adapter, false);
                apply.Invoke(adapter, new object[] { false });
                Assert.That(mainHud.GetComponent<CanvasGroup>().alpha, Is.Zero);

                mainHud.name = "XPlaneWeatherRadarCanvas";
                prune.Invoke(adapter, null);

                Assert.That(mainHud.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(adapterObject);
                UnityEngine.Object.DestroyImmediate(mainHud);
            }
        }

        private static GameObject CreateCanvas(string name)
        {
            return new GameObject(name, typeof(RectTransform), typeof(Canvas));
        }

        private static MethodInfo GetPrivateMethod(Type type, string name)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Could not locate {name}.");
            return method;
        }
    }
}
