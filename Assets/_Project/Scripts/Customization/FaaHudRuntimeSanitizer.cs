using System.Collections.Generic;
using System.Reflection;
using CompassNavigatorPro;
using HUDControl.CompassBar;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using WeatherRadar;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FAA.Customization
{
    [DefaultExecutionOrder(10000)]
    [AddComponentMenu("FAA/Customization/FAA HUD Runtime Sanitizer")]
    public class FaaHudRuntimeSanitizer : MonoBehaviour
    {
        private const string DefaultRadarControlsObjectName = "X-Plane Radar Controls";
        private const string XPlaneOriginalTextureObjectName = "XPlaneOriginalTexture";
        private const string ProceduralWeatherTextureObjectName = "FAA Procedural Weather";
        private const float LegacyScreenFlightHudScale = 420f;
        private const float MinimumReadableScreenFlightHudScale = 520f;
        private const float DefaultScreenFlightHudScale = 540f;
        private const float LegacyWeatherRadarSize = 372f;
        private const float LegacyTrafficRadarSize = 420f;
        private const float MinimumRadarSize = 220f;
        private const string HeadingTapeCanvasName = "FAAHeadingTapeCanvas";
        private const string HeadingTapeOverlayName = "FAA Heading Tape Overlay";
        private static readonly Color HudGreen = new Color(0.2f, 1f, 0.2f, 1f);
        private static readonly Color HudGreenDim = new Color(0.2f, 1f, 0.2f, 0.74f);
        private static readonly HashSet<int> BrokenSpriteImageInstanceIds = new HashSet<int>();
        private static readonly Vector2 LegacyScreenFlightHudAnchoredPosition = new Vector2(960f, 740f);
        private static readonly Vector2 DefaultScreenFlightHudAnchoredPosition = new Vector2(960f, 690f);
        private static readonly Vector2 HeadingTapeAnchoredPosition = new Vector2(-610f, 430f);
        private static readonly Vector2 HeadingTapeSize = new Vector2(600f, 38f);

        [Header("Duplicate HUD Protection")]
        [SerializeField] private bool disableWorldSpaceSymbologyCanvas = true;
        [SerializeField] private string worldSpaceCanvasName = "FAASymbologyCanvasWorldSpace";

        [Header("Opaque Block Cleanup")]
        [SerializeField] private bool hideLargeBlankHudImages = true;
        [SerializeField] private float minimumBlockSize = 48f;
        [SerializeField] private float minimumEffectiveBlockSize = 120f;

        [Header("Screen HUD Layout")]
        [SerializeField] private bool enforceScreenFlightHudLayout = true;
        [SerializeField] private string screenSymbologyCanvasName = "FAASymbologyCanvas";
        [SerializeField] private Vector2 screenFlightHudAnchoredPosition = new Vector2(960f, 690f);
        [SerializeField] private float screenFlightHudScale = DefaultScreenFlightHudScale;
        [SerializeField] private int screenFlightHudSortingOrder = 5000;
        [SerializeField] private bool hideLegacyOverlayGroups = true;
        [SerializeField] private bool suppressLegacyCompassStrips = true;

        [Header("Radar Pair Layout")]
        [SerializeField] private bool enforceRadarPairLayout = true;
        [SerializeField] private string weatherRadarCanvasName = "XPlaneWeatherRadarCanvas";
        [SerializeField] private string weatherRadarRootName = "X-Plane Weather Radar System";
        [SerializeField] private string trafficRadarCanvasName = "XPlaneTrafficRadarCanvas";
        [SerializeField] private string trafficRadarRootName = "Traffic Radar System";
        [SerializeField] private string indicatorCanvasName = "XPlaneWeatherIndicatorCanvas";
        [SerializeField] private Vector2 weatherRadarSize = new Vector2(280f, 280f);
        [SerializeField] private Vector2 trafficRadarSize = new Vector2(296f, 296f);
        [SerializeField] private Vector2 radarInset = new Vector2(28f, 28f);
        [SerializeField] private bool createRadarControlStrips = true;
        [SerializeField] private string radarControlsObjectName = DefaultRadarControlsObjectName;

        [Header("Deprecated 3D Weather")]
        [SerializeField] private bool deactivateDeprecatedWeather3DSystems = true;

        [Header("Cesium Presentation Cleanup")]
        [SerializeField] private bool hideCesiumCreditOverlay = true;

        [Header("HUD-Friendly Skybox")]
        [SerializeField] private bool tuneSkyboxForHudContrast = true;
        [SerializeField] private Color hudSkyTint = new Color(0.24f, 0.48f, 0.72f, 1f);
        [SerializeField] private Color hudHorizonTint = new Color(0.14f, 0.19f, 0.24f, 1f);
        [SerializeField] private float hudSkyboxExposure = 0.68f;

        [Header("Runtime Rescan")]
        [Tooltip("Opt in only for scenes that create HUD objects after startup. Continuous scans can override user-driven visibility, color, and layout changes.")]
        [SerializeField] private bool continuousRuntimeRescan = false;
        [SerializeField] private int initialFrameScans = 240;
        [SerializeField] private float rescanIntervalSeconds = 0.5f;

        private int _remainingInitialScans;
        private float _nextScanTime;
        private Material _sourceSkyboxMaterial;
        private Material _hudSkyboxMaterial;

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void QueueBrokenSpriteRepairOnEditorReload()
        {
            EditorApplication.delayCall -= RepairBrokenImageSpritesFromEditor;
            EditorApplication.delayCall += RepairBrokenImageSpritesFromEditor;
        }

        private static void RepairBrokenImageSpritesFromEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            RepairBrokenImageSpritesOnce();
            ClearSerializedXPlaneWeatherTexturesFromEditor();
        }
#endif

        private void Awake()
        {
            EnsureRuntimeDefaults();
            _remainingInitialScans = continuousRuntimeRescan ? initialFrameScans : 0;
            SanitizeNow();
        }

        private void OnEnable()
        {
            EnsureRuntimeDefaults();
            _remainingInitialScans = continuousRuntimeRescan ? initialFrameScans : 0;
            SanitizeNow();
        }

        private void Start()
        {
            SanitizeNow();
        }

        private void OnDestroy()
        {
            if (_hudSkyboxMaterial == null)
            {
                return;
            }

            if (RenderSettings.skybox == _hudSkyboxMaterial)
            {
                RenderSettings.skybox = _sourceSkyboxMaterial;
            }

            DestroySkyboxMaterial(_hudSkyboxMaterial);
            _hudSkyboxMaterial = null;
        }

        private void LateUpdate()
        {
            if (!continuousRuntimeRescan)
            {
                return;
            }

            bool shouldScan = _remainingInitialScans > 0 || Time.unscaledTime >= _nextScanTime;
            if (!shouldScan)
            {
                return;
            }

            if (_remainingInitialScans > 0)
            {
                _remainingInitialScans--;
            }

            _nextScanTime = Time.unscaledTime + Mathf.Max(0.1f, rescanIntervalSeconds);
            SanitizeNow();
        }

        [ContextMenu("Sanitize FAA HUD Now")]
        public void SanitizeNow()
        {
            EnsureRuntimeDefaults();

            if (tuneSkyboxForHudContrast && Application.isPlaying)
            {
                ApplyHudFriendlySkybox();
            }

            if (disableWorldSpaceSymbologyCanvas)
            {
                DisableDuplicateWorldSpaceHud();
            }

            NormalizeScreenSpaceLegacyHud();
            EnsureHeadingTapeOverlay();
            if (suppressLegacyCompassStrips)
            {
                SuppressLegacyCompassStrips();
            }

            if (enforceRadarPairLayout)
            {
                NormalizeRadarPairLayout();
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ClearSerializedXPlaneWeatherTexturesFromEditor();
            }
#endif

            if (deactivateDeprecatedWeather3DSystems)
            {
                DeactivateDeprecatedWeather3DSystems();
            }

            if (hideCesiumCreditOverlay)
            {
                HideCesiumCreditOverlay();
            }

            if (hideLargeBlankHudImages)
            {
#if UNITY_EDITOR
                RepairBrokenImageSpritesOnce();
#endif
                HideLargeBlankHudImages();
            }
        }

        private void ApplyHudFriendlySkybox()
        {
            Material activeSkybox = RenderSettings.skybox;
            if (activeSkybox == null || activeSkybox == _hudSkyboxMaterial)
            {
                return;
            }

            if (_hudSkyboxMaterial != null)
            {
                DestroySkyboxMaterial(_hudSkyboxMaterial);
            }

            _sourceSkyboxMaterial = activeSkybox;
            _hudSkyboxMaterial = new Material(activeSkybox)
            {
                name = activeSkybox.name + " (FAA HUD Contrast)",
                hideFlags = HideFlags.HideAndDontSave
            };

            SetMaterialColorIfPresent(_hudSkyboxMaterial, "_SkyTint", hudSkyTint);
            SetMaterialColorIfPresent(_hudSkyboxMaterial, "_Tint", new Color(0.70f, 0.82f, 0.94f, 1f));
            SetMaterialColorIfPresent(_hudSkyboxMaterial, "_GroundColor", hudHorizonTint);
            SetMaterialColorIfPresent(_hudSkyboxMaterial, "_NightSkyTint", new Color(0.035f, 0.07f, 0.13f, 1f));
            SetMaterialFloatIfPresent(_hudSkyboxMaterial, "_Exposure", Mathf.Clamp(hudSkyboxExposure, 0.2f, 1.5f));
            SetMaterialFloatIfPresent(_hudSkyboxMaterial, "_AtmosphereThickness", 0.82f);
            SetMaterialFloatIfPresent(_hudSkyboxMaterial, "_HorizonBrightness", 0.72f);
            SetMaterialFloatIfPresent(_hudSkyboxMaterial, "_SunHDR", 0.78f);

            RenderSettings.skybox = _hudSkyboxMaterial;
            RenderSettings.ambientIntensity = 0.82f;
            RenderSettings.reflectionIntensity = 0.58f;
            DynamicGI.UpdateEnvironment();
        }

        private static void SetMaterialColorIfPresent(Material material, string propertyName, Color value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetMaterialFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void DestroySkyboxMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(material);
            }
            else
            {
                Object.DestroyImmediate(material);
            }
        }

        private void EnsureHeadingTapeOverlay()
        {
            Canvas canvas = EnsureHeadingTapeCanvas();
            if (canvas == null)
            {
                return;
            }

            Transform existing = FindExistingHeadingTapeOverlay();
            bool createdOverlayObject = existing == null;
            GameObject overlayObject = existing != null
                ? existing.gameObject
                : new GameObject(HeadingTapeOverlayName, typeof(RectTransform));

            RectTransform rectTransform = EnsureRectTransform(overlayObject);
            if (rectTransform.parent != canvas.transform)
            {
                rectTransform.SetParent(canvas.transform, false);
            }
            rectTransform.SetAsLastSibling();
            overlayObject.name = HeadingTapeOverlayName;
            overlayObject.SetActive(true);
            RemoveDuplicateHeadingTapeOverlays(overlayObject);

            FaaHeadingTapeOverlay overlay = overlayObject.GetComponent<FaaHeadingTapeOverlay>() ??
                                           overlayObject.AddComponent<FaaHeadingTapeOverlay>();
            overlay.enabled = true;
            ConfigureHeadingTapeLayoutIfCreated(overlay, createdOverlayObject);
            overlay.SetDataSources(
                FindAnyObjectByType<AviationUI.AviationFlightDataProvider>(FindObjectsInactive.Include),
                FindAnyObjectByType<AircraftControl.Core.AircraftController>(FindObjectsInactive.Include),
                Camera.main != null ? Camera.main.transform : null);
        }

        private static void ConfigureHeadingTapeLayoutIfCreated(FaaHeadingTapeOverlay overlay, bool createdOverlayObject)
        {
            if (overlay != null && createdOverlayObject)
            {
                overlay.Configure(HeadingTapeAnchoredPosition, HeadingTapeSize, HudGreen, HudGreenDim);
            }
        }

        private static Transform FindExistingHeadingTapeOverlay()
        {
            foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform != null && transform.gameObject.name == HeadingTapeOverlayName && IsLoadedSceneObject(transform.gameObject))
                {
                    return transform;
                }
            }

            return null;
        }

        private static void RemoveDuplicateHeadingTapeOverlays(GameObject keep)
        {
            foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform == null || transform.gameObject == keep || transform.gameObject.name != HeadingTapeOverlayName ||
                    !IsLoadedSceneObject(transform.gameObject))
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(transform.gameObject);
                }
                else
                {
                    DestroyImmediate(transform.gameObject);
                }
            }
        }

        private Canvas EnsureHeadingTapeCanvas()
        {
            Canvas canvas = null;
            foreach (Canvas candidate in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate == null || candidate.gameObject.name != HeadingTapeCanvasName || !IsLoadedSceneObject(candidate.gameObject))
                {
                    continue;
                }

                canvas = candidate;
                break;
            }

            if (canvas == null)
            {
                GameObject canvasObject = new GameObject(
                    HeadingTapeCanvasName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(UnityEngine.UI.CanvasScaler),
                    typeof(GraphicRaycaster));
                Scene activeScene = SceneManager.GetActiveScene();
                if (activeScene.IsValid() && activeScene.isLoaded)
                {
                    SceneManager.MoveGameObjectToScene(canvasObject, activeScene);
                }

                canvas = canvasObject.GetComponent<Canvas>();
            }

            RemoveDuplicateHeadingTapeCanvases(canvas.gameObject);

            if (canvas.transform.parent != null)
            {
                canvas.transform.SetParent(null, false);
            }

            canvas.gameObject.name = HeadingTapeCanvasName;
            canvas.gameObject.SetActive(true);
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = screenFlightHudSortingOrder + 200;

            RectTransform rectTransform = EnsureRectTransform(canvas.gameObject);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;

            UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>() ??
                                                 canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>() ??
                                         canvas.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;
            return canvas;
        }

        private static void RemoveDuplicateHeadingTapeCanvases(GameObject keep)
        {
            foreach (Canvas candidate in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate == null ||
                    candidate.gameObject == keep ||
                    candidate.gameObject.name != HeadingTapeCanvasName)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(candidate.gameObject);
                }
                else
                {
                    DestroyImmediate(candidate.gameObject);
                }
            }
        }

        private Canvas FindScreenSymbologyCanvas()
        {
            Canvas bestCanvas = null;
            int bestScore = int.MinValue;
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas == null || canvas.gameObject.name != screenSymbologyCanvasName || !IsLoadedSceneObject(canvas.gameObject))
                {
                    continue;
                }

                string path = GetHierarchyPath(canvas.transform);
                int score = 0;
                if (path.Contains("/_ui/faasymbologycanvas"))
                {
                    score += 1000;
                }
                if (!path.Contains("faasymbologycanvasworldspace"))
                {
                    score += 500;
                }
                if (canvas.gameObject.activeInHierarchy)
                {
                    score += 250;
                }
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    score += 100;
                }

                if (bestCanvas == null || score > bestScore)
                {
                    bestCanvas = canvas;
                    bestScore = score;
                }
            }

            return bestCanvas;
        }

        private void EnsureRuntimeDefaults()
        {
            if (string.IsNullOrWhiteSpace(radarControlsObjectName))
            {
                radarControlsObjectName = DefaultRadarControlsObjectName;
                createRadarControlStrips = true;
            }

            if (Mathf.Approximately(screenFlightHudScale, LegacyScreenFlightHudScale) ||
                screenFlightHudScale < MinimumReadableScreenFlightHudScale)
            {
                screenFlightHudScale = DefaultScreenFlightHudScale;
            }

            if (Vector2.Distance(screenFlightHudAnchoredPosition, LegacyScreenFlightHudAnchoredPosition) < 0.5f)
            {
                screenFlightHudAnchoredPosition = DefaultScreenFlightHudAnchoredPosition;
            }

            if (Vector2.Distance(weatherRadarSize, Vector2.one * LegacyWeatherRadarSize) < 0.5f ||
                weatherRadarSize.x < MinimumRadarSize || weatherRadarSize.y < MinimumRadarSize)
            {
                weatherRadarSize = new Vector2(280f, 280f);
            }

            if (Vector2.Distance(trafficRadarSize, Vector2.one * LegacyTrafficRadarSize) < 0.5f ||
                trafficRadarSize.x < MinimumRadarSize || trafficRadarSize.y < MinimumRadarSize)
            {
                trafficRadarSize = new Vector2(296f, 296f);
            }
        }

        private void DisableDuplicateWorldSpaceHud()
        {
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas == null || canvas.gameObject.name != worldSpaceCanvasName || !IsLoadedSceneObject(canvas.gameObject))
                {
                    continue;
                }

                canvas.enabled = false;

                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null)
                {
                    raycaster.enabled = false;
                }

                UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                if (scaler != null)
                {
                    scaler.enabled = false;
                }

                if (canvas.gameObject.activeSelf)
                {
                    canvas.gameObject.SetActive(false);
                }
            }

        }

        private static void SuppressLegacyCompassStrips()
        {
            SuppressCompassNavigatorBars();
            SuppressGeneratedCompassBars();
        }

        private static void SuppressCompassNavigatorBars()
        {
            foreach (CompassPro compass in FindObjectsByType<CompassPro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (compass == null || !IsLoadedSceneObject(compass.gameObject))
                {
                    continue;
                }

                SetPrivateField(compass, "_showCompassBar", false);
                SetPrivateField(compass, "_showCardinalPoints", false);
                SetPrivateField(compass, "_showOrdinalPoints", false);
                SetPrivateField(compass, "_showHalfWinds", false);
                SetPrivateField(compass, "_alpha", 0f);
                SetPrivateField(compass, "_alwaysVisibleInEditMode", false);
                compass.enabled = false;

                foreach (Graphic graphic in compass.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.enabled = false;
                    graphic.raycastTarget = false;
                }

                foreach (Canvas canvas in compass.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.enabled = false;
                }

                foreach (CanvasGroup group in compass.GetComponentsInChildren<CanvasGroup>(true))
                {
                    group.alpha = 0f;
                    group.interactable = false;
                    group.blocksRaycasts = false;
                }

                HideCompassNavigatorChild(compass.transform);
                if (compass.gameObject.activeSelf)
                {
                    compass.gameObject.SetActive(false);
                }
            }
        }

        private static void SuppressGeneratedCompassBars()
        {
            foreach (Behaviour behaviour in FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || !IsLoadedSceneObject(behaviour.gameObject))
                {
                    continue;
                }

                string typeName = behaviour.GetType().FullName ?? string.Empty;
                if (typeName == "HUDControl.CompassBar.CompassBarElement")
                {
                    SetPrivateField(behaviour, "enableTapeScroll", false);
                    SetPrivateField(behaviour, "compassTape", null);
                    continue;
                }

                if (IsLegacyCompassStripControllerType(typeName))
                {
                    behaviour.enabled = false;
                }
            }

            foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform == null || !IsLoadedSceneObject(transform.gameObject))
                {
                    continue;
                }

                string path = GetHierarchyPath(transform);
                if (!IsLegacyCompassStripPath(path) && !IsLegacyCompassStripObjectName(transform.gameObject.name))
                {
                    continue;
                }

                SuppressCompassStripRoot(transform.gameObject);
            }
        }

        private static bool IsLegacyCompassStripControllerType(string typeName)
        {
            return typeName == "HUDControl.CompassBar.CompassBarGenerator" ||
                   typeName == "CompassBarSystem.CompassTapeGenerator" ||
                   typeName == "CompassBarSystem.CompassBarController";
        }

        private static bool IsLegacyCompassStripPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return path.Contains("/heading panel/compass bar generated") ||
                   path.Contains("/compass bar generated") ||
                   path.Contains("/faa_compasstape") ||
                   path.Contains("/maskcanvas") ||
                   path.Contains("/masker/compassnavigatorpro") ||
                   path.Contains("/compassnavigatorpro");
        }

        private static bool IsLegacyCompassStripObjectName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            string lowerName = objectName.ToLowerInvariant();
            return lowerName == "compass bar generated" ||
                   lowerName == "faa_compasstape" ||
                   lowerName == "maskcanvas" ||
                   lowerName == "masker" ||
                   lowerName == "compassnavigatorpro";
        }

        private static void SuppressCompassStripRoot(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().FullName ?? string.Empty;
                if (typeName == "HUDControl.CompassBar.CompassBarElement")
                {
                    SetPrivateField(behaviour, "enableTapeScroll", false);
                    SetPrivateField(behaviour, "compassTape", null);
                }
                else if (IsLegacyCompassStripControllerType(typeName) ||
                         typeName == "CompassNavigatorPro.CompassPro")
                {
                    behaviour.enabled = false;
                }
            }

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null)
                {
                    continue;
                }

                graphic.enabled = false;
                graphic.raycastTarget = false;
                Color color = graphic.color;
                color.a = 0f;
                graphic.color = color;
            }

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null)
                {
                    continue;
                }

                text.enabled = false;
                text.raycastTarget = false;
                text.color = new Color(text.color.r, text.color.g, text.color.b, 0f);
                text.canvasRenderer.SetAlpha(0f);
            }

            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                canvas.enabled = false;
            }

            foreach (CanvasGroup group in root.GetComponentsInChildren<CanvasGroup>(true))
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            foreach (CanvasRenderer renderer in root.GetComponentsInChildren<CanvasRenderer>(true))
            {
                renderer.cull = true;
                renderer.SetAlpha(0f);
            }

            if (root.activeSelf)
            {
                root.SetActive(false);
            }
        }

        private static void HideCompassNavigatorChild(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                string lowerName = child.gameObject.name.ToLowerInvariant();
                if (lowerName.Contains("compassbar") ||
                    lowerName.StartsWith("cardinal") ||
                    lowerName.StartsWith("intercardinal") ||
                    lowerName.Contains("compass icon"))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void HideLargeBlankHudImages()
        {
            foreach (Graphic graphic in FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (graphic == null || graphic.color.a <= 0.01f || !IsLoadedSceneObject(graphic.gameObject))
                {
                    continue;
                }

                if (!IsFaaHudGraphic(graphic) || IsAllowedHudChrome(graphic))
                {
                    continue;
                }

                if (!IsBlankImageBlock(graphic))
                {
                    continue;
                }

                Color color = graphic.color;
                color.a = 0f;
                graphic.color = color;
                graphic.raycastTarget = false;
            }
        }

        private void NormalizeScreenSpaceLegacyHud()
        {
            GameObject preferredScreenFlightHud = enforceScreenFlightHudLayout
                ? FindPreferredScreenFlightHud()
                : null;

            foreach (RectTransform rectTransform in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rectTransform == null || !IsLoadedSceneObject(rectTransform.gameObject))
                {
                    continue;
                }

                string path = GetHierarchyPath(rectTransform);
                if (rectTransform.gameObject.name == screenSymbologyCanvasName)
                {
                    NormalizeScreenSymbologyCanvas(rectTransform);
                    continue;
                }

                if (rectTransform.gameObject.name == "Second Interation GUI")
                {
                    bool isScreenFlightHud = IsScreenFlightHudPath(path);
                    if (enforceScreenFlightHudLayout && isScreenFlightHud)
                    {
                        if (preferredScreenFlightHud != null && rectTransform.gameObject != preferredScreenFlightHud)
                        {
                            SuppressDuplicateHudRoot(rectTransform.gameObject);
                            continue;
                        }

                        rectTransform.anchorMin = Vector2.zero;
                        rectTransform.anchorMax = Vector2.zero;
                        rectTransform.anchoredPosition = screenFlightHudAnchoredPosition;
                        rectTransform.sizeDelta = new Vector2(100f, 100f);
                        rectTransform.localScale = Vector3.one * Mathf.Max(1f, screenFlightHudScale);
                        rectTransform.localRotation = Quaternion.identity;
                        if (!rectTransform.gameObject.activeSelf)
                        {
                            rectTransform.gameObject.SetActive(true);
                        }

                        Canvas canvas = rectTransform.GetComponent<Canvas>();
                        if (canvas != null)
                        {
                            canvas.overrideSorting = true;
                            canvas.sortingOrder = screenFlightHudSortingOrder;
                        }

                        SuppressDuplicatePitchLadder(rectTransform.gameObject);
                        NormalizeFlightHudSymbology(rectTransform.gameObject);
                    }
                    else if (!isScreenFlightHud)
                    {
                        SuppressDuplicateHudRoot(rectTransform.gameObject);
                    }

                    continue;
                }

                if (hideLegacyOverlayGroups && ShouldHideLegacyOverlayGroup(path) &&
                    rectTransform.gameObject.activeSelf)
                {
                    rectTransform.gameObject.SetActive(false);
                }
            }
        }

        private void NormalizeScreenSymbologyCanvas(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.gameObject.SetActive(true);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;

            Canvas canvas = rectTransform.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = true;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = screenFlightHudSortingOrder;
            }

            UnityEngine.UI.CanvasScaler scaler = rectTransform.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            GraphicRaycaster raycaster = rectTransform.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = true;
            }
        }

        private void NormalizeRadarPairLayout()
        {
            EnsureOverlayCanvas(indicatorCanvasName, screenFlightHudSortingOrder + 20, false);

            Canvas weatherCanvas = EnsureOverlayCanvas(weatherRadarCanvasName, screenFlightHudSortingOrder + 25, true);
            GameObject weatherRoot = FindNamedRoot(weatherRadarRootName);
            if (weatherRoot != null && weatherCanvas != null)
            {
                PositionRadarRoot(
                    weatherRoot,
                    weatherCanvas.transform,
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(radarInset.x, radarInset.y),
                    weatherRadarSize);
                EnableWeatherReferenceOverlays(weatherRoot);
            }

            Canvas trafficCanvas = EnsureOverlayCanvas(trafficRadarCanvasName, screenFlightHudSortingOrder + 80, true);
            GameObject trafficRoot = FindPreferredTrafficRadarRoot();
            if (trafficRoot != null && trafficCanvas != null)
            {
                PositionRadarRoot(
                    trafficRoot,
                    trafficCanvas.transform,
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(-radarInset.x, radarInset.y),
                    trafficRadarSize);

                NormalizeTrafficRadarDisplay(trafficRoot);
                SuppressDuplicateTrafficRadarRoots(trafficRoot);
            }

            if (createRadarControlStrips)
            {
                EnsureRadarControlsOverlay(weatherRoot, trafficRoot);
            }
        }

        private void NormalizeTrafficRadarDisplay(GameObject trafficRoot)
        {
            Transform radarDisplay = FindChildRecursive(trafficRoot != null ? trafficRoot.transform : null, "Radar Display");
            if (radarDisplay == null)
            {
                return;
            }

            radarDisplay.gameObject.SetActive(true);
            RectTransform displayRect = EnsureRectTransform(radarDisplay.gameObject);
            displayRect.SetParent(trafficRoot.transform, false);
            displayRect.anchorMin = Vector2.zero;
            displayRect.anchorMax = Vector2.one;
            displayRect.pivot = new Vector2(0.5f, 0.5f);
            displayRect.anchoredPosition = Vector2.zero;
            displayRect.sizeDelta = Vector2.zero;
            displayRect.localScale = Vector3.one;
            displayRect.localRotation = Quaternion.identity;

            foreach (CanvasGroup group in radarDisplay.GetComponentsInChildren<CanvasGroup>(true))
            {
                group.alpha = 1f;
                group.interactable = true;
            }

            RestoreTrafficDisplayMask(radarDisplay.gameObject);
            foreach (TrafficRadar.TrafficRadarDisplay display in radarDisplay.GetComponentsInChildren<TrafficRadar.TrafficRadarDisplay>(true))
            {
                display.enabled = true;
                display.ConfigureHudPresentation(0.34f, 0.28f);
                display.PreferXPlaneTrafficTexture = false;
                RestoreDesignedRadarImage(display.RadarImage);
            }
        }

        private static void RestoreTrafficDisplayMask(GameObject radarDisplay)
        {
            if (radarDisplay == null)
            {
                return;
            }

            foreach (Mask mask in radarDisplay.GetComponents<Mask>())
            {
                if (mask != null)
                {
                    mask.enabled = true;
                    mask.showMaskGraphic = false;
                }
            }

            UnityEngine.UI.Image image = radarDisplay.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                image.color = new Color(0.01f, 0.08f, 0.075f, 0f);
                image.raycastTarget = false;
            }
        }

        private static void RestoreDesignedRadarImage(RawImage image)
        {
            if (image == null)
            {
                return;
            }

            image.gameObject.SetActive(true);
            image.enabled = true;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private static void EnableWeatherReferenceOverlays(GameObject weatherRoot)
        {
            if (weatherRoot == null)
            {
                return;
            }

            foreach (XPlaneOriginalWeatherRadarDisplay display in weatherRoot.GetComponentsInChildren<XPlaneOriginalWeatherRadarDisplay>(true))
            {
                if (display != null)
                {
                    // The FAA radar is rendered from live X-Plane datarefs.
                    // Keep the legacy reference/raster treatment out of the
                    // presentation; the procedural display owns its own
                    // modern grid and return styling.
                    display.ShowReferenceOverlay = false;
                    display.ConfigureHudPresentation(0.82f);
                }
            }

            StyleWeatherRadarGlass(weatherRoot);

            foreach (XPlaneWeatherRadarOverlay overlay in weatherRoot.GetComponentsInChildren<XPlaneWeatherRadarOverlay>(true))
            {
                if (overlay == null)
                {
                    continue;
                }

                overlay.gameObject.SetActive(false);
                overlay.enabled = false;
                RawImage image = overlay.GetComponent<RawImage>();
                if (image != null)
                {
                    image.enabled = false;
                    image.raycastTarget = false;
                }
            }
        }

        private static void StyleWeatherRadarGlass(GameObject weatherRoot)
        {
            foreach (UnityEngine.UI.Image image in weatherRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (image == null)
                {
                    continue;
                }

                string objectName = image.gameObject.name;
                bool isRootPlate = image.transform == weatherRoot.transform;
                bool isBackground = objectName.IndexOf("Background", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    objectName.IndexOf("Backplate", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    objectName.IndexOf("Bezel", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isRootPlate && !isBackground)
                {
                    continue;
                }

                image.color = isRootPlate
                    ? new Color(0.005f, 0.04f, 0.03f, 0f)
                    : new Color(0.008f, 0.065f, 0.05f, 0.06f);
                image.raycastTarget = false;
            }
        }

        private void SuppressDuplicateTrafficRadarRoots(GameObject keep)
        {
            if (keep == null)
            {
                return;
            }

            foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform == null || transform.gameObject == keep ||
                    transform.gameObject.name != trafficRadarRootName ||
                    !IsLoadedSceneObject(transform.gameObject))
                {
                    continue;
                }

                string path = GetHierarchyPath(transform);
                bool legacyRadarCanvas = path.Contains("/faasymbologycanvas/radarcanvas") ||
                                         path.Contains("faasymbologycanvasworldspace");
                if (!legacyRadarCanvas && transform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                DisableTrafficTextureMode(transform.gameObject);
                SuppressDuplicateHudRoot(transform.gameObject);
            }
        }

        private static void DisableTrafficTextureMode(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (TrafficRadar.TrafficRadarDisplay display in root.GetComponentsInChildren<TrafficRadar.TrafficRadarDisplay>(true))
            {
                if (display != null)
                {
                    display.PreferXPlaneTrafficTexture = false;
                }
            }
        }

        private void EnsureRadarControlsOverlay(GameObject weatherRoot, GameObject trafficRoot)
        {
            Transform parent = weatherRoot != null
                ? weatherRoot.transform.parent
                : trafficRoot != null ? trafficRoot.transform.parent : null;
            if (parent == null)
            {
                return;
            }

            GameObject controlsObject = FindNamedRoot(radarControlsObjectName);
            if (controlsObject == null)
            {
                controlsObject = new GameObject(radarControlsObjectName, typeof(RectTransform));
            }

            RectTransform rectTransform = EnsureRectTransform(controlsObject);
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.pivot = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            controlsObject.SetActive(true);

            FaaRadarControlsOverlay controls = controlsObject.GetComponent<FaaRadarControlsOverlay>() ??
                                               controlsObject.AddComponent<FaaRadarControlsOverlay>();
            controls.Configure(weatherRoot != null ? weatherRoot.transform : null, trafficRoot != null ? trafficRoot.transform : null);
        }

        private static Canvas EnsureOverlayCanvas(string objectName, int sortingOrder, bool raycasterEnabled)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Canvas canvas = null;
            foreach (Canvas candidate in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate == null || candidate.gameObject.name != objectName)
                {
                    continue;
                }

                int candidateScore = ScoreOverlayCanvas(candidate);
                int canvasScore = canvas != null ? ScoreOverlayCanvas(canvas) : int.MinValue;
                if (canvas == null || candidateScore > canvasScore)
                {
                    canvas = candidate;
                }
            }

            if (canvas == null)
            {
                GameObject canvasObject = new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(UnityEngine.UI.CanvasScaler),
                    typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
            }

            RehomeDuplicateOverlayCanvases(objectName, canvas);

            if (canvas.transform.parent != null)
            {
                canvas.transform.SetParent(null, false);
            }

            canvas.gameObject.name = objectName;
            canvas.gameObject.SetActive(true);
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            RectTransform rectTransform = EnsureRectTransform(canvas.gameObject);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;

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
            raycaster.enabled = raycasterEnabled;
            return canvas;
        }

        private static int ScoreOverlayCanvas(Canvas canvas)
        {
            if (canvas == null)
            {
                return int.MinValue;
            }

            int score = canvas.transform.childCount;
            if (canvas.gameObject.activeInHierarchy)
            {
                score += 100;
            }
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                score += 50;
            }
            if (canvas.gameObject.name == "XPlaneTrafficRadarCanvas" || canvas.gameObject.name == "XPlaneWeatherRadarCanvas")
            {
                score += 25;
            }

            return score;
        }

        private static void RehomeDuplicateOverlayCanvases(string objectName, Canvas keep)
        {
            if (keep == null)
            {
                return;
            }

            foreach (Canvas duplicate in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (duplicate == null || duplicate == keep || duplicate.gameObject.name != objectName)
                {
                    continue;
                }

                while (duplicate.transform.childCount > 0)
                {
                    duplicate.transform.GetChild(0).SetParent(keep.transform, false);
                }

                if (Application.isPlaying)
                {
                    Destroy(duplicate.gameObject);
                }
                else
                {
                    DestroyImmediate(duplicate.gameObject);
                }
            }
        }

        private static void PositionRadarRoot(
            GameObject root,
            Transform parent,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            if (root == null || parent == null)
            {
                return;
            }

            RectTransform rectTransform = EnsureRectTransform(root);
            rectTransform.SetParent(parent, false);
            root.SetActive(true);
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
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

        private static GameObject FindNamedRoot(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform != null && transform.gameObject.name == objectName && IsLoadedSceneObject(transform.gameObject))
                {
                    return transform.gameObject;
                }
            }

            return null;
        }

        private GameObject FindPreferredTrafficRadarRoot()
        {
            GameObject bestRoot = null;
            int bestScore = int.MinValue;
            GameObject fallback = null;

            foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform == null || transform.gameObject.name != trafficRadarRootName || !IsLoadedSceneObject(transform.gameObject))
                {
                    continue;
                }

                fallback ??= transform.gameObject;
                string path = GetHierarchyPath(transform);
                int score = ScoreTrafficRadarRoot(transform.gameObject, path);
                if (score > bestScore)
                {
                    bestRoot = transform.gameObject;
                    bestScore = score;
                }
            }

            return bestRoot != null ? bestRoot : fallback;
        }

        private static int ScoreTrafficRadarRoot(GameObject root, string path)
        {
            if (root == null)
            {
                return int.MinValue;
            }

            string lowerPath = (path ?? string.Empty).ToLowerInvariant();
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
            if (root.activeSelf)
            {
                score += 500;
            }
            if (root.activeInHierarchy)
            {
                score += 500;
            }

            Transform radarDisplay = FindChildRecursive(root.transform, "Radar Display");
            if (radarDisplay != null)
            {
                score += radarDisplay.gameObject.activeSelf ? 250 : 50;
                TrafficRadar.TrafficRadarDisplay display =
                    radarDisplay.GetComponentInChildren<TrafficRadar.TrafficRadarDisplay>(true);
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
            }

            return score;
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            foreach (Transform child in parent)
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

        private static bool IsLoadedSceneObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            Scene scene = gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private static bool TryGetLoadedSceneGameObject(Component component, out GameObject gameObject)
        {
            gameObject = null;

            try
            {
                if (component == null)
                {
                    return false;
                }

                gameObject = component.gameObject;
            }
            catch (System.Exception ex) when (ex is MissingReferenceException ||
                                              ex is System.InvalidCastException ||
                                              ex is System.NullReferenceException)
            {
                return false;
            }

            return IsLoadedSceneObject(gameObject);
        }

        private static bool TryGetGameObjectName(GameObject gameObject, out string objectName)
        {
            objectName = null;
            try
            {
                if (gameObject == null)
                {
                    return false;
                }

                objectName = gameObject.name;
                return true;
            }
            catch (System.Exception ex) when (ex is MissingReferenceException ||
                                              ex is System.InvalidCastException ||
                                              ex is System.NullReferenceException)
            {
                return false;
            }
        }

        private static bool TryGetActiveSelf(GameObject gameObject)
        {
            try
            {
                return gameObject != null && gameObject.activeSelf;
            }
            catch (System.Exception ex) when (ex is MissingReferenceException ||
                                              ex is System.InvalidCastException ||
                                              ex is System.NullReferenceException)
            {
                return false;
            }
        }

        private static void TrySetActive(GameObject gameObject, bool active)
        {
            try
            {
                if (gameObject != null)
                {
                    gameObject.SetActive(active);
                }
            }
            catch (System.Exception ex) when (ex is MissingReferenceException ||
                                              ex is System.InvalidCastException ||
                                              ex is System.NullReferenceException)
            {
            }
        }

        private static bool IsFaaHudGraphic(Graphic graphic)
        {
            string path = GetHierarchyPath(graphic.transform);
            return path.Contains("faasymbologycanvas");
        }

        private static bool IsAllowedHudChrome(Graphic graphic)
        {
            string path = GetHierarchyPath(graphic.transform);
            return path.Contains("/second interation gui/") ||
                   path.Contains("/maskcanvas/") ||
                   graphic.GetComponent<Mask>() != null ||
                   graphic.GetComponent<RectMask2D>() != null;
        }

        private static bool IsScreenFlightHudPath(string path)
        {
            return path.Contains("/faasymbologycanvas/second interation gui") &&
                   !path.Contains("faasymbologycanvasworldspace");
        }

        private static GameObject FindPreferredScreenFlightHud()
        {
            GameObject best = null;
            int bestScore = int.MinValue;

            foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform == null || transform.gameObject.name != "Second Interation GUI" || !IsLoadedSceneObject(transform.gameObject))
                {
                    continue;
                }

                string path = GetHierarchyPath(transform);
                if (!IsScreenFlightHudPath(path))
                {
                    continue;
                }

                int score = ScoreHudRoot(transform.gameObject);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = transform.gameObject;
                }
            }

            return best;
        }

        private static int ScoreHudRoot(GameObject root)
        {
            if (root == null)
            {
                return int.MinValue;
            }

            int score = 0;
            if (root.activeSelf)
            {
                score += 1000;
            }

            if (root.activeInHierarchy)
            {
                score += 500;
            }

            foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().FullName ?? string.Empty;
                if (typeName.StartsWith("HUDControl.", System.StringComparison.Ordinal))
                {
                    score += behaviour.enabled ? 60 : 20;
                }
            }

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic != null && graphic.color.a > 0.01f)
                {
                    score += graphic.gameObject.activeSelf ? 2 : 1;
                }
            }

            return score;
        }

        private static void SuppressDuplicateHudRoot(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                canvas.enabled = false;
            }

            foreach (GraphicRaycaster raycaster in root.GetComponentsInChildren<GraphicRaycaster>(true))
            {
                raycaster.enabled = false;
            }

            if (root.activeSelf)
            {
                root.SetActive(false);
            }
        }

        private static void SuppressDuplicatePitchLadder(GameObject legacyHudRoot)
        {
            if (legacyHudRoot == null)
            {
                return;
            }

            Transform scaleMasker = legacyHudRoot.transform.Find("Attitude/ScaleMasker");
            if (scaleMasker == null)
            {
                return;
            }

            Transform primaryScale = scaleMasker.Find("Scale");
            if (primaryScale != null && !primaryScale.gameObject.activeSelf)
            {
                primaryScale.gameObject.SetActive(true);
            }

            Transform duplicateScale = scaleMasker.Find("ScaleIteration2");
            if (duplicateScale == null)
            {
                return;
            }

            RepointAttitudePitchLadder(legacyHudRoot, primaryScale as RectTransform, duplicateScale as RectTransform, scaleMasker as RectTransform);

            foreach (Graphic graphic in duplicateScale.GetComponentsInChildren<Graphic>(true))
            {
                graphic.enabled = false;
                graphic.raycastTarget = false;
            }

            CanvasRenderer[] renderers = duplicateScale.GetComponentsInChildren<CanvasRenderer>(true);
            foreach (CanvasRenderer renderer in renderers)
            {
                renderer.cull = true;
            }

            if (duplicateScale.gameObject.activeSelf)
            {
                duplicateScale.gameObject.SetActive(false);
            }
        }

        private static void RepointAttitudePitchLadder(
            GameObject legacyHudRoot,
            RectTransform primaryScale,
            RectTransform duplicateScale,
            RectTransform scaleMasker)
        {
            if (legacyHudRoot == null || primaryScale == null)
            {
                return;
            }

            foreach (Behaviour behaviour in legacyHudRoot.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().FullName != "HUDControl.Elements.AttitudeIndicatorElement")
                {
                    continue;
                }

                SetRectTransformFieldIfDuplicate(behaviour, "pitchLadder", primaryScale, duplicateScale);
                if (scaleMasker != null)
                {
                    SetRectTransformFieldIfMissing(behaviour, "maskContainer", scaleMasker);
                }
            }
        }

        private static void SetRectTransformFieldIfDuplicate(
            Component component,
            string fieldName,
            RectTransform preferred,
            RectTransform duplicate)
        {
            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || !typeof(RectTransform).IsAssignableFrom(field.FieldType))
            {
                return;
            }

            RectTransform current = field.GetValue(component) as RectTransform;
            if (current == null || current == duplicate || current.gameObject.name == "ScaleIteration2")
            {
                field.SetValue(component, preferred);
            }
        }

        private static void SetRectTransformFieldIfMissing(Component component, string fieldName, RectTransform fallback)
        {
            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || !typeof(RectTransform).IsAssignableFrom(field.FieldType))
            {
                return;
            }

            if (field.GetValue(component) == null)
            {
                field.SetValue(component, fallback);
            }
        }

        private static void SetPrivateField(Component component, string fieldName, object value)
        {
            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(component, value);
            }
        }

        private static void NormalizeFlightHudSymbology(GameObject legacyHudRoot)
        {
            if (legacyHudRoot == null)
            {
                return;
            }

            Transform generatedCompass = legacyHudRoot.transform.Find("Heading Panel/Compass Bar Generated");
            if (generatedCompass != null)
            {
                SuppressCompassStripRoot(generatedCompass.gameObject);
            }

            foreach (Behaviour behaviour in legacyHudRoot.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().FullName != "HUDControl.CompassBar.CompassBarElement")
                {
                    continue;
                }

                SetPrivateField(behaviour, "enableTapeScroll", false);
                SetPrivateField(behaviour, "compassTape", null);
            }

            foreach (Graphic graphic in legacyHudRoot.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null || ShouldSkipHudColorTarget(graphic.transform))
                {
                    continue;
                }

                graphic.raycastTarget = false;
            }

            foreach (TMP_Text text in legacyHudRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null || ShouldSkipHudColorTarget(text.transform))
                {
                    continue;
                }

                if (text.font == null && TMP_Settings.defaultFontAsset != null)
                {
                    text.font = TMP_Settings.defaultFontAsset;
                }
                text.raycastTarget = false;
            }

            foreach (Text text in legacyHudRoot.GetComponentsInChildren<Text>(true))
            {
                if (text == null || ShouldSkipHudColorTarget(text.transform))
                {
                    continue;
                }

                text.raycastTarget = false;
            }
        }

        private static bool ShouldSkipHudColorTarget(Transform transform)
        {
            string path = GetHierarchyPath(transform);
            return path.Contains("/compass bar generated") ||
                   path.Contains("/radarcanvas/") ||
                   path.Contains("/maskcanvas/") ||
                   path.Contains("/compassnavigatorpro") ||
                   path.Contains("/visualunderstanding") ||
                   path.Contains("/analysis trigger buttons") ||
                   path.Contains("/vc/") ||
                   path.Contains("/voice");
        }

        private static bool ShouldHideLegacyOverlayGroup(string path)
        {
            return path.Contains("/_ui/faasymbologycanvas/maskcanvas") ||
                   path.Contains("/_ui/faasymbologycanvas/compassnavigatorpro") ||
                   path.Contains("/heading panel/compass bar generated") ||
                   path.Contains("/_ui/faasymbologycanvas/visualunderstanding") ||
                   path.Contains("/_ui/faasymbologycanvas/vc") ||
                   path.Contains("/_ui/faasymbologycanvas/[indicator system]") ||
                   path.Contains("/_ui/faasymbologycanvas/analysis trigger buttons") ||
                   path.EndsWith("/faasymbologycanvas/radarcanvas/weather radar system/radarpanel") ||
                   path.EndsWith("/faasymbologycanvas/radarcanvas/weather radar system/controlpanel") ||
                   path.EndsWith("/faasymbologycanvas/radarcanvas/traffic radar system/radar display") ||
                   path.EndsWith("/faasymbologycanvas/radarcanvas/traffic range ui") ||
                   path.EndsWith("/faasymbologycanvas/radarcanvas/traffic radar system/radar display/mapcanvas/map image");
        }

        private static void DeactivateDeprecatedWeather3DSystems()
        {
            foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform == null || !IsLoadedSceneObject(transform.gameObject))
                {
                    continue;
                }

                string path = GetHierarchyPath(transform);
                if (IsSupportedWeatherOverlayPath(path))
                {
                    continue;
                }

                if (!IsDeprecatedWeather3DName(transform.gameObject.name))
                {
                    continue;
                }

                if (transform.gameObject.activeSelf)
                {
                    transform.gameObject.SetActive(false);
                }
            }

            foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform == null || !IsLoadedSceneObject(transform.gameObject))
                {
                    continue;
                }

                string objectName = transform.gameObject.name;
                if (!string.Equals(objectName, "UniStorm Clouds", System.StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(objectName, "UniStorm Clouds (Lightning)", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (transform.gameObject.activeSelf)
                {
                    transform.gameObject.SetActive(false);
                }

                if (transform.TryGetComponent<MeshRenderer>(out MeshRenderer renderer))
                {
                    renderer.enabled = false;
                }
            }

            foreach (Behaviour behaviour in FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || !IsLoadedSceneObject(behaviour.gameObject))
                {
                    continue;
                }

                string path = GetHierarchyPath(behaviour.transform);
                if (IsSupportedWeatherOverlayPath(path))
                {
                    continue;
                }

                string typeName = behaviour.GetType().FullName ?? string.Empty;
                if (!IsDeprecatedWeather3DType(typeName))
                {
                    continue;
                }

                behaviour.enabled = false;
            }
        }

        private static bool IsSupportedWeatherOverlayPath(string path)
        {
            return path.Contains("/xplaneweatherindicatorcanvas") ||
                   path.Contains("/x-plane weather radar system") ||
                   path.Contains("/xplaneweatherradarcanvas") ||
                   path.Contains("/[indicator system]");
        }

        private static bool IsDeprecatedWeather3DName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            string lowerName = objectName.ToLowerInvariant();
            return lowerName.Contains("weathervisualization3d") ||
                   lowerName.Contains("weather3d") ||
                   lowerName.Contains("weather 3d") ||
                   lowerName.Contains("volumetric weather") ||
                   lowerName.Contains("unistorm system") ||
                   lowerName.Contains("unistorm clouds") ||
                   lowerName.Contains("weathersimulator") ||
                   lowerName.Contains("precipitationvfx") ||
                   lowerName.Contains("intensitypillarrenderer") ||
                   lowerName.Contains("volumetriclightning");
        }

        private static bool IsDeprecatedWeather3DType(string typeName)
        {
            return typeName.StartsWith("WeatherVisualization3D.", System.StringComparison.Ordinal) ||
                   typeName.StartsWith("WeatherRadar.Weather3D.", System.StringComparison.Ordinal) ||
                   typeName.StartsWith("Weather3D.", System.StringComparison.Ordinal) ||
                   typeName == "UniStormSystem" ||
                   typeName == "IndicatorSystem.Integration.Weather3DIndicatorBridge";
        }

        private static void HideCesiumCreditOverlay()
        {
            foreach (Behaviour behaviour in Resources.FindObjectsOfTypeAll<Behaviour>())
            {
                if (behaviour == null || !IsMutableCesiumCreditObject(behaviour.gameObject))
                {
                    continue;
                }

                string typeName = behaviour.GetType().FullName ?? string.Empty;
                if (typeName != "CesiumForUnity.CesiumCreditSystemUI")
                {
                    continue;
                }

                behaviour.enabled = false;
            }

            foreach (UIDocument document in Resources.FindObjectsOfTypeAll<UIDocument>())
            {
                if (document == null || !IsMutableCesiumCreditObject(document.gameObject) || !IsCesiumCreditUi(document))
                {
                    continue;
                }

                HideCesiumCreditVisualElements(document);
                document.enabled = false;
            }
        }

        private static bool IsMutableCesiumCreditObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            if (IsLoadedSceneObject(gameObject))
            {
                return true;
            }

            if (gameObject.name == "CesiumCreditSystemDefault")
            {
                return true;
            }

            bool isHiddenRuntimeObject = (gameObject.hideFlags & HideFlags.HideAndDontSave) == HideFlags.HideAndDontSave ||
                                         (gameObject.hideFlags & HideFlags.DontSaveInEditor) == HideFlags.DontSaveInEditor;
            return isHiddenRuntimeObject && gameObject.name.Contains("CesiumCreditSystem");
        }

        private static bool IsCesiumCreditUi(UIDocument document)
        {
            if (document == null)
            {
                return false;
            }

            string path = GetHierarchyPath(document.transform);
            if (path.Contains("cesiumcreditsystem"))
            {
                return true;
            }

            VisualElement root = document.rootVisualElement;
            return root != null &&
                   (root.Q("OnScreenCredits") != null || root.Q("PopupCredits") != null);
        }

        private static void HideCesiumCreditVisualElements(UIDocument document)
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null)
            {
                return;
            }

            root.style.display = DisplayStyle.None;
            HideVisualElement(root.Q("OnScreenCredits"));
            HideVisualElement(root.Q("PopupCredits"));
        }

        private static void HideVisualElement(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            element.Clear();
            element.visible = false;
            element.style.display = DisplayStyle.None;
        }

