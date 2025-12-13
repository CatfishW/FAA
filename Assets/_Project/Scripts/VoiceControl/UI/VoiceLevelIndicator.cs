using UnityEngine;
using UnityEngine.UI;
using VoiceControl.Manager;

namespace VoiceControl.UI
{
    /// <summary>
    /// Audio level visualization with animated bars.
    /// Responds to microphone input levels during recording.
    /// </summary>
    [AddComponentMenu("Voice Control/UI/Voice Level Indicator")]
    public class VoiceLevelIndicator : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private VoiceControlManager manager;
        [SerializeField] private VoiceControlSettings settings;
        
        [Header("Bars")]
        [SerializeField] private int barCount = 5;
        [SerializeField] private Image[] bars;
        [SerializeField] private RectTransform barsContainer;
        
        [Header("Appearance")]
        [SerializeField] private float barWidth = 8f;
        [SerializeField] private float barSpacing = 4f;
        [SerializeField] private float minBarHeight = 4f;
        [SerializeField] private float maxBarHeight = 40f;
        [SerializeField] private Color activeColor = new Color(0.2f, 0.6f, 1f, 1f);
        [SerializeField] private Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        
        [Header("Animation")]
        [SerializeField] private float smoothSpeed = 10f;
        [SerializeField] private float pulseSpeed = 2f;
        
        private float[] _barHeights;
        private float[] _targetHeights;
        private float _currentLevel;
        private float _pulsePhase;
        private bool _isActive;
        
        private void Awake()
        {
            _barHeights = new float[barCount];
            _targetHeights = new float[barCount];
            
            if (bars == null || bars.Length != barCount)
            {
                CreateBars();
            }
        }
        
        private void Start()
        {
            if (manager == null)
                manager = FindObjectOfType<VoiceControlManager>();
            
            if (manager != null)
            {
                manager.OnAudioLevelUpdate += OnAudioLevelUpdate;
                manager.OnRecordingStateChanged += OnRecordingStateChanged;
            }
            
            ApplySettings();
        }
        
        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.OnAudioLevelUpdate -= OnAudioLevelUpdate;
                manager.OnRecordingStateChanged -= OnRecordingStateChanged;
            }
        }
        
        private void Update()
        {
            UpdateBars();
        }
        
        private void ApplySettings()
        {
            if (settings == null) return;
            
            activeColor = settings.primaryColor;
        }
        
        private void CreateBars()
        {
            if (barsContainer == null)
            {
                GameObject containerObj = new GameObject("BarsContainer");
                containerObj.transform.SetParent(transform, false);
                barsContainer = containerObj.AddComponent<RectTransform>();
                barsContainer.anchoredPosition = Vector2.zero;
            }
            
            // Clear existing
            for (int i = barsContainer.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(barsContainer.GetChild(i).gameObject);
            }
            
            bars = new Image[barCount];
            float totalWidth = barCount * barWidth + (barCount - 1) * barSpacing;
            float startX = -totalWidth / 2f + barWidth / 2f;
            
            for (int i = 0; i < barCount; i++)
            {
                GameObject barObj = new GameObject($"Bar_{i}");
                barObj.transform.SetParent(barsContainer, false);
                
                RectTransform rect = barObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(barWidth, minBarHeight);
                rect.anchoredPosition = new Vector2(startX + i * (barWidth + barSpacing), 0);
                
                Image img = barObj.AddComponent<Image>();
                img.color = inactiveColor;
                
                // Rounded corners via sprite or leave as is
                bars[i] = img;
                _barHeights[i] = minBarHeight;
                _targetHeights[i] = minBarHeight;
            }
        }
        
        private void OnAudioLevelUpdate(float level)
        {
            _currentLevel = level;
            
            // Distribute level across bars with some randomness
            for (int i = 0; i < barCount; i++)
            {
                float barLevel = level * Random.Range(0.6f, 1.4f);
                barLevel = Mathf.Clamp01(barLevel * 5f); // Amplify for visibility
                _targetHeights[i] = Mathf.Lerp(minBarHeight, maxBarHeight, barLevel);
            }
        }
        
        private void OnRecordingStateChanged(bool isRecording)
        {
            _isActive = isRecording;
            
            if (!isRecording)
            {
                // Reset to minimum
                for (int i = 0; i < barCount; i++)
                {
                    _targetHeights[i] = minBarHeight;
                }
            }
        }
        
        private void UpdateBars()
        {
            if (bars == null) return;
            
            _pulsePhase += Time.deltaTime * pulseSpeed;
            
            for (int i = 0; i < barCount && i < bars.Length; i++)
            {
                if (bars[i] == null) continue;
                
                // Smooth height transition
                _barHeights[i] = Mathf.Lerp(_barHeights[i], _targetHeights[i], Time.deltaTime * smoothSpeed);
                
                // Apply pulse when processing
                float height = _barHeights[i];
                if (!_isActive && manager != null && manager.IsProcessing)
                {
                    float pulse = Mathf.Sin(_pulsePhase + i * 0.5f) * 0.5f + 0.5f;
                    height = Mathf.Lerp(minBarHeight, maxBarHeight * 0.6f, pulse);
                }
                
                // Update bar height
                RectTransform rect = bars[i].rectTransform;
                rect.sizeDelta = new Vector2(barWidth, height);
                
                // Update color based on activity
                float heightRatio = (height - minBarHeight) / (maxBarHeight - minBarHeight);
                bars[i].color = Color.Lerp(inactiveColor, activeColor, heightRatio);
            }
        }
    }
}
