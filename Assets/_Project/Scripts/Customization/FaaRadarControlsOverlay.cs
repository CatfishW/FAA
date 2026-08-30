using TMPro;
using FAA.XPlaneIntegration.Runtime;
using TrafficRadar;
using TrafficRadar.Core;
using TrafficRadar.Controls;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using WeatherRadar;

namespace FAA.Customization
{
    [DefaultExecutionOrder(10020)]
    [AddComponentMenu("FAA/Customization/FAA Radar Controls Overlay")]
    public class FaaRadarControlsOverlay : MonoBehaviour
    {
        private const float CompactStripHeight = 56f;
        private const float RowHeight = 40f;
        private const float WeatherCollapsedWidth = 214f;
        private const float WeatherCompactWidth = 352f;
        private const float WeatherAdvancedWidth = 392f;
        private const float TrafficCollapsedWidth = 292f;
        private const float TrafficCompactWidth = 382f;
        private const float TrafficAdvancedWidth = 408f;
        private const float ThreeRowStripHeight = 140f;
        private const string WeatherSizePreferenceKey = "FAA.HUD.WeatherRadarSize";
        private const string TrafficSizePreferenceKey = "FAA.HUD.TrafficRadarSize";
        private static readonly Color StripBackgroundColor = new Color(0.006f, 0.045f, 0.04f, 0.88f);
        private static readonly Color StripStrokeColor = new Color(0.20f, 0.96f, 0.58f, 0.62f);
        private static readonly Color ButtonNormalColor = new Color(0.018f, 0.15f, 0.105f, 0.84f);
        private static readonly Color ButtonHighlightedColor = new Color(0.055f, 0.29f, 0.19f, 0.96f);
        private static readonly Color ButtonPressedColor = new Color(0.012f, 0.10f, 0.075f, 0.98f);
        private static readonly Color ButtonActiveColor = new Color(0.075f, 0.32f, 0.22f, 0.96f);
        private static readonly Color PrimaryTextColor = new Color(0.64f, 1f, 0.68f, 1f);
        private static readonly Color SecondaryTextColor = new Color(0.76f, 1f, 0.78f, 1f);

        [Header("Scene Names")]
        [SerializeField] private string weatherRadarRootName = "X-Plane Weather Radar System";
        [SerializeField] private string trafficRadarRootName = "Traffic Radar System";

        [Header("Layout")]
        [SerializeField] private Vector2 weatherStripSize = new Vector2(176f, 44f);
        [SerializeField] private Vector2 trafficStripSize = new Vector2(226f, 44f);
        [SerializeField] private Vector2 stripOffset = new Vector2(0f, 8f);
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private bool startExpanded;
        [SerializeField] private bool showConfigurationButtonsOnStart;
        [SerializeField] private bool reducedMotion;

        [Header("Radar Sizing")]
        [SerializeField] private float defaultWeatherRadarSize = 280f;
        [SerializeField] private float defaultTrafficRadarSize = 296f;
        [SerializeField] private float minimumRadarSize = 220f;
        [SerializeField] private float maximumRadarSize = 560f;
        [SerializeField] private float radarSizeStep = 32f;
        [SerializeField] private bool rememberRadarSizes = true;

        [Header("Controls")]
        [SerializeField] private bool enableWeatherControls = true;
        [SerializeField] private bool enableTrafficControls = true;
        [SerializeField] private bool enableKeyboardShortcuts;

        [Header("Compatibility")]
        [SerializeField] private bool suppressLegacyRadarControlPanels = true;
        [SerializeField] private bool suppressInlineWeatherLabels = true;

        private Transform _weatherRoot;
        private Transform _trafficRoot;
        private WeatherRadarDataProvider _weatherDataProvider;
        private XPlaneOriginalWeatherRadarProvider _weatherProvider;
        private XPlane12ApiHudBridge _xPlaneBridge;
        private XPlaneWeatherRadarOverlay[] _weatherOverlays;
        private TrafficRadarController _trafficController;
        private TrafficRadarDisplay _trafficDisplay;
        private TrafficRadarDataManager _trafficDataManager;
        private RectTransform _weatherStrip;
        private XPlaneWeatherInfoStrip _weatherConditionsStrip;
        private FaaRadarConfigurationDrawer _weatherDrawer;
        private FaaRadarInteractionSurface _weatherInteractionSurface;
        private RectTransform _trafficStrip;
        private FaaRadarConfigurationDrawer _trafficDrawer;
        private FaaRadarInteractionSurface _trafficInteractionSurface;
        private TMP_Text _weatherRangeText;
        private TMP_Text _weatherSummaryText;
        private TMP_Text _weatherTiltText;
        private TMP_Text _weatherGainText;
        private TMP_Text _weatherModeText;
        private TMP_Text _weatherPowerText;
        private TMP_Text _weatherExpandText;
        private TMP_Text _weatherAdvancedText;
        private TMP_Text _weatherSizeText;
        private TMP_Text _trafficRangeText;
        private TMP_Text _trafficSummaryText;
        private TMP_Text _trafficTargetText;
        private TMP_Text _trafficMaxText;
        private TMP_Text _trafficModeText;
        private TMP_Text _trafficAutoText;
        private TMP_Text _trafficChartText;
        private TMP_Text _trafficBackgroundText;
        private TMP_Text _trafficRingsText;
        private TMP_Text _trafficOpacityText;
        private TMP_Text _trafficExpandText;
        private TMP_Text _trafficAdvancedText;
        private TMP_Text _trafficSizeText;
        // The weather panel is procedural/dataref-backed. Start with the
        // legacy reference overlay hidden so it cannot mimic the native
        // X-Plane raster presentation; pilots can still opt into vector
        // guidance through the control surface when needed.
        private bool _weatherOverlayVisible = false;
        private bool _weatherExpanded;
        private bool _trafficExpanded;
        private bool _weatherConfigurationVisible;
        private bool _trafficConfigurationVisible;
        private bool _showWeatherAdvancedControls;
        private bool _showTrafficAdvancedControls;
        private bool _controlsVisible;
        private bool _visibilityInitialized;
        private float _nextRefreshTime;
        private Transform _weatherSizedRoot;
        private Transform _trafficSizedRoot;

        public void Configure(Transform weatherRoot, Transform trafficRoot)
        {
            _weatherRoot = weatherRoot;
            _trafficRoot = trafficRoot;
            RefreshReferences();
            EnsureRadarSizesInitialized();
            EnsureControlStrips();
            UpdateLabels();
        }

        private void Awake()
        {
            _weatherExpanded = startExpanded;
            _trafficExpanded = startExpanded;
            _weatherConfigurationVisible = showConfigurationButtonsOnStart;
            _trafficConfigurationVisible = showConfigurationButtonsOnStart;
            RefreshReferences();
            EnsureRadarSizesInitialized();
            EnsureControlStrips();
            SetVisible(showOnStart);
        }

        private void OnEnable()
        {
            _weatherExpanded |= startExpanded;
            _trafficExpanded |= startExpanded;
            RefreshReferences();
            EnsureRadarSizesInitialized();
            EnsureControlStrips();
            SetVisible(showOnStart);
        }

