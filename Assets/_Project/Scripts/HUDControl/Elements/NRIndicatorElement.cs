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

        [Tooltip("Keep each numeric value centered on its corresponding NR/N2 bar")]
        [SerializeField] private bool alignReadoutsToPointers = true;

        [SerializeField] private Vector2 leftReadoutPosition = new Vector2(-0.11f, -0.055f);
        [SerializeField] private Vector2 centerReadoutPosition = new Vector2(0f, -0.055f);
        [SerializeField] private Vector2 rightReadoutPosition = new Vector2(0.11f, -0.055f);

        [Range(14f, 32f)]
        [SerializeField] private float readoutFontSize = 20f;

        [Header("Bar Identification")]
        [Tooltip("Show a compact caption and channel markers so pilots can identify NR and N2 bars")]
        [SerializeField] private bool showBarLabels = true;

        [Tooltip("Caption displayed below the NR/N2 bars")]
        [SerializeField] private string barCaption = "NR / N2";

        [Tooltip("Common rotor speed channel marker")]
        [SerializeField] private string centerBarLabel = "NR";

        [Tooltip("Left engine speed channel marker")]
        [SerializeField] private string leftBarLabel = "N2 L";

        [Tooltip("Right engine speed channel marker")]
        [SerializeField] private string rightBarLabel = "N2 R";

        [Tooltip("Caption text object; authored in the scene or generated once for a configured HUD")]
        [SerializeField] private TMP_Text barCaptionText;

        [Tooltip("Common rotor speed label text object")]
        [SerializeField] private TMP_Text centerBarLabelText;

        [Tooltip("Left engine speed label text object")]
        [SerializeField] private TMP_Text leftBarLabelText;

        [Tooltip("Right engine speed label text object")]
        [SerializeField] private TMP_Text rightBarLabelText;

        [Range(10f, 22f)]
        [SerializeField] private float barLabelFontSize = 15f;

        [Tooltip("Vertical gap below the channel markers used by the NR/N2 caption")]
        [SerializeField] private float barCaptionGap = 0.045f;

        [Tooltip("Vertical gap between a numeric value and its channel marker")]
        [SerializeField] private float channelLabelGap = 0.050f;

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

        [Tooltip("Scale labels authored as child TextMeshPro objects in the scene/prefab")]
        [SerializeField] private TMP_Text[] nrScaleLabels = new TMP_Text[0];
        
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
        public override string ElementId => "NRIndicator";
        
        protected override void OnInitialize()
        {
            ConfigureNumericReadouts();
            ApplyScaleLabels();
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
            ConfigureNumericReadouts();
            ApplyScaleLabels();
            ApplyPointerPositions();
        }

        public void ConfigureReadouts(TMP_Text center, TMP_Text left, TMP_Text right)
        {
            rpmValueCenter = center;
            rpmValueL = left;
            rpmValueR = right;
            ConfigureNumericReadouts();
            UpdateNumericReadouts();
        }

        public void ConfigureScaleLabels(TMP_Text[] labels)
        {
            nrScaleLabels = labels ?? new TMP_Text[0];
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

        private void ConfigureNumericReadouts()
        {
            if (!showNumericReadouts)
            {
                EngineHudNumericReadout.SetValue(rpmValueCenter, 0f, false, false);
                EngineHudNumericReadout.SetValue(rpmValueL, 0f, false, false);
                EngineHudNumericReadout.SetValue(rpmValueR, 0f, false, false);
                HideBarLabels();
                return;
            }

            int readoutLayer = nrFrame != null ? nrFrame.gameObject.layer : gameObject.layer;
            EngineHudNumericReadout.ConfigureExisting(rpmValueCenter, readoutFontSize, readoutLayer);
            EngineHudNumericReadout.ConfigureExisting(rpmValueL, readoutFontSize, readoutLayer);
            EngineHudNumericReadout.ConfigureExisting(rpmValueR, readoutFontSize, readoutLayer);
            ApplyReadoutLayout();
            ConfigureBarLabels(readoutLayer);
        }

        private void ApplyReadoutLayout()
        {
            if (!alignReadoutsToPointers)
            {
                return;
            }

            EngineHudNumericReadout.AlignReadout(rpmValueCenter, rpmCenterPointer, centerReadoutPosition);
            EngineHudNumericReadout.AlignReadout(rpmValueL, rpmPointerL, leftReadoutPosition);
            EngineHudNumericReadout.AlignReadout(rpmValueR, rpmPointerR, rightReadoutPosition);
        }

        private void ConfigureBarLabels(int layer)
        {
            bool canAuthorLabels = showBarLabels &&
                                   nrFrame != null &&
                                   (rpmCenterPointer != null || rpmPointerL != null || rpmPointerR != null);
            if (!canAuthorLabels)
            {
                HideBarLabels();
                return;
            }

            float valueY = GetReadoutPosition(rpmValueCenter, rpmCenterPointer, centerReadoutPosition).y;
            if (rpmValueL != null)
            {
                valueY = Mathf.Min(valueY, GetReadoutPosition(rpmValueL, rpmPointerL, leftReadoutPosition).y);
            }

            if (rpmValueR != null)
            {
                valueY = Mathf.Min(valueY, GetReadoutPosition(rpmValueR, rpmPointerR, rightReadoutPosition).y);
            }

            // Keep the group caption below the live values. The area above the
            // engine bars is occupied by IAS/ALT readouts in the flight HUD.
            float captionY = valueY - channelLabelGap - barCaptionGap;
            barCaptionText = EngineHudNumericReadout.EnsureDescriptor(
                transform,
                barCaptionText,
                "NR Bar Caption",
                barCaption,
                new Vector2(nrFrame.anchoredPosition.x, captionY),
                barLabelFontSize,
                layer,
                110f);

            if (rpmCenterPointer != null || rpmValueCenter != null)
            {
                Vector2 valuePosition = GetReadoutPosition(rpmValueCenter, rpmCenterPointer, centerReadoutPosition);
                centerBarLabelText = EngineHudNumericReadout.EnsureDescriptor(
                    transform,
                    centerBarLabelText,
                    "NR Bar Label Center",
                    centerBarLabel,
                    new Vector2(valuePosition.x, valuePosition.y - channelLabelGap),
                    barLabelFontSize,
                    layer,
                    48f);
            }

            if (rpmPointerL != null || rpmValueL != null)
            {
                Vector2 valuePosition = GetReadoutPosition(rpmValueL, rpmPointerL, leftReadoutPosition);
                leftBarLabelText = EngineHudNumericReadout.EnsureDescriptor(
                    transform,
                    leftBarLabelText,
                    "NR Bar Label L",
                    leftBarLabel,
                    new Vector2(valuePosition.x, valuePosition.y - channelLabelGap),
                    barLabelFontSize,
                    layer,
                    48f);
            }

            if (rpmPointerR != null || rpmValueR != null)
            {
                Vector2 valuePosition = GetReadoutPosition(rpmValueR, rpmPointerR, rightReadoutPosition);
                rightBarLabelText = EngineHudNumericReadout.EnsureDescriptor(
                    transform,
                    rightBarLabelText,
                    "NR Bar Label R",
                    rightBarLabel,
                    new Vector2(valuePosition.x, valuePosition.y - channelLabelGap),
                    barLabelFontSize,
                    layer,
                    48f);
            }

            UpdateBarLabels();
        }

        private Vector2 GetReadoutPosition(TMP_Text readout, RectTransform pointer, Vector2 fallback)
        {
            Vector2 position = readout != null ? readout.rectTransform.anchoredPosition : fallback;
            if (alignReadoutsToPointers && pointer != null && pointer.parent == transform)
            {
                position.x = pointer.anchoredPosition.x;
            }

            return position;
        }

        private void UpdateBarLabels()
        {
            bool labelsVisible = showBarLabels && showNumericReadouts;
            EngineHudNumericReadout.SetDescriptor(barCaptionText, barCaption, labelsVisible);
            EngineHudNumericReadout.SetDescriptor(
                centerBarLabelText,
                centerBarLabel,
                labelsVisible && rpmValueCenter != null && rpmValueCenter.gameObject.activeSelf);
            EngineHudNumericReadout.SetDescriptor(
                leftBarLabelText,
                leftBarLabel,
                labelsVisible && rpmValueL != null && rpmValueL.gameObject.activeSelf);
            EngineHudNumericReadout.SetDescriptor(
                rightBarLabelText,
                rightBarLabel,
                labelsVisible && rpmValueR != null && rpmValueR.gameObject.activeSelf);
        }

        private void HideBarLabels()
        {
            EngineHudNumericReadout.SetDescriptor(barCaptionText, barCaption, false);
            EngineHudNumericReadout.SetDescriptor(centerBarLabelText, centerBarLabel, false);
            EngineHudNumericReadout.SetDescriptor(leftBarLabelText, leftBarLabel, false);
            EngineHudNumericReadout.SetDescriptor(rightBarLabelText, rightBarLabel, false);
        }

        private void ApplyScaleLabels()
        {
            if (!showScaleLabels || nrFrame == null)
            {
                HideScaleLabels();
                return;
            }

            int[] values = EngineHudNumericReadout.BuildScaleValues(maxRPMPercent, scaleLabelStepPercent);
            for (int i = 0; i < nrScaleLabels.Length; i++)
            {
                bool visible = i < values.Length;
                EngineHudNumericReadout.ConfigureExisting(
                    nrScaleLabels[i], scaleLabelFontSize, nrFrame.gameObject.layer);
                EngineHudNumericReadout.SetScaleLabel(
                    nrScaleLabels[i], visible ? values[i] : 0, visible);
            }
        }

        private void HideScaleLabels()
        {
            for (int i = 0; i < nrScaleLabels.Length; i++)
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
            UpdateBarLabels();
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
