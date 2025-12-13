/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of interception requests to download tiles
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "CustomDownloadTileExample")]
    public class CustomDownloadTileExample : MonoBehaviour
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

            // Subscribe to the tile download event.
            TileManager.OnStartDownloadTile += OnStartDownloadTile;
        }

        private void OnStartDownloadTile(Tile tile)
        {
            // Note: create a texture only when you are sure that the tile exists.
            // Otherwise, you will get a memory leak.
            Texture2D tileTexture = new Texture2D(256, 256);

            // Here your code to load tile texture from any source.
            
            // Note: If the tile will load asynchronously, set
            // tile.status = OnlineMapsTileStatus.loading;
            // Otherwise, the map will try to load the tile again and again.
            
            // Set the texture to the tile.
            tile.texture = tileTexture;

            // Note: If the tile does not exist or an error occurred, set
            // tile.status = OnlineMapsTileStatus.error;
            // Otherwise, the map will try to load the tile again and again.

            // Redraw map (using best redraw type)
            map.Redraw();
        }
    }
}