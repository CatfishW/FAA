using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AircraftControl.Core;

namespace HUDControl.Elements
{
    /// <summary>
    /// VSI (Vertical Speed Indicator) element for Image-based HUD.
    /// Animates VSI pointer, tape, and digital readout with auto-sync and calibration.
    /// </summary>
    [AddComponentMenu("HUD Control/Elements/VSI")]
    public class VSIElement : Core.HUDElementBase
    {
        #region Inspector - UI References
        
        [Header("VSI References")]
        [Tooltip("VSI pointer that rotates")]
        [SerializeField] private RectTransform vsiPointer;
        
        [Tooltip("VSI tape that moves vertically")]
        [SerializeField] private RectTransform vsiTape;
        
        [Tooltip("Digital readout for VS value")]
        [SerializeField] private TMP_Text digitalReadout;
        
        #endregion
        
        #region Inspector - Data Source
        
        [Header("Data Source")]
        [Tooltip("Direct reference to AircraftController for standalone operation. If null, uses data from HUDController.")]
        [SerializeField] private AircraftController aircraftController;
        
        [Tooltip("Auto-find AircraftController in scene if not assigned")]
        [SerializeField] private bool autoFindController = true;
        
        [Tooltip("Update independently (for standalone use outside HUDController)")]
        [SerializeField] private bool standaloneUpdate = false;
        
        #endregion
        
        #region Inspector - Animation Enables
        
        [Header("Animation Enables")]
        [Tooltip("Enable VSI pointer rotation")]
        [SerializeField] private bool enablePointer = true;
        
        [Tooltip("Enable VSI tape movement")]
        [SerializeField] private bool enableTape = true;
        
        [Tooltip("Enable digital readout")]
        [SerializeField] private bool enableReadout = true;
        
        #endregion
        
        #region Inspector - Calibration
        
        [Header("Auto-Calibration")]
        [Tooltip("Automatically calibrate tape movement from sprite dimensions")]
        [SerializeField] private bool autoCalibrateTape = true;
        
        [Tooltip("Automatically sync readout format with tape scale")]
        [SerializeField] private bool autoSyncReadout = true;
        
        [Tooltip("FPM value represented at top of tape sprite (used for auto-calibration)")]
        [SerializeField] private float tapeTopFpm = 2000f;
        
        [Tooltip("FPM value represented at bottom of tape sprite")]
        [SerializeField] private float tapeBottomFpm = -2000f;
        
        [Tooltip("Height of visible tape window in pixels (for clamping)")]
        [SerializeField] private float visibleWindowHeight = 100f;
        
        #endregion
        
        #region Inspector - Bounds
        
        [Header("VSI Bounds")]
        [Tooltip("Rotation angle at max climb (positive = clockwise)")]
        [SerializeField] private float maxClimbAngle = 90f;
        
        [Tooltip("Rotation angle at max descent (negative = counter-clockwise)")]
        [SerializeField] private float maxDescentAngle = -90f;
        
        [Tooltip("Maximum VS in fpm for full deflection")]
        [SerializeField] private float maxVSFpm = 2000f;
        
        [Tooltip("Pixels per FPM for tape movement (auto-calculated if autoCalibrateTape is true)")]
        [SerializeField] private float pixelsPerFpm = 0.05f;
        
        [Tooltip("Maximum tape offset in pixels (fallback if auto-calibrate disabled)")]
        [SerializeField] private float maxTapeOffsetPixels = 100f;
        
        [Header("Tape Position Bounds")]
        [Tooltip("Minimum Y position for tape (bottom limit)")]
        [SerializeField] private float tapeMinPosY = -1.1f;
        
        [Tooltip("Maximum Y position for tape (top limit)")]
        [SerializeField] private float tapeMaxPosY = 1.1f;
        
        #endregion
        
        #region Inspector - Readout Format
        
        [Header("Readout Format")]
        [Tooltip("Round readout to this interval (e.g., 100 = round to nearest 100)")]
        [SerializeField] private int readoutRoundingInterval = 100;
        
        [Tooltip("Show + sign for positive values")]
        [SerializeField] private bool showPlusSign = false;
        
        [Tooltip("Minimum digits to display (pads with zeros)")]
        [SerializeField] private int minimumDigits = 3;
        
