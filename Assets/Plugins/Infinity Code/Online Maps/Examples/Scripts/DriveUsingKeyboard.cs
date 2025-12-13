/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsDemos
{
    [AddComponentMenu("Infinity Code/Online Maps/Demos/DriveUsingKeyboard")]
    public class DriveUsingKeyboard : MonoBehaviour
    {
        public GameObject prefab;
        public float markerScale = 5f;
        public float speed;
        public float maxSpeed = 160;
        public float rotation;
        public bool rotateCamera = true;
        public bool centerOnMarker = true;

        private Map map;
        private CameraOrbit cameraOrbit;
        private Marker3D marker;
        private GeoPoint location;

        private void Start()
        {
            map = Map.instance;
            location = map.view.center;
            cameraOrbit = CameraOrbit.instance;

            marker = map.marker3DManager.Create(location, prefab);
            marker.scale = markerScale;
            marker.rotation = rotation;
        }

        private void Update()
        {
            float acc = 0;

            if (InputManager.GetKey(KeyCode.W) || InputManager.GetKey(KeyCode.UpArrow)) acc = 1;
            else if (InputManager.GetKey(KeyCode.S) || InputManager.GetKey(KeyCode.DownArrow)) acc = -1;
            
            if (Mathf.Abs(acc) > 0) speed = Mathf.Lerp(speed, maxSpeed * Mathf.Sign(acc), Time.deltaTime * Mathf.Abs(acc));
            else speed = Mathf.Lerp(speed, 0, Time.deltaTime * 0.1f);

            if (Mathf.Abs(speed) < 0.1) return;

            float r = 0;
            
            if (InputManager.GetKey(KeyCode.A) || InputManager.GetKey(KeyCode.LeftArrow)) r = -1;
            else if (InputManager.GetKey(KeyCode.D) || InputManager.GetKey(KeyCode.RightArrow)) r = 1;
            
            rotation += r * Time.deltaTime * speed;
            location = location.Distant(speed * Time.deltaTime / 3600, rotation + 180);

            marker.rotation = rotation;
            marker.location = location;
            if (centerOnMarker) map.view.center = location;
            if (rotateCamera) cameraOrbit.rotation = new Vector2(cameraOrbit.rotation.x, rotation + 180);
        }
    }
}