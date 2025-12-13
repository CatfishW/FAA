using UnityEngine;
using AircraftControl.Core;

namespace HUDControl.Elements
{
    /// <summary>
    /// Bank Scale element for Image-based HUD.
    /// Animates bank scale rotation and slip/skid indicator.
    /// All animations have strict bounds.
    /// </summary>
    [AddComponentMenu("HUD Control/Elements/Bank Scale")]
    public class BankScaleElement : Core.HUDElementBase
    {
        #region Inspector - UI References
        
        [Header("Bank Scale References")]
        [Tooltip("Bank scale arc Image")]
        [SerializeField] private RectTransform bankScale;
        
        [Tooltip("Bank scale inner part")]
        [SerializeField] private Transform bankScaleIP;
        
        [Tooltip("Roll pointer indicator")]
        [SerializeField] private RectTransform rollPointer;
        
        [Tooltip("Slip/Skid slider")]
        [SerializeField] private RectTransform slipSlider;
        
        #endregion
        
        #region Inspector - Animation Enables
        
        [Header("Animation Enables")]
        [Tooltip("Enable bank scale rotation")]
        [SerializeField] private bool enableBankRotation = true;
        
        [Tooltip("Enable roll pointer rotation (if not rotating scale)")]
        [SerializeField] private bool enablePointerRotation = false;
        
        [Tooltip("Enable slip/skid indicator")]
        [SerializeField] private bool enableSlip = true;
        
        [Tooltip("Rotate scale (true) or pointer (false)")]
        [SerializeField] private bool rotateScale = false;
        
        [Tooltip("Enable Bank Scale IP rotation on Z axis for roll indication")]
        [SerializeField] private bool enableBankScaleIPRotation = true;
        
        #endregion
        
        #region Inspector - Bounds
        
        [Header("Bank Bounds")]
        [Tooltip("Maximum bank angle in degrees")]
        [SerializeField] private float maxBankAngle = 45f;
        
        [Header("Slip Bounds")]
        [Tooltip("Pixels per unit of slip")]
        [SerializeField] private float slipPixelsPerUnit = 10f;
        
        [Tooltip("Maximum slip offset in pixels (KEEP SMALL)")]
        [SerializeField] private float maxSlipOffsetPixels = 15f;
        
        [Tooltip("Simulate slip from rudder input")]
        [SerializeField] private bool simulateSlip = true;
        
        #endregion
        
        private float displayedRoll;
        private float displayedSlip;
        private Vector2 slipBasePos;
        
        public override string ElementId => "BankScale";
        
        protected override void OnInitialize()
        {
            displayedRoll = 0f;
            displayedSlip = 0f;
            
            if (slipSlider != null)
                slipBasePos = slipSlider.anchoredPosition;
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            float targetRoll = Mathf.Clamp(state.Roll, -maxBankAngle, maxBankAngle);
            displayedRoll = Core.HUDAnimator.SmoothAngle(displayedRoll, targetRoll, smoothing);
            
            // Bank rotation
            if (rotateScale && enableBankRotation)
            {
                if (bankScale != null)
                    bankScale.localRotation = Quaternion.Euler(0, 0, displayedRoll);
            }
            else if (enablePointerRotation && rollPointer != null)
            {
                rollPointer.localRotation = Quaternion.Euler(0, 0, -displayedRoll);
            }
            
            // Bank Scale IP rotation (independent of scale rotation)
            if (enableBankScaleIPRotation && bankScaleIP != null)
            {
                bankScaleIP.localRotation = Quaternion.Euler(0, 0, displayedRoll);
            }
            
            // Slip indicator
            if (enableSlip && slipSlider != null)
            {
                float targetSlip = simulateSlip ? -state.RudderInput : 0f;
                displayedSlip = Core.HUDAnimator.SmoothValue(displayedSlip, targetSlip, smoothing);
                
                float slipOffset = displayedSlip * slipPixelsPerUnit;
                slipOffset = Mathf.Clamp(slipOffset, -maxSlipOffsetPixels, maxSlipOffsetPixels);
                
                Vector2 newPos = slipBasePos;
                newPos.x += slipOffset;
                slipSlider.anchoredPosition = newPos;
            }
        }
        
        public void SetSlipValue(float slip)
        {
            simulateSlip = false;
            displayedSlip = Mathf.Clamp(slip, -1f, 1f);
        }
        
        public float GetDisplayedRoll() => displayedRoll;
        public float GetDisplayedSlip() => displayedSlip;
    }
}
