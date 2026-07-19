using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace WeatherRadar
{
    /// <summary>
    /// Displays the source X-Plane weather radar PNG directly, preserving its aspect
    /// ratio and avoiding any synthetic recoloring or return reconstruction.
    /// </summary>
    [AddComponentMenu("Weather Radar/Display/X-Plane Original Weather Radar Display")]
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
        [SerializeField] private Color onlineTint = Color.white;
        [SerializeField] private Color staleTint = Color.white;
        [SerializeField] private Color offlineTint = new Color(0f, 0f, 0f, 1f);
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
        [SerializeField] private Vector2 minimumDisplaySize = new Vector2(352f, 352f);
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
            // Existing scenes serialized this off while the overlay was still a
            // diagnostic layer. It is now the primary aircraft reference layer;
            // controls can still toggle it after initialization.
            showReferenceOverlay = true;
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

            Vector2 displayBounds = new Vector2(
                Mathf.Max(128f, minimumDisplaySize.x),
                Mathf.Max(128f, minimumDisplaySize.y));
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
                    : onlineTint;
                targetImage.enabled = true;
                targetImage.raycastTarget = false;
            }

            if (sourceLabel != null)
            {
                sourceLabel.text = weatherProvider is XPlaneOriginalWeatherRadarProvider
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
                        powerBadgeBackground.color = new Color(0f, 0f, 0f, 0.72f);
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
                            ? new Color(0f, 0.16f, 0.04f, 0.82f)
                            : new Color(0.22f, 0.04f, 0f, 0.82f);
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
                   (textureName.StartsWith("XPlaneOriginalWeatherRadar", System.StringComparison.OrdinalIgnoreCase) ||
                    textureName.StartsWith("XPlaneStreamWeatherRadar", System.StringComparison.OrdinalIgnoreCase) ||
                    textureName.StartsWith("v1/render/weather", System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
