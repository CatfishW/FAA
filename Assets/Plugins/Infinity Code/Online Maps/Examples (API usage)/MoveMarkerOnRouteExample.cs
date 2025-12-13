/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Linq;
using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of simulation movement marker on the route.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "MoveMarkerOnRouteExample")]
    public class MoveMarkerOnRouteExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// Start location name
        /// </summary>
        public string fromPlace = "Los Angeles";

        /// <summary>
        /// End location name
        /// </summary>
        public string toPlace = "Hollywood";

        /// <summary>
        /// Speed of movement (km/h).
        /// </summary>
        public float speed = 60;

        /// <summary>
        /// Move map to marker position
        /// </summary>
        public bool lookToMarker = false;

        /// <summary>
        /// Orient marker on next point.
        /// </summary>
        public bool orientMarkerOnNextPoint = false;

        /// <summary>
        /// Reference to marker
        /// </summary>
        private Marker2D marker;

        /// <summary>
        /// Array of route points
        /// </summary>
        private GeoPoint[] points;

        /// <summary>
        /// Current point index
        /// </summary>
        private int pointIndex = -1;

        /// <summary>
        /// Current step progress
        /// </summary>
        private double progress;

        private void Start()
        {
            if (!KeyManager.hasGoogleMaps)
            {
                Debug.LogWarning("Please specify Google API Key");
                return;
            }
            
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }

            // Looking for a route between locations.
            new GoogleRoutingRequest(fromPlace, toPlace).HandleResult(OnResult).Send();
        }

        private void OnResult(GoogleRoutingResult result)
        {
            if (result == null || result.routes.Length == 0)
            {
                Debug.Log("Something wrong");
                return;
            }

            GoogleRoutingResult.Route firstRoute = result.routes[0];
            List<GoogleRoutingResult.RouteLegStep> steps = firstRoute.legs.SelectMany(l => l.steps).ToList();

            // Create a new marker in first point.
            marker = map.marker2DManager.Create(steps[0].startLocation.latLng, "Car");

            // Gets points of route.
            points = firstRoute.polyline.points;

            // Draw the route.
            Line route = new Line(points, Color.red, 3);
            map.drawingElementManager.Add(route);

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

            GeoPoint location;

            if (progress < 1)
            {
                location = GeoPoint.Lerp(p1, p2, progress);
                marker.location = location;

                // Orient marker
                if (orientMarkerOnNextPoint) marker.rotation = 450 - (float)Geometry.Angle2D(p1, p2);
            }
            else
            {
                location = p2;
                marker.location = location;
                pointIndex++;
                progress = 0;
                if (pointIndex >= points.Length - 1)
                {
                    Debug.Log("Finish");
                    pointIndex = -1;
                }
                else
                {
                    // Orient marker
                    if (orientMarkerOnNextPoint) marker.rotation = 450 - (float)Geometry.Angle2D(p2, points[pointIndex + 1]);
                }
            }

            if (lookToMarker) map.view.center = location;
            map.Redraw();
        }
    }
}