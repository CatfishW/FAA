using UnityEngine;
using UnityEngine.UI;
using WeatherRadar;

namespace VisualUnderstanding.Integration
{
    /// <summary>
    /// Captures weather radar display for vision analysis.
    /// Supports RawImage (UI), RenderTexture, or Camera sources.
    /// </summary>
    [AddComponentMenu("Visual Understanding/Integration/Radar Image Capture")]
    public class RadarImageCapture : MonoBehaviour
    {
        [Header("Capture Settings")]
        [SerializeField] private RawImage sourceRawImage;
        [SerializeField] private RenderTexture sourceRenderTexture;
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private int captureWidth = 512;
        [SerializeField] private int captureHeight = 512;
        
        [Header("Auto Find")]
        [SerializeField] private bool autoFindSource = true;
        
        [Header("Auto Capture")]
        [SerializeField] private bool autoCaptureEnabled = false;
        [SerializeField] private float captureIntervalSeconds = 60f;
        
        private Texture2D _capturedTexture;
        private float _lastCaptureTime;
        
        /// <summary>
        /// Event fired when new capture is available
        /// </summary>
        public event System.Action<Texture2D> OnCapture;
        
        /// <summary>
        /// Last captured texture
        /// </summary>
        public Texture2D CapturedTexture => _capturedTexture;
        
        private void Awake()
        {
            if (autoFindSource && sourceRawImage == null)
            {
                TryAutoFindSource();
            }
        }
        
        private void TryAutoFindSource()
        {
            // Try to find RadarReturnRenderer and get its RawImage
            var radarRenderer = GetComponent<RadarReturnRenderer>();
            if (radarRenderer == null)
            {
                radarRenderer = GetComponentInParent<RadarReturnRenderer>();
            }
            if (radarRenderer == null)
            {
                radarRenderer = FindObjectOfType<RadarReturnRenderer>();
            }
            
            if (radarRenderer != null)
            {
                // Use reflection to get the private returnDisplay field
                var field = typeof(RadarReturnRenderer).GetField("returnDisplay", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    sourceRawImage = field.GetValue(radarRenderer) as RawImage;
                    if (sourceRawImage != null)
                    {
                        Debug.Log($"[RadarImageCapture] Auto-found RawImage from RadarReturnRenderer: {sourceRawImage.name}");
                    }
                }
            }
            
            if (sourceRawImage == null)
            {
                Debug.LogWarning("[RadarImageCapture] Could not auto-find RawImage source. Please assign manually.");
            }
        }
        
        private void Update()
        {
            if (autoCaptureEnabled && Time.time - _lastCaptureTime >= captureIntervalSeconds)
            {
                Capture();
            }
        }
        
        /// <summary>
        /// Set the source RawImage (UI element)
        /// </summary>
        public void SetSource(RawImage rawImage)
        {
            sourceRawImage = rawImage;
        }
        
        /// <summary>
        /// Set the source RenderTexture
        /// </summary>
        public void SetSource(RenderTexture renderTexture)
        {
            sourceRenderTexture = renderTexture;
        }
        
        /// <summary>
        /// Set the source camera
        /// </summary>
        public void SetSource(Camera camera)
        {
            sourceCamera = camera;
        }
        
        /// <summary>
        /// Capture the current radar display
        /// </summary>
        public Texture2D Capture()
        {
            Debug.Log($"[RadarImageCapture] Capture() called - RawImage: {sourceRawImage != null}, RT: {sourceRenderTexture != null}, Camera: {sourceCamera != null}");
            
            if (sourceRawImage != null)
            {
                Debug.Log($"[RadarImageCapture] Capturing from RawImage, texture: {sourceRawImage.texture}");
                _capturedTexture = CaptureFromRawImage(sourceRawImage);
            }
            else if (sourceRenderTexture != null)
            {
                Debug.Log("[RadarImageCapture] Capturing from RenderTexture");
                _capturedTexture = CaptureFromRenderTexture(sourceRenderTexture);
            }
            else if (sourceCamera != null)
            {
                Debug.Log("[RadarImageCapture] Capturing from Camera");
                _capturedTexture = CaptureFromCamera(sourceCamera);
            }
            else
            {
                Debug.LogWarning("[RadarImageCapture] No source configured");
                return null;
            }
            
            if (_capturedTexture == null)
            {
                Debug.LogWarning("[RadarImageCapture] Capture returned null texture");
                return null;
            }
            
            Debug.Log($"[RadarImageCapture] Capture successful: {_capturedTexture.width}x{_capturedTexture.height}");
            _lastCaptureTime = Time.time;
            OnCapture?.Invoke(_capturedTexture);
            
            return _capturedTexture;
        }
        
