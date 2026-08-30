using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace WeatherRadar
{
    /// <summary>
    /// Displays the bridge's dataref-derived weather texture with a compact
    /// pilot-readable presentation. The same component can render a legacy
    /// source when explicitly configured, but the FAA scene stays procedural.
    /// </summary>
    [AddComponentMenu("Weather Radar/Display/FAA Dataref Weather Radar Display")]
    public class XPlaneOriginalWeatherRadarDisplay : MonoBehaviour
    {
        private const string SweepOverlayName = "XPlaneWeatherSweepOverlay";
        private const float DefaultTextureAspect = 724f / 512f;

        [Header("References")]
        [SerializeField] private WeatherRadarProviderBase weatherProvider;
        [SerializeField] private WeatherRadarDataProvider dataProvider;
        [SerializeField] private RawImage targetImage;
        [SerializeField] private AspectRatioFitter aspectRatioFitter;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text sourceLabel;
        [SerializeField] private TMP_Text ageLabel;
        [SerializeField] private TMP_Text powerLabel;

        [Header("Look")]
        [SerializeField] private Color onlineTint = new Color(1f, 1f, 1f, 0.84f);
        [SerializeField] private Color staleTint = new Color(0.82f, 0.9f, 0.84f, 0.72f);
        [SerializeField] private Color offlineTint = new Color(0.004f, 0.055f, 0.04f, 0.06f);
        [SerializeField] private Color radarOnColor = new Color(0.35f, 1f, 0.35f, 1f);
        [SerializeField] private Color radarOffColor = new Color(1f, 0.35f, 0.2f, 1f);
        [SerializeField] private Color radarUnknownColor = new Color(0.72f, 0.9f, 0.72f, 1f);
        [SerializeField] private float staleAfterSeconds = 6f;
        [SerializeField] private bool preserveAspectRatio = true;
        [SerializeField] private Image powerBadgeBackground;
        [SerializeField] private bool requestTextureWhenEmpty = true;
        [SerializeField] private float emptyRefreshDelaySeconds = 0.75f;
        [SerializeField] private float staleRefreshDelaySeconds = 3f;
        [SerializeField] private bool keepTextureVisibleWhenRadarOff = true;
        [SerializeField] private Vector2 minimumDisplaySize = new Vector2(160f, 160f);
        [SerializeField] private float displayPadding = 8f;
        [SerializeField] private bool showReferenceOverlay = true;

        private Texture _currentTexture;
        private float _lastTextureRealtime = -1f;
        private float _nextSelfRefreshRealtime;
        private ProviderStatus _lastStatus = ProviderStatus.Inactive;
        private bool _hasRadarPowerState;
        private bool _isRadarPowered;
        private int _radarMode = -1;
        private Texture2D _blackPlaceholder;
        private float _nextLabelRefreshRealtime;
        private bool _layerOrderDirty = true;
        private XPlaneWeatherRadarSweepOverlay _sweepOverlay;
        private Vector2 _lastDisplayBounds = new Vector2(-1f, -1f);

        public RawImage TargetImage => targetImage;
        public Texture CurrentTexture => _currentTexture;
        public float LastTextureRealtime => _lastTextureRealtime;
        public bool HasFreshTexture => _currentTexture != null &&
            _lastTextureRealtime >= 0f &&
            Time.realtimeSinceStartup - _lastTextureRealtime <= staleAfterSeconds;
        public bool HasUsableTexture => _currentTexture != null && _lastTextureRealtime >= 0f;
        public bool HasRadarPowerState => _hasRadarPowerState;
        public bool IsRadarPowered => _isRadarPowered;
        public int RadarMode => _radarMode;
        public XPlaneWeatherRadarSweepOverlay SweepOverlay => _sweepOverlay;
        public bool ShowReferenceOverlay
        {
            get => showReferenceOverlay;
            set
            {
                if (showReferenceOverlay == value)
                {
                    return;
                }

                showReferenceOverlay = value;
                _layerOrderDirty = true;
            }
        }

        private void Awake()
        {
            // The FAA scene uses a procedural dataref radar texture. Keep the
            // optional reference overlay off so native X-Plane raster styling
            // is not reproduced over the custom display.
            showReferenceOverlay = false;
            AutoFindReferences();
            ApplyInitialVisualState();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            AutoFindReferences();
            Subscribe();
            RefreshLabels();
        }

        private void Update()
        {
            if (weatherProvider == null || dataProvider == null || targetImage == null || aspectRatioFitter == null)
            {
                AutoFindReferences();
            }

            RefreshLayoutIfBoundsChanged();

            if (_layerOrderDirty)
            {
                EnforceLayerOrder();
                _layerOrderDirty = false;
            }

            RefreshSourceTextureIfNeeded();

            if (Time.realtimeSinceStartup >= _nextLabelRefreshRealtime)
            {
                _nextLabelRefreshRealtime = Time.realtimeSinceStartup + 0.25f;
                RefreshLabels();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (_blackPlaceholder != null)
            {
                Destroy(_blackPlaceholder);
                _blackPlaceholder = null;
            }
        }

        public void SetProvider(WeatherRadarProviderBase provider)
        {
            if (ReferenceEquals(weatherProvider, provider))
            {
                return;
            }

            Unsubscribe();
            weatherProvider = provider;
            Subscribe();
            RefreshLabels();
        }

        public void SetDataProvider(WeatherRadarDataProvider provider)
        {
            dataProvider = provider;
            if (_sweepOverlay != null)
            {
                _sweepOverlay.Configure(targetImage, this, dataProvider);
            }
        }

        public void SetTargetImage(RawImage image)
        {
            targetImage = image;
            EnsureSweepOverlay();
            if (targetImage != null && _currentTexture != null)
            {
                EnsureVisibleDisplayRect();
                targetImage.texture = _currentTexture;
                targetImage.color = onlineTint;
                targetImage.enabled = true;
                targetImage.raycastTarget = false;
                _layerOrderDirty = true;
            }
            else if (targetImage != null)
            {
                ApplyInitialVisualState();
            }
        }

        public void SetStatusLabels(TMP_Text status, TMP_Text source, TMP_Text age, TMP_Text power = null)
        {
            statusLabel = status;
            sourceLabel = source;
            ageLabel = age;
            powerLabel = power;
            if (powerLabel != null && powerBadgeBackground == null)
            {
                powerBadgeBackground = powerLabel.GetComponentInParent<Image>();
            }
            RefreshLabels();
        }

        public void SetRadarPowerState(bool hasState, bool isPowered, int mode = -1)
        {
            _hasRadarPowerState = hasState;
            _isRadarPowered = isPowered;
            _radarMode = mode;
            RefreshLabels();
        }

        public void ShowTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            _currentTexture = texture;
            _lastTextureRealtime = Time.realtimeSinceStartup;

            if (targetImage != null)
            {
                EnsureVisibleDisplayRect();
                _layerOrderDirty = true;
                targetImage.texture = texture;
                targetImage.color = onlineTint;
                targetImage.enabled = true;
            }

            if (preserveAspectRatio && aspectRatioFitter != null && texture.height > 0)
            {
                aspectRatioFitter.aspectRatio = texture.width / (float)texture.height;
            }

            EnsureSweepOverlay();

            if (dataProvider != null)
            {
                dataProvider.UpdateRadarTexture(texture);
            }

            RefreshLabels();
        }

        /// <summary>
        /// Applies the compact FAA glass treatment without modifying the native
        /// X-Plane pixels. Alpha is applied at presentation time so weather cells
        /// remain authentic while the outside view remains visible underneath.
        /// </summary>
        public void ConfigureHudPresentation(float textureOpacity)
        {
            float opacity = Mathf.Clamp(textureOpacity, 0.35f, 1f);
            onlineTint = new Color(1f, 1f, 1f, opacity);
            staleTint = new Color(0.82f, 0.9f, 0.84f, Mathf.Min(opacity, 0.72f));
            offlineTint = new Color(0.004f, 0.055f, 0.04f, Mathf.Min(opacity, 0.06f));
            minimumDisplaySize = new Vector2(160f, 160f);
            displayPadding = Mathf.Max(0f, displayPadding);
            RefreshLayout();
            RefreshLabels();
        }

        public void RefreshLayout()
        {
            _lastDisplayBounds = new Vector2(-1f, -1f);
            EnsureVisibleDisplayRect();
            _layerOrderDirty = true;
        }

        private void AutoFindReferences()
        {
            if (weatherProvider == null)
            {
                weatherProvider = GetComponentInParent<WeatherRadarProviderBase>();
            }

            if (dataProvider == null)
            {
                dataProvider = GetComponentInParent<WeatherRadarDataProvider>();
            }

            if (targetImage == null)
            {
                targetImage = GetComponent<RawImage>();
            }

            if (aspectRatioFitter == null && targetImage != null)
            {
                aspectRatioFitter = targetImage.GetComponent<AspectRatioFitter>();
            }

            // X-Plane's native render is a 724x512 sector. Square-stretching it
            // distorts bearings and weather cells, so runtime always preserves it.
            preserveAspectRatio = true;
            EnsureVisibleDisplayRect();
            EnsureSweepOverlay();
            _layerOrderDirty = true;
        }

        private void EnforceLayerOrder()
        {
            if (targetImage == null)
            {
                return;
            }

            Transform textureTransform = targetImage.transform;
            Transform parent = textureTransform.parent;
            if (parent != null)
            {
                Transform background = parent.Find("Background");
                if (background != null)
                {
                    background.SetAsFirstSibling();
                }

                textureTransform.SetSiblingIndex(background != null ? Mathf.Min(1, parent.childCount - 1) : 0);
            }

            Transform overlay = textureTransform.Find("FAAReferenceOverlay");
            if (_sweepOverlay != null)
            {
                _sweepOverlay.transform.SetAsLastSibling();
            }

            if (overlay != null)
            {
                ApplyReferenceOverlayVisibility(overlay);
                if (showReferenceOverlay)
                {
                    overlay.SetAsLastSibling();
                }
            }
        }

        private void ApplyReferenceOverlayVisibility(Transform overlay)
        {
            if (overlay == null)
            {
                return;
            }

            if (overlay.gameObject.activeSelf != showReferenceOverlay)
            {
                overlay.gameObject.SetActive(showReferenceOverlay);
            }

            XPlaneWeatherRadarOverlay referenceOverlay = overlay.GetComponent<XPlaneWeatherRadarOverlay>();
            if (referenceOverlay != null)
            {
                referenceOverlay.enabled = showReferenceOverlay;
            }

            RawImage overlayImage = overlay.GetComponent<RawImage>();
            if (overlayImage != null)
            {
                overlayImage.enabled = showReferenceOverlay;
                overlayImage.raycastTarget = false;
            }
        }

        private void RefreshSourceTextureIfNeeded()
        {
            if (!requestTextureWhenEmpty || weatherProvider == null)
            {
                return;
            }

            bool hasTexture = _currentTexture != null;
            float age = hasTexture && _lastTextureRealtime >= 0f
                ? Time.realtimeSinceStartup - _lastTextureRealtime
                : float.PositiveInfinity;

            bool needsInitialTexture = !hasTexture;
            bool needsStaleTexture = hasTexture && age > staleRefreshDelaySeconds;
            if (!needsInitialTexture && !needsStaleTexture)
            {
                return;
            }

            if (_hasRadarPowerState && !_isRadarPowered)
            {
                return;
            }

            if (Time.realtimeSinceStartup < _nextSelfRefreshRealtime)
            {
                return;
            }

            _nextSelfRefreshRealtime = Time.realtimeSinceStartup + (hasTexture ? staleRefreshDelaySeconds : emptyRefreshDelaySeconds);
            if (weatherProvider.Status == ProviderStatus.Inactive)
            {
                weatherProvider.Activate();
            }
            weatherProvider.RefreshData();
        }

        private void ApplyInitialVisualState()
        {
            if (targetImage != null)
            {
                EnsureVisibleDisplayRect();
                if (IsRuntimeXPlaneWeatherTexture(targetImage.texture))
                {
                    _currentTexture = targetImage.texture;
                    _lastTextureRealtime = Time.realtimeSinceStartup;
                    targetImage.color = onlineTint;
                    targetImage.enabled = true;
                    targetImage.raycastTarget = false;
                    return;
                }

                targetImage.texture = GetBlackPlaceholder();
                targetImage.color = offlineTint;
                targetImage.enabled = true;
                targetImage.raycastTarget = false;
            }
        }

        private void EnsureVisibleDisplayRect()
        {
            if (targetImage == null)
            {
                return;
            }

            RectTransform rectTransform = targetImage.rectTransform;
            if (rectTransform == null)
            {
                return;
            }

            Vector2 displayBounds = ResolveAvailableDisplayBounds(rectTransform);
            float aspect = GetSourceTextureAspect();
            Vector2 fittedSize = CalculateAspectFitSize(displayBounds, aspect);

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0f, 2f);
            rectTransform.sizeDelta = fittedSize;
            rectTransform.localScale = Vector3.one;

            if (aspectRatioFitter != null)
            {
                // The size is fitted explicitly so it stays deterministic even
                // under parent layout groups and while the texture is refreshing.
                aspectRatioFitter.aspectRatio = aspect;
                aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.None;
                aspectRatioFitter.enabled = false;
            }

            _lastDisplayBounds = displayBounds;
        }

        private void RefreshLayoutIfBoundsChanged()
        {
            if (targetImage == null)
            {
                return;
            }

            Vector2 bounds = ResolveAvailableDisplayBounds(targetImage.rectTransform);
            if ((bounds - _lastDisplayBounds).sqrMagnitude > 0.25f)
            {
                EnsureVisibleDisplayRect();
                _layerOrderDirty = true;
            }
        }

        private Vector2 ResolveAvailableDisplayBounds(RectTransform imageRect)
        {
            Vector2 fallback = new Vector2(
                Mathf.Max(128f, minimumDisplaySize.x),
                Mathf.Max(128f, minimumDisplaySize.y));
            Vector2 available = fallback;
            bool foundParentBounds = false;

            Transform ancestor = imageRect != null ? imageRect.parent : null;
            while (ancestor != null)
            {
                RectTransform ancestorRect = ancestor as RectTransform;
                if (ancestorRect != null)
                {
                    float width = ancestorRect.rect.width;
                    float height = ancestorRect.rect.height;
                    if (width >= 128f && height >= 128f)
                    {
                        Vector2 candidate = new Vector2(
                            Mathf.Max(128f, width - displayPadding * 2f),
                            Mathf.Max(128f, height - displayPadding * 2f));
                        available = foundParentBounds
                            ? new Vector2(Mathf.Min(available.x, candidate.x), Mathf.Min(available.y, candidate.y))
                            : candidate;
                        foundParentBounds = true;
                    }
                }

                if (ancestor.GetComponent<Canvas>() != null)
                {
                    break;
                }

                ancestor = ancestor.parent;
            }

            return foundParentBounds ? available : fallback;
        }

        public static Vector2 CalculateAspectFitSize(Vector2 bounds, float aspect)
        {
            float width = Mathf.Max(1f, bounds.x);
            float height = Mathf.Max(1f, bounds.y);
            float safeAspect = Mathf.Max(0.01f, aspect);

            float fittedWidth = width;
            float fittedHeight = fittedWidth / safeAspect;
            if (fittedHeight > height)
            {
                fittedHeight = height;
                fittedWidth = fittedHeight * safeAspect;
            }

            return new Vector2(fittedWidth, fittedHeight);
        }

        private float GetSourceTextureAspect()
        {
            Texture texture = _currentTexture != null
                ? _currentTexture
                : targetImage != null ? targetImage.texture : null;
            return texture != null && texture.height > 0
                ? texture.width / (float)texture.height
                : DefaultTextureAspect;
        }

        private void EnsureSweepOverlay()
        {
            if (!Application.isPlaying || targetImage == null)
            {
                return;
            }

            if (_sweepOverlay == null)
            {
                Transform existing = targetImage.transform.Find(SweepOverlayName);
                GameObject overlayObject;
                if (existing != null)
                {
                    overlayObject = existing.gameObject;
                }
                else
                {
                    overlayObject = new GameObject(
                        SweepOverlayName,
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(RawImage));
                    overlayObject.layer = targetImage.gameObject.layer;
                    overlayObject.transform.SetParent(targetImage.transform, false);
                }

                RawImage image = overlayObject.GetComponent<RawImage>() ?? overlayObject.AddComponent<RawImage>();
                image.texture = Texture2D.whiteTexture;
                image.color = Color.white;
                image.raycastTarget = false;
                _sweepOverlay = overlayObject.GetComponent<XPlaneWeatherRadarSweepOverlay>() ??
                                overlayObject.AddComponent<XPlaneWeatherRadarSweepOverlay>();
            }

            _sweepOverlay.Configure(targetImage, this, dataProvider);
            _layerOrderDirty = true;
        }

        private void Subscribe()
        {
            if (weatherProvider == null)
            {
                return;
            }

            weatherProvider.OnRadarDataUpdated -= OnRadarDataUpdated;
            weatherProvider.OnRadarDataUpdated += OnRadarDataUpdated;
            weatherProvider.OnStatusChanged -= OnProviderStatusChanged;
            weatherProvider.OnStatusChanged += OnProviderStatusChanged;
            _lastStatus = weatherProvider.Status;
        }

        private void Unsubscribe()
        {
            if (weatherProvider == null)
            {
                return;
            }

            weatherProvider.OnRadarDataUpdated -= OnRadarDataUpdated;
            weatherProvider.OnStatusChanged -= OnProviderStatusChanged;
        }

        private void OnRadarDataUpdated(Texture2D texture)
        {
            ShowTexture(texture);
        }

        private void OnProviderStatusChanged(ProviderStatus status)
        {
            _lastStatus = status;
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            bool hasTexture = _currentTexture != null;
            float age = hasTexture && _lastTextureRealtime >= 0f
                ? Time.realtimeSinceStartup - _lastTextureRealtime
                : float.PositiveInfinity;
            bool isStale = age > staleAfterSeconds;

            if (targetImage != null)
            {
                if (!hasTexture && !IsRuntimeXPlaneWeatherTexture(targetImage.texture))
                {
                    targetImage.texture = GetBlackPlaceholder();
                }

                bool shouldShowTexture = HasUsableTexture &&
                    (keepTextureVisibleWhenRadarOff || !_hasRadarPowerState || _isRadarPowered);
                if (HasUsableTexture && targetImage.texture != _currentTexture)
                {
                    targetImage.texture = _currentTexture;
                }

                targetImage.color = !HasUsableTexture
                    ? offlineTint
                    : !shouldShowTexture ? offlineTint
                    : isStale ? staleTint
                    : onlineTint;
                targetImage.enabled = true;
                targetImage.raycastTarget = false;
            }

            if (sourceLabel != null)
            {
                sourceLabel.text = weatherProvider is XPlaneOriginalWeatherRadarProvider originalProvider &&
                                   !originalProvider.UsesNativeTexture
                    ? "XPL DATAREF WX"
                    : weatherProvider is XPlaneOriginalWeatherRadarProvider
                        ? "XPL WX LIVE"
                    : weatherProvider != null ? "X-PLANE WX" : "WX SOURCE";
            }

            if (statusLabel != null)
            {
                string providerStatus = weatherProvider is XPlaneOriginalWeatherRadarProvider originalProvider
                    ? originalProvider.LastStatus
                    : string.Empty;
                statusLabel.text = !string.IsNullOrWhiteSpace(providerStatus) &&
                    !providerStatus.StartsWith("Requesting ", System.StringComparison.OrdinalIgnoreCase)
                    ? providerStatus
                    : hasTexture
                        ? $"{_currentTexture.width}x{_currentTexture.height}"
                        : _lastStatus.ToString().ToUpperInvariant();
            }

            if (ageLabel != null)
            {
                ageLabel.text = hasTexture && !float.IsInfinity(age)
                    ? $"{Mathf.FloorToInt(age)}s"
                    : "--";
            }

            if (powerLabel != null)
            {
                if (!_hasRadarPowerState)
                {
                    powerLabel.text = "WX --";
                    powerLabel.color = radarUnknownColor;
                    if (powerBadgeBackground != null)
                    {
                        powerBadgeBackground.color = new Color(0.004f, 0.10f, 0.065f, 0.38f);
                    }
                }
                else
                {
                    powerLabel.text = _radarMode >= 0
                        ? $"WX {(_isRadarPowered ? "ON" : "OFF")} M{_radarMode}"
                        : $"WX {(_isRadarPowered ? "ON" : "OFF")}";
                    powerLabel.color = _isRadarPowered ? radarOnColor : radarOffColor;
                    if (powerBadgeBackground != null)
                    {
                        Color badgeColor = _isRadarPowered
                            ? new Color(0.004f, 0.16f, 0.07f, 0.52f)
                            : new Color(0.22f, 0.04f, 0f, 0.52f);
                        powerBadgeBackground.color = badgeColor;
                    }
                }
            }
        }

        private Texture2D GetBlackPlaceholder()
        {
            if (_blackPlaceholder != null)
            {
                return _blackPlaceholder;
            }

            _blackPlaceholder = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "XPlaneWeatherRadarBlackPlaceholder",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _blackPlaceholder.SetPixels(new[]
            {
                Color.black,
                Color.black,
                Color.black,
                Color.black
            });
            _blackPlaceholder.Apply(false, true);
            return _blackPlaceholder;
        }

        private static bool IsRuntimeXPlaneWeatherTexture(Texture texture)
        {
            if (texture == null)
            {
                return false;
            }

            string textureName = texture.name;
            return !string.IsNullOrEmpty(textureName) &&
                   (textureName.StartsWith("FAAProceduralWeatherRadar", System.StringComparison.OrdinalIgnoreCase) ||
                    textureName.StartsWith("XPlaneStreamWeatherRadar", System.StringComparison.OrdinalIgnoreCase) ||
                    textureName.StartsWith("XPlaneOriginalWeatherRadar", System.StringComparison.OrdinalIgnoreCase) ||
                    textureName.StartsWith("v1/render/weather", System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
