/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to get the elevations in the area using Bing Maps Elevation API.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "BingMapsElevationsExample")]
    public class BingMapsElevationsExample : MonoBehaviour
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
            
            // Get the coordinates of the map corners
            GeoRect viewRect = map.view.rect;

            // Create a new request and subscribe to OnComplete event
            new BingMapsBoundsElevationRequest(viewRect, 32, 32)
                .HandleResult(OnResult) // Subscribe to OnResult event
                .Send(); // Send the request
        }

        /// <summary>
        /// On result received
        /// </summary>
        /// <param name="result">Result object</param>
        private void OnResult(BingMapsElevationResult result)
        {
            // Log elevations length
            if (result != null) Debug.Log(result.resourceSets[0].resources[0].elevations.Length);
            else Debug.Log("Result is null");
        }
    }
}