        private Texture2D CaptureFromRawImage(RawImage rawImage)
        {
            Texture sourceTexture = rawImage.texture;
            
            if (sourceTexture == null)
            {
                Debug.LogWarning("[RadarImageCapture] RawImage has no texture assigned");
                return null;
            }
            
            Texture2D result;
            
            Debug.Log($"[RadarImageCapture] Source texture type: {sourceTexture.GetType().Name}, size: {sourceTexture.width}x{sourceTexture.height}");
            
            // Handle different texture types
            if (sourceTexture is Texture2D tex2D)
            {
                Debug.Log($"[RadarImageCapture] Texture2D isReadable: {tex2D.isReadable}, format: {tex2D.format}");
                
                if (tex2D.isReadable)
                {
                    // For readable textures, copy the pixel data directly
                    result = new Texture2D(tex2D.width, tex2D.height, TextureFormat.RGBA32, false);
                    
                    // Use GetPixels32/SetPixels32 for proper data copy
                    Color32[] pixels = tex2D.GetPixels32();
                    result.SetPixels32(pixels);
                    result.Apply();
                    
                    Debug.Log($"[RadarImageCapture] Copied {pixels.Length} pixels from readable Texture2D");
                }
                else
                {
                    // Use GPU blit for non-readable textures
                    Debug.Log("[RadarImageCapture] Using GPU blit for non-readable texture");
                    RenderTexture tempRT = RenderTexture.GetTemporary(tex2D.width, tex2D.height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(tex2D, tempRT);
                    result = CaptureFromRenderTexture(tempRT);
                    RenderTexture.ReleaseTemporary(tempRT);
                    return result; // Already resized in CaptureFromRenderTexture
                }
            }
            else if (sourceTexture is RenderTexture rt)
            {
                Debug.Log("[RadarImageCapture] Source is RenderTexture");
                // RawImage is displaying a RenderTexture
                return CaptureFromRenderTexture(rt);
            }
            else
            {
                // Generic texture - use GPU blit
                Debug.Log($"[RadarImageCapture] Generic texture, using GPU blit");
                RenderTexture tempRT = RenderTexture.GetTemporary(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(sourceTexture, tempRT);
                result = CaptureFromRenderTexture(tempRT);
                RenderTexture.ReleaseTemporary(tempRT);
                return result; // Already resized in CaptureFromRenderTexture
            }
            
            // Resize if needed
            if (result.width != captureWidth || result.height != captureHeight)
            {
                Debug.Log($"[RadarImageCapture] Resizing from {result.width}x{result.height} to {captureWidth}x{captureHeight}");
                result = ResizeTexture(result, captureWidth, captureHeight);
            }
            
            return result;
        }
        
        private Texture2D CaptureFromRenderTexture(RenderTexture rt)
        {
            // Create destination texture
            Texture2D texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            
            // Store active RT
            RenderTexture previous = RenderTexture.active;
            
            // Read pixels
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            
            // Restore active RT
            RenderTexture.active = previous;
            
            // Resize if needed
            if (rt.width != captureWidth || rt.height != captureHeight)
            {
                texture = ResizeTexture(texture, captureWidth, captureHeight);
            }
            
            return texture;
        }
        
        private Texture2D CaptureFromCamera(Camera cam)
        {
            // Create temporary RT using GetTemporary
            RenderTexture tempRT = RenderTexture.GetTemporary(captureWidth, captureHeight, 24);
            
            // Store camera's previous RT
            RenderTexture previousCamRT = cam.targetTexture;
            
            // Render to temp RT
            cam.targetTexture = tempRT;
            cam.Render();
            
            // Read pixels
            Texture2D texture = CaptureFromRenderTexture(tempRT);
            
            // Restore camera
            cam.targetTexture = previousCamRT;
            
            // Cleanup - use ReleaseTemporary since we used GetTemporary
            RenderTexture.ReleaseTemporary(tempRT);
            
            return texture;
        }
        
        private Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
        {
            // Create temporary RT for GPU resize
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            
            // Copy with bilinear filtering
            Graphics.Blit(source, rt);
            
            // Read back
            Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            
            // Cleanup
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
