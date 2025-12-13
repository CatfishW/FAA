using UnityEngine;
using System;

namespace AviationUI
{
    /// <summary>
    /// Centralized container for all flight data that feeds the UI components.
    /// Single source of truth for all aviation instrument data.
    /// </summary>
    [Serializable]
    public class AviationFlightData
    {
        [Header("Attitude")]
        [Tooltip("Aircraft pitch in degrees (-90 to 90)")]
        [Range(-90f, 90f)]
        public float pitch;
        
        [Tooltip("Aircraft roll/bank in degrees (-180 to 180)")]
        [Range(-180f, 180f)]
        public float roll;
        
        [Tooltip("Aircraft heading in degrees (0 to 360)")]
        [Range(0f, 360f)]
        public float heading;

        [Header("Speed")]
        [Tooltip("Indicated airspeed in knots")]
        public float indicatedAirspeed;
        
        [Tooltip("Ground speed in knots")]
        public float groundSpeed;
        
        [Tooltip("True airspeed in knots")]
        public float trueAirspeed;

        [Header("Altitude")]
        [Tooltip("Altitude MSL in feet")]
        public float altitudeMSL;
        
        [Tooltip("Altitude AGL (radar altitude) in feet")]
        public float altitudeAGL;
        
        [Tooltip("Vertical speed in feet per minute")]
        public float verticalSpeed;
        
        [Tooltip("Barometric setting in inches of mercury")]
        public float barometricSetting = 29.92f;

        [Header("Navigation")]
        [Tooltip("Magnetic variation in degrees")]
        public float magneticVariation;
        
        [Tooltip("Track over ground in degrees")]
        public float track;
        
        [Tooltip("Course deviation in dots")]
        public float courseDeviation;
        
        [Tooltip("Glideslope deviation in dots")]
        public float glideslopeDeviation;

        [Header("Engine 1")]
        [Tooltip("Engine 1 torque percentage")]
        [Range(0f, 150f)]
        public float engine1Torque;
        
        [Tooltip("Engine 1 rotor RPM percentage")]
        [Range(0f, 120f)]
        public float engine1NR;
        
        [Tooltip("Engine 1 gas generator RPM percentage")]
        [Range(0f, 120f)]
        public float engine1NG;

        [Header("Engine 2")]
        [Tooltip("Engine 2 torque percentage")]
        [Range(0f, 150f)]
        public float engine2Torque;
        
        [Tooltip("Engine 2 rotor RPM percentage")]
        [Range(0f, 120f)]
        public float engine2NR;
        
        [Tooltip("Engine 2 gas generator RPM percentage")]
        [Range(0f, 120f)]
        public float engine2NG;

        [Header("Wind")]
        [Tooltip("Wind direction in degrees")]
        [Range(0f, 360f)]
        public float windDirection;
        
        [Tooltip("Wind speed in knots")]
        public float windSpeed;

        [Header("Flight Path")]
        [Tooltip("Flight path angle in degrees")]
        public float flightPathAngle;
        
        [Tooltip("Slip/Skid indicator value (-1 to 1)")]
        [Range(-1f, 1f)]
        public float slipSkid;

        [Header("System Status")]
        [Tooltip("GPS valid")]
        public bool gpsValid = true;
        
        [Tooltip("ILS valid")]
        public bool ilsValid = false;
        
        [Tooltip("Autopilot engaged")]
        public bool autopilotEngaged = false;

        /// <summary>
        /// Create a copy of the flight data
        /// </summary>
        public AviationFlightData Clone()
        {
            return (AviationFlightData)MemberwiseClone();
        }

