using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

namespace TrafficRadar.Controls
{
    /// <summary>
    /// Click handler for Traffic Radar display that toggles UI controls visibility.
    /// Features script-based fade animations for high performance.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class TrafficRadarClickHandler : MonoBehaviour, IPointerClickHandler
    {
        #region Inspector Fields

        [Header("UI References")]
        [Tooltip("Range UI to toggle. Auto-found if null.")]
        [SerializeField] private TrafficRadarRangeUI rangeUI;
        
        [Tooltip("Filter UI to toggle. Auto-found if null.")]
        [SerializeField] private TrafficRadarFilterUI filterUI;

        [Header("Toggle Settings")]
        [SerializeField] private bool toggleRangeUI = true;
        [SerializeField] private bool toggleFilterUI = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.T;

        [Header("Animation")]
        [SerializeField] private float animationDuration = 0.25f;
        [SerializeField] private float clickFeedbackScale = 0.98f;
        [SerializeField] private float clickFeedbackDuration = 0.1f;

        [Header("Initial State")]
        [SerializeField] private bool startUIVisible = true;

        #endregion

        #region Private Fields

        private bool uiVisible;
        private RectTransform rectTransform;
        private Coroutine clickFeedbackCoroutine;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            
            // Auto-find UI components
            if (rangeUI == null)
                rangeUI = FindObjectOfType<TrafficRadarRangeUI>();
            if (filterUI == null)
                filterUI = FindObjectOfType<TrafficRadarFilterUI>();

            uiVisible = startUIVisible;
        }

        private void Start()
        {
            // Set initial visibility state
            SetUIVisibility(startUIVisible, false);
        }

        private void Update()
        {
            // Toggle with hotkey
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleUI();
            }
        }

        #endregion

        #region IPointerClickHandler

        public void OnPointerClick(PointerEventData eventData)
        {
            // Play click feedback animation
            if (clickFeedbackCoroutine != null)
                StopCoroutine(clickFeedbackCoroutine);
            clickFeedbackCoroutine = StartCoroutine(ClickFeedback());

            // Toggle UI visibility
            ToggleUI();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Toggle UI visibility
        /// </summary>
        public void ToggleUI()
        {
            SetUIVisibility(!uiVisible, true);
        }

        /// <summary>
        /// Set UI visibility with optional animation
        /// </summary>
        public void SetUIVisibility(bool visible, bool animate = true)
        {
            uiVisible = visible;

            if (toggleRangeUI && rangeUI != null)
            {
                rangeUI.SetVisibility(visible);
            }

            if (toggleFilterUI && filterUI != null)
            {
                filterUI.SetVisibility(visible);
            }

            Debug.Log($"[TrafficRadarClickHandler] UI visibility set to {visible}");
        }

        /// <summary>
        /// Show UI with animation
        /// </summary>
        public void ShowUI()
        {
            SetUIVisibility(true, true);
        }

        /// <summary>
        /// Hide UI with animation
        /// </summary>
        public void HideUI()
        {
            SetUIVisibility(false, true);
        }

        #endregion

        #region Animation

        private IEnumerator ClickFeedback()
        {
            if (rectTransform == null) yield break;

            Vector3 originalScale = rectTransform.localScale;
            Vector3 targetScale = originalScale * clickFeedbackScale;
            float halfDuration = clickFeedbackDuration * 0.5f;

            // Scale down
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutQuad(elapsed / halfDuration);
                rectTransform.localScale = Vector3.Lerp(originalScale, targetScale, t);
                yield return null;
            }

            // Scale back up
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutQuad(elapsed / halfDuration);
                rectTransform.localScale = Vector3.Lerp(targetScale, originalScale, t);
                yield return null;
            }

            rectTransform.localScale = originalScale;
        }

        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        #endregion
    }
}
