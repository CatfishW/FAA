using System;
using System.Collections;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace VoiceControl.Network
{
    /// <summary>
    /// HTTPS client for Speech-to-Text server (Whisper.cpp compatible).
    /// Uses System.Net.Http.HttpClient for robust TLS/HTTPS connection handling.
    /// Falls back to UnityWebRequest for WebGL builds.
    /// </summary>
    public class STTClient : MonoBehaviour
    {
        [Header("Server Configuration")]
        [SerializeField] private string serverUrl = "https://game.agaii.org/stt";
        [SerializeField] private float timeoutSeconds = 60f;
        
        [Header("HTTPS Settings")]
        [SerializeField] private bool acceptAllCertificates = true;
        [SerializeField] private int maxRetries = 3;
        [SerializeField] private float retryDelaySeconds = 0.5f;
        
        [Header("Request Settings")]
        [SerializeField] private float temperature = 0f;
        [SerializeField] private float temperatureInc = 0.2f;
        [SerializeField] private string responseFormat = "json";
        
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;
        
        // Persistent HttpClient instance for connection reuse with proper HTTPS handling
        private static HttpClient _httpClient;
        private static readonly object _httpClientLock = new object();
        
        /// <summary>
        /// Event fired when transcription completes
        /// </summary>
        public event Action<string> OnTranscriptionComplete;
        
        /// <summary>
        /// Event fired on error
        /// </summary>
        public event Action<string> OnError;
        
        /// <summary>
        /// Whether a request is currently in progress
        /// </summary>
        public bool IsProcessing { get; private set; }
        
        private void OnEnable()
        {
            EnsureHttpClient();
        }
        
        private void OnDestroy()
        {
            // Don't dispose static HttpClient - it's designed for reuse
        }
        
        private void EnsureHttpClient()
        {
            if (_httpClient != null) return;
            
            lock (_httpClientLock)
            {
                if (_httpClient != null) return;
                
                var handler = new HttpClientHandler();
                
                // Configure for better HTTPS handling
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    if (acceptAllCertificates)
                        return true;
                    return errors == SslPolicyErrors.None;
                };
                
                // Disable proxy for direct connection
                handler.UseProxy = false;
                
                // Use TLS 1.2 (Tls13 not available in Unity's .NET version)
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
                
                _httpClient = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(timeoutSeconds)
                };
                
                // Set default headers
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                
                Log("HttpClient initialized for HTTPS connections");
            }
        }
        
        /// <summary>
        /// Set the server URL
        /// </summary>
        public void SetServerUrl(string url)
        {
            serverUrl = url.TrimEnd('/');
        }
        
        /// <summary>
        /// Transcribe audio data to text
        /// </summary>
        /// <param name="audioData">WAV audio data</param>
        /// <param name="callback">Callback with transcribed text</param>
        public void Transcribe(byte[] audioData, Action<string> callback = null)
        {
            if (IsProcessing)
            {
                Log("Already processing a request");
                return;
            }
            
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL must use UnityWebRequest
            StartCoroutine(TranscribeWebGL(audioData, callback));
#else
            // Use HttpClient for better HTTPS handling on other platforms
            StartCoroutine(TranscribeHttpClient(audioData, callback));
#endif
        }
        
        /// <summary>
        /// HttpClient-based transcription for better HTTPS connection handling
        /// </summary>
        private IEnumerator TranscribeHttpClient(byte[] audioData, Action<string> callback)
        {
            IsProcessing = true;
            EnsureHttpClient();
            
            string url = $"{serverUrl}/inference";
            Log($"[HTTPS] Sending {audioData.Length} bytes to {url}");
            
            string lastError = null;
            bool success = false;
            string transcription = null;
            
            for (int attempt = 0; attempt < maxRetries && !success; attempt++)
            {
                if (attempt > 0)
                {
                    float delay = retryDelaySeconds * Mathf.Pow(2, attempt - 1);
                    Log($"[HTTPS] Retry attempt {attempt + 1}/{maxRetries} after {delay:F1}s delay...");
                    yield return new WaitForSeconds(delay);
                }
                
                // Create task and run async
                Task<(bool success, string result, string error)> task = null;
                float startTime = Time.realtimeSinceStartup;
                
                // Use shorter timeout on retries
                var cts = new CancellationTokenSource();
                int effectiveTimeoutMs = attempt == 0 
                    ? (int)(timeoutSeconds * 1000) 
                    : Mathf.Max(15000, (int)(timeoutSeconds * 500));
                cts.CancelAfter(effectiveTimeoutMs);
                
                task = SendHttpsRequest(url, audioData, cts.Token);
                
                // Wait for task to complete (outside try-catch to allow yield)
                while (!task.IsCompleted)
                {
                    yield return null;
                }
                
                try
                {
                    
                    float elapsed = Time.realtimeSinceStartup - startTime;
                    
                    if (task.IsFaulted)
                    {
                        lastError = $"HTTPS Error: {task.Exception?.InnerException?.Message ?? task.Exception?.Message}";
                        Log($"[HTTPS] Attempt {attempt + 1} failed after {elapsed:F1}s: {lastError}");
                    }
                    else if (task.IsCanceled)
                    {
                        lastError = "HTTPS Error: Request was cancelled or timed out";
                        Log($"[HTTPS] Attempt {attempt + 1} cancelled after {elapsed:F1}s");
                    }
                    else
                    {
                        var result = task.Result;
                        Log($"[HTTPS] Request completed in {elapsed:F1}s (attempt {attempt + 1})");
                        
                        if (result.success)
                        {
                            Log($"[HTTPS] Response: {result.result}");
                            transcription = ParseTranscription(result.result);
                            success = true;
                        }
                        else
                        {
                            lastError = result.error;
                            Log($"[HTTPS] Attempt {attempt + 1} failed: {lastError}");
                        }
                    }
                }
                catch (Exception e)
                {
                    lastError = $"HTTPS Exception: {e.Message}";
                    Log($"[HTTPS] Attempt {attempt + 1} exception: {e.Message}");
                }
            }
            
            CompleteRequest(success, transcription, lastError, callback);
        }
        
        private async Task<(bool success, string result, string error)> SendHttpsRequest(
            string url, byte[] audioData, CancellationToken cancellationToken)
        {
            try
            {
                // Build multipart form content
                using (var content = new MultipartFormDataContent())
                {
                    // Add audio file
                    var audioContent = new ByteArrayContent(audioData);
                    audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                    content.Add(audioContent, "file", "audio.wav");
                    
                    // Add parameters
                    content.Add(new StringContent(temperature.ToString("F1")), "temperature");
                    content.Add(new StringContent(temperatureInc.ToString("F1")), "temperature_inc");
                    content.Add(new StringContent(responseFormat), "response_format");
                    
                    // Send request
                    var response = await _httpClient.PostAsync(url, content, cancellationToken);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string responseText = await response.Content.ReadAsStringAsync();
                        return (true, responseText, null);
                    }
                    else
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        return (false, null, $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase} - {errorBody}");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return (false, null, "Request timed out");
            }
            catch (HttpRequestException e)
            {
                return (false, null, $"HTTPS request failed: {e.Message}");
            }
            catch (Exception e)
            {
                return (false, null, $"Unexpected error: {e.Message}");
            }
        }
        
        /// <summary>
        /// WebGL fallback using UnityWebRequest
        /// </summary>
        private IEnumerator TranscribeWebGL(byte[] audioData, Action<string> callback)
        {
            IsProcessing = true;
            
            string url = $"{serverUrl}/inference";
            Log($"[WebGL] Sending {audioData.Length} bytes to {url}");
            
            string lastError = null;
            bool success = false;
            string transcription = null;
            
            for (int attempt = 0; attempt < maxRetries && !success; attempt++)
            {
                if (attempt > 0)
                {
                    float delay = retryDelaySeconds * Mathf.Pow(2, attempt - 1);
                    Log($"[WebGL] Retry attempt {attempt + 1}/{maxRetries} after {delay:F1}s delay...");
                    yield return new WaitForSeconds(delay);
                }
                
                // Build multipart form data
                var formData = new System.Collections.Generic.List<IMultipartFormSection>();
                formData.Add(new MultipartFormFileSection("file", audioData, "audio.wav", "audio/wav"));
                formData.Add(new MultipartFormDataSection("temperature", temperature.ToString("F1")));
                formData.Add(new MultipartFormDataSection("temperature_inc", temperatureInc.ToString("F1")));
                formData.Add(new MultipartFormDataSection("response_format", responseFormat));
                
                byte[] boundary = UnityWebRequest.GenerateBoundary();
                byte[] formBytes = UnityWebRequest.SerializeFormSections(formData, boundary);
                
                using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    var uploadHandler = new UploadHandlerRaw(formBytes);
                    uploadHandler.contentType = "multipart/form-data; boundary=" + Encoding.UTF8.GetString(boundary);
                    request.uploadHandler = uploadHandler;
                    request.downloadHandler = new DownloadHandlerBuffer();
                    
                    // Shorter timeout on retries
                    request.timeout = attempt == 0 ? (int)timeoutSeconds : Mathf.Max(15, (int)(timeoutSeconds / 2));
                    request.useHttpContinue = false;
                    
                    // Force connection close to prevent stale connections
                    request.SetRequestHeader("Connection", "close");
                    request.SetRequestHeader("Accept", "application/json, */*");
                    
                    if (acceptAllCertificates && url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                    {
                        request.certificateHandler = new AcceptAllCertificatesHandler();
                    }
                    
                    float startTime = Time.realtimeSinceStartup;
                    yield return request.SendWebRequest();
                    float elapsed = Time.realtimeSinceStartup - startTime;
                    
                    Log($"[WebGL] Request completed in {elapsed:F1}s (attempt {attempt + 1})");
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string responseText = request.downloadHandler.text;
                        Log($"[WebGL] Response: {responseText}");
                        transcription = ParseTranscription(responseText);
                        success = true;
                    }
                    else
                    {
                        lastError = BuildErrorMessage(request);
                        Log($"[WebGL] Attempt {attempt + 1} failed: {lastError}");
                        
                        if (ShouldNotRetry(request))
                        {
                            Log("[WebGL] Error is non-retryable, stopping attempts");
                            break;
                        }
                    }
                }
            }
            
            CompleteRequest(success, transcription, lastError, callback);
        }
        
        private void CompleteRequest(bool success, string transcription, string lastError, Action<string> callback)
        {
            if (success)
            {
                callback?.Invoke(transcription);
                OnTranscriptionComplete?.Invoke(transcription);
            }
            else
            {
                lastError = lastError ?? "Unknown error occurred";
                Log($"All {maxRetries} attempts failed. Last error: {lastError}");
                OnError?.Invoke(lastError);
                callback?.Invoke(null);
            }
            
            IsProcessing = false;
        }
        
        private string BuildErrorMessage(UnityWebRequest request)
        {
            string error = $"STT Error: {request.error}";
            
            if (request.error != null)
            {
                if (request.error.Contains("SSL") || request.error.Contains("TLS") || 
                    request.error.Contains("certificate") || request.error.Contains("Certificate"))
                {
                    error += " [HTTPS/SSL issue]";
                }
                else if (request.error.Contains("Cannot connect") || request.error.Contains("connection"))
                {
                    error += " [Connection failed]";
                }
                else if (request.error.Contains("timeout") || request.error.Contains("Timeout"))
                {
                    error += " [Request timed out]";
                }
            }
            
            if (request.responseCode > 0)
            {
                error += $" (HTTP {request.responseCode})";
            }
            if (!string.IsNullOrEmpty(request.downloadHandler?.text))
            {
                error += $" - {request.downloadHandler.text}";
            }
            
            return error;
        }
        
        private bool ShouldNotRetry(UnityWebRequest request)
        {
            if (request.responseCode >= 400 && request.responseCode < 500 && request.responseCode != 429)
                return true;
            if (request.responseCode == 400 || request.responseCode == 415)
                return true;
            return false;
        }
        
        private string ParseTranscription(string jsonResponse)
        {
            try
            {
                var response = JsonUtility.FromJson<STTResponse>(jsonResponse);
                if (!string.IsNullOrEmpty(response.text))
                    return response.text.Trim();
                
                var altResponse = JsonUtility.FromJson<STTAltResponse>(jsonResponse);
                if (altResponse.result != null && !string.IsNullOrEmpty(altResponse.result.text))
                    return altResponse.result.text.Trim();
                
                return jsonResponse;
            }
            catch (Exception e)
            {
                Log($"Error parsing response: {e.Message}");
                return jsonResponse;
            }
        }
        
        /// <summary>
        /// Test connection to STT server
        /// </summary>
        public void TestConnection(Action<bool, string> callback)
        {
            StartCoroutine(TestConnectionCoroutine(callback));
        }
        
        private IEnumerator TestConnectionCoroutine(Action<bool, string> callback)
        {
            string url = $"{serverUrl}/healthz";
            Log($"Testing HTTPS connection to {url}");
            
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL uses UnityWebRequest
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                request.SetRequestHeader("Connection", "close");
                
                if (acceptAllCertificates && url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                {
                    request.certificateHandler = new AcceptAllCertificatesHandler();
                }
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    Log("Connection test successful");
                    callback?.Invoke(true, "STT server connected");
                }
                else
                {
                    string error = BuildErrorMessage(request);
                    Log($"Connection test failed: {error}");
                    callback?.Invoke(false, $"Cannot connect to STT server: {error}");
                }
            }
#else
            // Use HttpClient for other platforms
            EnsureHttpClient();
            
            var task = Task.Run(async () =>
            {
                try
                {
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                        return (true, "STT server connected", (string)null);
                    else
                        return (false, null, $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
                }
                catch (Exception e)
                {
                    return (false, null, e.Message);
                }
            });
            
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            var result = task.Result;
            if (result.Item1)
            {
                Log("Connection test successful");
                callback?.Invoke(true, result.Item2);
            }
            else
            {
                Log($"Connection test failed: {result.Item3}");
                callback?.Invoke(false, $"Cannot connect to STT server: {result.Item3}");
            }
#endif
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[STTClient] {message}");
            }
        }
        
        [Serializable]
        private class STTResponse
        {
            public string text;
        }
        
        [Serializable]
        private class STTAltResponse
        {
            public STTResultField result;
        }
        
        [Serializable]
        private class STTResultField
        {
            public string text;
        }
    }
    
    /// <summary>
    /// Certificate handler that accepts all certificates (WebGL fallback only).
    /// </summary>
    public class AcceptAllCertificatesHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }
}
