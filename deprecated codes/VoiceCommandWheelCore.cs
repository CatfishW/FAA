using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VoiceControl.UI
{
    /// <summary>
    /// Core radial menu controller - simplified and robust implementation.
    /// </summary>
    [AddComponentMenu("Voice Control/UI/Voice Command Wheel Core")]
    public class VoiceCommandWheelCore : MonoBehaviour
    {
        [Header("Input")]
        public bool useTabKey = true;
        public bool useLazySelection = true;

        [Header("Animation")]
        public float animationDuration = 0.3f;
        public AnimationCurve expandCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("References")]
        public RectTransform expandedRoot;
        public RectTransform collapsedRoot;
        public CanvasGroup expandedGroup;
        public CanvasGroup collapsedGroup;
        public TextMeshProUGUI centerLabel;
        public TextMeshProUGUI centerSubLabel;

        [Header("Segments")]
        public List<VoiceCommandSegment> segments = new List<VoiceCommandSegment>();

        // Events
        public event Action<int> OnSegmentSelected;
        public event Action OnWheelOpened;
        public event Action OnWheelClosed;

        // State
        private bool isExpanded = false;
        private bool isAnimating = false;
        private int currentIndex = -1;

        void Awake()
        {
            // Ensure we have references
            if (expandedGroup == null && expandedRoot != null)
                expandedGroup = expandedRoot.GetComponent<CanvasGroup>();
            if (collapsedGroup == null && collapsedRoot != null)
                collapsedGroup = collapsedRoot.GetComponent<CanvasGroup>();

            // Setup segment parents
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] != null)
                {
                    segments[i].parentWheel = this;
                    segments[i].assignedIndex = i;
                }
            }

            // Start collapsed
            SetInitialState();
        }

        void SetInitialState()
        {
            isExpanded = false;

            if (expandedRoot != null)
            {
                expandedRoot.localScale = Vector3.zero;
                expandedRoot.gameObject.SetActive(false);
            }

            if (collapsedRoot != null)
            {
                collapsedRoot.localScale = Vector3.one;
                collapsedRoot.gameObject.SetActive(true);
            }

            if (expandedGroup != null)
            {
                expandedGroup.alpha = 0;
                expandedGroup.blocksRaycasts = false;
            }

            if (collapsedGroup != null)
            {
                collapsedGroup.alpha = 1;
                collapsedGroup.blocksRaycasts = true;
            }
        }

        void Update()
        {
            // Tab key toggle
            if (useTabKey && Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleExpanded();
                return;
            }

            // Escape to close
            if (isExpanded && Input.GetKeyDown(KeyCode.Escape))
            {
                SetExpanded(false);
                return;
            }

            if (!isExpanded || !useLazySelection) return;

            // Get mouse position relative to wheel center
            Vector2 wheelCenter = expandedRoot.position;
            Vector2 mousePos = Input.mousePosition;
            Vector2 delta = mousePos - wheelCenter;

            float distance = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            // Convert to 0-360 with 0 at top
            angle = NormalizeAngle(-angle + 90);

            // Calculate which segment (8 segments = 45 degrees each)
            int segmentCount = segments.Count;
            float degreesPerSegment = 360f / segmentCount;

            // Offset by half segment to center the selection zones
            float adjustedAngle = NormalizeAngle(angle + degreesPerSegment / 2);
            int selectedIndex = Mathf.FloorToInt(adjustedAngle / degreesPerSegment);

            // Clamp to valid range
            selectedIndex = Mathf.Clamp(selectedIndex, 0, segmentCount - 1);

            // Only select if mouse is in the ring area (not too close to center, not too far)
            if (distance > 80 && distance < 450 && selectedIndex >= 0 && selectedIndex < segments.Count)
            {
                if (selectedIndex != currentIndex)
                {
                    SelectSegment(selectedIndex);
                }
            }
        }

        public void ToggleExpanded()
        {
            SetExpanded(!isExpanded);
        }

        public void SetExpanded(bool expanded)
        {
            if (isExpanded == expanded || isAnimating) return;

            isExpanded = expanded;
            StopAllCoroutines();
            StartCoroutine(AnimateTransition(expanded));

            if (expanded)
                OnWheelOpened?.Invoke();
            else
                OnWheelClosed?.Invoke();
        }

        IEnumerator AnimateTransition(bool expanding)
        {
            isAnimating = true;
            float elapsed = 0;

            // Activate both for crossfade
            if (expandedRoot != null) expandedRoot.gameObject.SetActive(true);
            if (collapsedRoot != null) collapsedRoot.gameObject.SetActive(true);

            // Get start values
            float startExpandedScale = expandedRoot != null ? expandedRoot.localScale.x : 0;
            float endExpandedScale = expanding ? 1f : 0f;

            float startCollapsedScale = collapsedRoot != null ? collapsedRoot.localScale.x : 1;
            float endCollapsedScale = expanding ? 0.5f : 1f;

            float startExpandedAlpha = expandedGroup != null ? expandedGroup.alpha : 0;
            float endExpandedAlpha = expanding ? 1f : 0f;

            float startCollapsedAlpha = collapsedGroup != null ? collapsedGroup.alpha : 1;
            float endCollapsedAlpha = expanding ? 0f : 1f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                float curvedT = expandCurve.Evaluate(t);

                if (expandedRoot != null)
                {
                    float s = Mathf.Lerp(startExpandedScale, endExpandedScale, curvedT);
                    expandedRoot.localScale = new Vector3(s, s, 1);
                }

                if (collapsedRoot != null)
                {
                    float s = Mathf.Lerp(startCollapsedScale, endCollapsedScale, curvedT);
                    collapsedRoot.localScale = new Vector3(s, s, 1);
                }

                if (expandedGroup != null)
                {
                    expandedGroup.alpha = Mathf.Lerp(startExpandedAlpha, endExpandedAlpha, curvedT);
                    expandedGroup.blocksRaycasts = expanding && t > 0.5f;
                }

                if (collapsedGroup != null)
                {
                    collapsedGroup.alpha = Mathf.Lerp(startCollapsedAlpha, endCollapsedAlpha, curvedT);
                    collapsedGroup.blocksRaycasts = !expanding || t < 0.5f;
                }

                yield return null;
            }

            // Final state
            if (expandedRoot != null)
            {
                float es = expanding ? 1f : 0f;
                expandedRoot.localScale = new Vector3(es, es, 1);
                expandedRoot.gameObject.SetActive(expanding);
            }

            if (collapsedRoot != null)
            {
                float cs = expanding ? 0.5f : 1f;
                collapsedRoot.localScale = new Vector3(cs, cs, 1);
                collapsedRoot.gameObject.SetActive(!expanding);
            }

            if (expandedGroup != null)
            {
                expandedGroup.alpha = expanding ? 1f : 0f;
                expandedGroup.blocksRaycasts = expanding;
            }

            if (collapsedGroup != null)
            {
                collapsedGroup.alpha = expanding ? 0f : 1f;
                collapsedGroup.blocksRaycasts = !expanding;
            }

            isAnimating = false;
        }

        void SelectSegment(int index)
        {
            if (index < 0 || index >= segments.Count) return;

            // Unhighlight previous
            if (currentIndex >= 0 && currentIndex < segments.Count && currentIndex != index)
            {
                segments[currentIndex].UnHighlight();
            }

            // Highlight new
            segments[index].Highlight();
            currentIndex = index;

            // Update center labels
            if (centerLabel != null)
                centerLabel.text = segments[index].displayLabel;
            if (centerSubLabel != null)
                centerSubLabel.text = segments[index].subLabel;
        }

        public void OnSegmentClicked(int index)
        {
            Debug.Log($"[VoiceCommandWheel] Segment {index} clicked");
            OnSegmentSelected?.Invoke(index);
        }

        float NormalizeAngle(float angle)
        {
            angle = angle % 360f;
            if (angle < 0) angle += 360f;
            return angle;
        }
    }
}
