/*         INFINITY CODE         */
/*   https://infinity-code.com   */

#if (!UNITY_ANDROID && !UNITY_IPHONE) || UNITY_EDITOR
#define USE_MOUSE_ROTATION
#endif

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Implements camera rotation around the map
    /// </summary>
    [AddComponentMenu("Infinity Code/Online Maps/Plugins/Camera Orbit")]
    [Plugin("Camera Orbit", typeof(ControlBaseDynamicMesh), true)]
    public class CameraOrbit : MonoBehaviour, ISavable
    {
        /// <summary>
        /// Called when the rotation has been changed in any way
        /// </summary>
        public Action OnCameraControl;

        /// <summary>
        /// Called when the rotation has been changed by Input
        /// </summary>
        public Action OnChangedByInput;

        /// <summary>
        /// Point on which the camera is looking
        /// </summary>
        public CameraAdjust adjustTo = CameraAdjust.averageCenter;

        /// <summary>
        /// GameObject on which the camera is looking
        /// </summary>
        public GameObject adjustToGameObject;

        /// <summary>
        /// Distance from point to camera
        /// </summary>
        public float distance = 1000;

        /// <summary>
        /// Maximum camera tilt
        /// </summary>
        public float maxRotationX = 80;

        /// <summary>
        /// Camera rotation (X - tilt, Y - pan)
        /// </summary>
        public Vector2 rotation = Vector2.zero;

        /// <summary>
        /// Camera rotation speed
        /// </summary>
        public Vector2 speed = Vector2.one;

        /// <summary>
        /// Forbid changing tilt (rotation.x)
        /// </summary>
        public bool lockTilt;

        /// <summary>
        /// Forbid changing pan (rotation.y)
        /// </summary>
        public bool lockPan;

        private static CameraOrbit _instance;

        private Map map;
        private ControlBaseDynamicMesh control;
        private MouseController mouseController;

        private bool isCameraControl;
        private Vector2 lastInputPosition;

        /// <summary>
        /// Instance
        /// </summary>
        public static CameraOrbit instance => _instance;
        private Camera activeCamera => control.activeCamera;
        private Vector2 sizeInScene => control.sizeInScene;

        private void LateUpdate() => UpdateCameraPosition();

        private void OnEnable()
        {
            _instance = this;
            map = GetComponent<Map>();
            if (!map) map = Compatibility.FindObjectOfType<Map>();
            control = map.control as ControlBaseDynamicMesh;
        }

        private void Start()
        {
            if (control) control.OnMeshUpdated += UpdateCameraPosition;
        }

        private void Update()
        {
#if USE_MOUSE_ROTATION
            if (InputManager.GetMouseButton(1))
            { 
                Vector2 inputPosition = InputManager.mousePosition;
#else
            if (InputManager.touchCount == 2)
            {
                Vector2 p1 = InputManager.GetTouch(0).position;
                Vector2 p2 = InputManager.GetTouch(1).position;

                Vector2 inputPosition = Vector2.Lerp(p1, p2, 0.5f);
#endif
                if (control.IsCursorOnUIElement(inputPosition)) return;
                
                isCameraControl = true;
                if (lastInputPosition == Vector2.zero) lastInputPosition = inputPosition;
                if (lastInputPosition != inputPosition && lastInputPosition != Vector2.zero)
                {
                    Vector2 offset = lastInputPosition - inputPosition;
                    bool changed = offset.sqrMagnitude > 0 && (!lockPan || !lockTilt);
                    if (!lockTilt) rotation.x -= offset.y / 10f * speed.x;
                    if (!lockPan) rotation.y -= offset.x / 10f * speed.y;

                    if (changed && OnChangedByInput != null) OnChangedByInput();
                }
                lastInputPosition = inputPosition;
            }
            else if (isCameraControl)
            {
                lastInputPosition = Vector2.zero;
                isCameraControl = false;
            }
        }

        /// <summary>
        /// Updates camera position
        /// </summary>
        public void UpdateCameraPosition()
        {
            if (rotation.x > maxRotationX) rotation.x = maxRotationX;
            else if (rotation.x < 0) rotation.x = 0;

            if (!activeCamera) return;

            float rx = 90 - rotation.x;
            if (rx > 89.9) rx = 89.9f;

            double px = Math.Cos(rx * Mathf.Deg2Rad) * distance;
            double py = Math.Sin(rx * Mathf.Deg2Rad) * distance;
            double pz = Math.Cos(rotation.y * Mathf.Deg2Rad) * px;
            px = Math.Sin(rotation.y * Mathf.Deg2Rad) * px;
            
            float yScale = 0;

            Vector3 targetPosition;

            if (adjustTo == CameraAdjust.gameObject && adjustToGameObject != null)
            {
                targetPosition = adjustToGameObject.transform.position;
            }
            else
            {
                targetPosition = map.transform.position;
                Vector3 offset = new Vector3(sizeInScene.x / -2, 0, sizeInScene.y / 2);

                if (ElevationManagerBase.useElevation)
                {
                    GeoRect r = map.view.rect;
                    yScale = ElevationManagerBase.GetElevationScale(r, control.elevationManager);

                    if (adjustTo == CameraAdjust.maxElevationInArea) offset.y = ElevationManagerBase.instance.GetMaxElevation(yScale);
                    else if (adjustTo == CameraAdjust.averageCenter)
                    {
                        float ox = sizeInScene.x / 64;
                        float oz = sizeInScene.y / 64;
                        offset.y = ElevationManagerBase.GetElevation(targetPosition.x, targetPosition.z, yScale, r) * 3;

                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x - ox, targetPosition.z - oz, yScale, r) * 2;
                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x, targetPosition.z - oz, yScale, r) * 2;
                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x + ox, targetPosition.z - oz, yScale, r) * 2;
                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x + ox, targetPosition.z, yScale, r) * 2;
                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x + ox, targetPosition.z + oz, yScale, r) * 2;
                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x, targetPosition.z + oz, yScale, r) * 2;
                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x - ox, targetPosition.z + oz, yScale, r) * 2;
                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x - ox, targetPosition.z, yScale, r) * 2;

                        ox *= 2;
                        oz *= 2;

                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x - ox, targetPosition.z - oz, yScale, r);
                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x, targetPosition.z - oz, yScale, r);
                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x + ox, targetPosition.z - oz, yScale, r);
                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x + ox, targetPosition.z, yScale, r);
                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x + ox, targetPosition.z + oz, yScale,r);
                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x, targetPosition.z + oz, yScale, r);
                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x - ox, targetPosition.z + oz, yScale, r);
                        offset.y += ElevationManagerBase.GetElevation(targetPosition.x - ox, targetPosition.z, yScale, r);

                        offset.y /= 27;
                    }
                    else offset.y = ElevationManagerBase.GetElevation(targetPosition.x, targetPosition.z, yScale, r);
                }

                offset.Scale(map.transform.lossyScale);
            
                targetPosition += map.transform.rotation * offset;
            }

            Vector3 oldPosition = activeCamera.transform.position;
            Vector3 newPosition = map.transform.rotation * new Vector3((float)px,  (float)py, (float)pz) + targetPosition;

            if (ElevationManagerBase.useElevation)
            {
                float y = ElevationManagerBase.GetElevation(newPosition.x, newPosition.z, yScale, map.view.rect);
                if (newPosition.y < y + 200) newPosition.y = y + 200;
            }

            activeCamera.transform.position = newPosition;
            activeCamera.transform.LookAt(targetPosition);

            if (mouseController?.isMapDrag ?? false) mouseController.UpdateLastLocation();
            if (oldPosition != newPosition && OnCameraControl != null) OnCameraControl();
        }
    }
}