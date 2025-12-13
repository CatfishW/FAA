using UnityEngine;
using AircraftControl.Core;

namespace HUDControl.Elements
{
    /// <summary>
    /// Localizer CDI element for Image-based HUD.
    /// Animates CDI needle horizontal position.
    /// All animations have strict bounds.
    /// </summary>
    [AddComponentMenu("HUD Control/Elements/Localizer")]
    public class LocalizerElement : Core.HUDElementBase
    {
        #region Inspector - UI References
        
        [Header("CDI References")]
        [Tooltip("CDI needle that moves horizontally")]
        [SerializeField] private RectTransform cdiNeedle;
        
        [Tooltip("Deviation dots panel (optional)")]
        [SerializeField] private RectTransform deviationDotsPanel;
        
        #endregion
        
        #region Inspector - Animation Enables
        
        [Header("Animation Enables")]
        [Tooltip("Enable CDI needle movement")]
        [SerializeField] private bool enableCDI = true;
        
        #endregion
        
        #region Inspector - Bounds
        
        [Header("CDI Bounds")]
        [Tooltip("Pixels per dot of deviation")]
        [SerializeField] private float pixelsPerDot = 10f;
        
        [Tooltip("Maximum CDI offset in pixels (KEEP SMALL)")]
        [SerializeField] private float maxCDIOffsetPixels = 20f;
        
        [Tooltip("Simulate deviation (for testing)")]
        [SerializeField] private bool simulateDeviation = true;
        
        [Tooltip("Simulated deviation amount (-2.5 to 2.5 dots)")]
        [Range(-2.5f, 2.5f)]
        [SerializeField] private float simulatedDeviation = 0f;
        
        #endregion
        
        private float displayedDeviation;
        private Vector2 cdiBasePos;
        
        public override string ElementId => "Localizer";
        
        protected override void OnInitialize()
        {
            displayedDeviation = 0f;
            
            if (cdiNeedle != null)
                cdiBasePos = cdiNeedle.anchoredPosition;
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            if (!enableCDI || cdiNeedle == null) return;
            
            // Get deviation (simulated or from external source)
            float targetDeviation = simulateDeviation ? simulatedDeviation : 0f;
            displayedDeviation = Core.HUDAnimator.SmoothValue(displayedDeviation, targetDeviation, smoothing);
            
            // Calculate offset with strict bounds
            float offset = displayedDeviation * pixelsPerDot;
            offset = Mathf.Clamp(offset, -maxCDIOffsetPixels, maxCDIOffsetPixels);
            
            Vector2 newPos = cdiBasePos;
            newPos.x += offset;
            cdiNeedle.anchoredPosition = newPos;
        }
        
        public void SetDeviation(float dots)
        {
            simulateDeviation = false;
            displayedDeviation = Mathf.Clamp(dots, -2.5f, 2.5f);
        }
        
        public float GetDisplayedDeviation() => displayedDeviation;
    }
}
