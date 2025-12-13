using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using TrafficRadar.Core;

namespace TrafficRadar.Controls
{
    /// <summary>
    /// Runtime UI for adjusting Traffic Radar fetch radius (in NM).
    /// Features script-based animations for high performance.
    /// Positioned on top of the traffic radar display.
    /// </summary>
    public class TrafficRadarFilterUI : MonoBehaviour
    {
        #region Inspector Fields

        [Header("References")]
        [SerializeField] private TrafficRadarController radarController;
        [SerializeField] private TrafficRadarDataManager dataManager;

        [Header("UI Container")]
        [Tooltip("Parent container for the filter UI. If null, will be created.")]
        [SerializeField] private RectTransform container;

        [Header("Appearance")]
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.12f, 0.18f, 0.9f);
        [SerializeField] private Color sliderTrackColor = new Color(0.15f, 0.22f, 0.30f, 1f);
        [SerializeField] private Color sliderFillColor = new Color(0.2f, 0.5f, 0.8f, 1f);
        [SerializeField] private Color sliderHandleColor = new Color(0.4f, 0.75f, 1f, 1f);
        [SerializeField] private Color labelColor = new Color(0.7f, 0.85f, 0.95f, 1f);
        [SerializeField] private Color valueColor = new Color(0.95f, 1f, 1f, 1f);

        [Header("Filter Settings")]
        [SerializeField] private float minRadiusNM = 10f;
        [SerializeField] private float maxRadiusNM = 250f;
        [SerializeField] private float[] presetValues = { 25f, 50f, 100f, 150f, 200f };

        [Header("Animation")]
        [SerializeField] private float animationSpeed = 8f;
        [SerializeField] private float pulseSpeed = 2f;

        [Header("Visibility")]
        [SerializeField] private bool startVisible = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F;

        #endregion

        #region Private Fields

        private CanvasGroup canvasGroup;
        private bool isVisible;
        private float targetAlpha;

        // UI Elements
        private Image backgroundImage;
        private Image sliderTrack;
        private Image sliderFill;
        private Image sliderHandle;
        private TMP_Text titleLabel;
        private TMP_Text valueLabel;
        private TMP_InputField inputField;

        // Animation state
        private float currentValue;
        private float targetValue;
        private float handleAnimPhase;
        private bool isDragging;
        private RectTransform handleRect;
        private RectTransform fillRect;
        private RectTransform trackRect;

        // Preset buttons
        private Image[] presetButtons;
        private bool[] presetHovered;
        private float[] presetAnimProgress;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Auto-find references
            if (radarController == null)
                radarController = FindObjectOfType<TrafficRadarController>();
            if (dataManager == null)
                dataManager = FindObjectOfType<TrafficRadarDataManager>();

