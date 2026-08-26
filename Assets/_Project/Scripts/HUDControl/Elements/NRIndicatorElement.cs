using System.Collections.Generic;
using UnityEngine;
using TMPro;
using AircraftControl.Core;

namespace HUDControl.Elements
{
    /// <summary>
    /// NR/RPM Indicator element for Image-based HUD.
    /// Animates common NR and dual-engine N2 pointers along the authored bars.
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

        [Header("Numeric Readouts")]
        [Tooltip("Show compact live NR/N2 values below the pointer scales")]
        [SerializeField] private bool showNumericReadouts = true;

        [Tooltip("Common rotor NR value")]
        [SerializeField] private TMP_Text rpmValueCenter;

        [Tooltip("Left engine N2 value")]
        [SerializeField] private TMP_Text rpmValueL;

        [Tooltip("Right engine N2 value")]
        [SerializeField] private TMP_Text rpmValueR;

        [SerializeField] private Vector2 leftReadoutPosition = new Vector2(-0.11f, -0.055f);
        [SerializeField] private Vector2 centerReadoutPosition = new Vector2(0f, -0.055f);
        [SerializeField] private Vector2 rightReadoutPosition = new Vector2(0.11f, -0.055f);

        [Range(14f, 32f)]
        [SerializeField] private float readoutFontSize = 20f;

        [Header("Scale Labels")]
        [Tooltip("Show the fixed percentage scale beside the dual NR/N2 bars")]
        [SerializeField] private bool showScaleLabels = true;

        [Tooltip("Percentage increment between adjacent NR/N2 scale labels")]
        [Range(1, 100)]
        [SerializeField] private int scaleLabelStepPercent = 20;

        [Range(10f, 24f)]
        [SerializeField] private float scaleLabelFontSize = 16f;

        [Tooltip("World-space gap between the frame and the scale labels")]
        [SerializeField] private float scaleLabelGap = 0.045f;
        
        #endregion
        
        #region Inspector - Animation Enables
        
        [Header("Animation Enables")]
        [Tooltip("Enable NR animation")]
        [SerializeField] private bool enableAnimation = true;
        
        [Tooltip("Simulate RPM from throttle")]
        [SerializeField] private bool simulateFromThrottle = true;
        
        #endregion
        
        #region Inspector - Bounds
        
        [Header("RPM Bar Calibration")]
        [Tooltip("Anchored Y position representing zero RPM")]
        [SerializeField] private float pointerMinimumY = 0.03f;

        [Tooltip("Vertical pointer travel from zero to maximum RPM")]
        [SerializeField] private float pointerTravelY = 0.24f;
        
        [Tooltip("Maximum NR/N2 percentage represented at the top of the bar")]
        [SerializeField] private float maxRPMPercent = 110f;
        
        [Tooltip("Normal operating RPM percent")]
        [SerializeField] private float normalRPM = 100f;
        
        #endregion
        
        private float displayedRPMCenter;
        private float displayedRPML;
        private float displayedRPMR;
        private float targetRPMCenter;
        private float targetRPML;
        private float targetRPMR;
        private bool hasExternalCenter;
        private bool hasExternalL;
        private bool hasExternalR;
        private int availableEngineCount = 2;
        private readonly List<TMP_Text> nrScaleLabels = new List<TMP_Text>();
        
        public override string ElementId => "NRIndicator";
        
