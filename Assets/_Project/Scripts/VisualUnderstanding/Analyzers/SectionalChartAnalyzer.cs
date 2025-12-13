using UnityEngine;
using VisualUnderstanding.Core;

namespace VisualUnderstanding.Analyzers
{
    /// <summary>
    /// Specialized analyzer for FAA sectional charts.
    /// Provides aviation-specific analysis including airspace, airports, and obstacles.
    /// </summary>
    [AddComponentMenu("Visual Understanding/Analyzers/Sectional Chart Analyzer")]
    public class SectionalChartAnalyzer : BaseVisualAnalyzer
    {
        public override VisualAnalysisType AnalysisType => VisualAnalysisType.SectionalChart;
        
        [Header("Sectional Chart Settings")]
        [Tooltip("Additional context about current position")]
        [SerializeField] private string currentAirport = "";
        
        [Tooltip("Current altitude for relevant airspace analysis")]
        [SerializeField] private float currentAltitudeFt = 3000f;
        
        /// <summary>
        /// Set current position context
        /// </summary>
        public void SetContext(string airport, float altitudeFt)
        {
            currentAirport = airport;
            currentAltitudeFt = altitudeFt;
        }
        
        protected override VisionAnalysisResult PostProcessResult(VisionAnalysisResult result)
        {
            if (!result.IsSuccess) return result;
            
            // Add aviation-specific icon assignments
            foreach (var finding in result.findings)
            {
                string lower = finding.title.ToLower();
                
                if (lower.Contains("class b") || lower.Contains("class c") || lower.Contains("class d"))
                {
                    finding.iconName = "airspace";
                }
                else if (lower.Contains("airport") || lower.Contains("runway"))
                {
                    finding.iconName = "airport";
                }
                else if (lower.Contains("terrain") || lower.Contains("obstacle") || lower.Contains("tower"))
                {
                    finding.iconName = "obstacle";
                }
                else if (lower.Contains("tfr") || lower.Contains("notam") || lower.Contains("restricted"))
                {
                    finding.iconName = "warning";
                    finding.priority = BriefingPriority.Warning;
                }
                else if (lower.Contains("frequency") || lower.Contains("freq") || lower.Contains("mhz"))
                {
                    finding.iconName = "radio";
                }
            }
            
            return result;
        }
    }
}
