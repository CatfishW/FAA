using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using WeatherRadar;

#pragma warning disable CS0649 // Serialized fields are configured by the editor setup menu.

namespace FAA.Headset
{
    /// <summary>
    /// Bridges the FAA scene output to a native Varjo XR-3 session or to the
    /// XR Interaction Toolkit's desktop simulator. The legacy SA-147 path is
    /// left intact and is only suspended while this component is active.
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    [DefaultExecutionOrder(11010)]
    [DisallowMultipleComponent]
    [AddComponentMenu("FAA/Headset/Varjo XR-3 Compatibility")]
    public sealed class XR3HeadsetCompatibility : MonoBehaviour
    {
        public enum ActivationMode
        {
            Auto,
            VarjoXR3,
            UnitySimulator,
        }

        private const string RuntimeObjectName = "FAA XR-3 Headset Compatibility";
        private const string SimulatorResourcePath = "FAA/XR3/XR Interaction Simulator";
        private const string SimulatorUiName = "XR Interaction Simulator UI";
        private const string SimulatorPlayModeMenuName = "PlayModeMenu";
        private const string SimulatorInputSelectionWindowName = "InputSelectionWindow";
        private const string SimulatorInputSelectionClosedWindowName = "InputSelectionClosedWindow";

        [Header("Activation")]
        [SerializeField] private ActivationMode activationMode = ActivationMode.Auto;
        [SerializeField] private bool autoDetectNativeXr = true;
        [SerializeField] private bool enableEditorSimulator;
        [SerializeField] private bool enableSimulatorInPlayer;
        [SerializeField] private GameObject simulatorPrefab;

        [Header("XR Camera")]
        [SerializeField] private Camera xrCamera;
        [Tooltip("Drive the FAA camera from Varjo/XR Interaction Simulator HMD pose input.")]
        [SerializeField] private bool driveCameraFromXrHmd = true;
        [SerializeField] private bool routeOverlayCanvasesToXrCamera = true;
        [Tooltip("Keep FAA HUD and radar canvases in screen-space overlay mode while using the desktop XR simulator. Native Varjo mode uses camera-space canvases for stereo output.")]
        [SerializeField] private bool useScreenSpaceOverlayForSimulator = true;
        [Tooltip("Keep the desktop pointer available for FAA's screen-space controls while the Unity XR simulator is running in the Editor. Disable this only when testing controller point-and-click input with a camera-space XR UI.")]
        [SerializeField] private bool preferEditorPointerInput = true;
        [Header("Simulator UI Layout")]
        [Tooltip("Move the XR Interaction Simulator input-selection menu to the upper-left safe area so it does not cover the FAA weather/traffic radar pair.")]
        [SerializeField] private bool repositionSimulatorInputSelection = true;
        [Tooltip("Margin in simulator canvas reference pixels from the upper-left safe area.")]
        [SerializeField] private Vector2 simulatorInputSelectionMargin = new Vector2(18f, 18f);
        [SerializeField] private string[] overlayCanvasNames =
        {
            "FAASymbologyCanvas",
            "FAASymbologyCanvasWorldSpace",
            "FAAHeadingTapeCanvas",
            "OverlayCanvas",
            "XPlaneWeatherIndicatorCanvas",
            "XPlaneWeatherRadarCanvas",
            "XPlaneTrafficRadarCanvas",
        };

        [Header("Legacy SA-147 Coordination")]
        [SerializeField] private bool suspendLegacySa147WhileActive = true;
        [SerializeField] private bool logActivation = true;

        [Header("Simulator Radar Data")]
        [Tooltip("Generate a local weather-radar texture while the XR-3 desktop simulator is active and X-Plane is not connected.")]
        [SerializeField] private bool enableSimulatorWeatherFallback = true;

