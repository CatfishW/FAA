/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of detection of water by texture.
    /// This example requires a texture:
    /// https://infinity-code.com/atlas/online-maps/images/mapForDetectWaterBW4.jpg
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "DetectWaterByTextureExample")]
    public class DetectWaterByTextureExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// Color of water on the texture.
        /// </summary>
        public Color32 waterColor = Color.black;

        // Set map 2048x2048, with Read / Write Enabled
        public Texture2D mapForDetectWater;

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
            }
        }

        private void Update()
        {
            // Check if the P key is released
            if (!InputManager.GetKeyUp(KeyCode.P)) return;
            
            // Get the coordinates under the cursor.
            GeoPoint cursorLocation = map.control.ScreenToLocation();
                
            // Check if there is water at this point
            bool hasWater = HasWater(cursorLocation);
            Debug.Log(hasWater ? "Has Water" : "No Water");
        }

        private bool HasWater(GeoPoint location)
        {
            // Convert geo coordinates to tile coordinates
            MercatorPoint mercatorPoint = location.ToMercator(map);

            // Check pixel color
            Color color = mapForDetectWater.GetPixelBilinear((float)mercatorPoint.x, (float)(1 - mercatorPoint.y));
            Debug.Log(mercatorPoint.x + "   " + mercatorPoint.y);

            return color == waterColor;
        }
    }
}