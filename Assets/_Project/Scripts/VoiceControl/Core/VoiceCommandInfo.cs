using System;
using System.Collections.Generic;

namespace VoiceControl.Core
{
    /// <summary>
    /// Describes a voice command that can be executed.
    /// Used for LLM function calling schema generation.
    /// </summary>
    [Serializable]
    public class VoiceCommandInfo
    {
        /// <summary>
        /// Command name (e.g., "increase_range", "set_mode")
        /// </summary>
        public string Name;
        
        /// <summary>
        /// Description for LLM context
        /// </summary>
        public string Description;
        
        /// <summary>
        /// Parameter definitions
        /// </summary>
        public VoiceCommandParameter[] Parameters;
        
        /// <summary>
        /// Target ID this command belongs to (set by registry)
        /// </summary>
        public string TargetName;
        
        /// <summary>
        /// Alias for Name (for consistency in command lookup)
        /// </summary>
        public string CommandName => Name;
        
        public VoiceCommandInfo(string name, string description, params VoiceCommandParameter[] parameters)
        {
            Name = name;
            Description = description;
            Parameters = parameters ?? Array.Empty<VoiceCommandParameter>();
        }
        
        /// <summary>
        /// Convert to OpenAI function calling format
        /// </summary>
        public Dictionary<string, object> ToOpenAITool(string targetId)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();
            
            foreach (var param in Parameters)
            {
                var propDef = new Dictionary<string, object>
                {
                    ["type"] = param.Type,
                    ["description"] = param.Description
                };
                
                if (param.EnumValues != null && param.EnumValues.Length > 0)
                {
                    propDef["enum"] = param.EnumValues;
                }
                
                properties[param.Name] = propDef;
                
                if (param.Required)
                {
                    required.Add(param.Name);
                }
            }
            
            return new Dictionary<string, object>
            {
                ["type"] = "function",
                ["function"] = new Dictionary<string, object>
                {
                    ["name"] = $"{targetId}_{Name}",
                    ["description"] = Description,
                    ["parameters"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = properties,
                        ["required"] = required
                    }
                }
            };
        }
    }
    
    /// <summary>
    /// Describes a parameter for a voice command.
    /// </summary>
    [Serializable]
    public class VoiceCommandParameter
    {
        public string Name;
        public string Type; // "string", "number", "boolean", "integer"
        public string Description;
        public bool Required;
        public string[] EnumValues; // Optional: allowed values
        
        public VoiceCommandParameter(string name, string type, string description, bool required = false, string[] enumValues = null)
        {
            Name = name;
            Type = type;
            Description = description;
            Required = required;
            EnumValues = enumValues;
        }
    }
}
