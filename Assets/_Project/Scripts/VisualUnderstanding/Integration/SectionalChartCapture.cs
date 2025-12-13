using UnityEngine;

namespace VisualUnderstanding.Integration
{
    /// <summary>
    /// Captures sectional chart display for vision analysis.
    /// Works with OnlineMaps or any RawImage displaying chart tiles.
    /// </summary>
    [AddComponentMenu("Visual Understanding/Integration/Sectional Chart Capture")]
    public class SectionalChartCapture : MonoBehaviour
    {
        [Header("Capture Settings")]
        [SerializeField] private RenderTexture sourceRenderTexture;
        [SerializeField] private UnityEngine.UI.RawImage sourceRawImage;
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private int captureWidth = 512;
        [SerializeField] private int captureHeight = 512;
        
        private Texture2D _capturedTexture;
        
        /// <summary>
        /// Event fired when new capture is available
        /// </summary>
        public event System.Action<Texture2D> OnCapture;
        
        /// <summary>
        /// Last captured texture
        /// </summary>
        public Texture2D CapturedTexture => _capturedTexture;
        
        /// <summary>
        /// Set the source RenderTexture
        /// </summary>
        public void SetSource(RenderTexture renderTexture)
        {
            sourceRenderTexture = renderTexture;
        }
        
        /// <summary>
        /// Set the source RawImage (e.g., from OnlineMaps)
        /// </summary>
        public void SetSource(UnityEngine.UI.RawImage rawImage)
        {
            sourceRawImage = rawImage;
        }
        
        /// <summary>
        /// Capture the current chart display
        /// </summary>
        public Texture2D Capture()
        {
            if (sourceRenderTexture != null)
            {
                _capturedTexture = CaptureFromRenderTexture(sourceRenderTexture);
            }
            else if (sourceRawImage != null && sourceRawImage.texture != null)
            {
                _capturedTexture = CaptureFromTexture(sourceRawImage.texture);
            }
            else if (sourceCamera != null)
            {
                _capturedTexture = CaptureFromCamera(sourceCamera);
            }
            else
            {
                Debug.LogWarning("[SectionalChartCapture] No source configured");
                return null;
            }
            
            OnCapture?.Invoke(_capturedTexture);
            return _capturedTexture;
        }
        
        /// <summary>
        /// Capture a specific region of the chart
        /// </summary>
        public Texture2D CaptureRegion(Rect normalizedRegion)
        {
            Texture2D fullCapture = Capture();
            if (fullCapture == null) return null;
            
            int x = Mathf.FloorToInt(normalizedRegion.x * fullCapture.width);
            int y = Mathf.FloorToInt(normalizedRegion.y * fullCapture.height);
            int width = Mathf.FloorToInt(normalizedRegion.width * fullCapture.width);
            int height = Mathf.FloorToInt(normalizedRegion.height * fullCapture.height);
            
            // Clamp to bounds
            x = Mathf.Clamp(x, 0, fullCapture.width - 1);
            y = Mathf.Clamp(y, 0, fullCapture.height - 1);
            width = Mathf.Clamp(width, 1, fullCapture.width - x);
            height = Mathf.Clamp(height, 1, fullCapture.height - y);
            
            Color[] pixels = fullCapture.GetPixels(x, y, width, height);
            
            Texture2D region = new Texture2D(width, height, TextureFormat.RGB24, false);
            region.SetPixels(pixels);
            region.Apply();
            
            return region;
        }
        
        private Texture2D CaptureFromRenderTexture(RenderTexture rt)
        {
            Texture2D texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            
            if (rt.width != captureWidth || rt.height != captureHeight)
            {
                texture = ResizeTexture(texture, captureWidth, captureHeight);
            }
            
            return texture;
        }
        
        private Texture2D CaptureFromTexture(Texture sourceTexture)
        {
            // Handle different texture types
            if (sourceTexture is Texture2D tex2D)
            {
                if (tex2D.isReadable)
                {
                    return CopyTexture(tex2D);
                }
            }
            
            // Use GPU to copy non-readable textures
            RenderTexture tempRT = RenderTexture.GetTemporary(sourceTexture.width, sourceTexture.height);
            Graphics.Blit(sourceTexture, tempRT);
            
            Texture2D result = CaptureFromRenderTexture(tempRT);
            
            RenderTexture.ReleaseTemporary(tempRT);
            
            return result;
        }
        
        private Texture2D CaptureFromCamera(Camera cam)
        {
            RenderTexture tempRT = new RenderTexture(captureWidth, captureHeight, 24);
            
            RenderTexture previousCamRT = cam.targetTexture;
            cam.targetTexture = tempRT;
            cam.Render();
            
            Texture2D texture = CaptureFromRenderTexture(tempRT);
            
            cam.targetTexture = previousCamRT;
            tempRT.Release();
            
            return texture;
        }
        
        private Texture2D CopyTexture(Texture2D source)
        {
            Texture2D copy = new Texture2D(source.width, source.height, source.format, source.mipmapCount > 1);
            Graphics.CopyTexture(source, copy);
            
            if (source.width != captureWidth || source.height != captureHeight)
            {
                return ResizeTexture(copy, captureWidth, captureHeight);
            }
            
            return copy;
        }
        
        private Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            Graphics.Blit(source, rt);
            
            Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            
            RenderTexture.ReleaseTemporary(rt);
            
            if (source != _capturedTexture)
            {
                Destroy(source);
            }
            
            return result;
        }
        
        private void OnDestroy()
        {
            if (_capturedTexture != null)
            {
                Destroy(_capturedTexture);
            }
        }
    }
}
