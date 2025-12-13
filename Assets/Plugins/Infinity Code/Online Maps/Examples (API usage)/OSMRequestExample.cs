/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Linq;
using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to make a request to Open Street Map Overpass API and handle the response.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "OSMRequestExample")]
    public class OSMRequestExample : MonoBehaviour
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
            
            // Get map corners
            GeoRect r = map.view.rect;

            // Create OSM Overpass request where highway is primary or residential
            string requestData = string.Format(Culture.numberFormat, 
                "node({0},{1},{2},{3});way(bn)[{4}];(._;>;);out;",
                r.bottom, 
                r.left, 
                r.top, 
                r.right, 
                "'highway'~'primary|residential'");

            // Send request and subscribe to complete event
            new OSMOverpassRequest(requestData).
                HandleResult(OnResult). // Subscribe to the result event
                Send(); // Send the request
        }

        /// This event called when the request is completed.
        private void OnResult(OSMOverpassResult result)
        {
            foreach (var pair in result.ways)
            {
                // Log highway type
                Debug.Log(pair.Value.tags.FirstOrDefault(t => t.key == "highway").value);
            }
        }
    }
}