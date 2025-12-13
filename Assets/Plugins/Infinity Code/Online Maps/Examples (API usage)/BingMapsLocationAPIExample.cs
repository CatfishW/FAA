/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of a request to Bing Maps Location API, and get the result.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "BingMapsLocationAPIExample")]
    public class BingMapsLocationAPIExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;

        /// <summary>
        /// Search query
        /// </summary>
        public string query = "New York";

        private void Start()
        {
            if (!KeyManager.hasBingMaps)
            {
                Debug.LogError("Bing Maps API Key is missing. Specify the key in Key Manager.");
                return;
            }
            
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            // Looking for a location by name.
            new BingMapsGeocodingRequest(query)
                .HandleResult(OnResult) // Subscribe to OnResult event
                .Send(); // Send the request

            // Subscribe to map click event.
            map.control.OnClick += OnMapClick;
        }

        /// <summary>
        /// This method is called when click on map.
        /// </summary>
        private void OnMapClick()
        {
            // Looking for a location by coordinates.
            new BingMapsReverseGeocodingRequest(map.view.center)
                .HandleResult(OnResult) // Subscribe to OnResult event
                .Send(); // Send the request
        }

        /// <summary>
        /// This method is called when a response is received and results are processed.
        /// </summary>
        /// <param name="results">Array of results</param>
        private static void OnResult(BingMapsLocationResult[] results)
        {
            if (results == null)
            {
                Debug.Log("No results");
                return;
            }

            // Log results info.
            Debug.Log(results.Length);
            foreach (BingMapsLocationResult result in results)
            {
                Debug.Log(result.name);
                Debug.Log(result.formattedAddress);
                foreach (KeyValuePair<string, string> pair in result.address)
                {
                    Debug.Log(pair.Key + ": " + pair.Value);
                }
                Debug.Log("------------------------------");
            }
        }
    }
}