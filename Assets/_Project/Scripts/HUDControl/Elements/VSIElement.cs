using UnityEngine;
using TMPro;
using AircraftControl.Core;

namespace HUDControl.Elements
{
    /// <summary>
    /// VSI (Vertical Speed Indicator) element for Image-based HUD.
    /// Animates VSI pointer rotation with strict bounds.
    /// </summary>
    [AddComponentMenu("HUD Control/Elements/VSI")]
    public class VSIElement : Core.HUDElementBase
    {
        #region Inspector - UI References
        
        [Header("VSI References")]
        [Tooltip("VSI pointer that rotates")]
        [SerializeField] private RectTransform vsiPointer;
        
        [Tooltip("VSI tape that moves vertically (optional)")]
        [SerializeField] private RectTransform vsiTape;
        
        [Tooltip("Digital readout (optional)")]
        [SerializeField] private TMP_Text digitalReadout;
        
        #endregion
        
        #region Inspector - Animation Enables
        
        [Header("Animation Enables")]
        [Tooltip("Enable VSI pointer rotation")]
        [SerializeField] private bool enablePointer = true;
        
        [Tooltip("Enable VSI tape movement")]
        [SerializeField] private bool enableTape = false;
        
        [Tooltip("Enable digital readout")]
        [SerializeField] private bool enableReadout = true;
        
        #endregion
        
        #region Inspector - Bounds
        
        [Header("VSI Bounds")]
        [Tooltip("Rotation angle at max climb")]
        [SerializeField] private float maxClimbAngle = 90f;
        
        [Tooltip("Rotation angle at max descent")]
        [SerializeField] private float maxDescentAngle = -90f;
        
        [Tooltip("Maximum VS in fpm for full deflection")]
        [SerializeField] private float maxVSFpm = 2000f;
        
        [Tooltip("Maximum tape offset in pixels")]
        [SerializeField] private float maxTapeOffsetPixels = 20f;
        
        #endregion
        
        private float displayedVS;
        private Vector2 tapeBasePos;
        
        public override string ElementId => "VSI";
        
        protected override void OnInitialize()
        {
            displayedVS = 0f;
            
            if (vsiTape != null)
                tapeBasePos = vsiTape.anchoredPosition;
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            float targetVS = Mathf.Clamp(state.VerticalSpeedFpm, -maxVSFpm, maxVSFpm);
            displayedVS = Core.HUDAnimator.SmoothValue(displayedVS, targetVS, smoothing);
            
            // Pointer rotation
            if (enablePointer && vsiPointer != null)
            {
                float normalizedVS = displayedVS / maxVSFpm;
                float rotation = Mathf.Lerp(0, displayedVS > 0 ? maxClimbAngle : maxDescentAngle, Mathf.Abs(normalizedVS));
                rotation = Mathf.Clamp(rotation, maxDescentAngle, maxClimbAngle);
                
                vsiPointer.localRotation = Quaternion.Euler(0, 0, rotation);
            }
            
            // Tape movement
            if (enableTape && vsiTape != null)
            {
                float normalizedVS = displayedVS / maxVSFpm;
                float offset = normalizedVS * maxTapeOffsetPixels;
                offset = Mathf.Clamp(offset, -maxTapeOffsetPixels, maxTapeOffsetPixels);
                
                Vector2 newPos = tapeBasePos;
                newPos.y += offset;
                vsiTape.anchoredPosition = newPos;
            }
            
            // Digital readout
            if (enableReadout && digitalReadout != null)
            {
                int rounded = Mathf.RoundToInt(displayedVS / 100f) * 100;
                digitalReadout.text = rounded >= 0 ? $"+{rounded}" : $"{rounded}";
            }
        }
        
        public float GetDisplayedVS() => displayedVS;
    }
}
