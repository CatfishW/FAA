using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace VoiceControl.UI
{
    /// <summary>
    /// Individual segment for the voice command wheel.
    /// </summary>
    public class VoiceCommandSegment : MonoBehaviour
    {
        [Header("Visual Components")]
        public Image backgroundImage;
        public Button button;
        public TextMeshProUGUI labelText;
        public CanvasGroup canvasGroup;

        [Header("Content")]
        public string displayLabel = "Command";
        public string subLabel = "System";

        [Header("Visual Settings")]
        public Color normalColor = new Color(0.25f, 0.28f, 0.32f, 0.95f);
        public Color highlightColor = new Color(0.45f, 0.85f, 0.55f, 0.98f);
        public float highlightScale = 1.1f;

        // Internal state
        [HideInInspector] public VoiceCommandWheelCore parentWheel;
        [HideInInspector] public int assignedIndex = 0;
        [HideInInspector] public bool isActive = false;

        private Vector3 originalScale;

        void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            originalScale = transform.localScale;

            // Setup button click
            if (button != null)
            {
                button.onClick.AddListener(OnClick);
            }
        }

        void Start()
        {
            // Set initial label
            if (labelText != null)
                labelText.text = displayLabel;

            // Set initial color
            if (backgroundImage != null)
                backgroundImage.color = normalColor;
        }

        public void Highlight()
        {
            if (isActive) return;

            isActive = true;

            if (backgroundImage != null)
                backgroundImage.color = highlightColor;

            transform.localScale = originalScale * highlightScale;
        }

        public void UnHighlight()
        {
            if (!isActive) return;

            isActive = false;

            if (backgroundImage != null)
                backgroundImage.color = normalColor;

            transform.localScale = originalScale;
        }

        void OnClick()
        {
            Debug.Log($"[VoiceCommandSegment] Segment {assignedIndex} clicked");

            if (parentWheel != null)
            {
                parentWheel.OnSegmentClicked(assignedIndex);
            }
        }

        void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OnClick);
        }
    }
}