        private readonly Dictionary<Canvas, CanvasState> _canvasStates = new Dictionary<Canvas, CanvasState>();
        private readonly List<Behaviour> _suspendedSa147Components = new List<Behaviour>();
        private readonly List<GameObject> _suspendedSa147Objects = new List<GameObject>();
        private GameObject _simulatorInstance;
        private bool _ownsSimulatorInstance;
        private bool _simulatorPointAndClickStateCaptured;
        private bool _simulatorOriginalPointAndClick;
        private TrackedPoseDriver _trackedPoseDriver;
        private Camera _trackedPoseDriverCamera;
        private bool _createdTrackedPoseDriver;
        private bool _existingTrackedPoseDriverEnabled;
        private bool _existingTrackedPoseDriverStateCaptured;
        private bool _active;
        private ActivationMode _activeMode;
        private bool _simulatorInputSelectionLayoutConfigured;
        private int _nativeDetectionFrames;
        private bool _reportedUnavailable;

        private struct CanvasState
        {
            public Canvas canvas;
            public RenderMode renderMode;
            public Camera worldCamera;
            public float planeDistance;
            public int targetDisplay;
            public RectTransform rectTransform;
            public bool hasRectTransformState;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public Vector2 pivot;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeHost()
        {
            if (FindFirstObjectByType<XR3HeadsetCompatibility>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            GameObject host = new GameObject(RuntimeObjectName);
            DontDestroyOnLoad(host);
            host.AddComponent<XR3HeadsetCompatibility>();
        }

        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            EvaluateActivation();
        }

        private void Start()
        {
            EvaluateActivation();
            if (!_active && autoDetectNativeXr)
            {
                StartCoroutine(WaitForNativeXrStartup());
            }
        }

        private void Update()
        {
            if (!_active && autoDetectNativeXr && activationMode == ActivationMode.Auto)
            {
                // Varjo's loader can finish initialization after the first
                // scene frame. Keep this check cheap and bounded for projects
                // that do not have an XR runtime installed.
                _nativeDetectionFrames++;
                if ((_nativeDetectionFrames & 15) == 0 && IsNativeXrActive())
                {
                    Activate(ActivationMode.VarjoXR3, false);
                }
            }

            // The simulator UI is instantiated by XRI one frame after the
            // simulator root in some editor versions. Retry only until the
            // two input-selection windows have been positioned, rather than
            // walking the full UI hierarchy every frame for the rest of the
            // session.
            if (_active && _activeMode == ActivationMode.UnitySimulator &&
                !_simulatorInputSelectionLayoutConfigured)
            {
                ConfigureSimulatorInputSelectionLayout();
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ConfigureSimulatorWeatherFallback(false);
            RestoreCanvases();
            RestoreLegacySa147();
            RestoreTrackedPoseDriver();
            DestroySimulator();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_active)
            {
                _simulatorInputSelectionLayoutConfigured = false;
                if (suspendLegacySa147WhileActive)
                {
                    SuspendLegacySa147();
                }

                ConfigureXrCamera();
                EnsureTrackedPoseDriver();
                RouteOverlayCanvases();
                ConfigureSimulatorPointerInput();
                ConfigureSimulatorWeatherFallback(_activeMode == ActivationMode.UnitySimulator);
            }
            else
            {
                EvaluateActivation();
            }
        }

        [ContextMenu("Enable Varjo XR-3 Mode")]
        public void EnableVarjoXR3()
        {
            Activate(ActivationMode.VarjoXR3, false);
        }

        [ContextMenu("Enable Unity XR Interaction Simulator")]
        public void EnableUnitySimulator()
        {
            Activate(ActivationMode.UnitySimulator, true);
        }

        [ContextMenu("Disable XR-3 Compatibility")]
        public void DisableCompatibility()
        {
            _active = false;
            ConfigureSimulatorWeatherFallback(false);
            RestoreCanvases();
            RestoreLegacySa147();
            RestoreTrackedPoseDriver();
            DestroySimulator();
        }

        private void EvaluateActivation()
        {
            bool forceVarjo = activationMode == ActivationMode.VarjoXR3 || HasAnyCommandLineFlag(
                "-xr3", "--xr3", "-varjo", "--varjo", "-varjo-xr3", "--varjo-xr3");
            bool forceSimulator = activationMode == ActivationMode.UnitySimulator || HasAnyCommandLineFlag(
                "-xr3-sim", "--xr3-sim", "-xr-simulator", "--xr-simulator", "-varjo-sim", "--varjo-sim");

            if (forceVarjo || (autoDetectNativeXr && IsNativeXrActive()))
            {
                Activate(ActivationMode.VarjoXR3, false);
                return;
            }

            if (forceSimulator || (Application.isEditor && enableEditorSimulator) ||
                (!Application.isEditor && enableSimulatorInPlayer))
            {
                Activate(ActivationMode.UnitySimulator, true);
            }
        }

