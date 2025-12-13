/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to download a tile from WMS that does not support tiles.
    /// Important: if you have a chance to make Tiled WMS - use it, because this way because this path is quite heavy.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "CustomWMSProvider")]
    public class CustomWMSProvider : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// URL pattern for your server
        /// </summary>
        public string url = "http://192.168.0.1:8080/geoserver/tuzla/wms?LAYERS=tuzla&STYLES=&FORMAT=image%2Fpng&SERVICE=WMS&VERSION=1.1.1&REQUEST=GetMap&SRS=EPSG%3A4326&BBOX={lx},{by},{rx},{ty}&WIDTH=256&HEIGHT=256";

        /// <summary>
        /// This method is called when the script starts
        /// </summary>
        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            // Register a new provider and map type
            TileProvider.CreateMapType("mywms.style1", url);

            // Select a new map type
            map.mapType = "mywms.style1";

            // Subscribe to replace token event
            Tile.OnReplaceURLToken += OnReplaceUrlToken;

            map.view.SetCenter(29.254738, 40.8027188, 14);
        }

        /// <summary>
        /// This method will be called for each token in url
        /// </summary>
        /// <param name="tile">The tile for which url is generated</param>
        /// <param name="token">Token to be processed</param>
        /// <returns>Value for token</returns>
        private string OnReplaceUrlToken(Tile tile, string token)
        {
            // If it is a corner token, return a value
            if (token == "ty") return tile.topLeft.y.ToString(Culture.numberFormat);
            if (token == "by") return tile.bottomRight.y.ToString(Culture.numberFormat);
            if (token == "lx") return tile.topLeft.x.ToString(Culture.numberFormat);
            if (token == "rx") return tile.bottomRight.x.ToString(Culture.numberFormat);

            // Otherwise, return null
            return null;
        }
    }
}