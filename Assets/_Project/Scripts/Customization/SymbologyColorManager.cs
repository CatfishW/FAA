using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FAA.Customization
{
    public static class SymbologyTintUtility
    {
        private static readonly string[] UiChromeNameFragments =
        {
            "background",
            "masker",
            "controlpanel",
            "radarpanel",
            "visualunderstanding",
            "button",
            "toggle",
            "slider",
            "scroll",
            "viewport",
            "handle",
            "border",
            "window",
            "container",
            "placeholder",
            "image",
            "rawimage",
            "radarreturns",
            "rangerings",
            "sweepline",
            "readout",
            "readoutimage"
        };

        private static readonly string[] UiChromePathFragments =
        {
            "radarcanvas",
            "xplaneweatherradarcanvas",
            "xplanetrafficradarcanvas",
            "x-plane weather radar system",
            "weather radar system",
            "traffic radar system",
            "[indicator system]",
            "indicator system",
            "traffic indicator",
            "weather indicator",
            "maskcanvas",
            "scalemasker",
            "visualunderstanding",
            "analysis trigger buttons",
            "/voice",
            "/vc",
            "minimap"
        };

        private static readonly string[] SymbologyNameFragments =
        {
            "tick",
            "minor",
            "reticle",
            "ladder",
            "scale",
            "pointer",
            "needle",
            "chevron",
            "fpv",
            "vsi",
            "altitude",
            "airspeed",
            "heading",
            "attitude",
            "bank",
            "slip",
            "skid",
            "wind",
            "torque",
            "cardinal"
        };

        private static readonly string[] DefaultTextChromePathFragments =
        {
            "radarcanvas",
            "xplaneweatherradarcanvas",
            "xplanetrafficradarcanvas",
            "x-plane weather radar system",
            "weather radar system",
            "traffic radar system",
            "[indicator system]",
            "indicator system",
            "traffic indicator",
            "weather indicator",
            "maskcanvas",
            "scalemasker",
            "visualunderstanding",
            "analysis trigger buttons",
            "/voice",
            "/vc",
            "minimap"
        };

        public static bool ShouldTintImage(Image image, IList<string> excludedPathFragments = null)
        {
            if (image == null)
            {
                return false;
            }

            string path = GetHierarchyPath(image.transform);
            if (HasExcludedPathFragment(path, excludedPathFragments))
            {
                return false;
            }

            if (HasFragment(path, UiChromePathFragments))
            {
                return false;
            }

            if (image.GetComponent<Mask>() != null || image.GetComponent<RectMask2D>() != null)
            {
                return false;
            }

            string lowerName = image.name.ToLowerInvariant();
            if (HasFragment(lowerName, UiChromeNameFragments))
            {
                return false;
            }

            Rect rect = image.rectTransform != null ? image.rectTransform.rect : Rect.zero;
            float width = Mathf.Abs(rect.width);
            float height = Mathf.Abs(rect.height);
            float shortest = Mathf.Min(width, height);
            float longest = Mathf.Max(width, height);
            bool hasUsableRect = width > 0.01f && height > 0.01f;
            bool isThinLine = hasUsableRect && (shortest <= 6f || (shortest <= 10f && longest / Mathf.Max(shortest, 0.01f) >= 8f));
            float lossyScaleX = image.transform != null ? Mathf.Abs(image.transform.lossyScale.x) : 1f;
            float lossyScaleY = image.transform != null ? Mathf.Abs(image.transform.lossyScale.y) : 1f;
            float effectiveWidth = width * Mathf.Max(lossyScaleX, 0.0001f);
            float effectiveHeight = height * Mathf.Max(lossyScaleY, 0.0001f);
            bool isHugeInWorld = effectiveWidth >= 120f && effectiveHeight >= 120f;
            bool isLargeSolidImage = hasUsableRect && width >= 16f && height >= 16f && image.sprite == null && !isThinLine;
            bool isScreenHudSpriteSymbology = image.sprite != null &&
                path.Contains("/faasymbologycanvas/second interation gui/") &&
                HasFragment(lowerName, SymbologyNameFragments);

            if (isLargeSolidImage || (isHugeInWorld && !isScreenHudSpriteSymbology))
            {
                return false;
            }

            return isThinLine || isScreenHudSpriteSymbology || HasFragment(lowerName, SymbologyNameFragments);
        }

        public static bool ShouldTintText(Transform textTransform, IList<string> excludedPathFragments = null)
        {
            if (textTransform == null)
            {
                return false;
            }

            string path = GetHierarchyPath(textTransform);
            if (HasExcludedPathFragment(path, excludedPathFragments))
            {
                return false;
            }

            if (HasFragment(path, DefaultTextChromePathFragments))
            {
                return false;
            }

            if (path.Contains("/second interation gui/"))
            {
                return true;
            }

            return path.Contains("/maskcanvas/") ||
                   path.Contains("hud") ||
                   path.Contains("compass") ||
                   path.Contains("altitude") ||
                   path.Contains("airspeed") ||
                   path.Contains("heading") ||
                   path.Contains("vsi");
        }

        public static Color BuildTintColor(Color tint, Color baseColor, bool preserveElementAlpha, float opacityMultiplier = -1f)
        {
            float alpha = opacityMultiplier >= 0f ? opacityMultiplier : tint.a;
            if (preserveElementAlpha)
            {
                alpha *= baseColor.a;
            }

            return new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(alpha));
        }

        public static string GetHierarchyPath(Transform transform)
        {
            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name.ToLowerInvariant());
                current = current.parent;
            }

            names.Reverse();
            return "/" + string.Join("/", names);
        }

        private static bool HasExcludedPathFragment(string path, IList<string> excludedPathFragments)
        {
            if (excludedPathFragments == null)
            {
                return false;
            }

            foreach (string fragment in excludedPathFragments)
            {
                if (!string.IsNullOrWhiteSpace(fragment) && path.Contains(fragment.ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasFragment(string value, string[] fragments)
        {
            foreach (string fragment in fragments)
            {
                if (value.Contains(fragment))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// High-performance color manager for FAA symbology sprites.
    /// Provides smooth animated color transitions with one-button toggle.
    /// </summary>
    public enum ColorPreset
    {
        Black,
        White,
        Green,
        Cyan,
        Custom
    }

    [ExecuteInEditMode]
    [AddComponentMenu("FAA/Customization/Symbology Color Manager")]
    public class SymbologyColorManager : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("Target Roots")]
        [Tooltip("Root transforms containing all symbology elements (e.g., Second Iteration GUI)")]
        [SerializeField] private List<Transform> symbologyRoots = new List<Transform>();
        
        [Header("Color Settings")]
        [SerializeField] private ColorPreset currentPreset = ColorPreset.Black;
        [SerializeField] private Color customColor = Color.white;
        
        [Header("Preset Colors")]
        [SerializeField] private Color blackColor = Color.black;
        [SerializeField] private Color whiteColor = Color.white;
        [SerializeField] private Color greenColor = new Color(0.2f, 1f, 0.2f, 1f);
        [SerializeField] private Color cyanColor = new Color(0f, 1f, 1f, 1f);
        
        [Header("Animation")]
        [SerializeField] private float animationDuration = 0.3f;
        [SerializeField] private AnimationCurve easingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool useUnscaledTime = true;
        
        [Header("Button Integration")]
        [SerializeField] private Button colorToggleButton;
        [SerializeField] private Image buttonIcon;
        [SerializeField] private Sprite lightModeIcon;
        [SerializeField] private Sprite darkModeIcon;
        
        [Header("Exceptions")]
        [Tooltip("Transforms whose children should be excluded from color changes")]
        [SerializeField] private List<Transform> exceptionParents = new List<Transform>();

        [Tooltip("Keep each graphic's original alpha when applying a symbology color")]
        [SerializeField] private bool preserveElementAlpha = true;

        [Tooltip("Avoid tinting panels, buttons, radar controls, voice UI, and other UI chrome")]
        [SerializeField] private bool tintOnlySymbologyElements = true;

        [Tooltip("Hierarchy/name fragments excluded from symbology tinting")]
        [SerializeField] private List<string> excludedPathFragments = new List<string>
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
            "maskcanvas",
            "masker",
            "scalemasker",
            "radarreturns",
            "rangerings",
            "sweepline",
            "readoutimage"
        };
        
        [Header("Debug")]
        [SerializeField] private bool logColorChanges = false;
        
        #endregion
        
        #region Private Fields
        
        private List<Image> _cachedImages = new List<Image>();
        private List<TMP_Text> _cachedTexts = new List<TMP_Text>();
        private List<Text> _cachedStandardTexts = new List<Text>();
        private readonly Dictionary<Image, Color> _imageBaseColors = new Dictionary<Image, Color>();
        private readonly Dictionary<TMP_Text, Color> _tmpTextBaseColors = new Dictionary<TMP_Text, Color>();
        private readonly Dictionary<Text, Color> _standardTextBaseColors = new Dictionary<Text, Color>();
        private Coroutine _animationCoroutine;
        private Color _currentColor;
        private bool _isInitialized = false;
        
        #endregion
        
        #region Properties
        
        public ColorPreset CurrentPreset => currentPreset;
        public Color CurrentColor => _currentColor;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            Initialize();
            ApplyColorImmediate(_currentColor);
        }
        
        private void OnEnable()
        {
            if (colorToggleButton != null && Application.isPlaying)
            {
                colorToggleButton.onClick.RemoveListener(ToggleColor);
                colorToggleButton.onClick.AddListener(ToggleColor);
            }

            if (Application.isPlaying)
            {
                ApplyColorImmediate(GetPresetColor(currentPreset));
            }
        }
        
        private void OnDisable()
        {
            if (colorToggleButton != null)
            {
                colorToggleButton.onClick.RemoveListener(ToggleColor);
            }
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        Initialize();
                        ApplyColorImmediate(GetPresetColor(currentPreset));
                    }
                };
            }
        }
