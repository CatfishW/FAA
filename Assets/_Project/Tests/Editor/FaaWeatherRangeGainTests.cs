using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FAA.Customization.Tests
{
    public class FaaWeatherRangeGainTests
    {
        private static Type Field => Type.GetType("WeatherRadar.XPlaneSimWeatherField, WeatherRadar", true);
        private static Type Bridge => Type.GetType("FAA.XPlaneIntegration.Runtime.XPlane12ApiHudBridge, Assembly-CSharp", true);
        private static object Call(string method, params object[] args) => Field.GetMethod(method).Invoke(null, args);

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(215f)]
        public void RangeChange_SamplesTheSameWeatherAtTheSamePhysicalLocation(float heading)
        {
            var offset = new Vector2(23.5f, -17f);
            var near = (Vector2)Call("SamplePositionNM", new Vector2(.2f, .4f), 20f, heading, offset);
            var far = (Vector2)Call("SamplePositionNM", new Vector2(.1f, .2f), 40f, heading, offset);
            Assert.That(Vector2.Distance(near, far), Is.LessThan(.0001f));
            Assert.That(Call("SampleSignal", near, .8f, 0f), Is.EqualTo(Call("SampleSignal", far, .8f, 0f)));
        }

        [Test]
        public void HeadingChange_RotatesTheFieldInsteadOfReseedingIt()
        {
            var aheadEast = (Vector2)Call("SamplePositionNM", new Vector2(0, .5f), 40f, 90f, Vector2.zero);
            var rightNorth = (Vector2)Call("SamplePositionNM", new Vector2(.5f, 0), 40f, 0f, Vector2.zero);
            Assert.That(Vector2.Distance(aheadEast, rightNorth), Is.LessThan(.0001f));
        }

        [TestCase(-8f, 0.3981072f)]
        [TestCase(0f, 1f)]
        [TestCase(8f, 2.5118864f)]
        [TestCase(100f, 2.5118864f)]
        [TestCase(float.NaN, 1f)]
        public void Gain_ChangesEchoAmplitudeAndClampsToTheControls(float gain, float expected) =>
            Assert.That(Call("ApplyGain", 1f, gain), Is.EqualTo(expected).Within(.0001f));

        [Test]
        public void RangeChange_ActuallyZoomsTheRenderedPixels()
        {
            var twenty = Render(20f, 0f, .8f);
            var forty = Render(40f, 0f, .8f);
            int echoPairs = 0, differences = 0;
            // At half the range, the same return is twice as far from ownship in pixels.
            for (int dy = 5; dy <= 90; dy++)
            for (int dx = -80; dx <= 80; dx++)
            {
                Color32 near = twenty[(32 + dy * 2) * 512 + 256 + dx * 2];
                Color32 far = forty[(32 + dy) * 512 + 256 + dx];
                Assert.That(near, Is.EqualTo(far), "The same physical cell changed while zooming");
                if (far.g > 60) echoPairs++;
            }
            for (int i = 0; i < twenty.Length; i++) if (!twenty[i].Equals(forty[i])) differences++;
            Assert.That(echoPairs, Is.GreaterThan(20), "A blank frame must not pass the zoom test");
            Assert.That(differences, Is.GreaterThan(1000), "Range only changed labels, not weather pixels");
        }

        [Test]
        public void Gain_IncreasesVisibleReturnsAndBrightnessWithoutMovingCells()
        {
            var low = Render(40f, -8f, .8f);
            var normal = Render(40f, 0f, .8f);
            var high = Render(40f, 8f, .8f);
            int lowCount = 0, normalCount = 0, highCount = 0;
            long lowEnergy = 0, normalEnergy = 0, highEnergy = 0;
            for (int i = 0; i < low.Length; i++)
            {
                if (low[i].g > 60) lowCount++;
                if (normal[i].g > 60) normalCount++;
                if (high[i].g > 60) highCount++;
                lowEnergy += low[i].g; normalEnergy += normal[i].g; highEnergy += high[i].g;
                if (low[i].g > 60) Assert.That(high[i].g, Is.GreaterThan(60), "Gain must not move an existing return");
            }
            Assert.That(normalCount, Is.GreaterThan(lowCount));
            Assert.That(highCount, Is.GreaterThan(normalCount));
            Assert.That(normalEnergy, Is.GreaterThan(lowEnergy));
            Assert.That(highEnergy, Is.GreaterThan(normalEnergy));
        }

        [Test]
        public void MaximumGain_DoesNotInventRainInDryWeather()
        {
            foreach (Color32 pixel in Render(20f, 8f, 0f))
                Assert.That(pixel, Is.EqualTo(new Color32(8, 22, 31, 200)));
        }

        [Test]
        public void ReturningToASetting_RecreatesTheSamePicture()
        {
            var first = Render(20f, 0f, .8f);
            Render(80f, 8f, .8f);
            var last = Render(20f, 0f, .8f);
            CollectionAssert.AreEqual(first, last);
        }

        private static Color32[] Render(float range, float gain, float rain)
        {
            var metricsType = Bridge.GetNestedType("StreamWeatherMetrics", BindingFlags.NonPublic);
            var metrics = Activator.CreateInstance(metricsType);
            metricsType.GetField("Precipitation").SetValue(metrics, rain);
            metricsType.GetField("CloudCoverage").SetValue(metrics, 1f);
            metricsType.GetField("RangeNM").SetValue(metrics, range);
            metricsType.GetField("GainDB").SetValue(metrics, gain);
            var pixels = new Color32[512 * 362];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(8, 22, 31, 200);
            Bridge.GetMethod("DrawModernWeatherReturns", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                new object[] { pixels, 512, 362, 256, 32, 256f, 55f, null, metrics });
            return pixels;
        }
    }
}
