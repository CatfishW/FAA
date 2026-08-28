#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FAA.Editor
{
    [InitializeOnLoad]
    public static class FaaMcpBootstrap
    {
        private const string UseHttpTransport = "MCPForUnity.UseHttpTransport";
        private const string HttpTransportScope = "MCPForUnity.HttpTransportScope";
        private const string HttpBaseUrl = "MCPForUnity.HttpUrl";
        private const string HttpRemoteBaseUrl = "MCPForUnity.HttpRemoteUrl";
        private const string SetupCompleted = "MCPForUnity.SetupCompleted";
        private const string SetupDismissed = "MCPForUnity.SetupDismissed";
        private const string ProjectScopedToolsLocalHttp = "MCPForUnity.ProjectScopedTools.LocalHttp";
        private const string ResumeHttpAfterReload = "MCPForUnity.ResumeHttpAfterReload";
        private const string AutoLoadMainScene = "FAA.MCPBootstrap.AutoLoadMainScene";
        internal const string MainScenePath = "Assets/_Project/Scenes/Main.unity";

        private static bool reloadQueued;

        static FaaMcpBootstrap()
        {
            if (IsAssetImportWorker())
            {
                return;
            }

            ConfigurePrefs();
            ConfigureAssetRefresh();
            EditorApplication.delayCall += StartBridge;
            if (EditorPrefs.GetBool(AutoLoadMainScene, false))
            {
                EditorApplication.delayCall += QueueMainSceneReload;
            }
            EditorApplication.update += ReloadMainSceneWhenIdle;
        }

        private static bool IsAssetImportWorker()
        {
            try
            {
                return AssetDatabase.IsAssetImportWorkerProcess();
            }
            catch
            {
                return false;
            }
        }

        private static void ConfigurePrefs()
        {
            EditorPrefs.SetBool(UseHttpTransport, true);
            EditorPrefs.SetString(HttpTransportScope, "local");
            EditorPrefs.SetString(HttpBaseUrl, "http://localhost:8080");
            EditorPrefs.SetString(HttpRemoteBaseUrl, "http://localhost:8080");
            EditorPrefs.SetBool(SetupCompleted, true);
            EditorPrefs.SetBool(SetupDismissed, true);
            EditorPrefs.SetBool(ProjectScopedToolsLocalHttp, true);
            EditorPrefs.SetBool(ResumeHttpAfterReload, true);
        }

        private static void ConfigureAssetRefresh()
        {
            EditorPrefs.SetInt("kAutoRefresh", 1);
            EditorPrefs.SetBool("ScriptCompilationDuringPlay", true);
        }

        private static async void StartBridge()
        {
            if (IsAssetImportWorker())
            {
                return;
            }

            try
            {
                ConfigurePrefs();

                Type cacheType = Type.GetType("MCPForUnity.Editor.Services.EditorConfigurationCache, MCPForUnity.Editor");
                object cache = cacheType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                cacheType?.GetMethod("Refresh", BindingFlags.Public | BindingFlags.Instance)?.Invoke(cache, null);

                Type locatorType = Type.GetType("MCPForUnity.Editor.Services.MCPServiceLocator, MCPForUnity.Editor");
                object bridge = locatorType?.GetProperty("Bridge", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                MethodInfo startAsync = bridge?.GetType().GetMethod("StartAsync", BindingFlags.Public | BindingFlags.Instance);
                if (startAsync == null)
                {
                    Debug.LogWarning("[FaaMcpBootstrap] MCP bridge service is not available yet.");
                    return;
                }

                var task = startAsync.Invoke(bridge, null) as System.Threading.Tasks.Task<bool>;
                bool started = task != null && await task;
                Debug.Log(started
                    ? "[FaaMcpBootstrap] MCP HTTP bridge started for FAA at http://localhost:8080."
                    : "[FaaMcpBootstrap] MCP HTTP bridge did not start; check the MCP server service and Unity console.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FaaMcpBootstrap] Failed to start MCP bridge: " + ex.Message);
            }
        }

        internal static void QueueMainSceneReload()
        {
            if (!EditorPrefs.GetBool(AutoLoadMainScene, false))
            {
                return;
            }

            reloadQueued = true;
        }

        private static void ReloadMainSceneWhenIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }

            if (!reloadQueued)
            {
                return;
            }

            reloadQueued = false;
            ConfigurePrefs();
            ConfigureAssetRefresh();

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path == MainScenePath || !System.IO.File.Exists(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), MainScenePath)))
            {
                return;
            }

            try
            {
                EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
                Debug.Log("[FaaMcpBootstrap] Loaded Main scene after editor refresh.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FaaMcpBootstrap] Failed to load Main scene after refresh: " + ex.Message);
            }
        }
    }

    public sealed class FaaMcpBootstrapAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string asset in importedAssets)
            {
                if (asset == FaaMcpBootstrap.MainScenePath)
                {
                    FaaMcpBootstrap.QueueMainSceneReload();
                    break;
                }
            }
        }
    }
}
#endif
