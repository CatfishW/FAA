using UnityEngine;
using System;

namespace WeatherRadar
{
    /// <summary>
    /// Handles radar mode switching and mode-specific behaviors.
    /// Manages transitions between WX, WX+T, TURB, MAP, and STBY modes.
    /// </summary>
    public class RadarModeController : MonoBehaviour
    {
        [Header("Mode Settings")]
        [SerializeField] private RadarMode defaultMode = RadarMode.WX;
        [SerializeField] private bool rememberLastMode = true;

        [Header("Mode-Specific Parameters")]
        [SerializeField] private ModeParameters wxModeParams;
        [SerializeField] private ModeParameters wxTModeParams;
        [SerializeField] private ModeParameters turbModeParams;
        [SerializeField] private ModeParameters mapModeParams;

        private WeatherRadarDataProvider dataProvider;
        private RadarMode currentMode;
        private RadarMode previousMode;

        /// <summary>
        /// Event fired when mode changes
        /// </summary>
        public event Action<RadarMode, RadarMode> OnModeTransition;

        /// <summary>
        /// Current radar mode
        /// </summary>
        public RadarMode CurrentMode => currentMode;

        /// <summary>
        /// Previous radar mode (before last change)
        /// </summary>
        public RadarMode PreviousMode => previousMode;

        /// <summary>
        /// Initialize the mode controller
        /// </summary>
        public void Initialize(WeatherRadarDataProvider provider)
        {
            dataProvider = provider;

            if (dataProvider != null)
            {
                currentMode = dataProvider.RadarData.currentMode;
                dataProvider.OnModeChanged += OnProviderModeChanged;
            }
            else
            {
                currentMode = defaultMode;
            }
        }

        private void OnDestroy()
        {
            if (dataProvider != null)
            {
                dataProvider.OnModeChanged -= OnProviderModeChanged;
            }
        }

        private void OnProviderModeChanged(RadarMode newMode)
        {
            if (newMode != currentMode)
            {
                TransitionToMode(newMode);
            }
        }

        /// <summary>
        /// Set the radar mode
        /// </summary>
        public void SetMode(RadarMode mode)
        {
            if (mode != currentMode)
            {
                TransitionToMode(mode);

                if (dataProvider != null)
                {
                    dataProvider.SetMode(mode);
                }
            }
        }

        /// <summary>
        /// Toggle between current mode and standby
        /// </summary>
        public void ToggleStandby()
        {
            if (currentMode == RadarMode.STBY)
            {
                // Return to previous mode
                SetMode(rememberLastMode ? previousMode : defaultMode);
            }
            else
            {
                // Go to standby
                SetMode(RadarMode.STBY);
            }
        }

        /// <summary>
        /// Cycle through weather modes (WX -> WX+T -> TURB -> WX)
        /// </summary>
        public void CycleWeatherModes()
        {
            switch (currentMode)
            {
                case RadarMode.WX:
                    SetMode(RadarMode.WX_T);
                    break;
                case RadarMode.WX_T:
                    SetMode(RadarMode.TURB);
                    break;
                case RadarMode.TURB:
                    SetMode(RadarMode.WX);
                    break;
                default:
                    SetMode(RadarMode.WX);
                    break;
            }
        }

        private void TransitionToMode(RadarMode newMode)
        {
            previousMode = currentMode;
            currentMode = newMode;

            // Apply mode-specific parameters
            ApplyModeParameters(newMode);

            // Fire transition event
            OnModeTransition?.Invoke(previousMode, currentMode);

            Debug.Log($"[WeatherRadar] Mode transition: {previousMode} -> {currentMode}");
        }

        private void ApplyModeParameters(RadarMode mode)
        {
            ModeParameters parameters = GetModeParameters(mode);
            
            if (parameters == null || dataProvider == null) return;

            // Apply gain adjustment
            if (parameters.useCustomGain)
            {
                dataProvider.SetGain(parameters.defaultGain);
            }
        }

        private ModeParameters GetModeParameters(RadarMode mode)
        {
            switch (mode)
            {
                case RadarMode.WX:
                    return wxModeParams ?? ModeParameters.DefaultWX;
                case RadarMode.WX_T:
                    return wxTModeParams ?? ModeParameters.DefaultWXT;
                case RadarMode.TURB:
                    return turbModeParams ?? ModeParameters.DefaultTURB;
                case RadarMode.MAP:
                    return mapModeParams ?? ModeParameters.DefaultMAP;
                case RadarMode.STBY:
                    return ModeParameters.DefaultSTBY;
                default:
                    return ModeParameters.DefaultWX;
            }
        }

        /// <summary>
        /// Get display properties for current mode
        /// </summary>
        public ModeDisplayProperties GetCurrentModeDisplay()
        {
            ModeParameters parameters = GetModeParameters(currentMode);
            return new ModeDisplayProperties
            {
                showWeatherReturns = parameters.showWeatherReturns,
                showTurbulence = parameters.showTurbulence,
                showGroundClutter = parameters.showGroundClutter,
                scanActive = parameters.scanActive,
                sweepSpeed = parameters.sweepSpeed
            };
        }
    }
}
