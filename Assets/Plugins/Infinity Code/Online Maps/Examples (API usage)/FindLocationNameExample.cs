/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example how to find the name of the location by coordinates.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "FindLocationNameExample")]
    public class FindLocationNameExample : MonoBehaviour
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
            
            // Subscribe to click event.
            map.control.OnClick += OnMapClick;
        }

        private void OnMapClick()
        {
            // Get the coordinates where the user clicked.
            GeoPoint cursorLocation = map.control.ScreenToLocation();

            // Try to find location name by coordinates.
            new GoogleReverseGeocodingRequest(cursorLocation)
                .HandleResult(OnResult) // Subscribe to get result
                .Send(); // Send request
        }

        /// <summary>
        /// This method is called when a results are received.
        /// </summary>
        /// <param name="results">Result array</param>
        private void OnResult(GoogleGeocodingResult[] results)
        {
            // Log the first address from the results.
            if (results.Length > 0) Debug.Log(results[0].formatted_address);
        }
    }
}