        protected override void OnInitialize()
        {
            EnsureNumericReadouts();
            EnsureScaleLabels();
            displayedRPMCenter = 0f;
            displayedRPML = 0f;
            displayedRPMR = 0f;
            targetRPMCenter = 0f;
            targetRPML = 0f;
            targetRPMR = 0f;
            if (!simulateFromThrottle)
            {
                SetPointerAvailable(rpmCenterPointer, false);
                SetPointerAvailable(rpmPointerL, false);
                SetPointerAvailable(rpmPointerR, false);
            }
            ApplyPointerPositions();
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            if (!enableAnimation) return;
            
            // Simulate RPM from throttle (reaches 100% at ~50% throttle, stays there)
            float simRPM = simulateFromThrottle ? Mathf.Min((state.ThrottlePercent / 100f) * 2f, 1f) * normalRPM : 0f;
            
            float targetCenter = simulateFromThrottle ? simRPM : targetRPMCenter;
            float targetL = simulateFromThrottle ? simRPM : targetRPML;
            float targetR = simulateFromThrottle ? simRPM : targetRPMR;
            
            displayedRPMCenter = Core.HUDAnimator.SmoothValue(displayedRPMCenter, targetCenter, smoothing);
            displayedRPML = Core.HUDAnimator.SmoothValue(displayedRPML, targetL, smoothing);
            displayedRPMR = Core.HUDAnimator.SmoothValue(displayedRPMR, targetR, smoothing);
            
            ApplyPointerPositions();
        }
        
        public void SetRPM(float centerPercent, float leftPercent, float rightPercent)
        {
            SetRPMData(centerPercent, true, leftPercent, true, rightPercent, true);
        }

        public void SetRPMData(
            float centerPercent,
            bool centerValid,
            float leftPercent,
            bool leftValid,
            float rightPercent,
            bool rightValid)
        {
            simulateFromThrottle = false;
            if (centerValid)
            {
                targetRPMCenter = Mathf.Clamp(centerPercent, 0f, maxRPMPercent);
                if (!hasExternalCenter)
                {
                    displayedRPMCenter = targetRPMCenter;
                }
                hasExternalCenter = true;
            }
            if (leftValid)
            {
                targetRPML = Mathf.Clamp(leftPercent, 0f, maxRPMPercent);
                if (!hasExternalL)
                {
                    displayedRPML = targetRPML;
                }
                hasExternalL = true;
            }
            if (rightValid)
            {
                targetRPMR = Mathf.Clamp(rightPercent, 0f, maxRPMPercent);
                if (!hasExternalR)
                {
                    displayedRPMR = targetRPMR;
                }
                hasExternalR = true;
            }

            SetPointerAvailable(rpmCenterPointer, centerValid || hasExternalCenter);
            SetPointerAvailable(rpmPointerL, leftValid || hasExternalL);
            SetPointerAvailable(rpmPointerR, rightValid || hasExternalR);
            ApplyPointerPositions();
        }

        public void ConfigurePointers(RectTransform center, RectTransform left, RectTransform right, RectTransform frame)
        {
            rpmCenterPointer = center;
            rpmPointerL = left;
            rpmPointerR = right;
            nrFrame = frame;
            EnsureScaleLabels();
            ApplyPointerPositions();
        }

        public void ConfigureReadouts(TMP_Text center, TMP_Text left, TMP_Text right)
        {
            rpmValueCenter = center;
            rpmValueL = left;
            rpmValueR = right;
            EnsureNumericReadouts();
            UpdateNumericReadouts();
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
            targetRPMCenter = displayedRPMCenter = 0f;
            targetRPML = displayedRPML = 0f;
            targetRPMR = displayedRPMR = 0f;
            hasExternalCenter = false;
            hasExternalL = false;
            hasExternalR = false;
            SetPointerAvailable(rpmCenterPointer, false);
            SetPointerAvailable(rpmPointerL, false);
            SetPointerAvailable(rpmPointerR, false);
            ApplyPointerPositions();
        }

        private void ClearRightChannel()
        {
            targetRPMR = displayedRPMR = 0f;
            hasExternalR = false;
            SetPointerAvailable(rpmPointerR, false);
            ApplyPointerPosition(rpmPointerR, displayedRPMR);
        }

        private void ApplyPointerPositions()
        {
            ApplyPointerPosition(rpmCenterPointer, displayedRPMCenter);
            ApplyPointerPosition(rpmPointerL, displayedRPML);
            ApplyPointerPosition(rpmPointerR, displayedRPMR);
            UpdateNumericReadouts();
        }

