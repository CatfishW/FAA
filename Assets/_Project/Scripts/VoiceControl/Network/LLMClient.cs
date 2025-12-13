using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using VoiceControl.Core;

namespace VoiceControl.Network
{
    /// <summary>
    /// HTTP client for LLM server (OpenAI-compatible API).
    /// Sends chat completions with function calling support.
    /// </summary>
    public class LLMClient : MonoBehaviour
    {
        [Header("Server Configuration")]
        [SerializeField] private string serverUrl = "http://localhost:25565";
        [SerializeField] private string modelName = "qwen3-30b-a3b-instruct";
        [SerializeField] private string apiKey = "";
        [SerializeField] private float timeout = 120f;
        
        [Header("Generation Settings")]
        [SerializeField] private float temperature = 0.3f;
        [SerializeField] private int maxTokens = 1024;
        
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;
        
        /// <summary>
        /// Event fired when LLM response is received
        /// </summary>
        public event Action<LLMResponse> OnResponseReceived;
        
        /// <summary>
        /// Event fired on streaming text chunk
        /// </summary>
        public event Action<string> OnStreamingChunk;
        
        /// <summary>
        /// Event fired on error
        /// </summary>
        public event Action<string> OnError;
        
        /// <summary>
        /// Whether a request is currently in progress
        /// </summary>
        public bool IsProcessing { get; private set; }
        
        private VoiceCommandRegistry _registry;
        
        private void Awake()
        {
            _registry = VoiceCommandRegistry.Instance;
        }
        
        /// <summary>
        /// Set the server URL
        /// </summary>
        public void SetServerUrl(string url)
        {
            serverUrl = url.TrimEnd('/');
        }
        
        /// <summary>
        /// Set API key for authentication
        /// </summary>
        public void SetApiKey(string key)
        {
            apiKey = key;
        }
        
        /// <summary>
        /// Send a voice command to the LLM for interpretation
        /// </summary>
        /// <param name="userMessage">Transcribed voice command</param>
        /// <param name="callback">Callback with parsed response</param>
        public void SendVoiceCommand(string userMessage, Action<LLMResponse> callback = null)
        {
            if (IsProcessing)
            {
                Log("Already processing a request");
                return;
            }
            
            if (_registry == null)
            {
                _registry = VoiceCommandRegistry.Instance;
            }
            
            StartCoroutine(SendRequestCoroutine(userMessage, callback));
        }
        
        private IEnumerator SendRequestCoroutine(string userMessage, Action<LLMResponse> callback)
        {
            IsProcessing = true;
            
            string url = $"{serverUrl}/v1/chat/completions";
            Log($"Sending to {url}: \"{userMessage}\"");
            
            // Build request body
            var requestBody = BuildRequestBody(userMessage);
            string jsonBody = SerializeRequest(requestBody);
            
            Log($"Request body: {jsonBody}");
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                if (!string.IsNullOrEmpty(apiKey))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }
                
                request.timeout = (int)timeout;
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    Log($"Response: {responseText}");
                    
                    LLMResponse response = ParseResponse(responseText);
                    
