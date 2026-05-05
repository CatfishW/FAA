using System;
using System.Linq;
using System.Reflection;
using AircraftControl.Core;
using AircraftControl.Integration;
using FAA.Geo;
using UnityEngine;

namespace FAA.XPlaneIntegration.Runtime
{
    [AddComponentMenu("X-Plane Integration/Runtime/X-Plane 12 Terrain Sync")]
    public class XPlane12TerrainSync : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AircraftController aircraftController;
        [SerializeField] private GeoPosUnityPosProjectManager geoProjection;
        [SerializeField] private OnlineMapAircraftBridge onlineMapBridge;

        [Header("Projection Sync")]
        [SerializeField] private bool syncGeoProjectionOrigin = true;
        [SerializeField] private bool setDefaultPositionToAircraft = true;

        [Header("Cesium Sync")]
        [SerializeField] private bool syncCesiumGeoreference = true;
        [SerializeField] private string cesiumGeoreferenceObjectName = "CesiumGeoreference";
        [SerializeField] private bool useAircraftAltitudeForCesium = false;
        [SerializeField] private float cesiumReferenceHeightMeters = 100f;

        [Header("Recenter Threshold")]
        [SerializeField] private bool anchorOnStart = true;
        [SerializeField] private float recenterDistanceKm = 25f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        private bool _hasAnchor;
        private double _lastAnchorLatitude;
        private double _lastAnchorLongitude;
        private Component _cesiumGeoreference;
        private MethodInfo _cesiumSetOriginMethod;
        private MethodInfo _cesiumMoveOriginMethod;
        private MethodInfo _onlineMapForceUpdateMethod;

        private void Awake()
        {
            FindDependencies();
        }

        private void OnEnable()
        {
            FindDependencies();
            if (aircraftController != null)
            {
                aircraftController.OnPositionChanged += HandleAircraftPositionChanged;
            }
        }

        private void Start()
        {
            if (anchorOnStart)
            {
                AnchorTerrainNow();
            }
        }

        private void OnDisable()
        {
            if (aircraftController != null)
            {
                aircraftController.OnPositionChanged -= HandleAircraftPositionChanged;
            }
        }

        [ContextMenu("Anchor Terrain To Aircraft")]
        public void AnchorTerrainNow()
        {
            FindDependencies();
            if (aircraftController?.State == null)
            {
                return;
            }

            ApplyAnchor(
                aircraftController.State.Latitude,
                aircraftController.State.Longitude,
                aircraftController.State.AltitudeMeters);
        }

        private void HandleAircraftPositionChanged(double latitude, double longitude, float altitudeMeters)
        {
            if (!IsValidCoordinate(latitude, longitude))
            {
                return;
            }

            if (!_hasAnchor || NeedsRecentering(latitude, longitude))
            {
                ApplyAnchor(latitude, longitude, altitudeMeters);
            }
        }

        private void FindDependencies()
        {
            if (aircraftController == null)
            {
                aircraftController = FindAnyObjectByType<AircraftController>(FindObjectsInactive.Include);
            }

            if (geoProjection == null)
            {
                geoProjection = GeoPosUnityPosProjectManager.Instance ?? FindAnyObjectByType<GeoPosUnityPosProjectManager>(FindObjectsInactive.Include);
            }

            if (onlineMapBridge == null)
            {
                onlineMapBridge = FindAnyObjectByType<OnlineMapAircraftBridge>(FindObjectsInactive.Include);
            }

            if (onlineMapBridge != null && _onlineMapForceUpdateMethod == null)
            {
                _onlineMapForceUpdateMethod = onlineMapBridge.GetType().GetMethod("ForceUpdate", BindingFlags.Instance | BindingFlags.Public);
            }

            if (_cesiumGeoreference == null)
            {
                _cesiumGeoreference = FindCesiumGeoreference();
                CacheCesiumMethods();
            }
        }

