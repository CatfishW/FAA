/*         INFINITY CODE         */
/*   https://infinity-code.com   */

#if !UNITY_WP_8_1 || UNITY_EDITOR

using System.IO;
using System.Text;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example how to make a runtime caching tiles.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "CacheTilesExample")]
    public class CacheTilesExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        private static StringBuilder builder = new StringBuilder();

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            // Subscribe to the event of success download tile.
            Tile.OnTileDownloaded += OnTileDownloaded;

            // Intercepts requests to the download of the tile.
            TileManager.OnStartDownloadTile += OnStartDownloadTile;
        }

        /// <summary>
        /// Gets the local path for tile.
        /// </summary>
        /// <param name="tile">Reference to tile</param>
        /// <returns>Local path for tile</returns>
        private static string GetTilePath(Tile tile)
        {
            RasterTile rTile = tile as RasterTile;
            
            builder.Length = 0;
            builder.Append(Application.persistentDataPath)
                .Append("/OnlineMapsTileCache/")
                .Append(rTile.mapType.provider.id)
                .Append("/")
                .Append(rTile.mapType.id)
                .Append("/")
                .Append(tile.zoom)
                .Append("/")
                .Append(tile.x)
                .Append("/")
                .Append(tile.y)
                .Append(".png");
            
            return builder.ToString();
        }

        /// <summary>
        /// This method is called when loading the tile.
        /// </summary>
        /// <param name="tile">Reference to tile</param>
        private void OnStartDownloadTile(Tile tile)
        {
            // Get local path.
            string path = GetTilePath(tile);

            // If the tile is cached.
            if (File.Exists(path))
            {
                // Load tile texture from cache.
                Texture2D tileTexture = new Texture2D(256, 256, TextureFormat.RGB24, false);
                tileTexture.LoadImage(File.ReadAllBytes(path));
                tileTexture.wrapMode = TextureWrapMode.Clamp;
                tile.texture = tileTexture;
                
                // Redraw map.
                map.Redraw();
            }
            else
            {
                // If the tile is not cached, download tile with a standard loader.
                TileManager.StartDownloadTile(tile);
            }
        }

        /// <summary>
        /// This method is called when tile is success downloaded.
        /// </summary>
        /// <param name="tile">Reference to tile.</param>
        private void OnTileDownloaded(Tile tile)
        {
            // Get local path.
            string path = GetTilePath(tile);

            // Cache tile.
            FileInfo fileInfo = new FileInfo(path);
            DirectoryInfo directory = fileInfo.Directory;
            if (!directory.Exists) directory.Create();

            File.WriteAllBytes(path, tile.www.bytes);
        }
    }
}

#endif