#if UNITY_EDITOR
        [InitializeOnLoad]
        private static class CesiumCreditOverlayEditorSuppressor
        {
            private const double SuppressIntervalSeconds = 0.5;
            private static double _nextSuppressTime;

            static CesiumCreditOverlayEditorSuppressor()
            {
                EditorApplication.update -= OnEditorUpdate;
                EditorApplication.update += OnEditorUpdate;
            }

            private static void OnEditorUpdate()
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    return;
                }

                double now = EditorApplication.timeSinceStartup;
                if (now < _nextSuppressTime)
                {
                    return;
                }

                _nextSuppressTime = now + SuppressIntervalSeconds;
                SanitizeLoadedHudOverlays();
            }

            private static void SanitizeLoadedHudOverlays()
            {
                // This editor callback exists only to suppress Cesium's transient
                // credit document. Running the complete HUD sanitizer here used to
                // rescan and mutate the entire integrated FAA_OPL scene every half
                // second, pinning the editor main thread on large terrain scenes.
                // Runtime normalization still runs deterministically from Awake,
                // OnEnable, and Start on each sanitizer instance.
                HideCesiumCreditOverlay();
            }
        }
#endif

        private bool IsBlankImageBlock(Graphic graphic)
        {
            RectTransform rectTransform = graphic.rectTransform;
            if (rectTransform == null)
            {
                return false;
            }

            Rect rect = rectTransform.rect;
            float width = Mathf.Abs(rect.width);
            float height = Mathf.Abs(rect.height);
            if (width <= 0.01f || height <= 0.01f)
            {
                return false;
            }

            float shortest = Mathf.Min(width, height);
            float longest = Mathf.Max(width, height);
            bool isThinLine = shortest <= 6f || (shortest <= 10f && longest / Mathf.Max(shortest, 0.01f) >= 8f);
            if (isThinLine)
            {
                return false;
            }

            bool isBlankImage = graphic is UnityEngine.UI.Image image && image.sprite == null;
            bool isBlankRawImage = graphic is RawImage rawImage && rawImage.texture == null;
            if (!isBlankImage && !isBlankRawImage)
            {
                return false;
            }

            float effectiveWidth = width * Mathf.Abs(graphic.transform.lossyScale.x);
            float effectiveHeight = height * Mathf.Abs(graphic.transform.lossyScale.y);
            bool largeRect = width >= minimumBlockSize && height >= minimumBlockSize;
            bool largeEffectiveRect = effectiveWidth >= minimumEffectiveBlockSize && effectiveHeight >= minimumEffectiveBlockSize;

            return largeRect || largeEffectiveRect;
        }

