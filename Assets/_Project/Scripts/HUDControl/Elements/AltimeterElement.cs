using UnityEngine;
using TMPro;
using AircraftControl.Core;

namespace HUDControl.Elements
{
    /// <summary>
    /// Altimeter element for Image-based HUD.
    /// Animates altitude tape vertical position with strict bounds.
    /// </summary>
    [AddComponentMenu("HUD Control/Elements/Altimeter")]
    public class AltimeterElement : Core.HUDElementBase
    {
        #region Inspector - UI References
        
        [Header("Altimeter References")]
        [Tooltip("Altitude tape that scrolls vertically")]
        [SerializeField] private RectTransform altitudeTape;
        
        [Tooltip("Altitude readout text")]
        [SerializeField] private TMP_Text altitudeReadout;
        
        [Tooltip("Altimeter window panel (non-animating)")]
        [SerializeField] private RectTransform windowPanel;
        
        #endregion
        
        #region Inspector - Animation Enables
        
        [Header("Animation Enables")]
        [Tooltip("Enable altitude tape movement")]
        [SerializeField] private bool enableTape = true;
        
        [Tooltip("Enable altitude readout")]
        [SerializeField] private bool enableReadout = true;
        
        #endregion
        
        #region Inspector - Bounds
        
        [Header("Altimeter Bounds")]
        [Tooltip("Pixels per foot of altitude")]
        [SerializeField] private float pixelsPerFoot = 0.01f;
        
        [Tooltip("Maximum tape offset in pixels (KEEP SMALL)")]
        [SerializeField] private float maxTapeOffsetPixels = 30f;
        
        [Tooltip("Reference altitude (tape centered at this altitude)")]
        [SerializeField] private float referenceAltitude = 1000f;
        
        [Tooltip("Display format")]
        [SerializeField] private string displayFormat = "{0:0}";
        
        #endregion
        
        private float displayedAltitude;
        private float lastDisplayedAltitude = -1f;
        private Vector2 tapeBasePos;
        
        public override string ElementId => "Altimeter";
        
        protected override void OnInitialize()
        {
            displayedAltitude = 0f;
            
            if (altitudeTape != null)
                tapeBasePos = altitudeTape.anchoredPosition;
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            float targetAltitude = state.AltitudeFeet;
            displayedAltitude = Core.HUDAnimator.SmoothValue(displayedAltitude, targetAltitude, smoothing);
            
            // Altitude tape movement
            if (enableTape && altitudeTape != null)
            {
                // Calculate offset relative to reference
                float deltaAlt = displayedAltitude - referenceAltitude;
                float offset = deltaAlt * pixelsPerFoot;
                offset = Mathf.Clamp(offset, -maxTapeOffsetPixels, maxTapeOffsetPixels);
                
                Vector2 newPos = tapeBasePos;
                newPos.y += offset;
                altitudeTape.anchoredPosition = newPos;
            }
            
            // Altitude readout
            if (enableReadout && altitudeReadout != null)
            {
                int rounded = Mathf.RoundToInt(displayedAltitude);
                
                if (rounded != Mathf.RoundToInt(lastDisplayedAltitude))
                {
                    altitudeReadout.text = string.Format(displayFormat, rounded);
                    lastDisplayedAltitude = rounded;
                }
            }
        }
        
        public float GetDisplayedAltitude() => displayedAltitude;
    }
}
