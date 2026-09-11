using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class FaaTurbulenceModeTests
    {
        private static Type Evidence => Type.GetType("WeatherRadar.TurbulenceEvidence, WeatherRadar", true);
        private static Type Mode => Type.GetType("WeatherRadar.RadarMode, WeatherRadar", true);
        private static Type Bridge => Type.GetType("FAA.XPlaneIntegration.Runtime.XPlane12ApiHudBridge, Assembly-CSharp", true);
        private static Dictionary<string, float> Samples(float value) => Enumerable.Range(0, 13)
            .ToDictionary(i => "sim/weather/region/turbulence[" + i + "]", _ => value);

        [Test]
        public void MissingSamplesAreNotZeroTurbulence()
        {
            var args = new object[] { new Dictionary<string, float>(), 0f, 0 };
            Assert.That(Evidence.GetMethod("TryRegionalMaximum").Invoke(null, args), Is.False);
            Assert.That(Evidence.GetMethod("Explanation").Invoke(null, new object[] { args[0], true }),
                Is.EqualTo("No turbulence samples · not a clear indication"));
        }

        [TestCase(0f)] [TestCase(.5f)] [TestCase(5f)] [TestCase(10f)]
        public void RegionalSettingsAreNeverCalledASpatialScan(float value)
        {
            var args = new object[] { Samples(value), 0f, 0 };
            Assert.That(Evidence.GetMethod("TryRegionalMaximum").Invoke(null, args), Is.True);
            Assert.That((float)args[1], Is.EqualTo(value));
            Assert.That(Evidence.GetMethod("Explanation").Invoke(null, new object[] { args[0], true }),
                Is.EqualTo("Region " + value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "/10 · no spatial scan"));
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)] [TestCase(10.1f)]
        public void InvalidOrIncompleteRegionalSamplesStayUnavailable(float sample)
        {
            var data = Samples(0);
            data["sim/weather/region/turbulence[0]"] = sample;
            Assert.That(Evidence.GetMethod("TryRegionalMaximum").Invoke(null, new object[] { data, 0f, 0 }), Is.False);
        }

        [Test]
        public void LocalLayerSamplesAreSeparateFromRegionalSettings()
        {
            var data = Samples(.5f);
            for (int i = 0; i < 13; i++) data["sim/weather/aircraft/turbulence[" + i + "]"] = .3f;
            var args = new object[] { data, 0f, 0 };
            Assert.That(Evidence.GetMethod("TryAircraftMaximum").Invoke(null, args), Is.True);
            Assert.That(args[1], Is.EqualTo(.3f));
            Assert.That(Evidence.GetMethod("Explanation").Invoke(null, new object[] { data, true }),
                Is.EqualTo("Region 0.5/10\nLocal layers max 0.3/10 · no spatial scan"));
            data.Remove("sim/weather/aircraft/turbulence[12]");
            Assert.That(Evidence.GetMethod("TryAircraftMaximum").Invoke(null, new object[] { data, 0f, 0 }), Is.False);
        }

        [Test]
        public void StaleSamplesCannotClaimLiveTurbulence() =>
            Assert.That(Evidence.GetMethod("Explanation").Invoke(null, new object[] { Samples(5f), false }),
                Is.EqualTo("Telemetry stale · no turbulence scan"));

        [TestCase("TURB")] [TestCase("STBY")]
        public void TurbulenceAndStandbyCannotRenderRainAsTheirReturns(string mode)
        {
            var metricsType = Bridge.GetNestedType("StreamWeatherMetrics", BindingFlags.NonPublic);
            var metrics = Activator.CreateInstance(metricsType);
            metricsType.GetField("Precipitation").SetValue(metrics, 1f);
            metricsType.GetField("RangeNM").SetValue(metrics, 20f);
            metricsType.GetField("GainDB").SetValue(metrics, 8f);
            metricsType.GetField("DisplayMode").SetValue(metrics, Enum.Parse(Mode, mode));
            var backdrop = new Color32(8, 22, 31, 200);
            var pixels = Enumerable.Repeat(backdrop, 512 * 362).ToArray();
            Bridge.GetMethod("DrawModernWeatherReturns", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                new object[] { pixels, 512, 362, 256, 32, 256f, 55f, null, metrics });
            Assert.That(pixels.All(p => p.Equals(backdrop)), Is.True);
        }

        [TestCase("WX")] [TestCase("WX_T")]
        public void RainModesKeepTheirRainLayer(string mode) =>
            Assert.That(Evidence.GetMethod("ShouldDrawRain").Invoke(null, new[] { Enum.Parse(Mode, mode) }), Is.True);
    }
}
