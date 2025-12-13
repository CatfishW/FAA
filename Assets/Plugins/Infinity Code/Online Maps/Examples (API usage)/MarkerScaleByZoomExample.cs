/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of scaling markers when changing zoom.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "MarkerScaleByZoomExample")]
    public class MarkerScaleByZoomExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// Zoom, when the scale = 1.
        /// </summary>
        public int defaultZoom = 15;

        /// <summary>
        /// Instance of marker.
        /// </summary>
        private Marker marker;

        /// <summary>
        /// Init.
        /// </summary>
        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            // Create a new marker.
            marker = map.marker2DManager.Create(15, 15);

            // Subscribe to change zoom.
            map.OnZoomChanged += OnChangeZoom;

            // Initial rescale marker.
            OnChangeZoom();
        }

        /// <summary>
        /// On change zoom.
        /// </summary>
        private void OnChangeZoom()
        {
            float originalScale = 1 << defaultZoom;
            float currentScale = map.view.maxTiles;

            marker.scale = currentScale / originalScale;
        }
    }
}