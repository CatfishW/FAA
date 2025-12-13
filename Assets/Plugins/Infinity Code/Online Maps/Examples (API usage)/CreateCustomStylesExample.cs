/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to dynamically create custom styles
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "CreateCustomStylesExample")]
    public class CreateCustomStylesExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// URL of the first style.
        /// </summary>
        public string style1 = "https://a.tiles.mapbox.com/v4/mapbox.satellite/{zoom}/{x}/{y}.png?access_token=";
        
        /// <summary>
        /// URL of the second style.
        /// </summary>
        public string style2 = "https://a.tiles.mapbox.com/v4/mapbox.streets/{zoom}/{x}/{y}.png?access_token=";
        
        /// <summary>
        /// Mapbox Access Token
        /// </summary>
        public string mapboxAccessToken;

        /// <summary>
        /// Indicates which style is currently used.
        /// </summary>
        private bool useFirstStyle = true;

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            // Create a new provider
            TileProvider.Create("myprovider").AppendTypes(
                // Create a new map types
                new MapType("style1")
                {
                    urlWithLabels = style1 + mapboxAccessToken
                }
            );
            
            // Another way to create a map type
            TileProvider.CreateMapType("myprovider.style2", style2 + mapboxAccessToken);
            
            // Get a provider
            TileProvider provider = TileProvider.Get("myprovider");
            Debug.Log($"Provider: {provider.title}, count types: {provider.types.Length}");

            // Select map type
            map.mapType = "myprovider.style1";
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Change Style"))
            {
                useFirstStyle = !useFirstStyle;
                
                // Switch map type
                map.mapType = "myprovider.style" + (useFirstStyle ? "1" : "2");
            }
        }
    }
}