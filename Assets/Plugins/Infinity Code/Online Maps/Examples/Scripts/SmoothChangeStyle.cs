using System.Collections.Generic;
using OnlineMaps;
using UnityEngine;
using Cache = OnlineMaps.Cache;

namespace OnlineMapsDemos
{
    /// <summary>
    /// Smoothly transitions between different map styles.
    /// </summary>
    [AddComponentMenu("Infinity Code/Online Maps/Demos/SmoothChangeStyle")]
    public class SmoothChangeStyle : MonoBehaviour
    {
        /// <summary>
        /// (Optional) The control for the Online Maps.
        /// </summary>
        public TileSetControl control;

        /// <summary>
        /// The duration for the transition.
        /// </summary>
        public float duration = 1;

        // Cache for the map tiles
        private Cache cache;

        /// <summary>
        /// Reference to the map
        /// </summary>
        private Map map;

        // Overlay textures for the map tiles
        private Dictionary<ulong, Texture2D> overlayTextures;

        // Progress of the transition
        private float progress = 1;

        // The target map type for the transition
        private MapType targetMapType;
        
        private Dictionary<ulong, WebRequest> activeRequests = new Dictionary<ulong, WebRequest>();

        /// <summary>
        /// Finishes the transition by updating the map type and setting up tile preloading.
        /// </summary>
        private void FinishTransition()
        {
            // Unsubscribe from the OnUpdateBefore event
            map.OnUpdateBefore -= FinishTransition;

            foreach (Tile tile in map.tileManager.tiles)
            {
                tile.overlayBackTexture = null;
            }

            foreach (KeyValuePair<ulong, WebRequest> pair in activeRequests)
            {
                pair.Value.Dispose();
            }
            
            // Subscribe to the OnPreloadTiles event
            TileManager.OnPreloadTiles += OnPreloadTiles;
            
            // Set the new map type
            progress = 1;
            
            map.activeType = targetMapType;
            
            map.tileManager.Reset();
            map.RedrawImmediately();
        }

        /// <summary>
        /// Handles preloading of tiles.
        /// </summary>
        private void OnPreloadTiles(TileManager tileManager)
        {
            if (tileManager.tiles.Count == 0) return;
            
            // Unsubscribe from the OnPreloadTiles event
            TileManager.OnPreloadTiles -= OnPreloadTiles;
            
            if (overlayTextures == null) return;
            
            // Iterate through all tiles
            foreach (Tile tile in tileManager.tiles)
            {
                // If the texture is in the cache, set it to a tile
                Texture2D texture;
                if (!overlayTextures.TryGetValue(tile.key, out texture)) continue;
                
                tile.texture = texture;
                tile.status = TileStatus.loaded;
            }
            
            overlayTextures = null;
        }

        /// <summary>
        /// Handles completion of WWW requests for map tiles.
        /// </summary>
        private void OnWWWComplete(WebRequest www)
        {
            // Get the tile from the request
            Tile tile = www.GetData<Tile>("tile");
            if (tile == null) return;

            activeRequests.Remove(tile.key);
            
            // If there is an error, display it in the console
            if (www.hasError)
            {
                Debug.LogError(www.error);
                return;
            }
            
            if (overlayTextures == null) return;

            // Create a texture from the downloaded image
            Texture2D texture = new Texture2D(1, 1);
            www.LoadImageIntoTexture(texture);
            
            // Set wrap mode to work around seams between tiles
            texture.wrapMode = TextureWrapMode.Clamp;
            
            // Set the texture as the overlay texture for the tile
            tile.overlayBackTexture = texture;
            
            // Cache the texture to restore it immediately after finishing the transition
            overlayTextures[tile.key] = texture;

            // Add texture to the file cache
            cache?.SetTileTexture(tile, targetMapType, texture);
            
            // Redraw the map
            map.Redraw();
        }

        /// <summary>
        /// Sets the map style to the specified style name.
        /// </summary>
        public void SetStyle(string styleName)
        {
            // Find the map type
            targetMapType = TileProvider.FindMapType(styleName);
            
            // If the map type is not found, display an error message
            if (targetMapType == null)
            {
                Debug.LogError("Can not find map type: " + styleName);
                return;
            }

            // If the target map type is the same as the current active type, do nothing
            if (map.activeType == targetMapType) return;

            // If duration is 0, then immediately change the map type
            // Otherwise, start the transition
            if (duration <= 0) map.activeType = targetMapType;
            else StartTransition();
        }

        /// <summary>
        /// Unity's Start method, used for initialization.
        /// </summary>
        private void Start()
        {
            // Initialize the control
            if (!control)
            {
                control = TileSetControl.instance;
                if (!control)
                {
                    Debug.LogError("Can not find TileSet.");
                    return;
                }
            }

            map = control.map;

            // Initialize the cache
            cache = Cache.instance;
        }

        /// <summary>
        /// Starts the transition between map styles.
        /// </summary>
        private void StartTransition()
        {
            // Reset the progress
            progress = 0;
            
            // Clear the overlay textures
            overlayTextures = new Dictionary<ulong, Texture2D>();

            // Iterate through all the map tiles
            List<Tile> tiles = map.tileManager.tiles;
            for (int i = 0; i < tiles.Count; i++)
            {
                // Get the tile
                Tile tile = tiles[i];
                
                // Reset the alpha channel of the overlay texture
                tile.overlayBackAlpha = 0;

                // If cache exists, try to get the texture from it
                if (cache)
                {
                    Texture2D texture;
                    if (cache.GetTileTexture(tile, targetMapType, out texture))
                    {
                        // Set the texture to the back overlay
                        tile.overlayBackTexture = texture;
                        
                        // Cache the texture to restore it immediately after finishing the transition
                        overlayTextures[tile.key] = texture;
                        continue;
                    }
                }

                // Reset the overlay texture
                tile.overlayBackTexture = null;
                
                // Create a new WWW request for the tile
                string url = targetMapType.GetURL(tile);
                WebRequest www = new WebRequest(url);
                
                // Store the tile in the WWW request to use it in the callback
                www["tile"] = tile;
                activeRequests[tile.key] = www;
                
                // Subscribe to the completion of the request
                www.OnComplete += OnWWWComplete;
            }
        }

        /// <summary>
        /// Unity's Update method, used for frame-by-frame updates.
        /// </summary>
        private void Update()
        {
            // If the transition is complete, we do not need to do anything.
            if (progress >= 1) return;
            
            // Update the progress of the transition.
            progress += Time.deltaTime / duration;
            
            // If the transition is complete, we need to finish it.
            if (progress >= 1)
            {
                progress = 1;
                
                // Delay the completion of the transition until the next frame.
                // This is necessary to make that changing the map type and initializing the tiles be performed in the same frame.
                map.OnUpdateBefore += FinishTransition;
            }
            
            // Update the alpha channel of the overlay textures.
            foreach (Tile tile in map.tileManager.tiles)
            {
                tile.overlayBackAlpha = progress;
            }
            
            // Redraw the map.
            map.Redraw();
        }
    }
}