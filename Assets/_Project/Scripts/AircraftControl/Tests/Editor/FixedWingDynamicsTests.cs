using NUnit.Framework;
using AircraftControl.Core;
using UnityEngine;

namespace AircraftControl.Tests.Editor
{
    /// <summary>
    /// Unit tests for FixedWingDynamics class
    /// </summary>
    public class FixedWingDynamicsTests
    {
        private FixedWingDynamics _dynamics;
        private AircraftState _state;

        [SetUp]
        public void Setup()
        {
            _dynamics = new FixedWingDynamics();
            _state = AircraftState.CreateDefault(AircraftType.FixedWing);
            _dynamics.Initialize(_state);
        }

        [Test]
        public void Initialize_SetsAircraftTypeToFixedWing()
        {
            Assert.AreEqual(AircraftType.FixedWing, _state.AircraftType);
        }

        [Test]
        public void AircraftType_ReturnsFixedWing()
        {
            Assert.AreEqual(AircraftType.FixedWing, _dynamics.AircraftType);
        }

        [Test]
        public void UpdatePhysics_WithZeroThrottle_SlowsDown()
        {
            _state.ThrottlePercent = 0f;
            float initialSpeed = _state.IndicatedAirspeedKnots;

            _dynamics.UpdatePhysics(_state, 1f);

            Assert.Less(_state.IndicatedAirspeedKnots, initialSpeed);
        }

        [Test]
        public void UpdatePhysics_WithFullThrottle_SpeedsUp()
        {
            _state.ThrottlePercent = 100f;
            _state.IndicatedAirspeedKnots = 100f; // Start slow

            _dynamics.UpdatePhysics(_state, 5f);

            Assert.Greater(_state.IndicatedAirspeedKnots, 100f);
        }

        [Test]
        public void UpdatePhysics_PitchUp_Climbs()
        {
            _state.ElevatorInput = 1f; // Pitch up
            _state.IndicatedAirspeedKnots = 150f; // Need airspeed for climb

            _dynamics.UpdatePhysics(_state, 2f);

            // Should have positive pitch
            Assert.Greater(_state.Pitch, 0f);
        }

        [Test]
        public void UpdatePhysics_PitchDown_Descends()
        {
            _state.ElevatorInput = -1f; // Pitch down
            _state.IndicatedAirspeedKnots = 150f;

            _dynamics.UpdatePhysics(_state, 2f);

            // Should have negative pitch
            Assert.Less(_state.Pitch, 0f);
        }

        [Test]
        public void UpdatePhysics_RollRight_PositiveRoll()
        {
            _state.AileronInput = 1f; // Roll right

            _dynamics.UpdatePhysics(_state, 2f);

            Assert.Greater(_state.Roll, 0f);
        }

        [Test]
        public void UpdatePhysics_CoordinatedTurn_ChangesHeading()
        {
            _state.AileronInput = 0.5f; // Bank angle
            _state.IndicatedAirspeedKnots = 150f; // Need speed for turn
            float initialHeading = _state.Heading;

            _dynamics.UpdatePhysics(_state, 5f);

            // Heading should change
            Assert.AreNotEqual(initialHeading, _state.Heading);
        }

        [Test]
        public void UpdatePhysics_AutoLevel_ReturnsToLevel()
        {
            // Start with non-zero pitch
            _state.Pitch = 30f;
            _state.ElevatorInput = 0f; // No input

            // Update multiple times
            for (int i = 0; i < 10; i++)
            {
                _dynamics.UpdatePhysics(_state, 0.5f);
            }

            // Should auto-level toward 0
            Assert.Less(Mathf.Abs(_state.Pitch), 30f);
        }

        [Test]
        public void UpdatePhysics_MaximumPitch_DoesNotExceedLimit()
        {
            _state.ElevatorInput = 1f;

            // Hold full up for extended time
            for (int i = 0; i < 100; i++)
            {
                _dynamics.UpdatePhysics(_state, 0.1f);
            }

            // Should be clamped to max
            Assert.LessOrEqual(_state.Pitch, 80f);
        }

        [Test]
        public void UpdatePhysics_MaximumRoll_DoesNotExceedLimit()
        {
            _state.AileronInput = 1f;

            // Hold full right for extended time
            for (int i = 0; i < 100; i++)
            {
                _dynamics.UpdatePhysics(_state, 0.1f);
            }

            // Should be clamped to max
            Assert.LessOrEqual(_state.Roll, 89f);
        }

        [Test]
        public void UpdatePhysics_StallSpeed_DoesNotGoBelow()
        {
            _state.ThrottlePercent = 0f;
            _state.IndicatedAirspeedKnots = _dynamics.MinAirspeedKnots - 20f;

            _dynamics.UpdatePhysics(_state, 5f);

            Assert.GreaterOrEqual(_state.IndicatedAirspeedKnots, _dynamics.MinAirspeedKnots);
        }

        [Test]
        public void Reset_ClearsAttitude()
        {
            _state.Pitch = 30f;
            _state.Roll = 45f;

            _dynamics.Reset(_state);

            Assert.AreEqual(0f, _state.Pitch);
            Assert.AreEqual(0f, _state.Roll);
        }

        [Test]
        public void GetRequiredInputNames_ReturnsFixedWingInputs()
        {
            var inputs = _dynamics.GetRequiredInputNames();
            
            var inputList = new System.Collections.Generic.List<string>(inputs);
            Assert.Contains("Elevator", inputList);
            Assert.Contains("Aileron", inputList);
            Assert.Contains("Rudder", inputList);
            Assert.Contains("Throttle", inputList);
        }

        [Test]
        public void ValidateState_ValidState_ReturnsTrue()
        {
            Assert.IsTrue(_dynamics.ValidateState(_state));
        }

        [Test]
        public void ValidateState_NegativeAirspeed_ReturnsFalse()
        {
            _state.IndicatedAirspeedKnots = -10f;
            Assert.IsFalse(_dynamics.ValidateState(_state));
        }

        [Test]
        public void UpdatePhysics_PositionUpdatesCorrectly()
        {
            _state.IndicatedAirspeedKnots = 200f;
            _state.ThrottlePercent = 50f;

            double initialLat = _state.Latitude;
            double initialLon = _state.Longitude;

            _dynamics.UpdatePhysics(_state, 10f);

            // Position should have changed
            Assert.AreNotEqual(initialLat, _state.Latitude);
            Assert.AreNotEqual(initialLon, _state.Longitude);
        }

        [Test]
        public void UpdatePhysics_VerticalSpeedFromPitch()
        {
            _state.Pitch = 10f; // 10 degrees pitch up
            _state.TrueAirspeedKnots = 150f;

            _dynamics.UpdatePhysics(_state, 1f);

            // Should have positive vertical speed
            Assert.Greater(_state.VerticalSpeedFpm, 0f);
        }

        [Test]
        public void UpdatePhysics_HeadingWraparound()
        {
            _state.Heading = 350f;
            _state.Roll = 30f; // Bank for turn
            _state.IndicatedAirspeedKnots = 150f;

            // Update to turn past 360
            for (int i = 0; i < 50; i++)
            {
                _dynamics.UpdatePhysics(_state, 0.2f);
            }

            // Heading should wrap to valid range
            Assert.That(_state.Heading, Is.InRange(0f, 360f));
        }
    }
}
