using UnityEngine;
using AircraftControl.Core;

namespace HUDControl.Elements
{
    /// <summary>
    /// Torque Panel element for Image-based HUD.
    /// Animates dual engine torque pointers along the authored vertical bar scale.
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
        
        [Header("Torque Bar Calibration")]
        [Tooltip("Anchored Y position representing zero torque")]
        [SerializeField] private float pointerMinimumY = 0.004f;

        [Tooltip("Vertical pointer travel from zero to maximum torque")]
        [SerializeField] private float pointerTravelY = 0.24f;
        
        [Tooltip("Maximum torque percentage represented at the top of the bar")]
        [SerializeField] private float maxTorquePercent = 120f;
        
        #endregion
        
        private float displayedTorqueL;
        private float displayedTorqueR;
        private float targetTorqueL;
        private float targetTorqueR;
        private bool hasExternalTorqueL;
        private bool hasExternalTorqueR;
        
        public override string ElementId => "TorquePanel";
        
        protected override void OnInitialize()
        {
            displayedTorqueL = 0f;
            displayedTorqueR = 0f;
            targetTorqueL = 0f;
            targetTorqueR = 0f;
            if (!simulateFromThrottle)
            {
                SetPointerAvailable(torquePointerL, false);
                SetPointerAvailable(torquePointerR, false);
            }
            ApplyPointerPositions();
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            if (!enableAnimation) return;
            
            float targetL = simulateFromThrottle
                ? (state.ThrottlePercent / 100f) * maxTorquePercent
                : targetTorqueL;
            float targetR = simulateFromThrottle
                ? (state.ThrottlePercent / 100f) * maxTorquePercent
                : targetTorqueR;
            
            displayedTorqueL = Core.HUDAnimator.SmoothValue(displayedTorqueL, targetL, smoothing);
            displayedTorqueR = Core.HUDAnimator.SmoothValue(displayedTorqueR, targetR, smoothing);
            
            ApplyPointerPositions();
        }
        
        public void SetTorque(float leftPercent, float rightPercent)
        {
            SetTorqueData(leftPercent, true, rightPercent, true);
        }

        public void SetTorqueData(float leftPercent, bool leftValid, float rightPercent, bool rightValid)
        {
            simulateFromThrottle = false;
            if (leftValid)
            {
                targetTorqueL = Mathf.Clamp(leftPercent, 0f, maxTorquePercent);
                // Snap only the first live sample. Subsequent X-Plane samples become
                // animation targets so the authored bar moves continuously between
                // API polling frames instead of teleporting several times per second.
                if (!hasExternalTorqueL)
                {
                    displayedTorqueL = targetTorqueL;
                }
                hasExternalTorqueL = true;
            }

            if (rightValid)
            {
                targetTorqueR = Mathf.Clamp(rightPercent, 0f, maxTorquePercent);
                if (!hasExternalTorqueR)
                {
                    displayedTorqueR = targetTorqueR;
                }
                hasExternalTorqueR = true;
            }

            SetPointerAvailable(torquePointerL, leftValid || hasExternalTorqueL);
            SetPointerAvailable(torquePointerR, rightValid || hasExternalTorqueR);
            ApplyPointerPositions();
        }

        public void ConfigurePointers(RectTransform left, RectTransform right, RectTransform frame)
        {
            torquePointerL = left;
            torquePointerR = right;
            torqueFrame = frame;
            ApplyPointerPositions();
        }

        public void SetEngineCount(int engineCount)
        {
            if (engineCount <= 0)
            {
                ClearExternalData();
                return;
            }

            if (engineCount == 1)
            {
                ClearRightChannel();
            }
        }

        public void ClearExternalData()
        {
            simulateFromThrottle = false;
            targetTorqueL = displayedTorqueL = 0f;
            targetTorqueR = displayedTorqueR = 0f;
            hasExternalTorqueL = false;
            hasExternalTorqueR = false;
            SetPointerAvailable(torquePointerL, false);
            SetPointerAvailable(torquePointerR, false);
            ApplyPointerPositions();
        }

        private void ClearRightChannel()
        {
            targetTorqueR = displayedTorqueR = 0f;
            hasExternalTorqueR = false;
            SetPointerAvailable(torquePointerR, false);
            ApplyPointerPosition(torquePointerR, displayedTorqueR);
        }

        private void ApplyPointerPositions()
        {
            ApplyPointerPosition(torquePointerL, displayedTorqueL);
            ApplyPointerPosition(torquePointerR, displayedTorqueR);
        }

        private void ApplyPointerPosition(RectTransform pointer, float torquePercent)
        {
            if (pointer == null)
            {
                return;
            }

            Vector2 anchored = pointer.anchoredPosition;
            anchored.y = pointerMinimumY + Mathf.Clamp01(torquePercent / Mathf.Max(1f, maxTorquePercent)) * pointerTravelY;
            pointer.anchoredPosition = anchored;
        }

        private static void SetPointerAvailable(RectTransform pointer, bool available)
        {
            if (pointer != null && pointer.gameObject.activeSelf != available)
            {
                pointer.gameObject.SetActive(available);
            }
        }
        
        public float GetDisplayedTorqueL() => displayedTorqueL;
        public float GetDisplayedTorqueR() => displayedTorqueR;
        public float GetTargetTorqueL() => targetTorqueL;
        public float GetTargetTorqueR() => targetTorqueR;
    }
}
