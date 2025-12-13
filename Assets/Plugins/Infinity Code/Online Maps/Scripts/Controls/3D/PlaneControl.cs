/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Class control the map for the Texture.
    /// </summary>
    [System.Serializable]
    [AddComponentMenu("Infinity Code/Online Maps/Controls/Plane Control")]
    [RequireComponent(typeof(MeshRenderer))]
    public class PlaneControl : ControlBase3D, ITextureControl
    {
        /// <summary>
        /// Texture, which is used to draw the map. To change this value use SetTexture method.
        /// </summary>
        [SerializeField]
        protected Texture2D _texture;

        private IMapDrawer _mapDrawer;

        public override Bounds bounds
        {
            get
            {
                Vector3 s = transform.lossyScale;
                Vector3 size = transform.rotation * new Vector3(10 * s.x, 0, 10 * s.z);
                return new Bounds(transform.position + size / 2, size);
            }
        }

        /// <summary>
        /// Singleton instance of TextureControl control.
        /// </summary>
        public new static PlaneControl instance => _instance as PlaneControl;

        public override int height => _texture ? _texture.height : 0;

        public Texture2D texture
        {
            get => _texture;
            set => SetTexture(value);
        }

        public override int width => _texture ? _texture.width : 0;

        public override void OnAwakeBefore()
        {
            base.OnAwakeBefore();

            mapDrawer = new TextureDrawer(this);
            mapDrawer.Initialize();
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
            RaycastHit hit;

            tilePoint = TilePoint.zero;
            if (!mapDrawer.Raycast(currentCamera.ScreenPointToRay(position), out hit, Constants.MaxRaycastDistance)) return false;
            if (!rendererInstance || !rendererInstance.sharedMaterial || !rendererInstance.sharedMaterial.mainTexture) return false;

            Vector2 r = hit.textureCoord;

            float zoomFactor = map.view.zoomFactor;
            r.x = (r.x - 0.5f) * zoomFactor;
            r.y = (r.y - 0.5f) * zoomFactor;

            tilePoint = map.view.GetCenterTile();
            tilePoint.Add(map.view.countTilesX * r.x, -map.view.countTilesY * r.y);
            return true;
        }

        public void SetDefaultTexture()
        {
            if (!texture) return;
            if (texture.width * texture.height != map.defaultColors.Length) return;
            
            texture.SetPixels(map.defaultColors);
            texture.Apply();
        }

        public void SetTexture(Texture2D texture)
        {
            _texture = texture;
            map.view.SetSize(texture.width, texture.height);
            GetComponent<Renderer>().sharedMaterial.mainTexture = texture;
        }
        
        public override void UpdateControl()
        {
            if (texture)
            {
                texture.SetPixels32(map.buffer.frontBuffer);
                texture.Apply(false);
            }
            
            base.UpdateControl();
        }
    }
}