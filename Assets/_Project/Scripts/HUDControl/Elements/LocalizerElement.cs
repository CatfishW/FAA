using UnityEngine;
using UnityEngine.UI;
using AircraftControl.Core;
using TrafficRadar;

namespace HUDControl.Elements
{
    /// <summary>
    /// Localizer CDI element for Image-based HUD.
    /// Animates CDI needle horizontal position.
    /// All animations have strict bounds.
    /// </summary>
    [AddComponentMenu("HUD Control/Elements/Localizer")]
    public class LocalizerElement : Core.HUDElementBase
    {
        #region Inspector - UI References
        
        [Header("CDI References")]
        [Tooltip("CDI needle that moves horizontally")]
        [SerializeField] private RectTransform cdiNeedle;
        
        [Tooltip("Deviation dots panel (optional)")]
        [SerializeField] private RectTransform deviationDotsPanel;
        
        #endregion
        
        #region Inspector - Animation Enables
        
        [Header("Animation Enables")]
        [Tooltip("Enable CDI needle movement")]
        [SerializeField] private bool enableCDI = true;
        
        #endregion
        
        #region Inspector - Bounds
        
        [Header("CDI Bounds")]
        [Tooltip("Pixels per dot of deviation")]
        [SerializeField] private float pixelsPerDot = 10f;
        
        [Tooltip("Maximum CDI offset in pixels (KEEP SMALL)")]
        [SerializeField] private float maxCDIOffsetPixels = 20f;
        
        [Tooltip("Simulate deviation (for testing)")]
        [SerializeField] private bool simulateDeviation = true;
        
        [Tooltip("Simulated deviation amount (-2.5 to 2.5 dots)")]
        [Range(-2.5f, 2.5f)]
        [SerializeField] private float simulatedDeviation = 0f;
        
        #endregion

        #region Inspector - Navigation Target Cue

        [Header("Navigation Target Cue")]
        [Tooltip("Traffic radar display that supplies the selected map target. Leave empty to auto-find the active display.")]
        [SerializeField] private TrafficRadarDisplay navigationDisplay;

        [Tooltip("Show the selected map target as an amber cue on the localizer bar.")]
        [SerializeField] private bool showNavigationTargetCue = true;

        [Tooltip("Relative bearing represented by the full localizer bar, in degrees left/right of the aircraft nose.")]
        [Range(30f, 90f)]
        [SerializeField] private float navigationTargetBearingWindow = 60f;

        [Tooltip("Accent used for a selected map target.")]
        [SerializeField] private Color navigationTargetColor = new Color(1f, 0.78f, 0.28f, 1f);

        [Tooltip("Pulse speed for the selected target cue.")]
        [Min(0f)]
        [SerializeField] private float navigationTargetPulseSpeed = 0.75f;

        #endregion
        
        private float displayedDeviation;
        private float targetDeviation;
        private Vector2 cdiBasePos;
        private NavigationTargetCueGraphic navigationTargetCue;
        private float navigationTargetPulse;
        
        public override string ElementId => "Localizer";
        
        protected override void OnInitialize()
        {
            displayedDeviation = 0f;
            targetDeviation = Mathf.Clamp(simulatedDeviation, -2.5f, 2.5f);
            
            if (cdiNeedle != null)
                cdiBasePos = cdiNeedle.anchoredPosition;

            EnsureNavigationTargetCue();
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            if (enableCDI && cdiNeedle != null)
            {
                float desiredDeviation = simulateDeviation ? simulatedDeviation : targetDeviation;
                displayedDeviation = Core.HUDAnimator.SmoothValue(displayedDeviation, desiredDeviation, smoothing);

                // Calculate offset with strict bounds
                float offset = displayedDeviation * pixelsPerDot;
                offset = Mathf.Clamp(offset, -maxCDIOffsetPixels, maxCDIOffsetPixels);

                Vector2 newPos = cdiBasePos;
                newPos.x += offset;
                cdiNeedle.anchoredPosition = newPos;
            }

            UpdateNavigationTargetCue();
        }

        private void Update()
        {
            // The X-Plane bridge normally calls UpdateElement, but keeping the
            // cue alive here also covers editor previews and scenes where the
            // HUD controller is intentionally disabled.
            if (isInitialized)
            {
                UpdateNavigationTargetCue();
            }
        }

        public void SetNavigationDisplay(TrafficRadarDisplay display)
        {
            navigationDisplay = display;
            UpdateNavigationTargetCue();
        }

