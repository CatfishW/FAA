using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace WeatherRadar
{
    /// <summary>
    /// Renders navigation waypoints on the radar display.
    /// Plots waypoints relative to aircraft position with labels.
    /// </summary>
    public class WaypointOverlayRenderer : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private RectTransform displayRect;
        [SerializeField] private Transform waypointsContainer;

        [Header("Prefabs")]
        [SerializeField] private GameObject waypointMarkerPrefab;
        [SerializeField] private GameObject vorMarkerPrefab;
        [SerializeField] private GameObject airportMarkerPrefab;

        [Header("Settings")]
        [SerializeField] private float markerSize = 10f;
        [SerializeField] private bool showLabels = true;
        [SerializeField] private float labelOffset = 12f;

        private WeatherRadarConfig config;
        private WeatherRadarDataProvider dataProvider;
        private Dictionary<string, WaypointMarker> activeMarkers = new Dictionary<string, WaypointMarker>();
        private float currentRange = 40f;

        private class WaypointMarker
        {
            public GameObject gameObject;
            public RectTransform rectTransform;
            public Image markerImage;
            public TextMeshProUGUI label;
            public WaypointType type;
        }

        /// <summary>
        /// Initialize the waypoint renderer
        /// </summary>
        public void Initialize(WeatherRadarConfig radarConfig, WeatherRadarDataProvider provider)
        {
            config = radarConfig;
            dataProvider = provider;

            if (dataProvider != null)
            {
                currentRange = dataProvider.RadarData.currentRange;
                dataProvider.OnRadarDataUpdated += OnRadarDataUpdated;
            }

            // Create container if needed
            if (waypointsContainer == null)
            {
                GameObject container = new GameObject("WaypointsContainer");
                container.transform.SetParent(transform, false);
                waypointsContainer = container.transform;
            }
        }

        private void OnDestroy()
        {
            if (dataProvider != null)
            {
                dataProvider.OnRadarDataUpdated -= OnRadarDataUpdated;
            }

            // Clean up markers
            foreach (var marker in activeMarkers.Values)
            {
                if (marker.gameObject != null)
                {
                    Destroy(marker.gameObject);
                }
            }
            activeMarkers.Clear();
        }

        private void OnRadarDataUpdated(WeatherRadarData data)
        {
            currentRange = data.currentRange;
            UpdateWaypoints(data.waypoints, data.heading);
        }

        /// <summary>
        /// Update waypoint positions
        /// </summary>
        public void UpdateWaypoints(List<RadarWaypointData> waypoints, float aircraftHeading)
        {
            if (waypointsContainer == null || displayRect == null) return;

            // Track which waypoints are still active
            HashSet<string> activeIds = new HashSet<string>();

            float radarRadius = displayRect.rect.width / 2f;

            foreach (var waypoint in waypoints)
            {
                // Skip waypoints outside range
                if (waypoint.distance > currentRange) continue;

                activeIds.Add(waypoint.identifier);

                // Get or create marker
                WaypointMarker marker = GetOrCreateMarker(waypoint.identifier, waypoint.type);

                // Calculate position on radar
                // Bearing is relative to north, adjust for aircraft heading
                float adjustedBearing = waypoint.bearing - aircraftHeading;
                float distanceRatio = waypoint.distance / currentRange;
                float pixelDistance = distanceRatio * radarRadius;

                // Convert bearing to position (0 = north/up)
                float angleRad = (90f - adjustedBearing) * Mathf.Deg2Rad;
                float x = Mathf.Cos(angleRad) * pixelDistance;
                float y = Mathf.Sin(angleRad) * pixelDistance;

                // Update marker position
                marker.rectTransform.anchoredPosition = new Vector2(x, y);

                // Update label
                if (marker.label != null && showLabels)
                {
                    marker.label.text = waypoint.identifier;
                    marker.label.gameObject.SetActive(true);
                }

                marker.gameObject.SetActive(true);
            }

            // Hide markers for waypoints no longer in list
            foreach (var kvp in activeMarkers)
            {
                if (!activeIds.Contains(kvp.Key))
                {
                    kvp.Value.gameObject.SetActive(false);
                }
            }
        }

        private WaypointMarker GetOrCreateMarker(string id, WaypointType type)
        {
            if (activeMarkers.TryGetValue(id, out WaypointMarker existing))
            {
                return existing;
            }

            // Create new marker
            WaypointMarker marker = new WaypointMarker();
            marker.type = type;

            // Select prefab based on type
            GameObject prefab = GetPrefabForType(type);
            
            if (prefab != null)
            {
                marker.gameObject = Instantiate(prefab, waypointsContainer);
            }
            else
            {
                // Create default marker
                marker.gameObject = CreateDefaultMarker(type);
            }

            marker.rectTransform = marker.gameObject.GetComponent<RectTransform>();
            if (marker.rectTransform == null)
            {
                marker.rectTransform = marker.gameObject.AddComponent<RectTransform>();
            }

            marker.markerImage = marker.gameObject.GetComponent<Image>();
            marker.label = marker.gameObject.GetComponentInChildren<TextMeshProUGUI>();

            // Apply size
            marker.rectTransform.sizeDelta = new Vector2(markerSize, markerSize);

            // Apply color from config
            if (marker.markerImage != null)
            {
                marker.markerImage.color = GetColorForType(type);
            }

            activeMarkers[id] = marker;
            return marker;
        }

        private GameObject GetPrefabForType(WaypointType type)
        {
            switch (type)
            {
                case WaypointType.VOR:
                    return vorMarkerPrefab;
                case WaypointType.Airport:
                    return airportMarkerPrefab;
                default:
                    return waypointMarkerPrefab;
            }
        }

        private GameObject CreateDefaultMarker(WaypointType type)
        {
            GameObject markerObj = new GameObject($"Waypoint_{type}");
            markerObj.transform.SetParent(waypointsContainer, false);

            // Add RectTransform
            RectTransform rect = markerObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(markerSize, markerSize);

            // Add Image component
            Image img = markerObj.AddComponent<Image>();
            img.color = GetColorForType(type);

            // Add label
            if (showLabels)
            {
                GameObject labelObj = new GameObject("Label");
                labelObj.transform.SetParent(markerObj.transform, false);

                RectTransform labelRect = labelObj.AddComponent<RectTransform>();
                labelRect.anchoredPosition = new Vector2(labelOffset, labelOffset);
                labelRect.sizeDelta = new Vector2(60, 15);

                TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
                label.fontSize = config != null ? config.waypointFontSize : 10;
                label.color = GetColorForType(type);
                label.alignment = TextAlignmentOptions.Left;
            }

            return markerObj;
        }

        private Color GetColorForType(WaypointType type)
        {
            if (config == null)
            {
                return type == WaypointType.VOR
                    ? new Color(0.8f, 0.8f, 1f, 1f)
                    : new Color(0f, 1f, 1f, 1f);
            }

            switch (type)
            {
                case WaypointType.VOR:
                    return config.vorColor;
                case WaypointType.Airport:
                    return config.airportColor;
                default:
                    return config.waypointColor;
            }
        }

        /// <summary>
        /// Add a waypoint to display
        /// </summary>
        public void AddWaypoint(string id, float bearing, float distance, WaypointType type)
        {
            if (dataProvider != null)
            {
                dataProvider.AddWaypoint(new RadarWaypointData
                {
                    identifier = id,
                    bearing = bearing,
                    distance = distance,
                    type = type
                });
            }
        }

        /// <summary>
        /// Clear all waypoints
        /// </summary>
        public void ClearWaypoints()
        {
            foreach (var marker in activeMarkers.Values)
            {
                marker.gameObject.SetActive(false);
            }

            if (dataProvider != null)
            {
                dataProvider.ClearWaypoints();
            }
        }

        /// <summary>
        /// Set visibility
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (waypointsContainer != null)
            {
                waypointsContainer.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Set range for distance calculations
        /// </summary>
        public void SetRange(float rangeNM)
        {
            currentRange = rangeNM;
        }
    }
}
