using System.Collections.Generic;
using UnityEngine;
using VoiceControl.Core;
using IndicatorSystem.Controller;

namespace VoiceControl.Adapters
{
    /// <summary>
    /// Voice command adapter for Indicator System.
    /// Implements IVoiceCommandTarget to expose indicator controls to voice commands.
    /// </summary>
    [AddComponentMenu("Voice Control/Indicator System Voice Adapter")]
    public class IndicatorSystemVoiceAdapter : MonoBehaviour, IVoiceCommandTarget
    {
        [Header("Target Components")]
        [SerializeField] private IndicatorSystemController controller;
        
        [Header("Settings")]
        [SerializeField] private bool autoFindComponents = true;
        [SerializeField] private bool verboseLogging = true;
        
        public string TargetId => "indicator_system";
        public string DisplayName => "Indicator System";
        
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
            if (controller == null)
            {
                controller = FindObjectOfType<IndicatorSystemController>();
            }
            
            Log($"Found components - Controller: {controller != null}");
        }
        
        public VoiceCommandInfo[] GetAvailableCommands()
        {
            if (_commands != null)
                return _commands;
            
            _commands = new VoiceCommandInfo[]
            {
                new VoiceCommandInfo(
                    "clear_all",
                    "Clear all indicators from the display"
                ),
                new VoiceCommandInfo(
                    "refresh_settings",
                    "Reload indicator settings and refresh the display"
                ),
                new VoiceCommandInfo(
                    "reinitialize",
                    "Reinitialize the entire indicator system"
                ),
                new VoiceCommandInfo(
                    "show_traffic",
                    "Show traffic indicators on the display"
                ),
                new VoiceCommandInfo(
                    "hide_traffic",
                    "Hide traffic indicators from the display"
                ),
                new VoiceCommandInfo(
                    "toggle_traffic",
                    "Toggle traffic indicator visibility"
                ),
                new VoiceCommandInfo(
                    "show_weather",
                    "Show weather indicators on the display"
                ),
                new VoiceCommandInfo(
                    "hide_weather",
                    "Hide weather indicators from the display"
                ),
                new VoiceCommandInfo(
                    "toggle_weather",
                    "Toggle weather indicator visibility"
                ),
                // Opacity control commands
                new VoiceCommandInfo(
                    "show_all_indicators",
                    "Show all indicators at full opacity"
                ),
                new VoiceCommandInfo(
                    "hide_all_indicators",
                    "Hide all indicators by setting opacity to zero"
                ),
                new VoiceCommandInfo(
                    "set_opacity",
                    "Set the global opacity/transparency of all indicators (0-1 or 0-100%)",
                    new VoiceCommandParameter("opacity", "number", "Opacity value from 0 (invisible) to 1 (fully visible), or as percentage 0-100", true)
                ),
                new VoiceCommandInfo(
                    "set_nearby_opacity",
                    "Set the opacity for indicators close to own aircraft",
                    new VoiceCommandParameter("opacity", "number", "Opacity value from 0 (invisible) to 1 (fully visible), or as percentage 0-100", true),
                    new VoiceCommandParameter("distance", "number", "Distance threshold in nautical miles (optional)", false)
                ),
                new VoiceCommandInfo(
                    "toggle_proximity_mode",
                    "Toggle proximity-based opacity mode, which dims nearby indicators differently"
                )
            };
            
            return _commands;
        }
        
