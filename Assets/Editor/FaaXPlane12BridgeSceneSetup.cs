#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AircraftControl.Core;
using AviationUI;
using FAA.Customization;
using FAA.Geo;
using FAA.XPlaneIntegration.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using WeatherVisualization3D;

namespace FAA.Editor
{
    public static class FaaXPlane12BridgeSceneSetup
    {
        private const string ExperimentScenePath = "Assets/ExperimentScene.unity";
        private const string BridgeObjectName = "X-Plane 12 API HUD Bridge";
        private const string OwnshipObjectName = "X-Plane Ownship";
        private const string ManagersObjectName = "[MANAGERS]";
        private const string UiRootObjectName = "_UI";
        private const string UiToolkitHudObjectName = "FAA UI Toolkit HUD";
        private const string HudModeSwitcherObjectName = "FAA HUD Mode Switcher";
        private const string HudRuntimeSanitizerObjectName = "FAA HUD Runtime Sanitizer";
        private const string PanelSettingsPath = "Assets/_Project/UI/FaaHudPanelSettings.asset";
        private const string ExistingPanelSettingsPath = "Assets/_Project/New Panel Settings.asset";
        private const string ThunderstormScenarioPath = "Assets/_Project/ScriptableObjects/ThunderstormCellsScenario.asset";
        private const float ScreenFlightHudScale = 420f;
        private const int ScreenFlightHudSortingOrder = 5000;