        private IEnumerator WaitForNativeXrStartup()
        {
            const int maxFrames = 180;
            for (int i = 0; i < maxFrames && !_active; i++)
            {
                if (IsNativeXrActive())
                {
                    Activate(ActivationMode.VarjoXR3, false);
                    yield break;
                }

                yield return null;
            }
        }

        private void Activate(ActivationMode mode, bool startSimulator)
        {
            bool modeChanged = _active && _activeMode != mode;
            if (modeChanged)
            {
                ConfigureSimulatorWeatherFallback(false);
                RestoreCanvases();
                _simulatorInputSelectionLayoutConfigured = false;
            }

            bool wasActive = _active && _activeMode == mode;
            _active = true;
            _activeMode = mode;
            Application.runInBackground = true;

            if (suspendLegacySa147WhileActive)
            {
                SuspendLegacySa147();
            }

            ConfigureXrCamera();
            EnsureTrackedPoseDriver();
            RouteOverlayCanvases();

            if (startSimulator || mode == ActivationMode.UnitySimulator)
            {
                EnsureSimulator();
                ConfigureSimulatorPointerInput();
                ConfigureSimulatorInputSelectionLayout();
            }
            else
            {
                _simulatorInputSelectionLayoutConfigured = false;
                DestroySimulator();
            }

            ConfigureSimulatorWeatherFallback(mode == ActivationMode.UnitySimulator);

            if (!wasActive && logActivation)
            {
                string provider = mode == ActivationMode.UnitySimulator ? "Unity XR Interaction Simulator" : "Varjo XR-3";
                Debug.Log($"[FAA XR] {provider} output active. HUD/weather/traffic overlays are routed to the XR camera.", this);
            }
        }

        private void ConfigureXrCamera()
        {
            if (xrCamera == null || !xrCamera.isActiveAndEnabled)
            {
                xrCamera = Camera.main;
            }

            if (xrCamera == null)
            {
                if (!_reportedUnavailable)
                {
                    Debug.LogWarning("[FAA XR] No MainCamera is available; XR-3 tracking will be picked up when a camera appears.", this);
                    _reportedUnavailable = true;
                }

                return;
            }

            _reportedUnavailable = false;
            xrCamera.stereoTargetEye = StereoTargetEyeMask.Both;
            xrCamera.targetDisplay = 0;
        }

        private void EnsureTrackedPoseDriver()
        {
            if (!driveCameraFromXrHmd || xrCamera == null)
            {
                return;
            }

            // A scene transition can replace the camera while this bridge is
            // kept alive. Restore the old camera before binding the new one.
            if (_trackedPoseDriverCamera != null && _trackedPoseDriverCamera != xrCamera)
            {
                RestoreTrackedPoseDriver();
            }

            if (_trackedPoseDriver != null)
            {
                if (!_trackedPoseDriver.enabled)
                {
                    _trackedPoseDriver.enabled = true;
                }

                return;
            }

            _trackedPoseDriverCamera = xrCamera;
            _trackedPoseDriver = xrCamera.GetComponent<TrackedPoseDriver>();
            if (_trackedPoseDriver != null)
            {
                _existingTrackedPoseDriverEnabled = _trackedPoseDriver.enabled;
                _existingTrackedPoseDriverStateCaptured = true;
                _trackedPoseDriver.enabled = true;
                return;
            }

            _trackedPoseDriver = xrCamera.gameObject.AddComponent<TrackedPoseDriver>();
            _createdTrackedPoseDriver = true;
            _trackedPoseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            _trackedPoseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            _trackedPoseDriver.ignoreTrackingState = false;

            // These bindings are the same camera bindings used by Unity's XR
            // Main Camera factory. The AR fallback keeps the simulator useful
            // on machines where a native HMD is not connected.
            var positionAction = new InputAction(
                "Position",
                binding: "<XRHMD>/centerEyePosition",
                expectedControlType: "Vector3");
            positionAction.AddBinding("<HandheldARInputDevice>/devicePosition");

            var rotationAction = new InputAction(
                "Rotation",
                binding: "<XRHMD>/centerEyeRotation",
                expectedControlType: "Quaternion");
            rotationAction.AddBinding("<HandheldARInputDevice>/deviceRotation");

            var trackingStateAction = new InputAction(
                "Tracking State",
                binding: "<XRHMD>/trackingState",
                expectedControlType: "Integer");

            _trackedPoseDriver.positionInput = new InputActionProperty(positionAction);
            _trackedPoseDriver.rotationInput = new InputActionProperty(rotationAction);
            _trackedPoseDriver.trackingStateInput = new InputActionProperty(trackingStateAction);
        }

