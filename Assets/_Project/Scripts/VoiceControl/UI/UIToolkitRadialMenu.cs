using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using VoiceControl.Core;

namespace VoiceControl.UI
{
    /// <summary>
    /// Responsive radial menu using Unity UI Toolkit with rich animations and effects.
    /// Integrates with the voice control system to display and execute commands.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("Voice Control/UI Toolkit/Radial Menu")]
    public class UIToolkitRadialMenu : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset menuUXML;

        [Header("Input")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
        [SerializeField] private KeyCode closeKey = KeyCode.Escape;
        [SerializeField] private bool useGamepadInput = true;
        [SerializeField] private string horizontalAxis = "Horizontal";
        [SerializeField] private string verticalAxis = "Vertical";
        [SerializeField] private float gamepadDeadzone = 0.35f;

        [Header("Menu Configuration")]
        [SerializeField] private int segmentCount = 8;
        [SerializeField] private float innerRadius = 60f;
        [SerializeField] private float outerRadius = 200f;
        [SerializeField] private bool autoDiscoverCommands = true;
        [SerializeField] private bool groupByTarget = true;

        [Header("Animation")]
        [SerializeField] private float expandDuration = 0.35f;
        [SerializeField] private float collapseDuration = 0.25f;
        [SerializeField] private float segmentStaggerDelay = 0.03f;
        [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float selectScale = 1.15f;
        [SerializeField] private AnimationCurve expandCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve elasticCurve;

        [Header("Visual Effects")]
        [SerializeField] private bool useGlowEffect = true;
        [SerializeField] private float glowIntensity = 0.5f;
        [SerializeField] private bool useParticles = true;
        [SerializeField] private int particleCount = 12;

        [Header("Colors")]
        [SerializeField] private Color[] targetColors = new Color[]
        {
            new Color(0.2f, 0.8f, 1f, 1f),      // Weather - Cyan
            new Color(1f, 0.6f, 0.2f, 1f),       // Traffic - Orange
            new Color(0.4f, 0.9f, 0.4f, 1f),     // Indicators - Green
            new Color(0.9f, 0.4f, 0.9f, 1f),     // Symbology - Purple
            new Color(1f, 0.8f, 0.2f, 1f),       // Vision - Yellow
        };

        // Events
        public event Action<CommandInfo> OnCommandSelected;
        public event Action OnMenuOpened;
        public event Action OnMenuClosed;

        // State
        private VisualElement _root;
        private VisualElement _menuContainer;
        private VisualElement _centerPanel;
        private VisualElement _particleContainer;
        private Label _centerTitle;
        private Label _centerDescription;
        private Label _centerTarget;
        private List<RadialSegment> _segments = new List<RadialSegment>();
        private List<CommandInfo> _commands = new List<CommandInfo>();
        private bool _isOpen;
        private int _selectedIndex = -1;
        private float _currentRotation;
        private Dictionary<string, int> _targetColorIndex = new Dictionary<string, int>();

        // Animation state
        private float _expandProgress;
        private bool _isAnimating;
        private bool _isRefreshingCommands; // Prevent recursive RefreshCommands calls
        private List<ParticleElement> _particles = new List<ParticleElement>();

        [System.Serializable]
        public class CommandInfo
        {
            public string TargetId;
            public string TargetName;
            public string CommandName;
            public string DisplayName;
            public string Description;
            public string Icon;
            public Color AccentColor;
            public bool RequiresParameters;
        }

        private class RadialSegment
        {
            public VisualElement Container;
            public VisualElement Background;
            public VisualElement Glow;
            public Label IconLabel;
            public Label CommandLabel;
            public int Index;
            public float StartAngle;
            public float EndAngle;
            public CommandInfo Command;
            public bool IsHovered;
            public Vector2 CenterPosition;
        }

        private class ParticleElement
        {
            public VisualElement Element;
            public float Angle;
            public float Speed;
            public float Distance;
            public float Alpha;
        }

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            // Initialize elastic curve
            if (elasticCurve == null || elasticCurve.length == 0)
            {
                elasticCurve = new AnimationCurve(
                    new Keyframe(0, 0, 0, 0),
                    new Keyframe(0.6f, 1.1f, 0, 0),
                    new Keyframe(1, 1, 0, 0)
                );
            }
        }

        private void OnEnable()
        {
            SetupUI();
            RefreshCommands();

            var registry = VoiceCommandRegistry.Instance;
            if (registry != null)
                registry.OnRegistryUpdated += OnRegistryUpdated;
        }

        private void OnDisable()
        {
            var registry = VoiceCommandRegistry.Instance;
            if (registry != null)
                registry.OnRegistryUpdated -= OnRegistryUpdated;
        }

        private void Update()
        {
            HandleInput();

            if (_isOpen)
            {
                UpdateSelection();
                UpdateAnimations();
                if (useParticles)
                    UpdateParticles();
            }
        }

        private void SetupUI()
        {
            if (uiDocument == null) return;

            _root = uiDocument.rootVisualElement;
            if (_root == null) return;

            // Create main container
            _menuContainer = new VisualElement();
            _menuContainer.AddToClassList("radial-menu-container");
            _menuContainer.pickingMode = PickingMode.Ignore;
            _root.Add(_menuContainer);

            // Create particle container (behind segments)
            _particleContainer = new VisualElement();
            _particleContainer.AddToClassList("particle-container");
            _particleContainer.pickingMode = PickingMode.Ignore;
            _menuContainer.Add(_particleContainer);

            // Create segments
            CreateSegments();

            // Create center panel
            CreateCenterPanel();

            // Apply initial styles
            ApplyStyles();

            // Initially hidden
            SetMenuOpen(false, true);
        }

        private void CreateSegments()
        {
            float angleStep = 360f / segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                float startAngle = i * angleStep;
                float endAngle = (i + 1) * angleStep;
                float midAngle = startAngle + angleStep / 2;

                var segment = new RadialSegment
                {
                    Index = i,
                    StartAngle = startAngle,
                    EndAngle = endAngle,
                    Container = new VisualElement(),
                    Background = new VisualElement(),
                    Glow = new VisualElement(),
                    IconLabel = new Label(),
                    CommandLabel = new Label()
                };

                // Setup container
                segment.Container.AddToClassList("segment-container");
                segment.Container.pickingMode = PickingMode.Position;

                // Setup background (segment slice)
                segment.Background.AddToClassList("segment-background");
                segment.Container.Add(segment.Background);

                // Setup glow effect
                if (useGlowEffect)
                {
                    segment.Glow.AddToClassList("segment-glow");
                    segment.Glow.style.opacity = 0;
                    segment.Container.Add(segment.Glow);
                }

                // Setup icon label
                segment.IconLabel.AddToClassList("segment-icon");
                segment.IconLabel.text = "○";
                segment.Container.Add(segment.IconLabel);

                // Setup command label
                segment.CommandLabel.AddToClassList("segment-label");
                segment.CommandLabel.text = $"CMD{i + 1}";
                segment.Container.Add(segment.CommandLabel);

                // Position segment
                PositionSegment(segment, midAngle);

                // Add hover events
                int index = i;
                segment.Container.RegisterCallback<MouseEnterEvent>(evt => OnSegmentHover(index, true));
                segment.Container.RegisterCallback<MouseLeaveEvent>(evt => OnSegmentHover(index, false));
                segment.Container.RegisterCallback<ClickEvent>(evt => OnSegmentClick(index));

                _menuContainer.Add(segment.Container);
                _segments.Add(segment);
            }
        }

