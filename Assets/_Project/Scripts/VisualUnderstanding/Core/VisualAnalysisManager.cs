using System;
using System.Collections.Generic;
using UnityEngine;
using VisualUnderstanding.Core;
using VisualUnderstanding.Network;
using VisualUnderstanding.Analyzers;

namespace VisualUnderstanding.Core
{
    /// <summary>
    /// Central manager for visual analysis operations.
    /// Coordinates analyzers and provides events for UI updates.
    /// </summary>
    [AddComponentMenu("Visual Understanding/Core/Visual Analysis Manager")]
    public class VisualAnalysisManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private VisionSettings settings;
        
        [Header("Dependencies")]
        [SerializeField] private VisionLLMClient visionClient;
        [SerializeField] private SectionalChartAnalyzer sectionalAnalyzer;
        [SerializeField] private WeatherRadarAnalyzer weatherRadarAnalyzer;
        
        [Header("Cache Settings")]
        [SerializeField] private bool enableCaching = true;
        [SerializeField] private float cacheExpirationSeconds = 60f;
        [SerializeField] private int maxCachedResults = 5;
        
        /// <summary>
        /// Event fired when analysis starts
        /// </summary>
        public event Action<VisualAnalysisType> OnAnalysisStarted;
        
        /// <summary>
        /// Event fired when analysis completes
        /// </summary>
        public event Action<VisionAnalysisResult> OnAnalysisComplete;
        
        /// <summary>
        /// Event fired on analysis error
        /// </summary>
        public event Action<string> OnAnalysisError;
        
        public VisionSettings Settings => settings;
        public bool IsProcessing => _isProcessing;
        
        private bool _isProcessing;
        private List<CachedResult> _resultCache = new List<CachedResult>();
        
