/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Search for a POIs, by using AMap search
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "AMapSearchExample")]
    public class AMapSearchExample : MonoBehaviour
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

            // Start a new search
            new AMapTextSearchRequest
                {
                    // Set the search parameters
                    keywords = "北京大学",
                    city = "beijing"
                }.HandleResult(OnResult) // Subscribe to OnResult event
                .Send(); // Send the request
        }

        /// <summary>
        /// This method will be called when the search is completed.
        /// </summary>
        /// <param name="result">Result object</param>
        private void OnResult(AMapSearchResult result)
        {
            // Validate result and status
            if (result == null || result.status != 1) return;

            foreach (AMapSearchResult.POI poi in result.pois)
            {
                // Get POI location
                GeoPoint location = poi.GetLocation();

                // Create a new marker for each POI
                map.marker2DManager.Create(location, poi.name);
            }
        }
    }
}