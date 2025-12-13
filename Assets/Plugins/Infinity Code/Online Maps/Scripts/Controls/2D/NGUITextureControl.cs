/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Class control the map for the NGUI.
    /// </summary>
    [System.Serializable]
    [AddComponentMenu("Infinity Code/Online Maps/Controls/NGUI Texture Control")]
    public class NGUITextureControl : ControlBase2D
    {
#if NGUI
        private UITexture uiTexture;
        private UIWidget uiWidget;
        private MouseController mouseController;

        /// <summary>
        /// Singleton instance of NGUITextureControl control.
        /// </summary>
        public new static NGUITextureControl instance
        {
            get { return ControlBase.instance as NGUITextureControl; }
        }

        public override Rect uvRect
        {
            get { return uiTexture.uvRect; }
        }

        public override Rect GetScreenRect()
        {
            int w = Screen.width / 2;
            int h = Screen.height / 2;

            Bounds b = NGUIMath.CalculateAbsoluteWidgetBounds(uiTexture.transform);

            int rx = Mathf.RoundToInt(b.min.x * h + w);
            int ry = Mathf.RoundToInt((b.min.y + 1) * h);
            int rz = Mathf.RoundToInt(b.size.x * h);
            int rw = Mathf.RoundToInt(b.size.y * h);

            return new Rect(rx, ry, rz, rw);
        }

        public override bool HitTest(Vector2 position)
        {
            return true;
        }

        public override Vector2 LocationToScreen(double lng, double lat)
        {
            if (UICamera.currentCamera == null) return Vector2.zero;

            double px, py;
            Vector2d p = GetPosition(lng, lat);
            px = (p.x / map.view.width - 0.5f) * uiWidget.localSize.x;
            py = (0.5f - p.y / map.view.height) * uiWidget.localSize.y;
            Vector3 worldPos = transform.TransformPoint(new Vector3((float)px, (float)py, 0));
            Vector3 screenPosition = UICamera.currentCamera.WorldToScreenPoint(worldPos);
            return screenPosition;
        }


        protected override void OnEnableLate()
        {
            uiWidget = GetComponent<UIWidget>();
            uiTexture = GetComponent<UITexture>();
            if (uiTexture == null)
            {
                Debug.LogError("Can not find UITexture.");
                Utils.Destroy(this);
            }

            mouseController = GetComponent<MouseController>();
        }

        private void OnPress(bool state)
        {
            if (state) mouseController.OnPress();
            else mouseController.OnRelease();
        }

        public override bool ScreenToLocation(Vector2 position, out GeoPoint point)
        {
            point = GeoPoint.zero;
            TilePoint t;
            if (!ScreenToTile(position, out t)) return false;
            point = t.ToLocation(map);
            return true;
        }

        public override bool ScreenToTile(Vector2 position, out TilePoint tilePoint)
        {
            tilePoint = TilePoint.zero;
            if (UICamera.currentCamera == null) return false;

            Vector3 worldPos = UICamera.currentCamera.ScreenToWorldPoint(position);
            Vector3 localPos = transform.worldToLocalMatrix.MultiplyPoint3x4(worldPos);

            localPos.x /= uiWidget.localSize.x;
            localPos.y /= uiWidget.localSize.y;
            
            tilePoint = map.view.centerTile;

            int countX = map.texture.width / Constants.TileSize;
            int countY = map.texture.height / Constants.TileSize;

            float zoomFactor = map.view.zoomFactor;
            tilePoint.Add(countX * localPos.x * zoomFactor, -countY * localPos.y * zoomFactor);

            return true;
        }

        public override void SetTexture(Texture2D texture)
        {
            base.SetTexture(texture);
            StartCoroutine(OnFrameEnd(texture));
        }

        public IEnumerator OnFrameEnd(Texture2D texture)
        {
            yield return new WaitForEndOfFrame();
            uiTexture.mainTexture = texture;
        }
#else

        public override bool ScreenToTile(Vector2 position, out TilePoint tilePoint)
        {
            tilePoint = TilePoint.zero;
            return false;
        }

        public override bool ScreenToLocation(Vector2 position, out GeoPoint point)
        {
            point = GeoPoint.zero;
            return false;
        }
#endif
    }
}