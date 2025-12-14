#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using VoiceControl.Adapters;
using VoiceControl.Core;

namespace VoiceControl.Editor
{
    /// <summary>
    /// Editor utility to set up voice control adapters in the scene.
    /// </summary>
    public static class VoiceControlSetup
    {
        [MenuItem("FAA/Voice Control/Add Symbology Color Voice Adapter")]
        public static void AddSymbologyColorVoiceAdapter()
        {
            // Find the VoiceControlSystem GameObject (has VoiceCommandRegistry)
            var registry = Object.FindObjectOfType<VoiceCommandRegistry>();
            
            if (registry == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "Could not find VoiceCommandRegistry in the scene. Make sure VoiceControlSystem exists.", 
                    "OK");
                return;
            }
            
            GameObject voiceControlSystem = registry.gameObject;
            
            // Check if adapter already exists
            var existingAdapter = voiceControlSystem.GetComponent<SymbologyColorVoiceAdapter>();
            if (existingAdapter != null)
            {
                EditorUtility.DisplayDialog("Info", 
                    "SymbologyColorVoiceAdapter already exists on VoiceControlSystem.", 
                    "OK");
                Selection.activeGameObject = voiceControlSystem;
                return;
            }
            
            // Add the adapter
            Undo.AddComponent<SymbologyColorVoiceAdapter>(voiceControlSystem);
            
            // Mark the scene as dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(voiceControlSystem.scene);
            
            // Select the GameObject
            Selection.activeGameObject = voiceControlSystem;
            
            EditorUtility.DisplayDialog("Success", 
                "SymbologyColorVoiceAdapter has been added to VoiceControlSystem.\n\n" +
                "The adapter will auto-discover the SymbologyColorManager at runtime.", 
                "OK");
            
            Debug.Log("[VoiceControlSetup] Added SymbologyColorVoiceAdapter to VoiceControlSystem");
        }
        
        [MenuItem("FAA/Voice Control/Add Symbology Color Voice Adapter", true)]
        public static bool ValidateAddSymbologyColorVoiceAdapter()
        {
            // Only enable in edit mode
            return !Application.isPlaying;
        }
    }
}
#endif
