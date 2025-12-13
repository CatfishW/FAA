/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of moving the map, at the approach of a 3D marker to the map border.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "DragMarkerAndTranslateMapExample")]
    public class DragMarkerAndTranslateMapExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map control. If not specified, the current instance will be used.
        /// </summary>
        public ControlBase3D control;
        
        /// <summary>
        /// Prefab of 3D marker.
        /// </summary>
        public GameObject prefab;

        /// <summary>
        /// The minimum speed of movement map.
        /// </summary>
        public float minSpeed = 0;

        /// <summary>
        /// The maximum speed of movement map.
        /// </summary>
        public float maxSpeed = 1;

        /// <summary>
        /// Relative edge of map (0-1).
        /// </summary>
        public float edge = 0.1f;

        private void Start()
        {
            // If control is not specified, use the current instance.
            if (!control && !(control = ControlBase3D.instance))
            {
                Debug.LogError("Control not found");
                return;
            }
            
            // Create a new 3D marker.
            Marker3D marker = control.marker3DManager.Create(control.map.view.center, prefab);

            // Subscribe to OnDrag event.
            marker.OnDrag += OnMarkerDrag;
        }

        private void OnMarkerDrag(Marker marker)
        {
            // Stores the coordinates of the boundaries of the map.
            GeoPoint tl = control.map.view.topLeft;
            GeoPoint br = control.map.view.bottomRight;

            // Fix 180 meridian.
            Vector2d dist = tl - br;
            dist.x *= -1;
            if (dist.x < 0) dist.x += 360;

            Vector2 scale = dist * edge;

            // Calculates offset of map.
            Vector2d offTL = marker.location - tl;
            Vector2d offBR = marker.location - br;

            offTL.y *= -1;
            offBR.x *= -1;

            if (offTL.x < 0) offTL.x += 360;
            if (offBR.x < 0) offBR.x += 360;

            Vector2d mapOffset = new Vector2d();

            if (offTL.x < scale.x) mapOffset.x = -offTL.x * Mathd.Lerp(minSpeed, maxSpeed, 1 - offTL.x / scale.x);
            if (offTL.y < scale.y) mapOffset.y = offTL.y * Mathd.Lerp(minSpeed, maxSpeed, 1 - offTL.y / scale.y);
            if (offBR.x < scale.x) mapOffset.x = offBR.x * Mathd.Lerp(minSpeed, maxSpeed, 1 - offBR.x / scale.x);
            if (offBR.y < scale.y) mapOffset.y = -offBR.y * Mathd.Lerp(minSpeed, maxSpeed, 1 - offBR.y / scale.y);

            // If offset not equal zero, then move the map.
            if (mapOffset != Vector2d.zero) control.map.view.center += mapOffset;
        }
    }
}