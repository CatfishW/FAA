/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Smoothes zoom changes on mouse and double touch events.
    /// </summary>
    [Plugin("Smooth Zoom On Mouse Events", true)]
    [AddComponentMenu("Infinity Code/Online Maps/Plugins/Smooth Zoom On Mouse Events")]
    public class SmoothZoomOnMouseEvents : MonoBehaviour, ISavable
    {
        /// <summary>
        /// (Optional) Reference to an instance of the map
        /// </summary>
        [JsonNonSerialized]
        public Map map;

        /// <summary>
        /// Animation duration
        /// </summary>
        public float duration = 0.3f;
        
        /// <summary>
        /// Round the zoom value to an integer
        /// </summary>
        public bool roundToInt = true;

        private MouseController mouseController;
        private float fromZoom;
        private float toZoom;
        private float progress;
        private bool processAnimation;
        private ControlBase control;
        private Vector2 inputPosition;
        private float lastUpdateTime;

        private bool OnValidateZoom(ZoomEvent zoomEvent, float value)
        {
            if (zoomEvent != ZoomEvent.wheel && zoomEvent != ZoomEvent.doubleClick) return true;

            float z = map.view.zoom;
            if (processAnimation) z = toZoom;
            float delta = mouseController.zoomSpeed;
            if (zoomEvent == ZoomEvent.wheel && map.view.zoom > value) delta = -mouseController.zoomSpeed;
            
            if (roundToInt) z = Mathf.RoundToInt(z + delta);
            else z += delta;
            
            StartAnim(z);

            return false;
        }

        private void Start()
        {
            if (!map) map = GetComponent<Map>();
            if (!map) map = Map.instance;

            control = map.control;
            mouseController = map.GetComponent<MouseController>();
            
            if (mouseController) mouseController.OnValidateZoom += OnValidateZoom;
        }

        private void StartAnim(float targetZoom)
        {
            fromZoom = map.view.zoom;
            toZoom = targetZoom;
            if (map.view.zoomRange != null) toZoom = map.view.zoomRange.CheckAndFix(toZoom);
            progress = 0;
            inputPosition = InputManager.mousePosition;
            processAnimation = true;

            lastUpdateTime = Time.time;

            map.OnMapUpdated -= UpdateZoom;
            map.OnMapUpdated += UpdateZoom;

            map.Redraw();
        }

        private void UpdateZoom()
        {
            if (!processAnimation)
            {
                map.OnMapUpdated -= UpdateZoom;
                return;
            }

            progress += (Time.time - lastUpdateTime) / duration;
            lastUpdateTime = Time.time;

            if (progress >= 1)
            {
                progress = 1;
                processAnimation = false;
                map.OnMapUpdated -= UpdateZoom;
            }

            float z = Mathf.Lerp(fromZoom, toZoom, progress);
            if (mouseController.zoomMode == ZoomMode.center) map.view.zoom = z;
            else control.ZoomOnPoint(z - map.view.zoom, inputPosition);
            map.Redraw();
        }
    }
}