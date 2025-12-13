/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Search for a location by name, calculates best position and zoom to show it.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "FindLocationExample")]
    public class FindLocationExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;

        /// <summary>
        /// Add marker at first found location.
        /// </summary>
        public bool addMarker = true;
        
        /// <summary>
        /// Set map position at first found location.
        /// </summary>
        public bool setPosition = true;

        /// <summary>
        /// Set best zoom at first found location.
        /// </summary>
        public bool setZoom = true;

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

            // Start search Chicago.
            new GoogleGeocodingRequest("Chicago")
                .HandleResult(OnResult) // Subscribe to the result event.
                .Send(); // Send the request.
        }

        private void OnResult(GoogleGeocodingResult[] results)
        {
            if (results == null || results.Length == 0)
            {
                Debug.Log("Location not found.");
                return;
            }
            
            GoogleGeocodingResult result = results[0];
            
            // Create a new marker at the position of Chicago.
            if (addMarker) map.marker2DManager.Create(result.geometry_location, "Chicago");

            // Set best zoom
            if (setZoom)
            {
                // Get best zoom
                GeoPoint[] locations =
                {
                    result.geometry_viewport_southwest, 
                    result.geometry_viewport_northeast
                };
                (GeoPoint center, int zoom) centerAndZoom = GeoMath.CenterPointAndZoom(locations);

                // Set map zoom
                map.view.intZoom = centerAndZoom.zoom;
            }

            // Set map position
            if (setPosition) map.location = result.geometry_location;
        }
    }
}