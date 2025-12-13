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
        private float lastDisplayedAirspeed = -1f;
        private Vector2 tapeBasePos;
        
        public override string ElementId => "Airspeed";
        
        protected override void OnInitialize()
        {
            displayedAirspeed = 0f;
            
            if (speedTape != null)
                tapeBasePos = speedTape.anchoredPosition;
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            float targetAirspeed = Mathf.Max(0f, state.IndicatedAirspeedKnots);
            displayedAirspeed = Core.HUDAnimator.SmoothValue(displayedAirspeed, targetAirspeed, smoothing);
            
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
        
        public float GetDisplayedAirspeed() => displayedAirspeed;
    }
}
