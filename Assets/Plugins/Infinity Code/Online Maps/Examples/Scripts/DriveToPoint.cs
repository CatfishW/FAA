/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsDemos
{
    [AddComponentMenu("Infinity Code/Online Maps/Demos/DriveToPoint")]
    public class DriveToPoint : MonoBehaviour
    {
        public GameObject prefab;
        public float markerScale = 5f;

        public GameObject targetPrefab;
        public float targetScale = 5f;

        public float speed;
        public float maxSpeed = 160;
        public float rotation = 0;
        public bool centerOnMarker = true;

        public Material lineRendererMaterial;
        public float lineRendererDuration = 0.5f;

        private Map map;
        private TileSetControl control;
        private Marker3D marker;
        private Marker3D targetMarker;
        private GeoPoint location;
        private GeoPoint targetLocation;
        private bool hasTargetPoint;
        private LineRenderer lineRenderer;
        private float lineRendererProgress = 1;

        private void Start()
        {
            map = Map.instance;
            control = TileSetControl.instance;
            control.OnClick += OnMapClick;
            
            location = map.view.center;

            marker = control.marker3DManager.Create(location, prefab);
            marker.scale = markerScale;
            marker.rotation = rotation;
        }

        private void OnMapClick()
        {
            targetLocation = control.ScreenToLocation();

            if (targetMarker == null)
            {
                targetMarker = control.marker3DManager.Create(targetLocation, targetPrefab);
                targetMarker.scale = targetScale;
            }
            else targetMarker.location = targetLocation;

            TilePoint t1 = location.ToTile(map);
            TilePoint t2 = targetLocation.ToTile(map);

            rotation = (float) Geometry.Angle2D(t1, t2) - 90;
            hasTargetPoint = true;

            if (!lineRenderer)
            {
                GameObject go = new GameObject("LineRenderer");
                go.transform.SetParent(transform, false);
                lineRenderer = go.AddComponent<LineRenderer>();
                lineRenderer.material = lineRendererMaterial;
                lineRenderer.positionCount = 2;
                lineRenderer.widthCurve = AnimationCurve.Constant(0, 1, 10);
            }
            else lineRenderer.enabled = true;

            Vector3 p1 = location.ToWorld(control);
            lineRenderer.SetPosition(0, p1);
            lineRenderer.SetPosition(1, p1);

            lineRendererProgress = 0;
        }

        private void Update()
        {
            if (!hasTargetPoint) return;
            
            double distance = location.Distance(targetLocation);
            float cMaxSpeed = maxSpeed;
            if (distance < 0.1) cMaxSpeed = maxSpeed * (float) (distance / 0.1);

            speed = Mathf.Lerp(speed, cMaxSpeed, Time.deltaTime);
            location = location.Distant(speed * Time.deltaTime / 3600, rotation + 180);
            
            if (location.Distance(targetLocation) < 0.001)
            {
                hasTargetPoint = false;
                speed = 0;
            }

            marker.rotation = rotation;
            marker.location = location;
            if (centerOnMarker) map.view.center = location;

            if (lineRendererProgress < 1)
            {
                lineRendererProgress += Time.deltaTime / lineRendererDuration;

                Vector3 p1 = location.ToWorld(control);
                Vector3 p2 = targetLocation.ToWorld(control);
                Vector3 p3 = lineRendererProgress > 0.5 ? Vector3.Lerp(p1, p2, (lineRendererProgress - 0.5f) * 2f) : p1;
                Vector3 p4 = lineRendererProgress < 0.5 ? Vector3.Lerp(p1, p2, lineRendererProgress * 2) : p2;
                lineRenderer.SetPosition(0, p4);
                lineRenderer.SetPosition(1, p3);
            }
            else
            {
                lineRendererProgress = 1;
                if (lineRenderer.enabled) lineRenderer.enabled = false;
            }
        }
    }
}