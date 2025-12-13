/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using OnlineMaps;
using UnityEngine;
using UnityEngine.UI;

namespace OnlineMapsDemos
{
    [AddComponentMenu("Infinity Code/Online Maps/Demos/CustomMarkerEngine")]
    public class CustomMarkerEngine : MonoBehaviour
    {
        private List<MarkerInstance> markers;

        public RawImageTouchForwarder forwarder;
        public RectTransform container;
        public GameObject prefab;
        public MarkerData[] datas;

        private Canvas canvas;
        private Map map;
        private TileSetControl control;

        private Camera worldCamera
        {
            get
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
                return canvas.worldCamera;
            }
        }

        private void OnEnable()
        {
            canvas = container.GetComponentInParent<Canvas>();
        }

        private void SetText(RectTransform rt, string childName, string value)
        {
            Transform child = rt.Find(childName);
            if (!child) return;

            Text t = child.gameObject.GetComponent<Text>();
            if (t) t.text = value;
        }

        private void Start()
        {
            map = Map.instance;
            control = TileSetControl.instance;

            map.OnMapUpdated += UpdateMarkers;
            CameraOrbit.instance.OnCameraControl += UpdateMarkers;

            markers = new List<MarkerInstance>();

            foreach (MarkerData data in datas)
            {
                GameObject markerGameObject = Instantiate(prefab);
                markerGameObject.name = data.title;
                RectTransform rectTransform = markerGameObject.transform as RectTransform;
                rectTransform.SetParent(container);
                markerGameObject.transform.localScale = Vector3.one;
                MarkerInstance marker = new MarkerInstance();
                marker.data = data;
                marker.gameObject = markerGameObject;
                marker.transform = rectTransform;

                SetText(rectTransform, "Title", data.title);
                SetText(rectTransform, "Population", data.population);

                markers.Add(marker);
            }

            UpdateMarkers();
        }

        private void UpdateMarkers()
        {
            foreach (MarkerInstance marker in markers) UpdateMarker(marker);
        }

        private void UpdateMarker(MarkerInstance marker)
        {
            double px = marker.data.longitude;
            double py = marker.data.latitude;

            Vector2 screenPosition = control.LocationToScreen(px, py);
            if (forwarder)
            {
                if (!map.view.Contains(px, py))
                {
                    marker.gameObject.SetActive(false);
                    return;
                }

                screenPosition = forwarder.MapToForwarderSpace(screenPosition);
            }

            if (screenPosition.x < 0 || screenPosition.x > Screen.width ||
                screenPosition.y < 0 || screenPosition.y > Screen.height)
            {
                marker.gameObject.SetActive(false);
                return;
            }

            RectTransform markerRectTransform = marker.transform;

            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);

            Vector2 point;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRectTransform.parent as RectTransform, screenPosition, worldCamera, out point);
            markerRectTransform.localPosition = point;
        }

        [Serializable]
        public class MarkerData
        {
            public string title;
            public double longitude;
            public double latitude;
            public string population;
        }

        public class MarkerInstance
        {
            public MarkerData data;
            public GameObject gameObject;
            public RectTransform transform;
        }
    }
}