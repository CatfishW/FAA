/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using System.Linq;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to make drag and zoom inertia for the map.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "DragAndZoomInertia")]
    public class DragAndZoomInertia : MonoBehaviour
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
        private List<double> speedX;
        private List<double> speedY;
        private List<float> speedZ;
        private double rsX;
        private double rsY;
        private float rsZ;
        private TilePoint pt;
        private float pz;
        private const int maxSamples = 5;

        private ControlBase control;

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            control = map.control;

            // Subscribe to map events
            control.OnPress += OnMapPress;
            control.OnRelease += OnMapRelease;

            // Initialize arrays of speed
            speedX = new List<double>(maxSamples);
            speedY = new List<double>(maxSamples);
            speedZ = new List<float>(maxSamples);
        }

        private void FixedUpdate()
        {
            if (isInteract && InputManager.touchCount == 0) isInteract = false;

            // If there is interaction with the map.
            if (isInteract)
            {
                // Calculates speeds.
                TilePoint t = map.view.GetCenterTile(20);

                Vector2d delta = t - pt;
                double cSpeedX = delta.x;
                double cSpeedY = delta.y;
                float cSpeedZ = map.view.zoom - pz;

                int halfMax = 1 << 19;
                int max = 1 << 20;
                if (cSpeedX > halfMax) cSpeedX -= max;
                else if (cSpeedX < -halfMax) cSpeedX += max;

                while (speedX.Count >= maxSamples) speedX.RemoveAt(0);
                while (speedY.Count >= maxSamples) speedY.RemoveAt(0);
                while (speedZ.Count >= maxSamples) speedZ.RemoveAt(0);

                speedX.Add(cSpeedX);
                speedY.Add(cSpeedY);
                speedZ.Add(cSpeedZ);

                pt = t;
                pz = map.view.zoom;
            }
            // If no interaction with the map.
            else if (rsX * rsX + rsY * rsY > 0.01 || rsZ > 0.01)
            {
                // Continue to move the map with the current speed.
                pt.Add(rsX, rsY);

                int max = 1 << 20;
                if (pt.x >= max) pt.x -= max;
                else if (pt.x < 0) pt.x += max;

                map.view.SetCenterTile(pt);
                map.view.zoom += rsZ;

                // Reduces the current speed.
                rsX *= friction;
                rsY *= friction;
                rsZ *= friction;
            }
        }

        /// <summary>
        /// This method is called when you press on the map.
        /// </summary>
        private void OnMapPress()
        {
            // Get tile coordinates of map
            pt = map.view.GetCenterTile(20);
            pz = map.view.zoom;

            // Is marked, that is the interaction with the map.
            isInteract = true;
        }

        /// <summary>
        /// This method is called when you release on the map.
        /// </summary>
        private void OnMapRelease()
        {
            if (InputManager.touchCount != 0) return;

            // Is marked, that ended the interaction with the map.
            isInteract = false;

            // Calculates the average speed.
            rsX = speedX.Count > 0 ? speedX.Average() : 0;
            rsY = speedY.Count > 0 ? speedY.Average() : 0;
            rsZ = speedZ.Count > 0 ? speedZ.Average() : 0;

            speedX.Clear();
            speedY.Clear();
            speedZ.Clear();
        }
    }
}