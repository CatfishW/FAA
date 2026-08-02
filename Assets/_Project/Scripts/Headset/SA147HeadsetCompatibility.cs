using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#pragma warning disable CS0649 // Private serialized fields are assigned by the scene setup tool/Inspector.

namespace FAA.Headset
{
    [DefaultExecutionOrder(11000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("FAA/Headset/SA-147 Headset Compatibility")]
    public sealed class SA147HeadsetCompatibility : MonoBehaviour
    {
        public const string HeadsetName = "SA Photonics / Vision Products SA-147/S";

        [Header("Activation")]
        [SerializeField] private bool enableOnStart;
        [SerializeField] private bool autoEnableWhenHeadsetDisplaysPresent = true;
        [SerializeField] private bool activateAdditionalDisplays = true;
        [SerializeField] private bool setFullscreenResolution;

        [Header("SA-147 Output")]
        [SerializeField] private GameObject sa147Rig;
        [SerializeField] private GameObject archerBridge;
        [SerializeField] private bool enableArcherTracker = true;
        [SerializeField] private int leftDisplayIndex = 1;
        [SerializeField] private int rightDisplayIndex = 2;
        [SerializeField] private int perEyeWidth = 1920;
        [SerializeField] private int perEyeHeight = 1200;
        [SerializeField] private float verticalFovDegrees = 33f;
        [SerializeField] private float horizontalFovDegrees = 53f;

        [Header("HUD Display Routing")]
        [SerializeField] private bool mirrorOverlayCanvasesToRightEye = true;
        [SerializeField] private bool routeOverlayCanvasesToLeftEye = true;
        [Tooltip("Composite the live HUD through the SA-147 rig cameras so the headset prewarp is applied to symbology as well as terrain.")]
        [SerializeField] private bool renderHudThroughHeadsetPrewarp = true;
        [Range(8, 31)]
        [SerializeField] private int hudCaptureLayer = 31;
        [SerializeField] private string[] overlayCanvasNames =
        {
            "FAASymbologyCanvas",
            "FAAHeadingTapeCanvas",
            "XPlaneWeatherIndicatorCanvas",
            "XPlaneWeatherRadarCanvas",
            "XPlaneTrafficRadarCanvas",
        };

        private readonly List<GameObject> _hudOutputs = new List<GameObject>();
        private readonly List<Canvas> _capturedCanvases = new List<Canvas>();
        private Camera _hudCaptureCamera;
        private RenderTexture _hudCaptureTexture;
        private bool _headsetModeActive;

        private void Awake()
        {
            if (!ShouldEnableHeadsetMode())
            {
                if (sa147Rig != null)
                {
                    sa147Rig.SetActive(false);
                }

                if (archerBridge != null)
                {
                    archerBridge.SetActive(false);
                }

                return;
            }

            EnableHeadsetMode();
        }

        private void OnDestroy()
        {
            ReleaseHudCapture();
        }

        private void Start()
        {
            if (_headsetModeActive)
            {
                // The HUD sanitizer has its own final Start pass. Route again at
                // this later execution order so those canvases remain attached to
                // the headset capture camera.
                ConfigureOverlayCanvases();
            }
        }

        [ContextMenu("Enable SA-147 Headset Mode")]
        public void EnableHeadsetMode()
        {
            _headsetModeActive = true;
            Application.runInBackground = true;

            if (activateAdditionalDisplays)
            {
                ActivateDisplays();
            }

            if (setFullscreenResolution)
            {
                Screen.SetResolution(perEyeWidth * 2, perEyeHeight, FullScreenMode.FullScreenWindow);
            }

            if (sa147Rig != null)
            {
                sa147Rig.SetActive(true);
                ConfigureRigCameras(sa147Rig);
            }

            ConfigureOverlayCanvases();
            ConfigureArcherBridge();

            Debug.Log($"[SA147] {HeadsetName} compatibility active. Displays={Display.displays.Length}, left={leftDisplayIndex}, right={rightDisplayIndex}");
        }

        private bool ShouldEnableHeadsetMode()
        {
            if (enableOnStart || HasCommandLineFlag("-sa147") || HasCommandLineFlag("--sa147") || HasCommandLineFlag("-hmd") || HasCommandLineFlag("--hmd"))
            {
                return true;
            }

            return autoEnableWhenHeadsetDisplaysPresent && Display.displays.Length > Mathf.Max(leftDisplayIndex, rightDisplayIndex);
        }

        private static bool HasCommandLineFlag(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void ActivateDisplays()
        {
            for (int i = 1; i < Display.displays.Length; i++)
            {
                if (setFullscreenResolution)
                {
                    Display.displays[i].Activate(perEyeWidth, perEyeHeight, Screen.currentResolution.refreshRateRatio);
                }
                else
                {
                    Display.displays[i].Activate();
                }
            }
        }

        private void ConfigureRigCameras(GameObject rig)
        {
            Camera[] cameras = rig.GetComponentsInChildren<Camera>(true);
            foreach (Camera camera in cameras)
            {
                if (camera == null)
                {
                    continue;
                }

                string cameraName = camera.gameObject.name;
                if (cameraName.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    camera.targetDisplay = leftDisplayIndex;
                }
                else if (cameraName.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    camera.targetDisplay = rightDisplayIndex;
                }

                camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.allowHDR = false;
                camera.aspect = Mathf.Abs(Mathf.Tan(Mathf.Deg2Rad * horizontalFovDegrees * 0.5f) / Mathf.Tan(Mathf.Deg2Rad * verticalFovDegrees * 0.5f));
                camera.fieldOfView = verticalFovDegrees;
            }
        }

        private void ConfigureOverlayCanvases()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            List<Canvas> routedCanvases = new List<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || !ShouldRouteCanvas(canvas))
                {
                    continue;
                }

                routedCanvases.Add(canvas);
            }

            if (routedCanvases.Count == 0)
            {
                Debug.LogWarning("[SA147] No configured FAA overlay canvases were available for headset routing.", this);
                return;
            }

            // Capture the live canvases once. This deliberately avoids cloning
            // their X-Plane providers, coroutines, and control scripts per eye.
            ConfigureHudCapture(routedCanvases);
            bool createdPrewarpedOutputs = renderHudThroughHeadsetPrewarp && CreatePrewarpedHudOutputs();
            if (!createdPrewarpedOutputs)
            {
                CreateDisplayOverlayOutputs();
            }
        }