        private void RestoreTrackedPoseDriver()
        {
            if (_trackedPoseDriver != null)
            {
                if (_createdTrackedPoseDriver)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(_trackedPoseDriver);
                    }
                    else
                    {
                        DestroyImmediate(_trackedPoseDriver);
                    }
                }
                else if (_existingTrackedPoseDriverStateCaptured)
                {
                    _trackedPoseDriver.enabled = _existingTrackedPoseDriverEnabled;
                }
            }

            _trackedPoseDriver = null;
            _trackedPoseDriverCamera = null;
            _createdTrackedPoseDriver = false;
            _existingTrackedPoseDriverStateCaptured = false;
        }

        private void RouteOverlayCanvases()
        {
            if (!routeOverlayCanvasesToXrCamera || xrCamera == null)
            {
                return;
            }

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || !ShouldRouteCanvas(canvas))
                {
                    continue;
                }

                if (!_canvasStates.ContainsKey(canvas))
                {
                    CanvasState state = new CanvasState
                    {
                        canvas = canvas,
                        renderMode = canvas.renderMode,
                        worldCamera = canvas.worldCamera,
                        planeDistance = canvas.planeDistance,
                        targetDisplay = canvas.targetDisplay,
                    };

                    if (IsRootCanvas(canvas))
                    {
                        RectTransform rectTransform = canvas.GetComponent<RectTransform>();
                        if (rectTransform != null)
                        {
                            state.rectTransform = rectTransform;
                            state.hasRectTransformState = true;
                            state.anchorMin = rectTransform.anchorMin;
                            state.anchorMax = rectTransform.anchorMax;
                            state.anchoredPosition = rectTransform.anchoredPosition;
                            state.sizeDelta = rectTransform.sizeDelta;
                            state.pivot = rectTransform.pivot;
                            state.localPosition = rectTransform.localPosition;
                            state.localRotation = rectTransform.localRotation;
                            state.localScale = rectTransform.localScale;
                        }
                    }

                    _canvasStates.Add(canvas, state);
                }

