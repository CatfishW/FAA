using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IndicatorSystem.Core;

namespace IndicatorSystem.Display
{
    /// <summary>
    /// Individual indicator UI element.
    /// Handles display of on-screen symbols and off-screen arrows.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class IndicatorElement : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("UI References")]
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Image symbolImage;
        [SerializeField] private Image arrowImage;
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private TextMeshProUGUI distanceText;
        [SerializeField] private TextMeshProUGUI altitudeText;
        [SerializeField] private GameObject typeBadgeObject;
        [SerializeField] private GameObject labelBackgroundObject;
        [SerializeField] private GameObject distanceBackgroundObject;
        [SerializeField] private GameObject altitudeBackgroundObject;
        [SerializeField] private bool hideTextBackgrounds = true;
        
        [Header("Trail")]
        [SerializeField] private RectTransform trailContainer;
        
        [Header("Sprites")]
        [SerializeField] private Sprite onScreenSymbol;
        [SerializeField] private Sprite offScreenArrow;
        [SerializeField] private Sprite altitudeArrowSprite;
        
        [Header("Animation")]
        [SerializeField] private float smoothSpeed = 10f;
        [SerializeField] private CanvasGroup canvasGroup;
        
        [Header("Navigation Lights")]
        [SerializeField] private Image portLightImage;
        [SerializeField] private Image starboardLightImage;
        [SerializeField] private Image tailLightImage;
        
        #endregion
        
        #region Private Fields
        
        private IndicatorData _currentData;
        private Vector2 _targetPosition;
        private Vector2 _currentPosition;
        private float _targetRotation;
        private float _currentRotation;
        private Color _targetColor;
        private bool _isInitialized;
        private bool _hasValidPosition;
        private float _pulseTimer;
        private bool _smoothMovement = true;
        private const float MAX_TRANSITION_DISTANCE = 300f;
        private static readonly Color IndicatorOutlineColor = new Color(0f, 0.02f, 0f, 0.86f);
        private const string WeatherVisualRootName = "OverlayWeatherCellVisual";

        private RectTransform _weatherVisualRoot;
        private Image _weatherShadowImage;
        private Image _weatherHaloImage;
        private Image _weatherBackImage;
        private Image _weatherCoreImage;
        private Image _weatherHighlightImage;
        private Image _weatherRingImage;
        private Image _weatherBoltImage;
        private Image _weatherPointerImage;
        private bool _weatherVisualActive;
        private float _weatherVisualSeed;

        private static Sprite _weatherDiscSprite;
        private static Sprite _weatherRingSprite;
        private static Sprite _weatherPointerSprite;
        private static Sprite _weatherBoltSprite;
        
        // Trail system
        private struct TrailPoint
        {
            public Vector2 Position;
            public float Timestamp;
        }
        private TrailPoint[] _trailPoints;
        private int _trailIndex;
        private float _lastTrailSampleTime;
        private Image[] _trailImages;
        private bool _trailInitialized;
        
        // Navigation lights
        private float _navLightBlinkTimer;
        private bool _navLightsInitialized;
        
        // Transparency override
        private float _baseOpacity = 1f;
        private bool _opacityOverride = false;
        
        #endregion
        
        #region Properties
        
