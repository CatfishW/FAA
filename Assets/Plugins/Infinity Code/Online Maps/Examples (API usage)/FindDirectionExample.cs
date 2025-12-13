/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Search a route between two locations and draws the route.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "FindDirectionExample")]
    public class FindDirectionExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;

        private void Start()
        {
            // Check Google API Key
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

            // Begin to search a route from Los Angeles to the specified coordinates.
            new GoogleRoutingRequest
                (
                    "Los Angeles", // origin (address or location)
                    new GeoPoint(-118.178960f, 35.063995f) // destination (address or location)
                ).HandleResult(OnResult) // Subscribe to the result event.
                .Send(); // Send the request.
        }

        private void OnResult(GoogleRoutingResult result)
        {
            // Check that the result is not null, and the number of routes is not zero.
            if (result == null || result.routes.Length == 0)
            {
                Debug.Log("Find direction failed");
                return;
            }

            // Showing the console instructions for each step.
            foreach (GoogleRoutingResult.RouteLeg leg in result.routes[0].legs)
            {
                foreach (GoogleRoutingResult.RouteLegStep step in leg.steps)
                {
                    Debug.Log(step.navigationInstruction.instructions);
                }
            }

            // Create a line, on the basis of points of the route.
            Line route = new Line(result.routes[0].polyline.points, Color.green);

            // Add the line route on the map.
            map.drawingElementManager.Add(route);
        }
    }
}