using System;
using UnityEngine;
using VisualUnderstanding.Core;
using VisualUnderstanding.Network;

namespace VisualUnderstanding.Analyzers
{
    /// <summary>
    /// Base class for visual analyzers using Vision LLM
    /// </summary>
    public abstract class BaseVisualAnalyzer : MonoBehaviour, IVisualAnalyzer
    {
        [Header("Dependencies")]
        [SerializeField] protected VisionLLMClient visionClient;
        [SerializeField] protected VisionSettings settings;
        
        public abstract VisualAnalysisType AnalysisType { get; }
        
        public bool IsReady => visionClient != null && settings != null;
        
        public bool IsProcessing => visionClient != null && visionClient.IsProcessing;
        
        private Action<VisionAnalysisResult> _pendingCallback;
        
        protected virtual void Awake()
        {
            if (visionClient == null)
            {
                visionClient = FindObjectOfType<VisionLLMClient>();
            }
        }
        
        /// <summary>
        /// Set the vision client
        /// </summary>
        public void SetVisionClient(VisionLLMClient client)
        {
            visionClient = client;
        }
        
        /// <summary>
        /// Set the settings
        /// </summary>
        public void SetSettings(VisionSettings newSettings)
        {
            settings = newSettings;
            if (visionClient != null)
            {
                visionClient.SetSettings(settings);
            }
        }
        
        public virtual void Analyze(Texture2D image, Action<VisionAnalysisResult> callback)
        {
            if (!IsReady)
            {
                callback?.Invoke(VisionAnalysisResult.Error("Analyzer not ready - missing client or settings"));
                return;
            }
            
            if (IsProcessing)
            {
                callback?.Invoke(VisionAnalysisResult.Error("Analysis already in progress"));
                return;
            }
            
            _pendingCallback = callback;
            visionClient.AnalyzeImage(image, AnalysisType, OnAnalysisComplete);
        }
        
        protected virtual void OnAnalysisComplete(VisionAnalysisResult result)
        {
            // Allow subclasses to post-process results
            result = PostProcessResult(result);
            
            _pendingCallback?.Invoke(result);
            _pendingCallback = null;
        }
        
        /// <summary>
        /// Override to post-process analysis results
        /// </summary>
        protected virtual VisionAnalysisResult PostProcessResult(VisionAnalysisResult result)
        {
            return result;
        }
        
        public virtual void Cancel()
        {
            _pendingCallback = null;
            
            // Also cancel the vision client to stop HTTP request
            if (visionClient != null)
            {
                visionClient.Cancel();
            }
        }
    }
}