        private void PositionSegment(RadialSegment segment, float angle)
        {
            float midAngleRad = angle * Mathf.Deg2Rad;
            float midRadius = (innerRadius + outerRadius) / 2;

            segment.CenterPosition = new Vector2(
                Mathf.Cos(midAngleRad) * midRadius,
                Mathf.Sin(midAngleRad) * midRadius
            );

            // Position will be set during animation
            segment.Container.style.position = Position.Absolute;
            segment.Container.style.width = (outerRadius - innerRadius);
            segment.Container.style.height = 60;
        }

        private void CreateCenterPanel()
        {
            _centerPanel = new VisualElement();
            _centerPanel.AddToClassList("center-panel");

            _centerTitle = new Label();
            _centerTitle.AddToClassList("center-title");
            _centerTitle.text = "VOICE CONTROL";
            _centerPanel.Add(_centerTitle);

            _centerDescription = new Label();
            _centerDescription.AddToClassList("center-description");
            _centerDescription.text = "Select a command";
            _centerPanel.Add(_centerDescription);

            _centerTarget = new Label();
            _centerTarget.AddToClassList("center-target");
            _centerPanel.Add(_centerTarget);

            _menuContainer.Add(_centerPanel);
        }

        private void ApplyStyles()
        {
            // Load or create stylesheet
            var stylesheet = Resources.Load<StyleSheet>("VoiceControl/RadialMenuStyles");
            if (stylesheet != null)
            {
                _root.styleSheets.Add(stylesheet);
            }
            else
            {
                ApplyInlineStyles();
            }
        }

