using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace FAA.Customization.Tests
{
    public class FaaTrafficTypeMetadataTests
    {
        private static Type Catalog => Type.GetType("FAA.XPlaneIntegration.XPlaneTrafficTypeCatalog, Assembly-CSharp", true);
        private static object Call(string method, params object[] args) => Catalog.GetMethod(method).Invoke(null, args);
        private static string Key(int index) => "sim/cockpit2/tcas/targets/icao_type[" + index + "]";

        [TestCase("C172", "General")]
        [TestCase("BE58", "General")]
        [TestCase("SR22", "General")]
        [TestCase("R22", "Helicopter")]
        [TestCase("S76", "Helicopter")]
        [TestCase("F4", "Military")]
        [TestCase("B738", "Commercial")]
        [TestCase("MD82", "Commercial")]
        [TestCase("A333", "Commercial")]
        [TestCase("", "Unknown")]
        [TestCase("ZZZZ", "Unknown")]
        [TestCase("XPL-01", "Unknown")]
        public void ReportedIcao_SelectsCategoryWithoutCallsignGuessing(string code, string expected) =>
            Assert.That(Call("CategoryForIcao", code).ToString(), Is.EqualTo(expected));

        [Test]
        public void Slots_AreNotCompactedAndNeverUseOwnshipType()
        {
            var data = new Dictionary<string, float>();
            for (int i = 0; i < 160; i++) data[Key(i)] = 0;
            foreach (var pair in new[] { (0, "S76"), (2, "C172"), (19, "B738") })
                for (int i = 0; i < pair.Item2.Length; i++) data[Key(pair.Item1 * 8 + i)] = pair.Item2[i];
            Assert.That(Call("ReadNativeIcao", data, 0), Is.EqualTo(""));
            Assert.That(Call("ReadNativeIcao", data, 1), Is.EqualTo(""));
            Assert.That(Call("ReadNativeIcao", data, 2), Is.EqualTo("C172"));
            Assert.That(Call("ReadNativeIcao", data, 19), Is.EqualTo("B738"));
        }

        [TestCase(float.NaN)]
        [TestCase(65.5f)]
        [TestCase(-1f)]
        [TestCase(255f)]
        public void InvalidByte_DoesNotProduceAType(float invalid)
        {
            var data = new Dictionary<string, float>();
            for (int i = 8; i < 16; i++) data[Key(i)] = 'A';
            Assert.That(Call("ReadNativeIcao", data, 1), Is.EqualTo(""), "Unterminated slot");
            data[Key(8)] = invalid;
            Assert.That(Call("ReadNativeIcao", data, 1), Is.EqualTo(""));
        }
    }
}
