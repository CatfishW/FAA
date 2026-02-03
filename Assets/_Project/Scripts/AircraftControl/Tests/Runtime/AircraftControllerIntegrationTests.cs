using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AircraftControl.Core;

namespace AircraftControl.Tests.Runtime
{
    /// <summary>
    /// Integration tests for AircraftController
    /// These tests run in PlayMode and test the full controller behavior
    /// </summary>
    public class AircraftControllerIntegrationTests
    {
        private GameObject _aircraftObject;
        private AircraftController _controller;

        [SetUp]
        public void Setup()
        {
            _aircraftObject = new GameObject("TestAircraft");
            _controller = _aircraftObject.AddComponent<AircraftController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_aircraftObject != null)
            {
                Object.Destroy(_aircraftObject);
            }
        }

        [UnityTest]
        public IEnumerator FixedWing_DefaultState_HasValidAirspeed()
        {
            yield return null; // Wait for Awake

            Assert.AreEqual(AircraftType.FixedWing, _controller.CurrentAircraftType);
            Assert.Greater(_controller.State.IndicatedAirspeedKnots, 0f);
        }

        [UnityTest]
        public IEnumerator Helicopter_DefaultState_HasZeroRotorRpm()
        {
            _controller.SetAircraftType(AircraftType.Helicopter);
            yield return null;

            Assert.AreEqual(AircraftType.Helicopter, _controller.CurrentAircraftType);
            Assert.AreEqual(0f, _controller.State.MainRotorRpm);
        }

        [UnityTest]
        public IEnumerator SetAircraftType_ChangesType()
        {
            yield return null;

            _controller.SetAircraftType(AircraftType.Helicopter);

            Assert.AreEqual(AircraftType.Helicopter, _controller.CurrentAircraftType);
            Assert.IsNotNull(_controller.FlightDynamics);
            Assert.AreEqual(AircraftType.Helicopter, _controller.FlightDynamics.AircraftType);
        }

        [UnityTest]
        public IEnumerator SetThrottle_UpdatesThrottlePercent()
        {
            yield return null;

            _controller.SetThrottle(0.75f);

            Assert.AreEqual(75f, _controller.State.ThrottlePercent, 0.1f);
        }

        [UnityTest]
        public IEnumerator SetPitch_UpdatesElevatorInput()
        {
            yield return null;

            _controller.SetPitch(0.5f);

            // Need to wait for Update to apply
            yield return null;

            Assert.AreEqual(0.5f, _controller.State.ElevatorInput, 0.1f);
        }

        [UnityTest]
        public IEnumerator Helicopter_SetCollective_UpdatesCollectiveInput()
        {
            _controller.SetAircraftType(AircraftType.Helicopter);
            yield return null;

            _controller.SetCollective(0.8f);
            yield return null;

            Assert.AreEqual(0.8f, _controller.State.CollectiveInput, 0.1f);
        }

        [UnityTest]
        public IEnumerator Helicopter_SetCyclic_UpdatesCyclicInputs()
        {
            _controller.SetAircraftType(AircraftType.Helicopter);
            yield return null;

            _controller.SetCyclicLongitudinal(0.5f);
            _controller.SetCyclicLateral(-0.3f);
            yield return null;

            Assert.AreEqual(0.5f, _controller.State.CyclicLongitudinalInput, 0.1f);
            Assert.AreEqual(-0.3f, _controller.State.CyclicLateralInput, 0.1f);
        }

        [UnityTest]
        public IEnumerator Helicopter_RotorSpoolUp_IncreasesRpm()
        {
            _controller.SetAircraftType(AircraftType.Helicopter);
            yield return null;

            // Set full throttle
            _controller.SetThrottle(1f);

            float initialRpm = _controller.State.MainRotorRpm;

            // Wait for spool up
            yield return new WaitForSeconds(5f);

            Assert.Greater(_controller.State.MainRotorRpm, initialRpm);
        }

        [UnityTest]
        public IEnumerator SetControlEnabled_DisablesControl()
        {
            yield return null;

            _controller.SetControlEnabled(false);
            float initialPitch = _controller.State.Pitch;

            _controller.SetPitch(1f);
            yield return null;

            // Control disabled, pitch should not change
            Assert.AreEqual(initialPitch, _controller.State.Pitch, 0.01f);
        }

        [UnityTest]
        public IEnumerator ResetToDefault_ResetsState()
        {
            yield return null;

            // Change some values
            _controller.SetPitch(0.5f);
            _controller.SetRoll(0.3f);
            yield return new WaitForSeconds(0.5f);

            // Reset
            _controller.ResetToDefault();
            yield return null;

            Assert.AreEqual(0f, _controller.State.Pitch, 0.1f);
            Assert.AreEqual(0f, _controller.State.Roll, 0.1f);
        }

        [UnityTest]
        public IEnumerator FixedWing_PitchUp_IncreasesPitch()
        {
            yield return null;

            float initialPitch = _controller.State.Pitch;
            _controller.SetPitch(1f);

            // Wait for physics update
            yield return new WaitForSeconds(0.5f);

            Assert.Greater(_controller.State.Pitch, initialPitch);
        }

        [UnityTest]
        public IEnumerator FixedWing_RollRight_IncreasesRoll()
        {
            yield return null;

            float initialRoll = _controller.State.Roll;
            _controller.SetRoll(1f);

            yield return new WaitForSeconds(0.5f);

            Assert.Greater(_controller.State.Roll, initialRoll);
        }

        [UnityTest]
        public IEnumerator Helicopter_ThrottleZero_RotorStops()
        {
            _controller.SetAircraftType(AircraftType.Helicopter);
            yield return null;

            // First spool up
            _controller.SetThrottle(1f);
            yield return new WaitForSeconds(10f);

            // Then cut throttle
            _controller.SetThrottle(0f);
            float rpmBefore = _controller.State.MainRotorRpm;

            yield return new WaitForSeconds(5f);

            Assert.Less(_controller.State.MainRotorRpm, rpmBefore);
        }

        [UnityTest]
        public IEnumerator StateChangedEvent_Fires()
        {
            yield return null;

            bool eventFired = false;
            AircraftState receivedState = null;

            _controller.OnStateChanged += (state) =>
            {
                eventFired = true;
                receivedState = state;
            };

            _controller.SetPitch(0.5f);
            yield return null;

            Assert.IsTrue(eventFired);
            Assert.IsNotNull(receivedState);
        }

        [UnityTest]
        public IEnumerator GetFlightDynamics_ReturnsCorrectType()
        {
            yield return null;

            var fixedWingDynamics = _controller.GetFlightDynamics<FixedWingDynamics>();
            Assert.IsNotNull(fixedWingDynamics);

            _controller.SetAircraftType(AircraftType.Helicopter);
            yield return null;

            var heliDynamics = _controller.GetFlightDynamics<HelicopterDynamics>();
            Assert.IsNotNull(heliDynamics);
            Assert.IsNull(_controller.GetFlightDynamics<FixedWingDynamics>());
        }
    }
}
