using NUnit.Framework;
using AircraftControl.Core;
using UnityEngine;

namespace AircraftControl.Tests.Editor
{
    /// <summary>
    /// Unit tests for HelicopterDynamics class
    /// </summary>
    public class HelicopterDynamicsTests
    {
        private HelicopterDynamics _dynamics;
        private AircraftState _state;

        [SetUp]
        public void Setup()
        {
            _dynamics = new HelicopterDynamics();
            _state = AircraftState.CreateDefault(AircraftType.Helicopter);
            _dynamics.Initialize(_state);
        }

        [Test]
        public void Initialize_SetsAircraftTypeToHelicopter()
        {
            Assert.AreEqual(AircraftType.Helicopter, _state.AircraftType);
        }

        [Test]
        public void AircraftType_ReturnsHelicopter()
        {
            Assert.AreEqual(AircraftType.Helicopter, _dynamics.AircraftType);
        }

        [Test]
        public void UpdatePhysics_WithZeroThrottle_RotorSpoolsDown()
        {
            // Set initial RPM to 100%
            _dynamics.SetRotorRpm(400f);
            _state.ThrottlePercent = 0f;

            // Update for 1 second
            _dynamics.UpdatePhysics(_state, 1f);

            // RPM should decrease
            Assert.Less(_state.MainRotorRpm, 100f);
        }

        [Test]
        public void UpdatePhysics_WithFullThrottle_RotorSpoolsUp()
        {
            _state.ThrottlePercent = 100f;

            // Update for multiple seconds to allow spool up
            for (int i = 0; i < 20; i++)
            {
                _dynamics.UpdatePhysics(_state, 0.5f);
            }

            // RPM should increase significantly
            Assert.Greater(_state.MainRotorRpm, 50f);
        }

        [Test]
        public void UpdatePhysics_WithMaxCollective_Climbs()
        {
            // Setup: Spooled up rotor
            _state.ThrottlePercent = 100f;
            _state.CollectiveInput = 1f;

            // Let rotors spool up
            for (int i = 0; i < 30; i++)
            {
                _dynamics.UpdatePhysics(_state, 0.5f);
            }

            // Should be climbing
            Assert.Greater(_state.VerticalSpeedFpm, 0f);
        }

        [Test]
        public void UpdatePhysics_WithMinCollective_Descends()
        {
            // Setup: Start at altitude with spooled up rotor
            _state.AltitudeMeters = 1000f;
            _state.ThrottlePercent = 100f;
            _state.CollectiveInput = -1f;

            // Let rotors spool up
            for (int i = 0; i < 30; i++)
            {
                _dynamics.UpdatePhysics(_state, 0.5f);
            }

            // Should be descending
            Assert.Less(_state.VerticalSpeedFpm, 0f);
        }

        [Test]
        public void UpdatePhysics_ForwardCyclic_MovesForward()
        {
            // Setup: Spooled up at altitude
            _state.ThrottlePercent = 100f;
            _state.CollectiveInput = 0.5f; // Some lift
            _state.CyclicLongitudinalInput = 1f; // Forward

            // Let rotors spool up and stabilize
            for (int i = 0; i < 40; i++)
            {
                _dynamics.UpdatePhysics(_state, 0.5f);
            }

            // Should be moving forward
            Assert.Greater(_state.GroundSpeedKnots, 0f);
            // Nose should pitch down slightly
            Assert.Less(_state.Pitch, 0f);
        }

        [Test]
        public void UpdatePhysics_LateralCyclic_Rolls()
        {
            // Setup: Spooled up at altitude
            _state.ThrottlePercent = 100f;
            _state.CollectiveInput = 0.5f;
            _state.CyclicLateralInput = 1f; // Right

            // Let rotors spool up
            for (int i = 0; i < 40; i++)
            {
                _dynamics.UpdatePhysics(_state, 0.5f);
            }

            // Should roll right (negative in aviation convention)
            Assert.Less(_state.Roll, 0f);
        }

        [Test]
        public void UpdatePhysics_TailRotor_Yaws()
        {
            // Setup: Spooled up
            _state.ThrottlePercent = 100f;
            _state.TailRotorInput = 1f; // Right pedal
            float initialHeading = _state.Heading;

            // Let rotors spool up
            for (int i = 0; i < 40; i++)
            {
                _dynamics.UpdatePhysics(_state, 0.5f);
            }

            // Heading should change
            Assert.AreNotEqual(initialHeading, _state.Heading);
        }

        [Test]
        public void GroundEffect_NearGround_HasEffect()
        {
            _state.AltitudeMeters = 5f; // Low altitude

            _dynamics.UpdatePhysics(_state, 0.1f);

            Assert.Greater(_state.GroundEffectFactor, 0f);
        }

