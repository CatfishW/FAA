/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsDemos
{
    [AddComponentMenu("Infinity Code/Online Maps/Demos/Aircraft")]
    public class Aircraft : MonoBehaviour
    {
        private const float MaxTilt = 50;
        public GameObject container;
        public float altitude = 1000; // meters
        public float speed = 900; // km/h
        public Vector3 cameraOffset = new Vector3(-10, -3, 0);

        public float tiltSpeed = 1;
        public float altitudeChangeSpeed = 100;
        public AnimationCurve altitudeZoomCurve = AnimationCurve.Linear(0, 19, 1, 13);
        public float maxAltitude = 4000; // meters

        private GeoPoint location;
        public float tilt = 0;

        public float rotateSpeed = 1;
        
        private Map map;
        private ElevationManagerBase elevationManager;
        private TileSetControl control;

        private void Start()
        {
            control = TileSetControl.instance;
            map = control.map;
            elevationManager = control.elevationManager;

            Vector3 position = map.view.center.ToWorld(map);
            position.y = altitude;
            if (elevationManager) position.y *= elevationManager.GetElevationScale() * elevationManager.scale;

            transform.position = position;
            location = map.view.center;
        }

        private void ProcessInput()
        {
            if (InputManager.GetKey(KeyCode.LeftArrow) || InputManager.GetKey(KeyCode.A))
            {
                tilt -= Time.deltaTime * tiltSpeed * MaxTilt;
            }
            else if (InputManager.GetKey(KeyCode.RightArrow) || InputManager.GetKey(KeyCode.D))
            {
                tilt += Time.deltaTime * tiltSpeed * MaxTilt;
            }
            else if (tilt != 0)
            {
                float tiltOffset = Time.deltaTime * tiltSpeed * MaxTilt;
                if (Mathf.Abs(tilt) > tiltOffset) tilt -= tiltOffset * Mathf.Sign(tilt);
                else tilt = 0;
            }

            if (InputManager.GetKey(KeyCode.W))
            {
                altitude += altitudeChangeSpeed * Time.deltaTime;
            }
            else if (InputManager.GetKey(KeyCode.S))
            {
                altitude -= altitudeChangeSpeed * Time.deltaTime;
            }
        }

        private void Update()
        {
            ProcessInput();

            tilt = Mathf.Clamp(tilt, -MaxTilt, MaxTilt);
            container.transform.localRotation = Quaternion.Euler(tilt, 0, 0);

            if (Math.Abs(tilt) > float.Epsilon)
            {
                transform.Rotate(Vector3.up, tilt * rotateSpeed * Time.deltaTime);
            }

            GeoRect rect = map.view.rect;
            Vector2d distances = rect.Distances();

            double mx = rect.width / distances.x;
            double my = -rect.height / distances.y;

            double v = (double)speed * Time.deltaTime / 3600.0;

            double ox = mx * v * Math.Cos(transform.rotation.eulerAngles.y * Constants.Deg2Rad);
            double oy = my * v * Math.Sin((360 - transform.rotation.eulerAngles.y) * Constants.Deg2Rad);
            
            location.Add(ox, oy);
            map.view.SetCenter(location, altitudeZoomCurve.Evaluate(altitude / maxAltitude));

            Vector3 pos = transform.position;
            pos.y = altitude;
            if (elevationManager) pos.y *= ElevationManagerBase.GetElevationScale(rect, elevationManager);
            transform.position = pos;

            distances = map.view.rect.Distances();
            control.sizeInScene = distances * 1000;
            Vector3 d = transform.position - control.center;
            map.transform.position = new Vector3(d.x, map.transform.position.y, d.z);

            Transform camTransform = Camera.main.transform;
            camTransform.position = transform.position - transform.rotation * cameraOffset;
            camTransform.LookAt(transform);
        }
    }
}