                    callback?.Invoke(response);
                    OnResponseReceived?.Invoke(response);
                }
                else
                {
                    string error = $"LLM Error: {request.error} - {request.downloadHandler?.text}";
                    Log(error);
                    OnError?.Invoke(error);
                    callback?.Invoke(null);
                }
            }
            
            IsProcessing = false;
        }
        
        private Dictionary<string, object> BuildRequestBody(string userMessage)
        {
            var messages = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string>
                {
                    ["role"] = "system",
                    ["content"] = _registry != null ? _registry.GetSystemPrompt() : GetDefaultSystemPrompt()
                },
                new Dictionary<string, string>
                {
                    ["role"] = "user",
                    ["content"] = userMessage
                }
            };
            
            // Simple request body - no tool calls, LLM outputs JSON directly
            var body = new Dictionary<string, object>
            {
                ["model"] = modelName,
                ["messages"] = messages,
                ["temperature"] = temperature,
                ["max_tokens"] = maxTokens,
                ["stream"] = false
            };
            
            return body;
        }
        
        private string GetDefaultSystemPrompt()
        {
            return "You are a voice command assistant. Output JSON: {\"command\": \"command_name\", \"args\": {}}";
        }
        
        private string SerializeRequest(Dictionary<string, object> request)
        {
            // Simple JSON serialization (Unity's JsonUtility doesn't handle dictionaries well)
            return MiniJson.Serialize(request);
        }
        
        private LLMResponse ParseResponse(string jsonResponse)
        {
            try
            {
                var response = new LLMResponse();
                
                // Parse using MiniJson
                var json = MiniJson.Deserialize(jsonResponse) as Dictionary<string, object>;
                if (json == null)
                {
                    response.Error = "Failed to parse JSON response";
                    return response;
                }
                
                // Get choices array
                if (json.TryGetValue("choices", out var choicesObj) && choicesObj is List<object> choices && choices.Count > 0)
                {
                    var choice = choices[0] as Dictionary<string, object>;
                    if (choice != null && choice.TryGetValue("message", out var messageObj))
                    {
                        var message = messageObj as Dictionary<string, object>;
                        if (message != null)
                        {
                            // Get text content - this is where LLM outputs JSON command
                            if (message.TryGetValue("content", out var contentObj) && contentObj != null)
                            {
                                response.TextContent = contentObj.ToString();
                                
                                // Parse the JSON command from content
                                var toolCall = ParseJsonCommand(response.TextContent);
                                if (toolCall != null)
                                {
                                    response.ToolCalls = new List<LLMToolCall> { toolCall };
                                    Log($"Parsed command from JSON: {toolCall.FunctionName}");
                                }
                            }
                        }
                    }
                }
                
                return response;
            }
            catch (Exception e)
            {
                Log($"Error parsing response: {e.Message}");
                return new LLMResponse { Error = e.Message };
            }
        }
        
        /// <summary>
        /// Parse JSON command output from LLM content
        /// Expected format: {"command": "target_name", "args": {...}}
        /// Or for multiple: {"commands": [{"command": "...", "args": {}}, ...]}
        /// </summary>
        private LLMToolCall ParseJsonCommand(string content)
        {
            if (string.IsNullOrEmpty(content)) return null;
            
            content = content.Trim();
            
            // Strip markdown code blocks if present
            if (content.StartsWith("```"))
            {
                int start = content.IndexOf('\n');
                int end = content.LastIndexOf("```");
                if (start > 0 && end > start)
                {
                    content = content.Substring(start + 1, end - start - 1).Trim();
                }
            }
            
            // Must start with {
            if (!content.StartsWith("{"))
            {
                // Try to find JSON in the content
                int jsonStart = content.IndexOf('{');
                int jsonEnd = content.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    content = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                }
                else
                {
                    return null;
                }
            }
            
            try
            {
                var obj = MiniJson.Deserialize(content) as Dictionary<string, object>;
                if (obj == null) return null;
                
                // Check for error response
                if (obj.TryGetValue("command", out var cmdVal) && cmdVal?.ToString() == "error")
                {
                    Log($"LLM returned error: {obj.GetValueOrDefault("message", "Unknown error")}");
                    return null;
                }
                
                // Check for multi-command array format: {"commands": [...]}
                if (obj.TryGetValue("commands", out var commandsVal) && commandsVal is List<object> commandsList)
                {
                    Log($"Parsing multi-command response with {commandsList.Count} commands");
                    // Return a special multi-command tool call
                    return new LLMToolCall("multi_cmd", "multi_command", new Dictionary<string, object>
                    {
                        ["commands"] = commandsList
                    });
                }
                
                // Extract command name (single command format)
                string commandName = null;
                if (obj.TryGetValue("command", out var commandVal))
                    commandName = commandVal?.ToString();
                else if (obj.TryGetValue("function", out var funcVal))
                    commandName = funcVal?.ToString();
                else if (obj.TryGetValue("name", out var nameVal))
                    commandName = nameVal?.ToString();
                
                if (string.IsNullOrEmpty(commandName))
                    return null;
                
                // Extract arguments
                var args = new Dictionary<string, object>();
                if (obj.TryGetValue("args", out var argsVal) && argsVal is Dictionary<string, object> argsDict)
                    args = argsDict;
                else if (obj.TryGetValue("arguments", out var args2Val) && args2Val is Dictionary<string, object> args2Dict)
                    args = args2Dict;
                else if (obj.TryGetValue("parameters", out var paramsVal) && paramsVal is Dictionary<string, object> paramsDict)
                    args = paramsDict;
                
                return new LLMToolCall("json_cmd", commandName, args);
            }
            catch (Exception e)
            {
                Log($"Failed to parse JSON command: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Check if text looks like a function name (snake_case, no spaces except maybe arguments)
        /// </summary>
        private bool IsLikelyFunctionName(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            
            // Remove any JSON-like arguments at the end
            int parenIndex = text.IndexOf('(');
            if (parenIndex > 0)
                text = text.Substring(0, parenIndex);
            
            // Check first part - should be snake_case like function names
            string potentialName = text.Split(' ', '\n')[0].Trim();
            
            // Function names typically have underscores and are lowercase
            if (potentialName.Contains("_") && !potentialName.Contains(" "))
            {
                // Verify it matches a known command pattern
                if (_registry != null)
                {
                    var commands = _registry.GetAllCommands();
                    foreach (var cmd in commands)
                    {
                        string fullName = $"{cmd.TargetName}_{cmd.CommandName}";
                        if (potentialName.Equals(fullName, StringComparison.OrdinalIgnoreCase) ||
                            potentialName.Equals(cmd.CommandName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
                // Even if not in registry, accept snake_case patterns
                return System.Text.RegularExpressions.Regex.IsMatch(potentialName, @"^[a-z][a-z0-9_]*$");
            }
            
            return false;
        }
        
        /// <summary>
        /// Try to parse text content as a tool call
        /// </summary>
        private LLMToolCall ParseTextAsToolCall(string text)
        {
            // Parse function name - could be "function_name" or "function_name(args)"
            string functionName = text.Trim();
            var arguments = new Dictionary<string, object>();
            
            // Check for parentheses with arguments
            int parenStart = functionName.IndexOf('(');
            if (parenStart > 0)
            {
                int parenEnd = functionName.LastIndexOf(')');
                if (parenEnd > parenStart)
                {
                    string argsStr = functionName.Substring(parenStart + 1, parenEnd - parenStart - 1);
                    functionName = functionName.Substring(0, parenStart);
                    
                    // Try to parse arguments
                    if (!string.IsNullOrWhiteSpace(argsStr))
                    {
                        // Try JSON parse
                        if (argsStr.StartsWith("{"))
                        {
                            var parsed = MiniJson.Deserialize(argsStr) as Dictionary<string, object>;
                            if (parsed != null)
                                arguments = parsed;
                        }
                        else
                        {
                            // Simple key=value parsing
                            foreach (var part in argsStr.Split(','))
                            {
                                var keyVal = part.Split('=');
                                if (keyVal.Length == 2)
                                {
                                    arguments[keyVal[0].Trim()] = keyVal[1].Trim().Trim('"', '\'');
                                }
                            }
                        }
                    }
                }
            }
            
            // Take only the first "word" if there are spaces (LLM might add explanation)
            functionName = functionName.Split(' ', '\n')[0].Trim();
            
            if (string.IsNullOrEmpty(functionName))
                return null;
            
            return new LLMToolCall("fallback_" + Guid.NewGuid().ToString("N").Substring(0, 8), functionName, arguments);
        }
        
        /// <summary>
        /// Try to parse a JSON object as a tool call
        /// </summary>
        private LLMToolCall TryParseJsonToolCall(string json)
        {
            try
            {
                var obj = MiniJson.Deserialize(json) as Dictionary<string, object>;
                if (obj == null) return null;
                
                string name = null;
                var args = new Dictionary<string, object>();
                
                // Look for function/name field
                if (obj.TryGetValue("function", out var funcVal))
                    name = funcVal?.ToString();
                else if (obj.TryGetValue("name", out var nameVal))
                    name = nameVal?.ToString();
                else if (obj.TryGetValue("tool", out var toolVal))
                    name = toolVal?.ToString();
                else if (obj.TryGetValue("command", out var cmdVal))
                    name = cmdVal?.ToString();
                
                // Look for arguments
                if (obj.TryGetValue("arguments", out var argsVal) && argsVal is Dictionary<string, object> argsDict)
                    args = argsDict;
                else if (obj.TryGetValue("args", out var args2Val) && args2Val is Dictionary<string, object> args2Dict)
                    args = args2Dict;
                else if (obj.TryGetValue("parameters", out var paramsVal) && paramsVal is Dictionary<string, object> paramsDict)
                    args = paramsDict;
                
                if (!string.IsNullOrEmpty(name))
                    return new LLMToolCall("json_" + Guid.NewGuid().ToString("N").Substring(0, 8), name, args);
            }
            catch { }
            
            return null;
        }
        
        /// <summary>
        /// Test connection to LLM server
        /// </summary>
        public void TestConnection(Action<bool, string> callback)
        {
            StartCoroutine(TestConnectionCoroutine(callback));
        }
        
        private IEnumerator TestConnectionCoroutine(Action<bool, string> callback)
        {
            string url = $"{serverUrl}/health";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 5;
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    callback?.Invoke(true, "LLM server connected");
                }
                else
                {
                    callback?.Invoke(false, $"Cannot connect to LLM server: {request.error}");
                }
            }
        }
        
        /// <summary>
        /// Fetch available models from the LLM server (for Editor use)
        /// </summary>
        /// <param name="serverUrl">URL of the LLM server</param>
        /// <param name="callback">Callback with list of model IDs and error message</param>
        public static void FetchAvailableModels(string serverUrl, System.Action<List<string>, string> callback)
        {
            var models = new List<string>();
            string url = serverUrl.TrimEnd('/') + "/v1/models";
            
            try
            {
                using (var client = new System.Net.WebClient())
                {
                    string response = client.DownloadString(url);
                    
                    // Parse using MiniJson
                    var json = MiniJson.Deserialize(response) as Dictionary<string, object>;
                    if (json != null && json.TryGetValue("data", out var dataObj) && dataObj is List<object> dataList)
                    {
                        foreach (var item in dataList)
                        {
                            if (item is Dictionary<string, object> modelObj)
                            {
                                if (modelObj.TryGetValue("id", out var idObj) && idObj != null)
                                {
                                    models.Add(idObj.ToString());
                                }
                            }
                        }
                    }
                    
                    if (models.Count > 0)
                    {
                        callback?.Invoke(models, null);
                    }
                    else
                    {
                        callback?.Invoke(models, "No models found in server response");
                    }
                }
            }
            catch (System.Exception e)
            {
                callback?.Invoke(models, $"Failed to fetch models: {e.Message}");
            }
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[LLMClient] {message}");
            }
        }
    }
    
    /// <summary>
    /// Parsed LLM response
    /// </summary>
    [Serializable]
    public class LLMResponse
    {
        public string TextContent;
        public List<LLMToolCall> ToolCalls;
        public string Error;
        
        public bool HasToolCalls => ToolCalls != null && ToolCalls.Count > 0;
        public bool HasError => !string.IsNullOrEmpty(Error);
    }
    
    /// <summary>
    /// Simple JSON serializer/deserializer for Unity
    /// Based on MiniJSON by Calvin Rien
    /// </summary>
    public static class MiniJson
    {
        public static string Serialize(object obj)
        {
            return SerializeValue(obj);
        }
        
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;
            
            int index = 0;
            return ParseValue(json, ref index);
        }
        
        private static string SerializeValue(object value)
        {
            if (value == null)
                return "null";
            
            if (value is bool b)
                return b ? "true" : "false";
            
            if (value is string s)
                return SerializeString(s);
            
            if (value is int || value is long || value is float || value is double)
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            
            if (value is IDictionary<string, object> dict)
                return SerializeDict(dict);
            
            if (value is IList<object> list)
                return SerializeList(list);
            
            if (value is IEnumerable<Dictionary<string, object>> enumDict)
            {
                var sb = new StringBuilder("[");
                bool first = true;
                foreach (var item in enumDict)
                {
                    if (!first) sb.Append(",");
                    sb.Append(SerializeDict(item));
                    first = false;
                }
                sb.Append("]");
                return sb.ToString();
            }
            
            if (value is IEnumerable<Dictionary<string, string>> enumStrDict)
            {
                var sb = new StringBuilder("[");
                bool first = true;
                foreach (var item in enumStrDict)
                {
                    if (!first) sb.Append(",");
                    sb.Append("{");
                    bool innerFirst = true;
                    foreach (var kvp in item)
                    {
                        if (!innerFirst) sb.Append(",");
                        sb.Append(SerializeString(kvp.Key));
                        sb.Append(":");
                        sb.Append(SerializeString(kvp.Value));
                        innerFirst = false;
                    }
                    sb.Append("}");
                    first = false;
                }
                sb.Append("]");
                return sb.ToString();
            }
            
            if (value is System.Collections.IEnumerable enumerable)
            {
                var sb = new StringBuilder("[");
                bool first = true;
                foreach (var item in enumerable)
                {
                    if (!first) sb.Append(",");
                    sb.Append(SerializeValue(item));
                    first = false;
                }
                sb.Append("]");
                return sb.ToString();
            }
            
            return SerializeString(value.ToString());
        }
        
        private static string SerializeString(string str)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in str)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }
        
        private static string SerializeDict(IDictionary<string, object> dict)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var kvp in dict)
            {
                if (!first) sb.Append(",");
                sb.Append(SerializeString(kvp.Key));
                sb.Append(":");
                sb.Append(SerializeValue(kvp.Value));
                first = false;
            }
            sb.Append("}");
            return sb.ToString();
        }
        
        private static string SerializeList(IList<object> list)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var item in list)
            {
                if (!first) sb.Append(",");
                sb.Append(SerializeValue(item));
                first = false;
            }
            sb.Append("]");
            return sb.ToString();
        }
        
        private static object ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) return null;
            
            char c = json[index];
            
            if (c == '{') return ParseObject(json, ref index);
            if (c == '[') return ParseArray(json, ref index);
            if (c == '"') return ParseString(json, ref index);
            if (c == 't' || c == 'f') return ParseBool(json, ref index);
            if (c == 'n') return ParseNull(json, ref index);
            if (c == '-' || char.IsDigit(c)) return ParseNumber(json, ref index);
            
            return null;
        }
        
        private static Dictionary<string, object> ParseObject(string json, ref int index)
        {
            var dict = new Dictionary<string, object>();
            index++; // skip '{'
            
            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                if (json[index] == '}') { index++; return dict; }
                if (json[index] == ',') { index++; continue; }
                
                string key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                if (json[index] == ':') index++;
                object value = ParseValue(json, ref index);
                dict[key] = value;
            }
            
            return dict;
        }
        
        private static List<object> ParseArray(string json, ref int index)
        {
            var list = new List<object>();
            index++; // skip '['
            
            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                if (json[index] == ']') { index++; return list; }
                if (json[index] == ',') { index++; continue; }
                
                list.Add(ParseValue(json, ref index));
            }
            
            return list;
        }
        
        private static string ParseString(string json, ref int index)
        {
            var sb = new StringBuilder();
            index++; // skip opening quote
            
            while (index < json.Length)
            {
                char c = json[index++];
                if (c == '"') return sb.ToString();
                if (c == '\\' && index < json.Length)
                {
                    char next = json[index++];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (index + 4 <= json.Length)
                            {
                                string hex = json.Substring(index, 4);
                                sb.Append((char)Convert.ToInt32(hex, 16));
                                index += 4;
                            }
                            break;
                        default: sb.Append(next); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            
            return sb.ToString();
        }
        
        private static object ParseNumber(string json, ref int index)
        {
            int start = index;
            if (json[index] == '-') index++;
            while (index < json.Length && char.IsDigit(json[index])) index++;
            
            bool isFloat = false;
            if (index < json.Length && json[index] == '.')
            {
                isFloat = true;
                index++;
                while (index < json.Length && char.IsDigit(json[index])) index++;
            }
            
            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                isFloat = true;
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-')) index++;
                while (index < json.Length && char.IsDigit(json[index])) index++;
            }
            
            string numStr = json.Substring(start, index - start);
            if (isFloat)
                return double.Parse(numStr, System.Globalization.CultureInfo.InvariantCulture);
            else
                return long.Parse(numStr);
        }
        
        private static bool ParseBool(string json, ref int index)
        {
            if (json.Substring(index, 4) == "true") { index += 4; return true; }
            if (json.Substring(index, 5) == "false") { index += 5; return false; }
            return false;
        }
        
        private static object ParseNull(string json, ref int index)
        {
            index += 4;
            return null;
        }
        
        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }
    }
}
