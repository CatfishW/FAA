using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class FaaCueStabilityTests
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static Type Runtime(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static void Set(object value, string field, object data) => value.GetType().GetField(field, Any).SetValue(value, data);
        private static object Get(object value, string field) => value.GetType().GetField(field, Any).GetValue(value);
        private static object Call(object value, string method, params object[] args) => value.GetType().GetMethod(method, Any).Invoke(value, args);

        [TestCase("Commercial", "AircraftAirliner")]
        [TestCase("General", "AircraftGeneral")]
        [TestCase("Military", "AircraftMilitary")]
        [TestCase("Helicopter", "AircraftHelicopter")]
        [TestCase("Unknown", "AircraftUnknown")]
        public void AircraftCategory_UsesDistinctSvgAndKeepsUnknownHonest(string category, string icon)
        {
            var data = Activator.CreateInstance(Runtime("IndicatorSystem.Core.IndicatorData"));
            var type = data.GetType().GetField("AircraftType").FieldType;
            Set(data, "AircraftType", Enum.Parse(type, category));
            var mapped = Runtime("IndicatorSystem.Display.PilotIndicatorCue").GetMethod("IconFor").Invoke(null, new[] { data });
            Assert.That(mapped.ToString(), Is.EqualTo(icon));
        }

        [TestCase("Return", "WeatherReturn")]
        [TestCase("RainLight", "WeatherRainLight")]
        [TestCase("RainModerate", "WeatherRainModerate")]
        [TestCase("RainHeavy", "WeatherRainHeavy")]
        public void WeatherKind_UsesMatchingSvgNotLightning(string kind, string icon)
        {
            var data = Activator.CreateInstance(Runtime("IndicatorSystem.Core.IndicatorData"));
            Set(data, "Type", Enum.Parse(data.GetType().GetField("Type").FieldType, "Weather"));
            Set(data, "WeatherKind", Enum.Parse(Runtime("IndicatorSystem.Core.WeatherCueKind"), kind));
            var mapped = Runtime("IndicatorSystem.Display.PilotIndicatorCue").GetMethod("IconFor").Invoke(null, new[] { data });
            Assert.That(mapped.ToString(), Is.EqualTo(icon));
        }

        [Test]
        public void SvgCue_AutomaticallyHasRendererAndAllBakedGeometry()
        {
            var go = new GameObject("SVG cue test", typeof(RectTransform));
            try
            {
                go.AddComponent(Runtime("IndicatorSystem.Display.PilotCueSymbol"));
                Assert.That(go.GetComponent<CanvasRenderer>(), Is.Not.Null, "Without CanvasRenderer the icon silently disappears");
                var library = Resources.Load("HudIcons/FaaRadarIconLibrary");
                Assert.That(library, Is.Not.Null);
                foreach (var entry in (IEnumerable)Get(library, "entries"))
                {
                    Assert.That(((Array)Get(entry, "vertices")).Length, Is.GreaterThan(0), Get(entry, "icon").ToString());
                    Assert.That(((Array)Get(entry, "triangles")).Length, Is.GreaterThan(0));
                }
                Assert.That(((Array)Get(library, "entries")).Length, Is.EqualTo(16));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static Array Samples(params (float x, float y, float intensity)[] values)
        {
            var type = Runtime("IndicatorSystem.Core.WeatherCueSample");
            var array = Array.CreateInstance(type, values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                var sample = Activator.CreateInstance(type);
                Set(sample, "PositionNM", new Vector2(values[i].x, values[i].y));
                Set(sample, "Intensity", values[i].intensity);
                array.SetValue(sample, i);
            }
            return array;
        }

        private static List<object> Select(object selector, Array samples, int limit = 4)
        {
            var result = new List<object>();
            foreach (var value in (IEnumerable)Call(selector, "Select", samples, 2f, 3f, limit)) result.Add(value);
            return result;
        }

        [Test]
        public void WeatherSelection_SurvivesSampleReorderingJitterAndStrongerSameClassPixels()
        {
            var selector = Activator.CreateInstance(Runtime("IndicatorSystem.Core.StableWeatherCueSelector"));
            var first = Select(selector, Samples((0, 8, .4f), (10, 8, .4f)), 2);
            for (int i = 0; i < 60; i++)
            {
                float delta = i % 2 == 0 ? .05f : -.05f;
                var next = Select(selector, Samples((20, 8, .4f), (10 + delta, 8, .4f), (delta, 8, .4f)), 2);
                Assert.That(Get(next[0], "Id"), Is.EqualTo(Get(first[0], "Id")));
                Assert.That(Get(next[1], "Id"), Is.EqualTo(Get(first[1], "Id")));
            }
        }

        [Test]
        public void WeatherSelection_NewHeavyReturnPreemptsLightAtCapacity()
        {
            var selector = Activator.CreateInstance(Runtime("IndicatorSystem.Core.StableWeatherCueSelector"));
            Select(selector, Samples((0, 8, .4f)), 1);
            var next = Select(selector, Samples((0, 8, .4f), (10, 8, 1f)), 1);
            Assert.That(Get(next[0], "Intensity"), Is.EqualTo(1f));
        }

        [Test]
        public void WeatherSelection_DryOrInvalidScanDoesNotCoastOldReturns()
        {
            var selector = Activator.CreateInstance(Runtime("IndicatorSystem.Core.StableWeatherCueSelector"));
            Select(selector, Samples((0, 8, .4f)));
            Assert.That(Select(selector, Samples((float.NaN, 8, .7f), (0, 8, 0))).Count, Is.Zero);
        }

        [Test]
        public void TrafficPipeline_PreservesReportedAircraftCategory()
        {
            var stateType = Type.GetType("TrafficRadar.Core.AircraftState, TrafficRadar", true);
            var go = new GameObject("Aircraft category test"); go.SetActive(false);
            try
            {
                var type = Type.GetType("TrafficRadar.Core.TrafficRadarController, TrafficRadar", true);
                var controller = go.AddComponent(type);
                var state = Activator.CreateInstance(stateType);
                Set(state, "Icao24", "HELI"); Set(state, "Latitude", 35.1); Set(state, "Longitude", -80.0);
                Set(state, "LastUpdate", DateTime.UtcNow);
                Set(state, "AircraftType", Enum.Parse(stateType.GetField("AircraftType").FieldType, "Helicopter"));
                ((IList)Get(controller, "_cachedAircraftStates")).Add(state);
                Call(controller, "SetOwnPosition", 35.0, -80.0, 2000f, 0f);
                var targets = (IList)Call(controller, "GetIndicatorTargets", 80f);
                Assert.That(Get(targets[0], "AircraftType").ToString(), Is.EqualTo("Helicopter"));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void PilotReadout_StaysOpaqueEvenOnFirstFrameAndAfterPoolReuse()
        {
            var go = new GameObject("Steady cue test", typeof(RectTransform), typeof(Canvas));
            var settings = ScriptableObject.CreateInstance(Runtime("IndicatorSystem.Core.IndicatorSettings"));
            try
            {
                Set(settings, "usePilotCueStyle", true); Set(settings, "globalOpacity", .8f);
                Set(settings, "useProximityOpacity", false);
                var element = Runtime("IndicatorSystem.Display.IndicatorElement").GetMethod("CreateDefault").Invoke(null, new object[] {go.transform});
                var data = Activator.CreateInstance(Runtime("IndicatorSystem.Core.IndicatorData"));
                Set(data, "IsActive", true); Set(data, "Id", "STEADY"); Set(data, "DistanceNM", 12f);
                for (int i = 0; i < 8; i++)
                {
                    Call(element, "Reset"); Call(element, "UpdateIndicator", data, settings);
                    Assert.That(((Component)element).GetComponent<CanvasGroup>().alpha, Is.EqualTo(.8f).Within(.001));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void CueControls_CollapseAndExpandWithoutChangingMarkerVisibility()
        {
            WithPanel((panel, settings) =>
            {
                bool traffic = (bool)Get(settings, "showTrafficIndicators"), weather = (bool)Get(settings, "showWeatherIndicators");
                Assert.That(panel.GetType().GetProperty("IsExpanded").GetValue(panel), Is.False);
                var root = ((Component)panel).GetComponent<RectTransform>();
                Assert.That(root.sizeDelta, Is.EqualTo(new Vector2(160, 35)));
                var header = root.GetComponentsInChildren<UnityEngine.UI.Button>(true)[0];
                header.onClick.Invoke();
                Assert.That(panel.GetType().GetProperty("IsExpanded").GetValue(panel), Is.True);
                Assert.That(root.sizeDelta, Is.EqualTo(new Vector2(306, 167)));
                header.onClick.Invoke();
                Assert.That(panel.GetType().GetProperty("IsExpanded").GetValue(panel), Is.False);
                Assert.That(header.gameObject.activeInHierarchy, Is.True, "The reopen button must remain available");
                Assert.That(Get(settings, "showTrafficIndicators"), Is.EqualTo(traffic));
                Assert.That(Get(settings, "showWeatherIndicators"), Is.EqualTo(weather));
            });
        }

        [Test]
        public void CueControls_CollapseClosesLegendAndReleasesItsOccupiedArea()
        {
            WithPanel((panel, settings) =>
            {
                Call(panel, "ToggleLegend"); Canvas.ForceUpdateCanvases();
                var expanded = (Rect)Call(panel, "OccupiedScreenBounds");
                Assert.That(((GameObject)Get(panel, "_legend")).activeInHierarchy, Is.True);
                Call(panel, "SetExpanded", false); Canvas.ForceUpdateCanvases();
                var collapsed = (Rect)Call(panel, "OccupiedScreenBounds");
                Assert.That(((GameObject)Get(panel, "_legend")).activeSelf, Is.False);
                Assert.That(collapsed.height, Is.LessThan(expanded.height * .2f));
                Call(panel, "SetExpanded", true);
                Assert.That(((GameObject)Get(panel, "_legend")).activeSelf, Is.False, "Reopen only controls, not the large legend");
            });
        }

        private static void WithPanel(Action<object, object> verify)
        {
            const string key = "FAA.Cues.ControlsExpanded";
            bool hadKey = PlayerPrefs.HasKey(key); int prior = PlayerPrefs.GetInt(key);
            var root = new GameObject("Cue controls canvas", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var source = new GameObject("Cue controls test source"); source.SetActive(false);
            var settings = ScriptableObject.CreateInstance(Runtime("IndicatorSystem.Core.IndicatorSettings"));
            try
            {
                PlayerPrefs.DeleteKey(key);
                var controller = source.AddComponent(Runtime("IndicatorSystem.Controller.IndicatorSystemController"));
                Set(controller, "settings", settings);
                var panel = Runtime("IndicatorSystem.Display.IndicatorControlsPanel").GetMethod("Create").Invoke(null, new object[] { controller, root.transform });
                verify(panel, settings);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(settings);
                if (hadKey) PlayerPrefs.SetInt(key, prior); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }
    }
}
