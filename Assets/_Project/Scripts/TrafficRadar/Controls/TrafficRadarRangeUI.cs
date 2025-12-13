using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using TrafficRadar.Core;

namespace TrafficRadar.Controls
{
    /// <summary>
    /// Runtime UI for adjusting Traffic Radar range with script-based animations.
    /// Creates a compact segmented button bar that can be shown during play mode.
    /// </summary>
    public class TrafficRadarRangeUI : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Controller Reference")]
        [SerializeField] private TrafficRadarController radarController;

        [Header("UI Container")]
        [Tooltip("Parent container for the range buttons. If null, will be created.")]
        [SerializeField] private RectTransform buttonContainer;

        [Header("Appearance")]
        [SerializeField] private Color normalColor = new Color(0.12f, 0.18f, 0.25f, 0.95f);
        [SerializeField] private Color hoverColor = new Color(0.18f, 0.28f, 0.38f, 1f);
        [SerializeField] private Color pressedColor = new Color(0.08f, 0.12f, 0.18f, 1f);
        [SerializeField] private Color activeColor = new Color(0.15f, 0.45f, 0.65f, 1f);
        [SerializeField] private Color glowColor = new Color(0.3f, 0.7f, 1f, 0.4f);
        [SerializeField] private Color textNormalColor = new Color(0.7f, 0.85f, 0.95f, 1f);
        [SerializeField] private Color textActiveColor = new Color(0.95f, 1f, 1f, 1f);

        [Header("Layout")]
        [SerializeField] private float buttonWidth = 55f;
        [SerializeField] private float buttonHeight = 36f;
        [SerializeField] private float buttonSpacing = 4f;
        [SerializeField] private float containerPadding = 8f;

        [Header("Animation")]
        [SerializeField] private float hoverDuration = 0.12f;
        [SerializeField] private float pressDuration = 0.06f;
        [SerializeField] private float glowPulseSpeed = 2.5f;
        [SerializeField] private float glowPulseAmount = 0.15f;
        [SerializeField] private float scalePressAmount = 0.92f;

        [Header("Visibility")]
        [SerializeField] private bool startVisible = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.R;

        #endregion

        #region Private Fields

        private List<RangeButtonState> buttonStates = new List<RangeButtonState>();
        private CanvasGroup canvasGroup;
        private bool isVisible;
        private float panelAnimProgress = 1f;
        private bool panelTargetVisible;
        private float glowPhase;

        private class RangeButtonState
        {
            public RectTransform rectTransform;
            public Image backgroundImage;
            public Image glowImage;
            public TMP_Text label;
            public float rangeValue;
            public bool isHovered;
            public bool isPressed;
            public bool isActive;
            public Color currentColor;
            public Color targetColor;
            public float animProgress;
            public Vector3 originalScale;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Auto-find radar controller if not assigned
            if (radarController == null)
            {
                radarController = FindObjectOfType<TrafficRadarController>();
            }

            // Setup canvas group for panel animations
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            isVisible = startVisible;
            panelTargetVisible = startVisible;
            panelAnimProgress = startVisible ? 1f : 0f;
            canvasGroup.alpha = startVisible ? 1f : 0f;
        }

        private void Start()
        {
            CreateUI();
            UpdateActiveButton();
        }

        private void Update()
        {
            // Toggle visibility with hotkey
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleVisibility();
            }

            // Animate panel visibility
            AnimatePanelVisibility();

            // Update button animations
            AnimateButtons();

            // Update glow phase
            glowPhase += Time.deltaTime * glowPulseSpeed;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Toggle panel visibility with animation
        /// </summary>
        public void ToggleVisibility()
        {
            SetVisibility(!isVisible);
        }

        /// <summary>
        /// Set panel visibility with animation
        /// </summary>
        public void SetVisibility(bool visible)
        {
            isVisible = visible;
            panelTargetVisible = visible;
        }

        /// <summary>
        /// Force refresh of buttons (call when range options change)
        /// </summary>
        public void RefreshButtons()
        {
            ClearButtons();
            CreateUI();
            UpdateActiveButton();
        }

        #endregion

        #region UI Creation

        private void CreateUI()
        {
            if (radarController == null)
            {
                Debug.LogWarning("[TrafficRadarRangeUI] No radar controller found!");
                return;
            }

            // Get range options from controller via reflection or public property
            float[] rangeOptions = GetRangeOptions();
            if (rangeOptions == null || rangeOptions.Length == 0)
            {
                Debug.LogWarning("[TrafficRadarRangeUI] No range options available!");
                return;
            }

            // Create container if needed
            if (buttonContainer == null)
            {
                CreateContainer();
            }

            // Calculate container size
            float totalWidth = (buttonWidth * rangeOptions.Length) + (buttonSpacing * (rangeOptions.Length - 1)) + (containerPadding * 2);
            float totalHeight = buttonHeight + (containerPadding * 2);
            buttonContainer.sizeDelta = new Vector2(totalWidth, totalHeight);

            // Create buttons for each range option
            for (int i = 0; i < rangeOptions.Length; i++)
            {
                CreateRangeButton(rangeOptions[i], i);
            }
        }