        private void EnsureNumericReadouts()
        {
            if (!showNumericReadouts)
            {
                EngineHudNumericReadout.SetValue(rpmValueCenter, 0f, false, false);
                EngineHudNumericReadout.SetValue(rpmValueL, 0f, false, false);
                EngineHudNumericReadout.SetValue(rpmValueR, 0f, false, false);
                return;
            }

            int readoutLayer = nrFrame != null ? nrFrame.gameObject.layer : gameObject.layer;
            rpmValueCenter = EngineHudNumericReadout.Ensure(
                transform, rpmValueCenter, "NR Value Center", centerReadoutPosition, readoutFontSize, readoutLayer);
            rpmValueL = EngineHudNumericReadout.Ensure(
                transform, rpmValueL, "NR Value L", leftReadoutPosition, readoutFontSize, readoutLayer);
            rpmValueR = EngineHudNumericReadout.Ensure(
                transform, rpmValueR, "NR Value R", rightReadoutPosition, readoutFontSize, readoutLayer);
        }

        private void EnsureScaleLabels()
        {
            if (!showScaleLabels || nrFrame == null)
            {
                HideScaleLabels();
                return;
            }

            int[] values = EngineHudNumericReadout.BuildScaleValues(maxRPMPercent, scaleLabelStepPercent);
            int labelLayer = nrFrame.gameObject.layer;
            // The two bars share one scale. Keep it outside the left bar so
            // the center and right pointers retain an unobstructed silhouette.
            float horizontalOffset = -(EngineHudNumericReadout.GetFrameWidth(nrFrame) * 0.5f + scaleLabelGap);

            for (int i = 0; i < values.Length; i++)
            {
                Vector2 position = EngineHudNumericReadout.GetScaleLabelPosition(
                    nrFrame,
                    values[i],
                    maxRPMPercent,
                    horizontalOffset,
                    pointerMinimumY,
                    pointerTravelY);
                TMP_Text label = i < nrScaleLabels.Count ? nrScaleLabels[i] : null;
                label = EngineHudNumericReadout.Ensure(
                    transform, label, $"NR Scale {i}", position, scaleLabelFontSize, labelLayer);

                if (i >= nrScaleLabels.Count)
                {
                    nrScaleLabels.Add(label);
                }

                EngineHudNumericReadout.SetScaleLabel(label, values[i], true);
            }

            for (int i = values.Length; i < nrScaleLabels.Count; i++)
            {
                EngineHudNumericReadout.SetScaleLabel(nrScaleLabels[i], 0, false);
            }
        }

        private void HideScaleLabels()
        {
            for (int i = 0; i < nrScaleLabels.Count; i++)
            {
                EngineHudNumericReadout.SetScaleLabel(nrScaleLabels[i], 0, false);
            }
        }

        private void UpdateNumericReadouts()
        {
            bool hasSimulatedData = simulateFromThrottle;
            bool enginesVisible = showNumericReadouts && availableEngineCount > 0;
            EngineHudNumericReadout.SetValue(
                rpmValueCenter,
                displayedRPMCenter,
                hasSimulatedData || hasExternalCenter,
                showNumericReadouts && (hasSimulatedData || hasExternalCenter));
            EngineHudNumericReadout.SetValue(
                rpmValueL, displayedRPML, hasSimulatedData || hasExternalL, enginesVisible);
            EngineHudNumericReadout.SetValue(
                rpmValueR,
                displayedRPMR,
                hasSimulatedData || hasExternalR,
                showNumericReadouts && availableEngineCount > 1);
        }

        private void ApplyPointerPosition(RectTransform pointer, float rpmPercent)
        {
            if (pointer == null)
            {
                return;
            }

            Vector2 anchored = pointer.anchoredPosition;
            anchored.y = pointerMinimumY + Mathf.Clamp01(rpmPercent / Mathf.Max(1f, maxRPMPercent)) * pointerTravelY;
            pointer.anchoredPosition = anchored;
        }

        private static void SetPointerAvailable(RectTransform pointer, bool available)
        {
            if (pointer != null && pointer.gameObject.activeSelf != available)
            {
                pointer.gameObject.SetActive(available);
            }
        }
        
        public float GetDisplayedRPMCenter() => displayedRPMCenter;
        public float GetDisplayedRPML() => displayedRPML;
        public float GetDisplayedRPMR() => displayedRPMR;
    }
}
