using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VoiceControl.UI
{
    /// <summary>
    /// Represents a single segment in the radial command wheel with sub-node expansion capability.
    /// </summary>
    public class VoiceCommandWheelSegment : MonoBehaviour
    {
        [Header("Segment Visuals")]
        [SerializeField] private Image segmentImage;
        [SerializeField] private Button segmentButton;
        [SerializeField] private TMP_Text commandLabel;
        [SerializeField] private TMP_Text iconLabel;
        [SerializeField] private Image iconBadge;
        [SerializeField] private RectTransform contentTransform;

        [Header("Sub-Node Settings")]
        [SerializeField] private GameObject subNodePrefab;
        [SerializeField] private Transform subNodeContainer;
        [SerializeField] private float subNodeRadius = 140f;
        [SerializeField] private float expansionDuration = 0.3f;

        [Header("Animation Settings")]
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float selectScale = 1.12f;
        [SerializeField] private float animationSpeed = 8f;

        // State
        public int SegmentIndex { get; set; }
        public bool IsSelected { get; private set; }
        public bool IsExpanded { get; private set; }
        public CommandData Command { get; private set; }

        // Sub-nodes
        private List<VoiceCommandSubNode> subNodes = new List<VoiceCommandSubNode>();
        private List<CommandData> subCommands = new List<CommandData>();

        // Animation
        private Vector3 targetScale = Vector3.one;
        private Color targetColor;
        private float targetAlpha = 1f;

        // Events
        public event Action<VoiceCommandWheelSegment> OnSegmentSelected;
        public event Action<VoiceCommandWheelSegment> OnSegmentDeselected;
        public event Action<CommandData> OnSubNodeSelected;

        [System.Serializable]
        public class CommandData
        {
            public string id;
            public string displayName;
            public string shortCode;
            public string icon;
            public Color accentColor;
            public List<CommandData> subCommands;
        }

        private void Awake()
        {
            if (segmentButton != null)
            {
                segmentButton.onClick.AddListener(OnSegmentClicked);
            }

            targetScale = transform.localScale;
        }

        private void Update()
        {
            // Smooth scale animation
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * animationSpeed);

            // Smooth color animation
            if (segmentImage != null)
            {
                segmentImage.color = Color.Lerp(segmentImage.color, targetColor, Time.deltaTime * animationSpeed);
            }
        }

        public void Initialize(CommandData command, int index)
        {
            Command = command;
            SegmentIndex = index;

            // Set labels
            if (commandLabel != null)
            {
                commandLabel.text = command.displayName ?? "";
            }

            if (iconLabel != null)
            {
                iconLabel.text = command.shortCode ?? "";
            }

            // Set accent color
            if (command.accentColor != default && iconBadge != null)
            {
                iconBadge.color = command.accentColor;
            }

            // Store sub-commands
            subCommands = command.subCommands ?? new List<CommandData>();
        }

        public void SetVisualState(Color color, float alpha = 1f)
        {
            targetColor = new Color(color.r, color.g, color.b, color.a * alpha);
            targetAlpha = alpha;

            // Update labels alpha
            if (commandLabel != null)
            {
                var textColor = commandLabel.color;
                textColor.a = alpha;
                commandLabel.color = textColor;
            }

            if (iconBadge != null)
            {
                var badgeColor = iconBadge.color;
                badgeColor.a = alpha;
                iconBadge.color = badgeColor;
            }
        }

        public void SetHovered(bool hovered)
        {
            if (hovered)
            {
                targetScale = Vector3.one * hoverScale;
            }
            else if (!IsSelected)
            {
                targetScale = Vector3.one;
            }
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;

            if (selected)
            {
                targetScale = Vector3.one * selectScale;
                ExpandSubNodes();
            }
            else
            {
                targetScale = Vector3.one;
                CollapseSubNodes();
            }
        }

        private void OnSegmentClicked()
        {
            OnSegmentSelected?.Invoke(this);
        }

        #region Sub-Node Expansion

        private void ExpandSubNodes()
        {
            if (subCommands.Count == 0 || IsExpanded) return;

            IsExpanded = true;

            // Create sub-nodes if they don't exist
            if (subNodes.Count == 0)
            {
                CreateSubNodes();
            }

            // Animate sub-nodes in
            StartCoroutine(AnimateSubNodes(true));
        }

        private void CollapseSubNodes()
        {
            if (!IsExpanded) return;

            IsExpanded = false;

            // Animate sub-nodes out
            StartCoroutine(AnimateSubNodes(false));
        }

        private void CreateSubNodes()
        {
            int count = subCommands.Count;
            float startAngle = (SegmentIndex * (360f / 8)) - 22.5f; // Center on parent segment
            float angleSpread = Mathf.Min(60f, count * 20f); // Spread sub-nodes

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                float angle = startAngle - angleSpread * 0.5f + angleSpread * t;

                GameObject subNodeObj = Instantiate(subNodePrefab, subNodeContainer);
                subNodeObj.name = $"SubNode_{i}";

                // Position in arc pattern
                RectTransform rect = subNodeObj.GetComponent<RectTransform>();
                float rad = angle * Mathf.Deg2Rad;
                rect.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * subNodeRadius;
                rect.localScale = Vector3.zero;

                // Initialize sub-node
                VoiceCommandSubNode subNode = subNodeObj.GetComponent<VoiceCommandSubNode>();
                if (subNode != null)
                {
                    subNode.Initialize(subCommands[i], this);
                    subNode.OnSelected += OnSubNodeClicked;
                    subNodes.Add(subNode);
                }
            }
        }

        private System.Collections.IEnumerator AnimateSubNodes(bool expand)
        {
            float elapsed = 0f;
            int count = subNodes.Count;

            while (elapsed < expansionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / expansionDuration;

                if (!expand) t = 1f - t;

                // Elastic ease out
                float ease = expand ? ElasticEaseOut(t) : EaseInCubic(t);

                for (int i = 0; i < count; i++)
                {
                    if (subNodes[i] != null)
                    {
                        float delay = i * 0.05f;
                        float localT = Mathf.Clamp01((t - delay) / (1f - delay));
                        float scale = expand ? ElasticEaseOut(localT) : EaseInCubic(localT);

                        subNodes[i].SetScale(Vector3.one * Mathf.Max(0, scale));

                        // Fade in/out
                        subNodes[i].SetAlpha(expand ? localT : 1f - localT);
                    }
                }

                yield return null;
            }

            // Final state
            if (!expand)
            {
                foreach (var node in subNodes)
                {
                    if (node != null) node.SetScale(Vector3.zero);
                }
            }
        }

        private void OnSubNodeClicked(VoiceCommandSubNode subNode)
        {
            // Find the corresponding command data for this subnode
            int index = subNodes.IndexOf(subNode);
            if (index >= 0 && index < subCommands.Count)
            {
                OnSubNodeSelected?.Invoke(subCommands[index]);
            }
        }

        #endregion

        #region Easing Functions

        private float ElasticEaseOut(float t)
        {
            if (t == 0) return 0;
            if (t == 1) return 1;

            float p = 0.3f;
            float s = p / 4f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - s) * (2f * Mathf.PI) / p) + 1f;
        }

        private float EaseInCubic(float t)
        {
            return t * t * t;
        }

        private float EaseOutBack(float t)
        {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        #endregion

        private void OnDestroy()
        {
            if (segmentButton != null)
            {
                segmentButton.onClick.RemoveListener(OnSegmentClicked);
            }

            foreach (var node in subNodes)
            {
                if (node != null)
                {
                    node.OnSelected -= OnSubNodeClicked;
                }
            }
        }
    }
}