        private void Start()
        {
            // FaaHudRuntimeSanitizer performs a final deterministic layout pass in
            // Start. Reapply the saved pilot sizes afterward (this component has a
            // later execution order) so startup normalization never erases them.
            _weatherSizedRoot = null;
            _trafficSizedRoot = null;
            EnsureRadarSizesInitialized();
            EnsureControlStrips();
            UpdateLabels();
        }

        private void OnDisable()
        {
            if (_weatherConditionsStrip != null)
            {
                _weatherConditionsStrip.ExpandedChanged -= OnWeatherConditionsExpandedChanged;
            }
        }

        private void Update()
        {
            if (enableKeyboardShortcuts)
            {
                HandleKeyboardShortcuts();
            }

            if (Time.unscaledTime >= _nextRefreshTime)
            {
                _nextRefreshTime = Time.unscaledTime + 0.25f;
                RefreshReferences();
                EnsureRadarSizesInitialized();
                EnsureControlStrips();
                UpdateLabels();
            }
        }

        public void SetVisible(bool visible)
        {
            _controlsVisible = visible;
            _visibilityInitialized = true;
            if (_weatherConditionsStrip != null)
            {
                _weatherConditionsStrip.gameObject.SetActive(visible && enableWeatherControls);
            }

            ApplyRadarConfigurationVisibility();
        }

        public void ToggleVisible()
        {
            bool nextVisible = !_controlsVisible;
            SetVisible(nextVisible);
        }

        public void ToggleRadarConfiguration(FaaRadarKind radarKind)
        {
            EnsureControlStrips();
            bool show;
            if (radarKind == FaaRadarKind.Weather)
            {
                show = !(_weatherConditionsStrip != null
                    ? _weatherConditionsStrip.IsExpanded
                    : _weatherConfigurationVisible);
                _weatherConfigurationVisible = show;
                if (show)
                {
                    _trafficConfigurationVisible = false;
                }

                _weatherConditionsStrip?.SetExpanded(show);
            }
            else
            {
                show = !_trafficConfigurationVisible;
                _trafficConfigurationVisible = show;
                if (show)
                {
                    _weatherConfigurationVisible = false;
                    _weatherConditionsStrip?.SetExpanded(false);
                }
            }

            ApplyRadarConfigurationVisibility();
        }

        public void SetRadarConfigurationVisible(FaaRadarKind radarKind, bool visible, bool immediate = false)
        {
            if (radarKind == FaaRadarKind.Weather)
            {
                _weatherConfigurationVisible = visible;
                _weatherConditionsStrip?.SetExpanded(visible, immediate);
                if (visible)
                {
                    _trafficConfigurationVisible = false;
                }
            }
            else
            {
                _trafficConfigurationVisible = visible;
                if (visible)
                {
                    _weatherConfigurationVisible = false;
                    _weatherConditionsStrip?.SetExpanded(false, immediate);
                }
            }

            EnsureControlStrips();
            ApplyRadarConfigurationVisibility(immediate);
        }

        public void ToggleWeatherExpanded()
        {
            _weatherExpanded = !_weatherExpanded;
            if (!_weatherExpanded)
            {
                _showWeatherAdvancedControls = false;
            }

            EnsureControlStrips();
            UpdateLabels();
        }

        public void ToggleTrafficExpanded()
        {
            _trafficExpanded = !_trafficExpanded;
            if (!_trafficExpanded)
            {
                _showTrafficAdvancedControls = false;
            }

            EnsureControlStrips();
            UpdateLabels();
        }

        public void ToggleWeatherAdvanced()
        {
            _weatherExpanded = true;
            _showWeatherAdvancedControls = !_showWeatherAdvancedControls;
            EnsureControlStrips();
            UpdateLabels();
        }

        public void ToggleTrafficAdvanced()
        {
            _trafficExpanded = true;
            _showTrafficAdvancedControls = !_showTrafficAdvancedControls;
            EnsureControlStrips();
            UpdateLabels();
        }

        public void WeatherRangeDown()
        {
            _weatherDataProvider?.DecreaseRange();
            SyncWeatherProviderSettings();
        }

        public void WeatherRangeUp()
        {
            _weatherDataProvider?.IncreaseRange();
            SyncWeatherProviderSettings();
        }

        public void WeatherTiltDown()
        {
            if (_weatherDataProvider == null)
            {
                return;
            }

            _weatherDataProvider.SetTilt(_weatherDataProvider.RadarData.tiltAngle - 0.5f);
            SyncWeatherProviderSettings();
        }

        public void WeatherTiltUp()
        {
            if (_weatherDataProvider == null)
            {
                return;
            }

            _weatherDataProvider.SetTilt(_weatherDataProvider.RadarData.tiltAngle + 0.5f);
            SyncWeatherProviderSettings();
        }

        public void WeatherGainDown()
        {
            if (_weatherDataProvider == null)
            {
                return;
            }

            _weatherDataProvider.SetGain(_weatherDataProvider.RadarData.gainOffset - 1f);
            SyncWeatherProviderSettings();
        }

        public void WeatherGainUp()
        {
            if (_weatherDataProvider == null)
            {
                return;
            }

            _weatherDataProvider.SetGain(_weatherDataProvider.RadarData.gainOffset + 1f);
            SyncWeatherProviderSettings();
        }

        public void CycleWeatherMode()
        {
            if (_weatherDataProvider == null)
            {
                return;
            }

            RadarMode current = _weatherDataProvider.RadarData.currentMode;
            RadarMode next = current switch
            {
                RadarMode.WX => RadarMode.WX_T,
                RadarMode.WX_T => RadarMode.TURB,
                RadarMode.TURB => RadarMode.MAP,
                RadarMode.MAP => RadarMode.STBY,
                _ => RadarMode.WX
            };
            _weatherDataProvider.SetMode(next);
            SyncWeatherProviderSettings();
        }

        public void ToggleWeatherOverlay()
        {
            _weatherOverlayVisible = !_weatherOverlayVisible;
            ApplyWeatherOverlayVisibility();
        }

        public void RefreshWeatherTexture()
        {
            if (_weatherProvider == null)
            {
                return;
            }

            _weatherProvider.Activate();
            _weatherProvider.RefreshData();
        }

        public void ToggleWeatherProvider()
        {
            if (_weatherProvider == null)
            {
                return;
            }

            if (_weatherProvider.Status == ProviderStatus.Inactive)
            {
                _weatherProvider.Activate();
                _weatherProvider.RefreshData();
            }
            else
            {
                _weatherProvider.Deactivate();
            }
        }

        public void TrafficRangeDown()
        {
            _trafficController?.DecreaseRange();
        }

        public void TrafficRangeUp()
        {
            _trafficController?.IncreaseRange();
        }

        public void TrafficMaxTargetsDown()
        {
            _trafficController?.DecreaseMaxTargets();
        }

        public void TrafficMaxTargetsUp()
        {
            _trafficController?.IncreaseMaxTargets();
        }

        public void ToggleTrafficAutoRange()
        {
            _trafficController?.ToggleAutoRange();
        }

        public void ToggleTrafficTrackMode()
        {
            _trafficDisplay?.ToggleTrackUpMode();
        }

        public void ToggleTrafficChart()
        {
            _trafficDisplay?.ToggleChartBackground();
        }

        public void ToggleTrafficBackground()
        {
            _trafficDisplay?.ToggleRadarBackground();
        }

