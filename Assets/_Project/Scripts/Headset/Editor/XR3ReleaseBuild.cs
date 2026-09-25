#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Management;

namespace FAA.Headset.Editor
{
    /// <summary>Reproducible native Varjo XR-3 release, cross-buildable on macOS.</summary>
    public static class XR3ReleaseBuild
    {
        // Ship the integration scene that contains the currently validated HUD and terrain bridge.
        public const string MainScene = "Assets/_Project/Scenes/ExperimentScene.unity";
        public static bool IsBuildingXR3 { get; private set; }

        [Serializable]
        private sealed class ReleaseReport
        {
            public string result;
            public string utc;
            public string unity;
            public string target = "StandaloneWindows64";
            public string graphicsApi = "Direct3D11";
            public string scriptingBackend = "Mono";
            public string varjoPlugin;
            public string executable;
            public string[] scenes;
            public ulong bytes;
            public double seconds;
            public int errors;
            public int warnings;
            public bool development = false;
            public bool hardwareTested = false;
            public string[] buildErrors;
        }

        [MenuItem("FAA/Headset/Build XR-3 Windows x64 Release")]
        public static void BuildWindows64()
        {
            const BuildTarget target = BuildTarget.StandaloneWindows64;
            var namedTarget = NamedBuildTarget.Standalone;
            if (EditorUserBuildSettings.activeBuildTarget != target)
                throw new BuildFailedException("Select Windows x64 first, or pass -buildTarget Win64 to the Unity command line.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new BuildFailedException("Install this Unity editor's Windows Build Support (Mono) module.");
            if (!File.Exists(MainScene))
                throw new BuildFailedException("FAA ExperimentScene is missing.");
            if (!EditorBuildSettings.TryGetConfigObject<XRGeneralSettingsPerBuildTarget>(
                XRGeneralSettings.k_SettingsKey, out var perTarget))
                throw new BuildFailedException("Standalone XR settings are missing.");
            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (general == null || !general.InitManagerOnStart || general.Manager == null ||
                general.Manager.activeLoaders.Count == 0 ||
                general.Manager.activeLoaders[0].GetType().FullName != "Varjo.XR.VarjoLoader")
                throw new BuildFailedException("Varjo must be the first Standalone XR loader, with Initialize XR on Startup enabled.");

            string output = GetArgument("-xr3Output") ?? "Builds/FAA-XR3-Windows-x64/FAA-XR3.exe";
            output = Path.GetFullPath(output);
            if (!string.Equals(Path.GetExtension(output), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("-xr3Output must name an .exe file.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            // Restore the user's editor/player preferences even on a failed build.
            bool autoApi = PlayerSettings.GetUseDefaultGraphicsAPIs(target);
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(target);
            ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(namedTarget);
            ColorSpace colorSpace = PlayerSettings.colorSpace;
            bool background = PlayerSettings.runInBackground;
            bool graphicsJobs = PlayerSettings.graphicsJobs;
            bool development = EditorUserBuildSettings.development;
            bool allowDebugging = EditorUserBuildSettings.allowDebugging;
            try
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
                PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.Direct3D11 });
                PlayerSettings.SetScriptingBackend(namedTarget, ScriptingImplementation.Mono2x);
                PlayerSettings.colorSpace = ColorSpace.Linear;
                PlayerSettings.runInBackground = true;
                PlayerSettings.graphicsJobs = false;
                EditorUserBuildSettings.development = false;
                EditorUserBuildSettings.allowDebugging = false;
                IsBuildingXR3 = true;

                Debug.Log("[FAA XR3 BUILD] Building native Varjo Windows x64 release: " + output);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MainScene },
                    locationPathName = output,
                    target = target,
                    options = BuildOptions.CompressWithLz4,
                });
                var summary = report.summary;
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Varjo.XR.VarjoLoader).Assembly);
                var evidence = new ReleaseReport
                {
                    result = summary.result.ToString(),
                    utc = DateTime.UtcNow.ToString("O"),
                    unity = Application.unityVersion,
                    varjoPlugin = package != null ? package.version : "unknown",
                    executable = Path.GetFileName(output),
                    scenes = new[] { MainScene },
                    bytes = summary.totalSize,
                    seconds = summary.totalTime.TotalSeconds,
                    errors = summary.totalErrors,
                    warnings = summary.totalWarnings,
                    buildErrors = report.steps.SelectMany(step => step.messages)
                        .Where(message => message.type == LogType.Error || message.type == LogType.Exception)
                        .Select(message => message.content).ToArray(),
                };
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "build-report.json"), JsonUtility.ToJson(evidence, true));
                // Unity can return Succeeded even when individual shaders fail.
                // Never package a release with pink/error shader variants.
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0 || !File.Exists(output))
                    throw new BuildFailedException("XR-3 player build failed; see build-report.json and the Unity log.");
                Debug.Log("[FAA XR3 BUILD] SUCCESS: " + output + " (" + summary.totalSize + " bytes)");
            }
            finally
            {
                IsBuildingXR3 = false;
                PlayerSettings.SetGraphicsAPIs(target, apis);
                PlayerSettings.SetUseDefaultGraphicsAPIs(target, autoApi);
                PlayerSettings.SetScriptingBackend(namedTarget, backend);
                PlayerSettings.colorSpace = colorSpace;
                PlayerSettings.runInBackground = background;
                PlayerSettings.graphicsJobs = graphicsJobs;
                EditorUserBuildSettings.development = development;
                EditorUserBuildSettings.allowDebugging = allowDebugging;
            }
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == name)
                {
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                        throw new BuildFailedException("Missing value for " + name);
                    return args[i + 1];
                }
            return null;
        }
    }

    /// <summary>Modify only the build's scene copy, never the user's scene asset.</summary>
    internal sealed class XR3ReleaseSceneProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 10000;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!XR3ReleaseBuild.IsBuildingXR3 || report == null ||
                report.summary.platform != BuildTarget.StandaloneWindows64) return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // XRI 3.x's XRInteractionSimulator is not an XRDeviceSimulator
                // subclass. Strip both implementations before any player Awake.
                foreach (XRInteractionSimulator simulator in root.GetComponentsInChildren<XRInteractionSimulator>(true))
                    if (simulator != null) UnityEngine.Object.DestroyImmediate(simulator.gameObject);
                if (root == null) continue;
                foreach (XRDeviceSimulator simulator in root.GetComponentsInChildren<XRDeviceSimulator>(true))
                    if (simulator != null) UnityEngine.Object.DestroyImmediate(simulator.gameObject);
                if (root == null) continue;
                // Post Processing Stack v1's Graphics.Blit intermediates are
                // not a validated texture-array/quad-view path. Keep native
                // headset rendering free of these legacy camera effects.
                foreach (Behaviour component in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (component != null && component.enabled &&
                        component.GetType().FullName == "UnityEngine.PostProcessing.PostProcessingBehaviour")
                    {
                        component.enabled = false;
                        Debug.Log("[FAA XR3 BUILD] Disabled legacy Post Processing Stack v1 on " + component.gameObject.name);
                    }
                }
                foreach (XR3HeadsetCompatibility bridge in root.GetComponentsInChildren<XR3HeadsetCompatibility>(true))
                {
                    var settings = new SerializedObject(bridge);
                    settings.FindProperty("activationMode").enumValueIndex = (int)XR3HeadsetCompatibility.ActivationMode.Auto;
                    settings.FindProperty("autoDetectNativeXr").boolValue = true;
                    settings.FindProperty("enableEditorSimulator").boolValue = false;
                    settings.FindProperty("enableSimulatorInPlayer").boolValue = false;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<XRInteractionSimulator>(true) != null ||
                    root.GetComponentInChildren<XRDeviceSimulator>(true) != null)
                    throw new BuildFailedException("A desktop XR simulator remains in the native XR-3 scene.");
            }
            Debug.Log("[FAA XR3 BUILD] Native-only scene prepared: " + scene.path);
        }
    }
}
#endif
