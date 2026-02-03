using NUnit.Framework;
using AircraftControl.Core;
using UnityEngine;

namespace AircraftControl.Tests.Editor
{
    /// <summary>
    /// Unit tests for AircraftState class
    /// </summary>
    public class AircraftStateTests
    {
        [Test]
        public void CreateDefault_FixedWing_ReturnsValidState()
        {
            var state = AircraftState.CreateDefault(AircraftType.FixedWing);

            Assert.AreEqual(AircraftType.FixedWing, state.AircraftType);
            Assert.AreEqual(250f, state.IndicatedAirspeedKnots, 1f);
            Assert.AreEqual(50f, state.ThrottlePercent, 0.1f);
            Assert.IsFalse(state.IsOnGround);
        }

        [Test]
        public void CreateDefault_Helicopter_ReturnsValidState()
        {
            var state = AircraftState.CreateDefault(AircraftType.Helicopter);

            Assert.AreEqual(AircraftType.Helicopter, state.AircraftType);
            Assert.AreEqual(0f, state.IndicatedAirspeedKnots, 0.1f);
            Assert.AreEqual(0f, state.ThrottlePercent, 0.1f);
            Assert.AreEqual(0f, state.MainRotorRpm, 0.1f);
            Assert.IsTrue(state.IsInHover);
        }

        [Test]
        public void Clone_CreatesIndependentCopy()
        {
            var original = AircraftState.CreateDefault(AircraftType.FixedWing);
            original.Latitude = 40.7128;
            original.Longitude = -74.0060;

            var clone = original.Clone();

            Assert.AreEqual(original.Latitude, clone.Latitude);
            Assert.AreEqual(original.Longitude, clone.Longitude);

            // Modify clone should not affect original
            clone.Latitude = 34.0522;
            Assert.AreNotEqual(original.Latitude, clone.Latitude);
        }

        [Test]
        public void Clone_WithTypeOverride_ChangesAircraftType()
        {
            var original = AircraftState.CreateDefault(AircraftType.FixedWing);
            var clone = original.Clone(AircraftType.Helicopter);

            Assert.AreEqual(AircraftType.Helicopter, clone.AircraftType);
        }

        [Test]
        public void Lerp_InterpolatesValuesCorrectly()
        {
            var a = new AircraftState
            {
                Latitude = 0,
                Longitude = 0,
                AltitudeMeters = 1000f,
                Heading = 0f,
                Pitch = 0f,
                Roll = 0f,
                IndicatedAirspeedKnots = 100f
            };

            var b = new AircraftState
            {
                Latitude = 10,
                Longitude = 20,
                AltitudeMeters = 2000f,
                Heading = 180f,
                Pitch = 30f,
                Roll = 45f,
                IndicatedAirspeedKnots = 200f
            };

            var result = AircraftState.Lerp(a, b, 0.5f);

            Assert.AreEqual(5.0, result.Latitude, 0.01);
            Assert.AreEqual(10.0, result.Longitude, 0.01);
            Assert.AreEqual(1500f, result.AltitudeMeters, 0.1f);
            Assert.AreEqual(90f, result.Heading, 0.1f);
            Assert.AreEqual(15f, result.Pitch, 0.1f);
            Assert.AreEqual(22.5f, result.Roll, 0.1f);
            Assert.AreEqual(150f, result.IndicatedAirspeedKnots, 0.1f);
        }

        [Test]
        public void Lerp_HelicopterFields_InterpolatesCorrectly()
        {
            var a = new AircraftState
            {
                AircraftType = AircraftType.Helicopter,
                MainRotorRpm = 0f,
                CollectiveInput = -1f,
                CyclicLongitudinalInput = -0.5f
            };

            var b = new AircraftState
            {
                AircraftType = AircraftType.Helicopter,
                MainRotorRpm = 100f,
                CollectiveInput = 1f,
                CyclicLongitudinalInput = 0.5f
            };

            var result = AircraftState.Lerp(a, b, 0.5f);

            Assert.AreEqual(50f, result.MainRotorRpm, 0.1f);
            Assert.AreEqual(0f, result.CollectiveInput, 0.1f);
            Assert.AreEqual(0f, result.CyclicLongitudinalInput, 0.1f);
        }

        [Test]
        public void AltitudeFeet_CalculatesCorrectly()
        {
            var state = new AircraftState { AltitudeMeters = 304.8f }; // ~1000 feet
            Assert.AreEqual(1000f, state.AltitudeFeet, 1f);
        }

        [Test]
        public void GroundSpeedMps_ConvertsCorrectly()
        {
            // 1 knot = 0.514444 m/s
            var state = new AircraftState { GroundSpeedKnots = 100f };
            Assert.AreEqual(51.4444f, state.GroundSpeedMps, 0.01f);
        }

        [Test]
        public void VerticalSpeedMps_ConvertsCorrectly()
        {
            // 1000 fpm = 5.08 m/s
            var state = new AircraftState { VerticalSpeedFpm = 1000f };
            Assert.AreEqual(5.08f, state.VerticalSpeedMps, 0.01f);
        }

        [Test]
        public void Lerp_HeadingHandlesWraparound()
        {
            var a = new AircraftState { Heading = 350f };
            var b = new AircraftState { Heading = 10f };

            var result = AircraftState.Lerp(a, b, 0.5f);

            // Should interpolate through 0, giving 0 degrees (or 360)
            Assert.AreEqual(0f, result.Heading, 1f);
        }

        [Test]
        public void InitialState_Helicopter_HasZeroRotorRpm()
        {
            var state = AircraftState.CreateDefault(AircraftType.Helicopter);

            Assert.AreEqual(0f, state.MainRotorRpm, "Main rotor RPM should be 0 for new helicopter");
            Assert.AreEqual(0f, state.TailRotorRpm, "Tail rotor RPM should be 0 for new helicopter");
            Assert.IsFalse(state.IsRotorSpooledUp, "Rotor should not be spooled up initially");
        }

        [Test]
        public void InitialState_Helicopter_HasZeroCollective()
        {
            var state = AircraftState.CreateDefault(AircraftType.Helicopter);

            Assert.AreEqual(0f, state.CollectiveInput, "Collective should be 0 initially");
            Assert.AreEqual(0f, state.CyclicLongitudinalInput, "Cyclic longitudinal should be 0 initially");
            Assert.AreEqual(0f, state.CyclicLateralInput, "Cyclic lateral should be 0 initially");
            Assert.AreEqual(0f, state.TailRotorInput, "Tail rotor input should be 0 initially");
        }
    }
}
