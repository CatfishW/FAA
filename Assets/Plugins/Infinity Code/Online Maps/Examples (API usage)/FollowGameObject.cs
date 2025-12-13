/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// How to make the map follow GameObject
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "FollowGameObject")]
    public class FollowGameObject : MonoBehaviour
    {
        /// <summary>
        /// Reference to the control. If not specified, the current instance will be used.
        /// </summary>
        public TileSetControl control;

        /// <summary>
        /// GameObject to be followed by the map
        /// </summary>
        public GameObject target;

        /// <summary>
        /// Last position of GameObject
        /// </summary>
        private Vector3 lastPosition;

        /// <summary>
        /// Reference to the map
        /// </summary>
        private Map map;

        private MouseController mouseController;

        /// <summary>
        /// Last tile position of the center of the map
        /// </summary>
        private TilePoint tilePoint;

        private void Start()
        {
            // If the control is not specified, get the current instance.
            if (!control && !(control = TileSetControl.instance))
            {
                Debug.LogError("Control not found");
                return;
            }
            
            // Set a reference to the map and control
            map = control.map;

            mouseController = map.GetComponent<MouseController>();

            // Disable the movement and zoom of the map with the mouse
            mouseController.allowUserControl = false;
            mouseController.allowZoom = false;

            // Subscribe to change zoom event
            map.OnZoomChanged += OnChangeZoom;

            // Store tile position of the center of the map
            tilePoint = map.view.centerTile;

            // Initial update the map
            UpdateMap();
        }

        /// <summary>
        /// This method is called when the zoom changes (in this case, using a script or inspector)
        /// </summary>
        private void OnChangeZoom()
        {
            // Store tile position of the center of the map
            tilePoint = map.view.centerTile;
        }

        private void Update()
        {
            // If GameObject position has changed, update the map
            if (target.transform.position != lastPosition) UpdateMap();
        }

        /// <summary>
        /// Updates map position
        /// </summary>
        private void UpdateMap()
        {
            // Store last position of GameObject
            lastPosition = target.transform.position;

            // Size of map in scene
            Vector2 size = control.sizeInScene;

            // Calculate offset (in tile position)
            Quaternion rotation = map.transform.rotation;
            Vector3 offset = rotation * (lastPosition - map.transform.position - control.center);
            offset.x = offset.x / size.x * map.view.countTilesX * map.view.zoomFactor;
            offset.z = offset.z / size.y * map.view.countTilesY * map.view.zoomFactor;

            // Calculate current tile position of the center of the map
            tilePoint.Add(-offset.x, offset.z);

            // Set position of the map center
            if (Mathf.Abs(offset.x) > float.Epsilon || Mathf.Abs(offset.z) > float.Epsilon) map.view.centerTile = tilePoint;

            // Update map GameObject position
            map.transform.position = lastPosition - rotation * control.center;
        }
    }
}