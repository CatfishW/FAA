using System;
using System.Collections.Generic;
using UnityEngine;

namespace VisualUnderstanding.Core
{
    /// <summary>
    /// Analysis type identifier
    /// </summary>
    public enum VisualAnalysisType
    {
        SectionalChart,
        WeatherRadar,
        Generic
    }
    
    /// <summary>
    /// Priority level for briefing items
    /// </summary>
    public enum BriefingPriority
    {
        Info,
        Caution,
        Warning,
        Critical
    }
    
    /// <summary>
    /// Individual finding from analysis
    /// </summary>
    [Serializable]
    public class AnalysisFinding
    {
        public string title;
        public string description;
        public BriefingPriority priority;
        public string iconName;
        
        public AnalysisFinding() { }
        
        public AnalysisFinding(string title, string description, BriefingPriority priority = BriefingPriority.Info)
        {
            this.title = title;
            this.description = description;
            this.priority = priority;
        }
    }
    
    /// <summary>
    /// Result of a visual analysis operation
    /// </summary>
    [Serializable]
    public class VisionAnalysisResult
    {
        /// <summary>
        /// Type of analysis performed
        /// </summary>
        public VisualAnalysisType analysisType;
        
        /// <summary>
        /// Brief summary for the pilot
        /// </summary>
        public string summary;
        
        /// <summary>
        /// Detailed findings list
        /// </summary>
        public List<AnalysisFinding> findings = new List<AnalysisFinding>();
        
        /// <summary>
        /// Raw LLM response text
        /// </summary>
        public string rawResponse;
        
        /// <summary>
        /// Confidence score (0-1)
        /// </summary>
        public float confidence;
        
        /// <summary>
        /// When the analysis was performed
        /// </summary>
        public DateTime timestamp;
        
        /// <summary>
        /// Processing time in seconds
        /// </summary>
        public float processingTime;
        
        /// <summary>
        /// Error message if analysis failed
        /// </summary>
        public string error;
        
        /// <summary>
        /// Whether analysis was successful
        /// </summary>
        public bool IsSuccess => string.IsNullOrEmpty(error);
        
        /// <summary>
        /// Create a successful result
        /// </summary>
        public static VisionAnalysisResult Success(VisualAnalysisType type, string summary)
        {
            return new VisionAnalysisResult
            {
                analysisType = type,
                summary = summary,
                timestamp = DateTime.Now
            };
        }
        
        /// <summary>
        /// Create an error result
        /// </summary>
        public static VisionAnalysisResult Error(string errorMessage)
        {
            return new VisionAnalysisResult
            {
                error = errorMessage,
                timestamp = DateTime.Now
            };
        }
    }
}
