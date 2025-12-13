#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VisualUnderstanding.Core;
using VoiceControl.Network;

namespace VisualUnderstanding.Editor
{
    /// <summary>
    /// Custom editor for VisionSettings that adds auto-detect model button.
    /// </summary>
    [CustomEditor(typeof(VisionSettings))]
    public class VisionSettingsEditor : UnityEditor.Editor
    {
        private bool _isFetching;
        private string _fetchStatus = "";
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Server Configuration
            EditorGUILayout.LabelField("Server Configuration", EditorStyles.boldLabel);
            
            var serverUrlProp = serializedObject.FindProperty("serverUrl");
            EditorGUILayout.PropertyField(serverUrlProp);
            
            // Model Name with Detect button
            EditorGUILayout.BeginHorizontal();
            var modelNameProp = serializedObject.FindProperty("modelName");
            EditorGUILayout.PropertyField(modelNameProp);
            
            GUI.enabled = !_isFetching && !string.IsNullOrEmpty(serverUrlProp.stringValue);
            if (GUILayout.Button(_isFetching ? "..." : "Detect", GUILayout.Width(55)))
            {
                DetectModel(serverUrlProp.stringValue, modelNameProp);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            // Show status
            if (!string.IsNullOrEmpty(_fetchStatus))
            {
                EditorGUILayout.HelpBox(_fetchStatus, MessageType.Info);
            }
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty("apiKey"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("timeout"));
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("temperature"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxTokens"));
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Analysis Prompts", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sectionalChartPrompt"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weatherRadarPrompt"));
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("UI Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("typewriterSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeInDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeOutDuration"));
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("infoColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cautionColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("warningColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("criticalColor"));
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("verboseLogging"));
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DetectModel(string serverUrl, SerializedProperty modelNameProp)
        {
            _isFetching = true;
            _fetchStatus = "Detecting...";
            
            // Normalize URL - remove /v1 suffix
            string baseUrl = serverUrl.TrimEnd('/');
            if (baseUrl.EndsWith("/v1"))
            {
                baseUrl = baseUrl.Substring(0, baseUrl.Length - 3);
            }
            
            EditorApplication.delayCall += () =>
            {
                LLMClient.FetchAvailableModels(baseUrl, (models, error) =>
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        _fetchStatus = error;
                    }
                    else if (models != null && models.Count > 0)
                    {
                        modelNameProp.stringValue = models[0];
                        serializedObject.ApplyModifiedProperties();
                        _fetchStatus = $"Detected: {models[0]}";
                    }
                    else
                    {
                        _fetchStatus = "No models found";
                    }
                    _isFetching = false;
                    Repaint();
                });
            };
        }
    }
}
#endif
