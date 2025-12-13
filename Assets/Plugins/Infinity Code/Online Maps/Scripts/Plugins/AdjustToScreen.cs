/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;
using UnityEngine.UI;

namespace OnlineMaps
{
    /// <summary>
    /// Adjusts map size to fit screen.
    /// </summary>
    [AddComponentMenu("Infinity Code/Online Maps/Plugins/Adjust to Screen")]
    [Plugin("Adjust to Screen")]
    public class AdjustToScreen : MonoBehaviour
    {
        /// <summary>
        /// Use half the size of the display source. Strongly improves performance.
        /// </summary>
        [Header("Recommended for 2D Controls")]
        public bool halfSize;

        /// <summary>
        /// Makes the map square with side size equal to the maximum value of the screen width and height.
        /// </summary>
        [Header("To not see the edges when rotating the map")]
        public bool useMaxSide;

        /// <summary>
        /// When set, the forwarder texture size will be used instead of the screen size.
        /// </summary>
        [Header("Optional")]
        public RawImageTouchForwarder forwarder;

        /// <summary>
        /// A camera reference that draws a map when you have multiple cameras in a scene.
        /// </summary>
        public Camera mapCamera;

        private int screenWidth;
        private int screenHeight;
        private Map map;
        private ControlBase control;
        private CameraOrbit cameraOrbit;

        private void GetScreenSize(out int width, out int height)
        {
            if (!forwarder)
            {
                width = Screen.width;
                height = Screen.height;
            }
            else
            {
                Vector3[] fourCorners = new Vector3[4];
                forwarder.image.rectTransform.GetWorldCorners(fourCorners);
                Vector3 size = fourCorners[2] - fourCorners[0];
                width = Mathf.RoundToInt(size.x);
                height = Mathf.RoundToInt(size.y);
            }
        }

        private void ResizeMap()
        {
            int mapWidth = screenWidth / 256 * 256;
            int mapHeight = screenHeight / 256 * 256;

            int zoom = map.view.intZoom;
        
            if (halfSize)
            {
                mapWidth = mapWidth / 512 * 256;
                mapHeight = mapHeight / 512 * 256;
            }

            if (screenWidth % 256 != 0) mapWidth += 256;
            if (screenHeight % 256 != 0) mapHeight += 256;

            if (useMaxSide) mapWidth = mapHeight = Mathf.Max(mapWidth, mapHeight);

            if (mapHeight > (1 << zoom) * Constants.TileSize)
            {
                zoom = Mathf.CeilToInt(Mathf.Log(mapHeight, 2) - 8);
            }

            if (mapWidth > (1 << zoom) * Constants.TileSize)
            {
                zoom = Mathf.CeilToInt(Mathf.Log(mapWidth, 2) - 8);
            }

            int viewWidth = mapWidth;
            int viewHeight = mapHeight;

            if (halfSize)
            {
                viewWidth *= 2;
                viewHeight *= 2;
            }

            if (map.view.intZoom != zoom) map.view.intZoom = zoom;

            ITextureControl textureControl = control as ITextureControl;
            Texture2D texture = textureControl?.texture;
            if (texture)
            {
                SetTextureSize(textureControl, texture, mapWidth, mapHeight, viewWidth, viewHeight);
            }
            else if (control is TileSetControl)
            {
                TileSetControl ts = control as TileSetControl;

                ts.Resize(mapWidth, mapHeight, viewWidth, viewHeight);
                if (ts.currentCamera.orthographic) ts.currentCamera.orthographicSize = screenHeight / 2f;
                else if (cameraOrbit) cameraOrbit.distance = screenHeight * 0.8f;
                SetForwarderSize();
            }
        }

        private void SetForwarderSize()
        {
            if (!forwarder) return;
            RenderTexture targetTexture = forwarder.targetTexture;
            if (targetTexture.width == screenWidth && targetTexture.height == screenHeight) return;
            
            targetTexture.Release();
            targetTexture = new RenderTexture(screenWidth, screenHeight, 32);
            forwarder.targetTexture = targetTexture;
            forwarder.image.texture = targetTexture;
            if (mapCamera) mapCamera.targetTexture = targetTexture;
        }

        private void SetTextureSize(ITextureControl textureControl, Texture2D texture, int mapWidth, int mapHeight, int viewWidth, int viewHeight)
        {
            Utils.Destroy(texture);
            if (control is UIImageControl)
            {
                Utils.Destroy(GetComponent<Image>().sprite);
            }
            else if (control is SpriteRendererControl)
            {
                Utils.Destroy(GetComponent<SpriteRenderer>().sprite);
            }

            texture = new Texture2D(mapWidth, mapHeight, TextureFormat.RGB24, false);
            textureControl.SetTexture(texture);

            if (control is UIRawImageControl)
            {
                RectTransform rt = transform as RectTransform;
                rt.sizeDelta = new Vector2(viewWidth, viewHeight);
            }
            else if (control is UIImageControl)
            {
                RectTransform rt = transform as RectTransform;
                rt.sizeDelta = new Vector2(viewWidth, viewHeight);
            }
            else if (control is SpriteRendererControl)
            {
                GetComponent<BoxCollider>().size = new Vector3(viewWidth / 100f, viewHeight / 100f, 0.2f);
            }
            
            map.RedrawImmediately();
        }

        private void Start()
        {
            map = GetComponent<Map>();
            control = map.control;
            cameraOrbit = GetComponent<CameraOrbit>();
            
            GetScreenSize(out screenWidth, out screenHeight);
            ResizeMap();
        }

        private void Update()
        {
            int currentScreenWidth, currentScreenHeight;
            GetScreenSize(out currentScreenWidth, out currentScreenHeight);
            if (screenWidth == currentScreenWidth && screenHeight == currentScreenHeight) return;
            
            screenWidth = currentScreenWidth;
            screenHeight = currentScreenHeight;

            ResizeMap();
        }
    }
}