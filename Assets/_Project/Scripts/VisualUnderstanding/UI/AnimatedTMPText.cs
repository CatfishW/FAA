using UnityEngine;
using TMPro;

namespace VisualUnderstanding.UI
{
    /// <summary>
    /// High-performance TMP_Text animator.
    /// Uses TMP's built-in features for efficient animations without external dependencies.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Visual Understanding/UI/Animated TMP Text")]
    public class AnimatedTMPText : MonoBehaviour
    {
        [Header("Typewriter Effect")]
        [SerializeField] private bool enableTypewriter = true;
        [SerializeField] private float charactersPerSecond = 80f;
        [SerializeField] private bool useUnscaledTime = true;
        
        [Header("Fade Effect")]
        [SerializeField] private bool fadeInCharacters = true;
        [SerializeField] private float fadeDistance = 5f;
        
        [Header("Wave Effect")]
        [SerializeField] private bool enableWave = false;
        [SerializeField] private float waveAmplitude = 2f;
        [SerializeField] private float waveFrequency = 2f;
        [SerializeField] private float waveSpeed = 3f;
        
        /// <summary>
        /// Event when typewriter completes
        /// </summary>
        public event System.Action OnTypewriterComplete;
        
        private TMP_Text _tmpText;
        private string _fullText;
        private float _charProgress;
        private bool _isAnimating;
        private bool _typewriterComplete;
        
        private int _lastVisibleCount;
        private TMP_MeshInfo[] _cachedMeshInfo;
        
        #region Properties
        
        public TMP_Text TextComponent => _tmpText;
        public bool IsAnimating => _isAnimating;
        public bool TypewriterComplete => _typewriterComplete;
        
        public float CharactersPerSecond
        {
            get => charactersPerSecond;
            set => charactersPerSecond = Mathf.Max(1f, value);
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _tmpText = GetComponent<TMP_Text>();
        }
        
        private void Update()
        {
            if (!_isAnimating) return;
            
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            
            if (enableTypewriter && !_typewriterComplete)
            {
                UpdateTypewriter(deltaTime);
            }
            
            if (fadeInCharacters || enableWave)
            {
                UpdateMeshEffects();
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Set text and start animations
        /// </summary>
        public void SetText(string text, bool animate = true)
        {
            _fullText = text;
            _tmpText.text = text;
            _tmpText.ForceMeshUpdate();
            
            if (animate && enableTypewriter)
            {
                _charProgress = 0f;
                _typewriterComplete = false;
                _tmpText.maxVisibleCharacters = 0;
                _isAnimating = true;
            }
            else
            {
                _tmpText.maxVisibleCharacters = int.MaxValue;
                _typewriterComplete = true;
                _isAnimating = enableWave;
            }
            
            _lastVisibleCount = -1;
        }
        
        /// <summary>
        /// Append text with animation
        /// </summary>
        public void AppendText(string text)
        {
            _fullText += text;
            _tmpText.text = _fullText;
            _tmpText.ForceMeshUpdate();
            _typewriterComplete = false;
            _isAnimating = true;
        }
        
        /// <summary>
        /// Skip to end of current animation
        /// </summary>
        public void SkipToEnd()
        {
            if (_tmpText == null) return;
            
            _tmpText.maxVisibleCharacters = int.MaxValue;
            _charProgress = _tmpText.textInfo.characterCount;
            _typewriterComplete = true;
            _isAnimating = enableWave;
            
            // Reset mesh to original state
            _tmpText.ForceMeshUpdate();
            
            OnTypewriterComplete?.Invoke();
        }
        
        /// <summary>
        /// Clear all text
        /// </summary>
        public void Clear()
        {
            _fullText = "";
            _tmpText.text = "";
            _charProgress = 0f;
            _typewriterComplete = false;
            _isAnimating = false;
        }
        
        /// <summary>
        /// Pause animation
        /// </summary>
        public void Pause()
        {
            _isAnimating = false;
        }
        
        /// <summary>
        /// Resume animation
        /// </summary>
        public void Resume()
        {
            if (!_typewriterComplete || enableWave)
            {
                _isAnimating = true;
            }
        }
        
        #endregion
        
        #region Animation Updates
        
        private void UpdateTypewriter(float deltaTime)
        {
            int totalChars = _tmpText.textInfo.characterCount;
            if (totalChars == 0) return;
            
            _charProgress += charactersPerSecond * deltaTime;
            int visibleChars = Mathf.Min(Mathf.FloorToInt(_charProgress), totalChars);
            
            _tmpText.maxVisibleCharacters = visibleChars;
            
            if (visibleChars >= totalChars && !_typewriterComplete)
            {
                _typewriterComplete = true;
                _isAnimating = enableWave;
                OnTypewriterComplete?.Invoke();
            }
            
            _lastVisibleCount = visibleChars;
        }
        
        private void UpdateMeshEffects()
        {
            _tmpText.ForceMeshUpdate();
            
            TMP_TextInfo textInfo = _tmpText.textInfo;
            if (textInfo.characterCount == 0) return;
            
            int visibleChars = _tmpText.maxVisibleCharacters;
            if (visibleChars == 0) return;
            
            float time = useUnscaledTime ? Time.unscaledTime : Time.time;
            
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;
                
                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;
                
                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
                Color32[] colors = textInfo.meshInfo[materialIndex].colors32;
                
                // Apply fade effect
                if (fadeInCharacters && enableTypewriter && i < visibleChars)
                {
                    float charAge = _charProgress - i;
                    float alpha = Mathf.Clamp01(charAge / fadeDistance);
                    
                    for (int j = 0; j < 4; j++)
                    {
                        Color32 c = colors[vertexIndex + j];
                        colors[vertexIndex + j] = new Color32(c.r, c.g, c.b, (byte)(alpha * 255));
                    }
                }
                
                // Apply wave effect
                if (enableWave)
                {
                    float offset = Mathf.Sin((i * waveFrequency) + (time * waveSpeed)) * waveAmplitude;
                    
                    for (int j = 0; j < 4; j++)
                    {
                        Vector3 v = vertices[vertexIndex + j];
                        vertices[vertexIndex + j] = new Vector3(v.x, v.y + offset, v.z);
                    }
                }
            }
            
            // Apply changes to mesh
            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
                textInfo.meshInfo[i].mesh.colors32 = textInfo.meshInfo[i].colors32;
                _tmpText.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
            }
        }
        
        #endregion
    }
}
