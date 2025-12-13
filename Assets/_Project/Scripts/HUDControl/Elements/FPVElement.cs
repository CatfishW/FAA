using UnityEngine;
using AircraftControl.Core;

namespace HUDControl.Elements
{
    /// <summary>
    /// FPV (Flight Path Vector) element for Image-based HUD.
    /// Animates FPV marker position based on flight path angle.
    /// All animations have strict bounds.
    /// </summary>
    [AddComponentMenu("HUD Control/Elements/FPV")]
    public class FPVElement : Core.HUDElementBase
    {
        #region Inspector - UI References
        
        [Header("FPV References")]
        [Tooltip("FPV marker Image")]
        [SerializeField] private RectTransform fpvMarker;
        
        #endregion
        
        #region Inspector - Animation Enables
        
        [Header("Animation Enables")]
        [Tooltip("Enable vertical FPV movement")]
        [SerializeField] private bool enableVertical = true;
        
        [Tooltip("Enable horizontal FPV movement (drift)")]
        [SerializeField] private bool enableHorizontal = false;
        
        #endregion
        
        #region Inspector - Bounds
        
        [Header("FPV Bounds")]
        [Tooltip("Pixels per degree of FPA")]
        [SerializeField] private float pixelsPerDegree = 2f;
        
        [Tooltip("Maximum vertical offset in pixels (KEEP SMALL)")]
        [SerializeField] private float maxVerticalOffsetPixels = 15f;
        
        [Tooltip("Maximum horizontal offset in pixels (KEEP SMALL)")]
        [SerializeField] private float maxHorizontalOffsetPixels = 15f;
        
        #endregion
        
        private float displayedVertical;
        private float displayedHorizontal;
        private Vector2 fpvBasePos;
        
        public override string ElementId => "FPV";
        
        protected override void CacheReferences()
        {
            base.CacheReferences();
            
            if (fpvMarker == null)
                fpvMarker = rectTransform;
        }
        
        protected override void OnInitialize()
        {
            displayedVertical = 0f;
            displayedHorizontal = 0f;
            
            if (fpvMarker != null)
                fpvBasePos = fpvMarker.anchoredPosition;
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            if (fpvMarker == null) return;
            
            Vector2 newPos = fpvBasePos;
            
            // Vertical FPV (flight path angle)
            if (enableVertical)
            {
                float fpa = CalculateFPA(state);
                displayedVertical = Core.HUDAnimator.SmoothValue(displayedVertical, fpa, smoothing);
                
                float vOffset = -displayedVertical * pixelsPerDegree;
                vOffset = Mathf.Clamp(vOffset, -maxVerticalOffsetPixels, maxVerticalOffsetPixels);
                newPos.y += vOffset;
            }
            
            // Horizontal FPV (drift/sideslip)
            if (enableHorizontal)
            {
                float drift = CalculateDrift(state);
                displayedHorizontal = Core.HUDAnimator.SmoothValue(displayedHorizontal, drift, smoothing);
                
                float hOffset = displayedHorizontal * pixelsPerDegree;
                hOffset = Mathf.Clamp(hOffset, -maxHorizontalOffsetPixels, maxHorizontalOffsetPixels);
                newPos.x += hOffset;
            }
            
            fpvMarker.anchoredPosition = newPos;
        }
        
        private float CalculateFPA(AircraftState state)
        {
            if (state.GroundSpeedKnots < 10f) return 0f;
            float vsKnots = state.VerticalSpeedFpm / 101.269f;
            return Mathf.Clamp(Mathf.Atan2(vsKnots, state.GroundSpeedKnots) * Mathf.Rad2Deg, -20f, 20f);
        }
        
        private float CalculateDrift(AircraftState state)
        {
            // Simplified drift calculation from sideslip
            return Mathf.Clamp(state.RudderInput * 5f, -10f, 10f);
        }
        
        public float GetDisplayedVertical() => displayedVertical;
        public float GetDisplayedHorizontal() => displayedHorizontal;
    }
}
