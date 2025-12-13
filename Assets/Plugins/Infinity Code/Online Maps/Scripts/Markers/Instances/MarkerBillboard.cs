/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Instance of Billboard marker.
    /// </summary>
    [AddComponentMenu("")]
    public class MarkerBillboard : MarkerInstanceBase
    {
        /// <summary>
        /// Indicates whether to display the marker.
        /// </summary>
        public bool used;

        [SerializeField] 
        private Marker2D _marker;

        public override Marker marker
        {
            get => _marker;
            set => _marker = value as Marker2D;
        }

        /// <summary>
        /// Creates a new instance of the billboard marker.
        /// </summary>
        /// <param name="marker">Marker</param>
        /// <returns>Instance of billboard marker</returns>
        public static MarkerBillboard Create(Marker2D marker)
        {
            GameObject billboardGO = new GameObject("Marker");
            SpriteRenderer spriteRenderer = billboardGO.AddComponent<SpriteRenderer>();
            MarkerBillboard billboard = billboardGO.AddComponent<MarkerBillboard>();
        
            billboard.marker = marker;
            marker.OnInitComplete += billboard.OnInitComplete;
            Texture2D texture = marker.texture;
            if (!marker.texture) texture = (marker.manager as Marker2DManager).defaultTexture;
            if (texture)
            {
                spriteRenderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0));
                spriteRenderer.flipX = true;
            }
        
            return billboard;
        }

        /// <summary>
        /// Dispose billboard instance
        /// </summary>
        public void Dispose()
        {
            if (gameObject) Utils.Destroy(gameObject);
            if (marker != null) marker.OnInitComplete -= OnInitComplete;
            marker = null;
        }

        private void LateUpdate()
        {
            transform.LookAt(marker.manager.map.control3D.currentCamera.transform.position);
            Vector3 euler = transform.rotation.eulerAngles;
            euler.y = marker.rotation;
            transform.rotation = Quaternion.Euler(euler);
        }

        private void OnInitComplete(Marker markerBase)
        {
            Marker2D marker = markerBase as Marker2D;
            Texture2D texture = marker.texture;
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (!marker.texture) texture = (marker.manager as Marker2DManager).defaultTexture;
            if (texture)
            {
                spriteRenderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0));
                spriteRenderer.flipX = true;
            }
        }

        private void Start()
        {
            gameObject.AddComponent<BoxCollider>();
            LateUpdate();
        }
    }
}