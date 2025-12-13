/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Searches for places where you can eat in a radius of 5 km from the specified coordinates, creating markers for these places, showing them on the map, and displays the name and coordinates of these locations in the console.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "FindPlacesExample")]
    public class FindPlacesExample : MonoBehaviour
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

            // Makes a request to Google Places API.
            new GooglePlacesNearbyRequest(
                        151.1957362, // Longitude
                        -33.8670522f, // Latitude
                        5000) // Radius
                    {
                        types = "food"
                    }.HandleResult(OnResult)
                .Send();
        }

        /// <summary>
        /// This method is called when a response is received.
        /// </summary>
        /// <param name="results">Array of results</param>
        private void OnResult(GooglePlacesResult[] results)
        {
            // If there is no result
            if (results == null || results.Length == 0)
            {
                Debug.Log("No results");
                return;
            }

            List<Marker2D> markers = new List<Marker2D>();

            foreach (GooglePlacesResult result in results)
            {
                // Log name and location of each result.
                Debug.Log(result.name);
                Debug.Log(result.location);

                // Create a marker at the location of the result.
                Marker2D marker = map.marker2DManager.Create(result.location, result.name);
                markers.Add(marker);
            }

            // Get center point and best zoom for markers
            (GeoPoint center, int zoom) = GeoMath.CenterPointAndZoom(markers.ToArray());

            // Set map position and zoom.
            map.view.SetCenter(center, zoom + 1);
        }
    }
}