using UnityEngine;
using System;
using System.Collections.Generic;

namespace AviationUI.XPlaneIntegration
{
    /// <summary>
    /// Data normalization layer that maps X-Plane DataRef names to AviationFlightData fields.
    /// Handles unit conversions (radians→degrees, m/s→knots, meters→feet) and coordinate system differences.
    /// </summary>
    public static class XPlaneDataRefMapper
    {
        #region X-Plane DataRef Paths

        public const string DataRef_Pitch = "sim/flightmodel/position/theta";
        public const string DataRef_Roll = "sim/flightmodel/position/phi";
        public const string DataRef_Heading = "sim/flightmodel/position/psi";

        public const string DataRef_IAS = "sim/flightmodel/position/indicated_airspeed";
        public const string DataRef_TAS = "sim/flightmodel/position/true_airspeed";
        public const string DataRef_GS = "sim/flightmodel/position/groundspeed";

        public const string DataRef_Latitude = "sim/flightmodel/position/latitude";
        public const string DataRef_Longitude = "sim/flightmodel/position/longitude";
        public const string DataRef_Elevation = "sim/flightmodel/position/elevation";

        public const string DataRef_WindSpeed = "sim/weather/wind_speed_total[0]";
        public const string DataRef_WindDirection = "sim/weather/wind_direction_true[0]";
        public const string DataRef_Pressure = "sim/weather/barometer[0]";
        public const string DataRef_Temperature = "sim/weather/temperature_c[0]";

        public const string DataRef_VerticalSpeed = "sim/flightmodel/position/vh_ind";
        public const string DataRef_AGL = "sim/flightmodel/position/y_agl";

        #endregion

        #region Conversion Constants

        private const float RadToDegFactor = 57.29578f;
        private const float MpsToKnotsFactor = 1.94384f;
        private const float MetersToFeetFactor = 3.28084f;
        private const float HpaToInHgFactor = 0.02953f;

        #endregion

        #region Conversion Utilities

        /// <summary>
        /// Convert radians to degrees
        /// </summary>
        public static float RadToDeg(float radians)
        {
            return radians * RadToDegFactor;
        }

        /// <summary>
        /// Convert meters per second to knots
        /// </summary>
        public static float MpsToKnots(float mps)
        {
            return mps * MpsToKnotsFactor;
        }

        /// <summary>
        /// Convert meters to feet
        /// </summary>
        public static float MetersToFeet(float meters)
        {
            return meters * MetersToFeetFactor;
        }

        /// <summary>
        /// Convert hectopascals to inches of mercury
        /// </summary>
        public static float HpaToInHg(float hpa)
        {
            return hpa * HpaToInHgFactor;
        }

        /// <summary>
        /// Normalize heading to 0-360 range
        /// </summary>
        public static float NormalizeHeading(float headingDeg)
        {
            headingDeg = headingDeg % 360f;
            if (headingDeg < 0f)
            {
                headingDeg += 360f;
            }
            return headingDeg;
        }

        /// <summary>
        /// Normalize angle to -180 to 180 range
        /// </summary>
        public static float NormalizeAngle(float angleDeg)
        {
            angleDeg = angleDeg % 360f;
            if (angleDeg > 180f)
            {
                angleDeg -= 360f;
            }
            else if (angleDeg < -180f)
            {
                angleDeg += 360f;
            }
            return angleDeg;
        }

        #endregion

        #region Safe Value Extraction

        /// <summary>
        /// Safely extract a float value from a dictionary, returning default if missing or invalid
        /// </summary>
        private static float SafeGet(IDictionary<string, float> dataRefs, string key, float defaultValue = 0f)
        {
            if (dataRefs == null)
            {
                return defaultValue;
            }

            if (dataRefs.TryGetValue(key, out float value))
            {
                return value;
            }

            return defaultValue;
        }

        /// <summary>
        /// Safely extract a float value from an array index, returning default if out of bounds
        /// </summary>
        private static float SafeGet(float[] array, int index, float defaultValue = 0f)
        {
            if (array == null || index < 0 || index >= array.Length)
            {
                return defaultValue;
            }

            return array[index];
        }

        #endregion

        #region Main Mapping Function

        /// <summary>
        /// Map raw X-Plane DataRef values to AviationFlightData
        /// </summary>
        /// <param name="dataRefs">Dictionary of X-Plane DataRef paths to values</param>
        /// <returns>Populated AviationFlightData instance</returns>
        public static AviationFlightData Map(IDictionary<string, float> dataRefs)
        {
            var flightData = new AviationFlightData();

            flightData.pitch = Mathf.Clamp(RadToDeg(SafeGet(dataRefs, DataRef_Pitch)), -90f, 90f);
            flightData.roll = NormalizeAngle(RadToDeg(SafeGet(dataRefs, DataRef_Roll)));
            flightData.heading = NormalizeHeading(RadToDeg(SafeGet(dataRefs, DataRef_Heading)));

            flightData.indicatedAirspeed = MpsToKnots(SafeGet(dataRefs, DataRef_IAS));
            flightData.trueAirspeed = MpsToKnots(SafeGet(dataRefs, DataRef_TAS));
            flightData.groundSpeed = MpsToKnots(SafeGet(dataRefs, DataRef_GS));

            flightData.altitudeMSL = MetersToFeet(SafeGet(dataRefs, DataRef_Elevation));
            flightData.altitudeAGL = MetersToFeet(SafeGet(dataRefs, DataRef_AGL));
            flightData.verticalSpeed = MetersToFeet(SafeGet(dataRefs, DataRef_VerticalSpeed)) * 60f;

            flightData.windSpeed = MpsToKnots(SafeGet(dataRefs, DataRef_WindSpeed));
            flightData.windDirection = NormalizeHeading(SafeGet(dataRefs, DataRef_WindDirection));
            
            float pressureHpa = SafeGet(dataRefs, DataRef_Pressure, 1013.25f);
            flightData.barometricSetting = HpaToInHg(pressureHpa);
            
            _ = SafeGet(dataRefs, DataRef_Temperature, 15f);

            flightData.gpsValid = true;
            flightData.ilsValid = false;
            flightData.autopilotEngaged = false;

            return flightData;
        }

