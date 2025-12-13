/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using System.Linq;
using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of a request to HERE Routing API.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "HereRoutingAPIExample")]
    public class HereRoutingAPIExample : MonoBehaviour
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

            // Coordinates of the start and end points.
            GeoPoint origin = new GeoPoint(37.38589, 55.90042);
            GeoPoint destination = new GeoPoint(37.6853002, 55.8635228);

            // Additional parameters for the request.
            Dictionary<string, string> extra = new Dictionary<string, string>
            {
                { "transportMode", "bus" },
                { "lang", "ru-ru" },
                { "alternatives", "3" },
                { "return", "polyline,actions,instructions" }
            };

            // Looking for public transport route between the coordinates.
            new HereRoutingRequest(origin, destination)
                {
                    extra = extra
                }.HandleResult(OnResult) // Subscribe to the event of the completion of the request.
                .Send(); // Send the request.
        }

        /// <summary>
        /// This method is called when a response is received.
        /// </summary>
        /// <param name="result">Result object</param>
        private void OnResult(HereRoutingResult result)
        {
            if (result == null)
            {
                Debug.Log("Request failed");
                return;
            }

            Color[] colors =
            {
                Color.green,
                Color.red,
                Color.blue,
                Color.magenta
            };
            int colorIndex = 0;

            // Draw all the routes in different colors.
            foreach (HereRoutingResult.Route route in result.routes)
            {
                foreach (HereRoutingResult.Section section in route.sections)
                {
                    if (section.polylinePoints == null) continue;

                    DrawingElement line = new Line(section.polylinePoints.Cast<GeoPoint>().ToArray(), colors[colorIndex++]);
                    map.drawingElementManager.Add(line);
                }
            }
        }
    }
}