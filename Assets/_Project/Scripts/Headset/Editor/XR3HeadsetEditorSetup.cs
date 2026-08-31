#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using Varjo.XR;

namespace FAA.Headset.Editor
{
    /// <summary>One-click setup for the Varjo XR-3 provider and desktop simulator.</summary>
    public static class XR3HeadsetEditorSetup
    {
        private const string XriPackageName = "com.unity.xr.interaction.toolkit";
        private const string SimulatorSampleName = "XR Interaction Simulator";
        private const string SimulatorPrefabName = "XR Interaction Simulator";
        private const string VarjoLoaderType = "Varjo.XR.VarjoLoader";
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string ExperimentScenePath = "Assets/_Project/Scenes/ExperimentScene.unity";
        private const string SettingsDirectory = "Assets/Settings";
        private const string SettingsAssetPath = SettingsDirectory + "/XRGeneralSettingsPerBuildTarget.asset";
        private const string VarjoSettingsPath = "Assets/XR/Settings/VarjoSettings.asset";
        private const string VarjoSettingsKey = "Varjo.XR.Settings";
        private const string SimulatorResourceDirectory = "Assets/Resources/FAA/XR3";
        private const string SimulatorResourcePrefabPath = SimulatorResourceDirectory + "/XR Interaction Simulator.prefab";

        [MenuItem("FAA/Headset/Install XR-3 Simulator Sample")]
        public static void InstallSimulatorSample()
        {
            GameObject prefab = ImportSimulatorSample();
            prefab = EnsureResourcePrefab(prefab);
            if (prefab == null)
            {
                throw new InvalidOperationException("XR Interaction Simulator prefab could not be found after importing the sample.");
            }

            Debug.Log("[FAA XR] XR Interaction Simulator sample is installed at " + AssetDatabase.GetAssetPath(prefab));
        }

        [MenuItem("FAA/Headset/Configure Varjo XR-3 Provider")]
        public static void ConfigureVarjoProvider()
        {
            ConfigureVarjoLoader();
            Debug.Log("[FAA XR] Varjo XR-3 loader assigned to the Standalone XR settings.");
        }

        [MenuItem("FAA/Headset/Configure XR-3 + Simulator In FAA Scenes")]
        public static void ConfigureScenesAndProvider()
        {
            GameObject simulatorPrefab = EnsureResourcePrefab(ImportSimulatorSample());
            ConfigureVarjoLoader();
            ConfigureScene(MainScenePath, simulatorPrefab);
            ConfigureScene(ExperimentScenePath, simulatorPrefab);
            Debug.Log("[FAA XR] Main and Experiment scenes are configured for Varjo XR-3 and the Unity simulator.");
        }

        public static void ConfigureAllForBatch()
        {
            ConfigureScenesAndProvider();
        }

        private static GameObject ImportSimulatorSample()
        {
            IEnumerable<Sample> samples = Sample.FindByPackage(XriPackageName, string.Empty);
            if (samples == null)
            {
                throw new InvalidOperationException("The XR Interaction Toolkit package is not resolved yet.");
            }

            Sample selected = default;
            bool found = false;
            foreach (Sample sample in samples)
            {
                if (string.Equals(sample.displayName, SimulatorSampleName, StringComparison.Ordinal))
                {
                    selected = sample;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                throw new InvalidOperationException("The XR Interaction Simulator sample is not present in the installed XRI package.");
            }

            if (!selected.isImported && !selected.Import(Sample.ImportOptions.HideImportWindow | Sample.ImportOptions.OverridePreviousImports))
            {
                throw new InvalidOperationException("Unity rejected the XR Interaction Simulator sample import.");
            }

            AssetDatabase.Refresh();
            string[] guids = AssetDatabase.FindAssets(SimulatorPrefabName + " t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && string.Equals(prefab.name, SimulatorPrefabName, StringComparison.Ordinal))
                {
                    return prefab;
                }
            }

            return null;
        }

        private static GameObject EnsureResourcePrefab(GameObject samplePrefab)
        {
            if (samplePrefab == null)
            {
                return null;
            }

            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/FAA");
            EnsureFolder(SimulatorResourceDirectory);

            if (!File.Exists(SimulatorResourcePrefabPath))
            {
                string sourcePath = AssetDatabase.GetAssetPath(samplePrefab);
                if (!AssetDatabase.CopyAsset(sourcePath, SimulatorResourcePrefabPath))
                {
                    throw new InvalidOperationException("Could not copy the XR Interaction Simulator prefab into Resources.");
                }
                AssetDatabase.Refresh();
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorResourcePrefabPath) ?? samplePrefab;
        }

        private static void ConfigureVarjoLoader()
        {
            EnsureDirectory(SettingsDirectory);

            XRGeneralSettingsPerBuildTarget settings = FindOrCreateGeneralSettings();
            if (!settings.HasSettingsForBuildTarget(BuildTargetGroup.Standalone))
            {
                settings.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Standalone);
            }

            if (!settings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Standalone))
            {
                settings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Standalone);
            }

