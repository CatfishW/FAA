using UnityEngine;
using UnityEngine.UI;
using AircraftControl.Core;

namespace HUDControl.Elements
{
    /// <summary>
    /// Attitude Indicator element for Image-based HUD.
    /// Animates pitch ladder vertically and rotates for roll.
    /// Auto-calculates movement based on ladder texture dimensions.
    /// </summary>
    [AddComponentMenu("HUD Control/Elements/Attitude Indicator")]
    public class AttitudeIndicatorElement : Core.HUDElementBase
    {
        #region Inspector - UI References
        
        [Header("Required References")]
        [Tooltip("The pitch ladder/scale Image that moves vertically and rotates")]
        [SerializeField] private RectTransform pitchLadder;
        
        [Header("Optional References")]
        [Tooltip("Miniature aircraft symbol (static - does not animate)")]
        [SerializeField] private RectTransform miniatureAircraft;
        
        [Tooltip("Flight Path Vector marker")]
        [SerializeField] private RectTransform fpvMarker;
        
        [Tooltip("Bank scale arc (optional - keeps sync with roll)")]
        [SerializeField] private RectTransform bankScaleArc;
        
        [Tooltip("The mask/container defining visible area")]
        [SerializeField] private RectTransform maskContainer;
        
        #endregion
        
        #region Inspector - Animation Enables
        
        [Header("Animation Enables")]
        [Tooltip("Enable pitch ladder vertical movement")]
        [SerializeField] private bool enablePitch = true;
        
        [Tooltip("Enable roll rotation")]
        [SerializeField] private bool enableRoll = true;
        
        [Tooltip("Enable FPV marker movement")]
        [SerializeField] private bool enableFPV = true;
        
        #endregion
        
        #region Inspector - Pitch Ladder Settings
        
        [Header("Pitch Ladder Texture Settings")]
        [Tooltip("Total degrees covered by the pitch ladder texture (e.g., 110 for -55° to +55°)")]
        [SerializeField] private float ladderTotalDegrees = 110f;
        
        [Tooltip("Auto-calculate units per degree from ladder height")]
        [SerializeField] private bool autoCalculate = true;
        
        [Tooltip("Manual: Units (normalized) per degree of pitch")]
        [SerializeField] private float unitsPerDegree = 0.0773f;
        
        [Header("Roll Settings")]
        [Tooltip("Maximum roll angle in degrees")]
        [SerializeField] private float maxRollDegrees = 60f;
        
        [Tooltip("Invert roll direction")]
        [SerializeField] private bool invertRoll = false;
        
        [Header("FPV Settings")]
        [Tooltip("FPV uses same scale as pitch ladder")]
        [SerializeField] private bool fpvMatchesPitchScale = true;
        
        [Tooltip("Manual: FPV units per degree (if not matching pitch)")]
        [SerializeField] private float fpvUnitsPerDegree = 0.0773f;
        
        [Tooltip("Maximum FPV offset from center (0 = auto from mask height)")]
        [SerializeField] private float fpvMaxOffset = 0f;
        
        [Tooltip("FPV clamp margin from mask edge")]
        [SerializeField] private float fpvClampMargin = 20f;
        
        #endregion
        
        private float displayedPitch;
        private float displayedRoll;
        private float displayedFPVPitch;
        private Vector2 pitchLadderBasePos;
        private Vector2 fpvBasePos;
        private float calculatedUnitsPerDegree;
        private float ladderHeight;
        private float maskHeight;
        
        public override string ElementId => "Attitude";
        
