/*         INFINITY CODE         */
/*   https://infinity-code.com   */

#if CURVEDUI
using CurvedUI;
#endif

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OnlineMaps
{
    /// <summary>
    /// The base class for uGUI controls.
    /// </summary>
    /// <typeparam name="T">Type of display source.</typeparam>
    public abstract class ControlBaseUI<T> : ControlBase2D where T: MaskableGraphic
    {
        [NonSerialized]
        private T _image;

        /// <summary>
        /// Image that displays the map.
        /// </summary>
        protected T image
        {
            get
            {
                if (!_image) _image = GetComponent<T>();
                return _image;
            }
        }

#if CURVEDUI
        private CurvedUISettings curvedUI;
#endif

        /// <summary>
        /// Reference to the current camera.
        /// </summary>
        protected Camera worldCamera
        {
            get
            {
                if (image.canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
                return image.canvas.worldCamera;
            }
        }

        public override Rect GetScreenRect()
        {
            RectTransform rectTransform = (RectTransform)transform;
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            float xMin = float.PositiveInfinity, xMax = float.NegativeInfinity, yMin = float.PositiveInfinity, yMax = float.NegativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector3 screenCoord = RectTransformUtility.WorldToScreenPoint(worldCamera, corners[i]);
                if (screenCoord.x < xMin) xMin = screenCoord.x;
                if (screenCoord.x > xMax) xMax = screenCoord.x;
                if (screenCoord.y < yMin) yMin = screenCoord.y;
                if (screenCoord.y > yMax) yMax = screenCoord.y;
            }
            Rect result = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
            return result;
        }

        public override bool HitTest(Vector2 position)
        {
#if CURVEDUI
            if (curvedUI != null)
            {
                Camera activeCamera = image.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? Camera.main : image.canvas.worldCamera;
                return curvedUI.RaycastToCanvasSpace(activeCamera.ScreenPointToRay(position), out position);
            }
        
#endif
            if (!EventSystem.current) return false;

            PointerEventData pe = new PointerEventData(EventSystem.current);
            pe.position = position;
            List<RaycastResult> hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pe, hits);

            if (hits.Count > 0 && hits[0].gameObject != gameObject) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(image.rectTransform, position, worldCamera);
        }

        public override Vector2 LocationToScreen(double lng, double lat)
        {
            Vector2d p = LocationToLocal(lng, lat);
            StateProps lastState = map.buffer.lastState;
            p.x /= lastState.width;
            p.y /= lastState.height;
            Rect mapRect = image.GetPixelAdjustedRect();
            p.x = mapRect.x + mapRect.width * p.x;
            p.y = mapRect.y + mapRect.height - mapRect.height * p.y;

            Vector3 worldPoint = new Vector3((float)p.x, (float)p.y, 0);

            Matrix4x4 matrix = transform.localToWorldMatrix;
            worldPoint = matrix.MultiplyPoint(worldPoint);

            return RectTransformUtility.WorldToScreenPoint(worldCamera, worldPoint);
        }

        protected override void OnEnableLate()
        {
            _image = GetComponent<T>();
            if (!_image)
            {
                Debug.LogError("Can not find " + typeof(T));
                Utils.Destroy(this);
            }

#if CURVEDUI
            curvedUI = _image.canvas.GetComponent<CurvedUISettings>();
#endif
        }

        public override bool ScreenToLocation(Vector2 position, out GeoPoint point)
        {
            point = GeoPoint.zero;
            if (!ScreenToTile(position, out TilePoint t)) return false;
            point = t.ToLocation(map);
            return true;
        }

        public override bool ScreenToTile(Vector2 position, out TilePoint tilePoint)
        {
            tilePoint = TilePoint.zero;

            Vector2 point;

#if CURVEDUI
            if (curvedUI != null)
            {
                Camera activeCamera = image.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? Camera.main : image.canvas.worldCamera;

                if (!curvedUI.RaycastToCanvasSpace(activeCamera.ScreenPointToRay(position), out point)) return false;
                Vector3 worldPoint = image.canvas.transform.localToWorldMatrix.MultiplyPoint(point);
                point = image.rectTransform.worldToLocalMatrix.MultiplyPoint(worldPoint);
            }
            else
            {
#endif
            RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform, position, worldCamera, out point);
            if (point == Vector2.zero) return false;
#if CURVEDUI
            }
#endif

            Rect rect = image.GetPixelAdjustedRect();

            Vector2 size = rect.max - point;
            size.x /= rect.size.x;
            size.y /= rect.size.y;

            Vector2 r = new Vector2(size.x - .5f, size.y - .5f) * map.view.zoomFactor;
            tilePoint = map.view.centerTile;
            tilePoint.Add(-map.view.countTilesX * r.x, map.view.countTilesY * r.y);

            return true;
        }
    }
}