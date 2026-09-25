using System;
using System.Collections.Generic;
using AircraftControl.Camera;
using AircraftControl.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace FAA.Customization
{
    /// <summary>
    /// Pilot-owned cockpit UI layout. Radar canvases use seat/aircraft space; only non-conformal
    /// modules have a size multiplier. Never changes projection, FOV, IPD, flight data or conformal cues.
    /// </summary>
    [DefaultExecutionOrder(12500), DisallowMultipleComponent]
    public sealed partial class FaaSpatialWorkspace : MonoBehaviour
    {
        public static FaaSpatialWorkspace Current { get; private set; }
        public Camera View { get; private set; }
        public Transform CockpitFrame { get; private set; }
        public bool Initialized { get; private set; }
        public bool EditMode { get; private set; }
        public bool SpatialPanelsEnabled { get; private set; }
        public bool NativeXr => View != null && (View.stereoEnabled || XRSettings.isDeviceActive);
        public string SelectedId { get; private set; } = "airspeed";
        public IReadOnlyList<FaaSpatialRadarPanel> Panels => panels;
        public IReadOnlyList<FaaSpatialRadarPanel> UtilityPanels => utilityPanels;
        public IEnumerable<FaaSpatialRadarPanel> InteractivePanels
        { get { foreach(var p in utilityPanels)yield return p;foreach(var p in panels)yield return p; } }
        public IReadOnlyList<FaaNonConformalScaleTarget> Modules => modules;
        public string InputStatus { get; set; } = "Mouse / controller; hand tracking unavailable";
        public string ProfileStatus { get; private set; } = "Default layout";
        public bool PersistChanges { get; set; } = true;

        private readonly List<FaaSpatialRadarPanel> panels = new();
        private readonly List<FaaSpatialRadarPanel> utilityPanels = new();
        private readonly List<FaaNonConformalScaleTarget> modules = new();
        private readonly List<XRInputSubsystem> trackingSubsystems = new();
        private Transform aircraft;
        private Vector3 seatOffset;
        private Scene boundScene;
        private float bindAfter, saveAfter;
        private bool dirty, nativeDefaults, recenterPending;
        private string profileKey;
        private FaaWorkspaceGestureInput gestures;
        private FaaNonConformalReflow fixedInstrumentLayout;
        private UnityEngine.InputSystem.XR.TrackedPoseDriver trackedView;
        private AircraftCameraController desktopView;
        private readonly FaaTrackedSeatReference trackedSeat = new();
        private bool lastNativeXr;
        private float nextUiRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Current != null) return;
            var existing = FindFirstObjectByType<FaaSpatialWorkspace>();
            if (existing != null) { Current = existing; return; }
            var host = new GameObject("FAA Spatial Workspace");
            DontDestroyOnLoad(host); host.AddComponent<FaaSpatialWorkspace>();
        }

        private void Awake()
        {
            if (Current != null && Current != this) { enabled = false; Destroy(this); return; }
            Current = this;
            bindAfter = Time.unscaledTime + 1f;
            SceneManager.sceneLoaded += SceneLoaded;
        }
        private void OnEnable()
        {
            if (Current == null) Current = this;
            Application.onBeforeRender += RefreshBeforeRender;
        }
        private void OnDisable()
        {
            Application.onBeforeRender -= RefreshBeforeRender;
            CancelManipulation();
            if (gestures != null) gestures.enabled = false;
            SaveNow();
            ReleaseBindings();
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (Current == this) Current = null;
        }
        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            CancelManipulation();
            gestures?.CancelPointers();
            SetEditMode(false);
        }
        private void OnApplicationPause(bool paused) { if (paused) OnApplicationFocus(false); }
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SaveNow(); ReleaseBindings(); bindAfter = Time.unscaledTime + 1f;
        }

        private void LateUpdate()
        {
            if (!Initialized)
            {
                if (Time.unscaledTime < bindAfter) return;
                bindAfter = Time.unscaledTime + 1f;
                try { if (!TryBind()) return; }
                catch (Exception exception)
                {
                    Debug.LogException(exception,this);
                    ReleaseBindings(); // Never accumulate partial panels/modules when initialization fails.
                    return;
                }
            }
            if (View == null || aircraft == null) { ReleaseBindings(); return; }
            if (trackedView == null) trackedView = View.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            if (NativeXr && !lastNativeXr)
            {
                trackedSeat.Capture(View.transform);
                // A late-starting native loader must not leave the radars in desktop overlay mode.
                if (!nativeDefaults) { SaveNow(); ReleaseBindings(); bindAfter = Time.unscaledTime + .25f; return; }
            }
            lastNativeXr = NativeXr;
            if (recenterPending) { RecenterSeat(); recenterPending = false; }
            RefreshTransforms();
            HandleKeyboard();
            if (Time.unscaledTime >= nextUiRefresh)
            {
                nextUiRefresh = Time.unscaledTime + .1f;
                RefreshControls();
            }
            if (dirty && !IsManipulating && Time.unscaledTime >= saveAfter) SaveNow();
        }

        [BeforeRenderOrder(400)]
        private void RefreshBeforeRender() { if (Initialized && isActiveAndEnabled) RefreshTransforms(); }

        public void RefreshTransforms()
        {
            if (aircraft == null || View == null || CockpitFrame == null) return;
            // Never use view.rotation here: head turns must reveal, not drag, cockpit panels.
            // Desktop aircraft-camera smoothing may intentionally lag the physical aircraft by metres.
            // Its untracked eye is the preview seat origin; a real tracked HMD keeps the captured seat
            // independent of physical head translation. Rotation is NEVER taken from head look.
            bool desktopPreview = !NativeXr && (trackedView == null || !trackedView.isActiveAndEnabled);
            if (desktopPreview)
            {
                // Use the same smoothed aircraft reference as the displayed cockpit, but NEVER
                // the manual/head look offset. Raw telemetry rotation makes side controls swim
                // against the camera's filtered aircraft attitude during a click/drag.
                Quaternion reference = desktopView != null && desktopView.isActiveAndEnabled
                    ? desktopView.AircraftReferenceRotation : aircraft.rotation;
                CockpitFrame.SetPositionAndRotation(View.transform.position, reference);
            }
            else trackedSeat.Apply(CockpitFrame);
            CockpitFrame.localScale = Vector3.one;
            foreach (var panel in panels) panel.Apply(CockpitFrame, View, EditMode);
            foreach (var panel in utilityPanels) panel.Apply(CockpitFrame, View, EditMode);
            foreach (var module in modules) module.Apply();
            ApplySymbologyPresentation();
            if(CurrentSymbology==FaaSymbologyVersion.Digital)fixedInstrumentLayout?.Apply(View);
            UpdateControlCanvasPose();
            UpdateSelectionFrame();
        }

        private bool TryBind()
        {
            View = Camera.main;
            if (View == null) return false;
            trackedView = View.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            trackedSeat.Capture(View.transform);
            lastNativeXr = NativeXr;
            var cameraController = View.GetComponent<AircraftCameraController>();
            desktopView = cameraController;
            aircraft = cameraController != null ? cameraController.AircraftTransform : null;
            if (aircraft == null)
            {
                var controller = FindFirstObjectByType<AircraftController>();
                aircraft = controller != null ? controller.transform : null;
            }
            Canvas flight = FindCanvas("FAASymbologyCanvas");
            if (flight == null || aircraft == null) return false;
            boundScene = SceneManager.GetActiveScene();
            seatOffset = aircraft.InverseTransformPoint(View.transform.position);
            var cockpit = new GameObject("FAA Seat Reference (aircraft anchored)");
            cockpit.transform.SetParent(transform, false); CockpitFrame = cockpit.transform;
            var xr3 = FindFirstObjectByType<FAA.Headset.XR3HeadsetCompatibility>();
            nativeDefaults = NativeXr || xr3 != null && xr3.IsCompatibilityActive;
            SpatialPanelsEnabled = true; // Laptop and headset radars both stay in protected cockpit-side space.
            profileKey = "FAA.SpatialLayout.v1." + boundScene.name + (nativeDefaults ? ".XR" : ".Desktop");
            AddRadar("weather", "XPlaneWeatherRadarCanvas", "X-Plane Weather Radar System", -55f);
            AddRadar("traffic", "XPlaneTrafficRadarCanvas", "Traffic Radar System", 55f);
            Transform root = flight.transform.Find("Second Interation GUI");
            if (root != null)
            {
                AddModule("airspeed", "Airspeed", root.Find("Airspeed Indicator"));
                AddModule("altitude", "Altitude", root.Find("Altimeter"));
                // Slash is part of this authored object name, not a hierarchy separator.
                foreach (Transform child in root)
                    if (child.name == "NR/ENG Ind") AddModule("nr", "Rotor / engine RPM", child);
                AddModule("vertical-speed", "Vertical speed", root.Find("VSI"));
                AddModule("torque", "Engine torque", root.Find("Torque Panel"));
                AddModule("glideslope", "Glideslope deviation", root.Find("Glidescope"));
                AddModule("localizer", "Course deviation", root.Find("Localizer Position Ind."));
                AddModule("bank", "Bank / command references", root.Find("Bank Scale"));
                AddModule("legacy-compass", "Legacy heading panel", root.Find("Heading Panel"));
                // The Attitude branch contains the calibrated pitch ladder/boresight. It is deliberately excluded.
            }
            Canvas heading = FindCanvas("FAAHeadingTapeCanvas");
            if (heading != null) AddModule("heading", "Heading / compass tape", heading.transform.Find("FAA Heading Tape Overlay"));
            LoadProfile();
            fixedInstrumentLayout = new FaaNonConformalReflow(modules);
            BindSymbologyVersions(flight,root,heading);
            BuildControls();
            BindLaptopCamera();
            LoadUtilityProfile();
            gestures = GetComponent<FaaWorkspaceGestureInput>() ?? gameObject.AddComponent<FaaWorkspaceGestureInput>();
            gestures.Bind(this); gestures.enabled = true;
            foreach (var panel in panels) panel.SetSpatial(SpatialPanelsEnabled);
            SubsystemManager.GetSubsystems(trackingSubsystems);
            foreach (var subsystem in trackingSubsystems) subsystem.trackingOriginUpdated += TrackingOriginUpdated;
            Initialized = true;
            RefreshTransforms(); RefreshControls();
            return true;
        }

        private void TrackingOriginUpdated(XRInputSubsystem subsystem) { CancelManipulation(); recenterPending = true; }
        private static Canvas FindCanvas(string name)
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas.name == name && canvas.gameObject.scene.IsValid() && canvas.gameObject.scene.isLoaded) return canvas;
            return null;
        }
        private void AddRadar(string id, string canvasName, string rootName, float yaw)
        {
            Canvas canvas = FindCanvas(canvasName);
            if (canvas == null) return;
            var root = canvas.transform.Find(rootName) as RectTransform;
            if (root != null) panels.Add(new FaaSpatialRadarPanel(this, id, canvas, root, yaw));
        }
        private void AddModule(string id, string caption, Transform target)
        {
            if (target == null || target.GetComponentInChildren<FaaPitchLadderGraphic>(true) != null ||
                target.GetComponentInChildren<FaaRotorcraftConformalLayer>(true) != null) return;
            modules.Add(new FaaNonConformalScaleTarget(id, caption, target, nativeDefaults ? .72f : 1f));
        }

        public static bool OwnsCanvas(Canvas canvas)
        {
            if (canvas == null || Current == null || !Current.isActiveAndEnabled) return false;
            foreach (var panel in Current.InteractivePanels) if (panel.Canvas == canvas && panel.IsSpatial) return true;
            return false;
        }
        public static bool TryResizeRadar(FaaRadarKind kind, float steps)
        {
            if (Current == null || !Current.SpatialPanelsEnabled) return false;
            string id = kind == FaaRadarKind.Weather ? "weather" : "traffic";
            var entry = Current.GetEntry(id);
            if (entry == null) return false;
            Current.SetScale(id, entry.scale + Mathf.Sign(steps) * .05f); return true;
        }
        public static bool TrySetModuleScale(Transform target, float value)
        {
            if (Current == null) return false;
            foreach (var module in Current.modules)
                if (module.Target == target) { Current.SetScale(module.Id, value); return true; }
            return false;
        }

        public FaaSpatialLayoutEntry GetEntry(string id)
        {
            foreach(var panel in utilityPanels)if(panel.Id==id)return panel.Layout;
            foreach (var panel in panels) if (panel.Id == id) return panel.Layout;
            foreach (var module in modules) if (module.Id == id) return module.Layout;
            return null;
        }
        public FaaSpatialRadarPanel GetPanel(string id)
        {
            foreach(var panel in utilityPanels)if(panel.Id==id)return panel;
            foreach (var panel in panels) if (panel.Id == id) return panel;
            return null;
        }
        public void Select(string id)
        {
            if (GetEntry(id) == null) return;
            SelectedId = id; RefreshControls();
        }
        public void SetScale(string id, float scale)
        {
            LaptopCamera?.CancelSizing();
            if (!FaaSpatialLayoutMath.Finite(scale)) return;
            var entry = GetEntry(id); if (entry == null) return;
            entry.scale = FaaSpatialLayoutMath.Scale(scale); MarkChanged();
            RefreshTransforms(); RefreshControls();
        }
        public void AdjustSelectedScale(float delta)
        {
            var entry = GetEntry(SelectedId); if (entry != null) SetScale(SelectedId, entry.scale + delta);
        }
        public void SetEditMode(bool enabled)
        {
            LaptopCamera?.CancelSizing();
            CancelManipulation(); gestures?.CancelPointers();
            EditMode = enabled;
            if (!enabled) SaveNow();
            RefreshTransforms(); RefreshControls();
        }
        public void SetSpatialPanels(bool enabled)
        {
            // Legacy callers may request desktop mode. Never restore obstructing forward overlays.
            CancelManipulation(); SpatialPanelsEnabled = true;
            foreach (var panel in panels) panel.SetSpatial(true);
            RefreshTransforms(); RefreshControls();
        }
        public void MovePanel(string id, Vector3 worldPosition)
        {
            var panel = GetPanel(id);
            if (panel == null || CockpitFrame == null) return;
            if (FaaSpatialLayoutMath.SetPosition(panel.Layout, CockpitFrame.InverseTransformPoint(worldPosition))) MarkChanged();
        }
        public void PlaceSelected(float yaw, float elevation)
        {
            var panel = GetPanel(SelectedId); if (panel == null) return;
            panel.Layout.yaw = yaw; panel.Layout.elevation = elevation;
            FaaSpatialLayoutMath.Sanitize(panel.Layout); MarkChanged(); RefreshTransforms();
        }
        public void AdjustDistance(float delta)
        {
            var panel = GetPanel(SelectedId); if (panel == null) return;
            panel.Layout.distance = Mathf.Clamp(panel.Layout.distance + delta, FaaSpatialLayoutMath.MinDistance, FaaSpatialLayoutMath.MaxDistance);
            MarkChanged(); RefreshTransforms(); RefreshControls();
        }
        public void RecallPanels()
        {
            if (View == null || CockpitFrame == null) return;
            CancelManipulation();
            foreach (var panel in panels)
            {
                panel.Layout.yaw = panel.DefaultLayout.yaw;
                panel.Layout.elevation = panel.DefaultLayout.elevation; panel.Layout.distance = panel.DefaultLayout.distance;
                FaaSpatialLayoutMath.Sanitize(panel.Layout);
            }
            MarkChanged(); RefreshTransforms();
        }
        public void ResetSelected()
        {
            LaptopCamera?.CancelSizing();
            var panel = GetPanel(SelectedId);
            if (panel != null) panel.Reset();
            foreach (var module in modules) if (module.Id == SelectedId) module.Layout.scale = module.DefaultScale;
            MarkChanged(); RefreshTransforms(); RefreshControls();
        }
        public void ResetAll()
        {
            LaptopCamera?.CancelSizing();
            CancelManipulation(); foreach (var panel in panels) panel.Reset();
            foreach(var panel in utilityPanels)panel.Reset();
            foreach (var module in modules) module.Layout.scale = module.DefaultScale;
            MarkChanged(); RefreshTransforms(); RefreshControls();
        }
        public void RecenterSeat()
        {
            if (View == null || aircraft == null) return;
            CancelManipulation(); seatOffset = aircraft.InverseTransformPoint(View.transform.position);
            trackedSeat.Capture(View.transform); RefreshTransforms();
        }
        public void ScaleAllModules(float delta)
        {
            LaptopCamera?.CancelSizing();
            foreach (var module in modules) module.Layout.scale = FaaSpatialLayoutMath.Scale(module.Layout.scale + delta);
            MarkChanged(); RefreshTransforms(); RefreshControls();
        }
        private void MarkChanged() { dirty = true; saveAfter = Time.unscaledTime + .75f; ProfileStatus = "Layout changed"; }
        private void LoadProfile()
        {
            if (!PersistChanges || !PlayerPrefs.HasKey(profileKey)) return;
            if (!FaaSpatialLayoutMath.TryReadProfile(PlayerPrefs.GetString(profileKey), out var profile))
            {
                ProfileStatus = "Invalid saved layout ignored; defaults restored"; return;
            }
            foreach (var saved in profile.entries)
            {
                var entry = GetEntry(saved.id); if (entry == null) continue;
                entry.yaw = saved.yaw; entry.elevation = saved.elevation; entry.distance = saved.distance; entry.scale = saved.scale;
            }
            ProfileStatus = "Saved layout restored";
        }
        public string ExportProfile()
        {
            var profile = new FaaSpatialLayoutProfile();
            foreach (var panel in panels) profile.entries.Add(panel.Layout.Copy());
            foreach(var panel in utilityPanels)profile.entries.Add(panel.Layout.Copy());
            foreach (var module in DigitalProfileModules) profile.entries.Add(module.Layout.Copy());
            return JsonUtility.ToJson(profile);
        }
        public void SaveNow()
        {
            if (!dirty || !Initialized || !PersistChanges || string.IsNullOrEmpty(profileKey)) return;
            PlayerPrefs.SetString(profileKey, ExportProfile()); SaveSymbologyPreferences(); PlayerPrefs.Save();
            dirty = false; ProfileStatus = "Layout saved on this device";
        }
        private void HandleKeyboard()
        {
            Keyboard keyboard = Keyboard.current; if (keyboard == null) return;
            // F8 already switches the existing HUD implementation. Do not steal it.
            if (keyboard.f9Key.wasPressedThisFrame) ToggleMenu();
            if (keyboard.f10Key.wasPressedThisFrame) RecallPanels();
            if (keyboard.escapeKey.wasPressedThisFrame && EditMode) SetEditMode(false);
        }
        private void ReleaseBindings()
        {
            LaptopCamera?.StopCamera();
            CancelManipulation();
            ReleaseSymbologyVersions();
            foreach (var panel in panels) panel.Dispose(); panels.Clear();
            foreach(var panel in utilityPanels)panel.Dispose();utilityPanels.Clear();
            foreach (var module in modules) module.Restore(); modules.Clear();
            fixedInstrumentLayout = null;
            foreach (var subsystem in trackingSubsystems) subsystem.trackingOriginUpdated -= TrackingOriginUpdated;
            trackingSubsystems.Clear();
            if (CockpitFrame != null) Destroy(CockpitFrame.gameObject);
            CockpitFrame = null;
            DestroyControls();
            Initialized = false; EditMode = false; dirty = false;
        }
    }
}
