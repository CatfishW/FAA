using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VoiceControl.Manager;

namespace VoiceControl.UI
{
    /// <summary>
    /// Displays command feedback with animated transitions.
    /// Shows transcription, executed commands, and errors.
    /// </summary>
    [AddComponentMenu("Voice Control/UI/Voice Command Feedback")]
    public class VoiceCommandFeedback : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private VoiceControlSettings settings;
        
        [Header("UI References")]
        [SerializeField] private TMP_Text commandText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image iconImage;
        [SerializeField] private CanvasGroup canvasGroup;
        
        [Header("Icons")]
        [SerializeField] private Sprite successIcon;
        [SerializeField] private Sprite errorIcon;
        
        [Header("Animation")]
        [SerializeField] private float displayDuration = 3f;
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        
        [Header("Colors")]
        [SerializeField] private Color successColor = new Color(0.3f, 0.9f, 0.4f, 1f);
        [SerializeField] private Color errorColor = new Color(1f, 0.3f, 0.3f, 1f);
        
        private Coroutine _displayCoroutine;
        private RectTransform _rectTransform;
        
        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            
            // Start hidden
            canvasGroup.alpha = 0f;
        }
        
        private void Start()
        {
            ApplySettings();
        }
        
        private void ApplySettings()
        {
            if (settings == null) return;
            
            displayDuration = settings.feedbackDisplayDuration;
            successColor = settings.successColor;
            errorColor = settings.errorColor;
        }
        
        /// <summary>
        /// Show feedback for a command execution
        /// </summary>
        public void ShowFeedback(string command, bool success)
        {
            string displayCommand = FormatCommandName(command);
            string status = success ? "Executed" : "Failed";
            Color color = success ? successColor : errorColor;
            Sprite icon = success ? successIcon : errorIcon;
            
            Show(displayCommand, status, color, icon);
        }
        
        /// <summary>
        /// Show an error message
        /// </summary>
        public void ShowError(string errorMessage)
        {
            Show("Error", errorMessage, errorColor, errorIcon);
        }
        
        /// <summary>
        /// Show custom feedback
        /// </summary>
        public void Show(string command, string status, Color color, Sprite icon = null)
        {
            if (commandText != null)
            {
                commandText.text = command;
                commandText.color = color;
            }
            
            if (statusText != null)
            {
                statusText.text = status;
            }
            
            if (iconImage != null && icon != null)
            {
                iconImage.sprite = icon;
                iconImage.color = color;
                iconImage.gameObject.SetActive(true);
            }
            else if (iconImage != null)
            {
                iconImage.gameObject.SetActive(false);
            }
            
            // Restart display coroutine
            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
            }
            _displayCoroutine = StartCoroutine(DisplayCoroutine());
        }
        
        /// <summary>
        /// Hide the feedback immediately
        /// </summary>
        public void Hide()
        {
            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
                _displayCoroutine = null;
            }
            canvasGroup.alpha = 0f;
        }
        
        private string FormatCommandName(string command)
        {
            if (string.IsNullOrEmpty(command)) return "";
            
            // Convert snake_case to readable format
            // e.g., "weather_radar_increase_range" -> "Weather Radar: Increase Range"
            
            string[] parts = command.Split('_');
            if (parts.Length >= 2)
            {
                string target = CapitalizeWords(parts[0]);
                string action = CapitalizeWords(string.Join(" ", parts, 1, parts.Length - 1));
                return $"{target}: {action}";
            }
            
            return CapitalizeWords(command.Replace("_", " "));
        }
        
        private string CapitalizeWords(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            var chars = text.ToCharArray();
            bool capitalizeNext = true;
            
            for (int i = 0; i < chars.Length; i++)
            {
                if (char.IsWhiteSpace(chars[i]))
                {
                    capitalizeNext = true;
                }
                else if (capitalizeNext)
                {
                    chars[i] = char.ToUpper(chars[i]);
                    capitalizeNext = false;
                }
            }
            
            return new string(chars);
        }
        
        private IEnumerator DisplayCoroutine()
        {
            // Fade in
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
            
            // Hold
            yield return new WaitForSecondsRealtime(displayDuration);
            
            // Fade out
            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
            
            _displayCoroutine = null;
        }
    }
}