        private bool ShouldRouteCanvas(Canvas canvas)
        {
            string path = GetHierarchyPath(canvas.transform);
            for (int i = 0; i < overlayCanvasNames.Length; i++)
            {
                string canvasName = overlayCanvasNames[i];
                if (!string.IsNullOrWhiteSpace(canvasName) &&
                    path.IndexOf(canvasName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void ConfigureHudCapture(List<Canvas> canvases)
        {
            ClearHudOutputs();
            EnsureHudCaptureTexture();

            if (_hudCaptureCamera == null)
            {
                GameObject cameraObject = new GameObject("SA147 HUD Capture Camera", typeof(Camera));
                cameraObject.transform.SetParent(transform, false);
                cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
                cameraObject.transform.localRotation = Quaternion.identity;
                _hudCaptureCamera = cameraObject.GetComponent<Camera>();
            }

            int captureLayer = Mathf.Clamp(hudCaptureLayer, 8, 31);
            _hudCaptureCamera.enabled = true;
            _hudCaptureCamera.clearFlags = CameraClearFlags.SolidColor;
            _hudCaptureCamera.backgroundColor = Color.clear;
            _hudCaptureCamera.cullingMask = 1 << captureLayer;
            _hudCaptureCamera.orthographic = true;
            _hudCaptureCamera.allowHDR = false;
            _hudCaptureCamera.allowMSAA = false;
            _hudCaptureCamera.depth = -100f;
            _hudCaptureCamera.targetTexture = _hudCaptureTexture;

            _capturedCanvases.Clear();
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null)
                {
                    continue;
                }

                SetLayerRecursively(canvas.gameObject, captureLayer);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = _hudCaptureCamera;
                canvas.planeDistance = 1f;
                canvas.targetDisplay = 0;
                _capturedCanvases.Add(canvas);
            }
        }

        private void EnsureHudCaptureTexture()
        {
            int width = Mathf.Max(640, perEyeWidth);
            int height = Mathf.Max(480, perEyeHeight);
            if (_hudCaptureTexture != null && _hudCaptureTexture.width == width && _hudCaptureTexture.height == height)
            {
                return;
            }

            if (_hudCaptureTexture != null)
            {
                _hudCaptureTexture.Release();
                DestroyUnityObject(_hudCaptureTexture);
            }

            _hudCaptureTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "SA147 Live FAA HUD",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            _hudCaptureTexture.Create();
        }

        private bool CreatePrewarpedHudOutputs()
        {
            if (sa147Rig == null || _hudCaptureTexture == null)
            {
                return false;
            }

            int outputCount = 0;
            foreach (Camera camera in sa147Rig.GetComponentsInChildren<Camera>(true))
            {
                if (camera == null)
                {
                    continue;
                }

                bool left = camera.gameObject.name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0;
                bool right = camera.gameObject.name.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0;
                if ((!left && !right) || (left && !routeOverlayCanvasesToLeftEye) || (right && !mirrorOverlayCanvasesToRightEye))
                {
                    continue;
                }

                Rect viewport = camera.rect;
                Rect uv = CalculateHudUvRect(viewport);
                CreateHudOutputCanvas(
                    $"SA147 Prewarped HUD - {camera.gameObject.name}",
                    camera,
                    camera.targetDisplay,
                    uv);
                outputCount++;
            }

            if (outputCount > 0)
            {
                Debug.Log($"[SA147] Live FAA HUD routed through {outputCount} prewarped headset camera view(s).", this);
            }

            return outputCount > 0;
        }

        private void CreateDisplayOverlayOutputs()
        {
            if (routeOverlayCanvasesToLeftEye)
            {
                CreateHudOutputCanvas("SA147 Left Eye HUD", null, leftDisplayIndex, new Rect(0f, 0f, 1f, 1f));
            }

            if (mirrorOverlayCanvasesToRightEye)
            {
                CreateHudOutputCanvas("SA147 Right Eye HUD", null, rightDisplayIndex, new Rect(0f, 0f, 1f, 1f));
            }

            Debug.LogWarning("[SA147] Headset cameras were unavailable; HUD is using direct display overlays without prewarp.", this);
        }

        private void CreateHudOutputCanvas(string outputName, Camera targetCamera, int targetDisplay, Rect uvRect)
        {
            GameObject output = new GameObject(outputName, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            output.transform.SetParent(transform, false);
            output.layer = 5; // Unity's built-in UI layer; the SA-147 cameras include it.

            Canvas canvas = output.GetComponent<Canvas>();
            canvas.renderMode = targetCamera != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = targetCamera;
            canvas.targetDisplay = targetDisplay;
            canvas.sortingOrder = short.MaxValue - 8;
            canvas.overrideSorting = true;
            if (targetCamera != null)
            {
                canvas.planeDistance = Mathf.Max(targetCamera.nearClipPlane + 0.05f, 0.4f);
                targetCamera.cullingMask |= 1 << output.layer;
            }

            UnityEngine.UI.CanvasScaler scaler = output.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = targetCamera != null
                ? new Vector2(
                    Mathf.Max(1f, perEyeWidth * Mathf.Max(0.01f, targetCamera.rect.width)),
                    Mathf.Max(1f, perEyeHeight * Mathf.Max(0.01f, targetCamera.rect.height)))
                : new Vector2(perEyeWidth, perEyeHeight);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject imageObject = new GameObject("Live FAA HUD", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(output.transform, false);
            imageObject.layer = output.layer;
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            RawImage image = imageObject.GetComponent<RawImage>();
            image.texture = _hudCaptureTexture;
            image.uvRect = uvRect;
            image.color = Color.white;
            image.raycastTarget = false;

            _hudOutputs.Add(output);
        }

        public static Rect CalculateHudUvRect(Rect cameraViewport)
        {
            return new Rect(
                Mathf.Clamp01(cameraViewport.x),
                Mathf.Clamp01(cameraViewport.y),
                Mathf.Clamp01(cameraViewport.width),
                Mathf.Clamp01(cameraViewport.height));
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                if (child != null)
                {
                    SetLayerRecursively(child.gameObject, layer);
                }
            }
        }

        private void ClearHudOutputs()
        {
            foreach (GameObject output in _hudOutputs)
            {
                DestroyUnityObject(output);
            }

            _hudOutputs.Clear();
        }

        private void ReleaseHudCapture()
        {
            ClearHudOutputs();
            if (_hudCaptureCamera != null)
            {
                _hudCaptureCamera.targetTexture = null;
                DestroyUnityObject(_hudCaptureCamera.gameObject);
                _hudCaptureCamera = null;
            }

            if (_hudCaptureTexture != null)
            {
                _hudCaptureTexture.Release();
                DestroyUnityObject(_hudCaptureTexture);
                _hudCaptureTexture = null;
            }
        }

        private static void DestroyUnityObject(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }

        private void ConfigureArcherBridge()
        {
            if (archerBridge == null)
            {
                return;
            }

            bool windowsRuntime =
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                true;
#else
                false;
#endif
            archerBridge.SetActive(enableArcherTracker && windowsRuntime);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            Stack<string> names = new Stack<string>();
            while (transform != null)
            {
                names.Push(transform.name);
                transform = transform.parent;
            }

            return string.Join("/", names);
        }
    }
}
