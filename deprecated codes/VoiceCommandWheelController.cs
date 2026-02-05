using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoiceControl.Core;

namespace VoiceControl.UI
{
    /// <summary>
    /// Radial command wheel optimized for gamepads.
    /// Supports expand/collapse with lightweight animations and command paging.
    /// </summary>
    [AddComponentMenu("Voice Control/UI/Voice Command Wheel Controller")]
    public class VoiceCommandWheelController : MonoBehaviour
    {
        [Header("UI Roots")]
        [SerializeField] private RectTransform expandedRoot;
        [SerializeField] private RectTransform collapsedRoot;
        [SerializeField] private CanvasGroup expandedGroup;
        [SerializeField] private CanvasGroup collapsedGroup;

        [Header("Wheel Segments")]
        [SerializeField] private Image[] segmentImages;
        [SerializeField] private Button[] segmentButtons;
        [SerializeField] private TMP_Text[] segmentLabels;
        [SerializeField] private TMP_Text[] segmentIconLabels;

        [Header("Center Panel")]
        [SerializeField] private TMP_Text centerTitle;
        [SerializeField] private TMP_Text centerCommandLabel;
        [SerializeField] private TMP_Text centerTargetLabel;
        [SerializeField] private TMP_Text pageLabel;
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private Image[] statFills;

        [Header("Commands")]
        [SerializeField] private bool autoDiscoverTargets = true;
        [SerializeField] private bool includeParameterizedCommandsInWheel = false;
        [SerializeField] private bool executeOnClick = true;

        [Header("Selection")]
        [SerializeField] private Color segmentNormal = new Color(0.36f, 0.39f, 0.43f, 0.95f);
        [SerializeField] private Color segmentSelected = new Color(0.5f, 0.9f, 0.5f, 0.95f);
        [SerializeField] private Color segmentDim = new Color(0.2f, 0.24f, 0.28f, 0.7f);

        [Header("Input")]
        [SerializeField] private string horizontalAxis = "Horizontal";
        [SerializeField] private string verticalAxis = "Vertical";
        [SerializeField, Range(0f, 1f)] private float deadzone = 0.35f;
        [SerializeField] private bool allowMouseClick = true;
        [SerializeField] private string submitButton = "Submit";
        [SerializeField] private string nextPageInput = "Fire3";
        [SerializeField] private string previousPageInput = "Fire2";
        [SerializeField] private KeyCode nextPageKey = KeyCode.E;
        [SerializeField] private KeyCode previousPageKey = KeyCode.Q;
        [SerializeField] private KeyCode executeKey = KeyCode.Return;

        [Header("Animation")]
        [SerializeField] private float expandDuration = 0.22f;
        [SerializeField] private AnimationCurve expandCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private Vector3 expandedScale = Vector3.one;
        [SerializeField] private Vector3 collapsedScale = new Vector3(0.75f, 0.75f, 1f);

        private class CommandEntry
        {
            public string targetId;
            public string commandName;
            public string displayName;
            public string targetDisplayName;
            public string description;
            public bool requiresParams;
        }

        private readonly List<CommandEntry> _commands = new List<CommandEntry>();
        private int _selectedIndex;
        private bool _isExpanded;
        private Coroutine _animationRoutine;
        private int _pageIndex;
        private int _itemsPerPage;
        private bool _ready;

        public bool IsExpanded => _isExpanded;

        private void Awake()
        {
            if (expandedGroup == null && expandedRoot != null)
                expandedGroup = expandedRoot.GetComponent<CanvasGroup>();
            if (collapsedGroup == null && collapsedRoot != null)
                collapsedGroup = collapsedRoot.GetComponent<CanvasGroup>();

            WireButtons();
        }

        private void OnEnable()
        {
            var registry = VoiceCommandRegistry.Instance;
            if (registry != null)
                registry.OnRegistryUpdated += HandleRegistryUpdated;
        }

        private void OnDisable()
        {
            var registry = VoiceCommandRegistry.Instance;
            if (registry != null)
                registry.OnRegistryUpdated -= HandleRegistryUpdated;
        }

        private void Start()
        {
            RefreshCommands();
            ApplyExpandedState(false, true);
            UpdateSelection(0, false);
            _ready = true;
        }

        private void Update()
        {
            if (!_isExpanded)
            {
                if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.Escape))
                    SetExpanded(true);
                return;
            }