        public void TrafficRingsDown()
        {
            _trafficDisplay?.DecreaseRangeRingCount();
        }

        public void TrafficRingsUp()
        {
            _trafficDisplay?.IncreaseRangeRingCount();
        }

        public void TrafficOpacityDown()
        {
            if (_trafficDisplay != null)
            {
                _trafficDisplay.DecreaseChartOpacity(0.1f);
            }
        }

        public void TrafficOpacityUp()
        {
            if (_trafficDisplay != null)
            {
                _trafficDisplay.IncreaseChartOpacity(0.1f);
            }
        }

        public void WeatherSizeDown()
        {
            AdjustRadarSize(FaaRadarKind.Weather, -1f);
        }

        public void WeatherSizeUp()
        {
            AdjustRadarSize(FaaRadarKind.Weather, 1f);
        }

        public void TrafficSizeDown()
        {
            AdjustRadarSize(FaaRadarKind.Traffic, -1f);
        }

        public void TrafficSizeUp()
        {
            AdjustRadarSize(FaaRadarKind.Traffic, 1f);
        }

        /// <summary>
        /// Resizes a radar by one configured step. This is shared by the visible
        /// +/- controls and pointer-wheel interaction on the radar glass.
        /// </summary>
        public void AdjustRadarSize(FaaRadarKind radarKind, float direction)
        {
            Transform root = radarKind == FaaRadarKind.Weather ? _weatherRoot : _trafficRoot;
            RectTransform rootRect = root as RectTransform ?? root?.GetComponent<RectTransform>();
            if (rootRect == null || Mathf.Approximately(direction, 0f))
            {
                return;
            }

            float current = GetRadarPixelSize(rootRect);
            float next = ClampRadarSize(
                current + Mathf.Sign(direction) * Mathf.Max(1f, radarSizeStep),
                minimumRadarSize,
                maximumRadarSize);
            ApplyRadarSize(rootRect, next, radarKind, persist: true);
            EnsureControlStrips();
            UpdateLabels();
        }

        public static float ClampRadarSize(float size, float minimum, float maximum)
        {
            float safeMinimum = Mathf.Max(128f, minimum);
            float safeMaximum = Mathf.Max(safeMinimum, maximum);
            return Mathf.Clamp(size, safeMinimum, safeMaximum);
        }

        public void RefreshTraffic()
        {
            _trafficController?.RefreshData();
            _trafficDataManager?.FetchDataNow();
        }

        private void EnsureRadarSizesInitialized()
        {
            if (_weatherRoot != null && _weatherSizedRoot != _weatherRoot)
            {
                _weatherSizedRoot = _weatherRoot;
                float initial = ReadInitialRadarSize(WeatherSizePreferenceKey, defaultWeatherRadarSize);
                RectTransform rect = _weatherRoot as RectTransform ?? _weatherRoot.GetComponent<RectTransform>();
                ApplyRadarSize(rect, initial, FaaRadarKind.Weather, persist: false);
            }

            if (_trafficRoot != null && _trafficSizedRoot != _trafficRoot)
            {
                _trafficSizedRoot = _trafficRoot;
                float initial = ReadInitialRadarSize(TrafficSizePreferenceKey, defaultTrafficRadarSize);
                RectTransform rect = _trafficRoot as RectTransform ?? _trafficRoot.GetComponent<RectTransform>();
                ApplyRadarSize(rect, initial, FaaRadarKind.Traffic, persist: false);
            }
        }

        private float ReadInitialRadarSize(string preferenceKey, float fallback)
        {
            // Editor scene setup must be deterministic and must never serialize a
            // developer machine's PlayerPrefs into the shared scene asset.
            if (!Application.isPlaying || !rememberRadarSizes || !PlayerPrefs.HasKey(preferenceKey))
            {
                return ClampRadarSize(fallback, minimumRadarSize, maximumRadarSize);
            }

            float value = PlayerPrefs.GetFloat(preferenceKey);
            float legacyDefault = preferenceKey == WeatherSizePreferenceKey ? 372f : 420f;
            if (Mathf.Abs(value - legacyDefault) < 0.5f)
            {
                value = fallback;
                PlayerPrefs.SetFloat(preferenceKey, value);
                PlayerPrefs.Save();
            }

            return ClampRadarSize(value, minimumRadarSize, maximumRadarSize);
        }

        private void ApplyRadarSize(RectTransform rootRect, float requestedSize, FaaRadarKind radarKind, bool persist)
        {
            if (rootRect == null)
            {
                return;
            }

            float size = ClampRadarSize(requestedSize, minimumRadarSize, maximumRadarSize);
            rootRect.sizeDelta = new Vector2(size, size);
            LayoutRebuilder.MarkLayoutForRebuild(rootRect);

            if (radarKind == FaaRadarKind.Weather)
            {
                foreach (XPlaneOriginalWeatherRadarDisplay display in
                         rootRect.GetComponentsInChildren<XPlaneOriginalWeatherRadarDisplay>(true))
                {
                    display?.RefreshLayout();
                }

                ImproveWeatherLabelLegibility(rootRect);
                if (_weatherStrip != null)
                {
                    MatchStripToRadarRoot(_weatherStrip, rootRect);
                }
            }
            else if (_trafficStrip != null)
            {
                MatchStripToRadarRoot(_trafficStrip, rootRect);
            }

            if (persist && rememberRadarSizes)
            {
                PlayerPrefs.SetFloat(
                    radarKind == FaaRadarKind.Weather ? WeatherSizePreferenceKey : TrafficSizePreferenceKey,
                    size);
                PlayerPrefs.Save();
            }
        }

        private static float GetRadarPixelSize(RectTransform rect)
        {
            if (rect == null)
            {
                return 0f;
            }

            float width = rect.rect.width > 1f ? rect.rect.width : rect.sizeDelta.x;
            float height = rect.rect.height > 1f ? rect.rect.height : rect.sizeDelta.y;
            return Mathf.Max(1f, Mathf.Min(Mathf.Abs(width), Mathf.Abs(height)));
        }

        private void RefreshReferences()
        {
            if (_weatherRoot == null)
            {
                _weatherRoot = FindLoadedTransform(weatherRadarRootName);
            }

            if (_trafficRoot == null)
            {
                _trafficRoot = FindLoadedTransform(trafficRadarRootName);
            }

            if (_weatherRoot != null)
            {
                _weatherDataProvider = _weatherRoot.GetComponentInChildren<WeatherRadarDataProvider>(true);
                _weatherProvider = _weatherRoot.GetComponentInChildren<XPlaneOriginalWeatherRadarProvider>(true);
                _weatherOverlays = _weatherRoot.GetComponentsInChildren<XPlaneWeatherRadarOverlay>(true);
                ApplyWeatherOverlayVisibility();
            }

            if (_xPlaneBridge == null)
            {
                _xPlaneBridge = FindAnyObjectByType<XPlane12ApiHudBridge>(FindObjectsInactive.Include);
            }

            if (_trafficRoot != null)
            {
                _trafficController = _trafficRoot.GetComponentInChildren<TrafficRadarController>(true);
                _trafficDisplay = _trafficRoot.GetComponentInChildren<TrafficRadarDisplay>(true);
                _trafficDataManager = _trafficRoot.GetComponentInChildren<TrafficRadarDataManager>(true);
            }
        }

