using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using VisualUnderstanding.Core;

namespace VisualUnderstanding.Network
{
    /// <summary>
    /// Vision LLM client supporting OpenAI-compatible vision API.
    /// Handles base64 image encoding and multimodal chat completions.
    /// </summary>
    [AddComponentMenu("Visual Understanding/Network/Vision LLM Client")]
    public class VisionLLMClient : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private VisionSettings settings;
        
        [Header("Override Settings (optional)")]
        [SerializeField] private string serverUrlOverride;
        [SerializeField] private string modelNameOverride;
        
        /// <summary>
        /// Event fired when analysis completes
        /// </summary>
        public event Action<VisionAnalysisResult> OnAnalysisComplete;
        
        /// <summary>
        /// Event fired on each streaming text chunk
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
        
        private string ServerUrl => !string.IsNullOrEmpty(serverUrlOverride) ? serverUrlOverride : settings?.serverUrl ?? "https://game.agaii.org/llm/v1";
        private string ModelName => !string.IsNullOrEmpty(modelNameOverride) ? modelNameOverride : settings?.modelName ?? "Qwen/Qwen3-VL-4B-Instruct-FP8";
        
        // Cancellation support
        private bool _cancelRequested = false;
        private Coroutine _currentCoroutine = null;
        private UnityWebRequest _currentRequest = null;
        
        /// <summary>
        /// Cancel the current request if in progress
        /// </summary>
        public void Cancel()
        {
            if (IsProcessing || _currentRequest != null)
            {
                Debug.Log("[VisionLLMClient] Canceling current request");
                _cancelRequested = true;
                IsProcessing = false;
                
                // Abort and dispose the HTTP request
                if (_currentRequest != null)
                {
                    try
                    {
                        _currentRequest.Abort();
                        _currentRequest.Dispose();
                    }
                    catch (System.Exception) { }
                    _currentRequest = null;
                }
                
                // Stop the coroutine
                if (_currentCoroutine != null)
                {
                    StopCoroutine(_currentCoroutine);
                    _currentCoroutine = null;
                }
            }
        }
        
        #region Public Methods
        
        /// <summary>
        /// Set the settings asset
        /// </summary>
        public void SetSettings(VisionSettings newSettings)
        {
            settings = newSettings;
        }
        
        /// <summary>
        /// Analyze a Texture2D image
        /// </summary>
        public void AnalyzeImage(Texture2D image, VisualAnalysisType type, Action<VisionAnalysisResult> callback = null)
        {
            if (IsProcessing)
            {
                Log("Already processing a request, canceling...");
                Cancel();
            }
            
            if (image == null)
            {
                Log("Image is null");
                callback?.Invoke(VisionAnalysisResult.Error("Image is null"));
                return;
            }
            
            _cancelRequested = false;
            _currentCoroutine = StartCoroutine(AnalyzeImageCoroutine(image, type, callback));
        }
        
        /// <summary>
        /// Analyze an image from a file path
        /// </summary>
        public void AnalyzeImageFromFile(string filePath, VisualAnalysisType type, Action<VisionAnalysisResult> callback = null)
        {
            if (IsProcessing)
            {
                callback?.Invoke(VisionAnalysisResult.Error("Already processing a request"));
                return;
            }
            
            StartCoroutine(AnalyzeImageFromFileCoroutine(filePath, type, callback));
        }
        
        /// <summary>
        /// Analyze an image from a URL
        /// </summary>
        public void AnalyzeImageFromUrl(string imageUrl, VisualAnalysisType type, Action<VisionAnalysisResult> callback = null)
        {
            if (IsProcessing)
            {
                callback?.Invoke(VisionAnalysisResult.Error("Already processing a request"));
                return;
            }
            
            StartCoroutine(AnalyzeImageUrlCoroutine(imageUrl, type, callback));
        }
        
        /// <summary>
        /// Test connection to the vision LLM server
        /// </summary>
        public void TestConnection(Action<bool, string> callback)
        {
            StartCoroutine(TestConnectionCoroutine(callback));
        }
        
