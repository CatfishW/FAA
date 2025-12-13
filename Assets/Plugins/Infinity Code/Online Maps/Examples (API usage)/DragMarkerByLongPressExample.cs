/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to drag the markers by long press. It is convenient for mobile devices.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "DragMarkerByLongPressExample")]
    public class DragMarkerByLongPressExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;

        private MouseController mouseController;
        
        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }

            mouseController = map.GetComponent<MouseController>();
            if (!mouseController)
            {
                Debug.LogError("MouseController not found");
                return;
            }
            
            // Create a new marker.
            Marker2D marker = map.marker2DManager.Create(map.view.center, "My Marker");

            // Subscribe to OnLongPress event.
            marker.OnLongPress += OnMarkerLongPress;
        }

        private void OnMarkerLongPress(Marker marker)
        {
            // Starts moving the marker.
            map.control.marker2DManager.dragMarker = marker;
            mouseController.isMapDrag = false;
        }
    }
}