        #endregion
        
        // Runtime state
        private float displayedVS;
        private Vector2 tapeBasePos;
        private float calculatedPixelsPerFpm;
        private float tapeHeight;
        private Image tapeImage;
        private bool calibrated;
        
        public override string ElementId => "VSI";
        
        /// <summary>
        /// Current displayed vertical speed (smoothed)
        /// </summary>
        public float DisplayedVS => displayedVS;
        
        /// <summary>
        /// Calibrated pixels per FPM value
        /// </summary>
        public float PixelsPerFpm => autoCalibrateTape ? calculatedPixelsPerFpm : pixelsPerFpm;
        
        protected override void OnInitialize()
        {
            displayedVS = 0f;
            calibrated = false;
            
            // Find AircraftController if needed
            if (aircraftController == null && autoFindController)
            {
                aircraftController = FindObjectOfType<AircraftController>();
                if (aircraftController != null)
                {
                    Debug.Log($"[VSIElement] Auto-found AircraftController: {aircraftController.name}");
                }
            }
            
            CacheTapeReferences();
            CalibrateFromTapeSprite();
        }
        
        /// <summary>
        /// Unity Update - for standalone operation only
        /// </summary>
        protected virtual void Update()
        {
            if (!standaloneUpdate || !isEnabled) return;
            
            // Get data directly from AircraftController
            if (aircraftController != null)
            {
                var state = aircraftController.State;
                if (state != null)
                {
                    // Calculate frame-rate independent smoothing
                    smoothing = Core.HUDAnimator.CalculateSmoothing(animationSpeed);
                    
                    // Process the real aircraft data
                    ProcessVerticalSpeed(state.VerticalSpeedFpm);
                }
            }
        }
        
        /// <summary>
        /// Process vertical speed value (from any source)
        /// </summary>
        private void ProcessVerticalSpeed(float verticalSpeedFpm)
        {
            // Ensure calibration on first update
            if (!calibrated)
            {
                CalibrateFromTapeSprite();
            }
            
            // Smooth the VS value
            float targetVS = Mathf.Clamp(verticalSpeedFpm, -maxVSFpm, maxVSFpm);
            displayedVS = Core.HUDAnimator.SmoothValue(displayedVS, targetVS, smoothing);
            
            // Update all synchronized displays
            UpdatePointer();
            UpdateTape();
            UpdateReadout();
        }
        
        /// <summary>
        /// Cache tape image and dimensions
        /// </summary>
        private void CacheTapeReferences()
        {
            if (vsiTape != null)
            {
                tapeBasePos = vsiTape.anchoredPosition;
                tapeImage = vsiTape.GetComponent<Image>();
                tapeHeight = vsiTape.rect.height;
            }
        }
        
        /// <summary>
        /// Auto-calibrate tape movement based on sprite dimensions and FPM range
        /// </summary>
        private void CalibrateFromTapeSprite()
        {
            if (!autoCalibrateTape || vsiTape == null)
            {
                calculatedPixelsPerFpm = pixelsPerFpm;
                calibrated = true;
                return;
            }
            
            // Get tape height from RectTransform
            float spriteHeight = vsiTape.rect.height;
            
            // If we have an Image component with a sprite, use its native size
            if (tapeImage != null && tapeImage.sprite != null)
            {
                spriteHeight = tapeImage.sprite.rect.height;
                
                // Account for any scaling
                float scaleY = vsiTape.localScale.y;
                if (Mathf.Abs(scaleY) > 0.001f)
                {
                    spriteHeight *= scaleY;
                }
            }
            
            // Calculate FPM range
            float fpmRange = tapeTopFpm - tapeBottomFpm;
            
            if (Mathf.Abs(fpmRange) > 0.001f && spriteHeight > 0)
            {
                // Pixels per FPM = sprite height / FPM range
                calculatedPixelsPerFpm = spriteHeight / fpmRange;
                
                // Store tape height for offset calculations
                tapeHeight = spriteHeight;
                
                calibrated = true;
                
                Debug.Log($"[VSIElement] Auto-calibrated: {calculatedPixelsPerFpm:F4} pixels/FPM " +
                         $"(sprite height: {spriteHeight}px, range: {fpmRange} FPM)");
            }
            else
            {
                calculatedPixelsPerFpm = pixelsPerFpm;
                Debug.LogWarning("[VSIElement] Auto-calibration failed - using fallback pixelsPerFpm");
            }
        }
        
