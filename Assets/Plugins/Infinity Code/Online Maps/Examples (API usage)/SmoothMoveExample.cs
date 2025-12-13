/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of a smooth movement to current GPS location.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "SmoothMoveExample")]
    public class SmoothMoveExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// Move duration (sec)
        /// </summary>
        public float time = 3;

        /// <summary>
        /// Relative position (0-1) between from and to
        /// </summary>
        private float angle;

        /// <summary>
        /// Movement trigger
        /// </summary>
        private bool isMovement;

        private GeoPoint fromLocation;
        private GeoPoint toLocation;
        private TilePoint fromTile, toTile;
        private int moveZoom;

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
            // On click button, starts movement
            if (GUI.Button(new Rect(5, 5, 100, 30), "Goto marker"))
            {
                // from current map location
                fromLocation = map.location;

                // to GPS position;
                toLocation = UserLocation.instance.location;

                // calculates tile locations
                moveZoom = map.view.intZoom;
                fromTile = fromLocation.ToTile(map);
                toTile = toLocation.ToTile(map);
                
                // if tile offset < 4, then start smooth movement
                if ((fromTile - toTile).magnitude < 4)
                {
                    // set relative position 0
                    angle = 0;

                    // start movement
                    isMovement = true;
                }
                else // too far
                {
                    map.view.center = toLocation;
                }
            }
        }

        private void Update()
        {
            // if not movement then return
            if (!isMovement) return;

            // update relative position
            angle += Time.deltaTime / time;

            if (angle > 1)
            {
                // stop movement
                isMovement = false;
                angle = 1;
            }

            // Set new position
            TilePoint centerTile = TilePoint.Lerp(fromTile, toTile, angle, angle);
            map.view.SetCenterTile(centerTile);
        }
    }
}