using System.Collections.Generic;
using AircraftControl.Core;
using AviationUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization
{
    [ExecuteAlways]
    [DefaultExecutionOrder(9950)]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("FAA/Customization/FAA Heading Tape Overlay")]
    public class FaaHeadingTapeOverlay : MonoBehaviour
    {
        private const int MarkerCount = 73;
        private const float MarkerSpacingDegrees = 5f;
        private const float DefaultPixelsPerDegree = 1.5f;
        private static readonly Vector2 DefaultClipAnchoredPosition = new Vector2(0f, -5f);

        [Header("Data Sources")]
        [SerializeField] private AviationFlightDataProvider flightDataProvider;
        [SerializeField] private AircraftController aircraftController;
        [SerializeField] private global::HeadingHUD headingHud;
        [SerializeField] private Transform headingTarget;
        [SerializeField] private bool autoFindSources = true;

        [Header("Layout")]
        [SerializeField, HideInInspector] private Vector2 anchoredPosition = new Vector2(-610f, 430f);
        [SerializeField, HideInInspector] private Vector2 size = new Vector2(600f, 38f);
        [SerializeField, HideInInspector] private Vector2 clipAnchoredPosition = new Vector2(0f, -5f);
        [SerializeField] private float pixelsPerDegree = DefaultPixelsPerDegree;
        [SerializeField] private float smoothing = 0.18f;

        [Header("Style")]
        [SerializeField] private Color hudColor = new Color(0.2f, 1f, 0.2f, 1f);
        [SerializeField] private Color hudDimColor = new Color(0.2f, 1f, 0.2f, 0.74f);

        private readonly List<CompassMarker> _markers = new List<CompassMarker>(MarkerCount);
        private readonly List<TMP_Text> _fixedCardinalLabels = new List<TMP_Text>(4);
        private RectTransform _rectTransform;
        private RectTransform _clipRect;
        private Image _baseline;
        private Image _topRule;
        private Image _centerTick;
        private TMP_Text _headingReadout;
        private float _displayedHeading;

        public void Configure(Vector2 overlayAnchoredPosition, Vector2 overlaySize, Color primaryColor, Color dimColor)
        {
            anchoredPosition = overlayAnchoredPosition;
            size = overlaySize;
            hudColor = primaryColor;
            hudDimColor = dimColor;
            pixelsPerDegree = DefaultPixelsPerDegree;
            EnsureBuilt();
            ApplyLayoutAndStyle();
            UpdateTape(true);
        }

        public void SetDataSources(
            AviationFlightDataProvider provider,
            AircraftController controller,
            Transform fallbackHeadingTarget)
        {
            flightDataProvider = provider;
            aircraftController = controller;
            if (fallbackHeadingTarget != null)
            {
                headingTarget = fallbackHeadingTarget;
            }
        }

        private void Awake()
        {
            CaptureEditorLayout();
            EnsureBuilt();
            RefreshDataSources();
            _displayedHeading = ReadHeading();
            UpdateTape(true);
        }

        private void OnEnable()
        {
            CaptureEditorLayout();
            EnsureBuilt();
            RefreshDataSources();
            _displayedHeading = ReadHeading();
            UpdateTape(true);
        }

        private void OnValidate()
        {
            CaptureEditorLayout();
            size.x = Mathf.Max(260f, size.x);
            size.y = Mathf.Max(34f, size.y);
            pixelsPerDegree = Mathf.Clamp(pixelsPerDegree, 1.1f, GetMaximumPixelsPerDegree(size.x));
            smoothing = Mathf.Clamp01(smoothing);
            EnsureBuilt();
            ApplyLayoutAndStyle();
            UpdateTape(true);
        }

        private void Update()
        {
            CaptureEditorLayout();

            if (autoFindSources && (flightDataProvider == null || aircraftController == null || headingTarget == null))
            {
                RefreshDataSources();
            }

            UpdateTape(false);
        }

        private void CaptureEditorLayout()
        {
            if (Application.isPlaying)
            {
                return;
            }

            // Once the overlay has been generated, its RectTransforms are the source of
            // truth in edit mode. This keeps ExecuteAlways from undoing Scene view and
            // Inspector edits on every update.
            Transform existingClip = transform.Find("Heading Tape Clip");
            if (existingClip == null)
            {
                return;
            }

            _rectTransform = GetComponent<RectTransform>();
            _clipRect = existingClip as RectTransform;
            bool changed = false;

            if (_rectTransform != null)
            {
                Vector2 currentPosition = _rectTransform.anchoredPosition;
                Vector2 currentSize = _rectTransform.sizeDelta;
                if (!Approximately(anchoredPosition, currentPosition))
                {
                    anchoredPosition = currentPosition;
                    changed = true;
                }

                if (!Approximately(size, currentSize))
                {
                    size = currentSize;
                    changed = true;
                }
            }

            if (_clipRect != null)
            {
                Vector2 currentClipPosition = _clipRect.anchoredPosition;
                if (!IsReasonableClipOffset(currentClipPosition, size))
                {
                    // The clip is an internal scrolling viewport. Moving it hundreds of
                    // pixels away from the overlay detaches the compass labels/ticks from
                    // the heading index, which is easy to do accidentally in Scene view.
                    // Keep legitimate small authoring offsets, but repair detached clips.
                    clipAnchoredPosition = DefaultClipAnchoredPosition;
                    _clipRect.anchoredPosition = DefaultClipAnchoredPosition;
                    changed = true;
                }
                else if (!Approximately(clipAnchoredPosition, currentClipPosition))
                {
                    clipAnchoredPosition = currentClipPosition;
                    changed = true;
                }
            }

#if UNITY_EDITOR
            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }

        private void EnsureBuilt()
        {
            _rectTransform = GetComponent<RectTransform>();
            ApplyRootLayout();

            RectTransform clip = GetOrCreateRectChild(transform, "Heading Tape Clip");
            _clipRect = clip;
            if (clip.GetComponent<RectMask2D>() == null)
            {
                clip.gameObject.AddComponent<RectMask2D>();
            }

            _baseline = GetOrCreateImageChild(clip, "Heading Tape Baseline");
            _topRule = GetOrCreateImageChild(transform, "Heading Tape Top Rule");
            _centerTick = GetOrCreateImageChild(transform, "Current Heading Index");
            _headingReadout = GetOrCreateTextChild(transform, "Current Heading Readout");
            EnsureFixedCardinalLabels();

            while (_markers.Count < MarkerCount)
            {
                int index = _markers.Count;
                RectTransform markerRoot = GetOrCreateRectChild(clip, $"Heading Tape Marker {index:00}");
                Image tick = GetOrCreateImageChild(markerRoot, "Tick");
                TMP_Text label = GetOrCreateTextChild(markerRoot, "Label");
                _markers.Add(new CompassMarker(markerRoot, tick, label));
            }

            ApplyLayoutAndStyle();
        }

        private void ApplyRootLayout()
        {
            if (_rectTransform == null)
            {
                return;
            }

            _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.anchoredPosition = anchoredPosition;
            _rectTransform.sizeDelta = size;
            _rectTransform.localScale = Vector3.one;
            _rectTransform.localRotation = Quaternion.identity;
            gameObject.SetActive(true);
        }

        private void ApplyLayoutAndStyle()
        {
            ApplyRootLayout();

            if (_clipRect != null)
            {
                if (!IsReasonableClipOffset(clipAnchoredPosition, size))
                {
                    clipAnchoredPosition = DefaultClipAnchoredPosition;
                }

                _clipRect.anchorMin = new Vector2(0.5f, 0.5f);
                _clipRect.anchorMax = new Vector2(0.5f, 0.5f);
                _clipRect.pivot = new Vector2(0.5f, 0.5f);
                _clipRect.anchoredPosition = clipAnchoredPosition;
                _clipRect.sizeDelta = new Vector2(size.x, Mathf.Max(34f, size.y));
                _clipRect.localScale = Vector3.one;
                _clipRect.localRotation = Quaternion.identity;
            }

            ConfigureLine(_baseline, new Vector2(size.x - 32f, 2f), new Vector2(0f, -11f), hudDimColor);
            if (_topRule != null)
            {
                _topRule.gameObject.SetActive(false);
            }
            ConfigureLine(_centerTick, new Vector2(2.4f, 28f), new Vector2(0f, -6f), hudColor);
            ApplyFixedCardinalLabelLayout();

            if (_headingReadout != null)
            {
                DisableTextGraphic(_headingReadout);
            }

            foreach (CompassMarker marker in _markers)
            {
                if (marker.Root == null)
                {
                    continue;
                }

                marker.Root.anchorMin = new Vector2(0.5f, 0.5f);
                marker.Root.anchorMax = new Vector2(0.5f, 0.5f);
                marker.Root.pivot = new Vector2(0.5f, 0.5f);
                marker.Root.sizeDelta = new Vector2(54f, 30f);
                marker.Root.localScale = Vector3.one;
                marker.Root.localRotation = Quaternion.identity;

                if (marker.Tick != null)
                {
                    marker.Tick.raycastTarget = false;
                }

                if (marker.Label != null)
                {
                    marker.Label.alignment = TextAlignmentOptions.Center;
                    marker.Label.fontStyle = FontStyles.Bold;
                    marker.Label.raycastTarget = false;
                    ApplyTextColor(marker.Label, hudColor);
                }
            }
        }

        private void EnsureFixedCardinalLabels()
        {
            string[] names = { "Fixed Cardinal W", "Fixed Cardinal N", "Fixed Cardinal E", "Fixed Cardinal S" };
            while (_fixedCardinalLabels.Count < names.Length)
            {
                TMP_Text label = GetOrCreateTextChild(transform, names[_fixedCardinalLabels.Count]);
                _fixedCardinalLabels.Add(label);
            }
        }

        private void ApplyFixedCardinalLabelLayout()
        {
            if (_fixedCardinalLabels.Count < 4)
            {
                return;
            }

            for (int i = 0; i < _fixedCardinalLabels.Count; i++)
            {
                TMP_Text label = _fixedCardinalLabels[i];
                if (label == null)
                {
                    continue;
                }

                DisableTextGraphic(label);
            }
        }

        private void UpdateTape(bool immediate)
        {
            EnsureBuilt();

            float targetHeading = ReadHeading();
            if (immediate || smoothing <= 0f)
            {
                _displayedHeading = targetHeading;
            }
            else
            {
                float lerpFactor = 1f - Mathf.Pow(1f - smoothing, Time.unscaledDeltaTime * 60f);
                _displayedHeading = Mathf.LerpAngle(_displayedHeading, targetHeading, lerpFactor);
            }

            _displayedHeading = Normalize360(_displayedHeading);
            if (_headingReadout != null)
            {
                DisableTextGraphic(_headingReadout);
            }
            ApplyFixedCardinalLabelLayout();

            int centerStep = Mathf.RoundToInt(_displayedHeading / MarkerSpacingDegrees);
            int halfCount = MarkerCount / 2;
            float halfWidth = size.x * 0.5f;
            // Keep the opposite cardinal fully inside the mask. A wider scale puts
            // S at the exact edge when heading north (and likewise for N/E/W), so
            // the label is clipped even though its tick remains visible.
            float effectivePixelsPerDegree = Mathf.Min(pixelsPerDegree, GetMaximumPixelsPerDegree(size.x));

            for (int i = 0; i < _markers.Count; i++)
            {
                CompassMarker marker = _markers[i];
                if (marker.Root == null)
                {
                    continue;
                }

                int step = centerStep + i - halfCount;
                int markerDegrees = Mathf.RoundToInt(step * MarkerSpacingDegrees);
                int normalizedDegrees = Mathf.RoundToInt(Normalize360(markerDegrees)) % 360;
                float delta = Mathf.DeltaAngle(_displayedHeading, markerDegrees);
                float x = delta * effectivePixelsPerDegree;
                bool visible = Mathf.Abs(x) <= halfWidth + 28f;
                marker.Root.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                bool cardinal = IsCardinal(normalizedDegrees);
                bool major = normalizedDegrees % 10 == 0;
                bool labeled = cardinal || normalizedDegrees % 30 == 0;
                float tickHeight = cardinal ? 15f : major ? 10f : 6f;
                float tickWidth = cardinal || major ? 2f : 1.25f;

                marker.Root.anchoredPosition = new Vector2(x, 0f);
                ConfigureLine(marker.Tick, new Vector2(tickWidth, tickHeight), new Vector2(0f, -10f), major ? hudColor : hudDimColor);

                if (marker.Label != null)
                {
                    marker.Label.gameObject.SetActive(labeled);
                    if (labeled)
                    {
                        EnableTextGraphic(marker.Label);
                        marker.Label.text = GetLabel(normalizedDegrees);
                        marker.Label.fontSize = cardinal ? 20f : 13f;
                        marker.Label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                        marker.Label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                        marker.Label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                        marker.Label.rectTransform.anchoredPosition = new Vector2(0f, 7f);
                        marker.Label.rectTransform.sizeDelta = new Vector2(cardinal ? 52f : 42f, 24f);
                        ApplyTextColor(marker.Label, cardinal ? hudColor : hudDimColor);
                    }
                }
            }
        }

        private void RefreshDataSources()
        {
            if (!autoFindSources)
            {
                return;
            }

            if (flightDataProvider == null)
            {
                flightDataProvider = FindAnyObjectByType<AviationFlightDataProvider>(FindObjectsInactive.Include);
            }

            if (aircraftController == null)
            {
                aircraftController = FindAnyObjectByType<AircraftController>(FindObjectsInactive.Include);
            }

            if (headingHud == null)
            {
                headingHud = FindAnyObjectByType<global::HeadingHUD>(FindObjectsInactive.Include);
            }

            if (headingTarget == null && aircraftController != null)
            {
                headingTarget = aircraftController.transform;
            }

            if (headingTarget == null && Camera.main != null)
            {
                headingTarget = Camera.main.transform;
            }
        }

        private float ReadHeading()
        {
            if (aircraftController != null && aircraftController.State != null)
            {
                return Normalize360(aircraftController.State.Heading);
            }

            if (headingHud != null)
            {
                return Normalize360(headingHud.GetCurrentHeading());
            }

            if (flightDataProvider != null && flightDataProvider.FlightData != null)
            {
                return Normalize360(flightDataProvider.FlightData.heading);
            }

            if (headingTarget != null)
            {
                Vector3 forward = headingTarget.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                {
                    return Normalize360(Quaternion.LookRotation(forward).eulerAngles.y);
                }
            }

            return Normalize360(_displayedHeading);
        }

        private static RectTransform GetOrCreateRectChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            child.SetActive(true);

            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = child.AddComponent<RectTransform>();
            }

            return rect;
        }

        private static Image GetOrCreateImageChild(Transform parent, string childName)
        {
            RectTransform rect = GetOrCreateRectChild(parent, childName);
            Image image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
            }

            image.sprite = null;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            image.enabled = true;
            return image;
        }

        private static TMP_Text GetOrCreateTextChild(Transform parent, string childName)
        {
            RectTransform rect = GetOrCreateRectChild(parent, childName);
            TextMeshProUGUI text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            text.raycastTarget = false;
            text.enabled = true;
            if (text.font == null && TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private static void DisableTextGraphic(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.text = string.Empty;
            text.enabled = false;
            text.raycastTarget = false;
            text.canvasRenderer.SetAlpha(0f);
            text.canvasRenderer.Clear();
            text.ForceMeshUpdate(true, true);
            text.gameObject.SetActive(false);
        }

        private static void EnableTextGraphic(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.gameObject.SetActive(true);
            text.enabled = true;
            text.raycastTarget = false;
            text.canvasRenderer.cull = false;
            text.canvasRenderer.SetAlpha(1f);
        }

        private static void ConfigureLine(Image image, Vector2 lineSize, Vector2 anchored, Color color)
        {
            if (image == null)
            {
                return;
            }

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = lineSize;
            rect.anchoredPosition = anchored;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            image.color = color;
            image.raycastTarget = false;
            image.enabled = true;
        }

        private static void ApplyTextColor(TMP_Text text, Color color)
        {
            if (text == null)
            {
                return;
            }

            if (text.font == null && TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            text.color = color;
            if (text.fontSharedMaterial != null)
            {
                text.faceColor = color;
                text.outlineColor = new Color(color.r, color.g, color.b, Mathf.Min(color.a, 0.62f));
            }

            text.enableVertexGradient = false;
            text.raycastTarget = false;
            text.canvasRenderer.SetColor(color);
            text.ForceMeshUpdate(true, true);
        }

        private static bool IsCardinal(int degrees)
        {
            return degrees == 0 || degrees == 90 || degrees == 180 || degrees == 270;
        }

        private static string GetLabel(int degrees)
        {
            switch (degrees)
            {
                case 0:
                    return "N";
                case 90:
                    return "E";
                case 180:
                    return "S";
                case 270:
                    return "W";
                default:
                    return (degrees / 10).ToString("00");
            }
        }

        private static float Normalize360(float degrees)
        {
            degrees %= 360f;
            if (degrees < 0f)
            {
                degrees += 360f;
            }

            return degrees;
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return (left - right).sqrMagnitude < 0.0001f;
        }

        private static bool IsReasonableClipOffset(Vector2 offset, Vector2 overlaySize)
        {
            float maxHorizontalOffset = Mathf.Max(32f, Mathf.Abs(overlaySize.x) * 0.25f);
            float maxVerticalOffset = Mathf.Max(24f, Mathf.Abs(overlaySize.y));
            return Mathf.Abs(offset.x) <= maxHorizontalOffset && Mathf.Abs(offset.y) <= maxVerticalOffset;
        }

        private static float GetMaximumPixelsPerDegree(float overlayWidth)
        {
            const float cardinalLabelWidth = 52f;
            return Mathf.Max(1.1f, (Mathf.Abs(overlayWidth) - cardinalLabelWidth) / 360f);
        }

        private readonly struct CompassMarker
        {
            public CompassMarker(RectTransform root, Image tick, TMP_Text label)
            {
                Root = root;
                Tick = tick;
                Label = label;
            }

            public RectTransform Root { get; }
            public Image Tick { get; }
            public TMP_Text Label { get; }
        }
    }
}