        private void EnsureNavigationTargetCue()
        {
            // Every generated deviation dot carries a copy of this component
            // with a null CDI reference. Only the actual localizer root should
            // create a target cue.
            if (!showNavigationTargetCue || cdiNeedle == null)
            {
                return;
            }

            if (navigationTargetCue == null)
            {
                Transform existing = transform.Find("FAA Navigation Target Cue");
                GameObject cueObject = existing != null
                    ? existing.gameObject
                    : new GameObject(
                        "FAA Navigation Target Cue",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(NavigationTargetCueGraphic));
                cueObject.transform.SetParent(transform, false);
                RectTransform cueRect = cueObject.GetComponent<RectTransform>() ??
                                        cueObject.AddComponent<RectTransform>();
                cueRect.anchorMin = Vector2.zero;
                cueRect.anchorMax = Vector2.one;
                cueRect.offsetMin = Vector2.zero;
                cueRect.offsetMax = Vector2.zero;
                cueRect.pivot = new Vector2(0.5f, 0.5f);
                cueRect.localScale = Vector3.one;
                cueRect.localRotation = Quaternion.identity;
                navigationTargetCue = cueObject.GetComponent<NavigationTargetCueGraphic>() ??
                                       cueObject.AddComponent<NavigationTargetCueGraphic>();
                navigationTargetCue.raycastTarget = false;
            }

            navigationTargetCue.transform.SetAsLastSibling();
        }

        private void UpdateNavigationTargetCue()
        {
            if (!showNavigationTargetCue || cdiNeedle == null)
            {
                if (navigationTargetCue != null)
                {
                    navigationTargetCue.SetTarget(Vector2.zero, false, false, navigationTargetColor, 0f);
                }

                return;
            }

            EnsureNavigationTargetCue();
            if (navigationTargetCue == null)
            {
                return;
            }

            if (navigationDisplay == null)
            {
                navigationDisplay = FindAnyObjectByType<TrafficRadarDisplay>();
                if (navigationDisplay == null)
                {
                    navigationDisplay = FindAnyObjectByType<TrafficRadarDisplay>(FindObjectsInactive.Include);
                }
            }

            if (navigationDisplay == null ||
                !navigationDisplay.HasNavigationTarget ||
                !navigationDisplay.ShowNavigationTarget)
            {
                navigationTargetCue.SetTarget(Vector2.zero, false, false, navigationTargetColor, 0f);
                return;
            }

            RadarNavigationTarget target = navigationDisplay.CurrentNavigationTarget;
            float window = Mathf.Max(30f, navigationTargetBearingWindow);
            float relativeBearing = Mathf.DeltaAngle(0f, target.RelativeBearingDegrees);
            bool edgeClamped = Mathf.Abs(relativeBearing) > window;
            float normalized = Mathf.Clamp(relativeBearing / window, -1f, 1f);
            navigationTargetPulse += Time.unscaledDeltaTime * Mathf.Clamp(navigationTargetPulseSpeed, 0f, 0.85f);
            Color tint = target.IsOffscreen
                ? new Color(1f, 0.62f, 0.18f, 1f)
                : navigationTargetColor;
            navigationTargetCue.SetTarget(
                new Vector2(normalized, 0f),
                true,
                edgeClamped,
                tint,
                navigationTargetPulse);
        }
        
        public void SetDeviation(float dots)
        {
            simulateDeviation = false;
            targetDeviation = Mathf.Clamp(dots, -2.5f, 2.5f);
        }
        
        public float GetDisplayedDeviation() => displayedDeviation;
    }

