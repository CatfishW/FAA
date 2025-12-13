using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using VisualUnderstanding.Core;

namespace VisualUnderstanding.UI
{
    /// <summary>
    /// Helper class to force layout rebuilds on text change
    /// </summary>
    public static class LayoutHelper
    {
        public static void ForceRebuildLayoutImmediate(RectTransform rect)
        {
            if (rect == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            
            // Also rebuild parent layouts up the hierarchy
            var parent = rect.parent as RectTransform;
            while (parent != null)
            {
                var fitter = parent.GetComponent<ContentSizeFitter>();
                if (fitter != null && fitter.enabled)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
                }
                parent = parent.parent as RectTransform;
            }
        }
    }
    /// <summary>
    /// Main panel for displaying visual analysis results with animations.
    /// Click on the panel to hide it.
    /// </summary>
    [AddComponentMenu("Visual Understanding/UI/Visual Briefing Panel")]
    public class VisualBriefingPanel : MonoBehaviour, IPointerClickHandler
    {
        [Header("Settings")]
        [SerializeField] private VisionSettings settings;
        
        [Header("UI References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform contentContainer;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private AnimatedTMPText animatedSummary;
        [SerializeField] private Transform findingsContainer;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private UnityEngine.UI.RawImage imagePreview;
        
        [Header("Dynamic Sizing")]
        [SerializeField] private ContentSizeFitter panelSizeFitter;
        [SerializeField] private ContentSizeFitter summarySizeFitter;
        [SerializeField] private LayoutElement summaryLayoutElement;
        [SerializeField] private float minPanelHeight = 150f;
        [SerializeField] private float maxPanelHeight = 600f;
        
        [Header("Prefabs")]
        [SerializeField] private GameObject briefingItemPrefab;
        
        [Header("Animation")]
        [SerializeField] private float slideDistance = 50f;
        [SerializeField] private float staggerDelay = 0.1f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField, Range(0f, 1f)] private float initialCanvasAlpha = 0f;
        
        [Header("Auto Hide")]
        [SerializeField] private bool autoHide = true;
        [SerializeField] private float autoHideDelay = 15f;
        
        private VisualAnalysisManager _manager;
        private List<GameObject> _spawnedItems = new List<GameObject>();
        private Coroutine _displayCoroutine;
        private Coroutine _autoHideCoroutine;
        private RectTransform _rectTransform;
        private Vector2 _originalPosition;
        private bool _isVisible;
        private bool _streamingInProgress; // Track if we're actively receiving streaming content
        
        #region Properties
        
        public bool IsVisible => _isVisible;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _originalPosition = _rectTransform.anchoredPosition;
            
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
            
            // Start fully hidden; animate to the configured alpha when shown
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            _isVisible = false;
            
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }
        }
        
        private void OnEnable()
        {
            _manager = FindObjectOfType<VisualAnalysisManager>();
            if (_manager != null)
            {
                _manager.OnAnalysisStarted += HandleAnalysisStarted;
                _manager.OnAnalysisComplete += HandleAnalysisComplete;
                _manager.OnAnalysisError += HandleAnalysisError;
            }
            
            // Subscribe to streaming events from the vision client
            var visionClient = FindObjectOfType<Network.VisionLLMClient>();
            if (visionClient != null)
            {
                visionClient.OnStreamingChunk += HandleStreamingChunk;
            }
        }
        