        private class CachedResult
        {
            public VisualAnalysisType type;
            public VisionAnalysisResult result;
            public DateTime timestamp;
            public int imageHash;
        }
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            InitializeComponents();
        }
        
        private void OnEnable()
        {
            if (visionClient != null)
            {
                visionClient.OnAnalysisComplete += HandleAnalysisComplete;
                visionClient.OnError += HandleError;
            }
        }
        
        private void OnDisable()
        {
            if (visionClient != null)
            {
                visionClient.OnAnalysisComplete -= HandleAnalysisComplete;
                visionClient.OnError -= HandleError;
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Set the settings asset and propagate to children
        /// </summary>
        public void SetSettings(VisionSettings newSettings)
        {
            settings = newSettings;
            
            if (visionClient != null)
                visionClient.SetSettings(settings);
            if (sectionalAnalyzer != null)
                sectionalAnalyzer.SetSettings(settings);
            if (weatherRadarAnalyzer != null)
                weatherRadarAnalyzer.SetSettings(settings);
        }
        
        /// <summary>
        /// Analyze a sectional chart image
        /// </summary>
        public void AnalyzeSectionalChart(Texture2D image, Action<VisionAnalysisResult> callback = null)
        {
            AnalyzeWithType(image, VisualAnalysisType.SectionalChart, callback);
        }
        
        /// <summary>
        /// Analyze a weather radar image
        /// </summary>
        public void AnalyzeWeatherRadar(Texture2D image, Action<VisionAnalysisResult> callback = null)
        {
            AnalyzeWithType(image, VisualAnalysisType.WeatherRadar, callback);
        }
        
        /// <summary>
        /// Analyze an image with specified type
        /// </summary>
        /// <param name="forceRefresh">If true, bypass cache and perform fresh analysis</param>
        public void AnalyzeWithType(Texture2D image, VisualAnalysisType type, Action<VisionAnalysisResult> callback = null, bool forceRefresh = false)
        {
            Debug.Log($"[VisualAnalysisManager] AnalyzeWithType called for {type}, forceRefresh={forceRefresh}");
            
            // If already processing, cancel the current request and start the new one
            if (_isProcessing)
            {
                Debug.Log("[VisualAnalysisManager] Canceling current analysis to start new one");
                CancelCurrentAnalysis();
            }
            
            if (image == null)
            {
                Debug.LogError("[VisualAnalysisManager] Image is null");
                callback?.Invoke(VisionAnalysisResult.Error("Image is null"));
                OnAnalysisError?.Invoke("Image is null");
                return;
            }
            
            Debug.Log($"[VisualAnalysisManager] Image size: {image.width}x{image.height}");
            
            // Check cache (skip if forceRefresh is true)
            if (enableCaching && !forceRefresh)
            {
                int imageHash = ComputeImageHash(image);
                var cached = GetCachedResult(type, imageHash);
                if (cached != null)
                {
                    Debug.Log("[VisualAnalysisManager] Returning cached result");
                    callback?.Invoke(cached);
                    OnAnalysisComplete?.Invoke(cached);
                    return;
                }
            }
            
            _isProcessing = true;
            Debug.Log($"[VisualAnalysisManager] Firing OnAnalysisStarted for {type}");
            OnAnalysisStarted?.Invoke(type);
            
            IVisualAnalyzer analyzer = GetAnalyzerForType(type);
            if (analyzer == null)
            {
                _isProcessing = false;
                var error = VisionAnalysisResult.Error($"No analyzer available for type: {type}");
                callback?.Invoke(error);
                OnAnalysisError?.Invoke(error.error);
                return;
            }
            
            // Store image hash for caching
            int hash = ComputeImageHash(image);
            
            analyzer.Analyze(image, result =>
            {
                _isProcessing = false;
                
                if (result.IsSuccess && enableCaching)
                {
                    CacheResult(type, result, hash);
                }
                
                callback?.Invoke(result);
                OnAnalysisComplete?.Invoke(result);
            });
        }
        
        /// <summary>
        /// Analyze an image from file path
        /// </summary>
        public void AnalyzeFromFile(string filePath, VisualAnalysisType type, Action<VisionAnalysisResult> callback = null)
        {
            if (visionClient == null)
            {
                callback?.Invoke(VisionAnalysisResult.Error("Vision client not available"));
                return;
            }
            
            _isProcessing = true;
            OnAnalysisStarted?.Invoke(type);
            
            visionClient.AnalyzeImageFromFile(filePath, type, result =>
            {
                _isProcessing = false;
                callback?.Invoke(result);
                OnAnalysisComplete?.Invoke(result);
            });
        }
        
        /// <summary>
        /// Clear the result cache
        /// </summary>
        public void ClearCache()
        {
            _resultCache.Clear();
            Log("Cache cleared");
        }
        
        /// <summary>
        /// Cancel the current analysis in progress
        /// </summary>
        public void CancelCurrentAnalysis()
        {
            if (!_isProcessing) return;
            
            Debug.Log("[VisualAnalysisManager] Canceling current analysis");
            
            // Cancel analyzers
            if (sectionalAnalyzer != null)
                sectionalAnalyzer.Cancel();
            if (weatherRadarAnalyzer != null)
                weatherRadarAnalyzer.Cancel();
                
            // Reset processing state
            _isProcessing = false;
            
            // Fire error event so buttons reset their state
            OnAnalysisError?.Invoke("Analysis cancelled");
        }
        
        /// <summary>
        /// Get the last analysis result
        /// </summary>
        public VisionAnalysisResult GetLastResult(VisualAnalysisType type)
        {
            for (int i = _resultCache.Count - 1; i >= 0; i--)
            {
                if (_resultCache[i].type == type)
                {
                    return _resultCache[i].result;
                }
            }
            return null;
        }
        
        #endregion
        
        #region Private Methods
        
        private void InitializeComponents()
        {
            Debug.Log("[VisualAnalysisManager] Initializing components...");
            
            if (visionClient == null)
            {
                visionClient = GetComponentInChildren<VisionLLMClient>();
                if (visionClient == null)
                {
                    visionClient = GetComponent<VisionLLMClient>();
                }
                if (visionClient == null)
                {
                    visionClient = FindObjectOfType<VisionLLMClient>();
                }
            }
            Debug.Log($"[VisualAnalysisManager] VisionClient: {(visionClient != null ? "Found" : "NOT FOUND")}");
            
            if (sectionalAnalyzer == null)
            {
                sectionalAnalyzer = GetComponentInChildren<SectionalChartAnalyzer>();
                if (sectionalAnalyzer == null)
                {
                    sectionalAnalyzer = GetComponent<SectionalChartAnalyzer>();
                }
            }
            Debug.Log($"[VisualAnalysisManager] SectionalAnalyzer: {(sectionalAnalyzer != null ? "Found" : "NOT FOUND")}");
            
            if (weatherRadarAnalyzer == null)
            {
                weatherRadarAnalyzer = GetComponentInChildren<WeatherRadarAnalyzer>();
                if (weatherRadarAnalyzer == null)
                {
                    weatherRadarAnalyzer = GetComponent<WeatherRadarAnalyzer>();
                }
            }
            Debug.Log($"[VisualAnalysisManager] WeatherRadarAnalyzer: {(weatherRadarAnalyzer != null ? "Found" : "NOT FOUND")}");
            
            // Propagate settings and vision client to analyzers
            if (settings != null)
            {
                SetSettings(settings);
            }
            
            // Ensure analyzers have the vision client
            if (visionClient != null)
            {
                if (sectionalAnalyzer != null)
                    sectionalAnalyzer.SetVisionClient(visionClient);
                if (weatherRadarAnalyzer != null)
                    weatherRadarAnalyzer.SetVisionClient(visionClient);
            }
            
            Debug.Log("[VisualAnalysisManager] Initialization complete");
        }
        
        private IVisualAnalyzer GetAnalyzerForType(VisualAnalysisType type)
        {
            IVisualAnalyzer analyzer = type switch
            {
                VisualAnalysisType.SectionalChart => sectionalAnalyzer,
                VisualAnalysisType.WeatherRadar => weatherRadarAnalyzer,
                _ => null
            };
            
            if (analyzer != null)
            {
                Debug.Log($"[VisualAnalysisManager] Using analyzer for {type}, IsReady: {analyzer.IsReady}");
                
                // Double-check dependencies are set
                if (!analyzer.IsReady && visionClient != null && settings != null)
                {
                    Debug.Log($"[VisualAnalysisManager] Analyzer not ready, re-linking dependencies...");
                    var baseAnalyzer = analyzer as BaseVisualAnalyzer;
                    if (baseAnalyzer != null)
                    {
                        baseAnalyzer.SetVisionClient(visionClient);
                        baseAnalyzer.SetSettings(settings);
                        Debug.Log($"[VisualAnalysisManager] Re-linked, IsReady now: {analyzer.IsReady}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[VisualAnalysisManager] No analyzer found for type: {type}");
            }
            
            return analyzer;
        }
        
        private int ComputeImageHash(Texture2D image)
        {
            // Simple hash based on dimensions and a sample of pixels
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + image.width;
                hash = hash * 31 + image.height;
                
                // Sample a few pixels for faster hashing
                if (image.isReadable)
                {
                    var pixels = image.GetPixels32();
                    int step = Mathf.Max(1, pixels.Length / 10);
                    for (int i = 0; i < pixels.Length; i += step)
                    {
                        hash = hash * 31 + pixels[i].GetHashCode();
                    }
                }
                
                return hash;
            }
        }
        
        private VisionAnalysisResult GetCachedResult(VisualAnalysisType type, int imageHash)
        {
            CleanExpiredCache();
            
            foreach (var cached in _resultCache)
            {
                if (cached.type == type && cached.imageHash == imageHash)
                {
                    return cached.result;
                }
            }
            
            return null;
        }
        
        private void CacheResult(VisualAnalysisType type, VisionAnalysisResult result, int imageHash)
        {
            // Remove excess cached items
            while (_resultCache.Count >= maxCachedResults)
            {
                _resultCache.RemoveAt(0);
            }
            
            _resultCache.Add(new CachedResult
            {
                type = type,
                result = result,
                timestamp = DateTime.Now,
                imageHash = imageHash
            });
        }
        
        private void CleanExpiredCache()
        {
            DateTime now = DateTime.Now;
            _resultCache.RemoveAll(c => (now - c.timestamp).TotalSeconds > cacheExpirationSeconds);
        }
        
        private void HandleAnalysisComplete(VisionAnalysisResult result)
        {
            // Additional handling if needed
        }
        
        private void HandleError(string error)
        {
            _isProcessing = false;
            OnAnalysisError?.Invoke(error);
        }
        
        private void Log(string message)
        {
            if (settings == null || settings.verboseLogging)
            {
                Debug.Log($"[VisualAnalysisManager] {message}");
            }
        }
        
        #endregion
    }
}