        public bool ExecuteCommand(string commandName, Dictionary<string, object> parameters)
        {
            Log($"Executing command: {commandName}");
            
            switch (commandName.ToLower())
            {
                case "clear_all":
                    if (controller != null)
                    {
                        controller.ClearAll();
                        return true;
                    }
                    break;
                    
                case "refresh_settings":
                    if (controller != null)
                    {
                        controller.RefreshSettings();
                        return true;
                    }
                    break;
                    
                case "reinitialize":
                    if (controller != null)
                    {
                        // Use reflection to call debug method if available
                        var method = controller.GetType().GetMethod("DebugReinitialize", 
                            System.Reflection.BindingFlags.Instance | 
                            System.Reflection.BindingFlags.Public | 
                            System.Reflection.BindingFlags.NonPublic);
                        
                        if (method != null)
                        {
                            method.Invoke(controller, null);
                            return true;
                        }
                        else
                        {
                            // Fallback: just refresh settings
                            controller.RefreshSettings();
                            return true;
                        }
                    }
                    break;
                    
                case "show_traffic":
                    return SetIndicatorVisibility("traffic", true);
                    
                case "hide_traffic":
                    return SetIndicatorVisibility("traffic", false);
                    
                case "toggle_traffic":
                    return ToggleIndicatorVisibility("traffic");
                    
                case "show_weather":
                    return SetIndicatorVisibility("weather", true);
                    
                case "hide_weather":
                    return SetIndicatorVisibility("weather", false);
                    
                case "toggle_weather":
                    return ToggleIndicatorVisibility("weather");
                    
                // Opacity control commands
                case "show_all_indicators":
                    if (controller != null)
                    {
                        controller.ShowAllIndicators();
                        return true;
                    }
                    break;
                    
                case "hide_all_indicators":
                    if (controller != null)
                    {
                        controller.HideAllIndicators();
                        return true;
                    }
                    break;
                    
                case "set_opacity":
                    return HandleSetOpacity(parameters);
                    
                case "set_nearby_opacity":
                    return HandleSetNearbyOpacity(parameters);
                    
                case "toggle_proximity_mode":
                    if (controller != null)
                    {
                        controller.ToggleProximityOpacity();
                        return true;
                    }
                    break;
                    
                default:
                    Log($"Unknown command: {commandName}");
                    break;
            }
            
            return false;
        }
        
        private bool SetIndicatorVisibility(string type, bool visible)
        {
            if (controller == null || controller.Settings == null)
            {
                Log("Controller or settings not found");
                return false;
            }
            
            var settings = controller.Settings;
            switch (type.ToLower())
            {
                case "traffic":
                    settings.showTrafficIndicators = visible;
                    Log($"Traffic indicators: {(visible ? "shown" : "hidden")}");
                    return true;
                case "weather":
                    settings.showWeatherIndicators = visible;
                    Log($"Weather indicators: {(visible ? "shown" : "hidden")}");
                    return true;
                default:
                    Log($"Unknown indicator type: {type}");
                    return false;
            }
        }
        
        private bool ToggleIndicatorVisibility(string type)
        {
            if (controller == null || controller.Settings == null)
            {
                Log("Controller or settings not found");
                return false;
            }
            
            var settings = controller.Settings;
            switch (type.ToLower())
            {
                case "traffic":
                    return SetIndicatorVisibility("traffic", !settings.showTrafficIndicators);
                case "weather":
                    return SetIndicatorVisibility("weather", !settings.showWeatherIndicators);
                default:
                    Log($"Unknown indicator type: {type}");
                    return false;
            }
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[IndicatorSystemVoiceAdapter] {message}");
            }
        }
        
        private bool HandleSetOpacity(Dictionary<string, object> parameters)
        {
            if (controller == null)
            {
                Log("Controller not found");
                return false;
            }
            
            if (parameters == null || !parameters.TryGetValue("opacity", out var opacityObj))
            {
                Log("Opacity parameter not provided");
                return false;
            }
            
            float opacity = ParseOpacityValue(opacityObj);
            controller.SetGlobalOpacity(opacity);
            Log($"Set global opacity to {opacity:F2}");
            return true;
        }
        
        private bool HandleSetNearbyOpacity(Dictionary<string, object> parameters)
        {
            if (controller == null)
            {
                Log("Controller not found");
                return false;
            }
            
            if (parameters == null || !parameters.TryGetValue("opacity", out var opacityObj))
            {
                Log("Opacity parameter not provided");
                return false;
            }
            
            float opacity = ParseOpacityValue(opacityObj);
            float distance = -1f;
            
            if (parameters.TryGetValue("distance", out var distanceObj))
            {
                distance = ParseNumericValue(distanceObj);
            }
            
            controller.SetNearbyOpacity(opacity, distance);
            Log($"Set nearby opacity to {opacity:F2}" + (distance > 0 ? $" within {distance:F1} NM" : ""));
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
    }
}
