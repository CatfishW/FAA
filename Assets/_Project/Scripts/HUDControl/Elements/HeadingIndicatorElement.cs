using UnityEngine;
using TMPro;
using AircraftControl.Core;

namespace HUDControl.Elements
{
    /// <summary>
    /// Heading Indicator element for Image-based HUD.
    /// Animates compass tape horizontal position with seamless 360° wrapping.
    /// Works with both static textures and HeadingTapeGenerator.
    /// </summary>
    [AddComponentMenu("HUD Control/Elements/Heading Indicator")]
    public class HeadingIndicatorElement : Core.HUDElementBase
    {
        #region Inspector - UI References
        
        [Header("Heading References")]
        [Tooltip("Compass tape that scrolls horizontally (or generated tape container)")]
        [SerializeField] private RectTransform compassTape;
        
        [Tooltip("Heading readout text")]
        [SerializeField] private TMP_Text headingReadout;
        
        [Tooltip("Heading panel/mask (non-animating, defines visible area)")]
        [SerializeField] private RectTransform headingPanel;
        
        [Tooltip("Optional: HeadingTapeGenerator for auto-setup")]
        [SerializeField] private HeadingTapeGenerator tapeGenerator;
        
        #endregion
        
        #region Inspector - Animation Enables
        
        [Header("Animation Enables")]
        [Tooltip("Enable compass tape movement")]
        [SerializeField] private bool enableCompass = true;
        
        [Tooltip("Enable heading readout")]
        [SerializeField] private bool enableReadout = true;
        
        #endregion
        
        #region Inspector - Tape Settings
        
        [Header("Tape Settings")]
        [Tooltip("Auto-get settings from HeadingTapeGenerator if assigned")]
        [SerializeField] private bool autoFromGenerator = true;
        
        [Tooltip("Manual: Units per degree of heading")]
        [SerializeField] private float unitsPerDegree = 10f;
        
        [Tooltip("Manual: Total tape width (for wrapping calculation)")]
        [SerializeField] private float totalTapeWidth = 3600f;
        
        [Header("Display")]
        [Tooltip("Display format for heading readout")]
        [SerializeField] private string displayFormat = "{0:000}°";
        
        #endregion
        
        private float displayedHeading;
        private float lastDisplayedHeading = -1f;
        private Vector2 tapeBasePos;
        private float effectiveUnitsPerDegree;
        private float effectiveTapeWidth;
        
        public override string ElementId => "Heading";
        
        protected override void OnInitialize()
        {
            displayedHeading = 0f;
            
            if (compassTape != null)
            {
                tapeBasePos = compassTape.anchoredPosition;
            }
            
            // Get settings from generator if available
            if (autoFromGenerator && tapeGenerator != null)
            {
                effectiveUnitsPerDegree = tapeGenerator.PixelsPerDegree;
                effectiveTapeWidth = tapeGenerator.TapeWidth;
                Debug.Log($"[HeadingIndicator] Using generator settings: {effectiveUnitsPerDegree:F2} px/deg");
            }
            else
            {
                effectiveUnitsPerDegree = unitsPerDegree;
                effectiveTapeWidth = totalTapeWidth;
            }
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            float targetHeading = Core.HUDAnimator.NormalizeAngle(state.Heading);
            
            // Smooth with proper angle wrapping
            displayedHeading = SmoothHeading(displayedHeading, targetHeading, smoothing);
            
            // Compass tape movement
            if (enableCompass && compassTape != null)
            {
                // Calculate tape offset
                // Heading increases = tape moves left (negative X)
                float offset = -displayedHeading * effectiveUnitsPerDegree;
                
                // Apply wrapping if tape wraps seamlessly
                // The tape should be centered at heading 0 at base position
                
                Vector2 newPos = tapeBasePos;
                newPos.x += offset;
                compassTape.anchoredPosition = newPos;
            }
            
            // Heading readout
            if (enableReadout && headingReadout != null)
            {
                int rounded = Mathf.RoundToInt(displayedHeading);
                if (rounded >= 360) rounded = 0;
                if (rounded < 0) rounded += 360;
                
                if (rounded != Mathf.RoundToInt(lastDisplayedHeading))
                {
                    headingReadout.text = string.Format(displayFormat, rounded);
                    lastDisplayedHeading = rounded;
                }
            }
        }
        
        /// <summary>
        /// Smooth heading with proper wraparound handling (0/360 boundary)
        /// </summary>
        private float SmoothHeading(float current, float target, float smoothFactor)
        {
            // Handle wraparound: if difference > 180, take shorter path
            float diff = target - current;
            
            if (diff > 180f)
                diff -= 360f;
            else if (diff < -180f)
                diff += 360f;
            
            float newHeading = current + diff * smoothFactor;
            
            // Normalize result
            while (newHeading < 0) newHeading += 360f;
            while (newHeading >= 360f) newHeading -= 360f;
            
            return newHeading;
        }
        
        public float GetDisplayedHeading() => displayedHeading;
        
        #region Editor
        
#if UNITY_EDITOR
        [ContextMenu("Auto-Link Generator")]
        private void AutoLinkGenerator()
        {
            if (compassTape != null)
            {
                tapeGenerator = compassTape.GetComponent<HeadingTapeGenerator>();
                if (tapeGenerator != null)
                {
                    autoFromGenerator = true;
                    UnityEditor.EditorUtility.SetDirty(this);
                    Debug.Log("[HeadingIndicator] Linked to HeadingTapeGenerator");
                }
            }
        }
#endif
        
        #endregion
    }
}
