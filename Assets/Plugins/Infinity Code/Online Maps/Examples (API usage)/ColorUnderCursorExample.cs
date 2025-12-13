/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example how to get the color under the cursor
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "ColorUnderCursorExample")]
    public class ColorUnderCursorExample : MonoBehaviour
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
            
            // Subscribe to OnMapClick
            map.control.OnClick += OnMapClick;
        }

        /// <summary>
        /// This method is called when the map is clicked.
        /// </summary>
        private void OnMapClick()
        {
            // Get the coordinates under the cursor.
            GeoPoint point = map.control.ScreenToLocation();

            // Convert coordinates to tile position
            TilePoint t = point.ToTile(map);

            // Get tile
            Tile tile = map.tileManager.GetTile(map.view.intZoom, (int)t.x, (int)t.y);

            // If the tile exists, but is not yet loaded, take the parent
            while (tile != null && tile.status != TileStatus.loaded)
            {
                tile = tile.parent;
                t = t.parent;
            }

            // If the tile does not exist
            if (tile == null)
            {
                Debug.Log("No loaded tiles under cursor");
                return;
            }

            // Calculate the relative position
            Vector2 fp = t.fractionPart;
            
            Color color = tile.GetPixel(fp.x, 1 - fp.y);
            Debug.Log(color);
        }
    }
}