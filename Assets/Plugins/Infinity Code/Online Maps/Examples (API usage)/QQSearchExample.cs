/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Search for a POIs, by using QQ search
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "QQSearchExample")]
    public class QQSearchExample : MonoBehaviour
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
            new QQSearchRequest(
                "成都", // keywords
                "北京") // Params of request with region
                {
                    // Additional parameters
                    page_size = 20,
                    page_index = 1
                }.HandleResult(OnResult) // Subscribe to get result
                .Send(); // Send request
        }

        /// <summary>
        /// On request Complete
        /// </summary>
        /// <param name="result">Result of request</param>
        private void OnResult(QQSearchResult result)
        {
            // Validate result and status
            if (result == null || result.status != 0)
            {
                Debug.Log("Something wrong");
                return;
            }

            foreach (QQSearchResult.Data data in result.data)
            {
                // Create a new marker for each POI
                map.marker2DManager.Create(data.location, data.title);
            }
        }
    }
}