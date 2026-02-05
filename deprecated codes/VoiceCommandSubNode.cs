using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VoiceControl.UI
{
    /// <summary>
    /// Represents a sub-node that expands from a radial segment when selected.
    /// </summary>
    public class VoiceCommandSubNode : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private Image background;
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.22f, 0.26f, 0.30f, 0.95f);
        [SerializeField] private Color highlightColor = new Color(0.55f, 0.90f, 0.65f, 0.98f);

        [Header("Animation")]
        [SerializeField] private float hoverScale = 1.15f;
        [SerializeField] private float animationSpeed = 10f;

        public event Action<VoiceCommandSubNode> OnSelected;

        private Vector3 targetScale = Vector3.zero;
        private float targetAlpha = 0f;
        private bool isInitialized = false;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (button != null)
            {
                button.onClick.AddListener(OnClicked);
            }

            // Add hover effects
            var trigger = gameObject.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry();
            enter.eventID = EventTriggerType.PointerEnter;
            enter.callback.AddListener((e) => SetHovered(true));
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry();
            exit.eventID = EventTriggerType.PointerExit;
            exit.callback.AddListener((e) => SetHovered(false));
            trigger.triggers.Add(exit);
        }

        private void Update()
        {
            // Smooth scale animation
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * animationSpeed);

            // Smooth alpha animation
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * animationSpeed);
            }
        }

        /// <summary>
        /// Initialize the sub-node with required components (called by VoiceCommandWheelCore)
        /// </summary>
        public void Initialize(Image bg, Button btn, CanvasGroup cg, TextMeshProUGUI label, Color normalCol, Color highlightCol)
        {
            background = bg;
            button = btn;
            canvasGroup = cg;
            labelText = label;
            normalColor = normalCol;
            highlightColor = highlightCol;
            isInitialized = true;
        }

        /// <summary>
        /// Legacy initialization for backward compatibility
        /// </summary>
        public void Initialize(VoiceCommandWheelSegment.CommandData cmd, VoiceCommandWheelSegment parent)
        {
            if (labelText != null)
            {
                labelText.text = cmd.displayName ?? "";
            }

            if (cmd.accentColor != default && background != null)
            {
                background.color = cmd.accentColor;
            }
            isInitialized = true;
        }

        /// <summary>
        /// Set the visual state for animation (scale and alpha)
        /// </summary>
        public void SetVisuals(float normalizedValue)
        {
            targetScale = Vector3.one * normalizedValue;
            targetAlpha = normalizedValue;
        }

        public void SetScale(Vector3 scale)
        {
            targetScale = scale;
        }

        public void SetAlpha(float alpha)
        {
            targetAlpha = alpha;
        }

        private void SetHovered(bool hovered)
        {
            if (!isInitialized) return;

            if (hovered)
            {
                targetScale = Vector3.one * hoverScale;
                if (background != null)
                    background.color = highlightColor;
            }
            else
            {
                targetScale = Vector3.one;
                if (background != null)
                    background.color = normalColor;
            }
        }

        private void OnClicked()
        {
            OnSelected?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClicked);
            }
        }
    }
}
