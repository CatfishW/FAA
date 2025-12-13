/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of calculating the amount of clicking on the marker.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "MarkerClickCountExample")]
    public class MarkerClickCountExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map control. If not specified, the current instance will be used.
        /// </summary>
        public ControlBase3D control;
        
        /// <summary>
        /// Prefab of 3D marker
        /// </summary>
        public GameObject prefab;

        private void Start()
        {
            // If the control is not specified, get the current instance.
            if (!control && !(control = ControlBase3D.instance))
            {
                Debug.LogError("Control not found");
                return;
            }
            
            // Create a new markers.
            Marker3D marker1 = control.marker3DManager.Create(0, 0, prefab);
            Marker3D marker2 = control.marker3DManager.Create(10, 0, prefab);

            // Store click count in marker custom data.
            marker1["clickCount"] = 0;
            marker2["clickCount"] = 0;

            // Subscribe to click event.
            marker1.OnClick += OnMarkerClick;
            marker2.OnClick += OnMarkerClick;

            marker1.OnPress += OnPress;
        }

        private void OnPress(Marker onlineMapsMarkerBase)
        {
            Debug.Log("OnPress");
        }

        private void OnMarkerClick(Marker marker)
        {
            int clickCount = marker.GetData<int>("clickCount");
            clickCount++;
            Debug.Log(clickCount);
            marker["clickCount"] = clickCount;
        }
    }    
}