#endif
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Initialize and cache all UI components
        /// </summary>
        public void Initialize()
        {
            CacheComponents();
            _currentColor = GetPresetColor(currentPreset);
            _isInitialized = true;
        }
        
        /// <summary>
        /// Toggle between Black and White (or cycle through presets)
        /// </summary>
        public void ToggleColor()
        {
            // Simple toggle between black and white
            ColorPreset newPreset = currentPreset == ColorPreset.Black 
                ? ColorPreset.White 
                : ColorPreset.Black;
            
            SetColorPreset(newPreset);
        }
        
        /// <summary>
        /// Cycle through all color presets
        /// </summary>
        public void CycleColorPreset()
        {
            int nextIndex = ((int)currentPreset + 1) % 5;
            SetColorPreset((ColorPreset)nextIndex);
        }
        
        /// <summary>
        /// Set a specific color preset with animation
        /// </summary>
        public void SetColorPreset(ColorPreset preset)
        {
            currentPreset = preset;
            Color targetColor = GetPresetColor(preset);
            
            if (Application.isPlaying)
            {
                AnimateToColor(targetColor);
            }
            else
            {
                ApplyColorImmediate(targetColor);
            }
            
            UpdateButtonIcon();
            
            if (logColorChanges)
            {
                Debug.Log($"[SymbologyColorManager] Changed to preset: {preset}");
            }
        }
        
        /// <summary>
        /// Set a custom color with animation
        /// </summary>
        public void SetCustomColor(Color color)
        {
            currentPreset = ColorPreset.Custom;
            customColor = color;
            
            if (Application.isPlaying)
            {
                AnimateToColor(color);
            }
            else
            {
                ApplyColorImmediate(color);
            }
        }
        
        /// <summary>
        /// Apply color immediately without animation
        /// </summary>
        public void ApplyColorImmediate(Color color)
        {
            if (!_isInitialized)
            {
                Initialize();
            }
            
            _currentColor = color;
            
            foreach (var img in _cachedImages)
            {
                if (img != null)
                {
                    img.color = BuildTintColor(color, GetBaseColor(img));
                }
            }
            
            foreach (var txt in _cachedTexts)
            {
                if (txt != null)
                {
                    txt.color = BuildTintColor(color, GetBaseColor(txt));
                }
            }
            
            foreach (var txt in _cachedStandardTexts)
            {
                if (txt != null)
                {
                    txt.color = BuildTintColor(color, GetBaseColor(txt));
                }
            }
            
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }
        
        /// <summary>
        /// Get the current opacity (alpha) value
        /// </summary>
        public float CurrentOpacity => _currentColor.a;
        
        /// <summary>
        /// Set the opacity/transparency of all symbology elements (0 = invisible, 1 = fully visible)
        /// </summary>
        /// <param name="opacity">Opacity value from 0 to 1</param>
        public void SetOpacity(float opacity)
        {
            if (!_isInitialized)
            {
                Initialize();
            }
            
            opacity = Mathf.Clamp01(opacity);
            _currentColor.a = opacity;
            
            foreach (var img in _cachedImages)
            {
                if (img != null)
                {
                    img.color = BuildTintColor(_currentColor, GetBaseColor(img), opacity);
                }
            }
            
            foreach (var txt in _cachedTexts)
            {
                if (txt != null)
                {
                    txt.color = BuildTintColor(_currentColor, GetBaseColor(txt), opacity);
                }
            }
            
            foreach (var txt in _cachedStandardTexts)
            {
                if (txt != null)
                {
                    txt.color = BuildTintColor(_currentColor, GetBaseColor(txt), opacity);
                }
            }
            
            if (logColorChanges)
            {
                Debug.Log($"[SymbologyColorManager] Set opacity to {opacity:F2}");
            }
        }
        
        /// <summary>
        /// Show all symbology elements (set opacity to 1)
        /// </summary>
        public void Show()
        {
            SetOpacity(1f);
        }
        
        /// <summary>
        /// Hide all symbology elements (set opacity to 0)
        /// </summary>
        public void Hide()
        {
            SetOpacity(0f);
        }
        
        /// <summary>
        /// Refresh the component cache (call after hierarchy changes)
        /// </summary>
        public void RefreshCache()
        {
            CacheComponents();
        }
        
        #endregion
        
        #region Private Methods
        
        private void CacheComponents()
        {
            _cachedImages.Clear();
            _cachedTexts.Clear();
            _cachedStandardTexts.Clear();
            PruneDestroyedBaseColors();
            
            // Get all roots to process (use self if no roots specified)
            List<Transform> rootsToProcess = new List<Transform>();
            if (symbologyRoots != null && symbologyRoots.Count > 0)
            {
                foreach (var root in symbologyRoots)
                {
                    if (root != null)
                        rootsToProcess.Add(root);       
                }
            }
            
            // Fallback to self if no valid roots
            if (rootsToProcess.Count == 0)
            {
                rootsToProcess.Add(transform);
            }
            
            // Cache components from all roots
            foreach (var root in rootsToProcess)
            {
                // Cache all Image components
                Image[] images = root.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img == buttonIcon) continue;
                    if (IsUnderExceptionParent(img.transform)) continue;
                    if (tintOnlySymbologyElements && !SymbologyTintUtility.ShouldTintImage(img, excludedPathFragments)) continue;
                    if (!_cachedImages.Contains(img)) // Avoid duplicates
                    {
                        _cachedImages.Add(img);
                        RememberBaseColor(img);
                    }
                }
                
                // Cache all TMP_Text components
                TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
                foreach (var txt in texts)
                {
                    if (IsUnderExceptionParent(txt.transform)) continue;
                    if (tintOnlySymbologyElements && !SymbologyTintUtility.ShouldTintText(txt.transform, excludedPathFragments)) continue;
                    if (!_cachedTexts.Contains(txt)) // Avoid duplicates
                    {
                        _cachedTexts.Add(txt);
                        RememberBaseColor(txt);
                    }
                }

                // Cache all legacy Text components
                Text[] standardTexts = root.GetComponentsInChildren<Text>(true);
                foreach (var txt in standardTexts)
                {
                    if (IsUnderExceptionParent(txt.transform)) continue;
                    if (tintOnlySymbologyElements && !SymbologyTintUtility.ShouldTintText(txt.transform, excludedPathFragments)) continue;
                    if (!_cachedStandardTexts.Contains(txt)) // Avoid duplicates
                    {
                        _cachedStandardTexts.Add(txt);
                        RememberBaseColor(txt);
                    }
                }
            }
            
            if (logColorChanges)
            {
                Debug.Log($"[SymbologyColorManager] Cached {_cachedImages.Count} images, {_cachedTexts.Count} TMP texts, and {_cachedStandardTexts.Count} standard texts from {rootsToProcess.Count} roots");
            }
        }
        
        private bool IsUnderExceptionParent(Transform t)
        {
            foreach (var parent in exceptionParents)
            {
                if (parent == null) continue;
                if (t == parent || t.IsChildOf(parent))
                    return true;
            }
            return false;
        }

        private void RememberBaseColor(Image image)
        {
            if (!_imageBaseColors.ContainsKey(image))
            {
                _imageBaseColors[image] = image.color;
            }
        }

        private void RememberBaseColor(TMP_Text text)
        {
            if (!_tmpTextBaseColors.ContainsKey(text))
            {
                _tmpTextBaseColors[text] = text.color;
            }
        }

        private void RememberBaseColor(Text text)
        {
            if (!_standardTextBaseColors.ContainsKey(text))
            {
                _standardTextBaseColors[text] = text.color;
            }
        }

        private Color GetBaseColor(Image image)
        {
            return _imageBaseColors.TryGetValue(image, out Color color) ? color : image.color;
        }

        private Color GetBaseColor(TMP_Text text)
        {
            return _tmpTextBaseColors.TryGetValue(text, out Color color) ? color : text.color;
        }

        private Color GetBaseColor(Text text)
        {
            return _standardTextBaseColors.TryGetValue(text, out Color color) ? color : text.color;
        }

        private Color BuildTintColor(Color tint, Color baseColor, float opacityMultiplier = -1f)
        {
            return SymbologyTintUtility.BuildTintColor(tint, baseColor, preserveElementAlpha, opacityMultiplier);
        }

        private void PruneDestroyedBaseColors()
        {
            RemoveDestroyedKeys(_imageBaseColors);
            RemoveDestroyedKeys(_tmpTextBaseColors);
            RemoveDestroyedKeys(_standardTextBaseColors);
        }

        private static void RemoveDestroyedKeys<T>(Dictionary<T, Color> colors) where T : Object
        {
            List<T> destroyedKeys = null;
            foreach (T key in colors.Keys)
            {
                if (key == null)
                {
                    if (destroyedKeys == null)
                    {
                        destroyedKeys = new List<T>();
                    }

                    destroyedKeys.Add(key);
                }
            }

            if (destroyedKeys == null)
            {
                return;
            }

            foreach (T key in destroyedKeys)
            {
                colors.Remove(key);
            }
        }
        
        private Color GetPresetColor(ColorPreset preset)
        {
            return preset switch
            {
                ColorPreset.Black => blackColor,
                ColorPreset.White => whiteColor,
                ColorPreset.Green => greenColor,
                ColorPreset.Cyan => cyanColor,
                ColorPreset.Custom => customColor,
                _ => blackColor
            };
        }
        
        private void AnimateToColor(Color targetColor)
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }
            
            _animationCoroutine = StartCoroutine(AnimateColorCoroutine(targetColor));
        }
        
        private IEnumerator AnimateColorCoroutine(Color targetColor)
        {
            Color startColor = _currentColor;
            float elapsed = 0f;
            
            while (elapsed < animationDuration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = elapsed / animationDuration;
                float curveT = easingCurve.Evaluate(t);
                
                Color currentLerpColor = Color.Lerp(startColor, targetColor, curveT);
                _currentColor = currentLerpColor;
                
                // Batch update all cached components
                for (int i = 0; i < _cachedImages.Count; i++)
                {
                    if (_cachedImages[i] != null)
                    {
                        _cachedImages[i].color = BuildTintColor(currentLerpColor, GetBaseColor(_cachedImages[i]));
                    }
                }
                
                for (int i = 0; i < _cachedTexts.Count; i++)
                {
                    if (_cachedTexts[i] != null)
                    {
                        _cachedTexts[i].color = BuildTintColor(currentLerpColor, GetBaseColor(_cachedTexts[i]));
                    }
                }
                
                for (int i = 0; i < _cachedStandardTexts.Count; i++)
                {
                    if (_cachedStandardTexts[i] != null)
                    {
                        _cachedStandardTexts[i].color = BuildTintColor(currentLerpColor, GetBaseColor(_cachedStandardTexts[i]));
                    }
                }
                
                yield return null;
            }
            
            // Ensure final color is exact
            _currentColor = targetColor;
            ApplyColorImmediate(targetColor);
            
            _animationCoroutine = null;
        }
        
        private void UpdateButtonIcon()
        {
            if (buttonIcon != null)
            {
                bool isDark = currentPreset == ColorPreset.Black;
                if (isDark && lightModeIcon != null)
                {
                    buttonIcon.sprite = lightModeIcon;
                }
                else if (!isDark && darkModeIcon != null)
                {
                    buttonIcon.sprite = darkModeIcon;
                }
            }
        }
        
        #endregion
        
        #region Context Menu
        
        [ContextMenu("Toggle Color")]
        private void ContextToggleColor()
        {
            ToggleColor();
        }
        
        [ContextMenu("Cycle Preset")]
        private void ContextCyclePreset()
        {
            CycleColorPreset();
        }
        
        [ContextMenu("Refresh Component Cache")]
        private void ContextRefreshCache()
        {
            RefreshCache();
            Debug.Log($"[SymbologyColorManager] Cache refreshed: {_cachedImages.Count} images, {_cachedTexts.Count} TMP texts, {_cachedStandardTexts.Count} standard texts");
        }
        
        [ContextMenu("Apply Current Preset")]
        private void ContextApplyPreset()
        {
            ApplyColorImmediate(GetPresetColor(currentPreset));
        }
        
        #endregion
    }
}
