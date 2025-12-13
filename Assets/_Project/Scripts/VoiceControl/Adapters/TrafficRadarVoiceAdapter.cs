using System.Collections.Generic;
using UnityEngine;
using VoiceControl.Core;
using TrafficRadar;
using TrafficRadar.Core;
using TrafficRadar.Controls;

namespace VoiceControl.Adapters
{
    /// <summary>
    /// Voice command adapter for Traffic Radar system.
    /// Implements IVoiceCommandTarget to expose traffic radar controls to voice commands.
    /// </summary>
    [AddComponentMenu("Voice Control/Traffic Radar Voice Adapter")]
    public class TrafficRadarVoiceAdapter : MonoBehaviour, IVoiceCommandTarget
    {
        [Header("Target Components")]
        [SerializeField] private TrafficRadarController controller;
        [SerializeField] private TrafficRadarDisplay display;
        [SerializeField] private TrafficRadarRangeUI rangeUI;
        [SerializeField] private TrafficRadarFilterUI filterUI;
        
        [Header("Settings")]
        [SerializeField] private bool autoFindComponents = true;
        [SerializeField] private bool verboseLogging = true;
        
        public string TargetId => "traffic_radar";
        public string DisplayName => "Traffic Radar";
        
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
                controller = FindObjectOfType<TrafficRadarController>();
            }
            if (display == null)
            {
                display = FindObjectOfType<TrafficRadarDisplay>();
            }
            if (rangeUI == null)
            {
                rangeUI = FindObjectOfType<TrafficRadarRangeUI>();
            }
            if (filterUI == null)
            {
                filterUI = FindObjectOfType<TrafficRadarFilterUI>();
            }
            
