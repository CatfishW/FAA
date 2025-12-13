/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of dynamic creating 3d marker, and change the position of 3D marker.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "Marker3D_Example")]
    public class Marker3D_Example : MonoBehaviour
    {
        /// <summary>
        /// Reference to the 3D control (Texture or Tileset). If not specified, the current instance will be used.
        /// </summary>
        public ControlBase3D control;
        
        /// <summary>
        /// Prefab of 3D marker
        /// </summary>
        public GameObject markerPrefab;

        private Marker3D marker3D;

        private void Start()
        {
            // If the control is not specified, get the current instance.
            if (!control && !(control = ControlBase3D.instance))
            {
                Debug.LogError("Control not found");
                return;
            }

            // Marker position. Geographic coordinates.
            GeoPoint markerLocation = new GeoPoint(0, 0);

            // Create 3D marker
            marker3D = control.marker3DManager.Create(markerLocation, markerPrefab);

            // Specifies that marker should be shown only when zoom from 1 to 10.
            marker3D.range = new LimitedRange(1, 10);
        }

        private void OnGUI()
        {
            if (GUI.Button(new Rect(5, 5, 100, 20), "Move Left"))
            {
                // Change the marker coordinates.
                GeoPoint location = marker3D.location;
                location.x += 0.1f;
                marker3D.location = location;
            }
        }
    }
}