using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VisualUnderstanding.Core;

namespace VisualUnderstanding.UI
{
    /// <summary>
    /// Animated button that triggers vision analysis when pressed.
    /// Provides visual feedback during analysis.
    /// </summary>
    [AddComponentMenu("Visual Understanding/UI/Analysis Trigger Button")]
    public class AnalysisTriggerButton : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private VisualAnalysisType analysisType = VisualAnalysisType.SectionalChart;
        
        [Header("References")]
        [SerializeField] private Button button;
        [SerializeField] private Image buttonImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private CanvasGroup canvasGroup;
        
        [Header("Animation")]
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseMinScale = 0.95f;
        [SerializeField] private float pulseMaxScale = 1.05f;
        [SerializeField] private float pressScaleMultiplier = 0.9f;
        [SerializeField] private float animationDuration = 0.15f;
        
        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.4f, 0.6f, 1f);
        [SerializeField] private Color processingColor = new Color(0.4f, 0.8f, 1f, 1f);
        [SerializeField] private Color successColor = new Color(0.3f, 0.8f, 0.4f, 1f);
        [SerializeField] private Color errorColor = new Color(1f, 0.3f, 0.3f, 1f);
        
        [Header("Image Sources")]
        [SerializeField] private Integration.RadarImageCapture radarCapture;
        [SerializeField] private Integration.SectionalChartCapture chartCapture;
        
        private VisualAnalysisManager _manager;
        private RectTransform _rectTransform;
        private Vector3 _originalScale;
        private bool _isProcessing;
        private Coroutine _animationCoroutine;
        
        #region Properties
        
        public VisualAnalysisType AnalysisType
        {
            get => analysisType;
            set => analysisType = value;
        }
        
        public bool IsProcessing => _isProcessing;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _originalScale = _rectTransform != null ? _rectTransform.localScale : Vector3.one;
            
            if (button == null)
                button = GetComponent<Button>();
            
            if (buttonImage == null)
                buttonImage = GetComponent<Image>();
            
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        
        private void OnEnable()
        {
            _manager = FindObjectOfType<VisualAnalysisManager>();
            
            if (button != null)
            {
                button.onClick.AddListener(OnButtonClick);
            }
            
            if (_manager != null)
            {
                _manager.OnAnalysisStarted += HandleAnalysisStarted;
                _manager.OnAnalysisComplete += HandleAnalysisComplete;
                _manager.OnAnalysisError += HandleAnalysisError;
            }
        }
        
        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClick);
            }
            
            if (_manager != null)
            {
                _manager.OnAnalysisStarted -= HandleAnalysisStarted;
                _manager.OnAnalysisComplete -= HandleAnalysisComplete;
                _manager.OnAnalysisError -= HandleAnalysisError;
            }
        }
        
        private void Update()
        {
            if (_isProcessing)
            {
                // Pulse animation while processing
                float pulse = Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI);
                float scale = Mathf.Lerp(pulseMinScale, pulseMaxScale, (pulse + 1f) / 2f);
                _rectTransform.localScale = _originalScale * scale;
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Set the manager reference
        /// </summary>
        public void SetManager(VisualAnalysisManager manager)
        {
            if (_manager != null)
            {
                _manager.OnAnalysisComplete -= HandleAnalysisComplete;
                _manager.OnAnalysisError -= HandleAnalysisError;
            }
            
            _manager = manager;
            
            if (_manager != null)
            {
                _manager.OnAnalysisComplete += HandleAnalysisComplete;
                _manager.OnAnalysisError += HandleAnalysisError;
            }
        }
        
        /// <summary>
        /// Set the radar capture source
        /// </summary>
        public void SetRadarCapture(Integration.RadarImageCapture capture)
        {
            radarCapture = capture;
        }
        
        /// <summary>
        /// Set the chart capture source
        /// </summary>
        public void SetChartCapture(Integration.SectionalChartCapture capture)
        {
            chartCapture = capture;
        }
        
        /// <summary>
        /// Trigger analysis manually
        /// </summary>
        public void TriggerAnalysis()
        {
            if (_isProcessing) return;
            
            OnButtonClick();
        }
        
        #endregion
        
        #region Button Handling
        
        private void OnButtonClick()
        {
            Debug.Log($"[AnalysisTriggerButton] Button clicked! Type: {analysisType}, IsProcessing: {_isProcessing}");
            
            // If already processing, cancel and start new analysis
            if (_isProcessing)
            {
                Debug.Log("[AnalysisTriggerButton] Canceling current analysis to start new one");
                _manager?.CancelCurrentAnalysis();
                StopProcessing(false);
            }
            
            if (_manager == null)
            {
                _manager = FindObjectOfType<VisualAnalysisManager>();
                if (_manager == null)
                {
                    Debug.LogError("[AnalysisTriggerButton] No VisualAnalysisManager found in scene!");
                    ShowError();
                    return;
                }
                else
                {
                    Debug.Log("[AnalysisTriggerButton] Found VisualAnalysisManager");
                    // Subscribe to events
                    _manager.OnAnalysisComplete += HandleAnalysisComplete;
                    _manager.OnAnalysisError += HandleAnalysisError;
                }
            }
            
            // Start capture and analysis coroutine
            StartCoroutine(CaptureAndAnalyzeCoroutine());
        }
        
        private IEnumerator CaptureAndAnalyzeCoroutine()
        {
            // Wait for end of frame to ensure screen is rendered
            yield return new WaitForEndOfFrame();
            
            // Capture image based on type
            Texture2D image = CaptureImage();
            if (image == null)
            {
                Debug.LogError($"[AnalysisTriggerButton] Failed to capture image for {analysisType}");
                ShowError();
                yield break;
            }
            
            Debug.Log($"[AnalysisTriggerButton] Image captured successfully: {image.width}x{image.height}");
            
            // Show captured image in briefing panel
            var briefingPanel = FindObjectOfType<VisualBriefingPanel>();
            if (briefingPanel != null)
            {
                briefingPanel.ShowCapturedImage(image);
            }
            
            // Start processing animation
            StartProcessing();
            
            // Trigger analysis
            // Trigger analysis with forceRefresh to bypass cache
            Debug.Log("[AnalysisTriggerButton] Calling AnalyzeWithType on manager with forceRefresh...");
            _manager.AnalyzeWithType(image, analysisType, null, forceRefresh: true);
        }
        
        private Texture2D CaptureImage()
        {
            Debug.Log($"[AnalysisTriggerButton] Attempting to capture for {analysisType}");
            
            switch (analysisType)
            {
                case VisualAnalysisType.WeatherRadar:
                    if (radarCapture != null)
                    {
                        Debug.Log("[AnalysisTriggerButton] Using assigned RadarImageCapture");
                        return radarCapture.Capture();
                    }
                    break;
                    
                case VisualAnalysisType.SectionalChart:
                    if (chartCapture != null)
                    {
                        Debug.Log("[AnalysisTriggerButton] Using assigned SectionalChartCapture");
                        return chartCapture.Capture();
                    }
                    break;
            }
            
            // Try to find capture components if not assigned
            if (analysisType == VisualAnalysisType.WeatherRadar)
            {
                radarCapture = FindObjectOfType<Integration.RadarImageCapture>();
                if (radarCapture != null)
                {
                    Debug.Log("[AnalysisTriggerButton] Found RadarImageCapture in scene");
                    return radarCapture.Capture();
                }
            }
            else if (analysisType == VisualAnalysisType.SectionalChart)
            {
                chartCapture = FindObjectOfType<Integration.SectionalChartCapture>();
                if (chartCapture != null)
                {
                    Debug.Log("[AnalysisTriggerButton] Found SectionalChartCapture in scene");
                    return chartCapture.Capture();
                }
            }
            
            // Fallback: Capture the entire screen for testing
            Debug.Log("[AnalysisTriggerButton] No capture component found - using screen capture fallback");
            return CaptureScreen();
        }
        
        private Texture2D CaptureScreen()
        {
            // Capture the entire screen as a fallback for testing
            int width = Screen.width;
            int height = Screen.height;
            
            Texture2D screenTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenTexture.Apply();
            
            Debug.Log($"[AnalysisTriggerButton] Screen captured: {width}x{height}");
            return screenTexture;
        }
        
        #endregion
        
        #region Animation
        
        private void StartProcessing()
        {
            _isProcessing = true;
            
            if (button != null)
                button.interactable = false;
            
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
            
            _animationCoroutine = StartCoroutine(PressAnimation());
            
            // Change color to processing
            if (buttonImage != null)
                buttonImage.color = processingColor;
        }
        
        private void StopProcessing(bool success)
        {
            _isProcessing = false;
            
            if (button != null)
                button.interactable = true;
            
            _rectTransform.localScale = _originalScale;
            
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
            
            _animationCoroutine = StartCoroutine(ResultAnimation(success));
        }
        
        private IEnumerator PressAnimation()
        {
            // Press down
            float elapsed = 0f;
            Vector3 targetScale = _originalScale * pressScaleMultiplier;
            
            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseOutCubic(elapsed / animationDuration);
                _rectTransform.localScale = Vector3.Lerp(_originalScale, targetScale, t);
                yield return null;
            }
            
            // Bounce back slightly
            elapsed = 0f;
            Vector3 bounceScale = _originalScale * 1.02f;
            
            while (elapsed < animationDuration * 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseOutCubic(elapsed / (animationDuration * 0.5f));
                _rectTransform.localScale = Vector3.Lerp(targetScale, bounceScale, t);
                yield return null;
            }
        }
        
        private IEnumerator ResultAnimation(bool success)
        {
            Color targetColor = success ? successColor : errorColor;
            Color startColor = buttonImage != null ? buttonImage.color : processingColor;
            
            // Flash result color
            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseOutCubic(elapsed / animationDuration);
                
                if (buttonImage != null)
                    buttonImage.color = Color.Lerp(startColor, targetColor, t);
                
                _rectTransform.localScale = Vector3.Lerp(_originalScale * 0.95f, _originalScale * 1.1f, t);
                
                yield return null;
            }
            
            // Hold briefly
            yield return new WaitForSecondsRealtime(0.3f);
            
            // Return to normal
            elapsed = 0f;
            while (elapsed < animationDuration * 2f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseOutCubic(elapsed / (animationDuration * 2f));
                
                if (buttonImage != null)
                    buttonImage.color = Color.Lerp(targetColor, normalColor, t);
                
                _rectTransform.localScale = Vector3.Lerp(_originalScale * 1.1f, _originalScale, t);
                
                yield return null;
            }
            
            if (buttonImage != null)
                buttonImage.color = normalColor;
            
            _rectTransform.localScale = _originalScale;
        }
        
        private void ShowError()
        {
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
            
            _animationCoroutine = StartCoroutine(ResultAnimation(false));
        }
        
        private float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleAnalysisStarted(VisualAnalysisType startedType)
        {
            // If a different analysis type started, reset this button
            if (startedType != analysisType && _isProcessing)
            {
                Debug.Log($"[AnalysisTriggerButton] Different analysis type ({startedType}) started, resetting {analysisType} button");
                _isProcessing = false;
                
                if (button != null)
                    button.interactable = true;
                
                _rectTransform.localScale = _originalScale;
                
                if (buttonImage != null)
                    buttonImage.color = normalColor;
            }
        }
        
        private void HandleAnalysisComplete(VisionAnalysisResult result)
        {
            if (result.analysisType == analysisType)
            {
                StopProcessing(result.IsSuccess);
            }
        }
        
        private void HandleAnalysisError(string error)
        {
            if (_isProcessing)
            {
                StopProcessing(false);
            }
        }
        
        #endregion
    }
}
