/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to handle change of the position and zoom the map.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "ChangePositionAndZoomEventsExample")]
    public class ChangePositionAndZoomEventsExample : MonoBehaviour
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
            
            // Subscribe to change position event.
            map.OnLocationChanged += OnChangePosition;

            // Subscribe to change zoom event.
            map.OnZoomChanged += OnChangeZoom;
        }

        /// <summary>
        /// This method is called when the position of the map is changed.
        /// </summary>
        private void OnChangePosition()
        {
            // When the position changes you will see in the console new map coordinates.
            Debug.Log(map.view.center);
        }

        /// <summary>
        /// This method is called when the zoom of the map is changed.
        /// </summary>
        private void OnChangeZoom()
        {
            // When the zoom changes you will see in the console new zoom.
            Debug.Log(map.view.zoom);
        }
    }
}