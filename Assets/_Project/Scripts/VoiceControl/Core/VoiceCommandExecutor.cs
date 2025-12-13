using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoiceControl.Core
{
    /// <summary>
    /// Executes parsed LLM responses containing tool calls.
    /// Validates parameters and routes commands through the registry.
    /// </summary>
    public class VoiceCommandExecutor : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private VoiceCommandRegistry registry;
        
        [Header("Settings")]
        [SerializeField] private bool verboseLogging = true;
        
        /// <summary>
        /// Event fired when a command is executed
        /// </summary>
        public event Action<string, bool> OnCommandExecuted;
        
        /// <summary>
        /// Result of command execution
        /// </summary>
        public class ExecutionResult
        {
            public bool Success;
            public string CommandName;
            public string TargetId;
            public string Message;
            public Dictionary<string, object> Parameters;
        }
        
        private void Awake()
        {
            if (registry == null)
            {
                registry = VoiceCommandRegistry.Instance;
            }
        }
        
        /// <summary>
        /// Execute a tool call from LLM response
        /// </summary>
        /// <param name="toolCall">Parsed tool call object</param>
        /// <returns>Execution result</returns>
        public ExecutionResult ExecuteToolCall(LLMToolCall toolCall)
        {
            if (toolCall == null)
            {
                return new ExecutionResult
                {
                    Success = false,
                    Message = "Tool call is null"
                };
            }
            
            Log($"Executing tool call: {toolCall.FunctionName}");
            
            // Handle multi-command array format
            if (toolCall.FunctionName == "multi_command" && toolCall.Arguments.TryGetValue("commands", out var commandsObj))
            {
                return ExecuteMultiCommand(commandsObj as List<object>);
            }
            
            string targetId;
            string commandName;
            
            // Parse the command name - could be either:
            // - Full format: targetId_commandName (e.g., indicator_system_clear_all)
            // - Short format: just commandName (e.g., clear_all)
            int underscoreIndex = toolCall.FunctionName.IndexOf('_');
            
            if (underscoreIndex <= 0)
            {
                // No underscore - treat entire name as command and search for it
                targetId = null;
                commandName = toolCall.FunctionName;
            }
            else
            {
                // Has underscore - try to find matching target_command combo
                // First try: assume format is targetId_commandName
                string potentialTarget = toolCall.FunctionName.Substring(0, underscoreIndex);
                string potentialCommand = toolCall.FunctionName.Substring(underscoreIndex + 1);
                
                // Check if this exact target exists
                if (registry.HasTarget(potentialTarget))
                {
                    targetId = potentialTarget;
                    commandName = potentialCommand;
                }
                else
                {
                    // Target not found - might be multi-word target (e.g., indicator_system_clear_all)
                    // Search for the best matching command
                    var matchResult = FindMatchingCommand(toolCall.FunctionName);
                    if (matchResult.HasValue)
                    {
                        targetId = matchResult.Value.targetId;
                        commandName = matchResult.Value.commandName;
                    }
                    else
                    {
                        // Still no match - treat entire name as command
                        targetId = null;
                        commandName = toolCall.FunctionName;
                    }
                }
            }
            
            // If no target found, search for command across all targets
            if (string.IsNullOrEmpty(targetId))
            {
                var matchResult = FindCommandByName(commandName);
                if (matchResult.HasValue)
                {
                    targetId = matchResult.Value.targetId;
                    commandName = matchResult.Value.commandName;
                    Log($"Found command '{commandName}' under target '{targetId}'");
                }
                else
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        CommandName = toolCall.FunctionName,
                        Message = $"Unknown command: {toolCall.FunctionName}"
                    };
                }
            }
            
            // Execute via registry
            bool success = registry.ExecuteCommand(targetId, commandName, toolCall.Arguments);
            
            var result = new ExecutionResult
            {
                Success = success,
                CommandName = commandName,
                TargetId = targetId,
                Parameters = toolCall.Arguments,
                Message = success 
                    ? $"Executed {commandName} on {targetId}" 
                    : $"Failed to execute {commandName} on {targetId}"
            };
            
            OnCommandExecuted?.Invoke(toolCall.FunctionName, success);
            
            return result;
        }
        
        /// <summary>
        /// Find a command that matches the full function name (e.g., indicator_system_clear_all)
        /// </summary>
        private (string targetId, string commandName)? FindMatchingCommand(string functionName)
        {
            var commands = registry.GetAllCommands();
            
            foreach (var cmd in commands)
            {
                // Build expected full name: targetName_commandName
                string fullName = $"{cmd.TargetName}_{cmd.CommandName}";
                if (functionName.Equals(fullName, StringComparison.OrdinalIgnoreCase))
                {
                    return (cmd.TargetName, cmd.CommandName);
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Find the target that has a command with the given name
        /// </summary>
        private (string targetId, string commandName)? FindCommandByName(string commandName)
        {
            var commands = registry.GetAllCommands();
            
            foreach (var cmd in commands)
            {
                if (cmd.CommandName.Equals(commandName, StringComparison.OrdinalIgnoreCase))
                {
                    return (cmd.TargetName, cmd.CommandName);
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Execute multiple tool calls from LLM response
        /// </summary>
        public List<ExecutionResult> ExecuteToolCalls(List<LLMToolCall> toolCalls)
        {
            var results = new List<ExecutionResult>();
            
            if (toolCalls == null || toolCalls.Count == 0)
            {
                Log("No tool calls to execute");
                return results;
            }
            
            foreach (var toolCall in toolCalls)
            {
                results.Add(ExecuteToolCall(toolCall));
            }
            
            return results;
        }
        
        /// <summary>
        /// Execute multiple commands from array format (e.g. "hide all panels")
        /// </summary>
        private ExecutionResult ExecuteMultiCommand(List<object> commands)
        {
            if (commands == null || commands.Count == 0)
            {
                return new ExecutionResult
                {
                    Success = false,
                    Message = "No commands in array"
                };
            }
            
            Log($"Executing {commands.Count} commands from multi-command array");
            
            int successCount = 0;
            var messages = new List<string>();
            
            foreach (var cmdObj in commands)
            {
                if (!(cmdObj is Dictionary<string, object> cmdDict))
                    continue;
                
                // Extract command name
                string commandName = null;
                if (cmdDict.TryGetValue("command", out var cmdVal))
                    commandName = cmdVal?.ToString();
                else if (cmdDict.TryGetValue("name", out var nameVal))
                    commandName = nameVal?.ToString();
                
                if (string.IsNullOrEmpty(commandName))
                    continue;
                
                // Extract arguments
                var args = new Dictionary<string, object>();
                if (cmdDict.TryGetValue("args", out var argsVal) && argsVal is Dictionary<string, object> argsDict)
                    args = argsDict;
                else if (cmdDict.TryGetValue("arguments", out var args2Val) && args2Val is Dictionary<string, object> args2Dict)
                    args = args2Dict;
                
                // Create a tool call and execute it
                var toolCall = new LLMToolCall("multi_" + successCount, commandName, args);
                var result = ExecuteToolCall(toolCall);
                
                if (result.Success)
                    successCount++;
                    
                messages.Add(result.Message);
            }
            
            bool allSuccess = successCount == commands.Count;
            return new ExecutionResult
            {
                Success = allSuccess,
                CommandName = "multi_command",
                Message = allSuccess 
                    ? $"Executed {successCount} commands successfully" 
                    : $"Executed {successCount}/{commands.Count} commands: {string.Join("; ", messages)}"
            };
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[VoiceCommandExecutor] {message}");
            }
        }
    }
    
    /// <summary>
    /// Represents a tool call from LLM response
    /// </summary>
    [Serializable]
    public class LLMToolCall
    {
        public string Id;
        public string FunctionName;
        public Dictionary<string, object> Arguments;
        
        public LLMToolCall(string id, string functionName, Dictionary<string, object> arguments)
        {
            Id = id;
            FunctionName = functionName;
            Arguments = arguments ?? new Dictionary<string, object>();
        }
    }
}
