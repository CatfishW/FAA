using UnityEngine;
using System;
using System.Collections.Generic;

namespace WeatherRadar
{
    /// <summary>
    /// MonoBehaviour wrapper for WeatherRadarData to use in scene.
    /// Provides centralized data and event notification.
    /// </summary>
    public class WeatherRadarDataProvider : MonoBehaviour
    {
        [SerializeField]
        private WeatherRadarData _radarData = new WeatherRadarData();

        /// <summary>
        /// Current radar data
        /// </summary>
        public WeatherRadarData RadarData => _radarData;

        /// <summary>
        /// Event fired when radar data is updated
        /// </summary>
        public event Action<WeatherRadarData> OnRadarDataUpdated;

        /// <summary>
        /// Event fired when radar returns texture is updated
        /// </summary>
        public event Action<Texture2D> OnRadarTextureUpdated;

        /// <summary>
        /// Event fired when range changes
        /// </summary>
        public event Action<float> OnRangeChanged;

        /// <summary>
        /// Event fired when mode changes
        /// </summary>
        public event Action<RadarMode> OnModeChanged;

        /// <summary>
        /// Update the complete radar data
        /// </summary>
        public void UpdateRadarData(WeatherRadarData newData)
        {
            _radarData = newData;
            OnRadarDataUpdated?.Invoke(_radarData);
        }

        /// <summary>
        /// Update radar returns texture
        /// </summary>
        public void UpdateRadarTexture(Texture2D texture)
        {
            _radarData.radarReturns = texture;
            OnRadarTextureUpdated?.Invoke(texture);
        }

        /// <summary>
        /// Set the current range
        /// </summary>
        public void SetRange(float rangeNM)
        {
            if (!Mathf.Approximately(_radarData.currentRange, rangeNM))
            {
                _radarData.currentRange = rangeNM;
                OnRangeChanged?.Invoke(rangeNM);
                NotifyUpdate();
            }
        }

        /// <summary>
        /// Set the current mode
        /// </summary>
        public void SetMode(RadarMode mode)
        {
            if (_radarData.currentMode != mode)
            {
                _radarData.currentMode = mode;
                OnModeChanged?.Invoke(mode);
                NotifyUpdate();
            }
        }

        /// <summary>
        /// Set antenna tilt
        /// </summary>
        public void SetTilt(float tiltDegrees)
        {
            _radarData.tiltAngle = Mathf.Clamp(tiltDegrees, -15f, 15f);
            NotifyUpdate();
        }

        /// <summary>
        /// Set gain offset
        /// </summary>
        public void SetGain(float gainDB)
        {
            _radarData.gainOffset = Mathf.Clamp(gainDB, -8f, 8f);
            NotifyUpdate();
        }

        /// <summary>
        /// Set aircraft position
        /// </summary>
        public void SetPosition(float lat, float lon, float alt)
        {
            _radarData.latitude = lat;
            _radarData.longitude = lon;
            _radarData.altitude = alt;
            NotifyUpdate();
        }

        /// <summary>
        /// Set aircraft heading
        /// </summary>
        public void SetHeading(float headingDegrees)
        {
            _radarData.heading = headingDegrees;
            NotifyUpdate();
        }

        /// <summary>
        /// Update sweep angle
        /// </summary>
        public void SetSweepAngle(float angle)
        {
            _radarData.sweepAngle = angle % 360f;
        }

        /// <summary>
        /// Increase range
        /// </summary>
        public void IncreaseRange()
        {
            float oldRange = _radarData.currentRange;
            _radarData.IncreaseRange();
            if (!Mathf.Approximately(oldRange, _radarData.currentRange))
            {
                OnRangeChanged?.Invoke(_radarData.currentRange);
                NotifyUpdate();
            }
        }

        /// <summary>
        /// Decrease range
        /// </summary>
        public void DecreaseRange()
        {
            float oldRange = _radarData.currentRange;
            _radarData.DecreaseRange();
            if (!Mathf.Approximately(oldRange, _radarData.currentRange))
            {
                OnRangeChanged?.Invoke(_radarData.currentRange);
                NotifyUpdate();
            }
        }

        /// <summary>
        /// Add a waypoint to the display
        /// </summary>
        public void AddWaypoint(RadarWaypointData waypoint)
        {
            _radarData.waypoints.Add(waypoint);
            NotifyUpdate();
        }

        /// <summary>
        /// Clear all waypoints
        /// </summary>
        public void ClearWaypoints()
        {
            _radarData.waypoints.Clear();
            NotifyUpdate();
        }

        private void NotifyUpdate()
        {
            OnRadarDataUpdated?.Invoke(_radarData);
        }
    }
}