        [Test]
        public void GroundEffect_HighAltitude_NoEffect()
        {
            _state.AltitudeMeters = 200f; // High altitude

            _dynamics.UpdatePhysics(_state, 0.1f);

            Assert.AreEqual(0f, _state.GroundEffectFactor);
        }

        [Test]
        public void IsRotorSpooledUp_BelowThreshold_ReturnsFalse()
        {
            _dynamics.SetRotorRpm(200f); // Below 320f minimum

            Assert.IsFalse(_dynamics.IsRotorSpooledUp());
        }

        [Test]
        public void IsRotorSpooledUp_AboveThreshold_ReturnsTrue()
        {
            _dynamics.SetRotorRpm(350f); // Above 320f minimum

            Assert.IsTrue(_dynamics.IsRotorSpooledUp());
        }

        [Test]
        public void Reset_ClearsStateToDefaults()
        {
            // Set some non-default values
            _state.AltitudeMeters = 5000f;
            _state.Heading = 90f;
            _state.GroundSpeedKnots = 50f;
            _state.MainRotorRpm = 100f;
            _state.CollectiveInput = 0.5f;

            _dynamics.Reset(_state);

            Assert.AreEqual(0f, _state.AltitudeMeters);
            Assert.AreEqual(0f, _state.GroundSpeedKnots);
            Assert.AreEqual(0f, _state.MainRotorRpm);
            Assert.AreEqual(0f, _state.CollectiveInput);
            Assert.AreEqual(0f, _state.Pitch);
            Assert.AreEqual(0f, _state.Roll);
        }

        [Test]
        public void GetRequiredInputNames_ReturnsHelicopterInputs()
        {
            var inputs = _dynamics.GetRequiredInputNames();
            
            var inputList = new System.Collections.Generic.List<string>(inputs);
            Assert.Contains("Collective", inputList);
            Assert.Contains("CyclicLongitudinal", inputList);
            Assert.Contains("CyclicLateral", inputList);
            Assert.Contains("TailRotor", inputList);
            Assert.Contains("Throttle", inputList);
        }

        [Test]
        public void ValidateState_ValidState_ReturnsTrue()
        {
            Assert.IsTrue(_dynamics.ValidateState(_state));
        }

        [Test]
        public void ValidateState_NegativeRotorRpm_ReturnsFalse()
        {
            _state.MainRotorRpm = -10f;
            Assert.IsFalse(_dynamics.ValidateState(_state));
        }

        [Test]
        public void ValidateState_ExcessiveCollective_ReturnsFalse()
        {
            _state.CollectiveInput = 2f;
            Assert.IsFalse(_dynamics.ValidateState(_state));
        }

        [Test]
        public void UpdatePhysics_NoRotorSpeed_NoLift()
        {
            // Zero rotor RPM
            _dynamics.SetRotorRpm(0f);
            _state.CollectiveInput = 1f;
            _state.ThrottlePercent = 0f;

            float initialAltitude = _state.AltitudeMeters;

            // Update physics
            _dynamics.UpdatePhysics(_state, 1f);

            // Should be descending (no lift)
            Assert.Less(_state.VerticalSpeedFpm, 100f);
        }

        [Test]
        public void UpdatePhysics_MaximumCollective_DoesNotExceedLimit()
        {
            _state.ThrottlePercent = 100f;
            _state.CollectiveInput = 1f; // Maximum

            // Let rotors spool up and stabilize
            for (int i = 0; i < 50; i++)
            {
                _dynamics.UpdatePhysics(_state, 0.5f);
            }

            // Vertical speed should not exceed max climb rate
            Assert.LessOrEqual(_state.VerticalSpeedFpm, _dynamics.MaxClimbRateFpm * 1.1f);
        }

        [Test]
        public void UpdatePhysics_RapidYaw_HandlesGracefully()
        {
            _state.ThrottlePercent = 100f;
            _state.TailRotorInput = 1f; // Full right pedal

            // Simulate rapid yaw inputs
            for (int i = 0; i < 20; i++)
            {
                _state.TailRotorInput = i % 2 == 0 ? 1f : -1f;
                _dynamics.UpdatePhysics(_state, 0.1f);
            }

            // Heading should remain valid (0-360)
            Assert.That(_state.Heading, Is.InRange(0f, 360f));
        }

        [Test]
        public void UpdatePhysics_PositionUpdatesCorrectly()
        {
            // Setup moving helicopter
            _state.ThrottlePercent = 100f;
            _state.CollectiveInput = 0.5f;
            _state.CyclicLongitudinalInput = 1f; // Forward

            double initialLat = _state.Latitude;
            double initialLon = _state.Longitude;

            // Let it move
            for (int i = 0; i < 50; i++)
            {
                _dynamics.UpdatePhysics(_state, 0.5f);
            }

            // Position should have changed
            Assert.AreNotEqual(initialLat, _state.Latitude);
            Assert.AreNotEqual(initialLon, _state.Longitude);
        }
    }
}
