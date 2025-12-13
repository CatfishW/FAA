/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to rotate the camera on a compass.
    /// Requires Tileset Control + Allow Camera Control - ON.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "RotateCameraByCompassExample")]
    public class RotateCameraByCompassExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the camera orbit. If not specified, the current instance will be used.
        /// </summary>
        public CameraOrbit cameraOrbit;

        private void Start()
        {
            // If the camera orbit is not specified, get the current instance.
            if (!cameraOrbit && !(cameraOrbit = CameraOrbit.instance))
            {
                Debug.LogError("CameraOrbit not found");
                return;
            }
            
            // Subscribe to compass event
            UserLocation.instance.OnCompassChanged += OnCompassChanged;
        }

        /// <summary>
        /// This method is called when the compass value is changed.
        /// </summary>
        /// <param name="heading">New compass value (0-360)</param>
        private void OnCompassChanged(float heading)
        {
            // Rotate the camera.
            cameraOrbit.rotation.y = heading;
        }
    }
}