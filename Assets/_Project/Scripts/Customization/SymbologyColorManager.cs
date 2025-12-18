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
        
        [Header("Debug")]
        [SerializeField] private bool logColorChanges = false;
        
        #endregion
        
        #region Private Fields
        
        private List<Image> _cachedImages = new List<Image>();
        private List<TMP_Text> _cachedTexts = new List<TMP_Text>();
        private List<Text> _cachedStandardTexts = new List<Text>();
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
        }
        
        private void OnEnable()
        {
            if (colorToggleButton != null && Application.isPlaying)
            {
                colorToggleButton.onClick.RemoveListener(ToggleColor);
                colorToggleButton.onClick.AddListener(ToggleColor);
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
                    img.color = color;
                }
            }
            
            foreach (var txt in _cachedTexts)
            {
                if (txt != null)
                {
                    txt.color = color;
                }
            }
            
            foreach (var txt in _cachedStandardTexts)
            {
                if (txt != null)
                {
                    txt.color = color;
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
                    var c = img.color;
                    c.a = opacity;
                    img.color = c;
                }
            }
            
            foreach (var txt in _cachedTexts)
            {
                if (txt != null)
                {
                    var c = txt.color;
                    c.a = opacity;
                    txt.color = c;
                }
            }
            
            foreach (var txt in _cachedStandardTexts)
            {
                if (txt != null)
                {
                    var c = txt.color;
                    c.a = opacity;
                    txt.color = c;
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
                    if (!_cachedImages.Contains(img)) // Avoid duplicates
                        _cachedImages.Add(img);
                }
                
                // Cache all TMP_Text components
                TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
                foreach (var txt in texts)
                {
                    if (IsUnderExceptionParent(txt.transform)) continue;
                    if (!_cachedTexts.Contains(txt)) // Avoid duplicates
                        _cachedTexts.Add(txt);
                }

                // Cache all legacy Text components
                Text[] standardTexts = root.GetComponentsInChildren<Text>(true);
                foreach (var txt in standardTexts)
                {
                    if (IsUnderExceptionParent(txt.transform)) continue;
                    if (!_cachedStandardTexts.Contains(txt)) // Avoid duplicates
                        _cachedStandardTexts.Add(txt);
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
                        _cachedImages[i].color = currentLerpColor;
                    }
                }
                
                for (int i = 0; i < _cachedTexts.Count; i++)
                {
                    if (_cachedTexts[i] != null)
                    {
                        _cachedTexts[i].color = currentLerpColor;
                    }
                }
                
                for (int i = 0; i < _cachedStandardTexts.Count; i++)
                {
                    if (_cachedStandardTexts[i] != null)
                    {
                        _cachedStandardTexts[i].color = currentLerpColor;
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
