using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FAA.Customization
{
    [DefaultExecutionOrder(10000)]
    [AddComponentMenu("FAA/Customization/FAA HUD Runtime Sanitizer")]
    public class FaaHudRuntimeSanitizer : MonoBehaviour
    {
        [Header("Duplicate HUD Protection")]
        [SerializeField] private bool disableWorldSpaceSymbologyCanvas = true;
        [SerializeField] private string worldSpaceCanvasName = "FAASymbologyCanvasWorldSpace";

        [Header("Opaque Block Cleanup")]
        [SerializeField] private bool hideLargeBlankHudImages = true;
        [SerializeField] private float minimumBlockSize = 48f;
        [SerializeField] private float minimumEffectiveBlockSize = 120f;

        [Header("Screen HUD Layout")]
        [SerializeField] private bool enforceScreenFlightHudLayout = true;
        [SerializeField] private float screenFlightHudScale = 420f;
        [SerializeField] private int screenFlightHudSortingOrder = 5000;
        [SerializeField] private bool hideLegacyOverlayGroups = true;

        [Header("Runtime Rescan")]
        [SerializeField] private int initialFrameScans = 240;
        [SerializeField] private float rescanIntervalSeconds = 0.5f;

        private int _remainingInitialScans;
        private float _nextScanTime;

        private void Awake()
        {
            _remainingInitialScans = initialFrameScans;
            SanitizeNow();
        }

        private void OnEnable()
        {
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
            if (disableWorldSpaceSymbologyCanvas)
            {
                DisableDuplicateWorldSpaceHud();
            }

            NormalizeScreenSpaceLegacyHud();

            if (hideLargeBlankHudImages)
            {
                HideLargeBlankHudImages();
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

                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
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
            foreach (RectTransform rectTransform in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rectTransform == null || !IsLoadedSceneObject(rectTransform.gameObject))
                {
                    continue;
                }

                string path = GetHierarchyPath(rectTransform);
                if (rectTransform.gameObject.name == "Second Interation GUI")
                {
                    if (enforceScreenFlightHudLayout && IsScreenFlightHudPath(path))
                    {
                        rectTransform.localScale = Vector3.one * Mathf.Max(1f, screenFlightHudScale);
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
            return path.Contains("/second interation gui/");
        }

        private static bool IsScreenFlightHudPath(string path)
        {
            return path.Contains("/_ui/faasymbologycanvas/second interation gui");
        }

        private static bool ShouldHideLegacyOverlayGroup(string path)
        {
            return path.Contains("/_ui/faasymbologycanvas/maskcanvas") ||
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

            bool isBlankImage = graphic is Image image && image.sprite == null;
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
