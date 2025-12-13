using UnityEngine;
using AircraftControl.Core;

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
        
        private float displayedDeviation;
        private Vector2 gsBasePos;
        
        public override string ElementId => "Glidescope";
        
        protected override void OnInitialize()
        {
            displayedDeviation = 0f;
            
            if (glidescopeNeedle != null)
                gsBasePos = glidescopeNeedle.anchoredPosition;
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            if (!enableGS || glidescopeNeedle == null) return;
            
            float targetDeviation = simulateDeviation ? simulatedDeviation : 0f;
            displayedDeviation = Core.HUDAnimator.SmoothValue(displayedDeviation, targetDeviation, smoothing);
            
            // Calculate offset with strict bounds (positive deviation = above glideslope = needle down)
            float offset = -displayedDeviation * pixelsPerDot;
            offset = Mathf.Clamp(offset, -maxGSOffsetPixels, maxGSOffsetPixels);
            
            Vector2 newPos = gsBasePos;
            newPos.y += offset;
            glidescopeNeedle.anchoredPosition = newPos;
        }
        
        public void SetDeviation(float dots)
        {
            simulateDeviation = false;
            displayedDeviation = Mathf.Clamp(dots, -2.5f, 2.5f);
        }
        
        public float GetDisplayedDeviation() => displayedDeviation;
    }
}
