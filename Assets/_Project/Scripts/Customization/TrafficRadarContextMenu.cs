using System.Collections;
using System.Collections.Generic;
using System;
using System.Globalization;
using TMPro;
using TrafficRadar;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private const float CompactPanelWidth = 284f;
        private const float FocusPanelWidth = 386f;
        private const float CompactRowHeight = 48f;
        private const float FocusRowHeight = 58f;
        private const float RowGap = 8f;
        private const float HeaderHeight = 62f;
        private const float PanelPadding = 14f;
        private const float RadarGap = 20f;
        private const float CompactTopSafeInset = 86f;
        private const float FocusTopSafeInset = 132f;
        private const float CoordinateStep = 0.001f;

        private static readonly Color Accent = FaaRadarVisualStyle.Accent;
        private static readonly Color PanelColor = FaaRadarVisualStyle.Glass;
        private static readonly Color ButtonColor = FaaRadarVisualStyle.GlassRaised;
        private static readonly Color ButtonFocusColor = FaaRadarVisualStyle.GlassHover;
        private static readonly Color ButtonPressColor = FaaRadarVisualStyle.GlassPressed;
        private static readonly Color TextColor = FaaRadarVisualStyle.TextPrimary;
        private static readonly Color StateColor = FaaRadarVisualStyle.TextSecondary;

        private enum ActionKind
        {
            Linework,
            Map,
            Range,
            Target,
            Center,
            View
        }

        private sealed class ActionView
        {
            public ActionKind Kind;
            public RectTransform Rect;
            public Image Background;
            public Image Accent;
            public Image IconPlate;
            public TMP_Text Icon;
            public TMP_Text Title;
            public TMP_Text State;
            public Button Button;
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
        private ActionKind? _focusedAction;
        private bool _layoutFocused;
        private Vector2 _targetTapLocalPoint;
        private bool _hasTargetTap;
        // Keep the tap in the radar's own normalized coordinate space as well
        // as in the interaction surface's local space.  FULL/REST resizes the
        // shared traffic-system root, so replaying only the original local
        // pixel coordinate can move the target by several pixels (or even
        // outside the scope) while the menu remains open.
        private Vector2 _targetTapDisplayNormalized;
        private bool _hasTargetTapDisplayNormalized;

        // Target confirmation dialog state. The dialog is a child of the
        // same overlay as the quick menu, so it can keep the maximized chart
        // visible and receive map taps through its non-raycast dimmer.
        private RectTransform _targetDialogRoot;
        private RectTransform _targetDialogCard;
        private CanvasGroup _targetDialogGroup;
        private TMP_Text _targetDialogHint;
        private TMP_Text _targetDialogCoordinates;
        private TMP_Text _targetDialogStatus;
        private TMP_InputField _latitudeInput;
        private TMP_InputField _longitudeInput;
        private Button _targetDialogConfirm;
        private Button _targetDialogCancel;
        private Button _targetDialogClear;
        private bool _targetSetupOpen;
        private bool _updatingTargetDialog;
        private readonly List<GameObject> _simulatorInputPanels = new List<GameObject>();
        private readonly List<bool> _simulatorInputPanelStates = new List<bool>();
        private bool _simulatorInputPanelStateCaptured;

        public bool IsOpen => _targetOpen || _progress > 0.01f;
        public bool IsTargetSetupOpen => _targetSetupOpen;

        /// <summary>
        /// UnityEvent/XR-friendly entry points for the explicit target flow.
        /// </summary>
        public void OpenNavigationTargetDialog() => BeginTargetSetup();
        public void ConfirmNavigationTarget() => ConfirmTargetSetup();
        public void CancelNavigationTarget() => CancelTargetSetup();

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

            Vector2 localPoint = _hostRect != null ? _hostRect.rect.center : Vector2.zero;
            bool resolved = _hostRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _hostRect,
                screenPoint,
                eventCamera,
                out localPoint);
            if (!resolved && _hostRect != null)
            {
                localPoint = _hostRect.rect.center;
            }
            _targetTapLocalPoint = localPoint;
            _hasTargetTap = true;
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
            _targetTapLocalPoint = localPoint;
            _hasTargetTap = true;
            CaptureTargetTapInDisplaySpace(localPoint);
            _visualRoot.gameObject.SetActive(true);
            _interactionLocked = false;
            _selectedAction = null;
            _focusedAction = null;
            RefreshActionLabels();
            LayoutForCurrentRadar(localPoint);
            _targetOpen = true;
            enabled = true;
        }

        public void Close(bool immediate = false)
        {
            if (_targetSetupOpen)
            {
                CancelTargetSetup();
            }
            _targetOpen = false;
            _interactionLocked = false;
            _selectedAction = null;
            _focusedAction = null;
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
                if (_display != null && _layoutFocused != _display.IsFullscreen)
                {
                    if (_targetSetupOpen && !_display.IsFullscreen)
                    {
                        CancelTargetSetup();
                    }
                    LayoutForCurrentRadar(Vector2.zero);
                }
                RefreshActionLabels();
                UpdateLeaderGeometry();
            }

            if (_targetSetupOpen)
            {
                if (_display == null || !_display.IsFullscreen)
                {
                    CancelTargetSetup();
                }
                else
                {
                    LayoutTargetDialog();
                    RefreshTargetDialog();
                }
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
            FaaRadarVisualStyle.ApplyRounded(panelImage, PanelColor, 15);
            panelImage.raycastTarget = true;
            Outline outline = panelObject.GetComponent<Outline>() ?? panelObject.AddComponent<Outline>();
            outline.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.20f);
            outline.effectDistance = new Vector2(1f, -1f);
            FaaRadarVisualStyle.EnsureDropShadow(
                panelObject,
                new Color(0f, 0.012f, 0.018f, 0.78f),
                new Vector2(9f, -12f));

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
            titleRect.offsetMin = new Vector2(PanelPadding, -HeaderHeight + 8f);
            titleRect.offsetMax = new Vector2(-PanelPadding, -8f);
            title.text = "TRAFFIC CONTROLS";
            title.fontSize = 15f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.TopLeft;
            title.color = TextColor;
            _headerTitle = title;

            RectTransform hintRect = EnsureTextRect(_panel, "Hint", out TMP_Text hint);
            hintRect.anchorMin = new Vector2(0f, 1f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.offsetMin = new Vector2(PanelPadding, -HeaderHeight + 8f);
            hintRect.offsetMax = new Vector2(-PanelPadding, -30f);
            hint.text = "TAP TO APPLY  ·  TAP RADAR TO CLOSE";
            hint.fontSize = 10f;
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
            EnsureAction(ActionKind.Target, "ActionTarget");
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
            FaaRadarVisualStyle.ApplyRounded(background, ButtonColor, 10);
            Button button = actionObject.GetComponent<Button>() ?? actionObject.AddComponent<Button>();
            FaaRadarVisualStyle.ConfigureButton(button, background);
            button.onClick.RemoveAllListeners();
            ActionKind capturedKind = kind;
            button.onClick.AddListener(() => BeginAction(capturedKind));

            FaaRadarButtonMotion motion = actionObject.GetComponent<FaaRadarButtonMotion>() ??
                                          actionObject.AddComponent<FaaRadarButtonMotion>();
            motion.Configure(reducedMotion, 1.012f);
            TrafficRadarActionFocus focus = actionObject.GetComponent<TrafficRadarActionFocus>() ??
                                            actionObject.AddComponent<TrafficRadarActionFocus>();
            focus.Configure(this, (int)kind);

            Outline outline = actionObject.GetComponent<Outline>() ?? actionObject.AddComponent<Outline>();
            outline.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.10f);
            outline.effectDistance = new Vector2(1f, -1f);

            RectTransform accentRect = EnsureImageRect(actionRect, "Accent", out Image accent);
            accentRect.anchorMin = new Vector2(0f, 0.5f);
            accentRect.anchorMax = new Vector2(0f, 0.5f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.anchoredPosition = new Vector2(4f, 0f);
            accentRect.sizeDelta = new Vector2(3f, 20f);
            FaaRadarVisualStyle.ApplyRounded(accent, Accent, 4);
            accent.raycastTarget = false;

            RectTransform iconPlateRect = EnsureImageRect(actionRect, "IconPlate", out Image iconPlate);
            iconPlateRect.anchorMin = new Vector2(0f, 0.5f);
            iconPlateRect.anchorMax = new Vector2(0f, 0.5f);
            iconPlateRect.pivot = new Vector2(0f, 0.5f);
            iconPlateRect.anchoredPosition = new Vector2(12f, 0f);
            iconPlateRect.sizeDelta = new Vector2(31f, 31f);
            FaaRadarVisualStyle.ApplyRounded(
                iconPlate,
                new Color(Accent.r, Accent.g, Accent.b, 0.13f),
                9);
            iconPlate.raycastTarget = false;

            RectTransform iconRect = EnsureTextRect(iconPlateRect, "Glyph", out TMP_Text icon);
            Stretch(iconRect);
            icon.text = ActionIcon(kind);
            icon.fontSize = 10.5f;
            icon.fontStyle = FontStyles.Bold;
            icon.alignment = TextAlignmentOptions.Center;
            icon.color = Accent;

            RectTransform titleRect = EnsureTextRect(actionRect, "Label", out TMP_Text title);
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(0.68f, 1f);
            titleRect.offsetMin = new Vector2(51f, 0f);
            titleRect.offsetMax = new Vector2(-4f, 0f);
            title.fontSize = 13f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.color = TextColor;

            RectTransform stateRect = EnsureTextRect(actionRect, "State", out TMP_Text state);
            stateRect.anchorMin = new Vector2(0.68f, 0f);
            stateRect.anchorMax = new Vector2(1f, 1f);
            stateRect.offsetMin = new Vector2(3f, 0f);
            stateRect.offsetMax = new Vector2(-12f, 0f);
            state.fontSize = 10.5f;
            state.fontStyle = FontStyles.Bold;
            state.alignment = TextAlignmentOptions.MidlineRight;
            state.color = StateColor;

            _actions.Add(new ActionView
            {
                Kind = kind,
                Rect = actionRect,
                Background = background,
                Accent = accent,
                IconPlate = iconPlate,
                Icon = icon,
                Title = title,
                State = state,
                Button = button,
                GameObject = actionObject
            });
        }

        private void LayoutForCurrentRadar(Vector2 localPoint)
        {
            bool focused = _display != null && _display.IsFullscreen;
            _layoutFocused = focused;
            if (_leaders != null)
            {
                _leaders.IdleAlpha = focused ? 0.24f : 0.17f;
            }
            float panelWidth = focused ? FocusPanelWidth : CompactPanelWidth;
            float rowHeight = focused ? FocusRowHeight : CompactRowHeight;
            int visibleCount = focused ? 6 : 5;
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
                action.Title.fontSize = focused ? 14.5f : 12.5f;
                action.State.fontSize = focused ? 11f : 9.5f;
                if (action.IconPlate != null)
                {
                    float iconSize = focused ? 36f : 31f;
                    action.IconPlate.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
                }
                if (action.Icon != null)
                {
                    action.Icon.fontSize = focused ? 12f : 10.5f;
                }
                float y = -HeaderHeight - PanelPadding - rowHeight * 0.5f -
                          visibleIndex * (rowHeight + RowGap);
                action.Rect.anchoredPosition = new Vector2(0f, y);
                visibleIndex++;
            }

            if (_headerTitle != null)
            {
                _headerTitle.fontSize = focused ? 18f : 15.5f;
            }

            if (_headerHint != null)
            {
                _headerHint.fontSize = focused ? 10.5f : 9.5f;
                _headerHint.text = _targetSetupOpen
                    ? "MAP PREVIEW  ·  CONFIRM OR CANCEL"
                    : focused
                        ? "TAP ACTION TO APPLY  ·  TAP RADAR TO CLOSE"
                        : "FULL MAP TO SET TARGET  ·  TAP RADAR TO CLOSE";
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
            // Leave a deliberate safe band below the horizontal traffic
            // controls.  The old clamp only guarded the canvas edge, so a
            // focused menu could slide underneath FULL/CHT/TRK and become
            // visually and interactively ambiguous.
            float topSafeInset = focused ? FocusTopSafeInset : CompactTopSafeInset;
            Transform controls = _canvas != null ? _canvas.transform.Find("TrafficControlStrip") : null;
            if (controls != null)
            {
                RectTransform controlsRect = controls as RectTransform;
                if (controlsRect != null)
                {
                    Vector3[] corners = new Vector3[4];
                    controlsRect.GetWorldCorners(corners);
                    float controlsBottom = _hostRect.InverseTransformPoint(corners[0]).y;
                    topSafeInset = Mathf.Max(topSafeInset, canvasMax.y - controlsBottom + (focused ? 18f : 12f));
                }
            }

            float minPanelY = canvasMin.y + panelHeight * 0.5f + 10f;
            float maxPanelY = canvasMax.y - panelHeight * 0.5f - topSafeInset;
            if (maxPanelY < minPanelY)
            {
                // On a very short XR viewport, preserve visibility rather
                // than producing an inverted Clamp range.
                maxPanelY = canvasMax.y - panelHeight * 0.5f - 10f;
            }
            desiredY = Mathf.Clamp(desiredY, minPanelY, Mathf.Max(minPanelY, maxPanelY));
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
                    _leaders.SetConnector(i, Vector2.zero, Vector2.zero, false, false, false, Color.clear);
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
                    case ActionKind.Target:
                        target = ResolveTargetLeaderPoint(center, radius);
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
                    _selectedAction.HasValue && _selectedAction.Value == action.Kind,
                    _focusedAction.HasValue && _focusedAction.Value == action.Kind,
                    GetActionColor(action.Kind));
            }
        }

        private Vector2 ResolveTargetLeaderPoint(Vector2 radarCenter, float radius)
        {
            Vector2 hostPoint = radarCenter;
            if (_hasTargetTapDisplayNormalized && _display != null &&
                _display.DisplayRectTransform != null)
            {
                RectTransform displayRect = _display.DisplayRectTransform;
                Rect displayBounds = displayRect.rect;
                float displayRadius = Mathf.Min(displayBounds.width, displayBounds.height) * 0.5f;
                Vector2 displayLocal = displayBounds.center + _targetTapDisplayNormalized * displayRadius;
                Vector3 tapWorld = displayRect.TransformPoint(displayLocal);
                hostPoint = _visualRoot != null
                    ? _visualRoot.InverseTransformPoint(tapWorld)
                    : radarCenter;
            }
            else if (_hasTargetTap && _hostRect != null)
            {
                // The tap is captured in the interaction surface's local
                // space. Convert through world space before drawing in the
                // menu's visual-root space so the leader stays aligned when
                // the display and hit surface have different anchors/scales.
                Vector3 tapWorld = _hostRect.TransformPoint(_targetTapLocalPoint);
                hostPoint = _visualRoot != null
                    ? _visualRoot.InverseTransformPoint(tapWorld)
                    : _targetTapLocalPoint;
            }
            else if (_display != null && _display.HasNavigationTarget)
            {
                hostPoint = radarCenter +
                            _display.CurrentNavigationTarget.RadarPosition * radius;
            }

            // The leader should terminate inside the scope even if a pointer
            // landed on the outer frame or the target is currently off-range.
            Vector2 offset = hostPoint - radarCenter;
            float maxRadius = Mathf.Max(18f, radius * 0.90f);
            if (offset.sqrMagnitude > maxRadius * maxRadius)
            {
                hostPoint = radarCenter + offset.normalized * maxRadius;
            }

            return hostPoint;
        }

        private void ApplyNavigationTargetAtTap()
        {
            BeginTargetSetup();
        }

        private void BeginTargetSetup()
        {
            if (_display == null || !_display.CanSetNavigationTarget)
            {
                return;
            }

            EnsureTargetDialog();
            _targetSetupOpen = true;
            _interactionLocked = false;
            _selectedAction = ActionKind.Target;
            SetSimulatorFeedbackVisible(false);

            // Start from the previously committed waypoint when editing;
            // otherwise use the original tap (or the scope centre) as the
            // first candidate. Nothing is committed until CONFIRM TARGET.
            _display.ClearNavigationPreview();
            if (_display.HasNavigationTarget && _display.CurrentNavigationTarget.HasGeoPosition)
            {
                _display.SetNavigationPreview(
                    _display.CurrentNavigationTarget.Latitude,
                    _display.CurrentNavigationTarget.Longitude,
                    "EDIT");
            }
            else if (_hasTargetTapDisplayNormalized && _display.DisplayRectTransform != null)
            {
                RectTransform displayRect = _display.DisplayRectTransform;
                Rect displayBounds = displayRect.rect;
                float displayRadius = Mathf.Min(displayBounds.width, displayBounds.height) * 0.5f;
                Vector2 displayLocal = displayBounds.center + _targetTapDisplayNormalized * displayRadius;
                _display.SetNavigationPreviewFromLocalPoint(displayLocal, "MAP");
            }
            else if (_display.DisplayRectTransform != null)
            {
                _display.SetNavigationPreviewFromLocalPoint(
                    _display.DisplayRectTransform.rect.center,
                    "MAP");
            }

            if (_panel != null)
            {
                _panel.gameObject.SetActive(false);
            }
            if (_leaders != null)
            {
                _leaders.gameObject.SetActive(false);
            }
            _targetDialogRoot.gameObject.SetActive(true);
            LayoutTargetDialog();
            RefreshTargetDialog(true);
            RefreshActionLabels();
        }

        /// <summary>
        /// Called by the interaction surface while the confirmation dialog is
        /// open. A map tap updates only the cyan preview marker and coordinate
        /// fields; it never closes the menu or commits a destination.
        /// </summary>
        public void HandleMapTapFromScreenPoint(Vector2 screenPoint, Camera eventCamera)
        {
            if (!_targetSetupOpen || _display == null || !_display.CanSetNavigationTarget)
            {
                return;
            }

            if (_display.SetNavigationPreviewFromScreenPoint(screenPoint, eventCamera, "MAP"))
            {
                RefreshTargetDialog(true);
            }
        }

        private void ConfirmTargetSetup()
        {
            if (_display == null || !_display.CommitNavigationPreview())
            {
                SetTargetDialogStatus("SELECT A VALID MAP POINT OR COORDINATES");
                return;
            }

            CloseTargetDialog(false);
            _selectedAction = null;
            _targetOpen = true;
            _interactionLocked = false;
            RefreshActionLabels();
            LayoutForCurrentRadar(Vector2.zero);
        }

        private void CancelTargetSetup()
        {
            if (!_targetSetupOpen && _targetDialogRoot == null)
            {
                return;
            }

            _display?.ClearNavigationPreview();
            CloseTargetDialog(false);
            _selectedAction = null;
            _interactionLocked = false;
            RefreshActionLabels();
        }

        private void ClearCommittedTargetFromDialog()
        {
            _display?.ClearNavigationTarget();
            _display?.ClearNavigationPreview();
            CloseTargetDialog(false);
            _selectedAction = null;
            _interactionLocked = false;
            RefreshActionLabels();
        }

        private void CloseTargetDialog(bool immediate)
        {
            _targetSetupOpen = false;
            if (_targetDialogRoot != null)
            {
                _targetDialogRoot.gameObject.SetActive(false);
            }
            SetSimulatorFeedbackVisible(true);
            if (_panel != null)
            {
                _panel.gameObject.SetActive(_targetOpen);
            }
            if (_leaders != null)
            {
                _leaders.gameObject.SetActive(_targetOpen);
            }
            if (immediate)
            {
                if (_targetDialogGroup != null)
                {
                    _targetDialogGroup.alpha = 0f;
                }
            }
        }

        private void LayoutTargetDialog()
        {
            if (_targetDialogCard == null || _targetDialogRoot == null)
            {
                return;
            }

            // Keep the scope centre open for map taps. The card is docked to
            // the left edge of the maximized radar when there is room and
            // falls back to centre only on unusually narrow XR viewports.
            Rect bounds = _targetDialogRoot.rect;
            Vector2 cardSize = _targetDialogCard.rect.size;
            float halfWidth = Mathf.Max(1f, bounds.width * 0.5f);
            float halfHeight = Mathf.Max(1f, bounds.height * 0.5f);
            float x = -halfWidth + cardSize.x * 0.5f + 14f;
            if (bounds.width < cardSize.x + 28f)
            {
                x = 0f;
            }

            float y = Mathf.Clamp(
                -18f,
                -halfHeight + cardSize.y * 0.5f + 14f,
                halfHeight - cardSize.y * 0.5f - 14f);
            _targetDialogCard.anchoredPosition = new Vector2(x, y);
        }

        private void SetSimulatorFeedbackVisible(bool visible)
        {
            if (!visible && !_simulatorInputPanelStateCaptured)
            {
                _simulatorInputPanels.Clear();
                _simulatorInputPanelStates.Clear();
                GameObject[] candidates = FindObjectsByType<GameObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (int i = 0; i < candidates.Length; i++)
                {
                    GameObject candidate = candidates[i];
                    if (candidate == null ||
                        (candidate.name != "SelectedInputFeedbackPanel" &&
                         candidate.name != "InputSelectionClosedWindow" &&
                         candidate.name != "InputSelectionWindow"))
                    {
                        continue;
                    }

                    bool underSimulator = false;
                    Transform ancestor = candidate.transform.parent;
                    while (ancestor != null)
                    {
                        if (ancestor.name == "XR Interaction Simulator UI")
                        {
                            underSimulator = true;
                            break;
                        }

                        ancestor = ancestor.parent;
                    }

                    if (underSimulator)
                    {
                        _simulatorInputPanels.Add(candidate);
                        _simulatorInputPanelStates.Add(candidate.activeSelf);
                    }
                }

                _simulatorInputPanelStateCaptured = true;
            }

            if (!visible && !_simulatorInputPanelStateCaptured)
            {
                return;
            }

            if (!visible)
            {
                for (int i = 0; i < _simulatorInputPanels.Count; i++)
                {
                    if (_simulatorInputPanels[i] != null)
                    {
                        _simulatorInputPanels[i].SetActive(false);
                    }
                }
            }
            else if (_simulatorInputPanelStateCaptured)
            {
                for (int i = 0; i < _simulatorInputPanels.Count; i++)
                {
                    if (_simulatorInputPanels[i] != null)
                    {
                        _simulatorInputPanels[i].SetActive(_simulatorInputPanelStates[i]);
                    }
                }

                _simulatorInputPanels.Clear();
                _simulatorInputPanelStates.Clear();
                _simulatorInputPanelStateCaptured = false;
            }
        }

        private void EnsureTargetDialog()
        {
            if (_targetDialogRoot != null)
            {
                return;
            }

            GameObject rootObject = new GameObject(
                "NavigationTargetDialog",
                typeof(RectTransform),
                typeof(CanvasGroup));
            rootObject.transform.SetParent(_visualRoot, false);
            _targetDialogRoot = rootObject.GetComponent<RectTransform>();
            Stretch(_targetDialogRoot);
            _targetDialogGroup = rootObject.GetComponent<CanvasGroup>();
            _targetDialogGroup.alpha = 1f;
            _targetDialogGroup.interactable = true;
            // Ignore the quick-menu CanvasGroup while the modal is open. The
            // card/buttons receive pointer/XR raycasts, while the dimmer has
            // raycastTarget=false so map taps outside the card pass through to
            // the interaction surface.
            _targetDialogGroup.blocksRaycasts = true;
            _targetDialogGroup.ignoreParentGroups = true;

            GameObject dimObject = new GameObject(
                "Dimmer",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            dimObject.transform.SetParent(_targetDialogRoot, false);
            RectTransform dimRect = dimObject.GetComponent<RectTransform>();
            Stretch(dimRect);
            Image dim = dimObject.GetComponent<Image>();
            dim.color = new Color(0.002f, 0.012f, 0.018f, 0.28f);
            dim.raycastTarget = false;

            GameObject cardObject = new GameObject(
                "TargetSetupCard",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            cardObject.transform.SetParent(_targetDialogRoot, false);
            _targetDialogCard = cardObject.GetComponent<RectTransform>();
            _targetDialogCard.anchorMin = new Vector2(0.5f, 0.5f);
            _targetDialogCard.anchorMax = new Vector2(0.5f, 0.5f);
            _targetDialogCard.pivot = new Vector2(0.5f, 0.5f);
            _targetDialogCard.anchoredPosition = new Vector2(-226f, -18f);
            _targetDialogCard.sizeDelta = new Vector2(430f, 360f);
            Image cardImage = cardObject.GetComponent<Image>();
            FaaRadarVisualStyle.ApplyRounded(cardImage, new Color(0.008f, 0.045f, 0.055f, 0.985f), 16);
            cardImage.raycastTarget = true;
            Outline cardOutline = cardObject.GetComponent<Outline>();
            cardOutline.effectColor = new Color(FaaRadarVisualStyle.Accent.r, FaaRadarVisualStyle.Accent.g, FaaRadarVisualStyle.Accent.b, 0.30f);
            cardOutline.effectDistance = new Vector2(1f, -1f);
            FaaRadarVisualStyle.EnsureDropShadow(
                cardObject,
                new Color(0f, 0.004f, 0.008f, 0.88f),
                new Vector2(8f, -10f));

            TMP_Text title = CreateDialogText(_targetDialogCard, "Title", "SET NAVIGATION TARGET", 18f, TextColor, FontStyles.Bold);
            title.alignment = TextAlignmentOptions.TopLeft;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(22f, -42f);
            title.rectTransform.offsetMax = new Vector2(-22f, -14f);
            _targetDialogHint = CreateDialogText(_targetDialogCard, "Hint", "TAP THE MAP TO PREVIEW  ·  COORDINATES STAY UNCOMMITTED", 10f, StateColor, FontStyles.Normal);
            _targetDialogHint.alignment = TextAlignmentOptions.TopLeft;
            _targetDialogHint.rectTransform.anchorMin = new Vector2(0f, 1f);
            _targetDialogHint.rectTransform.anchorMax = new Vector2(1f, 1f);
            _targetDialogHint.rectTransform.pivot = new Vector2(0.5f, 1f);
            _targetDialogHint.rectTransform.offsetMin = new Vector2(22f, -68f);
            _targetDialogHint.rectTransform.offsetMax = new Vector2(-22f, -46f);

            CreateDialogText(_targetDialogCard, "LatitudeLabel", "LATITUDE", 10f, StateColor, FontStyles.Bold,
                new Vector2(-194f, 92f), new Vector2(-108f, 116f));
            CreateDialogText(_targetDialogCard, "LongitudeLabel", "LONGITUDE", 10f, StateColor, FontStyles.Bold,
                new Vector2(-194f, 42f), new Vector2(-108f, 66f));
            _latitudeInput = CreateCoordinateInput(_targetDialogCard, "LatitudeInput", "37.00000", new Vector2(-46f, 88f));
            _longitudeInput = CreateCoordinateInput(_targetDialogCard, "LongitudeInput", "-75.00000", new Vector2(-46f, 38f));

            CreateDialogButton(_targetDialogCard, "LatMinus", "−", new Vector2(106f, 88f), new Vector2(34f, 34f), () => NudgeCoordinate(true, -CoordinateStep));
            CreateDialogButton(_targetDialogCard, "LatPlus", "+", new Vector2(146f, 88f), new Vector2(34f, 34f), () => NudgeCoordinate(true, CoordinateStep));
            CreateDialogButton(_targetDialogCard, "LonMinus", "−", new Vector2(106f, 38f), new Vector2(34f, 34f), () => NudgeCoordinate(false, -CoordinateStep));
            CreateDialogButton(_targetDialogCard, "LonPlus", "+", new Vector2(146f, 38f), new Vector2(34f, 34f), () => NudgeCoordinate(false, CoordinateStep));

            _targetDialogCoordinates = CreateDialogText(_targetDialogCard, "Coordinates", "PREVIEW  —", 12f, TextColor, FontStyles.Bold,
                new Vector2(-194f, -4f), new Vector2(194f, 22f));
            _targetDialogCoordinates.alignment = TextAlignmentOptions.MidlineLeft;
            _targetDialogStatus = CreateDialogText(_targetDialogCard, "Status", "SELECT A POINT TO CONTINUE", 10f, StateColor, FontStyles.Normal,
                new Vector2(-194f, -34f), new Vector2(194f, -10f));
            _targetDialogStatus.alignment = TextAlignmentOptions.MidlineLeft;

            _targetDialogCancel = CreateDialogButton(_targetDialogCard, "Cancel", "CANCEL", new Vector2(-128f, -142f), new Vector2(102f, 42f), CancelTargetSetup);
            _targetDialogClear = CreateDialogButton(_targetDialogCard, "Clear", "CLEAR ACTIVE", new Vector2(0f, -142f), new Vector2(122f, 42f), ClearCommittedTargetFromDialog);
            _targetDialogConfirm = CreateDialogButton(_targetDialogCard, "Confirm", "CONFIRM TARGET", new Vector2(138f, -142f), new Vector2(144f, 42f), ConfirmTargetSetup);

            _latitudeInput.onEndEdit.AddListener(_ => OnCoordinateInputEdited());
            _longitudeInput.onEndEdit.AddListener(_ => OnCoordinateInputEdited());
            rootObject.transform.SetAsLastSibling();
            rootObject.SetActive(false);
            LayoutTargetDialog();
        }

        private TMP_Text CreateDialogText(
            RectTransform parent,
            string name,
            string value,
            float fontSize,
            Color color,
            FontStyles style,
            Vector2? anchoredMin = null,
            Vector2? anchoredMax = null)
        {
            RectTransform rect = EnsureTextRect(parent, name, out TMP_Text text);
            if (anchoredMin.HasValue && anchoredMax.HasValue)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = (anchoredMin.Value + anchoredMax.Value) * 0.5f;
                rect.sizeDelta = anchoredMax.Value - anchoredMin.Value;
            }
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private TMP_InputField CreateCoordinateInput(
            RectTransform parent,
            string name,
            string placeholder,
            Vector2 position)
        {
            GameObject inputObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(TMP_InputField));
            inputObject.transform.SetParent(parent, false);
            RectTransform rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(144f, 34f);
            Image image = inputObject.GetComponent<Image>();
            FaaRadarVisualStyle.ApplyRounded(image, new Color(0.015f, 0.095f, 0.105f, 1f), 8);
            image.raycastTarget = true;

            TMP_InputField field = inputObject.GetComponent<TMP_InputField>();
            field.contentType = TMP_InputField.ContentType.DecimalNumber;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterValidation = TMP_InputField.CharacterValidation.Decimal;
            field.caretWidth = 2;
            field.selectionColor = new Color(FaaRadarVisualStyle.Accent.r, FaaRadarVisualStyle.Accent.g, FaaRadarVisualStyle.Accent.b, 0.26f);

            RectTransform textRect = EnsureTextRect(rect, "Text", out TMP_Text text);
            Stretch(textRect);
            textRect.offsetMin = new Vector2(10f, 3f);
            textRect.offsetMax = new Vector2(-10f, -3f);
            text.fontSize = 14f;
            text.fontStyle = FontStyles.Bold;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            field.textComponent = text as TMP_Text;

            RectTransform placeholderRect = EnsureTextRect(rect, "Placeholder", out TMP_Text placeholderText);
            Stretch(placeholderRect);
            placeholderRect.offsetMin = new Vector2(10f, 3f);
            placeholderRect.offsetMax = new Vector2(-10f, -3f);
            placeholderText.fontSize = 12f;
            placeholderText.color = StateColor;
            placeholderText.alignment = TextAlignmentOptions.MidlineLeft;
            placeholderText.text = placeholder;
            field.placeholder = placeholderText;
            field.text = string.Empty;
            return field;
        }

        private Button CreateDialogButton(
            RectTransform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            FaaRadarVisualStyle.ApplyRounded(image, FaaRadarVisualStyle.GlassRaised, 9);
            Button button = buttonObject.GetComponent<Button>();
            FaaRadarVisualStyle.ConfigureButton(button, image);
            button.onClick.AddListener(action);
            TMP_Text text = CreateDialogText(rect, "Label", label, 11f, TextColor, FontStyles.Bold);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = size.x > 130f ? 10.5f : 10f;
            return button;
        }

        private void RefreshTargetDialog(bool force = false)
        {
            if (!_targetSetupOpen || _display == null || _targetDialogRoot == null)
            {
                return;
            }

            RadarNavigationTarget preview = _display.CurrentNavigationPreview;
            if (preview.HasGeoPosition)
            {
                _updatingTargetDialog = true;
                if (force || _latitudeInput == null || !_latitudeInput.isFocused)
                {
                    SetCoordinateInputText(
                        _latitudeInput,
                        preview.Latitude.ToString("0.00000", CultureInfo.InvariantCulture));
                }
                if (force || _longitudeInput == null || !_longitudeInput.isFocused)
                {
                    SetCoordinateInputText(
                        _longitudeInput,
                        preview.Longitude.ToString("0.00000", CultureInfo.InvariantCulture));
                }
                _updatingTargetDialog = false;
            }

            if (_targetDialogCoordinates != null)
            {
                _targetDialogCoordinates.text = preview.IsValid && preview.HasGeoPosition
                    ? $"PREVIEW  {FormatCoordinate(preview.Latitude, true)}  {FormatCoordinate(preview.Longitude, false)}"
                    : "PREVIEW  —  TAP THE MAP OR ENTER COORDINATES";
            }

            if (_targetDialogStatus != null && string.IsNullOrEmpty(_targetDialogStatus.text))
            {
                SetTargetDialogStatus("SELECT A POINT TO CONTINUE");
            }

            if (_targetDialogConfirm != null)
            {
                _targetDialogConfirm.interactable = preview.IsValid && preview.HasGeoPosition;
            }
            if (_targetDialogClear != null)
            {
                _targetDialogClear.interactable = _display.HasNavigationTarget;
            }
        }

        private void SetTargetDialogStatus(string message)
        {
            if (_targetDialogStatus != null)
            {
                _targetDialogStatus.text = message;
            }
        }

        private static void SetCoordinateInputText(TMP_InputField field, string value)
        {
            if (field == null)
            {
                return;
            }

            field.text = value ?? string.Empty;
            // TMP_InputField normally toggles its placeholder from its own
            // editing callbacks. Runtime-created fields can be refreshed
            // before that initialization pass, so keep the two graphics
            // explicit and force the label mesh immediately.
            if (field.placeholder != null)
            {
                field.placeholder.gameObject.SetActive(string.IsNullOrEmpty(field.text));
            }

            field.ForceLabelUpdate();
        }

        private void OnCoordinateInputEdited()
        {
            if (_updatingTargetDialog || !_targetSetupOpen || _display == null)
            {
                return;
            }

            if (!TryReadCoordinateInputs(out double latitude, out double longitude))
            {
                SetTargetDialogStatus("ENTER VALID DECIMAL LAT / LON");
                return;
            }

            if (_display.SetNavigationPreview(latitude, longitude, "LAT/LON"))
            {
                SetTargetDialogStatus("COORDINATE PREVIEW READY  ·  CONFIRM TARGET");
                RefreshTargetDialog(true);
            }
            else
            {
                SetTargetDialogStatus("LAT −90…90  ·  LON −180…180");
            }
        }

        private bool TryReadCoordinateInputs(out double latitude, out double longitude)
        {
            latitude = 0d;
            longitude = 0d;
            if (_latitudeInput == null || _longitudeInput == null)
            {
                return false;
            }

            return double.TryParse(
                       _latitudeInput.text,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out latitude) &&
                   double.TryParse(
                       _longitudeInput.text,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out longitude) &&
                   latitude >= -90d && latitude <= 90d &&
                   longitude >= -180d && longitude <= 180d;
        }

        private void NudgeCoordinate(bool latitudeAxis, float delta)
        {
            if (_display == null || !_targetSetupOpen)
            {
                return;
            }

            RadarNavigationTarget preview = _display.CurrentNavigationPreview;
            double latitude = preview.HasGeoPosition ? preview.Latitude : 0d;
            double longitude = preview.HasGeoPosition ? preview.Longitude : 0d;
            if (!preview.HasGeoPosition && !_display.TryGetOwnshipCoordinates(out latitude, out longitude, out _))
            {
                SetTargetDialogStatus("PICK A MAP POINT FIRST");
                return;
            }

            if (latitudeAxis)
            {
                latitude = MathdClamp(latitude + delta, -90d, 90d);
            }
            else
            {
                longitude = WrapLongitude(longitude + delta);
            }

            if (_display.SetNavigationPreview(latitude, longitude, "LAT/LON"))
            {
                SetTargetDialogStatus("ADJUSTED 0.001°  ·  CONFIRM TARGET");
                RefreshTargetDialog(true);
            }
        }

        private static double MathdClamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static double WrapLongitude(double value)
        {
            while (value > 180d) value -= 360d;
            while (value < -180d) value += 360d;
            return value;
        }

        private static string FormatCoordinate(double value, bool latitude)
        {
            string hemi = latitude
                ? value >= 0d ? "N" : "S"
                : value >= 0d ? "E" : "W";
            return $"{Math.Abs(value):0.00000}°{hemi}";
        }

        private void CaptureTargetTapInDisplaySpace(Vector2 hostLocalPoint)
        {
            _hasTargetTapDisplayNormalized = false;
            if (_display == null || _display.DisplayRectTransform == null || _hostRect == null)
            {
                return;
            }

            RectTransform displayRect = _display.DisplayRectTransform;
            Rect displayBounds = displayRect.rect;
            float displayRadius = Mathf.Min(displayBounds.width, displayBounds.height) * 0.5f;
            if (displayRadius <= 1f)
            {
                return;
            }

            Vector3 tapWorld = _hostRect.TransformPoint(hostLocalPoint);
            Vector2 displayLocal = displayRect.InverseTransformPoint(tapWorld);
            Vector2 normalized = (displayLocal - displayBounds.center) / displayRadius;
            if (normalized.sqrMagnitude > 1f)
            {
                normalized = normalized.normalized;
            }

            _targetTapDisplayNormalized = normalized;
            _hasTargetTapDisplayNormalized = true;
        }

        internal void SetFocusedAction(int actionIndex, bool focused)
        {
            ActionKind kind = (ActionKind)Mathf.Clamp(actionIndex, 0, (int)ActionKind.View);
            if (focused)
            {
                _focusedAction = kind;
            }
            else if (_focusedAction.HasValue && _focusedAction.Value == kind && !_selectedAction.HasValue)
            {
                _focusedAction = null;
            }

            RefreshActionLabels();
            UpdateLeaderGeometry();
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
                        action.Title.text = _layoutFocused ? "GUIDE LINES" : "LINES";
                        action.State.text = _display.ReferenceLineworkVisible ? "HIDE" : "SHOW";
                        break;
                    case ActionKind.Map:
                        action.Title.text = _layoutFocused ? "MAP SOURCE" : "MAP";
                        action.State.text = _layoutFocused
                            ? $"{CompactSourceName(_display.MapSourceName)} · SWITCH"
                            : CompactSourceName(_display.MapSourceName);
                        break;
                    case ActionKind.Range:
                        action.Title.text = "RANGE";
                        action.State.text = _layoutFocused
                            ? _display.AutoRangeEnabled
                                ? $"{_display.RangeNM:0} NM · MANUAL"
                                : $"{_display.RangeNM:0} NM · NEXT"
                            : $"{_display.RangeNM:0} NM";
                        break;
                    case ActionKind.Target:
                        bool compactTarget = !_layoutFocused;
                        action.Title.text = compactTarget
                            ? "TARGET"
                            : _display.HasNavigationTarget ? "EDIT TARGET" : "SET TARGET";
                        action.State.text = _display.HasNavigationTarget
                            ? compactTarget
                                ? "FULL · EDIT"
                                : $"{_display.CurrentNavigationTarget.BearingDegrees:000}° · {_display.CurrentNavigationTarget.DistanceNM:0.0} NM"
                            : compactTarget ? "FULL TO SET" : "TAP MAP";
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

                bool selected = _selectedAction.HasValue && _selectedAction.Value == action.Kind;
                bool focused = _focusedAction.HasValue && _focusedAction.Value == action.Kind;
                bool available = action.Kind != ActionKind.Target ||
                                 (_layoutFocused && !_targetSetupOpen);
                if (action.Button != null)
                {
                    action.Button.interactable = available && !_interactionLocked;
                }
                Color actionColor = GetActionColor(action.Kind);
                action.Background.color = selected
                    ? ButtonPressColor
                    : focused ? ButtonFocusColor : ButtonColor;
                if (action.Accent != null)
                {
                    action.Accent.color = new Color(
                        actionColor.r,
                        actionColor.g,
                        actionColor.b,
                        selected || focused ? 1f : 0.40f);
                }
                if (action.IconPlate != null)
                {
                    action.IconPlate.color = new Color(
                        actionColor.r,
                        actionColor.g,
                        actionColor.b,
                        selected ? 0.30f : focused ? 0.22f : 0.11f);
                }
                if (action.Icon != null)
                {
                    action.Icon.color = new Color(
                        actionColor.r,
                        actionColor.g,
                        actionColor.b,
                        selected || focused ? 1f : 0.78f);
                }
                float textAlpha = available ? 1f : 0.42f;
                action.State.color = new Color(
                    (selected || focused ? actionColor : StateColor).r,
                    (selected || focused ? actionColor : StateColor).g,
                    (selected || focused ? actionColor : StateColor).b,
                    (selected || focused ? actionColor : StateColor).a * textAlpha);
                if (action.Title != null)
                {
                    action.Title.color = new Color(TextColor.r, TextColor.g, TextColor.b, textAlpha);
                }
            }

            if (_headerHint != null)
            {
                _headerHint.text = _targetSetupOpen
                    ? "MAP PREVIEW  ·  CONFIRM OR CANCEL"
                    : _layoutFocused
                        ? "TAP ACTION TO APPLY  ·  TAP RADAR TO CLOSE"
                        : "FULL MAP TO SET TARGET  ·  TAP RADAR TO CLOSE";
            }
        }

        private void BeginAction(ActionKind kind)
        {
            if (_interactionLocked || _display == null ||
                (kind == ActionKind.Target && (!_layoutFocused || _targetSetupOpen)))
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
            _focusedAction = kind;
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

            ApplyAction(kind);
            if (kind == ActionKind.View)
            {
                // The radar root changes size and anchor during FULL/REST.
                // Keep the menu open, wait for that animation to settle, then
                // dock the same persistent panel against the new scope bounds.
                yield return new WaitForSecondsRealtime(reducedMotion ? 0.08f : 0.34f);
                LayoutForCurrentRadar(Vector2.zero);
            }

            _targetOpen = true;
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
                case ActionKind.Target:
                    BeginTargetSetup();
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

        private static string ActionIcon(ActionKind kind)
        {
            switch (kind)
            {
                case ActionKind.Linework: return "LN";
                case ActionKind.Map: return "MAP";
                case ActionKind.Range: return "NM";
                case ActionKind.Target: return "TGT";
                case ActionKind.Center: return "AC";
                default: return "FIT";
            }
        }

        private static Color GetActionColor(ActionKind kind)
        {
            switch (kind)
            {
                case ActionKind.Linework: return new Color(0.31f, 0.95f, 0.89f, 1f);
                case ActionKind.Map: return new Color(0.40f, 0.77f, 1f, 1f);
                case ActionKind.Range: return new Color(1f, 0.78f, 0.35f, 1f);
                case ActionKind.Target: return new Color(1f, 0.76f, 0.26f, 1f);
                case ActionKind.Center: return new Color(0.55f, 1f, 0.62f, 1f);
                default: return new Color(0.69f, 0.74f, 1f, 1f);
            }
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
    /// Keeps the action-to-radar relationship discoverable for mouse, touch,
    /// keyboard, and XR ray pointers without making every connector equally
    /// prominent all the time.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class TrafficRadarActionFocus : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler
    {
        private TrafficRadarContextMenu _owner;
        private int _actionIndex;

        public void Configure(TrafficRadarContextMenu owner, int actionIndex)
        {
            _owner = owner;
            _actionIndex = actionIndex;
        }

        private void OnDisable()
        {
            _owner?.SetFocusedAction(_actionIndex, false);
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            _owner?.SetFocusedAction(_actionIndex, true);

        public void OnPointerExit(PointerEventData eventData) =>
            _owner?.SetFocusedAction(_actionIndex, false);

        public void OnSelect(BaseEventData eventData) =>
            _owner?.SetFocusedAction(_actionIndex, true);

        public void OnDeselect(BaseEventData eventData) =>
            _owner?.SetFocusedAction(_actionIndex, false);
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
            public bool Focused;
            public Color Tint;
        }

        private readonly List<Connector> _connectors = new List<Connector>();
        public float Reveal { get; set; }
        public float Pulse { get; set; }
        public float IdleAlpha { get; set; } = 0.24f;

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

        public void SetConnector(
            int index,
            Vector2 source,
            Vector2 target,
            bool visible,
            bool selected,
            bool focused,
            Color tint)
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
                Selected = selected,
                Focused = focused,
                Tint = tint
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
                bool emphasized = connector.Selected || connector.Focused;
                float baseAlpha = connector.Selected ? 1f : connector.Focused ? 0.98f : IdleAlpha;
                Color tint = connector.Tint.a > 0f ? connector.Tint : FaaRadarVisualStyle.Accent;
                Color main = new Color(tint.r, tint.g, tint.b, baseAlpha * stagedReveal);

                IReadOnlyList<Vector2> points = BuildConnectorCurve(connector.Source, connector.Target);
                if (emphasized)
                {
                    Color halo = new Color(0.004f, 0.018f, 0.025f, 0.82f * stagedReveal);
                    DrawPartialPolyline(vertexHelper, points, stagedReveal, 6.2f, halo);
                }
                DrawPartialPolyline(
                    vertexHelper,
                    points,
                    stagedReveal,
                    connector.Selected ? 3.2f : connector.Focused ? 2.8f : 1.25f,
                    main);

                if (stagedReveal >= 0.96f)
                {
                    AddDisc(vertexHelper, connector.Target, emphasized ? 4.2f : 2.1f, main, 14);
                    if (emphasized)
                    {
                        AddRing(
                            vertexHelper,
                            connector.Target,
                            8f,
                            1.4f,
                            new Color(main.r, main.g, main.b, main.a * 0.68f),
                            24);
                    }
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

        private static IReadOnlyList<Vector2> BuildConnectorCurve(Vector2 source, Vector2 target)
        {
            const int segmentCount = 10;
            Vector2[] points = new Vector2[segmentCount + 1];
            float direction = target.x >= source.x ? 1f : -1f;
            Vector2 controlA = source + Vector2.right * (direction * 34f);
            Vector2 controlB = target - Vector2.right * (direction * 52f);
            for (int i = 0; i <= segmentCount; i++)
            {
                float t = i / (float)segmentCount;
                float inverse = 1f - t;
                points[i] = inverse * inverse * inverse * source +
                            3f * inverse * inverse * t * controlA +
                            3f * inverse * t * t * controlB +
                            t * t * t * target;
            }
            return points;
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
