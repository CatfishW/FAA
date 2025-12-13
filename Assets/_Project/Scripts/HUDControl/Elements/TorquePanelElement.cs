using UnityEngine;
using AircraftControl.Core;

namespace HUDControl.Elements
{
    /// <summary>
    /// Torque Panel element for Image-based HUD.
    /// Animates dual engine torque pointers with strict bounds.
    /// </summary>
    [AddComponentMenu("HUD Control/Elements/Torque Panel")]
    public class TorquePanelElement : Core.HUDElementBase
    {
        #region Inspector - UI References
        
        [Header("Torque References")]
        [Tooltip("Left engine torque pointer")]
        [SerializeField] private RectTransform torquePointerL;
        
        [Tooltip("Right engine torque pointer")]
        [SerializeField] private RectTransform torquePointerR;
        
        [Tooltip("Torque frame (non-animating)")]
        [SerializeField] private RectTransform torqueFrame;
        
        #endregion
        
        #region Inspector - Animation Enables
        
        [Header("Animation Enables")]
        [Tooltip("Enable torque animation")]
        [SerializeField] private bool enableAnimation = true;
        
        [Tooltip("Simulate torque from throttle")]
        [SerializeField] private bool simulateFromThrottle = true;
        
        #endregion
        
        #region Inspector - Bounds
        
        [Header("Torque Bounds")]
        [Tooltip("Minimum torque rotation angle")]
        [SerializeField] private float minRotationAngle = 0f;
        
        [Tooltip("Maximum torque rotation angle")]
        [SerializeField] private float maxRotationAngle = 90f;
        
        [Tooltip("Max torque value (100% = this rotation)")]
        [SerializeField] private float maxTorquePercent = 100f;
        
        #endregion
        
        private float displayedTorqueL;
        private float displayedTorqueR;
        
        public override string ElementId => "TorquePanel";
        
        protected override void OnInitialize()
        {
            displayedTorqueL = 0f;
            displayedTorqueR = 0f;
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            if (!enableAnimation) return;
            
            // Get torque values (simulated from throttle or external)
            float targetL = simulateFromThrottle ? (state.ThrottlePercent / 100f) * maxTorquePercent : 0f;
            float targetR = simulateFromThrottle ? (state.ThrottlePercent / 100f) * maxTorquePercent : 0f;
            
            displayedTorqueL = Core.HUDAnimator.SmoothValue(displayedTorqueL, targetL, smoothing);
            displayedTorqueR = Core.HUDAnimator.SmoothValue(displayedTorqueR, targetR, smoothing);
            
            // Calculate rotation with bounds
            float rotL = Mathf.Lerp(minRotationAngle, maxRotationAngle, displayedTorqueL / maxTorquePercent);
            float rotR = Mathf.Lerp(minRotationAngle, maxRotationAngle, displayedTorqueR / maxTorquePercent);
            
            rotL = Mathf.Clamp(rotL, minRotationAngle, maxRotationAngle);
            rotR = Mathf.Clamp(rotR, minRotationAngle, maxRotationAngle);
            
            if (torquePointerL != null)
                torquePointerL.localRotation = Quaternion.Euler(0, 0, -rotL);
            if (torquePointerR != null)
                torquePointerR.localRotation = Quaternion.Euler(0, 0, rotR);
        }
        
        public void SetTorque(float leftPercent, float rightPercent)
        {
            simulateFromThrottle = false;
            displayedTorqueL = Mathf.Clamp(leftPercent, 0f, maxTorquePercent);
            displayedTorqueR = Mathf.Clamp(rightPercent, 0f, maxTorquePercent);
        }
        
        public float GetDisplayedTorqueL() => displayedTorqueL;
        public float GetDisplayedTorqueR() => displayedTorqueR;
    }
}