        protected override void OnUpdateElement(AircraftState state)
        {
            // Skip if using standalone update (avoid double processing)
            if (standaloneUpdate) return;
            
            // Use the state's VerticalSpeedFpm from OwnAircraft/AircraftController
            ProcessVerticalSpeed(state.VerticalSpeedFpm);
        }
        
        /// <summary>
        /// Update VSI pointer rotation
        /// </summary>
        private void UpdatePointer()
        {
            if (!enablePointer || vsiPointer == null) return;
            
            // Normalize VS to -1 to +1 range
            float normalizedVS = Mathf.Clamp(displayedVS / maxVSFpm, -1f, 1f);
            
            // Calculate rotation angle
            float rotation;
            if (normalizedVS >= 0)
            {
                rotation = Mathf.Lerp(0f, maxClimbAngle, normalizedVS);
            }
            else
            {
                rotation = Mathf.Lerp(0f, maxDescentAngle, -normalizedVS);
            }
            
            vsiPointer.localRotation = Quaternion.Euler(0, 0, rotation);
        }
        
        /// <summary>
        /// Update VSI tape position - synchronized with sprite scale
        /// Tape Y position is clamped within [tapeMinPosY, tapeMaxPosY] range
        /// </summary>
        private void UpdateTape()
        {
            if (!enableTape || vsiTape == null) return;
            
            // Calculate normalized VS (-1 to +1) based on maxVSFpm
            float normalizedVS = Mathf.Clamp(displayedVS / maxVSFpm, -1f, 1f);
            
            // Map normalized VS to Y position:
            // - At 0 FPM: tape at Y = 0 (centered)
            // - At +maxVSFpm (climb): tape at Y = tapeMinPosY (-1.1) - tape moves DOWN
            // - At -maxVSFpm (descent): tape at Y = tapeMaxPosY (+1.1) - tape moves UP
            float targetY = -normalizedVS * tapeMaxPosY;
            
            // Clamp final Y position to bounds
            targetY = Mathf.Clamp(targetY, tapeMinPosY, tapeMaxPosY);
            
            // Apply position
            Vector2 newPos = vsiTape.anchoredPosition;
            newPos.y = targetY;
            vsiTape.anchoredPosition = newPos;
        }
        
        /// <summary>
        /// Update digital readout - synchronized with displayed VS
        /// </summary>
        private void UpdateReadout()
        {
            if (!enableReadout || digitalReadout == null) return;
            
            // Round to interval
            int roundedVS = readoutRoundingInterval > 0 
                ? Mathf.RoundToInt(displayedVS / readoutRoundingInterval) * readoutRoundingInterval
                : Mathf.RoundToInt(displayedVS);
            
            // Format the display
            string format = minimumDigits > 0 ? $"D{minimumDigits}" : "";
            string valueStr = Mathf.Abs(roundedVS).ToString(format);
            
            string prefix;
            if (roundedVS > 0 && showPlusSign)
            {
                prefix = "+";
            }
            else if (roundedVS < 0)
            {
                prefix = "-";
            }
            else
            {
                prefix = "";
            }
            
            digitalReadout.text = prefix + valueStr;
        }
        
        /// <summary>
        /// Get the current displayed vertical speed
        /// </summary>
        public float GetDisplayedVS() => displayedVS;
        
        /// <summary>
        /// Force recalibration of tape parameters
        /// </summary>
        public void Recalibrate()
        {
            calibrated = false;
            CacheTapeReferences();
            CalibrateFromTapeSprite();
        }
        
        /// <summary>
        /// Manually set the tape calibration values
        /// </summary>
        public void SetTapeCalibration(float topFpm, float bottomFpm, float windowHeight)
        {
            tapeTopFpm = topFpm;
            tapeBottomFpm = bottomFpm;
            visibleWindowHeight = windowHeight;
            Recalibrate();
        }
        
