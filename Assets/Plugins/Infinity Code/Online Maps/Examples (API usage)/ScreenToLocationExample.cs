/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of the conversion of screen coordinates into geographic coordinates.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "ScreenToLocationExample")]
    public class ScreenToLocationExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map control. If not specified, the current instance will be used.
        /// </summary>
        public ControlBase control;

        private void Start()
        {
            // If the control is not specified, get the current instance.
            if (!control && !(control = ControlBase.instance))
            {
                Debug.LogError("Control not found");
                return;
            }
        }
        
        private void Update()
        {
            // Screen coordinate of the cursor.
            Vector3 mousePosition = InputManager.mousePosition;

            // Converts the screen coordinates to geographic.
            Vector3 mouseGeoLocation = control.ScreenToLocation(mousePosition);

            // Showing geographical coordinates in the console.
            Debug.Log(mouseGeoLocation);
        }
    }
}