        private void EnsureControlStrips()
        {
            EnsureEventSystem();
            SuppressLegacyRadarControlPanels();

            if (enableWeatherControls && _weatherRoot != null)
            {
                if (suppressInlineWeatherLabels)
                {
                    ImproveWeatherLabelLegibility(_weatherRoot);
                }

                _weatherStrip = EnsureStrip(_weatherRoot, "WeatherControlStrip", GetWeatherStripSize());
                EnsureWeatherControls(_weatherStrip);
                _weatherDrawer = EnsureDrawer(_weatherStrip);
                _weatherInteractionSurface = EnsureInteractionSurface(_weatherRoot, FaaRadarKind.Weather);
                _weatherConditionsStrip = EnsureWeatherConditionsStrip(_weatherRoot, _weatherStrip);
            }
            else if (_weatherConditionsStrip != null)
            {
                _weatherConditionsStrip.gameObject.SetActive(false);
            }

            if (enableTrafficControls && _trafficRoot != null)
            {
                _trafficStrip = EnsureStrip(_trafficRoot, "TrafficControlStrip", GetTrafficStripSize());
                EnsureTrafficControls(_trafficStrip);
                _trafficDrawer = EnsureDrawer(_trafficStrip);
                _trafficInteractionSurface = EnsureInteractionSurface(_trafficRoot, FaaRadarKind.Traffic);
            }

            ApplyRadarConfigurationVisibility();
        }

        private FaaRadarConfigurationDrawer EnsureDrawer(RectTransform strip)
        {
            if (strip == null)
            {
                return null;
            }

            FaaRadarConfigurationDrawer drawer = strip.GetComponent<FaaRadarConfigurationDrawer>() ??
                                                   strip.gameObject.AddComponent<FaaRadarConfigurationDrawer>();
            drawer.Configure(reducedMotion);
            return drawer;
        }

        private FaaRadarInteractionSurface EnsureInteractionSurface(Transform root, FaaRadarKind radarKind)
        {
            RectTransform rootRect = root as RectTransform ?? root.GetComponent<RectTransform>();
            if (rootRect == null)
            {
                return null;
            }

            string objectName = radarKind == FaaRadarKind.Weather
                ? FaaRadarInteractionSurface.WeatherObjectName
                : FaaRadarInteractionSurface.TrafficObjectName;
            Transform existing = root.Find(objectName);
            GameObject surfaceObject = existing != null
                ? existing.gameObject
                : new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform surfaceRect = surfaceObject.GetComponent<RectTransform>();
            surfaceRect.SetParent(root, false);
            StretchToParent(surfaceRect);
            surfaceRect.SetAsLastSibling();

            FaaRadarInteractionSurface surface = surfaceObject.GetComponent<FaaRadarInteractionSurface>() ??
                                                 surfaceObject.AddComponent<FaaRadarInteractionSurface>();
            surface.Configure(this, radarKind, reducedMotion);
            return surface;
        }

        private void ApplyRadarConfigurationVisibility(bool immediate = false)
        {
            bool overallVisible = _visibilityInitialized ? _controlsVisible : showOnStart;
            bool weatherEnabled = overallVisible && enableWeatherControls;
            bool trafficEnabled = overallVisible && enableTrafficControls;

            ApplyDrawerState(
                _weatherStrip,
                _weatherDrawer,
                weatherEnabled && _weatherConfigurationVisible,
                weatherEnabled,
                immediate);
            ApplyDrawerState(
                _trafficStrip,
                _trafficDrawer,
                trafficEnabled && _trafficConfigurationVisible,
                trafficEnabled,
                immediate);

            if (_weatherInteractionSurface != null)
            {
                _weatherInteractionSurface.SetInteractionEnabled(weatherEnabled);
                _weatherInteractionSurface.SetOpen(weatherEnabled && _weatherConfigurationVisible);
            }

            if (_trafficInteractionSurface != null)
            {
                _trafficInteractionSurface.SetInteractionEnabled(trafficEnabled);
                _trafficInteractionSurface.SetOpen(trafficEnabled && _trafficConfigurationVisible);
            }
        }

        private static void ApplyDrawerState(
            RectTransform strip,
            FaaRadarConfigurationDrawer drawer,
            bool drawerVisible,
            bool controlsEnabled,
            bool immediate)
        {
            if (strip == null)
            {
                return;
            }

            strip.gameObject.SetActive(controlsEnabled);
            if (controlsEnabled)
            {
                drawer?.SetVisible(drawerVisible, immediate);
            }
        }