        private void OnDisable()
        {
            if (_manager != null)
            {
                _manager.OnAnalysisStarted -= HandleAnalysisStarted;
                _manager.OnAnalysisComplete -= HandleAnalysisComplete;
                _manager.OnAnalysisError -= HandleAnalysisError;
            }
            
            // Unsubscribe from streaming events
            var visionClient = FindObjectOfType<Network.VisionLLMClient>();
            if (visionClient != null)
            {
                visionClient.OnStreamingChunk -= HandleStreamingChunk;
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Set the visual analysis manager
        /// </summary>
        public void SetManager(VisualAnalysisManager manager)
        {
            if (_manager != null)
            {
                _manager.OnAnalysisStarted -= HandleAnalysisStarted;
                _manager.OnAnalysisComplete -= HandleAnalysisComplete;
                _manager.OnAnalysisError -= HandleAnalysisError;
            }
            
            _manager = manager;
            
            if (_manager != null)
            {
                _manager.OnAnalysisStarted += HandleAnalysisStarted;
                _manager.OnAnalysisComplete += HandleAnalysisComplete;
                _manager.OnAnalysisError += HandleAnalysisError;
            }
        }
        
        /// <summary>
        /// Show the panel with a result
        /// </summary>
        public void ShowResult(VisionAnalysisResult result)
        {
            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
            }
            
            _displayCoroutine = StartCoroutine(ShowResultCoroutine(result));
        }
        
        /// <summary>
        /// Display the captured image with animation
        /// </summary>
        public void ShowCapturedImage(Texture2D image)
        {
            if (imagePreview == null || image == null) return;
            
            imagePreview.texture = image;
            imagePreview.gameObject.SetActive(true);
            
            // Start fade+scale animation
            StartCoroutine(AnimateImageIn());
        }
        
        private IEnumerator AnimateImageIn()
        {
            if (imagePreview == null) yield break;
            
            var rect = imagePreview.GetComponent<RectTransform>();
            float duration = 0.4f;
            float elapsed = 0f;
            
            // Start with small scale and zero alpha
            rect.localScale = Vector3.one * 0.6f;
            imagePreview.color = new Color(1f, 1f, 1f, 0f);
            
            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = EaseOutCubic(elapsed / duration);
                
                rect.localScale = Vector3.Lerp(Vector3.one * 0.6f, Vector3.one, t);
                imagePreview.color = new Color(1f, 1f, 1f, t);
                
                yield return null;
            }
            
            rect.localScale = Vector3.one;
            imagePreview.color = Color.white;
        }

        /// <summary>
        /// Show loading state
        /// </summary>
        public void ShowLoading(VisualAnalysisType type)
        {
            ClearFindings();
            
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(true);
            }
            
            if (statusText != null)
            {
                statusText.text = $"Analyzing {GetTypeName(type)}...";
            }
            
            if (headerText != null)
            {
                headerText.text = $"{GetTypeName(type)} Analysis";
            }
            
