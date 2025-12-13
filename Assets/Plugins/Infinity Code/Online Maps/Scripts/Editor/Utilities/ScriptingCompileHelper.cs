/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [InitializeOnLoad]
    public static class ScriptingCompileHelper
    {
        private static bool waitingForCompile;
        
        static ScriptingCompileHelper()
        {
            Map.OnStart += OnMapStarted;
        }

        private static void CheckScriptCompiling()
        {
            if (!EditorApplication.isCompiling) return;
            
            Debug.Log("Online Maps stop playing to compile scripts.");
            EditorApplication.update -= CheckScriptCompiling;
            EditorApplication.isPlaying = false;
        }

        private static void OnMapStarted(Map map)
        {
            if (!map.stopPlayingWhenScriptsCompile) return;
            if (waitingForCompile) return;
            
            EditorApplication.update += CheckScriptCompiling;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            waitingForCompile = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode) return;
            
            EditorApplication.update -= CheckScriptCompiling;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            waitingForCompile = false;
        }
    }
}