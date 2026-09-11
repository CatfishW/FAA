using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FAA.Customization;
using FAA.XPlaneIntegration.Runtime;
using Newtonsoft.Json;
using TMPro;
using TrafficRadar;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BriefCanvasScaler = UnityEngine.UI.CanvasScaler;

namespace FAA.Explanations
{
    /// <summary>Click-only, lower-center brief dock. No keyboard entry or modal flight-HUD takeover.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ExplanationAssistantController))]
    public sealed class ExplanationAssistantPanel : MonoBehaviour
    {
        public const float DockWidth = 520, DockHeight = 82, ResultHeight = 196, DockBottom = 18, CardGap = 8;
        public const int CanvasOrder = 5400;
        private const int ViewVersion = 3;
        private static readonly Color Ink = new Color(.025f, .061f, .09f, .985f);
        private static readonly Color Card = new Color(.052f, .12f, .16f, 1);
        private static readonly Color Edge = new Color(.19f, .36f, .42f, .85f);
        private static readonly Color Text = new Color(.90f, .96f, .98f, 1);
        private static readonly Color Muted = new Color(.57f, .73f, .79f, 1);
        private static readonly Color Accent = new Color(.35f, .89f, .85f, 1);
        private static readonly Color Amber = new Color(.99f, .76f, .40f, 1);
        private ExplanationAssistantController controller;
        private Canvas canvas;
        private RectTransform dock, result, launcher, viewport, content, progress;
        private CanvasGroup resultGroup;
        private ScrollRect scroll;
        private TMP_Text answer, dockState, titleText, ageText, footerText, refreshText, launcherText, sourcesText, pageText;
        private RectTransform pager;
        private Button previousPage, nextPage;
        private int briefPage = 1, pageCount = 1;
        private TrafficRadarDisplay trafficDisplay;
        private RectTransform[] protectedRects = Array.Empty<RectTransform>();
        private readonly Vector3[] corners = new Vector3[4];
        private readonly List<BriefPlacement.Box> obstacles = new List<BriefPlacement.Box>();
        private float nextLayoutScan;
        private bool layoutBlocked;
        private Image[] actionPlates;
        private Button refreshButton;
        private bool open = true, resultVisible, dirty = true;
        private float reveal, nextRefresh, nextAge;
        private int tab;
        private string activeAction, selectedEvidence, lastRenderKey;
        [SerializeField, HideInInspector] private int builtVersion;
        public bool IsOpen => open;
        public bool IsResultVisible => open && resultVisible && !layoutBlocked;
        public bool LayoutBlocked => layoutBlocked;
        public int BriefPageCount => pageCount;
        public int CurrentBriefPage => briefPage;
        public bool IsWide => false;
        public string ActiveAction => activeAction;
        public bool ReducedMotion { get; private set; }
        public ExplanationAssistantController Controller => controller;
        public RectTransform DockRect => dock;
        public RectTransform ResultRect => result;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            EnsureForScene();
        }
        private static void SceneLoaded(Scene scene, LoadSceneMode mode) => EnsureForScene();
        private static void EnsureForScene()
        {
            if (UnityEngine.Object.FindAnyObjectByType<XPlane12ApiHudBridge>() == null ||
                UnityEngine.Object.FindAnyObjectByType<ExplanationAssistantPanel>() != null) return;
            new GameObject("FAA Explanation Assistant").AddComponent<ExplanationAssistantPanel>();
        }
        private void Awake() => EnsureView();
        private void OnEnable() => EnsureView();
        private void EnsureView()
        {
            controller = GetComponent<ExplanationAssistantController>();
            ReducedMotion = PlayerPrefs.GetInt("FAA.Explanations.ReducedMotion", 0) == 1;
            controller.IncludeImages = PlayerPrefs.GetInt("FAA.Explanations.IncludeImages", 1) == 1;
            controller.Changed -= MarkDirty;
            controller.Changed += MarkDirty;
            if (canvas == null || dock == null || builtVersion != ViewVersion) RebuildView();
            canvas.gameObject.SetActive(true);
        }
        private void OnDisable()
        {
            if (controller != null) controller.Changed -= MarkDirty;
            if (canvas != null) canvas.gameObject.SetActive(false);
        }
        private void OnDestroy()
        {
            if (controller != null) controller.Changed -= MarkDirty;
            if (canvas != null) DestroyOwned(canvas.gameObject);
        }
        private static void DestroyOwned(GameObject owned)
        {
            if (Application.isPlaying) Destroy(owned); else DestroyImmediate(owned);
        }
        private void MarkDirty() => dirty = true;

