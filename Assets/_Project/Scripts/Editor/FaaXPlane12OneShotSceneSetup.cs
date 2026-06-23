#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FAA.Editor
{
    [InitializeOnLoad]
    public static class FaaXPlane12OneShotSceneSetup
    {
        private const string PendingKey = "FAA.XPlane12.OneShotSceneSetup.Pending";
        private const string CompletedKey = "FAA.XPlane12.OneShotSceneSetup.Completed";
        private const string RequestedAtKey = "FAA.XPlane12.OneShotSceneSetup.RequestedAt";
        private const string CompletedAtKey = "FAA.XPlane12.OneShotSceneSetup.CompletedAt";
        private const string RequestFilePath = "Assets/_Project/Verification/FaaXPlane12OneShotSceneSetup.request";

        static FaaXPlane12OneShotSceneSetup()
        {
            EditorApplication.delayCall += RunIfPending;
        }

        [MenuItem("FAA/X-Plane 12/Request One Shot Scene Setup")]
        public static void Request()
        {
            EditorPrefs.SetBool(PendingKey, true);
            EditorPrefs.SetBool(CompletedKey, false);
            EditorPrefs.SetString(RequestedAtKey, DateTime.UtcNow.ToString("O"));
            EnsureRequestFile();
            EditorApplication.delayCall += RunIfPending;
            Debug.Log("[FaaXPlane12OneShotSceneSetup] Requested one-shot ExperimentScene setup.");
        }

        private static void RunIfPending()
        {
            bool hasRequestFile = File.Exists(RequestFilePath);
            if (!EditorPrefs.GetBool(PendingKey, false) && !hasRequestFile)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RunIfPending;
                return;
            }

            try
            {
                FaaXPlane12BridgeSceneSetup.ConfigureExperimentScene();
                DeleteRequestFile();
                EditorPrefs.SetBool(PendingKey, false);
                EditorPrefs.SetBool(CompletedKey, true);
                EditorPrefs.SetString(CompletedAtKey, DateTime.UtcNow.ToString("O"));
                Debug.Log("[FaaXPlane12OneShotSceneSetup] Completed one-shot ExperimentScene setup.");
            }
            catch (Exception ex)
            {
                EditorPrefs.SetBool(PendingKey, false);
                EditorPrefs.SetBool(CompletedKey, false);
                Debug.LogError("[FaaXPlane12OneShotSceneSetup] Failed one-shot ExperimentScene setup: " + ex);
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
            if (!File.Exists(RequestFilePath))
            {
                return;
            }

            File.Delete(RequestFilePath);
            string metaPath = RequestFilePath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }
    }
}
#endif
