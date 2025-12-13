using UnityEngine;
using TMPro;
using AircraftControl.Core;

namespace HUDControl.Core
{
    /// <summary>
    /// Abstract base class for all HUD elements.
    /// Provides common functionality for animation, visibility, and data binding.
    /// </summary>
    public abstract class HUDElementBase : MonoBehaviour, IHUDElement
    {
        #region Inspector Settings
        
        [Header("Element Settings")]
        [Tooltip("Enable/disable this element")]
        [SerializeField] protected bool isEnabled = true;
        
        [Tooltip("Animation responsiveness (higher = faster, 5-15 typical)")]
        [Range(1f, 30f)]
        [SerializeField] protected float animationSpeed = 10f;
        
        [Tooltip("Threshold below which value changes are ignored")]
        [SerializeField] protected float updateThreshold = 0.01f;
        
        [Header("Visibility")]
        [SerializeField] protected CanvasGroup canvasGroup;
        [SerializeField] protected float fadeSpeed = 5f;
        
        #endregion
        
        #region Protected Fields
        
        protected RectTransform rectTransform;
        protected float smoothing;
        protected bool isInitialized;
        
        // Cached previous values for change detection
        protected AircraftState cachedState;
        
        #endregion
        
        #region IHUDElement Implementation
        
        public abstract string ElementId { get; }
        
        public bool IsEnabled => isEnabled && gameObject.activeInHierarchy;
        
        public virtual void Initialize()
        {
            rectTransform = GetComponent<RectTransform>();
            
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
            
            OnInitialize();
            isInitialized = true;
        }
        
        public virtual void UpdateElement(AircraftState state)
        {
            if (!isEnabled || state == null) return;
            
            // Calculate frame-rate independent smoothing
            smoothing = HUDAnimator.CalculateSmoothing(animationSpeed);
            
            // Perform element-specific update
            OnUpdateElement(state);
            
            // Cache state for next frame comparison
            cachedState = state;
        }
        
        public virtual void SetEnabled(bool enabled)
        {
            isEnabled = enabled;
            
            if (canvasGroup != null)
            {
                canvasGroup.alpha = enabled ? 1f : 0f;
                canvasGroup.interactable = enabled;
                canvasGroup.blocksRaycasts = enabled;
            }
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        protected virtual void Awake()
        {
            CacheReferences();
        }
        
        protected virtual void Start()
        {
            if (!isInitialized)
            {
                Initialize();
            }
        }
        
        protected virtual void OnEnable()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = isEnabled ? 1f : 0f;
            }
        }
        
        #endregion
        
        #region Protected Virtual Methods
        
        /// <summary>
        /// Cache component references. Override to add element-specific caching.
        /// </summary>
        protected virtual void CacheReferences()
        {
            rectTransform = GetComponent<RectTransform>();
            
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }
        
        /// <summary>
        /// Perform element-specific initialization
        /// </summary>
        protected abstract void OnInitialize();
        
        /// <summary>
        /// Perform element-specific update with aircraft state
        /// </summary>
        protected abstract void OnUpdateElement(AircraftState state);
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Set text on a TMP component safely
        /// </summary>
        protected void SetText(TMP_Text textComponent, string value)
        {
            if (textComponent != null && textComponent.text != value)
            {
                textComponent.text = value;
            }
        }
        
        /// <summary>
        /// Set text with format string
        /// </summary>
        protected void SetTextFormatted(TMP_Text textComponent, string format, params object[] args)
        {
            if (textComponent != null)
            {
                string value = string.Format(format, args);
                if (textComponent.text != value)
                {
                    textComponent.text = value;
                }
            }
        }
        
        /// <summary>
        /// Check if a value has changed beyond threshold
        /// </summary>
        protected bool HasChanged(float current, float previous, float threshold = -1f)
        {
            if (threshold < 0) threshold = updateThreshold;
            return Mathf.Abs(current - previous) > threshold;
        }
        
        /// <summary>
        /// Animate fade (call each frame for smooth transition)
        /// </summary>
        protected void AnimateFade(bool visible)
        {
            if (canvasGroup == null) return;
            
            float targetAlpha = visible ? 1f : 0f;
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, 
                HUDAnimator.CalculateSmoothing(fadeSpeed));
        }
        
        #endregion
        
        #region Editor
        
#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (canvasGroup != null && !Application.isPlaying)
            {
                canvasGroup.alpha = isEnabled ? 1f : 0.3f;
            }
        }
        
        [ContextMenu("Auto-Find References")]
        protected virtual void AutoFindReferences()
        {
            CacheReferences();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
        
        #endregion
    }
}