        private void CreateContainer()
        {
            // Create container object
            GameObject containerObj = new GameObject("RangeButtonContainer");
            containerObj.transform.SetParent(transform, false);

            buttonContainer = containerObj.AddComponent<RectTransform>();
            buttonContainer.anchorMin = new Vector2(0.5f, 0);
            buttonContainer.anchorMax = new Vector2(0.5f, 0);
            buttonContainer.pivot = new Vector2(0.5f, 0);
            buttonContainer.anchoredPosition = new Vector2(0, 10);

            // Add background
            Image bgImage = containerObj.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.08f, 0.12f, 0.9f);

            // Add rounded corners via sprite if available, otherwise solid color
        }

        private void CreateRangeButton(float rangeValue, int index)
        {
            // Create button object
            GameObject buttonObj = new GameObject($"RangeBtn_{rangeValue}");
            buttonObj.transform.SetParent(buttonContainer, false);

            RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
            float xPos = containerPadding + (index * (buttonWidth + buttonSpacing)) + (buttonWidth / 2) - (buttonContainer.sizeDelta.x / 2);
            rectTransform.anchoredPosition = new Vector2(xPos, 0);
            rectTransform.sizeDelta = new Vector2(buttonWidth, buttonHeight);

            // Add glow image (behind button)
            GameObject glowObj = new GameObject("Glow");
            glowObj.transform.SetParent(buttonObj.transform, false);
            RectTransform glowRect = glowObj.AddComponent<RectTransform>();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-4, -4);
            glowRect.offsetMax = new Vector2(4, 4);
            Image glowImage = glowObj.AddComponent<Image>();
            glowImage.color = Color.clear;

            // Add background image
            Image bgImage = buttonObj.AddComponent<Image>();
            bgImage.color = normalColor;
            bgImage.raycastTarget = true;

            // Add text
            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(buttonObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TMP_Text label = textObj.AddComponent<TextMeshProUGUI>();
            label.text = $"{rangeValue:0}";
            label.fontSize = 14;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = textNormalColor;

            // Add event trigger
            EventTrigger trigger = buttonObj.AddComponent<EventTrigger>();
            
            // Pointer Enter
            EventTrigger.Entry enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            int capturedIndex = index;
            enterEntry.callback.AddListener((data) => OnButtonPointerEnter(capturedIndex));
            trigger.triggers.Add(enterEntry);

            // Pointer Exit
            EventTrigger.Entry exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((data) => OnButtonPointerExit(capturedIndex));
            trigger.triggers.Add(exitEntry);

            // Pointer Down
            EventTrigger.Entry downEntry = new EventTrigger.Entry();
            downEntry.eventID = EventTriggerType.PointerDown;
            downEntry.callback.AddListener((data) => OnButtonPointerDown(capturedIndex));
            trigger.triggers.Add(downEntry);

            // Pointer Up
            EventTrigger.Entry upEntry = new EventTrigger.Entry();
            upEntry.eventID = EventTriggerType.PointerUp;
            upEntry.callback.AddListener((data) => OnButtonPointerUp(capturedIndex));
            trigger.triggers.Add(upEntry);

            // Pointer Click
            EventTrigger.Entry clickEntry = new EventTrigger.Entry();
            clickEntry.eventID = EventTriggerType.PointerClick;
            clickEntry.callback.AddListener((data) => OnButtonClick(capturedIndex));
            trigger.triggers.Add(clickEntry);

            // Store button state
            RangeButtonState state = new RangeButtonState
            {
                rectTransform = rectTransform,
                backgroundImage = bgImage,
                glowImage = glowImage,
                label = label,
                rangeValue = rangeValue,
                isHovered = false,
                isPressed = false,
                isActive = false,
                currentColor = normalColor,
                targetColor = normalColor,
                animProgress = 1f,
                originalScale = Vector3.one
            };
            buttonStates.Add(state);
        }

        private void ClearButtons()
        {
            foreach (var state in buttonStates)
            {
                if (state.rectTransform != null)
                {
                    Destroy(state.rectTransform.gameObject);
                }
            }
            buttonStates.Clear();
        }

        private float[] GetRangeOptions()
        {
            if (radarController == null) return new float[] { 10, 20, 40, 80, 150 };

            // Try to get range options via reflection
            var field = typeof(TrafficRadarController).GetField("rangeOptionsNM", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(radarController) as float[];
            }

            // Default fallback
            return new float[] { 10, 20, 40, 80, 150 };
        }

        #endregion

        #region Button Event Handlers

        private void OnButtonPointerEnter(int index)
        {
            if (index < buttonStates.Count)
            {
                buttonStates[index].isHovered = true;
                UpdateButtonTarget(index);
            }
        }

        private void OnButtonPointerExit(int index)
        {
            if (index < buttonStates.Count)
            {
                buttonStates[index].isHovered = false;
                buttonStates[index].isPressed = false;
                UpdateButtonTarget(index);
            }
        }

        private void OnButtonPointerDown(int index)
        {
            if (index < buttonStates.Count)
            {
                buttonStates[index].isPressed = true;
                UpdateButtonTarget(index);
            }
        }

        private void OnButtonPointerUp(int index)
        {
            if (index < buttonStates.Count)
            {
                buttonStates[index].isPressed = false;
                UpdateButtonTarget(index);
            }
        }

        private void OnButtonClick(int index)
        {
            if (index < buttonStates.Count && radarController != null)
            {
                float newRange = buttonStates[index].rangeValue;
                radarController.RangeNM = newRange;
                UpdateActiveButton();

                // Trigger click pulse animation
                StartCoroutine(ClickPulseAnimation(index));
            }
        }

        private void UpdateButtonTarget(int index)
        {
            var state = buttonStates[index];
            
            if (state.isPressed)
            {
                state.targetColor = pressedColor;
            }
            else if (state.isActive)
            {
                state.targetColor = state.isHovered ? Color.Lerp(activeColor, hoverColor, 0.3f) : activeColor;
            }
            else if (state.isHovered)
            {
                state.targetColor = hoverColor;
            }
            else
            {
                state.targetColor = normalColor;
            }

            state.animProgress = 0f;
        }

        private void UpdateActiveButton()
        {
            if (radarController == null) return;

            float currentRange = radarController.RangeNM;

            for (int i = 0; i < buttonStates.Count; i++)
            {
                bool wasActive = buttonStates[i].isActive;
                buttonStates[i].isActive = Mathf.Approximately(buttonStates[i].rangeValue, currentRange);

                if (wasActive != buttonStates[i].isActive)
                {
                    UpdateButtonTarget(i);
                }

                // Update text color
                if (buttonStates[i].label != null)
                {
                    buttonStates[i].label.color = buttonStates[i].isActive ? textActiveColor : textNormalColor;
                }
            }
        }

        #endregion

        #region Animations

        private void AnimatePanelVisibility()
        {
            float targetAlpha = panelTargetVisible ? 1f : 0f;
            
            if (!Mathf.Approximately(canvasGroup.alpha, targetAlpha))
            {
                canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * 8f);
                
                if (Mathf.Abs(canvasGroup.alpha - targetAlpha) < 0.01f)
                {
                    canvasGroup.alpha = targetAlpha;
                }
            }

            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;
        }

        private void AnimateButtons()
        {
            float pulse = Mathf.Sin(glowPhase) * 0.5f + 0.5f;

            foreach (var state in buttonStates)
            {
                // Animate color transition
                if (state.animProgress < 1f)
                {
                    float duration = state.isPressed ? pressDuration : hoverDuration;
                    state.animProgress += Time.deltaTime / duration;
                    state.animProgress = Mathf.Clamp01(state.animProgress);

                    float t = EaseOutQuad(state.animProgress);
                    state.currentColor = Color.Lerp(state.currentColor, state.targetColor, t);

                    if (state.backgroundImage != null)
                    {
                        state.backgroundImage.color = state.currentColor;
                    }
                }

                // Animate scale
                float targetScale = state.isPressed ? scalePressAmount : 1f;
                if (state.rectTransform != null)
                {
                    float currentScale = state.rectTransform.localScale.x;
                    if (!Mathf.Approximately(currentScale, targetScale))
                    {
                        float newScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime / pressDuration * 2f);
                        state.rectTransform.localScale = Vector3.one * newScale;
                    }
                }

                // Animate glow for active/hovered buttons
                if (state.glowImage != null)
                {
                    if (state.isActive || state.isHovered)
                    {
                        float alpha = glowColor.a + pulse * glowPulseAmount;
                        if (state.isActive) alpha *= 1.5f;

                        Color glow = glowColor;
                        glow.a = alpha;
                        state.glowImage.color = glow;
                    }
                    else
                    {
                        // Fade out glow
                        Color glow = state.glowImage.color;
                        glow.a = Mathf.Lerp(glow.a, 0f, Time.deltaTime * 5f);
                        state.glowImage.color = glow;
                    }
                }
            }
        }

        private System.Collections.IEnumerator ClickPulseAnimation(int index)
        {
            if (index >= buttonStates.Count) yield break;

            var state = buttonStates[index];
            Vector3 targetScale = Vector3.one * 1.1f;
            float duration = 0.08f;
            float elapsed = 0f;

            // Scale up
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutQuad(elapsed / duration);
                state.rectTransform.localScale = Vector3.Lerp(Vector3.one, targetScale, t);
                yield return null;
            }

            // Scale back
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutQuad(elapsed / duration);
                state.rectTransform.localScale = Vector3.Lerp(targetScale, Vector3.one, t);
                yield return null;
            }

            state.rectTransform.localScale = Vector3.one;
        }

        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        #endregion
    }
}
