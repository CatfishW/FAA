using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VoiceControl.Core;
using VisualUnderstanding.Core;
using VisualUnderstanding.UI;
using VisualUnderstanding.Integration;

namespace VoiceControl.Adapters
{
    /// <summary>
    /// Voice command adapter for Vision Analysis system.
    /// Enables voice-triggered sectional chart and weather radar briefings.
    /// </summary>
    [AddComponentMenu("Voice Control/Vision Voice Adapter")]
    public class VisionVoiceAdapter : MonoBehaviour, IVoiceCommandTarget
    {
        [Header("Target Components")]
        [SerializeField] private VisualAnalysisManager analysisManager;
        [SerializeField] private RadarImageCapture radarCapture;
        [SerializeField] private SectionalChartCapture chartCapture;
        [SerializeField] private VisualBriefingPanel briefingPanel;
        
        [Header("Settings")]
        [SerializeField] private bool autoFindComponents = true;
        [SerializeField] private bool verboseLogging = true;
        
        public string TargetId => "visionbriefing";
        public string DisplayName => "Vision Briefing";
        
        private VoiceCommandInfo[] _commands;
        
        private void Awake()
        {
            if (autoFindComponents)
            {
                AutoFindComponents();
            }
        }
        
        private void Start()
        {
            if (VoiceCommandRegistry.Instance != null)
            {
                VoiceCommandRegistry.Instance.RegisterTarget(this);
            }
        }
        
        private void OnDestroy()
        {
            if (VoiceCommandRegistry.Instance != null)
            {
                VoiceCommandRegistry.Instance.UnregisterTarget(TargetId);
            }
        }
        
        private void AutoFindComponents()
        {
            if (analysisManager == null)
            {
                analysisManager = FindObjectOfType<VisualAnalysisManager>();
            }
            if (radarCapture == null)
            {
                radarCapture = FindObjectOfType<RadarImageCapture>();
            }
            if (chartCapture == null)
            {
                chartCapture = FindObjectOfType<SectionalChartCapture>();
            }
            if (briefingPanel == null)
            {
                briefingPanel = FindObjectOfType<VisualBriefingPanel>();
            }
            
            Log($"Found components - Manager: {analysisManager != null}, Radar: {radarCapture != null}, Chart: {chartCapture != null}, Panel: {briefingPanel != null}");
        }
        
        public VoiceCommandInfo[] GetAvailableCommands()
        {
            if (_commands != null)
                return _commands;
            
            _commands = new VoiceCommandInfo[]
            {
                new VoiceCommandInfo(
                    "analyze_sectional",
                    "Analyze the sectional chart and provide a pilot briefing"
                ),
                new VoiceCommandInfo(
                    "analyze_weather",
                    "Analyze the weather radar and provide a weather briefing"
                ),
                new VoiceCommandInfo(
                    "sectional_briefing",
                    "Get a visual briefing of the current sectional chart display"
                ),
                new VoiceCommandInfo(
                    "weather_briefing",
                    "Get a visual briefing of the current weather radar display"
                ),
                new VoiceCommandInfo(
                    "show_briefing",
                    "Show the vision briefing panel"
                ),
                new VoiceCommandInfo(
                    "hide_briefing",
                    "Hide the vision briefing panel"
                ),
                new VoiceCommandInfo(
                    "toggle_briefing",
                    "Toggle the visibility of the vision briefing panel"
                )
            };
            
            return _commands;
        }
        
        public bool ExecuteCommand(string commandName, Dictionary<string, object> parameters)
        {
            Log($"Executing command: {commandName}");
            
            switch (commandName.ToLower())
            {
                case "analyze_sectional":
                case "sectional_briefing":
                    return TriggerSectionalAnalysis();
                    
                case "analyze_weather":
                case "weather_briefing":
                    return TriggerWeatherAnalysis();
                    
                case "show_briefing":
                    if (briefingPanel != null)
                    {
                        briefingPanel.Show();
                        return true;
                    }
                    break;
                    
                case "hide_briefing":
                    if (briefingPanel != null)
                    {
                        briefingPanel.Hide();
                        return true;
                    }
                    break;
                    
                case "toggle_briefing":
                    if (briefingPanel != null)
                    {
                        briefingPanel.Toggle();
                        return true;
                    }
                    break;
                    
                default:
                    Log($"Unknown command: {commandName}");
                    break;
            }
            
            return false;
        }
        
        private bool TriggerSectionalAnalysis()
        {
            if (analysisManager == null)
            {
                Log("Analysis manager not found");
                return false;
            }
            
            // Capture sectional chart image
            Texture2D image = null;
            
            if (chartCapture != null)
            {
                image = chartCapture.Capture();
                Log("Captured sectional chart from chartCapture");
            }
            else
            {
                // Fallback to screen capture
                StartCoroutine(CaptureAndAnalyze(VisualAnalysisType.SectionalChart));
                return true;
            }
            
            if (image != null)
            {
                // Show image preview
                if (briefingPanel != null)
                {
                    briefingPanel.ShowCapturedImage(image);
                }
                
                // Trigger analysis with forceRefresh to bypass cache
                analysisManager.AnalyzeWithType(image, VisualAnalysisType.SectionalChart, null, forceRefresh: true);
                Log("Triggered sectional chart analysis");
                return true;
            }
            
            Log("Failed to capture sectional chart image");
            return false;
        }
        
        private bool TriggerWeatherAnalysis()
        {
            if (analysisManager == null)
            {
                Log("Analysis manager not found");
                return false;
            }
            
            // Capture weather radar image
            Texture2D image = null;
            
            if (radarCapture != null)
            {
                image = radarCapture.Capture();
                Log("Captured weather radar from radarCapture");
            }
            else
            {
                // Fallback to screen capture
                StartCoroutine(CaptureAndAnalyze(VisualAnalysisType.WeatherRadar));
                return true;
            }
            
            if (image != null)
            {
                // Show image preview
                if (briefingPanel != null)
                {
                    briefingPanel.ShowCapturedImage(image);
                }
                
                // Trigger analysis with forceRefresh to bypass cache
                analysisManager.AnalyzeWithType(image, VisualAnalysisType.WeatherRadar, null, forceRefresh: true);
                Log("Triggered weather radar analysis");
                return true;
            }
            
            Log("Failed to capture weather radar image");
            return false;
        }
        
        private IEnumerator CaptureAndAnalyze(VisualAnalysisType type)
        {
            yield return new WaitForEndOfFrame();
            
            // Screen capture fallback
            Texture2D image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            image.Apply();
            
            Log($"Captured screen for {type}: {image.width}x{image.height}");
            
            // Show image preview
            if (briefingPanel != null)
            {
                briefingPanel.ShowCapturedImage(image);
            }
            
            // Trigger analysis
            analysisManager.AnalyzeWithType(image, type);
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[VisionVoiceAdapter] {message}");
            }
        }
    }
}
