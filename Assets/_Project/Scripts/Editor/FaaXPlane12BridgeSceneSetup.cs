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
using IndicatorSystem.Controller;
using IndicatorSystem.Core;
using IndicatorSystem.Integration;
using TrafficRadar.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using TMPro;
using WeatherRadar;

namespace FAA.Editor
{
    public static class FaaXPlane12BridgeSceneSetup
    {
        private const string ExperimentScenePath = "Assets/_Project/Scenes/ExperimentScene.unity";
        private const string SecondIterationGuiPrefabPath = "Assets/Resources/FFA GUI ASSETS/Prefabs/Second Interation GUI.prefab";
        private const string SceneRootObjectName = "FAA_Scene";
        private const string BridgeObjectName = "X-Plane 12 API HUD Bridge";
        private const string XPlaneWeatherRadarRootName = "X-Plane Weather Radar System";
        private const string XPlaneWeatherRadarCanvasName = "XPlaneWeatherRadarCanvas";
        private const string XPlaneTrafficRadarCanvasName = "XPlaneTrafficRadarCanvas";
        private const string TrafficRadarRootName = "Traffic Radar System";
        private const string RadarControlsObjectName = "X-Plane Radar Controls";
        private const string IndicatorSystemObjectName = "[Indicator System]";
        private const string IndicatorCanvasObjectName = "XPlaneWeatherIndicatorCanvas";
        private const string TangTunnelXPlane12ApiBaseUrl = "http://127.0.0.1:12678";
        private const string OwnshipObjectName = "X-Plane Ownship";
        private const string ManagersObjectName = "[MANAGERS]";
        private const string UiRootObjectName = "_UI";
        private const string UiToolkitHudObjectName = "FAA UI Toolkit HUD";
        private const string HudModeSwitcherObjectName = "FAA HUD Mode Switcher";
        private const string HudRuntimeSanitizerObjectName = "FAA HUD Runtime Sanitizer";
        private const string HeadingTapeCanvasName = "FAAHeadingTapeCanvas";
        private const string HeadingTapeOverlayName = "FAA Heading Tape Overlay";
        private const string PanelSettingsPath = "Assets/_Project/UI/FaaHudPanelSettings.asset";
        private const string ExistingPanelSettingsPath = "Assets/_Project/New Panel Settings.asset";
        private const string XPlaneWeatherRadarConfigPath = "Assets/_Project/ScriptableObjects/XPlaneWeatherRadarConfig.asset";
        private const string XPlaneWeatherRadarPreviewPath = "Assets/_Project/Textures/XPlaneWeatherRadarPreview.png";
        private const float ScreenFlightHudScale = 540f;
        private const float WeatherRadarDefaultRangeNM = 160f;
        private static readonly Vector2 WeatherRadarSize = new Vector2(280f, 280f);
        private static readonly Vector2 TrafficRadarSize = new Vector2(296f, 296f);
        private static readonly Vector2 ScreenFlightHudAnchoredPosition = new Vector2(960f, 690f);
        private static readonly Vector2 HeadingTapeAnchoredPosition = new Vector2(-610f, 430f);
        private static readonly Vector2 HeadingTapeSize = new Vector2(600f, 38f);
        private static readonly Color HudGreen = new Color(0.2f, 1f, 0.2f, 1f);
        private static readonly Color HudGreenDim = new Color(0.2f, 1f, 0.2f, 0.74f);
        private const int ScreenFlightHudSortingOrder = 5000;
        // These names identify the authored tape objects removed by the
        // rollback pass. Setup never recreates them or imports a raster tape.
        private const string AirspeedTapeObjectName = "FAA IAS Tape";
        private const string AltimeterTapeObjectName = "FAA ALT Tape";
        private const string LegacyAirspeedTapeObjectName = "Original IAS Tape";
        private const string LegacyAltimeterTapeObjectName = "Original ALT Tape";

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
            ConfigureSymbologyCustomization();
            EnsureTerrainSync(managers.transform);
            DisableCesiumPhysicsMeshes();
            DisableDeprecated3DWeatherSystems();
            RenameDeprecated3DWeatherAssetFolders();
            DisableDuplicateWorldSpaceHud();
            DisableLegacyRadarAndAnalysisOverlays();
            RemoveGeneratedNavigationRepairArtifacts();
            NormalizeLegacyHudOverlays();
            EnsureHeadingTapeOverlay(provider, ownship);
            FaaHudRuntimeSanitizer sanitizer = EnsureHudRuntimeSanitizer(managers.transform);
            ConfigureHudControlStack(ownship);
            DisableOwnAircraftRadarBridge();
            EnsureXPlaneWeatherRadarSystem(bridge);
            EnsureXPlaneTrafficRadarSystem(bridge);
            EnsureXPlaneWeatherIndicatorSystem(bridge);
            EnsureRadarControlsOverlay();
            EnsureUiToolkitHud(provider, ownship, sanitizer, false);
            ConfigureAdvancedRadialMenu();

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

            int removedMissingScripts = RemoveMissingScripts(scene);
            GameObject managers = FindOrCreateRoot(ManagersObjectName);
            XPlane12ApiHudBridge bridge = EnsureBridge(managers.transform);
            AviationFlightDataProvider provider = EnsureFlightDataProvider(bridge);
            AircraftController ownship = EnsureOwnship(managers.transform);
            EnsureGeoProjection();

            ConfigureBridge(bridge);
            AssignCameraTargets(ownship.transform);
            ConfigureUniStorm(ownship.transform);
            EnsureTerrainSync(managers.transform);
            DisableCesiumPhysicsMeshes();
            DisableDeprecated3DWeatherSystems();
            RenameDeprecated3DWeatherAssetFolders();
            DisableDuplicateWorldSpaceHud();
            DisableLegacyRadarAndAnalysisOverlays();
            RemoveGeneratedNavigationRepairArtifacts();
            NormalizeLegacyHudOverlays();
            EnsureHeadingTapeOverlay(provider, ownship);
            FaaHudRuntimeSanitizer sanitizer = EnsureHudRuntimeSanitizer(managers.transform);
            ConfigureHudControlStack(ownship);
            DisableOwnAircraftRadarBridge();
            EnsureXPlaneWeatherRadarSystem(bridge);
            EnsureXPlaneTrafficRadarSystem(bridge);
            EnsureXPlaneWeatherIndicatorSystem(bridge);
            EnsureRadarControlsOverlay();
            EnsureUiToolkitHud(provider, ownship, sanitizer, false);
            ConfigureAdvancedRadialMenu();

            bridge.FindDependencies();
            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(ownship);
            EditorUtility.SetDirty(bridge);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);

            Debug.Log(saved
                ? $"[FaaXPlane12BridgeSceneSetup] Configured live X-Plane data only in {ExperimentScenePath}. Removed {removedMissingScripts} missing script component(s)."
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
            EnsureHeadingTapeOverlay(provider, ownship);
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

        [MenuItem("FAA/X-Plane 12/Repair Live Engine Bars In Experiment Scene")]
        public static void ConfigureLiveEngineBarsInExperimentScene()
        {
            Scene scene = OpenExperimentScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            AircraftController ownship = FindSceneObjects<AircraftController>()
                .FirstOrDefault(controller => controller != null && controller.gameObject.scene == scene);
            if (ownship == null)
            {
                Debug.LogError("[FaaXPlane12BridgeSceneSetup] Cannot repair engine bars because the own-aircraft controller is missing.");
                return;
            }

            ConfigureHudControlStack(ownship);
            RemoveEngineBarScaleLabelsInPrefab();
            XPlane12ApiHudBridge bridge = FindSceneObjects<XPlane12ApiHudBridge>()
                .FirstOrDefault(candidate => candidate != null && candidate.gameObject.scene == scene);
            bridge?.FindDependencies();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FaaXPlane12BridgeSceneSetup] Repaired and registered live X-Plane torque and NR/N2 bars.");
        }

        [MenuItem("FAA/X-Plane 12/Remove Engine Bar Scale Numbers In Scene And Prefab")]
        public static void AuthorEngineBarNumbersInSceneAndPrefab()
        {
            Scene scene = OpenExperimentScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject legacyHudRoot = FindLegacyHudRoot();
            if (legacyHudRoot == null)
            {
                Debug.LogError("[FaaXPlane12BridgeSceneSetup] Cannot author engine bar numbers because the primary HUD root is missing.");
                return;
            }

            RemoveEngineBarScaleLabels(legacyHudRoot);
            // Keep the live value readouts bound to the authored objects while
            // removing only the fixed side-number columns.
            EnsureEngineBarEditorLabels(legacyHudRoot);
            RemoveLegacyInstrumentReadoutRastersInScene();
            EnsurePrimaryTorquePanelElement(legacyHudRoot);
            EnsurePrimaryNrIndicatorElement(legacyHudRoot);
            EditorSceneManager.MarkSceneDirty(scene);
            bool sceneSaved = EditorSceneManager.SaveScene(scene);
            bool prefabSaved = RemoveEngineBarScaleLabelsInPrefab();
            Debug.Log($"[FaaXPlane12BridgeSceneSetup] Removed engine bar scale numbers from scene (saved={sceneSaved}) and reusable prefab (saved={prefabSaved}).");
        }

        [MenuItem("FAA/X-Plane 12/Roll Back Authored IAS And Altimeter Tapes")]
        public static void RollbackAuthoredPilotScalesInSceneAndPrefab()
        {
            Scene scene = OpenExperimentScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject legacyHudRoot = FindLegacyHudRoot();
            if (legacyHudRoot == null)
            {
                Debug.LogError("[FaaXPlane12BridgeSceneSetup] Cannot roll back IAS/ALT tapes because the primary HUD root is missing.");
                return;
            }

            RollbackAuthoredPilotTapeVisuals(legacyHudRoot);
            EnsurePrimaryAirspeedElement(legacyHudRoot);
            EnsurePrimaryAltimeterElement(legacyHudRoot);
            RemoveLegacyInstrumentReadoutRastersInScene();
            EditorSceneManager.MarkSceneDirty(scene);
            bool sceneSaved = EditorSceneManager.SaveScene(scene);
            bool prefabSaved = RollbackAuthoredPilotScalesInPrefab();
            Debug.Log($"[FaaXPlane12BridgeSceneSetup] Rolled back authored IAS/ALT tapes in scene (saved={sceneSaved}) and reusable prefab (saved={prefabSaved}).");
        }

        [MenuItem("FAA/X-Plane 12/Apply Compact Radar Glass In Experiment Scene")]
        public static void ApplyCompactRadarGlassInExperimentScene()
        {
            Scene scene = OpenExperimentScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject managers = FindOrCreateRoot(ManagersObjectName);
            XPlane12ApiHudBridge bridge = EnsureBridge(managers.transform);
            EnsureXPlaneWeatherRadarSystem(bridge);
            EnsureXPlaneTrafficRadarSystem(bridge);
            EnsureHudRuntimeSanitizer(managers.transform);
            EnsureRadarControlsOverlay();
            bridge.FindDependencies();

            EditorUtility.SetDirty(bridge);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log(saved
                ? "[FaaXPlane12BridgeSceneSetup] Applied compact, transparent weather and traffic radar glass."
                : $"[FaaXPlane12BridgeSceneSetup] Failed to save compact radar glass in {ExperimentScenePath}.");
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
            SetString(serializedBridge, "baseUrl", TangTunnelXPlane12ApiBaseUrl);
            SetBool(serializedBridge, "autoStartOnPlay", true);
            SetFloat(serializedBridge, "pollIntervalSeconds", 0.1f);
            SetFloat(serializedBridge, "requestTimeoutSeconds", 2f);
            SetBool(serializedBridge, "pollAircraft", true);
            SetBool(serializedBridge, "pollWeather", true);
            SetBool(serializedBridge, "pollSystems", true);
            SetBool(serializedBridge, "pollTraffic", true);
            // Weather returns are generated from the live X-Plane datarefs on
            // the 4090 stream. Do not fetch or display the native X-Plane
            // raster artifact; keep the render loop only for non-weather
            // manifests/traffic assets.
            SetBool(serializedBridge, "pollRenderAssets", true);
            SetFloat(serializedBridge, "renderAssetPollIntervalSeconds", 1f);
            SetBool(serializedBridge, "publishWeatherDatarefTextureFromStream", true);
            SetFloat(serializedBridge, "streamWeatherTextureIntervalSeconds", 1f);
            SetInt(serializedBridge, "streamWeatherTextureSize", 512);
            SetObject(serializedBridge, "hudController", FindFirstSceneObject(FindType("HUDControl.Core.HUDController")));
            SetBool(serializedBridge, "applyToAviationHud", true);
            SetBool(serializedBridge, "applyToLegacyHud", true);
            SetBool(serializedBridge, "applyToAircraftController", true);
            SetBool(serializedBridge, "applyToTrafficRadar", true);
            SetBool(serializedBridge, "applyToWeatherRadar", true);
            SetBool(serializedBridge, "disableUserControlWhenReceiving", true);
            SetBool(serializedBridge, "disableTrafficApiWhenReceiving", true);
            SetBool(serializedBridge, "allowExternalTrafficFallback", false);
            SetBool(serializedBridge, "treatFreshWeatherTextureAsRadarOn", true);
            SetFloat(serializedBridge, "minimumUnityTerrainClearanceMeters", 120f);
            SetInt(serializedBridge, "transportMode", 0);
            SetString(serializedBridge, "tcpStreamHost", "127.0.0.1");
            SetInt(serializedBridge, "tcpStreamPort", 37212);
            SetString(serializedBridge, "webSocketUrl", "ws://127.0.0.1:37212/v1/stream/ws");
            SetFloat(serializedBridge, "webSocketReconnectDelaySeconds", 0.5f);
            SetInt(serializedBridge, "webSocketReceiveBufferBytes", 262144);
            SetBool(serializedBridge, "webSocketUseMqttFallback", false);
            SetBool(serializedBridge, "webSocketUseHttpFallback", false);
            SetFloat(serializedBridge, "webSocketFallbackAfterSeconds", 1.25f);
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
            SetBool(serializedBridge, "interpolateDisplayBetweenPackets", true);
            SetFloat(serializedBridge, "maxPredictionSeconds", 0.2f);
            SetFloat(serializedBridge, "smoothingResponseRate", 90f);
            SetFloat(serializedBridge, "aggressiveSmoothingResponseRate", 180f);
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
                ConfigureGeoProjection(projection);
                return projection;
            }

