using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

namespace WeatherRadar
{
    /// <summary>
    /// Animated radar control button with script-based hover, press, and pulse effects.
    /// No Animator required - all animations driven by code.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class RadarButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.15f, 0.25f, 0.15f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.25f, 0.45f, 0.25f, 1f);
        [SerializeField] private Color pressedColor = new Color(0.1f, 0.2f, 0.1f, 1f);
        [SerializeField] private Color activeColor = new Color(0.2f, 0.6f, 0.3f, 1f);
        [SerializeField] private Color textNormalColor = new Color(0.7f, 0.9f, 0.7f, 1f);
        [SerializeField] private Color textActiveColor = new Color(0.9f, 1f, 0.9f, 1f);

        [Header("Animation Settings")]
        [SerializeField] private float hoverDuration = 0.15f;
        [SerializeField] private float pressDuration = 0.08f;
        [SerializeField] private float scalePressAmount = 0.95f;
        [SerializeField] private float glowPulseSpeed = 2f;
        [SerializeField] private float glowPulseAmount = 0.1f;

        [Header("Glow Effect")]
        [SerializeField] private bool enableGlow = true;
        [SerializeField] private Image glowImage;
        [SerializeField] private Color glowColor = new Color(0.3f, 1f, 0.5f, 0.3f);

        [Header("State")]
        [SerializeField] private bool isToggle = false;
        [SerializeField] private bool isActive = false;

        [Header("References")]
        [SerializeField] private TMP_Text buttonText;
        [SerializeField] private Image buttonImage;

        // Events
        public event Action OnClick;
        public event Action<bool> OnToggled;

        private RectTransform rectTransform;
        private Vector3 originalScale;
        private Color currentTargetColor;
        private Color currentColor;
        private float animationProgress = 1f;
        private bool isHovered = false;
        private bool isPressed = false;
        private float glowPhase = 0f;

        public bool IsActive
        {
            get => isActive;
            set => SetActive(value);
        }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            originalScale = rectTransform.localScale;

            if (buttonImage == null)
            {
                buttonImage = GetComponent<Image>();
            }

            if (buttonText == null)
            {
                buttonText = GetComponentInChildren<TMP_Text>();
            }

            currentColor = normalColor;
            currentTargetColor = normalColor;

            if (buttonImage != null)
            {
                buttonImage.color = currentColor;
            }

            // Create glow image if enabled and not assigned
            if (enableGlow && glowImage == null)
            {
                CreateGlowImage();
            }
        }

        private void CreateGlowImage()
        {
            GameObject glowObj = new GameObject("Glow");
            glowObj.transform.SetParent(transform, false);
            glowObj.transform.SetAsFirstSibling();

            RectTransform glowRect = glowObj.AddComponent<RectTransform>();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-4, -4);
            glowRect.offsetMax = new Vector2(4, 4);

            glowImage = glowObj.AddComponent<Image>();
            glowImage.color = Color.clear;
        }

        private void Update()
        {
            // Animate color transition
            if (animationProgress < 1f)
            {
                float duration = isPressed ? pressDuration : hoverDuration;
                animationProgress += Time.deltaTime / duration;
                animationProgress = Mathf.Clamp01(animationProgress);

                float t = EaseOutQuad(animationProgress);
                currentColor = Color.Lerp(currentColor, currentTargetColor, t);

                if (buttonImage != null)
                {
                    buttonImage.color = currentColor;
                }
            }

            // Animate scale
            float targetScale = isPressed ? scalePressAmount : 1f;
            float currentScale = rectTransform.localScale.x / originalScale.x;
            if (!Mathf.Approximately(currentScale, targetScale))
            {
                float newScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime / pressDuration * 2f);
                rectTransform.localScale = originalScale * newScale;
            }

            // Animate glow pulse when active or hovered
            if (enableGlow && glowImage != null && (isActive || isHovered))
            {
                glowPhase += Time.deltaTime * glowPulseSpeed;
                float pulse = Mathf.Sin(glowPhase) * 0.5f + 0.5f;
                float alpha = (glowColor.a + pulse * glowPulseAmount);

                if (isActive)
                {
                    alpha *= 1.5f;
                }

                Color glow = glowColor;
                glow.a = alpha;
                glowImage.color = glow;
            }
            else if (glowImage != null)
            {
                // Fade out glow
                Color glow = glowImage.color;
                glow.a = Mathf.Lerp(glow.a, 0f, Time.deltaTime * 5f);
                glowImage.color = glow;
            }
        }

        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            UpdateVisualState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
            UpdateVisualState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
            UpdateVisualState();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            UpdateVisualState();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (isToggle)
            {
                isActive = !isActive;
                OnToggled?.Invoke(isActive);
            }

            OnClick?.Invoke();

            // Quick pulse effect on click
            StartCoroutine(ClickPulse());
        }

        private System.Collections.IEnumerator ClickPulse()
        {
            Vector3 targetScale = originalScale * 1.1f;
            float duration = 0.1f;
            float elapsed = 0f;

            // Scale up
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutQuad(elapsed / duration);
                rectTransform.localScale = Vector3.Lerp(originalScale, targetScale, t);
                yield return null;
            }

            // Scale back
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutQuad(elapsed / duration);
                rectTransform.localScale = Vector3.Lerp(targetScale, originalScale, t);
                yield return null;
            }

            rectTransform.localScale = originalScale;
        }

        private void UpdateVisualState()
        {
            Color targetColor;

            if (isPressed)
            {
                targetColor = pressedColor;
            }
            else if (isActive)
            {
                targetColor = isHovered ? Color.Lerp(activeColor, hoverColor, 0.3f) : activeColor;
            }
            else if (isHovered)
            {
                targetColor = hoverColor;
            }
            else
            {
                targetColor = normalColor;
            }

            currentTargetColor = targetColor;
            animationProgress = 0f;

            // Update text color
            if (buttonText != null)
            {
                buttonText.color = isActive ? textActiveColor : textNormalColor;
            }
        }

        /// <summary>
        /// Set active state (for toggle buttons)
        /// </summary>
        public void SetActive(bool active)
        {
            isActive = active;
            UpdateVisualState();
        }

        /// <summary>
        /// Set button colors
        /// </summary>
        public void SetColors(Color normal, Color hover, Color pressed)
        {
            normalColor = normal;
            hoverColor = hover;
            pressedColor = pressed;
            UpdateVisualState();
        }

        /// <summary>
        /// Set button text
        /// </summary>
        public void SetText(string text)
        {
            if (buttonText != null)
            {
                buttonText.text = text;
            }
        }

        /// <summary>
        /// Play a highlight animation
        /// </summary>
        public void PlayHighlight()
        {
            StartCoroutine(HighlightAnimation());
        }

        private System.Collections.IEnumerator HighlightAnimation()
        {
            if (glowImage == null) yield break;

            Color startColor = glowImage.color;
            Color peakColor = glowColor;
            peakColor.a = 0.8f;

            float duration = 0.3f;
            float elapsed = 0f;

            // Glow up
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutQuad(elapsed / duration);
                glowImage.color = Color.Lerp(startColor, peakColor, t);
                yield return null;
            }

            // Glow down
            elapsed = 0f;
            duration = 0.5f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutQuad(elapsed / duration);
                glowImage.color = Color.Lerp(peakColor, startColor, t);
                yield return null;
            }
        }
    }
}
