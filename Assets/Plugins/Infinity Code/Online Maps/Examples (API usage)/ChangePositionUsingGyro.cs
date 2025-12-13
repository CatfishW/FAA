/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example how to change the position of the map using the gyroscope.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "ChangePositionUsingGyro")]
    public class ChangePositionUsingGyro : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// Speed of moving the map.
        /// </summary>
        public float speed;

        private bool allowDrag;

        private void OnGUI()
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);

            // If the button is pressed, allow movement of map.
            allowDrag = GUI.RepeatButton(new Rect(5, 5, 100, 100), "Drag", style);

            // Log gyroscope rotationRate.
            string rotationRate = Input.gyro.rotationRate.ToString("F4");
            GUI.Label(new Rect(5, Screen.height - 50, Screen.width - 10, 50), rotationRate);
        }

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            // Forbid the user to control the map.
            map.GetComponent<MouseController>().allowUserControl = false;

            // Turn on the gyro.
            Input.gyro.enabled = true;
        }

        private void Update()
        {
            // If the movement is not allowed to return.
            if (!allowDrag) return;

            // Gets rotationRate
            Vector3 rate = Input.gyro.rotationRate;

            // Gets map tile position
            TilePoint tilePoint = map.view.centerTile;

            // Move tile coordinates
            tilePoint.Add(rate.x * speed, rate.y * speed);

            // Converts the tile coordinates to the geographic coordinates.
            GeoPoint location = tilePoint.ToLocation(map);

            // Set map position
            map.view.center = location;
        }
    }
}