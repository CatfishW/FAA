using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VoiceControl.Manager;

namespace VoiceControl.UI
{
    /// <summary>
    /// Main voice control overlay panel with animated visibility.
    /// Uses script-based animations for high performance.
    /// </summary>
    [AddComponentMenu("Voice Control/UI/Voice Control UI")]
    public class VoiceControlUI : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private VoiceControlManager manager;
        [SerializeField] private VoiceControlSettings settings;
        
        [Header("UI References")]
        [SerializeField] private RectTransform panelTransform;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text transcriptionText;
        
        [Header("Child Components")]
        [SerializeField] private VoiceLevelIndicator levelIndicator;
        [SerializeField] private VoiceCommandFeedback commandFeedback;
        
        [Header("Animation")]
        [SerializeField] private float animationDuration = 0.25f;
        [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        private bool _isVisible = false;
        private Coroutine _animationCoroutine;
        private RectTransform _rectTransform;
        
        public bool IsVisible => _isVisible;
        
        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            
            if (panelTransform == null)
                panelTransform = _rectTransform;
            
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            
            // Start hidden
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        
        private void Start()
        {
            AutoFindComponents();
            ApplySettings();
            SubscribeToEvents();
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        private void AutoFindComponents()
        {
            if (manager == null)
                manager = FindObjectOfType<VoiceControlManager>();
            
            if (settings == null && manager != null)
            {
                // Try to get from manager via reflection
                var field = manager.GetType().GetField("settings", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
                if (field != null)
                    settings = field.GetValue(manager) as VoiceControlSettings;
            }
            
            if (levelIndicator == null)
                levelIndicator = GetComponentInChildren<VoiceLevelIndicator>();
            
            if (commandFeedback == null)
                commandFeedback = GetComponentInChildren<VoiceCommandFeedback>();
        }
        
        private void ApplySettings()
        {
            if (settings == null) return;
            
            animationDuration = settings.animationDuration;
            
            if (backgroundImage != null)
            {
                backgroundImage.color = settings.secondaryColor;
            }
        }
        
        private void SubscribeToEvents()
        {
            if (manager == null) return;
            
            manager.OnRecordingStateChanged += OnRecordingStateChanged;
            manager.OnStateChanged += OnStateChanged;
            manager.OnTranscriptionReceived += OnTranscriptionReceived;
            manager.OnCommandExecuted += OnCommandExecuted;
            manager.OnError += OnError;
        }
        
        private void UnsubscribeFromEvents()
        {
            if (manager == null) return;
            
            manager.OnRecordingStateChanged -= OnRecordingStateChanged;
            manager.OnStateChanged -= OnStateChanged;
            manager.OnTranscriptionReceived -= OnTranscriptionReceived;
            manager.OnCommandExecuted -= OnCommandExecuted;
            manager.OnError -= OnError;
        }
        
        #region Public Methods
        
        /// <summary>
        /// Show the voice control panel
        /// </summary>
        public void Show()
        {
            if (_isVisible) return;
            _isVisible = true;
            AnimateVisibility(true);
        }
        
        /// <summary>
        /// Hide the voice control panel
        /// </summary>
        public void Hide()
        {
            if (!_isVisible) return;
            _isVisible = false;
            AnimateVisibility(false);
        }
        
        /// <summary>
        /// Toggle panel visibility
        /// </summary>
        public void Toggle()
        {
            if (_isVisible)
                Hide();
            else
                Show();
        }
        
        /// <summary>
        /// Set visibility without animation
        /// </summary>
        public void SetVisibilityImmediate(bool visible)
        {
            _isVisible = visible;
            
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
            
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnRecordingStateChanged(bool isRecording)
        {
            if (isRecording && !_isVisible)
            {
                Show();
            }
            
            UpdateStatusText();
        }
        
        private void OnStateChanged(VoiceControlState state)
        {
            UpdateStatusText();
        }
        
        private void OnTranscriptionReceived(string transcription)
        {
            if (transcriptionText != null)
            {
                transcriptionText.text = $"\"{transcription}\"";
            }
        }
        
        private void OnCommandExecuted(string command, bool success)
        {
            if (commandFeedback != null)
            {
                commandFeedback.ShowFeedback(command, success);
            }
        }
        
        private void OnError(string error)
        {
            if (commandFeedback != null)
            {
                commandFeedback.ShowError(error);
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private void UpdateStatusText()
        {
            if (statusText == null || manager == null) return;
            
            string status;
            Color statusColor = settings != null ? settings.primaryColor : Color.cyan;
            
            switch (manager.CurrentState)
            {
                case VoiceControlState.Recording:
                    status = "● Listening...";
                    statusColor = settings != null ? settings.recordingColor : Color.red;
                    break;
                case VoiceControlState.ProcessingSTT:
                    status = "◌ Transcribing...";
                    break;
                case VoiceControlState.ProcessingLLM:
                    status = "◌ Processing...";
                    break;
                case VoiceControlState.Executing:
                    status = "▶ Executing...";
                    break;
                default:
                    status = "Ready";
                    break;
            }
            
            statusText.text = status;
            statusText.color = statusColor;
        }
        
        private void AnimateVisibility(bool show)
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }
            _animationCoroutine = StartCoroutine(AnimateVisibilityCoroutine(show));
        }
        
        private IEnumerator AnimateVisibilityCoroutine(bool show)
        {
            float startAlpha = canvasGroup.alpha;
            float targetAlpha = show ? 1f : 0f;
            
            Vector2 startOffset = panelTransform.anchoredPosition;
            Vector2 targetOffset = startOffset;
            
            // Slide animation
            float slideDistance = 20f;
            if (show)
            {
                startOffset.y -= slideDistance;
            }
            else
            {
                targetOffset.y -= slideDistance;
            }
            
            if (show)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                panelTransform.anchoredPosition = startOffset;
            }
            
            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = easeCurve.Evaluate(elapsed / animationDuration);
                
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                panelTransform.anchoredPosition = Vector2.Lerp(startOffset, targetOffset, t);
                
                yield return null;
            }
            
            canvasGroup.alpha = targetAlpha;
            panelTransform.anchoredPosition = targetOffset;
            
            if (!show)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            
            _animationCoroutine = null;
        }
        
        #endregion
    }
}
