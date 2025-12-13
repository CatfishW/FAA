/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of a request to Open Route Service.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "OpenRouteServiceExample")]
    public class OpenRouteServiceExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }

            // Looking for pedestrian route between the coordinates.
            new ORSDirectionsRequest(
                    new GeoPoint(8.6817521f, 49.4173462f),
                    new GeoPoint(8.6828883f, 49.4067577f))
                {
                    // Extra params
                    language = "ru",
                    profile = ORSDirectionsRequest.Profile.footWalking
                }.HandleResult(OnResult) // Subscribe to the event of the completion of the request.
                .Send(); // Send the request.
        }

        /// <summary>
        /// This method is called when a response is received.
        /// </summary>
        /// <param name="result">Result object</param>
        private void OnResult(ORSDirectionsResult result)
        {
            if (result == null || result.routes.Length == 0)
            {
                Debug.Log("Open Route Service Directions failed.");
                return;
            }

            // Get the points of the first route.
            List<GeoPoint> points = result.routes[0].points;

            // Draw the route.
            Line line = new Line(points, Color.red);
            map.drawingElementManager.Add(line);

            // Set the map position to the first point of route.
            map.view.center = points[0];
        }
    }
}