using UnityEngine;
using TMPro;
using AircraftControl.Core;

namespace HUDControl.CompassBar
{
    /// <summary>
    /// Compass Bar HUD element.
    /// Displays a horizontal compass tape that scrolls based on aircraft heading.
    /// Uses anchor-based scrolling to keep tape within parent bounds.
    /// </summary>
    [AddComponentMenu("HUD Control/Elements/Compass Bar")]
    public class CompassBarElement : Core.HUDElementBase
    {
        #region Inspector - UI References
        
        [Header("Compass Bar References")]
        [Tooltip("The scrolling compass tape RectTransform")]
        [SerializeField] private RectTransform compassTape;
        
        [Tooltip("Optional center heading readout text")]
        [SerializeField] private TMP_Text headingReadout;
        
        [Tooltip("Optional: CompassBarGenerator for auto-setup")]
        [SerializeField] private CompassBarGenerator tapeGenerator;
        
        #endregion
        
        #region Inspector - Animation Settings
        
        [Header("Animation Enables")]
        [SerializeField] private bool enableTapeScroll = true;
        [SerializeField] private bool enableReadout = true;
        
        #endregion
        
        #region Inspector - Tape Settings
        
        [Header("Tape Settings")]
        [Tooltip("Auto-get settings from CompassBarGenerator if assigned")]
        [SerializeField] private bool autoFromGenerator = true;
        
        [Tooltip("Manual: Pixels per degree of heading")]
        [SerializeField] private float pixelsPerDegree = 4f;
        
        [Tooltip("Manual: Total tape width (360 * pixelsPerDegree)")]
        [SerializeField] private float totalTapeWidth = 1440f;
        
        [Header("Display")]
        [Tooltip("Display format for heading readout")]
        [SerializeField] private string displayFormat = "{0:000}°";
        
        #endregion
        
        #region Private Fields
        
        private float displayedHeading;
        private float lastDisplayedHeading = -1f;
        private float effectivePixelsPerDegree;
        private float effectiveTapeWidth;
        
        #endregion
        
        public override string ElementId => "CompassBar";
        
        public float PixelsPerDegree => effectivePixelsPerDegree;
        
        protected override void OnInitialize()
        {
            displayedHeading = 0f;
            
            // Get settings from generator if available
            if (autoFromGenerator && tapeGenerator != null)
            {
                effectivePixelsPerDegree = tapeGenerator.PixelsPerDegree;
                effectiveTapeWidth = tapeGenerator.TapeWidth;
                Debug.Log($"[CompassBar] Using generator settings: {effectivePixelsPerDegree:F2} px/deg, tape width: {effectiveTapeWidth}");
            }
            else
            {
                effectivePixelsPerDegree = pixelsPerDegree;
                effectiveTapeWidth = totalTapeWidth;
            }
            
            // Ensure tape anchor is set up for scrolling
            if (compassTape != null)
            {
                // Set anchors to be at vertical center, we'll adjust X based on heading
                compassTape.anchorMin = new Vector2(0.5f, 0.5f);
                compassTape.anchorMax = new Vector2(0.5f, 0.5f);
                compassTape.pivot = new Vector2(0.5f, 0.5f);
            }
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            float targetHeading = Core.HUDAnimator.NormalizeAngle(state.Heading);
            
            // Smooth heading with proper 0/360 wraparound
            displayedHeading = SmoothHeading(displayedHeading, targetHeading, smoothing);
            
            // Anchor-based tape scrolling
            if (enableTapeScroll && compassTape != null)
            {
                // Calculate anchor offset based on heading
                // 360° of tape = anchor range of 1.0 for a single copy
                // For heading 0° (North), anchor should center North
                // For heading 90° (East), anchor should shift to show East at center
                
                // Normalize heading to 0-1 range (0° = 0, 360° = 1)
                float headingNormalized = displayedHeading / 360f;
                
                // Offset anchor X: as heading increases, we need to shift tape left
                // which means increasing the anchor proportion in the "shift" direction
                float anchorOffset = headingNormalized;
                
                // Wrap within valid range
                while (anchorOffset < 0f) anchorOffset += 1f;
                while (anchorOffset >= 1f) anchorOffset -= 1f;
                
                // Convert to anchor position
                // When anchorOffset = 0, we want center (0.5)
                // When anchorOffset = 0.5, we want the tape shifted by half (showing 180°)
                float anchorX = 0.5f - anchorOffset;
                if (anchorX < 0f) anchorX += 1f;
                
                compassTape.anchorMin = new Vector2(anchorX, 0.5f);
                compassTape.anchorMax = new Vector2(anchorX, 0.5f);
            }
            
            // Heading readout
            if (enableReadout && headingReadout != null)
            {
                int rounded = Mathf.RoundToInt(displayedHeading);
                rounded = ((rounded % 360) + 360) % 360;
                
                if (rounded != Mathf.RoundToInt(lastDisplayedHeading))
                {
                    headingReadout.text = string.Format(displayFormat, rounded);
                    lastDisplayedHeading = rounded;
                }
            }
        }
        
        private float SmoothHeading(float current, float target, float smoothFactor)
        {
            float diff = target - current;
            if (diff > 180f) diff -= 360f;
            else if (diff < -180f) diff += 360f;
            
            float newHeading = current + diff * smoothFactor;
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
                tapeGenerator = compassTape.GetComponent<CompassBarGenerator>();
                if (tapeGenerator != null)
                {
                    autoFromGenerator = true;
                    UnityEditor.EditorUtility.SetDirty(this);
                    Debug.Log("[CompassBar] Linked to CompassBarGenerator");
                }
            }
        }
        
        [ContextMenu("Find References")]
        private void FindReferences()
        {
            if (compassTape == null)
            {
                var tape = transform.Find("Tape") ?? transform.Find("CompassTape");
                if (tape != null) compassTape = tape.GetComponent<RectTransform>();
            }
            
            if (headingReadout == null)
            {
                var readout = transform.Find("Readout") ?? transform.Find("HeadingReadout");
                if (readout != null) headingReadout = readout.GetComponent<TMP_Text>();
            }
            
            if (tapeGenerator == null && compassTape != null)
            {
                tapeGenerator = compassTape.GetComponent<CompassBarGenerator>();
            }
            
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
        
        #endregion
    }
}
