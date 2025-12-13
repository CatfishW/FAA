using System.Collections.Generic;
using UnityEngine;
using VoiceControl.Core;
using WeatherRadar;

namespace VoiceControl.Adapters
{
    /// <summary>
    /// Voice command adapter for Weather Radar system.
    /// Implements IVoiceCommandTarget to expose radar controls to voice commands.
    /// 
    /// Design: High cohesion - only handles weather radar commands.
    /// Low coupling - references RadarControlPanel via interface, not directly.
    /// </summary>
    [AddComponentMenu("Voice Control/Weather Radar Voice Adapter")]
    public class WeatherRadarVoiceAdapter : MonoBehaviour, IVoiceCommandTarget
    {
        [Header("Target Components")]
        [SerializeField] private RadarControlPanel controlPanel;
        [SerializeField] private WeatherRadarDataProvider dataProvider;
        
        [Header("Settings")]
        [SerializeField] private bool autoFindComponents = true;
        [SerializeField] private bool verboseLogging = true;
        
        public string TargetId => "weather_radar";
        public string DisplayName => "Weather Radar";
        
        private VoiceCommandInfo[] _commands;
        
        private void Awake()
        {
            if (autoFindComponents)
            {
                AutoFindComponents();
            }
        }
        
        private void Start()
        {
            // Register with the voice command registry
            if (VoiceCommandRegistry.Instance != null)
            {
                VoiceCommandRegistry.Instance.RegisterTarget(this);
            }
        }
        
        private void OnDestroy()
        {
            if (VoiceCommandRegistry.Instance != null)
            {
                VoiceCommandRegistry.Instance.UnregisterTarget(TargetId);
            }
        }
        
        private void AutoFindComponents()
        {
            if (controlPanel == null)
            {
                controlPanel = FindObjectOfType<RadarControlPanel>();
            }
            if (dataProvider == null)
            {
                dataProvider = FindObjectOfType<WeatherRadarDataProvider>();
            }
            
            Log($"Found components - ControlPanel: {controlPanel != null}, DataProvider: {dataProvider != null}");
        }
        
        public VoiceCommandInfo[] GetAvailableCommands()
        {
            if (_commands != null)
                return _commands;
            
            _commands = new VoiceCommandInfo[]
            {
                new VoiceCommandInfo(
                    "increase_range",
                    "Increase the weather radar range to show a wider area"
                ),
                new VoiceCommandInfo(
                    "decrease_range",
                    "Decrease the weather radar range to show a smaller, more detailed area"
                ),
                new VoiceCommandInfo(
                    "increase_tilt",
                    "Tilt the radar antenna up to scan higher altitudes"
                ),
                new VoiceCommandInfo(
                    "decrease_tilt",
                    "Tilt the radar antenna down to scan lower altitudes"
                ),
                new VoiceCommandInfo(
                    "set_tilt",
                    "Set the radar antenna tilt to a specific angle",
                    new VoiceCommandParameter(
                        "degrees",
                        "number",
                        "Tilt angle in degrees (-15 to +15). Positive values tilt up, negative tilt down.",
                        true
                    )
                ),
                new VoiceCommandInfo(
                    "increase_gain",
                    "Increase radar gain/sensitivity by 1 dB"
                ),
                new VoiceCommandInfo(
                    "decrease_gain",
                    "Decrease radar gain/sensitivity by 1 dB"
                ),
                new VoiceCommandInfo(
                    "set_gain",
                    "Set radar gain/sensitivity to a specific dB value",
                    new VoiceCommandParameter(
                        "dB",
                        "number",
                        "Gain offset in dB (-8 to +8). Use 0 for normal, positive for more sensitivity, negative for less.",
                        true
                    )
                ),
                new VoiceCommandInfo(
                    "set_range",
                    "Set the weather radar range to a specific value in nautical miles",
                    new VoiceCommandParameter(
                        "range_nm",
                        "number",
                        "Range in nautical miles (typical values: 10, 20, 40, 80, 160, 320)",
                        true
                    )
                ),
                new VoiceCommandInfo(
                    "set_mode",
                    "Set the weather radar operating mode",
                    new VoiceCommandParameter(
                        "mode",
                        "string",
                        "Radar mode: WX (weather), WX_T (weather+turbulence), TURB (turbulence only), MAP (ground mapping), STBY (standby)",
                        true,
                        new[] { "WX", "WX_T", "TURB", "MAP", "STBY" }
                    )
                ),
                new VoiceCommandInfo(
                    "show_panel",
                    "Show the weather radar control panel"
                ),
                new VoiceCommandInfo(
                    "hide_panel",
                    "Hide the weather radar control panel"
                ),
                new VoiceCommandInfo(
                    "toggle_panel",
                    "Toggle the visibility of the weather radar control panel"
                )
            };
            
            return _commands;
        }
        