        /// <summary>
        /// Map raw X-Plane DataRef array values to AviationFlightData
        /// Alternative overload for array-based data structures
        /// </summary>
        /// <param name="dataRefValues">Array of X-Plane DataRef values in expected order</param>
        /// <returns>Populated AviationFlightData instance</returns>
        public static AviationFlightData Map(float[] dataRefValues)
        {
            if (dataRefValues == null || dataRefValues.Length == 0)
            {
                return new AviationFlightData();
            }

            var flightData = new AviationFlightData();
            int idx = 0;

            if (idx < dataRefValues.Length)
                flightData.pitch = Mathf.Clamp(RadToDeg(dataRefValues[idx++]), -90f, 90f);
            if (idx < dataRefValues.Length)
                flightData.roll = NormalizeAngle(RadToDeg(dataRefValues[idx++]));
            if (idx < dataRefValues.Length)
                flightData.heading = NormalizeHeading(RadToDeg(dataRefValues[idx++]));

            if (idx < dataRefValues.Length)
                flightData.indicatedAirspeed = MpsToKnots(dataRefValues[idx++]);
            if (idx < dataRefValues.Length)
                flightData.trueAirspeed = MpsToKnots(dataRefValues[idx++]);
            if (idx < dataRefValues.Length)
                flightData.groundSpeed = MpsToKnots(dataRefValues[idx++]);

            if (idx < dataRefValues.Length)
                idx++;
            if (idx < dataRefValues.Length)
                idx++;
            if (idx < dataRefValues.Length)
                flightData.altitudeMSL = MetersToFeet(dataRefValues[idx++]);

            if (idx < dataRefValues.Length)
                flightData.windSpeed = MpsToKnots(dataRefValues[idx++]);
            if (idx < dataRefValues.Length)
                flightData.windDirection = NormalizeHeading(dataRefValues[idx++]);
            if (idx < dataRefValues.Length)
                flightData.barometricSetting = HpaToInHg(dataRefValues[idx++]);
            if (idx < dataRefValues.Length)
                idx++;

            if (idx < dataRefValues.Length)
                flightData.verticalSpeed = MetersToFeet(dataRefValues[idx++]) * 60f;

            if (idx < dataRefValues.Length)
                flightData.altitudeAGL = MetersToFeet(dataRefValues[idx++]);

            return flightData;
        }

        #endregion

        #region Individual Field Mappers

        /// <summary>
        /// Map pitch from X-Plane DataRef value
        /// </summary>
        public static float MapPitch(float thetaRadians)
        {
            return Mathf.Clamp(RadToDeg(thetaRadians), -90f, 90f);
        }

        /// <summary>
        /// Map roll from X-Plane DataRef value
        /// </summary>
        public static float MapRoll(float phiRadians)
        {
            return NormalizeAngle(RadToDeg(phiRadians));
        }

        /// <summary>
        /// Map heading from X-Plane DataRef value
        /// </summary>
        public static float MapHeading(float psiRadians)
        {
            return NormalizeHeading(RadToDeg(psiRadians));
        }

        /// <summary>
        /// Map indicated airspeed from X-Plane DataRef value
        /// </summary>
        public static float MapIndicatedAirspeed(float iasMps)
        {
            return MpsToKnots(iasMps);
        }

        /// <summary>
        /// Map true airspeed from X-Plane DataRef value
        /// </summary>
        public static float MapTrueAirspeed(float tasMps)
        {
            return MpsToKnots(tasMps);
        }

        /// <summary>
        /// Map ground speed from X-Plane DataRef value
        /// </summary>
        public static float MapGroundSpeed(float gsMps)
        {
            return MpsToKnots(gsMps);
        }

        /// <summary>
        /// Map altitude from X-Plane DataRef value
        /// </summary>
        public static float MapAltitude(float elevationMeters)
        {
            return MetersToFeet(elevationMeters);
        }

        /// <summary>
        /// Map wind speed from X-Plane DataRef value
        /// </summary>
        public static float MapWindSpeed(float windSpeedMps)
        {
            return MpsToKnots(windSpeedMps);
        }

        /// <summary>
        /// Map wind direction from X-Plane DataRef value
        /// </summary>
        public static float MapWindDirection(float windDirectionDeg)
        {
            return NormalizeHeading(windDirectionDeg);
        }

        /// <summary>
        /// Map barometric pressure from X-Plane DataRef value
        /// </summary>
        public static float MapBarometricPressure(float pressureHpa)
        {
            return HpaToInHg(pressureHpa);
        }

        /// <summary>
        /// Map vertical speed from X-Plane DataRef value
        /// </summary>
        public static float MapVerticalSpeed(float vsMps)
        {
            return MetersToFeet(vsMps) * 60f;
        }

        #endregion
    }
}