        private void ApplyInlineStyles()
        {
            // Menu container styles
            _menuContainer.style.position = Position.Absolute;
            _menuContainer.style.left = new StyleLength(new Length(50, LengthUnit.Percent));
            _menuContainer.style.top = new StyleLength(new Length(50, LengthUnit.Percent));
            _menuContainer.style.width = 0;
            _menuContainer.style.height = 0;

            // Segment styles
            foreach (var segment in _segments)
            {
                segment.Container.style.backgroundColor = new Color(0.15f, 0.18f, 0.22f, 0.9f);
                segment.Container.style.borderTopLeftRadius = 8;
                segment.Container.style.borderTopRightRadius = 8;
                segment.Container.style.borderBottomLeftRadius = 8;
                segment.Container.style.borderBottomRightRadius = 8;

                segment.IconLabel.style.fontSize = 24;
                segment.IconLabel.style.color = Color.white;
                segment.IconLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

                segment.CommandLabel.style.fontSize = 12;
                segment.CommandLabel.style.color = new Color(0.8f, 0.85f, 0.9f, 1f);
                segment.CommandLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            }

            // Center panel styles
            _centerPanel.style.position = Position.Absolute;
            _centerPanel.style.width = innerRadius * 2;
            _centerPanel.style.height = innerRadius * 2;
            _centerPanel.style.left = -innerRadius;
            _centerPanel.style.top = -innerRadius;
            _centerPanel.style.backgroundColor = new Color(0.1f, 0.12f, 0.15f, 0.95f);
            _centerPanel.style.borderTopLeftRadius = innerRadius;
            _centerPanel.style.borderTopRightRadius = innerRadius;
            _centerPanel.style.borderBottomLeftRadius = innerRadius;
            _centerPanel.style.borderBottomRightRadius = innerRadius;

            _centerTitle.style.fontSize = 14;
            _centerTitle.style.color = new Color(0.2f, 0.8f, 1f, 1f);
            _centerTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _centerTitle.style.marginTop = innerRadius - 40;

            _centerDescription.style.fontSize = 11;
            _centerDescription.style.color = new Color(0.7f, 0.75f, 0.8f, 1f);
            _centerDescription.style.unityTextAlign = TextAnchor.MiddleCenter;

            _centerTarget.style.fontSize = 10;
            _centerTarget.style.color = new Color(0.5f, 0.55f, 0.6f, 1f);
            _centerTarget.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        private void CreateParticles()
        {
            if (!useParticles) return;

            for (int i = 0; i < particleCount; i++)
            {
                var particle = new VisualElement();
                particle.AddToClassList("particle");
                particle.style.width = 4;
                particle.style.height = 4;
                particle.style.backgroundColor = Color.white;
                particle.style.borderTopLeftRadius = 2;
                particle.style.borderTopRightRadius = 2;
                particle.style.borderBottomLeftRadius = 2;
                particle.style.borderBottomRightRadius = 2;

                var p = new ParticleElement
                {
                    Element = particle,
                    Angle = UnityEngine.Random.Range(0f, 360f),
                    Speed = UnityEngine.Random.Range(10f, 30f),
                    Distance = innerRadius + UnityEngine.Random.Range(0f, outerRadius - innerRadius),
                    Alpha = UnityEngine.Random.Range(0.3f, 0.8f)
                };

                particle.style.opacity = p.Alpha;
                _particleContainer.Add(particle);
                _particles.Add(p);
            }
        }

        private void UpdateParticles()
        {
            foreach (var particle in _particles)
            {
                particle.Angle += particle.Speed * Time.unscaledDeltaTime;
                float rad = particle.Angle * Mathf.Deg2Rad;

                float x = Mathf.Cos(rad) * particle.Distance * _expandProgress;
                float y = Mathf.Sin(rad) * particle.Distance * _expandProgress;

                particle.Element.style.left = x - 2;
                particle.Element.style.top = y - 2;

                // Pulse alpha
                float alpha = particle.Alpha * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f + particle.Angle));
                particle.Element.style.opacity = alpha * _expandProgress;
            }
        }

        private void RefreshCommands()
        {
            // Prevent recursive calls (DiscoverTargets triggers OnRegistryUpdated)
            if (_isRefreshingCommands) return;
            _isRefreshingCommands = true;

            try
            {
                _commands.Clear();

                var registry = VoiceCommandRegistry.Instance;
                if (registry == null)
                {
                    // Add demo commands for testing
                    AddDemoCommands();
                    return;
                }

                if (autoDiscoverCommands)
                    registry.DiscoverTargets();

                var displayNames = registry.Targets.ToDictionary(k => k.Key, v => v.Value.DisplayName);
                var commands = registry.GetAllCommands();

                int colorIndex = 0;
                foreach (var cmd in commands)
                {
                    // Skip parameterized commands for radial menu
                    bool requiresParams = cmd.Parameters != null && cmd.Parameters.Any(p => p.Required);

                    string targetId = cmd.TargetName;
                    if (!_targetColorIndex.ContainsKey(targetId))
                    {
                        _targetColorIndex[targetId] = colorIndex % targetColors.Length;
                        colorIndex++;
                    }

                    _commands.Add(new CommandInfo
                    {
                        TargetId = targetId,
                        TargetName = displayNames.GetValueOrDefault(targetId, targetId),
                        CommandName = cmd.Name,
                        DisplayName = FormatDisplayName(cmd.Name),
                        Description = cmd.Description,
                        Icon = GetIconForCommand(cmd.Name),
                        AccentColor = targetColors[_targetColorIndex[targetId]],
                        RequiresParameters = requiresParams
                    });
                }

                AssignCommandsToSegments();
            }
            finally
            {
                _isRefreshingCommands = false;
            }
        }

        private void AddDemoCommands()
        {
            string[] demoTargets = { "weather_radar", "traffic_radar", "indicator_system", "symbology" };
            string[] demoCommands = { "increase_range", "decrease_range", "toggle_mode", "show_panel", "hide_panel" };
            string[] demoIcons = { "▲", "▼", "◆", "□", "○" };

            int colorIndex = 0;
            int cmdIndex = 0;
            foreach (var target in demoTargets)
            {
                for (int i = 0; i < 2 && cmdIndex < segmentCount; i++)
                {
                    _commands.Add(new CommandInfo
                    {
                        TargetId = target,
                        TargetName = target.Replace("_", " ").ToUpper(),
                        CommandName = demoCommands[cmdIndex % demoCommands.Length],
                        DisplayName = FormatDisplayName(demoCommands[cmdIndex % demoCommands.Length]),
                        Description = $"Execute {demoCommands[cmdIndex % demoCommands.Length]} on {target}",
                        Icon = demoIcons[cmdIndex % demoIcons.Length],
                        AccentColor = targetColors[colorIndex % targetColors.Length],
                        RequiresParameters = false
                    });
                    cmdIndex++;
                }
                colorIndex++;
            }

            AssignCommandsToSegments();
        }

        private void AssignCommandsToSegments()
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                segment.Command = i < _commands.Count ? _commands[i] : null;

                if (segment.Command != null)
                {
                    segment.IconLabel.text = segment.Command.Icon;
                    segment.CommandLabel.text = segment.Command.DisplayName;
                    segment.Background.style.backgroundColor = segment.Command.AccentColor * 0.3f;
                    segment.Container.style.opacity = 1;
                    segment.Container.SetEnabled(true);
                }
                else
                {
                    segment.IconLabel.text = "";
                    segment.CommandLabel.text = "";
                    segment.Background.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
                    segment.Container.style.opacity = 0.3f;
                    segment.Container.SetEnabled(false);
                }
            }
        }

        private void HandleInput()
        {
            // Toggle menu
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleMenu();
            }

            // Close menu
            if (_isOpen && Input.GetKeyDown(closeKey))
            {
                SetMenuOpen(false);
            }

            // Gamepad input
            if (_isOpen && useGamepadInput)
            {
                Vector2 stick = new Vector2(Input.GetAxis(horizontalAxis), Input.GetAxis(verticalAxis));
                if (stick.magnitude > gamepadDeadzone)
                {
                    float angle = Mathf.Atan2(stick.y, stick.x) * Mathf.Rad2Deg;
                    angle = NormalizeAngle(angle);

                    int selectedIndex = GetSegmentIndexFromAngle(angle);
                    if (selectedIndex != _selectedIndex)
                    {
                        SelectSegment(selectedIndex);
                    }
                }

                // Execute on button press
                if (Input.GetButtonDown("Submit") && _selectedIndex >= 0)
                {
                    ExecuteSelectedCommand();
                }
            }
        }

        private void UpdateSelection()
        {
            // Mouse-based selection is handled by hover events
        }

        private void UpdateAnimations()
        {
            if (!_isAnimating) return;

            float targetProgress = _isOpen ? 1f : 0f;
            float speed = _isOpen ? 1f / expandDuration : 1f / collapseDuration;

            _expandProgress = Mathf.MoveTowards(_expandProgress, targetProgress, Time.unscaledDeltaTime * speed);

            float curvedProgress = expandCurve.Evaluate(_expandProgress);

            // Animate segments
            for (int i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                float staggerOffset = i * segmentStaggerDelay;
                float segmentProgress = Mathf.Clamp01((_expandProgress - staggerOffset) / (1f - staggerOffset));
                float segmentCurved = expandCurve.Evaluate(segmentProgress);

                if (_isOpen)
                {
                    float elastic = elasticCurve.Evaluate(segmentCurved);
                    float scale = _selectedIndex == i ? selectScale : 1f;
                    scale *= elastic;

                    segment.Container.style.scale = new Scale(new Vector3(scale, scale, 1));
                    segment.Container.style.opacity = segmentCurved;

                    // Position with expansion
                    Vector2 pos = segment.CenterPosition * elastic;
                    segment.Container.style.left = pos.x - (outerRadius - innerRadius) / 2;
                    segment.Container.style.top = pos.y - 30;
                }
                else
                {
                    segment.Container.style.opacity = segmentCurved;
                    segment.Container.style.scale = new Scale(Vector3.one * (0.5f + 0.5f * segmentCurved));
                }
            }

            // Animate center panel
            if (_centerPanel != null)
            {
                float centerScale = 0.8f + 0.2f * curvedProgress;
                _centerPanel.style.scale = new Scale(new Vector3(centerScale, centerScale, 1));
                _centerPanel.style.opacity = curvedProgress;
            }

            // Check animation complete
            if (Mathf.Approximately(_expandProgress, targetProgress))
            {
                _isAnimating = false;
                if (!_isOpen)
                {
                    _menuContainer.style.display = DisplayStyle.None;
                }
            }
        }

        public void ToggleMenu()
        {
            SetMenuOpen(!_isOpen);
        }

        public void SetMenuOpen(bool open, bool immediate = false)
        {
            if (_isOpen == open) return;

            _isOpen = open;

            if (_isOpen)
            {
                _menuContainer.style.display = DisplayStyle.Flex;
                RefreshCommands();
                CreateParticles();
                OnMenuOpened?.Invoke();
            }
            else
            {
                ClearParticles();
                OnMenuClosed?.Invoke();
            }

            if (immediate)
            {
                _expandProgress = _isOpen ? 1f : 0f;
                _isAnimating = false;
                UpdateAnimations();
            }
            else
            {
                _isAnimating = true;
            }
        }

        private void OnSegmentHover(int index, bool hovered)
        {
            if (!_isOpen || !_segments[index].Container.enabledSelf) return;

            _segments[index].IsHovered = hovered;

            if (hovered)
            {
                SelectSegment(index);
            }
            else if (_selectedIndex == index)
            {
                _selectedIndex = -1;
                UpdateCenterPanel(null);
            }
        }

        private void OnSegmentClick(int index)
        {
            if (!_isOpen || _segments[index].Command == null) return;

            SelectSegment(index);
            ExecuteSelectedCommand();
        }

        private void SelectSegment(int index)
        {
            if (_selectedIndex == index) return;

            // Deselect previous
            if (_selectedIndex >= 0 && _selectedIndex < _segments.Count)
            {
                var prevSegment = _segments[_selectedIndex];
                prevSegment.IsHovered = false;
                prevSegment.Container.RemoveFromClassList("segment-selected");

                if (useGlowEffect)
                {
                    prevSegment.Glow.style.opacity = 0;
                    prevSegment.Glow.style.scale = new Scale(Vector3.one);
                }
            }

            _selectedIndex = index;
            var segment = _segments[index];
            segment.Container.AddToClassList("segment-selected");

            // Apply glow effect
            if (useGlowEffect)
            {
                segment.Glow.style.opacity = glowIntensity;
                segment.Glow.style.scale = new Scale(new Vector3(1.1f, 1.1f, 1));
            }

            UpdateCenterPanel(segment.Command);
        }

        private void UpdateCenterPanel(CommandInfo command)
        {
            if (command != null)
            {
                _centerTitle.text = command.DisplayName.ToUpper();
                _centerDescription.text = command.Description;
                _centerTarget.text = command.TargetName;
                _centerTarget.style.color = command.AccentColor;
            }
            else
            {
                _centerTitle.text = "VOICE CONTROL";
                _centerDescription.text = "Select a command";
                _centerTarget.text = "";
            }
        }

        private void ExecuteSelectedCommand()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _segments.Count) return;

            var segment = _segments[_selectedIndex];
            if (segment.Command == null) return;

            // Visual feedback
            segment.Background.style.backgroundColor = Color.white;

            // Execute command
            OnCommandSelected?.Invoke(segment.Command);

            var registry = VoiceCommandRegistry.Instance;
            if (registry != null)
            {
                registry.ExecuteCommand(segment.Command.TargetId, segment.Command.CommandName, null);
            }

            // Close menu after execution
            SetMenuOpen(false);
        }

        private void ClearParticles()
        {
            _particleContainer.Clear();
            _particles.Clear();
        }

        private void OnRegistryUpdated()
        {
            RefreshCommands();
        }

        private int GetSegmentIndexFromAngle(float angle)
        {
            float angleStep = 360f / segmentCount;
            float adjustedAngle = NormalizeAngle(angle + angleStep / 2);
            return Mathf.FloorToInt(adjustedAngle / angleStep) % segmentCount;
        }

        private float NormalizeAngle(float angle)
        {
            angle = angle % 360f;
            if (angle < 0) angle += 360f;
            return angle;
        }

        private string FormatDisplayName(string commandName)
        {
            if (string.IsNullOrEmpty(commandName))
                return "Command";

            // Convert snake_case to Title Case
            string[] parts = commandName.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join(" ", parts);
        }

        private string GetIconForCommand(string commandName)
        {
            if (commandName.Contains("increase")) return "▲";
            if (commandName.Contains("decrease")) return "▼";
            if (commandName.Contains("toggle")) return "◆";
            if (commandName.Contains("show")) return "□";
            if (commandName.Contains("hide")) return "■";
            if (commandName.Contains("set")) return "●";
            if (commandName.Contains("clear")) return "✕";
            if (commandName.Contains("refresh")) return "↻";
            return "○";
        }

        // Public API
        public bool IsOpen => _isOpen;
        public IReadOnlyList<CommandInfo> Commands => _commands;

        public void SetCommandEnabled(string commandName, bool enabled)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i].Command != null && _segments[i].Command.CommandName == commandName)
                {
                    _segments[i].Container.SetEnabled(enabled);
                    break;
                }
            }
        }
    }
}
