using System.Collections.Generic;
using UnityEngine;
using VoiceControl.Core;
using FAA.Customization;

namespace VoiceControl.Adapters
{
    /// <summary>
    /// Voice command adapter for Symbology Color Manager.
    /// Implements IVoiceCommandTarget to expose symbology color controls to voice commands.
    /// </summary>
    [AddComponentMenu("Voice Control/Symbology Color Voice Adapter")]
    public class SymbologyColorVoiceAdapter : MonoBehaviour, IVoiceCommandTarget
    {
        private const string HeadingTapeCanvasName = "FAAHeadingTapeCanvas";

        private static readonly string[] FlightHudCanvasNames =
        {
            "FAASymbologyCanvas",
            "FAASymbologyCanvasWorldSpace",
            HeadingTapeCanvasName
        };

        private static readonly string[] ExcludedCanvasNameFragments =
        {
            "weather",
            "traffic",
            "radar",
            "indicator",
            "menu",
            "radial",
            "voicecontrol"
        };

        private sealed class HudVisibilityTarget
        {
            public CanvasGroup CanvasGroup;
            public float VisibleAlpha;
            public bool VisibleInteractable;
            public bool VisibleBlocksRaycasts;
        }

        [Header("Target Components")]
        [SerializeField] private SymbologyColorManager colorManager;
        
        [Header("Settings")]
        [SerializeField] private bool autoFindComponents = true;
        [SerializeField] private bool verboseLogging = true;
        
        public string TargetId => "symbology";
        public string DisplayName => "Symbology Color";
        
        private VoiceCommandInfo[] _commands;
        private readonly List<HudVisibilityTarget> _hudVisibilityTargets = new List<HudVisibilityTarget>();
        private bool _hudRootsVisible = true;
        
        private void Awake()
        {
            if (autoFindComponents)
            {
                AutoFindComponents();
            }
        }
        
        private void Start()
        {
            if (VoiceCommandRegistry.Instance != null)
            {
                VoiceCommandRegistry.Instance.RegisterTarget(this);
            }
        }
        
        private void OnDestroy()
        {
            if (VoiceCommandRegistry.Instance != null)
            {
                VoiceCommandRegistry.Instance.UnregisterTarget(TargetId);
            }
        }
        
        private void AutoFindComponents()
        {
            if (colorManager == null)
            {
                colorManager = FindObjectOfType<SymbologyColorManager>();
            }
            
            Log($"Found components - ColorManager: {colorManager != null}");
        }
        
        public VoiceCommandInfo[] GetAvailableCommands()
        {
            if (_commands != null)
                return _commands;
            
            _commands = new VoiceCommandInfo[]
            {
                new VoiceCommandInfo(
                    "toggle_color",
                    "Toggle symbology color between black and white (dark/light mode)"
                ),
                new VoiceCommandInfo(
                    "set_black",
                    "Set symbology color to black (dark mode)"
                ),
                new VoiceCommandInfo(
                    "set_white",
                    "Set symbology color to white (light mode)"
                ),
                new VoiceCommandInfo(
                    "set_green",
                    "Set symbology color to green"
                ),
                new VoiceCommandInfo(
                    "set_cyan",
                    "Set symbology color to cyan"
                ),
                new VoiceCommandInfo(
                    "cycle_color",
                    "Cycle through all available color presets"
                ),
                new VoiceCommandInfo(
                    "set_preset",
                    "Set symbology color to a specific preset",
                    new VoiceCommandParameter("preset", "string", "Color preset name", true, 
                        new string[] { "black", "white", "green", "cyan" })
                ),
                new VoiceCommandInfo(
                    "refresh",
                    "Refresh the symbology color cache and reapply current color"
                ),
                // Opacity/transparency commands
                new VoiceCommandInfo(
                    "set_opacity",
                    "Set the opacity/transparency of all symbology elements (0 = invisible, 1 = fully visible)",
                    new VoiceCommandParameter("opacity", "number", "Opacity value from 0 (invisible) to 1 (fully visible), or as percentage 0-100", true)
                ),
                new VoiceCommandInfo(
                    "show",
                    "Show all symbology elements (set opacity to 100%)"
                ),
                new VoiceCommandInfo(
                    "hide",
                    "Hide all symbology elements (set opacity to 0%)"
                )
            };
            
            return _commands;
        }
        