        /// <summary>
        /// Lerp between two flight data states for smooth transitions
        /// </summary>
        public static AviationFlightData Lerp(AviationFlightData a, AviationFlightData b, float t)
        {
            return new AviationFlightData
            {
                pitch = Mathf.Lerp(a.pitch, b.pitch, t),
                roll = Mathf.Lerp(a.roll, b.roll, t),
                heading = Mathf.LerpAngle(a.heading, b.heading, t),
                indicatedAirspeed = Mathf.Lerp(a.indicatedAirspeed, b.indicatedAirspeed, t),
                groundSpeed = Mathf.Lerp(a.groundSpeed, b.groundSpeed, t),
                trueAirspeed = Mathf.Lerp(a.trueAirspeed, b.trueAirspeed, t),
                altitudeMSL = Mathf.Lerp(a.altitudeMSL, b.altitudeMSL, t),
                altitudeAGL = Mathf.Lerp(a.altitudeAGL, b.altitudeAGL, t),
                verticalSpeed = Mathf.Lerp(a.verticalSpeed, b.verticalSpeed, t),
                barometricSetting = Mathf.Lerp(a.barometricSetting, b.barometricSetting, t),
                magneticVariation = Mathf.Lerp(a.magneticVariation, b.magneticVariation, t),
                track = Mathf.LerpAngle(a.track, b.track, t),
                courseDeviation = Mathf.Lerp(a.courseDeviation, b.courseDeviation, t),
                glideslopeDeviation = Mathf.Lerp(a.glideslopeDeviation, b.glideslopeDeviation, t),
                engine1Torque = Mathf.Lerp(a.engine1Torque, b.engine1Torque, t),
                engine1NR = Mathf.Lerp(a.engine1NR, b.engine1NR, t),
                engine1NG = Mathf.Lerp(a.engine1NG, b.engine1NG, t),
                engine2Torque = Mathf.Lerp(a.engine2Torque, b.engine2Torque, t),
                engine2NR = Mathf.Lerp(a.engine2NR, b.engine2NR, t),
                engine2NG = Mathf.Lerp(a.engine2NG, b.engine2NG, t),
                windDirection = Mathf.LerpAngle(a.windDirection, b.windDirection, t),
                windSpeed = Mathf.Lerp(a.windSpeed, b.windSpeed, t),
                flightPathAngle = Mathf.Lerp(a.flightPathAngle, b.flightPathAngle, t),
                slipSkid = Mathf.Lerp(a.slipSkid, b.slipSkid, t),
                gpsValid = t < 0.5f ? a.gpsValid : b.gpsValid,
                ilsValid = t < 0.5f ? a.ilsValid : b.ilsValid,
                autopilotEngaged = t < 0.5f ? a.autopilotEngaged : b.autopilotEngaged
            };
        }

        /// <summary>
        /// Reset all values to defaults
        /// </summary>
        public void Reset()
        {
            pitch = 0f;
            roll = 0f;
            heading = 0f;
            indicatedAirspeed = 0f;
            groundSpeed = 0f;
            trueAirspeed = 0f;
            altitudeMSL = 0f;
            altitudeAGL = 0f;
            verticalSpeed = 0f;
            barometricSetting = 29.92f;
            magneticVariation = 0f;
            track = 0f;
            courseDeviation = 0f;
            glideslopeDeviation = 0f;
            engine1Torque = 0f;
            engine1NR = 0f;
            engine1NG = 0f;
            engine2Torque = 0f;
            engine2NR = 0f;
            engine2NG = 0f;
            windDirection = 0f;
            windSpeed = 0f;
            flightPathAngle = 0f;
            slipSkid = 0f;
            gpsValid = true;
            ilsValid = false;
            autopilotEngaged = false;
        }
    }

    /// <summary>
    /// MonoBehaviour wrapper for AviationFlightData to use in scene
    /// </summary>
    public class AviationFlightDataProvider : MonoBehaviour
    {
        [SerializeField]
        private AviationFlightData _flightData = new AviationFlightData();

        public AviationFlightData FlightData => _flightData;

        /// <summary>
        /// Event fired when flight data is updated
        /// </summary>
        public event Action<AviationFlightData> OnFlightDataUpdated;

        /// <summary>
        /// Update the flight data and notify listeners
        /// </summary>
        public void UpdateFlightData(AviationFlightData newData)
        {
            _flightData = newData;
            OnFlightDataUpdated?.Invoke(_flightData);
        }

        /// <summary>
        /// Set individual values (for external system integration)
        /// </summary>
        public void SetHeading(float heading) { _flightData.heading = heading; NotifyUpdate(); }
        public void SetPitch(float pitch) { _flightData.pitch = pitch; NotifyUpdate(); }
        public void SetRoll(float roll) { _flightData.roll = roll; NotifyUpdate(); }
        public void SetAirspeed(float airspeed) { _flightData.indicatedAirspeed = airspeed; NotifyUpdate(); }
        public void SetAltitude(float altitude) { _flightData.altitudeMSL = altitude; NotifyUpdate(); }
        public void SetVerticalSpeed(float vs) { _flightData.verticalSpeed = vs; NotifyUpdate(); }
        public void SetAltitudeAGL(float agl) { _flightData.altitudeAGL = agl; NotifyUpdate(); }
        public void SetEngine1Torque(float torque) { _flightData.engine1Torque = torque; NotifyUpdate(); }
        public void SetEngine2Torque(float torque) { _flightData.engine2Torque = torque; NotifyUpdate(); }
        public void SetEngine1NR(float nr) { _flightData.engine1NR = nr; NotifyUpdate(); }
        public void SetEngine2NR(float nr) { _flightData.engine2NR = nr; NotifyUpdate(); }

        private void NotifyUpdate()
        {
            OnFlightDataUpdated?.Invoke(_flightData);
        }
    }
}