        private XPlaneWeatherInfoStrip EnsureWeatherConditionsStrip(Transform root, RectTransform weatherControlStrip)
        {
            Transform stripParent = root.parent != null ? root.parent : transform;
            Transform existing = stripParent.Find(XPlaneWeatherInfoStrip.StripObjectName) ??
                                 root.Find(XPlaneWeatherInfoStrip.StripObjectName);
            GameObject stripObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    XPlaneWeatherInfoStrip.StripObjectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer));
            stripObject.transform.SetParent(stripParent, false);
            XPlaneWeatherInfoStrip infoStrip = stripObject.GetComponent<XPlaneWeatherInfoStrip>() ??
                                               stripObject.AddComponent<XPlaneWeatherInfoStrip>();
            infoStrip.ExpandedChanged -= OnWeatherConditionsExpandedChanged;
            infoStrip.ExpandedChanged += OnWeatherConditionsExpandedChanged;
            infoStrip.Configure(_xPlaneBridge, root as RectTransform ?? root.GetComponent<RectTransform>(), weatherControlStrip);
            stripObject.SetActive((_visibilityInitialized ? _controlsVisible : showOnStart) && enableWeatherControls);
            stripObject.transform.SetAsLastSibling();
            return infoStrip;
        }

        private void OnWeatherConditionsExpandedChanged(bool expanded)
        {
            _weatherConfigurationVisible = expanded;
            if (expanded)
            {
                _trafficConfigurationVisible = false;
            }

            ApplyRadarConfigurationVisibility();
        }

        private RectTransform EnsureStrip(Transform root, string stripName, Vector2 size)
        {
            Transform stripParent = root.parent != null ? root.parent : transform;
            Transform existing = stripParent.Find(stripName) ?? root.Find(stripName);
            GameObject stripObject = existing != null ? existing.gameObject : new GameObject(stripName, typeof(RectTransform));
            RectTransform rectTransform = stripObject.GetComponent<RectTransform>();
            rectTransform.SetParent(stripParent, false);
            MatchStripToRadarRoot(rectTransform, root);
            rectTransform.sizeDelta = size;
            if (stripObject.GetComponent<FaaRadarConfigurationDrawer>() == null)
            {
                rectTransform.localScale = Vector3.one;
            }
            rectTransform.localRotation = Quaternion.identity;
            stripObject.SetActive((_visibilityInitialized ? _controlsVisible : showOnStart) &&
                                  (stripName.Contains("Weather") ? enableWeatherControls : enableTrafficControls));

            Image background = stripObject.GetComponent<Image>() ?? stripObject.AddComponent<Image>();
            background.color = StripBackgroundColor;
            background.raycastTarget = true;

            Outline outline = stripObject.GetComponent<Outline>() ?? stripObject.AddComponent<Outline>();
            outline.effectColor = StripStrokeColor;
            outline.effectDistance = new Vector2(1f, -1f);

            HorizontalLayoutGroup oldHorizontalLayout = stripObject.GetComponent<HorizontalLayoutGroup>();
            if (oldHorizontalLayout != null)
            {
                DestroyUnityObject(oldHorizontalLayout);
            }

            VerticalLayoutGroup layout = stripObject.GetComponent<VerticalLayoutGroup>() ?? stripObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            stripObject.transform.SetAsLastSibling();
            return rectTransform;
        }

        private void EnsureWeatherControls(RectTransform strip)
        {
            RectTransform primaryRow = EnsureRow(strip, "WeatherControlRowPrimary");
            RectTransform secondaryRow = EnsureRow(strip, "WeatherControlRowSecondary");
            RectTransform tertiaryRow = EnsureRow(strip, "WeatherControlRowTertiary");
            HideDirectControlChildren(strip, primaryRow, secondaryRow, tertiaryRow);

            if (_weatherExpanded)
            {
                _weatherExpandText = GetButtonLabel(EnsureButton(primaryRow, "WXExpandToggle", "<", ToggleWeatherExpanded, 24f));
                _weatherRangeText = EnsureLabel(primaryRow, "WXRangeValue", "WX 160", 58f);
                EnsureButton(primaryRow, "WXRangeDown", "-", WeatherRangeDown, 30f);
                EnsureButton(primaryRow, "WXRangeUp", "+", WeatherRangeUp, 30f);
                _weatherModeText = GetButtonLabel(EnsureButton(primaryRow, "WXModeCycle", "WX", CycleWeatherMode, 48f));
                _weatherAdvancedText = GetButtonLabel(EnsureButton(primaryRow, "WXAdvancedToggle", "MORE", ToggleWeatherAdvanced, 58f));
                HideUnexpectedRowChildren(
                    primaryRow,
                    "WXExpandToggle", "WXRangeValue", "WXRangeDown", "WXRangeUp", "WXModeCycle", "WXAdvancedToggle");
            }
            else
            {
                _weatherSummaryText = GetButtonLabel(EnsureButton(primaryRow, "WXSummaryToggle", "CONFIG WX · 160NM", ToggleWeatherExpanded, WeatherCollapsedWidth - 10f));
                _weatherExpandText = null;
                HideUnexpectedRowChildren(primaryRow, "WXSummaryToggle");
            }

            _weatherTiltText = EnsureLabel(secondaryRow, "WXTiltValue", "T+0.0", 58f);
            EnsureButton(secondaryRow, "WXTiltDown", "T-", WeatherTiltDown, 32f);
            EnsureButton(secondaryRow, "WXTiltUp", "T+", WeatherTiltUp, 32f);
            _weatherGainText = EnsureLabel(secondaryRow, "WXGainValue", "G+0", 50f);
            EnsureButton(secondaryRow, "WXGainDown", "G-", WeatherGainDown, 32f);
            EnsureButton(secondaryRow, "WXGainUp", "G+", WeatherGainUp, 32f);
            HideUnexpectedRowChildren(
                secondaryRow,
                "WXTiltValue", "WXTiltDown", "WXTiltUp", "WXGainValue", "WXGainDown", "WXGainUp");

            _weatherPowerText = GetButtonLabel(EnsureButton(tertiaryRow, "WXPowerToggle", "PWR", ToggleWeatherProvider, 42f));
            EnsureButton(tertiaryRow, "WXRefresh", "REF", RefreshWeatherTexture, 42f);
            EnsureButton(tertiaryRow, "WXSizeDown", "S-", WeatherSizeDown, 32f);
            _weatherSizeText = EnsureLabel(tertiaryRow, "WXSizeValue", "296PX", 58f);
            EnsureButton(tertiaryRow, "WXSizeUp", "S+", WeatherSizeUp, 32f);
            HideUnexpectedRowChildren(
                tertiaryRow,
                "WXPowerToggle", "WXRefresh", "WXSizeDown", "WXSizeValue", "WXSizeUp");
            secondaryRow.gameObject.SetActive(_weatherExpanded && _showWeatherAdvancedControls);
            tertiaryRow.gameObject.SetActive(_weatherExpanded && _showWeatherAdvancedControls);
        }

        private void EnsureTrafficControls(RectTransform strip)
        {
            RectTransform primaryRow = EnsureRow(strip, "TrafficControlRowPrimary");
            RectTransform secondaryRow = EnsureRow(strip, "TrafficControlRowSecondary");
            RectTransform tertiaryRow = EnsureRow(strip, "TrafficControlRowTertiary");
            HideDirectControlChildren(strip, primaryRow, secondaryRow, tertiaryRow);

            if (_trafficExpanded)
            {
                _trafficExpandText = GetButtonLabel(EnsureButton(primaryRow, "TCASExpandToggle", "<", ToggleTrafficExpanded, 24f));
                _trafficRangeText = EnsureLabel(primaryRow, "TCASRangeValue", "TRF 40", 64f);
                EnsureButton(primaryRow, "TCASRangeDown", "-", TrafficRangeDown, 30f);
                EnsureButton(primaryRow, "TCASRangeUp", "+", TrafficRangeUp, 30f);
                _trafficTargetText = EnsureLabel(primaryRow, "TCASTargetValue", "0/50", 54f);
                _trafficAutoText = GetButtonLabel(EnsureButton(primaryRow, "TCASAutoToggle", "AUTO", ToggleTrafficAutoRange, 48f));
                _trafficAdvancedText = GetButtonLabel(EnsureButton(primaryRow, "TCASAdvancedToggle", "MORE", ToggleTrafficAdvanced, 58f));
                HideUnexpectedRowChildren(
                    primaryRow,
                    "TCASExpandToggle", "TCASRangeValue", "TCASRangeDown", "TCASRangeUp", "TCASTargetValue",
                    "TCASAutoToggle", "TCASAdvancedToggle");
            }
            else
            {
                _trafficSummaryText = GetButtonLabel(EnsureButton(primaryRow, "TCASSummaryToggle", "CONFIG TRF · 0/50 · 40NM", ToggleTrafficExpanded, TrafficCollapsedWidth - 10f));
                _trafficExpandText = null;
                HideUnexpectedRowChildren(primaryRow, "TCASSummaryToggle");
            }

            _trafficMaxText = EnsureLabel(secondaryRow, "TCASMaxValue", "MAX 50", 56f);
            EnsureButton(secondaryRow, "TCASMaxDown", "M-", TrafficMaxTargetsDown, 32f);
            EnsureButton(secondaryRow, "TCASMaxUp", "M+", TrafficMaxTargetsUp, 32f);
            _trafficModeText = GetButtonLabel(EnsureButton(secondaryRow, "TCASTrackToggle", "TRK", ToggleTrafficTrackMode, 44f));
            _trafficChartText = GetButtonLabel(EnsureButton(secondaryRow, "TCASChartToggle", "CHT", ToggleTrafficChart, 44f));
            _trafficBackgroundText = GetButtonLabel(EnsureButton(secondaryRow, "TCASBackgroundToggle", "BKG", ToggleTrafficBackground, 44f));
            HideUnexpectedRowChildren(
                secondaryRow,
                "TCASMaxValue", "TCASMaxDown", "TCASMaxUp", "TCASTrackToggle",
                "TCASChartToggle", "TCASBackgroundToggle");

            _trafficRingsText = EnsureLabel(tertiaryRow, "TCASRingsValue", "R4", 34f);
            EnsureButton(tertiaryRow, "TCASRingsDown", "R-", TrafficRingsDown, 32f);
            EnsureButton(tertiaryRow, "TCASRingsUp", "R+", TrafficRingsUp, 32f);
            _trafficOpacityText = EnsureLabel(tertiaryRow, "TCASOpacityValue", "50%", 40f);
            EnsureButton(tertiaryRow, "TCASOpacityDown", "O-", TrafficOpacityDown, 32f);
            EnsureButton(tertiaryRow, "TCASOpacityUp", "O+", TrafficOpacityUp, 32f);
            EnsureButton(tertiaryRow, "TCASSizeDown", "S-", TrafficSizeDown, 32f);
            _trafficSizeText = EnsureLabel(tertiaryRow, "TCASSizeValue", "320PX", 58f);
            EnsureButton(tertiaryRow, "TCASSizeUp", "S+", TrafficSizeUp, 32f);
            EnsureButton(tertiaryRow, "TCASRefresh", "REF", RefreshTraffic, 42f);
            HideUnexpectedRowChildren(
                tertiaryRow,
                "TCASRingsValue", "TCASRingsDown", "TCASRingsUp", "TCASOpacityValue",
                "TCASOpacityDown", "TCASOpacityUp", "TCASSizeDown", "TCASSizeValue", "TCASSizeUp", "TCASRefresh");
            secondaryRow.gameObject.SetActive(_trafficExpanded && _showTrafficAdvancedControls);
            tertiaryRow.gameObject.SetActive(_trafficExpanded && _showTrafficAdvancedControls);
        }

        private RectTransform EnsureRow(RectTransform strip, string rowName)
        {
            Transform existing = strip.Find(rowName);
            GameObject rowObject = existing != null ? existing.gameObject : new GameObject(rowName, typeof(RectTransform));
            rowObject.SetActive(true);
            RectTransform rectTransform = rowObject.GetComponent<RectTransform>();
            rectTransform.SetParent(strip, false);
            rectTransform.SetAsLastSibling();
            rectTransform.sizeDelta = new Vector2(strip.sizeDelta.x - 10f, RowHeight);
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;

            LayoutElement layoutElement = rowObject.GetComponent<LayoutElement>() ?? rowObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = Mathf.Max(1f, strip.sizeDelta.x - 10f);
            layoutElement.preferredHeight = RowHeight;
            layoutElement.minHeight = RowHeight;

            HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>() ?? rowObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            return rectTransform;
        }

        private Button EnsureButton(RectTransform parent, string name, string text, UnityAction action, float width)
        {
            Transform existing = parent.Find(name);
            GameObject buttonObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            buttonObject.SetActive(true);
            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.SetAsLastSibling();
            rectTransform.sizeDelta = new Vector2(width, RowHeight);

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>() ?? buttonObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = RowHeight;
            layout.minWidth = width;
            layout.minHeight = RowHeight;

            Image image = buttonObject.GetComponent<Image>() ?? buttonObject.AddComponent<Image>();
            image.color = ButtonNormalColor;
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>() ?? buttonObject.AddComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonNormalColor;
            colors.highlightedColor = ButtonHighlightedColor;
            colors.pressedColor = ButtonPressedColor;
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.02f, 0.05f, 0.025f, 0.5f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            TMP_Text label = EnsureText(buttonObject.transform, "Label", text, 15f);
            StretchToParent(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.color = PrimaryTextColor;
            return button;
        }

        private TMP_Text EnsureLabel(RectTransform parent, string name, string text, float width)
        {
            Transform existing = parent.Find(name);
            GameObject labelObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            labelObject.SetActive(true);
            RectTransform rectTransform = labelObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.SetAsLastSibling();
            rectTransform.sizeDelta = new Vector2(width, RowHeight);

            LayoutElement layout = labelObject.GetComponent<LayoutElement>() ?? labelObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = RowHeight;
            layout.minWidth = width;
            layout.minHeight = RowHeight;

            TMP_Text label = EnsureText(labelObject.transform, "Text", text, 14f);
            StretchToParent(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.color = SecondaryTextColor;
            return label;
        }

        private TMP_Text EnsureText(Transform parent, string name, string text, float fontSize)
        {
            Transform existing = parent.Find(name);
            GameObject textObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            textObject.SetActive(true);
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>() ?? textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.enableAutoSizing = false;
            label.fontSizeMin = Mathf.Min(12f, fontSize);
            label.fontSizeMax = fontSize;
            label.extraPadding = true;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private void UpdateLabels()
        {
            if (_weatherDataProvider != null)
            {
                WeatherRadarData data = _weatherDataProvider.RadarData;
                string modeText = data.currentMode.ToString().Replace("_", "+");
                bool bridgeHasWeatherTexture = _xPlaneBridge != null && _xPlaneBridge.LatestWeatherTexture != null;
                string powerText = _weatherProvider != null && _weatherProvider.Status == ProviderStatus.Inactive && !bridgeHasWeatherTexture ? "OFF" : modeText;
                SetText(_weatherSummaryText, $"CONFIG {powerText} · {data.currentRange:0}NM");
                SetText(_weatherRangeText, $"WX {data.currentRange:0}");
                SetText(_weatherTiltText, $"T{Signed(data.tiltAngle, "0.0")}");
                SetText(_weatherGainText, $"G{Signed(data.gainOffset, "0")}");
                SetText(_weatherModeText, modeText);
                SetInlineWeatherText(_weatherRoot, "ModeLabel", modeText);
                SetInlineWeatherText(_weatherRoot, "RangeLabel", $"{data.currentRange:0} NM");
                SetInlineWeatherText(_weatherRoot, "TiltLabel", $"TILT {Signed(data.tiltAngle, "0.0")}°");
            }

            if (_weatherProvider != null)
            {
                bool bridgeHasWeatherTexture = _xPlaneBridge != null && _xPlaneBridge.LatestWeatherTexture != null;
                SetText(_weatherPowerText, _weatherProvider.Status == ProviderStatus.Inactive && !bridgeHasWeatherTexture ? "OFF" : "ON");
            }

            if (_trafficController != null)
            {
                int liveTrafficCount = _xPlaneBridge != null && _xPlaneBridge.IsFeedHealthy
                    ? _xPlaneBridge.TrafficCount
                    : _trafficController.TargetCount;
                SetText(_trafficSummaryText, $"CONFIG TRF · {liveTrafficCount}/{_trafficController.MaxTargets} · {_trafficController.RangeNM:0}NM");
                SetText(_trafficRangeText, $"TRF {_trafficController.RangeNM:0}");
                SetText(_trafficTargetText, $"{liveTrafficCount}/{_trafficController.MaxTargets}");
                SetText(_trafficMaxText, $"MAX {_trafficController.MaxTargets}");
                SetText(_trafficAutoText, _trafficController.AutoRangeEnabled ? "AUTO" : "MAN");
            }

            if (_trafficDisplay != null)
            {
                SetText(_trafficModeText, _trafficDisplay.TrackUpModeEnabled ? "TRK" : "NUP");
                SetText(_trafficChartText, _trafficDisplay.ChartBackgroundVisible ? "CHT" : "NO");
                SetText(_trafficBackgroundText, _trafficDisplay.ShowRadarBackground ? "BKG" : "CLR");
                SetText(_trafficRingsText, $"R{_trafficDisplay.RangeRingCount}");
                SetText(_trafficOpacityText, $"{Mathf.RoundToInt(_trafficDisplay.ChartOpacity * 100f)}%");
            }

            RectTransform weatherRect = _weatherRoot as RectTransform ?? _weatherRoot?.GetComponent<RectTransform>();
            RectTransform trafficRect = _trafficRoot as RectTransform ?? _trafficRoot?.GetComponent<RectTransform>();
            SetText(_weatherSizeText, $"{Mathf.RoundToInt(GetRadarPixelSize(weatherRect))}PX");
            SetText(_trafficSizeText, $"{Mathf.RoundToInt(GetRadarPixelSize(trafficRect))}PX");

            SetText(_weatherAdvancedText, _showWeatherAdvancedControls ? "LESS" : "MORE");
            SetText(_trafficAdvancedText, _showTrafficAdvancedControls ? "LESS" : "MORE");
            SetText(_weatherExpandText, _weatherExpanded ? "<" : ">");
            SetText(_trafficExpandText, _trafficExpanded ? "<" : ">");
            SetButtonActive(_weatherAdvancedText, _showWeatherAdvancedControls);
            SetButtonActive(_trafficAdvancedText, _showTrafficAdvancedControls);
            SetButtonActive(_weatherPowerText, _weatherProvider != null && _weatherProvider.Status != ProviderStatus.Inactive);
        }

        private void SyncWeatherProviderSettings()
        {
            if (_weatherProvider == null || _weatherDataProvider == null)
            {
                return;
            }

            WeatherRadarData data = _weatherDataProvider.RadarData;
            _weatherProvider.SetRange(data.currentRange);
            _weatherProvider.SetTilt(data.tiltAngle);
            _weatherProvider.SetGain(data.gainOffset);
        }

        private void ApplyWeatherOverlayVisibility()
        {
            if (_weatherRoot == null)
            {
                return;
            }

            foreach (XPlaneOriginalWeatherRadarDisplay display in
                     _weatherRoot.GetComponentsInChildren<XPlaneOriginalWeatherRadarDisplay>(true))
            {
                if (display != null)
                {
                    display.ShowReferenceOverlay = _weatherOverlayVisible;
                }
            }

            _weatherOverlays = _weatherRoot.GetComponentsInChildren<XPlaneWeatherRadarOverlay>(true);

            foreach (XPlaneWeatherRadarOverlay overlay in _weatherOverlays)
            {
                if (overlay != null)
                {
                    overlay.gameObject.SetActive(_weatherOverlayVisible);
                    overlay.enabled = _weatherOverlayVisible;
                    RawImage image = overlay.GetComponent<RawImage>();
                    if (image != null)
                    {
                        image.enabled = _weatherOverlayVisible;
                        image.raycastTarget = false;
                    }
                }
            }
        }

        private void HandleKeyboardShortcuts()
        {
            bool resize = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                if (resize) WeatherSizeDown(); else WeatherRangeDown();
            }
            else if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                if (resize) WeatherSizeUp(); else WeatherRangeUp();
            }
            else if (Input.GetKeyDown(KeyCode.Comma))
            {
                if (resize) TrafficSizeDown(); else TrafficRangeDown();
            }
            else if (Input.GetKeyDown(KeyCode.Period))
            {
                if (resize) TrafficSizeUp(); else TrafficRangeUp();
            }
        }

        private static void SetStripVisible(RectTransform strip, bool visible)
        {
            if (strip != null)
            {
                strip.gameObject.SetActive(visible);
            }
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        private static void SetButtonActive(TMP_Text label, bool active)
        {
            if (label == null)
            {
                return;
            }

            Image image = label.GetComponentInParent<Image>();
            if (image != null)
            {
                image.color = active ? ButtonActiveColor : ButtonNormalColor;
            }
        }

        private static string Signed(float value, string format)
        {
            return value >= 0f ? "+" + value.ToString(format) : value.ToString(format);
        }

        private Vector2 GetWeatherStripSize()
        {
            return _showWeatherAdvancedControls
                ? new Vector2(Mathf.Max(weatherStripSize.x, WeatherAdvancedWidth), Mathf.Max(weatherStripSize.y, ThreeRowStripHeight))
                : new Vector2(_weatherExpanded ? WeatherCompactWidth : WeatherCollapsedWidth, CompactStripHeight);
        }

        private Vector2 GetTrafficStripSize()
        {
            return _showTrafficAdvancedControls
                ? new Vector2(Mathf.Max(trafficStripSize.x, TrafficAdvancedWidth), Mathf.Max(trafficStripSize.y, ThreeRowStripHeight))
                : new Vector2(_trafficExpanded ? TrafficCompactWidth : TrafficCollapsedWidth, CompactStripHeight);
        }

        private void SuppressLegacyRadarControlPanels()
        {
            if (!suppressLegacyRadarControlPanels)
            {
                return;
            }

            foreach (TrafficRadarRangeUI rangeUi in FindObjectsByType<TrafficRadarRangeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SuppressLegacyPanel(rangeUi);
            }

            foreach (TrafficRadarFilterUI filterUi in FindObjectsByType<TrafficRadarFilterUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SuppressLegacyPanel(filterUi);
            }

            foreach (TrafficRadarClickHandler clickHandler in FindObjectsByType<TrafficRadarClickHandler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (clickHandler != null)
                {
                    clickHandler.enabled = false;
                }
            }

            foreach (WeatherRadarClickHandler clickHandler in FindObjectsByType<WeatherRadarClickHandler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (clickHandler != null)
                {
                    clickHandler.enabled = false;
                }
            }
        }

        private static void ImproveWeatherLabelLegibility(Transform weatherRoot)
        {
            if (weatherRoot == null)
            {
                return;
            }

            foreach (Transform child in weatherRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || child == weatherRoot)
                {
                    continue;
                }

                switch (child.name)
                {
                    case "ModeLabel":
                    case "TextureStatusLabel":
                    case "SourceLabel":
                    case "TextureAgeLabel":
                        // The power badge owns the authoritative live mode. Source and
                        // texture diagnostics remain available in the conditions drawer,
                        // where they do not crowd the compact radar presentation.
                        child.gameObject.SetActive(false);
                        break;
                    case "RangeLabel":
                    case "TiltLabel":
                    case "WeatherPowerBadge":
                        child.gameObject.SetActive(true);
                        LayoutWeatherReadout(child, child.name, weatherRoot);
                        StyleWeatherReadout(child, WeatherReadoutFontSize(child.name));
                        break;
                }
            }
        }

        private static void LayoutWeatherReadout(Transform root, string objectName, Transform weatherRoot)
        {
            RectTransform rect = root as RectTransform ?? root.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            float scale = CalculateWeatherLayoutScale(weatherRoot);

            switch (objectName)
            {
                case "SourceLabel":
                    rect.anchoredPosition = new Vector2(-105f, 142f) * scale;
                    rect.sizeDelta = new Vector2(132f, 26f) * scale;
                    break;
                case "TextureAgeLabel":
                    rect.anchoredPosition = new Vector2(137f, 142f) * scale;
                    rect.sizeDelta = new Vector2(64f, 26f) * scale;
                    break;
                case "TextureStatusLabel":
                    rect.anchoredPosition = new Vector2(-105f, -126f) * scale;
                    rect.sizeDelta = new Vector2(138f, 26f) * scale;
                    break;
                case "TiltLabel":
                    rect.anchoredPosition = new Vector2(82f, -104f) * scale;
                    rect.sizeDelta = new Vector2(112f, 26f) * scale;
                    break;
                case "RangeLabel":
                    rect.anchoredPosition = new Vector2(0f, -127f) * scale;
                    rect.sizeDelta = new Vector2(112f, 28f) * scale;
                    break;
                case "WeatherPowerBadge":
                    rect.sizeDelta = new Vector2(116f, 26f) * Mathf.Max(0.9f, scale);
                    break;
            }
        }

        private static float CalculateWeatherLayoutScale(Transform weatherRoot)
        {
            RectTransform rootRect = weatherRoot as RectTransform ?? weatherRoot?.GetComponent<RectTransform>();
            if (rootRect == null)
            {
                return 1f;
            }

            float width = rootRect.rect.width > 1f ? rootRect.rect.width : rootRect.sizeDelta.x;
            float height = rootRect.rect.height > 1f ? rootRect.rect.height : rootRect.sizeDelta.y;
            float shortest = Mathf.Min(Mathf.Abs(width), Mathf.Abs(height));
            return Mathf.Clamp(shortest / 280f, 0.78f, 2f);
        }

        private static void SetInlineWeatherText(Transform weatherRoot, string objectName, string value)
        {
            if (weatherRoot == null)
            {
                return;
            }

            foreach (TMP_Text text in weatherRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text != null && text.name == objectName)
                {
                    text.text = value;
                }
            }
        }

        private static float WeatherReadoutFontSize(string objectName)
        {
            switch (objectName)
            {
                case "RangeLabel": return 17f;
                case "TiltLabel": return 16f;
                case "SourceLabel": return 15f;
                case "WeatherPowerBadge": return 16f;
                case "TextureAgeLabel": return 13f;
                default: return 11f;
            }
        }

        private static void StyleWeatherReadout(Transform root, float fontSize)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null)
                {
                    continue;
                }

                text.enableAutoSizing = false;
                text.fontSize = fontSize;
                text.fontStyle |= FontStyles.Bold;
                text.extraPadding = true;
                text.outlineWidth = 0.18f;
                text.outlineColor = new Color32(0, 10, 7, 235);
                text.color = new Color(0.78f, 1f, 0.8f, 1f);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                text.raycastTarget = false;
            }
        }

        private void SuppressLegacyPanel(MonoBehaviour panel)
        {
            if (panel == null)
            {
                return;
            }

            panel.enabled = false;
            CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (CanDeactivateLegacyPanelObject(panel.transform))
            {
                panel.gameObject.SetActive(false);
                return;
            }

            HideLegacyGeneratedChild(panel.transform, "RangeButtonContainer");
            HideLegacyGeneratedChild(panel.transform, "FilterContainer");
        }

        private bool CanDeactivateLegacyPanelObject(Transform panelTransform)
        {
            if (panelTransform == null)
            {
                return false;
            }

            if (_weatherRoot != null && panelTransform == _weatherRoot)
            {
                return false;
            }

            if (_trafficRoot != null && panelTransform == _trafficRoot)
            {
                return false;
            }

            if (_trafficDisplay != null && panelTransform == _trafficDisplay.transform)
            {
                return false;
            }

            return panelTransform.GetComponent<TrafficRadarDisplay>() == null &&
                   panelTransform.GetComponent<WeatherRadarPanel>() == null;
        }

        private static void HideLegacyGeneratedChild(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("FAA Radar Controls EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            if (eventSystem.GetComponent<BaseInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }

            if (!eventSystem.gameObject.activeSelf)
            {
                eventSystem.gameObject.SetActive(true);
            }
        }

        private static Transform FindLoadedTransform(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform != null && transform.name == objectName && transform.gameObject.scene.IsValid() && transform.gameObject.scene.isLoaded)
                {
                    return transform;
                }
            }

            return null;
        }

        private void MatchStripToRadarRoot(RectTransform strip, Transform root)
        {
            RectTransform rootRect = root as RectTransform;
            if (rootRect == null)
            {
                rootRect = root.GetComponent<RectTransform>();
            }

            if (rootRect == null)
            {
                strip.anchorMin = new Vector2(0.5f, 1f);
                strip.anchorMax = new Vector2(0.5f, 1f);
                strip.pivot = new Vector2(0.5f, 0f);
                strip.anchoredPosition = stripOffset;
                return;
            }

            strip.anchorMin = rootRect.anchorMin;
            strip.anchorMax = rootRect.anchorMax;
            float rootWidth = rootRect.rect.width > 1f ? rootRect.rect.width : rootRect.sizeDelta.x;
            float rootHeight = rootRect.rect.height > 1f ? rootRect.rect.height : rootRect.sizeDelta.y;
            bool rightAnchored = rootRect.anchorMin.x > 0.5f || rootRect.pivot.x > 0.5f;

            if (rightAnchored)
            {
                strip.pivot = new Vector2(1f, 0f);
                float rootRightEdge = rootRect.anchoredPosition.x + (rootWidth * (1f - rootRect.pivot.x));
                strip.anchoredPosition = new Vector2(rootRightEdge - stripOffset.x, rootRect.anchoredPosition.y + rootHeight + stripOffset.y);
                return;
            }

            strip.pivot = new Vector2(0f, 0f);
            float rootLeftEdge = rootRect.anchoredPosition.x - (rootWidth * rootRect.pivot.x);
            strip.anchoredPosition = new Vector2(rootLeftEdge + stripOffset.x, rootRect.anchoredPosition.y + rootHeight + stripOffset.y);
        }

        private static void HideDirectControlChildren(RectTransform strip, params RectTransform[] rowsToKeep)
        {
            for (int i = 0; i < strip.childCount; i++)
            {
                Transform child = strip.GetChild(i);
                if (child == null || IsKeptRow(child, rowsToKeep))
                {
                    continue;
                }

                if (child.name.Contains("ControlRow"))
                {
                    continue;
                }

                child.gameObject.SetActive(false);
            }
        }

        private static bool IsKeptRow(Transform child, RectTransform[] rowsToKeep)
        {
            for (int i = 0; i < rowsToKeep.Length; i++)
            {
                if (rowsToKeep[i] != null && child == rowsToKeep[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static void HideUnexpectedRowChildren(RectTransform row, params string[] visibleNames)
        {
            if (row == null)
            {
                return;
            }

            for (int i = 0; i < row.childCount; i++)
            {
                Transform child = row.GetChild(i);
                if (child != null && !IsVisibleControlName(child.name, visibleNames))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static bool IsVisibleControlName(string childName, string[] visibleNames)
        {
            for (int i = 0; i < visibleNames.Length; i++)
            {
                if (childName == visibleNames[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static TMP_Text GetButtonLabel(Button button)
        {
            return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
