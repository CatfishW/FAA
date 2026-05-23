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
        private const float CompactStripHeight = 48f;
        private const float RowHeight = 34f;
        private const float WeatherCollapsedWidth = 186f;
        private const float WeatherCompactWidth = 308f;
        private const float WeatherAdvancedWidth = 336f;
        private const float TrafficCollapsedWidth = 246f;
        private const float TrafficCompactWidth = 330f;
        private const float TrafficAdvancedWidth = 344f;
        private const float TwoRowStripHeight = 82f;
        private const float ThreeRowStripHeight = 118f;
        private static readonly Color StripBackgroundColor = new Color(0f, 0.026f, 0.018f, 0.82f);
        private static readonly Color StripStrokeColor = new Color(0.18f, 1f, 0.32f, 0.55f);
        private static readonly Color ButtonNormalColor = new Color(0.014f, 0.15f, 0.055f, 0.96f);
        private static readonly Color ButtonHighlightedColor = new Color(0.045f, 0.25f, 0.105f, 1f);
        private static readonly Color ButtonPressedColor = new Color(0.012f, 0.095f, 0.035f, 1f);
        private static readonly Color ButtonActiveColor = new Color(0.075f, 0.28f, 0.12f, 1f);
        private static readonly Color PrimaryTextColor = new Color(0.64f, 1f, 0.68f, 1f);
        private static readonly Color SecondaryTextColor = new Color(0.76f, 1f, 0.78f, 1f);

        [Header("Scene Names")]
        [SerializeField] private string weatherRadarRootName = "X-Plane Weather Radar System";
        [SerializeField] private string trafficRadarRootName = "Traffic Radar System";

        [Header("Layout")]
        [SerializeField] private Vector2 weatherStripSize = new Vector2(430f, 76f);
        [SerializeField] private Vector2 trafficStripSize = new Vector2(448f, 76f);
        [SerializeField] private Vector2 stripOffset = new Vector2(0f, 8f);
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private bool startExpanded;

        [Header("Controls")]
        [SerializeField] private bool enableWeatherControls = true;
        [SerializeField] private bool enableTrafficControls = true;
        [SerializeField] private bool enableKeyboardShortcuts;

        [Header("Compatibility")]
        [SerializeField] private bool suppressLegacyRadarControlPanels = true;

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
        private RectTransform _trafficStrip;
        private TMP_Text _weatherRangeText;
        private TMP_Text _weatherSummaryText;
        private TMP_Text _weatherTiltText;
        private TMP_Text _weatherGainText;
        private TMP_Text _weatherModeText;
        private TMP_Text _weatherPowerText;
        private TMP_Text _weatherExpandText;
        private TMP_Text _weatherAdvancedText;
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
        private bool _weatherOverlayVisible;
        private bool _weatherExpanded;
        private bool _trafficExpanded;
        private bool _showWeatherAdvancedControls;
        private bool _showTrafficAdvancedControls;
        private bool _controlsVisible;
        private bool _visibilityInitialized;
        private float _nextRefreshTime;

        public void Configure(Transform weatherRoot, Transform trafficRoot)
        {
            _weatherRoot = weatherRoot;
            _trafficRoot = trafficRoot;
            RefreshReferences();
            EnsureControlStrips();
            UpdateLabels();
        }

        private void Awake()
        {
            _weatherExpanded = startExpanded;
            _trafficExpanded = startExpanded;
            RefreshReferences();
            EnsureControlStrips();
            SetVisible(showOnStart);
        }

        private void OnEnable()
        {
            _weatherExpanded |= startExpanded;
            _trafficExpanded |= startExpanded;
            RefreshReferences();
            EnsureControlStrips();
            SetVisible(showOnStart);
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
                EnsureControlStrips();
                UpdateLabels();
            }
        }

        public void SetVisible(bool visible)
        {
            _controlsVisible = visible;
            _visibilityInitialized = true;
            SetStripVisible(_weatherStrip, visible && enableWeatherControls);
            SetStripVisible(_trafficStrip, visible && enableTrafficControls);
        }

        public void ToggleVisible()
        {
            bool nextVisible = !(_weatherStrip != null && _weatherStrip.gameObject.activeSelf);
            SetVisible(nextVisible);
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

        public void RefreshTraffic()
        {
            _trafficController?.RefreshData();
            _trafficDataManager?.FetchDataNow();
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
                _weatherStrip = EnsureStrip(_weatherRoot, "WeatherControlStrip", GetWeatherStripSize());
                EnsureWeatherControls(_weatherStrip);
            }

            if (enableTrafficControls && _trafficRoot != null)
            {
                _trafficStrip = EnsureStrip(_trafficRoot, "TrafficControlStrip", GetTrafficStripSize());
                EnsureTrafficControls(_trafficStrip);
            }
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
            rectTransform.localScale = Vector3.one;
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
            HideDirectControlChildren(strip, primaryRow, secondaryRow);

            if (_weatherExpanded)
            {
                _weatherExpandText = GetButtonLabel(EnsureButton(primaryRow, "WXExpandToggle", "<", ToggleWeatherExpanded, 24f));
                _weatherRangeText = EnsureLabel(primaryRow, "WXRangeValue", "WX 40", 58f);
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
                _weatherSummaryText = GetButtonLabel(EnsureButton(primaryRow, "WXSummaryToggle", "WX 40NM >", ToggleWeatherExpanded, WeatherCollapsedWidth - 10f));
                _weatherExpandText = null;
                HideUnexpectedRowChildren(primaryRow, "WXSummaryToggle");
            }

            _weatherTiltText = EnsureLabel(secondaryRow, "WXTiltValue", "T+0.0", 58f);
            EnsureButton(secondaryRow, "WXTiltDown", "T-", WeatherTiltDown, 32f);
            EnsureButton(secondaryRow, "WXTiltUp", "T+", WeatherTiltUp, 32f);
            _weatherGainText = EnsureLabel(secondaryRow, "WXGainValue", "G+0", 50f);
            EnsureButton(secondaryRow, "WXGainDown", "G-", WeatherGainDown, 32f);
            EnsureButton(secondaryRow, "WXGainUp", "G+", WeatherGainUp, 32f);
            _weatherPowerText = GetButtonLabel(EnsureButton(secondaryRow, "WXPowerToggle", "PWR", ToggleWeatherProvider, 42f));
            EnsureButton(secondaryRow, "WXRefresh", "REF", RefreshWeatherTexture, 42f);
            HideUnexpectedRowChildren(
                secondaryRow,
                "WXTiltValue", "WXTiltDown", "WXTiltUp", "WXGainValue", "WXGainDown", "WXGainUp",
                "WXPowerToggle", "WXRefresh");
            secondaryRow.gameObject.SetActive(_weatherExpanded && _showWeatherAdvancedControls);
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
                _trafficSummaryText = GetButtonLabel(EnsureButton(primaryRow, "TCASSummaryToggle", "TRF 0/50 40NM >", ToggleTrafficExpanded, TrafficCollapsedWidth - 10f));
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
            EnsureButton(tertiaryRow, "TCASRefresh", "REF", RefreshTraffic, 42f);
            HideUnexpectedRowChildren(
                tertiaryRow,
                "TCASRingsValue", "TCASRingsDown", "TCASRingsUp", "TCASOpacityValue",
                "TCASOpacityDown", "TCASOpacityUp", "TCASRefresh");
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

            TMP_Text label = EnsureText(buttonObject.transform, "Label", text, 13f);
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

            TMP_Text label = EnsureText(labelObject.transform, "Text", text, 12f);
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
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = fontSize;
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
                SetText(_weatherSummaryText, $"{powerText} {data.currentRange:0}NM >");
                SetText(_weatherRangeText, $"WX {data.currentRange:0}");
                SetText(_weatherTiltText, $"T{Signed(data.tiltAngle, "0.0")}");
                SetText(_weatherGainText, $"G{Signed(data.gainOffset, "0")}");
                SetText(_weatherModeText, modeText);
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
                SetText(_trafficSummaryText, $"TRF {liveTrafficCount}/{_trafficController.MaxTargets} {_trafficController.RangeNM:0}NM >");
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
            if (_weatherOverlays == null)
            {
                return;
            }

            foreach (XPlaneWeatherRadarOverlay overlay in _weatherOverlays)
            {
                if (overlay != null)
                {
                    overlay.enabled = _weatherOverlayVisible;
                    RawImage image = overlay.GetComponent<RawImage>();
                    if (image != null)
                    {
                        image.enabled = _weatherOverlayVisible;
                    }
                }
            }
        }

        private void HandleKeyboardShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                WeatherRangeDown();
            }
            else if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                WeatherRangeUp();
            }
            else if (Input.GetKeyDown(KeyCode.Comma))
            {
                TrafficRangeDown();
            }
            else if (Input.GetKeyDown(KeyCode.Period))
            {
                TrafficRangeUp();
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
                ? new Vector2(Mathf.Max(weatherStripSize.x, WeatherAdvancedWidth), Mathf.Max(weatherStripSize.y, TwoRowStripHeight))
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
