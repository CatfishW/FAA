/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsDemos
{
    [AddComponentMenu("Infinity Code/Online Maps/Demos/DriveToPointByRoute")]
    public class DriveToPointByRoute : MonoBehaviour
    {
        public GameObject prefab;
        public float markerScale = 5f;

        public GameObject targetPrefab;
        public float targetScale = 5f;

        public float speed;
        public float rotation = 0;

        private Map map;
        private TileSetControl control;
        private Marker3D marker;
        private Marker3D targetMarker;
        private GeoPoint location;
        private GeoPoint targetLocation;
        private GeoPoint[] points;
        private int pointIndex = -1;
        private double progress;
        private Line route;
        private float targetRotation;

        private void Start()
        {
            control = TileSetControl.instance;
            map = control.map;

            control.OnClick += OnMapClick;

            location = map.view.center;

            marker = control.marker3DManager.Create(location, prefab);
            marker.scale = markerScale;
            marker.rotation = rotation;
        }

        private void OnMapClick()
        {
            targetLocation = control.ScreenToLocation();

            if (targetMarker == null)
            {
                targetMarker = control.marker3DManager.Create(targetLocation, targetPrefab);
                targetMarker.scale = targetScale;
            }
            else targetMarker.location = targetLocation;
            
            rotation = (float) location.AngleInMercator(map, targetLocation) - 90;

            if (!KeyManager.hasGoogleMaps)
            {
                Debug.LogWarning("Please enter Map / Key Manager / Google Maps");
                return;
            }

            new GoogleRoutingRequest(location, targetLocation)
                .HandleResult(OnResult)
                .Send();
        }

        private void OnResult(GoogleRoutingResult result)
        {
            if (result == null || result.routes.Length == 0)
            {
                Debug.Log("No result");
                return;
            }

            points = result.routes[0].polyline.points;
            if (route == null)
            {
                route = new Line(points, Color.green, 3);
                DrawingElementManager.AddItem(route);
            }
            else route.points = points;
            
            pointIndex = 0;
        }

        private void Update()
        {
            if (pointIndex == -1) return;

            // Start point
            GeoPoint p1 = points[pointIndex];

            // End point
            GeoPoint p2 = points[pointIndex + 1];
            
            // Total step distance
            double stepDistance = p1.Distance(p2);
            
            // Total step time
            double totalTime = stepDistance / speed * 3600;

            // Current step progress
            progress += Time.deltaTime / totalTime;

            if (progress < 1)
            {
                marker.location = GeoPoint.Lerp(p1, p2, progress);

                // Orient marker
                targetRotation = (float)p1.AngleInMercator(map, p2) - 90;
            }
            else
            {
                marker.location = p2;
                pointIndex++;
                progress = 0;
                if (pointIndex >= points.Length - 1)
                {
                    Debug.Log("Finish");
                    pointIndex = -1;
                }
                else
                {
                    GeoPoint p3 = points[pointIndex + 1];
                    targetRotation = (float)p2.AngleInMercator(map, p3) - 90;
                }
            }

            marker.rotation = Mathf.LerpAngle(marker.rotation, targetRotation, Time.deltaTime * 10);
            map.view.center = location = marker.location;
        }
    }
}