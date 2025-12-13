using System;
using UnityEngine;
using VisualUnderstanding.Core;

namespace VisualUnderstanding.Analyzers
{
    /// <summary>
    /// Interface for visual analyzers
    /// </summary>
    public interface IVisualAnalyzer
    {
        /// <summary>
        /// The type of analysis this analyzer performs
        /// </summary>
        VisualAnalysisType AnalysisType { get; }
        
        /// <summary>
        /// Whether the analyzer is ready to process requests
        /// </summary>
        bool IsReady { get; }
        
        /// <summary>
        /// Whether an analysis is currently in progress
        /// </summary>
        bool IsProcessing { get; }
        
        /// <summary>
        /// Analyze an image
        /// </summary>
        /// <param name="image">Image to analyze</param>
        /// <param name="callback">Callback with result</param>
        void Analyze(Texture2D image, Action<VisionAnalysisResult> callback);
        
        /// <summary>
        /// Cancel current analysis if any
        /// </summary>
        void Cancel();
    }
}