            GameObject projectionObject = new GameObject("Geo Projection Manager");
            projection = projectionObject.AddComponent<GeoPosUnityPosProjectManager>();
            ConfigureGeoProjection(projection);
            return projection;
        }

        private static void ConfigureGeoProjection(GeoPosUnityPosProjectManager projection)
        {
            if (projection == null)
            {
                return;
            }

            SerializedObject projectionSo = new SerializedObject(projection);
            SetFloat(projectionSo, "scaleFactor", 111000f);
            SetFloat(projectionSo, "unitsPerMeter", 1f);
            SetFloat(projectionSo, "altitudeExaggeration", 1f);
            SetInt(projectionSo, "altitudeReference", 0);
            SetInt(projectionSo, "projectionType", 0);
            projectionSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(projection);
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
            Camera mainCamera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
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
                uniStorm.enabled = false;
                uniStorm.gameObject.SetActive(false);
                EditorUtility.SetDirty(uniStorm);
                EditorUtility.SetDirty(uniStorm.gameObject);
            }

            SuppressUniStormCloudVisuals();
        }

        private static void SuppressUniStormCloudVisuals()
        {
            foreach (Transform transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform == null)
                {
                    continue;
                }

                string objectName = transform.gameObject.name;
                if (!string.Equals(objectName, "UniStorm Clouds", System.StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(objectName, "UniStorm Clouds (Lightning)", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                transform.gameObject.SetActive(false);

                if (transform.TryGetComponent<MeshRenderer>(out MeshRenderer renderer))
                {
                    renderer.enabled = false;
                    EditorUtility.SetDirty(renderer);
                }

                EditorUtility.SetDirty(transform.gameObject);
            }
        }

        private static void EnsureXPlaneWeatherRadarSystem(XPlane12ApiHudBridge bridge)
        {
            Canvas canvas = EnsureRadarCanvas();
            WeatherRadarConfig config = EnsureXPlaneWeatherRadarConfig();

            GameObject root = FindOrCreateUniqueNamedRoot(
                XPlaneWeatherRadarRootName,
                candidate => candidate.transform.parent == canvas.transform || candidate.activeInHierarchy,
                canvas.transform);
            root.transform.SetParent(canvas.transform, false);
            root.SetActive(true);
            RectTransform rootRect = EnsureRectTransform(root);
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(0f, 0f);
            rootRect.pivot = new Vector2(0f, 0f);
            rootRect.anchoredPosition = new Vector2(28f, 28f);
            rootRect.sizeDelta = WeatherRadarSize;

            WeatherRadarDataProvider dataProvider = root.GetComponent<WeatherRadarDataProvider>() ?? root.AddComponent<WeatherRadarDataProvider>();
            XPlaneOriginalWeatherRadarProvider provider = root.GetComponent<XPlaneOriginalWeatherRadarProvider>() ?? root.AddComponent<XPlaneOriginalWeatherRadarProvider>();
            dataProvider.SetRange(WeatherRadarDefaultRangeNM);

            SerializedObject providerSo = new SerializedObject(provider);
            // Keep the provider as the dataref-backed weather source, but do
            // not request the native X-Plane PNG. The bridge publishes a
            // compact procedural radar texture from the live 4090 snapshot.
            SetString(providerSo, "radarTextureUrl", string.Empty);
            SetBool(providerSo, "preferNativePluginTexture", false);
            SetBool(providerSo, "allowHttpTexturePolling", false);
            SetFloat(providerSo, "rangeNM", WeatherRadarDefaultRangeNM);
            SetFloat(providerSo, "requestTimeoutSeconds", 2f);
            SetBool(providerSo, "cacheBustRequests", true);
            SetBool(providerSo, "acceptAllCertificates", true);
            SetBool(providerSo, "keepLastTextureOnError", true);
            SetBool(providerSo, "autoUpdate", false);
            SetFloat(providerSo, "updateInterval", 5f);
            providerSo.ApplyModifiedPropertiesWithoutUndo();

            WeatherRadarPanel panel = EnsureRadarPanel(root.transform, config, dataProvider, provider, out XPlaneOriginalWeatherRadarDisplay originalDisplay);

            SerializedObject bridgeSo = new SerializedObject(bridge);
            SetObject(bridgeSo, "weatherRadarDataProvider", dataProvider);
            SetObject(bridgeSo, "weatherRadarProvider", provider);
            SetObject(bridgeSo, "xPlaneWeatherRadarDisplay", originalDisplay);
            SetObject(bridgeSo, "weatherImageTarget", originalDisplay != null ? originalDisplay.TargetImage : null);
            SetBool(bridgeSo, "pollRenderAssets", true);
            SetFloat(bridgeSo, "renderAssetPollIntervalSeconds", 1f);
            SetBool(bridgeSo, "refreshWeatherRadarTexture", false);
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();

            if (panel != null)
            {
                EditorUtility.SetDirty(panel);
            }

            EditorUtility.SetDirty(dataProvider);
            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(bridge);
        }

        private static void EnsureXPlaneTrafficRadarSystem(XPlane12ApiHudBridge bridge)
        {
            Canvas canvas = EnsureTrafficRadarCanvas();
            global::TrafficRadar.Core.TrafficRadarController controller = FindPreferredTrafficRadarController();
            if (controller == null)
            {
                Debug.LogWarning("[FaaXPlane12BridgeSceneSetup] No Traffic Radar System was found to place on the right side.");
                return;
            }

            GameObject root = controller.gameObject;
            root.name = TrafficRadarRootName;
            root.transform.SetParent(canvas.transform, false);
            root.SetActive(true);

            RectTransform rootRect = EnsureRectTransform(root);
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(-28f, 28f);
            rootRect.sizeDelta = TrafficRadarSize;
            rootRect.localScale = Vector3.one;

            global::TrafficRadar.TrafficRadarDataManager dataManager =
                root.GetComponentInChildren<global::TrafficRadar.TrafficRadarDataManager>(true);
            global::TrafficRadar.TrafficRadarDisplay display =
                root.GetComponentInChildren<global::TrafficRadar.TrafficRadarDisplay>(true);
            if (display != null)
            {
                display.gameObject.SetActive(true);
                NormalizeTrafficRadarDisplayRoot(root, display);
                SerializedObject displaySo = new SerializedObject(display);
                SetObject(displaySo, "radarController", controller);
                // The sectional chart is presented as a masked, low-opacity
                // context layer in the XR-3 traffic radar.  TrafficRadarDisplay
                // still lets the pilot hide it with CHT/voice controls.
                SetBool(displaySo, "showChartBackground", true);
                SetFloat(displaySo, "chartOpacity", 0.28f);
                SetFloat(displaySo, "chartEdgeSoftness", 0.035f);
                SetBool(displaySo, "enableChartFadeAnimation", true);
                SetFloat(displaySo, "chartFadeDuration", 0.24f);
                SetFloat(displaySo, "chartPositionRetrySeconds", 0.75f);
                SetBool(displaySo, "showRadarBackground", true);
                SetBool(displaySo, "enforceReadablePanelBackground", false);
                SetFloat(displaySo, "minimumPanelBackgroundOpacity", 0f);
                SetFloat(displaySo, "minimumChartBackgroundOpacity", 0f);
                SetBool(displaySo, "preferXPlaneTrafficTexture", false);
                SetBool(displaySo, "hideGeneratedOverlaysWithXPlaneTexture", false);
                SetVector2(displaySo, "xPlaneTextureFallbackSize", new Vector2(420f, 480f));
                SetInt(displaySo, "displaySize", 512);
                SetFloat(displaySo, "rangeNM", 40f);
                SetInt(displaySo, "rangeRingCount", 4);
                SetColor(displaySo, "backgroundColor", new Color(0.004f, 0.055f, 0.06f, 0.34f));
                SetColor(displaySo, "rangeRingColor", new Color(0.18f, 0.9f, 0.84f, 0.58f));
                SetColor(displaySo, "compassMarkingsColor", new Color(0.74f, 1f, 0.95f, 0.88f));
                SetColor(displaySo, "ownAircraftColor", new Color(0.35f, 1f, 0.55f, 1f));
                SetObject(displaySo, "radarImage", EnsureTrafficRadarImage(display));
                SetObject(displaySo, "chartBackgroundImage", EnsureTrafficChartImage(display));
                displaySo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(display);
            }

            SuppressDuplicateTrafficRadarControllers(controller);

            if (dataManager != null)
            {
                SerializedObject managerSo = new SerializedObject(dataManager);
                SetBool(managerSo, "autoStartFetching", false);
                SetBool(managerSo, "suppressAutoStartDisabledWarning", true);
                SetFloat(managerSo, "radiusFilterKm", 80f);
                managerSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dataManager);
            }

            SerializedObject controllerSo = new SerializedObject(controller);
            SetObject(controllerSo, "dataManager", dataManager);
            SetObject(controllerSo, "radarDisplay", display);
            SetFloat(controllerSo, "rangeNM", 40f);
            SetBool(controllerSo, "autoRangeEnabled", true);
            SetBool(controllerSo, "verboseLogging", false);
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject bridgeSo = new SerializedObject(bridge);
            SetObject(bridgeSo, "trafficRadarDataManager", dataManager);
            SetObject(bridgeSo, "trafficRadarController", controller);
            SetObject(bridgeSo, "xPlaneTrafficRadarDisplay", null);
            SetObject(bridgeSo, "trafficImageTarget", null);
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(bridge);
        }

        private static RawImage EnsureTrafficRadarImage(global::TrafficRadar.TrafficRadarDisplay display)
        {
            if (display == null)
            {
                return null;
            }

            RawImage image = display.RadarImage;
            if (image == null)
            {
                Transform existing = FindChildRecursive(display.transform, "Radar Image");
                GameObject imageObject = existing != null
                    ? existing.gameObject
                    : new GameObject("Radar Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                imageObject.transform.SetParent(display.transform, false);
                image = imageObject.GetComponent<RawImage>() ?? imageObject.AddComponent<RawImage>();
            }

            image.gameObject.name = "Radar Image";
            image.gameObject.SetActive(true);
            image.enabled = true;
            // Keep the serialized/edit-mode state transparent. TrafficRadarDisplay
            // supplies its circular generated texture as soon as runtime starts.
            image.texture = null;
            image.color = Color.clear;
            image.material = null;
            image.raycastTarget = false;

            RectTransform imageRect = EnsureRectTransform(image.gameObject);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.sizeDelta = Vector2.zero;
            imageRect.localScale = Vector3.one;
            imageRect.localRotation = Quaternion.identity;

            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(image.gameObject);
            EditorUtility.SetDirty(imageRect);
            return image;
        }

        private static RawImage EnsureTrafficChartImage(global::TrafficRadar.TrafficRadarDisplay display)
        {
            if (display == null)
            {
                return null;
            }

            Transform existing = FindChildRecursive(display.transform, "Chart Background");
            GameObject imageObject = existing != null
                ? existing.gameObject
                : new GameObject("Chart Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(display.transform, false);
            imageObject.name = "Chart Background";
            imageObject.SetActive(true);

            RawImage image = imageObject.GetComponent<RawImage>() ?? imageObject.AddComponent<RawImage>();
            image.enabled = true;
            image.texture = null;
            image.color = Color.white;
            image.raycastTarget = false;

            RectTransform imageRect = EnsureRectTransform(imageObject);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.sizeDelta = Vector2.zero;
            imageRect.localScale = Vector3.one;
            imageRect.localRotation = Quaternion.identity;

            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(imageObject);
            EditorUtility.SetDirty(imageRect);
            return image;
        }

        private static void NormalizeTrafficRadarDisplayRoot(GameObject root, global::TrafficRadar.TrafficRadarDisplay display)
        {
            if (root == null || display == null)
            {
                return;
            }

            RectTransform displayRect = EnsureRectTransform(display.gameObject);
            displayRect.SetParent(root.transform, false);
            displayRect.anchorMin = Vector2.zero;
            displayRect.anchorMax = Vector2.one;
            displayRect.pivot = new Vector2(0.5f, 0.5f);
            displayRect.anchoredPosition = Vector2.zero;
            displayRect.sizeDelta = Vector2.zero;
            displayRect.localScale = Vector3.one;
            displayRect.localRotation = Quaternion.identity;
            display.gameObject.SetActive(true);
            display.enabled = true;
            RestoreTrafficDisplayMask(display.gameObject);
            EnsureTrafficRadarImage(display);
            EditorUtility.SetDirty(displayRect);
            EditorUtility.SetDirty(display.gameObject);
        }

        private static void RestoreTrafficDisplayMask(GameObject displayObject)
        {
            if (displayObject == null)
            {
                return;
            }

            Mask mask = displayObject.GetComponent<Mask>();
            if (mask != null)
            {
                mask.enabled = true;
                mask.showMaskGraphic = false;
                EditorUtility.SetDirty(mask);
            }

            UnityEngine.UI.Image image = displayObject.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                image.color = new Color(0.004f, 0.055f, 0.06f, 0f);
                image.raycastTarget = false;
                EditorUtility.SetDirty(image);
            }
        }

        private static void SuppressDuplicateTrafficRadarControllers(global::TrafficRadar.Core.TrafficRadarController keep)
        {
            if (keep == null)
            {
                return;
            }

            foreach (global::TrafficRadar.Core.TrafficRadarController controller in FindSceneObjects<global::TrafficRadar.Core.TrafficRadarController>())
            {
                if (controller == null || controller == keep || controller.gameObject.name != TrafficRadarRootName)
                {
                    continue;
                }

                string lowerPath = GetHierarchyPath(controller.transform).ToLowerInvariant();
                if (!lowerPath.Contains("/faasymbologycanvas/radarcanvas") &&
                    !lowerPath.Contains("faasymbologycanvasworldspace") &&
                    controller.gameObject.activeInHierarchy)
                {
                    continue;
                }

                DisableTrafficTextureMode(controller.gameObject);
                DisableLegacyOverlayRoot(controller.gameObject);
            }
        }

        private static void DisableTrafficTextureMode(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (global::TrafficRadar.TrafficRadarDisplay display in
                     root.GetComponentsInChildren<global::TrafficRadar.TrafficRadarDisplay>(true))
            {
                if (display == null)
                {
                    continue;
                }

                SerializedObject displaySo = new SerializedObject(display);
                SetBool(displaySo, "preferXPlaneTrafficTexture", false);
                SetBool(displaySo, "hideGeneratedOverlaysWithXPlaneTexture", false);
                displaySo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(display);
            }
        }

        private static void EnsureXPlaneWeatherIndicatorSystem(XPlane12ApiHudBridge bridge)
        {
            if (bridge == null)
            {
                return;
            }

            IndicatorSettings settings = AssetDatabase.LoadAssetAtPath<IndicatorSettings>("Assets/_Project/Scripts/IndicatorSystem/Settings/IndicatorSettings.asset");
            if (settings == null)
            {
                settings = IndicatorSettings.CreateDefault();
                string settingsDirectory = "Assets/_Project/Scripts/IndicatorSystem/Settings";
                if (!Directory.Exists(settingsDirectory))
                {
                    Directory.CreateDirectory(settingsDirectory);
                }
                AssetDatabase.CreateAsset(settings, $"{settingsDirectory}/IndicatorSettings.asset");
            }

            settings.enabled = true;
            settings.maxIndicators = 10;
            settings.showWeatherIndicators = true;
            settings.showTrafficIndicators = true;
            settings.showWaypointIndicators = true;
            settings.edgePadding = 64f;
            settings.globalScale = 1.08f;
            settings.indicatorSize = 36f;
            settings.arrowSize = 39f;
            settings.labelFontSize = 11f;
            settings.distanceFontSize = 10f;
            settings.altitudeFontSize = 11f;
            settings.maxDisplayDistance = 80f;
            settings.minDisplayDistance = 0f;
            settings.enableDistanceScaling = true;
            settings.closeDistanceNM = 3f;
            settings.farDistanceNM = 40f;
            settings.closeDistanceScale = 1.15f;
            settings.farDistanceScale = 0.78f;
            settings.showDistanceLabels = false;
            settings.showAltitudeIndicators = false;
            settings.showTrails = false;
            settings.showNavigationLights = false;
            settings.smoothMovement = false;
            settings.smoothSpeed = 18f;
            settings.pulseHighPriority = true;
            settings.globalOpacity = 1f;
            settings.trafficNormalColor = new Color(0.08f, 1f, 1f, 0.9f);
            settings.trafficAdvisoryColor = new Color(1f, 0.82f, 0.08f, 0.96f);
            settings.trafficResolutionColor = new Color(1f, 0.1f, 0.08f, 1f);
            settings.weatherLightColor = new Color(0.18f, 1f, 0.28f, 1f);
            settings.weatherModerateColor = new Color(1f, 0.9f, 0.12f, 1f);
            settings.weatherHeavyColor = new Color(1f, 0.12f, 0.08f, 1f);
            EditorUtility.SetDirty(settings);

            Transform managersRoot = FindOrCreateRoot(ManagersObjectName).transform;
            IndicatorSystemController controller = FindPreferredIndicatorController(managersRoot);
            if (controller == null)
            {
                GameObject systemObject = new GameObject(IndicatorSystemObjectName);
                systemObject.transform.SetParent(managersRoot, false);
                controller = systemObject.AddComponent<IndicatorSystemController>();
            }

            controller.gameObject.name = IndicatorSystemObjectName;
            controller.gameObject.SetActive(true);
            controller.transform.SetParent(managersRoot, false);
            RemoveDuplicateIndicatorSystems(controller);

            Canvas indicatorCanvas = EnsureIndicatorCanvas();
            Camera mainCamera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();

            SerializedObject controllerSo = new SerializedObject(controller);
            SetObject(controllerSo, "settings", settings);
            SetObject(controllerSo, "targetCanvas", indicatorCanvas);
            SetObject(controllerSo, "targetCamera", mainCamera);
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            WeatherIndicatorBridge weatherBridge = controller.GetComponent<WeatherIndicatorBridge>() ?? controller.gameObject.AddComponent<WeatherIndicatorBridge>();
            SerializedObject weatherSo = new SerializedObject(weatherBridge);
            SetObject(weatherSo, "weatherProvider", FindFirstSceneObject<XPlaneOriginalWeatherRadarProvider>());
            SetObject(weatherSo, "indicatorController", controller);
            SetObject(weatherSo, "positionReference", mainCamera != null ? mainCamera.transform : null);
            SetFloat(weatherSo, "minIntensityThreshold", 0.18f);
            SetInt(weatherSo, "sampleGridSize", 24);
            SetInt(weatherSo, "maxWeatherIndicators", 4);
            SetFloat(weatherSo, "updateInterval", 1.5f);
            SetBool(weatherSo, "requirePoweredRadar", true);
            SetBool(weatherSo, "showPoweredRadarFallback", false);
            SetBool(weatherSo, "useRadarRelativeScreenProjection", true);
            SetFloat(weatherSo, "poweredRadarFallbackDistanceNM", 12f);
            SetFloat(weatherSo, "poweredRadarFallbackRelativeBearing", 35f);
            SetFloat(weatherSo, "indicatorVerticalOffsetMeters", 3f);
            weatherSo.ApplyModifiedPropertiesWithoutUndo();

            TrafficIndicatorBridge trafficBridge = controller.GetComponent<TrafficIndicatorBridge>() ?? controller.gameObject.AddComponent<TrafficIndicatorBridge>();
            if (trafficBridge != null)
            {
                trafficBridge.enabled = true;
                SerializedObject trafficSo = new SerializedObject(trafficBridge);
                SetObject(trafficSo, "trafficRadarController", FindFirstSceneObject<TrafficRadarController>());
                SetObject(trafficSo, "indicatorController", controller);
                SetObject(trafficSo, "positionReference", mainCamera != null ? mainCamera.transform : null);
                SetBool(trafficSo, "syncPositionFromRadar", true);
                SetBool(trafficSo, "useRadarRelativeScreenProjection", true);
                trafficSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(trafficBridge);
            }

            EditorUtility.SetDirty(indicatorCanvas);
            EditorUtility.SetDirty(indicatorCanvas.gameObject);
            EditorUtility.SetDirty(weatherBridge);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(controller.gameObject);
        }

        private static GameObject FindOrCreateUniqueNamedRoot(
            string objectName,
            Func<GameObject, bool> prefer,
            Transform fallbackParent)
        {
            List<GameObject> matches = FindSceneObjects<Transform>()
                .Where(transform => transform != null && transform.gameObject.name == objectName)
                .Select(transform => transform.gameObject)
                .Distinct()
                .ToList();

            GameObject chosen = matches
                .OrderByDescending(candidate => prefer?.Invoke(candidate) == true)
                .ThenByDescending(candidate => candidate.activeInHierarchy)
                .FirstOrDefault();

            if (chosen == null)
            {
                chosen = new GameObject(objectName);
                if (fallbackParent != null)
                {
                    chosen.transform.SetParent(fallbackParent, false);
                }
            }

            foreach (GameObject duplicate in matches)
            {
                if (duplicate == null || duplicate == chosen)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(duplicate);
            }

            return chosen;
        }

        private static IndicatorSystemController FindPreferredIndicatorController(Transform managersRoot)
        {
            return FindSceneObjects<IndicatorSystemController>()
                .Where(controller => controller != null && controller.gameObject.name == IndicatorSystemObjectName)
                .OrderByDescending(controller => managersRoot != null && controller.transform.parent == managersRoot)
                .ThenByDescending(controller => controller.gameObject.activeInHierarchy)
                .FirstOrDefault();
        }

        private static void RemoveDuplicateIndicatorSystems(IndicatorSystemController preferred)
        {
            foreach (IndicatorSystemController duplicate in FindSceneObjects<IndicatorSystemController>())
            {
                if (duplicate == null || duplicate == preferred || duplicate.gameObject.name != IndicatorSystemObjectName)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
            }
        }

        private static Canvas EnsureIndicatorCanvas()
        {
            Canvas canvas = FindSceneObjects<Canvas>()
                .FirstOrDefault(candidate => candidate != null && candidate.gameObject.name == IndicatorCanvasObjectName);

            if (canvas == null)
            {
                GameObject canvasObject = new GameObject(IndicatorCanvasObjectName);
                canvas = canvasObject.AddComponent<Canvas>();
                canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            canvas.gameObject.name = IndicatorCanvasObjectName;
            canvas.gameObject.SetActive(true);
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = ScreenFlightHudSortingOrder + 40;

            UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            }
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            raycaster.enabled = false;

            EditorUtility.SetDirty(scaler);
            EditorUtility.SetDirty(raycaster);
            return canvas;
        }

        private static Canvas EnsureRadarCanvas()
        {
            Canvas canvas = FindSceneObjects<Canvas>()
                .FirstOrDefault(candidate => candidate != null && candidate.gameObject.name == XPlaneWeatherRadarCanvasName);

            if (canvas == null)
            {
                GameObject canvasObject = new GameObject(XPlaneWeatherRadarCanvasName);
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            canvas.gameObject.SetActive(true);
            canvas.gameObject.name = XPlaneWeatherRadarCanvasName;
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = ScreenFlightHudSortingOrder + 25;

            UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            }
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            raycaster.enabled = true;

            EditorUtility.SetDirty(canvas.gameObject);
            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(scaler);
            EditorUtility.SetDirty(raycaster);
            return canvas;
        }

        private static Canvas EnsureTrafficRadarCanvas()
        {
            Canvas canvas = FindSceneObjects<Canvas>()
                .FirstOrDefault(candidate => candidate != null && candidate.gameObject.name == XPlaneTrafficRadarCanvasName);

            if (canvas == null)
            {
                GameObject canvasObject = new GameObject(XPlaneTrafficRadarCanvasName);
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            canvas.gameObject.SetActive(true);
            canvas.gameObject.name = XPlaneTrafficRadarCanvasName;
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = ScreenFlightHudSortingOrder + 80;

            UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            }
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            raycaster.enabled = true;

            EditorUtility.SetDirty(canvas.gameObject);
            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(scaler);
            EditorUtility.SetDirty(raycaster);
            return canvas;
        }

        private static global::TrafficRadar.Core.TrafficRadarController FindPreferredTrafficRadarController()
        {
            return FindSceneObjects<global::TrafficRadar.Core.TrafficRadarController>()
                .Where(controller => controller != null && controller.gameObject.name == TrafficRadarRootName)
                .OrderByDescending(controller => ScoreTrafficRadarController(controller))
                .FirstOrDefault();
        }

        private static int ScoreTrafficRadarController(global::TrafficRadar.Core.TrafficRadarController controller)
        {
            if (controller == null)
            {
                return int.MinValue;
            }

            string lowerPath = GetHierarchyPath(controller.transform).ToLowerInvariant();
            int score = 0;
            if (lowerPath.StartsWith("xplanetrafficradarcanvas/"))
            {
                score += 5000;
            }
            if (lowerPath.Contains("/faasymbologycanvas/radarcanvas") ||
                lowerPath.Contains("faasymbologycanvasworldspace"))
            {
                score -= 2000;
            }
            if (controller.gameObject.activeSelf)
            {
                score += 500;
            }
            if (controller.gameObject.activeInHierarchy)
            {
                score += 500;
            }
            if (HasActiveChildNamed(controller.transform, "Radar Display"))
            {
                score += 250;
            }

            global::TrafficRadar.TrafficRadarDisplay display =
                controller.GetComponentInChildren<global::TrafficRadar.TrafficRadarDisplay>(true);
            if (display != null)
            {
                score += 100;
                RawImage image = display.RadarImage;
                if (image != null)
                {
                    score += 100;
                    if (image.gameObject.activeSelf && image.enabled && image.color.a > 0.01f)
                    {
                        score += 100;
                    }
                }
            }

            return score;
        }

        private static bool HasActiveChildNamed(Transform root, string childName)
        {
            if (root == null)
            {
                return false;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.name == childName && child.gameObject.activeSelf)
                {
                    return true;
                }
            }

            return false;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            foreach (Transform child in root)
            {
                if (child.name == childName)
                {
                    return child;
                }

                Transform match = FindChildRecursive(child, childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static WeatherRadarConfig EnsureXPlaneWeatherRadarConfig()
        {
            WeatherRadarConfig config = AssetDatabase.LoadAssetAtPath<WeatherRadarConfig>(XPlaneWeatherRadarConfigPath);
            if (config == null)
            {
                string directory = Path.GetDirectoryName(XPlaneWeatherRadarConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                config = ScriptableObject.CreateInstance<WeatherRadarConfig>();
                AssetDatabase.CreateAsset(config, XPlaneWeatherRadarConfigPath);
                AssetDatabase.SaveAssets();
            }

            config.textureResolution = 512;
            config.sweepSpeed = 96f;
            config.sweepLineWidth = 2f;
            config.sweepLineColor = new Color(0.22f, 1f, 0.32f, 0.52f);
            config.sweepTrailLength = 22f;
            config.rangeRingColor = new Color(0.42f, 0.95f, 0.52f, 0.34f);
            config.rangeRingWidth = 1f;
            config.rangeRingCount = 4;
            config.headingLineColor = new Color(0.65f, 1f, 0.72f, 0.6f);
            config.backgroundColor = new Color(0.004f, 0.055f, 0.04f, 0.06f);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static WeatherRadarPanel EnsureRadarPanel(
            Transform parent,
            WeatherRadarConfig config,
            WeatherRadarDataProvider dataProvider,
            XPlaneOriginalWeatherRadarProvider provider,
            out XPlaneOriginalWeatherRadarDisplay originalDisplay)
        {
            GameObject panelObject = FindChildByName(parent, "RadarPanel")?.gameObject ?? new GameObject("RadarPanel");
            panelObject.transform.SetParent(parent, false);
            panelObject.SetActive(true);
            RectTransform panelRect = EnsureRectTransform(panelObject);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>() ?? panelObject.AddComponent<CanvasGroup>();
            WeatherRadarPanel panel = panelObject.GetComponent<WeatherRadarPanel>() ?? panelObject.AddComponent<WeatherRadarPanel>();

            UnityEngine.UI.Image background = EnsureImage(
                panelObject.transform,
                "Background",
                new Color(0.004f, 0.055f, 0.04f, 0.06f));
            StretchToParent(background.rectTransform);

            RemoveChildrenByExactName(panelObject.transform, "XPlaneOriginalTexture");
            GameObject textureObject = EnsureChild(panelObject.transform, "FAA Procedural Weather");
            RectTransform textureRect = EnsureRectTransform(textureObject);
            AspectRatioFitter aspect = textureObject.GetComponent<AspectRatioFitter>() ?? textureObject.AddComponent<AspectRatioFitter>();
            aspect.enabled = false;
            aspect.aspectMode = AspectRatioFitter.AspectMode.None;
            aspect.aspectRatio = 724f / 512f;

            textureRect.anchorMin = new Vector2(0.5f, 0.5f);
            textureRect.anchorMax = new Vector2(0.5f, 0.5f);
            textureRect.pivot = new Vector2(0.5f, 0.5f);
            textureRect.anchoredPosition = new Vector2(0f, 2f);
            textureRect.sizeDelta = XPlaneOriginalWeatherRadarDisplay.CalculateAspectFitSize(
                WeatherRadarSize - new Vector2(16f, 16f),
                aspect.aspectRatio);
            textureRect.localScale = Vector3.one;

            RawImage textureImage = textureObject.GetComponent<RawImage>() ?? textureObject.AddComponent<RawImage>();
            textureImage.texture = null;
            textureImage.color = new Color(0.004f, 0.055f, 0.04f, 0f);
            textureImage.raycastTarget = false;

            originalDisplay = textureObject.GetComponent<XPlaneOriginalWeatherRadarDisplay>() ?? textureObject.AddComponent<XPlaneOriginalWeatherRadarDisplay>();

            GameObject originalOverlayObject = EnsureChild(textureObject.transform, "FAAReferenceOverlay");
            RectTransform originalOverlayRect = EnsureRectTransform(originalOverlayObject);
            StretchToParent(originalOverlayRect);
            RawImage originalOverlayImage = originalOverlayObject.GetComponent<RawImage>() ?? originalOverlayObject.AddComponent<RawImage>();
            originalOverlayImage.color = Color.clear;
            originalOverlayImage.raycastTarget = false;
            XPlaneWeatherRadarOverlay originalOverlay = originalOverlayObject.GetComponent<XPlaneWeatherRadarOverlay>() ?? originalOverlayObject.AddComponent<XPlaneWeatherRadarOverlay>();
            SerializedObject originalOverlaySo = new SerializedObject(originalOverlay);
            SetObject(originalOverlaySo, "overlayImage", originalOverlayImage);
            SetObject(originalOverlaySo, "dataProvider", dataProvider);
            SetInt(originalOverlaySo, "textureWidth", 724);
            SetInt(originalOverlaySo, "textureHeight", 512);
            SetInt(originalOverlaySo, "rangeRingCount", 4);
            SetFloat(originalOverlaySo, "sectorHalfAngleDegrees", 55f);
            SetFloat(originalOverlaySo, "originHeightRatio", 0.07f);
            SetFloat(originalOverlaySo, "lineWidthPixels", 1.15f);
            SetFloat(originalOverlaySo, "majorLineWidthPixels", 1.7f);
            originalOverlaySo.ApplyModifiedPropertiesWithoutUndo();
            originalOverlay.enabled = false;
            originalOverlayImage.enabled = false;
            originalOverlayObject.SetActive(false);

            GameObject returnsObject = EnsureChild(panelObject.transform, "RadarReturns");
            RectTransform returnsRect = EnsureCenteredSquare(returnsObject, WeatherRadarSize.x - 16f);
            RawImage returnsImage = returnsObject.GetComponent<RawImage>() ?? returnsObject.AddComponent<RawImage>();
            returnsImage.color = Color.clear;
            returnsImage.raycastTarget = false;
            returnsObject.SetActive(false);
            RadarReturnRenderer returnRenderer = returnsObject.GetComponent<RadarReturnRenderer>() ?? returnsObject.AddComponent<RadarReturnRenderer>();
            SerializedObject returnSo = new SerializedObject(returnRenderer);
            SetObject(returnSo, "returnDisplay", returnsImage);
            SetObject(returnSo, "displayRect", returnsRect);
            returnSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject ringsObject = EnsureChild(panelObject.transform, "RangeRings");
            RectTransform ringsRect = EnsureCenteredSquare(ringsObject, WeatherRadarSize.x - 16f);
            RawImage ringsImage = ringsObject.GetComponent<RawImage>() ?? ringsObject.AddComponent<RawImage>();
            ringsImage.color = Color.clear;
            ringsImage.raycastTarget = false;
            ringsObject.SetActive(false);
            RangeRingsRenderer ringsRenderer = ringsObject.GetComponent<RangeRingsRenderer>() ?? ringsObject.AddComponent<RangeRingsRenderer>();
            SerializedObject ringsSo = new SerializedObject(ringsRenderer);
            SetObject(ringsSo, "ringsDisplay", ringsImage);
            SetObject(ringsSo, "displayRect", ringsRect);
            ringsSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject sweepObject = EnsureChild(panelObject.transform, "SweepLine");
            RectTransform sweepRect = EnsureCenteredSquare(sweepObject, WeatherRadarSize.x - 16f);
            RawImage sweepImage = sweepObject.GetComponent<RawImage>() ?? sweepObject.AddComponent<RawImage>();
            sweepImage.color = Color.clear;
            sweepImage.raycastTarget = false;
            sweepObject.SetActive(false);
            RadarSweepRenderer sweepRenderer = sweepObject.GetComponent<RadarSweepRenderer>() ?? sweepObject.AddComponent<RadarSweepRenderer>();
            SerializedObject sweepSo = new SerializedObject(sweepRenderer);
            SetObject(sweepSo, "sweepImage", sweepImage);
            sweepSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject waypointObject = EnsureChild(panelObject.transform, "WaypointOverlay");
            RectTransform waypointRect = EnsureCenteredSquare(waypointObject, WeatherRadarSize.x - 16f);
            WaypointOverlayRenderer waypointRenderer = waypointObject.GetComponent<WaypointOverlayRenderer>() ?? waypointObject.AddComponent<WaypointOverlayRenderer>();
            SerializedObject waypointSo = new SerializedObject(waypointRenderer);
            SetObject(waypointSo, "displayRect", waypointRect);
            waypointSo.ApplyModifiedPropertiesWithoutUndo();

            TMP_Text modeLabel = EnsureLabel(panelObject.transform, "ModeLabel", "WX", new Vector2(0f, 165f), TextAlignmentOptions.Center, 15f, new Color(0.75f, 1f, 0.75f, 1f), 110f);
            modeLabel.gameObject.SetActive(false);
            TMP_Text rangeLabel = EnsureLabel(panelObject.transform, "RangeLabel", "160 NM", new Vector2(0f, -127f), TextAlignmentOptions.Center, 14f, new Color(0.72f, 1f, 0.76f, 1f), 110f);
            TMP_Text tiltLabel = EnsureLabel(panelObject.transform, "TiltLabel", "TILT +0.0°", new Vector2(82f, -104f), TextAlignmentOptions.Right, 13f, new Color(0.72f, 1f, 0.76f, 1f), 112f);
            TMP_Text statusLabel = EnsureLabel(panelObject.transform, "TextureStatusLabel", "---", new Vector2(-105f, -126f), TextAlignmentOptions.Left, 11f, new Color(0.62f, 0.88f, 0.62f, 1f), 138f);
            TMP_Text sourceLabel = EnsureLabel(panelObject.transform, "SourceLabel", "X-PLANE WX", new Vector2(-105f, 142f), TextAlignmentOptions.Left, 12f, new Color(0.62f, 0.92f, 0.62f, 1f), 132f);
            TMP_Text ageLabel = EnsureLabel(panelObject.transform, "TextureAgeLabel", "--", new Vector2(137f, 142f), TextAlignmentOptions.Right, 12f, new Color(0.62f, 0.92f, 0.62f, 1f), 64f);
            statusLabel.gameObject.SetActive(false);
            sourceLabel.gameObject.SetActive(false);
            ageLabel.gameObject.SetActive(false);
            GameObject powerBadge = EnsureChild(panelObject.transform, "WeatherPowerBadge");
            RectTransform powerBadgeRect = EnsureRectTransform(powerBadge);
            powerBadgeRect.anchorMin = new Vector2(0.5f, 1f);
            powerBadgeRect.anchorMax = new Vector2(0.5f, 1f);
            powerBadgeRect.pivot = new Vector2(0.5f, 1f);
            powerBadgeRect.anchoredPosition = new Vector2(0f, -8f);
            powerBadgeRect.sizeDelta = new Vector2(116f, 26f);
            UnityEngine.UI.Image powerBadgeBackground = powerBadge.GetComponent<UnityEngine.UI.Image>() ?? powerBadge.AddComponent<UnityEngine.UI.Image>();
            powerBadgeBackground.color = new Color(0.004f, 0.10f, 0.065f, 0.38f);
            powerBadgeBackground.raycastTarget = false;
            TMP_Text powerLabel = EnsureLabel(powerBadge.transform, "PowerLabel", "WX --", Vector2.zero, TextAlignmentOptions.Center, 14f, new Color(0.72f, 1f, 0.76f, 1f), 110f);
            StretchToParent(powerLabel.rectTransform);

            SerializedObject displaySo = new SerializedObject(originalDisplay);
            SetObject(displaySo, "weatherProvider", provider);
            SetObject(displaySo, "dataProvider", dataProvider);
            SetObject(displaySo, "targetImage", textureImage);
            SetObject(displaySo, "aspectRatioFitter", aspect);
            SetObject(displaySo, "statusLabel", statusLabel);
            SetObject(displaySo, "sourceLabel", sourceLabel);
            SetObject(displaySo, "ageLabel", ageLabel);
            SetObject(displaySo, "powerLabel", powerLabel);
            SetObject(displaySo, "powerBadgeBackground", powerBadgeBackground);
            SetColor(displaySo, "onlineTint", new Color(1f, 1f, 1f, 0.82f));
            SetColor(displaySo, "staleTint", new Color(0.82f, 0.9f, 0.84f, 0.58f));
            SetColor(displaySo, "offlineTint", new Color(0.004f, 0.055f, 0.04f, 0.06f));
            SetBool(displaySo, "preserveAspectRatio", true);
            SetBool(displaySo, "requestTextureWhenEmpty", false);
            SetFloat(displaySo, "emptyRefreshDelaySeconds", 0.75f);
            SetFloat(displaySo, "staleRefreshDelaySeconds", 3f);
            SetBool(displaySo, "keepTextureVisibleWhenRadarOff", true);
            SetVector2(displaySo, "minimumDisplaySize", new Vector2(160f, 160f));
            SetFloat(displaySo, "displayPadding", 8f);
            SetBool(displaySo, "showReferenceOverlay", false);
            displaySo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject panelSo = new SerializedObject(panel);
            SetObject(panelSo, "config", config);
            SetObject(panelSo, "dataProvider", dataProvider);
            SetObject(panelSo, "weatherProvider", provider);
            SetObject(panelSo, "sweepRenderer", sweepRenderer);
            SetObject(panelSo, "returnRenderer", returnRenderer);
            SetObject(panelSo, "rangeRingsRenderer", ringsRenderer);
            SetObject(panelSo, "waypointRenderer", waypointRenderer);
            SetObject(panelSo, "canvasGroup", canvasGroup);
            SetObject(panelSo, "panelRect", panelRect);
            SetFloat(panelSo, "sweepCycleDuration", 3.75f);
            SetObject(panelSo, "modeLabel", modeLabel);
            SetObject(panelSo, "rangeLabel", rangeLabel);
            SetObject(panelSo, "tiltLabel", tiltLabel);
            SetObject(panelSo, "radarDisplay", textureImage);
            panelSo.ApplyModifiedPropertiesWithoutUndo();

            background.transform.SetAsFirstSibling();
            textureObject.transform.SetSiblingIndex(Mathf.Min(1, panelObject.transform.childCount - 1));
            returnsObject.transform.SetSiblingIndex(Mathf.Min(2, panelObject.transform.childCount - 1));
            ringsObject.transform.SetSiblingIndex(Mathf.Min(3, panelObject.transform.childCount - 1));
            waypointObject.transform.SetSiblingIndex(Mathf.Min(4, panelObject.transform.childCount - 1));
            sweepObject.transform.SetSiblingIndex(Mathf.Min(5, panelObject.transform.childCount - 1));
            modeLabel.transform.SetAsLastSibling();
            rangeLabel.transform.SetAsLastSibling();
            tiltLabel.transform.SetAsLastSibling();
            statusLabel.transform.SetAsLastSibling();
            sourceLabel.transform.SetAsLastSibling();
            ageLabel.transform.SetAsLastSibling();
            powerBadge.transform.SetAsLastSibling();
            powerLabel.transform.SetAsLastSibling();

            EditorUtility.SetDirty(textureImage);
            EditorUtility.SetDirty(originalOverlay);
            EditorUtility.SetDirty(originalOverlayImage);
            EditorUtility.SetDirty(originalOverlayObject);
            EditorUtility.SetDirty(originalDisplay);
            EditorUtility.SetDirty(powerBadge);
            EditorUtility.SetDirty(powerBadgeBackground);
            EditorUtility.SetDirty(panelObject);
            return panel;
        }

        private static Texture2D EnsureXPlaneWeatherRadarPreviewTexture()
        {
            TextureImporter importer = AssetImporter.GetAtPath(XPlaneWeatherRadarPreviewPath) as TextureImporter;
            if (importer != null)
            {
                bool changed = importer.textureType != TextureImporterType.Default ||
                               importer.mipmapEnabled ||
                               !importer.isReadable ||
                               importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                               importer.wrapMode != TextureWrapMode.Clamp ||
                               importer.filterMode != FilterMode.Bilinear;

                if (changed)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.mipmapEnabled = false;
                    importer.isReadable = true;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.SaveAndReimport();
                }
            }

            Texture2D preview = AssetDatabase.LoadAssetAtPath<Texture2D>(XPlaneWeatherRadarPreviewPath);
            if (preview == null)
            {
                Debug.LogWarning($"[FaaXPlane12BridgeSceneSetup] X-Plane weather radar preview texture not found: {XPlaneWeatherRadarPreviewPath}. Runtime will fetch the live texture.");
            }

            return preview;
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
                "button",
                "toggle",
                "header",
                "background",
                "radarreturns",
                "rangerings",
                "sweepline"
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

            Component syncComponent = UnityEngine.Object.FindObjectsByType(syncType, FindObjectsSortMode.None)
                .OfType<Component>()
                .Where(component => component != null && component.gameObject.scene.IsValid())
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
            SetBool(serializedSync, "syncCesiumGeoreference", false);
            SetBool(serializedSync, "useAircraftAltitudeForCesium", false);
            SetFloat(serializedSync, "cesiumReferenceHeightMeters", 0f);
            SetFloat(serializedSync, "cesiumHeightOffsetMeters", 0f);
            SetBool(serializedSync, "keepOriginNearGround", false);
            SetFloat(serializedSync, "minimumOriginGroundClearanceMeters", 120f);
            SetFloat(serializedSync, "recenterDistanceKm", 25f);
            serializedSync.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(syncComponent);
        }

        private static void DisableDeprecated3DWeatherSystems()
        {
            string[] deprecatedNames =
            {
                "WeatherVisualization3D",
                "Weather3D",
                "Weather 3D",
                "Volumetric Weather",
                "Weather3DSystem",
                "WeatherSimulator",
                "PrecipitationVFX",
                "IntensityPillarRenderer",
                "VolumetricLightning"
            };

            foreach (Transform transform in FindSceneObjects<Transform>())
            {
                if (transform == null)
                {
                    continue;
                }

                string path = GetHierarchyPath(transform);
                if (!deprecatedNames.Any(name => transform.gameObject.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    continue;
                }

                if (path.IndexOf("XPlaneWeatherIndicatorCanvas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf(IndicatorSystemObjectName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf(XPlaneWeatherRadarRootName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                if (!transform.gameObject.name.EndsWith("_Deprecated", StringComparison.OrdinalIgnoreCase))
                {
                    transform.gameObject.name += "_Deprecated";
                }

                if (transform.gameObject.activeSelf)
                {
                    transform.gameObject.SetActive(false);
                }

                EditorUtility.SetDirty(transform.gameObject);
            }

            string[] deprecatedTypeFragments =
            {
                "IndicatorSystem.Integration.Weather3DIndicatorBridge",
                "WeatherVisualization3D.",
                "WeatherRadar.Weather3D.",
                "Weather3D."
            };

            foreach (Behaviour behaviour in FindSceneObjects<Behaviour>())
            {
                if (behaviour == null)
                {
                    continue;
                }

                string fullName = behaviour.GetType().FullName ?? string.Empty;
                if (!deprecatedTypeFragments.Any(fragment => fullName.StartsWith(fragment, StringComparison.Ordinal)))
                {
                    continue;
                }

                behaviour.enabled = false;
                EditorUtility.SetDirty(behaviour);
            }
        }

        private static void RenameDeprecated3DWeatherAssetFolders()
        {
            string[] folderPaths =
            {
                "Assets/_Project/Prefabs/WeatherVisualization",
                "Assets/_Project/Materials/WeatherVisualization",
                "Assets/_Project/Textures/WeatherVisualization",
                "Assets/_Project/ScriptableObjects/WeatherVisualization"
            };

            foreach (string sourcePath in folderPaths)
            {
                if (!AssetDatabase.IsValidFolder(sourcePath))
                {
                    continue;
                }

                string parentPath = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(parentPath))
                {
                    continue;
                }

                string deprecatedPath = $"{parentPath}/WeatherVisualization_Deprecated";
                if (AssetDatabase.IsValidFolder(deprecatedPath))
                {
                    Debug.Log($"[FaaXPlane12BridgeSceneSetup] Deprecated weather asset folder already exists, leaving active folder in place: {deprecatedPath}");
                    continue;
                }

                string error = AssetDatabase.RenameAsset(sourcePath, "WeatherVisualization_Deprecated");
                if (string.IsNullOrEmpty(error))
                {
                    Debug.Log($"[FaaXPlane12BridgeSceneSetup] Renamed deprecated weather asset folder: {sourcePath} -> {deprecatedPath}");
                }
                else
                {
                    Debug.LogWarning($"[FaaXPlane12BridgeSceneSetup] Failed to rename deprecated weather asset folder {sourcePath}: {error}");
                }
            }

            AssetDatabase.SaveAssets();
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
                    rectTransform.anchorMin = Vector2.zero;
                    rectTransform.anchorMax = Vector2.zero;
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    rectTransform.anchoredPosition = ScreenFlightHudAnchoredPosition;
                    rectTransform.sizeDelta = new Vector2(100f, 100f);
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

                if (ShouldHideLegacyOverlayRoot(rectTransform.gameObject.name))
                {
                    DisableLegacyOverlayRoot(rectTransform.gameObject);
                    continue;
                }

                if (ShouldHideLegacyOverlayGroup(lowerPath) &&
                    rectTransform.gameObject.activeSelf)
                {
                    DisableLegacyOverlayRoot(rectTransform.gameObject);
                }
            }
        }

        private static void DisableLegacyRadarAndAnalysisOverlays()
        {
            foreach (Transform transform in FindSceneObjects<Transform>())
            {
                if (transform == null || !ShouldHideLegacyOverlayRoot(transform.gameObject.name))
                {
                    continue;
                }

                DisableLegacyOverlayRoot(transform.gameObject);
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
            SetVector2(serializedSanitizer, "screenFlightHudAnchoredPosition", ScreenFlightHudAnchoredPosition);
            SetFloat(serializedSanitizer, "screenFlightHudScale", ScreenFlightHudScale);
            SetInt(serializedSanitizer, "screenFlightHudSortingOrder", ScreenFlightHudSortingOrder);
            SetBool(serializedSanitizer, "hideLegacyOverlayGroups", true);
            SetBool(serializedSanitizer, "suppressLegacyCompassStrips", true);
            SetBool(serializedSanitizer, "enforceRadarPairLayout", true);
            SetString(serializedSanitizer, "weatherRadarCanvasName", XPlaneWeatherRadarCanvasName);
            SetString(serializedSanitizer, "weatherRadarRootName", XPlaneWeatherRadarRootName);
            SetString(serializedSanitizer, "trafficRadarCanvasName", XPlaneTrafficRadarCanvasName);
            SetString(serializedSanitizer, "trafficRadarRootName", TrafficRadarRootName);
            SetVector2(serializedSanitizer, "weatherRadarSize", WeatherRadarSize);
            SetVector2(serializedSanitizer, "trafficRadarSize", TrafficRadarSize);
            SetVector2(serializedSanitizer, "radarInset", new Vector2(28f, 28f));
            SetBool(serializedSanitizer, "createRadarControlStrips", true);
            SetString(serializedSanitizer, "radarControlsObjectName", "X-Plane Radar Controls");
            SetInt(serializedSanitizer, "initialFrameScans", 240);
            SetFloat(serializedSanitizer, "rescanIntervalSeconds", 0.5f);
            serializedSanitizer.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(sanitizer);
            EditorUtility.SetDirty(sanitizer.gameObject);
            return sanitizer;
        }

        private static void EnsureHeadingTapeOverlay(AviationFlightDataProvider provider, AircraftController ownship)
        {
            Canvas headingCanvas = EnsureHeadingTapeCanvas();
            Transform canvas = headingCanvas != null ? headingCanvas.transform : null;
            if (canvas == null)
            {
                return;
            }

            Transform existing = FindNamedSceneTransform(HeadingTapeOverlayName);
            GameObject overlayObject = existing != null
                ? existing.gameObject
                : new GameObject(HeadingTapeOverlayName, typeof(RectTransform));

            overlayObject.name = HeadingTapeOverlayName;
            overlayObject.transform.SetParent(canvas, false);
            overlayObject.transform.SetAsLastSibling();
            overlayObject.SetActive(true);
            RemoveDuplicateHeadingTapeOverlays(overlayObject);

            FaaHeadingTapeOverlay overlay = overlayObject.GetComponent<FaaHeadingTapeOverlay>() ??
                                           overlayObject.AddComponent<FaaHeadingTapeOverlay>();
            overlay.enabled = true;
            overlay.Configure(HeadingTapeAnchoredPosition, HeadingTapeSize, HudGreen, HudGreenDim);
            overlay.SetDataSources(provider, ownship, Camera.main != null ? Camera.main.transform : null);

            EditorUtility.SetDirty(overlay);
            EditorUtility.SetDirty(overlayObject);
        }

        private static Canvas EnsureHeadingTapeCanvas()
        {
            Transform existing = FindNamedSceneTransform(HeadingTapeCanvasName);
            GameObject canvasObject = existing != null ? existing.gameObject : null;
            Canvas canvas = canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
            if (canvas == null)
            {
                canvasObject = new GameObject(HeadingTapeCanvasName, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
            }

            RemoveDuplicateHeadingTapeCanvases(canvasObject);

            canvasObject.name = HeadingTapeCanvasName;
            canvasObject.transform.SetParent(null, false);
            canvasObject.SetActive(true);
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = ScreenFlightHudSortingOrder + 200;
            SerializedObject serializedCanvas = new SerializedObject(canvas);
            SetBool(serializedCanvas, "m_OverrideSorting", true);
            SetInt(serializedCanvas, "m_SortingOrder", ScreenFlightHudSortingOrder + 200);
            serializedCanvas.ApplyModifiedPropertiesWithoutUndo();

            RectTransform rectTransform = canvasObject.GetComponent<RectTransform>() ?? canvasObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;

            UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>() ??
                                                 canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>() ??
                                         canvasObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            EditorUtility.SetDirty(canvasObject);
            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(scaler);
            EditorUtility.SetDirty(raycaster);
            return canvas;
        }

        private static void RemoveDuplicateHeadingTapeCanvases(GameObject keep)
        {
            foreach (Transform transform in FindSceneObjects<Transform>())
            {
                if (transform == null || transform.gameObject == keep || transform.gameObject.name != HeadingTapeCanvasName)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(transform.gameObject);
            }
        }

        private static void RemoveDuplicateHeadingTapeOverlays(GameObject keep)
        {
            foreach (Transform transform in FindSceneObjects<Transform>())
            {
                if (transform == null || transform.gameObject == keep || transform.gameObject.name != HeadingTapeOverlayName)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(transform.gameObject);
            }
        }

        private static void EnsureRadarControlsOverlay()
        {
            Transform weatherRoot = FindNamedSceneTransform(XPlaneWeatherRadarRootName);
            Transform trafficRoot = FindPreferredTrafficRadarController()?.transform ??
                                    FindNamedSceneTransform(TrafficRadarRootName);
            Transform parent = weatherRoot != null
                ? weatherRoot.parent
                : trafficRoot != null ? trafficRoot.parent : FindOrCreateRoot(ManagersObjectName).transform;

            GameObject controlsObject = GameObject.Find(RadarControlsObjectName);
            if (controlsObject == null)
            {
                controlsObject = new GameObject(RadarControlsObjectName, typeof(RectTransform));
            }

            controlsObject.name = RadarControlsObjectName;
            controlsObject.transform.SetParent(parent, false);
            controlsObject.SetActive(true);

            RectTransform rectTransform = controlsObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = controlsObject.AddComponent<RectTransform>();
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.pivot = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;

            FaaRadarControlsOverlay controls = controlsObject.GetComponent<FaaRadarControlsOverlay>() ??
                                               controlsObject.AddComponent<FaaRadarControlsOverlay>();
            SerializedObject serializedControls = new SerializedObject(controls);
            SetFloat(serializedControls, "defaultWeatherRadarSize", WeatherRadarSize.x);
            SetFloat(serializedControls, "defaultTrafficRadarSize", TrafficRadarSize.x);
            SetFloat(serializedControls, "minimumRadarSize", 220f);
            SetFloat(serializedControls, "maximumRadarSize", 560f);
            SetFloat(serializedControls, "radarSizeStep", 32f);
            SetBool(serializedControls, "rememberRadarSizes", true);
            serializedControls.ApplyModifiedPropertiesWithoutUndo();
            controls.Configure(weatherRoot, trafficRoot);

            EditorUtility.SetDirty(controls);
            EditorUtility.SetDirty(controlsObject);
        }

        private static Transform FindNamedSceneTransform(string objectName)
        {
            return FindSceneObjects<Transform>()
                .FirstOrDefault(transform => transform != null && transform.gameObject.name == objectName);
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

            RollbackAuthoredPilotTapeVisuals(legacyHudRoot);
            EnsurePrimaryAirspeedElement(legacyHudRoot);
            EnsurePrimaryAltimeterElement(legacyHudRoot);
            EnsureEngineBarEditorLabels(legacyHudRoot);
            EnsurePrimaryTorquePanelElement(legacyHudRoot);
            EnsurePrimaryNrIndicatorElement(legacyHudRoot);
            SuppressDuplicatePitchLadder(legacyHudRoot);

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

        /// <summary>
        /// Keep the live engine value readouts authored in the scene/prefab.
        /// The fixed side-number columns are intentionally removed; the
        /// runtime HUD only updates references that already exist.
        /// </summary>
        private static void EnsureEngineBarEditorLabels(GameObject legacyHudRoot)
        {
            if (legacyHudRoot == null)
            {
                return;
            }

            Transform torquePanel = FindChildByName(legacyHudRoot.transform, "Torque Panel");
            if (torquePanel != null)
            {
                RemoveChildrenByNamePrefix(torquePanel, "Torque Scale ");
                RectTransform frame = FindChildByName(torquePanel, "Torque Frame")?.GetComponent<RectTransform>();
                int layer = frame != null ? frame.gameObject.layer : torquePanel.gameObject.layer;

                EnsureEngineNumericLabel(
                    torquePanel, "Torque Value L", "---", new Vector2(-0.055f, -0.055f), 22f, layer, HudGreenDim);
                EnsureEngineNumericLabel(
                    torquePanel, "Torque Value R", "---", new Vector2(0.088f, -0.055f), 22f, layer, HudGreenDim);
            }

            Transform nrIndicator = FindChildByName(legacyHudRoot.transform, "NR/ENG Ind");
            if (nrIndicator != null)
            {
                RemoveChildrenByNamePrefix(nrIndicator, "NR Scale ");
                RectTransform frame = FindChildByName(nrIndicator, "NR Indicator Frame")?.GetComponent<RectTransform>();
                int layer = frame != null ? frame.gameObject.layer : nrIndicator.gameObject.layer;

                EnsureEngineNumericLabel(
                    nrIndicator, "NR Value Center", "---", new Vector2(0f, -0.055f), 20f, layer, HudGreenDim);
                EnsureEngineNumericLabel(
                    nrIndicator, "NR Value L", "---", new Vector2(-0.11f, -0.055f), 20f, layer, HudGreenDim);
                EnsureEngineNumericLabel(
                    nrIndicator, "NR Value R", "---", new Vector2(0.11f, -0.055f), 20f, layer, HudGreenDim);
            }
        }

        /// <summary>
        /// Remove only the authored IAS/ALT tape objects. The center attitude,
        /// heading, radar, and other symbology are outside this subtree and are
        /// left intact. The numeric text readouts remain bound to live data.
        /// </summary>
        private static void RollbackAuthoredPilotTapeVisuals(GameObject legacyHudRoot)
        {
            if (legacyHudRoot == null)
            {
                return;
            }

            Transform airspeed = FindChildByName(legacyHudRoot.transform, "Airspeed Indicator");
            Transform altimeter = FindChildByName(legacyHudRoot.transform, "Altimeter");
            RemovePilotTape(airspeed, AirspeedTapeObjectName, LegacyAirspeedTapeObjectName);
            RemovePilotTape(altimeter, AltimeterTapeObjectName, LegacyAltimeterTapeObjectName);
            RemoveLegacyReadoutRaster(airspeed, "Window Panel", "Airspeed Readout");
            RemoveLegacyReadoutRaster(altimeter, "Window Panel Alt", "Alt Readout");
        }

        private static void RemovePilotTape(Transform indicator, string authoredName, string legacyName)
        {
            if (indicator == null)
            {
                return;
            }

            Transform authoredTape = FindChildByName(indicator, authoredName);
            if (authoredTape != null)
            {
                UnityEngine.Object.DestroyImmediate(authoredTape.gameObject);
            }

            Transform legacyTape = FindChildByName(indicator, legacyName);
            if (legacyTape != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyTape.gameObject);
            }

            RemoveChildrenByNamePrefix(indicator, "IAS Scale ");
            RemoveChildrenByNamePrefix(indicator, "ALT Scale ");
            EditorUtility.SetDirty(indicator.gameObject);
        }

        /// <summary>
        /// The legacy readout container carries a large baked PNG which also
        /// contains the old side-number columns. Keep the authored TMP text
        /// children, but remove the raster Image component so the editor HUD
        /// is fully vector-authored and cannot reintroduce those columns.
        /// </summary>
        private static void RemoveLegacyReadoutRaster(
            Transform indicator,
            string panelName,
            string readoutName)
        {
            Transform panel = FindChildByName(indicator, panelName);
            Transform readout = FindChildByName(panel, readoutName);
            if (readout == null)
            {
                return;
            }

            UnityEngine.UI.Image image = readout.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                UnityEngine.Object.DestroyImmediate(image);
            }

            EditorUtility.SetDirty(readout.gameObject);
        }

        private static void RemoveLegacyInstrumentReadoutRastersInScene()
        {
            foreach (UnityEngine.UI.Image image in FindSceneObjects<UnityEngine.UI.Image>())
            {
                if (image == null || image.gameObject.scene != SceneManager.GetActiveScene())
                {
                    continue;
                }

                string objectName = image.gameObject.name;
                if (!string.Equals(objectName, "Airspeed Readout", StringComparison.Ordinal) &&
                    !string.Equals(objectName, "Alt Readout", StringComparison.Ordinal))
                {
                    continue;
                }

                Scene scene = image.gameObject.scene;
                UnityEngine.Object.DestroyImmediate(image);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static void RemoveChildrenByNamePrefix(Transform root, string prefix)
        {
            if (root == null || string.IsNullOrEmpty(prefix))
            {
                return;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child == null || child == root || child.name.IndexOf(prefix, StringComparison.Ordinal) != 0)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void RemoveChildrenByExactName(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName))
            {
                return;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child == null || child == root ||
                    !string.Equals(child.name, objectName, StringComparison.Ordinal))
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void EnsurePrimaryAirspeedElement(GameObject legacyHudRoot)
        {
            Type elementType = FindType("HUDControl.Elements.AirspeedIndicatorElement");
            if (elementType == null || legacyHudRoot == null)
            {
                return;
            }

            Transform indicator = FindChildByName(legacyHudRoot.transform, "Airspeed Indicator");
            if (indicator == null)
            {
                return;
            }

            Component primary = indicator.GetComponent(elementType) ?? indicator.gameObject.AddComponent(elementType);
            RectTransform tape = null;
            TMP_Text readout = FindChildByName(indicator, "AirspeedReadoutText")?.GetComponent<TMP_Text>();
            RectTransform window = FindChildByName(indicator, "Window Panel")?.GetComponent<RectTransform>();

            SerializedObject serializedPrimary = new SerializedObject(primary);
            SetObject(serializedPrimary, "speedTape", tape);
            SetObject(serializedPrimary, "airspeedReadout", readout);
            SetObject(serializedPrimary, "windowPanel", window);
            SetBool(serializedPrimary, "enableTape", tape != null);
            SetBool(serializedPrimary, "enableReadout", readout != null);
            SetFloat(serializedPrimary, "pixelsPerKnot", 0.0018f);
            SetFloat(serializedPrimary, "maxTapeOffsetPixels", 0.18f);
            SetFloat(serializedPrimary, "referenceAirspeed", 100f);
            SetString(serializedPrimary, "displayFormat", "{0:000}");
            SetFloat(serializedPrimary, "animationSpeed", 14f);
            SetBool(serializedPrimary, "isEnabled", tape != null || readout != null);
            serializedPrimary.ApplyModifiedPropertiesWithoutUndo();

            if (primary is Behaviour primaryBehaviour)
            {
                primaryBehaviour.enabled = tape != null || readout != null;
            }

            DisableDuplicateHudElementComponents(indicator, elementType, primary);
            EditorUtility.SetDirty(primary);
            EditorUtility.SetDirty(primary.gameObject);
        }

        private static void EnsurePrimaryAltimeterElement(GameObject legacyHudRoot)
        {
            Type elementType = FindType("HUDControl.Elements.AltimeterElement");
            if (elementType == null || legacyHudRoot == null)
            {
                return;
            }

            Transform indicator = FindChildByName(legacyHudRoot.transform, "Altimeter");
            if (indicator == null)
            {
                return;
            }

            Component primary = indicator.GetComponent(elementType) ?? indicator.gameObject.AddComponent(elementType);
            RectTransform tape = null;
            TMP_Text readout = FindChildByName(indicator, "AltReadoutText")?.GetComponent<TMP_Text>();
            RectTransform window = FindChildByName(indicator, "Window Panel Alt")?.GetComponent<RectTransform>();

            SerializedObject serializedPrimary = new SerializedObject(primary);
            SetObject(serializedPrimary, "altitudeTape", tape);
            SetObject(serializedPrimary, "altitudeReadout", readout);
            SetObject(serializedPrimary, "windowPanel", window);
            SetBool(serializedPrimary, "enableTape", tape != null);
            SetBool(serializedPrimary, "enableReadout", readout != null);
            SetFloat(serializedPrimary, "pixelsPerFoot", 0.00018f);
            SetFloat(serializedPrimary, "maxTapeOffsetPixels", 0.18f);
            SetFloat(serializedPrimary, "referenceAltitude", 10000f);
            SetString(serializedPrimary, "displayFormat", "{0:00000}");
            SetFloat(serializedPrimary, "animationSpeed", 14f);
            SetBool(serializedPrimary, "isEnabled", tape != null || readout != null);
            serializedPrimary.ApplyModifiedPropertiesWithoutUndo();

            if (primary is Behaviour primaryBehaviour)
            {
                primaryBehaviour.enabled = tape != null || readout != null;
            }

            DisableDuplicateHudElementComponents(indicator, elementType, primary);
            EditorUtility.SetDirty(primary);
            EditorUtility.SetDirty(primary.gameObject);
        }

        private static void DisableDuplicateHudElementComponents(Transform root, Type elementType, Component primary)
        {
            if (root == null || elementType == null)
            {
                return;
            }

            foreach (Component duplicate in root.GetComponentsInChildren(elementType, true).OfType<Component>())
            {
                if (duplicate == null || duplicate == primary)
                {
                    continue;
                }

                SerializedObject serializedDuplicate = new SerializedObject(duplicate);
                SetBool(serializedDuplicate, "isEnabled", false);
                serializedDuplicate.ApplyModifiedPropertiesWithoutUndo();
                if (duplicate is Behaviour duplicateBehaviour)
                {
                    duplicateBehaviour.enabled = false;
                }

                EditorUtility.SetDirty(duplicate);
            }
        }

        private static bool RollbackAuthoredPilotScalesInPrefab()
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), SecondIterationGuiPrefabPath)))
            {
                return false;
            }

            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(SecondIterationGuiPrefabPath);
                RollbackAuthoredPilotTapeVisuals(prefabRoot);
                EnsurePrimaryAirspeedElement(prefabRoot);
                EnsurePrimaryAltimeterElement(prefabRoot);
                EditorUtility.SetDirty(prefabRoot);
                return PrefabUtility.SaveAsPrefabAsset(prefabRoot, SecondIterationGuiPrefabPath) != null;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FaaXPlane12BridgeSceneSetup] Failed to roll back authored IAS/ALT tapes in prefab: {exception.Message}");
                return false;
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static bool AuthorEngineBarNumbersInPrefab()
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), SecondIterationGuiPrefabPath)))
            {
                return false;
            }

            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(SecondIterationGuiPrefabPath);
                RemoveEngineBarScaleLabels(prefabRoot);
                EnsureEngineBarEditorLabels(prefabRoot);
                EditorUtility.SetDirty(prefabRoot);
                return PrefabUtility.SaveAsPrefabAsset(prefabRoot, SecondIterationGuiPrefabPath) != null;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FaaXPlane12BridgeSceneSetup] Failed to author engine bar numbers in prefab: {exception.Message}");
                return false;
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static bool RemoveEngineBarScaleLabelsInPrefab()
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), SecondIterationGuiPrefabPath)))
            {
                return false;
            }

            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(SecondIterationGuiPrefabPath);
                RemoveEngineBarScaleLabels(prefabRoot);
                EnsurePrimaryTorquePanelElement(prefabRoot);
                EnsurePrimaryNrIndicatorElement(prefabRoot);
                EditorUtility.SetDirty(prefabRoot);
                return PrefabUtility.SaveAsPrefabAsset(prefabRoot, SecondIterationGuiPrefabPath) != null;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FaaXPlane12BridgeSceneSetup] Failed to remove engine bar scale labels from prefab: {exception.Message}");
                return false;
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static void RemoveEngineBarScaleLabels(GameObject legacyHudRoot)
        {
            if (legacyHudRoot == null)
            {
                return;
            }

            Transform torquePanel = FindChildByName(legacyHudRoot.transform, "Torque Panel");
            RemoveChildrenByNamePrefix(torquePanel, "Torque Scale ");

            Transform nrIndicator = FindChildByName(legacyHudRoot.transform, "NR/ENG Ind");
            RemoveChildrenByNamePrefix(nrIndicator, "NR Scale ");
        }

        private static List<Component> FindEngineScaleLabels(Transform parent, string prefix, int count)
        {
            List<Component> labels = new List<Component>(count);
            for (int i = 0; i < count; i++)
            {
                TMP_Text label = FindChildByName(parent, prefix + i)?.GetComponent<TMP_Text>();
                if (label != null)
                {
                    labels.Add(label);
                }
            }

            return labels;
        }

        private static TMP_Text EnsureEngineNumericLabel(
            Transform parent,
            string childName,
            string text,
            Vector2 anchoredPosition,
            float fontSize,
            int layer,
            Color color)
        {
            TMP_Text label = EnsureLabel(
                parent,
                childName,
                text,
                anchoredPosition,
                TextAlignmentOptions.Center,
                fontSize,
                color,
                52f);
            RectTransform rect = label.rectTransform;
            rect.sizeDelta = new Vector2(52f, 26f);
            rect.localScale = Vector3.one * 0.0016f;
            rect.localRotation = Quaternion.identity;
            label.overflowMode = TextOverflowModes.Overflow;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.gameObject.layer = layer;
            label.transform.SetAsLastSibling();
            EditorUtility.SetDirty(label);
            EditorUtility.SetDirty(label.gameObject);
            return label;
        }

        private static float GetEngineScaleHorizontalOffset(RectTransform frame, float gap)
        {
            float width = frame != null ? Mathf.Abs(frame.rect.width) : 0.2918475f;
            if (width < 0.0001f && frame != null)
            {
                width = Mathf.Abs(frame.sizeDelta.x);
            }

            if (width < 0.0001f)
            {
                width = 0.2918475f;
            }

            return -(width * 0.5f + gap);
        }

        private static Vector2 GetEngineScaleLabelPosition(
            RectTransform frame,
            float value,
            float maximumPercent,
            float horizontalOffset,
            float pointerMinimumY,
            float pointerTravelY)
        {
            float x = frame != null ? frame.anchoredPosition.x + horizontalOffset : horizontalOffset;
            float normalized = Mathf.Clamp01(value / Mathf.Max(1f, maximumPercent));
            return new Vector2(x, pointerMinimumY + normalized * pointerTravelY);
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
            TMP_Text torqueValueL = FindChildByName(torquePanel, "Torque Value L")?.GetComponent<TMP_Text>();
            TMP_Text torqueValueR = FindChildByName(torquePanel, "Torque Value R")?.GetComponent<TMP_Text>();
            List<Component> torqueScaleLabels = FindEngineScaleLabels(torquePanel, "Torque Scale ", 7);

            SerializedObject serializedPrimary = new SerializedObject(primary);
            SetObject(serializedPrimary, "torqueFrame", frame);
            SetObject(serializedPrimary, "torquePointerL", leftIndicator);
            SetObject(serializedPrimary, "torquePointerR", rightIndicator);
            SetObject(serializedPrimary, "torqueValueL", torqueValueL);
            SetObject(serializedPrimary, "torqueValueR", torqueValueR);
            SetObjectArray(serializedPrimary, "torqueScaleLabels", torqueScaleLabels);
            SetBool(serializedPrimary, "enableAnimation", leftIndicator != null || rightIndicator != null);
            SetBool(serializedPrimary, "simulateFromThrottle", false);
            SetFloat(serializedPrimary, "pointerMinimumY", 0.004f);
            SetFloat(serializedPrimary, "pointerTravelY", 0.24f);
            SetFloat(serializedPrimary, "maxTorquePercent", 120f);
            SetBool(serializedPrimary, "showNumericReadouts", true);
            // Scale labels are intentionally omitted from the compact pilot
            // layout; the bars and live value readouts remain visible.
            SetBool(serializedPrimary, "showScaleLabels", false);
            SetInt(serializedPrimary, "scaleLabelStepPercent", 20);
            SetFloat(serializedPrimary, "scaleLabelFontSize", 16f);
            SetFloat(serializedPrimary, "scaleLabelGap", 0.045f);
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

        private static void EnsurePrimaryNrIndicatorElement(GameObject legacyHudRoot)
        {
            Type nrIndicatorType = FindType("HUDControl.Elements.NRIndicatorElement");
            if (nrIndicatorType == null || legacyHudRoot == null)
            {
                return;
            }

            Transform nrIndicator = FindChildByName(legacyHudRoot.transform, "NR/ENG Ind");
            if (nrIndicator == null)
            {
                return;
            }

            Component primary = nrIndicator.GetComponent(nrIndicatorType) ??
                                nrIndicator.gameObject.AddComponent(nrIndicatorType);
            RectTransform frame = FindChildByName(nrIndicator, "NR Indicator Frame")?.GetComponent<RectTransform>();
            RectTransform center = FindChildByName(nrIndicator, "RPM Center Pointer")?.GetComponent<RectTransform>();
            RectTransform left = FindChildByName(nrIndicator, "RPM Pointer L")?.GetComponent<RectTransform>();
            RectTransform right = FindChildByName(nrIndicator, "RPM Pointer R")?.GetComponent<RectTransform>();
            TMP_Text rpmValueCenter = FindChildByName(nrIndicator, "NR Value Center")?.GetComponent<TMP_Text>();
            TMP_Text rpmValueL = FindChildByName(nrIndicator, "NR Value L")?.GetComponent<TMP_Text>();
            TMP_Text rpmValueR = FindChildByName(nrIndicator, "NR Value R")?.GetComponent<TMP_Text>();
            List<Component> nrScaleLabels = FindEngineScaleLabels(nrIndicator, "NR Scale ", 7);

            SerializedObject serializedPrimary = new SerializedObject(primary);
            SetObject(serializedPrimary, "nrFrame", frame);
            SetObject(serializedPrimary, "rpmCenterPointer", center);
            SetObject(serializedPrimary, "rpmPointerL", left);
            SetObject(serializedPrimary, "rpmPointerR", right);
            SetObject(serializedPrimary, "rpmValueCenter", rpmValueCenter);
            SetObject(serializedPrimary, "rpmValueL", rpmValueL);
            SetObject(serializedPrimary, "rpmValueR", rpmValueR);
            SetObjectArray(serializedPrimary, "nrScaleLabels", nrScaleLabels);
            SetBool(serializedPrimary, "enableAnimation", center != null || left != null || right != null);
            SetBool(serializedPrimary, "simulateFromThrottle", false);
            SetFloat(serializedPrimary, "pointerMinimumY", 0.03f);
            SetFloat(serializedPrimary, "pointerTravelY", 0.24f);
            SetFloat(serializedPrimary, "maxRPMPercent", 110f);
            SetBool(serializedPrimary, "showNumericReadouts", true);
            // Scale labels are intentionally omitted from the compact pilot
            // layout; the bars and live value readouts remain visible.
            SetBool(serializedPrimary, "showScaleLabels", false);
            SetInt(serializedPrimary, "scaleLabelStepPercent", 20);
            SetFloat(serializedPrimary, "scaleLabelFontSize", 16f);
            SetFloat(serializedPrimary, "scaleLabelGap", 0.045f);
            SetBool(serializedPrimary, "isEnabled", true);
            serializedPrimary.ApplyModifiedPropertiesWithoutUndo();

            if (primary is Behaviour primaryBehaviour)
            {
                primaryBehaviour.enabled = true;
            }

            foreach (Component duplicate in nrIndicator.GetComponentsInChildren(nrIndicatorType, true).OfType<Component>())
            {
                if (duplicate == null || duplicate == primary)
                {
                    continue;
                }

                SerializedObject serializedDuplicate = new SerializedObject(duplicate);
                SetBool(serializedDuplicate, "isEnabled", false);
                serializedDuplicate.ApplyModifiedPropertiesWithoutUndo();
                if (duplicate is Behaviour duplicateBehaviour)
                {
                    duplicateBehaviour.enabled = false;
                }

                EditorUtility.SetDirty(duplicate);
            }

            EditorUtility.SetDirty(primary);
            EditorUtility.SetDirty(primary.gameObject);
        }

        private static void SuppressDuplicatePitchLadder(GameObject legacyHudRoot)
        {
            Transform scaleMasker = legacyHudRoot != null
                ? legacyHudRoot.transform.Find("Attitude/ScaleMasker")
                : null;
            if (scaleMasker == null)
            {
                return;
            }

            Transform primaryScale = scaleMasker.Find("Scale");
            if (primaryScale != null && !primaryScale.gameObject.activeSelf)
            {
                primaryScale.gameObject.SetActive(true);
                EditorUtility.SetDirty(primaryScale.gameObject);
            }

            Transform duplicateScale = scaleMasker.Find("ScaleIteration2");
            if (duplicateScale == null)
            {
                return;
            }

            foreach (Graphic graphic in duplicateScale.GetComponentsInChildren<Graphic>(true))
            {
                graphic.enabled = false;
                graphic.raycastTarget = false;
                EditorUtility.SetDirty(graphic);
            }

            foreach (CanvasRenderer renderer in duplicateScale.GetComponentsInChildren<CanvasRenderer>(true))
            {
                renderer.cull = true;
                EditorUtility.SetDirty(renderer);
            }

            if (duplicateScale.gameObject.activeSelf)
            {
                duplicateScale.gameObject.SetActive(false);
            }

            EditorUtility.SetDirty(duplicateScale.gameObject);
        }

        private static RectTransform FindPrimaryPitchLadder(Transform attitudeElement)
        {
            Transform scaleMasker = attitudeElement != null ? attitudeElement.Find("ScaleMasker") : null;
            Transform primaryScale = scaleMasker != null ? scaleMasker.Find("Scale") : null;
            return primaryScale != null ? primaryScale.GetComponent<RectTransform>() : null;
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
                    RectTransform primaryPitchLadder = FindPrimaryPitchLadder(element.transform);
                    if (primaryPitchLadder != null)
                    {
                        SetObject(serializedElement, "pitchLadder", primaryPitchLadder);
                        if (primaryPitchLadder.parent is RectTransform maskContainer)
                        {
                            SetObject(serializedElement, "maskContainer", maskContainer);
                        }
                    }
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
                    Transform cdi = FindChildByName(element.transform, "CDI");
                    Transform dotsPanel = FindChildByName(element.transform, "Deviation Dots Panel") ??
                                          FindChildByName(element.transform, "DeviationDotsPanel");
                    SetObject(serializedElement, "cdiNeedle", cdi != null ? cdi.GetComponent<RectTransform>() : null);
                    SetObject(serializedElement, "deviationDotsPanel", dotsPanel != null ? dotsPanel.GetComponent<RectTransform>() : null);
                    DisableFeatureIfReferenceMissing(serializedElement, "enableCDI", "cdiNeedle");
                    SetFloat(serializedElement, "pixelsPerDot", 0.064f);
                    SetFloat(serializedElement, "maxCDIOffsetPixels", 0.16f);
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
            SuppressDuplicateUiToolkitHuds(hud);

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
                "CompassNavigatorPro",
                "Compass Bar Generated",
                "FAA_CompassTape",
                "RadarCanvas",
                "VisualUnderstanding",
                "VC",
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

        private static void SuppressDuplicateUiToolkitHuds(Component keep)
        {
            if (keep == null)
            {
                return;
            }

            Type hudType = keep.GetType();
            foreach (Component duplicate in FindSceneObjects(hudType))
            {
                if (duplicate == null || duplicate == keep)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
            }
        }

        private static void ConfigureAdvancedRadialMenu()
        {
            Type radialMenuType = FindType("VoiceControl.UI.UIToolkitRadialMenuAdvanced");
            Component radialMenu = FindFirstSceneObject(radialMenuType);
            if (radialMenu == null)
            {
                return;
            }

            SerializedObject serializedMenu = new SerializedObject(radialMenu);
            SetFloat(serializedMenu, "innerRadius", 150f);
            SetFloat(serializedMenu, "middleRadius", 320f);
            SetFloat(serializedMenu, "outerRadius", 465f);
            SetFloat(serializedMenu, "collapsedButtonSize", 68f);
            SetBool(serializedMenu, "collapsedButtonTopRight", true);
            SetInt(serializedMenu, "maxSubSegmentCount", 6);
            SetFloat(serializedMenu, "openDuration", 0.28f);
            SetFloat(serializedMenu, "closeDuration", 0.18f);
            SetFloat(serializedMenu, "subMenuExpandDuration", 0.18f);
            SetBool(serializedMenu, "reducedMotion", false);
            SetFloat(serializedMenu, "mainSegmentStagger", 0.025f);
            SetFloat(serializedMenu, "subSegmentStagger", 0.02f);
            SetFloat(serializedMenu, "hoverScaleBoost", 0.06f);
            SetBool(serializedMenu, "useRippleEffect", true);
            SetBool(serializedMenu, "usePulseAnimation", true);
            SetFloat(serializedMenu, "menuTransparency", 0.92f);
            SetFloat(serializedMenu, "ringBackgroundTransparency", 0.82f);
            SetFloat(serializedMenu, "segmentTransparency", 0.94f);
            SetFloat(serializedMenu, "centerTransparency", 0.94f);
            SetFloat(serializedMenu, "mainLabelFontSize", 16f);
            SetFloat(serializedMenu, "subLabelFontSize", 13f);
            SetFloat(serializedMenu, "centerTitleFontSize", 21f);
            SetFloat(serializedMenu, "centerSubtitleFontSize", 13f);
            serializedMenu.ApplyModifiedPropertiesWithoutUndo();

            InvokeIfPresent(radialMenu, "ApplyAviationHudPreset", false);
            EditorUtility.SetDirty(radialMenu);
            EditorUtility.SetDirty(radialMenu.gameObject);
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

            return UnityEngine.Object.FindObjectsByType(componentType, FindObjectsSortMode.None)
                .OfType<Component>()
                .Where(component => component != null && component.gameObject.scene.IsValid())
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
                   lowerPath.Contains("_ui/faasymbologycanvas/compassnavigatorpro") ||
                   lowerPath.Contains("_ui/faasymbologycanvas/visualunderstanding") ||
                   lowerPath.Contains("_ui/faasymbologycanvas/vc") ||
                   lowerPath.Contains("_ui/faasymbologycanvas/radarcanvas") ||
                   lowerPath.Contains("/faasymbologycanvas/radarcanvas") ||
                   lowerPath.Contains("_ui/faasymbologycanvas/analysis trigger buttons") ||
                   lowerPath.EndsWith("/faasymbologycanvas/radarcanvas/weather radar system/radarpanel") ||
                   lowerPath.EndsWith("/faasymbologycanvas/radarcanvas/weather radar system/controlpanel") ||
                   lowerPath.EndsWith("/faasymbologycanvas/radarcanvas/traffic radar system/radar display") ||
                   lowerPath.EndsWith("/faasymbologycanvas/radarcanvas/traffic range ui") ||
                   lowerPath.EndsWith("/faasymbologycanvas/radarcanvas/traffic radar system/radar display/mapcanvas/map image");
        }

        private static bool ShouldHideLegacyOverlayRoot(string objectName)
        {
            return string.Equals(objectName, "MaskCanvas", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(objectName, "CompassNavigatorPro", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(objectName, "FAA_CompassTape", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(objectName, "CompassBarSystem", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(objectName, "RadarCanvas", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(objectName, "VisualUnderstanding", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(objectName, "VC", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(objectName, "Analysis Trigger Buttons", StringComparison.OrdinalIgnoreCase);
        }

        private static void RemoveGeneratedNavigationRepairArtifacts()
        {
            foreach (Transform transform in FindSceneObjects<Transform>())
            {
                if (transform == null || !IsGeneratedNavigationRepairArtifact(transform.gameObject.name))
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(transform.gameObject);
            }
        }

        private static bool IsGeneratedNavigationRepairArtifact(string objectName)
        {
            return string.Equals(objectName, "FAA_LocalizerLeftLimitTick", StringComparison.Ordinal) ||
                   string.Equals(objectName, "FAA_LocalizerCenterMark", StringComparison.Ordinal) ||
                   string.Equals(objectName, "FAA_LocalizerRightLimitTick", StringComparison.Ordinal);
        }

        private static void DisableLegacyOverlayRoot(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                canvas.enabled = false;
                EditorUtility.SetDirty(canvas);
            }

            foreach (GraphicRaycaster raycaster in root.GetComponentsInChildren<GraphicRaycaster>(true))
            {
                raycaster.enabled = false;
                EditorUtility.SetDirty(raycaster);
            }

            if (root.activeSelf)
            {
                root.SetActive(false);
            }

            EditorUtility.SetDirty(root);
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
                SetBool(serializedManager, "suppressAutoStartDisabledWarning", true);
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
            if (root != null)
            {
                return root;
            }

            root = new GameObject(name);
            if (name != SceneRootObjectName)
            {
                GameObject sceneRoot = GameObject.Find(SceneRootObjectName) ?? new GameObject(SceneRootObjectName);
                root.transform.SetParent(sceneRoot.transform, true);
            }

            return root;
        }

        private static T FindFirstSceneObject<T>() where T : Component
        {
            return FindSceneObjects<T>().FirstOrDefault();
        }

        private static T[] FindSceneObjects<T>() where T : Component
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private static Component[] FindSceneObjects(Type componentType)
        {
            if (componentType == null)
            {
                return Array.Empty<Component>();
            }

            return UnityEngine.Object.FindObjectsByType(componentType, FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OfType<Component>()
                .Where(component => component != null && component.gameObject.scene.IsValid())
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

        private static void SetColor(SerializedObject serializedObject, string propertyName, Color value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.colorValue = value;
            }
        }

        private static void SetVector2(SerializedObject serializedObject, string propertyName, Vector2 value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.vector2Value = value;
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

        private static GameObject EnsureChild(Transform parent, string childName)
        {
            GameObject child = FindChildByName(parent, childName)?.gameObject;
            if (child == null)
            {
                child = new GameObject(childName);
            }

            child.transform.SetParent(parent, false);
            return child;
        }

        private static RectTransform EnsureRectTransform(GameObject gameObject)
        {
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = gameObject.AddComponent<RectTransform>();
            }

            return rectTransform;
        }

        private static RectTransform EnsureCenteredSquare(GameObject gameObject, float size)
        {
            RectTransform rectTransform = EnsureRectTransform(gameObject);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0f, 8f);
            rectTransform.sizeDelta = new Vector2(size, size);
            return rectTransform;
        }

        private static UnityEngine.UI.Image EnsureImage(Transform parent, string childName, Color color)
        {
            GameObject child = EnsureChild(parent, childName);
            UnityEngine.UI.Image image = child.GetComponent<UnityEngine.UI.Image>() ?? child.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
        }

        private static TMP_Text EnsureLabel(
            Transform parent,
            string childName,
            string text,
            Vector2 anchoredPosition,
            TextAlignmentOptions alignment,
            float fontSize,
            Color color,
            float width)
        {
            GameObject labelObject = EnsureChild(parent, childName);
            RectTransform rectTransform = EnsureRectTransform(labelObject);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = new Vector2(width, 24f);

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>() ?? labelObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = GetDefaultTmpFont();
            if (font != null)
            {
                label.font = font;
            }

            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static TMP_FontAsset GetDefaultTmpFont()
        {
            if (TMP_Settings.defaultFontAsset != null)
            {
                return TMP_Settings.defaultFontAsset;
            }

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
            {
                return font;
            }

            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            if (guids.Length == 0)
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
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
