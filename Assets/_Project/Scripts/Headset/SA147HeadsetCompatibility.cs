using System;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable CS0649 // Private serialized fields are assigned by the scene setup tool/Inspector.

namespace FAA.Headset
{
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
        [SerializeField] private string[] overlayCanvasNames =
        {
            "FAASymbologyCanvas",
            "FAAHeadingTapeCanvas",
            "XPlaneWeatherIndicatorCanvas",
            "XPlaneWeatherRadarCanvas",
            "XPlaneTrafficRadarCanvas",
        };

        private readonly List<GameObject> _rightEyeMirrors = new List<GameObject>();

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

        [ContextMenu("Enable SA-147 Headset Mode")]
        public void EnableHeadsetMode()
        {
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
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay || canvas.targetDisplay != 0 || !ShouldRouteCanvas(canvas))
                {
                    continue;
                }

                if (routeOverlayCanvasesToLeftEye)
                {
                    canvas.targetDisplay = leftDisplayIndex;
                }

                if (mirrorOverlayCanvasesToRightEye)
                {
                    CreateRightEyeMirror(canvas);
                }
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

        private void CreateRightEyeMirror(Canvas source)
        {
            string mirrorName = source.gameObject.name + " (SA147 Right Eye)";
            Transform parent = source.transform.parent;
            Transform existing = parent != null ? parent.Find(mirrorName) : null;
            GameObject mirror = existing != null ? existing.gameObject : Instantiate(source.gameObject, parent);
            mirror.name = mirrorName;
            mirror.SetActive(true);

            Canvas mirrorCanvas = mirror.GetComponent<Canvas>();
            if (mirrorCanvas != null)
            {
                mirrorCanvas.targetDisplay = rightDisplayIndex;
            }

            _rightEyeMirrors.Add(mirror);
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
