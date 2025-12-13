using UnityEngine;

namespace VoiceControl.Manager
{
    /// <summary>
    /// ScriptableObject for voice control configuration.
    /// Create via Assets > Create > Voice Control > Settings.
    /// </summary>
    [CreateAssetMenu(fileName = "VoiceControlSettings", menuName = "Voice Control/Settings", order = 1)]
    public class VoiceControlSettings : ScriptableObject
    {
        [Header("STT Server")]
        [Tooltip("URL of the Speech-to-Text server")]
        public string sttServerUrl = "http://localhost:25567";
        
        [Tooltip("Request timeout in seconds")]
        public float sttTimeout = 180f;
        
        [Header("LLM Server")]
        [Tooltip("URL of the LLM server (OpenAI-compatible)")]
        public string llmServerUrl = "http://localhost:25565";
        
        [Tooltip("Model name for the LLM")]
        public string llmModelName = "qwen3-30b-a3b-instruct";
        
        [Tooltip("API key for authentication (optional)")]
        public string llmApiKey = "";
        
        [Tooltip("Request timeout in seconds")]
        public float llmTimeout = 120f;
        
        [Tooltip("Temperature for LLM generation (0-1)")]
        [Range(0f, 1f)]
        public float llmTemperature = 0.3f;
        
        [Header("Audio Settings")]
        [Tooltip("Microphone sample rate in Hz")]
        public int sampleRate = 16000;
        
        [Tooltip("Maximum recording duration in seconds")]
        public float maxRecordingDuration = 30f;
        
        [Tooltip("Minimum recording duration in seconds")]
        public float minRecordingDuration = 0.5f;
        
        [Tooltip("Silence threshold for automatic stopping (0-1)")]
        [Range(0f, 1f)]
        public float silenceThreshold = 0.02f;
        
        [Tooltip("Duration of silence before auto-stop in seconds")]
        public float silenceDuration = 1.5f;
        
        [Header("UI Colors")]
        [Tooltip("Primary accent color for voice UI")]
        public Color primaryColor = new Color(0.2f, 0.6f, 1f, 1f);
        
        [Tooltip("Secondary color for backgrounds")]
        public Color secondaryColor = new Color(0.1f, 0.12f, 0.15f, 0.95f);
        
        [Tooltip("Recording indicator color")]
        public Color recordingColor = new Color(1f, 0.3f, 0.3f, 1f);
        
        [Tooltip("Success color for command confirmation")]
        public Color successColor = new Color(0.3f, 0.9f, 0.4f, 1f);
        
        [Tooltip("Error color for failures")]
        public Color errorColor = new Color(1f, 0.3f, 0.3f, 1f);
        
        [Header("UI Animation")]
        [Tooltip("Animation duration in seconds")]
        public float animationDuration = 0.25f;
        
        [Tooltip("Feedback display duration in seconds")]
        public float feedbackDisplayDuration = 3f;
        
        [Header("UI Layout")]
        [Tooltip("Position of the voice control UI")]
        public VoiceUIPosition uiPosition = VoiceUIPosition.BottomRight;
        
        [Tooltip("Offset from screen edge")]
        public Vector2 uiOffset = new Vector2(20f, 20f);
        
        [Tooltip("Size of the voice panel")]
        public Vector2 panelSize = new Vector2(320f, 180f);
        
        [Header("Debug")]
        [Tooltip("Enable verbose logging")]
        public bool verboseLogging = true;
        
        /// <summary>
        /// Create default settings
        /// </summary>
        public static VoiceControlSettings CreateDefault()
        {
            var settings = CreateInstance<VoiceControlSettings>();
            return settings;
        }
    }
    
    /// <summary>
    /// Position options for the voice control UI
    /// </summary>
    public enum VoiceUIPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }
}