        #region Editor Support
        
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            
            // Clamp values
            maxVSFpm = Mathf.Max(100f, maxVSFpm);
            readoutRoundingInterval = Mathf.Max(1, readoutRoundingInterval);
            minimumDigits = Mathf.Clamp(minimumDigits, 0, 6);
            visibleWindowHeight = Mathf.Max(10f, visibleWindowHeight);
            
            // Recalibrate in editor if playing
            if (Application.isPlaying && autoCalibrateTape)
            {
                Recalibrate();
            }
        }
        
        [ContextMenu("Auto-Find VSI References")]
        private void AutoFindVSIReferences()
        {
            // Find pointer
            if (vsiPointer == null)
            {
                vsiPointer = FindChildByName<RectTransform>("Pointer", "VSI Pointer", "Needle", "Hand");
            }
            
            // Find tape
            if (vsiTape == null)
            {
                vsiTape = FindChildByName<RectTransform>("Tape", "VSI Tape", "Scale", "VSITape");
            }
            
            // Find readout
            if (digitalReadout == null)
            {
                digitalReadout = FindChildByName<TMP_Text>("Readout", "Digital", "Value", "Text", "VSI Readout");
            }
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[VSIElement] Auto-find complete - Pointer: {vsiPointer != null}, Tape: {vsiTape != null}, Readout: {digitalReadout != null}");
        }
        
        [ContextMenu("Calibrate From Tape Sprite")]
        private void EditorCalibrateFromSprite()
        {
            CacheTapeReferences();
            CalibrateFromTapeSprite();
            UnityEditor.EditorUtility.SetDirty(this);
        }
        
        [ContextMenu("Sync All Settings")]
        private void SyncAllSettings()
        {
            // Ensure maxVSFpm matches tape range
            if (autoCalibrateTape)
            {
                maxVSFpm = Mathf.Max(Mathf.Abs(tapeTopFpm), Mathf.Abs(tapeBottomFpm));
            }
            
            // Sync pointer angles to be symmetric if desired
            if (Mathf.Abs(maxClimbAngle) != Mathf.Abs(maxDescentAngle))
            {
                float maxAngle = Mathf.Max(Mathf.Abs(maxClimbAngle), Mathf.Abs(maxDescentAngle));
                maxClimbAngle = maxAngle;
                maxDescentAngle = -maxAngle;
            }
            
            Recalibrate();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[VSIElement] Settings synchronized");
        }
        
        private T FindChildByName<T>(params string[] names) where T : Component
        {
            foreach (string name in names)
            {
                // Direct child
                Transform child = transform.Find(name);
                if (child != null)
                {
                    T component = child.GetComponent<T>();
                    if (component != null) return component;
                }
                
                // Recursive search
                T found = FindInChildren<T>(transform, name);
                if (found != null) return found;
            }
            return null;
        }
        
        private T FindInChildren<T>(Transform parent, string name) where T : Component
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains(name) || name.Contains(child.name))
                {
                    T component = child.GetComponent<T>();
                    if (component != null) return component;
                }
                
                T found = FindInChildren<T>(child, name);
                if (found != null) return found;
            }
            return null;
        }
        
        [ContextMenu("Debug Info")]
        private void PrintDebugInfo()
        {
            Debug.Log("=== VSI Element Debug Info ===");
            Debug.Log($"Pointer: {(vsiPointer != null ? vsiPointer.name : "None")} (enabled: {enablePointer})");
            Debug.Log($"Tape: {(vsiTape != null ? vsiTape.name : "None")} (enabled: {enableTape})");
            Debug.Log($"Readout: {(digitalReadout != null ? digitalReadout.name : "None")} (enabled: {enableReadout})");
            Debug.Log($"Max VS FPM: {maxVSFpm}");
            Debug.Log($"Tape Range: {tapeBottomFpm} to {tapeTopFpm} FPM");
            Debug.Log($"Auto-Calibrate: {autoCalibrateTape}");
            Debug.Log($"Pixels Per FPM: {PixelsPerFpm:F4} (calculated: {calculatedPixelsPerFpm:F4})");
            Debug.Log($"Tape Height: {tapeHeight}px");
            Debug.Log($"Visible Window: {visibleWindowHeight}px");
            Debug.Log($"Current Displayed VS: {displayedVS:F1} FPM");
        }
#endif
        
        #endregion
    }
}
