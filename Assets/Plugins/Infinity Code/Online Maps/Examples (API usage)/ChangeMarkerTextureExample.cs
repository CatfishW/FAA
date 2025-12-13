/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of a dynamic change texture of 2D marker.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "ChangeMarkerTextureExample")]
    public class ChangeMarkerTextureExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// New texture for markers. Must have "Read / Write Enabled - ON".
        /// </summary>
        public Texture2D newMarkerTexture;

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
            }
        }

        private void OnGUI()
        {
            // When you click on ...
            if (GUI.Button(new Rect(10, 10, 100, 20), "Change markers"))
            {
                // ... all markers will change the texture.
                foreach (Marker2D marker in map.marker2DManager)
                {
                    marker.texture = newMarkerTexture;
                    marker.Init();
                }

                // Redraw map
                map.Redraw();
            }
        }
    }
}