        [MenuItem("FAA/X-Plane 12/Configure API HUD Bridge In Experiment Scene")]
        public static void ConfigureExperimentScene()
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ExperimentScenePath)))
            {
                Debug.LogError($"[FaaXPlane12BridgeSceneSetup] Scene not found: {ExperimentScenePath}");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.path != ExperimentScenePath)
            {
                scene = EditorSceneManager.OpenScene(ExperimentScenePath, OpenSceneMode.Single);
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"[FaaXPlane12BridgeSceneSetup] Failed to load scene: {ExperimentScenePath}");
                return;
            }

            int removedMissingScripts = RemoveMissingScripts(scene);
            GameObject managers = FindOrCreateRoot(ManagersObjectName);
            XPlane12ApiHudBridge bridge = EnsureBridge(managers.transform);
            AviationFlightDataProvider provider = EnsureFlightDataProvider(bridge);
            AircraftController ownship = EnsureOwnship(managers.transform);
            EnsureGeoProjection();

            ConfigureBridge(bridge);
            AssignCameraTargets(ownship.transform);
            ConfigureUniStorm(ownship.transform);
            ConfigureWeatherSimulatorScenario();
            ConfigureSymbologyCustomization();
            EnsureTerrainSync(managers.transform);
            DisableCesiumPhysicsMeshes();
            DisableDuplicateWorldSpaceHud();
            NormalizeLegacyHudOverlays();
            FaaHudRuntimeSanitizer sanitizer = EnsureHudRuntimeSanitizer(managers.transform);
            ConfigureHudControlStack(ownship);
            DisableOwnAircraftRadarBridge();
            EnsureUiToolkitHud(provider, ownship, sanitizer, false);

            bridge.FindDependencies();
            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(ownship);
            EditorUtility.SetDirty(bridge);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);

            Debug.Log(saved
                ? $"[FaaXPlane12BridgeSceneSetup] Configured {BridgeObjectName} in {ExperimentScenePath}. Removed {removedMissingScripts} missing script component(s)."
                : $"[FaaXPlane12BridgeSceneSetup] Failed to save {ExperimentScenePath}.");
        }

        [MenuItem("FAA/X-Plane 12/Configure Live Data Only In Experiment Scene")]
        public static void ConfigureExperimentSceneLiveDataOnly()
        {
            Scene scene = OpenExperimentScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject managers = FindOrCreateRoot(ManagersObjectName);
            XPlane12ApiHudBridge bridge = EnsureBridge(managers.transform);
            AviationFlightDataProvider provider = EnsureFlightDataProvider(bridge);
            AircraftController ownship = EnsureOwnship(managers.transform);
            EnsureGeoProjection();

            ConfigureBridge(bridge);
            AssignCameraTargets(ownship.transform);
            ConfigureUniStorm(ownship.transform);
            ConfigureWeatherSimulatorScenario();
            EnsureTerrainSync(managers.transform);
            DisableCesiumPhysicsMeshes();
            DisableDuplicateWorldSpaceHud();
            NormalizeLegacyHudOverlays();
            FaaHudRuntimeSanitizer sanitizer = EnsureHudRuntimeSanitizer(managers.transform);
            ConfigureHudControlStack(ownship);
            DisableOwnAircraftRadarBridge();
            EnsureUiToolkitHud(provider, ownship, sanitizer, false);

            bridge.FindDependencies();
            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(ownship);
            EditorUtility.SetDirty(bridge);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);

            Debug.Log(saved
                ? $"[FaaXPlane12BridgeSceneSetup] Configured live X-Plane data only in {ExperimentScenePath}."
                : $"[FaaXPlane12BridgeSceneSetup] Failed to save live X-Plane data setup in {ExperimentScenePath}.");
        }

        [MenuItem("FAA/HUD/Create Or Update Secondary UI Toolkit HUD In Experiment Scene")]
        public static void ConfigureUiToolkitHudInExperimentScene()
        {
            Scene scene = OpenExperimentScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject managers = FindOrCreateRoot(ManagersObjectName);
            XPlane12ApiHudBridge bridge = EnsureBridge(managers.transform);
            AviationFlightDataProvider provider = EnsureFlightDataProvider(bridge);
            AircraftController ownship = EnsureOwnship(managers.transform);
            FaaHudRuntimeSanitizer sanitizer = EnsureHudRuntimeSanitizer(managers.transform);
            ConfigureHudControlStack(ownship);
            EnsureUiToolkitHud(provider, ownship, sanitizer, false);

            bridge.FindDependencies();
            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(ownship);
            EditorUtility.SetDirty(bridge);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);

            Debug.Log(saved
                ? $"[FaaXPlane12BridgeSceneSetup] Created or updated secondary UI Toolkit HUD in {ExperimentScenePath}; uGUI remains the default HUD."
                : $"[FaaXPlane12BridgeSceneSetup] Failed to save UI Toolkit HUD setup in {ExperimentScenePath}.");
        }

        private static Scene OpenExperimentScene()
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ExperimentScenePath)))
            {
                Debug.LogError($"[FaaXPlane12BridgeSceneSetup] Scene not found: {ExperimentScenePath}");
                return default;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.path != ExperimentScenePath)
            {
                scene = EditorSceneManager.OpenScene(ExperimentScenePath, OpenSceneMode.Single);
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"[FaaXPlane12BridgeSceneSetup] Failed to load scene: {ExperimentScenePath}");
            }

            return scene;
        }

        private static XPlane12ApiHudBridge EnsureBridge(Transform parent)
        {
            XPlane12ApiHudBridge[] bridges = FindSceneObjects<XPlane12ApiHudBridge>();
            XPlane12ApiHudBridge bridge = bridges.Length > 0 ? bridges[0] : null;

            if (bridge == null)
            {
                GameObject bridgeObject = new GameObject(BridgeObjectName);
                bridge = bridgeObject.AddComponent<XPlane12ApiHudBridge>();
            }

            bridge.gameObject.name = BridgeObjectName;
            bridge.transform.SetParent(parent, false);

            for (int i = 1; i < bridges.Length; i++)
            {
                if (bridges[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(bridges[i].gameObject);
                }
            }

            return bridge;
        }

        private static void ConfigureBridge(XPlane12ApiHudBridge bridge)
        {
            SerializedObject serializedBridge = new SerializedObject(bridge);
            SetString(serializedBridge, "baseUrl", "https://faa.agaii.org/xplane12");
            SetBool(serializedBridge, "autoStartOnPlay", true);
            SetFloat(serializedBridge, "pollIntervalSeconds", 0.1f);
            SetFloat(serializedBridge, "requestTimeoutSeconds", 2f);
            SetBool(serializedBridge, "pollAircraft", true);
            SetBool(serializedBridge, "pollWeather", true);
            SetBool(serializedBridge, "pollSystems", true);
            SetBool(serializedBridge, "pollTraffic", true);
            SetBool(serializedBridge, "pollRenderAssets", false);
            SetBool(serializedBridge, "applyToAviationHud", true);
            SetBool(serializedBridge, "applyToLegacyHud", true);
            SetBool(serializedBridge, "applyToAircraftController", true);
            SetBool(serializedBridge, "applyToTrafficRadar", true);
            SetBool(serializedBridge, "applyToWeatherRadar", true);
            SetBool(serializedBridge, "disableUserControlWhenReceiving", true);
            SetBool(serializedBridge, "disableTrafficApiWhenReceiving", true);
            SetInt(serializedBridge, "transportMode", 1);
            SetString(serializedBridge, "mqttBrokerHost", "127.0.0.1");
            SetInt(serializedBridge, "mqttBrokerPort", 18883);
            SetString(serializedBridge, "mqttSnapshotTopic", "xplane12/snapshot");
            SetString(serializedBridge, "mqttClientId", "FAA-XPlane12-Unity");
            SetString(serializedBridge, "mqttUsername", string.Empty);
            SetString(serializedBridge, "mqttPassword", string.Empty);
            SetBool(serializedBridge, "mqttAutoReconnect", true);
            SetFloat(serializedBridge, "mqttReconnectDelaySeconds", 2f);
            SetInt(serializedBridge, "smoothingStrategy", 1);
            SetBool(serializedBridge, "compensatePacketAge", true);
            SetFloat(serializedBridge, "maxPredictionSeconds", 0.35f);
            SetFloat(serializedBridge, "smoothingResponseRate", 32f);
            SetFloat(serializedBridge, "aggressiveSmoothingResponseRate", 70f);
            SetFloat(serializedBridge, "attitudeSnapDegrees", 35f);
            SetFloat(serializedBridge, "headingSnapDegrees", 60f);
            SetFloat(serializedBridge, "airspeedSnapKnots", 50f);
            SetFloat(serializedBridge, "altitudeSnapFeet", 1000f);
            SetFloat(serializedBridge, "verticalSpeedSnapFpm", 2500f);
            serializedBridge.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AviationFlightDataProvider EnsureFlightDataProvider(XPlane12ApiHudBridge bridge)
        {
            AviationFlightDataProvider[] providers = FindSceneObjects<AviationFlightDataProvider>();

            foreach (AviationFlightDataProvider provider in providers)
            {
                if (provider != null)
                {
                    UnityEngine.Object.DestroyImmediate(provider);
                }
            }

            return bridge.gameObject.AddComponent<AviationFlightDataProvider>();
        }

        private static AircraftController EnsureOwnship(Transform parent)
        {
            AircraftController ownship = FindFirstSceneObject<AircraftController>();
            if (ownship != null)
            {
                return ownship;
            }

            GameObject ownshipObject = GameObject.Find(OwnshipObjectName) ?? new GameObject(OwnshipObjectName);
            ownshipObject.transform.SetParent(parent, false);
            ownship = ownshipObject.GetComponent<AircraftController>();
            if (ownship == null)
            {
                ownship = ownshipObject.AddComponent<AircraftController>();
            }

            return ownship;
        }

        private static GeoPosUnityPosProjectManager EnsureGeoProjection()
        {
            GeoPosUnityPosProjectManager projection = FindFirstSceneObject<GeoPosUnityPosProjectManager>();
            if (projection != null)
            {
                projection.transform.SetParent(null, true);
                return projection;
            }

            GameObject projectionObject = new GameObject("Geo Projection Manager");
            projection = projectionObject.AddComponent<GeoPosUnityPosProjectManager>();
            return projection;
        }

        private static void AssignCameraTargets(Transform target)
        {
            foreach (global::CameraController cameraController in FindSceneObjects<global::CameraController>())
            {
                SerializedObject serializedCamera = new SerializedObject(cameraController);
                SerializedProperty aircraftTransform = serializedCamera.FindProperty("aircraftTransform");
                if (aircraftTransform != null && aircraftTransform.objectReferenceValue == null)
                {
                    aircraftTransform.objectReferenceValue = target;
                    serializedCamera.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(cameraController);
                }
            }
        }

        private static void ConfigureUniStorm(Transform playerTarget)
        {
            Camera mainCamera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            foreach (global::UniStormSystem uniStorm in FindSceneObjects<global::UniStormSystem>())
            {
                SerializedObject serializedUniStorm = new SerializedObject(uniStorm);
                SetInt(serializedUniStorm, "GetPlayerAtRuntime", 1);
                SetInt(serializedUniStorm, "UseRuntimeDelay", 1);
                SetInt(serializedUniStorm, "UseUniStormMenu", 1);

                SerializedProperty playerTransform = serializedUniStorm.FindProperty("PlayerTransform");
                if (playerTransform != null)
                {
                    playerTransform.objectReferenceValue = playerTarget;
                }

                SerializedProperty playerCamera = serializedUniStorm.FindProperty("PlayerCamera");
                if (playerCamera != null)
                {
                    playerCamera.objectReferenceValue = mainCamera;
                }

                serializedUniStorm.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(uniStorm);
            }
        }

        private static void ConfigureWeatherSimulatorScenario()
        {
            WeatherScenarioPreset scenario = AssetDatabase.LoadAssetAtPath<WeatherScenarioPreset>(ThunderstormScenarioPath);
            if (scenario == null)
            {
                Debug.LogWarning($"[FaaXPlane12BridgeSceneSetup] Weather scenario asset not found: {ThunderstormScenarioPath}");
                return;
            }

            foreach (WeatherSimulator simulator in FindSceneObjects<WeatherSimulator>())
            {
                SerializedObject serializedSimulator = new SerializedObject(simulator);
                SetObject(serializedSimulator, "activeScenario", scenario);
                SetInt(serializedSimulator, "defaultScenarioType", (int)ScenarioType.ThunderstormCells);
                SetBool(serializedSimulator, "simulationEnabled", true);
                SetBool(serializedSimulator, "showDebugInfo", false);
                serializedSimulator.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(simulator);
            }
        }

        private static void DisableCesiumPhysicsMeshes()
        {
            foreach (Behaviour behaviour in FindSceneObjects<Behaviour>())
            {
                if (behaviour == null || behaviour.GetType().FullName != "CesiumForUnity.Cesium3DTileset")
                {
                    continue;
                }

                SerializedObject serializedBehaviour = new SerializedObject(behaviour);
                SetBool(serializedBehaviour, "_createPhysicsMeshes", false);
                serializedBehaviour.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(behaviour);
            }
        }

        private static void ConfigureSymbologyCustomization()
        {
            string[] excludedPathFragments =
            {
                "radarcanvas",
                "weather radar system",
                "visualunderstanding",
                "analysis trigger buttons",
                "voice",
                "vc",
                "minimap",
                "compassnavigatorpro",
                "panel",
                "button",
                "toggle",
                "header",
                "background",
                "masker",
                "radarreturns",
                "rangerings",
                "sweepline",
                "readoutimage"
            };

            foreach (SymbologyColorManager manager in FindSceneObjects<SymbologyColorManager>())
            {
                SerializedObject serializedManager = new SerializedObject(manager);
                SetInt(serializedManager, "currentPreset", (int)ColorPreset.Green);
                SetBool(serializedManager, "preserveElementAlpha", true);
                SetBool(serializedManager, "tintOnlySymbologyElements", true);
                SetStringList(serializedManager, "excludedPathFragments", excludedPathFragments);
                serializedManager.ApplyModifiedPropertiesWithoutUndo();

                manager.Initialize();
                manager.ApplyColorImmediate(manager.CurrentColor);
                EditorUtility.SetDirty(manager);
            }
        }

        private static void EnsureTerrainSync(Transform parent)
        {
            System.Type syncType = System.AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType("FAA.XPlaneIntegration.Runtime.XPlane12TerrainSync"))
                .FirstOrDefault(type => type != null);
            if (syncType == null)
            {
                Debug.LogWarning("[FaaXPlane12BridgeSceneSetup] XPlane12TerrainSync type was not loaded yet; terrain sync was not added to the scene.");
                return;
            }

            Component syncComponent = UnityEngine.Object.FindObjectsByType(syncType, FindObjectsInactive.Include)
                .OfType<Component>()
                .FirstOrDefault();
            if (syncComponent == null)
            {
                GameObject syncObject = new GameObject("X-Plane 12 Terrain Sync");
                syncComponent = syncObject.AddComponent(syncType);
            }

            syncComponent.transform.SetParent(parent, false);

            SerializedObject serializedSync = new SerializedObject(syncComponent);
            SetBool(serializedSync, "anchorOnStart", true);
            SetBool(serializedSync, "syncGeoProjectionOrigin", true);
            SetBool(serializedSync, "setDefaultPositionToAircraft", true);
            SetBool(serializedSync, "syncCesiumGeoreference", true);
            SetBool(serializedSync, "useAircraftAltitudeForCesium", false);
            SetFloat(serializedSync, "cesiumReferenceHeightMeters", 100f);
            SetFloat(serializedSync, "recenterDistanceKm", 25f);
            serializedSync.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(syncComponent);
        }

        private static void DisableDuplicateWorldSpaceHud()
        {
            foreach (Canvas canvas in FindSceneObjects<Canvas>())
            {
                if (canvas == null || canvas.gameObject.name != "FAASymbologyCanvasWorldSpace")
                {
                    continue;
                }

                canvas.enabled = false;
                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null)
                {
                    raycaster.enabled = false;
                    EditorUtility.SetDirty(raycaster);
                }

                UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                if (scaler != null)
                {
                    scaler.enabled = false;
                    EditorUtility.SetDirty(scaler);
                }

                if (canvas.gameObject.activeSelf)
                {
                    canvas.gameObject.SetActive(false);
                }

                EditorUtility.SetDirty(canvas.gameObject);
                EditorUtility.SetDirty(canvas);
            }

            foreach (Transform transform in FindSceneObjects<Transform>())
            {
                if (transform == null || transform.gameObject.name != "FAASymbologyCanvasWorldSpace")
                {
                    continue;
                }

                if (transform.gameObject.activeSelf)
                {
                    transform.gameObject.SetActive(false);
                    EditorUtility.SetDirty(transform.gameObject);
                }
            }
        }

        private static void NormalizeLegacyHudOverlays()
        {
            foreach (RectTransform rectTransform in FindSceneObjects<RectTransform>())
            {
                if (rectTransform == null)
                {
                    continue;
                }

                string lowerPath = GetHierarchyPath(rectTransform).ToLowerInvariant();
                if (rectTransform.gameObject.name == "FAASymbologyCanvas")
                {
                    rectTransform.localScale = Vector3.one;
                    if (!rectTransform.gameObject.activeSelf)
                    {
                        rectTransform.gameObject.SetActive(true);
                    }

                    Canvas canvas = rectTransform.GetComponent<Canvas>();
                    if (canvas != null)
                    {
                        canvas.enabled = true;
                        EditorUtility.SetDirty(canvas);
                    }

                    UnityEngine.UI.CanvasScaler scaler = rectTransform.GetComponent<UnityEngine.UI.CanvasScaler>();
                    if (scaler != null)
                    {
                        scaler.enabled = true;
                        EditorUtility.SetDirty(scaler);
                    }

                    GraphicRaycaster raycaster = rectTransform.GetComponent<GraphicRaycaster>();
                    if (raycaster != null)
                    {
                        raycaster.enabled = true;
                        EditorUtility.SetDirty(raycaster);
                    }

                    EditorUtility.SetDirty(rectTransform.gameObject);
                    EditorUtility.SetDirty(rectTransform);
                    continue;
                }

                if (rectTransform.gameObject.name == "Second Interation GUI" &&
                    lowerPath.Contains("_ui/faasymbologycanvas/second interation gui"))
                {
                    rectTransform.localScale = Vector3.one * ScreenFlightHudScale;
                    if (!rectTransform.gameObject.activeSelf)
                    {
                        rectTransform.gameObject.SetActive(true);
                    }

                    Canvas canvas = rectTransform.GetComponent<Canvas>();
                    if (canvas != null)
                    {
                        canvas.overrideSorting = true;
                        canvas.sortingOrder = ScreenFlightHudSortingOrder;
                        EditorUtility.SetDirty(canvas);
                    }

                    EditorUtility.SetDirty(rectTransform.gameObject);
                    EditorUtility.SetDirty(rectTransform);
                    continue;
                }

                if (ShouldHideLegacyOverlayGroup(lowerPath) &&
                    rectTransform.gameObject.activeSelf)
                {
                    rectTransform.gameObject.SetActive(false);
                    EditorUtility.SetDirty(rectTransform.gameObject);
                }
            }
        }

        private static FaaHudRuntimeSanitizer EnsureHudRuntimeSanitizer(Transform parent)
        {
            FaaHudRuntimeSanitizer sanitizer = FindFirstSceneObject<FaaHudRuntimeSanitizer>();
            if (sanitizer == null)
            {
                GameObject sanitizerObject = new GameObject(HudRuntimeSanitizerObjectName);
                sanitizer = sanitizerObject.AddComponent<FaaHudRuntimeSanitizer>();
            }

            sanitizer.gameObject.name = HudRuntimeSanitizerObjectName;
            sanitizer.transform.SetParent(parent, false);
            sanitizer.enabled = true;

            SerializedObject serializedSanitizer = new SerializedObject(sanitizer);
            SetBool(serializedSanitizer, "disableWorldSpaceSymbologyCanvas", true);
            SetBool(serializedSanitizer, "hideLargeBlankHudImages", true);
            SetFloat(serializedSanitizer, "minimumBlockSize", 48f);
            SetFloat(serializedSanitizer, "minimumEffectiveBlockSize", 120f);
            SetBool(serializedSanitizer, "enforceScreenFlightHudLayout", true);
            SetFloat(serializedSanitizer, "screenFlightHudScale", ScreenFlightHudScale);
            SetInt(serializedSanitizer, "screenFlightHudSortingOrder", ScreenFlightHudSortingOrder);
            SetBool(serializedSanitizer, "hideLegacyOverlayGroups", true);
            SetInt(serializedSanitizer, "initialFrameScans", 240);
            SetFloat(serializedSanitizer, "rescanIntervalSeconds", 0.5f);
            serializedSanitizer.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(sanitizer);
            EditorUtility.SetDirty(sanitizer.gameObject);
            return sanitizer;
        }

        private static void ConfigureHudControlStack(AircraftController ownship)
        {
            Type controllerType = FindType("HUDControl.Core.HUDController");
            Type elementBaseType = FindType("HUDControl.Core.HUDElementBase");
            if (controllerType == null || elementBaseType == null)
            {
                Debug.LogWarning("[FaaXPlane12BridgeSceneSetup] HUDControl scripts are not loaded yet. Refresh/compile scripts, then run this setup again.");
                return;
            }

            Component controller = FindFirstSceneObject(controllerType);
            GameObject legacyHudRoot = FindLegacyHudRoot();
            if (controller == null || legacyHudRoot == null)
            {
                Debug.LogWarning("[FaaXPlane12BridgeSceneSetup] Cannot configure HUDControl stack because the HUDController or legacy HUD root is missing.");
                return;
            }

            EnsurePrimaryTorquePanelElement(legacyHudRoot);

            Component[] candidates = legacyHudRoot
                .GetComponentsInChildren(elementBaseType, true)
                .OfType<Component>()
                .OrderBy(component => GetHierarchyPath(component.transform))
                .ToArray();

            List<Component> registeredElements = new List<Component>();
            foreach (Component element in candidates)
            {
                if (element == null)
                {
                    continue;
                }

                bool hasTarget = NormalizeHudControlElement(element);
                if (hasTarget && element.gameObject.activeInHierarchy)
                {
                    registeredElements.Add(element);
                }
            }

            SerializedObject serializedController = new SerializedObject(controller);
            SetObject(serializedController, "aircraftController", ownship);
            SetBool(serializedController, "autoFindController", true);
            SetBool(serializedController, "updateEveryFrame", true);
            SetBool(serializedController, "enableOnStart", true);
            SetObjectArray(serializedController, "elements", registeredElements);
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            Behaviour controllerBehaviour = controller as Behaviour;
            if (controllerBehaviour != null)
            {
                controllerBehaviour.enabled = true;
            }

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(controller.gameObject);
            Debug.Log($"[FaaXPlane12BridgeSceneSetup] HUDControl stack configured with {registeredElements.Count} active uGUI element(s).");
        }

        private static void EnsurePrimaryTorquePanelElement(GameObject legacyHudRoot)
        {
            Type torquePanelType = FindType("HUDControl.Elements.TorquePanelElement");
            if (torquePanelType == null || legacyHudRoot == null)
            {
                return;
            }

            Transform torquePanel = FindChildByName(legacyHudRoot.transform, "Torque Panel");
            if (torquePanel == null)
            {
                return;
            }

            Component primary = torquePanel.GetComponent(torquePanelType);
            if (primary == null)
            {
                primary = torquePanel.gameObject.AddComponent(torquePanelType);
            }

            RectTransform frame = FindChildByName(torquePanel, "Torque Frame")?.GetComponent<RectTransform>();
            RectTransform leftIndicator = FindChildByName(torquePanel, "Torque Indicator L")?.GetComponent<RectTransform>();
            RectTransform rightIndicator = FindChildByName(torquePanel, "Torque Indicator R")?.GetComponent<RectTransform>();

            SerializedObject serializedPrimary = new SerializedObject(primary);
            SetObject(serializedPrimary, "torqueFrame", frame);
            SetObject(serializedPrimary, "torquePointerL", leftIndicator);
            SetObject(serializedPrimary, "torquePointerR", rightIndicator);
            SetBool(serializedPrimary, "enableAnimation", leftIndicator != null || rightIndicator != null);
            SetBool(serializedPrimary, "simulateFromThrottle", true);
            SetBool(serializedPrimary, "isEnabled", true);
            serializedPrimary.ApplyModifiedPropertiesWithoutUndo();

            Behaviour primaryBehaviour = primary as Behaviour;
            if (primaryBehaviour != null)
            {
                primaryBehaviour.enabled = true;
            }

            foreach (Component duplicate in torquePanel.GetComponentsInChildren(torquePanelType, true).OfType<Component>())
            {
                if (duplicate == null || duplicate == primary)
                {
                    continue;
                }

                SerializedObject serializedDuplicate = new SerializedObject(duplicate);
                SetBool(serializedDuplicate, "isEnabled", false);
                serializedDuplicate.ApplyModifiedPropertiesWithoutUndo();

                Behaviour duplicateBehaviour = duplicate as Behaviour;
                if (duplicateBehaviour != null)
                {
                    duplicateBehaviour.enabled = false;
                }

                EditorUtility.SetDirty(duplicate);
            }

            EditorUtility.SetDirty(primary);
            EditorUtility.SetDirty(primary.gameObject);
        }

        private static bool NormalizeHudControlElement(Component element)
        {
            SerializedObject serializedElement = new SerializedObject(element);
            string fullName = element.GetType().FullName;

            switch (fullName)
            {
                case "HUDControl.Elements.AirspeedIndicatorElement":
                    DisableFeatureIfReferenceMissing(serializedElement, "enableTape", "speedTape");
                    DisableFeatureIfReferenceMissing(serializedElement, "enableReadout", "airspeedReadout");
                    break;
                case "HUDControl.Elements.AltimeterElement":
                    DisableFeatureIfReferenceMissing(serializedElement, "enableTape", "altitudeTape");
                    DisableFeatureIfReferenceMissing(serializedElement, "enableReadout", "altitudeReadout");
                    break;
                case "HUDControl.Elements.VSIElement":
                    DisableFeatureIfReferenceMissing(serializedElement, "enablePointer", "vsiPointer");
                    DisableFeatureIfReferenceMissing(serializedElement, "enableTape", "vsiTape");
                    DisableFeatureIfReferenceMissing(serializedElement, "enableReadout", "digitalReadout");
                    break;
                case "HUDControl.Elements.AttitudeIndicatorElement":
                    DisableFeatureIfReferenceMissing(serializedElement, "enablePitch", "pitchLadder");
                    DisableFeatureIfReferenceMissing(serializedElement, "enableFPV", "fpvMarker");
                    if (GetBool(serializedElement, "enableRoll", false) && GetObjectReference(serializedElement, "pitchLadder") == null)
                    {
                        SetBool(serializedElement, "enableRoll", false);
                    }
                    break;
                case "HUDControl.Elements.BankScaleElement":
                    DisableFeatureIfReferenceMissing(serializedElement, "enableBankScaleIPRotation", "bankScaleIP");
                    DisableFeatureIfReferenceMissing(serializedElement, "enableSlip", "slipSlider");
                    if (GetBool(serializedElement, "rotateScale", false))
                    {
                        DisableFeatureIfReferenceMissing(serializedElement, "enableBankRotation", "bankScale");
                    }
                    else
                    {
                        DisableFeatureIfReferenceMissing(serializedElement, "enablePointerRotation", "rollPointer");
                    }
                    break;
                case "HUDControl.Elements.GlidescopeElement":
                    DisableFeatureIfReferenceMissing(serializedElement, "enableGS", "glidescopeNeedle");
                    break;
                case "HUDControl.Elements.LocalizerElement":
                    DisableFeatureIfReferenceMissing(serializedElement, "enableCDI", "cdiNeedle");
                    break;
                case "HUDControl.Elements.HeadingIndicatorElement":
                    DisableFeatureIfReferenceMissing(serializedElement, "enableCompass", "compassTape");
                    DisableFeatureIfReferenceMissing(serializedElement, "enableReadout", "headingReadout");
                    break;
                case "HUDControl.CompassBar.CompassBarElement":
                    DisableFeatureIfReferenceMissing(serializedElement, "enableTapeScroll", "compassTape");
                    DisableFeatureIfReferenceMissing(serializedElement, "enableReadout", "headingReadout");
                    break;
            }

            bool hasVisualTarget = HasHudElementVisualTarget(element, serializedElement);
            SetBool(serializedElement, "isEnabled", hasVisualTarget);
            serializedElement.ApplyModifiedPropertiesWithoutUndo();

            Behaviour behaviour = element as Behaviour;
            if (behaviour != null)
            {
                behaviour.enabled = hasVisualTarget;
            }

            EditorUtility.SetDirty(element);
            return hasVisualTarget;
        }

        private static bool HasHudElementVisualTarget(Component element, SerializedObject serializedElement)
        {
            switch (element.GetType().FullName)
            {
                case "HUDControl.Elements.AirspeedIndicatorElement":
                    return HasObjectReference(serializedElement, "speedTape", "airspeedReadout");
                case "HUDControl.Elements.AltimeterElement":
                    return HasObjectReference(serializedElement, "altitudeTape", "altitudeReadout");
                case "HUDControl.Elements.VSIElement":
                    return HasObjectReference(serializedElement, "vsiPointer", "vsiTape", "digitalReadout");
                case "HUDControl.Elements.AttitudeIndicatorElement":
                    return HasObjectReference(serializedElement, "pitchLadder", "miniatureAircraft", "fpvMarker");
                case "HUDControl.Elements.BankScaleElement":
                    return HasObjectReference(serializedElement, "bankScale", "bankScaleIP", "rollPointer", "slipSlider");
                case "HUDControl.Elements.TorquePanelElement":
                    return HasObjectReference(serializedElement, "torquePointerL", "torquePointerR");
                case "HUDControl.Elements.NRIndicatorElement":
                    return HasObjectReference(serializedElement, "rpmCenterPointer", "rpmPointerL", "rpmPointerR");
                case "HUDControl.Elements.GlidescopeElement":
                    return HasObjectReference(serializedElement, "glidescopeNeedle");
                case "HUDControl.Elements.LocalizerElement":
                    return HasObjectReference(serializedElement, "cdiNeedle");
                case "HUDControl.Elements.HeadingIndicatorElement":
                    return HasObjectReference(serializedElement, "compassTape", "headingReadout");
                case "HUDControl.CompassBar.CompassBarElement":
                    return HasObjectReference(serializedElement, "compassTape", "headingReadout");
                case "HUDControl.Elements.FPVElement":
                    return HasObjectReference(serializedElement, "fpvMarker") || element.GetComponent<RectTransform>() != null;
                default:
                    return true;
            }
        }

        private static void DisableFeatureIfReferenceMissing(SerializedObject serializedObject, string featureName, string referenceName)
        {
            SerializedProperty feature = serializedObject.FindProperty(featureName);
            SerializedProperty reference = serializedObject.FindProperty(referenceName);
            if (feature != null && feature.boolValue && reference != null && reference.objectReferenceValue == null)
            {
                feature.boolValue = false;
            }
        }

        private static Component EnsureUiToolkitHud(
            AviationFlightDataProvider provider,
            AircraftController ownship,
            FaaHudRuntimeSanitizer sanitizer,
            bool makeUiToolkitActive)
        {
            Type hudType = FindType("FAA.HUDToolkit.FaaUiToolkitHud");
            Type switcherType = FindType("FAA.HUDToolkit.FaaHudModeSwitcher");
            if (hudType == null || switcherType == null)
            {
                Debug.LogWarning("[FaaXPlane12BridgeSceneSetup] UI Toolkit HUD scripts are not loaded yet. Refresh/compile scripts, then run this setup again.");
                return null;
            }

            Transform uiRoot = FindOrCreateRoot(UiRootObjectName).transform;
            PanelSettings panelSettings = EnsurePanelSettings();

            Component hud = FindFirstSceneObject(hudType);
            if (hud == null)
            {
                GameObject hudObject = new GameObject(UiToolkitHudObjectName);
                hudObject.transform.SetParent(uiRoot, false);
                UIDocument document = hudObject.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                hud = hudObject.AddComponent(hudType);
            }

            hud.gameObject.name = UiToolkitHudObjectName;
            hud.transform.SetParent(uiRoot, false);

            UIDocument uiDocument = hud.GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = hud.gameObject.AddComponent<UIDocument>();
            }

            uiDocument.panelSettings = panelSettings;
            uiDocument.sortingOrder = ScreenFlightHudSortingOrder + 25;
            SerializedObject serializedHud = new SerializedObject(hud);
            SetBool(serializedHud, "visibleOnStart", makeUiToolkitActive);
            serializedHud.ApplyModifiedPropertiesWithoutUndo();
            InvokeIfPresent(hud, "Configure", provider, ownship);
            InvokeIfPresent(hud, "SetVisible", makeUiToolkitActive);

            Component switcher = FindFirstSceneObject(switcherType);
            if (switcher == null)
            {
                GameObject switcherObject = new GameObject(HudModeSwitcherObjectName);
                switcherObject.transform.SetParent(uiRoot, false);
                switcher = switcherObject.AddComponent(switcherType);
            }

            switcher.gameObject.name = HudModeSwitcherObjectName;
            switcher.transform.SetParent(uiRoot, false);

            SerializedObject serializedSwitcher = new SerializedObject(switcher);
            SetInt(serializedSwitcher, "activeMode", makeUiToolkitActive
                ? 1
                : 0);
            SetBool(serializedSwitcher, "applyOnStart", true);
            SetBool(serializedSwitcher, "enableHotkey", true);
            SetInt(serializedSwitcher, "switchKey", (int)KeyCode.F8);
            SetInt(serializedSwitcher, "legacyStartupReassertFrames", 240);
            SetObject(serializedSwitcher, "legacyHudRoot", FindLegacyHudRoot());
            SetObject(serializedSwitcher, "uiToolkitHud", hud);
            SetObject(serializedSwitcher, "legacyHudSanitizer", sanitizer != null ? sanitizer : FindFirstSceneObject<FaaHudRuntimeSanitizer>());
            SetBool(serializedSwitcher, "autoFindTargets", true);
            SetString(serializedSwitcher, "legacyHudName", "Second Interation GUI");
            SetString(serializedSwitcher, "legacyCanvasName", "FAASymbologyCanvas");
            SetStringList(serializedSwitcher, "legacyCanvasNames", new[]
            {
                "FAASymbologyCanvas"
            });
            SetStringList(serializedSwitcher, "suppressedLegacyRootNames", new[]
            {
                "FAASymbologyCanvasWorldSpace",
                "MaskCanvas",
                "RadarCanvas",
                "VisualUnderstanding",
                "VC",
                "[Indicator System]",
                "Analysis Trigger Buttons"
            });
            SetStringList(serializedSwitcher, "overlayNamesToHideInToolkitMode", new[]
            {
                "UI Toolkit Radial Menu (Advanced)"
            });
            serializedSwitcher.ApplyModifiedPropertiesWithoutUndo();
            InvokeSwitcherApplyMode(switcher, makeUiToolkitActive ? 1 : 0);

            EditorUtility.SetDirty(uiDocument);
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(switcher);
            EditorUtility.SetDirty(hud.gameObject);
            EditorUtility.SetDirty(switcher.gameObject);
            return hud;
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(type => type != null);
        }

        private static Component FindFirstSceneObject(Type componentType)
        {
            if (componentType == null)
            {
                return null;
            }

            return UnityEngine.Object.FindObjectsByType(componentType, FindObjectsInactive.Include)
                .OfType<Component>()
                .FirstOrDefault();
        }

        private static void InvokeIfPresent(Component component, string methodName, params object[] args)
        {
            if (component == null)
            {
                return;
            }

            component.GetType()
                .GetMethod(methodName)
                ?.Invoke(component, args);
        }

        private static void InvokeSwitcherApplyMode(Component switcher, int modeValue)
        {
            if (switcher == null)
            {
                return;
            }

            Type enumType = switcher.GetType().GetNestedType("HudMode");
            object mode = enumType != null ? Enum.ToObject(enumType, modeValue) : modeValue;
            switcher.GetType()
                .GetMethod("ApplyMode")
                ?.Invoke(switcher, new[] { mode });
        }

        private static PanelSettings EnsurePanelSettings()
        {
            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings != null)
            {
                return panelSettings;
            }

            panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(ExistingPanelSettingsPath);
            if (panelSettings != null)
            {
                return panelSettings;
            }

            string directory = Path.GetDirectoryName(PanelSettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            return panelSettings;
        }

        private static GameObject FindLegacyHudRoot()
        {
            GameObject fallback = null;
            foreach (Transform transform in FindSceneObjects<Transform>())
            {
                if (transform != null && transform.gameObject.name == "Second Interation GUI")
                {
                    fallback ??= transform.gameObject;
                    string lowerPath = GetHierarchyPath(transform).ToLowerInvariant();
                    if (lowerPath.Contains("/faasymbologycanvas/second interation gui") &&
                        !lowerPath.Contains("/faasymbologycanvasworldspace/"))
                    {
                        return transform.gameObject;
                    }
                }
            }

            return fallback;
        }

        private static bool ShouldHideLegacyOverlayGroup(string lowerPath)
        {
            return lowerPath.Contains("/_ui/faasymbologycanvas/maskcanvas") ||
                   lowerPath.Contains("_ui/faasymbologycanvas/maskcanvas") ||
                   lowerPath.Contains("_ui/faasymbologycanvas/visualunderstanding") ||
                   lowerPath.Contains("_ui/faasymbologycanvas/vc") ||
                   lowerPath.Contains("_ui/faasymbologycanvas/[indicator system]") ||
                   lowerPath.Contains("_ui/faasymbologycanvas/analysis trigger buttons") ||
                   lowerPath.EndsWith("/faasymbologycanvas/radarcanvas/weather radar system/radarpanel") ||
                   lowerPath.EndsWith("/faasymbologycanvas/radarcanvas/weather radar system/controlpanel") ||
                   lowerPath.EndsWith("/faasymbologycanvas/radarcanvas/traffic radar system/radar display") ||
                   lowerPath.EndsWith("/faasymbologycanvas/radarcanvas/traffic range ui") ||
                   lowerPath.EndsWith("/faasymbologycanvas/radarcanvas/traffic radar system/radar display/mapcanvas/map image");
        }

        private static void DisableTrafficRadarVerboseLogging()
        {
            foreach (global::TrafficRadar.Core.TrafficRadarController controller in FindSceneObjects<global::TrafficRadar.Core.TrafficRadarController>())
            {
                SerializedObject serializedController = new SerializedObject(controller);
                SetBool(serializedController, "verboseLogging", false);
                serializedController.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }

            foreach (global::TrafficRadar.TrafficRadarDataManager manager in FindSceneObjects<global::TrafficRadar.TrafficRadarDataManager>())
            {
                SerializedObject serializedManager = new SerializedObject(manager);
                SetBool(serializedManager, "verboseLogging", false);
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
            }

            foreach (global::TrafficRadar.TrafficRadarDisplay display in FindSceneObjects<global::TrafficRadar.TrafficRadarDisplay>())
            {
                SerializedObject serializedDisplay = new SerializedObject(display);
                SetBool(serializedDisplay, "verboseLogging", false);
                serializedDisplay.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(display);
            }
        }

        private static void DisableLegacyWeatherReceivers()
        {
            foreach (global::WeatherDataReceiver receiver in FindSceneObjects<global::WeatherDataReceiver>())
            {
                SerializedObject serializedReceiver = new SerializedObject(receiver);
                SerializedProperty connectOnStart = serializedReceiver.FindProperty("connectOnStart");
                if (connectOnStart != null)
                {
                    connectOnStart.boolValue = false;
                }

                receiver.enabled = false;
                serializedReceiver.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(receiver);
            }
        }

        private static void DisableDeprecatedLiveDataDrivers()
        {
            DisableSceneBehaviours<global::OnlineMapLocationUpdater>();
            DisableSceneBehaviours<global::TrafficGeoPositionUpdater>();
            DisableSceneBehaviours<global::TrafficDataDebugger>();
            DisableSceneBehaviours<global::UnityTrafficEntityManager>();

            foreach (global::MqttTrafficDataManager manager in FindSceneObjects<global::MqttTrafficDataManager>())
            {
                SerializedObject serializedManager = new SerializedObject(manager);
                SetBool(serializedManager, "connectOnEnable", false);
                manager.enabled = false;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
            }

            foreach (global::TrafficDataManager manager in FindSceneObjects<global::TrafficDataManager>())
            {
                if (manager is global::MqttTrafficDataManager)
                {
                    continue;
                }

                SerializedObject serializedManager = new SerializedObject(manager);
                SetBool(serializedManager, "autoStartFetching", false);
                manager.enabled = false;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
            }
        }

        private static void DisableOwnAircraftRadarBridge()
        {
            DisableSceneBehaviours<global::AircraftControl.Integration.OwnAircraftRadarBridge>();
        }

        private static void DisableSceneBehaviours<T>() where T : Behaviour
        {
            foreach (T behaviour in FindSceneObjects<T>())
            {
                behaviour.enabled = false;
                EditorUtility.SetDirty(behaviour);
            }
        }

        private static int RemoveMissingScripts(Scene scene)
        {
            int removed = 0;
            foreach (GameObject gameObject in GetAllGameObjects(scene))
            {
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (missingCount <= 0)
                {
                    continue;
                }

                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
                removed += missingCount;
                EditorUtility.SetDirty(gameObject);
            }

            return removed;
        }

        private static GameObject[] GetAllGameObjects(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .ToArray();
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            GameObject root = GameObject.Find(name);
            return root != null ? root : new GameObject(name);
        }

        private static T FindFirstSceneObject<T>() where T : Component
        {
            return FindSceneObjects<T>().FirstOrDefault();
        }

        private static T[] FindSceneObjects<T>() where T : Component
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        }

        private static Component[] FindSceneObjects(Type componentType)
        {
            if (componentType == null)
            {
                return Array.Empty<Component>();
            }

            return UnityEngine.Object.FindObjectsByType(componentType, FindObjectsInactive.Include)
                .OfType<Component>()
                .ToArray();
        }

        private static void SetString(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static bool GetBool(SerializedObject serializedObject, string propertyName, bool fallback)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null ? property.boolValue : fallback;
        }

        private static UnityEngine.Object GetObjectReference(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue : null;
        }

        private static bool HasObjectReference(SerializedObject serializedObject, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (GetObjectReference(serializedObject, propertyName) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static Transform FindChildByName(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetObjectArray(SerializedObject serializedObject, string propertyName, IReadOnlyList<Component> values)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return;
            }

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void SetStringList(SerializedObject serializedObject, string propertyName, string[] values)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            return string.Join("/", transform.GetComponentsInParent<Transform>(true)
                .Reverse()
                .Select(parent => parent.name));
        }
    }
}
#endif
