using UnityEngine;
using VisualUnderstanding.Core;

namespace VisualUnderstanding.Analyzers
{
    /// <summary>
    /// Specialized analyzer for weather radar tiles.
    /// Provides pilot-relevant weather hazard analysis.
    /// </summary>
    [AddComponentMenu("Visual Understanding/Analyzers/Weather Radar Analyzer")]
    public class WeatherRadarAnalyzer : BaseVisualAnalyzer
    {
        public override VisualAnalysisType AnalysisType => VisualAnalysisType.WeatherRadar;
        
        [Header("Weather Radar Settings")]
        [Tooltip("Current tilt angle for context")]
        [SerializeField] private float currentTiltDegrees = 0f;
        
        [Tooltip("Current range in NM")]
        [SerializeField] private float currentRangeNM = 25f;
        
        /// <summary>
        /// Set current radar settings for context
        /// </summary>
        public void SetRadarContext(float tiltDegrees, float rangeNM)
        {
            currentTiltDegrees = tiltDegrees;
            currentRangeNM = rangeNM;
        }
        
        protected override VisionAnalysisResult PostProcessResult(VisionAnalysisResult result)
        {
            if (!result.IsSuccess) return result;
            
            // Add weather-specific icon and priority assignments
            foreach (var finding in result.findings)
            {
                string lower = finding.title.ToLower();
                
                if (lower.Contains("heavy") || lower.Contains("intense"))
                {
                    finding.iconName = "heavy_rain";
                    finding.priority = BriefingPriority.Warning;
                }
                else if (lower.Contains("moderate"))
                {
                    finding.iconName = "moderate_rain";
                    finding.priority = BriefingPriority.Caution;
                }
                else if (lower.Contains("light"))
                {
                    finding.iconName = "light_rain";
                    finding.priority = BriefingPriority.Info;
                }
                else if (lower.Contains("thunderstorm") || lower.Contains("cell") || lower.Contains("cb"))
                {
                    finding.iconName = "thunderstorm";
                    finding.priority = BriefingPriority.Critical;
                }
                else if (lower.Contains("clear") || lower.Contains("no precipitation"))
                {
                    finding.iconName = "clear";
                    finding.priority = BriefingPriority.Info;
                }
                else if (lower.Contains("movement") || lower.Contains("moving"))
                {
                    finding.iconName = "wind";
                }
            }
            
            return result;
        }
    }
}
