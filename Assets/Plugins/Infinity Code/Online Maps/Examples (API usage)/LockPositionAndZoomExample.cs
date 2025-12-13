/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to limit the position and zoom the map.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "LockPositionAndZoomExample")]
    public class LockPositionAndZoomExample : MonoBehaviour
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
            
            // Lock map zoom range
            map.view.zoomRange = new LimitedRange(10, 15);

            // Lock map coordinates range
            map.view.locationRange = new LocationRange(33, -119, 34, -118);

            // Initializes the position and zoom
            map.view.SetCenter(map.view.locationRange.center, 10);
        }
    }
}