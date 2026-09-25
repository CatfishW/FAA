using System;
using AircraftControl.Camera;
using AircraftControl.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FAA.Customization
{
    /// <summary>
    /// Chooses how the primary flight symbology is presented.
    ///
    /// Conformal mode defaults to a separate native world-space rotorcraft cue
    /// layer while instruments stay fixed. The earlier aircraft-reference group
    /// projection remains an explicit compatibility option. HeadFixed preserves
    /// the traditional overlay for desktop familiarisation.
    /// </summary>
    // AircraftCameraController commits its final pose at 11100. Projecting
    // before it creates a one-frame registration error even with fresh data.
    [DefaultExecutionOrder(12050)]
    [AddComponentMenu("FAA/Customization/Conformal HUD Controller")]
    public sealed class FaaConformalHudController : MonoBehaviour
    {
        // The presentation binding is intentionally runtime-safe so legacy
        // scenes can opt into conformal projection without reauthoring HUDs.
        public enum HudPresentationMode
        {
            Conformal,
            HeadFixed
        }

        [Header("Presentation")]
        [SerializeField] private HudPresentationMode presentationMode = HudPresentationMode.Conformal;
        [SerializeField] private bool fallbackToHeadFixed = true;

        [Tooltip("Render earth/scene-referenced rotorcraft cues separately from the fixed instruments. Legacy projection remains available when disabled.")]
        [SerializeField] private bool useRotorcraftSceneLayer = true;
        private FaaRotorcraftConformalLayer _rotorcraftLayer;

        [Header("Scene bindings")]
        [SerializeField] private string screenCanvasName = "FAASymbologyCanvas";
        [SerializeField] private string conformalCanvasName = "FAASymbologyCanvasWorldSpace";
        [SerializeField] private string conformalHudRootName = "Second Interation GUI";
        [SerializeField] private string headingCanvasName = "FAAHeadingTapeCanvas";
        [SerializeField] private string headingHudRootName = "FAA Heading Tape Overlay";
        [Tooltip("Keep the separate heading tape aligned with the primary conformal HUD.")]
        [SerializeField] private bool projectHeadingTape = true;
        [SerializeField] private Transform aircraftTransform;
        [SerializeField] private UnityEngine.Camera projectionCamera;

        [Header("Conformal rendering")]
        [Tooltip("The world-space canvas is sorted above the aircraft scene while remaining aircraft-relative.")]
        [SerializeField] private int conformalSortingOrder = 5050;
        [Tooltip("Distance in metres from the aircraft reference point to the conformal symbology plane.")]
        [Min(1f)]
        [SerializeField] private float conformalDistance = 175.6f;
        [Tooltip("Use the camera controller's no-look aircraft reference, never its current head-look rotation.")]
        [SerializeField] private bool alignToAircraftReference = true;
        [Tooltip("Use the dormant authored world-space duplicate when its scene layout is calibrated. Screen projection is safer for legacy scenes and is the default.")]
        [SerializeField] private bool useWorldSpaceConformalCanvas = false;
        [Tooltip("World-space HUD input is disabled so looking at the outside view cannot be captured by the HUD.")]
        [SerializeField] private bool disableConformalRaycasts = true;

        private Canvas _screenCanvas;
        private Canvas _conformalCanvas;
        private Canvas _headingCanvas;
        private Scene _boundScene;
        private HudPresentationMode _lastAppliedMode = (HudPresentationMode)(-1);
        private bool _warnedMissingCanvas;
        private RectTransform _screenHudRoot;
        private RectTransform _headingHudRoot;
        private Vector2 _headFixedAnchoredPosition;
        private Quaternion _headFixedLocalRotation;
        private bool _headFixedRootCaptured;
        private Vector2 _headingHeadFixedAnchoredPosition;
        private Quaternion _headingHeadFixedLocalRotation;
        private bool _headingHeadFixedRootCaptured;
        private static FaaConformalHudController _runtimeInstance;

        public HudPresentationMode PresentationMode => presentationMode;
        public bool IsConformal => presentationMode == HudPresentationMode.Conformal &&
                                    (_screenCanvas != null || _conformalCanvas != null);
        public bool UsesRotorcraftSceneLayer => Application.isPlaying && useRotorcraftSceneLayer &&
            _rotorcraftLayer != null && _rotorcraftLayer.enabled;
        public bool UsesScreenProjection => presentationMode == HudPresentationMode.Conformal &&
            !useWorldSpaceConformalCanvas && !UsesRotorcraftSceneLayer;
        public Canvas ScreenCanvas => _screenCanvas;
        public Canvas ConformalCanvas => _conformalCanvas;

        public event Action<HudPresentationMode> PresentationModeChanged;

        /// <summary>
        /// Creates a small scene-persistent binding object for scenes that do
        /// not have the controller authored yet. A scene-authored instance is
        /// preferred when present.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (_runtimeInstance != null)
            {
                return;
            }

            FaaConformalHudController existing = FindFirstObjectByType<FaaConformalHudController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                _runtimeInstance = existing;
                return;
            }

            GameObject host = new GameObject("FAA Conformal HUD Presentation");
            DontDestroyOnLoad(host);
            _runtimeInstance = host.AddComponent<FaaConformalHudController>();
        }

        private void Awake()
        {
            if (_runtimeInstance == null)
            {
                _runtimeInstance = this;
            }
            else if (_runtimeInstance != this && transform.root == _runtimeInstance.transform.root)
            {
                // A scene-authored controller wins over the persistent helper.
                Destroy(gameObject);
                return;
            }
        }

        private void OnEnable()
        {
            Canvas.preWillRenderCanvases += RefreshRenderProjection;
            Application.onBeforeRender += RefreshRenderProjection;
        }

        private void OnDisable()
        {
            Canvas.preWillRenderCanvases -= RefreshRenderProjection;
            Application.onBeforeRender -= RefreshRenderProjection;
            if (_rotorcraftLayer != null) _rotorcraftLayer.enabled = false;
            RestoreHeadFixedRoot();
        }

        // Reproject only; never integrate input or telemetry smoothing here.
        // The canvas callback also covers a pose changed during XR before-render.
        [BeforeRenderOrder(200)]
        private void RefreshRenderProjection()
        {
            if (isActiveAndEnabled && UsesScreenProjection && _screenHudRoot != null)
                UpdateScreenConformalProjection();
        }

        private void OnDestroy()
        {
            if (_runtimeInstance == this)
            {
                _runtimeInstance = null;
            }
        }

        private void LateUpdate()
        {
            if (!ResolveTargets())
            {
                if (_rotorcraftLayer != null) _rotorcraftLayer.enabled = false;
                return;
            }

            if (_lastAppliedMode != presentationMode)
            {
                ApplyPresentationMode();
            }
            else
            {
                // The runtime sanitizer may scan after scene load. Keep the
                // selected presentation authoritative without changing the
                // authored local aircraft-relative transform.
                EnforceCanvasState();
            }
        }

        public void SetPresentationMode(HudPresentationMode mode)
        {
            if (presentationMode == mode && _lastAppliedMode == mode)
            {
                return;
            }

            presentationMode = mode;
            ApplyPresentationMode();
            PresentationModeChanged?.Invoke(mode);
        }

        public void TogglePresentationMode()
        {
            SetPresentationMode(presentationMode == HudPresentationMode.Conformal
                ? HudPresentationMode.HeadFixed
                : HudPresentationMode.Conformal);
        }

        [ContextMenu("Use Conformal HUD")]
        private void UseConformalHud() => SetPresentationMode(HudPresentationMode.Conformal);

        [ContextMenu("Use Head-Fixed HUD")]
        private void UseHeadFixedHud() => SetPresentationMode(HudPresentationMode.HeadFixed);

        private bool ResolveTargets()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (_boundScene != activeScene)
            {
                _boundScene = activeScene;
                _screenCanvas = null;
                _conformalCanvas = null;
                _headingCanvas = null;
                _screenHudRoot = null;
                _headingHudRoot = null;
                _headFixedRootCaptured = false;
                _headingHeadFixedRootCaptured = false;
                _warnedMissingCanvas = false;
            }

            if (projectionCamera == null)
            {
                projectionCamera = UnityEngine.Camera.main;
            }

            if (aircraftTransform == null)
            {
                AircraftCameraController cameraController = FindFirstObjectByType<AircraftCameraController>(FindObjectsInactive.Include);
                aircraftTransform = cameraController != null ? cameraController.AircraftTransform : null;
            }

            if (aircraftTransform == null)
            {
                AircraftController aircraft = FindFirstObjectByType<AircraftController>(FindObjectsInactive.Include);
                aircraftTransform = aircraft != null ? aircraft.transform : null;
            }

            if (_screenCanvas == null)
            {
                _screenCanvas = FindCanvas(screenCanvasName);
                _screenHudRoot = null;
            }

            if (_conformalCanvas == null)
            {
                _conformalCanvas = FindCanvas(conformalCanvasName);
            }

            if (_headingCanvas == null)
            {
                _headingCanvas = FindCanvas(headingCanvasName);
            }

            bool hasAnyCanvas = _screenCanvas != null || _conformalCanvas != null;
            if (!hasAnyCanvas && !_warnedMissingCanvas)
            {
                _warnedMissingCanvas = true;
                Debug.LogWarning("[FaaConformalHudController] No FAA HUD canvas was found; waiting for scene bindings.");
            }

            return hasAnyCanvas;
        }

        private void ApplyPresentationMode()
        {
            if (presentationMode == HudPresentationMode.Conformal && _conformalCanvas == null && fallbackToHeadFixed)
            {
                // The screen-projected conformal path does not require the
                // duplicate world-space canvas. Only fall back when the
                // primary screen HUD itself is unavailable.
                if (_screenCanvas == null)
                {
                    presentationMode = HudPresentationMode.HeadFixed;
                }
            }

            if (presentationMode == HudPresentationMode.Conformal && useWorldSpaceConformalCanvas)
            {
                ConfigureConformalCanvas();
            }

            EnforceCanvasState();
            _lastAppliedMode = presentationMode;
        }

        private void EnforceCanvasState()
        {
            if (Application.isPlaying && useRotorcraftSceneLayer &&
                presentationMode == HudPresentationMode.Conformal && _screenCanvas != null)
            {
                // Do not rotate/translate IAS, ALT, RA, torque, NR or the heading
                // tape with the pilot's gaze. Only the separate scene layer is projected.
                SetCanvasVisible(_screenCanvas, true);
                SetCanvasVisible(_conformalCanvas, false);
                RestoreHeadFixedRoot();
                if (_rotorcraftLayer == null)
                    _rotorcraftLayer = GetComponent<FaaRotorcraftConformalLayer>() ?? gameObject.AddComponent<FaaRotorcraftConformalLayer>();
                _rotorcraftLayer.Bind(projectionCamera, aircraftTransform, _screenCanvas);
                _rotorcraftLayer.enabled = true;
                return;
            }
            if (_rotorcraftLayer != null) _rotorcraftLayer.enabled = false;
            bool useWorldSpace = presentationMode == HudPresentationMode.Conformal &&
                                 useWorldSpaceConformalCanvas && _conformalCanvas != null;

            SetCanvasVisible(_screenCanvas, !useWorldSpace);
            SetCanvasVisible(_conformalCanvas, useWorldSpace);

            if (useWorldSpace)
            {
                ConfigureConformalCanvas();
            }

            if (presentationMode == HudPresentationMode.Conformal && !useWorldSpace)
            {
                UpdateScreenConformalProjection();
            }
            else
            {
                RestoreHeadFixedRoot();
            }
        }

        private void UpdateScreenConformalProjection()
        {
            if (_screenCanvas == null || aircraftTransform == null || projectionCamera == null)
            {
                return;
            }

            if (_screenHudRoot == null)
            {
                Transform root = _screenCanvas.transform.Find(conformalHudRootName);
                _screenHudRoot = root as RectTransform;
                if (_screenHudRoot == null)
                {
                    return;
                }

                _headFixedAnchoredPosition = _screenHudRoot.anchoredPosition;
                _headFixedLocalRotation = _screenHudRoot.localRotation;
                _headFixedRootCaptured = true;
            }

            AircraftCameraController cameraController = projectionCamera.GetComponent<AircraftCameraController>();
            Quaternion reference = alignToAircraftReference && cameraController != null
                ? cameraController.AircraftReferenceRotation
                : aircraftTransform.rotation;
            if (reference == default)
            {
                reference = aircraftTransform.rotation;
            }

            if (projectHeadingTape && _headingCanvas != null)
            {
                if (_headingHudRoot == null)
                {
                    _headingHudRoot = _headingCanvas.transform.Find(headingHudRootName) as RectTransform;
                    if (_headingHudRoot != null)
                    {
                        _headingHeadFixedAnchoredPosition = _headingHudRoot.anchoredPosition;
                        _headingHeadFixedLocalRotation = _headingHudRoot.localRotation;
                        _headingHeadFixedRootCaptured = true;
                    }
                }

            }

            // A collimated HUD is angular, not a sign 175m in front of the
            // aircraft. Camera translation lag, head translation, and floating
            // origin changes must not move the flight reference across the glass.
            if (!TryProjectReference(projectionCamera, reference, out Vector2 screenPoint, out Vector2 screenUp))
            {
                // Never freeze a visible reference in the last forward position
                // while looking behind. Leave independently controlled UI alone.
                MoveOutsideView(_screenCanvas, _screenHudRoot, _headFixedAnchoredPosition);
                if (projectHeadingTape)
                    MoveOutsideView(_headingCanvas, _headingHudRoot, _headingHeadFixedAnchoredPosition);
                return;
            }
            ProjectRoot(_screenCanvas, _screenHudRoot, _headFixedAnchoredPosition, _headFixedLocalRotation,
                screenPoint, screenUp);
            if (projectHeadingTape)
                ProjectRoot(_headingCanvas, _headingHudRoot, _headingHeadFixedAnchoredPosition,
                    _headingHeadFixedLocalRotation, screenPoint, screenUp);
        }

        public static bool TryProjectReference(UnityEngine.Camera camera, Quaternion reference,
            out Vector2 screenPoint, out Vector2 screenUp)
        {
            screenPoint = screenUp = Vector2.zero;
            if (camera == null) return false;
            Quaternion relative = Quaternion.Inverse(camera.transform.rotation) * reference;
            Vector3 forward = relative * Vector3.forward;
            Vector3 up = relative * (Vector3.forward + Vector3.up * .08f);
            if (forward.z <= .01f || up.z <= .01f) return false;

            // Work in rotation-only camera space. No subtraction of large world
            // positions, no finite-distance parallax, and no TAA sample jitter.
            Matrix4x4 matrix = camera.nonJitteredProjectionMatrix;
            Vector4 center = matrix * new Vector4(forward.x, forward.y, -forward.z, camera.orthographic ? 1 : 0);
            Vector4 top = matrix * new Vector4(up.x, up.y, -up.z, camera.orthographic ? 1 : 0);
            if (Mathf.Abs(center.w) < .0001f || Mathf.Abs(top.w) < .0001f) return false;
            Rect pixels = camera.pixelRect;
            screenPoint = new Vector2(pixels.x + (center.x / center.w + 1f) * .5f * pixels.width,
                pixels.y + (center.y / center.w + 1f) * .5f * pixels.height);
            screenUp = new Vector2((top.x / top.w - center.x / center.w) * .5f * pixels.width,
                (top.y / top.w - center.y / center.w) * .5f * pixels.height);
            return IsFinite(screenPoint.x) && IsFinite(screenPoint.y) && IsFinite(screenUp.x) && IsFinite(screenUp.y);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void MoveOutsideView(Canvas canvas, RectTransform root, Vector2 basePosition)
        {
            if (canvas != null && root != null)
                root.anchoredPosition = basePosition + new Vector2(GetReferenceResolution(canvas).x * 4f, 0f);
        }

        private void ProjectRoot(Canvas canvas, RectTransform root, Vector2 basePosition,
            Quaternion baseRotation, Vector2 screenPoint, Vector2 screenUp)
        {
            if (canvas == null || root == null)
            {
                return;
            }

            Vector2 referenceResolution = GetReferenceResolution(canvas);
            Rect pixelRect = projectionCamera.pixelRect;
            float pixelWidth = Mathf.Max(1f, pixelRect.width);
            float pixelHeight = Mathf.Max(1f, pixelRect.height);
            Vector2 offset = new Vector2(
                (screenPoint.x - (pixelRect.x + pixelWidth * 0.5f)) * referenceResolution.x / pixelWidth,
                (screenPoint.y - (pixelRect.y + pixelHeight * 0.5f)) * referenceResolution.y / pixelHeight);
            root.anchoredPosition = basePosition + offset;

            if (screenUp.sqrMagnitude > 0.01f)
            {
                float roll = Mathf.Atan2(screenUp.y, screenUp.x) * Mathf.Rad2Deg - 90f;
                root.localRotation = baseRotation * Quaternion.Euler(0f, 0f, roll);
            }
        }

        private void RestoreHeadFixedRoot()
        {
            if (_headFixedRootCaptured && _screenHudRoot != null)
            {
                _screenHudRoot.anchoredPosition = _headFixedAnchoredPosition;
                _screenHudRoot.localRotation = _headFixedLocalRotation;
            }
            if (_headingHeadFixedRootCaptured && _headingHudRoot != null)
            {
                _headingHudRoot.anchoredPosition = _headingHeadFixedAnchoredPosition;
                _headingHudRoot.localRotation = _headingHeadFixedLocalRotation;
            }
        }

        private static Vector2 GetReferenceResolution(Canvas canvas)
        {
            UnityEngine.UI.CanvasScaler scaler = canvas != null ? canvas.GetComponent<UnityEngine.UI.CanvasScaler>() : null;
            if (scaler != null && scaler.uiScaleMode == UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                scaler.referenceResolution.x > 1f && scaler.referenceResolution.y > 1f)
            {
                return scaler.referenceResolution;
            }

            return new Vector2(1920f, 1080f);
        }

        private void ConfigureConformalCanvas()
        {
            if (_conformalCanvas == null)
            {
                return;
            }

            Transform canvasTransform = _conformalCanvas.transform;
            if (aircraftTransform != null && canvasTransform.parent != aircraftTransform)
            {
                // Preserve the authored local placement when repairing an old
                // scene whose duplicate canvas was detached from the aircraft.
                canvasTransform.SetParent(aircraftTransform, false);
            }

            if (aircraftTransform != null && alignToAircraftReference)
            {
                AircraftCameraController cameraController = projectionCamera != null
                    ? projectionCamera.GetComponent<AircraftCameraController>()
                    : null;
                Quaternion aircraftReference = cameraController != null
                    ? cameraController.AircraftReferenceRotation
                    : aircraftTransform.rotation;
                if (aircraftReference == default)
                {
                    aircraftReference = aircraftTransform.rotation;
                }

                // This is the only transform update performed by conformal
                // mode. It is derived from aircraft attitude, not camera
                // look-offset, so a side-window scan cannot drag the HUD.
                canvasTransform.SetPositionAndRotation(
                    aircraftTransform.position + aircraftReference * (Vector3.forward * Mathf.Max(1f, conformalDistance)),
                    aircraftReference);
            }

            _conformalCanvas.renderMode = RenderMode.WorldSpace;
            _conformalCanvas.worldCamera = projectionCamera;
            _conformalCanvas.overrideSorting = true;
            _conformalCanvas.sortingOrder = conformalSortingOrder;

            // The duplicate canvas is intentionally dormant in the authored
            // scene. Re-enable only the primary symbology group; radar, voice,
            // and analysis overlays stay independently controlled.
            Transform conformalHudRoot = _conformalCanvas.transform.Find(conformalHudRootName);
            if (conformalHudRoot != null)
            {
                conformalHudRoot.gameObject.SetActive(true);
                Canvas childCanvas = conformalHudRoot.GetComponent<Canvas>();
                if (childCanvas != null)
                {
                    childCanvas.enabled = true;
                    childCanvas.renderMode = RenderMode.WorldSpace;
                    childCanvas.worldCamera = projectionCamera;
                    childCanvas.overrideSorting = true;
                    childCanvas.sortingOrder = conformalSortingOrder + 1;
                }

                UnityEngine.UI.CanvasScaler childScaler = conformalHudRoot.GetComponent<UnityEngine.UI.CanvasScaler>();
                if (childScaler != null)
                {
                    childScaler.enabled = true;
                }
            }

            GraphicRaycaster raycaster = _conformalCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = !disableConformalRaycasts;
            }

            UnityEngine.UI.CanvasScaler scaler = _conformalCanvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null)
            {
                scaler.enabled = true;
            }
        }

        private static void SetCanvasVisible(Canvas canvas, bool visible)
        {
            if (canvas == null)
            {
                return;
            }

            if (!canvas.gameObject.activeSelf)
            {
                canvas.gameObject.SetActive(visible);
            }
            else if (!visible)
            {
                canvas.gameObject.SetActive(false);
            }

            canvas.enabled = visible;
        }

        private static Canvas FindCanvas(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas != null && canvas.gameObject.name == objectName && IsLoadedSceneObject(canvas.gameObject))
                {
                    return canvas;
                }
            }

            return null;
        }

        private static bool IsLoadedSceneObject(GameObject target)
        {
            return target != null && target.scene.IsValid() && target.scene.isLoaded;
        }
    }
}