        public void RebuildView()
        {
            controller = GetComponent<ExplanationAssistantController>();
            if (canvas != null) { canvas.gameObject.SetActive(false); DestroyOwned(canvas.gameObject); }
            Build();
            builtVersion = ViewVersion;
            open = true; resultVisible = false; reveal = 0;
            lastRenderKey = null;
            nextLayoutScan = 0;
            Refresh(); ApplyLayout();
        }
        public void SetOpen(bool value)
        {
            open = value;
            if (!value && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
            ApplyLayout(); dirty = true;
        }
        public void ToggleOpen() => SetOpen(!open);
        public void HideResult() { resultVisible = false; ApplyLayout(); dirty = true; }
        public void ShowResult() { resultVisible = true; SetOpen(true); }
        public void SetTab(int value)
        {
            tab = Mathf.Clamp(value, 0, 3);
            briefPage = 1;
            lastRenderKey = null; ShowResult(); dirty = true;
        }
        public void ToggleMotion()
        {
            ReducedMotion = !ReducedMotion;
            PlayerPrefs.SetInt("FAA.Explanations.ReducedMotion", ReducedMotion ? 1 : 0);
            lastRenderKey = null; dirty = true;
        }
        public void ToggleImageSharing()
        {
            if (controller.IsBusy) return;
            controller.IncludeImages = !controller.IncludeImages;
            PlayerPrefs.SetInt("FAA.Explanations.IncludeImages", controller.IncludeImages ? 1 : 0);
            lastRenderKey = null; dirty = true;
        }
        public void RefreshOrStop()
        {
            if (controller.IsBusy) { controller.Cancel(); return; }
            if (activeAction != null) AskQuick(activeAction);
        }
        public void AskQuick(string action)
        {
            if (!ExplanationPilotActions.TryGetPrompt(action, out string prompt)) return;
            if (controller.IsBusy && activeAction == action) { SetTab(0); return; }
            if (controller.IsBusy) controller.Cancel();
            activeAction = action;
            selectedEvidence = null;
            SetTab(0);
            controller.Ask(prompt);
        }
        public void ChangeBriefPage(int delta)
        {
            if (tab == 0) briefPage = Mathf.Clamp(briefPage + delta, 1, pageCount);
            else
            {
                float overflow = content.rect.height - viewport.rect.height;
                if (overflow > 0) scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - delta * viewport.rect.height * .85f / overflow);
            }
            UpdatePages();
        }

        public static Vector2 CompactSize(Vector2 canvasSize) =>
            new Vector2(Mathf.Min(DockWidth, Mathf.Max(280, canvasSize.x - 32)), ResultHeight);

        private void Update()
        {
            if (dock == null) return;
            if (open && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (resultVisible) HideResult(); else SetOpen(false);
            }
            reveal = ReducedMotion ? (IsResultVisible ? 1 : 0) :
                Mathf.MoveTowards(reveal, IsResultVisible ? 1 : 0, Time.unscaledDeltaTime * 6);
            if (controller.IsBusy && !ReducedMotion)
            {
                float left = Mathf.PingPong(Time.unscaledTime * .2f, .72f);
                progress.anchorMin = new Vector2(left, 0); progress.anchorMax = new Vector2(left + .28f, 0);
            }
            else
            {
                progress.anchorMin = Vector2.zero;
                progress.anchorMax = new Vector2(controller.IsBusy ? .45f : 1, 0);
            }
            progress.offsetMin = Vector2.zero; progress.offsetMax = new Vector2(0, 2);
            if (dirty && Time.unscaledTime >= nextRefresh)
            { nextRefresh = Time.unscaledTime + .08f; Refresh(); }
            if (Time.unscaledTime >= nextAge)
            {
                nextAge = Time.unscaledTime + 1;
                int seconds = controller.Snapshot == null ? 0 : Math.Max(0, (int)(DateTime.UtcNow - controller.Snapshot.CapturedUtc).TotalSeconds);
                ageText.text = controller.Snapshot == null ? "Read-only simulation assistant · choose an action below" :
                    "AS OF " + controller.Snapshot.CapturedUtc.ToString("HH:mm:ss 'UTC'") + " · " + seconds + "s old" +
                    (seconds >= 30 ? " · REFRESH FOR CURRENT DATA" : " · not live");
                ageText.color = seconds >= 30 ? Amber : Muted;
            }
        }
        private void LateUpdate() { ApplyLayout(); UpdatePages(); }

