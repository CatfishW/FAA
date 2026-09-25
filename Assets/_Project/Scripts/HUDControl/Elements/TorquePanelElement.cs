using UnityEngine;
using TMPro;
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

        [Header("Numeric Readouts")]
        [Tooltip("Show compact live torque values below the pointer scale")]
        [SerializeField] private bool showNumericReadouts = true;

        [Tooltip("Left engine torque value")]
        [SerializeField] private TMP_Text torqueValueL;

        [Tooltip("Right engine torque value")]
        [SerializeField] private TMP_Text torqueValueR;

        [SerializeField] private Vector2 leftReadoutPosition = new Vector2(-0.055f, -0.055f);
        [SerializeField] private Vector2 rightReadoutPosition = new Vector2(0.088f, -0.055f);

        [Range(14f, 32f)]
        [SerializeField] private float readoutFontSize = 22f;

        [Header("Scale Labels")]
        [Tooltip("Show the fixed percentage scale beside the torque bar")]
        [SerializeField] private bool showScaleLabels = true;

        [Tooltip("Percentage increment between adjacent torque scale labels")]
        [Range(1, 100)]
        [SerializeField] private int scaleLabelStepPercent = 20;

        [Range(10f, 24f)]
        [SerializeField] private float scaleLabelFontSize = 16f;

        [Tooltip("World-space gap between the frame and the scale labels")]
        [SerializeField] private float scaleLabelGap = 0.045f;

        [Tooltip("Scale labels authored as child TextMeshPro objects in the scene/prefab")]
        [SerializeField] private TMP_Text[] torqueScaleLabels = new TMP_Text[0];
        
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
        private int availableEngineCount = 2;
        public override string ElementId => "TorquePanel";
        
        protected override void OnInitialize()
        {
            ConfigureNumericReadouts();
            ApplyScaleLabels();
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
            ApplyScaleLabels();
            ApplyPointerPositions();
        }

        public void ConfigureReadouts(TMP_Text left, TMP_Text right)
        {
            torqueValueL = left;
            torqueValueR = right;
            ConfigureNumericReadouts();
            UpdateNumericReadouts();
        }

        public void ConfigureScaleLabels(TMP_Text[] labels)
        {
            torqueScaleLabels = labels ?? new TMP_Text[0];
            ApplyScaleLabels();
        }

        public void SetEngineCount(int engineCount)
        {
            availableEngineCount = Mathf.Max(0, engineCount);
            if (engineCount <= 0)
            {
                ClearExternalData();
                return;
            }

            if (engineCount == 1)
            {
                ClearRightChannel();
            }

            UpdateNumericReadouts();
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
            UpdateNumericReadouts();
        }

        private void ConfigureNumericReadouts()
        {
            if (!showNumericReadouts)
            {
                EngineHudNumericReadout.SetValue(torqueValueL, 0f, false, false);
                EngineHudNumericReadout.SetValue(torqueValueR, 0f, false, false);
                return;
            }

            int readoutLayer = torqueFrame != null ? torqueFrame.gameObject.layer : gameObject.layer;
            EngineHudNumericReadout.ConfigureExisting(torqueValueL, readoutFontSize, readoutLayer);
            EngineHudNumericReadout.ConfigureExisting(torqueValueR, readoutFontSize, readoutLayer);
        }

        private void ApplyScaleLabels()
        {
            if (!showScaleLabels || torqueFrame == null)
            {
                HideScaleLabels();
                return;
            }

            int[] values = EngineHudNumericReadout.BuildScaleValues(maxTorquePercent, scaleLabelStepPercent);

            for (int i = 0; i < torqueScaleLabels.Length; i++)
            {
                bool visible = i < values.Length;
                EngineHudNumericReadout.ConfigureExisting(
                    torqueScaleLabels[i], scaleLabelFontSize, torqueFrame.gameObject.layer);
                EngineHudNumericReadout.SetScaleLabel(
                    torqueScaleLabels[i], visible ? values[i] : 0, visible);
            }
        }

        private void HideScaleLabels()
        {
            for (int i = 0; i < torqueScaleLabels.Length; i++)
            {
                EngineHudNumericReadout.SetScaleLabel(torqueScaleLabels[i], 0, false);
            }
        }

        private void UpdateNumericReadouts()
        {
            bool leftVisible = showNumericReadouts && availableEngineCount > 0;
            bool rightVisible = showNumericReadouts && availableEngineCount > 1;
            EngineHudNumericReadout.SetValue(
                torqueValueL, displayedTorqueL, simulateFromThrottle || hasExternalTorqueL, leftVisible);
            EngineHudNumericReadout.SetValue(
                torqueValueR, displayedTorqueR, simulateFromThrottle || hasExternalTorqueR, rightVisible);
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