        #endregion
        
        #region Coroutines
        
        private IEnumerator AnalyzeImageCoroutine(Texture2D image, VisualAnalysisType type, Action<VisionAnalysisResult> callback)
        {
            IsProcessing = true;
            float startTime = Time.realtimeSinceStartup;
            
            // Resize image if too large - 300 max for balance of OCR accuracy and upload size
            Texture2D resizedImage = image;
            int maxSize = 300;
            
            if (image.width > maxSize || image.height > maxSize)
            {
                float scale = Mathf.Min((float)maxSize / image.width, (float)maxSize / image.height);
                int newWidth = Mathf.Max(1, Mathf.RoundToInt(image.width * scale));
                int newHeight = Mathf.Max(1, Mathf.RoundToInt(image.height * scale));
                
                resizedImage = ResizeTexture(image, newWidth, newHeight);
                Log($"Resized image from {image.width}x{image.height} to {newWidth}x{newHeight}");
            }
            
            // Convert to base64 using JPEG for smaller size
            string base64Image;
            string mimeType = "image/jpeg";
            
            try
            {
                byte[] imageBytes = resizedImage.EncodeToJPG(50); // 50% quality for small size
                base64Image = Convert.ToBase64String(imageBytes);
                Log($"Image encoded as JPEG, size: {base64Image.Length} chars ({imageBytes.Length} bytes)");
                
                // Cleanup if we created a resized copy
                if (resizedImage != image)
                {
                    Destroy(resizedImage);
                }
            }
            catch (Exception e)
            {
                IsProcessing = false;
                if (resizedImage != image)
                {
                    Destroy(resizedImage);
                }
                var error = VisionAnalysisResult.Error($"Failed to encode image: {e.Message}");
                callback?.Invoke(error);
                OnError?.Invoke(error.error);
                yield break;
            }
            
            string dataUrl = $"data:{mimeType};base64,{base64Image}";
            string prompt = settings != null ? settings.GetPromptForType(type) : "Describe this image concisely.";
            
            yield return SendVisionRequest(dataUrl, prompt, type, startTime, callback);
        }
        