            // Setup canvas group
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            isVisible = startVisible;
            targetAlpha = startVisible ? 1f : 0f;
            canvasGroup.alpha = targetAlpha;
        }

        private void Start()
        {
            CreateUI();
            
            // Initialize value from data manager
            if (dataManager != null)
            {
                currentValue = dataManager.RadiusFilterNM;
                targetValue = currentValue;
                UpdateVisuals();
            }
        }

        private void Update()
        {
            // Toggle visibility
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleVisibility();
            }

            // Animate visibility
            AnimateVisibility();

            // Animate value changes
            AnimateValue();

            // Animate handle pulse when dragging
            if (isDragging)
            {
                handleAnimPhase += Time.deltaTime * pulseSpeed * Mathf.PI * 2f;
            }

            // Animate preset buttons
            AnimatePresetButtons();
        }

        #endregion

        #region Public Methods

        public void ToggleVisibility()
        {
            SetVisibility(!isVisible);
        }

        public void SetVisibility(bool visible)
        {
            isVisible = visible;
            targetAlpha = visible ? 1f : 0f;
        }

        public void SetRadius(float radiusNM)
        {
            targetValue = Mathf.Clamp(radiusNM, minRadiusNM, maxRadiusNM);
        }

        #endregion

        #region UI Creation

        private void CreateUI()
        {
            // Create container if needed
            if (container == null)
            {
                CreateContainer();
            }

            // Create background
            backgroundImage = container.gameObject.AddComponent<Image>();
            backgroundImage.color = backgroundColor;
            backgroundImage.raycastTarget = true;

            // Create title label
            CreateTitleLabel();

            // Create slider
            CreateSlider();

            // Create value display
            CreateValueDisplay();

            // Create preset buttons
            CreatePresetButtons();
        }

        private void CreateContainer()
        {
            GameObject containerObj = new GameObject("FilterContainer");
            containerObj.transform.SetParent(transform, false);

            container = containerObj.AddComponent<RectTransform>();
            // Position at top of parent (radar display)
            container.anchorMin = new Vector2(0, 1);
            container.anchorMax = new Vector2(1, 1);
            container.pivot = new Vector2(0.5f, 1);
            container.anchoredPosition = new Vector2(0, 0);
            container.sizeDelta = new Vector2(0, 80);
        }

        private void CreateTitleLabel()
        {
            GameObject labelObj = new GameObject("TitleLabel");
            labelObj.transform.SetParent(container, false);

            RectTransform rt = labelObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0.3f, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(10, -8);
            rt.sizeDelta = new Vector2(0, 20);

            titleLabel = labelObj.AddComponent<TextMeshProUGUI>();
            titleLabel.text = "FETCH RADIUS";
            titleLabel.fontSize = 11;
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.color = labelColor;
            titleLabel.alignment = TextAlignmentOptions.Left;
        }

        private void CreateSlider()
        {
            // Track
            GameObject trackObj = new GameObject("SliderTrack");
            trackObj.transform.SetParent(container, false);

            trackRect = trackObj.AddComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0.02f, 0.35f);
            trackRect.anchorMax = new Vector2(0.75f, 0.55f);
            trackRect.offsetMin = Vector2.zero;
            trackRect.offsetMax = Vector2.zero;

            sliderTrack = trackObj.AddComponent<Image>();
            sliderTrack.color = sliderTrackColor;
            sliderTrack.raycastTarget = true;

            // Add event trigger for track clicks
            EventTrigger trackTrigger = trackObj.AddComponent<EventTrigger>();
            AddPointerEvent(trackTrigger, EventTriggerType.PointerDown, OnTrackPointerDown);
            AddPointerEvent(trackTrigger, EventTriggerType.Drag, OnTrackDrag);
            AddPointerEvent(trackTrigger, EventTriggerType.PointerUp, OnTrackPointerUp);

            // Fill
            GameObject fillObj = new GameObject("SliderFill");
            fillObj.transform.SetParent(trackObj.transform, false);

            fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.5f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            sliderFill = fillObj.AddComponent<Image>();
            sliderFill.color = sliderFillColor;
            sliderFill.raycastTarget = false;

            // Handle
            GameObject handleObj = new GameObject("SliderHandle");
            handleObj.transform.SetParent(trackObj.transform, false);

            handleRect = handleObj.AddComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(20, 24);

            sliderHandle = handleObj.AddComponent<Image>();
            sliderHandle.color = sliderHandleColor;
            sliderHandle.raycastTarget = true;

            // Handle drag events
            EventTrigger handleTrigger = handleObj.AddComponent<EventTrigger>();
            AddPointerEvent(handleTrigger, EventTriggerType.BeginDrag, OnHandleBeginDrag);
            AddPointerEvent(handleTrigger, EventTriggerType.Drag, OnHandleDrag);
            AddPointerEvent(handleTrigger, EventTriggerType.EndDrag, OnHandleEndDrag);
        }

        private void CreateValueDisplay()
        {
            // Value label (large NM display)
            GameObject valueObj = new GameObject("ValueLabel");
            valueObj.transform.SetParent(container, false);

            RectTransform rt = valueObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.78f, 0.25f);
            rt.anchorMax = new Vector2(1f, 0.85f);
            rt.offsetMin = new Vector2(0, 0);
            rt.offsetMax = new Vector2(-10, 0);

            valueLabel = valueObj.AddComponent<TextMeshProUGUI>();
            valueLabel.text = "100 NM";
            valueLabel.fontSize = 18;
            valueLabel.fontStyle = FontStyles.Bold;
            valueLabel.color = valueColor;
            valueLabel.alignment = TextAlignmentOptions.Center;

            // Optional input field overlay (for direct input)
            GameObject inputObj = new GameObject("InputField");
            inputObj.transform.SetParent(valueObj.transform, false);

            RectTransform inputRt = inputObj.AddComponent<RectTransform>();
            inputRt.anchorMin = Vector2.zero;
            inputRt.anchorMax = Vector2.one;
            inputRt.offsetMin = Vector2.zero;
            inputRt.offsetMax = Vector2.zero;

            // Text area for input
            GameObject textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputObj.transform, false);
            RectTransform textAreaRt = textArea.AddComponent<RectTransform>();
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = Vector2.zero;
            textAreaRt.offsetMax = Vector2.zero;

            GameObject inputText = new GameObject("Text");
            inputText.transform.SetParent(textArea.transform, false);
            RectTransform inputTextRt = inputText.AddComponent<RectTransform>();
            inputTextRt.anchorMin = Vector2.zero;
            inputTextRt.anchorMax = Vector2.one;
            inputTextRt.offsetMin = Vector2.zero;
            inputTextRt.offsetMax = Vector2.zero;
            TMP_Text inputTmpText = inputText.AddComponent<TextMeshProUGUI>();
            inputTmpText.fontSize = 18;
            inputTmpText.alignment = TextAlignmentOptions.Center;
            inputTmpText.color = Color.clear; // Hidden, we use valueLabel instead

            inputField = inputObj.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRt;
            inputField.textComponent = inputTmpText;
            inputField.contentType = TMP_InputField.ContentType.DecimalNumber;
            inputField.onEndEdit.AddListener(OnInputEndEdit);
        }

        private void CreatePresetButtons()
        {
            presetButtons = new Image[presetValues.Length];
            presetHovered = new bool[presetValues.Length];
            presetAnimProgress = new float[presetValues.Length];

            float buttonWidth = 1f / (presetValues.Length + 0.5f);

            for (int i = 0; i < presetValues.Length; i++)
            {
                GameObject btnObj = new GameObject($"Preset_{presetValues[i]}");
                btnObj.transform.SetParent(container, false);

                RectTransform rt = btnObj.AddComponent<RectTransform>();
                float xPos = 0.02f + (i * buttonWidth * 0.95f);
                rt.anchorMin = new Vector2(xPos, 0.02f);
                rt.anchorMax = new Vector2(xPos + buttonWidth * 0.85f, 0.28f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                Image img = btnObj.AddComponent<Image>();
                img.color = sliderTrackColor;
                img.raycastTarget = true;
                presetButtons[i] = img;

                // Label
                GameObject labelObj = new GameObject("Label");
                labelObj.transform.SetParent(btnObj.transform, false);
                RectTransform labelRt = labelObj.AddComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;

                TMP_Text label = labelObj.AddComponent<TextMeshProUGUI>();
                label.text = $"{presetValues[i]:0}";
                label.fontSize = 10;
                label.alignment = TextAlignmentOptions.Center;
                label.color = labelColor;

                // Events
                int capturedIndex = i;
                EventTrigger trigger = btnObj.AddComponent<EventTrigger>();
                AddPointerEvent(trigger, EventTriggerType.PointerEnter, (data) => OnPresetEnter(capturedIndex));
                AddPointerEvent(trigger, EventTriggerType.PointerExit, (data) => OnPresetExit(capturedIndex));
                AddPointerEvent(trigger, EventTriggerType.PointerClick, (data) => OnPresetClick(capturedIndex));
            }
        }

        private void AddPointerEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = type;
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        #endregion

        #region Event Handlers

        private void OnTrackPointerDown(BaseEventData data)
        {
            isDragging = true;
            UpdateSliderFromPointer(data as PointerEventData);
        }

        private void OnTrackDrag(BaseEventData data)
        {
            UpdateSliderFromPointer(data as PointerEventData);
        }

        private void OnTrackPointerUp(BaseEventData data)
        {
            isDragging = false;
            ApplyValue();
        }

        private void OnHandleBeginDrag(BaseEventData data)
        {
            isDragging = true;
        }

        private void OnHandleDrag(BaseEventData data)
        {
            UpdateSliderFromPointer(data as PointerEventData);
        }

        private void OnHandleEndDrag(BaseEventData data)
        {
            isDragging = false;
            ApplyValue();
        }

        private void UpdateSliderFromPointer(PointerEventData eventData)
        {
            if (trackRect == null || eventData == null) return;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                trackRect, eventData.position, eventData.pressEventCamera, out localPoint);

            float normalizedX = (localPoint.x + trackRect.rect.width * 0.5f) / trackRect.rect.width;
            normalizedX = Mathf.Clamp01(normalizedX);

            targetValue = Mathf.Lerp(minRadiusNM, maxRadiusNM, normalizedX);
        }

        private void OnInputEndEdit(string value)
        {
            if (float.TryParse(value, out float newRadius))
            {
                targetValue = Mathf.Clamp(newRadius, minRadiusNM, maxRadiusNM);
                ApplyValue();
            }
            UpdateVisuals();
        }

        private void OnPresetEnter(int index)
        {
            presetHovered[index] = true;
        }

        private void OnPresetExit(int index)
        {
            presetHovered[index] = false;
        }

        private void OnPresetClick(int index)
        {
            targetValue = presetValues[index];
            ApplyValue();
        }

        #endregion

        #region Animation

        private void AnimateVisibility()
        {
            if (!Mathf.Approximately(canvasGroup.alpha, targetAlpha))
            {
                canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * animationSpeed);
                if (Mathf.Abs(canvasGroup.alpha - targetAlpha) < 0.01f)
                {
                    canvasGroup.alpha = targetAlpha;
                }
            }

            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;
        }

        private void AnimateValue()
        {
            if (!Mathf.Approximately(currentValue, targetValue))
            {
                currentValue = Mathf.Lerp(currentValue, targetValue, Time.deltaTime * animationSpeed);
                if (Mathf.Abs(currentValue - targetValue) < 0.5f)
                {
                    currentValue = targetValue;
                }
                UpdateVisuals();
            }
        }

        private void AnimatePresetButtons()
        {
            for (int i = 0; i < presetButtons.Length; i++)
            {
                float target = presetHovered[i] ? 1f : 0f;
                
                // Check if this is the active preset
                bool isActive = Mathf.Abs(currentValue - presetValues[i]) < 2f;
                if (isActive) target = 0.7f;

                presetAnimProgress[i] = Mathf.Lerp(presetAnimProgress[i], target, Time.deltaTime * animationSpeed);

                Color c = Color.Lerp(sliderTrackColor, sliderFillColor, presetAnimProgress[i]);
                presetButtons[i].color = c;
            }
        }

        private void UpdateVisuals()
        {
            if (fillRect == null || handleRect == null || valueLabel == null) return;

            float normalizedValue = Mathf.InverseLerp(minRadiusNM, maxRadiusNM, currentValue);

            // Update fill
            fillRect.anchorMax = new Vector2(normalizedValue, 1f);

            // Update handle position
            handleRect.anchorMin = new Vector2(normalizedValue, 0.5f);
            handleRect.anchorMax = new Vector2(normalizedValue, 0.5f);

            // Pulse handle when dragging
            if (isDragging)
            {
                float pulse = 1f + Mathf.Sin(handleAnimPhase) * 0.1f;
                handleRect.localScale = Vector3.one * pulse;
                sliderHandle.color = Color.Lerp(sliderHandleColor, Color.white, Mathf.Sin(handleAnimPhase) * 0.3f + 0.3f);
            }
            else
            {
                handleRect.localScale = Vector3.one;
                sliderHandle.color = sliderHandleColor;
            }

            // Update value label
            valueLabel.text = $"{currentValue:0} NM";
        }

        private void ApplyValue()
        {
            if (dataManager != null)
            {
                dataManager.RadiusFilterNM = currentValue;
                Debug.Log($"[TrafficRadarFilterUI] Radius set to {currentValue:0} NM");
            }
        }

        #endregion

        #region Utility

        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        #endregion
    }
}