        private void CollectObstacles()
        {
            if (trafficDisplay == null) trafficDisplay = UnityEngine.Object.FindAnyObjectByType<TrafficRadarDisplay>();
            if (Time.unscaledTime >= nextLayoutScan || protectedRects.Length == 0)
            {
                nextLayoutScan = Time.unscaledTime + 1;
                protectedRects = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(r => !r.IsChildOf(transform) && (r.name == "TrafficControlStrip" || r.name == "WeatherControlStrip" ||
                        r.name == "Screen cue controls" || r.name == "Screen cue symbol key" || r.name == "Radar Status Header" ||
                        (r.name == "ActionPanel" && r.parent != null && r.parent.name == "TrafficRadarQuickMenu") ||
                        r.name == "FAA Heading Tape Overlay" || r.name == "Heading Tape Clip" ||
                        IsFlightInstrument(r) ||
                        r.GetComponent<FaaRadarPresentation>() != null || r.GetComponent<FaaRadarConfigurationDrawer>() != null))
                    .ToArray();
            }
            obstacles.Clear();
            if (trafficDisplay != null && trafficDisplay.InstrumentDisplayEnabled)
                AddObstacle(trafficDisplay.DisplayRectTransform);
            foreach (var rect in protectedRects) AddObstacle(rect);
            var size = ((RectTransform)canvas.transform).rect.size;
            if (trafficDisplay == null || !trafficDisplay.IsFullscreen)
                obstacles.Add(new BriefPlacement.Box(size.x * .25f, size.y * .3f, size.x * .5f, size.y * .7f));
        }
        private void AddObstacle(RectTransform rect)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return;
            foreach (CanvasGroup group in rect.GetComponentsInParent<CanvasGroup>())
                if (group.alpha < .02f) return;
            Canvas sourceCanvas = rect.GetComponentInParent<Canvas>();
            if (sourceCanvas != null && !sourceCanvas.enabled) return;
            Camera camera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? sourceCanvas.worldCamera : null;
            var target = (RectTransform)canvas.transform;
            rect.GetWorldCorners(corners);
            if (IsFlightInstrument(rect))
            {
                // Several HUD roots use small world-unit rects with enlarged child
                // labels. Protect the drawn bounds, including the VSI readout.
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, rect);
                corners[0] = rect.TransformPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
                corners[1] = rect.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.center.z));
                corners[2] = rect.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
                corners[3] = rect.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.center.z));
            }
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 point in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, point);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(target, screen, null, out Vector2 local);
                local -= target.rect.min;
                min = Vector2.Min(min, local); max = Vector2.Max(max, local);
            }
            if (max.x - min.x >= 1 && max.y - min.y >= 1)
                obstacles.Add(new BriefPlacement.Box(min.x, min.y, max.x - min.x, max.y - min.y));
        }
        private static bool IsFlightInstrument(RectTransform rect) =>
            rect.GetComponent<FaaEngineInstrumentGraphic>() != null ||
            rect.GetComponent<FaaNavigationScaleGraphic>() != null ||
            rect.GetComponent<FaaPrimaryFlightReadout>() != null;
        private void ApplyLayout()
        {
            if (canvas == null || dock == null) return;
            CollectObstacles();
            var size = ((RectTransform)canvas.transform).rect.size;
            float width = CompactSize(size).x;
            bool mapOpen = trafficDisplay != null && trafficDisplay.IsFullscreen;
            // Reserve the complete stack even while collapsed, preventing a jump on each action.
            float stack = DockHeight + CardGap + ResultHeight;
            bool fits = BriefPlacement.TryPlace(size.x, size.y, width, stack, mapOpen, obstacles, out var placement);
            if (!fits && width > 440)
            { width = 440; fits = BriefPlacement.TryPlace(size.x, size.y, width, stack, mapOpen, obstacles, out placement); }
            bool wasBlocked = layoutBlocked;
            layoutBlocked = !fits;
            if (wasBlocked != layoutBlocked) dirty = true;
            bool launcherFits = fits;
            var launchPlacement = placement;
            if (!fits) launcherFits = BriefPlacement.TryPlace(size.x, size.y, 145, 34, mapOpen, obstacles, out launchPlacement);
            Vector2 origin = new Vector2(placement.X + width * .5f - size.x * .5f, placement.Y);
            dock.sizeDelta = new Vector2(width, DockHeight);
            dock.anchoredPosition = origin;
            result.sizeDelta = new Vector2(width, ResultHeight);
            float ease = reveal * reveal * (3 - 2 * reveal);
            result.anchoredPosition = origin + new Vector2(0, DockHeight + CardGap);
            // Fade in place: never slide a card across protected map/instrument pixels.
            resultGroup.alpha = ease;
            resultGroup.interactable = resultGroup.blocksRaycasts = IsResultVisible;
            launcher.anchoredPosition = fits ? origin : new Vector2(launchPlacement.X + 72.5f - size.x * .5f, launchPlacement.Y);
            dock.gameObject.SetActive(open && fits);
            launcher.gameObject.SetActive((!open || !fits) && launcherFits);
            result.gameObject.SetActive(IsResultVisible);
        }

        private void Build()
        {
            var root = new GameObject("Pilot Brief Canvas", typeof(RectTransform), typeof(Canvas), typeof(BriefCanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = CanvasOrder;
            var scaler = root.GetComponent<BriefCanvasScaler>(); scaler.uiScaleMode = BriefCanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;

            launcher = BottomCenter(canvas.transform, "Pilot brief launcher", 145, 34, DockBottom);
            Plate(launcher, Ink, true); Action(launcher, ToggleOpen);
            Glyph(launcher, "Brief symbol", ExplanationGlyph.Kind.Spark, 12, 7, 20, Accent);
            launcherText = Label(launcher, "Label", "PILOT BRIEF", 12, Text); Place(launcherText.rectTransform, 40, 0, 101, 34);

            dock = BottomCenter(canvas.transform, "Pilot brief action dock", DockWidth, DockHeight, DockBottom);
            Plate(dock, Ink, true);
            FaaRadarVisualStyle.EnsureDropShadow(dock.gameObject, new Color(0, 0, 0, .3f), new Vector2(0, -4));
            Glyph(dock, "Brief symbol", ExplanationGlyph.Kind.Spark, 13, 7, 17, Accent);
            var heading = Label(dock, "Heading", "PILOT BRIEF", 10, Text); Place(heading.rectTransform, 36, 4, 94, 23); heading.fontStyle = FontStyles.Bold;
            dockState = Label(dock, "State", "", 10, Muted); StretchTop(dockState.rectTransform, 138, 106, 4, 23);
            SmallButton(dock, "Brief options", "Options", 45, 5, 56, () => SetTab(3));
            SmallButton(dock, "Hide brief dock", "−", 8, 5, 26, ToggleOpen);

            var quick = Rect(dock, "Pilot actions"); StretchTop(quick, 10, 10, 32, 40);
            var horizontal = quick.gameObject.AddComponent<HorizontalLayoutGroup>(); horizontal.spacing = 6;
            horizontal.childControlWidth = horizontal.childControlHeight = horizontal.childForceExpandWidth = horizontal.childForceExpandHeight = true;
            FaaRadarIcon[] icons = { FaaRadarIcon.AircraftAirliner, FaaRadarIcon.WeatherRainModerate, FaaRadarIcon.Map, FaaRadarIcon.Lines };
            actionPlates = new Image[ExplanationPilotActions.Names.Count];
            for (int i = 0; i < ExplanationPilotActions.Names.Count; i++)
            {
                string action = ExplanationPilotActions.Names[i];
                var item = Rect(quick, action + " brief"); actionPlates[i] = Plate(item, Card, false); Action(item, () => AskQuick(action));
                var icon = Rect(item, "SVG icon"); Place(icon, 10, 9, 22, 22);
                var graphic = icon.gameObject.AddComponent<FaaSvgIconGraphic>(); graphic.SetIcon(icons[i]); graphic.color = Accent; graphic.raycastTarget = false;
                var label = Label(item, "Label", action, 13, Text); StretchTop(label.rectTransform, 38, 5, 0, 40);
            }

            result = BottomCenter(canvas.transform, "Pilot brief result", DockWidth, ResultHeight, DockBottom + DockHeight + CardGap);
            Plate(result, Ink, true);
            FaaRadarVisualStyle.EnsureDropShadow(result.gameObject, new Color(0, 0, 0, .34f), new Vector2(0, -4));
            resultGroup = result.gameObject.AddComponent<CanvasGroup>();
            var headingButton = Rect(result, "Back to brief"); StretchTop(headingButton, 14, 205, 9, 22);
            var headingHit = headingButton.gameObject.AddComponent<Image>(); headingHit.color = Color.clear; Action(headingButton, () => SetTab(0));
            titleText = Label(headingButton, "Title", "", 13, Text); Fill(titleText.rectTransform, 0); titleText.fontStyle = FontStyles.Bold;
            sourcesText = SmallButton(result, "Brief sources", "Sources", 86, 7, 60, () => SetTab(1));
            SmallButton(result, "Brief tools", "Tools", 38, 7, 43, () => SetTab(2));
            SmallButton(result, "Collapse brief result", "−", 8, 7, 26, HideResult);
            ageText = Label(result, "Snapshot time", "", 10, Muted); StretchTop(ageText.rectTransform, 14, 14, 32, 16);

            viewport = Rect(result, "Brief scroll viewport"); Fill(viewport, 0);
            viewport.offsetMin = new Vector2(14, 36); viewport.offsetMax = new Vector2(-14, -53);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, .001f);
            scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 26; scroll.viewport = viewport;
            content = Rect(viewport, "Content"); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var vertical = content.gameObject.AddComponent<VerticalLayoutGroup>(); vertical.spacing = 8;
            vertical.childControlWidth = vertical.childControlHeight = vertical.childForceExpandWidth = true; vertical.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            answer = Label(viewport, "Streamed brief", "", 14.5f, Text); Fill(answer.rectTransform, 0);
            answer.alignment = TextAlignmentOptions.TopLeft; answer.textWrappingMode = TextWrappingModes.Normal;
            answer.overflowMode = TextOverflowModes.Page; answer.richText = true; answer.lineSpacing = 3;
            var links = answer.gameObject.AddComponent<ExplanationCitationLink>(); links.Text = answer;
            links.Clicked = id => { selectedEvidence = id; SetTab(1); };
            pager = Rect(result, "Brief pages"); StretchBottom(pager, 14, 108, 7, 26);
            previousPage = PageButton(pager, "Previous page", "‹", 0, () => ChangeBriefPage(-1));
            nextPage = PageButton(pager, "Next page", "›", 100, () => ChangeBriefPage(1));
            pageText = Label(pager, "Page count", "", 10, Accent); Place(pageText.rectTransform, 32, 0, 66, 26); pageText.alignment = TextAlignmentOptions.Center;
            footerText = Label(result, "Brief limitation", "", 10, Muted); StretchBottom(footerText.rectTransform, 152, 108, 9, 21);
            var refresh = Rect(result, "Refresh or stop brief"); refresh.anchorMin = refresh.anchorMax = new Vector2(1, 0);
            refresh.pivot = new Vector2(1, 0); refresh.anchoredPosition = new Vector2(-10, 7); refresh.sizeDelta = new Vector2(82, 26);
            Plate(refresh, Card, false); refreshButton = Action(refresh, RefreshOrStop);
            refreshText = Label(refresh, "Label", "Refresh", 11, Accent); Fill(refreshText.rectTransform, 0); refreshText.alignment = TextAlignmentOptions.Center;
            progress = Rect(result, "Streaming progress"); Plate(progress, Accent, false);
        }

        private string ShortState()
        {
            switch (controller.State)
            {
                case ExplanationRunState.Capturing: return "Capturing snapshot";
                case ExplanationRunState.Connecting: return "Connecting…";
                case ExplanationRunState.UsingTools: return "Reading evidence…";
                case ExplanationRunState.Streaming: return "Streaming brief…";
                case ExplanationRunState.Complete: return "Brief ready · snapshot";
                case ExplanationRunState.Unverified: return "Check sources";
                case ExplanationRunState.Cancelled: return "Stopped";
                case ExplanationRunState.Failed: return "Brief unavailable";
                default: return "Tap an action · AI snapshot";
            }
        }
        private void Refresh()
        {
            dirty = false;
            bool caution = controller.State == ExplanationRunState.Failed || controller.State == ExplanationRunState.Cancelled || controller.State == ExplanationRunState.Unverified;
            dockState.text = ShortState(); dockState.color = caution ? Amber : Muted;
            launcherText.text = layoutBlocked ? "BRIEF · NO ROOM" : controller.IsBusy ? "BRIEF WORKING" : "PILOT BRIEF";
            titleText.text = tab == 1 ? "‹ Brief / Sources" : tab == 2 ? "‹ Brief / Tools" :
                tab == 3 ? "‹ Brief / Options" : activeAction == "Chart" ? "Chart · AI interpretation" : (activeAction ?? "Pilot") + " brief";
            sourcesText.text = "Sources " + (controller.Snapshot?.DeliveredIds.Count ?? 0);
            sourcesText.color = tab == 1 ? Accent : Muted;
            footerText.text = controller.IsBusy ? ShortState() + " · provisional" :
                controller.State == ExplanationRunState.Unverified ? "Source links unresolved · verify response" :
                "SIM ONLY · NOT GUIDANCE";
            footerText.color = caution ? Amber : Muted;
            refreshText.text = controller.IsBusy ? "Stop" : controller.State == ExplanationRunState.Failed || controller.State == ExplanationRunState.Cancelled ? "Retry" : "Refresh";
            refreshButton.interactable = controller.IsBusy || activeAction != null;
            for (int i = 0; i < actionPlates.Length; i++)
                actionPlates[i].color = IsResultVisible && activeAction == ExplanationPilotActions.Names[i] ? new Color(.09f, .29f, .31f, 1) : Card;
            string key = tab + ":" + (tab == 1 ? controller.Snapshot?.DeliveredIds.Count ?? 0 : tab == 2 ? controller.Activity.Count : 0) +
                ":" + selectedEvidence + ":" + controller.IncludeImages + ":" + ReducedMotion + ":" + controller.IsBusy;
            if (key != lastRenderKey) { lastRenderKey = key; RebuildContent(); }
            if (tab == 0 && answer != null)
            {
                string body = controller.Answer;
                if (string.IsNullOrEmpty(body))
                    body = controller.IsBusy ? "Reading the captured evidence. Your brief will appear here." :
                        controller.State == ExplanationRunState.Failed || controller.State == ExplanationRunState.Cancelled ? controller.Status :
                        "Choose Traffic, Weather, Chart or Status below.\nOne tap captures a fresh snapshot.";
                if (controller.State == ExplanationRunState.Unverified) body = "SOURCE CHECK NEEDED\n" + body;
                if (controller.State == ExplanationRunState.Cancelled && !string.IsNullOrEmpty(controller.Answer)) body = "STOPPED · INCOMPLETE\n" + body;
                if (controller.State == ExplanationRunState.Failed && !string.IsNullOrEmpty(controller.Answer)) body = "INCOMPLETE · " + controller.Status + "\n" + body;
                answer.text = FormatAnswer(body);
            }
        }

        private void RebuildContent()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            { var child = content.GetChild(i).gameObject; child.SetActive(false); DestroyOwned(child); }
            answer.gameObject.SetActive(tab == 0);
            content.gameObject.SetActive(tab != 0);
            scroll.enabled = tab != 0;
            if (tab == 0)
            {
                // Fixed-height paged text keeps every line accessible without enlarging the card.
                answer.pageToDisplay = briefPage;
            }
            else if (tab == 1)
            {
                TextBlock("Source caution", "Captured values, not live instruments. Tap a source to inspect its values.", 11, Muted);
                var snapshot = controller.Snapshot;
                if (snapshot == null || snapshot.DeliveredIds.Count == 0)
                    TextBlock("No sources", "No sources returned yet. Choose an action below.", 13, Text);
                else foreach (var evidence in snapshot.Records.Values.Where(e => snapshot.DeliveredIds.Contains(e.Id)))
                {
                    string id = evidence.Id;
                    var item = Rect(content, "Evidence " + id); Plate(item, Card, false);
                    var layout = item.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(10, 10, 8, 8); layout.spacing = 4;
                    layout.childControlWidth = layout.childControlHeight = layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                    Action(item, () => { selectedEvidence = selectedEvidence == id ? null : id; lastRenderKey = null; dirty = true; });
                    var title = FlowLabel(item, "Title", "[" + id + "]  " + evidence.Title, 12, Accent); title.fontStyle = FontStyles.Bold;
                    FlowLabel(item, "State", evidence.State + " · source age " + (evidence.SourceAgeSeconds.HasValue ? evidence.SourceAgeSeconds.Value.ToString("0.0") + "s" : "unknown"), 11, Muted);
                    if (selectedEvidence == id)
                    {
                        FlowLabel(item, "Source", evidence.Source + " · " + evidence.CapturedUtc.ToString("HH:mm:ss 'UTC'"), 11, Muted);
                        FlowLabel(item, "Raw evidence", evidence.Data.ToString(Formatting.Indented), 12, Text);
                    }
                }
                TextBlock("Citation audit", controller.CitationAudit, 11, Muted);
            }
            else if (tab == 2)
            {
                TextBlock("Tools limitation", "Read-only evidence tools · no aircraft control", 11, Accent);
                if (controller.Activity.Count == 0) TextBlock("No activity", "No tools have run. Opening this panel sends no data.", 13, Text);
                foreach (string activity in controller.Activity) TextBlock("Tool event", activity, 12, Text);
                TextBlock("Tool bounds", "Up to 4 model rounds / 8 tool calls. No shell, arbitrary URLs or flight-control tools.\nModel: " + controller.Model, 11, Muted);
            }
            else
            {
                var toggles = Rect(content, "Brief preferences");
                toggles.gameObject.AddComponent<LayoutElement>().preferredHeight = 35;
                var horizontal = toggles.gameObject.AddComponent<HorizontalLayoutGroup>(); horizontal.spacing = 8;
                horizontal.childControlWidth = horizontal.childControlHeight = horizontal.childForceExpandWidth = horizontal.childForceExpandHeight = true;
                var images = Rect(toggles, "Image sharing"); Plate(images, Card, false);
                var imagesButton = Action(images, ToggleImageSharing); imagesButton.interactable = !controller.IsBusy;
                var imageLabel = Label(images, "Label", "Chart / radar images " + (controller.IncludeImages ? "ON" : "OFF"), 11, controller.IncludeImages ? Accent : Muted);
                Fill(imageLabel.rectTransform, 8);
                var motion = Rect(toggles, "Motion preference"); Plate(motion, Card, false); Action(motion, ToggleMotion);
                var motionLabel = Label(motion, "Label", ReducedMotion ? "Motion OFF" : "Motion ON", 11, Muted); Fill(motionLabel.rectTransform, 8);
                TextBlock("Sharing disclosure", "Each action sends a fresh snapshot and enabled chart/radar crops to subtoken.shop. No whole-screen image or aircraft-control access.", 11, Muted);
            }
            Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1;
            if (tab == 1 && !string.IsNullOrEmpty(selectedEvidence) && content.Find("Evidence " + selectedEvidence) is RectTransform selected)
            {
                float overflow = content.rect.height - viewport.rect.height;
                float top = -content.InverseTransformPoint(selected.TransformPoint(new Vector3(0, selected.rect.yMax, 0))).y;
                if (overflow > 0) scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01(top / overflow);
            }
        }

        private void UpdatePages()
        {
            if (!IsResultVisible || answer == null) return;
            if (tab == 0)
            {
                answer.ForceMeshUpdate();
                pageCount = Math.Max(1, answer.textInfo.pageCount);
                briefPage = Mathf.Clamp(briefPage, 1, pageCount);
                answer.pageToDisplay = briefPage;
                pageText.text = briefPage + " / " + pageCount;
                previousPage.interactable = briefPage > 1;
                nextPage.interactable = briefPage < pageCount;
            }
            else
            {
                float overflow = content.rect.height - viewport.rect.height;
                bool more = overflow > 1;
                pageText.text = more ? "SCROLL" : "ALL";
                previousPage.interactable = more && scroll.verticalNormalizedPosition < .999f;
                nextPage.interactable = more && scroll.verticalNormalizedPosition > .001f;
            }
        }

        private string FormatAnswer(string value)
        {
            string escaped = value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("**", "");
            escaped = Regex.Replace(escaped, @"(?m)^(\s*[•*-]?\s*)(Picture|Context|Limit):", m =>
                m.Groups[1].Value + "<b><color=" + (m.Groups[2].Value == "Limit" ? "#FCC266" : "#59E3D9") + ">" +
                m.Groups[2].Value + ":</color></b>");
            return Regex.Replace(escaped, @"\[(E\d+)\]", m => controller.Snapshot != null && controller.Snapshot.DeliveredIds.Contains(m.Groups[1].Value)
                ? "<link=\"" + m.Groups[1].Value + "\"><color=#59E3D9><u>" + m.Value + "</u></color></link>" : m.Value);
        }
        private TMP_Text TextBlock(string name, string text, float size, Color color) => FlowLabel(content, name, text, size, color);
        private static TMP_Text FlowLabel(Transform parent, string name, string text, float size, Color color)
        {
            var label = Label(parent, name, text, size, color); label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Overflow;
            label.gameObject.AddComponent<LayoutElement>().minHeight = size * 1.4f; return label;
        }
        private static RectTransform Rect(Transform parent, string name)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform; }
        private static RectTransform BottomCenter(Transform parent, string name, float width, float height, float bottom)
        {
            var rt = Rect(parent, name); rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0); rt.pivot = new Vector2(.5f, 0);
            rt.anchoredPosition = new Vector2(0, bottom); rt.sizeDelta = new Vector2(width, height); return rt;
        }
        private static TMP_Text Label(Transform parent, string name, string text, float size, Color color)
        {
            var rt = Rect(parent, name); var label = rt.gameObject.AddComponent<TextMeshProUGUI>(); label.text = text; label.fontSize = size;
            label.font = TMP_Settings.defaultFontAsset; label.color = color; label.richText = false; label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.MidlineLeft; label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; return label;
        }
        private static Image Plate(RectTransform rt, Color color, bool border)
        {
            var image = rt.gameObject.AddComponent<Image>(); FaaRadarVisualStyle.ApplyRounded(image, color, 10); image.raycastTarget = true;
            if (border) { var outline = rt.gameObject.AddComponent<Outline>(); outline.effectColor = Edge; outline.effectDistance = new Vector2(.8f, -.8f); }
            return image;
        }
        private static Button Action(RectTransform rt, UnityEngine.Events.UnityAction action)
        {
            var button = rt.gameObject.AddComponent<Button>(); FaaRadarVisualStyle.ConfigureButton(button, rt.GetComponent<Image>());
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic }; button.onClick.AddListener(action); return button;
        }
        private static Button PageButton(Transform parent, string name, string text, float left, UnityEngine.Events.UnityAction action)
        {
            var rt = Rect(parent, name); Place(rt, left, 0, 30, 26); Plate(rt, Card, false);
            var button = Action(rt, action);
            var label = Label(rt, "Label", text, 19, Text); Fill(label.rectTransform, 0); label.alignment = TextAlignmentOptions.Center;
            return button;
        }
        private static TMP_Text SmallButton(Transform parent, string name, string text, float right, float top, float width, UnityEngine.Events.UnityAction action)
        {
            var rt = Rect(parent, name); rt.anchorMin = rt.anchorMax = Vector2.one; rt.pivot = Vector2.one;
            rt.anchoredPosition = new Vector2(-right, -top); rt.sizeDelta = new Vector2(width, 24); Plate(rt, Card, false); Action(rt, action);
            var label = Label(rt, "Label", text, 10, Muted); Fill(label.rectTransform, 0); label.alignment = TextAlignmentOptions.Center; return label;
        }
        private static void Place(RectTransform rt, float left, float top, float width, float height)
        { rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(left, -top); rt.sizeDelta = new Vector2(width, height); }
        private static void Fill(RectTransform rt, float pad)
        { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad); }
        private static void StretchTop(RectTransform rt, float left, float right, float top, float height)
        { rt.anchorMin = new Vector2(0, 1); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(.5f, 1); rt.offsetMin = new Vector2(left, -top - height); rt.offsetMax = new Vector2(-right, -top); }
        private static void StretchBottom(RectTransform rt, float left, float right, float bottom, float height)
        { rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(.5f, 0); rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, bottom + height); }
        private static ExplanationGlyph Glyph(Transform parent, string name, ExplanationGlyph.Kind kind, float left, float top, float size, Color color)
        { var rt = Rect(parent, name); Place(rt, left, top, size, size); var glyph = rt.gameObject.AddComponent<ExplanationGlyph>(); glyph.color = color; glyph.Set(kind); glyph.raycastTarget = false; return glyph; }
    }
}
