/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to use a texture from Real World Terrain for tiles.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "Texture From Real World Terrain")]
    public class TextureFromRWT : MonoBehaviour
    {
        /// <summary>
        /// Reference to the Tileset Control.
        /// </summary>
        public TileSetControl control;

        /// <summary>
        /// Top latitude of the texture.
        /// </summary>
        public double top;

        /// <summary>
        /// Left longitude of the texture.
        /// </summary>
        public double left;

        /// <summary>
        /// Bottom latitude of the texture.
        /// </summary>
        public double bottom;

        /// <summary>
        /// Right longitude of the texture.
        /// </summary>
        public double right;

        /// <summary>
        /// Texture.
        /// </summary>
        public Texture2D texture;

        /// <summary>
        /// Texture for empty tiles.
        /// </summary>
        public Texture2D emptyTexture;

        /// <summary>
        /// Mercator coordinates of the texture.
        /// </summary>
        private MercatorRect textureRect;

        private void Start()
        {
            // If the control is not specified, get the current instance.
            if (!control && !(control = GetComponent<TileSetControl>()))
            {
                Debug.LogError("TileSetControl not found");
                return;
            }
            
            // Convert coordinates to Mercator.
            MercatorPoint m1 = control.map.view.projection.LocationToMercator(left, top);
            MercatorPoint m2 = control.map.view.projection.LocationToMercator(right, bottom);
            textureRect = new MercatorRect(m1, m2);
            
            // Handle the OnDrawTile event to draw the texture.
            control.OnDrawTile += OnDrawTile;
            
            TileManager.OnStartDownloadTile += OnStartDownloadTile;
        }

        /// <summary>
        /// This event occurs when the map draws a tile.
        /// </summary>
        /// <param name="tile">Tile</param>
        /// <param name="material">Material</param>
        private void OnDrawTile(Tile tile, Material material)
        {
            MercatorRect mapRect = control.map.view.mercatorRect;

            // If the tile is outside the texture, then assign an empty texture.
            if (!mapRect.Intersects(textureRect))
            {
                material.mainTexture = emptyTexture;
                material.mainTextureOffset = Vector2.zero;
                material.mainTextureScale = Vector2.one;
                return;
            }

            // Assign the texture.
            material.mainTexture = texture;
            
            // Calculate the offset and scale of the texture.
            double ox = (mapRect.left - textureRect.left) / textureRect.width;
            double oy = (textureRect.bottom - mapRect.bottom) / textureRect.height;
            double sx = mapRect.width / textureRect.width;
            double sy = mapRect.height / mapRect.height;
            material.mainTextureOffset = new Vector2((float)ox, (float)oy);
            material.mainTextureScale = new Vector2((float)sx, (float)sy);
        }

        /// <summary>
        /// This event occurs when the tile starts loading.
        /// </summary>
        /// <param name="tile">Tile</param>
        private void OnStartDownloadTile(Tile tile)
        {
            // Mark the tile as loaded and assign the texture.
            tile.status = TileStatus.loaded;
            tile.texture = emptyTexture;
        }
    }
}