        public bool ExecuteCommand(string commandName, Dictionary<string, object> parameters)
        {
            Log($"Executing command: {commandName}");
            
            if (colorManager == null)
            {
                Log("ColorManager not found");
                return false;
            }
            
            switch (commandName.ToLower())
            {
                case "toggle_color":
                    colorManager.ToggleColor();
                    Log($"Toggled color, now: {colorManager.CurrentPreset}");
                    return true;
                    
                case "set_black":
                    colorManager.SetColorPreset(ColorPreset.Black);
                    Log("Set color to black");
                    return true;
                    
                case "set_white":
                    colorManager.SetColorPreset(ColorPreset.White);
                    Log("Set color to white");
                    return true;
                    
                case "set_green":
                    colorManager.SetColorPreset(ColorPreset.Green);
                    Log("Set color to green");
                    return true;
                    
                case "set_cyan":
                    colorManager.SetColorPreset(ColorPreset.Cyan);
                    Log("Set color to cyan");
                    return true;
                    
                case "cycle_color":
                    colorManager.CycleColorPreset();
                    Log($"Cycled color, now: {colorManager.CurrentPreset}");
                    return true;
                    
                case "set_preset":
                    return HandleSetPreset(parameters);
                    
                case "refresh":
                    colorManager.RefreshCache();
                    colorManager.ApplyColorImmediate(colorManager.CurrentColor);
                    Log("Refreshed color cache");
                    return true;
                    
                // Opacity commands
                case "set_opacity":
                    return HandleSetOpacity(parameters);
                    
                case "show":
                    colorManager.Show();
                    SetHudRootVisibility(true);
                    Log("Showed symbology (opacity 100%)");
                    return true;
                    
                case "hide":
                    colorManager.Hide();
                    SetHudRootVisibility(false);
                    Log("Hid symbology (opacity 0%)");
                    return true;
                    
                default:
                    Log($"Unknown command: {commandName}");
                    break;
            }
            
            return false;
        }
        
        private bool HandleSetPreset(Dictionary<string, object> parameters)
        {
            if (parameters == null || !parameters.TryGetValue("preset", out var presetObj))
            {
                Log("Preset parameter not provided");
                return false;
            }
            
            string presetName = presetObj?.ToString()?.ToLower() ?? "";
            ColorPreset preset;
            
            switch (presetName)
            {
                case "black":
                case "dark":
                    preset = ColorPreset.Black;
                    break;
                case "white":
                case "light":
                    preset = ColorPreset.White;
                    break;
                case "green":
                    preset = ColorPreset.Green;
                    break;
                case "cyan":
                case "blue":
                    preset = ColorPreset.Cyan;
                    break;
                default:
                    Log($"Unknown preset: {presetName}");
                    return false;
            }
            
            colorManager.SetColorPreset(preset);
            Log($"Set color preset to {preset}");
            return true;
        }
        
        private bool HandleSetOpacity(Dictionary<string, object> parameters)
        {
            if (parameters == null || !parameters.TryGetValue("opacity", out var opacityObj))
            {
                Log("Opacity parameter not provided");
                return false;
            }
            
            float opacity = ParseOpacityValue(opacityObj);
            colorManager.SetOpacity(opacity);
            SetHudRootVisibility(opacity > 0f);
            Log($"Set symbology opacity to {opacity:F2}");
            return true;
        }

        /// <summary>
        /// Hides complete flight-HUD roots without deactivating their GameObjects. This covers
        /// RawImages and custom UI graphics that are intentionally outside the color manager's
        /// Image/text cache while allowing telemetry and layout scripts to keep updating.
        /// </summary>
        private void SetHudRootVisibility(bool visible)
        {
            // Refresh against the previous state first. If a hidden HUD root was renamed or
            // repurposed as an excluded radar/menu canvas, pruning can restore it before the
            // new visibility state is applied.
            RefreshHudVisibilityTargets();
            _hudRootsVisible = visible;
            ApplyCachedHudVisibility(visible);
        }

        private void RefreshHudVisibilityTargets()
        {
            PruneHudVisibilityTargets();
            CacheHudVisibilityTargets(FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }

        private void CacheHudVisibilityTargets(IEnumerable<Canvas> canvases)
        {
            if (canvases == null)
            {
                return;
            }

            foreach (Canvas canvas in canvases)
            {
                if (!IsHudVisibilityRoot(canvas) || ContainsHudVisibilityTarget(canvas.gameObject))
                {
                    continue;
                }

                CanvasGroup group = canvas.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = canvas.gameObject.AddComponent<CanvasGroup>();
                }

                _hudVisibilityTargets.Add(new HudVisibilityTarget
                {
                    CanvasGroup = group,
                    VisibleAlpha = group.alpha > 0.001f ? group.alpha : 1f,
                    VisibleInteractable = group.interactable,
                    VisibleBlocksRaycasts = group.blocksRaycasts
                });

                // A HUD canvas can be created after a hide command (for example by a runtime
                // setup helper). Bring every newly discovered root into the current state.
                if (!_hudRootsVisible)
                {
                    group.alpha = 0f;
                    group.interactable = false;
                    group.blocksRaycasts = false;
                }
            }
        }