            if (!_isVisible)
            {
                Show();
            }
        }
        
        /// <summary>
        /// Show the panel
        /// </summary>
        public void Show()
        {
            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
            }
            
            _displayCoroutine = StartCoroutine(ShowPanelCoroutine());
        }
        
        /// <summary>
        /// Hide the panel
        /// </summary>
        public void Hide()
        {
            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
            }
            
            if (_autoHideCoroutine != null)
            {
                StopCoroutine(_autoHideCoroutine);
                _autoHideCoroutine = null;
            }
            
            // Cancel any ongoing analysis so buttons reset their state
            if (_manager != null)
            {
                _manager.CancelCurrentAnalysis();
            }
            
            _displayCoroutine = StartCoroutine(HidePanelCoroutine());
        }
        
        /// <summary>
        /// Toggle visibility
        /// </summary>
        public void Toggle()
        {
            if (_isVisible)
                Hide();
            else
                Show();
        }
        
        #endregion
        
        #region IPointerClickHandler
        
        /// <summary>
        /// Hide the panel when clicked
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isVisible)
            {
                Hide();
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleAnalysisStarted(VisualAnalysisType type)
        {
            // Cancel existing auto-hide timer so it resets for the new analysis
            if (_autoHideCoroutine != null)
            {
                StopCoroutine(_autoHideCoroutine);
                _autoHideCoroutine = null;
            }
            
            // Reset streaming state - critical to prevent text accumulation
            _streamingInProgress = false;
            
            // Clear the summary text immediately to prevent old text from mixing with new
            if (summaryText != null)
            {
                summaryText.text = "";
                summaryText.color = Color.white;
            }
            
            // Fade out previous content before showing loading state
            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
            }
            _displayCoroutine = StartCoroutine(FadeOutAndShowLoading(type));
        }
        
        private IEnumerator FadeOutAndShowLoading(VisualAnalysisType type)
        {
            // Fade out existing summary text
            if (summaryText != null && !string.IsNullOrEmpty(summaryText.text))
            {
                float fadeDuration = 0.3f;
                float elapsed = 0f;
                Color startColor = summaryText.color;
                
                while (elapsed < fadeDuration)
                {
                    elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    float t = elapsed / fadeDuration;
                    summaryText.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
                    yield return null;
                }
            }
            
            // Clear and reset text
            if (summaryText != null)
            {
                summaryText.text = "";
                summaryText.color = Color.white;
            }
            
            // Now show loading
            ShowLoading(type);
        }
        
        private void HandleStreamingChunk(string chunk)
        {
            // Append streaming text directly to summary
            if (summaryText != null)
            {
                // On first chunk of new analysis, ensure text is cleared
                if (!_streamingInProgress)
                {
                    summaryText.text = "";
                    _streamingInProgress = true;
                }
                
                summaryText.text += chunk;
                
                // Convert markdown to TMP rich text after appending
                summaryText.text = ConvertMarkdownToRichText(summaryText.text);
                
                // Force layout rebuild to expand panel with new text
                RefreshPanelLayout();
            }
        }
        
        /// <summary>
        /// Refresh the panel layout to accommodate new text content
        /// </summary>
        private void RefreshPanelLayout()
        {
            if (summaryText == null) return;
            
            // Force the TMP text to update its mesh
            summaryText.ForceMeshUpdate();
            
            // Get the preferred height of the text
            float preferredHeight = summaryText.preferredHeight;
            
            // If we have a layout element, update its min height
            if (summaryLayoutElement != null)
            {
                summaryLayoutElement.minHeight = preferredHeight;
                summaryLayoutElement.preferredHeight = preferredHeight;
            }
            
            // Force layout rebuild from summary text up through panel
            var summaryRect = summaryText.GetComponent<RectTransform>();
            if (summaryRect != null)
            {
                LayoutHelper.ForceRebuildLayoutImmediate(summaryRect);
            }
            
            // Also rebuild the panel itself
            if (_rectTransform != null)
            {
                LayoutHelper.ForceRebuildLayoutImmediate(_rectTransform);
                
                // Clamp the panel height within bounds
                ClampPanelHeight();
            }
        }
        
        /// <summary>
        /// Clamp the panel height between min and max values
        /// </summary>
        private void ClampPanelHeight()
        {
            if (_rectTransform == null) return;
            
            Vector2 size = _rectTransform.sizeDelta;
            float clampedHeight = Mathf.Clamp(size.y, minPanelHeight, maxPanelHeight);
            
            if (Mathf.Abs(size.y - clampedHeight) > 0.01f)
            {
                _rectTransform.sizeDelta = new Vector2(size.x, clampedHeight);
            }
        }
        
        /// <summary>
        /// Convert markdown formatting to TMP rich text
        /// </summary>
        private string ConvertMarkdownToRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // Convert **bold** to <b>bold</b>
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"\*\*([^*]+)\*\*", "<b>$1</b>");
            
            // Convert *italic* to <i>italic</i>
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"\*([^*]+)\*", "<i>$1</i>");
            
            // Convert bullet points - to •
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"^- ", "• ", System.Text.RegularExpressions.RegexOptions.Multiline);
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"\n- ", "\n• ");
            
            return text;
        }
        
        private void HandleAnalysisComplete(VisionAnalysisResult result)
        {
            // Reset streaming state
            _streamingInProgress = false;
            
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }
            
            // Text is already populated via streaming, just update status
            if (statusText != null)
            {
                statusText.text = result.IsSuccess 
                    ? $"Analyzed in {result.processingTime:F1}s"
                    : $"Error: {result.error}";
            }
            
            // Set up auto-hide
            if (autoHide)
            {
                if (_autoHideCoroutine != null)
                {
                    StopCoroutine(_autoHideCoroutine);
                }
                float hideDelay = settings?.displayDuration ?? autoHideDelay;
                _autoHideCoroutine = StartCoroutine(AutoHideCoroutine(hideDelay));
            }
        }
        
        private void HandleAnalysisError(string error)
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }
            
            if (statusText != null)
            {
                statusText.text = $"Error: {error}";
            }
            
            // Auto hide on error after a delay
            if (_autoHideCoroutine != null)
            {
                StopCoroutine(_autoHideCoroutine);
            }
            _autoHideCoroutine = StartCoroutine(AutoHideCoroutine(5f));
        }
        
        #endregion
        
        #region Coroutines
        
        private IEnumerator ShowPanelCoroutine()
        {
            float fadeIn = settings?.fadeInDuration ?? 0.3f;
            float elapsed = 0f;
            float targetAlpha = Mathf.Clamp01(initialCanvasAlpha);
            bool enableInteraction = targetAlpha > 0.001f;
            
            Vector2 startPos = _originalPosition + Vector2.left * slideDistance;
            if (enableInteraction)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            
            while (elapsed < fadeIn)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = EaseOutCubic(elapsed / fadeIn);
                
                canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, t);
                _rectTransform.anchoredPosition = Vector2.Lerp(startPos, _originalPosition, t);
                
                yield return null;
            }
            
            canvasGroup.alpha = targetAlpha;
            _rectTransform.anchoredPosition = _originalPosition;
            _isVisible = enableInteraction;
            if (!enableInteraction)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
        
        private IEnumerator HidePanelCoroutine()
        {
            float fadeOut = settings?.fadeOutDuration ?? 0.5f;
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            
            Vector2 endPos = _originalPosition + Vector2.left * slideDistance;
            
            // Get image preview start values
            RectTransform previewRect = null;
            if (imagePreview != null)
            {
                previewRect = imagePreview.GetComponent<RectTransform>();
            }
            
            while (elapsed < fadeOut)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = EaseInCubic(elapsed / fadeOut);
                
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                _rectTransform.anchoredPosition = Vector2.Lerp(_originalPosition, endPos, t);
                
                // Fade image preview along with panel
                if (imagePreview != null)
                {
                    imagePreview.color = new Color(1f, 1f, 1f, 1f - t);
                    if (previewRect != null)
                    {
                        previewRect.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.8f, t);
                    }
                }
                
                yield return null;
            }
            
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            _isVisible = false;
            
            // Hide image preview
            if (imagePreview != null)
            {
                imagePreview.gameObject.SetActive(false);
            }
            
            ClearFindings();
        }
        
        private IEnumerator ShowResultCoroutine(VisionAnalysisResult result)
        {
            ClearFindings();
            
            if (headerText != null)
            {
                headerText.text = $"{GetTypeName(result.analysisType)} Briefing";
            }
            
            if (statusText != null)
            {
                statusText.text = result.IsSuccess 
                    ? $"Analyzed in {result.processingTime:F1}s"
                    : $"Error: {result.error}";
            }
            
            // Show summary with animation
            if (animatedSummary != null && !string.IsNullOrEmpty(result.summary))
            {
                float speed = settings?.typewriterSpeed ?? 80f;
                animatedSummary.CharactersPerSecond = speed;
                animatedSummary.SetText(result.summary);
            }
            else if (summaryText != null)
            {
                summaryText.text = result.summary ?? "";
            }
            
            // Spawn findings with staggered animation
            if (result.findings != null && briefingItemPrefab != null && findingsContainer != null)
            {
                for (int i = 0; i < result.findings.Count; i++)
                {
                    var finding = result.findings[i];
                    var itemGO = Instantiate(briefingItemPrefab, findingsContainer);
                    _spawnedItems.Add(itemGO);
                    
                    var itemUI = itemGO.GetComponent<BriefingItemUI>();
                    if (itemUI != null)
                    {
                        itemUI.Initialize(finding, settings);
                        itemUI.AnimateIn(i * staggerDelay);
                    }
                }
            }
            
            if (!_isVisible)
            {
                yield return ShowPanelCoroutine();
            }
            
            // Set up auto-hide
            if (autoHide)
            {
                if (_autoHideCoroutine != null)
                {
                    StopCoroutine(_autoHideCoroutine);
                }
                float hideDelay = settings?.displayDuration ?? autoHideDelay;
                _autoHideCoroutine = StartCoroutine(AutoHideCoroutine(hideDelay));
            }
        }
        
        private IEnumerator AutoHideCoroutine(float delay)
        {
            if (useUnscaledTime)
            {
                yield return new WaitForSecondsRealtime(delay);
            }
            else
            {
                yield return new WaitForSeconds(delay);
            }
            
            Hide();
        }
        
        #endregion
        
        #region Helper Methods
        
        private void ClearFindings()
        {
            foreach (var item in _spawnedItems)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }
            _spawnedItems.Clear();
        }
        
        private string GetTypeName(VisualAnalysisType type)
        {
            return type switch
            {
                VisualAnalysisType.SectionalChart => "Sectional Chart",
                VisualAnalysisType.WeatherRadar => "Weather Radar",
                _ => "Visual"
            };
        }
        
        private float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }
        
        private float EaseInCubic(float t)
        {
            return t * t * t;
        }
        
        #endregion
    }
}
