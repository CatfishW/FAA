/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of an animated marker moving between locations.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "MoveMarkersExample")]
    public class MoveMarkersExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// Time of movement between locations.
        /// </summary>
        public float time = 10;

        private Marker2D marker;

        private GeoPoint fromPosition;
        private GeoPoint toPosition;

        /// <summary>
        /// Relative position (0-1) between from and to
        /// </summary>
        private float angle = 0.5f;

        /// <summary>
        /// Move direction
        /// </summary>
        private int direction = 1;

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            // Create a new marker.
            marker = map.marker2DManager.Create(map.view.center);
            fromPosition = map.view.topLeft;
            toPosition = map.view.bottomRight;
        }

        private void Update()
        {
            angle += Time.deltaTime / time * direction;
            if (angle > 1)
            {
                angle = 2 - angle;
                direction = -1;
            }
            else if (angle < 0)
            {
                angle *= -1;
                direction = 1;
            }

            // Set new position
            marker.location = GeoPoint.Lerp(fromPosition, toPosition, angle);

            // Marks the map should be redrawn.
            // Map is not redrawn immediately. It will take some time.
            map.Redraw();
        }
    }
}