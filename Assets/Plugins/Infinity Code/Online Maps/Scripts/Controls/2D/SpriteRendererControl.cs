/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Class control the map for the SpriteRenderer.
    /// </summary>
    [AddComponentMenu("Infinity Code/Online Maps/Controls/Sprite Renderer Control")]
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteRendererControl:ControlBase2D
    {
        private Collider _cl;
        private Collider2D _cl2D;

        [NonSerialized]
        private SpriteRenderer _spriteRenderer;

        /// <summary>
        /// Singleton instance of SpriteRendererControl control.
        /// </summary>
        public new static SpriteRendererControl instance => _instance as SpriteRendererControl;

        /// <summary>
        /// Collider
        /// </summary>
        public Collider cl
        {
            get
            {
                if (!_cl) _cl = GetComponent<Collider>();
                return _cl;
            }
        }

        /// <summary>
        /// Collider2D
        /// </summary>
        public Collider2D cl2D
        {
            get
            {
                if (!_cl2D) _cl2D = GetComponent<Collider2D>();
                return _cl2D;
            }
        }
        
        /// <summary>
        /// Instance of SpriteRenderer.
        /// </summary>
        private SpriteRenderer spriteRenderer
        {
            get
            {
                if (!_spriteRenderer) _spriteRenderer = GetComponent<SpriteRenderer>();
                return _spriteRenderer;
            }
        }

        public override Rect GetScreenRect()
        {
            Camera mainCamera = Camera.main;
            Vector2 p1 = mainCamera.WorldToScreenPoint(spriteRenderer.bounds.min);
            Vector2 p2 = mainCamera.WorldToScreenPoint(spriteRenderer.bounds.max);
            Vector2 s = p2 - p1;
            return new Rect(p1.x, p1.y, s.x, s.y);
        }

        private bool GetTile2D(Ray ray, out TilePoint tilePoint)
        {
            tilePoint = TilePoint.zero;
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity);
            if (hit.collider == null || hit.collider.gameObject != gameObject) return false;
            if (cl2D == null) return false;

            tilePoint = HitPointToTile(hit.point);
            return true;
        }

        private bool GetTile3D(Ray ray, out TilePoint tilePoint)
        {
            tilePoint = TilePoint.zero;
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit)) return false;

            if (hit.collider.gameObject != gameObject) return false;

            tilePoint = HitPointToTile(hit.point);
            return true;
        }

        private TilePoint HitPointToTile(Vector3 point)
        {
            TileRect r = map.view.GetTileRect();
            if (r.width <= 0) r.right += map.view.maxTiles;

            Bounds bounds = spriteRenderer.sprite.bounds;
            Vector3 size = bounds.size;
            Vector3 localPoint = transform.worldToLocalMatrix.MultiplyPoint(point);
            double tx = localPoint.x / size.x + 0.5;
            double ty = localPoint.y / size.y + 0.5;
            tx = r.width * tx + r.left;
            ty = r.bottom - r.height * ty;
            return new TilePoint(tx, ty, map.view.intZoom);
        }

        public override Vector2 LocationToScreen(double lng, double lat)
        {
            TileRect rect = map.view.GetTileRect();
            int max = map.view.maxTiles;
            if (rect.left > rect.right) rect.right += max;

            TilePoint p = map.view.projection.LocationToTile(lng, lat, map.view.intZoom);

            if (p.x + max / 2 < rect.left) p.x += max;

            Bounds bounds = spriteRenderer.sprite.bounds;
            Vector3 size = bounds.size;
            
            TilePoint r = rect.Lerp(p);
            r.x = (r.x - 0.5) * size.x;
            r.y = (0.5 - r.y) * size.y;

            Vector3 worldPoint = transform.localToWorldMatrix.MultiplyPoint(new Vector3((float)r.x, (float)r.y, bounds.center.z));
            return Camera.main.WorldToScreenPoint(worldPoint);
        }

        protected override void OnEnableLate()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (!_spriteRenderer)
            {
                Debug.LogError("Can not find SpriteRenderer.");
                Utils.Destroy(this);
            }
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
            if (!spriteRenderer.sprite)
            {
                tilePoint = TilePoint.zero;
                return false;
            }

            Ray ray = Camera.main.ScreenPointToRay(position);

            if (GetTile2D(ray, out tilePoint)) return true;
            return GetTile3D(ray, out tilePoint);
        }

        private void SetColliderSize(Texture2D texture)
        {
            if (!texture) return;
            
            BoxCollider boxCollider = cl as BoxCollider;
            if (boxCollider)
            {
                boxCollider.size = new Vector3(texture.width / 100f, texture.height / 100f, 0.2f);
                return;
            }
            
            BoxCollider2D boxCollider2D = cl2D as BoxCollider2D;
            if (boxCollider2D)
            {
                boxCollider2D.size = new Vector2(texture.width / 100f, texture.height / 100f);
            }
        }

        public override void SetTexture(Texture2D newTexture)
        {
            base.SetTexture(newTexture);

            Rect rect = new Rect(0, 0, newTexture.width, newTexture.height);
            spriteRenderer.sprite = Sprite.Create(newTexture, rect, new Vector2(0.5f, 0.5f));

            if (newTexture) SetColliderSize(newTexture);

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            props.SetTexture(ShaderProperties.MainTex, newTexture);
            spriteRenderer.SetPropertyBlock(props);
        }
    }
}