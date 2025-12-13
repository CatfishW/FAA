/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsDemos
{
    [AddComponentMenu("Infinity Code/Online Maps/Demos/AirRoute")]
    public class AirRoute : MonoBehaviour
    {
        public TileSetControl control;
        public float maxSpeed = 270; // km/h
        public float maxAltitude = 4000; // meters
        public GameObject airplane;
        public GameObject internalGO;
        public AnimationCurve altitudeZoomCurve = AnimationCurve.Linear(0, 19, 1, 13);
        public AnimationCurve accelerationCurve = AnimationCurve.EaseInOut(0, 200, 1, 0);
        public List<Point> points;

        private float speed;
        private float altitude;
        private Map map;
        private double totalDistance;
        private List<double> distances;
        private float targetRotation;
        private double progress;
        private float tilt;

        private Point GetPointByProgress(double p)
        {
            if (p > 1) p = 1;
            double coveredDistance = totalDistance * p;
            double d = 0;
            int index = -1;
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (d + distances[i] > coveredDistance)
                {
                    index = i;
                    break;
                }

                d += distances[i];
            }

            if (index == -1) return points[points.Count - 1];

            double localProgress = (coveredDistance - d) / distances[index];
            Point p1 = points[index];
            Point p2 = points[index + 1];
            return Point.Lerp(p1, p2, localProgress);
        }

        private void Start()
        {
            if (!control) control = TileSetControl.instance;
            map = control.map;

            if (points == null) points = new List<Point>();
            if (points.Count == 0) points.Add(new Point(map.view.center));

            Point p1 = points[0];
            Point p2 = points[1];

            TilePoint t1 = p1.ToTile(control);
            TilePoint t2 = p2.ToTile(control);
            
            targetRotation = (float)t1.Angle(t2) - 90;
            transform.rotation = Quaternion.Euler(0, targetRotation, 0);

            distances = new List<double>();
            totalDistance = 0;

            for (int i = 1; i < points.Count; i++)
            {
                p1 = points[i - 1];
                p2 = points[i];

                double stepDistance = GeoMath.Distance(p1.longitude, p1.latitude, p2.longitude, p2.latitude);
                distances.Add(stepDistance);
                totalDistance += stepDistance;
            }
        }

        private void Update()
        {
            double lookProgress = progress + 0.001;
            double tiltProgress = progress + 0.001;

            Point position = GetPointByProgress(progress);
            Point lookPosition = GetPointByProgress(lookProgress);
            Point tiltPosition = GetPointByProgress(tiltProgress);

            speed += accelerationCurve.Evaluate(speed / maxSpeed) * Time.deltaTime;
            if (speed > lookPosition.relativeSpeed * maxSpeed) speed = lookPosition.relativeSpeed * maxSpeed;
            double offset = Time.deltaTime * speed / 3600;
            progress += offset / totalDistance;

            TilePoint positionTile = position.ToTile(control);
            TilePoint lookTile = lookPosition.ToTile(control);
            targetRotation = (float) Geometry.Angle2D(positionTile, lookTile);

            TilePoint tiltTile = tiltPosition.ToTile(control);
            float tiltRotation = (float) Geometry.Angle2D(positionTile, tiltTile);

            tilt = tiltRotation - airplane.transform.rotation.eulerAngles.y;

            Vector3 p = control.LocationToWorld(position.longitude, position.latitude);
            altitude = position.relativeAltitude * maxAltitude;
            float elevation = ElevationManagerBase.GetUnscaledElevation(p.x, p.z);
            p.x = control.sizeInScene.x / -2;
            float zoom = altitudeZoomCurve.Evaluate(altitude / maxAltitude);
            p.y = Mathf.Max(altitude, elevation) * ElevationManagerBase.GetElevationScale(map.view.rect, control.elevationManager);
            p.z = control.sizeInScene.y / 2;
            airplane.transform.position = p;
            airplane.transform.rotation = Quaternion.Euler(0, Mathf.LerpAngle(airplane.transform.rotation.eulerAngles.y, targetRotation, Time.deltaTime), 0);
            float s = 1 / Mathf.Pow(2, 15 - map.view.zoom);
            transform.localScale = new Vector3(s, s, s);
            internalGO.transform.localRotation = Quaternion.Euler(tilt, 0, 0);
            map.view.SetCenter(position.longitude, position.latitude, zoom);
            CameraOrbit.instance.rotation = new Vector2(CameraOrbit.instance.rotation.x, airplane.transform.rotation.eulerAngles.y + 90);
        }

        [Serializable]
        public class Point
        {
            public double latitude;
            public double longitude;

            [Range(0, 1)] public float relativeAltitude;

            [Range(0, 1)] public float relativeSpeed;

            public Point(double longitude, double latitude, float relativeAltitude, float relativeSpeed)
            {
                this.longitude = longitude;
                this.latitude = latitude;
                this.relativeSpeed = relativeSpeed;
                this.relativeAltitude = relativeAltitude;
            }

            public Point(Vector2 pos)
            {
                longitude = pos.x;
                latitude = pos.y;
            }

            public static Point Lerp(Point c, Point n, double t)
            {
                if (t < 0) t = 0;
                else if (t > 1) t = 1;

                double x = (n.longitude - c.longitude) * t + c.longitude;
                double y = (n.latitude - c.latitude) * t + c.latitude;
                float altitude = (n.relativeAltitude - c.relativeAltitude) * (float) t + c.relativeAltitude;
                float speed = (n.relativeSpeed - c.relativeSpeed) * (float) t + c.relativeSpeed;

                return new Point(x, y, altitude, speed);
            }

            public TilePoint ToTile(TileSetControl control)
            {
                return control.map.view.projection.LocationToTile(longitude, latitude, control.map.view.intZoom);
            }
        }
    }
}