using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VisualUnderstanding.Core;

namespace VisualUnderstanding.UI
{
    /// <summary>
    /// Individual briefing item with icon, text, and animations.
    /// Used as a child of VisualBriefingPanel.
    /// </summary>
    [AddComponentMenu("Visual Understanding/UI/Briefing Item UI")]
    public class BriefingItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image priorityIndicator;
        [SerializeField] private CanvasGroup canvasGroup;
        
        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float slideDistance = 20f;
        [SerializeField] private bool useUnscaledTime = true;
        
        private RectTransform _rectTransform;
        private Vector2 _targetPosition;
        private float _animationProgress;
        private bool _isAnimating;
        
        #region Properties
        
        public bool IsAnimating => _isAnimating;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }
        
        private void Update()
        {
            if (!_isAnimating) return;
            
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _animationProgress += deltaTime / fadeInDuration;
            
            if (_animationProgress >= 1f)
            {
                _animationProgress = 1f;
                _isAnimating = false;
            }
            
            float t = EaseOutCubic(_animationProgress);
            
            // Fade in
            canvasGroup.alpha = t;
            
            // Slide in
            Vector2 startPos = _targetPosition + Vector2.right * slideDistance;
            _rectTransform.anchoredPosition = Vector2.Lerp(startPos, _targetPosition, t);
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Initialize the briefing item
        /// </summary>
        public void Initialize(AnalysisFinding finding, VisionSettings settings)
        {
            if (titleText != null)
            {
                titleText.text = finding.title;
            }
            
            if (descriptionText != null)
            {
                descriptionText.text = finding.description;
                descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(finding.description));
            }
            
            // Set priority color
            Color priorityColor = settings?.GetPriorityColor(finding.priority) ?? GetDefaultPriorityColor(finding.priority);
            
            if (priorityIndicator != null)
            {
                priorityIndicator.color = priorityColor;
            }
            
            if (titleText != null)
            {
                titleText.color = priorityColor;
            }
            
            // Set icon based on icon name
            if (iconImage != null && !string.IsNullOrEmpty(finding.iconName))
            {
                // Icon loading would be done here - for now just show/hide
                iconImage.gameObject.SetActive(true);
            }
        }
        
        /// <summary>
        /// Animate the item appearing
        /// </summary>
        public void AnimateIn(float delay = 0f)
        {
            _targetPosition = _rectTransform.anchoredPosition;
            _animationProgress = 0f;
            canvasGroup.alpha = 0f;
            
            if (delay > 0f)
            {
                StartCoroutine(DelayedAnimateIn(delay));
            }
            else
            {
                _isAnimating = true;
            }
        }
        
        /// <summary>
        /// Show instantly without animation
        /// </summary>
        public void ShowInstant()
        {
            canvasGroup.alpha = 1f;
            _isAnimating = false;
        }
        
        /// <summary>
        /// Hide instantly
        /// </summary>
        public void HideInstant()
        {
            canvasGroup.alpha = 0f;
            _isAnimating = false;
        }
        
        #endregion
        
        #region Private Methods
        
        private System.Collections.IEnumerator DelayedAnimateIn(float delay)
        {
            if (useUnscaledTime)
            {
                yield return new WaitForSecondsRealtime(delay);
            }
            else
            {
                yield return new WaitForSeconds(delay);
            }
            
            _isAnimating = true;
        }
        
        private float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }
        
        private Color GetDefaultPriorityColor(BriefingPriority priority)
        {
            return priority switch
            {
                BriefingPriority.Info => new Color(0.4f, 0.8f, 1f, 1f),
                BriefingPriority.Caution => new Color(1f, 0.9f, 0.3f, 1f),
                BriefingPriority.Warning => new Color(1f, 0.6f, 0.2f, 1f),
                BriefingPriority.Critical => new Color(1f, 0.3f, 0.3f, 1f),
                _ => Color.white
            };
        }
        
        #endregion
    }
}