            XRGeneralSettings general = settings.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            general.InitManagerOnStart = true;
            XRManagerSettings manager = general.Manager;
            if (!XRPackageMetadataStore.AssignLoader(manager, VarjoLoaderType, BuildTargetGroup.Standalone))
            {
                throw new InvalidOperationException("Varjo loader assignment failed. Confirm com.varjo.xr is resolved.");
            }

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(manager);
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settings, true);

            VarjoSettings varjoSettings = FindOrCreateVarjoSettings();
            EditorBuildSettings.AddConfigObject(VarjoSettingsKey, varjoSettings, true);
            AssetDatabase.SaveAssets();
        }

        private static VarjoSettings FindOrCreateVarjoSettings()
        {
            VarjoSettings settings = AssetDatabase.LoadAssetAtPath<VarjoSettings>(VarjoSettingsPath);
            if (settings != null)
            {
                return settings;
            }

            EnsureDirectory("Assets/XR/Settings");
            settings = ScriptableObject.CreateInstance<VarjoSettings>();
            AssetDatabase.CreateAsset(settings, VarjoSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static XRGeneralSettingsPerBuildTarget FindOrCreateGeneralSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
            XRGeneralSettingsPerBuildTarget settings = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
                if (settings != null)
                {
                    break;
                }
            }

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            }

            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settings, true);
            return settings;
        }

        private static void ConfigureScene(string scenePath, GameObject simulatorPrefab)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning("[FAA XR] Scene not found; skipping " + scenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            XR3HeadsetCompatibility compatibility = FindSceneComponent<XR3HeadsetCompatibility>(scene);
            if (compatibility == null)
            {
                GameObject host = new GameObject("FAA XR-3 Integration");
                SceneManager.MoveGameObjectToScene(host, scene);
                compatibility = host.AddComponent<XR3HeadsetCompatibility>();
            }

            SerializedObject serialized = new SerializedObject(compatibility);
            SetEnum(serialized, "activationMode", 0); // Auto
            SetBool(serialized, "autoDetectNativeXr", true);
            // Keep the desktop simulator enabled in the Editor so the XR-3
            // devices are available on development Macs/Linux workstations
            // that do not have a native Varjo runtime attached.
            SetBool(serialized, "enableEditorSimulator", true);
            SetBool(serialized, "enableSimulatorInPlayer", false);
            SetObject(serialized, "simulatorPrefab", simulatorPrefab);
            SetBool(serialized, "routeOverlayCanvasesToXrCamera", true);
            SetVector2(serialized, "simulatorInputSelectionMargin", new Vector2(18f, 150f));
            SetBool(serialized, "placeSimulatorInputSelectionBelowHeadingTape", true);
            SetBool(serialized, "suspendLegacySa147WhileActive", true);
            SetBool(serialized, "logActivation", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Keep a clearly named simulator entry in the Hierarchy. The
            // runtime bridge reuses this object, so Play mode does not create
            // a duplicate simulator instance.
            GameObject sceneSimulator = FindSceneObject("FAA XR-3 Unity Simulator", scene);
            if (sceneSimulator == null && simulatorPrefab != null)
            {
                sceneSimulator = (GameObject)PrefabUtility.InstantiatePrefab(simulatorPrefab, scene);
                sceneSimulator.name = "FAA XR-3 Unity Simulator";
                sceneSimulator.transform.position = Vector3.zero;
                EditorUtility.SetDirty(sceneSimulator);
            }

            EditorUtility.SetDirty(compatibility.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static GameObject FindSceneObject(string objectName, Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                {
                    return root;
                }

                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.gameObject.name == objectName)
                    {
                        return child.gameObject;
                    }
                }
            }

            return null;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static void EnsureDirectory(string path)
        {
            EnsureFolder(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void SetBool(SerializedObject serialized, string name, bool value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null) property.boolValue = value;
        }

        private static void SetEnum(SerializedObject serialized, string name, int value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null) property.enumValueIndex = value;
        }

        private static void SetObject(SerializedObject serialized, string name, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null) property.objectReferenceValue = value;
        }

        private static void SetVector2(SerializedObject serialized, string name, Vector2 value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null) property.vector2Value = value;
        }
    }

    /// <summary>
    /// Varjo's native runtime is a Windows component. Keep the serialized
    /// Standalone loader enabled for Windows builds, but avoid asking it to
    /// load inside the macOS/Linux editor where its native library cannot be
    /// present. The FAA bridge starts the desktop simulator in editor Play
    /// mode. This is an in-memory override and is never saved to disk.
    /// </summary>
    [InitializeOnLoad]
    internal static class FAAXR3EditorNativeLoaderGuard
    {
        private const string OverrideStateKey = "FAA.XR3.NativeLoaderGuard.Overridden";
        private const string PreviousStateKey = "FAA.XR3.NativeLoaderGuard.PreviousInitManagerOnStart";
        private static XRGeneralSettings s_GeneralSettings;
        private static bool s_PreviousInitManagerOnStart;
        private static bool s_Overridden;

        static FAAXR3EditorNativeLoaderGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // Entering Play can reload the scripting domain before the
            // EnteredEditMode callback runs. SessionState survives that
            // reload, so defer one recovery pass for the stop/reload path.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RestoreNativeLoaderSetting;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                DisableNativeLoaderForUnsupportedEditor();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                RestoreNativeLoaderSetting();
                // Unity may still report a pending play-mode transition for
                // this callback. Run the same restore once more on the next
                // editor tick so domain-reload and reload-disabled modes both
                // return to the serialized Windows-build setting.
                EditorApplication.delayCall -= RestoreNativeLoaderSetting;
                EditorApplication.delayCall += RestoreNativeLoaderSetting;
            }
        }

        private static void DisableNativeLoaderForUnsupportedEditor()
        {
            if (s_Overridden || !IsUnsupportedNativeEditor())
            {
                return;
            }

            if (!EditorBuildSettings.TryGetConfigObject<XRGeneralSettingsPerBuildTarget>(
                    XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget settings))
            {
                return;
            }

            XRGeneralSettings general = settings.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (general == null || !general.InitManagerOnStart)
            {
                return;
            }

            s_GeneralSettings = general;
            s_PreviousInitManagerOnStart = general.InitManagerOnStart;
            SessionState.SetBool(OverrideStateKey, true);
            SessionState.SetBool(PreviousStateKey, s_PreviousInitManagerOnStart);
            general.InitManagerOnStart = false;
            s_Overridden = true;
            Debug.Log("[FAA XR] Native Varjo startup is disabled for this macOS/Linux editor Play session; the Unity XR-3 simulator remains enabled. Windows builds retain native startup.");
        }

        private static void RestoreNativeLoaderSetting()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            bool sessionOverride = SessionState.GetBool(OverrideStateKey, false);
            if (!s_Overridden && !sessionOverride)
            {
                return;
            }

            XRGeneralSettings general = s_GeneralSettings;
            if (general == null &&
                EditorBuildSettings.TryGetConfigObject<XRGeneralSettingsPerBuildTarget>(
                    XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget settings))
            {
                general = settings.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            }

            if (general != null)
            {
                bool previous = s_Overridden
                    ? s_PreviousInitManagerOnStart
                    : SessionState.GetBool(PreviousStateKey, true);
                general.InitManagerOnStart = previous;
            }

            s_GeneralSettings = null;
            s_Overridden = false;
            SessionState.EraseBool(OverrideStateKey);
            SessionState.EraseBool(PreviousStateKey);
        }

        private static bool IsUnsupportedNativeEditor()
        {
            return Application.platform == RuntimePlatform.OSXEditor ||
                Application.platform == RuntimePlatform.LinuxEditor;
        }
    }
}
#endif
