#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FAA.Editor
{
    public static class FaaSceneDiagnostics
    {
        private const string ExperimentScenePath = "Assets/_Project/Scenes/ExperimentScene.unity";

        [MenuItem("FAA/Diagnostics/Log Missing Scripts In Loaded Objects")]
        public static void LogMissingScriptsInLoadedObjects()
        {
            int total = 0;
            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject == null)
                {
                    continue;
                }

                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (missingCount <= 0)
                {
                    continue;
                }

                total += missingCount;
                string assetPath = AssetDatabase.GetAssetPath(gameObject);
                string context = string.IsNullOrEmpty(assetPath) ? GetPath(gameObject) : $"{assetPath} :: {GetPath(gameObject)}";
                Debug.LogError($"[FaaSceneDiagnostics] Missing script count {missingCount} on {context}", gameObject);
            }

            Debug.Log($"[FaaSceneDiagnostics] Missing script scan complete. Missing component count: {total}.");
        }

        [MenuItem("FAA/Diagnostics/Remove Missing Scripts In Loaded Objects")]
        public static void RemoveMissingScriptsInLoadedObjects()
        {
            int total = 0;
            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject == null || EditorUtility.IsPersistent(gameObject))
                {
                    continue;
                }

                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (missingCount <= 0)
                {
                    continue;
                }

                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
                EditorUtility.SetDirty(gameObject);
                total += missingCount;
                Debug.Log($"[FaaSceneDiagnostics] Removed {missingCount} missing script component(s) from {GetPath(gameObject)}", gameObject);
            }

            Debug.Log($"[FaaSceneDiagnostics] Missing script removal complete. Removed component count: {total}.");
        }

        [MenuItem("FAA/Diagnostics/Log Missing Scripts In Prefab Assets")]
        public static void LogMissingScriptsInPrefabAssets()
        {
            int total = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    if (missingCount <= 0)
                    {
                        continue;
                    }

                    total += missingCount;
                    Debug.LogError($"[FaaSceneDiagnostics] Missing script count {missingCount} in prefab asset {path} :: {GetPath(transform.gameObject)}", transform.gameObject);
                }
            }

            Debug.Log($"[FaaSceneDiagnostics] Prefab missing script scan complete. Missing component count: {total}.");
        }

        [MenuItem("FAA/Diagnostics/Remove Missing Scripts In Prefab Assets")]
        public static void RemoveMissingScriptsInPrefabAssets()
        {
            int total = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                int prefabRemoved = 0;
                foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    if (missingCount <= 0)
                    {
                        continue;
                    }

                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                    EditorUtility.SetDirty(transform.gameObject);
                    prefabRemoved += missingCount;
                    Debug.Log($"[FaaSceneDiagnostics] Removed {missingCount} missing script component(s) from prefab asset {path} :: {GetPath(transform.gameObject)}", transform.gameObject);
                }

                if (prefabRemoved > 0)
                {
                    PrefabUtility.SavePrefabAsset(prefab);
                    EditorUtility.SetDirty(prefab);
                    total += prefabRemoved;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FaaSceneDiagnostics] Prefab missing script removal complete. Removed component count: {total}.");
        }

        [MenuItem("FAA/Diagnostics/Scan Active Scene Script References")]
        public static void ScanActiveSceneScriptReferences()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("[FaaSceneDiagnostics] No saved active scene is loaded.");
                return;
            }

            string sceneText = System.IO.File.ReadAllText(scene.path);
            MatchCollection matches = Regex.Matches(sceneText, @"m_Script:\s*\{fileID:\s*(?<fileID>-?\d+),\s*guid:\s*(?<guid>[0-9a-f]{32}),\s*type:\s*(?<type>\d+)\}");
            int unresolvedCount = 0;
            int nullClassCount = 0;
            int nonBehaviourClassCount = 0;

            foreach (Match match in matches)
            {
                string guid = match.Groups["guid"].Value;
                if (IsBuiltInUnityGuid(guid))
                {
                    continue;
                }

                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    unresolvedCount++;
                    Debug.LogError($"[FaaSceneDiagnostics] Unresolved script GUID in {scene.path}: {guid}");
                    continue;
                }

                if (IsBuiltInUnityAssetPath(path))
                {
                    continue;
                }

                MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (monoScript == null)
                {
                    unresolvedCount++;
                    Debug.LogError($"[FaaSceneDiagnostics] Script GUID resolves to non-MonoScript asset in {scene.path}: {guid} -> {path}");
                    continue;
                }

                if (monoScript.GetClass() == null)
                {
                    nullClassCount++;
                    Debug.LogError($"[FaaSceneDiagnostics] MonoScript has no loadable class in {scene.path}: {guid} -> {path}");
                    continue;
                }

                System.Type scriptClass = monoScript.GetClass();
                if (!IsScriptReferenceOnSceneGameObject(sceneText, match.Index))
                {
                    continue;
                }

                if (!typeof(MonoBehaviour).IsAssignableFrom(scriptClass))
                {
                    nonBehaviourClassCount++;
                    Debug.LogError($"[FaaSceneDiagnostics] Scene MonoBehaviour script reference does not resolve to a MonoBehaviour class in {scene.path}: {guid} -> {path} ({scriptClass.FullName})");
                }
            }

            Debug.Log($"[FaaSceneDiagnostics] Active scene script reference scan complete. References: {matches.Count}, unresolved: {unresolvedCount}, null classes: {nullClassCount}.");
            if (nonBehaviourClassCount > 0)
            {
                Debug.LogError($"[FaaSceneDiagnostics] Active scene has {nonBehaviourClassCount} MonoBehaviour script reference(s) whose resolved class is not a MonoBehaviour.");
            }
        }

        private static bool IsScriptReferenceOnSceneGameObject(string sceneText, int scriptReferenceIndex)
        {
            int blockStart = sceneText.LastIndexOf("\n--- !u!", scriptReferenceIndex, System.StringComparison.Ordinal);
            if (blockStart < 0)
            {
                blockStart = 0;
            }

            int blockEnd = sceneText.IndexOf("\n--- !u!", scriptReferenceIndex, System.StringComparison.Ordinal);
            if (blockEnd < 0)
            {
                blockEnd = sceneText.Length;
            }

            string block = sceneText.Substring(blockStart, blockEnd - blockStart);
            return Regex.IsMatch(block, @"m_GameObject:\s*\{fileID:\s*(?!0\})-?\d+\}");
        }

        [MenuItem("FAA/Diagnostics/Scan Project Script References")]
        public static void ScanProjectScriptReferences()
        {
            int referenceCount = 0;
            int unresolvedCount = 0;
            int nullClassCount = 0;
            int zeroScriptCount = 0;

            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!ShouldScanSerializedAsset(path))
                {
                    continue;
                }

                string assetText;
                try
                {
                    assetText = File.ReadAllText(path);
                }
                catch
                {
                    continue;
                }

                MatchCollection zeroMatches = Regex.Matches(assetText, @"m_Script:\s*\{fileID:\s*0(?:,|\})");
                foreach (Match zeroMatch in zeroMatches)
                {
                    zeroScriptCount++;
                    Debug.LogError($"[FaaSceneDiagnostics] Null script reference in {path} near character {zeroMatch.Index}.");
                }

                MatchCollection matches = Regex.Matches(assetText, @"m_Script:\s*\{fileID:\s*(?<fileID>-?\d+),\s*guid:\s*(?<guid>[0-9a-f]{32}),\s*type:\s*(?<type>\d+)\}");
                foreach (Match match in matches)
                {
                    referenceCount++;
                    string guid = match.Groups["guid"].Value;
                    if (IsBuiltInUnityGuid(guid))
                    {
                        continue;
                    }

                    string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(scriptPath))
                    {
                        unresolvedCount++;
                        Debug.LogError($"[FaaSceneDiagnostics] Unresolved script GUID in {path}: {guid}");
                        continue;
                    }

                    if (IsBuiltInUnityAssetPath(scriptPath))
                    {
                        continue;
                    }

                    MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                    if (monoScript == null)
                    {
                        unresolvedCount++;
                        Debug.LogError($"[FaaSceneDiagnostics] Script GUID resolves to non-MonoScript asset in {path}: {guid} -> {scriptPath}");
                        continue;
                    }

                    if (monoScript.GetClass() == null)
                    {
                        nullClassCount++;
                        Debug.LogError($"[FaaSceneDiagnostics] MonoScript has no loadable class in {path}: {guid} -> {scriptPath}");
                    }
                }
            }

            Debug.Log($"[FaaSceneDiagnostics] Project script reference scan complete. References: {referenceCount}, " +
                      $"unresolved: {unresolvedCount}, null classes: {nullClassCount}, null script refs: {zeroScriptCount}.");
        }

        [MenuItem("FAA/Diagnostics/Log Large Opaque UI Graphics")]
        public static void LogLargeOpaqueUiGraphics()
        {
            int count = 0;
            foreach (Graphic graphic in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (graphic == null || graphic.color.a <= 0.01f)
                {
                    continue;
                }

                RectTransform rectTransform = graphic.rectTransform;
                if (rectTransform == null)
                {
                    continue;
                }

                Rect rect = rectTransform.rect;
                float width = Mathf.Abs(rect.width);
                float height = Mathf.Abs(rect.height);
                if (width < 16f || height < 16f)
                {
                    continue;
                }

                string asset = AssetDatabase.GetAssetPath(graphic);
                string spriteName = graphic is Image image && image.sprite != null ? image.sprite.name : "";
                string textureName = graphic is RawImage rawImage && rawImage.texture != null ? rawImage.texture.name : "";
                Debug.Log(
                    $"[FaaSceneDiagnostics] Large opaque UI graphic path='{GetPath(graphic.gameObject)}' type={graphic.GetType().Name} " +
                    $"size=({width:F1},{height:F1}) color={FormatColor(graphic.color)} raycast={graphic.raycastTarget} " +
                    $"sprite='{spriteName}' texture='{textureName}' asset='{asset}'",
                    graphic);
                count++;
            }

            Debug.Log($"[FaaSceneDiagnostics] Large opaque UI graphic scan complete. Count: {count}.");
        }

        [MenuItem("FAA/Diagnostics/Validate FAA HUD Components")]
        public static void ValidateFaaHudComponents()
        {
            int issues = 0;

            GameObject legacyCanvas = GameObject.Find("FAASymbologyCanvas");
            Require(legacyCanvas != null, "Missing FAASymbologyCanvas.", ref issues, legacyCanvas);
            Require(legacyCanvas == null || legacyCanvas.activeInHierarchy, "FAASymbologyCanvas is not active in the hierarchy.", ref issues, legacyCanvas);
            ValidateLegacyCanvas(legacyCanvas, ref issues);
            ValidateLegacyHudVisualHealth(legacyCanvas, ref issues);

            ValidateHudControlStack(ref issues);
            ValidateRuntimeSanitizer(ref issues);
            ValidateModeSwitcher(ref issues);
            ValidateUiToolkitAlternative(ref issues);
            ValidateBridge(ref issues);

            Debug.Log($"[FaaSceneDiagnostics] FAA HUD validation complete. Issues: {issues}.");
        }

        [MenuItem("FAA/Diagnostics/Validate Experiment Scene FAA HUD Components")]
        public static void ValidateExperimentSceneFaaHudComponents()
        {
            if (!File.Exists(ExperimentScenePath))
            {
                Debug.LogError($"[FaaSceneDiagnostics] Experiment scene not found: {ExperimentScenePath}");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ExperimentScenePath)
            {
                EditorSceneManager.OpenScene(ExperimentScenePath, OpenSceneMode.Single);
            }

            ValidateFaaHudComponents();
        }

        [MenuItem("FAA/Diagnostics/Validate FAA HUD Mode Switching")]
        public static void ValidateFaaHudModeSwitching()
        {
            int issues = 0;
            Component switcher = FindSceneObjects(FindType("FAA.HUDToolkit.FaaHudModeSwitcher"))
                .FirstOrDefault(IsLoadedSceneComponent);
            Require(switcher != null, "Missing FAA HUD Mode Switcher.", ref issues, null);
            if (switcher == null)
            {
                Debug.Log($"[FaaSceneDiagnostics] FAA HUD mode-switch validation complete. Issues: {issues}.");
                return;
            }

            System.Type modeType = switcher.GetType().GetNestedType("HudMode");
            MethodInfo setModeMethod = switcher.GetType().GetMethod("SetMode", BindingFlags.Instance | BindingFlags.Public);
            Require(modeType != null, "FAA HUD Mode Switcher HudMode enum is not available.", ref issues, switcher);
            Require(setModeMethod != null, "FAA HUD Mode Switcher SetMode method is not available.", ref issues, switcher);
            if (modeType == null || setModeMethod == null)
            {
                Debug.Log($"[FaaSceneDiagnostics] FAA HUD mode-switch validation complete. Issues: {issues}.");
                return;
            }

            object legacyMode = System.Enum.Parse(modeType, "LegacyUGUI");
            object toolkitMode = System.Enum.Parse(modeType, "UIToolkit");

            setModeMethod.Invoke(switcher, new[] { toolkitMode });
            ValidateHudModeState(expectLegacyVisible: false, ref issues);

            setModeMethod.Invoke(switcher, new[] { legacyMode });
            ValidateHudModeState(expectLegacyVisible: true, ref issues);

            Debug.Log($"[FaaSceneDiagnostics] FAA HUD mode-switch validation complete. Issues: {issues}.");
        }

        private static void ValidateLegacyCanvas(GameObject legacyCanvas, ref int issues)
        {
            if (legacyCanvas == null)
            {
                return;
            }

            Canvas canvas = legacyCanvas.GetComponent<Canvas>();
            Require(canvas != null, "FAASymbologyCanvas is missing Canvas.", ref issues, legacyCanvas);
            Require(canvas == null || canvas.enabled, "FAASymbologyCanvas Canvas is disabled.", ref issues, canvas);

            UnityEngine.UI.CanvasScaler scaler = legacyCanvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            Require(scaler != null, "FAASymbologyCanvas is missing CanvasScaler.", ref issues, legacyCanvas);
            Require(scaler == null || scaler.enabled, "FAASymbologyCanvas CanvasScaler is disabled.", ref issues, scaler);

            GraphicRaycaster raycaster = legacyCanvas.GetComponent<GraphicRaycaster>();
            Require(raycaster != null, "FAASymbologyCanvas is missing GraphicRaycaster.", ref issues, legacyCanvas);
            Require(raycaster == null || raycaster.enabled, "FAASymbologyCanvas GraphicRaycaster is disabled.", ref issues, raycaster);
        }

        private static void ValidateLegacyHudVisualHealth(GameObject legacyCanvas, ref int issues)
        {
            if (legacyCanvas == null)
            {
                return;
            }

            Graphic[] graphics = legacyCanvas.GetComponentsInChildren<Graphic>(false);
            int activeGraphics = 0;
            int hudChromeGraphics = 0;
            int blankBlockers = 0;

            foreach (Graphic graphic in graphics)
            {
                if (!IsRenderableGraphic(graphic))
                {
                    continue;
                }

                activeGraphics++;
                string path = GetPath(graphic.gameObject);
                if (path.Contains("/Second Interation GUI/"))
                {
                    hudChromeGraphics++;
                }

                if (!IsLargeBlankOpaqueGraphic(graphic))
                {
                    continue;
                }

                blankBlockers++;
                Debug.LogError($"[FaaSceneDiagnostics] Large blank opaque HUD graphic still visible: {path} " +
                               $"type={graphic.GetType().Name} color={FormatColor(graphic.color)}.",
                    graphic);
            }

            Require(activeGraphics >= 12,
                $"FAASymbologyCanvas has too few active renderable Graphics: {activeGraphics}.",
                ref issues,
                legacyCanvas);
            Require(hudChromeGraphics >= 8,
                $"Second Interation GUI has too few active HUD chrome Graphics: {hudChromeGraphics}.",
                ref issues,
                legacyCanvas);
            Require(blankBlockers == 0,
                $"FAASymbologyCanvas still has {blankBlockers} large blank opaque UI graphic(s).",
                ref issues,
                legacyCanvas);

            Debug.Log($"[FaaSceneDiagnostics] Legacy HUD visual health: activeGraphics={activeGraphics}, " +
                      $"hudChromeGraphics={hudChromeGraphics}, blankBlockers={blankBlockers}.");
        }

        private static void ValidateHudControlStack(ref int issues)
        {
            System.Type controllerType = FindType("HUDControl.Core.HUDController");
            System.Type elementBaseType = FindType("HUDControl.Core.HUDElementBase");
            Require(controllerType != null, "HUDControl.Core.HUDController type is not loaded.", ref issues, null);
            Require(elementBaseType != null, "HUDControl.Core.HUDElementBase type is not loaded.", ref issues, null);
            if (controllerType == null || elementBaseType == null)
            {
                return;
            }

            Component controller = FindSceneObjects(controllerType)
                .FirstOrDefault(IsLoadedSceneComponent);
            Require(controller != null, "Missing HUDControl HUDController.", ref issues, null);
            if (controller == null)
            {
                return;
            }

            Behaviour controllerBehaviour = controller as Behaviour;
            Require(controllerBehaviour == null || controllerBehaviour.enabled, "HUDController component is disabled.", ref issues, controller);
            Require(controller.gameObject.activeInHierarchy, "HUDController GameObject is not active.", ref issues, controller);

            SerializedObject serializedController = new SerializedObject(controller);
            RequireObjectReference(serializedController, "aircraftController", "HUDController missing AircraftController reference.", ref issues, controller);
            RequireBool(serializedController, "updateEveryFrame", true, "HUDController updateEveryFrame is disabled.", ref issues, controller);
            RequireBool(serializedController, "enableOnStart", true, "HUDController enableOnStart is disabled.", ref issues, controller);

            List<Component> registeredElements = GetRegisteredHudElements(serializedController, elementBaseType, ref issues, controller);
            List<Component> activeElements = registeredElements
                .Where(component => component != null && component.gameObject.activeInHierarchy && IsPreferredLegacyHudComponent(component))
                .ToList();

            Require(activeElements.Count > 0, "HUDController has no active uGUI HUDControl elements registered.", ref issues, controller);
            ValidateRequiredHudElementPresence(activeElements, ref issues, controller);

            foreach (Component element in activeElements)
            {
                ValidateHudControlElement(element, ref issues);
            }

            Debug.Log($"[FaaSceneDiagnostics] HUDControl stack: controller='{GetPath(controller.gameObject)}', " +
                      $"registered={registeredElements.Count}, activeUGUI={activeElements.Count}, " +
                      $"elements=[{string.Join(", ", activeElements.Select(component => component.GetType().Name))}].");
        }

        private static List<Component> GetRegisteredHudElements(
            SerializedObject serializedController,
            System.Type elementBaseType,
            ref int issues,
            Component controller)
        {
            List<Component> registeredElements = new List<Component>();
            SerializedProperty elements = serializedController.FindProperty("elements");
            Require(elements != null, "HUDController serialized elements list is missing.", ref issues, controller);
            if (elements == null)
            {
                return registeredElements;
            }

            for (int i = 0; i < elements.arraySize; i++)
            {
                SerializedProperty item = elements.GetArrayElementAtIndex(i);
                Component component = item.objectReferenceValue as Component;
                Require(component != null, $"HUDController elements[{i}] is empty or not a Component.", ref issues, controller);
                if (component == null)
                {
                    continue;
                }

                Require(elementBaseType.IsAssignableFrom(component.GetType()),
                    $"HUDController elements[{i}] is not a HUDElementBase: {component.GetType().FullName}.",
                    ref issues,
                    component);
                registeredElements.Add(component);
            }

            return registeredElements;
        }

        private static void ValidateRequiredHudElementPresence(List<Component> activeElements, ref int issues, Component controller)
        {
            Require(HasElement(activeElements, "HUDControl.Elements.AirspeedIndicatorElement"),
                "Missing active uGUI AirspeedIndicatorElement in HUDController.", ref issues, controller);
            Require(HasElement(activeElements, "HUDControl.Elements.AltimeterElement"),
                "Missing active uGUI AltimeterElement in HUDController.", ref issues, controller);
            Require(HasElement(activeElements, "HUDControl.Elements.VSIElement"),
                "Missing active uGUI VSIElement in HUDController.", ref issues, controller);
            Require(HasElement(activeElements, "HUDControl.Elements.AttitudeIndicatorElement"),
                "Missing active uGUI AttitudeIndicatorElement in HUDController.", ref issues, controller);
            Require(HasElement(activeElements, "HUDControl.Elements.FPVElement"),
                "Missing active uGUI FPVElement in HUDController.", ref issues, controller);
            Require(HasElement(activeElements, "HUDControl.Elements.BankScaleElement"),
                "Missing active uGUI BankScaleElement in HUDController.", ref issues, controller);
            Require(HasElement(activeElements, "HUDControl.Elements.TorquePanelElement"),
                "Missing active uGUI TorquePanelElement in HUDController.", ref issues, controller);
            Require(HasElement(activeElements, "HUDControl.Elements.HeadingIndicatorElement") ||
                    HasElement(activeElements, "HUDControl.CompassBar.CompassBarElement"),
                "Missing active uGUI heading tape element in HUDController.", ref issues, controller);
        }

        private static bool HasElement(List<Component> activeElements, string fullTypeName)
        {
            return activeElements.Any(component => component != null && component.GetType().FullName == fullTypeName);
        }

        private static void ValidateHudControlElement(Component element, ref int issues)
        {
            string fullName = element.GetType().FullName;
            switch (fullName)
            {
                case "HUDControl.Elements.AirspeedIndicatorElement":
                    RequireAnyReference(element, new[] { "speedTape", "airspeedReadout" }, "AirspeedIndicatorElement has no visual target.", ref issues);
                    RequireFieldIfEnabled(element, "speedTape", "enableTape", ref issues);
                    RequireFieldIfEnabled(element, "airspeedReadout", "enableReadout", ref issues);
                    break;
                case "HUDControl.Elements.AltimeterElement":
                    RequireAnyReference(element, new[] { "altitudeTape", "altitudeReadout" }, "AltimeterElement has no visual target.", ref issues);
                    RequireFieldIfEnabled(element, "altitudeTape", "enableTape", ref issues);
                    RequireFieldIfEnabled(element, "altitudeReadout", "enableReadout", ref issues);
                    break;
                case "HUDControl.Elements.VSIElement":
                    RequireAnyReference(element, new[] { "vsiPointer", "vsiTape", "digitalReadout" }, "VSIElement has no visual target.", ref issues);
                    RequireFieldIfEnabled(element, "vsiPointer", "enablePointer", ref issues);
                    RequireFieldIfEnabled(element, "vsiTape", "enableTape", ref issues);
                    RequireFieldIfEnabled(element, "digitalReadout", "enableReadout", ref issues);
                    break;
                case "HUDControl.Elements.AttitudeIndicatorElement":
                    RequireAnyReference(element, new[] { "pitchLadder", "miniatureAircraft", "fpvMarker" }, "AttitudeIndicatorElement has no visual target.", ref issues);
                    RequireFieldIfAnyEnabled(element, "pitchLadder", new[] { "enablePitch", "enableRoll" }, ref issues);
                    RequireFieldIfEnabled(element, "fpvMarker", "enableFPV", ref issues);
                    break;
                case "HUDControl.Elements.BankScaleElement":
                    RequireAnyReference(element, new[] { "bankScale", "bankScaleIP", "rollPointer", "slipSlider" }, "BankScaleElement has no visual target.", ref issues);
                    if (GetBoolField(element, "enableBankRotation", true) && GetBoolField(element, "rotateScale", false))
                    {
                        RequireField(element, "bankScale", ref issues);
                    }

                    if (GetBoolField(element, "enablePointerRotation", false) && !GetBoolField(element, "rotateScale", false))
                    {
                        RequireField(element, "rollPointer", ref issues);
                    }

                    RequireFieldIfEnabled(element, "bankScaleIP", "enableBankScaleIPRotation", ref issues);
                    RequireFieldIfEnabled(element, "slipSlider", "enableSlip", ref issues);
                    break;
                case "HUDControl.Elements.TorquePanelElement":
                    RequireAnyReference(element, new[] { "torquePointerL", "torquePointerR" }, "TorquePanelElement has no torque pointer target.", ref issues);
                    break;
                case "HUDControl.Elements.NRIndicatorElement":
                    RequireAnyReference(element, new[] { "rpmCenterPointer", "rpmPointerL", "rpmPointerR" }, "NRIndicatorElement has no RPM pointer target.", ref issues);
                    break;
                case "HUDControl.Elements.GlidescopeElement":
                    RequireFieldIfEnabled(element, "glidescopeNeedle", "enableGS", ref issues);
                    break;
                case "HUDControl.Elements.LocalizerElement":
                    RequireFieldIfEnabled(element, "cdiNeedle", "enableCDI", ref issues);
                    break;
                case "HUDControl.Elements.HeadingIndicatorElement":
                    RequireAnyReference(element, new[] { "compassTape", "headingReadout" }, "HeadingIndicatorElement has no visual target.", ref issues);
                    RequireFieldIfEnabled(element, "compassTape", "enableCompass", ref issues);
                    RequireFieldIfEnabled(element, "headingReadout", "enableReadout", ref issues);
                    break;
                case "HUDControl.CompassBar.CompassBarElement":
                    RequireAnyReference(element, new[] { "compassTape", "headingReadout" }, "CompassBarElement has no visual target.", ref issues);
                    RequireFieldIfEnabled(element, "compassTape", "enableTapeScroll", ref issues);
                    RequireFieldIfEnabled(element, "headingReadout", "enableReadout", ref issues);
                    break;
                case "HUDControl.Elements.FPVElement":
                    Require(element.GetComponent<RectTransform>() != null, "FPVElement is missing RectTransform fallback target.", ref issues, element);
                    break;
            }
        }

        private static void ValidateModeSwitcher(ref int issues)
        {
            Component switcher = FindSceneObjects(FindType("FAA.HUDToolkit.FaaHudModeSwitcher"))
                .FirstOrDefault();
            Require(switcher != null, "Missing FAA HUD Mode Switcher.", ref issues, switcher);
            if (switcher == null)
            {
                return;
            }

            object activeMode = switcher.GetType().GetProperty("ActiveMode")?.GetValue(switcher);
            Require(activeMode != null && activeMode.ToString() == "LegacyUGUI",
                $"FAA HUD Mode Switcher active mode is {activeMode}; expected LegacyUGUI.",
                ref issues,
                switcher);

            SerializedObject serializedSwitcher = new SerializedObject(switcher);
            RequireObjectReference(serializedSwitcher, "legacyHudRoot", "FAA HUD Mode Switcher missing legacyHudRoot.", ref issues, switcher);
            RequireObjectReference(serializedSwitcher, "uiToolkitHud", "FAA HUD Mode Switcher missing uiToolkitHud.", ref issues, switcher);
            RequireObjectReference(serializedSwitcher, "legacyHudSanitizer", "FAA HUD Mode Switcher missing legacyHudSanitizer.", ref issues, switcher);
        }

        private static void ValidateUiToolkitAlternative(ref int issues)
        {
            Component toolkitHud = FindSceneObjects(FindType("FAA.HUDToolkit.FaaUiToolkitHud"))
                .FirstOrDefault();
            Require(toolkitHud != null, "Missing secondary FAA UI Toolkit HUD.", ref issues, toolkitHud);
            if (toolkitHud == null)
            {
                return;
            }

            object isVisible = toolkitHud.GetType().GetProperty("IsVisible")?.GetValue(toolkitHud);
            Require(isVisible is bool visible && !visible,
                $"FAA UI Toolkit HUD is visible by default ({isVisible}); expected hidden secondary HUD.",
                ref issues,
                toolkitHud);
        }

        private static void ValidateHudModeState(bool expectLegacyVisible, ref int issues)
        {
            GameObject legacyCanvas = FindLoadedSceneGameObject("FAASymbologyCanvas");
            Component toolkitHud = FindSceneObjects(FindType("FAA.HUDToolkit.FaaUiToolkitHud"))
                .FirstOrDefault(IsLoadedSceneComponent);
            object isToolkitVisible = toolkitHud?.GetType().GetProperty("IsVisible")?.GetValue(toolkitHud);

            Require(legacyCanvas != null, "Missing FAASymbologyCanvas while validating HUD mode switching.", ref issues, null);
            if (legacyCanvas != null)
            {
                bool legacyVisible = legacyCanvas.activeSelf && legacyCanvas.activeInHierarchy;
                Require(legacyVisible == expectLegacyVisible,
                    $"FAASymbologyCanvas visibility is {legacyVisible}; expected {expectLegacyVisible}.",
                    ref issues,
                    legacyCanvas);
            }

            Require(toolkitHud != null, "Missing FAA UI Toolkit HUD while validating HUD mode switching.", ref issues, null);
            if (toolkitHud != null)
            {
                bool expectedToolkitVisible = !expectLegacyVisible;
                Require(isToolkitVisible is bool visible && visible == expectedToolkitVisible,
                    $"FAA UI Toolkit HUD visibility is {isToolkitVisible}; expected {expectedToolkitVisible}.",
                    ref issues,
                    toolkitHud);
            }
        }

        private static void ValidateBridge(ref int issues)
        {
            Component bridge = FindSceneObjects(FindType("FAA.XPlaneIntegration.Runtime.XPlane12ApiHudBridge"))
                .FirstOrDefault();
            Require(bridge != null, "Missing X-Plane 12 API HUD Bridge.", ref issues, bridge);
            if (bridge == null)
            {
                return;
            }

            object latestFlightData = bridge.GetType().GetProperty("LatestFlightData")?.GetValue(bridge);
            object running = bridge.GetType().GetProperty("IsRunning")?.GetValue(bridge);
            object healthy = bridge.GetType().GetProperty("IsFeedHealthy")?.GetValue(bridge);
            object lastAge = bridge.GetType().GetProperty("LastPacketAgeSeconds")?.GetValue(bridge);
            object traffic = bridge.GetType().GetProperty("TrafficCount")?.GetValue(bridge);

            Debug.Log($"[FaaSceneDiagnostics] X-Plane bridge status: running={running}, healthy={healthy}, " +
                      $"lastPacketAge={lastAge}, traffic={traffic}, hasFlightData={latestFlightData != null}.");

            if (EditorApplication.isPlaying)
            {
                Require(running is bool runningValue && runningValue, "X-Plane bridge is not running in Play Mode.", ref issues, bridge);
                Require(healthy is bool healthyValue && healthyValue, "X-Plane bridge feed is not healthy in Play Mode.", ref issues, bridge);
                Require(latestFlightData != null, "X-Plane bridge has not produced flight data in Play Mode.", ref issues, bridge);
                ValidateRuntimeHudDataValues(latestFlightData, ref issues, bridge);
                if (lastAge is float age)
                {
                    float staleAfterSeconds = GetNumericMember(bridge, "staleAfterSeconds");
                    float maxAllowedAge = IsFinite(staleAfterSeconds) && staleAfterSeconds > 0f
                        ? Mathf.Max(2f, staleAfterSeconds)
                        : 2f;
                    Require(!float.IsNaN(age) && !float.IsInfinity(age) && age <= maxAllowedAge,
                        $"X-Plane packet age is too high: {age:F2}s (limit {maxAllowedAge:F2}s).",
                        ref issues,
                        bridge);
                }
            }
        }

        private static void ValidateRuntimeHudDataValues(object latestFlightData, ref int issues, Component bridge)
        {
            if (latestFlightData == null)
            {
                return;
            }

            float bridgeIas = GetNumericMember(latestFlightData, "indicatedAirspeed");
            float bridgeAlt = GetNumericMember(latestFlightData, "altitudeMSL");
            float bridgeHeading = GetNumericMember(latestFlightData, "heading");

            Component hudController = FindSceneObjects(FindType("HUDControl.Core.HUDController"))
                .FirstOrDefault(IsLoadedSceneComponent);
            object currentState = hudController?.GetType().GetProperty("CurrentState")?.GetValue(hudController);
            float stateIas = GetNumericMember(currentState, "IndicatedAirspeedKnots");
            float stateAlt = GetNumericMember(currentState, "AltitudeFeet");
            float stateHeading = GetNumericMember(currentState, "Heading");

            Component airspeedElement = FindRegisteredHudElement(hudController, "HUDControl.Elements.AirspeedIndicatorElement", "GetDisplayedAirspeed")
                ?? FindSceneObjects(FindType("HUDControl.Elements.AirspeedIndicatorElement"))
                .FirstOrDefault(IsPreferredLegacyHudComponent);
            Component altimeterElement = FindRegisteredHudElement(hudController, "HUDControl.Elements.AltimeterElement", "GetDisplayedAltitude")
                ?? FindSceneObjects(FindType("HUDControl.Elements.AltimeterElement"))
                .FirstOrDefault(IsPreferredLegacyHudComponent);
            float displayedIas = InvokeNumericMethod(airspeedElement, "GetDisplayedAirspeed");
            float displayedAlt = InvokeNumericMethod(altimeterElement, "GetDisplayedAltitude");

            Debug.Log($"[FaaSceneDiagnostics] Runtime HUD data: bridgeIAS={bridgeIas:F1}, stateIAS={stateIas:F1}, " +
                      $"displayIAS={displayedIas:F1}, bridgeALT={bridgeAlt:F1}, stateALT={stateAlt:F1}, " +
                      $"displayALT={displayedAlt:F1}, bridgeHDG={bridgeHeading:F1}, stateHDG={stateHeading:F1}, " +
                      $"airspeedElement='{(airspeedElement != null ? GetPath(airspeedElement.gameObject) : "<null>")}', " +
                      $"altimeterElement='{(altimeterElement != null ? GetPath(altimeterElement.gameObject) : "<null>")}'.");

            if (IsFinite(bridgeIas) && bridgeIas > 5f)
            {
                Require(IsFinite(stateIas) && Mathf.Abs(stateIas - bridgeIas) <= 20f,
                    $"HUD AircraftController IAS is not tracking X-Plane data. Bridge={bridgeIas:F1}, state={stateIas:F1}.",
                    ref issues,
                    hudController != null ? hudController : bridge);
                Require(IsFinite(displayedIas) && displayedIas > 5f,
                    $"AirspeedIndicatorElement display is not receiving runtime data. Display={displayedIas:F1}, bridge={bridgeIas:F1}.",
                    ref issues,
                    airspeedElement != null ? airspeedElement : bridge);
            }

            if (IsFinite(bridgeAlt) && bridgeAlt > 100f)
            {
                Require(IsFinite(stateAlt) && Mathf.Abs(stateAlt - bridgeAlt) <= 300f,
                    $"HUD AircraftController altitude is not tracking X-Plane data. Bridge={bridgeAlt:F1}, state={stateAlt:F1}.",
                    ref issues,
                    hudController != null ? hudController : bridge);
                Require(IsFinite(displayedAlt) && displayedAlt > 100f,
                    $"AltimeterElement display is not receiving runtime data. Display={displayedAlt:F1}, bridge={bridgeAlt:F1}.",
                    ref issues,
                    altimeterElement != null ? altimeterElement : bridge);
            }

            if (IsFinite(bridgeHeading) && IsFinite(stateHeading))
            {
                Require(Mathf.Abs(Mathf.DeltaAngle(stateHeading, bridgeHeading)) <= 15f,
                    $"HUD AircraftController heading is not tracking X-Plane data. Bridge={bridgeHeading:F1}, state={stateHeading:F1}.",
                    ref issues,
                    hudController != null ? hudController : bridge);
            }
        }

        private static Component FindRegisteredHudElement(Component hudController, string fullTypeName, string numericMethodName)
        {
            if (hudController == null)
            {
                return null;
            }

            SerializedProperty elements = new SerializedObject(hudController).FindProperty("elements");
            if (elements == null)
            {
                return null;
            }

            Component bestComponent = null;
            float bestValue = float.NegativeInfinity;

            for (int i = 0; i < elements.arraySize; i++)
            {
                Component component = elements.GetArrayElementAtIndex(i).objectReferenceValue as Component;
                if (component == null ||
                    component.GetType().FullName != fullTypeName ||
                    !IsPreferredLegacyHudComponent(component))
                {
                    continue;
                }

                float value = InvokeNumericMethod(component, numericMethodName);
                if (bestComponent == null || (IsFinite(value) && value > bestValue))
                {
                    bestComponent = component;
                    bestValue = value;
                }
            }

            return bestComponent;
        }

        private static void ValidateRuntimeSanitizer(ref int issues)
        {
            Component sanitizer = FindSceneObjects(FindType("FAA.Customization.FaaHudRuntimeSanitizer"))
                .FirstOrDefault(IsLoadedSceneComponent);
            Require(sanitizer != null, "Missing FAA HUD Runtime Sanitizer.", ref issues, null);
            if (sanitizer == null)
            {
                return;
            }

            Behaviour behaviour = sanitizer as Behaviour;
            Require(behaviour == null || behaviour.enabled, "FAA HUD Runtime Sanitizer is disabled.", ref issues, sanitizer);
            Require(sanitizer.gameObject.activeInHierarchy, "FAA HUD Runtime Sanitizer GameObject is inactive.", ref issues, sanitizer);
        }

        private static void RequireBool(SerializedObject serializedObject, string propertyName, bool expected, string message, ref int issues, UnityEngine.Object context)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            Require(property.boolValue == expected, message, ref issues, context);
        }

        private static void RequireObjectReference(SerializedObject serializedObject, string propertyName, string message, ref int issues, UnityEngine.Object context)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            Require(property.objectReferenceValue != null, message, ref issues, context);
        }

        private static void RequireAnyReference(Component component, string[] fieldNames, string message, ref int issues)
        {
            bool hasAny = fieldNames.Any(fieldName => GetUnityObjectField(component, fieldName) != null);
            Require(hasAny, $"{component.GetType().Name} '{GetPath(component.gameObject)}': {message}", ref issues, component);
        }

        private static void RequireFieldIfEnabled(Component component, string fieldName, string gateFieldName, ref int issues)
        {
            if (!GetBoolField(component, gateFieldName, true))
            {
                return;
            }

            RequireField(component, fieldName, ref issues);
        }

        private static void RequireFieldIfAnyEnabled(Component component, string fieldName, string[] gateFieldNames, ref int issues)
        {
            bool requiresField = gateFieldNames.Any(gateFieldName => GetBoolField(component, gateFieldName, false));
            if (!requiresField)
            {
                return;
            }

            RequireField(component, fieldName, ref issues);
        }

        private static void RequireField(Component component, string fieldName, ref int issues)
        {
            Require(GetUnityObjectField(component, fieldName) != null,
                $"{component.GetType().Name} '{GetPath(component.gameObject)}' has required field '{fieldName}' unassigned.",
                ref issues,
                component);
        }

        private static UnityEngine.Object GetUnityObjectField(Component component, string fieldName)
        {
            FieldInfo field = GetField(component.GetType(), fieldName);
            return field?.GetValue(component) as UnityEngine.Object;
        }

        private static bool GetBoolField(Component component, string fieldName, bool fallback)
        {
            FieldInfo field = GetField(component.GetType(), fieldName);
            return field != null && field.FieldType == typeof(bool)
                ? (bool)field.GetValue(component)
                : fallback;
        }

        private static float GetNumericMember(object target, string memberName)
        {
            if (target == null)
            {
                return float.NaN;
            }

            System.Type type = target.GetType();
            FieldInfo field = GetField(type, memberName);
            object value = field != null
                ? field.GetValue(target)
                : type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target);
            return ConvertToFloat(value);
        }

        private static float InvokeNumericMethod(Component component, string methodName)
        {
            if (component == null)
            {
                return float.NaN;
            }

            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return ConvertToFloat(method?.Invoke(component, null));
        }

        private static float ConvertToFloat(object value)
        {
            if (value == null)
            {
                return float.NaN;
            }

            try
            {
                return System.Convert.ToSingle(value);
            }
            catch
            {
                return float.NaN;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsRenderableGraphic(Graphic graphic)
        {
            return graphic != null &&
                   graphic.enabled &&
                   graphic.gameObject.activeInHierarchy &&
                   graphic.color.a > 0.01f;
        }

        private static bool IsLargeBlankOpaqueGraphic(Graphic graphic)
        {
            if (!IsRenderableGraphic(graphic) || graphic.color.a < 0.85f)
            {
                return false;
            }

            bool isBlankImage = graphic is Image image && image.sprite == null;
            bool isBlankRawImage = graphic is RawImage rawImage && rawImage.texture == null;
            if (!isBlankImage && !isBlankRawImage)
            {
                return false;
            }

            RectTransform rectTransform = graphic.rectTransform;
            if (rectTransform == null)
            {
                return false;
            }

            Rect rect = rectTransform.rect;
            float width = Mathf.Abs(rect.width * rectTransform.lossyScale.x);
            float height = Mathf.Abs(rect.height * rectTransform.lossyScale.y);
            if (width < 120f || height < 120f)
            {
                return false;
            }

            Color color = graphic.color;
            return color.r >= 0.85f && color.g >= 0.85f && color.b >= 0.85f;
        }

        private static FieldInfo GetField(System.Type type, string fieldName)
        {
            for (System.Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private static bool IsLoadedSceneComponent(Component component)
        {
            return component != null && component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded;
        }

        private static GameObject FindLoadedSceneGameObject(string name)
        {
            foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform != null &&
                    transform.gameObject.name == name &&
                    transform.gameObject.scene.IsValid() &&
                    transform.gameObject.scene.isLoaded)
                {
                    return transform.gameObject;
                }
            }

            return null;
        }

        private static bool IsPreferredLegacyHudComponent(Component component)
        {
            if (!IsLoadedSceneComponent(component))
            {
                return false;
            }

            string lowerPath = GetPath(component.gameObject).ToLowerInvariant();
            return lowerPath.Contains("/faasymbologycanvas/") &&
                   !lowerPath.Contains("/faasymbologycanvasworldspace/");
        }

        private static System.Type FindType(string fullName)
        {
            return System.AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(type => type != null);
        }

        private static Component[] FindSceneObjects(System.Type componentType)
        {
            if (componentType == null)
            {
                return new Component[0];
            }

            return Object.FindObjectsByType(componentType, FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OfType<Component>()
                .Where(component => component != null && component.gameObject.scene.IsValid())
                .ToArray();
        }

        private static T[] FindSceneObjects<T>() where T : Component
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private static void Require(bool condition, string message, ref int issues, Object context)
        {
            if (condition)
            {
                return;
            }

            issues++;
            Debug.LogError($"[FaaSceneDiagnostics] {message}", context);
        }

        private static string FormatColor(Color color)
        {
            return $"({color.r:F3},{color.g:F3},{color.b:F3},{color.a:F3})";
        }

        private static bool IsBuiltInUnityGuid(string guid)
        {
            return guid == "0000000000000000d000000000000000" ||
                   guid == "0000000000000000e000000000000000" ||
                   guid == "0000000000000000f000000000000000";
        }

        private static bool IsBuiltInUnityAssetPath(string path)
        {
            return path == "Library/unity default resources" ||
                   path == "Resources/unity_builtin_extra";
        }

        private static bool ShouldScanSerializedAsset(string path)
        {
            if (string.IsNullOrEmpty(path) || (!path.StartsWith("Assets/") && !path.StartsWith("Packages/")))
            {
                return false;
            }

            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".prefab" ||
                   extension == ".unity" ||
                   extension == ".asset" ||
                   extension == ".controller" ||
                   extension == ".override" ||
                   extension == ".playable" ||
                   extension == ".mat";
        }

        private static string GetPath(GameObject gameObject)
        {
            return string.Join("/", gameObject.GetComponentsInParent<Transform>(true)
                .Reverse()
                .Select(transform => transform.name));
        }
    }
}
#endif
