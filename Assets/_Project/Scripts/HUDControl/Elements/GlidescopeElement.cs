using UnityEngine;
using AircraftControl.Core;
using TrafficRadar;

namespace HUDControl.Elements
{
    /// <summary>
    /// Glidescope element for Image-based HUD.
    /// Animates glidescope needle vertical position.
    /// All animations have strict bounds.
    /// </summary>
    [AddComponentMenu("HUD Control/Elements/Glidescope")]
    public class GlidescopeElement : Core.HUDElementBase
    {
        #region Inspector - UI References
        
        [Header("Glidescope References")]
        [Tooltip("Glidescope needle that moves vertically")]
        [SerializeField] private RectTransform glidescopeNeedle;
        
        [Tooltip("Glidescope dots panel (optional)")]
        [SerializeField] private RectTransform glidescopeDotsPanel;
        
        #endregion
        
        #region Inspector - Animation Enables
        
        [Header("Animation Enables")]
        [Tooltip("Enable glidescope needle movement")]
        [SerializeField] private bool enableGS = true;
        
        #endregion
        
        #region Inspector - Bounds
        
        [Header("Glidescope Bounds")]
        [Tooltip("Pixels per dot of deviation")]
        [SerializeField] private float pixelsPerDot = 10f;
        
        [Tooltip("Maximum GS offset in pixels (KEEP SMALL)")]
        [SerializeField] private float maxGSOffsetPixels = 20f;
        
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

        [Tooltip("Show the selected map target as an amber cue on the glidescope bar.")]
        [SerializeField] private bool showNavigationTargetCue = true;

        [Tooltip("Fraction of the target's forward/backward radar vector represented by the full glidescope bar.")]
        [Range(0.45f, 1f)]
        [SerializeField] private float navigationTargetForwardWindow = 0.90f;

        [Tooltip("Accent used for a selected map target.")]
        [SerializeField] private Color navigationTargetColor = new Color(1f, 0.78f, 0.28f, 1f);

        [Tooltip("Pulse speed for the selected target cue.")]
        [Min(0f)]
        [SerializeField] private float navigationTargetPulseSpeed = 2.2f;

        #endregion
        
        private float displayedDeviation;
        private float targetDeviation;
        private Vector2 gsBasePos;
        private NavigationTargetCueGraphic navigationTargetCue;
        private float navigationTargetPulse;
        
        public override string ElementId => "Glidescope";
        
        protected override void OnInitialize()
        {
            displayedDeviation = 0f;
            targetDeviation = Mathf.Clamp(simulatedDeviation, -2.5f, 2.5f);
            
            if (glidescopeNeedle != null)
                gsBasePos = glidescopeNeedle.anchoredPosition;

            EnsureNavigationTargetCue();
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            if (enableGS && glidescopeNeedle != null)
            {
                float desiredDeviation = simulateDeviation ? simulatedDeviation : targetDeviation;
                displayedDeviation = Core.HUDAnimator.SmoothValue(displayedDeviation, desiredDeviation, smoothing);

                // Calculate offset with strict bounds (positive deviation = above glideslope = needle down)
                float offset = -displayedDeviation * pixelsPerDot;
                offset = Mathf.Clamp(offset, -maxGSOffsetPixels, maxGSOffsetPixels);

                Vector2 newPos = gsBasePos;
                newPos.y += offset;
                glidescopeNeedle.anchoredPosition = newPos;
            }

            UpdateNavigationTargetCue();
        }

        private void Update()
        {
            // Keep the target cue responsive in editor previews and in scenes
            // where the bridge deliberately does not drive this element.
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
            // Deviation dots and the needle carry disabled/empty copies of the
            // element. Only a root with a real needle should own a cue.
            if (!showNavigationTargetCue || glidescopeNeedle == null)
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
                navigationTargetCue = cueObject.GetComponent<NavigationTargetCueGraphic>() ??
                                       cueObject.AddComponent<NavigationTargetCueGraphic>();
                navigationTargetCue.raycastTarget = false;
            }

            RectTransform cueRect = navigationTargetCue.rectTransform;
            if (cueRect != null)
            {
                cueRect.anchorMin = new Vector2(0.5f, 0.5f);
                cueRect.anchorMax = new Vector2(0.5f, 0.5f);
                cueRect.pivot = new Vector2(0.5f, 0.5f);

                float targetX = glidescopeNeedle.anchoredPosition.x;
                float maximumY = 0.20f;
                RectTransform[] siblings = GetComponentsInChildren<RectTransform>(false);
                for (int i = 0; i < siblings.Length; i++)
                {
                    RectTransform sibling = siblings[i];
                    if (sibling == null || sibling == cueRect || sibling == transform)
                    {
                        continue;
                    }

                    if (sibling.parent == transform)
                    {
                        maximumY = Mathf.Max(
                            maximumY,
                            Mathf.Abs(sibling.anchoredPosition.y) + sibling.rect.height * 0.5f);
                    }
                }

                float halfHeight = Mathf.Max(0.22f, maximumY + 0.045f);
                float cueWidth = Mathf.Max(0.14f, glidescopeNeedle.rect.width * 2.5f);
                cueRect.anchoredPosition = new Vector2(targetX, 0f);
                cueRect.sizeDelta = new Vector2(cueWidth, halfHeight * 2f);
                cueRect.localScale = Vector3.one;
                cueRect.localRotation = Quaternion.identity;
            }

            navigationTargetCue.transform.SetAsLastSibling();
        }

        private void UpdateNavigationTargetCue()
        {
            if (!showNavigationTargetCue || glidescopeNeedle == null)
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
            float window = Mathf.Clamp(navigationTargetForwardWindow, 0.45f, 1f);
            // AircraftRelativePosition.y is positive ahead of the own ship and
            // negative behind it. It also includes range, so nearby targets sit
            // near the centre while distant targets occupy the outer cue.
            float forwardPosition = target.AircraftRelativePosition.y;
            bool edgeClamped = Mathf.Abs(forwardPosition) > window;
            float normalized = Mathf.Clamp(forwardPosition / window, -1f, 1f);
            navigationTargetPulse += Time.unscaledDeltaTime * Mathf.Max(0f, navigationTargetPulseSpeed);
            Color tint = target.IsOffscreen
                ? new Color(1f, 0.62f, 0.18f, 1f)
                : navigationTargetColor;
            navigationTargetCue.SetTarget(
                new Vector2(0f, normalized),
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
}