        public string Id => _currentData.Id;
        public bool IsActive => _currentData.IsActive;
        public IndicatorType Type => _currentData.Type;
        public float DistanceNM => _currentData.DistanceNM;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            Initialize();
        }
        
        private void Update()
        {
            if (!_isInitialized || !_currentData.IsActive)
                return;
            
            if (!_smoothMovement)
            {
                SnapToTargetVisualState();
                return;
            }

            // Smooth position with adaptive speed based on distance
            float distance = Vector2.Distance(_currentPosition, _targetPosition);
            float adaptiveSpeed = smoothSpeed;
            
            // If too far away, instant jump (likely teleport)
            if (distance > MAX_TRANSITION_DISTANCE)
            {
                _currentPosition = _targetPosition;
            }
            else if (distance > 1f)
            {
                // Smooth lerp with minimum movement to avoid stalling
                _currentPosition = Vector2.Lerp(
                    _currentPosition,
                    _targetPosition,
                    Time.deltaTime * adaptiveSpeed
                );
                
                // Ensure minimum movement to prevent stalling near target
                Vector2 diff = _targetPosition - _currentPosition;
                if (diff.magnitude > 0.5f && diff.magnitude < distance * Time.deltaTime * adaptiveSpeed)
                {
                    _currentPosition = Vector2.MoveTowards(_currentPosition, _targetPosition, 1f);
                }
            }
            else
            {
                _currentPosition = _targetPosition;
            }
            
            rectTransform.anchoredPosition = _currentPosition;
            
            // Smooth rotation for arrow
            if (arrowImage != null && arrowImage.gameObject.activeSelf)
            {
                _currentRotation = Mathf.LerpAngle(_currentRotation, _targetRotation, Time.deltaTime * smoothSpeed);
                arrowImage.rectTransform.localRotation = Quaternion.Euler(0, 0, -_currentRotation);
            }
            
            // Smooth color
            if (symbolImage != null)
            {
                symbolImage.color = Color.Lerp(symbolImage.color, _targetColor, Time.deltaTime * smoothSpeed);
            }
            if (arrowImage != null)
            {
                arrowImage.color = Color.Lerp(arrowImage.color, _targetColor, Time.deltaTime * smoothSpeed);
            }

            if (_weatherVisualActive)
            {
                UpdateWeatherVisualAnimation();
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Initialize the indicator element.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;
            
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();
            
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            
            // Apply sprites to images
            if (symbolImage != null && onScreenSymbol != null)
            {
                symbolImage.sprite = onScreenSymbol;
            }
            
            if (arrowImage != null && offScreenArrow != null)
            {
                arrowImage.sprite = offScreenArrow;
            }

            if (typeBadgeObject == null)
            {
                Transform typeBadge = transform.Find("TypeBadge");
                if (typeBadge != null)
                {
                    typeBadgeObject = typeBadge.gameObject;
                }
            }

            if (labelBackgroundObject == null)
            {
                Transform labelBackground = transform.Find("LabelBackground") ?? transform.Find("Label/LabelBackground");
                if (labelBackground != null)
                {
                    labelBackgroundObject = labelBackground.gameObject;
                }
            }

            if (distanceBackgroundObject == null)
            {
                Transform distanceBackground = transform.Find("DistBackground") ?? transform.Find("Distance/DistBackground");
                if (distanceBackground != null)
                {
                    distanceBackgroundObject = distanceBackground.gameObject;
                }
            }

            if (altitudeBackgroundObject == null)
            {
                Transform altitudeBackground = transform.Find("AltBackground");
                if (altitudeBackground == null)
                {
                    altitudeBackground = transform.Find("Altitude/AltBackground");
                }

                if (altitudeBackground != null)
                {
                    altitudeBackgroundObject = altitudeBackground.gameObject;
                }
            }

            ApplyNonBlockingChromeState();
            
            _isInitialized = true;
        }
        
        /// <summary>
        /// Update the indicator with new data.
        /// </summary>
        public void UpdateIndicator(IndicatorData data, IndicatorSettings settings)
        {
            _currentData = data;
            
            if (!data.IsActive)
            {
                SetVisible(false);
                return;
            }
            
            SetVisible(true);
            _smoothMovement = settings == null || settings.smoothMovement;
            if (settings != null)
            {
                smoothSpeed = settings.smoothSpeed;
            }
            
            // Update visibility mode (on-screen symbol vs off-screen arrow)
            bool isWeather = data.Type == IndicatorType.Weather;
            bool isTraffic = data.Type == IndicatorType.Traffic;
            bool isOffScreen = data.Visibility == IndicatorVisibility.OffScreen ||
                               data.Visibility == IndicatorVisibility.Behind;

            // Update target values for smooth animation
            _targetPosition = ScreenToIndicatorLocalPosition(data.ScreenPosition);
            _targetRotation = data.ArrowRotation;
            _targetColor = ResolveIndicatorColor(data.Color, isWeather, isTraffic, isOffScreen, data.Priority);
            
            // On first valid update, set current position immediately to avoid flying in from origin
            if (!_hasValidPosition)
            {
                _currentPosition = _targetPosition;
                _currentRotation = _targetRotation;
                rectTransform.anchoredPosition = _currentPosition;
                _hasValidPosition = true;
                ClearTrail();
            }
            // If distance is too large (e.g., target reappeared after being out of range), snap position
            else if (Vector2.Distance(_currentPosition, _targetPosition) > MAX_TRANSITION_DISTANCE)
            {
                _currentPosition = _targetPosition;
                _currentRotation = _targetRotation;
                rectTransform.anchoredPosition = _currentPosition;
                ClearTrail();
            }
            
            // Sample trail point
            if (settings.showTrails && Time.time - _lastTrailSampleTime >= settings.trailSampleInterval)
            {
                AddTrailPoint(_currentPosition);
                _lastTrailSampleTime = Time.time;
            }
            
            // Update trail rendering
            UpdateTrail(settings);
            
            // Calculate scale with distance-based adjustment
            float scale = settings.globalScale;
            
            if (settings.enableDistanceScaling)
            {
                float distanceNM = data.DistanceNM;
                float distanceT = Mathf.InverseLerp(settings.closeDistanceNM, settings.farDistanceNM, distanceNM);
                float distanceScale = Mathf.Lerp(settings.closeDistanceScale, settings.farDistanceScale, distanceT);
                scale *= distanceScale;
            }

            if (isWeather)
            {
                scale *= isOffScreen ? 1.05f : 1.02f;
            }
            else if (isTraffic)
            {
                scale *= isOffScreen ? 0.98f : 0.94f;
            }
            
            rectTransform.localScale = Vector3.one * scale;

            if (isWeather)
            {
                ApplyWeatherVisualState(settings, _targetColor, isOffScreen, data.ArrowRotation, data.Priority);
            }
            else
            {
                SetWeatherVisualActive(false);
            }
            
            if (symbolImage != null)
            {
                symbolImage.gameObject.SetActive(!isWeather && !isOffScreen);
                symbolImage.preserveAspect = true;
                symbolImage.rectTransform.sizeDelta = Vector2.one * (isWeather ? settings.indicatorSize * 0.92f : settings.indicatorSize);
                ConfigureContrastOutline(symbolImage, isWeather ? 1.55f : 1.15f);
                
                // Rotate symbol based on 3D heading projected to screen space
                if (settings.rotateSymbolByHeading && data.Type == IndicatorType.Traffic)
                {
                    float screenRotationZ = CalculateScreenRotation(data, settings);
                    
                    // Apply 3D perspective rotation if enabled
                    if (settings.enable3DRotation)
                    {
                        // Calculate X/Y tilt based on view angle
                        Vector3 rotation3D = Calculate3DRotation(data, settings, screenRotationZ);
                        symbolImage.rectTransform.localRotation = Quaternion.Euler(rotation3D);
                    }
                    else
                    {
                        // Billboard mode - only Z rotation
                        symbolImage.rectTransform.localRotation = Quaternion.Euler(0, 0, screenRotationZ);
                    }
                }
                else
                {
                    symbolImage.rectTransform.localRotation = Quaternion.identity;
                }
            }
            
            if (arrowImage != null)
            {
                arrowImage.gameObject.SetActive(!isWeather && isOffScreen);
                arrowImage.preserveAspect = true;
                arrowImage.rectTransform.sizeDelta = Vector2.one * (isWeather ? settings.arrowSize : settings.arrowSize * 0.96f);
                ConfigureContrastOutline(arrowImage, isWeather ? 1.75f : 1.3f);
                if (isOffScreen)
                {
                    arrowImage.rectTransform.localRotation = Quaternion.Euler(0, 0, -data.ArrowRotation);
                }
            }
            
            // Update label
            if (labelText != null)
            {
                labelText.text = "";
                labelText.gameObject.SetActive(false);
                labelText.fontSize = Mathf.Min(settings.labelFontSize, 11f);
            }
            
            // Update distance with 'nm' unit
            if (distanceText != null && settings.showDistanceLabels && !isWeather)
            {
                distanceText.text = $"{data.DistanceNM:F1}nm";
                distanceText.fontSize = settings.distanceFontSize;
                distanceText.gameObject.SetActive(true);
            }
            else if (distanceText != null)
            {
                distanceText.gameObject.SetActive(false);
            }

            if (typeBadgeObject != null)
            {
                typeBadgeObject.SetActive(false);
            }

            // Update altitude as text (+1, -3, 0)
            if (altitudeText != null && settings.showAltitudeIndicators && !isWeather)
            {
                float altFeet = data.RelativeAltitudeFeet;
                string altStr;
                
                if (settings.altitudeInThousands)
                {
                    // Show in thousands of feet
                    int altThousands = Mathf.RoundToInt(altFeet / 1000f);
                    if (Mathf.Abs(altFeet) < settings.altitudeThreshold)
                    {
                        altStr = "0";
                    }
                    else if (altThousands > 0)
                    {
                        altStr = $"+{altThousands}";
                    }
                    else
                    {
                        altStr = altThousands.ToString();
                    }
                }
                else
                {
                    // Show in hundreds of feet
                    int altHundreds = Mathf.RoundToInt(altFeet / 100f);
                    if (Mathf.Abs(altFeet) < settings.altitudeThreshold)
                    {
                        altStr = "00";
                    }
                    else if (altHundreds > 0)
                    {
                        altStr = $"+{Mathf.Abs(altHundreds):D2}";
                    }
                    else
                    {
                        altStr = $"-{Mathf.Abs(altHundreds):D2}";
                    }
                }
                
                altitudeText.text = altStr;
                altitudeText.fontSize = settings.altitudeFontSize;
                altitudeText.gameObject.SetActive(true);
            }
            else if (altitudeText != null)
            {
                altitudeText.gameObject.SetActive(false);
            }
            
            // Update navigation lights
            UpdateNavigationLights(data, settings, isOffScreen);

            ApplyNonBlockingChromeState();
            if (isWeather)
            {
                ApplyWeatherChromeState();
            }

            if (!_smoothMovement)
            {
                SnapToTargetVisualState();
            }
            
            // Calculate final opacity
            float finalOpacity = _opacityOverride ? _baseOpacity : settings.globalOpacity;
            
            // Apply proximity opacity if enabled
            if (!_opacityOverride && settings.useProximityOpacity && data.DistanceNM <= settings.nearbyDistanceThresholdNM)
            {
                finalOpacity = settings.nearbyOpacity;
            }
            
            // Pulse effect for high priority (applied on top of base opacity)
            if (settings.pulseHighPriority && data.Priority >= 2)
            {
                _pulseTimer += Time.deltaTime * settings.pulseFrequency * 2f * Mathf.PI;
                float pulse = (Mathf.Sin(_pulseTimer) + 1f) / 2f;
                float pulseAlpha = Mathf.Lerp(0.6f, 1f, pulse);
                finalOpacity *= pulseAlpha;
            }
            
            if (canvasGroup != null)
            {
                canvasGroup.alpha = finalOpacity;
            }
        }

        private void SnapToTargetVisualState()
        {
            _currentPosition = _targetPosition;
            _currentRotation = _targetRotation;
            rectTransform.anchoredPosition = _currentPosition;

            if (symbolImage != null)
            {
                symbolImage.color = _targetColor;
            }

            if (arrowImage != null)
            {
                arrowImage.color = _targetColor;
                if (arrowImage.gameObject.activeSelf)
                {
                    arrowImage.rectTransform.localRotation = Quaternion.Euler(0, 0, -_currentRotation);
                }
            }

            if (_weatherVisualActive)
            {
                UpdateWeatherVisualAnimation();
            }
        }

        private Vector2 ScreenToIndicatorLocalPosition(Vector2 screenPosition)
        {
            RectTransform parentRect = rectTransform != null ? rectTransform.parent as RectTransform : null;
            if (parentRect == null)
            {
                return screenPosition - new Vector2(Screen.width / 2f, Screen.height / 2f);
            }

            Canvas canvas = parentRect.GetComponentInParent<Canvas>();
            Camera uiCamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    screenPosition,
                    uiCamera,
                    out Vector2 localPosition))
            {
                return localPosition;
            }

            return screenPosition - new Vector2(Screen.width / 2f, Screen.height / 2f);
        }

        private static Color ResolveIndicatorColor(Color source, bool isWeather, bool isTraffic, bool isOffScreen, int priority)
        {
            Color color = source;

            if (isWeather)
            {
                color.a = Mathf.Clamp01(color.a <= 0.01f ? 1f : color.a);
                return color;
            }

            if (isTraffic)
            {
                color.a = Mathf.Clamp01(color.a <= 0.01f ? 1f : color.a);
                return color;
            }

            color.a = Mathf.Max(color.a, 0.92f);
            return color;
        }

        private static void ConfigureContrastOutline(Image image, float pixelOffset)
        {
            if (image == null)
            {
                return;
            }

            Outline outline = image.GetComponent<Outline>();
            if (outline == null)
            {
                outline = image.gameObject.AddComponent<Outline>();
            }

            outline.enabled = true;
            outline.effectColor = IndicatorOutlineColor;
            outline.effectDistance = new Vector2(pixelOffset, -pixelOffset);
            outline.useGraphicAlpha = true;
        }

        private void EnsureWeatherVisual()
        {
            if (_weatherVisualRoot == null)
            {
                Transform existing = transform.Find(WeatherVisualRootName);
                _weatherVisualRoot = existing as RectTransform;
                if (_weatherVisualRoot == null)
                {
                    GameObject root = new GameObject(WeatherVisualRootName, typeof(RectTransform));
                    root.transform.SetParent(transform, false);
                    _weatherVisualRoot = root.GetComponent<RectTransform>();
                    _weatherVisualRoot.anchorMin = new Vector2(0.5f, 0.5f);
                    _weatherVisualRoot.anchorMax = new Vector2(0.5f, 0.5f);
                    _weatherVisualRoot.pivot = new Vector2(0.5f, 0.5f);
                    _weatherVisualRoot.anchoredPosition = Vector2.zero;
                }

                _weatherVisualRoot.SetAsFirstSibling();
                _weatherVisualSeed = Random.value * Mathf.PI * 2f;
            }

            _weatherShadowImage ??= EnsureWeatherVisualLayer("DepthShadow", GetWeatherDiscSprite(), 0);
            _weatherHaloImage ??= EnsureWeatherVisualLayer("VolumetricHalo", GetWeatherDiscSprite(), 1);
            _weatherBackImage ??= EnsureWeatherVisualLayer("StormBackMass", GetWeatherDiscSprite(), 2);
            _weatherCoreImage ??= EnsureWeatherVisualLayer("StormCoreMass", GetWeatherDiscSprite(), 3);
            _weatherHighlightImage ??= EnsureWeatherVisualLayer("StormHighlight", GetWeatherDiscSprite(), 4);
            _weatherRingImage ??= EnsureWeatherVisualLayer("StormRangeRim", GetWeatherRingSprite(), 5);
            _weatherBoltImage ??= EnsureWeatherVisualLayer("ConvectiveBolt", GetWeatherBoltSprite(), 6);
            _weatherPointerImage ??= EnsureWeatherVisualLayer("DirectionalNose", GetWeatherPointerSprite(), 7);
        }

        private Image EnsureWeatherVisualLayer(string layerName, Sprite sprite, int siblingIndex)
        {
            Transform existing = _weatherVisualRoot.Find(layerName);
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null)
            {
                GameObject layer = new GameObject(layerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                layer.transform.SetParent(_weatherVisualRoot, false);
                image = layer.GetComponent<Image>();
            }

            image.sprite = sprite;
            image.preserveAspect = false;
            image.raycastTarget = false;
            image.maskable = true;
            image.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, _weatherVisualRoot.childCount - 1));
            return image;
        }

        private void ApplyWeatherVisualState(
            IndicatorSettings settings,
            Color sourceColor,
            bool isOffScreen,
            float arrowRotation,
            int priority)
        {
            EnsureWeatherVisual();
            SetWeatherVisualActive(true);

            float indicatorSize = settings != null ? settings.indicatorSize : 80f;
            float arrowSize = settings != null ? settings.arrowSize : 60f;
            float baseSize = Mathf.Max(44f, isOffScreen ? arrowSize * 1.42f : indicatorSize * 1.18f);
            Color vivid = BoostWeatherColor(sourceColor, priority);
            Color bright = Color.Lerp(vivid, Color.white, 0.28f);
            Color dark = Color.Lerp(Color.black, vivid, 0.42f);
            Color hot = Color.Lerp(vivid, new Color(1f, 0.9f, 0.18f, 1f), 0.45f);

            _weatherVisualRoot.sizeDelta = new Vector2(baseSize * 1.7f, baseSize * 1.25f);
            _weatherVisualRoot.localRotation = Quaternion.identity;

            ApplyWeatherLayer(_weatherShadowImage, baseSize * 1.2f, baseSize * 0.72f, new Vector2(baseSize * 0.08f, -baseSize * 0.08f), new Color(0f, 0.03f, 0.02f, 0.58f), 0f);
            ApplyWeatherLayer(_weatherHaloImage, baseSize * 1.58f, baseSize * 1.05f, Vector2.zero, WithAlpha(vivid, 0.28f), 0f);
            ApplyWeatherLayer(_weatherBackImage, baseSize * 0.98f, baseSize * 0.62f, new Vector2(-baseSize * 0.12f, -baseSize * 0.02f), WithAlpha(dark, 0.9f), -10f);
            ApplyWeatherLayer(_weatherCoreImage, baseSize * 0.82f, baseSize * 0.56f, new Vector2(baseSize * 0.1f, baseSize * 0.01f), WithAlpha(vivid, 0.98f), 8f);
            ApplyWeatherLayer(_weatherHighlightImage, baseSize * 0.38f, baseSize * 0.16f, new Vector2(-baseSize * 0.18f, baseSize * 0.14f), WithAlpha(Color.Lerp(Color.white, bright, 0.5f), 0.6f), -16f);
            ApplyWeatherLayer(_weatherRingImage, baseSize * 1.06f, baseSize * 0.72f, new Vector2(baseSize * 0.02f, 0f), WithAlpha(bright, 0.92f), 0f);
            ApplyWeatherLayer(_weatherBoltImage, baseSize * 0.34f, baseSize * 0.58f, new Vector2(baseSize * 0.18f, -baseSize * 0.03f), WithAlpha(hot, 0.95f), -8f);

            _weatherPointerImage.gameObject.SetActive(isOffScreen);
            if (isOffScreen)
            {
                float rotationRadians = arrowRotation * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Sin(rotationRadians), Mathf.Cos(rotationRadians));
                ApplyWeatherLayer(
                    _weatherPointerImage,
                    baseSize * 0.42f,
                    baseSize * 0.42f,
                    direction * (baseSize * 0.62f),
                    WithAlpha(bright, 0.98f),
                    -arrowRotation);
            }

            DisableRaycastTarget(_weatherVisualRoot.gameObject);
        }

        private static void ApplyWeatherLayer(Image image, float width, float height, Vector2 position, Color color, float zRotation)
        {
            if (image == null)
            {
                return;
            }

            image.gameObject.SetActive(true);
            image.color = color;
            image.rectTransform.sizeDelta = new Vector2(width, height);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
            image.rectTransform.localScale = Vector3.one;
        }

        private void UpdateWeatherVisualAnimation()
        {
            if (_weatherVisualRoot == null)
            {
                return;
            }

            float pulse = (Mathf.Sin(Time.unscaledTime * 3.1f + _weatherVisualSeed) + 1f) * 0.5f;
            if (_weatherHaloImage != null)
            {
                _weatherHaloImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1.1f, pulse);
            }

            if (_weatherCoreImage != null)
            {
                _weatherCoreImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.98f, 1.04f, pulse);
            }

            if (_weatherHighlightImage != null)
            {
                Color color = _weatherHighlightImage.color;
                color.a = Mathf.Lerp(0.42f, 0.7f, pulse);
                _weatherHighlightImage.color = color;
            }
        }

        private void SetWeatherVisualActive(bool active)
        {
            _weatherVisualActive = active;
            if (_weatherVisualRoot != null)
            {
                _weatherVisualRoot.gameObject.SetActive(active);
            }
        }

        private static Color BoostWeatherColor(Color color, int priority)
        {
            if (color.a <= 0.01f)
            {
                color.a = 1f;
            }

            Color boosted;
            if (color.r > 0.75f && color.g > 0.65f)
            {
                boosted = new Color(1f, 0.93f, 0.04f, 1f);
            }
            else if (color.r > 0.75f)
            {
                boosted = new Color(1f, 0.12f, 0.04f, 1f);
            }
            else if (color.g >= color.r)
            {
                boosted = new Color(0.05f, 1f, 0.16f, 1f);
            }
            else
            {
                boosted = Color.Lerp(color, Color.white, 0.15f);
                boosted.a = 1f;
            }

            if (priority >= 2)
            {
                boosted = Color.Lerp(boosted, new Color(1f, 0.32f, 0.02f, 1f), 0.3f);
            }

            return boosted;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Sprite GetWeatherDiscSprite()
        {
            return _weatherDiscSprite ??= CreateSoftDiscSprite(64);
        }

        private static Sprite GetWeatherRingSprite()
        {
            return _weatherRingSprite ??= CreateRingSprite(64);
        }

        private static Sprite GetWeatherPointerSprite()
        {
            return _weatherPointerSprite ??= CreatePolygonSprite(
                64,
                new[]
                {
                    new Vector2(0.5f, 0.96f),
                    new Vector2(0.12f, 0.14f),
                    new Vector2(0.5f, 0.34f),
                    new Vector2(0.88f, 0.14f)
                });
        }

        private static Sprite GetWeatherBoltSprite()
        {
            return _weatherBoltSprite ??= CreatePolygonSprite(
                64,
                new[]
                {
                    new Vector2(0.58f, 0.98f),
                    new Vector2(0.24f, 0.52f),
                    new Vector2(0.46f, 0.52f),
                    new Vector2(0.34f, 0.02f),
                    new Vector2(0.8f, 0.64f),
                    new Vector2(0.55f, 0.62f)
                });
        }

        private static Sprite CreateSoftDiscSprite(int size)
        {
            Texture2D texture = CreateTransparentTexture(size);
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 1f - Mathf.SmoothStep(0.42f, 1f, radius);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            return CreateRuntimeSprite(texture);
        }

        private static Sprite CreateRingSprite(int size)
        {
            Texture2D texture = CreateTransparentTexture(size);
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float outer = 1f - Mathf.SmoothStep(0.82f, 1f, radius);
                    float inner = Mathf.SmoothStep(0.52f, 0.68f, radius);
                    float alpha = Mathf.Clamp01(outer * inner);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            return CreateRuntimeSprite(texture);
        }

        private static Sprite CreatePolygonSprite(int size, Vector2[] points)
        {
            Texture2D texture = CreateTransparentTexture(size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float alpha = IsInsidePolygon(point, points) ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            return CreateRuntimeSprite(texture);
        }

        private static Texture2D CreateTransparentTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            return texture;
        }

        private static Sprite CreateRuntimeSprite(Texture2D texture)
        {
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static bool IsInsidePolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                bool crosses = polygon[i].y > point.y != polygon[j].y > point.y;
                if (crosses)
                {
                    float xAtY = (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) /
                        (polygon[j].y - polygon[i].y) + polygon[i].x;
                    if (point.x < xAtY)
                    {
                        inside = !inside;
                    }
                }
            }

            return inside;
        }

        private void ApplyNonBlockingChromeState()
        {
            if (hideTextBackgrounds)
            {
                if (labelBackgroundObject != null)
                {
                    labelBackgroundObject.SetActive(false);
                }

                if (distanceBackgroundObject != null)
                {
                    distanceBackgroundObject.SetActive(false);
                }

                if (altitudeBackgroundObject != null)
                {
                    altitudeBackgroundObject.SetActive(false);
                }
            }

            DisableRaycastTarget(symbolImage);
            DisableRaycastTarget(arrowImage);
            DisableRaycastTarget(labelText);
            DisableRaycastTarget(distanceText);
            DisableRaycastTarget(altitudeText);
            DisableRaycastTarget(typeBadgeObject);
            DisableRaycastTarget(labelBackgroundObject);
            DisableRaycastTarget(distanceBackgroundObject);
            DisableRaycastTarget(altitudeBackgroundObject);
        }

        private static void DisableRaycastTarget(Graphic graphic)
        {
            if (graphic != null)
            {
                graphic.raycastTarget = false;
            }
        }

        private static void DisableRaycastTarget(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            foreach (Graphic graphic in target.GetComponentsInChildren<Graphic>(true))
            {
                DisableRaycastTarget(graphic);
            }
        }

        private void ApplyWeatherChromeState()
        {
            DisableSiblingIfAssigned(labelText);
            DisableSiblingIfAssigned(distanceText);
            DisableSiblingIfAssigned(altitudeText);

            SetNavLightsVisible(false);

            foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null ||
                    graphic == symbolImage ||
                    graphic == arrowImage ||
                    IsWeatherVisualGraphic(graphic))
                {
                    continue;
                }

                graphic.gameObject.SetActive(false);
            }
        }

        private bool IsWeatherVisualGraphic(Graphic graphic)
        {
            return _weatherVisualRoot != null && graphic.transform.IsChildOf(_weatherVisualRoot);
        }

        private static void DisableSiblingIfAssigned(Component component)
        {
            if (component != null)
            {
                component.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Set the indicator visibility.
        /// </summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
        
        /// <summary>
        /// Set the opacity of this indicator.
        /// </summary>
        /// <param name="alpha">Opacity value from 0 (invisible) to 1 (fully visible)</param>
        /// <param name="override">If true, this opacity overrides settings-based opacity</param>
        public void SetOpacity(float alpha, bool overrideSettings = false)
        {
            _baseOpacity = Mathf.Clamp01(alpha);
            _opacityOverride = overrideSettings;
            
            if (canvasGroup != null)
            {
                canvasGroup.alpha = _baseOpacity;
            }
        }
        
        /// <summary>
        /// Clear the opacity override, allowing settings-based opacity to take effect.
        /// </summary>
        public void ClearOpacityOverride()
        {
            _opacityOverride = false;
            _baseOpacity = 1f;
        }
        
        /// <summary>
        /// Reset the indicator for pooling.
        /// </summary>
        public void Reset()
        {
            _currentData = default;
            _targetPosition = Vector2.zero;
            _currentPosition = Vector2.zero;
            _targetRotation = 0f;
            _currentRotation = 0f;
            _hasValidPosition = false;
            _pulseTimer = 0f;
            _baseOpacity = 1f;
            _opacityOverride = false;

            SetWeatherVisualActive(false);
            
            ClearTrail();
            
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
            
            SetVisible(false);
        }
        
        /// <summary>
        /// Calculate screen-space rotation from 3D world heading.
        /// Projects the aircraft's heading direction onto the screen plane.
        /// </summary>
        private float CalculateScreenRotation(IndicatorData data, IndicatorSettings settings)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return 0f;
            
            // Convert heading to world-space direction vector
            // Heading: 0° = North (+Z), 90° = East (+X)
            float headingRad = data.Heading * Mathf.Deg2Rad;
            Vector3 worldDirection = new Vector3(
                Mathf.Sin(headingRad),  // X component (East)
                0f,                      // Y component (no vertical for heading)
                Mathf.Cos(headingRad)   // Z component (North)
            );
            
            // Get positions in world space
            Vector3 aircraftPos = data.WorldPosition;
            Vector3 forwardPoint = aircraftPos + worldDirection * 100f; // Point 100 units ahead
            
            // Project both points to screen space
            Vector3 screenAircraft = cam.WorldToScreenPoint(aircraftPos);
            Vector3 screenForward = cam.WorldToScreenPoint(forwardPoint);
            
            // Calculate screen-space direction
            Vector2 screenDir = new Vector2(
                screenForward.x - screenAircraft.x,
                screenForward.y - screenAircraft.y
            );
            
            // Handle case where aircraft is behind camera
            if (screenAircraft.z < 0 || screenForward.z < 0)
            {
                // Fallback to simple heading-based rotation
                return -(data.Heading + settings.rotationOffset);
            }
            
            // Calculate angle from screen direction
            // atan2 gives angle from +X axis, we want angle from +Y (up)
            float rotation = Mathf.Atan2(screenDir.x, screenDir.y) * Mathf.Rad2Deg;
            
            // Apply offset
            rotation += settings.rotationOffset;
            
            // Optional: limit rotation to prevent invisible angles
            if (settings.limitRotationAngles)
            {
                rotation = LimitRotationAngle(rotation, settings.minVisibleAngle);
            }
            
            return -rotation;
        }
        
        /// <summary>
        /// Limits rotation angle to prevent "invisible" side views.
        /// Snaps angles near 90° or 270° to the nearest visible angle.
        /// </summary>
        private float LimitRotationAngle(float rotation, float minVisible)
        {
            // Normalize to 0-360
            rotation = rotation % 360f;
            if (rotation < 0) rotation += 360f;
            
            // Define "invisible" zones around 90° and 270° (pure side views)
            // If angle is within minVisible of these, snap to the edge
            
            float dist90 = Mathf.Abs(rotation - 90f);
            float dist270 = Mathf.Abs(rotation - 270f);
            
            if (dist90 < minVisible)
            {
                // Snap away from 90°
                rotation = rotation < 90f ? 90f - minVisible : 90f + minVisible;
            }
            else if (dist270 < minVisible)
            {
                // Snap away from 270°
                rotation = rotation < 270f ? 270f - minVisible : 270f + minVisible;
            }
            
            return rotation;
        }
        
        /// <summary>
        /// Calculate full 3D rotation (X, Y, Z) for perspective effect.
        /// X rotation = pitch (nose up/down relative to viewer)
        /// Y rotation = bank (wing tilt relative to viewer)
        /// Z rotation = heading direction on screen
        /// </summary>
        private Vector3 Calculate3DRotation(IndicatorData data, IndicatorSettings settings, float screenRotationZ)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return new Vector3(0, 0, screenRotationZ);
            
            // Get direction from camera to aircraft
            Vector3 toAircraft = (data.WorldPosition - cam.transform.position).normalized;
            
            // Get aircraft heading direction in world space (horizontal plane)
            float headingRad = data.Heading * Mathf.Deg2Rad;
            Vector3 aircraftForward = new Vector3(
                Mathf.Sin(headingRad),
                0f,
                Mathf.Cos(headingRad)
            ).normalized;
            
            // Calculate view angle relative to aircraft heading
            // How much are we seeing from the side? (-1 = left side, +1 = right side, 0 = front/back)
            Vector3 camForward = cam.transform.forward;
            camForward.y = 0;
            camForward.Normalize();
            
            Vector3 camRight = cam.transform.right;
            camRight.y = 0;
            camRight.Normalize();
            
            // Dot product of aircraft forward with camera right tells us side view amount
            float sideViewAmount = Vector3.Dot(aircraftForward, camRight);
            
            // Dot product of aircraft forward with camera forward tells us if approaching/receding
            float frontBackAmount = Vector3.Dot(aircraftForward, camForward);
            
            // Calculate vertical view angle (are we above or below the aircraft?)
            // Positive = we're above looking down, Negative = we're below looking up
            float heightDiff = cam.transform.position.y - data.WorldPosition.y;
            float horizontalDist = Vector3.Distance(
                new Vector3(data.WorldPosition.x, 0, data.WorldPosition.z),
                new Vector3(cam.transform.position.x, 0, cam.transform.position.z)
            );
            float verticalAngle = Mathf.Atan2(heightDiff, Mathf.Max(horizontalDist, 1f)) * Mathf.Rad2Deg;
            
            // X rotation (pitch) - tilt forward/back based on whether we're above or below
            // If we're above the aircraft, it should appear tilted with nose away from us
            float xRotation = Mathf.Clamp(verticalAngle, -settings.maxPerspectiveTiltX, settings.maxPerspectiveTiltX);
            
            // Y rotation (bank) - tilt wings based on side view
            // If we see the right side (-sideViewAmount), tilt right wing toward us
            float yRotation = -sideViewAmount * settings.maxPerspectiveTiltY;
            
            return new Vector3(xRotation, yRotation, screenRotationZ);
        }
        
        #endregion
        
        #region Navigation Lights
        
        private void UpdateNavigationLights(IndicatorData data, IndicatorSettings settings, bool isOffScreen)
        {
            // Only show nav lights for traffic indicators when on-screen
            bool showLights = settings.showNavigationLights && 
                              data.Type == IndicatorType.Traffic && 
                              !isOffScreen;
            
            if (!showLights)
            {
                SetNavLightsVisible(false);
                return;
            }
            
            // Initialize nav lights if needed
            if (!_navLightsInitialized)
            {
                InitializeNavLights();
            }
            
            // Calculate screen-space rotation to determine which lights to show
            Camera cam = Camera.main;
            if (cam == null)
            {
                SetNavLightsVisible(false);
                return;
            }
            
            // Get aircraft heading direction in world space
            float headingRad = data.Heading * Mathf.Deg2Rad;
            Vector3 worldDirection = new Vector3(
                Mathf.Sin(headingRad),
                0f,
                Mathf.Cos(headingRad)
            );
            
            // Calculate relative direction: is aircraft coming toward us or going away?
            Vector3 toAircraft = data.WorldPosition - cam.transform.position;
            toAircraft.y = 0; // Ignore vertical for this calculation
            toAircraft.Normalize();
            
            // Dot product tells us if aircraft is moving toward us or away
            float dotProduct = Vector3.Dot(worldDirection, toAircraft);
            // dotProduct > 0 = aircraft heading toward us (approaching)
            // dotProduct < 0 = aircraft heading away from us (receding)
            bool isApproaching = dotProduct > 0;
            
            // Cross product tells us which side of the aircraft we're viewing
            float crossY = worldDirection.x * toAircraft.z - worldDirection.z * toAircraft.x;
            // crossY > 0 = we see the right (starboard) side
            // crossY < 0 = we see the left (port) side
            
            bool showPort = false;
            bool showStarboard = false;
            bool showTail = false;
            
            // Threshold for "from the side" view
            float sideThreshold = 0.3f;
            
            if (Mathf.Abs(dotProduct) < sideThreshold)
            {
                // Pure side view - show only one light
                if (crossY > 0)
                    showStarboard = true; // We see right side = green light on our left
                else
                    showPort = true; // We see left side = red light on our right
            }
            else if (isApproaching)
            {
                // Aircraft coming toward us - show both lights on the wings
                showPort = true;
                showStarboard = true;
            }
            else
            {
                // Aircraft going away - show tail light
                showTail = true;
            }
            
            // Handle blinking
            float intensity = settings.navLightIntensity;
            if (settings.blinkNavLights)
            {
                _navLightBlinkTimer += Time.deltaTime * settings.navLightBlinkRate * 2f * Mathf.PI;
                float blink = (Mathf.Sin(_navLightBlinkTimer) + 1f) / 2f;
                intensity *= Mathf.Lerp(0.3f, 1f, blink);
            }
            
            // Apply visibility, colors, and sizes
            // Lights are parented to symbol, so they rotate automatically
            float lightSize = settings.navLightSize;
            
            // Wing offset distance - proportional to indicator size
            float wingOffset = settings.indicatorSize * 0.5f;
            float tailOffset = settings.indicatorSize * 0.4f;
            
            if (portLightImage != null)
            {
                portLightImage.gameObject.SetActive(showPort);
                if (showPort)
                {
                    Color c = settings.portLightColor;
                    c.a *= intensity;
                    portLightImage.color = c;
                    portLightImage.rectTransform.sizeDelta = Vector2.one * lightSize;
                    // Port (red) on LEFT wingtip - fixed position, rotates with symbol
                    portLightImage.rectTransform.anchoredPosition = new Vector2(-wingOffset, 0);
                }
            }
            
            if (starboardLightImage != null)
            {
                starboardLightImage.gameObject.SetActive(showStarboard);
                if (showStarboard)
                {
                    Color c = settings.starboardLightColor;
                    c.a *= intensity;
                    starboardLightImage.color = c;
                    starboardLightImage.rectTransform.sizeDelta = Vector2.one * lightSize;
                    // Starboard (green) on RIGHT wingtip - fixed position, rotates with symbol
                    starboardLightImage.rectTransform.anchoredPosition = new Vector2(wingOffset, 0);
                }
            }
            
            if (tailLightImage != null)
            {
                tailLightImage.gameObject.SetActive(showTail);
                if (showTail)
                {
                    Color c = settings.tailLightColor;
                    c.a *= intensity;
                    tailLightImage.color = c;
                    tailLightImage.rectTransform.sizeDelta = Vector2.one * lightSize;
                    // Tail light behind aircraft - fixed position, rotates with symbol
                    tailLightImage.rectTransform.anchoredPosition = new Vector2(0, -tailOffset);
                }
            }
            
            // Tint symbol to indicate approaching vs receding
            if (symbolImage != null)
            {
                if (isApproaching)
                {
                    // Slightly warmer/brighter tint for approaching aircraft
                    symbolImage.color = new Color(1f, 1f, 0.9f, 1f);
                }
                else
                {
                    // Slightly cooler/dimmer for receding aircraft
                    symbolImage.color = new Color(0.85f, 0.9f, 1f, 0.9f);
                }
            }
        }
        
        private void InitializeNavLights()
        {
            // Parent nav lights to symbol so they rotate with it
            Transform lightParent = symbolImage != null ? symbolImage.transform : transform;
            
            // Create nav light images if not assigned
            // Positions are relative to symbol center:
            // Port (red) = left wingtip = negative X
            // Starboard (green) = right wingtip = positive X
            // Tail (white) = behind = negative Y (since symbol nose points up)
            if (portLightImage == null)
            {
                portLightImage = CreateNavLight("PortLight", new Vector2(-30, 0), lightParent);
            }
            if (starboardLightImage == null)
            {
                starboardLightImage = CreateNavLight("StarboardLight", new Vector2(30, 0), lightParent);
            }
            if (tailLightImage == null)
            {
                tailLightImage = CreateNavLight("TailLight", new Vector2(0, -25), lightParent);
            }
            
            _navLightsInitialized = true;
        }
        
        private Image CreateNavLight(string name, Vector2 position, Transform parent)
        {
            GameObject lightObj = new GameObject(name);
            lightObj.transform.SetParent(parent, false);
            
            RectTransform rt = lightObj.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(8, 8);
            
            Image img = lightObj.AddComponent<Image>();
            img.color = Color.white;
            
            // Make it circular by using a soft sprite or just the default
            lightObj.SetActive(false);
            
            return img;
        }
        
        private void SetNavLightsVisible(bool visible)
        {
            if (portLightImage != null)
                portLightImage.gameObject.SetActive(visible && portLightImage.gameObject.activeSelf);
            if (starboardLightImage != null)
                starboardLightImage.gameObject.SetActive(visible && starboardLightImage.gameObject.activeSelf);
            if (tailLightImage != null)
                tailLightImage.gameObject.SetActive(visible && tailLightImage.gameObject.activeSelf);
            
            if (!visible)
            {
                if (portLightImage != null) portLightImage.gameObject.SetActive(false);
                if (starboardLightImage != null) starboardLightImage.gameObject.SetActive(false);
                if (tailLightImage != null) tailLightImage.gameObject.SetActive(false);
            }
        }
        
        #endregion
        
        #region Trail System
        
        private void InitializeTrail(int pointCount)
        {
            if (_trailInitialized && _trailPoints != null && _trailPoints.Length == pointCount)
                return;
            
            // Create trail container if needed
            if (trailContainer == null)
            {
                GameObject trailObj = new GameObject("TrailContainer");
                trailObj.transform.SetParent(transform, false);
                trailObj.transform.SetAsFirstSibling(); // Behind the indicator
                trailContainer = trailObj.AddComponent<RectTransform>();
                trailContainer.anchoredPosition = Vector2.zero;
            }
            
            // Clear existing trail images
            if (_trailImages != null)
            {
                foreach (var img in _trailImages)
                {
                    if (img != null)
                        Destroy(img.gameObject);
                }
            }
            
            _trailPoints = new TrailPoint[pointCount];
            _trailImages = new Image[pointCount];
            _trailIndex = 0;
            
            // Create trail point images
            for (int i = 0; i < pointCount; i++)
            {
                GameObject pointObj = new GameObject($"TrailPoint_{i}");
                pointObj.transform.SetParent(trailContainer, false);
                RectTransform pointRt = pointObj.AddComponent<RectTransform>();
                pointRt.sizeDelta = new Vector2(8, 8);
                
                Image pointImg = pointObj.AddComponent<Image>();
                pointImg.color = Color.clear;
                _trailImages[i] = pointImg;
                
                _trailPoints[i] = new TrailPoint { Position = Vector2.zero, Timestamp = 0 };
            }
            
            _trailInitialized = true;
        }
        
        private void AddTrailPoint(Vector2 position)
        {
            if (_trailPoints == null || _trailPoints.Length == 0)
                return;
            
            _trailPoints[_trailIndex] = new TrailPoint
            {
                Position = position,
                Timestamp = Time.time
            };
            
            _trailIndex = (_trailIndex + 1) % _trailPoints.Length;
        }
        
        private void UpdateTrail(IndicatorSettings settings)
        {
            if (!settings.showTrails)
            {
                if (trailContainer != null)
                    trailContainer.gameObject.SetActive(false);
                return;
            }
            
            // Initialize trail if needed
            if (!_trailInitialized || _trailPoints == null || _trailPoints.Length != settings.trailPointCount)
            {
                InitializeTrail(settings.trailPointCount);
            }
            
            if (trailContainer != null)
                trailContainer.gameObject.SetActive(true);
            
            float currentTime = Time.time;
            Color baseColor = settings.trailColor;
            
            for (int i = 0; i < _trailPoints.Length; i++)
            {
                var point = _trailPoints[i];
                var img = _trailImages[i];
                
                if (img == null)
                    continue;
                
                // Skip uninitialized points
                if (point.Timestamp == 0)
                {
                    img.color = Color.clear;
                    continue;
                }
                
                // Calculate age and fade
                float age = currentTime - point.Timestamp;
                float fade = 1f - Mathf.Clamp01(age / settings.trailFadeDuration);
                
                if (fade <= 0)
                {
                    img.color = Color.clear;
                    continue;
                }
                
                // Position relative to current indicator position
                Vector2 relativePos = point.Position - _currentPosition;
                img.rectTransform.anchoredPosition = relativePos;
                
                // Scale trail point size based on age
                float sizeScale = Mathf.Lerp(0.3f, 1f, fade);
                img.rectTransform.sizeDelta = Vector2.one * settings.trailWidth * sizeScale;
                
                // Apply fade to color
                Color pointColor = baseColor;
                pointColor.a = baseColor.a * fade;
                img.color = pointColor;
            }
        }
        
        private void ClearTrail()
        {
            _lastTrailSampleTime = 0f;
            
            if (_trailPoints != null)
            {
                for (int i = 0; i < _trailPoints.Length; i++)
                {
                    _trailPoints[i] = new TrailPoint { Position = Vector2.zero, Timestamp = 0 };
                }
            }
            
            if (_trailImages != null)
            {
                foreach (var img in _trailImages)
                {
                    if (img != null)
                        img.color = Color.clear;
                }
            }
        }
        
        #endregion
        
        #region Static Factory
        
        /// <summary>
        /// Create a new indicator element with default UI structure.
        /// </summary>
        public static IndicatorElement CreateDefault(Transform parent)
        {
            // Create root object
            GameObject root = new GameObject("Indicator");
            root.transform.SetParent(parent, false);
            
            RectTransform rt = root.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120, 100);
            
            CanvasGroup cg = root.AddComponent<CanvasGroup>();
            IndicatorElement element = root.AddComponent<IndicatorElement>();
            element.rectTransform = rt;
            element.canvasGroup = cg;
            
            // Create symbol image
            GameObject symbolObj = new GameObject("Symbol");
            symbolObj.transform.SetParent(root.transform, false);
            RectTransform symbolRt = symbolObj.AddComponent<RectTransform>();
            symbolRt.anchoredPosition = Vector2.zero;
            symbolRt.sizeDelta = new Vector2(60, 60);
            Image symbolImg = symbolObj.AddComponent<Image>();
            symbolImg.color = Color.cyan;
            element.symbolImage = symbolImg;
            
            // Create arrow image
            GameObject arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(root.transform, false);
            RectTransform arrowRt = arrowObj.AddComponent<RectTransform>();
            arrowRt.sizeDelta = new Vector2(45, 45);
            Image arrowImg = arrowObj.AddComponent<Image>();
            arrowImg.color = Color.cyan;
            element.arrowImage = arrowImg;
            arrowObj.SetActive(false);
            
            // Create label text (callsign)
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(root.transform, false);
            RectTransform labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchoredPosition = new Vector2(0, 40);
            labelRt.sizeDelta = new Vector2(100, 22);
            TextMeshProUGUI labelTxt = labelObj.AddComponent<TextMeshProUGUI>();
            labelTxt.fontSize = 14;
            labelTxt.fontStyle = FontStyles.Bold;
            labelTxt.alignment = TextAlignmentOptions.Center;
            labelTxt.color = Color.white;
            element.labelText = labelTxt;
            
            // Create distance text
            GameObject distObj = new GameObject("Distance");
            distObj.transform.SetParent(root.transform, false);
            RectTransform distRt = distObj.AddComponent<RectTransform>();
            distRt.anchoredPosition = new Vector2(0, -40);
            distRt.sizeDelta = new Vector2(80, 20);
            TextMeshProUGUI distText = distObj.AddComponent<TextMeshProUGUI>();
            distText.fontSize = 12;
            distText.alignment = TextAlignmentOptions.Center;
            distText.color = Color.white;
            element.distanceText = distText;
            
            // Create altitude text (+1, -3, 0)
            GameObject altObj = new GameObject("Altitude");
            altObj.transform.SetParent(root.transform, false);
            RectTransform altRt = altObj.AddComponent<RectTransform>();
            altRt.anchoredPosition = new Vector2(45, 0);
            altRt.sizeDelta = new Vector2(40, 24);
            TextMeshProUGUI altText = altObj.AddComponent<TextMeshProUGUI>();
            altText.fontSize = 11;
            altText.fontStyle = FontStyles.Bold;
            altText.alignment = TextAlignmentOptions.Left;
            altText.color = Color.white;
            element.altitudeText = altText;
            
            element.Initialize();
            element.SetVisible(false);
            
            return element;
        }
        
        #endregion
    }
}