    /// <summary>
    /// Resolution-independent selected-target cue used by the compact
    /// localizer and glidescope bars. The parent rectangle determines the
    /// orientation, so the same graphic works with normalized HUD prefabs and
    /// pixel-sized screen-space canvases.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NavigationTargetCueGraphic : MaskableGraphic
    {
        private Vector2 normalizedPosition;
        private bool visible;
        private bool edgeClamped;
        private Color accent = new Color(1f, 0.78f, 0.28f, 1f);
        private float pulse;

        public void SetTarget(
            Vector2 normalized,
            bool show,
            bool clamped,
            Color tint,
            float pulsePhase)
        {
            normalizedPosition = new Vector2(
                Mathf.Clamp(normalized.x, -1f, 1f),
                Mathf.Clamp(normalized.y, -1f, 1f));
            visible = show;
            edgeClamped = clamped;
            accent = tint.a > 0f ? tint : new Color(1f, 0.78f, 0.28f, 1f);
            pulse = pulsePhase;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (!visible)
            {
                return;
            }

            Rect rect = rectTransform.rect;
            float width = Mathf.Max(0.001f, rect.width);
            float height = Mathf.Max(0.001f, rect.height);
            float minimum = Mathf.Min(width, height);
            // Keep the cue subordinate to the existing CDI/GS symbology. The
            // previous 24% radius and 2.2x pulse rate read as a flashing
            // oversized waypoint in the XR-3 view.
            float radius = Mathf.Max(0.0025f, minimum * 0.14f);
            float stroke = Mathf.Max(0.0012f, minimum * 0.034f);
            bool horizontal = width >= height;
            Vector2 center = rect.center;
            float travel = (horizontal ? width : height) * 0.5f - radius * 1.35f;
            travel = Mathf.Max(0f, travel);
            Vector2 axis = horizontal ? Vector2.right : Vector2.up;
            float coordinate = horizontal ? normalizedPosition.x : normalizedPosition.y;
            Vector2 target = center + axis * (coordinate * travel);
            Color tint = new Color(accent.r, accent.g, accent.b, Mathf.Clamp01(accent.a));
            Color halo = new Color(0.005f, 0.018f, 0.022f, 0.86f);

            // A short cross-axis stem makes the selected cue legible even when
            // it sits over a dense row of existing dots or a bright texture.
            float stemHalf = (horizontal ? height : width) * 0.34f;
            Vector2 stemA = target - (horizontal ? Vector2.up : Vector2.right) * stemHalf;
            Vector2 stemB = target + (horizontal ? Vector2.up : Vector2.right) * stemHalf;
            AddLine(vertexHelper, stemA, stemB, stroke * 3.2f, halo);
            AddLine(vertexHelper, stemA, stemB, stroke, tint);

            AddDiamond(vertexHelper, target, radius * 1.10f, new Color(tint.r, tint.g, tint.b, 0.26f));
            AddDiamondOutline(vertexHelper, target, radius * 1.20f, stroke * 1.35f, tint);
            AddDisc(vertexHelper, target, radius * 0.24f, new Color(0.98f, 1f, 0.92f, 1f), 14);

            float phase = Mathf.Repeat(pulse, 1f);
            float pulseRadius = radius * (1.16f + phase * 0.66f);
            float pulseAlpha = 0.34f * (1f - phase);
            AddRing(
                vertexHelper,
                target,
                pulseRadius,
                Mathf.Lerp(stroke * 1.5f, stroke * 0.55f, phase),
                new Color(tint.r, tint.g, tint.b, pulseAlpha),
                24);

            if (edgeClamped)
            {
                float direction = Mathf.Sign(coordinate);
                if (Mathf.Approximately(direction, 0f))
                {
                    direction = 1f;
                }

                Vector2 tip = target + axis * (direction * radius * 0.95f);
                Vector2 basePoint = tip - axis * (direction * radius * 1.35f);
                Vector2 normal = horizontal ? Vector2.up : Vector2.right;
                AddLine(vertexHelper, tip, basePoint + normal * radius * 0.72f, stroke * 1.35f, tint);
                AddLine(vertexHelper, tip, basePoint - normal * radius * 0.72f, stroke * 1.35f, tint);
            }
        }

        private static void AddDiamond(VertexHelper vertexHelper, Vector2 center, float radius, Color color)
        {
            int start = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, center + Vector2.up * radius, color);
            AddVertex(vertexHelper, center + Vector2.right * radius, color);
            AddVertex(vertexHelper, center + Vector2.down * radius, color);
            AddVertex(vertexHelper, center + Vector2.left * radius, color);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddDiamondOutline(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            float width,
            Color color)
        {
            Vector2 top = center + Vector2.up * radius;
            Vector2 right = center + Vector2.right * radius;
            Vector2 bottom = center + Vector2.down * radius;
            Vector2 left = center + Vector2.left * radius;
            AddLine(vertexHelper, top, right, width, color);
            AddLine(vertexHelper, right, bottom, width, color);
            AddLine(vertexHelper, bottom, left, width, color);
            AddLine(vertexHelper, left, top, width, color);
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
                AddVertex(
                    vertexHelper,
                    center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius,
                    color);
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
                float angleA = Mathf.PI * 2f * i / segments;
                float angleB = Mathf.PI * 2f * (i + 1) / segments;
                Vector2 directionA = new Vector2(Mathf.Cos(angleA), Mathf.Sin(angleA));
                Vector2 directionB = new Vector2(Mathf.Cos(angleB), Mathf.Sin(angleB));
                AddQuad(
                    vertexHelper,
                    center + directionA * inner,
                    center + directionA * outer,
                    center + directionB * outer,
                    center + directionB * inner,
                    color);
            }
        }

        private static void AddLine(
            VertexHelper vertexHelper,
            Vector2 from,
            Vector2 to,
            float width,
            Color color)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude <= 0.000001f)
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
