/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of rotation of the camera together with a marker.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "RotateMapInsteadMarkerExample")]
    public class RotateMapInsteadMarkerExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// Reference to the camera orbit. If not specified, the current instance will be used.
        /// </summary>
        public CameraOrbit cameraOrbit;
        
        private Marker2D marker;

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            // If the camera orbit is not specified, get the current instance.
            if (!cameraOrbit && !(cameraOrbit = CameraOrbit.instance))
            {
                Debug.LogError("CameraOrbit not found");
                return;
            }

            // Create a new marker.
            marker = map.marker2DManager.Create(GeoPoint.zero, "Player");

            // Subscribe to UpdateBefore event.
            map.OnUpdateBefore += OnUpdateBefore;
        }

        private void OnUpdateBefore()
        {
            // Update camera rotation
            cameraOrbit.rotation = new Vector2(30, marker.rotation);
        }
    }
}