        protected override void OnInitialize()
        {
            displayedPitch = 0f;
            displayedRoll = 0f;
            displayedFPVPitch = 0f;
            
            // Store base positions
            if (pitchLadder != null)
            {
                pitchLadderBasePos = pitchLadder.anchoredPosition;
                ladderHeight = pitchLadder.rect.height;
                
                // Auto-calculate units per degree
                // Ladder height covers ladderTotalDegrees
                if (autoCalculate && ladderHeight > 0 && ladderTotalDegrees > 0)
                {
                    calculatedUnitsPerDegree = ladderHeight / ladderTotalDegrees;
                    Debug.Log($"[AttitudeIndicator] Auto-calculated: {calculatedUnitsPerDegree:F4} units/degree " +
                              $"(Ladder height: {ladderHeight}, Total degrees: {ladderTotalDegrees})");
                }
                else
                {
                    calculatedUnitsPerDegree = unitsPerDegree;
                }
            }
            
            if (maskContainer != null)
            {
                maskHeight = maskContainer.rect.height;
            }
            
            if (fpvMarker != null)
            {
                fpvBasePos = fpvMarker.anchoredPosition;
            }
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            float effectiveUnitsPerDegree = autoCalculate ? calculatedUnitsPerDegree : unitsPerDegree;
            
            // Pitch animation
            if (enablePitch && pitchLadder != null)
            {
                float targetPitch = state.Pitch;
                displayedPitch = Core.HUDAnimator.SmoothValue(displayedPitch, targetPitch, smoothing);
                
                // Calculate offset: negative pitch = ladder moves up, positive = down
                float pitchOffset = -displayedPitch * effectiveUnitsPerDegree;
                
                // Clamp to prevent going beyond ladder bounds
                float maxOffset = (ladderHeight - maskHeight) / 2f;
                if (maxOffset > 0)
                {
                    pitchOffset = Mathf.Clamp(pitchOffset, -maxOffset, maxOffset);
                }
                
                Vector2 newPos = pitchLadderBasePos;
                newPos.y += pitchOffset;
                pitchLadder.anchoredPosition = newPos;
            }
            
            // Roll animation
            if (enableRoll && pitchLadder != null)
            {
                float targetRoll = Mathf.Clamp(state.Roll, -maxRollDegrees, maxRollDegrees);
                if (invertRoll) targetRoll = -targetRoll;
                displayedRoll = Core.HUDAnimator.SmoothAngle(displayedRoll, targetRoll, smoothing);
                
                pitchLadder.localRotation = Quaternion.Euler(0, 0, displayedRoll);
                
                // Also rotate bank scale if assigned
                if (bankScaleArc != null)
                    bankScaleArc.localRotation = Quaternion.Euler(0, 0, displayedRoll);
            }
            
            // FPV animation
            if (enableFPV && fpvMarker != null)
            {
                float fpa = CalculateFPA(state);
                displayedFPVPitch = Core.HUDAnimator.SmoothValue(displayedFPVPitch, fpa, smoothing);
                
                float fpvScale = fpvMatchesPitchScale ? effectiveUnitsPerDegree : fpvUnitsPerDegree;
                float fpvOffset = -displayedFPVPitch * fpvScale;
                
                // Clamp FPV to stay within visible area
                float maxFPVOffset = fpvMaxOffset > 0 ? fpvMaxOffset : (maskHeight / 2f - fpvClampMargin);
                if (maxFPVOffset > 0)
                {
                    fpvOffset = Mathf.Clamp(fpvOffset, -maxFPVOffset, maxFPVOffset);
                }
                
                Vector2 newPos = fpvBasePos;
                newPos.y += fpvOffset;
                fpvMarker.anchoredPosition = newPos;
            }
        }
        
        private float CalculateFPA(AircraftState state)
        {
            if (state.GroundSpeedKnots < 10f) return 0f;
            float vsKnots = state.VerticalSpeedFpm / 101.269f;
            return Mathf.Clamp(Mathf.Atan2(vsKnots, state.GroundSpeedKnots) * Mathf.Rad2Deg, -30f, 30f);
        }
        
        public float GetDisplayedPitch() => displayedPitch;
        public float GetDisplayedRoll() => displayedRoll;
        public float GetCalculatedUnitsPerDegree() => calculatedUnitsPerDegree;
        
        #region Editor
        
#if UNITY_EDITOR
        [ContextMenu("Recalculate Units Per Degree")]
        private void RecalculateUnitsPerDegree()
        {
            if (pitchLadder != null && ladderTotalDegrees > 0)
            {
                float height = pitchLadder.rect.height;
                calculatedUnitsPerDegree = height / ladderTotalDegrees;
                unitsPerDegree = calculatedUnitsPerDegree;
                Debug.Log($"[AttitudeIndicator] Calculated: {calculatedUnitsPerDegree:F4} units/degree");
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
        
        protected override void OnValidate()
        {
            base.OnValidate();
            
            // Preview calculation
            if (pitchLadder != null && autoCalculate && ladderTotalDegrees > 0)
            {
                float height = pitchLadder.rect.height;
                if (height > 0)
                {
                    unitsPerDegree = height / ladderTotalDegrees;
                }
            }
        }
#endif
        
        #endregion
    }
}
