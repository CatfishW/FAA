using UnityEngine;
using UnityEngine.UI;

namespace AviationUI
{
    /// <summary>
    /// Base class for all Aviation UI panels.
    /// Provides common functionality for data binding, color theming, and visibility.
    /// </summary>
    public abstract class AviationUIPanel : MonoBehaviour
    {
        [Header("Panel Settings")]
        [Tooltip("Reference to the UI configuration")]
        [SerializeField] protected AviationUIConfig config;
        
        [Tooltip("Reference to the flight data provider")]
        [SerializeField] protected AviationFlightDataProvider dataProvider;
        
        [Tooltip("Panel visibility")]
        [SerializeField] protected bool isVisible = true;
        
        [Tooltip("Enable smooth value transitions")]
        [SerializeField] protected bool smoothTransitions = true;

        [Header("Panel References")]
        [Tooltip("Root canvas group for visibility control")]
        [SerializeField] protected CanvasGroup canvasGroup;
        
        [Tooltip("Panel root RectTransform")]
        [SerializeField] protected RectTransform panelRect;

        /// <summary>
        /// Current flight data snapshot
        /// </summary>
        protected AviationFlightData currentData;
        
        /// <summary>
        /// Smoothed flight data for display
        /// </summary>
        protected AviationFlightData displayData;

        /// <summary>
        /// Is the panel currently visible
        /// </summary>
        public bool IsVisible
        {
            get => isVisible;
            set => SetVisibility(value);
        }

        /// <summary>
        /// Panel identifier for the manager
        /// </summary>
        public abstract string PanelId { get; }

        /// <summary>
        /// Default anchor position for this panel
        /// </summary>
        public abstract Vector2 DefaultAnchorPosition { get; }

        protected virtual void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (panelRect == null)
            {
                panelRect = GetComponent<RectTransform>();
            }

            displayData = new AviationFlightData();
            currentData = new AviationFlightData();
        }

        protected virtual void Start()
        {
            // Subscribe to flight data updates
            if (dataProvider != null)
            {
                dataProvider.OnFlightDataUpdated += OnFlightDataUpdated;
            }

            // Apply initial visibility
            SetVisibility(isVisible);
            
            // Initialize panel
            InitializePanel();
        }

        protected virtual void OnDestroy()
        {
            // Unsubscribe from flight data updates
            if (dataProvider != null)
            {
                dataProvider.OnFlightDataUpdated -= OnFlightDataUpdated;
            }
        }

        protected virtual void Update()
        {
            if (!isVisible) return;
            
            // Smooth data transitions
            if (smoothTransitions && config != null)
            {
                displayData = AviationFlightData.Lerp(displayData, currentData, config.smoothingFactor);
            }
            else
            {
                displayData = currentData.Clone();
            }

            // Update panel display
            UpdateDisplay();
        }

        /// <summary>
        /// Called when flight data is updated from the provider
        /// </summary>
        protected virtual void OnFlightDataUpdated(AviationFlightData newData)
        {
            currentData = newData;
        }

        /// <summary>
        /// Initialize the panel (called once in Start)
        /// </summary>
        protected abstract void InitializePanel();

        /// <summary>
        /// Update the panel display with current data (called every frame)
        /// </summary>
        protected abstract void UpdateDisplay();

        /// <summary>
        /// Set panel visibility with optional animation
        /// </summary>
        public virtual void SetVisibility(bool visible, bool animate = false)
        {
            isVisible = visible;
            
            if (canvasGroup != null)
            {
                if (animate && config != null && config.enableAnimations)
                {
                    // Could add animation here
                    StopAllCoroutines();
                    StartCoroutine(AnimateVisibility(visible));
                }
                else
                {
                    canvasGroup.alpha = visible ? 1f : 0f;
                    canvasGroup.interactable = visible;
                    canvasGroup.blocksRaycasts = visible;
                }
            }
        }

        /// <summary>
        /// Animate visibility transition
        /// </summary>
        protected virtual System.Collections.IEnumerator AnimateVisibility(bool visible)
        {
            float targetAlpha = visible ? 1f : 0f;
            float duration = 0.3f;
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        /// <summary>
        /// Set the configuration asset
        /// </summary>
        public void SetConfig(AviationUIConfig newConfig)
        {
            config = newConfig;
            ApplyConfig();
        }

        /// <summary>
        /// Set the flight data provider
        /// </summary>
        public void SetDataProvider(AviationFlightDataProvider provider)
        {
            // Unsubscribe from old provider
            if (dataProvider != null)
            {
                dataProvider.OnFlightDataUpdated -= OnFlightDataUpdated;
            }

            dataProvider = provider;

            // Subscribe to new provider
            if (dataProvider != null)
            {
                dataProvider.OnFlightDataUpdated += OnFlightDataUpdated;
            }
        }

        /// <summary>
        /// Apply the current configuration to the panel
        /// </summary>
        protected virtual void ApplyConfig()
        {
            if (config == null) return;
            
            // Apply scale
            if (panelRect != null)
            {
                panelRect.localScale = Vector3.one * config.uiScale;
            }

            // Apply colors - override in derived classes
            ApplyColors();
        }

        /// <summary>
        /// Apply color theme to panel elements
        /// </summary>
        protected virtual void ApplyColors()
        {
            // Override in derived classes to apply colors to specific elements
        }

        /// <summary>
        /// Get color based on value thresholds
        /// </summary>
        protected Color GetThresholdColor(float value, float warningThreshold, float dangerThreshold)
        {
            if (config != null)
            {
                return config.GetThresholdColor(value, warningThreshold, dangerThreshold);
            }
            return Color.white;
        }

        /// <summary>
        /// Manually update with specific flight data (for testing)
        /// </summary>
        public void ManualUpdate(AviationFlightData data)
        {
            currentData = data;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor validation
        /// </summary>
        protected virtual void OnValidate()
        {
            if (Application.isPlaying)
            {
                SetVisibility(isVisible);
            }
        }
#endif
    }
}