#if UNITY_EDITOR
        private static void RepairBrokenImageSpritesOnce()
        {
            foreach (UnityEngine.UI.Image image in FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (image == null || !IsLoadedSceneObject(image.gameObject))
                {
                    continue;
                }

                int instanceId = image.GetInstanceID();
                if (BrokenSpriteImageInstanceIds.Contains(instanceId))
                {
                    continue;
                }

                Sprite sprite = null;
                try
                {
                    sprite = image.sprite;
                    if (sprite == null)
                    {
                        continue;
                    }

                    _ = sprite.texture;
                }
                catch (System.Exception ex) when (ex is MissingReferenceException ||
                                                  ex is System.InvalidCastException ||
                                                  ex is System.NullReferenceException)
                {
                    BrokenSpriteImageInstanceIds.Add(instanceId);
                    SerializedObject serializedImage = new SerializedObject(image);
                    SerializedProperty spriteProperty = serializedImage.FindProperty("m_Sprite");
                    if (spriteProperty != null)
                    {
                        spriteProperty.objectReferenceValue = null;
                    }

                    SerializedProperty enabledProperty = serializedImage.FindProperty("m_Enabled");
                    if (enabledProperty != null)
                    {
                        enabledProperty.boolValue = false;
                    }

                    SerializedProperty raycastProperty = serializedImage.FindProperty("m_RaycastTarget");
                    if (raycastProperty != null)
                    {
                        raycastProperty.boolValue = false;
                    }

                    serializedImage.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(image);
                    Debug.LogWarning($"[FaaHudRuntimeSanitizer] Removed broken HUD sprite from {GetHierarchyPath(image.transform)}.", image);
                }
            }
        }

        private static void ClearSerializedXPlaneWeatherTexturesFromEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            foreach (RawImage rawImage in FindObjectsByType<RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rawImage == null ||
                    !IsLoadedSceneObject(rawImage.gameObject) ||
                    (!string.Equals(rawImage.gameObject.name, XPlaneOriginalTextureObjectName, System.StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(rawImage.gameObject.name, ProceduralWeatherTextureObjectName, System.StringComparison.OrdinalIgnoreCase)) ||
                    rawImage.texture == null)
                {
                    continue;
                }

                SerializedObject serializedImage = new SerializedObject(rawImage);
                SerializedProperty textureProperty = serializedImage.FindProperty("m_Texture");
                if (textureProperty != null)
                {
                    textureProperty.objectReferenceValue = null;
                }

                SerializedProperty colorProperty = serializedImage.FindProperty("m_Color");
                if (colorProperty != null)
                {
                    colorProperty.colorValue = new Color(0.004f, 0.055f, 0.04f, 0f);
                }

                SerializedProperty raycastProperty = serializedImage.FindProperty("m_RaycastTarget");
                if (raycastProperty != null)
                {
                    raycastProperty.boolValue = false;
                }

                serializedImage.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rawImage);
                if (rawImage.gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rawImage.gameObject.scene);
                }

                Debug.Log($"[FaaHudRuntimeSanitizer] Cleared serialized X-Plane weather radar texture from {GetHierarchyPath(rawImage.transform)}.", rawImage);
            }
        }
#endif

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            string path = transform.name.ToLowerInvariant();
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name.ToLowerInvariant() + "/" + path;
                parent = parent.parent;
            }

            return "/" + path;
        }
    }
}
