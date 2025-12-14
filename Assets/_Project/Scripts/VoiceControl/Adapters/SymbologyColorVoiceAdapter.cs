using System.Collections.Generic;
using UnityEngine;
using VoiceControl.Core;
using FAA.Customization;

namespace VoiceControl.Adapters
{
    /// <summary>
    /// Voice command adapter for Symbology Color Manager.
    /// Implements IVoiceCommandTarget to expose symbology color controls to voice commands.
    /// </summary>
    [AddComponentMenu("Voice Control/Symbology Color Voice Adapter")]
    public class SymbologyColorVoiceAdapter : MonoBehaviour, IVoiceCommandTarget
    {
        [Header("Target Components")]
        [SerializeField] private SymbologyColorManager colorManager;
        
        [Header("Settings")]
        [SerializeField] private bool autoFindComponents = true;
        [SerializeField] private bool verboseLogging = true;
        
        public string TargetId => "symbology";
        public string DisplayName => "Symbology Color";
        
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
            if (colorManager == null)
            {
                colorManager = FindObjectOfType<SymbologyColorManager>();
            }
            
            Log($"Found components - ColorManager: {colorManager != null}");
        }
        
        public VoiceCommandInfo[] GetAvailableCommands()
        {
            if (_commands != null)
                return _commands;
            
            _commands = new VoiceCommandInfo[]
            {
                new VoiceCommandInfo(
                    "toggle_color",
                    "Toggle symbology color between black and white (dark/light mode)"
                ),
                new VoiceCommandInfo(
                    "set_black",
                    "Set symbology color to black (dark mode)"
                ),
                new VoiceCommandInfo(
                    "set_white",
                    "Set symbology color to white (light mode)"
                ),
                new VoiceCommandInfo(
                    "set_green",
                    "Set symbology color to green"
                ),
                new VoiceCommandInfo(
                    "set_cyan",
                    "Set symbology color to cyan"
                ),
                new VoiceCommandInfo(
                    "cycle_color",
                    "Cycle through all available color presets"
                ),
                new VoiceCommandInfo(
                    "set_preset",
                    "Set symbology color to a specific preset",
                    new VoiceCommandParameter("preset", "string", "Color preset name", true, 
                        new string[] { "black", "white", "green", "cyan" })
                ),
                new VoiceCommandInfo(
                    "refresh",
                    "Refresh the symbology color cache and reapply current color"
                ),
                // Opacity/transparency commands
                new VoiceCommandInfo(
                    "set_opacity",
                    "Set the opacity/transparency of all symbology elements (0 = invisible, 1 = fully visible)",
                    new VoiceCommandParameter("opacity", "number", "Opacity value from 0 (invisible) to 1 (fully visible), or as percentage 0-100", true)
                ),
                new VoiceCommandInfo(
                    "show",
                    "Show all symbology elements (set opacity to 100%)"
                ),
                new VoiceCommandInfo(
                    "hide",
                    "Hide all symbology elements (set opacity to 0%)"
                )
            };
            
            return _commands;
        }
        
        public bool ExecuteCommand(string commandName, Dictionary<string, object> parameters)
        {
            Log($"Executing command: {commandName}");
            
            if (colorManager == null)
            {
                Log("ColorManager not found");
                return false;
            }
            
            switch (commandName.ToLower())
            {
                case "toggle_color":
                    colorManager.ToggleColor();
                    Log($"Toggled color, now: {colorManager.CurrentPreset}");
                    return true;
                    
                case "set_black":
                    colorManager.SetColorPreset(ColorPreset.Black);
                    Log("Set color to black");
                    return true;
                    
                case "set_white":
                    colorManager.SetColorPreset(ColorPreset.White);
                    Log("Set color to white");
                    return true;
                    
                case "set_green":
                    colorManager.SetColorPreset(ColorPreset.Green);
                    Log("Set color to green");
                    return true;
                    
                case "set_cyan":
                    colorManager.SetColorPreset(ColorPreset.Cyan);
                    Log("Set color to cyan");
                    return true;
                    
                case "cycle_color":
                    colorManager.CycleColorPreset();
                    Log($"Cycled color, now: {colorManager.CurrentPreset}");
                    return true;
                    
                case "set_preset":
                    return HandleSetPreset(parameters);
                    
                case "refresh":
                    colorManager.RefreshCache();
                    colorManager.ApplyColorImmediate(colorManager.CurrentColor);
                    Log("Refreshed color cache");
                    return true;
                    
                // Opacity commands
                case "set_opacity":
                    return HandleSetOpacity(parameters);
                    
                case "show":
                    colorManager.Show();
                    Log("Showed symbology (opacity 100%)");
                    return true;
                    
                case "hide":
                    colorManager.Hide();
                    Log("Hid symbology (opacity 0%)");
                    return true;
                    
                default:
                    Log($"Unknown command: {commandName}");
                    break;
            }
            
            return false;
        }
        
        private bool HandleSetPreset(Dictionary<string, object> parameters)
        {
            if (parameters == null || !parameters.TryGetValue("preset", out var presetObj))
            {
                Log("Preset parameter not provided");
                return false;
            }
            
            string presetName = presetObj?.ToString()?.ToLower() ?? "";
            ColorPreset preset;
            
            switch (presetName)
            {
                case "black":
                case "dark":
                    preset = ColorPreset.Black;
                    break;
                case "white":
                case "light":
                    preset = ColorPreset.White;
                    break;
                case "green":
                    preset = ColorPreset.Green;
                    break;
                case "cyan":
                case "blue":
                    preset = ColorPreset.Cyan;
                    break;
                default:
                    Log($"Unknown preset: {presetName}");
                    return false;
            }
            
            colorManager.SetColorPreset(preset);
            Log($"Set color preset to {preset}");
            return true;
        }
        
        private bool HandleSetOpacity(Dictionary<string, object> parameters)
        {
            if (parameters == null || !parameters.TryGetValue("opacity", out var opacityObj))
            {
                Log("Opacity parameter not provided");
                return false;
            }
            
            float opacity = ParseOpacityValue(opacityObj);
            colorManager.SetOpacity(opacity);
            Log($"Set symbology opacity to {opacity:F2}");
            return true;
        }
        
        /// <summary>
        /// Parse opacity value - handles both 0-1 range and 0-100 percentage
        /// </summary>
        private float ParseOpacityValue(object value)
        {
            float numValue = ParseNumericValue(value);
            
            // If value is greater than 1, assume it's a percentage
            if (numValue > 1f)
            {
                numValue = numValue / 100f;
            }
            
            return Mathf.Clamp01(numValue);
        }
        
        /// <summary>
        /// Parse a numeric value from various object types
        /// </summary>
        private float ParseNumericValue(object value)
        {
            if (value is float f) return f;
            if (value is double d) return (float)d;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is string s && float.TryParse(s, out float parsed)) return parsed;
            return 0f;
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[SymbologyColorVoiceAdapter] {message}");
            }
        }
    }
}
