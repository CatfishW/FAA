using UnityEngine;
using UnityEngine.EventSystems;

namespace WeatherRadar
{
    /// <summary>
    /// Handles click events on the weather radar display to toggle the control panel.
    /// Attach this to the radar display image or a transparent overlay.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class WeatherRadarClickHandler : MonoBehaviour, IPointerClickHandler
    {
        [Header("References")]
        [Tooltip("The control panel to toggle. If null, will be auto-discovered.")]
        [SerializeField] private RadarControlPanel controlPanel;

        [Header("Settings")]
        [SerializeField] private bool startPanelVisible = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.P;

        [Header("Animation")]
        [SerializeField] private float animationDuration = 0.25f;
        [SerializeField] private float slideDistance = 30f;
        [SerializeField] private AnimationDirection slideDirection = AnimationDirection.Right;

        public enum AnimationDirection
        {
            Left,
            Right,
            Up,
            Down
        }

        private bool isPanelVisible;
        private CanvasGroup panelCanvasGroup;
        private RectTransform panelRect;
        private Vector2 panelShowPosition;
        private Vector2 panelHidePosition;
        private float animProgress = 1f;
        private bool animTargetVisible;

        private void Awake()
        {
            // Auto-find control panel
            if (controlPanel == null)
            {
                controlPanel = GetComponentInParent<WeatherRadarProviderBase>()?.GetComponentInChildren<RadarControlPanel>();
            }

            if (controlPanel == null)
            {
                controlPanel = FindObjectOfType<RadarControlPanel>();
            }
        }

        private void Start()
        {
            if (controlPanel != null)
            {
                SetupPanelAnimation();
                isPanelVisible = startPanelVisible;
                animTargetVisible = startPanelVisible;
                animProgress = 1f;
                
                // Set initial state
                ApplyVisibilityState(startPanelVisible ? 1f : 0f);
            }
            else
            {
                Debug.LogWarning("[WeatherRadarClickHandler] No RadarControlPanel found!");
            }
        }

        private void Update()
        {
            // Hotkey toggle
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleControlPanel();
            }

            // Animate panel
            AnimatePanel();
        }

        private void SetupPanelAnimation()
        {
            panelRect = controlPanel.GetComponent<RectTransform>();
            
            panelCanvasGroup = controlPanel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = controlPanel.gameObject.AddComponent<CanvasGroup>();
            }

            // Store the "show" position
            panelShowPosition = panelRect.anchoredPosition;

            // Calculate "hide" position based on slide direction
            panelHidePosition = panelShowPosition;
            switch (slideDirection)
            {
                case AnimationDirection.Left:
                    panelHidePosition.x -= slideDistance;
                    break;
                case AnimationDirection.Right:
                    panelHidePosition.x += slideDistance;
                    break;
                case AnimationDirection.Up:
                    panelHidePosition.y += slideDistance;
                    break;
                case AnimationDirection.Down:
                    panelHidePosition.y -= slideDistance;
                    break;
            }
        }

        /// <summary>
        /// Handle click on radar display
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            ToggleControlPanel();
        }

        /// <summary>
        /// Toggle control panel visibility with animation
        /// </summary>
        public void ToggleControlPanel()
        {
            if (controlPanel == null) return;

            isPanelVisible = !isPanelVisible;
            animTargetVisible = isPanelVisible;
            animProgress = 0f;

            Debug.Log($"[WeatherRadarClickHandler] Control panel toggled: {(isPanelVisible ? "SHOW" : "HIDE")}");
        }

        /// <summary>
        /// Set panel visibility directly
        /// </summary>
        public void SetPanelVisible(bool visible)
        {
            if (isPanelVisible == visible) return;

            isPanelVisible = visible;
            animTargetVisible = visible;
            animProgress = 0f;
        }

        private void AnimatePanel()
        {
            if (controlPanel == null || panelCanvasGroup == null || panelRect == null) return;

            if (animProgress < 1f)
            {
                animProgress += Time.deltaTime / animationDuration;
                animProgress = Mathf.Clamp01(animProgress);

                float t = EaseOutQuad(animProgress);
                ApplyVisibilityState(animTargetVisible ? t : 1f - t);
            }
        }

        private void ApplyVisibilityState(float t)
        {
            // Interpolate alpha
            panelCanvasGroup.alpha = t;

            // Interpolate position (slide)
            panelRect.anchoredPosition = Vector2.Lerp(panelHidePosition, panelShowPosition, t);

            // Set interactivity
            bool isFullyVisible = t >= 0.99f;
            panelCanvasGroup.interactable = isFullyVisible;
            panelCanvasGroup.blocksRaycasts = t > 0.1f; // Allow raycasts during animation for smoother feel
        }

        private float EaseOutQuad(float x)
        {
            return 1f - (1f - x) * (1f - x);
        }

        /// <summary>
        /// Check if a graphic raycaster exists for click detection
        /// </summary>
        private void OnValidate()
        {
            // Ensure we have a graphic raycaster in the canvas hierarchy
            if (Application.isPlaying) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                if (raycaster == null)
                {
                    Debug.LogWarning("[WeatherRadarClickHandler] Canvas needs a GraphicRaycaster for click detection!");
                }
            }
        }
    }
}
