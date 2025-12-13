/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using System.Linq;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to make the inertia, when you move the map.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "InertiaExample")]
    public class InertiaExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// Deceleration rate (0 - 1).
        /// </summary>
        public float friction = 0.9f;

        private bool isInteract;
        private List<Vector2d> speeds;
        private double rsX;
        private double rsY;
        private TilePoint tilePoint;
        private const int maxSamples = 5;

        private bool isSmoothZoomProceed;
        private bool waitZeroTouches;

        private ControlBase control;
        private TileSetControl tileset;
        private MouseController mouseController;

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            control = map.control;
            tileset = control as TileSetControl;
            mouseController = map.GetComponent<MouseController>();

            // Subscribe to map events
            control.OnPress += OnMapPress;
            control.OnRelease += OnMapRelease;

            // Prevents inertia with smooth zoom.
            if (tileset)
            {
                mouseController.OnSmoothZoomBegin += OnSmoothZoomBegin;
                mouseController.OnSmoothZoomFinish += OnSmoothZoomFinish;
            }

            // Initialize arrays of speed
            speeds = new List<Vector2d>(maxSamples);
        }

        /// <summary>
        /// This method is called when you press on the map.
        /// </summary>
        private void OnMapPress()
        {
            // Get tile coordinates of map
            tilePoint = map.view.centerTile;

            // Is marked, that is the interaction with the map.
            isInteract = true;
        }

        /// <summary>
        /// This method is called when you release on the map.
        /// </summary>
        private void OnMapRelease()
        {
            // Is marked, that ended the interaction with the map.
            isInteract = false;

            // Calculates the average speed.
            Vector2d avgSpeed = speeds.Count > 0 ? speeds.Aggregate((p1, p2) => p1 + p2) / speeds.Count : Vector2d.zero;
            rsX = avgSpeed.x;
            rsY = avgSpeed.y;

            if (waitZeroTouches && InputManager.touchCount == 0)
            {
                waitZeroTouches = false;
                rsX = rsY = 0;
            }

            speeds.Clear();
        }

        private void OnSmoothZoomFinish()
        {
            speeds.Clear();
            rsX = 0;
            rsY = 0;

            isSmoothZoomProceed = false;

            if (InputManager.touchCount != 0) waitZeroTouches = true;
        }

        private void OnSmoothZoomBegin()
        {
            speeds.Clear();
            rsX = 0;
            rsY = 0;
            isSmoothZoomProceed = true;
        }

        private void Update()
        {
            if (isSmoothZoomProceed || waitZeroTouches) return;

            // If there is interaction with the map.
            if (isInteract)
            {
                // Calculates speeds.
                TilePoint tp = map.view.centerTile;
                Vector2d speed = tp - tilePoint;

                int max = map.view.maxTiles;
                int halfMax = max >> 1;
                if (speed.x > halfMax) speed.x -= max;
                else if (speed.x < -halfMax) speed.x += max;

                while (speeds.Count >= maxSamples) speeds.RemoveAt(0);

                speeds.Add(speed);

                tilePoint = tp;
            }
            // If no interaction with the map.
            else if (rsX * rsX + rsY * rsY > 0.001)
            {
                // Continue to move the map with the current speed.
                TilePoint tp = map.view.centerTile;
                tp.Add(rsX, rsY);

                int max = map.view.maxTiles;
                if (tp.x >= max) tp.x -= max;
                else if (tp.x < 0) tp.x += max;

                map.view.centerTile = tp;

                // Reduces the current speed.
                rsX *= friction;
                rsY *= friction;
            }
        }
    }
}