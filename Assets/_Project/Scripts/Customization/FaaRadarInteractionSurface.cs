using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FAA.Customization
{
    public enum FaaRadarKind
    {
        Weather,
        Traffic
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    [AddComponentMenu("FAA/Customization/FAA Radar Configuration Drawer")]
    public sealed class FaaRadarConfigurationDrawer : MonoBehaviour
    {
        private const float OpenDuration = 0.24f;
        private const float CloseDuration = 0.16f;
        private const float HiddenScale = 0.94f;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private bool _targetVisible;
        private bool _reducedMotion;
        private bool _initialized;
        private float _progress;

        public bool TargetVisible => _targetVisible;
        public float Progress => _progress;

        public void Configure(bool reducedMotion)
        {
            _reducedMotion = reducedMotion;
            EnsureReferences();
            if (!_initialized)
            {
                _initialized = true;
                Snap(false);
            }
        }

        public void SetVisible(bool visible, bool immediate = false)
        {
            EnsureReferences();
            if (!_initialized)
            {
                _initialized = true;
                Snap(false);
            }

            if (_targetVisible == visible && !immediate)
            {
                if (!Mathf.Approximately(_progress, visible ? 1f : 0f))
                {
                    enabled = true;
                }

                return;
            }

            _targetVisible = visible;
            if (immediate)
            {
                Snap(visible);
                return;
            }

            enabled = true;
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void Update()
        {
            float target = _targetVisible ? 1f : 0f;
            float duration = _targetVisible ? OpenDuration : CloseDuration;
            if (_reducedMotion)
            {
                duration = Mathf.Min(duration, 0.08f);
            }

            _progress = Mathf.MoveTowards(_progress, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
            Apply(_progress);
            if (Mathf.Approximately(_progress, target))
            {
                enabled = false;
            }
        }

        private void EnsureReferences()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }

        private void Snap(bool visible)
        {
            _targetVisible = visible;
            _progress = visible ? 1f : 0f;
            Apply(_progress);
            enabled = false;
        }

        private void Apply(float progress)
        {
            if (_canvasGroup == null || _rectTransform == null)
            {
                return;
            }

            float eased = EaseOutQuart(Mathf.Clamp01(progress));
            _canvasGroup.alpha = eased;
            _canvasGroup.interactable = _targetVisible && progress >= 0.98f;
            _canvasGroup.blocksRaycasts = _targetVisible && progress >= 0.12f;
            float scale = _reducedMotion ? 1f : Mathf.Lerp(HiddenScale, 1f, eased);
            _rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        public static float EaseOutQuart(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse * inverse;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))]
    [AddComponentMenu("FAA/Customization/FAA Radar Interaction Surface")]
    public sealed class FaaRadarInteractionSurface : MonoBehaviour,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IScrollHandler
    {
        public const string WeatherObjectName = "FAAWeatherRadarInteractionSurface";
        public const string TrafficObjectName = "FAATrafficRadarInteractionSurface";

        private static readonly Color WeatherAccent = new Color(0.20f, 1f, 0.45f, 1f);
        private static readonly Color TrafficAccent = new Color(0.20f, 0.92f, 0.88f, 1f);

        [SerializeField] private FaaRadarControlsOverlay owner;
        [SerializeField] private FaaRadarKind radarKind;
        [SerializeField] private bool reducedMotion;

        private RectTransform _rectTransform;
        private CanvasGroup _focusGroup;
        private RectTransform _focusFrame;
        private TMP_Text _hintText;
        private bool _hovered;
        private bool _pressed;
        private bool _open;
        private bool _interactionEnabled = true;
        private float _visualProgress;

        public FaaRadarKind RadarKind => radarKind;
        public bool IsOpen => _open;

        public void Configure(FaaRadarControlsOverlay sourceOwner, FaaRadarKind kind, bool useReducedMotion)
        {
            owner = sourceOwner;
            radarKind = kind;
            reducedMotion = useReducedMotion;
            EnsureVisualTree();
            UpdateHint();
        }

        public void SetOpen(bool open)
        {
            _open = open;
            UpdateHint();
        }

        public void SetInteractionEnabled(bool value)
        {
            _interactionEnabled = value;
            Image hitArea = GetComponent<Image>();
            if (hitArea != null)
            {
                hitArea.raycastTarget = value;
            }

            if (!value)
            {
                _hovered = false;
                _pressed = false;
                _open = false;
            }

            enabled = true;
        }

        private void Awake()
        {
            EnsureVisualTree();
        }

        private void Update()
        {
            // A faint always-on edge reads as a lightweight glass instrument;
            // hover/open states strengthen the same frame without adding a box.
            float target = !_interactionEnabled ? 0f : _open ? 1f : _hovered ? 0.62f : 0.06f;
            float duration = reducedMotion ? 0.08f : target > _visualProgress ? 0.22f : 0.14f;
            _visualProgress = Mathf.MoveTowards(
                _visualProgress,
                target,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));

            if (_focusGroup != null)
            {
                _focusGroup.alpha = FaaRadarConfigurationDrawer.EaseOutQuart(_visualProgress);
            }

            if (_focusFrame != null)
            {
                float pressScale = _pressed && !reducedMotion ? 0.985f : 1f;
                float revealScale = reducedMotion ? 1f : Mathf.Lerp(0.985f, 1f, _visualProgress);
                _focusFrame.localScale = new Vector3(revealScale * pressScale, revealScale * pressScale, 1f);
            }

        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_interactionEnabled || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            owner?.ToggleRadarConfiguration(radarKind);
            eventData.Use();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_interactionEnabled)
            {
                return;
            }

            _hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_interactionEnabled && eventData.button == PointerEventData.InputButton.Left)
            {
                _pressed = true;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!_interactionEnabled || Mathf.Approximately(eventData.scrollDelta.y, 0f))
            {
                return;
            }

            owner?.AdjustRadarSize(radarKind, eventData.scrollDelta.y);
            eventData.Use();
        }

        private void EnsureVisualTree()
        {
            _rectTransform = GetComponent<RectTransform>();
            Image hitArea = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0.002f);
            hitArea.raycastTarget = _interactionEnabled;

            Transform existingFrame = transform.Find("RadarFocusFrame");
            GameObject frameObject = existingFrame != null
                ? existingFrame.gameObject
                : new GameObject("RadarFocusFrame", typeof(RectTransform), typeof(CanvasGroup));
            frameObject.transform.SetParent(transform, false);
            _focusFrame = frameObject.GetComponent<RectTransform>();
            Stretch(_focusFrame);
            _focusGroup = frameObject.GetComponent<CanvasGroup>() ?? frameObject.AddComponent<CanvasGroup>();
            _focusGroup.alpha = 0f;
            _focusGroup.blocksRaycasts = false;
            _focusGroup.interactable = false;

            Color accent = radarKind == FaaRadarKind.Weather ? WeatherAccent : TrafficAccent;
            EnsureEdge(_focusFrame, "TopEdge", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -2f), new Vector2(0f, 0f), accent);
            EnsureEdge(_focusFrame, "BottomEdge", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 2f), accent);
            EnsureEdge(_focusFrame, "LeftEdge", new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(2f, 0f), accent);
            EnsureEdge(_focusFrame, "RightEdge", new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-2f, 0f), new Vector2(0f, 0f), accent);

            Transform existingHint = _focusFrame.Find("ConfigureHint");
            GameObject hintObject = existingHint != null
                ? existingHint.gameObject
                : new GameObject("ConfigureHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hintObject.transform.SetParent(_focusFrame, false);
            // The animated outline is the interaction affordance. Keeping a rectangular
            // hint over the open radar obscures its mode and bearing symbology, while the
            // newly revealed configuration/conditions panels already communicate state.
            hintObject.SetActive(false);
            RectTransform hintRect = hintObject.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 1f);
            hintRect.anchorMax = new Vector2(0.5f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0f, -8f);
            hintRect.sizeDelta = new Vector2(188f, 34f);
            Image hintBackground = hintObject.GetComponent<Image>() ?? hintObject.AddComponent<Image>();
            hintBackground.color = new Color(0.004f, 0.035f, 0.026f, 0.96f);
            hintBackground.raycastTarget = false;
            Outline outline = hintObject.GetComponent<Outline>() ?? hintObject.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.88f);
            outline.effectDistance = new Vector2(1f, -1f);

            Transform existingText = hintRect.Find("Label");
            GameObject textObject = existingText != null
                ? existingText.gameObject
                : new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            textObject.transform.SetParent(hintRect, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            Stretch(textRect);
            _hintText = textObject.GetComponent<TextMeshProUGUI>() ?? textObject.AddComponent<TextMeshProUGUI>();
            _hintText.fontSize = 14f;
            _hintText.fontStyle = FontStyles.Bold;
            _hintText.enableAutoSizing = false;
            _hintText.extraPadding = true;
            _hintText.alignment = TextAlignmentOptions.Center;
            _hintText.textWrappingMode = TextWrappingModes.NoWrap;
            _hintText.color = new Color(0.82f, 1f, 0.87f, 1f);
            _hintText.raycastTarget = false;
            UpdateHint();
        }

        private void UpdateHint()
        {
            if (_hintText == null)
            {
                return;
            }

            if (radarKind == FaaRadarKind.Weather)
            {
                _hintText.text = _open ? "WX CONDITIONS OPEN" : "CLICK · WX CONDITIONS";
                return;
            }

            _hintText.text = _open ? "TRAFFIC CONFIG ACTIVE" : "CLICK · CONFIGURE TRAFFIC";
        }

        private static void EnsureEdge(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color color)
        {
            Transform existing = parent.Find(name);
            GameObject edgeObject = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            edgeObject.transform.SetParent(parent, false);
            RectTransform edge = edgeObject.GetComponent<RectTransform>();
            edge.anchorMin = anchorMin;
            edge.anchorMax = anchorMax;
            edge.offsetMin = offsetMin;
            edge.offsetMax = offsetMax;
            Image image = edgeObject.GetComponent<Image>() ?? edgeObject.AddComponent<Image>();
            image.color = new Color(color.r, color.g, color.b, 0.82f);
            image.raycastTarget = false;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }
    }
}
