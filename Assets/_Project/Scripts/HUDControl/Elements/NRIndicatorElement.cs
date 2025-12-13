using UnityEngine;
using AircraftControl.Core;

namespace HUDControl.Elements
{
    /// <summary>
    /// NR/RPM Indicator element for Image-based HUD.
    /// Animates center and dual engine RPM pointers with strict bounds.
    /// </summary>
    [AddComponentMenu("HUD Control/Elements/NR Indicator")]
    public class NRIndicatorElement : Core.HUDElementBase
    {
        #region Inspector - UI References
        
        [Header("NR/RPM References")]
        [Tooltip("Center RPM pointer")]
        [SerializeField] private RectTransform rpmCenterPointer;
        
        [Tooltip("Left engine RPM pointer")]
        [SerializeField] private RectTransform rpmPointerL;
        
        [Tooltip("Right engine RPM pointer")]
        [SerializeField] private RectTransform rpmPointerR;
        
        [Tooltip("NR frame (non-animating)")]
        [SerializeField] private RectTransform nrFrame;
        
        #endregion
        
        #region Inspector - Animation Enables
        
        [Header("Animation Enables")]
        [Tooltip("Enable NR animation")]
        [SerializeField] private bool enableAnimation = true;
        
        [Tooltip("Simulate RPM from throttle")]
        [SerializeField] private bool simulateFromThrottle = true;
        
        #endregion
        
        #region Inspector - Bounds
        
        [Header("RPM Bounds")]
        [Tooltip("Minimum RPM rotation angle")]
        [SerializeField] private float minRotationAngle = 0f;
        
        [Tooltip("Maximum RPM rotation angle")]
        [SerializeField] private float maxRotationAngle = 270f;
        
        [Tooltip("Max RPM value (100% = this rotation)")]
        [SerializeField] private float maxRPMPercent = 100f;
        
        [Tooltip("Normal operating RPM percent")]
        [SerializeField] private float normalRPM = 100f;
        
        #endregion
        
        private float displayedRPMCenter;
        private float displayedRPML;
        private float displayedRPMR;
        
        public override string ElementId => "NRIndicator";
        
        protected override void OnInitialize()
        {
            displayedRPMCenter = 0f;
            displayedRPML = 0f;
            displayedRPMR = 0f;
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            if (!enableAnimation) return;
            
            // Simulate RPM from throttle (reaches 100% at ~50% throttle, stays there)
            float simRPM = simulateFromThrottle ? Mathf.Min((state.ThrottlePercent / 100f) * 2f, 1f) * normalRPM : 0f;
            
            float targetCenter = simRPM;
            float targetL = simRPM;
            float targetR = simRPM;
            
            displayedRPMCenter = Core.HUDAnimator.SmoothValue(displayedRPMCenter, targetCenter, smoothing);
            displayedRPML = Core.HUDAnimator.SmoothValue(displayedRPML, targetL, smoothing);
            displayedRPMR = Core.HUDAnimator.SmoothValue(displayedRPMR, targetR, smoothing);
            
            // Calculate rotations with bounds
            float rotCenter = Mathf.Lerp(minRotationAngle, maxRotationAngle, displayedRPMCenter / maxRPMPercent);
            float rotL = Mathf.Lerp(minRotationAngle, maxRotationAngle, displayedRPML / maxRPMPercent);
            float rotR = Mathf.Lerp(minRotationAngle, maxRotationAngle, displayedRPMR / maxRPMPercent);
            
            rotCenter = Mathf.Clamp(rotCenter, minRotationAngle, maxRotationAngle);
            rotL = Mathf.Clamp(rotL, minRotationAngle, maxRotationAngle);
            rotR = Mathf.Clamp(rotR, minRotationAngle, maxRotationAngle);
            
            if (rpmCenterPointer != null)
                rpmCenterPointer.localRotation = Quaternion.Euler(0, 0, -rotCenter);
            if (rpmPointerL != null)
                rpmPointerL.localRotation = Quaternion.Euler(0, 0, -rotL);
            if (rpmPointerR != null)
                rpmPointerR.localRotation = Quaternion.Euler(0, 0, rotR);
        }
        
        public void SetRPM(float centerPercent, float leftPercent, float rightPercent)
        {
            simulateFromThrottle = false;
            displayedRPMCenter = Mathf.Clamp(centerPercent, 0f, maxRPMPercent);
            displayedRPML = Mathf.Clamp(leftPercent, 0f, maxRPMPercent);
            displayedRPMR = Mathf.Clamp(rightPercent, 0f, maxRPMPercent);
        }
        
        public float GetDisplayedRPMCenter() => displayedRPMCenter;
    }
}