        public bool ExecuteCommand(string commandName, Dictionary<string, object> parameters)
        {
            Log($"Executing command: {commandName}");
            
            switch (commandName.ToLower())
            {
                case "increase_range":
                    if (controlPanel != null)
                    {
                        controlPanel.IncreaseRange();
                        return true;
                    }
                    break;
                    
                case "decrease_range":
                    if (controlPanel != null)
                    {
                        controlPanel.DecreaseRange();
                        return true;
                    }
                    break;
                    
                case "increase_tilt":
                    if (controlPanel != null)
                    {
                        controlPanel.IncreaseTilt();
                        return true;
                    }
                    break;
                    
                case "decrease_tilt":
                    if (controlPanel != null)
                    {
                        controlPanel.DecreaseTilt();
                        return true;
                    }
                    break;
                    
                case "set_tilt":
                    return HandleSetTilt(parameters);
                    
                case "increase_gain":
                    if (controlPanel != null)
                    {
                        controlPanel.IncreaseGain();
                        return true;
                    }
                    break;
                    
                case "decrease_gain":
                    if (controlPanel != null)
                    {
                        controlPanel.DecreaseGain();
                        return true;
                    }
                    break;
                    
                case "set_gain":
                    return HandleSetGain(parameters);
                    
                case "set_range":
                    return HandleSetRange(parameters);
                    
                case "set_mode":
                    return HandleSetMode(parameters);
                    
                case "show_panel":
                    if (controlPanel != null)
                    {
                        controlPanel.SetVisibility(true);
                        return true;
                    }
                    break;
                    
                case "hide_panel":
                    if (controlPanel != null)
                    {
                        controlPanel.SetVisibility(false);
                        return true;
                    }
                    break;
                    
                case "toggle_panel":
                    if (controlPanel != null)
                    {
                        controlPanel.ToggleVisibility();
                        return true;
                    }
                    break;
                    
                default:
                    Log($"Unknown command: {commandName}");
                    break;
            }
            
            return false;
        }
        
        private bool HandleSetMode(Dictionary<string, object> parameters)
        {
            if (controlPanel == null)
            {
                Log("Control panel not found");
                return false;
            }
            
            if (!parameters.TryGetValue("mode", out var modeObj))
            {
                Log("Mode parameter not provided");
                return false;
            }
            
            string modeStr = modeObj.ToString().ToUpper().Replace("+", "_");
            
            RadarMode mode;
            switch (modeStr)
            {
                case "WX":
                    mode = RadarMode.WX;
                    break;
                case "WX_T":
                case "WXT":
                case "WEATHER_TURBULENCE":
                    mode = RadarMode.WX_T;
                    break;
                case "TURB":
                case "TURBULENCE":
                    mode = RadarMode.TURB;
                    break;
                case "MAP":
                case "GROUND":
                    mode = RadarMode.MAP;
                    break;
                case "STBY":
                case "STANDBY":
                    mode = RadarMode.STBY;
                    break;
                default:
                    Log($"Unknown mode: {modeStr}");
                    return false;
            }
            
            controlPanel.SetMode(mode);
            Log($"Set mode to {mode}");
            return true;
        }
        
        private bool HandleSetTilt(Dictionary<string, object> parameters)
        {
            if (controlPanel == null)
            {
                Log("Control panel not found");
                return false;
            }
            
            if (!parameters.TryGetValue("degrees", out var degreesObj))
            {
                Log("Degrees parameter not provided");
                return false;
            }
            
            float degrees = 0f;
            if (degreesObj is double d)
                degrees = (float)d;
            else if (degreesObj is long l)
                degrees = l;
            else if (degreesObj is int i)
                degrees = i;
            else if (!float.TryParse(degreesObj.ToString(), out degrees))
            {
                Log($"Invalid degrees value: {degreesObj}");
                return false;
            }
            
            controlPanel.SetTilt(degrees);
            Log($"Set tilt to {degrees}°");
            return true;
        }
        
        private bool HandleSetGain(Dictionary<string, object> parameters)
        {
            if (controlPanel == null)
            {
                Log("Control panel not found");
                return false;
            }
            
            if (!parameters.TryGetValue("dB", out var dBObj))
            {
                Log("dB parameter not provided");
                return false;
            }
            
            float dB = 0f;
            if (dBObj is double d)
                dB = (float)d;
            else if (dBObj is long l)
                dB = l;
            else if (dBObj is int i)
                dB = i;
            else if (!float.TryParse(dBObj.ToString(), out dB))
            {
                Log($"Invalid dB value: {dBObj}");
                return false;
            }
            
            controlPanel.SetGain(dB);
            Log($"Set gain to {dB} dB");
            return true;
        }
        
        private bool HandleSetRange(Dictionary<string, object> parameters)
        {
            if (dataProvider == null)
            {
                Log("Data provider not found");
                return false;
            }
            
            if (!parameters.TryGetValue("range_nm", out var rangeObj))
            {
                Log("range_nm parameter not provided");
                return false;
            }
            
            float range = 0f;
            if (rangeObj is double d)
                range = (float)d;
            else if (rangeObj is long l)
                range = l;
            else if (rangeObj is int i)
                range = i;
            else if (!float.TryParse(rangeObj.ToString(), out range))
            {
                Log($"Invalid range value: {rangeObj}");
                return false;
            }
            
            dataProvider.SetRange(range);
            Log($"Set range to {range} NM");
            return true;
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[WeatherRadarVoiceAdapter] {message}");
            }
        }
    }
}
