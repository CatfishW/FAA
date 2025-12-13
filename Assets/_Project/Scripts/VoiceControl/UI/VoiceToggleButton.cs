using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using VoiceControl.Manager;

namespace VoiceControl.UI
{
    /// <summary>
    /// Toggle button for voice control UI and push-to-talk.
    /// Features hover/press animations and state indication.
    /// </summary>
    [AddComponentMenu("Voice Control/UI/Voice Toggle Button")]
    public class VoiceToggleButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public enum ButtonMode
        {
            ToggleUI,       // Toggles VoiceControlUI visibility
            PushToTalk,     // Hold to record
            ToggleRecording // Click to toggle recording
        }
        
        [Header("Mode")]
        [SerializeField] private ButtonMode mode = ButtonMode.PushToTalk;
        
        [Header("Components")]
        [SerializeField] private VoiceControlManager manager;
        [SerializeField] private VoiceControlUI voiceUI;
        [SerializeField] private VoiceControlSettings settings;
        
        [Header("UI References")]
        [SerializeField] private Image buttonImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private RectTransform buttonTransform;
        
        [Header("Icons")]
        [SerializeField] private Sprite microphoneIcon;
        [SerializeField] private Sprite microphoneActiveIcon;
        [SerializeField] private Sprite panelIcon;
        
        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);
        [SerializeField] private Color hoverColor = new Color(0.2f, 0.2f, 0.25f, 0.95f);
        [SerializeField] private Color pressedColor = new Color(0.25f, 0.25f, 0.3f, 1f);
        [SerializeField] private Color activeColor = new Color(0.3f, 0.5f, 0.9f, 1f);
        [SerializeField] private Color recordingColor = new Color(0.9f, 0.3f, 0.3f, 1f);
        
        [Header("Animation")]
        [SerializeField] private float animationSpeed = 10f;
        [SerializeField] private float scaleOnHover = 1.05f;
        [SerializeField] private float scaleOnPress = 0.95f;
        
        private bool _isHovered;
        private bool _isPressed;
        private bool _isActive;
        private bool _isRecording;
        
        private Color _targetColor;
        private float _targetScale;
        private float _currentScale = 1f;
        
        private void Awake()
        {
            if (buttonTransform == null)
                buttonTransform = GetComponent<RectTransform>();
            
            if (buttonImage == null)
                buttonImage = GetComponent<Image>();
            
            _targetColor = normalColor;
            _targetScale = 1f;
        }
        
        private void Start()
        {
            AutoFindComponents();
            ApplySettings();
            SubscribeToEvents();
            UpdateIcon();
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        private void Update()
        {
            // Animate color
            if (buttonImage != null)
            {
                buttonImage.color = Color.Lerp(buttonImage.color, _targetColor, Time.unscaledDeltaTime * animationSpeed);
            }
            
            // Animate scale
            _currentScale = Mathf.Lerp(_currentScale, _targetScale, Time.unscaledDeltaTime * animationSpeed);
            buttonTransform.localScale = Vector3.one * _currentScale;
        }
        
        private void AutoFindComponents()
        {
            if (manager == null)
                manager = FindObjectOfType<VoiceControlManager>();
            
            if (voiceUI == null)
                voiceUI = FindObjectOfType<VoiceControlUI>();
        }
        
        private void ApplySettings()
        {
            if (settings == null) return;
            
            activeColor = settings.primaryColor;
            recordingColor = settings.recordingColor;
        }
        
        private void SubscribeToEvents()
        {
            if (manager == null) return;
            
            manager.OnRecordingStateChanged += OnRecordingStateChanged;
        }
        
        private void UnsubscribeFromEvents()
        {
            if (manager == null) return;
            
            manager.OnRecordingStateChanged -= OnRecordingStateChanged;
        }
        
        private void OnRecordingStateChanged(bool isRecording)
        {
            _isRecording = isRecording;
            UpdateVisuals();
            UpdateIcon();
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            UpdateVisuals();
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            
            // If push-to-talk and we exit while pressed, stop recording
            if (mode == ButtonMode.PushToTalk && _isPressed)
            {
                _isPressed = false;
                if (manager != null && manager.IsRecording)
                {
                    manager.StopRecording(true);
                }
            }
            
            UpdateVisuals();
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            
            switch (mode)
            {
                case ButtonMode.ToggleUI:
                    if (voiceUI != null)
                    {
                        voiceUI.Toggle();
                        _isActive = voiceUI.IsVisible;
                    }
                    break;
                    
                case ButtonMode.PushToTalk:
                    if (manager != null && !manager.IsRecording && !manager.IsProcessing)
                    {
                        manager.StartRecording();
                    }
                    break;
                    
                case ButtonMode.ToggleRecording:
                    if (manager != null)
                    {
                        manager.ToggleRecording();
                    }
                    break;
            }
            
            UpdateVisuals();
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            
            if (mode == ButtonMode.PushToTalk)
            {
                if (manager != null && manager.IsRecording)
                {
                    manager.StopRecording(true);
                }
            }
            
            UpdateVisuals();
        }
        
        private void UpdateVisuals()
        {
            // Determine target color
            if (_isRecording)
            {
                _targetColor = recordingColor;
            }
            else if (_isPressed)
            {
                _targetColor = pressedColor;
            }
            else if (_isHovered)
            {
                _targetColor = hoverColor;
            }
            else if (_isActive)
            {
                _targetColor = activeColor;
            }
            else
            {
                _targetColor = normalColor;
            }
            
            // Determine target scale
            if (_isPressed)
            {
                _targetScale = scaleOnPress;
            }
            else if (_isHovered)
            {
                _targetScale = scaleOnHover;
            }
            else
            {
                _targetScale = 1f;
            }
        }
        
        private void UpdateIcon()
        {
            if (iconImage == null) return;
            
            switch (mode)
            {
                case ButtonMode.ToggleUI:
                    if (panelIcon != null)
                        iconImage.sprite = panelIcon;
                    break;
                    
                case ButtonMode.PushToTalk:
                case ButtonMode.ToggleRecording:
                    if (_isRecording && microphoneActiveIcon != null)
                        iconImage.sprite = microphoneActiveIcon;
                    else if (microphoneIcon != null)
                        iconImage.sprite = microphoneIcon;
                    break;
            }
        }
    }
}
