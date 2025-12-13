using UnityEngine;

namespace VisualUnderstanding.Core
{
    /// <summary>
    /// ScriptableObject configuration for Visual Understanding System.
    /// Create via Assets > Create > Visual Understanding > Vision Settings
    /// </summary>
    [CreateAssetMenu(fileName = "VisionSettings", menuName = "Visual Understanding/Vision Settings")]
    public class VisionSettings : ScriptableObject
    {
        [Header("Server Configuration")]
        [Tooltip("Vision LLM server URL (OpenAI-compatible)")]
        public string serverUrl = "https://game.agaii.org/llm/v1";
        
        [Tooltip("Vision model name")]
        public string modelName = "Qwen/Qwen3-VL-4B-Instruct-FP8";
        
        [Tooltip("API key (leave empty if not required)")]
        public string apiKey = "empty";
        
        [Tooltip("Request timeout in seconds")]
        [Range(30, 300)]
        public float timeout = 120f;
        
        [Header("Generation Settings")]
        [Tooltip("Temperature for response generation")]
        [Range(0f, 1f)]
        public float temperature = 0.3f;
        
        [Tooltip("Maximum tokens in response")]
        [Range(50, 500)]
        public int maxTokens = 300;
        
        [Header("Analysis Prompts")]
        [TextArea(8, 12)]
        [Tooltip("Prompt for sectional chart analysis")]
        public string sectionalChartPrompt = 
            // "You are a pilot briefer analyzing an FAA sectional chart image.\n\n" +
            // "REPORT ALL VISIBLE ELEMENTS (terse bullet points, no prose):\n" +
            // "- AIRSPACE: Class B/C/D, MOAs, Restricted (altitudes if shown)\n" +
            // "- AIRPORTS: Name, (Pvt)/public, runway heading\n" +
            // "- OBSTACLES: Type + MSL height\n" +
            // "- NAVAIDS: VORs, NDBs, intersections\n" +
            // "- TERRAIN: MEF values, high terrain\n\n" +
            // "RULES: Report ONLY visible data. No invented codes/freqs. Use shorthand.\n" +
            // "End: 'Verify against official charts'";
            "You are an aviation briefing assistant. Analyze this FAA sectional chart and provide a concise pilot briefing.\n" +
            "Include: airspace classes, airports, frequencies, terrain features, obstacles, and NOTAMs if visible.\n" +
            "Be brief and use aviation terminology. Format as short bullet points.";
        
        [TextArea(3, 6)]
        [Tooltip("Prompt for weather radar analysis")]
        public string weatherRadarPrompt = 
            "You are a pilot briefer. Analyze this weather radar in under 100 words.\n" +
            "List: precipitation type/intensity, storm cells, flight hazards.\n" +
            "Terse format, aviation shorthand. No intro/outro. Max 150 tokens.";
        
        [Header("UI Settings")]
        [Tooltip("How long to display the briefing panel")]
        [Range(5f, 60f)]
        public float displayDuration = 15f;
        
        [Tooltip("Typewriter effect speed (chars per second)")]
        [Range(20f, 200f)]
        public float typewriterSpeed = 80f;
        
        [Tooltip("Panel fade in duration")]
        [Range(0.1f, 1f)]
        public float fadeInDuration = 0.3f;
        
        [Tooltip("Panel fade out duration")]
        [Range(0.1f, 1f)]
        public float fadeOutDuration = 0.5f;
        
        [Header("Colors")]
        public Color infoColor = new Color(0.4f, 0.8f, 1f, 1f);
        public Color cautionColor = new Color(1f, 0.9f, 0.3f, 1f);
        public Color warningColor = new Color(1f, 0.6f, 0.2f, 1f);
        public Color criticalColor = new Color(1f, 0.3f, 0.3f, 1f);
        
        [Header("Debug")]
        public bool verboseLogging = true;
        
        /// <summary>
        /// Get color for priority level
        /// </summary>
        public Color GetPriorityColor(BriefingPriority priority)
        {
            return priority switch
            {
                BriefingPriority.Info => infoColor,
                BriefingPriority.Caution => cautionColor,
                BriefingPriority.Warning => warningColor,
                BriefingPriority.Critical => criticalColor,
                _ => infoColor
            };
        }
        
        /// <summary>
        /// Get prompt for analysis type
        /// </summary>
        public string GetPromptForType(VisualAnalysisType type)
        {
            return type switch
            {
                VisualAnalysisType.SectionalChart => sectionalChartPrompt,
                VisualAnalysisType.WeatherRadar => weatherRadarPrompt,
                _ => "Describe this image concisely for a pilot."
            };
        }
    }
}