                // World-space canvases already follow the XR camera naturally.
                // The desktop simulator renders the FAA UI in the Game view,
                // where screen-space overlay is the most reliable presentation.
                // Native Varjo mode uses camera-space canvases for stereo output.
                if (canvas.renderMode != RenderMode.WorldSpace &&
                    _activeMode == ActivationMode.UnitySimulator &&
                    useScreenSpaceOverlayForSimulator)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = null;
                    canvas.targetDisplay = 0;
                    NormalizeOverlayCanvasLayout(canvas);
                }
                else if (canvas.renderMode != RenderMode.WorldSpace)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = xrCamera;
                    canvas.planeDistance = Mathf.Max(xrCamera.nearClipPlane + 0.05f, 0.4f);
                    canvas.targetDisplay = 0;
                    NormalizeCameraCanvasLayout(canvas);
                }

                if (_activeMode != ActivationMode.UnitySimulator || !useScreenSpaceOverlayForSimulator)
                {
                    xrCamera.cullingMask |= 1 << canvas.gameObject.layer;
                }
            }
        }

        private static bool IsRootCanvas(Canvas canvas)
        {
            if (canvas == null)
            {
                return false;
            }

            Transform parent = canvas.transform.parent;
            while (parent != null)
            {
                if (parent.GetComponent<Canvas>() != null)
                {
                    return false;
                }

                parent = parent.parent;
            }

            return true;
        }

        private static void NormalizeCameraCanvasLayout(Canvas canvas)
        {
            if (!IsRootCanvas(canvas))
            {
                return;
            }

            RectTransform rectTransform = canvas.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return;
            }

            // A ScreenSpaceOverlay canvas stores pixel coordinates (for example
            // 960,540). Once it becomes camera-space those values are world
            // coordinates, which moves the whole HUD outside the frustum.
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }

        private static void NormalizeOverlayCanvasLayout(Canvas canvas)
        {
            // The sanitizer already applies this layout, but reassert it when
            // switching from native camera-space mode back to the simulator.
            NormalizeCameraCanvasLayout(canvas);
        }

        private bool ShouldRouteCanvas(Canvas canvas)
        {
            string path = GetHierarchyPath(canvas.transform);
            if (overlayCanvasNames == null)
            {
                return false;
            }

            for (int i = 0; i < overlayCanvasNames.Length; i++)
            {
                string name = overlayCanvasNames[i];
                if (!string.IsNullOrWhiteSpace(name) && path.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void RestoreCanvases()
        {
            foreach (CanvasState state in _canvasStates.Values)
            {
                if (state.canvas == null)
                {
                    continue;
                }

                state.canvas.renderMode = state.renderMode;
                state.canvas.worldCamera = state.worldCamera;
                state.canvas.planeDistance = state.planeDistance;
                state.canvas.targetDisplay = state.targetDisplay;

                if (state.hasRectTransformState && state.rectTransform != null)
                {
                    state.rectTransform.anchorMin = state.anchorMin;
                    state.rectTransform.anchorMax = state.anchorMax;
                    state.rectTransform.anchoredPosition = state.anchoredPosition;
                    state.rectTransform.sizeDelta = state.sizeDelta;
                    state.rectTransform.pivot = state.pivot;
                    state.rectTransform.localPosition = state.localPosition;
                    state.rectTransform.localRotation = state.localRotation;
                    state.rectTransform.localScale = state.localScale;
                }
            }

            _canvasStates.Clear();
        }

        private void ConfigureSimulatorWeatherFallback(bool simulatorActive)
        {
            bool enableFallback = simulatorActive && enableSimulatorWeatherFallback;
            XPlaneOriginalWeatherRadarProvider[] providers =
                FindObjectsByType<XPlaneOriginalWeatherRadarProvider>(FindObjectsInactive.Include);

            foreach (XPlaneOriginalWeatherRadarProvider provider in providers)
            {
                if (provider != null)
                {
                    provider.SetSimulatorFallbackEnabled(enableFallback);
                }
            }
        }

        private void SuspendLegacySa147()
        {
            _suspendedSa147Components.RemoveAll(component => component == null);
            _suspendedSa147Objects.RemoveAll(legacyObject => legacyObject == null);
            SA147HeadsetCompatibility[] compatibilityComponents =
                FindObjectsByType<SA147HeadsetCompatibility>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (SA147HeadsetCompatibility component in compatibilityComponents)
            {
                if (component != null && component.enabled)
                {
                    component.enabled = false;
                    if (!_suspendedSa147Components.Contains(component))
                    {
                        _suspendedSa147Components.Add(component);
                    }
                }
            }

            GameObject[] legacyObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (GameObject legacyObject in legacyObjects)
            {
                if (legacyObject == null)
                {
                    continue;
                }

                if (legacyObject.name == "SA_147_Prefab" || legacyObject.name == "SA-147 Archer Head Tracker")
                {
                    if (legacyObject.activeSelf)
                    {
                        legacyObject.SetActive(false);
                        if (!_suspendedSa147Objects.Contains(legacyObject))
                        {
                            _suspendedSa147Objects.Add(legacyObject);
                        }
                    }
                }
            }
        }

        private void RestoreLegacySa147()
        {
            foreach (Behaviour component in _suspendedSa147Components)
            {
                if (component != null)
                {
                    component.enabled = true;
                }
            }

            _suspendedSa147Components.Clear();

            foreach (GameObject legacyObject in _suspendedSa147Objects)
            {
                if (legacyObject != null)
                {
                    legacyObject.SetActive(true);
                }
            }

            _suspendedSa147Objects.Clear();
        }

        private void EnsureSimulator()
        {
            if (_simulatorInstance != null)
            {
                return;
            }

            // The editor setup places a clearly named simulator prefab in the
            // scene so it is visible in the Hierarchy. Reuse that object at
            // runtime instead of creating a second simulator instance.
            GameObject sceneSimulator = FindSceneSimulator();
            if (sceneSimulator != null)
            {
                _simulatorInstance = sceneSimulator;
                _ownsSimulatorInstance = false;
                _simulatorInstance.SetActive(true);
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(_simulatorInstance);
                }

                return;
            }

            GameObject prefab = simulatorPrefab;
            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>(SimulatorResourcePath);
            }

#if UNITY_EDITOR
            if (prefab == null)
            {
                prefab = FindEditorSimulatorPrefab();
            }
#endif

            if (prefab == null)
            {
                if (!_reportedUnavailable)
                {
                    Debug.LogWarning("[FAA XR] XR Interaction Simulator sample is not imported. Use FAA/Headset/Install XR-3 Simulator Sample.", this);
                    _reportedUnavailable = true;
                }

                return;
            }

            _simulatorInstance = Instantiate(prefab);
            _simulatorInstance.name = "FAA XR-3 Unity Simulator";
            DontDestroyOnLoad(_simulatorInstance);
            _ownsSimulatorInstance = true;
        }

        /// <summary>
        /// XRI's simulator disables XRUI mouse/touch input while its
        /// point-and-click controller mode is active. FAA's editor simulator
        /// presents the radar and control strip as ScreenSpaceOverlay, so keep
        /// the normal desktop pointer path enabled for those controls. Native
        /// Varjo mode and camera-space XR UI retain the simulator's original
        /// setting.
        /// </summary>
        private void ConfigureSimulatorPointerInput()
        {
            if (_simulatorInstance == null)
            {
                return;
            }

            XRInteractionSimulator simulator = _simulatorInstance.GetComponent<XRInteractionSimulator>();
            if (simulator == null)
            {
                return;
            }

            if (!_simulatorPointAndClickStateCaptured)
            {
                _simulatorOriginalPointAndClick = simulator.usePointAndClick;
                _simulatorPointAndClickStateCaptured = true;
            }

            bool useEditorPointer = Application.isEditor &&
                _activeMode == ActivationMode.UnitySimulator &&
                useScreenSpaceOverlayForSimulator &&
                preferEditorPointerInput;
            simulator.usePointAndClick = useEditorPointer ? false : _simulatorOriginalPointAndClick;
        }

        /// <summary>
        /// XRI's sample UI places its input-selection strip at the lower-left
        /// corner by default. FAA uses both lower corners for the weather and
        /// traffic radar displays, so the strip can obscure the radar image
        /// and intercept its pointer events. Keep the XRI menu functional but
        /// move both its collapsed and expanded windows into the upper-left
        /// safe area. The windows are children of a VerticalLayoutGroup; an
        /// ignored LayoutElement is required so that group's next rebuild does
        /// not snap them back to the lower-left position.
        /// </summary>
        private void ConfigureSimulatorInputSelectionLayout()
        {
            if (!repositionSimulatorInputSelection)
            {
                // Avoid a per-frame retry loop when a project deliberately
                // opts out of the safe-area relocation.
                _simulatorInputSelectionLayoutConfigured = true;
                return;
            }

            if (_simulatorInstance == null || _activeMode != ActivationMode.UnitySimulator)
            {
                return;
            }

            Transform simulatorUi = FindSimulatorUiTransform();
            if (simulatorUi == null)
            {
                return;
            }

            Transform playModeMenu = FindDescendantByName(simulatorUi, SimulatorPlayModeMenuName);
            if (playModeMenu == null)
            {
                return;
            }

            RectTransform menuRect = playModeMenu as RectTransform;
            if (menuRect == null)
            {
                menuRect = playModeMenu.GetComponent<RectTransform>();
            }

            bool configured = false;
            configured |= PositionSimulatorInputSelectionWindow(
                FindDescendantByName(playModeMenu, SimulatorInputSelectionClosedWindowName),
                menuRect);
            configured |= PositionSimulatorInputSelectionWindow(
                FindDescendantByName(playModeMenu, SimulatorInputSelectionWindowName),
                menuRect);

            if (configured)
            {
                _simulatorInputSelectionLayoutConfigured = true;
            }
        }

        private Transform FindSimulatorUiTransform()
        {
            if (_simulatorInstance != null)
            {
                Transform[] descendants = _simulatorInstance.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < descendants.Length; i++)
                {
                    Transform candidate = descendants[i];
                    if (candidate != null && IsSimulatorUiName(candidate.name))
                    {
                        return candidate;
                    }
                }
            }

            // A nested UI prefab can be promoted out of the simulator root by
            // XRI's DontDestroyOnLoad handling. Fall back to a global lookup so
            // the layout still applies after a scene transition.
            Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform candidate = allTransforms[i];
                if (candidate != null && IsSimulatorUiName(candidate.name))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsSimulatorUiName(string objectName)
        {
            return !string.IsNullOrEmpty(objectName) &&
                objectName.StartsWith(SimulatorUiName, StringComparison.OrdinalIgnoreCase);
        }

        private static Transform FindDescendantByName(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform candidate = descendants[i];
                if (candidate != null && string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private bool PositionSimulatorInputSelectionWindow(Transform window, RectTransform menuRect)
        {
            if (window == null)
            {
                return false;
            }

            RectTransform rect = window as RectTransform;
            if (rect == null)
            {
                rect = window.GetComponent<RectTransform>();
            }

            if (rect == null)
            {
                return false;
            }

            LayoutElement layoutElement = window.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = window.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.ignoreLayout = true;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(
                Mathf.Max(0f, simulatorInputSelectionMargin.x),
                -Mathf.Max(0f, simulatorInputSelectionMargin.y));

            // Keep the menu above other simulator controls while preserving
            // XRI's own interaction and active-state toggling.
            rect.SetAsLastSibling();
            if (menuRect != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(menuRect);
            }

            return true;
        }

        private void RestoreSimulatorPointerInput()
        {
            if (!_simulatorPointAndClickStateCaptured)
            {
                return;
            }

            if (_simulatorInstance != null)
            {
                XRInteractionSimulator simulator = _simulatorInstance.GetComponent<XRInteractionSimulator>();
                if (simulator != null)
                {
                    simulator.usePointAndClick = _simulatorOriginalPointAndClick;
                }
            }

            _simulatorPointAndClickStateCaptured = false;
        }

        private static GameObject FindSceneSimulator()
        {
            GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (GameObject candidate in objects)
            {
                if (candidate != null && candidate.name == "FAA XR-3 Unity Simulator")
                {
                    return candidate;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        private static GameObject FindEditorSimulatorPrefab()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("XR Interaction Simulator t:Prefab");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                GameObject candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (candidate != null && candidate.name == "XR Interaction Simulator")
                {
                    return candidate;
                }
            }

            return null;
        }
#endif

        private void DestroySimulator()
        {
            RestoreSimulatorPointerInput();

            if (_simulatorInstance == null)
            {
                return;
            }

            if (!_ownsSimulatorInstance)
            {
                _simulatorInstance = null;
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_simulatorInstance);
            }
            else
            {
                DestroyImmediate(_simulatorInstance);
            }

            _simulatorInstance = null;
            _ownsSimulatorInstance = false;
        }

        private static bool IsNativeXrActive()
        {
            if (XRSettings.isDeviceActive)
            {
                return true;
            }

            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (XRDisplaySubsystem display in displays)
            {
                if (display != null && display.running)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnyCommandLineFlag(params string[] flags)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                for (int j = 0; j < flags.Length; j++)
                {
                    if (string.Equals(args[i], flags[j], StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var names = new Stack<string>();
            while (transform != null)
            {
                names.Push(transform.name);
                transform = transform.parent;
            }

            return string.Join("/", names);
        }
    }
}
