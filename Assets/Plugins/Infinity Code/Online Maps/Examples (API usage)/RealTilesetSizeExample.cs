/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to make a map that will be the real world size.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "RealTilesetSizeExample")]
    public class RealTilesetSizeExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map control. If not specified, the current instance will be used.
        /// </summary>
        public TileSetControl control;
        
        private void Start()
        {
            // Get the reference to the map control.
            if (!control && !(control = GetComponent<TileSetControl>()))
            {
                Debug.LogError("TileSetControl not found");
                return;
            }
            
            // Initial resize
            UpdateSize();

            // Subscribe to change zoom
            control.map.OnZoomChanged += OnChangeZoom;
        }

        private void OnChangeZoom()
        {
            UpdateSize();
        }

        private void UpdateSize()
        {
            // Get distance (km) between corners of map
            Vector2d distance = control.map.view.rect.Distances();

            // Set tileset size
            control.sizeInScene = distance * 1000;

            // Redraw map
            control.map.Redraw();
        }
    }
}