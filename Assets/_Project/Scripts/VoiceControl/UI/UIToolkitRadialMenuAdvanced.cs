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
    [DefaultExecutionOrder(32000)]
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("Voice Control/UI Toolkit/Advanced Radial Menu")]
    [ExecuteInEditMode]
    public class UIToolkitRadialMenuAdvanced : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Menu Structure")]
        [SerializeField] private bool applyAviationHudPresetOnAwake = true;
        [SerializeField] private int mainSegmentCount = 4;
        [SerializeField] private float innerRadius = 128f;
        [SerializeField] private float middleRadius = 275f;
        [SerializeField] private float outerRadius = 390f;
        [SerializeField] private bool enableSubMenus = true;
        [SerializeField] private bool startCollapsed = true;
        [SerializeField] private float collapsedButtonSize = 58f;
        [SerializeField] private Vector2 collapsedButtonPosition = new Vector2(34f, 34f);
        [SerializeField] private bool collapsedButtonTopRight = true;
        [SerializeField, Range(4, 8)] private int maxSubSegmentCount = 6;

        [Header("Input")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
        [SerializeField] private KeyCode closeKey = KeyCode.Escape;
        [SerializeField] private bool useMouseWheel = true;
        [SerializeField] private bool useGestures = true;
        [SerializeField] private float gestureSensitivity = 1.5f;

        [Header("Animation")]
        [SerializeField] private float openDuration = 0.28f;
        [SerializeField] private float closeDuration = 0.18f;
        [SerializeField] private float subMenuExpandDuration = 0.18f;
        [SerializeField] private AnimationCurve springCurve;
        [SerializeField] private AnimationCurve bounceCurve;
        [SerializeField] private bool reducedMotion = false;
        [SerializeField, Range(0.01f, 0.08f)] private float mainSegmentStagger = 0.025f;
        [SerializeField, Range(0.01f, 0.08f)] private float subSegmentStagger = 0.02f;
        [SerializeField, Range(0f, 0.14f)] private float hoverScaleBoost = 0.06f;

        [Header("Visual Effects")]
        [SerializeField] private bool useRippleEffect = true;
        [SerializeField] private bool usePulseAnimation = true;
        [SerializeField] private bool useGradientBackground = true;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField, Range(0.3f, 1f)] private float menuTransparency = 0.98f;
        [SerializeField, Range(0.5f, 1f)] private float ringBackgroundTransparency = 0.78f;
        [SerializeField, Range(0.5f, 1f)] private float segmentTransparency = 0.98f;
        [SerializeField, Range(0.7f, 1f)] private float centerTransparency = 0.99f;

        [Header("Backdrop")]
        [SerializeField] private bool useBackdrop = true;
        [SerializeField, Range(0f, 0.65f)] private float backdropOpacity = 0.30f;
        [SerializeField] private bool closeOnBackdropClick = true;

        [Header("HUD Suppression")]
        [SerializeField] private bool hideHudWhileOpen = true;
        [SerializeField] private string[] hudRootNamesToHide =
        {
            "Second Interation GUI",
            "FAA UI Toolkit HUD",
            "FAASymbologyCanvasWorldSpace"
        };

        [Header("Audio Feedback")]
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;
        [SerializeField] private AudioClip selectSound;
        [SerializeField] private AudioClip executeSound;

        private const float MainSegmentWidth = 168f;
        private const float MainSegmentHeight = 84f;
        private const float SubSegmentWidth = 158f;
        private const float SubSegmentHeight = 68f;
        private const float MainIconContainerSize = 46f;
        private const float MainIconSize = 36f;
        private const float SubIconContainerSize = 30f;
        private const float SubIconSize = 22f;

        [Header("Typography")]
        [SerializeField] private float mainLabelFontSize = 16f;
        [SerializeField] private float subLabelFontSize = 13f;
        [SerializeField] private float centerTitleFontSize = 21f;
        [SerializeField] private float centerSubtitleFontSize = 13f;
        private static readonly Color PanelBackgroundColor = new Color(0.004f, 0.018f, 0.026f, 0.985f);
        private static readonly Color SegmentBackgroundColor = new Color(0.012f, 0.055f, 0.070f, 0.985f);
        private static readonly Color SegmentBorderColor = new Color(0.18f, 0.92f, 0.76f, 0.72f);
        private static readonly Color SubBorderBaseColor = new Color(0.24f, 0.86f, 0.78f, 0.62f);
        private const float SubMenuSpreadDegrees = 126f;
        private const float SubMenuInnerOffset = 42f;
        private const float SubMenuOuterInset = 0f;
        private const float CenterSizePadding = 54f;

        // Events
        public event Action<MenuCommand> OnCommandExecuted;
        public event Action<string> OnCategoryChanged;
        public event Action OnMenuOpened;
        public event Action OnMenuClosed;

        // UI Elements
        private VisualElement _root;
        private VisualElement _scrim;
        private VisualElement _menuRoot;
        private VisualElement _collapsedButton;  // Small circular button when collapsed
        private VisualElement _collapsedIcon;
        private VisualElement _ringBackground;
        private VisualElement _centerInfo;
        private Label _centerTitle;
        private Label _centerSubtitle;
        private VisualElement _rippleContainer;
        private VisualElement _gestureIndicator;
        private VisualElement _builtRoot;

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
        private bool _uiBuilt;

        private readonly List<HudVisibilityState> _hiddenHudTargets = new List<HudVisibilityState>();
        private bool _hudSuppressed;

