/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of get elevation value in the coordinate using Google Elevation API.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "GetElevationExample")]
    public class GetElevationExample : MonoBehaviour
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
            
            // Subscribe to click on map event.
            map.control.OnClick += OnMapClick;
        }

        private void OnMapClick()
        {
            // Get the coordinates where the user clicked.
            GeoPoint location = map.control.ScreenToLocation();

            // Get elevation on click point
            new GoogleElevationRequest(location)
                .HandleResult(OnResult) // Subscribe to the result event
                .Send(); // Send request
        }

        private void OnResult(GoogleElevationResult[] results)
        {
            if (results == null)
            {
                // If results is null log message
                Debug.Log("Null result");
            }
            else
            {
                // Shows first result elevation
                Debug.Log(results[0].elevation);
            }
        }
    }
}