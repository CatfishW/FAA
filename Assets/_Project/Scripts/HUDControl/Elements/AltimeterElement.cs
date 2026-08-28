using UnityEngine;
using TMPro;
using AircraftControl.Core;

namespace HUDControl.Elements
{
    /// <summary>
    /// Altimeter element for Image-based HUD.
    /// Animates altitude tape vertical position with strict bounds.
    /// </summary>
    [AddComponentMenu("HUD Control/Elements/Altimeter")]
    public class AltimeterElement : Core.HUDElementBase
    {
        #region Inspector - UI References
        
        [Header("Altimeter References")]
        [Tooltip("Altitude tape that scrolls vertically")]
        [SerializeField] private RectTransform altitudeTape;
        
        [Tooltip("Altitude readout text")]
        [SerializeField] private TMP_Text altitudeReadout;
        
        [Tooltip("Altimeter window panel (non-animating)")]
        [SerializeField] private RectTransform windowPanel;
        
        #endregion
        
        #region Inspector - Animation Enables
        
        [Header("Animation Enables")]
        [Tooltip("Enable altitude tape movement")]
        [SerializeField] private bool enableTape = true;
        
        [Tooltip("Enable altitude readout")]
        [SerializeField] private bool enableReadout = true;
        
        #endregion
        
        #region Inspector - Bounds
        
        [Header("Altimeter Bounds")]
        [Tooltip("Pixels per foot of altitude")]
        [SerializeField] private float pixelsPerFoot = 0.01f;
        
        [Tooltip("Maximum tape offset in pixels (KEEP SMALL)")]
        [SerializeField] private float maxTapeOffsetPixels = 30f;
        
        [Tooltip("Reference altitude (tape centered at this altitude)")]
        [SerializeField] private float referenceAltitude = 1000f;
        
        [Tooltip("Display format")]
        [SerializeField] private string displayFormat = "{0:0}";
        
        #endregion
        
        private float displayedAltitude;
        private float targetAltitude;
        private float lastDisplayedAltitude = -1f;
        private Vector2 tapeBasePos;
        private bool hasExternalAltitude;
        private bool externalDataUnavailable;
        
        public override string ElementId => "Altimeter";
        
        protected override void OnInitialize()
        {
            displayedAltitude = 0f;
            targetAltitude = 0f;
            hasExternalAltitude = false;
            externalDataUnavailable = false;
            lastDisplayedAltitude = -1f;
            
            if (altitudeTape != null)
                tapeBasePos = altitudeTape.anchoredPosition;

            SetTapeAvailable(true);
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            if (state == null || externalDataUnavailable)
            {
                return;
            }

            float target = hasExternalAltitude
                ? targetAltitude
                : state.AltitudeFeet;
            float effectiveSmoothing = smoothing > 0f
                ? smoothing
                : Core.HUDAnimator.CalculateSmoothing(animationSpeed);
            displayedAltitude = Core.HUDAnimator.SmoothValue(displayedAltitude, target, effectiveSmoothing);
            
            // Altitude tape movement
            if (enableTape && altitudeTape != null)
            {
                // Calculate offset relative to reference
                float deltaAlt = displayedAltitude - referenceAltitude;
                float offset = deltaAlt * pixelsPerFoot;
                offset = Mathf.Clamp(offset, -maxTapeOffsetPixels, maxTapeOffsetPixels);
                
                Vector2 newPos = tapeBasePos;
                newPos.y += offset;
                altitudeTape.anchoredPosition = newPos;
            }
            
            // Altitude readout
            if (enableReadout && altitudeReadout != null)
            {
                int rounded = Mathf.RoundToInt(displayedAltitude);
                
                if (rounded != Mathf.RoundToInt(lastDisplayedAltitude))
                {
                    altitudeReadout.text = string.Format(displayFormat, rounded);
                    lastDisplayedAltitude = rounded;
                }
            }
        }
        
        /// <summary>
        /// Feed an authoritative X-Plane MSL altitude in feet.
        /// </summary>
        public void SetAltitudeData(float value, bool valid)
        {
            if (!valid || float.IsNaN(value) || float.IsInfinity(value))
            {
                ClearExternalData();
                return;
            }

            targetAltitude = value;
            if (!hasExternalAltitude)
            {
                displayedAltitude = targetAltitude;
            }

            hasExternalAltitude = true;
            externalDataUnavailable = false;
            SetTapeAvailable(true);
            UpdateReadout();
        }

        /// <summary>
        /// Bind only objects authored in the scene or prefab. No UI is created here.
        /// </summary>
        public void ConfigureVisuals(RectTransform tape, TMP_Text readout, RectTransform window)
        {
            altitudeTape = tape;
            altitudeReadout = readout;
            windowPanel = window;
            tapeBasePos = altitudeTape != null ? altitudeTape.anchoredPosition : Vector2.zero;
            SetTapeAvailable(true);
            UpdateReadout();
        }

        public void ClearExternalData()
        {
            hasExternalAltitude = false;
            externalDataUnavailable = true;
            targetAltitude = 0f;
            displayedAltitude = 0f;
            SetTapeAvailable(false);
            SetReadoutUnavailable();
        }

        public bool HasExternalData => hasExternalAltitude && !externalDataUnavailable;
        public float GetTargetAltitude() => targetAltitude;
        public float GetDisplayedAltitude() => displayedAltitude;

        private void UpdateReadout()
        {
            if (!enableReadout || altitudeReadout == null)
            {
                return;
            }

            int rounded = Mathf.RoundToInt(displayedAltitude);
            if (rounded != Mathf.RoundToInt(lastDisplayedAltitude) || altitudeReadout.text == "---")
            {
                altitudeReadout.text = string.Format(displayFormat, rounded);
                lastDisplayedAltitude = rounded;
            }

            altitudeReadout.color = new Color(0.2f, 1f, 0.2f, 1f);
        }

        private void SetReadoutUnavailable()
        {
            if (altitudeReadout == null)
            {
                return;
            }

            altitudeReadout.text = "---";
            altitudeReadout.color = new Color(0.2f, 1f, 0.2f, 0.46f);
        }

        private static void SetTapeAvailable(RectTransform tape, bool available)
        {
            if (tape != null && tape.gameObject.activeSelf != available)
            {
                tape.gameObject.SetActive(available);
            }
        }

        private void SetTapeAvailable(bool available)
        {
            SetTapeAvailable(altitudeTape, available);
        }
    }
}
