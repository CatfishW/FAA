using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using VoiceControl.Core;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VoiceControl.UI
{
    /// <summary>
    /// Advanced radial menu with hierarchical sub-menus, gesture support,
    /// and rich visual effects using Unity UI Toolkit.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("Voice Control/UI Toolkit/Advanced Radial Menu")]
    [ExecuteInEditMode]
    public class UIToolkitRadialMenuAdvanced : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Menu Structure")]
        [SerializeField] private int mainSegmentCount = 4;
        [SerializeField] private float innerRadius = 190f;
        [SerializeField] private float middleRadius = 390f;
        [SerializeField] private float outerRadius = 580f;
        [SerializeField] private bool enableSubMenus = true;
        [SerializeField] private bool startCollapsed = true;
        [SerializeField] private float collapsedButtonSize = 80f;
        [SerializeField] private Vector2 collapsedButtonPosition = new Vector2(100f, 100f);  // Bottom-left offset

        [Header("Input")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
        [SerializeField] private KeyCode closeKey = KeyCode.Escape;
        [SerializeField] private bool useMouseWheel = true;
        [SerializeField] private bool useGestures = true;
        [SerializeField] private float gestureSensitivity = 1.5f;

        [Header("Animation")]
        [SerializeField] private float openDuration = 0.4f;
        [SerializeField] private float closeDuration = 0.25f;
        [SerializeField] private float subMenuExpandDuration = 0.25f;
        [SerializeField] private AnimationCurve springCurve;
        [SerializeField] private AnimationCurve bounceCurve;

        [Header("Visual Effects")]
        [SerializeField] private bool useRippleEffect = true;
        [SerializeField] private bool usePulseAnimation = true;
        [SerializeField] private bool useGradientBackground = true;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField, Range(0.3f, 1f)] private float menuTransparency = 0.95f;
        [SerializeField, Range(0.5f, 1f)] private float ringBackgroundTransparency = 0.92f;
        [SerializeField, Range(0.5f, 1f)] private float segmentTransparency = 0.96f;
        [SerializeField, Range(0.7f, 1f)] private float centerTransparency = 0.98f;

        [Header("Audio Feedback")]
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;
        [SerializeField] private AudioClip selectSound;
        [SerializeField] private AudioClip executeSound;

        private const float MainSegmentWidth = 210f;
        private const float MainSegmentHeight = 120f;
        private const float SubSegmentWidth = 165f;
        private const float SubSegmentHeight = 92f;
        private const float MainIconContainerSize = 80f;
        private const float MainIconSize = 68f;
        private const float SubIconContainerSize = 48f;
        private const float SubIconSize = 36f;

        [Header("Typography")]
        [SerializeField] private float mainLabelFontSize = 18f;
        [SerializeField] private float subLabelFontSize = 14f;
        [SerializeField] private float centerTitleFontSize = 24f;
        [SerializeField] private float centerSubtitleFontSize = 16f;
        private static readonly Color SubBorderBaseColor = new Color(0.4f, 0.6f, 0.9f, 0.35f);
        private const float SubMenuSpreadDegrees = 70f;
        private const float SubMenuInnerOffset = 40f;
        private const float SubMenuOuterInset = 40f;
        private const float CenterSizePadding = 60f;

        // Events
        public event Action<MenuCommand> OnCommandExecuted;
        public event Action<string> OnCategoryChanged;
        public event Action OnMenuOpened;
        public event Action OnMenuClosed;

        // UI Elements
        private VisualElement _root;
        private VisualElement _menuRoot;
        private VisualElement _collapsedButton;  // Small circular button when collapsed
        private VisualElement _collapsedIcon;
        private VisualElement _ringBackground;
        private VisualElement _centerInfo;
        private Label _centerTitle;
        private Label _centerSubtitle;
        private VisualElement _rippleContainer;
        private VisualElement _gestureIndicator;

        // Menu state
        private List<MainSegment> _mainSegments = new List<MainSegment>();
        private List<SubSegment> _subSegments = new List<SubSegment>();
        private List<MenuCategory> _categories = new List<MenuCategory>();
        private int _selectedMainIndex = -1;
        private int _selectedSubIndex = -1;
        private bool _isOpen;
        private bool _isAnimating;
        private bool _subMenuOpen;
        private float _openProgress;
        private float _subMenuProgress;
        private float _rotationOffset;
        private Vector2 _lastMousePos;
        private float _gestureAccumulator;
        private bool _isLoadingCommands; // Prevent recursive LoadCommands calls

        // Category definitions for voice control commands - using FAA-styled icon paths
        private readonly Dictionary<string, (string iconPath, Color color)> _categoryDefs = new()
        {
            { "radar", (iconPath: "VoiceControl/IconsSvg/WeatherRadar", color: new Color(0.35f, 0.7f, 1f)) },
            { "indicator_system", (iconPath: "VoiceControl/IconsSvg/IndicatorSystem", color: new Color(0.3f, 0.8f, 0.5f)) },
            { "hud", (iconPath: "VoiceControl/IconsSvg/Symbology", color: new Color(0.35f, 0.9f, 0.55f)) },
            { "visionbriefing", (iconPath: "VoiceControl/IconsSvg/VisionBriefing", color: new Color(1f, 0.8f, 0.3f)) }
        };

        [Serializable]
        public class MenuCommand
        {
            public string Id;
            public string TargetId;
            public string CommandName;
            public string DisplayName;
            public string Description;
            public string Category;
            public string IconPath;
            public Color Color;
            public bool RequiresParams;
            public Dictionary<string, object> DefaultParams;
        }

        [Serializable]
        public class MenuCategory
        {
            public string Id;
            public string DisplayName;
            public string Icon;
            public Color Color;
            public List<MenuCommand> Commands = new List<MenuCommand>();
        }

        private class MainSegment
        {
            public VisualElement Container;
            public VisualElement Background;
            public VisualElement IconContainer;
            public VisualElement IconImage;  // Changed from Label to VisualElement for texture
            public Label NameLabel;
            public int Index;
            public float Angle;
            public MenuCategory Category;
            public bool IsHovered;
        }

        private class SubSegment
        {
            public VisualElement Container;
            public VisualElement Background;
            public VisualElement IconContainer;
            public VisualElement IconImage;
            public Label NameLabel;
            public int Index;
            public float Angle;
            public MenuCommand Command;
            public bool IsVisible;
        }

        private class Ripple
        {
            public VisualElement Element;
            public float Progress;
            public float Speed;
            public Color Color;
        }

        private List<Ripple> _ripples = new List<Ripple>();

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            InitializeCurves();
        }

        private void InitializeCurves()
        {
            if (springCurve == null || springCurve.length == 0)
            {
                springCurve = new AnimationCurve(
                    new Keyframe(0, 0, 0, 2),
                    new Keyframe(0.5f, 1.1f, 0, 0),
                    new Keyframe(1, 1, -0.5f, 0)
                );
            }

            if (bounceCurve == null || bounceCurve.length == 0)
            {
                bounceCurve = new AnimationCurve(
                    new Keyframe(0, 0, 0, 0),
                    new Keyframe(0.4f, 1.05f, 0, 0),
                    new Keyframe(0.7f, 0.95f, 0, 0),
                    new Keyframe(1, 1, 0, 0)
                );
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Hot-reload: refresh UI when properties change in editor
            if (!Application.isPlaying && uiDocument != null)
            {
                EditorApplication.delayCall += () =>
                {
                    if (this != null && uiDocument != null)
                    {
                        RefreshUI();
                    }
                };
            }
        }

        /// <summary>
        /// Refreshes the UI in edit mode for live preview.
        /// </summary>
        [ContextMenu("Refresh UI Preview")]
        public void RefreshUI()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            // Clean up existing UI
            CleanupUI();

            // Rebuild UI
            SetupUI();

            // Show preview state in editor
            if (!Application.isPlaying)
            {
                ShowEditorPreview();
            }
        }

        private void CleanupUI()
        {
            if (_root == null) return;

            // Remove all dynamically created elements
            _menuRoot?.RemoveFromHierarchy();
            _collapsedButton?.RemoveFromHierarchy();

            _mainSegments.Clear();
            _subSegments.Clear();
            _ripples.Clear();
        }

        private void ShowEditorPreview()
        {
            if (_menuRoot == null)
            {
                SetupUI();
            }

            if (_menuRoot == null)
            {
                return;
            }

            // In edit mode, show the menu open for preview
            _menuRoot.style.display = DisplayStyle.Flex;
            _openProgress = 1f;
            _isOpen = true;

            // Position segments for preview
            UpdateAnimations();

            // Hide collapsed button when showing preview
            if (_collapsedButton != null)
            {
                _collapsedButton.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Toggles the editor preview on/off.
        /// </summary>
        [ContextMenu("Toggle Editor Preview")]
        public void ToggleEditorPreview()
        {
            if (_isOpen)
            {
                SetMenuOpen(false);
            }
            else
            {
                ShowEditorPreview();
            }
        }
#endif

        private void OnEnable()
        {
            SetupUI();
            LoadCommands();

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
#if UNITY_EDITOR
            // In edit mode, just update animations for preview
            if (!Application.isPlaying)
            {
                if (_isOpen)
                {
                    UpdateAnimations();
                }
                return;
            }
#endif
            HandleInput();
            UpdateAnimations();

            if (_isOpen)
            {
                UpdateGestureRecognition();
                UpdateRipples();

                if (usePulseAnimation && _selectedMainIndex >= 0)
                {
                    UpdatePulseEffect();
                }
            }
        }

        private void SetupUI()
        {
            if (uiDocument == null) return;

            _root = uiDocument.rootVisualElement;
            if (_root == null) return;

            // Query for existing elements from UXML template or create new ones
            _collapsedButton = _root.Q<VisualElement>("CollapsedButton");
            if (_collapsedButton == null)
            {
                CreateCollapsedButton();
            }
            else
            {
                // Configure existing collapsed button from template
                ConfigureCollapsedButton();
            }

            // Query for MenuRoot or create new one
            _menuRoot = _root.Q<VisualElement>("MenuRoot");
            if (_menuRoot == null)
            {
                _menuRoot = new VisualElement();
                _menuRoot.name = "MenuRoot";
                _menuRoot.AddToClassList("adv-menu-root");
                _root.Add(_menuRoot);
            }
            _menuRoot.pickingMode = PickingMode.Ignore;

            // Query for ring background or create
            _ringBackground = _menuRoot.Q<VisualElement>("RingBackground");
            if (_ringBackground == null)
            {
                _ringBackground = new VisualElement();
                _ringBackground.name = "RingBackground";
                _ringBackground.AddToClassList("adv-ring-background");
                _menuRoot.Add(_ringBackground);
            }

            // Query for ripple container or create
            _rippleContainer = _menuRoot.Q<VisualElement>("RippleContainer");
            if (_rippleContainer == null)
            {
                _rippleContainer = new VisualElement();
                _rippleContainer.name = "RippleContainer";
                _rippleContainer.AddToClassList("adv-ripple-container");
                _menuRoot.Add(_rippleContainer);
            }
            _rippleContainer.pickingMode = PickingMode.Ignore;

            // Query for gesture indicator or create
            if (useGestures)
            {
                _gestureIndicator = _menuRoot.Q<VisualElement>("GestureIndicator");
                if (_gestureIndicator == null)
                {
                    _gestureIndicator = new VisualElement();
                    _gestureIndicator.name = "GestureIndicator";
                    _gestureIndicator.AddToClassList("adv-gesture-indicator");
                    _menuRoot.Add(_gestureIndicator);
                }
                _gestureIndicator.pickingMode = PickingMode.Ignore;
            }

            // Create main segments
            CreateMainSegments();

            // Create sub segments (initially hidden)
            if (enableSubMenus)
            {
                CreateSubSegments();
            }

            // Create center info panel
            CreateCenterInfo();

            // Apply styles
            ApplyInlineStyles();

            // Start collapsed or closed based on setting
            if (startCollapsed)
            {
                _menuRoot.style.display = DisplayStyle.None;
                _collapsedButton.style.display = DisplayStyle.Flex;
            }
            else
            {
                SetMenuOpen(false);
            }
        }

        private void CreateCollapsedButton()
        {
            _collapsedButton = new VisualElement();
            _collapsedButton.AddToClassList("adv-collapsed-button");
            _collapsedButton.pickingMode = PickingMode.Position;

            // Style the collapsed button
            _collapsedButton.style.position = Position.Absolute;
            _collapsedButton.style.left = collapsedButtonPosition.x;
            _collapsedButton.style.bottom = collapsedButtonPosition.y;
            _collapsedButton.style.width = collapsedButtonSize;
            _collapsedButton.style.height = collapsedButtonSize;
            _collapsedButton.style.backgroundColor = new Color(0.08f, 0.12f, 0.2f, 0.95f);
            _collapsedButton.style.borderTopLeftRadius = collapsedButtonSize / 2;
            _collapsedButton.style.borderTopRightRadius = collapsedButtonSize / 2;
            _collapsedButton.style.borderBottomLeftRadius = collapsedButtonSize / 2;
            _collapsedButton.style.borderBottomRightRadius = collapsedButtonSize / 2;
            _collapsedButton.style.borderTopWidth = 3;
            _collapsedButton.style.borderBottomWidth = 3;
            _collapsedButton.style.borderLeftWidth = 3;
            _collapsedButton.style.borderRightWidth = 3;
            _collapsedButton.style.borderTopColor = new Color(0.3f, 0.6f, 0.9f, 0.7f);
            _collapsedButton.style.borderBottomColor = new Color(0.3f, 0.6f, 0.9f, 0.7f);
            _collapsedButton.style.borderLeftColor = new Color(0.3f, 0.6f, 0.9f, 0.7f);
            _collapsedButton.style.borderRightColor = new Color(0.3f, 0.6f, 0.9f, 0.7f);
            _collapsedButton.style.alignItems = Align.Center;
            _collapsedButton.style.justifyContent = Justify.Center;
            _collapsedButton.style.transitionProperty = new List<StylePropertyName>
            {
                new StylePropertyName("scale"),
                new StylePropertyName("background-color"),
                new StylePropertyName("border-color")
            };
            _collapsedButton.style.transitionDuration = new List<TimeValue> { new TimeValue(0.15f) };
            _collapsedButton.style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOut };

            // Create icon inside button (microphone/voice icon using SVG or fallback)
            _collapsedIcon = new VisualElement();
            _collapsedIcon.style.width = collapsedButtonSize * 0.5f;
            _collapsedIcon.style.height = collapsedButtonSize * 0.5f;
            _collapsedIcon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;

            // Try to load SVG icon (VectorImage), fallback to PNG textures
            Texture2D buttonTexture = null;
            var svgIcon = Resources.Load<VectorImage>("VoiceControl/Icons/radial_menu");
            if (svgIcon != null)
            {
                _collapsedIcon.style.backgroundImage = new StyleBackground(Background.FromVectorImage(svgIcon));
            }
            else
            {
                // Try PNG from Resources
                buttonTexture = Resources.Load<Texture2D>("VoiceControl/Textures/WheelCenter");
                if (buttonTexture == null)
                {
                    // Try loading from project textures via AssetDatabase in editor
                    #if UNITY_EDITOR
                    buttonTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                        "Assets/_Project/Textures/480px_FAA_SYMBOLOLGY_OPTIONS/Weather_Radar_Base.png");
                    #endif
                }
                if (buttonTexture != null)
                {
                    _collapsedIcon.style.backgroundImage = new StyleBackground(buttonTexture);
                }
            }
            _collapsedIcon.style.unityBackgroundImageTintColor = new Color(0.4f, 0.8f, 1f, 1f);
            _collapsedButton.Add(_collapsedIcon);

            // Hover effects
            _collapsedButton.RegisterCallback<MouseEnterEvent>(evt =>
            {
                _collapsedButton.style.scale = new Scale(new Vector3(1.1f, 1.1f, 1));
                _collapsedButton.style.backgroundColor = new Color(0.12f, 0.18f, 0.28f, 0.98f);
                _collapsedButton.style.borderTopColor = new Color(0.4f, 0.75f, 1f, 0.9f);
                _collapsedButton.style.borderBottomColor = new Color(0.4f, 0.75f, 1f, 0.9f);
                _collapsedButton.style.borderLeftColor = new Color(0.4f, 0.75f, 1f, 0.9f);
                _collapsedButton.style.borderRightColor = new Color(0.4f, 0.75f, 1f, 0.9f);
            });

            _collapsedButton.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                _collapsedButton.style.scale = new Scale(Vector3.one);
                _collapsedButton.style.backgroundColor = new Color(0.08f, 0.12f, 0.2f, 0.95f);
                _collapsedButton.style.borderTopColor = new Color(0.3f, 0.6f, 0.9f, 0.7f);
                _collapsedButton.style.borderBottomColor = new Color(0.3f, 0.6f, 0.9f, 0.7f);
                _collapsedButton.style.borderLeftColor = new Color(0.3f, 0.6f, 0.9f, 0.7f);
                _collapsedButton.style.borderRightColor = new Color(0.3f, 0.6f, 0.9f, 0.7f);
            });

            // Click to expand
            _collapsedButton.RegisterCallback<ClickEvent>(evt =>
            {
                // Animate button press
                _collapsedButton.style.scale = new Scale(new Vector3(0.9f, 0.9f, 1));
                _collapsedButton.schedule.Execute(() =>
                {
                    _collapsedButton.style.scale = new Scale(Vector3.one);
                    ExpandFromCollapsed();
                }).StartingIn(100);
            });

            _root.Add(_collapsedButton);
        }

        private void ConfigureCollapsedButton()
        {
            // Configure existing collapsed button from UXML template
            _collapsedButton.pickingMode = PickingMode.Position;

            // Update position and size from serialized fields
            _collapsedButton.style.left = collapsedButtonPosition.x;
            _collapsedButton.style.bottom = collapsedButtonPosition.y;
            _collapsedButton.style.width = collapsedButtonSize;
            _collapsedButton.style.height = collapsedButtonSize;

            // Get or create icon element
            _collapsedIcon = _collapsedButton.Q<VisualElement>("CollapsedIcon");
            if (_collapsedIcon == null)
            {
                _collapsedIcon = new VisualElement();
                _collapsedIcon.name = "CollapsedIcon";
                _collapsedIcon.AddToClassList("adv-collapsed-icon");
                _collapsedButton.Add(_collapsedIcon);
            }

            // Load icon texture
            Texture2D buttonTexture = Resources.Load<Texture2D>("VoiceControl/Textures/WheelCenter");
            if (buttonTexture == null)
            {
                #if UNITY_EDITOR
                buttonTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/_Project/Textures/480px_FAA_SYMBOLOLGY_OPTIONS/Weather_Radar_Base.png");
                #endif
            }
            if (buttonTexture != null)
            {
                _collapsedIcon.style.backgroundImage = new StyleBackground(buttonTexture);
            }

            // Hover effects
            _collapsedButton.RegisterCallback<MouseEnterEvent>(evt =>
            {
                _collapsedButton.style.scale = new Scale(new Vector3(1.1f, 1.1f, 1));
            });

            _collapsedButton.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                _collapsedButton.style.scale = new Scale(Vector3.one);
            });

            // Click to expand
            _collapsedButton.RegisterCallback<ClickEvent>(evt =>
            {
                _collapsedButton.style.scale = new Scale(new Vector3(0.9f, 0.9f, 1));
                _collapsedButton.schedule.Execute(() =>
                {
                    _collapsedButton.style.scale = new Scale(Vector3.one);
                    ExpandFromCollapsed();
                }).StartingIn(100);
            });
        }

        private void ExpandFromCollapsed()
        {
            // Hide collapsed button with animation
            _collapsedButton.style.scale = new Scale(new Vector3(0.5f, 0.5f, 1));
            _collapsedButton.style.opacity = 0;

            _collapsedButton.schedule.Execute(() =>
            {
                _collapsedButton.style.display = DisplayStyle.None;
                _collapsedButton.style.scale = new Scale(Vector3.one);
                _collapsedButton.style.opacity = 1;
            }).StartingIn(150);

            // Open the radial menu
            SetMenuOpen(true);
        }

        private void CollapseToButton()
        {
            // Show collapsed button with animation
            _collapsedButton.style.display = DisplayStyle.Flex;
            _collapsedButton.style.scale = new Scale(new Vector3(0.5f, 0.5f, 1));
            _collapsedButton.style.opacity = 0;

            _collapsedButton.schedule.Execute(() =>
            {
                _collapsedButton.style.scale = new Scale(new Vector3(1.15f, 1.15f, 1));
                _collapsedButton.style.opacity = 1;
            }).StartingIn(50);

            _collapsedButton.schedule.Execute(() =>
            {
                _collapsedButton.style.scale = new Scale(Vector3.one);
            }).StartingIn(200);
        }

        private void CreateMainSegments()
        {
            float angleStep = 360f / mainSegmentCount;

            for (int i = 0; i < mainSegmentCount; i++)
            {
                float angle = i * angleStep - 90f; // Start from top (-90 degrees)
                var segment = new MainSegment
                {
                    Index = i,
                    Angle = angle,
                    Container = new VisualElement(),
                    Background = new VisualElement(),
                    IconContainer = new VisualElement(),
                    IconImage = new VisualElement(),  // VisualElement for texture
                    NameLabel = new Label()
                };

                segment.Container.AddToClassList("adv-main-segment");
                segment.Container.pickingMode = PickingMode.Position;

                segment.Background.AddToClassList("adv-main-bg");
                segment.Container.Add(segment.Background);

                segment.IconContainer.AddToClassList("adv-main-icon-container");
                segment.Container.Add(segment.IconContainer);

                segment.IconImage.AddToClassList("adv-main-icon");
                segment.IconContainer.Add(segment.IconImage);

                segment.NameLabel.AddToClassList("adv-main-name");
                segment.Container.Add(segment.NameLabel);

                int index = i;
                segment.Container.RegisterCallback<MouseEnterEvent>(evt => OnMainSegmentHover(index, true));
                segment.Container.RegisterCallback<MouseLeaveEvent>(evt => OnMainSegmentHover(index, false));
                segment.Container.RegisterCallback<ClickEvent>(evt => OnMainSegmentClick(index));

                _menuRoot.Add(segment.Container);
                _mainSegments.Add(segment);
            }
        }

        private void CreateSubSegments()
        {
            // Create up to 8 sub-segments per category
            for (int i = 0; i < 8; i++)
            {
                var segment = new SubSegment
                {
                    Index = i,
                    Container = new VisualElement(),
                    Background = new VisualElement(),
                    IconContainer = new VisualElement(),
                    IconImage = new VisualElement(),
                    NameLabel = new Label(),
                    IsVisible = false
                };

                segment.Container.AddToClassList("adv-sub-segment");
                segment.Container.pickingMode = PickingMode.Position;

                segment.Background.AddToClassList("adv-sub-bg");
                segment.Container.Add(segment.Background);

                segment.IconContainer.AddToClassList("adv-sub-icon-container");
                segment.IconImage.AddToClassList("adv-sub-icon-img");
                segment.IconContainer.Add(segment.IconImage);
                segment.Container.Add(segment.IconContainer);

                segment.NameLabel.AddToClassList("adv-sub-name");
                segment.Container.Add(segment.NameLabel);

                int index = i;
                segment.Container.RegisterCallback<MouseEnterEvent>(evt => OnSubSegmentHover(index, true));
                segment.Container.RegisterCallback<MouseLeaveEvent>(evt => OnSubSegmentHover(index, false));
                segment.Container.RegisterCallback<ClickEvent>(evt => OnSubSegmentClick(index));

                segment.Container.style.display = DisplayStyle.None;
                _menuRoot.Add(segment.Container);
                _subSegments.Add(segment);
            }
        }

        private void CreateCenterInfo()
        {
            _centerInfo = _menuRoot.Q<VisualElement>("CenterInfo");
            if (_centerInfo == null)
            {
                _centerInfo = new VisualElement();
                _centerInfo.name = "CenterInfo";
                _centerInfo.AddToClassList("adv-center-info");
                _menuRoot.Add(_centerInfo);
            }

            _centerTitle = _centerInfo.Q<Label>("CenterTitle");
            if (_centerTitle == null)
            {
                _centerTitle = new Label();
                _centerTitle.name = "CenterTitle";
                _centerTitle.AddToClassList("adv-center-title");
                _centerInfo.Add(_centerTitle);
            }
            _centerTitle.text = string.Empty;

            _centerSubtitle = _centerInfo.Q<Label>("CenterSubtitle");
            if (_centerSubtitle == null)
            {
                _centerSubtitle = new Label();
                _centerSubtitle.name = "CenterSubtitle";
                _centerSubtitle.AddToClassList("adv-center-subtitle");
                _centerInfo.Add(_centerSubtitle);
            }
            _centerSubtitle.text = string.Empty;
        }

        private void ApplyInlineStyles()
        {
            // Menu root - centered on screen
            _menuRoot.style.position = Position.Absolute;
            _menuRoot.style.left = new Length(50, LengthUnit.Percent);
            _menuRoot.style.top = new Length(50, LengthUnit.Percent);
            _menuRoot.style.width = 0;
            _menuRoot.style.height = 0;

            // Ring background - MUCH larger outer ring
            float ringSize = middleRadius * 2 + 100;
            _ringBackground.style.position = Position.Absolute;
            _ringBackground.style.width = ringSize;
            _ringBackground.style.height = ringSize;
            _ringBackground.style.left = -ringSize / 2;
            _ringBackground.style.top = -ringSize / 2;
            _ringBackground.style.backgroundColor = new Color(0.02f, 0.04f, 0.08f, ringBackgroundTransparency);
            _ringBackground.style.borderTopLeftRadius = ringSize / 2;
            _ringBackground.style.borderTopRightRadius = ringSize / 2;
            _ringBackground.style.borderBottomLeftRadius = ringSize / 2;
            _ringBackground.style.borderBottomRightRadius = ringSize / 2;
            _ringBackground.style.borderTopWidth = 4;
            _ringBackground.style.borderBottomWidth = 4;
            _ringBackground.style.borderLeftWidth = 4;
            _ringBackground.style.borderRightWidth = 4;
            _ringBackground.style.borderTopColor = new Color(0.2f, 0.4f, 0.7f, 0.5f * menuTransparency);
            _ringBackground.style.borderBottomColor = new Color(0.2f, 0.4f, 0.7f, 0.5f * menuTransparency);
            _ringBackground.style.borderLeftColor = new Color(0.2f, 0.4f, 0.7f, 0.5f * menuTransparency);
            _ringBackground.style.borderRightColor = new Color(0.2f, 0.4f, 0.7f, 0.5f * menuTransparency);

            // Main segments - compact, readable buttons
            float segmentWidth = MainSegmentWidth;
            float segmentHeight = MainSegmentHeight;
            foreach (var seg in _mainSegments)
            {
                seg.Container.style.position = Position.Absolute;
                seg.Container.style.width = segmentWidth;
                seg.Container.style.height = segmentHeight;
                seg.Container.style.backgroundColor = new Color(0.08f, 0.12f, 0.18f, segmentTransparency);
                seg.Container.style.borderTopLeftRadius = 24;
                seg.Container.style.borderTopRightRadius = 24;
                seg.Container.style.borderBottomLeftRadius = 24;
                seg.Container.style.borderBottomRightRadius = 24;
                seg.Container.style.borderTopWidth = 3;
                seg.Container.style.borderBottomWidth = 3;
                seg.Container.style.borderLeftWidth = 3;
                seg.Container.style.borderRightWidth = 3;
                seg.Container.style.borderTopColor = new Color(0.3f, 0.5f, 0.8f, 0.4f);
                seg.Container.style.borderBottomColor = new Color(0.3f, 0.5f, 0.8f, 0.4f);
                seg.Container.style.borderLeftColor = new Color(0.3f, 0.5f, 0.8f, 0.4f);
                seg.Container.style.borderRightColor = new Color(0.3f, 0.5f, 0.8f, 0.4f);
                seg.Container.style.alignItems = Align.Center;
                seg.Container.style.justifyContent = Justify.Center;
                seg.Container.style.flexDirection = FlexDirection.Column;
                seg.Container.style.paddingTop = 12;
                seg.Container.style.paddingBottom = 12;

                seg.Background.style.position = Position.Absolute;
                seg.Background.style.width = new Length(100, LengthUnit.Percent);
                seg.Background.style.height = new Length(100, LengthUnit.Percent);
                seg.Background.style.left = 0;
                seg.Background.style.top = 0;
                seg.Background.style.borderTopLeftRadius = 24;
                seg.Background.style.borderTopRightRadius = 24;
                seg.Background.style.borderBottomLeftRadius = 24;
                seg.Background.style.borderBottomRightRadius = 24;
                seg.Background.style.backgroundColor = new Color(0.15f, 0.25f, 0.35f, 0.3f);

                // Icon container - holds the image
                seg.IconContainer.style.position = Position.Relative;
                seg.IconContainer.style.width = MainIconContainerSize;
                seg.IconContainer.style.height = MainIconContainerSize;
                seg.IconContainer.style.alignItems = Align.Center;
                seg.IconContainer.style.justifyContent = Justify.Center;
                seg.IconContainer.style.marginBottom = 8;

                // Icon image - will display texture
                seg.IconImage.style.width = MainIconSize;
                seg.IconImage.style.height = MainIconSize;
                seg.IconImage.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;

                // Name label - LARGE and sharp text
                seg.NameLabel.style.position = Position.Relative;
                seg.NameLabel.style.fontSize = mainLabelFontSize;
                seg.NameLabel.style.color = new Color(0.92f, 0.95f, 0.98f, 1f);
                seg.NameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                seg.NameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                seg.NameLabel.style.width = segmentWidth - 20;
                seg.NameLabel.style.whiteSpace = WhiteSpace.NoWrap;
                seg.NameLabel.style.letterSpacing = 1;
            }

            // Sub segments - compact secondary buttons
            float subWidth = SubSegmentWidth;
            float subHeight = SubSegmentHeight;
            foreach (var seg in _subSegments)
            {
                seg.Container.style.position = Position.Absolute;
                seg.Container.style.width = subWidth;
                seg.Container.style.height = subHeight;
                seg.Container.style.backgroundColor = new Color(0.06f, 0.10f, 0.16f, segmentTransparency);
                seg.Container.style.borderTopLeftRadius = 18;
                seg.Container.style.borderTopRightRadius = 18;
                seg.Container.style.borderBottomLeftRadius = 18;
                seg.Container.style.borderBottomRightRadius = 18;
                seg.Container.style.borderTopWidth = 2;
                seg.Container.style.borderBottomWidth = 2;
                seg.Container.style.borderLeftWidth = 2;
                seg.Container.style.borderRightWidth = 2;
                seg.Container.style.borderTopColor = SubBorderBaseColor;
                seg.Container.style.borderBottomColor = SubBorderBaseColor;
                seg.Container.style.borderLeftColor = SubBorderBaseColor;
                seg.Container.style.borderRightColor = SubBorderBaseColor;
                seg.Container.style.alignItems = Align.Center;
                seg.Container.style.justifyContent = Justify.Center;
                seg.Container.style.flexDirection = FlexDirection.Column;
                seg.Container.style.paddingTop = 8;
                seg.Container.style.paddingBottom = 8;
                seg.Container.style.transitionProperty = new List<StylePropertyName>
                {
                    new StylePropertyName("scale"),
                    new StylePropertyName("opacity"),
                    new StylePropertyName("background-color"),
                    new StylePropertyName("border-color")
                };
                seg.Container.style.transitionDuration = new List<TimeValue> { new TimeValue(0.18f) };
                seg.Container.style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOut };

                seg.Background.style.position = Position.Absolute;
                seg.Background.style.width = new Length(100, LengthUnit.Percent);
                seg.Background.style.height = new Length(100, LengthUnit.Percent);
                seg.Background.style.left = 0;
                seg.Background.style.top = 0;
                seg.Background.style.borderTopLeftRadius = 18;
                seg.Background.style.borderTopRightRadius = 18;
                seg.Background.style.borderBottomLeftRadius = 18;
                seg.Background.style.borderBottomRightRadius = 18;

                seg.IconContainer.style.width = SubIconContainerSize;
                seg.IconContainer.style.height = SubIconContainerSize;
                seg.IconContainer.style.alignItems = Align.Center;
                seg.IconContainer.style.justifyContent = Justify.Center;
                seg.IconContainer.style.marginBottom = 4;
                seg.IconContainer.style.transitionProperty = new List<StylePropertyName>
                {
                    new StylePropertyName("scale"),
                    new StylePropertyName("opacity")
                };
                seg.IconContainer.style.transitionDuration = new List<TimeValue> { new TimeValue(0.18f) };
                seg.IconContainer.style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOut };

                seg.IconImage.style.width = SubIconSize;
                seg.IconImage.style.height = SubIconSize;
                seg.IconImage.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;

                seg.NameLabel.style.fontSize = subLabelFontSize;
                seg.NameLabel.style.color = new Color(0.85f, 0.90f, 0.95f, 0.95f);
                seg.NameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                seg.NameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                seg.NameLabel.style.width = subWidth - 16;
            }

            // Center info - LARGE prominent center hub
            float centerSize = innerRadius * 2 - CenterSizePadding;
            _centerInfo.style.position = Position.Absolute;
            _centerInfo.style.width = centerSize;
            _centerInfo.style.height = centerSize;
            _centerInfo.style.left = -centerSize / 2;
            _centerInfo.style.top = -centerSize / 2;
            _centerInfo.style.backgroundColor = new Color(0.03f, 0.06f, 0.12f, centerTransparency);
            _centerInfo.style.borderTopLeftRadius = centerSize / 2;
            _centerInfo.style.borderTopRightRadius = centerSize / 2;
            _centerInfo.style.borderBottomLeftRadius = centerSize / 2;
            _centerInfo.style.borderBottomRightRadius = centerSize / 2;
            _centerInfo.style.borderTopWidth = 4;
            _centerInfo.style.borderBottomWidth = 4;
            _centerInfo.style.borderLeftWidth = 4;
            _centerInfo.style.borderRightWidth = 4;
            _centerInfo.style.borderTopColor = new Color(0.25f, 0.5f, 0.85f, 0.7f * menuTransparency);
            _centerInfo.style.borderBottomColor = new Color(0.25f, 0.5f, 0.85f, 0.7f * menuTransparency);
            _centerInfo.style.borderLeftColor = new Color(0.25f, 0.5f, 0.85f, 0.7f * menuTransparency);
            _centerInfo.style.borderRightColor = new Color(0.25f, 0.5f, 0.85f, 0.7f * menuTransparency);
            _centerInfo.style.alignItems = Align.Center;
            _centerInfo.style.justifyContent = Justify.Center;

            // Center title - LARGE sharp text
            _centerTitle.style.fontSize = centerTitleFontSize;
            _centerTitle.style.color = new Color(0.3f, 0.75f, 1f, 1f);
            _centerTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _centerTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _centerTitle.style.letterSpacing = 1;

            // Center subtitle
            _centerSubtitle.style.fontSize = centerSubtitleFontSize;
            _centerSubtitle.style.color = new Color(0.75f, 0.82f, 0.90f, 0.95f);
            _centerSubtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _centerSubtitle.style.marginTop = 12;
        }

        private void LoadCommands()
        {
            // Prevent recursive calls (DiscoverTargets triggers OnRegistryUpdated)
            if (_isLoadingCommands) return;
            _isLoadingCommands = true;

            try
            {
                _categories.Clear();

                var registry = VoiceCommandRegistry.Instance;
                if (registry != null)
                {
                    LoadCommandsFromRegistry(registry);
                }
                else
                {
                    LoadDemoCommands();
                }

                AssignCategoriesToSegments();
            }
            finally
            {
                _isLoadingCommands = false;
            }
        }

        private void LoadCommandsFromRegistry(VoiceCommandRegistry registry)
        {
            registry.DiscoverTargets();
            var commands = registry.GetAllCommands();

            var commandLookup = new Dictionary<string, VoiceCommandInfo>();
            foreach (var cmd in commands)
            {
                commandLookup[$"{cmd.TargetName}:{cmd.Name}"] = cmd;
            }

            _categories.Clear();

            var radar = CreateCategory("radar", "Radar");
            TryAddCommand(radar, commandLookup, "weather_radar", "show_panel", "Show Weather Radar");
            TryAddCommand(radar, commandLookup, "weather_radar", "hide_panel", "Hide Weather Radar");
            TryAddCommand(radar, commandLookup, "traffic_radar", "show_panel", "Show Traffic Radar");
            TryAddCommand(radar, commandLookup, "traffic_radar", "hide_panel", "Hide Traffic Radar");
            if (radar.Commands.Count > 0) _categories.Add(radar);

            var indicators = CreateCategory("indicator_system", "Indicator System");
            TryAddCommand(indicators, commandLookup, "indicator_system", "show_all_indicators", "Show Indicators");
            TryAddCommand(indicators, commandLookup, "indicator_system", "hide_all_indicators", "Hide Indicators");
            if (indicators.Commands.Count > 0) _categories.Add(indicators);

            var hud = CreateCategory("hud", "HUD");
            TryAddCommand(hud, commandLookup, "symbology", "show", "Show HUD");
            TryAddCommand(hud, commandLookup, "symbology", "hide", "Hide HUD");
            TryAddCommand(hud, commandLookup, "symbology", "set_white", "Set White");
            TryAddCommand(hud, commandLookup, "symbology", "set_green", "Set Green");
            TryAddCommand(hud, commandLookup, "symbology", "set_black", "Set Black");
            if (hud.Commands.Count > 0) _categories.Add(hud);

            var vision = CreateCategory("visionbriefing", "Vision Briefing");
            if (!TryAddCommand(vision, commandLookup, "visionbriefing", "weather_briefing", "Weather Briefing"))
            {
                TryAddCommand(vision, commandLookup, "visionbriefing", "analyze_weather", "Weather Briefing");
            }

            if (!TryAddCommand(vision, commandLookup, "visionbriefing", "sectional_briefing", "Traffic Briefing"))
            {
                TryAddCommand(vision, commandLookup, "visionbriefing", "analyze_sectional", "Traffic Briefing");
            }

            TryAddCommand(vision, commandLookup, "visionbriefing", "hide_briefing", "Hide Briefing");
            if (vision.Commands.Count > 0) _categories.Add(vision);
        }

        private void LoadDemoCommands()
        {
            _categories.Clear();

            var radar = CreateCategory("radar", "Radar");
            AddDemoCommand(radar, "weather_radar", "show_panel", "Show Weather Radar");
            AddDemoCommand(radar, "weather_radar", "hide_panel", "Hide Weather Radar");
            AddDemoCommand(radar, "traffic_radar", "show_panel", "Show Traffic Radar");
            AddDemoCommand(radar, "traffic_radar", "hide_panel", "Hide Traffic Radar");
            _categories.Add(radar);

            var indicators = CreateCategory("indicator_system", "Indicator System");
            AddDemoCommand(indicators, "indicator_system", "show_all_indicators", "Show Indicators");
            AddDemoCommand(indicators, "indicator_system", "hide_all_indicators", "Hide Indicators");
            _categories.Add(indicators);

            var hud = CreateCategory("hud", "HUD");
            AddDemoCommand(hud, "symbology", "show", "Show HUD");
            AddDemoCommand(hud, "symbology", "hide", "Hide HUD");
            AddDemoCommand(hud, "symbology", "set_white", "Set White");
            AddDemoCommand(hud, "symbology", "set_green", "Set Green");
            AddDemoCommand(hud, "symbology", "set_black", "Set Black");
            _categories.Add(hud);

            var vision = CreateCategory("visionbriefing", "Vision Briefing");
            AddDemoCommand(vision, "visionbriefing", "weather_briefing", "Weather Briefing");
            AddDemoCommand(vision, "visionbriefing", "sectional_briefing", "Traffic Briefing");
            AddDemoCommand(vision, "visionbriefing", "hide_briefing", "Hide Briefing");
            _categories.Add(vision);
        }

        private MenuCategory CreateCategory(string id, string displayName)
        {
            var def = _categoryDefs.GetValueOrDefault(id, (iconPath: string.Empty, color: Color.gray));
            return new MenuCategory
            {
                Id = id,
                DisplayName = displayName,
                Icon = def.iconPath,
                Color = def.color
            };
        }

        private bool TryAddCommand(
            MenuCategory category,
            Dictionary<string, VoiceCommandInfo> lookup,
            string targetId,
            string commandName,
            string displayNameOverride = null)
        {
            if (!lookup.TryGetValue($"{targetId}:{commandName}", out var cmd))
            {
                return false;
            }

            if (cmd.Parameters?.Any(p => p.Required) ?? false)
            {
                return false;
            }

            var displayName = displayNameOverride ?? FormatCommandName(cmd.Name);
            category.Commands.Add(new MenuCommand
            {
                Id = $"{targetId}_{commandName}",
                TargetId = targetId,
                CommandName = cmd.Name,
                DisplayName = displayName,
                Description = cmd.Description,
                Category = category.Id,
                IconPath = GetCommandIconPath(targetId, cmd.Name, displayName),
                Color = category.Color,
                RequiresParams = false
            });

            return true;
        }

        private void AddDemoCommand(MenuCategory category, string targetId, string commandName, string displayName)
        {
            category.Commands.Add(new MenuCommand
            {
                Id = $"{targetId}_{commandName}",
                TargetId = targetId,
                CommandName = commandName,
                DisplayName = displayName,
                Description = displayName,
                Category = category.Id,
                IconPath = GetCommandIconPath(targetId, commandName, displayName),
                Color = category.Color,
                RequiresParams = false
            });
        }

        private void AssignCategoriesToSegments()
        {
            for (int i = 0; i < _mainSegments.Count; i++)
            {
                if (i < _categories.Count)
                {
                    _mainSegments[i].Category = _categories[i];
                    _mainSegments[i].Container.style.display = DisplayStyle.Flex;

                    // Load icon texture from path
                    bool iconLoaded = TrySetIcon(_mainSegments[i].IconImage, _categories[i].Icon, Color.white);

                    // Fallback: show category initial as a styled circle if no icon loaded
                    if (!iconLoaded)
                    {
                        // Create fallback initial display
                        string initial = !string.IsNullOrEmpty(_categories[i].DisplayName)
                            ? _categories[i].DisplayName.Substring(0, 1).ToUpper()
                            : "?";
                        _mainSegments[i].IconImage.style.backgroundColor = _categories[i].Color * 0.4f;
                        _mainSegments[i].IconImage.style.borderTopLeftRadius = 32;
                        _mainSegments[i].IconImage.style.borderTopRightRadius = 32;
                        _mainSegments[i].IconImage.style.borderBottomLeftRadius = 32;
                        _mainSegments[i].IconImage.style.borderBottomRightRadius = 32;

                        // Add initial label if not present
                        var existingLabel = _mainSegments[i].IconImage.Q<Label>("fallback-initial");
                        if (existingLabel == null)
                        {
                            var fallbackLabel = new Label(initial);
                            fallbackLabel.name = "fallback-initial";
                            fallbackLabel.style.fontSize = 28;
                            fallbackLabel.style.color = Color.white;
                            fallbackLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                            fallbackLabel.style.width = new Length(100, LengthUnit.Percent);
                            fallbackLabel.style.height = new Length(100, LengthUnit.Percent);
                            _mainSegments[i].IconImage.Add(fallbackLabel);
                        }
                        else
                        {
                            existingLabel.text = initial;
                        }
                    }

                    _mainSegments[i].NameLabel.text = _categories[i].DisplayName;
                    _mainSegments[i].Background.style.backgroundColor = _categories[i].Color * 0.25f;
                }
                else
                {
                    _mainSegments[i].Container.style.display = DisplayStyle.None;
                }
            }
        }

        private bool TrySetIcon(VisualElement target, string iconPath, Color tint)
        {
            if (target == null || string.IsNullOrEmpty(iconPath))
            {
                return false;
            }

            target.Clear();
            target.style.backgroundColor = Color.clear;

            VectorImage vector = Resources.Load<VectorImage>(iconPath);
            Sprite sprite = null;
            Texture2D texture = null;

            if (vector == null)
            {
                sprite = Resources.Load<Sprite>(iconPath);
            }

            if (vector == null && sprite == null)
            {
                texture = Resources.Load<Texture2D>(iconPath);
            }

            #if UNITY_EDITOR
            if (vector == null && sprite == null && texture == null)
            {
                vector = UnityEditor.AssetDatabase.LoadAssetAtPath<VectorImage>("Assets/" + iconPath + ".svg");
                if (vector == null)
                {
                    sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/" + iconPath + ".png");
                }
                if (vector == null && sprite == null)
                {
                    texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/" + iconPath + ".png");
                }
            }
            #endif

            if (vector != null)
            {
                target.style.backgroundImage = new StyleBackground(Background.FromVectorImage(vector));
            }
            else if (sprite != null)
            {
                target.style.backgroundImage = new StyleBackground(sprite);
            }
            else if (texture != null)
            {
                target.style.backgroundImage = new StyleBackground(texture);
            }
            else
            {
                return false;
            }

            target.style.unityBackgroundImageTintColor = tint;
            return true;
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleMenu();
            }

            if (_isOpen && Input.GetKeyDown(closeKey))
            {
                if (_subMenuOpen)
                {
                    CloseSubMenu();
                }
                else
                {
                    SetMenuOpen(false);
                }
            }

            // Mouse wheel navigation
            if (_isOpen && useMouseWheel)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    NavigateMenu(scroll > 0 ? 1 : -1);
                }
            }
        }

        private void UpdateAnimations()
        {
            if (!_isAnimating && !_isOpen) return;

            float targetOpen = _isOpen ? 1f : 0f;
            float openSpeed = _isOpen ? 1f / openDuration : 1f / closeDuration;

#if UNITY_EDITOR
            float deltaTime = Application.isPlaying ? Time.unscaledDeltaTime : 0.016f; // 60fps for editor preview
#else
            float deltaTime = Time.unscaledDeltaTime;
#endif
            _openProgress = Mathf.MoveTowards(_openProgress, targetOpen, deltaTime * openSpeed);

            float curvedOpen = springCurve.Evaluate(_openProgress);

            // Animate ring background
            float ringScale = 0.8f + 0.2f * curvedOpen;
            _ringBackground.style.scale = new Scale(new Vector3(ringScale, ringScale, 1));
            _ringBackground.style.opacity = curvedOpen;

            // Animate main segments - positioned on ring between inner and middle radius
            float segmentWidth = MainSegmentWidth;
            float segmentHeight = MainSegmentHeight;
            for (int i = 0; i < _mainSegments.Count; i++)
            {
                var seg = _mainSegments[i];
                if (seg.Category == null) continue;

                float stagger = i * 0.04f;
                float segProgress = Mathf.Clamp01((_openProgress - stagger) / (1f - stagger));
                float segCurved = springCurve.Evaluate(segProgress);

                // Position in circle - center of the ring between inner and middle
                float angleRad = (seg.Angle + _rotationOffset) * Mathf.Deg2Rad;
                float radius = (innerRadius + middleRadius) / 2 * segCurved;
                float x = Mathf.Cos(angleRad) * radius;
                float y = Mathf.Sin(angleRad) * radius;

                // Center the segment on the calculated position
                seg.Container.style.left = x - segmentWidth / 2;
                seg.Container.style.top = y - segmentHeight / 2;
                seg.Container.style.opacity = segProgress;

                // Scale based on selection with spring effect
                float baseScale = segCurved;
                float selectionBoost = _selectedMainIndex == i ? 1.1f : 1f;
                seg.Container.style.scale = new Scale(new Vector3(baseScale * selectionBoost, baseScale * selectionBoost, 1));
            }

            // Animate sub-menu
            float subMenuDelta = Application.isPlaying ? Time.unscaledDeltaTime : 0.016f;
            if (_subMenuOpen)
            {
                _subMenuProgress = Mathf.MoveTowards(_subMenuProgress, 1f, subMenuDelta / subMenuExpandDuration);
            }
            else
            {
                _subMenuProgress = Mathf.MoveTowards(_subMenuProgress, 0f, subMenuDelta / subMenuExpandDuration);
            }

            if (_subMenuProgress > 0)
            {
                float subCurved = bounceCurve.Evaluate(_subMenuProgress);
                float subWidth = SubSegmentWidth;
                float subHeight = SubSegmentHeight;

                for (int i = 0; i < _subSegments.Count; i++)
                {
                    var seg = _subSegments[i];
                    if (!seg.IsVisible) continue;

                    float stagger = i * 0.03f;
                    float segProgress = Mathf.Clamp01((_subMenuProgress - stagger) / (1f - stagger));
                    segProgress = bounceCurve.Evaluate(segProgress);

                    // Position relative to parent segment - fan out from the selected main segment
                    float baseAngle = _mainSegments[_selectedMainIndex].Angle + _rotationOffset;
                    float spread = SubMenuSpreadDegrees;  // degrees spread for sub-items
                    int visibleCount = _subSegments.Count(s => s.IsVisible);
                    float angleOffset = (i - (visibleCount - 1) / 2f) * (spread / Mathf.Max(1, visibleCount - 1));
                    float angle = (baseAngle + angleOffset) * Mathf.Deg2Rad;

                    float innerR = middleRadius + SubMenuInnerOffset;
                    float outerR = outerRadius - SubMenuOuterInset;
                    float radius = Mathf.Lerp(innerR, outerR, segProgress);

                    float x = Mathf.Cos(angle) * radius;
                    float y = Mathf.Sin(angle) * radius;

                    seg.Container.style.display = DisplayStyle.Flex;
                    seg.Container.style.left = x - subWidth / 2;
                    seg.Container.style.top = y - subHeight / 2;
                    seg.Container.style.opacity = segProgress;
                    seg.Container.style.scale = new Scale(new Vector3(segProgress, segProgress, 1));
                }
            }
            else
            {
                foreach (var seg in _subSegments)
                {
                    seg.Container.style.display = DisplayStyle.None;
                }
            }

            // Animate center info
            float centerScale = 0.9f + 0.1f * curvedOpen;
            _centerInfo.style.scale = new Scale(new Vector3(centerScale, centerScale, 1));
            _centerInfo.style.opacity = curvedOpen;

            // Check animation complete
            if (Mathf.Approximately(_openProgress, targetOpen) &&
                (!_subMenuOpen || Mathf.Approximately(_subMenuProgress, _subMenuOpen ? 1f : 0f)))
            {
                _isAnimating = false;
                if (!_isOpen)
                {
                    _menuRoot.style.display = DisplayStyle.None;
                }
            }
        }

        private void UpdateGestureRecognition()
        {
            if (!useGestures) return;

            Vector2 currentMousePos = Input.mousePosition;
            Vector2 delta = currentMousePos - _lastMousePos;

            // Circular gesture detection
            Vector2 center = new Vector2(Screen.width / 2, Screen.height / 2);
            Vector2 toCenter = currentMousePos - center;
            Vector2 prevToCenter = _lastMousePos - center;

            float currentAngle = Mathf.Atan2(toCenter.y, toCenter.x);
            float prevAngle = Mathf.Atan2(prevToCenter.y, prevToCenter.x);
            float angleDelta = Mathf.DeltaAngle(prevAngle * Mathf.Rad2Deg, currentAngle * Mathf.Rad2Deg);

            if (Mathf.Abs(angleDelta) > 1f && toCenter.magnitude > innerRadius && toCenter.magnitude < outerRadius * 1.5f)
            {
                _gestureAccumulator += angleDelta;

                // Update gesture indicator
                if (_gestureIndicator != null)
                {
                    float indicatorOpacity = Mathf.Clamp01(Mathf.Abs(_gestureAccumulator) / 30f);
                    _gestureIndicator.style.opacity = indicatorOpacity;
                    // Rotate appears to require different parameters in this Unity version
                    // For now, skip direct rotation assignment
                    // _gestureIndicator.style.rotate = new Rotate(Angle.Degrees(_gestureAccumulator));
                }

                // Apply rotation to menu
                if (Mathf.Abs(_gestureAccumulator) > 45f)
                {
                    int direction = (int)Mathf.Sign(_gestureAccumulator);
                    NavigateMenu(direction);
                    _gestureAccumulator = 0;
                }
            }

            _lastMousePos = currentMousePos;
        }

        private void UpdateRipples()
        {
            if (!useRippleEffect) return;

            for (int i = _ripples.Count - 1; i >= 0; i--)
            {
                var ripple = _ripples[i];
                ripple.Progress += Time.unscaledDeltaTime * ripple.Speed;

                if (ripple.Progress >= 1f)
                {
                    _rippleContainer.Remove(ripple.Element);
                    _ripples.RemoveAt(i);
                    continue;
                }

                float scale = 0.5f + ripple.Progress * 2f;
                float alpha = (1f - ripple.Progress) * 0.5f;

                ripple.Element.style.scale = new Scale(new Vector3(scale, scale, 1));
                ripple.Element.style.opacity = alpha;
                ripple.Element.style.backgroundColor = new Color(ripple.Color.r, ripple.Color.g, ripple.Color.b, alpha);
            }
        }

        private void UpdatePulseEffect()
        {
            if (_selectedMainIndex < 0 || _selectedMainIndex >= _mainSegments.Count) return;

            var seg = _mainSegments[_selectedMainIndex];
            float pulse = 0.9f + 0.1f * Mathf.Sin(Time.unscaledTime * 3f);

            seg.Background.style.backgroundColor = seg.Category.Color * pulse * 0.5f;
        }

        private void CreateRipple(Vector2 position, Color color)
        {
            if (!useRippleEffect) return;

            var ripple = new VisualElement();
            ripple.style.position = Position.Absolute;
            ripple.style.width = 20;
            ripple.style.height = 20;
            ripple.style.left = position.x - 10;
            ripple.style.top = position.y - 10;
            // ripple.style.borderRadius = 10;
            ripple.style.backgroundColor = color;

            _rippleContainer.Add(ripple);

            _ripples.Add(new Ripple
            {
                Element = ripple,
                Progress = 0,
                Speed = 2f,
                Color = color
            });
        }

        private void NavigateMenu(int direction)
        {
            if (_categories.Count == 0) return;

            int newIndex;
            if (_subMenuOpen)
            {
                newIndex = (_selectedSubIndex + direction + _subSegments.Count(s => s.IsVisible)) % _subSegments.Count(s => s.IsVisible);
                newIndex = Mathf.Max(0, newIndex);
                SelectSubSegment(newIndex);
            }
            else
            {
                newIndex = (_selectedMainIndex + direction + _categories.Count) % _categories.Count;
                SelectMainSegment(newIndex);
            }

            PlaySound(selectSound);
        }

        private void OnMainSegmentHover(int index, bool hovered)
        {
            if (!_isOpen) return;

            _mainSegments[index].IsHovered = hovered;

            if (hovered && _selectedMainIndex != index)
            {
                SelectMainSegment(index);

                // Immediately update sub-menu when hovering different category
                // This fixes the lingering sub-menu from previous hovered item
                if (enableSubMenus && _mainSegments[index].Category != null)
                {
                    OpenSubMenu(index);
                }
            }
        }

        private void OnMainSegmentClick(int index)
        {
            if (!_isOpen) return;

            SelectMainSegment(index);

            if (enableSubMenus && _mainSegments[index].Category != null)
            {
                OpenSubMenu(index);
            }
            else
            {
                ExecuteMainCommand(index);
            }

            PlaySound(executeSound);
        }

        private void OnSubSegmentHover(int index, bool hovered)
        {
            if (!_isOpen || !_subMenuOpen) return;

            if (hovered && _selectedSubIndex != index && _subSegments[index].IsVisible)
            {
                SelectSubSegment(index);
            }

            if (_subSegments[index].IsVisible)
            {
                SetSubSegmentHover(_subSegments[index], hovered);
            }
        }

        private void OnSubSegmentClick(int index)
        {
            if (!_isOpen || !_subMenuOpen) return;
            if (!_subSegments[index].IsVisible) return;

            ExecuteSubCommand(index);
        }

        private void SelectMainSegment(int index)
        {
            if (_selectedMainIndex == index) return;

            if (_subMenuOpen && _selectedMainIndex >= 0 && _selectedMainIndex != index)
            {
                _subMenuOpen = false;
                _subMenuProgress = 0f;
                _selectedSubIndex = -1;
                foreach (var subSeg in _subSegments)
                {
                    subSeg.IsVisible = false;
                    subSeg.Container.style.display = DisplayStyle.None;
                }
            }

            // Deselect previous
            if (_selectedMainIndex >= 0)
            {
                var prev = _mainSegments[_selectedMainIndex];
                prev.Container.RemoveFromClassList("adv-main-selected");
                prev.Background.style.backgroundColor = prev.Category?.Color * 0.3f ?? Color.gray;
            }

            _selectedMainIndex = index;
            var seg = _mainSegments[index];
            seg.Container.AddToClassList("adv-main-selected");
            seg.Background.style.backgroundColor = seg.Category?.Color * 0.6f ?? Color.gray;

            // Update center info
            if (seg.Category != null)
            {
                _centerTitle.text = seg.Category.DisplayName;
                _centerTitle.style.color = seg.Category.Color;
                _centerSubtitle.text = $"{seg.Category.Commands.Count} commands";
            }

            CreateRipple(Vector2.zero, seg.Category?.Color ?? Color.white);
            OnCategoryChanged?.Invoke(seg.Category?.Id);
        }

        private void SelectSubSegment(int index)
        {
            if (_selectedSubIndex == index) return;

            _selectedSubIndex = index;

            var cmd = _subSegments[index].Command;
            if (cmd != null)
            {
                _centerSubtitle.text = cmd.DisplayName;
            }
        }

        private void SetSubSegmentHover(SubSegment seg, bool hovered)
        {
            if (seg == null || seg.Command == null) return;

            var baseColor = seg.Command.Color;
            float bgAlpha = hovered ? 0.6f : 0.4f;
            var borderColor = Color.Lerp(SubBorderBaseColor, baseColor, hovered ? 0.8f : 0.2f);

            seg.Container.style.scale = new Scale(new Vector3(hovered ? 1.08f : 1f, hovered ? 1.08f : 1f, 1f));
            seg.IconContainer.style.scale = new Scale(new Vector3(hovered ? 1.1f : 1f, hovered ? 1.1f : 1f, 1f));
            seg.IconContainer.style.opacity = hovered ? 1f : 0.9f;

            seg.Container.style.borderTopColor = borderColor;
            seg.Container.style.borderBottomColor = borderColor;
            seg.Container.style.borderLeftColor = borderColor;
            seg.Container.style.borderRightColor = borderColor;

            seg.Background.style.backgroundColor = baseColor * bgAlpha;
            seg.NameLabel.style.color = hovered
                ? new Color(0.95f, 0.97f, 1f, 1f)
                : new Color(0.85f, 0.90f, 0.95f, 0.95f);
        }

        private void OpenSubMenu(int mainIndex)
        {
            var category = _mainSegments[mainIndex].Category;
            if (category == null) return;

            // If already showing sub-menu for different category, close it first
            // to prevent lingering items from previous hover
            if (_subMenuOpen && _selectedMainIndex != mainIndex)
            {
                // Hide all current sub-segments immediately
                foreach (var seg in _subSegments)
                {
                    seg.IsVisible = false;
                    seg.Container.style.display = DisplayStyle.None;
                }
                _subMenuOpen = false;
                _subMenuProgress = 0;
                _selectedSubIndex = -1;
            }

            _subMenuOpen = true;

            // Setup sub segments
            for (int i = 0; i < _subSegments.Count; i++)
            {
                var seg = _subSegments[i];
                if (i < category.Commands.Count)
                {
                    seg.Command = category.Commands[i];
                    TrySetIcon(seg.IconImage, seg.Command.IconPath, Color.white);
                    seg.NameLabel.text = seg.Command.DisplayName;
                    seg.Background.style.backgroundColor = category.Color * 0.4f;
                    seg.IsVisible = true;
                    SetSubSegmentHover(seg, false);
                }
                else
                {
                    seg.IsVisible = false;
                }
            }

            _selectedSubIndex = -1;
        }

        private void CloseSubMenu()
        {
            _subMenuOpen = false;
            _selectedSubIndex = -1;

            var seg = _mainSegments[_selectedMainIndex];
            if (seg.Category != null)
            {
                _centerSubtitle.text = $"{seg.Category.Commands.Count} commands";
            }
        }

        private void ExecuteMainCommand(int index)
        {
            var category = _mainSegments[index].Category;
            if (category?.Commands.Count > 0)
            {
                ExecuteCommand(category.Commands[0]);
            }
        }

        private void ExecuteSubCommand(int index)
        {
            var cmd = _subSegments[index].Command;
            if (cmd != null)
            {
                ExecuteCommand(cmd);
            }
        }

        private void ExecuteCommand(MenuCommand cmd)
        {
            OnCommandExecuted?.Invoke(cmd);

            var registry = VoiceCommandRegistry.Instance;
            if (registry != null)
            {
                registry.ExecuteCommand(cmd.TargetId, cmd.CommandName, cmd.DefaultParams);
            }

            SetMenuOpen(false);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && Camera.main != null)
            {
                AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, 0.5f);
            }
        }

        public void ToggleMenu()
        {
            SetMenuOpen(!_isOpen);
        }

        public void SetMenuOpen(bool open)
        {
            if (_isOpen == open) return;

            _isOpen = open;
            _isAnimating = true;

            if (_isOpen)
            {
                _menuRoot.style.display = DisplayStyle.Flex;
                _lastMousePos = Input.mousePosition;
                _gestureAccumulator = 0;
                LoadCommands();
                OnMenuOpened?.Invoke();
                PlaySound(openSound);
            }
            else
            {
                _subMenuOpen = false;
                _subMenuProgress = 0;
                OnMenuClosed?.Invoke();
                PlaySound(closeSound);

                // Show collapsed button when menu closes
                if (startCollapsed && _collapsedButton != null)
                {
                    CollapseToButton();
                }
            }
        }

        private void OnRegistryUpdated()
        {
            if (_isOpen)
            {
                LoadCommands();
            }
        }

        private string FormatCommandName(string name)
        {
            return string.Join(" ", name.Split('_')
                .Select(w => char.ToUpper(w[0]) + w.Substring(1)));
        }

        private string GetCommandIconPath(string targetId, string commandName, string displayName)
        {
            var lowerName = commandName.ToLowerInvariant();
            var lowerDisplay = displayName?.ToLowerInvariant() ?? string.Empty;

            if (lowerName.Contains("set_white")) return "VoiceControl/IconsSvg/ColorWhite";
            if (lowerName.Contains("set_green")) return "VoiceControl/IconsSvg/ColorGreen";
            if (lowerName.Contains("set_black")) return "VoiceControl/IconsSvg/ColorBlack";

            if (lowerDisplay.Contains("weather briefing") || lowerName.Contains("weather_briefing") || lowerName.Contains("analyze_weather"))
                return "VoiceControl/IconsSvg/WeatherBriefing";

            if (lowerDisplay.Contains("traffic briefing") || lowerName.Contains("sectional_briefing") || lowerName.Contains("analyze_sectional"))
                return "VoiceControl/IconsSvg/TrafficBriefing";

            if (lowerName.Contains("hide")) return "VoiceControl/IconsSvg/Hide";
            if (lowerName.Contains("show")) return "VoiceControl/IconsSvg/Show";

            return "VoiceControl/IconsSvg/Command";
        }

        // Public API
        public bool IsOpen => _isOpen;
        public bool IsSubMenuOpen => _subMenuOpen;
        public int SelectedCategory => _selectedMainIndex;
        public float MenuTransparency => menuTransparency;

        public void SetCategoryEnabled(string categoryId, bool enabled)
        {
            var seg = _mainSegments.FirstOrDefault(s => s.Category?.Id == categoryId);
            if (seg != null)
            {
                seg.Container.SetEnabled(enabled);
            }
        }

        /// <summary>
        /// Adjusts the overall menu transparency at runtime.
        /// </summary>
        /// <param name="overall">Overall transparency multiplier (0.3-1.0)</param>
        /// <param name="ring">Ring background transparency (0.5-1.0)</param>
        /// <param name="segments">Segment transparency (0.5-1.0)</param>
        /// <param name="center">Center hub transparency (0.7-1.0)</param>
        public void SetTransparency(float overall = -1f, float ring = -1f, float segments = -1f, float center = -1f)
        {
            if (overall >= 0) menuTransparency = Mathf.Clamp(overall, 0.3f, 1f);
            if (ring >= 0) ringBackgroundTransparency = Mathf.Clamp(ring, 0.5f, 1f);
            if (segments >= 0) segmentTransparency = Mathf.Clamp(segments, 0.5f, 1f);
            if (center >= 0) centerTransparency = Mathf.Clamp(center, 0.7f, 1f);

            // Re-apply styles with new transparency values
            if (_menuRoot != null)
            {
                ApplyInlineStyles();
            }
        }

        #region Editor API

        // Properties for custom editor access
        public float InnerRadius => innerRadius;
        public float MiddleRadius => middleRadius;
        public float OuterRadius => outerRadius;
        public float CollapsedButtonSize => collapsedButtonSize;
        public Vector2 CollapsedButtonPosition => collapsedButtonPosition;
        public float OpenDuration => openDuration;
        public float CloseDuration => closeDuration;
        public float SubMenuExpandDuration => subMenuExpandDuration;
        public float RingTransparency => ringBackgroundTransparency;
        public float SegmentTransparency => segmentTransparency;
        public float CenterTransparency => centerTransparency;

        /// <summary>
        /// Updates radial dimensions and refreshes the UI.
        /// </summary>
        public void SetRadialDimensions(float inner, float middle, float outer)
        {
            innerRadius = inner;
            middleRadius = middle;
            outerRadius = outer;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                RefreshUI();
            }
#endif
        }

        /// <summary>
        /// Updates collapsed button settings and refreshes the UI.
        /// </summary>
        public void SetCollapsedButton(float size, Vector2 position)
        {
            collapsedButtonSize = size;
            collapsedButtonPosition = position;

            if (_collapsedButton != null)
            {
                _collapsedButton.style.width = size;
                _collapsedButton.style.height = size;
                _collapsedButton.style.left = position.x;
                _collapsedButton.style.bottom = position.y;
            }
        }

        /// <summary>
        /// Updates animation durations.
        /// </summary>
        public void SetAnimationDurations(float open, float close, float subMenu)
        {
            openDuration = open;
            closeDuration = close;
            subMenuExpandDuration = subMenu;
        }

        #endregion
    }
}
