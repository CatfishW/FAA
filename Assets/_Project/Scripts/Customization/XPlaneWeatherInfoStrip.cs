using System;
using System.Collections.Generic;
using System.Globalization;
using AviationUI;
using FAA.XPlaneIntegration.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization
{
    public enum WeatherMetricIconKind
    {
        Temperature,
        Wind,
        Visibility,
        Pressure,
        Precipitation
    }

    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("FAA/Customization/Weather Metric Icon")]
    public sealed class WeatherMetricIconGraphic : MaskableGraphic
    {
        [SerializeField] private WeatherMetricIconKind iconKind;

        public WeatherMetricIconKind IconKind
        {
            get => iconKind;
            set
            {
                if (iconKind == value)
                {
                    return;
                }

                iconKind = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = rectTransform.rect;
            float size = Mathf.Max(1f, Mathf.Min(rect.width, rect.height));
            float unit = size / 24f;
            float stroke = Mathf.Max(1.15f, unit * 1.65f);
            Vector2 center = rect.center;
            Color32 tint = color;

            switch (iconKind)
            {
                case WeatherMetricIconKind.Temperature:
                    DrawTemperature(vertexHelper, center, unit, stroke, tint);
                    break;
                case WeatherMetricIconKind.Wind:
                    DrawWind(vertexHelper, center, unit, stroke, tint);
                    break;
                case WeatherMetricIconKind.Visibility:
                    DrawVisibility(vertexHelper, center, unit, stroke, tint);
                    break;
                case WeatherMetricIconKind.Pressure:
                    DrawPressure(vertexHelper, center, unit, stroke, tint);
                    break;
                case WeatherMetricIconKind.Precipitation:
                    DrawPrecipitation(vertexHelper, center, unit, stroke, tint);
                    break;
            }
        }

        private static void DrawTemperature(VertexHelper vh, Vector2 center, float unit, float stroke, Color32 tint)
        {
            Vector2 bulbCenter = center + Vector2.down * unit * 5f;
            AddLine(vh, bulbCenter + Vector2.up * unit, center + Vector2.up * unit * 7f, stroke * 1.8f, tint);
            AddCircle(vh, bulbCenter, unit * 3.2f, 18, tint, true, stroke);
            AddArc(vh, center + Vector2.up * unit * 2f, unit * 3.1f, -88f, 88f, 10, stroke, tint);
            AddLine(vh, center + new Vector2(unit * 2.5f, unit * 7f), center + new Vector2(unit * 5f, unit * 7f), stroke, tint);
        }

        private static void DrawWind(VertexHelper vh, Vector2 center, float unit, float stroke, Color32 tint)
        {
            AddLine(vh, center + new Vector2(-9f, 4.5f) * unit, center + new Vector2(6.5f, 4.5f) * unit, stroke, tint);
            AddArc(vh, center + new Vector2(6.5f, 2.3f) * unit, unit * 2.2f, 90f, 270f, 10, stroke, tint);
            AddLine(vh, center + new Vector2(-7f, 0f) * unit, center + new Vector2(3.5f, 0f) * unit, stroke, tint);
            AddLine(vh, center + new Vector2(-9f, -4.5f) * unit, center + new Vector2(7f, -4.5f) * unit, stroke, tint);
            AddLine(vh, center + new Vector2(7f, -4.5f) * unit, center + new Vector2(4.2f, -1.8f) * unit, stroke, tint);
        }

        private static void DrawVisibility(VertexHelper vh, Vector2 center, float unit, float stroke, Color32 tint)
        {
            AddArc(vh, center, unit * 9f, 18f, 162f, 15, stroke, tint);
            AddArc(vh, center, unit * 9f, 198f, 342f, 15, stroke, tint);
            AddCircle(vh, center, unit * 3f, 16, tint, false, stroke);
            AddCircle(vh, center, unit * 1.1f, 12, tint, true, stroke);
        }

        private static void DrawPressure(VertexHelper vh, Vector2 center, float unit, float stroke, Color32 tint)
        {
            AddCircle(vh, center, unit * 8.2f, 24, tint, false, stroke);
            AddLine(vh, center, center + new Vector2(4.8f, 4.6f) * unit, stroke * 1.25f, tint);
            AddCircle(vh, center, unit * 1.35f, 12, tint, true, stroke);
            AddLine(vh, center + new Vector2(-5.2f, -5.5f) * unit, center + new Vector2(5.2f, -5.5f) * unit, stroke, tint);
        }

        private static void DrawPrecipitation(VertexHelper vh, Vector2 center, float unit, float stroke, Color32 tint)
        {
            Vector2 tip = center + Vector2.up * unit * 9f;
            Vector2 left = center + new Vector2(-5.5f, -1.5f) * unit;
            Vector2 right = center + new Vector2(5.5f, -1.5f) * unit;
            AddTriangle(vh, tip, left, right, new Color32(tint.r, tint.g, tint.b, (byte)(tint.a * 0.52f)));
            AddCircle(vh, center + Vector2.down * unit * 2.8f, unit * 5.5f, 20, tint, true, stroke);
            AddLine(vh, tip, left, stroke, tint);
            AddLine(vh, tip, right, stroke, tint);
        }

        private static void AddArc(
            VertexHelper vh,
            Vector2 center,
            float radius,
            float startDegrees,
            float endDegrees,
            int segments,
            float width,
            Color32 tint)
        {
            Vector2 previous = center + Direction(startDegrees) * radius;
            for (int i = 1; i <= Mathf.Max(1, segments); i++)
            {
                float angle = Mathf.Lerp(startDegrees, endDegrees, i / (float)segments);
                Vector2 next = center + Direction(angle) * radius;
                AddLine(vh, previous, next, width, tint);
                previous = next;
            }
        }

        private static Vector2 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static void AddLine(VertexHelper vh, Vector2 from, Vector2 to, float width, Color32 tint)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector2 perpendicular = new Vector2(-delta.y, delta.x).normalized * Mathf.Max(0.5f, width) * 0.5f;
            AddQuad(vh, from - perpendicular, from + perpendicular, to + perpendicular, to - perpendicular, tint);
        }

        private static void AddCircle(
            VertexHelper vh,
            Vector2 center,
            float radius,
            int segments,
            Color32 tint,
            bool filled,
            float stroke)
        {
            int safeSegments = Mathf.Max(8, segments);
            if (!filled)
            {
                AddArc(vh, center, radius, 0f, 360f, safeSegments, stroke, tint);
                return;
            }

            int centerIndex = vh.currentVertCount;
            AddVertex(vh, center, tint);
            for (int i = 0; i <= safeSegments; i++)
            {
                float angle = i / (float)safeSegments * Mathf.PI * 2f;
                AddVertex(vh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint);
            }

            for (int i = 0; i < safeSegments; i++)
            {
                vh.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + i + 2);
            }
        }

        private static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color32 tint)
        {
            int start = vh.currentVertCount;
            AddVertex(vh, a, tint);
            AddVertex(vh, b, tint);
            AddVertex(vh, c, tint);
            vh.AddTriangle(start, start + 1, start + 2);
        }

        private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 tint)
        {
            int start = vh.currentVertCount;
            AddVertex(vh, a, tint);
            AddVertex(vh, b, tint);
            AddVertex(vh, c, tint);
            AddVertex(vh, d, tint);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        private static void AddVertex(VertexHelper vh, Vector2 position, Color32 tint)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = tint;
            vh.AddVert(vertex);
        }
    }

    public struct XPlaneWeatherReadout
    {
        public bool HasData;
        public float TemperatureC;
        public float WindDirectionDegrees;
        public float WindSpeedKnots;
        public float VisibilityMeters;
        public float QnhInHg;
        public float PrecipitationRatio;
    }

    [DefaultExecutionOrder(10025)]
    [RequireComponent(typeof(CanvasGroup))]
    [AddComponentMenu("FAA/Customization/X-Plane Weather Info Strip")]
    public sealed class XPlaneWeatherInfoStrip : MonoBehaviour
    {
        public const string StripObjectName = "WeatherConditionsStrip";
        private const float StripWidth = 240f;
        private const float HeaderHeight = 42f;
        private const float CollapsedHeight = 0f;
        private const float ExpandedHeight = 300f;
        private const float DetailsHeight = 242f;
        private const float MetricHeight = 46f;
        private const float StripGap = 12f;
        private const float ExpandDuration = 0.22f;
        private const float CollapseDuration = 0.16f;
        private const float RefreshInterval = 0.25f;
        private const float MetersPerStatuteMile = 1609.344f;
        private const float PascalsToInHg = 0.000295300f;

        private static readonly Color BackgroundOnline = new Color(0.016f, 0.065f, 0.09f, 0.97f);
        private static readonly Color BackgroundOffline = new Color(0.035f, 0.065f, 0.08f, 0.94f);
        private static readonly Color HeaderOnline = new Color(0.025f, 0.145f, 0.19f, 1f);
        private static readonly Color HeaderOffline = new Color(0.065f, 0.105f, 0.125f, 1f);
        private static readonly Color RowBackground = new Color(0.025f, 0.105f, 0.135f, 0.82f);
        private static readonly Color StrokeOnline = new Color(0.25f, 0.86f, 0.95f, 0.72f);
        private static readonly Color StrokeOffline = new Color(0.34f, 0.48f, 0.55f, 0.72f);
        private static readonly Color ValueNormal = new Color(0.82f, 0.98f, 1f, 1f);
        private static readonly Color ValueCool = new Color(0.34f, 0.8f, 1f, 1f);
        private static readonly Color ValueCaution = new Color(1f, 0.72f, 0.24f, 1f);
        private static readonly Color ValueWarning = new Color(1f, 0.35f, 0.25f, 1f);
        private static readonly Color LabelColor = new Color(0.5f, 0.76f, 0.82f, 1f);
        private static readonly Color AccentColor = new Color(0.31f, 0.9f, 0.96f, 1f);

        [SerializeField] private XPlane12ApiHudBridge bridge;
        [SerializeField] private RectTransform radarRoot;
        [SerializeField] private RectTransform controlStrip;
        [SerializeField] private bool expandedOnStart;
        [SerializeField] private bool reducedMotion;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Image _background;
        private Outline _outline;
        private RectTransform _detailsRect;
        private CanvasGroup _detailsCanvasGroup;
        private Image _headerBackground;
        private TMP_Text _headerStatus;
        private RectTransform _chevronRect;
        private MetricView _temperature;
        private MetricView _wind;
        private MetricView _visibility;
        private MetricView _pressure;
        private MetricView _precipitation;
        private bool _visualTreeReady;
        private bool _disclosureInitialized;
        private bool _expanded;
        private float _disclosureProgress;
        private float _nextRefreshTime;

        private sealed class MetricView
        {
            public WeatherMetricIconGraphic Icon;
            public TMP_Text Value;
        }

        public bool IsExpanded => _expanded;
        public float DisclosureProgress => _disclosureProgress;
        public event Action<bool> ExpandedChanged;

        public bool ReducedMotion
        {
            get => reducedMotion;
            set => reducedMotion = value;
        }

        public void Configure(XPlane12ApiHudBridge sourceBridge, RectTransform sourceRadarRoot, RectTransform sourceControlStrip)
        {
            bridge = sourceBridge;
            radarRoot = sourceRadarRoot;
            controlStrip = sourceControlStrip;
            EnsureVisualTree();
            PositionToRightOfRadar();
            RefreshReadout();
        }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            EnsureVisualTree();
            InitializeDisclosure();
        }

        private void OnEnable()
        {
            EnsureVisualTree();
            InitializeDisclosure();
            PositionToRightOfRadar();
            RefreshReadout();
        }

        private void Update()
        {
            AnimateDisclosure();

            if (Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.unscaledTime + RefreshInterval;
            if (bridge == null)
            {
                bridge = FindAnyObjectByType<XPlane12ApiHudBridge>(FindObjectsInactive.Include);
            }

            PositionToRightOfRadar();
            RefreshReadout();
        }

        private void EnsureVisualTree()
        {
            if (_visualTreeReady)
            {
                return;
            }

            _rectTransform = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            _rectTransform.sizeDelta = new Vector2(StripWidth, ExpandedHeight);
            _rectTransform.localScale = Vector3.one;
            _rectTransform.localRotation = Quaternion.identity;

            _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            _background = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            _background.color = BackgroundOffline;
            _background.raycastTarget = false;

            _outline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            _outline.effectColor = StrokeOffline;
            _outline.effectDistance = new Vector2(1f, -1f);

            RectMask2D mask = GetComponent<RectMask2D>() ?? gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;

            HorizontalLayoutGroup oldHorizontal = GetComponent<HorizontalLayoutGroup>();
            if (oldHorizontal != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(oldHorizontal);
                }
                else
                {
                    DestroyImmediate(oldHorizontal);
                }
            }

            // Retain the component for compatibility with existing scene instances while
            // using explicit anchors so the rail can resize without layout snapping.
            VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
            layout.enabled = false;

            EnsureHeader();
            EnsureDetailsContainer();

            _temperature = EnsureMetric("OAT", "OUTSIDE AIR TEMP", WeatherMetricIconKind.Temperature);
            _wind = EnsureMetric("AircraftWind", "AIRCRAFT WIND", WeatherMetricIconKind.Wind);
            _visibility = EnsureMetric("Visibility", "VISIBILITY", WeatherMetricIconKind.Visibility);
            _pressure = EnsureMetric("Pressure", "QNH · INHG", WeatherMetricIconKind.Pressure);
            _precipitation = EnsureMetric("Precipitation", "PRECIPITATION", WeatherMetricIconKind.Precipitation);
            _visualTreeReady = true;
        }

        private void EnsureHeader()
        {
            Transform existing = transform.Find("SummaryHeader");
            GameObject header = existing != null
                ? existing.gameObject
                : new GameObject("SummaryHeader", typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.SetParent(transform, false);
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0f, -6f);
            headerRect.sizeDelta = new Vector2(-12f, HeaderHeight);

            _headerBackground = header.GetComponent<Image>() ?? header.AddComponent<Image>();
            _headerBackground.color = HeaderOffline;
            _headerBackground.raycastTarget = true;

            Button button = header.GetComponent<Button>() ?? header.AddComponent<Button>();
            button.targetGraphic = _headerBackground;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.78f, 0.9f, 0.94f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.55f, 0.58f, 0.7f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = reducedMotion ? 0.01f : 0.1f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.RemoveListener(ToggleExpanded);
            button.onClick.AddListener(ToggleExpanded);

            TMP_Text title = EnsureText(header.transform, "Title", "WX CONDITIONS", 13f, ValueNormal, FontStyles.Bold);
            title.rectTransform.anchorMin = Vector2.zero;
            title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = new Vector2(13f, 0f);
            title.rectTransform.offsetMax = new Vector2(-102f, 0f);
            title.alignment = TextAlignmentOptions.MidlineLeft;

            _headerStatus = EnsureText(header.transform, "Status", "WAITING", 9.5f, LabelColor, FontStyles.Bold);
            _headerStatus.rectTransform.anchorMin = new Vector2(1f, 0f);
            _headerStatus.rectTransform.anchorMax = Vector2.one;
            _headerStatus.rectTransform.offsetMin = new Vector2(-99f, 0f);
            _headerStatus.rectTransform.offsetMax = new Vector2(-31f, 0f);
            _headerStatus.alignment = TextAlignmentOptions.MidlineRight;

            TMP_Text chevron = EnsureText(header.transform, "Chevron", ">", 20f, AccentColor, FontStyles.Bold);
            _chevronRect = chevron.rectTransform;
            _chevronRect.anchorMin = new Vector2(1f, 0f);
            _chevronRect.anchorMax = Vector2.one;
            _chevronRect.offsetMin = new Vector2(-29f, 0f);
            _chevronRect.offsetMax = new Vector2(-7f, 0f);
            chevron.alignment = TextAlignmentOptions.Center;
        }

        private void EnsureDetailsContainer()
        {
            Transform existing = transform.Find("Details");
            GameObject details = existing != null
                ? existing.gameObject
                : new GameObject(
                    "Details",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(CanvasGroup),
                    typeof(VerticalLayoutGroup));
            _detailsRect = details.GetComponent<RectTransform>();
            _detailsRect.SetParent(transform, false);
            _detailsRect.anchorMin = new Vector2(0f, 1f);
            _detailsRect.anchorMax = new Vector2(1f, 1f);
            _detailsRect.pivot = new Vector2(0.5f, 1f);
            _detailsRect.anchoredPosition = new Vector2(0f, -52f);
            _detailsRect.sizeDelta = new Vector2(-12f, DetailsHeight);

            _detailsCanvasGroup = details.GetComponent<CanvasGroup>() ?? details.AddComponent<CanvasGroup>();
            VerticalLayoutGroup layout = details.GetComponent<VerticalLayoutGroup>() ?? details.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private MetricView EnsureMetric(string name, string labelText, WeatherMetricIconKind kind)
        {
            Transform existing = _detailsRect.Find(name) ?? transform.Find(name);
            GameObject cell = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform cellRect = cell.GetComponent<RectTransform>();
            cellRect.SetParent(_detailsRect, false);
            cellRect.SetAsLastSibling();
            cellRect.sizeDelta = new Vector2(StripWidth - 12f, MetricHeight);
            cellRect.localScale = Vector3.one;
            cellRect.localRotation = Quaternion.identity;

            LayoutElement layout = cell.GetComponent<LayoutElement>() ?? cell.AddComponent<LayoutElement>();
            layout.minWidth = StripWidth - 12f;
            layout.preferredWidth = StripWidth - 12f;
            layout.minHeight = MetricHeight;
            layout.preferredHeight = MetricHeight;

            Image background = cell.GetComponent<Image>() ?? cell.AddComponent<Image>();
            background.color = RowBackground;
            background.raycastTarget = false;

            Transform iconTransform = cell.transform.Find("Icon");
            GameObject iconObject = iconTransform != null
                ? iconTransform.gameObject
                : new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(cell.transform, false);
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(20f, 0f);
            iconRect.sizeDelta = new Vector2(29f, 29f);

            WeatherMetricIconGraphic icon = iconObject.GetComponent<WeatherMetricIconGraphic>() ??
                                            iconObject.AddComponent<WeatherMetricIconGraphic>();
            icon.IconKind = kind;
            icon.color = ValueNormal;
            icon.raycastTarget = false;

            TMP_Text label = EnsureText(cell.transform, "Label", labelText, 10.5f, LabelColor, FontStyles.Bold);
            label.rectTransform.anchorMin = new Vector2(0f, 0.54f);
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(42f, 0f);
            label.rectTransform.offsetMax = new Vector2(-6f, -1f);

            TMP_Text value = EnsureText(cell.transform, "Value", "--", 17f, ValueNormal, FontStyles.Bold);
            value.rectTransform.anchorMin = Vector2.zero;
            value.rectTransform.anchorMax = new Vector2(1f, 0.62f);
            value.rectTransform.offsetMin = new Vector2(42f, 1f);
            value.rectTransform.offsetMax = new Vector2(-6f, 1f);

            return new MetricView { Icon = icon, Value = value };
        }

        private void InitializeDisclosure()
        {
            if (_disclosureInitialized)
            {
                return;
            }

            _disclosureInitialized = true;
            SetExpanded(expandedOnStart, true);
        }

        public void ToggleExpanded()
        {
            SetExpanded(!_expanded, reducedMotion);
        }

        public void SetExpanded(bool expanded, bool immediate = false)
        {
            bool changed = _expanded != expanded;
            _expanded = expanded;
            if (immediate || reducedMotion || !Application.isPlaying)
            {
                _disclosureProgress = expanded ? 1f : 0f;
                ApplyDisclosureVisuals(_disclosureProgress);
            }

            if (changed)
            {
                ExpandedChanged?.Invoke(expanded);
            }
        }

        private void AnimateDisclosure()
        {
            float target = _expanded ? 1f : 0f;
            if (Mathf.Approximately(_disclosureProgress, target))
            {
                return;
            }

            float duration = _expanded ? ExpandDuration : CollapseDuration;
            _disclosureProgress = reducedMotion
                ? target
                : Mathf.MoveTowards(_disclosureProgress, target, Time.unscaledDeltaTime / duration);
            ApplyDisclosureVisuals(_disclosureProgress);
        }

        private void ApplyDisclosureVisuals(float progress)
        {
            float eased = EaseOutQuart(progress);
            if (_rectTransform != null)
            {
                _rectTransform.sizeDelta = new Vector2(StripWidth, Mathf.Lerp(CollapsedHeight, ExpandedHeight, eased));
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, progress);
                _canvasGroup.interactable = _expanded && progress >= 0.99f;
                _canvasGroup.blocksRaycasts = _expanded && progress >= 0.99f;
            }

            if (_detailsCanvasGroup != null)
            {
                _detailsCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, progress);
                _detailsCanvasGroup.interactable = progress >= 0.99f;
                _detailsCanvasGroup.blocksRaycasts = progress >= 0.99f;
            }

            if (_detailsRect != null)
            {
                _detailsRect.localScale = new Vector3(1f, Mathf.Lerp(0.96f, 1f, eased), 1f);
            }

            if (_chevronRect != null)
            {
                _chevronRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, -90f, eased));
            }
        }

        public static float EaseOutQuart(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse * inverse;
        }

        private static TMP_Text EnsureText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            Color tint,
            FontStyles style)
        {
            Transform existing = parent.Find(name);
            GameObject textObject = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>() ?? textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.enableAutoSizing = false;
            label.extraPadding = true;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.color = tint;
            label.raycastTarget = false;
            return label;
        }

        private void PositionToRightOfRadar()
        {
            if (_rectTransform == null || radarRoot == null)
            {
                return;
            }

            if (_rectTransform.parent != radarRoot.parent)
            {
                _rectTransform.SetParent(radarRoot.parent, false);
            }

            _rectTransform.anchorMin = radarRoot.anchorMin;
            _rectTransform.anchorMax = radarRoot.anchorMax;
            float radarWidth = radarRoot.rect.width > 1f ? radarRoot.rect.width : radarRoot.sizeDelta.x;
            float radarHeight = radarRoot.rect.height > 1f ? radarRoot.rect.height : radarRoot.sizeDelta.y;
            Vector2 bottomLeft = CalculateRightSidePosition(
                radarRoot.anchoredPosition,
                new Vector2(radarWidth, radarHeight),
                radarRoot.pivot,
                new Vector2(StripWidth, ExpandedHeight),
                StripGap);
            _rectTransform.pivot = new Vector2(0f, 1f);
            _rectTransform.anchoredPosition = bottomLeft + Vector2.up * ExpandedHeight;

            _rectTransform.localScale = Vector3.one;
            _rectTransform.localRotation = Quaternion.identity;
            _rectTransform.SetAsLastSibling();
        }

        public static Vector2 CalculateRightSidePosition(
            Vector2 radarAnchoredPosition,
            Vector2 radarSize,
            Vector2 radarPivot,
            Vector2 stripSize,
            float gap)
        {
            float radarLeft = radarAnchoredPosition.x - radarSize.x * radarPivot.x;
            float radarBottom = radarAnchoredPosition.y - radarSize.y * radarPivot.y;
            float centeredBottom = radarBottom + Mathf.Max(0f, (radarSize.y - stripSize.y) * 0.5f);
            return new Vector2(radarLeft + radarSize.x + Mathf.Max(0f, gap), centeredBottom);
        }

        private void RefreshReadout()
        {
            if (!_visualTreeReady)
            {
                return;
            }

            XPlaneWeatherReadout weather = ReadWeather(bridge);
            bool online = bridge != null && bridge.IsFeedHealthy && weather.HasData;
            SetMetric(_temperature, FormatTemperature(weather.TemperatureC), TemperatureColor(weather.TemperatureC), weather.HasData);
            SetMetric(_wind, FormatWind(weather.WindDirectionDegrees, weather.WindSpeedKnots), WindColor(weather.WindSpeedKnots), weather.HasData);
            SetMetric(_visibility, FormatVisibility(weather.VisibilityMeters), VisibilityColor(weather.VisibilityMeters), weather.HasData);
            SetMetric(_pressure, FormatPressure(weather.QnhInHg), ValueNormal, weather.HasData);
            SetMetric(_precipitation, FormatPrecipitation(weather.PrecipitationRatio), PrecipitationColor(weather.PrecipitationRatio), weather.HasData);

            if (_background != null)
            {
                _background.color = online ? BackgroundOnline : BackgroundOffline;
            }

            if (_headerBackground != null)
            {
                _headerBackground.color = online ? HeaderOnline : HeaderOffline;
            }

            if (_headerStatus != null)
            {
                _headerStatus.text = online ? "X-PLANE LIVE" : "WAITING";
                _headerStatus.color = online ? AccentColor : LabelColor;
            }

            if (_outline != null)
            {
                _outline.effectColor = online ? StrokeOnline : StrokeOffline;
            }
        }

        private static void SetMetric(MetricView metric, string text, Color tint, bool hasData)
        {
            if (metric == null)
            {
                return;
            }

            Color effectiveTint = hasData ? tint : new Color(LabelColor.r, LabelColor.g, LabelColor.b, 0.62f);
            if (metric.Value != null)
            {
                metric.Value.text = hasData ? text : "--";
                metric.Value.color = effectiveTint;
            }

            if (metric.Icon != null)
            {
                metric.Icon.color = effectiveTint;
                metric.Icon.SetVerticesDirty();
            }
        }

        public static XPlaneWeatherReadout ReadWeather(XPlane12ApiHudBridge sourceBridge)
        {
            XPlaneWeatherReadout result = new XPlaneWeatherReadout
            {
                TemperatureC = float.NaN,
                WindDirectionDegrees = float.NaN,
                WindSpeedKnots = float.NaN,
                VisibilityMeters = float.NaN,
                QnhInHg = float.NaN,
                PrecipitationRatio = float.NaN
            };

            IDictionary<string, float> values = sourceBridge?.LatestSnapshot?.Weather;
            if (values == null || values.Count == 0)
            {
                return result;
            }

            result.HasData = true;
            result.TemperatureC = GetAny(values, float.NaN,
                "sim/weather/aircraft/temperature_ambient_deg_c",
                "sim/weather/temperature_ambient_c",
                "sim/weather/aircraft/ambient_temperature_c");

            AviationFlightData flightData = sourceBridge.LatestFlightData;
            if (flightData != null)
            {
                result.WindDirectionDegrees = Mathf.Repeat(flightData.windDirection, 360f);
                result.WindSpeedKnots = Mathf.Max(0f, flightData.windSpeed);
                result.QnhInHg = flightData.barometricSetting;
            }

            result.VisibilityMeters = GetAny(values, float.NaN,
                "sim/weather/visibility_reported_m",
                "sim/weather/aircraft/visibility_reported_m");
            if (!IsFinite(result.VisibilityMeters))
            {
                float visibilitySm = GetAny(values, float.NaN,
                    "sim/weather/region/visibility_reported_sm",
                    "sim/weather/aircraft/visibility_reported_sm");
                result.VisibilityMeters = IsFinite(visibilitySm)
                    ? visibilitySm * MetersPerStatuteMile
                    : float.NaN;
            }

            if (!IsFinite(result.QnhInHg) || result.QnhInHg < 20f || result.QnhInHg > 35f)
            {
                result.QnhInHg = GetAny(values, float.NaN,
                    "sim/weather/barometer_sealevel_inhg",
                    "sim/weather/aircraft/barometer_sealevel_inhg");
                if (!IsFinite(result.QnhInHg))
                {
                    float pascals = GetAny(values, float.NaN,
                        "sim/weather/aircraft/qnh_pas",
                        "sim/weather/aircraft/barometer_current_pas");
                    result.QnhInHg = IsFinite(pascals) ? pascals * PascalsToInHg : float.NaN;
                }
            }

            result.PrecipitationRatio = MaxNormalizedValue(values,
                "sim/weather/aircraft/precipitation_on_aircraft_ratio",
                "sim/weather/precipitation_on_aircraft_ratio",
                "sim/weather/region/rain_percent",
                "sim/weather/rain_percent");
            return result;
        }

        public static string FormatTemperature(float temperatureC)
        {
            return IsFinite(temperatureC)
                ? temperatureC.ToString("0", CultureInfo.InvariantCulture) + "°C"
                : "--";
        }

        public static string FormatWind(float directionDegrees, float speedKnots)
        {
            if (!IsFinite(directionDegrees) || !IsFinite(speedKnots))
            {
                return "--";
            }

            int direction = Mathf.RoundToInt(Mathf.Repeat(directionDegrees, 360f)) % 360;
            int speed = Mathf.Max(0, Mathf.RoundToInt(speedKnots));
            return $"{direction:000}° / {speed}KT";
        }

        public static string FormatVisibility(float visibilityMeters)
        {
            if (!IsFinite(visibilityMeters) || visibilityMeters < 0f)
            {
                return "--";
            }

            float miles = visibilityMeters / MetersPerStatuteMile;
            return miles >= 10f
                ? miles.ToString("0", CultureInfo.InvariantCulture) + " SM"
                : miles.ToString("0.0", CultureInfo.InvariantCulture) + " SM";
        }

        public static string FormatPressure(float qnhInHg)
        {
            return IsFinite(qnhInHg) && qnhInHg >= 20f && qnhInHg <= 35f
                ? qnhInHg.ToString("0.00", CultureInfo.InvariantCulture)
                : "--";
        }

        public static string FormatPrecipitation(float ratio)
        {
            return IsFinite(ratio)
                ? Mathf.RoundToInt(Mathf.Clamp01(ratio) * 100f).ToString(CultureInfo.InvariantCulture) + "%"
                : "--";
        }

        private static Color TemperatureColor(float temperatureC)
        {
            if (!IsFinite(temperatureC)) return LabelColor;
            if (temperatureC <= 0f) return ValueCool;
            if (temperatureC >= 35f) return ValueCaution;
            return ValueNormal;
        }

        private static Color WindColor(float windSpeedKnots)
        {
            if (!IsFinite(windSpeedKnots)) return LabelColor;
            if (windSpeedKnots >= 50f) return ValueWarning;
            if (windSpeedKnots >= 30f) return ValueCaution;
            return ValueNormal;
        }

        private static Color VisibilityColor(float visibilityMeters)
        {
            if (!IsFinite(visibilityMeters)) return LabelColor;
            float statuteMiles = visibilityMeters / MetersPerStatuteMile;
            if (statuteMiles < 3f) return ValueWarning;
            if (statuteMiles < 5f) return ValueCaution;
            return ValueNormal;
        }

        private static Color PrecipitationColor(float ratio)
        {
            if (!IsFinite(ratio)) return LabelColor;
            if (ratio >= 0.7f) return ValueWarning;
            if (ratio >= 0.25f) return ValueCaution;
            return ValueNormal;
        }

        private static float MaxNormalizedValue(IDictionary<string, float> values, params string[] keys)
        {
            float maximum = float.NaN;
            for (int i = 0; i < keys.Length; i++)
            {
                if (values.TryGetValue(keys[i], out float value) && IsFinite(value))
                {
                    value = value > 1f ? value / 100f : value;
                    maximum = !IsFinite(maximum) ? value : Mathf.Max(maximum, value);
                }
            }

            return IsFinite(maximum) ? Mathf.Clamp01(maximum) : float.NaN;
        }

        private static float GetAny(IDictionary<string, float> values, float fallback, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (values.TryGetValue(keys[i], out float value) && IsFinite(value))
                {
                    return value;
                }
            }

            return fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
