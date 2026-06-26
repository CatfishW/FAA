#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FAA.Editor
{
    [InitializeOnLoad]
    public static class FaaMainSceneRunner
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string RequestFilePath = "Assets/_Project/Verification/FaaMainSceneRunner.request";

        static FaaMainSceneRunner()
        {
            EditorApplication.delayCall += RunIfRequested;
        }

        [MenuItem("FAA/Open Main Scene And Play")]
        public static void RequestOpenAndPlay()
        {
            EnsureRequestFile();
            EditorApplication.delayCall += RunIfRequested;
            Debug.Log("[FaaMainSceneRunner] Requested Main scene play mode.");
        }

        private static void RunIfRequested()
        {
            if (!File.Exists(RequestFilePath))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunIfRequested;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += RunIfRequested;
                return;
            }

            try
            {
                string projectRoot = Directory.GetCurrentDirectory();
                string sceneFilePath = Path.Combine(projectRoot, MainScenePath);
                if (!File.Exists(sceneFilePath))
                {
                    Debug.LogError("[FaaMainSceneRunner] Main scene not found: " + MainScenePath);
                    DeleteRequestFile();
                    return;
                }

                Scene activeScene = SceneManager.GetActiveScene();
                if (!activeScene.isLoaded || activeScene.path != MainScenePath)
                {
                    EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
                }

                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(MainScenePath, true)
                };

                DeleteRequestFile();
                Debug.Log("FAA_OPL_RUNNER: Loaded Main scene and entering Play mode.");
                EditorApplication.isPlaying = true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[FaaMainSceneRunner] Failed to open Main scene and enter Play mode: " + ex);
            }
        }

        private static void EnsureRequestFile()
        {
            string directory = Path.GetDirectoryName(RequestFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(RequestFilePath, DateTime.UtcNow.ToString("O"));
            AssetDatabase.ImportAsset(RequestFilePath);
        }

        private static void DeleteRequestFile()
        {
            if (File.Exists(RequestFilePath))
            {
                File.Delete(RequestFilePath);
            }

            string metaPath = RequestFilePath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }
    }
}
#endif
