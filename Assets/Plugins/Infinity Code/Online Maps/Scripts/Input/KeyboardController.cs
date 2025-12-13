/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// The class allows you to change the map location using the keyboard and the joystick.
    /// </summary>
    [AddComponentMenu("Infinity Code/Online Maps/Plugins/Keyboard Controller")]
    [Plugin("Keyboard Controller")]
    public class KeyboardController : MonoBehaviour, ISavable
    {
        /// <summary>
        /// Speed of moving the map.
        /// </summary>
        public float speed = 1;

        private CameraOrbit cameraOrbit;
        private bool ignoreChangePosition;
        private Map map;
        private TilePoint centerTile;

        private void OnDisable()
        {
            map.OnLocationChanged -= UpdateTilePosition;
            map.OnZoomChanged -= UpdateTilePosition;

            GetComponent<ControlBase>().OnHandleInteraction -= HandleInteractions;
        }

        private void OnEnable()
        {
            map = GetComponent<Map>();
            cameraOrbit = GetComponent<CameraOrbit>();
            
            centerTile = map.view.centerTile;

            map.OnLocationChanged += UpdateTilePosition;
            map.OnZoomChanged += UpdateTilePosition;

            ControlBase control = GetComponent<ControlBase>();
            control.OnHandleInteraction += HandleInteractions;
            control.OnHandleInteraction += HandleInteractions;
        }

        private void HandleInteractions()
        {
            float latitudeSpeed = InputManager.GetAxis("Vertical") * Time.deltaTime;
            float longitudeSpeed = InputManager.GetAxis("Horizontal") * Time.deltaTime;

            if (Math.Abs(latitudeSpeed) < float.Epsilon && Math.Abs(longitudeSpeed) < float.Epsilon) return;

            if (cameraOrbit)
            {
                Vector3 v = Quaternion.Euler(0, cameraOrbit.rotation.y, 0) * new Vector3(longitudeSpeed, 0, latitudeSpeed);
                longitudeSpeed = v.x;
                latitudeSpeed = v.z;
            }
            
            centerTile.Add(longitudeSpeed * speed, -latitudeSpeed * speed);

            ignoreChangePosition = true;
            map.view.centerTile = centerTile;
            ignoreChangePosition = false;
        }

        private void UpdateTilePosition()
        {
            if (ignoreChangePosition) return;
            centerTile = map.view.centerTile;
        }
    }
}