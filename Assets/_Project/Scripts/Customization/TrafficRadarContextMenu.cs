using System.Collections;
using System.Collections.Generic;
using TMPro;
using TrafficRadar;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization
{
    /// <summary>
    /// A transient, one-tap traffic-radar menu. Each action is connected to
    /// the part of the scope it affects so pilots can understand the result
    /// before committing to it. The menu is built at runtime and follows the
    /// same radar object through compact and pilot-focus layouts.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("FAA/Customization/Traffic Radar Context Menu")]
    public sealed class TrafficRadarContextMenu : MonoBehaviour
    {
        private const float CompactPanelWidth = 188f;
        private const float FocusPanelWidth = 244f;
        private const float CompactRowHeight = 40f;
        private const float FocusRowHeight = 50f;
        private const float RowGap = 7f;
        private const float HeaderHeight = 48f;
        private const float PanelPadding = 12f;
        private const float RadarGap = 16f;

        private static readonly Color Accent = new Color(0.25f, 0.96f, 0.91f, 1f);
        private static readonly Color PanelColor = new Color(0.012f, 0.052f, 0.064f, 0.965f);
        private static readonly Color ButtonColor = new Color(0.018f, 0.105f, 0.115f, 0.94f);
        private static readonly Color ButtonPressColor = new Color(0.02f, 0.28f, 0.25f, 1f);
        private static readonly Color TextColor = new Color(0.84f, 1f, 0.96f, 1f);
        private static readonly Color StateColor = new Color(0.50f, 0.88f, 0.82f, 1f);

        private enum ActionKind
        {
            Linework,
            Map,
            Range,
            Center,
            View
        }

        private sealed class ActionView
        {
            public ActionKind Kind;
            public RectTransform Rect;
            public Image Background;
            public TMP_Text Title;
            public TMP_Text State;
            public GameObject GameObject;
        }

        [SerializeField] private bool reducedMotion;

        private readonly List<ActionView> _actions = new List<ActionView>();
        private RectTransform _hostRect;
        private RectTransform _visualRoot;
        private RectTransform _panel;
        private CanvasGroup _canvasGroup;
        private RadarContextLeaderGraphic _leaders;
        private TMP_Text _headerTitle;
        private TMP_Text _headerHint;
        private TrafficRadarDisplay _display;
        private Canvas _canvas;
        private bool _targetOpen;
        private bool _panelOnLeft;
        private bool _interactionLocked;
        private float _progress;
        private Vector2 _panelRestPosition;
        private Coroutine _actionRoutine;
        private ActionKind? _selectedAction;

        public bool IsOpen => _targetOpen || _progress > 0.01f;

        public void Configure(TrafficRadarDisplay display, bool useReducedMotion)
        {
            _display = display;
            reducedMotion = useReducedMotion;
            EnsureVisualTree();
            RefreshActionLabels();
        }

        public void ToggleAtScreenPoint(Vector2 screenPoint, Camera eventCamera)
        {
            EnsureVisualTree();
            if (_targetOpen)
            {
                Close();
                return;
            }

            Vector2 localPoint = Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _hostRect,
                screenPoint,
                eventCamera,
                out localPoint);
            OpenAtLocalPoint(localPoint);
        }

        /// <summary>
        /// Public local-space entry point used by XR adapters and live-editor
        /// verification without synthesizing a mouse device.
        /// </summary>
        public void OpenAtLocalPoint(Vector2 localPoint)
        {
            if (_display == null)
            {
                _display = GetComponentInParent<TrafficRadarDisplay>();
                if (_display == null && transform.parent != null)
                {
                    _display = transform.parent.GetComponentInChildren<TrafficRadarDisplay>(true);
                }
            }

            if (_display == null)
            {
                return;
            }

            EnsureVisualTree();
            _visualRoot.gameObject.SetActive(true);
            _interactionLocked = false;
            _selectedAction = null;
            RefreshActionLabels();
            LayoutForCurrentRadar(localPoint);
            _targetOpen = true;
            enabled = true;
        }

        public void Close(bool immediate = false)
        {
            _targetOpen = false;
            _interactionLocked = false;
            _selectedAction = null;
            if (_actionRoutine != null)
            {
                StopCoroutine(_actionRoutine);
                _actionRoutine = null;
            }

            if (immediate)
            {
                _progress = 0f;
                ApplyVisualState();
                if (_visualRoot != null)
                {
                    _visualRoot.gameObject.SetActive(false);
                }
                enabled = false;
                return;
            }

            enabled = true;
        }

        private void Awake()
        {
            EnsureVisualTree();
            Close(true);
        }

        private void OnDisable()
        {
            if (_visualRoot != null && !gameObject.activeInHierarchy)
            {
                _visualRoot.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            float target = _targetOpen ? 1f : 0f;
            float duration = reducedMotion
                ? 0.07f
                : target > _progress ? 0.22f : 0.15f;
            _progress = Mathf.MoveTowards(
                _progress,
                target,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
            ApplyVisualState();

            if (_targetOpen)
            {
                RefreshActionLabels();
                UpdateLeaderGeometry();
            }

            if (!_targetOpen && _progress <= 0.001f && _actionRoutine == null)
            {
                _visualRoot.gameObject.SetActive(false);
                enabled = false;
            }
        }

        private void EnsureVisualTree()
        {
            _hostRect = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();

            Transform existingRoot = transform.Find("TrafficRadarQuickMenu");
            GameObject rootObject = existingRoot != null
                ? existingRoot.gameObject
                : new GameObject("TrafficRadarQuickMenu", typeof(RectTransform), typeof(CanvasGroup));
            rootObject.transform.SetParent(transform, false);
            _visualRoot = rootObject.GetComponent<RectTransform>();
            Stretch(_visualRoot);
            _visualRoot.SetAsLastSibling();
            _canvasGroup = rootObject.GetComponent<CanvasGroup>() ?? rootObject.AddComponent<CanvasGroup>();

            Transform existingLeaders = _visualRoot.Find("ActionLeaders");
            GameObject leaderObject = existingLeaders != null
                ? existingLeaders.gameObject
                : new GameObject("ActionLeaders", typeof(RectTransform), typeof(CanvasRenderer));
            leaderObject.transform.SetParent(_visualRoot, false);
            RectTransform leaderRect = leaderObject.GetComponent<RectTransform>();
            Stretch(leaderRect);
            _leaders = leaderObject.GetComponent<RadarContextLeaderGraphic>() ??
                       leaderObject.AddComponent<RadarContextLeaderGraphic>();
            _leaders.raycastTarget = false;
            _leaders.color = Color.white;

            Transform existingPanel = _visualRoot.Find("ActionPanel");
            GameObject panelObject = existingPanel != null
                ? existingPanel.gameObject
                : new GameObject("ActionPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.transform.SetParent(_visualRoot, false);
            _panel = panelObject.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            Image panelImage = panelObject.GetComponent<Image>() ?? panelObject.AddComponent<Image>();
            panelImage.color = PanelColor;
            panelImage.raycastTarget = true;
            Outline outline = panelObject.GetComponent<Outline>() ?? panelObject.AddComponent<Outline>();
            outline.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.58f);
            outline.effectDistance = new Vector2(1f, -1f);
            Shadow shadow = null;
            foreach (Shadow candidate in panelObject.GetComponents<Shadow>())
            {
                // Outline derives from Shadow. Require an exact Shadow here so
                // the drop shadow and cyan one-pixel outline keep independent
                // colors/distances instead of overwriting one another.
                if (candidate != null && candidate.GetType() == typeof(Shadow))
                {
                    shadow = candidate;
                    break;
                }
            }

            if (shadow == null)
            {
                shadow = panelObject.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(0f, 0.015f, 0.02f, 0.72f);
            shadow.effectDistance = new Vector2(8f, -10f);

            EnsureHeader();
            EnsureActions();
            _panel.SetAsLastSibling();
        }

        private void EnsureHeader()
        {
            RectTransform titleRect = EnsureTextRect(_panel, "Title", out TMP_Text title);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(PanelPadding, -HeaderHeight + 4f);
            titleRect.offsetMax = new Vector2(-PanelPadding, -5f);
            title.text = "TRAFFIC · QUICK ACTIONS";
            title.fontSize = 13f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.TopLeft;
            title.color = TextColor;
            _headerTitle = title;

            RectTransform hintRect = EnsureTextRect(_panel, "Hint", out TMP_Text hint);
            hintRect.anchorMin = new Vector2(0f, 1f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.offsetMin = new Vector2(PanelPadding, -HeaderHeight + 2f);
            hintRect.offsetMax = new Vector2(-PanelPadding, -23f);
            hint.text = "SELECT ONCE · MENU AUTO-CLOSES";
            hint.fontSize = 9.5f;
            hint.fontStyle = FontStyles.Normal;
            hint.alignment = TextAlignmentOptions.BottomLeft;
            hint.color = StateColor;
            _headerHint = hint;
        }

        private void EnsureActions()
        {
            _actions.Clear();
            EnsureAction(ActionKind.Linework, "ActionLinework");
            EnsureAction(ActionKind.Map, "ActionMap");
            EnsureAction(ActionKind.Range, "ActionRange");
            EnsureAction(ActionKind.Center, "ActionCenter");
            EnsureAction(ActionKind.View, "ActionView");
        }

        private void EnsureAction(ActionKind kind, string objectName)
        {
            Transform existing = _panel.Find(objectName);
            GameObject actionObject = existing != null
                ? existing.gameObject
                : new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            actionObject.transform.SetParent(_panel, false);
            RectTransform actionRect = actionObject.GetComponent<RectTransform>();
            actionRect.anchorMin = new Vector2(0.5f, 1f);
            actionRect.anchorMax = new Vector2(0.5f, 1f);
            actionRect.pivot = new Vector2(0.5f, 0.5f);

            Image background = actionObject.GetComponent<Image>() ?? actionObject.AddComponent<Image>();
            background.color = ButtonColor;
            Button button = actionObject.GetComponent<Button>() ?? actionObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.34f, 1.28f, 1f);
            colors.pressedColor = new Color(1.06f, 1.52f, 1.36f, 1f);
            colors.selectedColor = new Color(1.14f, 1.30f, 1.24f, 1f);
            colors.disabledColor = new Color(0.4f, 0.5f, 0.5f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.09f;
            button.colors = colors;
            button.onClick.RemoveAllListeners();
            ActionKind capturedKind = kind;
            button.onClick.AddListener(() => BeginAction(capturedKind));

            Outline outline = actionObject.GetComponent<Outline>() ?? actionObject.AddComponent<Outline>();
            outline.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.22f);
            outline.effectDistance = new Vector2(1f, -1f);

            RectTransform accentRect = EnsureImageRect(actionRect, "Accent", out Image accent);
            accentRect.anchorMin = new Vector2(0f, 0.5f);
            accentRect.anchorMax = new Vector2(0f, 0.5f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.anchoredPosition = new Vector2(5f, 0f);
            accentRect.sizeDelta = new Vector2(3f, 23f);
            accent.color = Accent;
            accent.raycastTarget = false;

            RectTransform titleRect = EnsureTextRect(actionRect, "Label", out TMP_Text title);
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(0.57f, 1f);
            titleRect.offsetMin = new Vector2(15f, 0f);
            titleRect.offsetMax = new Vector2(0f, 0f);
            title.fontSize = 12f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.color = TextColor;

            RectTransform stateRect = EnsureTextRect(actionRect, "State", out TMP_Text state);
            stateRect.anchorMin = new Vector2(0.52f, 0f);
            stateRect.anchorMax = new Vector2(1f, 1f);
            stateRect.offsetMin = Vector2.zero;
            stateRect.offsetMax = new Vector2(-9f, 0f);
            state.fontSize = 10f;
            state.fontStyle = FontStyles.Bold;
            state.alignment = TextAlignmentOptions.MidlineRight;
            state.color = StateColor;

            _actions.Add(new ActionView
            {
                Kind = kind,
                Rect = actionRect,
                Background = background,
                Title = title,
                State = state,
                GameObject = actionObject
            });
        }

        private void LayoutForCurrentRadar(Vector2 localPoint)
        {
            bool focused = _display != null && _display.IsFullscreen;
            float panelWidth = focused ? FocusPanelWidth : CompactPanelWidth;
            float rowHeight = focused ? FocusRowHeight : CompactRowHeight;
            int visibleCount = focused ? 5 : 4;
            float panelHeight = HeaderHeight + PanelPadding +
                                visibleCount * rowHeight +
                                Mathf.Max(0, visibleCount - 1) * RowGap +
                                PanelPadding;
            _panel.sizeDelta = new Vector2(panelWidth, panelHeight);

            int visibleIndex = 0;
            foreach (ActionView action in _actions)
            {
                bool visible = action.Kind != ActionKind.Center || focused;
                action.GameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                action.Rect.sizeDelta = new Vector2(panelWidth - PanelPadding * 2f, rowHeight);
                action.Title.fontSize = focused ? 13.5f : 12f;
                action.State.fontSize = focused ? 10.8f : 10f;
                float y = -HeaderHeight - PanelPadding - rowHeight * 0.5f -
                          visibleIndex * (rowHeight + RowGap);
                action.Rect.anchoredPosition = new Vector2(0f, y);
                visibleIndex++;
            }

            if (_headerTitle != null)
            {
                _headerTitle.fontSize = focused ? 14.5f : 13f;
            }

            if (_headerHint != null)
            {
                _headerHint.fontSize = focused ? 10f : 9.5f;
            }

            Rect radarRect = _hostRect.rect;
            GetCanvasBoundsInHostSpace(out Vector2 canvasMin, out Vector2 canvasMax);
            float leftSpace = radarRect.xMin - canvasMin.x;
            float rightSpace = canvasMax.x - radarRect.xMax;
            bool canFitLeft = leftSpace >= panelWidth + RadarGap;
            bool canFitRight = rightSpace >= panelWidth + RadarGap;

            if (canFitLeft != canFitRight)
            {
                _panelOnLeft = canFitLeft;
            }
            else if (canFitLeft && canFitRight)
            {
                // Put the menu opposite the tap so the pilot's hand/controller
                // does not cover the choices that just appeared.
                _panelOnLeft = localPoint.x >= radarRect.center.x;
            }
            else
            {
                _panelOnLeft = leftSpace >= rightSpace;
            }

            float desiredX;
            if (_panelOnLeft && canFitLeft)
            {
                desiredX = radarRect.xMin - RadarGap - panelWidth * 0.5f;
            }
            else if (!_panelOnLeft && canFitRight)
            {
                desiredX = radarRect.xMax + RadarGap + panelWidth * 0.5f;
            }
            else
            {
                // Fullscreen scopes leave only a narrow outer margin. Dock
                // just inside that edge, where the circular map has the least
                // useful area, instead of pushing controls off-screen.
                desiredX = _panelOnLeft
                    ? radarRect.xMin + panelWidth * 0.5f + 14f
                    : radarRect.xMax - panelWidth * 0.5f - 14f;
            }

            float desiredY = Mathf.Clamp(localPoint.y, radarRect.yMin + 30f, radarRect.yMax - 30f);
            desiredX = Mathf.Clamp(
                desiredX,
                canvasMin.x + panelWidth * 0.5f + 10f,
                canvasMax.x - panelWidth * 0.5f - 10f);
            desiredY = Mathf.Clamp(
                desiredY,
                canvasMin.y + panelHeight * 0.5f + 10f,
                canvasMax.y - panelHeight * 0.5f - 10f);
            _panelRestPosition = new Vector2(desiredX, desiredY);
            _panel.anchoredPosition = _panelRestPosition;
            UpdateLeaderGeometry();
        }

        private void UpdateLeaderGeometry()
        {
            if (_leaders == null || _display == null || _panel == null)
            {
                return;
            }

            Rect radarRect = _hostRect.rect;
            Vector2 center = radarRect.center;
            float radius = Mathf.Max(24f, Mathf.Min(radarRect.width, radarRect.height) * 0.5f - 12f);
            float nearSide = _panelOnLeft ? -1f : 1f;
            int ringCount = Mathf.Clamp(_display.RangeRingCount, 1, 8);
            int halfRing = Mathf.Max(1, Mathf.CeilToInt(ringCount * 0.5f));
            float referenceRadius = radius * halfRing / ringCount;

            _leaders.BeginLayout(_actions.Count);
            for (int i = 0; i < _actions.Count; i++)
            {
                ActionView action = _actions[i];
                if (!action.GameObject.activeSelf)
                {
                    _leaders.SetConnector(i, Vector2.zero, Vector2.zero, false, false);
                    continue;
                }

                float sourceX = _panelOnLeft ? action.Rect.rect.xMax : action.Rect.rect.xMin;
                Vector3 sourceWorld = action.Rect.TransformPoint(new Vector3(sourceX, 0f, 0f));
                Vector2 source = _visualRoot.InverseTransformPoint(sourceWorld);
                Vector2 target;
                switch (action.Kind)
                {
                    case ActionKind.Linework:
                        target = center + new Vector2(nearSide * referenceRadius * 0.94f, referenceRadius * 0.24f);
                        break;
                    case ActionKind.Map:
                        // Point into the near-side chart field. Keeping this
                        // callout on the menu side avoids drawing a long line
                        // through own-ship and clustered traffic targets.
                        target = center + new Vector2(nearSide * radius * 0.28f, radius * 0.30f);
                        break;
                    case ActionKind.Range:
                        target = center + new Vector2(0f, -radius * 0.88f);
                        break;
                    case ActionKind.Center:
                        target = center;
                        break;
                    default:
                        target = center + new Vector2(nearSide * radius * 0.68f, radius * 0.68f);
                        break;
                }

                _leaders.SetConnector(
                    i,
                    source,
                    target,
                    true,
                    _selectedAction.HasValue && _selectedAction.Value == action.Kind);
            }
        }

        private void RefreshActionLabels()
        {
            if (_display == null)
            {
                return;
            }

            foreach (ActionView action in _actions)
            {
                switch (action.Kind)
                {
                    case ActionKind.Linework:
                        action.Title.text = "GUIDE LINES";
                        action.State.text = _display.ReferenceLineworkVisible ? "HIDE" : "SHOW";
                        break;
                    case ActionKind.Map:
                        action.Title.text = "MAP SOURCE";
                        action.State.text = $"{CompactSourceName(_display.MapSourceName)} · SWITCH";
                        break;
                    case ActionKind.Range:
                        action.Title.text = "RANGE";
                        action.State.text = _display.AutoRangeEnabled
                            ? $"{_display.RangeNM:0} NM · MANUAL"
                            : $"{_display.RangeNM:0} NM · NEXT";
                        break;
                    case ActionKind.Center:
                        action.Title.text = "OWN-SHIP";
                        action.State.text = "RECENTER";
                        break;
                    case ActionKind.View:
                        action.Title.text = "RADAR VIEW";
                        action.State.text = _display.IsFullscreen ? "RESTORE" : "MAXIMIZE";
                        break;
                }

                action.Background.color = _selectedAction.HasValue && _selectedAction.Value == action.Kind
                    ? ButtonPressColor
                    : ButtonColor;
            }
        }

        private void BeginAction(ActionKind kind)
        {
            if (_interactionLocked || _display == null)
            {
                return;
            }

            if (_actionRoutine != null)
            {
                StopCoroutine(_actionRoutine);
            }

            _actionRoutine = StartCoroutine(ExecuteAction(kind));
        }

        private IEnumerator ExecuteAction(ActionKind kind)
        {
            _interactionLocked = true;
            _selectedAction = kind;
            RefreshActionLabels();
            UpdateLeaderGeometry();

            float feedbackDuration = reducedMotion ? 0.06f : 0.16f;
            float elapsed = 0f;
            while (elapsed < feedbackDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_leaders != null)
                {
                    _leaders.Pulse = Mathf.Clamp01(elapsed / feedbackDuration);
                    _leaders.SetVerticesDirty();
                }
                yield return null;
            }

            // VIEW changes the coordinate system of this menu's parent. Close
            // first, then start the existing full-screen radar animation so
            // the popup never jumps across the pilot's field of view.
            if (kind == ActionKind.View)
            {
                _targetOpen = false;
                yield return new WaitForSecondsRealtime(reducedMotion ? 0.03f : 0.10f);
            }

            ApplyAction(kind);
            _targetOpen = false;
            _interactionLocked = false;
            _selectedAction = null;
            if (_leaders != null)
            {
                _leaders.Pulse = 0f;
            }
            _actionRoutine = null;
        }

        private void ApplyAction(ActionKind kind)
        {
            switch (kind)
            {
                case ActionKind.Linework:
                    _display.ToggleReferenceLinework();
                    break;
                case ActionKind.Map:
                    _display.CycleMapSourceAnimated();
                    break;
                case ActionKind.Range:
                    _display.CycleRangeManual();
                    break;
                case ActionKind.Center:
                    _display.ResetMapPan(false);
                    break;
                case ActionKind.View:
                    _display.ToggleFullscreen();
                    break;
            }
        }

        private void ApplyVisualState()
        {
            if (_canvasGroup == null || _panel == null || _leaders == null)
            {
                return;
            }

            float eased = FaaRadarConfigurationDrawer.EaseOutQuart(_progress);
            _canvasGroup.alpha = eased;
            _canvasGroup.interactable = _targetOpen && !_interactionLocked && _progress >= 0.92f;
            _canvasGroup.blocksRaycasts = _targetOpen && _progress >= 0.10f;
            float scale = reducedMotion ? 1f : Mathf.Lerp(0.94f, 1f, eased);
            _panel.localScale = new Vector3(scale, scale, 1f);
            float slideDirection = _panelOnLeft ? -1f : 1f;
            _panel.anchoredPosition = _panelRestPosition +
                                      Vector2.right * (slideDirection * Mathf.Lerp(12f, 0f, eased));
            _leaders.Reveal = eased;
            _leaders.SetVerticesDirty();
        }

        private void GetCanvasBoundsInHostSpace(out Vector2 minimum, out Vector2 maximum)
        {
            RectTransform canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;
            if (canvasRect == null)
            {
                Rect rect = _hostRect.rect;
                minimum = rect.min - Vector2.one * 400f;
                maximum = rect.max + Vector2.one * 400f;
                return;
            }

            Vector3[] corners = new Vector3[4];
            canvasRect.GetWorldCorners(corners);
            Vector3 first = _hostRect.InverseTransformPoint(corners[0]);
            minimum = first;
            maximum = first;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 local = _hostRect.InverseTransformPoint(corners[i]);
                minimum = Vector2.Min(minimum, local);
                maximum = Vector2.Max(maximum, local);
            }
        }

        private static string CompactSourceName(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return "MAP";
            }

            string upper = source.Trim().ToUpperInvariant();
            if (upper.Contains("SECTION")) return "SEC";
            if (upper.Contains("WORLD") || upper.Contains("AERONAUT")) return "WAC";
            if (upper.Contains("TERMINAL")) return "TAC";
            if (upper.Contains("STREET")) return "STREET";
            if (upper.Contains("SAT")) return "SAT";
            return upper.Length <= 7 ? upper : upper.Substring(0, 7);
        }

        private static RectTransform EnsureTextRect(RectTransform parent, string name, out TMP_Text text)
        {
            Transform existing = parent.Find(name);
            GameObject textObject = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            text = textObject.GetComponent<TextMeshProUGUI>() ?? textObject.AddComponent<TextMeshProUGUI>();
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.extraPadding = true;
            text.raycastTarget = false;
            return rect;
        }

        private static RectTransform EnsureImageRect(RectTransform parent, string name, out Image image)
        {
            Transform existing = parent.Find(name);
            GameObject imageObject = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            image = imageObject.GetComponent<Image>() ?? imageObject.AddComponent<Image>();
            return imageObject.GetComponent<RectTransform>();
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

    /// <summary>
    /// Lightweight UI mesh for animated action leader lines and target pulses.
    /// It avoids LineRenderer/world-space conversion so the same geometry is
    /// sharp in desktop, XR simulator, and screen-space overlay canvases.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class RadarContextLeaderGraphic : MaskableGraphic
    {
        private struct Connector
        {
            public Vector2 Source;
            public Vector2 Target;
            public bool Visible;
            public bool Selected;
        }

        private readonly List<Connector> _connectors = new List<Connector>();
        public float Reveal { get; set; }
        public float Pulse { get; set; }

        public void BeginLayout(int count)
        {
            while (_connectors.Count < count)
            {
                _connectors.Add(default);
            }

            if (_connectors.Count > count)
            {
                _connectors.RemoveRange(count, _connectors.Count - count);
            }
        }

        public void SetConnector(int index, Vector2 source, Vector2 target, bool visible, bool selected)
        {
            if (index < 0 || index >= _connectors.Count)
            {
                return;
            }

            _connectors[index] = new Connector
            {
                Source = source,
                Target = target,
                Visible = visible,
                Selected = selected
            };
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            float reveal = Mathf.Clamp01(Reveal);
            if (reveal <= 0.001f)
            {
                return;
            }

            for (int i = 0; i < _connectors.Count; i++)
            {
                Connector connector = _connectors[i];
                if (!connector.Visible)
                {
                    continue;
                }

                float stagedReveal = Mathf.Clamp01(reveal * 1.22f - i * 0.055f);
                Color main = connector.Selected
                    ? new Color(0.66f, 1f, 0.94f, 0.98f)
                    : new Color(0.25f, 0.96f, 0.91f, 0.84f);
                main.a *= stagedReveal;
                Color halo = new Color(0.005f, 0.025f, 0.034f, 0.80f * stagedReveal);

                Vector2 direction = connector.Target.x >= connector.Source.x ? Vector2.right : Vector2.left;
                Vector2 kneeA = connector.Source + direction * 17f;
                Vector2 kneeB = kneeA + new Vector2(direction.x * 23f, connector.Target.y - kneeA.y);
                Vector2[] points = { connector.Source, kneeA, kneeB, connector.Target };
                DrawPartialPolyline(vertexHelper, points, stagedReveal, 5.2f, halo);
                DrawPartialPolyline(vertexHelper, points, stagedReveal, connector.Selected ? 2.5f : 1.8f, main);

                if (stagedReveal >= 0.96f)
                {
                    AddDisc(vertexHelper, connector.Target, connector.Selected ? 4.2f : 3.1f, main, 14);
                    AddRing(vertexHelper, connector.Target, 7f, 1.3f, new Color(main.r, main.g, main.b, main.a * 0.62f), 24);
                }

                if (connector.Selected && Pulse > 0f)
                {
                    float pulse = Mathf.Clamp01(Pulse);
                    float radius = Mathf.Lerp(8f, 22f, pulse);
                    float alpha = (1f - pulse) * 0.9f;
                    AddRing(
                        vertexHelper,
                        connector.Target,
                        radius,
                        Mathf.Lerp(2.4f, 1f, pulse),
                        new Color(main.r, main.g, main.b, alpha),
                        32);
                }
            }
        }

        private static void DrawPartialPolyline(
            VertexHelper vertexHelper,
            IReadOnlyList<Vector2> points,
            float progress,
            float width,
            Color color)
        {
            float totalLength = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                totalLength += Vector2.Distance(points[i], points[i + 1]);
            }

            float remaining = totalLength * Mathf.Clamp01(progress);
            for (int i = 0; i < points.Count - 1 && remaining > 0f; i++)
            {
                Vector2 from = points[i];
                Vector2 to = points[i + 1];
                float length = Vector2.Distance(from, to);
                if (length <= 0.001f)
                {
                    continue;
                }

                float segmentAmount = Mathf.Clamp01(remaining / length);
                AddLine(vertexHelper, from, Vector2.Lerp(from, to, segmentAmount), width, color);
                remaining -= length;
            }
        }

        private static void AddLine(VertexHelper vertexHelper, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
            AddQuad(
                vertexHelper,
                from - normal,
                from + normal,
                to + normal,
                to - normal,
                color);
        }

        private static void AddDisc(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            Color color,
            int segments)
        {
            int centerIndex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, center, color);
            for (int i = 0; i <= segments; i++)
            {
                float radians = Mathf.PI * 2f * i / segments;
                AddVertex(vertexHelper, center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius, color);
                if (i > 0)
                {
                    vertexHelper.AddTriangle(centerIndex, centerIndex + i, centerIndex + i + 1);
                }
            }
        }

        private static void AddRing(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            float width,
            Color color,
            int segments)
        {
            float inner = Mathf.Max(0f, radius - width * 0.5f);
            float outer = radius + width * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * 2f * i / segments;
                float a1 = Mathf.PI * 2f * (i + 1) / segments;
                Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                Vector2 d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                AddQuad(
                    vertexHelper,
                    center + d0 * inner,
                    center + d0 * outer,
                    center + d1 * outer,
                    center + d1 * inner,
                    color);
            }
        }

        private static void AddQuad(
            VertexHelper vertexHelper,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Vector2 d,
            Color color)
        {
            int start = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, a, color);
            AddVertex(vertexHelper, b, color);
            AddVertex(vertexHelper, c, color);
            AddVertex(vertexHelper, d, color);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vertexHelper.AddVert(vertex);
        }
    }
}
