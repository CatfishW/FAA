/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using OnlineMaps;
using UnityEngine;
using Cache = OnlineMaps.Cache;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to make the overlay from MapTiler tiles.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "TilesetMapTilerOverlayExample")]
    public class TilesetMapTilerOverlayExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        // Overlay transparency
        [Range(0, 1)]
        public float alpha = 1;

        private float _alpha = 1;

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            if (Cache.instance)
            {
                // Subscribe to the cache events.
                Cache.instance.OnLoadedFromCache += LoadTileOverlay;
            }

            // Subscribe to the tile download event.
            TileManager.OnStartDownloadTile += OnStartDownloadTile;
        }

        private static void LoadTileOverlay(Tile tile)
        {
            // Load overlay for tile from Resources.
            string path = $"OnlineMapsOverlay/{tile.zoom}/{tile.x}/{tile.y}";
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture)
            {
                tile.overlayBackTexture = Instantiate(texture);
                Resources.UnloadAsset(texture);
            }
        }

        private void OnStartDownloadTile(Tile tile)
        {
            // Load overlay for tile.
            LoadTileOverlay(tile);

            // Load the tile using a standard loader.
            TileManager.StartDownloadTile(tile);
        }

        private void Update()
        {
            // Update the transparency of overlay.
            if (Math.Abs(_alpha - alpha) > float.Epsilon)
            {
                _alpha = alpha;
                foreach (Tile tile in map.tileManager.tiles) tile.overlayBackAlpha = alpha;
            }
        }
    }
}