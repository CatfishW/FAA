using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using WeatherRadar;

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
        private const float LegacyScreenFlightHudScale = 420f;
        private const float MinimumReadableScreenFlightHudScale = 520f;
        private const float DefaultScreenFlightHudScale = 540f;
        private const float MinimumTrafficRadarWidth = 520f;
        private const float MinimumTrafficRadarHeight = 520f;
        private static readonly Vector2 LegacyScreenFlightHudAnchoredPosition = new Vector2(960f, 740f);
        private static readonly Vector2 DefaultScreenFlightHudAnchoredPosition = new Vector2(960f, 690f);

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

        [Header("Radar Pair Layout")]
        [SerializeField] private bool enforceRadarPairLayout = true;
        [SerializeField] private string weatherRadarCanvasName = "XPlaneWeatherRadarCanvas";
        [SerializeField] private string weatherRadarRootName = "X-Plane Weather Radar System";
        [SerializeField] private string trafficRadarCanvasName = "XPlaneTrafficRadarCanvas";
        [SerializeField] private string trafficRadarRootName = "Traffic Radar System";
        [SerializeField] private string indicatorCanvasName = "XPlaneWeatherIndicatorCanvas";
        [SerializeField] private Vector2 weatherRadarSize = new Vector2(430f, 326f);
        [SerializeField] private Vector2 trafficRadarSize = new Vector2(560f, 560f);
        [SerializeField] private Vector2 radarInset = new Vector2(28f, 28f);
        [SerializeField] private bool createRadarControlStrips = true;
        [SerializeField] private string radarControlsObjectName = DefaultRadarControlsObjectName;

        [Header("Deprecated 3D Weather")]
        [SerializeField] private bool deactivateDeprecatedWeather3DSystems = true;

        [Header("Cesium Presentation Cleanup")]
        [SerializeField] private bool hideCesiumCreditOverlay = true;

        [Header("Runtime Rescan")]
        [SerializeField] private int initialFrameScans = 240;
        [SerializeField] private float rescanIntervalSeconds = 0.5f;

        private int _remainingInitialScans;
        private float _nextScanTime;

        private void Awake()
        {
            EnsureRuntimeDefaults();
            _remainingInitialScans = initialFrameScans;
            SanitizeNow();
        }

        private void OnEnable()
        {
            EnsureRuntimeDefaults();
            _remainingInitialScans = initialFrameScans;
            SanitizeNow();
        }

        private void Start()
        {
            SanitizeNow();
        }

        private void LateUpdate()
        {
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

            if (disableWorldSpaceSymbologyCanvas)
            {
                DisableDuplicateWorldSpaceHud();
            }

            NormalizeScreenSpaceLegacyHud();

            if (enforceRadarPairLayout)
            {
                NormalizeRadarPairLayout();
            }

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
                HideLargeBlankHudImages();
            }
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

            if (trafficRadarSize.x < MinimumTrafficRadarWidth || trafficRadarSize.y < MinimumTrafficRadarHeight)
            {
                trafficRadarSize = new Vector2(
                    Mathf.Max(trafficRadarSize.x, MinimumTrafficRadarWidth),
                    Mathf.Max(trafficRadarSize.y, MinimumTrafficRadarHeight));
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

            foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform == null || transform.gameObject.name != worldSpaceCanvasName || !IsLoadedSceneObject(transform.gameObject))
                {
                    continue;
                }

                if (transform.gameObject.activeSelf)
                {
                    transform.gameObject.SetActive(false);
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
                DisableWeatherReferenceOverlays(weatherRoot);
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

            DisableTrafficDisplayMask(radarDisplay.gameObject);
            foreach (TrafficRadar.TrafficRadarDisplay display in radarDisplay.GetComponentsInChildren<TrafficRadar.TrafficRadarDisplay>(true))
            {
                display.enabled = true;
                display.ShowRadarBackground = false;
                display.PreferXPlaneTrafficTexture = true;
                ForceVisibleRawImage(display.RadarImage, true);
            }
        }

        private static void DisableTrafficDisplayMask(GameObject radarDisplay)
        {
            if (radarDisplay == null)
            {
                return;
            }

            foreach (Mask mask in radarDisplay.GetComponents<Mask>())
            {
                if (mask != null)
                {
                    mask.enabled = false;
                    mask.showMaskGraphic = false;
                }
            }

            UnityEngine.UI.Image image = radarDisplay.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                Color color = image.color;
                color.a = 0f;
                image.color = color;
                image.raycastTarget = false;
            }
        }

        private static void ForceVisibleRawImage(RawImage image, bool preserveExistingTexture)
        {
            if (image == null)
            {
                return;
            }

            image.gameObject.SetActive(true);
            image.enabled = true;
            image.color = Color.white;
            image.material = null;
            image.raycastTarget = false;
            if (!preserveExistingTexture || image.texture == null)
            {
                image.texture = Texture2D.blackTexture;
            }
        }

        private static void DisableWeatherReferenceOverlays(GameObject weatherRoot)
        {
            if (weatherRoot == null)
            {
                return;
            }

            foreach (XPlaneOriginalWeatherRadarDisplay display in weatherRoot.GetComponentsInChildren<XPlaneOriginalWeatherRadarDisplay>(true))
            {
                if (display != null)
                {
                    display.ShowReferenceOverlay = false;
                }
            }

            foreach (XPlaneWeatherRadarOverlay overlay in weatherRoot.GetComponentsInChildren<XPlaneWeatherRadarOverlay>(true))
            {
                if (overlay == null)
                {
                    continue;
                }

                overlay.enabled = false;
                RawImage image = overlay.GetComponent<RawImage>();
                if (image != null)
                {
                    image.enabled = false;
                    image.raycastTarget = false;
                }

                if (overlay.gameObject.activeSelf)
                {
                    overlay.gameObject.SetActive(false);
                }
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

                SuppressDuplicateHudRoot(transform.gameObject);
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
            Scene scene = gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
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

        private static bool ShouldHideLegacyOverlayGroup(string path)
        {
            return path.Contains("/_ui/faasymbologycanvas/visualunderstanding") ||
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
                bool sanitizedAny = false;
                foreach (FaaHudRuntimeSanitizer sanitizer in Resources.FindObjectsOfTypeAll<FaaHudRuntimeSanitizer>())
                {
                    if (sanitizer == null || !IsLoadedSceneObject(sanitizer.gameObject))
                    {
                        continue;
                    }

                    sanitizedAny = true;
                    sanitizer.SanitizeNow();
                }

                if (!sanitizedAny)
                {
                    HideCesiumCreditOverlay();
                }
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