            Log($"Found components - Controller: {controller != null}, Display: {display != null}, RangeUI: {rangeUI != null}, FilterUI: {filterUI != null}");
        }
        
        public VoiceCommandInfo[] GetAvailableCommands()
        {
            if (_commands != null)
                return _commands;
            
            _commands = new VoiceCommandInfo[]
            {
                new VoiceCommandInfo(
                    "set_range",
                    "Set the traffic radar display range in nautical miles",
                    new VoiceCommandParameter(
                        "range_nm",
                        "number",
                        "Range in nautical miles (typical values: 10, 20, 40, 80, 150)",
                        true
                    )
                ),
                new VoiceCommandInfo(
                    "cycle_range",
                    "Cycle through available range options"
                ),
                new VoiceCommandInfo(
                    "auto_range",
                    "Automatically adjust range to include all nearby aircraft"
                ),
                new VoiceCommandInfo(
                    "refresh_data",
                    "Force an immediate refresh of traffic data from the server"
                ),
                new VoiceCommandInfo(
                    "increase_range",
                    "Increase the traffic radar range to the next higher option"
                ),
                new VoiceCommandInfo(
                    "decrease_range",
                    "Decrease the traffic radar range to the next lower option"
                ),
                new VoiceCommandInfo(
                    "show_range_ui",
                    "Show the traffic radar range selector UI"
                ),
                new VoiceCommandInfo(
                    "hide_range_ui",
                    "Hide the traffic radar range selector UI"
                ),
                new VoiceCommandInfo(
                    "toggle_range_ui",
                    "Toggle the visibility of the range selector UI"
                ),
                new VoiceCommandInfo(
                    "show_filter_ui",
                    "Show the traffic radar filter UI"
                ),
                new VoiceCommandInfo(
                    "hide_filter_ui",
                    "Hide the traffic radar filter UI"
                ),
                new VoiceCommandInfo(
                    "toggle_filter_ui",
                    "Toggle the visibility of the filter UI"
                ),
                new VoiceCommandInfo(
                    "show_panel",
                    "Show all traffic radar control panels (range and filter UI)"
                ),
                new VoiceCommandInfo(
                    "hide_panel",
                    "Hide all traffic radar control panels (range and filter UI)"
                ),
                new VoiceCommandInfo(
                    "toggle_panel",
                    "Toggle visibility of all traffic radar control panels"
                ),
                // Visual settings commands
                new VoiceCommandInfo(
                    "show_background",
                    "Show the radar background circle"
                ),
                new VoiceCommandInfo(
                    "hide_background",
                    "Hide the radar background circle"
                ),
                new VoiceCommandInfo(
                    "toggle_background",
                    "Toggle the radar background circle visibility"
                ),
                new VoiceCommandInfo(
                    "set_background_color",
                    "Set the radar background color",
                    new VoiceCommandParameter(
                        "color",
                        "string",
                        "Color name: red, green, blue, yellow, cyan, magenta, white, black, dark_blue, dark_green",
                        true,
                        new[] { "red", "green", "blue", "yellow", "cyan", "magenta", "white", "black", "dark_blue", "dark_green" }
                    )
                ),
                new VoiceCommandInfo(
                    "set_range_ring_color",
                    "Set the range ring color",
                    new VoiceCommandParameter(
                        "color",
                        "string",
                        "Color name: red, green, blue, yellow, cyan, magenta, white, gray, orange",
                        true,
                        new[] { "red", "green", "blue", "yellow", "cyan", "magenta", "white", "gray", "orange" }
                    )
                ),
                new VoiceCommandInfo(
                    "set_compass_color",
                    "Set the compass markings color",
                    new VoiceCommandParameter(
                        "color",
                        "string",
                        "Color name: red, green, blue, yellow, cyan, magenta, white, gray, orange",
                        true,
                        new[] { "red", "green", "blue", "yellow", "cyan", "magenta", "white", "gray", "orange" }
                    )
                ),
                new VoiceCommandInfo(
                    "set_own_aircraft_color",
                    "Set the own aircraft symbol color",
                    new VoiceCommandParameter(
                        "color",
                        "string",
                        "Color name: red, green, blue, yellow, cyan, magenta, white, orange",
                        true,
                        new[] { "red", "green", "blue", "yellow", "cyan", "magenta", "white", "orange" }
                    )
                ),
                // Opacity/transparency commands
                new VoiceCommandInfo(
                    "set_background_opacity",
                    "Set the radar background opacity/transparency (0 = fully transparent, 100 = fully opaque)",
                    new VoiceCommandParameter(
                        "percent",
                        "number",
                        "Opacity percentage from 0 to 100 (0 = transparent, 100 = opaque)",
                        true
                    )
                ),
                new VoiceCommandInfo(
                    "set_chart_opacity",
                    "Set the chart background opacity/transparency (0 = fully transparent, 100 = fully opaque)",
                    new VoiceCommandParameter(
                        "percent",
                        "number",
                        "Opacity percentage from 0 to 100 (0 = transparent, 100 = opaque)",
                        true
                    )
                ),
                new VoiceCommandInfo(
                    "increase_chart_opacity",
                    "Increase the chart background opacity by 10%"
                ),
                new VoiceCommandInfo(
                    "decrease_chart_opacity",
                    "Decrease the chart background opacity by 10%"
                )
            };
            
            return _commands;
        }
        
        public bool ExecuteCommand(string commandName, Dictionary<string, object> parameters)
        {
            Log($"Executing command: {commandName}");
            
            switch (commandName.ToLower())
            {
                case "set_range":
                    return HandleSetRange(parameters);
                    
                case "cycle_range":
                    if (controller != null)
                    {
                        controller.CycleRange();
                        return true;
                    }
                    break;
                    
                case "auto_range":
                    if (controller != null)
                    {
                        controller.AutoAdjustRange();
                        return true;
                    }
                    break;
                    
                case "refresh_data":
                    if (controller != null)
                    {
                        controller.RefreshData();
                        return true;
                    }
                    break;
                    
                case "increase_range":
                    if (controller != null)
                    {
                        controller.IncreaseRange();
                        return true;
                    }
                    break;
                    
                case "decrease_range":
                    if (controller != null)
                    {
                        controller.DecreaseRange();
                        return true;
                    }
                    break;
                    
                case "show_range_ui":
                    if (rangeUI != null)
                    {
                        rangeUI.SetVisibility(true);
                        return true;
                    }
                    break;
                    
                case "hide_range_ui":
                    if (rangeUI != null)
                    {
                        rangeUI.SetVisibility(false);
                        return true;
                    }
                    break;
                    
                case "toggle_range_ui":
                    if (rangeUI != null)
                    {
                        rangeUI.ToggleVisibility();
                        return true;
                    }
                    break;
                    
                case "show_filter_ui":
                    if (filterUI != null)
                    {
                        filterUI.SetVisibility(true);
                        return true;
                    }
                    break;
                    
                case "hide_filter_ui":
                    if (filterUI != null)
                    {
                        filterUI.SetVisibility(false);
                        return true;
                    }
                    break;
                    
                case "toggle_filter_ui":
                    if (filterUI != null)
                    {
                        filterUI.ToggleVisibility();
                        return true;
                    }
                    break;
                    
                case "show_panel":
                    {
                        bool success = false;
                        if (rangeUI != null)
                        {
                            rangeUI.SetVisibility(true);
                            success = true;
                        }
                        if (filterUI != null)
                        {
                            filterUI.SetVisibility(true);
                            success = true;
                        }
                        return success;
                    }
                    
                case "hide_panel":
                    {
                        bool success = false;
                        if (rangeUI != null)
                        {
                            rangeUI.SetVisibility(false);
                            success = true;
                        }
                        if (filterUI != null)
                        {
                            filterUI.SetVisibility(false);
                            success = true;
                        }
                        return success;
                    }
                    
                case "toggle_panel":
                    {
                        bool success = false;
                        if (rangeUI != null)
                        {
                            rangeUI.ToggleVisibility();
                            success = true;
                        }
                        if (filterUI != null)
                        {
                            filterUI.ToggleVisibility();
                            success = true;
                        }
                        return success;
                    }
                    
                // Visual settings commands
                case "show_background":
                    if (display != null)
                    {
                        display.ShowRadarBackground = true;
                        Log("Showing radar background");
                        return true;
                    }
                    break;
                    
                case "hide_background":
                    if (display != null)
                    {
                        display.ShowRadarBackground = false;
                        Log("Hiding radar background");
                        return true;
                    }
                    break;
                    
                case "toggle_background":
                    if (display != null)
                    {
                        display.ShowRadarBackground = !display.ShowRadarBackground;
                        Log($"Toggled radar background: {display.ShowRadarBackground}");
                        return true;
                    }
                    break;
                    
                case "set_background_color":
                    return HandleSetColor(parameters, "background");
                    
                case "set_range_ring_color":
                    return HandleSetColor(parameters, "range_ring");
                    
                case "set_compass_color":
                    return HandleSetColor(parameters, "compass");
                    
                case "set_own_aircraft_color":
                    return HandleSetColor(parameters, "own_aircraft");
                    
                case "set_background_opacity":
                    return HandleSetOpacity(parameters, "background");
                    
                case "set_chart_opacity":
                    return HandleSetOpacity(parameters, "chart");
                    
                case "increase_chart_opacity":
                    if (display != null)
                    {
                        display.IncreaseChartOpacity(0.1f);
                        Log($"Increased chart opacity to {display.ChartOpacity:P0}");
                        return true;
                    }
                    break;
                    
                case "decrease_chart_opacity":
                    if (display != null)
                    {
                        display.DecreaseChartOpacity(0.1f);
                        Log($"Decreased chart opacity to {display.ChartOpacity:P0}");
                        return true;
                    }
                    break;
                    
                default:
                    Log($"Unknown command: {commandName}");
                    break;
            }
            
            return false;
        }
        
        private bool HandleSetRange(Dictionary<string, object> parameters)
        {
            if (controller == null)
            {
                Log("Controller not found");
                return false;
            }
            
            if (!parameters.TryGetValue("range_nm", out var rangeObj))
            {
                Log("Range parameter not provided");
                return false;
            }
            
            float range;
            if (rangeObj is double d)
                range = (float)d;
            else if (rangeObj is long l)
                range = l;
            else if (rangeObj is int i)
                range = i;
            else if (float.TryParse(rangeObj.ToString(), out float parsed))
                range = parsed;
            else
            {
                Log($"Invalid range value: {rangeObj}");
                return false;
            }
            
            controller.RangeNM = range;
            Log($"Set range to {range} NM");
            return true;
        }
        
        private bool HandleSetColor(Dictionary<string, object> parameters, string target)
        {
            if (display == null)
            {
                Log("Display not found");
                return false;
            }
            
            if (!parameters.TryGetValue("color", out var colorObj))
            {
                Log("Color parameter not provided");
                return false;
            }
            
            string colorName = colorObj.ToString().ToLower().Replace(" ", "_");
            Color color = ParseColorName(colorName);
            
            switch (target)
            {
                case "background":
                    display.BackgroundColor = color;
                    Log($"Set background color to {colorName}");
                    break;
                case "range_ring":
                    display.RangeRingColor = color;
                    Log($"Set range ring color to {colorName}");
                    break;
                case "compass":
                    display.CompassMarkingsColor = color;
                    Log($"Set compass markings color to {colorName}");
                    break;
                case "own_aircraft":
                    display.OwnAircraftColor = color;
                    Log($"Set own aircraft color to {colorName}");
                    break;
                default:
                    Log($"Unknown color target: {target}");
                    return false;
            }
            
            return true;
        }
        
        private Color ParseColorName(string colorName)
        {
            switch (colorName)
            {
                case "red": return Color.red;
                case "green": return Color.green;
                case "blue": return Color.blue;
                case "yellow": return Color.yellow;
                case "cyan": return Color.cyan;
                case "magenta": return Color.magenta;
                case "white": return Color.white;
                case "black": return Color.black;
                case "gray":
                case "grey": return Color.gray;
                case "orange": return new Color(1f, 0.5f, 0f, 1f);
                case "dark_blue": return new Color(0.05f, 0.1f, 0.2f, 0.9f);
                case "dark_green": return new Color(0.05f, 0.2f, 0.1f, 0.9f);
                default:
                    Log($"Unknown color name: {colorName}, defaulting to white");
                    return Color.white;
            }
        }
        
        private bool HandleSetOpacity(Dictionary<string, object> parameters, string target)
        {
            if (display == null)
            {
                Log("Display not found");
                return false;
            }
            
            if (!parameters.TryGetValue("percent", out var percentObj))
            {
                Log("Percent parameter not provided");
                return false;
            }
            
            float percent;
            if (percentObj is double d)
                percent = (float)d;
            else if (percentObj is long l)
                percent = l;
            else if (percentObj is int i)
                percent = i;
            else if (float.TryParse(percentObj.ToString(), out float parsed))
                percent = parsed;
            else
            {
                Log($"Invalid percent value: {percentObj}");
                return false;
            }
            
            // Convert percentage (0-100) to normalized (0-1)
            float opacity = Mathf.Clamp01(percent / 100f);
            
            switch (target)
            {
                case "background":
                    // For background color, we modify alpha channel
                    Color bgColor = display.BackgroundColor;
                    bgColor.a = opacity;
                    display.BackgroundColor = bgColor;
                    Log($"Set background opacity to {percent:F0}%");
                    break;
                case "chart":
                    display.ChartOpacity = opacity;
                    Log($"Set chart opacity to {percent:F0}%");
                    break;
                default:
                    Log($"Unknown opacity target: {target}");
                    return false;
            }
            
            return true;
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[TrafficRadarVoiceAdapter] {message}");
            }
        }
    }
}