        private void ApplyAnchor(double latitude, double longitude, float altitudeMeters)
        {
            if (syncGeoProjectionOrigin && geoProjection != null)
            {
                if (setDefaultPositionToAircraft)
                {
                    geoProjection.SetDefaultPosition(latitude, longitude, altitudeMeters);
                }

                geoProjection.SetOrigin(latitude, longitude, altitudeMeters);
            }

            if (syncCesiumGeoreference)
            {
                float cesiumHeight = useAircraftAltitudeForCesium ? altitudeMeters : cesiumReferenceHeightMeters;
                TrySetCesiumOrigin(latitude, longitude, cesiumHeight);
            }

            _onlineMapForceUpdateMethod?.Invoke(onlineMapBridge, null);

            _hasAnchor = true;
            _lastAnchorLatitude = latitude;
            _lastAnchorLongitude = longitude;

            if (verboseLogging)
            {
                Debug.Log($"[XPlane12TerrainSync] Anchored terrain at lat={latitude:F6}, lon={longitude:F6}, alt={altitudeMeters:F1}m");
            }
        }

        private bool NeedsRecentering(double latitude, double longitude)
        {
            if (!_hasAnchor)
            {
                return true;
            }

            double distanceMeters;
            if (geoProjection != null)
            {
                distanceMeters = geoProjection.CalculateDistance(_lastAnchorLatitude, _lastAnchorLongitude, latitude, longitude);
            }
            else
            {
                distanceMeters = HaversineDistanceMeters(_lastAnchorLatitude, _lastAnchorLongitude, latitude, longitude);
            }

            return distanceMeters >= Mathf.Max(1f, recenterDistanceKm) * 1000f;
        }

        private Component FindCesiumGeoreference()
        {
            GameObject go = GameObject.Find(cesiumGeoreferenceObjectName);
            if (go != null)
            {
                return go.GetComponents<MonoBehaviour>()
                    .FirstOrDefault(component => component != null && component.GetType().FullName == "CesiumForUnity.CesiumGeoreference");
            }

            foreach (MonoBehaviour component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (component != null && component.GetType().FullName == "CesiumForUnity.CesiumGeoreference")
                {
                    return component;
                }
            }

            return null;
        }

        private void CacheCesiumMethods()
        {
            if (_cesiumGeoreference == null)
            {
                return;
            }

            Type type = _cesiumGeoreference.GetType();
            _cesiumSetOriginMethod = type.GetMethod(
                "SetOriginLongitudeLatitudeHeight",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(double), typeof(double), typeof(double) },
                null);
            _cesiumMoveOriginMethod = type.GetMethod("MoveOrigin", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private void TrySetCesiumOrigin(double latitude, double longitude, double heightMeters)
        {
            if (_cesiumGeoreference == null)
            {
                return;
            }

            try
            {
                if (_cesiumSetOriginMethod != null)
                {
                    _cesiumSetOriginMethod.Invoke(_cesiumGeoreference, new object[] { longitude, latitude, heightMeters });
                    return;
                }

                bool changed = false;
                changed |= SetNumericMember(_cesiumGeoreference, "latitude", latitude);
                changed |= SetNumericMember(_cesiumGeoreference, "_latitude", latitude);
                changed |= SetNumericMember(_cesiumGeoreference, "longitude", longitude);
                changed |= SetNumericMember(_cesiumGeoreference, "_longitude", longitude);
                changed |= SetNumericMember(_cesiumGeoreference, "height", heightMeters);
                changed |= SetNumericMember(_cesiumGeoreference, "_height", heightMeters);

                if (changed)
                {
                    _cesiumMoveOriginMethod?.Invoke(_cesiumGeoreference, null);
                }
            }
            catch (Exception ex)
            {
                if (verboseLogging)
                {
                    Debug.LogWarning($"[XPlane12TerrainSync] Failed to update Cesium georeference: {ex.Message}");
                }
            }
        }

        private static bool SetNumericMember(object target, string memberName, double value)
        {
            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, Convert.ChangeType(value, property.PropertyType));
                return true;
            }

            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, Convert.ChangeType(value, field.FieldType));
                return true;
            }

            return false;
        }

        private static bool IsValidCoordinate(double latitude, double longitude)
        {
            return !double.IsNaN(latitude) &&
                !double.IsNaN(longitude) &&
                Math.Abs(latitude) <= 90.0 &&
                Math.Abs(longitude) <= 180.0 &&
                (Math.Abs(latitude) > 0.000001 || Math.Abs(longitude) > 0.000001);
        }

        private static double HaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusMeters = 6371000.0;
            double latRad1 = lat1 * Math.PI / 180.0;
            double latRad2 = lat2 * Math.PI / 180.0;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;

            double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(latRad1) * Math.Cos(latRad2) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
            return earthRadiusMeters * c;
        }
    }
}
