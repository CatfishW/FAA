/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Linq;
using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example how to calculate the distance and the duration of the route
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "DistanceAndDurationExample")]
    public class DistanceAndDurationExample : MonoBehaviour
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

            if (!KeyManager.hasGoogleMaps)
            {
                Debug.LogWarning("Please specify Google API Key");
                return;
            }

            // Find route using Google Routing API
            // Origin and destination can be specified as coordinates or addresses
            string origin = "Los Angeles";
            GeoPoint destination = new GeoPoint(-118.178960, 35.063995);
            
            // Create a new request to Google Routing API
            new GoogleRoutingRequest(origin, destination)
                .HandleResult(OnResult) // Subscribe to the result event
                .Send(); // Send the request
        }

        /// <summary>
        /// This method is called when the response from Google Routing API is received and parsed.
        /// </summary>
        /// <param name="result">Result object</param>
        private void OnResult(GoogleRoutingResult result)
        {
            if (result == null || result.routes.Length == 0) return;

            // Get the first route
            GoogleRoutingResult.Route route = result.routes[0];

            // Draw route on the map
            Line line = new Line(route.polyline.points, Color.red, 3);
            map.drawingElementManager.Add(line);

            // Calculate the distance
            int distance = route.distanceMeters; // meters

            // Calculate the duration
            int duration = route.durationSec; // seconds

            // Log distance and duration
            Debug.Log("Distance: " + distance + " meters, or " + (distance / 1000f).ToString("F2") + " km");
            Debug.Log("Duration: " + duration + " sec, or " + (duration / 60f).ToString("F1") + " min, or " + (duration / 3600f).ToString("F1") + " hours");
        }
    }
}
