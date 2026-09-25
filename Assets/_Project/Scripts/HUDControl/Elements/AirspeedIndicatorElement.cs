using UnityEngine;
using TMPro;
using AircraftControl.Core;

namespace HUDControl.Elements
{
    /// <summary>
    /// Airspeed Indicator element for Image-based HUD.
    /// Animates speed tape vertical position with strict bounds.
    /// </summary>
    [AddComponentMenu("HUD Control/Elements/Airspeed Indicator")]
    public class AirspeedIndicatorElement : Core.HUDElementBase
    {
        #region Inspector - UI References
        
        [Header("Airspeed References")]
        [Tooltip("Speed tape that scrolls vertically")]
        [SerializeField] private RectTransform speedTape;
        
        [Tooltip("Airspeed readout text")]
        [SerializeField] private TMP_Text airspeedReadout;
        
        [Tooltip("Airspeed window panel (non-animating)")]
        [SerializeField] private RectTransform windowPanel;
        
        #endregion
        
        #region Inspector - Animation Enables
        
        [Header("Animation Enables")]
        [Tooltip("Enable speed tape movement")]
        [SerializeField] private bool enableTape = true;
        
        [Tooltip("Enable airspeed readout")]
        [SerializeField] private bool enableReadout = true;
        
        #endregion
        
        #region Inspector - Bounds
        
        [Header("Airspeed Bounds")]
        [Tooltip("Pixels per knot of airspeed")]
        [SerializeField] private float pixelsPerKnot = 1f;
        
        [Tooltip("Maximum tape offset in pixels (KEEP SMALL)")]
        [SerializeField] private float maxTapeOffsetPixels = 30f;
        
        [Tooltip("Reference airspeed (tape centered at this speed)")]
        [SerializeField] private float referenceAirspeed = 100f;
        
        [Tooltip("Display format")]
        [SerializeField] private string displayFormat = "{0:0}";
        
        #endregion
        
        private float displayedAirspeed;
        private float targetAirspeed;
        private float lastDisplayedAirspeed = -1f;
        private Vector2 tapeBasePos;
        private bool hasExternalAirspeed;
        private bool externalDataUnavailable;
        
        public override string ElementId => "Airspeed";
        
        protected override void OnInitialize()
        {
            displayedAirspeed = 0f;
            targetAirspeed = 0f;
            hasExternalAirspeed = false;
            externalDataUnavailable = false;
            lastDisplayedAirspeed = -1f;
            
            if (speedTape != null)
                tapeBasePos = speedTape.anchoredPosition;

            SetTapeAvailable(true);
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            if (state == null || externalDataUnavailable)
            {
                return;
            }

            float target = hasExternalAirspeed
                ? targetAirspeed
                : Mathf.Max(0f, state.IndicatedAirspeedKnots);
            float effectiveSmoothing = smoothing > 0f
                ? smoothing
                : Core.HUDAnimator.CalculateSmoothing(animationSpeed);
            displayedAirspeed = Core.HUDAnimator.SmoothValue(displayedAirspeed, target, effectiveSmoothing);
            
            // Speed tape movement
            if (enableTape && speedTape != null)
            {
                // Calculate offset relative to reference
                float deltaSpeed = displayedAirspeed - referenceAirspeed;
                float offset = deltaSpeed * pixelsPerKnot;
                offset = Mathf.Clamp(offset, -maxTapeOffsetPixels, maxTapeOffsetPixels);
                
                Vector2 newPos = tapeBasePos;
                newPos.y += offset;
                speedTape.anchoredPosition = newPos;
            }
            
            // Airspeed readout
            if (enableReadout && airspeedReadout != null)
            {
                int rounded = Mathf.RoundToInt(displayedAirspeed);
                
                if (rounded != Mathf.RoundToInt(lastDisplayedAirspeed))
                {
                    airspeedReadout.text = string.Format(displayFormat, rounded);
                    lastDisplayedAirspeed = rounded;
                }
            }
        }
        
        /// <summary>
        /// Feed an authoritative X-Plane value. The first valid packet establishes
        /// the display without a jump; subsequent packets remain animation targets.
        /// </summary>
        public void SetAirspeedData(float value, bool valid)
        {
            if (!valid || float.IsNaN(value) || float.IsInfinity(value))
            {
                ClearExternalData();
                return;
            }

            targetAirspeed = Mathf.Max(0f, value);
            if (!hasExternalAirspeed)
            {
                displayedAirspeed = targetAirspeed;
            }

            hasExternalAirspeed = true;
            externalDataUnavailable = false;
            SetTapeAvailable(true);
            UpdateReadout();
        }

        /// <summary>
        /// Bind only objects authored in the scene or prefab. No UI is created here.
        /// </summary>
        public void ConfigureVisuals(RectTransform tape, TMP_Text readout, RectTransform window)
        {
            speedTape = tape;
            airspeedReadout = readout;
            windowPanel = window;
            tapeBasePos = speedTape != null ? speedTape.anchoredPosition : Vector2.zero;
            SetTapeAvailable(true);
            UpdateReadout();
        }

        public void ClearExternalData()
        {
            hasExternalAirspeed = false;
            externalDataUnavailable = true;
            targetAirspeed = 0f;
            displayedAirspeed = 0f;
            SetTapeAvailable(false);
            SetReadoutUnavailable();
        }

        public bool HasExternalData => hasExternalAirspeed && !externalDataUnavailable;
        public float GetTargetAirspeed() => targetAirspeed;
        public float GetDisplayedAirspeed() => displayedAirspeed;

        private void UpdateReadout()
        {
            if (!enableReadout || airspeedReadout == null)
            {
                return;
            }

            int rounded = Mathf.RoundToInt(displayedAirspeed);
            if (rounded != Mathf.RoundToInt(lastDisplayedAirspeed) || airspeedReadout.text == "---")
            {
                airspeedReadout.text = string.Format(displayFormat, rounded);
                lastDisplayedAirspeed = rounded;
            }

            airspeedReadout.color = new Color(0.2f, 1f, 0.2f, 1f);
        }

        private void SetReadoutUnavailable()
        {
            if (airspeedReadout == null)
            {
                return;
            }

            airspeedReadout.text = "---";
            airspeedReadout.color = new Color(0.2f, 1f, 0.2f, 0.46f);
        }

        private static void SetTapeAvailable(RectTransform tape, bool available)
        {
            if (tape != null && tape.gameObject.activeSelf != available)
            {
                tape.gameObject.SetActive(available);
            }
        }

        private void SetTapeAvailable(bool available)
        {
            SetTapeAvailable(speedTape, available);
        }
    }
}
