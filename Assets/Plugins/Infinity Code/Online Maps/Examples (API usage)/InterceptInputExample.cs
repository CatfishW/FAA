/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to override the input, and use the center of screen as the cursor, and Z key as a left mouse button.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "InterceptInputExample")]
    public class InterceptInputExample : MonoBehaviour
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
            
            // Intercepts getting the cursor coordinates.
            InputManager.OnGetInputPosition += OnGetInputPosition;

            // Intercepts getting the number of touches.
            InputManager.OnGetTouchCount += OnGetTouchCount;
        }

        private Vector2 OnGetInputPosition()
        {

#if !UNITY_EDITOR
            // On the device returns center of screen.
            return Camera.main.ViewportToScreenPoint(new Vector3(0.5f, 0.5f, 0));
 #else
            // In the editor returns the coordinates of the mouse cursor.
            return InputManager.mousePosition;
#endif
        }

        private int OnGetTouchCount()
        {
            // If pressed Z, then it will work like the left mouse button.
            return InputManager.GetKey(KeyCode.Z) ? 1 : 0;
        }
    }
}