using System.Collections.Generic;
using UnityEngine;
using IndicatorSystem.Core;
using IndicatorSystem.Controller;
using TrafficRadar.Core;
using TrafficRadar;

namespace IndicatorSystem.Integration
{
    /// <summary>
    /// Bridge component connecting TrafficRadarController to the indicator system.
    /// Converts RadarTarget data to IIndicatorTarget for display.
    /// 
    /// Low Coupling: Listens to events, no modification to TrafficRadar code.
    /// </summary>
    [AddComponentMenu("Indicator System/Traffic Indicator Bridge")]
    public class TrafficIndicatorBridge : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("References")]
        [Tooltip("Traffic radar controller to get targets from. Auto-finds if null.")]
        [SerializeField] private TrafficRadarController trafficRadarController;
        
        [Tooltip("Indicator system controller. Auto-finds if null.")]
        [SerializeField] private IndicatorSystemController indicatorController;
        
        [Header("Position Reference")]
        [Tooltip("Reference latitude for world position conversion")]
        [SerializeField] private double referenceLatitude = 33.6407;
        [Tooltip("Reference longitude for world position conversion")]
        [SerializeField] private double referenceLongitude = -84.4277;
        [Tooltip("Reference altitude in meters")]
        [SerializeField] private float referenceAltitude = 313f;
        
        [Header("Settings")]
        [Tooltip("Update position reference from traffic radar controller")]
        [SerializeField] private bool syncPositionFromRadar = true;
        
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;
        
        #endregion
        
        #region Private Fields
        
        private readonly List<TrafficIndicatorTarget> _convertedTargets = new List<TrafficIndicatorTarget>();
        private bool _isConnected;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            AutoFindComponents();
        }
        
        private void OnEnable()
        {
            Connect();
        }
        
        private void OnDisable()
        {
            Disconnect();
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Manually set the reference position.
        /// </summary>
        public void SetReferencePosition(double lat, double lon, float altMeters)
        {
            referenceLatitude = lat;
            referenceLongitude = lon;
            referenceAltitude = altMeters;
            
            if (indicatorController != null)
            {
                indicatorController.SetReferencePosition(lat, lon, altMeters);
            }
        }
        
        /// <summary>
        /// Force reconnection to the traffic radar controller.
        /// </summary>
        public void Reconnect()
        {
            Disconnect();
            AutoFindComponents();
            Connect();
        }
        
        #endregion
        
        #region Private Methods
        
        private void AutoFindComponents()
        {
            if (trafficRadarController == null)
            {
                trafficRadarController = FindObjectOfType<TrafficRadarController>();
            }
            
            if (indicatorController == null)
            {
                indicatorController = FindObjectOfType<IndicatorSystemController>();
            }
            
            Log($"Found TrafficRadarController: {trafficRadarController != null}, IndicatorController: {indicatorController != null}");
        }
        
        private void Connect()
        {
            if (_isConnected || trafficRadarController == null)
                return;
            
            trafficRadarController.OnTargetsUpdated.AddListener(OnTrafficTargetsUpdated);
            _isConnected = true;
            
            Log("Connected to TrafficRadarController");
        }
        
        private void Disconnect()
        {
            if (!_isConnected || trafficRadarController == null)
                return;
            
            trafficRadarController.OnTargetsUpdated.RemoveListener(OnTrafficTargetsUpdated);
            _isConnected = false;
            
            Log("Disconnected from TrafficRadarController");
        }
        
        private void OnTrafficTargetsUpdated(IReadOnlyList<RadarTarget> targets)
        {
            if (indicatorController == null)
                return;
            
            // Update reference position from radar if enabled
            if (syncPositionFromRadar && trafficRadarController != null)
            {
                var ownPos = trafficRadarController.OwnPosition;
                if (ownPos.Latitude != 0 || ownPos.Longitude != 0)
                {
                    referenceLatitude = ownPos.Latitude;
                    referenceLongitude = ownPos.Longitude;
                    referenceAltitude = ownPos.AltitudeMeters;
                    indicatorController.SetReferencePosition(referenceLatitude, referenceLongitude, referenceAltitude);
                }
            }
            
            // Convert targets
            _convertedTargets.Clear();
            foreach (var target in targets)
            {
                _convertedTargets.Add(ConvertToIndicatorTarget(target));
            }
            
            // Update indicator system
            indicatorController.SetTargets(_convertedTargets);
            
            Log($"Updated {_convertedTargets.Count} traffic indicators");
        }
        
        private TrafficIndicatorTarget ConvertToIndicatorTarget(RadarTarget radarTarget)
        {
            // Calculate world position from geographic coordinates
            Vector3 worldPos = ScreenIndicatorCalculator.GeoToWorldPosition(
                radarTarget.Latitude,
                radarTarget.Longitude,
                radarTarget.AltitudeFeet * 0.3048f, // Convert feet to meters
                referenceLatitude,
                referenceLongitude,
                referenceAltitude
            );
            
            // Get color based on threat level
            Color color = GetColorForThreatLevel(radarTarget.ThreatLevel);
            
            // Create indicator target
            return new TrafficIndicatorTarget
            {
                id = radarTarget.Icao24,
                worldPosition = worldPos,
                displayColor = color,
                priority = GetPriorityForThreatLevel(radarTarget.ThreatLevel),
                label = !string.IsNullOrEmpty(radarTarget.Callsign) ? radarTarget.Callsign : radarTarget.Icao24,
                distanceNM = radarTarget.DistanceNM,
                relativeAltitudeFeet = radarTarget.RelativeAltitudeFeet,
                threatLevel = radarTarget.ThreatLevel,
                aircraftType = radarTarget.AircraftType,
                heading = radarTarget.Heading
            };
        }
        
        private Color GetColorForThreatLevel(ThreatLevel level)
        {
            if (indicatorController?.Settings != null)
            {
                return indicatorController.Settings.GetColorForTraffic(level);
            }
            
            // Fallback colors
            switch (level)
            {
                case ThreatLevel.ResolutionAdvisory:
                    return Color.red;
                case ThreatLevel.TrafficAdvisory:
                    return new Color(1f, 0.75f, 0f); // Amber
                default:
                    return Color.cyan;
            }
        }
        
        private int GetPriorityForThreatLevel(ThreatLevel level)
        {
            switch (level)
            {
                case ThreatLevel.ResolutionAdvisory:
                    return 3;
                case ThreatLevel.TrafficAdvisory:
                    return 2;
                case ThreatLevel.Proximate:
                    return 1;
                default:
                    return 0;
            }
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[TrafficIndicatorBridge] {message}");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Implementation of IIndicatorTarget for traffic radar targets.
    /// </summary>
    public class TrafficIndicatorTarget : IIndicatorTarget
    {
        public string id;
        public Vector3 worldPosition;
        public Color displayColor;
        public int priority;
        public string label;
        public float distanceNM;
        public float relativeAltitudeFeet;
        public ThreatLevel threatLevel;
        public TrafficRadarDataManager.AircraftType aircraftType;
        public float heading;
        
        // IIndicatorTarget implementation
        public string Id => id;
        public Vector3 WorldPosition => worldPosition;
        public Color DisplayColor => displayColor;
        public int Priority => priority;
        public IndicatorType Type => IndicatorType.Traffic;
        public string Label => label;
        public float DistanceNM => distanceNM;
        public float RelativeAltitudeFeet => relativeAltitudeFeet;
        public TrafficRadarDataManager.AircraftType AircraftType => aircraftType;
        public float Heading => heading;
    }
}
