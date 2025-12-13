#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VoiceControl.Network;

namespace VoiceControl.Editor
{
    /// <summary>
    /// Custom editor for LLMClient that adds auto-detect model button.
    /// </summary>
    [CustomEditor(typeof(LLMClient))]
    public class LLMClientEditor : UnityEditor.Editor
    {
        private bool _isFetching;
        private string _fetchStatus = "";
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Draw Server Configuration header
            EditorGUILayout.LabelField("Server Configuration", EditorStyles.boldLabel);
            
            // Server URL
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
            
            // Draw remaining properties
            EditorGUILayout.PropertyField(serializedObject.FindProperty("apiKey"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("timeout"));
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("temperature"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxTokens"));
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("verboseLogging"));
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DetectModel(string serverUrl, SerializedProperty modelNameProp)
        {
            _isFetching = true;
            _fetchStatus = "Detecting...";
            
            EditorApplication.delayCall += () =>
            {
                LLMClient.FetchAvailableModels(serverUrl, (models, error) =>
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