        private Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            Graphics.Blit(source, rt);
            
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            
            Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            
            return result;
        }
        
        private IEnumerator AnalyzeImageFromFileCoroutine(string filePath, VisualAnalysisType type, Action<VisionAnalysisResult> callback)
        {
            IsProcessing = true;
            float startTime = Time.realtimeSinceStartup;
            
            // Load file
            if (!System.IO.File.Exists(filePath))
            {
                IsProcessing = false;
                var error = VisionAnalysisResult.Error($"File not found: {filePath}");
                callback?.Invoke(error);
                OnError?.Invoke(error.error);
                yield break;
            }
            
            string extension = System.IO.Path.GetExtension(filePath).ToLower();
            string mimeType = extension switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "image/png"
            };
            
            string base64Image;
            try
            {
                byte[] imageBytes = System.IO.File.ReadAllBytes(filePath);
                base64Image = Convert.ToBase64String(imageBytes);
                Log($"Loaded file: {filePath}, size: {imageBytes.Length} bytes");
            }
            catch (Exception e)
            {
                IsProcessing = false;
                var error = VisionAnalysisResult.Error($"Failed to read file: {e.Message}");
                callback?.Invoke(error);
                OnError?.Invoke(error.error);
                yield break;
            }
            
            string dataUrl = $"data:{mimeType};base64,{base64Image}";
            string prompt = settings != null ? settings.GetPromptForType(type) : "Describe this image concisely.";
            
            yield return SendVisionRequest(dataUrl, prompt, type, startTime, callback);
        }
        
        private IEnumerator AnalyzeImageUrlCoroutine(string imageUrl, VisualAnalysisType type, Action<VisionAnalysisResult> callback)
        {
            IsProcessing = true;
            float startTime = Time.realtimeSinceStartup;
            
            string prompt = settings != null ? settings.GetPromptForType(type) : "Describe this image concisely.";
            
            yield return SendVisionRequest(imageUrl, prompt, type, startTime, callback);
        }
        
        private IEnumerator SendVisionRequest(string imageUrlOrData, string prompt, VisualAnalysisType type, float startTime, Action<VisionAnalysisResult> callback)
        {
            string url = $"{ServerUrl}/chat/completions";
            Log($"Sending streaming vision request to: {url}");
            
            // Build request body with streaming enabled
            var requestBody = BuildVisionRequestBody(imageUrlOrData, prompt);
            requestBody["stream"] = true; // Enable streaming
            string jsonBody = MiniJsonVision.Serialize(requestBody);
            
            Log($"Request body length: {jsonBody.Length} chars");
            
            // Check if request is too large
            if (jsonBody.Length > 10_000_000)
            {
                IsProcessing = false;
                var error = VisionAnalysisResult.Error("Request too large - image exceeds size limits");
                callback?.Invoke(error);
                OnError?.Invoke(error.error);
                yield break;
            }
            
            UnityWebRequest request = null;
            try
            {
                request = new UnityWebRequest(url, "POST");
                _currentRequest = request; // Store for cancellation
                
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new StreamingDownloadHandler(this);
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "text/event-stream");
                
                string apiKey = settings?.apiKey ?? "";
                if (!string.IsNullOrEmpty(apiKey))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }
                
                request.timeout = (int)(settings?.timeout ?? 120f);
                
                Log($"Sending streaming request with timeout: {request.timeout}s");
                
                var asyncOp = request.SendWebRequest();
                
                // Poll for completion while checking for cancellation
                while (!asyncOp.isDone)
                {
                    if (_cancelRequested)
                    {
                        Log("Request cancelled");
                        request.Abort();
                        IsProcessing = false;
                        yield break;
                    }
                    yield return null;
                }
                
                // Check if we were cancelled after the request completed
                if (_cancelRequested)
                {
                    IsProcessing = false;
                    yield break;
                }
                
                float processingTime = Time.realtimeSinceStartup - startTime;
                
                var streamHandler = request.downloadHandler as StreamingDownloadHandler;
                string fullText = streamHandler?.FullText ?? "";
                
                if (request.result == UnityWebRequest.Result.Success || !string.IsNullOrEmpty(fullText))
                {
                    Log($"Streaming complete in {processingTime:F2}s, total length: {fullText.Length}");
                    
                    var result = VisionAnalysisResult.Success(type, fullText);
                    result.rawResponse = fullText;
                    result.processingTime = processingTime;
                    result.confidence = 0.8f;
                    result.findings = ParseFindings(fullText);
                    
                    callback?.Invoke(result);
                    OnAnalysisComplete?.Invoke(result);
                }
                else
                {
                    string errorMsg;
                    switch (request.result)
                    {
                        case UnityWebRequest.Result.ConnectionError:
                            errorMsg = $"Connection error: {request.error}";
                            break;
                        case UnityWebRequest.Result.ProtocolError:
                            errorMsg = $"HTTP error {request.responseCode}: {request.error}";
                            break;
                        default:
                            errorMsg = $"Error: {request.error}";
                            break;
                    }
                    
                    Log(errorMsg);
                    
                    var error = VisionAnalysisResult.Error(errorMsg);
                    error.processingTime = processingTime;
                    callback?.Invoke(error);
                    OnError?.Invoke(errorMsg);
                }
            }
            finally
            {
                if (request != null)
                {
                    request.Dispose();
                }
                _currentRequest = null;
            }
            
            IsProcessing = false;
        }
        
        /// <summary>
        /// Fire streaming chunk event (called by StreamingDownloadHandler)
        /// </summary>
        internal void FireStreamingChunk(string chunk)
        {
            OnStreamingChunk?.Invoke(chunk);
        }
        
        private IEnumerator TestConnectionCoroutine(Action<bool, string> callback)
        {
            // Try a simple text completion to test connectivity
            string url = $"{ServerUrl}/chat/completions";
            
            var testBody = new Dictionary<string, object>
            {
                ["model"] = ModelName,
                ["messages"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = "Say 'OK' if you can hear me."
                    }
                },
                ["max_tokens"] = 16
            };
            
            string jsonBody = MiniJsonVision.Serialize(testBody);
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                string apiKey = settings?.apiKey ?? "empty";
                if (!string.IsNullOrEmpty(apiKey))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }
                
                request.timeout = 10;
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    callback?.Invoke(true, $"Connected to {ServerUrl} using {ModelName}");
                }
                else
                {
                    callback?.Invoke(false, $"Failed to connect: {request.error}");
                }
            }
        }
        
        #endregion
        
        #region Request Building
        
        private Dictionary<string, object> BuildVisionRequestBody(string imageUrlOrData, string prompt)
        {
            // Build content array with image and text
            var contentList = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["type"] = "image_url",
                    ["image_url"] = new Dictionary<string, object>
                    {
                        ["url"] = imageUrlOrData
                    }
                },
                new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] = prompt
                }
            };
            
            var messages = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["role"] = "user",
                    ["content"] = contentList
                }
            };
            
            return new Dictionary<string, object>
            {
                ["model"] = ModelName,
                ["messages"] = messages,
                ["max_tokens"] = settings?.maxTokens ?? 2048,
                ["temperature"] = settings?.temperature ?? 0.3f
            };
        }
        
        #endregion
        
        #region Response Parsing
        
        private VisionAnalysisResult ParseResponse(string jsonResponse, VisualAnalysisType type, float processingTime)
        {
            try
            {
                var json = MiniJsonVision.Deserialize(jsonResponse) as Dictionary<string, object>;
                if (json == null)
                {
                    return VisionAnalysisResult.Error("Failed to parse JSON response");
                }
                
                // Extract content from response
                if (json.TryGetValue("choices", out var choicesObj) && choicesObj is List<object> choices && choices.Count > 0)
                {
                    var choice = choices[0] as Dictionary<string, object>;
                    if (choice != null && choice.TryGetValue("message", out var messageObj))
                    {
                        var message = messageObj as Dictionary<string, object>;
                        if (message != null && message.TryGetValue("content", out var contentObj) && contentObj != null)
                        {
                            string content = contentObj.ToString();
                            
                            var result = VisionAnalysisResult.Success(type, content);
                            result.rawResponse = content;
                            result.processingTime = processingTime;
                            result.confidence = 0.8f; // Default confidence
                            
                            // Parse findings from content
                            result.findings = ParseFindings(content);
                            
                            return result;
                        }
                    }
                }
                
                return VisionAnalysisResult.Error("No content in response");
            }
            catch (Exception e)
            {
                Log($"Error parsing response: {e.Message}");
                return VisionAnalysisResult.Error($"Parse error: {e.Message}");
            }
        }
        
        private List<AnalysisFinding> ParseFindings(string content)
        {
            var findings = new List<AnalysisFinding>();
            
            // Split by common bullet point characters
            string[] lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                
                // Remove bullet point characters
                if (trimmed.StartsWith("-") || trimmed.StartsWith("•") || trimmed.StartsWith("*"))
                {
                    trimmed = trimmed.Substring(1).Trim();
                }
                else if (trimmed.Length > 2 && char.IsDigit(trimmed[0]) && (trimmed[1] == '.' || trimmed[1] == ')'))
                {
                    trimmed = trimmed.Substring(2).Trim();
                }
                
                if (string.IsNullOrEmpty(trimmed)) continue;
                
                // Determine priority based on keywords
                BriefingPriority priority = BriefingPriority.Info;
                string lowerLine = trimmed.ToLower();
                
                if (lowerLine.Contains("warning") || lowerLine.Contains("danger") || lowerLine.Contains("severe"))
                {
                    priority = BriefingPriority.Warning;
                }
                else if (lowerLine.Contains("caution") || lowerLine.Contains("moderate"))
                {
                    priority = BriefingPriority.Caution;
                }
                else if (lowerLine.Contains("critical") || lowerLine.Contains("emergency") || lowerLine.Contains("extreme"))
                {
                    priority = BriefingPriority.Critical;
                }
                
                findings.Add(new AnalysisFinding(trimmed, "", priority));
            }
            
            return findings;
        }
        
        #endregion
        
        private void Log(string message)
        {
            if (settings == null || settings.verboseLogging)
            {
                Debug.Log($"[VisionLLMClient] {message}");
            }
        }
    }
    
    /// <summary>
    /// Simple JSON serializer/deserializer for Vision requests
    /// </summary>
    public static class MiniJsonVision
    {
        public static string Serialize(object obj)
        {
            return SerializeValue(obj);
        }
        
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int index = 0;
            return ParseValue(json, ref index);
        }
        
        private static string SerializeValue(object value)
        {
            if (value == null) return "null";
            if (value is bool b) return b ? "true" : "false";
            if (value is string s) return SerializeString(s);
            if (value is int || value is long) return value.ToString();
            if (value is float f) return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value is double d) return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value is IDictionary<string, object> dict) return SerializeDict(dict);
            if (value is IList<object> list) return SerializeList(list);
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
            index++;
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
            index++;
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
            index++;
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
                else sb.Append(c);
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
            if (isFloat) return double.Parse(numStr, System.Globalization.CultureInfo.InvariantCulture);
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
    
    /// <summary>
    /// Custom download handler that parses SSE streaming responses and fires events
    /// </summary>
    public class StreamingDownloadHandler : DownloadHandlerScript
    {
        private VisionLLMClient _client;
        private StringBuilder _fullText = new StringBuilder();
        private StringBuilder _buffer = new StringBuilder();
        
        public string FullText => _fullText.ToString();
        
        public StreamingDownloadHandler(VisionLLMClient client) : base()
        {
            _client = client;
        }
        
        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength == 0) return true;
            
            string chunk = Encoding.UTF8.GetString(data, 0, dataLength);
            _buffer.Append(chunk);
            
            // Process complete lines in the buffer
            ProcessBuffer();
            
            return true;
        }
        
        private void ProcessBuffer()
        {
            string bufferStr = _buffer.ToString();
            string[] lines = bufferStr.Split('\n');
            
            // Process all complete lines (all but the last one which may be incomplete)
            for (int i = 0; i < lines.Length - 1; i++)
            {
                string line = lines[i].Trim();
                ProcessLine(line);
            }
            
            // Keep the last potentially incomplete line in the buffer
            _buffer.Clear();
            if (lines.Length > 0)
            {
                _buffer.Append(lines[lines.Length - 1]);
            }
        }
        
        private void ProcessLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            
            // SSE format: data: {...}
            if (line.StartsWith("data: "))
            {
                string jsonData = line.Substring(6);
                
                // Check for stream end
                if (jsonData == "[DONE]") return;
                
                // Parse the JSON to extract content delta
                try
                {
                    var json = MiniJsonVision.Deserialize(jsonData) as Dictionary<string, object>;
                    if (json == null) return;
                    
                    if (json.TryGetValue("choices", out var choicesObj) && 
                        choicesObj is List<object> choices && choices.Count > 0)
                    {
                        var choice = choices[0] as Dictionary<string, object>;
                        if (choice != null && choice.TryGetValue("delta", out var deltaObj))
                        {
                            var delta = deltaObj as Dictionary<string, object>;
                            if (delta != null && delta.TryGetValue("content", out var contentObj) && contentObj != null)
                            {
                                string content = contentObj.ToString();
                                if (!string.IsNullOrEmpty(content))
                                {
                                    _fullText.Append(content);
                                    _client?.FireStreamingChunk(content);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore parse errors for individual chunks
                }
            }
        }
        
        protected override void CompleteContent()
        {
            // Process any remaining buffer
            if (_buffer.Length > 0)
            {
                ProcessLine(_buffer.ToString().Trim());
            }
        }
    }
}