            if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.Escape))
            {
                SetExpanded(false);
                return;
            }

            if (Input.GetButtonDown(submitButton) || Input.GetKeyDown(executeKey))
            {
                ExecuteSelected();
                return;
            }

            if (Input.GetButtonDown(nextPageInput) || Input.GetKeyDown(nextPageKey))
            {
                NextPage();
                return;
            }

            if (Input.GetButtonDown(previousPageInput) || Input.GetKeyDown(previousPageKey))
            {
                PreviousPage();
                return;
            }

            Vector2 stick = new Vector2(Input.GetAxis(horizontalAxis), Input.GetAxis(verticalAxis));
            if (stick.magnitude < deadzone)
                return;

            float angle = Mathf.Atan2(stick.y, stick.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            int count = segmentImages != null ? segmentImages.Length : 0;
            if (count == 0)
                return;

            float slice = 360f / count;
            int index = Mathf.FloorToInt((angle + slice * 0.5f) / slice) % count;
            if (index != _selectedIndex)
            {
                UpdateSelection(index, false);
            }
        }

        public void ToggleExpanded()
        {
            SetExpanded(!_isExpanded);
        }

        public void SetExpanded(bool expanded)
        {
            if (_isExpanded == expanded)
                return;

            _isExpanded = expanded;
            if (_animationRoutine != null)
                StopCoroutine(_animationRoutine);
            _animationRoutine = StartCoroutine(AnimateExpanded(expanded));
        }

        private IEnumerator AnimateExpanded(bool expanded)
        {
            float time = 0f;
            float duration = Mathf.Max(0.01f, expandDuration);

            if (expandedRoot != null)
                expandedRoot.gameObject.SetActive(true);
            if (collapsedRoot != null)
                collapsedRoot.gameObject.SetActive(true);

            float startExpandedAlpha = expandedGroup != null ? expandedGroup.alpha : (expanded ? 0f : 1f);
            float endExpandedAlpha = expanded ? 1f : 0f;
            float startCollapsedAlpha = collapsedGroup != null ? collapsedGroup.alpha : (expanded ? 1f : 0f);
            float endCollapsedAlpha = expanded ? 0f : 1f;

            Vector3 startExpandedScale = expandedRoot != null ? expandedRoot.localScale : Vector3.one;
            Vector3 endExpandedScale = expanded ? expandedScale : collapsedScale;
            Vector3 startCollapsedScale = collapsedRoot != null ? collapsedRoot.localScale : Vector3.one;
            Vector3 endCollapsedScale = expanded ? Vector3.one * 0.8f : Vector3.one;

            while (time < duration)
            {
                float t = expandCurve.Evaluate(time / duration);
                if (expandedGroup != null) expandedGroup.alpha = Mathf.Lerp(startExpandedAlpha, endExpandedAlpha, t);
                if (collapsedGroup != null) collapsedGroup.alpha = Mathf.Lerp(startCollapsedAlpha, endCollapsedAlpha, t);
                if (expandedRoot != null) expandedRoot.localScale = Vector3.Lerp(startExpandedScale, endExpandedScale, t);
                if (collapsedRoot != null) collapsedRoot.localScale = Vector3.Lerp(startCollapsedScale, endCollapsedScale, t);
                time += Time.unscaledDeltaTime;
                yield return null;
            }

            ApplyExpandedState(expanded, false);
            _animationRoutine = null;
        }

        private void ApplyExpandedState(bool expanded, bool immediate)
        {
            _isExpanded = expanded;

            if (expandedGroup != null)
            {
                expandedGroup.alpha = expanded ? 1f : 0f;
                expandedGroup.blocksRaycasts = expanded;
                expandedGroup.interactable = expanded;
            }
            if (collapsedGroup != null)
            {
                collapsedGroup.alpha = expanded ? 0f : 1f;
                collapsedGroup.blocksRaycasts = !expanded;
                collapsedGroup.interactable = !expanded;
            }

            if (expandedRoot != null)
                expandedRoot.localScale = expanded ? expandedScale : collapsedScale;
            if (collapsedRoot != null)
                collapsedRoot.localScale = expanded ? Vector3.one * 0.8f : Vector3.one;

            if (!immediate && expandedRoot != null)
                expandedRoot.gameObject.SetActive(true);
            if (!immediate && collapsedRoot != null)
                collapsedRoot.gameObject.SetActive(true);
        }

        private void WireButtons()
        {
            if (segmentButtons != null)
            {
                for (int i = 0; i < segmentButtons.Length; i++)
                {
                    int index = i;
                    if (segmentButtons[i] == null) continue;
                    segmentButtons[i].onClick.RemoveAllListeners();
                    segmentButtons[i].onClick.AddListener(() =>
                    {
                        if (!allowMouseClick) return;
                        UpdateSelection(index, executeOnClick);
                    });
                }
            }

            if (previousPageButton != null)
            {
                previousPageButton.onClick.RemoveAllListeners();
                previousPageButton.onClick.AddListener(PreviousPage);
            }

            if (nextPageButton != null)
            {
                nextPageButton.onClick.RemoveAllListeners();
                nextPageButton.onClick.AddListener(NextPage);
            }
        }

        private void UpdateSelection(int index, bool execute)
        {
            int maxIndex = Mathf.Max(0, _itemsPerPage - 1);
            _selectedIndex = Mathf.Clamp(index, 0, maxIndex);

            if (segmentImages != null)
            {
                for (int i = 0; i < segmentImages.Length; i++)
                {
                    if (segmentImages[i] == null) continue;
                    segmentImages[i].color = (i == _selectedIndex) ? segmentSelected : segmentNormal;
                }
            }

            UpdateCenterPanel();

            if (execute)
                ExecuteSelected();
        }

        private void UpdateCenterPanel()
        {
            var command = GetCommandForIndex(_selectedIndex);
            if (command != null)
            {
                if (centerTitle != null)
                    centerTitle.text = "COMMAND";
                if (centerCommandLabel != null)
                    centerCommandLabel.text = command.displayName;
                if (centerTargetLabel != null)
                    centerTargetLabel.text = command.targetDisplayName;
            }
            else
            {
                if (centerTitle != null)
                    centerTitle.text = "COMMAND";
                if (centerCommandLabel != null)
                    centerCommandLabel.text = "N/A";
                if (centerTargetLabel != null)
                    centerTargetLabel.text = "";
            }

            int totalPages = GetTotalPages();
            if (pageLabel != null)
                pageLabel.text = $"{Mathf.Clamp(_pageIndex + 1, 1, totalPages)} / {Mathf.Max(1, totalPages)}";

            if (statFills != null && statFills.Length > 0)
            {
                float t = _itemsPerPage <= 1 ? 1f : (_selectedIndex + 1f) / _itemsPerPage;
                for (int i = 0; i < statFills.Length; i++)
                {
                    if (statFills[i] == null) continue;
                    float barValue = Mathf.Clamp01(t * (0.7f + 0.15f * i));
                    statFills[i].fillAmount = barValue;
                    statFills[i].color = segmentSelected;
                }
            }
        }

        private void RefreshCommands()
        {
            _commands.Clear();

            var registry = VoiceCommandRegistry.Instance ?? FindObjectOfType<VoiceCommandRegistry>();
            if (registry == null)
            {
                UpdateSegments();
                return;
            }

            if (autoDiscoverTargets)
                registry.DiscoverTargets();

            var displayNames = registry.Targets.ToDictionary(k => k.Key, v => v.Value.DisplayName);
            var commands = registry.GetAllCommands();

            foreach (var cmd in commands)
            {
                bool requires = cmd.Parameters != null && cmd.Parameters.Any(p => p.Required);
                if (!includeParameterizedCommandsInWheel && requires)
                    continue;

                string targetDisplay = displayNames.TryGetValue(cmd.TargetName, out var display)
                    ? display
                    : cmd.TargetName;

                _commands.Add(new CommandEntry
                {
                    targetId = cmd.TargetName,
                    commandName = cmd.Name,
                    displayName = ToDisplayName(cmd.Name),
                    targetDisplayName = targetDisplay,
                    description = cmd.Description,
                    requiresParams = requires
                });
            }

            _pageIndex = 0;
            UpdateSegments();
        }

        private void HandleRegistryUpdated()
        {
            if (!_ready)
                return;
            RefreshCommands();
        }

        private void UpdateSegments()
        {
            _itemsPerPage = segmentImages != null ? segmentImages.Length : 0;
            int start = _pageIndex * Mathf.Max(1, _itemsPerPage);

            for (int i = 0; i < _itemsPerPage; i++)
            {
                var command = GetCommandAt(start + i);
                bool hasCommand = command != null;

                if (segmentImages != null && i < segmentImages.Length && segmentImages[i] != null)
                {
                    segmentImages[i].color = hasCommand ? segmentNormal : segmentDim;
                }
                if (segmentButtons != null && i < segmentButtons.Length && segmentButtons[i] != null)
                {
                    segmentButtons[i].interactable = hasCommand;
                }
                if (segmentLabels != null && i < segmentLabels.Length && segmentLabels[i] != null)
                {
                    segmentLabels[i].text = hasCommand ? GetSegmentLabel(command).ToUpperInvariant() : "";
                }
                if (segmentIconLabels != null && i < segmentIconLabels.Length && segmentIconLabels[i] != null)
                {
                    segmentIconLabels[i].text = hasCommand ? GetBadge(command.targetId) : "";
                }
            }

            _selectedIndex = 0;
            UpdateCenterPanel();
        }

        private void ExecuteSelected()
        {
            var command = GetCommandForIndex(_selectedIndex);
            if (command == null)
                return;

            var registry = VoiceCommandRegistry.Instance ?? FindObjectOfType<VoiceCommandRegistry>();
            if (registry == null)
                return;

            registry.ExecuteCommand(command.targetId, command.commandName, new Dictionary<string, object>());
        }

        private void NextPage()
        {
            int totalPages = GetTotalPages();
            if (totalPages <= 1)
                return;

            _pageIndex = (_pageIndex + 1) % totalPages;
            UpdateSegments();
        }

        private void PreviousPage()
        {
            int totalPages = GetTotalPages();
            if (totalPages <= 1)
                return;

            _pageIndex = (_pageIndex - 1 + totalPages) % totalPages;
            UpdateSegments();
        }

        private int GetTotalPages()
        {
            if (_itemsPerPage <= 0)
                return 1;
            return Mathf.Max(1, Mathf.CeilToInt(_commands.Count / (float)_itemsPerPage));
        }

        private CommandEntry GetCommandForIndex(int segmentIndex)
        {
            int index = _pageIndex * Mathf.Max(1, _itemsPerPage) + segmentIndex;
            return GetCommandAt(index);
        }

        private CommandEntry GetCommandAt(int index)
        {
            if (index < 0 || index >= _commands.Count)
                return null;
            return _commands[index];
        }

        private string ToDisplayName(string commandName)
        {
            if (string.IsNullOrEmpty(commandName))
                return "Command";

            string spaced = commandName.Replace("_", " ");
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(spaced);
        }

        private string GetSegmentLabel(CommandEntry command)
        {
            if (command == null || string.IsNullOrEmpty(command.commandName))
                return "CMD";

            string[] parts = command.commandName.Split('_');
            string verb = parts.Length > 0 ? parts[0] : "";
            string subject = parts.Length > 1 ? parts[1] : "";

            string subjectLabel = subject switch
            {
                "range" => "RNG",
                "tilt" => "TILT",
                "gain" => "GAIN",
                "mode" => "MODE",
                "panel" => "PNL",
                "traffic" => "TFC",
                "weather" => "WX",
                "all" => "ALL",
                "opacity" => "OPC",
                "nearby" => "NBY",
                "proximity" => "PROX",
                "color" => "CLR",
                _ => subject.Length > 0 ? subject.ToUpperInvariant() : "CMD"
            };

            return verb switch
            {
                "increase" => $"{subjectLabel}+",
                "decrease" => $"{subjectLabel}-",
                "toggle" => $"{subjectLabel} TOG",
                "show" => $"{subjectLabel} ON",
                "hide" => $"{subjectLabel} OFF",
                "set" => $"{subjectLabel} SET",
                "clear" => "CLR",
                "refresh" => "REF",
                "reinitialize" => "INIT",
                _ => subjectLabel
            };
        }

        private string GetBadge(string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
                return "CMD";

            switch (targetId)
            {
                case "weather_radar": return "WX";
                case "traffic_radar": return "TFC";
                case "indicator_system": return "IND";
                case "symbology": return "SYM";
                case "visionbriefing": return "VIS";
                default:
                    return targetId.Substring(0, Mathf.Min(3, targetId.Length)).ToUpperInvariant();
            }
        }
    }
}