        private void ApplyCachedHudVisibility(bool visible)
        {
            for (int i = _hudVisibilityTargets.Count - 1; i >= 0; i--)
            {
                HudVisibilityTarget target = _hudVisibilityTargets[i];
                CanvasGroup group = target != null ? target.CanvasGroup : null;
                if (group == null)
                {
                    _hudVisibilityTargets.RemoveAt(i);
                    continue;
                }

                if (visible)
                {
                    group.alpha = target.VisibleAlpha > 0.001f ? target.VisibleAlpha : 1f;
                    group.interactable = target.VisibleInteractable;
                    group.blocksRaycasts = target.VisibleBlocksRaycasts;
                }
                else
                {
                    if (group.alpha > 0.001f)
                    {
                        target.VisibleAlpha = group.alpha;
                    }

                    group.alpha = 0f;
                    group.interactable = false;
                    group.blocksRaycasts = false;
                }
            }
        }

        private void PruneHudVisibilityTargets()
        {
            for (int i = _hudVisibilityTargets.Count - 1; i >= 0; i--)
            {
                HudVisibilityTarget target = _hudVisibilityTargets[i];
                CanvasGroup group = target != null ? target.CanvasGroup : null;
                Canvas canvas = group != null ? group.GetComponent<Canvas>() : null;
                if (group != null && IsHudVisibilityRoot(canvas))
                {
                    continue;
                }

                // If a cached object is repurposed/renamed as a radar or menu canvas while the
                // HUD is hidden, immediately release it from HUD visibility ownership.
                if (group != null && !_hudRootsVisible)
                {
                    group.alpha = target.VisibleAlpha > 0.001f ? target.VisibleAlpha : 1f;
                    group.interactable = target.VisibleInteractable;
                    group.blocksRaycasts = target.VisibleBlocksRaycasts;
                }

                _hudVisibilityTargets.RemoveAt(i);
            }
        }

        private bool ContainsHudVisibilityTarget(GameObject root)
        {
            for (int i = 0; i < _hudVisibilityTargets.Count; i++)
            {
                CanvasGroup group = _hudVisibilityTargets[i]?.CanvasGroup;
                if (group != null && group.gameObject == root)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsHudVisibilityRoot(Canvas canvas)
        {
            if (canvas == null || canvas.gameObject == null || !canvas.gameObject.scene.IsValid())
            {
                return false;
            }

            Transform candidate = canvas.transform;
            if (HasExcludedCanvasNameInHierarchy(candidate))
            {
                return false;
            }

            string candidateName = canvas.gameObject.name;
            for (int i = 0; i < FlightHudCanvasNames.Length; i++)
            {
                if (string.Equals(candidateName, FlightHudCanvasNames[i], System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Support authored/custom names when the canvas clearly owns the main HUD.
            string lowerCandidateName = candidateName.ToLowerInvariant();
            return lowerCandidateName.Contains("symbology") ||
                   lowerCandidateName.Contains("hud") ||
                   canvas.GetComponent<SymbologyColorManager>() != null ||
                   canvas.GetComponentInChildren<SymbologyColorManager>(true) != null ||
                   HasNamedDescendant(candidate, "Second Interation GUI");
        }

        private static bool HasExcludedCanvasNameInHierarchy(Transform candidate)
        {
            for (Transform current = candidate; current != null; current = current.parent)
            {
                string lowerName = current.gameObject.name.ToLowerInvariant();
                for (int i = 0; i < ExcludedCanvasNameFragments.Length; i++)
                {
                    if (lowerName.Contains(ExcludedCanvasNameFragments[i]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasNamedDescendant(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate != root &&
                    string.Equals(candidate.gameObject.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        
        /// <summary>
        /// Parse opacity value - handles both 0-1 range and 0-100 percentage
        /// </summary>
        private float ParseOpacityValue(object value)
        {
            float numValue = ParseNumericValue(value);
            
            // If value is greater than 1, assume it's a percentage
            if (numValue > 1f)
            {
                numValue = numValue / 100f;
            }
            
            return Mathf.Clamp01(numValue);
        }
        
        /// <summary>
        /// Parse a numeric value from various object types
        /// </summary>
        private float ParseNumericValue(object value)
        {
            if (value is float f) return f;
            if (value is double d) return (float)d;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is string s && float.TryParse(s, out float parsed)) return parsed;
            return 0f;
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[SymbologyColorVoiceAdapter] {message}");
            }
        }
    }
}