#if UNITY_EDITOR
        private bool _editorRefreshQueued;
#endif

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

        private class HudVisibilityState
        {
            public GameObject Target;
            public bool WasActive;
        }

        private List<Ripple> _ripples = new List<Ripple>();

        private void Awake()
        {
            if (applyAviationHudPresetOnAwake)
            {
                ApplyAviationHudPreset(false);
            }

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

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private static void SetBorderColor(VisualElement element, Color color)
        {
            if (element == null) return;

            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
        }

        private static void SetBorderWidth(VisualElement element, float width)
        {
            if (element == null) return;

            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
        }

        private static void SetRadius(VisualElement element, float radius)
        {
            if (element == null) return;

            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Hot-reload: refresh UI when properties change in editor
            if (!Application.isPlaying && uiDocument != null && !_editorRefreshQueued)
            {
                _editorRefreshQueued = true;
                EditorApplication.delayCall += () =>
                {
                    _editorRefreshQueued = false;
                    if (this != null && uiDocument != null)
                    {
                        RefreshUI();
                    }
                };
            }
        }

        /// <summary>
        /// Refreshes the UI tree without forcing the edit-mode preview open.
        /// </summary>
        [ContextMenu("Refresh UI")]
        public void RefreshUI()
        {
            RefreshUI(false);
        }

        public void RefreshUI(bool showPreview)
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            // Clean up existing UI
            CleanupUI();

            // Rebuild UI
            SetupUI();

            if (!Application.isPlaying)
            {
                if (showPreview)
                {
                    ShowEditorPreview();
                }
                else
                {
                    HideEditorPreview();
                }
            }
        }

        private void CleanupUI()
        {
            if (_root == null) return;

            // Remove all dynamically created elements
            _menuRoot?.RemoveFromHierarchy();
            _collapsedButton?.RemoveFromHierarchy();
            _scrim?.RemoveFromHierarchy();

            _mainSegments.Clear();
            _subSegments.Clear();
            _ripples.Clear();
            _scrim = null;
            _uiBuilt = false;
            _builtRoot = null;
        }

        public void ShowEditorPreview()
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

        public void HideEditorPreview()
        {
            _isOpen = false;
            _isAnimating = false;
            _subMenuOpen = false;
            _openProgress = 0f;
            _subMenuProgress = 0f;
            _selectedMainIndex = -1;
            _selectedSubIndex = -1;

            if (_menuRoot != null)
            {
                _menuRoot.style.display = DisplayStyle.None;
                _menuRoot.style.opacity = 0f;
            }

            if (_scrim != null)
            {
                _scrim.style.display = DisplayStyle.None;
                _scrim.style.opacity = 0f;
            }

            if (_collapsedButton != null)
            {
                _collapsedButton.style.display = DisplayStyle.None;
                _collapsedButton.style.opacity = 1f;
                _collapsedButton.style.scale = new Scale(Vector3.one);
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
            RestoreHudAfterMenu();

            var registry = VoiceCommandRegistry.Instance;
            if (registry != null)
                registry.OnRegistryUpdated -= OnRegistryUpdated;

            _ripples.Clear();
        }

        private void OnDestroy()
        {
            RestoreHudAfterMenu();
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

        private void LateUpdate()
        {
            if (Application.isPlaying && hideHudWhileOpen && _hudSuppressed && (_isOpen || _isAnimating))
            {
                EnforceHudSuppression();
            }
        }

        private void SetupUI()
        {
            if (uiDocument == null) return;

            _root = uiDocument.rootVisualElement;
            if (_root == null) return;

            if (_uiBuilt && _builtRoot == _root)
            {
                ApplyInlineStyles();
                return;
            }

            _mainSegments.Clear();
            _subSegments.Clear();
            _ripples.Clear();

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

            _scrim = _root.Q<VisualElement>("MenuBackdrop");
            if (_scrim == null)
            {
                _scrim = new VisualElement();
                _scrim.name = "MenuBackdrop";
                _scrim.AddToClassList("adv-menu-backdrop");
                _root.Add(_scrim);
            }
            _scrim.pickingMode = closeOnBackdropClick ? PickingMode.Position : PickingMode.Ignore;
            _scrim.UnregisterCallback<ClickEvent>(OnBackdropClick);
            _scrim.RegisterCallback<ClickEvent>(OnBackdropClick);

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
            _scrim.SendToBack();
            _menuRoot.BringToFront();
            _collapsedButton.BringToFront();

            // Start collapsed or closed based on setting
            if (startCollapsed)
            {
                _scrim.style.display = DisplayStyle.None;
                _menuRoot.style.display = DisplayStyle.None;
                _collapsedButton.style.display = DisplayStyle.Flex;
            }
            else
            {
                _isOpen = false;
                _openProgress = 0f;
                _scrim.style.display = DisplayStyle.None;
                _menuRoot.style.display = DisplayStyle.None;
                if (_collapsedButton != null)
                {
                    _collapsedButton.style.display = DisplayStyle.None;
                }
            }

            _uiBuilt = true;
            _builtRoot = _root;
        }

        private void CreateCollapsedButton()
        {
            _collapsedButton = new VisualElement();
            _collapsedButton.name = "CollapsedButton";
            _collapsedButton.AddToClassList("adv-collapsed-button");
            _collapsedButton.pickingMode = PickingMode.Position;
            _collapsedButton.focusable = true;
            _collapsedButton.tabIndex = 0;
            _collapsedButton.tooltip = "Open HUD command menu";

            // Style the collapsed button
            _collapsedButton.style.position = Position.Absolute;
            ApplyCollapsedButtonAnchor();
            _collapsedButton.style.width = collapsedButtonSize;
            _collapsedButton.style.height = collapsedButtonSize;
            _collapsedButton.style.backgroundColor = PanelBackgroundColor;
            SetRadius(_collapsedButton, collapsedButtonSize / 2);
            SetBorderWidth(_collapsedButton, 2);
            SetBorderColor(_collapsedButton, SegmentBorderColor);
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
                _collapsedButton.style.backgroundColor = new Color(0.035f, 0.075f, 0.075f, 0.98f);
                SetBorderColor(_collapsedButton, new Color(0.25f, 1f, 0.72f, 0.75f));
            });

            _collapsedButton.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                _collapsedButton.style.scale = new Scale(Vector3.one);
                _collapsedButton.style.backgroundColor = PanelBackgroundColor;
                SetBorderColor(_collapsedButton, SegmentBorderColor);
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
            _collapsedButton.RegisterCallback<KeyDownEvent>(OnCollapsedButtonKeyDown);

            _root.Add(_collapsedButton);
        }

        private void ConfigureCollapsedButton()
        {
            // Configure existing collapsed button from UXML template
            _collapsedButton.pickingMode = PickingMode.Position;
            _collapsedButton.focusable = true;
            _collapsedButton.tabIndex = 0;
            _collapsedButton.tooltip = "Open HUD command menu";

            // Update position and size from serialized fields
            ApplyCollapsedButtonAnchor();
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
            _collapsedButton.UnregisterCallback<KeyDownEvent>(OnCollapsedButtonKeyDown);
            _collapsedButton.RegisterCallback<KeyDownEvent>(OnCollapsedButtonKeyDown);
        }

        private void OnCollapsedButtonKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter && evt.keyCode != KeyCode.Space)
            {
                return;
            }

            ExpandFromCollapsed();
            evt.StopPropagation();
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
                segment.Container.RegisterCallback<ClickEvent>(evt =>
                {
                    OnMainSegmentClick(index);
                    evt.StopPropagation();
                });

                _menuRoot.Add(segment.Container);
                _mainSegments.Add(segment);
            }
        }

        private void CreateSubSegments()
        {
            int segmentLimit = Mathf.Clamp(maxSubSegmentCount, 4, 8);
            for (int i = 0; i < segmentLimit; i++)
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
                segment.Container.RegisterCallback<ClickEvent>(evt =>
                {
                    OnSubSegmentClick(index);
                    evt.StopPropagation();
                });

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
            if (_scrim != null)
            {
                _scrim.style.position = Position.Absolute;
                _scrim.style.left = 0;
                _scrim.style.top = 0;
                _scrim.style.right = 0;
                _scrim.style.bottom = 0;
                _scrim.style.backgroundColor = new Color(0f, 0.014f, 0.018f, Mathf.Clamp01(backdropOpacity));
                _scrim.style.opacity = 0f;
                _scrim.style.display = DisplayStyle.None;
            }

            // Menu root - centered on screen
            _menuRoot.style.position = Position.Absolute;
            _menuRoot.style.left = new Length(50, LengthUnit.Percent);
            _menuRoot.style.top = new Length(50, LengthUnit.Percent);
            _menuRoot.style.width = 0;
            _menuRoot.style.height = 0;

            float ringSize = middleRadius * 2 + 70;
            _ringBackground.style.position = Position.Absolute;
            _ringBackground.style.width = ringSize;
            _ringBackground.style.height = ringSize;
            _ringBackground.style.left = -ringSize / 2;
            _ringBackground.style.top = -ringSize / 2;
            _ringBackground.style.backgroundColor = WithAlpha(PanelBackgroundColor, ringBackgroundTransparency);
            SetRadius(_ringBackground, ringSize / 2);
            SetBorderWidth(_ringBackground, 2f);
            SetBorderColor(_ringBackground, new Color(0.12f, 0.92f, 0.78f, 0.62f * menuTransparency));

            // Main segments - compact, readable buttons
            float segmentWidth = MainSegmentWidth;
            float segmentHeight = MainSegmentHeight;
            foreach (var seg in _mainSegments)
            {
                seg.Container.style.position = Position.Absolute;
                seg.Container.style.width = segmentWidth;
                seg.Container.style.height = segmentHeight;
                seg.Container.style.backgroundColor = WithAlpha(SegmentBackgroundColor, segmentTransparency);
                SetRadius(seg.Container, 10);
                SetBorderWidth(seg.Container, 2f);
                SetBorderColor(seg.Container, SegmentBorderColor);
                seg.Container.style.alignItems = Align.Center;
                seg.Container.style.justifyContent = Justify.Center;
                seg.Container.style.flexDirection = FlexDirection.Column;
                seg.Container.style.paddingTop = 7;
                seg.Container.style.paddingBottom = 7;

                seg.Background.style.position = Position.Absolute;
                seg.Background.style.width = new Length(100, LengthUnit.Percent);
                seg.Background.style.height = new Length(100, LengthUnit.Percent);
                seg.Background.style.left = 0;
                seg.Background.style.top = 0;
                SetRadius(seg.Background, 10);
                seg.Background.style.backgroundColor = new Color(0.04f, 0.18f, 0.16f, 0.10f);

                // Icon container - holds the image
                seg.IconContainer.style.position = Position.Relative;
                seg.IconContainer.style.width = MainIconContainerSize;
                seg.IconContainer.style.height = MainIconContainerSize;
                seg.IconContainer.style.alignItems = Align.Center;
                seg.IconContainer.style.justifyContent = Justify.Center;
                seg.IconContainer.style.marginBottom = 5;

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
                seg.NameLabel.style.whiteSpace = WhiteSpace.Normal;
                seg.NameLabel.style.letterSpacing = 0;
            }

            // Sub segments - compact secondary buttons
            float subWidth = SubSegmentWidth;
            float subHeight = SubSegmentHeight;
            foreach (var seg in _subSegments)
            {
                seg.Container.style.position = Position.Absolute;
                seg.Container.style.width = subWidth;
                seg.Container.style.height = subHeight;
                seg.Container.style.backgroundColor = WithAlpha(SegmentBackgroundColor, segmentTransparency);
                SetRadius(seg.Container, 9);
                SetBorderWidth(seg.Container, 1.5f);
                SetBorderColor(seg.Container, SubBorderBaseColor);
                seg.Container.style.alignItems = Align.Center;
                seg.Container.style.justifyContent = Justify.Center;
                seg.Container.style.flexDirection = FlexDirection.Column;
                seg.Container.style.paddingTop = 6;
                seg.Container.style.paddingBottom = 6;
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
                SetRadius(seg.Background, 9);

                seg.IconContainer.style.width = SubIconContainerSize;
                seg.IconContainer.style.height = SubIconContainerSize;
                seg.IconContainer.style.alignItems = Align.Center;
                seg.IconContainer.style.justifyContent = Justify.Center;
                seg.IconContainer.style.marginBottom = 3;
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
                seg.NameLabel.style.whiteSpace = WhiteSpace.Normal;
            }

            // Center info - LARGE prominent center hub
            float centerSize = innerRadius * 2 - CenterSizePadding;
            _centerInfo.style.position = Position.Absolute;
            _centerInfo.style.width = centerSize;
            _centerInfo.style.height = centerSize;
            _centerInfo.style.left = -centerSize / 2;
            _centerInfo.style.top = -centerSize / 2;
            _centerInfo.style.backgroundColor = WithAlpha(PanelBackgroundColor, centerTransparency);
            SetRadius(_centerInfo, centerSize / 2);
            SetBorderWidth(_centerInfo, 2);
            SetBorderColor(_centerInfo, new Color(0.25f, 1f, 0.72f, 0.62f * menuTransparency));
            _centerInfo.style.alignItems = Align.Center;
            _centerInfo.style.justifyContent = Justify.Center;

            // Center title - LARGE sharp text
            _centerTitle.style.fontSize = centerTitleFontSize;
            _centerTitle.style.color = new Color(0.35f, 1f, 0.72f, 1f);
            _centerTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _centerTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _centerTitle.style.letterSpacing = 0;

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
                        _mainSegments[i].IconImage.style.backgroundColor = WithAlpha(_categories[i].Color, 0.32f);
                        SetRadius(_mainSegments[i].IconImage, 32);

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
                    SetMainSegmentHover(_mainSegments[i], _selectedMainIndex == i);
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

            float curvedOpen = reducedMotion ? _openProgress : springCurve.Evaluate(_openProgress);
            float clampedOpen = Mathf.Clamp01(curvedOpen);

            if (_scrim != null)
            {
                bool showBackdrop = useBackdrop && (_isOpen || _isAnimating);
                _scrim.style.display = showBackdrop ? DisplayStyle.Flex : DisplayStyle.None;
                _scrim.style.opacity = showBackdrop ? clampedOpen : 0f;
            }

            _menuRoot.style.opacity = clampedOpen;

            // Animate ring background
            float ringScale = 0.8f + 0.2f * curvedOpen;
            _ringBackground.style.scale = new Scale(new Vector3(ringScale, ringScale, 1));
            _ringBackground.style.opacity = clampedOpen;

            // Animate main segments - positioned on ring between inner and middle radius
            float segmentWidth = MainSegmentWidth;
            float segmentHeight = MainSegmentHeight;
            for (int i = 0; i < _mainSegments.Count; i++)
            {
                var seg = _mainSegments[i];
                if (seg.Category == null) continue;

                float stagger = reducedMotion ? 0f : i * mainSegmentStagger;
                float segProgress = Mathf.Clamp01((_openProgress - stagger) / (1f - stagger));
                float segCurved = reducedMotion ? segProgress : springCurve.Evaluate(segProgress);

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
                float selectionBoost = GetMainSegmentScaleBoost(seg, i);
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
                float subWidth = SubSegmentWidth;
                float subHeight = SubSegmentHeight;
                int visibleCount = _subSegments.Count(s => s.IsVisible);

                for (int i = 0; i < _subSegments.Count; i++)
                {
                    var seg = _subSegments[i];
                    if (!seg.IsVisible) continue;

                    float stagger = reducedMotion ? 0f : i * subSegmentStagger;
                    float segProgress = Mathf.Clamp01((_subMenuProgress - stagger) / (1f - stagger));
                    segProgress = reducedMotion ? segProgress : bounceCurve.Evaluate(segProgress);

                    // Position relative to parent segment - fan out from the selected main segment
                    float baseAngle = _mainSegments[_selectedMainIndex].Angle + _rotationOffset;
                    float spread = SubMenuSpreadDegrees;  // degrees spread for sub-items
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
                    float hoverBoost = _selectedSubIndex == i ? 1f + hoverScaleBoost : 1f;
                    seg.Container.style.scale = new Scale(new Vector3(segProgress * hoverBoost, segProgress * hoverBoost, 1));
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
            _centerInfo.style.opacity = clampedOpen;

            // Check animation complete
            if (Mathf.Approximately(_openProgress, targetOpen) &&
                (!_subMenuOpen || Mathf.Approximately(_subMenuProgress, _subMenuOpen ? 1f : 0f)))
            {
                _isAnimating = false;
                if (!_isOpen)
                {
                    _menuRoot.style.display = DisplayStyle.None;
                    if (_scrim != null)
                    {
                        _scrim.style.display = DisplayStyle.None;
                        _scrim.style.opacity = 0f;
                    }
                    RestoreHudAfterMenu();
                }
            }
        }

        private float GetMainSegmentScaleBoost(MainSegment seg, int index)
        {
            float boost = 1f;
            if (_selectedMainIndex == index)
            {
                boost += hoverScaleBoost;
            }

            if (seg.IsHovered)
            {
                boost += hoverScaleBoost * 0.5f;
            }

            return boost;
        }

        private void UpdateGestureRecognition()
        {
            if (!useGestures) return;

            Vector2 currentMousePos = Input.mousePosition;

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
            if (seg.Category == null) return;

            float pulse = 0.30f + 0.08f * Mathf.Sin(Time.unscaledTime * 3f);
            seg.Background.style.backgroundColor = WithAlpha(seg.Category.Color, pulse);
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
            SetRadius(ripple, 10);
            ripple.style.backgroundColor = WithAlpha(color, 0.28f);

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

            if (_subMenuOpen)
            {
                var visibleIndexes = _subSegments
                    .Where(s => s.IsVisible)
                    .Select(s => s.Index)
                    .ToList();
                if (visibleIndexes.Count == 0)
                {
                    return;
                }

                int currentPosition = visibleIndexes.IndexOf(_selectedSubIndex);
                if (currentPosition < 0)
                {
                    currentPosition = direction > 0 ? -1 : 0;
                }

                int newPosition = (currentPosition + direction + visibleIndexes.Count) % visibleIndexes.Count;
                SelectSubSegment(visibleIndexes[newPosition]);
            }
            else
            {
                int newIndex = (_selectedMainIndex + direction + _categories.Count) % _categories.Count;
                SelectMainSegment(newIndex);
            }

            PlaySound(selectSound);
        }

        private void OnMainSegmentHover(int index, bool hovered)
        {
            if (!_isOpen) return;

            _mainSegments[index].IsHovered = hovered;
            SetMainSegmentHover(_mainSegments[index], hovered || _selectedMainIndex == index);

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
                SetMainSegmentHover(prev, prev.IsHovered);
            }

            _selectedMainIndex = index;
            var seg = _mainSegments[index];
            seg.Container.AddToClassList("adv-main-selected");
            SetMainSegmentHover(seg, true);

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

            if (_selectedSubIndex >= 0 && _selectedSubIndex < _subSegments.Count)
            {
                SetSubSegmentHover(_subSegments[_selectedSubIndex], false);
            }

            _selectedSubIndex = index;
            SetSubSegmentHover(_subSegments[index], true);

            var cmd = _subSegments[index].Command;
            if (cmd != null)
            {
                _centerSubtitle.text = cmd.DisplayName;
            }
        }

        private void SetMainSegmentHover(MainSegment seg, bool active)
        {
            if (seg == null || seg.Category == null) return;

            Color accent = seg.Category.Color;
            seg.Background.style.backgroundColor = WithAlpha(accent, active ? 0.22f : 0.08f);
            seg.Container.style.backgroundColor = active
                ? new Color(0.018f, 0.10f, 0.095f, segmentTransparency)
                : WithAlpha(SegmentBackgroundColor, segmentTransparency);
            SetBorderColor(seg.Container, active ? WithAlpha(accent, 0.82f) : SegmentBorderColor);
            seg.IconContainer.style.opacity = active ? 1f : 0.86f;
            seg.NameLabel.style.color = active
                ? new Color(0.95f, 1f, 0.96f, 1f)
                : new Color(0.86f, 0.92f, 0.90f, 0.94f);
        }

        private void SetSubSegmentHover(SubSegment seg, bool hovered)
        {
            if (seg == null || seg.Command == null) return;

            var baseColor = seg.Command.Color;
            float bgAlpha = hovered ? 0.28f : 0.10f;
            var borderColor = Color.Lerp(SubBorderBaseColor, baseColor, hovered ? 0.8f : 0.2f);

            float scale = hovered ? 1f + hoverScaleBoost : 1f;
            seg.Container.style.scale = new Scale(new Vector3(scale, scale, 1f));
            seg.IconContainer.style.scale = new Scale(new Vector3(hovered ? 1.08f : 1f, hovered ? 1.08f : 1f, 1f));
            seg.IconContainer.style.opacity = hovered ? 1f : 0.9f;

            SetBorderColor(seg.Container, borderColor);

            seg.Background.style.backgroundColor = WithAlpha(baseColor, bgAlpha);
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
            int visibleCommandCount = Mathf.Min(category.Commands.Count, _subSegments.Count);

            // Setup sub segments
            for (int i = 0; i < _subSegments.Count; i++)
            {
                var seg = _subSegments[i];
                if (i < visibleCommandCount)
                {
                    seg.Command = category.Commands[i];
                    TrySetIcon(seg.IconImage, seg.Command.IconPath, Color.white);
                    seg.NameLabel.text = seg.Command.DisplayName;
                    seg.Background.style.backgroundColor = WithAlpha(category.Color, 0.10f);
                    seg.IsVisible = true;
                    SetSubSegmentHover(seg, false);
                }
                else
                {
                    seg.IsVisible = false;
                    seg.Command = null;
                    seg.Container.style.display = DisplayStyle.None;
                }
            }

            _selectedSubIndex = -1;
            _centerSubtitle.text = category.Commands.Count > visibleCommandCount
                ? $"{visibleCommandCount}/{category.Commands.Count} commands"
                : $"{category.Commands.Count} commands";
        }

        private void CloseSubMenu()
        {
            _subMenuOpen = false;
            _selectedSubIndex = -1;

            if (_selectedMainIndex < 0 || _selectedMainIndex >= _mainSegments.Count)
            {
                return;
            }

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
            if (ShouldLetHudCommandOwnVisibility(cmd))
            {
                RestoreHudAfterMenu();
            }

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

        private void OnBackdropClick(ClickEvent evt)
        {
            if (!Application.isPlaying || !_isOpen || !closeOnBackdropClick)
            {
                return;
            }

            if (evt.target == _scrim)
            {
                SetMenuOpen(false);
                evt.StopPropagation();
            }
        }

        private void PrimeInitialSelection()
        {
            int selectableCount = Mathf.Min(_categories.Count, _mainSegments.Count);
            if (selectableCount <= 0)
            {
                _selectedMainIndex = -1;
                _selectedSubIndex = -1;
                if (_centerTitle != null)
                {
                    _centerTitle.text = "HUD";
                    _centerTitle.style.color = new Color(0.35f, 1f, 0.72f, 1f);
                }
                if (_centerSubtitle != null)
                {
                    _centerSubtitle.text = "No commands";
                }
                return;
            }

            _selectedMainIndex = -1;
            _selectedSubIndex = -1;
            int index = Mathf.Clamp(SelectedCategory, 0, selectableCount - 1);
            SelectMainSegment(index);

            if (enableSubMenus)
            {
                OpenSubMenu(index);
            }
        }

        private static bool ShouldLetHudCommandOwnVisibility(MenuCommand cmd)
        {
            if (cmd == null ||
                string.IsNullOrWhiteSpace(cmd.TargetId) ||
                string.IsNullOrWhiteSpace(cmd.CommandName))
            {
                return false;
            }

            string targetId = cmd.TargetId.Trim();
            string commandName = cmd.CommandName.Trim();

            if (string.Equals(targetId, "symbology", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(commandName, "show", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(commandName, "hide", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(targetId, "weather_radar", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetId, "traffic_radar", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(commandName, "show_panel", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(commandName, "hide_panel", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(commandName, "toggle_panel", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(targetId, "indicator_system", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(commandName, "show_all_indicators", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(commandName, "hide_all_indicators", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private void SuppressHudForMenu()
        {
            if (!Application.isPlaying || !hideHudWhileOpen)
            {
                return;
            }

            _hudSuppressed = true;
            UnityEngine.Canvas.willRenderCanvases -= EnforceHudSuppression;
            UnityEngine.Canvas.willRenderCanvases += EnforceHudSuppression;
            TrackHudTargets();
            EnforceHudSuppression();
        }

        private void EnforceHudSuppression()
        {
            if (!Application.isPlaying || !hideHudWhileOpen)
            {
                return;
            }

            _hudSuppressed = true;
            TrackHudTargets();

            for (int i = _hiddenHudTargets.Count - 1; i >= 0; i--)
            {
                HudVisibilityState state = _hiddenHudTargets[i];
                if (state.Target == null)
                {
                    _hiddenHudTargets.RemoveAt(i);
                    continue;
                }

                if (ShouldSkipHudTarget(state.Target))
                {
                    continue;
                }

                if (state.Target.activeSelf)
                {
                    state.Target.SetActive(false);
                }
            }
        }

        private void RestoreHudAfterMenu()
        {
            UnityEngine.Canvas.willRenderCanvases -= EnforceHudSuppression;

            if (!_hudSuppressed && _hiddenHudTargets.Count == 0)
            {
                return;
            }

            foreach (HudVisibilityState state in _hiddenHudTargets)
            {
                if (state.Target == null)
                {
                    continue;
                }

                if (ShouldSkipHudTarget(state.Target))
                {
                    continue;
                }

                if (state.Target.activeSelf != state.WasActive)
                {
                    state.Target.SetActive(state.WasActive);
                }
            }

            _hiddenHudTargets.Clear();
            _hudSuppressed = false;
        }

        private void TrackHudTargets()
        {
            foreach (GameObject target in FindHudObjectsToHide())
            {
                if (target == null || _hiddenHudTargets.Any(state => state.Target == target))
                {
                    continue;
                }

                _hiddenHudTargets.Add(new HudVisibilityState
                {
                    Target = target,
                    WasActive = target.activeSelf
                });
            }
        }

        private IEnumerable<GameObject> FindHudObjectsToHide()
        {
            var results = new HashSet<GameObject>();

            if (hudRootNamesToHide != null && hudRootNamesToHide.Length > 0)
            {
                foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (candidate == null || candidate.gameObject == null)
                    {
                        continue;
                    }

                    string objectName = candidate.gameObject.name;
                    bool nameMatches = hudRootNamesToHide.Any(name =>
                        !string.IsNullOrWhiteSpace(name) &&
                        string.Equals(objectName, name, StringComparison.Ordinal));

                    if (nameMatches && !ShouldSkipHudTarget(candidate.gameObject))
                    {
                        results.Add(candidate.gameObject);
                    }
                }
            }

            foreach (FAA.HUDToolkit.FaaUiToolkitHud uiHud in FindObjectsByType<FAA.HUDToolkit.FaaUiToolkitHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (uiHud != null && !ShouldSkipHudTarget(uiHud.gameObject))
                {
                    results.Add(uiHud.gameObject);
                }
            }

            return results;
        }

        private bool ShouldSkipHudTarget(GameObject target)
        {
            if (target == null || target == gameObject)
            {
                return true;
            }

            Transform targetTransform = target.transform;
            Transform menuTransform = transform;
            return targetTransform == null ||
                   menuTransform == null ||
                   menuTransform.IsChildOf(targetTransform) ||
                   targetTransform.IsChildOf(menuTransform);
        }

        public void SetMenuOpen(bool open)
        {
            if (_menuRoot == null)
            {
                SetupUI();
            }

            if (_menuRoot == null) return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (open)
                {
                    LoadCommands();
                    ShowEditorPreview();
                }
                else
                {
                    HideEditorPreview();
                }

                return;
            }
#endif

            if (_isOpen == open) return;

            _isOpen = open;
            _isAnimating = true;

            if (_isOpen)
            {
                SuppressHudForMenu();
                if (_scrim != null)
                {
                    _scrim.style.display = useBackdrop ? DisplayStyle.Flex : DisplayStyle.None;
                    _scrim.style.opacity = 0f;
                }
                _menuRoot.style.display = DisplayStyle.Flex;
                if (_collapsedButton != null)
                {
                    _collapsedButton.style.display = DisplayStyle.None;
                }

                _lastMousePos = Input.mousePosition;
                _gestureAccumulator = 0;
                LoadCommands();
                PrimeInitialSelection();
                OnMenuOpened?.Invoke();
                PlaySound(openSound);
            }
            else
            {
                _subMenuOpen = false;
                _subMenuProgress = 0;
                _selectedMainIndex = -1;
                _selectedSubIndex = -1;
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
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Command";
            }

            return string.Join(" ", name.Split('_')
                .Where(w => !string.IsNullOrEmpty(w))
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
        public bool CollapsedButtonTopRight => collapsedButtonTopRight;
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
            innerRadius = Mathf.Max(90f, inner);
            middleRadius = Mathf.Max(innerRadius + 90f, middle);
            outerRadius = Mathf.Max(middleRadius + 80f, outer);

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
            collapsedButtonSize = Mathf.Clamp(size, 48f, 96f);
            collapsedButtonPosition = position;

            if (_collapsedButton != null)
            {
                _collapsedButton.style.width = collapsedButtonSize;
                _collapsedButton.style.height = collapsedButtonSize;
                ApplyCollapsedButtonAnchor();
            }
        }

        private void ApplyCollapsedButtonAnchor()
        {
            if (_collapsedButton == null)
            {
                return;
            }

            if (collapsedButtonTopRight)
            {
                _collapsedButton.style.left = new StyleLength(StyleKeyword.Auto);
                _collapsedButton.style.bottom = new StyleLength(StyleKeyword.Auto);
                _collapsedButton.style.right = collapsedButtonPosition.x;
                _collapsedButton.style.top = collapsedButtonPosition.y;
                return;
            }

            _collapsedButton.style.right = new StyleLength(StyleKeyword.Auto);
            _collapsedButton.style.top = new StyleLength(StyleKeyword.Auto);
            _collapsedButton.style.left = collapsedButtonPosition.x;
            _collapsedButton.style.bottom = collapsedButtonPosition.y;
        }

        /// <summary>
        /// Updates animation durations.
        /// </summary>
        public void SetAnimationDurations(float open, float close, float subMenu)
        {
            openDuration = Mathf.Clamp(open, 0.05f, 0.75f);
            closeDuration = Mathf.Clamp(close, 0.05f, 0.5f);
            subMenuExpandDuration = Mathf.Clamp(subMenu, 0.05f, 0.5f);
        }

        /// <summary>
        /// Applies the compact FAA cockpit preset used by ExperimentScene.
        /// </summary>
        public void ApplyAviationHudPreset(bool refresh = true)
        {
            innerRadius = 128f;
            middleRadius = 275f;
            outerRadius = 390f;
            collapsedButtonSize = 58f;
            collapsedButtonPosition = new Vector2(34f, 34f);
            collapsedButtonTopRight = true;
            maxSubSegmentCount = 6;
            openDuration = 0.28f;
            closeDuration = 0.18f;
            subMenuExpandDuration = 0.18f;
            mainSegmentStagger = 0.025f;
            subSegmentStagger = 0.02f;
            hoverScaleBoost = 0.06f;
            menuTransparency = 1f;
            ringBackgroundTransparency = 0.94f;
            segmentTransparency = 0.99f;
            centerTransparency = 1f;
            useBackdrop = true;
            backdropOpacity = 0.46f;
            closeOnBackdropClick = true;
            hideHudWhileOpen = true;
            mainLabelFontSize = 18f;
            subLabelFontSize = 14f;
            centerTitleFontSize = 23f;
            centerSubtitleFontSize = 13f;
            usePulseAnimation = true;
            useRippleEffect = true;

            if (refresh)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    RefreshUI();
                    return;
                }
#endif
                ApplyInlineStyles();
            }
        }

        #endregion
    }
}
