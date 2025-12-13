using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VoiceControl.Core
{
    /// <summary>
    /// Singleton registry for all voice command targets.
    /// Provides discovery, registration, and routing of voice commands.
    /// </summary>
    public class VoiceCommandRegistry : MonoBehaviour
    {
        private static VoiceCommandRegistry _instance;
        public static VoiceCommandRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<VoiceCommandRegistry>();
                }
                return _instance;
            }
        }
        
        [Header("Settings")]
        [SerializeField] private bool autoDiscoverOnStart = true;
        [SerializeField] private bool verboseLogging = true;
        
        private Dictionary<string, IVoiceCommandTarget> _targets = new Dictionary<string, IVoiceCommandTarget>();
        private List<Dictionary<string, object>> _cachedTools = null;
        
        /// <summary>
        /// Event fired when registry is updated
        /// </summary>
        public event Action OnRegistryUpdated;
        
        /// <summary>
        /// All registered targets
        /// </summary>
        public IReadOnlyDictionary<string, IVoiceCommandTarget> Targets => _targets;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }
        
        private void Start()
        {
            if (autoDiscoverOnStart)
            {
                DiscoverTargets();
            }
        }
        
        /// <summary>
        /// Discover all IVoiceCommandTarget implementations in the scene
        /// </summary>
        public void DiscoverTargets()
        {
            _targets.Clear();
            _cachedTools = null;
            
            var targets = FindObjectsOfType<MonoBehaviour>()
                .OfType<IVoiceCommandTarget>();
            
            foreach (var target in targets)
            {
                RegisterTarget(target);
            }
            
            Log($"Discovered {_targets.Count} voice command targets");
            OnRegistryUpdated?.Invoke();
        }
        
        /// <summary>
        /// Register a voice command target
        /// </summary>
        public void RegisterTarget(IVoiceCommandTarget target)
        {
            if (target == null) return;
            
            if (_targets.ContainsKey(target.TargetId))
            {
                Log($"Replacing existing target: {target.TargetId}");
            }
            
            _targets[target.TargetId] = target;
            _cachedTools = null;
            
            Log($"Registered target: {target.TargetId} ({target.DisplayName}) with {target.GetAvailableCommands().Length} commands");
        }
        
        /// <summary>
        /// Unregister a voice command target
        /// </summary>
        public void UnregisterTarget(string targetId)
        {
            if (_targets.Remove(targetId))
            {
                _cachedTools = null;
                Log($"Unregistered target: {targetId}");
                OnRegistryUpdated?.Invoke();
            }
        }
        
        /// <summary>
        /// Check if a target with the given ID exists
        /// </summary>
        public bool HasTarget(string targetId)
        {
            return _targets.ContainsKey(targetId);
        }
        
        /// <summary>
        /// Get all available commands across all targets
        /// </summary>
        public List<VoiceCommandInfo> GetAllCommands()
        {
            var allCommands = new List<VoiceCommandInfo>();
            
            foreach (var kvp in _targets)
            {
                var target = kvp.Value;
                foreach (var cmd in target.GetAvailableCommands())
                {
                    // Create a copy with the target name attached
                    var cmdWithTarget = new VoiceCommandInfo(cmd.Name, cmd.Description, cmd.Parameters);
                    cmdWithTarget.TargetName = target.TargetId;
                    allCommands.Add(cmdWithTarget);
                }
            }
            
            return allCommands;
        }
        
        /// <summary>
        /// Get all available tools in OpenAI function calling format
        /// </summary>
        public List<Dictionary<string, object>> GetToolsSchema()
        {
            if (_cachedTools != null)
                return _cachedTools;
            
            _cachedTools = new List<Dictionary<string, object>>();
            
            foreach (var kvp in _targets)
            {
                var target = kvp.Value;
                foreach (var command in target.GetAvailableCommands())
                {
                    _cachedTools.Add(command.ToOpenAITool(target.TargetId));
                }
            }
            
            return _cachedTools;
        }
        
        /// <summary>
        /// Get system prompt describing all available commands - JSON output format
        /// </summary>
        public string GetSystemPrompt()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("You are an aviation voice command assistant. Parse user commands and output JSON to execute.");
            sb.AppendLine();
            sb.AppendLine("## OUTPUT FORMAT");
            sb.AppendLine("For a SINGLE command, respond with:");
            sb.AppendLine("{\"command\": \"target_commandname\", \"args\": {}}");
            sb.AppendLine();
            sb.AppendLine("For MULTIPLE commands (e.g. 'all panels'), respond with a JSON array:");
            sb.AppendLine("{\"commands\": [{\"command\": \"weather_radar_hide_panel\", \"args\": {}}, {\"command\": \"traffic_radar_hide_panel\", \"args\": {}}]}");
            sb.AppendLine();
            sb.AppendLine("## EXAMPLES");
            sb.AppendLine("Single commands:");
            sb.AppendLine("- \"increase the radar range\" -> {\"command\": \"weather_radar_increase_range\", \"args\": {}}");
            sb.AppendLine("- \"set tilt to 5 degrees\" -> {\"command\": \"weather_radar_set_tilt\", \"args\": {\"degrees\": 5}}");
            sb.AppendLine("- \"set gain to 0\" -> {\"command\": \"weather_radar_set_gain\", \"args\": {\"dB\": 0}}");
            sb.AppendLine("- \"reduce the gain to minus 3 dB\" -> {\"command\": \"weather_radar_set_gain\", \"args\": {\"dB\": -3}}");
            sb.AppendLine("- \"hide the weather radar panel\" -> {\"command\": \"weather_radar_hide_panel\", \"args\": {}}");
            sb.AppendLine("- \"hide the traffic radar panel\" -> {\"command\": \"traffic_radar_hide_panel\", \"args\": {}}");
            sb.AppendLine("- \"set traffic range to 40\" -> {\"command\": \"traffic_radar_set_range\", \"args\": {\"range_nm\": 40}}");
            sb.AppendLine("- \"increase traffic range\" -> {\"command\": \"traffic_radar_increase_range\", \"args\": {}}");
            sb.AppendLine("- \"hide traffic indicators\" -> {\"command\": \"indicator_system_hide_traffic\", \"args\": {}}");
            sb.AppendLine("- \"clear all indicators\" -> {\"command\": \"indicator_system_clear_all\", \"args\": {}}");
            sb.AppendLine();
            sb.AppendLine("Indicator opacity/transparency commands:");
            sb.AppendLine("- \"hide all indicators\" -> {\"command\": \"indicator_system_hide_all_indicators\", \"args\": {}}");
            sb.AppendLine("- \"show all indicators\" -> {\"command\": \"indicator_system_show_all_indicators\", \"args\": {}}");
            sb.AppendLine("- \"set indicator opacity to 50 percent\" -> {\"command\": \"indicator_system_set_opacity\", \"args\": {\"opacity\": 0.5}}");
            sb.AppendLine("- \"make indicators half transparent\" -> {\"command\": \"indicator_system_set_opacity\", \"args\": {\"opacity\": 0.5}}");
            sb.AppendLine("- \"set indicator transparency to 70 percent\" -> {\"command\": \"indicator_system_set_opacity\", \"args\": {\"opacity\": 0.3}}");
            sb.AppendLine("- \"dim the indicators\" -> {\"command\": \"indicator_system_set_opacity\", \"args\": {\"opacity\": 0.5}}");
            sb.AppendLine("- \"fade indicators to 30 percent\" -> {\"command\": \"indicator_system_set_opacity\", \"args\": {\"opacity\": 0.3}}");
            sb.AppendLine("- \"set nearby indicator opacity to 30 percent\" -> {\"command\": \"indicator_system_set_nearby_opacity\", \"args\": {\"opacity\": 0.3}}");
            sb.AppendLine("- \"dim nearby indicators within 5 miles\" -> {\"command\": \"indicator_system_set_nearby_opacity\", \"args\": {\"opacity\": 0.5, \"distance\": 5}}");
            sb.AppendLine("- \"hide close indicators\" -> {\"command\": \"indicator_system_set_nearby_opacity\", \"args\": {\"opacity\": 0}}");
            sb.AppendLine("- \"fade indicators close to me\" -> {\"command\": \"indicator_system_set_nearby_opacity\", \"args\": {\"opacity\": 0.3}}");
            sb.AppendLine("- \"toggle proximity mode\" -> {\"command\": \"indicator_system_toggle_proximity_mode\", \"args\": {}}");
            sb.AppendLine();
            sb.AppendLine("Vision briefing commands:");
            sb.AppendLine("- \"analyze the sectional chart\" -> {\"command\": \"visionbriefing_analyze_sectional\", \"args\": {}}");
            sb.AppendLine("- \"sectional briefing\" -> {\"command\": \"visionbriefing_sectional_briefing\", \"args\": {}}");
            sb.AppendLine("- \"analyze weather radar\" -> {\"command\": \"visionbriefing_analyze_weather\", \"args\": {}}");
            sb.AppendLine("- \"weather briefing\" -> {\"command\": \"visionbriefing_weather_briefing\", \"args\": {}}");
            sb.AppendLine("- \"what's on the chart\" -> {\"command\": \"visionbriefing_analyze_sectional\", \"args\": {}}");
            sb.AppendLine("- \"what's the weather\" -> {\"command\": \"visionbriefing_analyze_weather\", \"args\": {}}");
            sb.AppendLine();
            sb.AppendLine("Multi-panel commands (use array format):");
            sb.AppendLine("- \"hide all panels\" -> {\"commands\": [{\"command\": \"weather_radar_hide_panel\", \"args\": {}}, {\"command\": \"traffic_radar_hide_panel\", \"args\": {}}]}");
            sb.AppendLine("- \"show all panels\" -> {\"commands\": [{\"command\": \"weather_radar_show_panel\", \"args\": {}}, {\"command\": \"traffic_radar_show_panel\", \"args\": {}}]}");
            sb.AppendLine("- \"hide all the panels\" -> {\"commands\": [{\"command\": \"weather_radar_hide_panel\", \"args\": {}}, {\"command\": \"traffic_radar_hide_panel\", \"args\": {}}]}");
            sb.AppendLine();
            sb.AppendLine("If command is unclear, respond: {\"command\": \"error\", \"message\": \"Please clarify...\"}");
            sb.AppendLine();
            sb.AppendLine("## AVAILABLE COMMANDS");
            sb.AppendLine();
            
            
            foreach (var kvp in _targets)
            {
                var target = kvp.Value;
                sb.AppendLine($"### {target.DisplayName} ({target.TargetId})");
                
                foreach (var cmd in target.GetAvailableCommands())
                {
                    string fullCmd = $"{target.TargetId}_{cmd.Name}";
                    sb.Append($"- {fullCmd}: {cmd.Description}");
                    if (cmd.Parameters.Length > 0)
                    {
                        var paramStrs = cmd.Parameters.Select(p => 
                            p.EnumValues != null 
                                ? $"{p.Name}=[{string.Join("|", p.EnumValues)}]" 
                                : $"{p.Name}:{p.Type}");
                        sb.Append($" (args: {string.Join(", ", paramStrs)})");
                    }
                    sb.AppendLine();
                }
                sb.AppendLine();
            }
            
            sb.AppendLine("IMPORTANT: Output ONLY valid JSON. No explanations, no markdown code blocks, just the raw JSON object.");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Execute a command by full name (targetId_commandName)
        /// </summary>
        public bool ExecuteCommand(string fullCommandName, Dictionary<string, object> parameters)
        {
            // Parse targetId_commandName format
            int underscoreIndex = fullCommandName.IndexOf('_');
            if (underscoreIndex <= 0)
            {
                Log($"Invalid command format: {fullCommandName}");
                return false;
            }
            
            string targetId = fullCommandName.Substring(0, underscoreIndex);
            string commandName = fullCommandName.Substring(underscoreIndex + 1);
            
            return ExecuteCommand(targetId, commandName, parameters);
        }
        
        /// <summary>
        /// Execute a command on a specific target
        /// </summary>
        public bool ExecuteCommand(string targetId, string commandName, Dictionary<string, object> parameters)
        {
            if (!_targets.TryGetValue(targetId, out var target))
            {
                Log($"Target not found: {targetId}");
                return false;
            }
            
            Log($"Executing: {targetId}.{commandName}({FormatParameters(parameters)})");
            
            try
            {
                bool success = target.ExecuteCommand(commandName, parameters ?? new Dictionary<string, object>());
                Log(success ? "Command executed successfully" : "Command execution failed");
                return success;
            }
            catch (Exception e)
            {
                Debug.LogError($"[VoiceCommandRegistry] Error executing command: {e.Message}");
                return false;
            }
        }
        
        private string FormatParameters(Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return "";
            return string.Join(", ", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[VoiceCommandRegistry] {message}");
            }
        }
    }
}
