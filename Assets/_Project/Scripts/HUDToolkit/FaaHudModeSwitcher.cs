using System;
using System.Collections.Generic;
using FAA.Customization;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.HUDToolkit
{
    [DefaultExecutionOrder(9100)]
    [AddComponentMenu("FAA/HUD/HUD Mode Switcher")]
    public class FaaHudModeSwitcher : MonoBehaviour
    {
        public enum HudMode
        {
            LegacyUGUI = 0,
            UIToolkit = 1
        }

        [Header("Mode")]
        [SerializeField] private HudMode activeMode = HudMode.LegacyUGUI;
        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool enableHotkey = true;
        [SerializeField] private KeyCode switchKey = KeyCode.F8;
        [SerializeField] private int legacyStartupReassertFrames = 240;

        [Header("Targets")]
        [SerializeField] private GameObject legacyHudRoot;
        [SerializeField] private FaaUiToolkitHud uiToolkitHud;
        [SerializeField] private FaaHudRuntimeSanitizer legacyHudSanitizer;
        [SerializeField] private bool autoFindTargets = true;
        [SerializeField] private string legacyHudName = "Second Interation GUI";
        [SerializeField] private string legacyCanvasName = "FAASymbologyCanvas";
        [SerializeField] private string[] legacyCanvasNames =
        {
            "FAASymbologyCanvas"
        };
        [SerializeField] private string[] suppressedLegacyRootNames =
        {
            "FAASymbologyCanvasWorldSpace",
            "MaskCanvas",
            "RadarCanvas",
            "VisualUnderstanding",
            "VC",
            "[Indicator System]",
            "Analysis Trigger Buttons"
        };
        [SerializeField] private string[] overlayNamesToHideInToolkitMode =
        {
            "UI Toolkit Radial Menu (Advanced)"
        };

        private readonly List<GameObject> legacyHudRoots = new List<GameObject>();
        private readonly List<GameObject> toolkitHiddenOverlays = new List<GameObject>();
        private bool _appliedInitialMode;
        private int _remainingLegacyReassertFrames;

        public HudMode ActiveMode => activeMode;
        public event Action<HudMode> OnModeChanged;

        private void Awake()
        {
            RefreshTargets();
            if (applyOnStart)
            {
                ApplyMode(activeMode);
                _appliedInitialMode = true;
            }
        }

        private void Start()
        {
            if (applyOnStart && !_appliedInitialMode)
            {
                ApplyMode(activeMode);
                _appliedInitialMode = true;
            }
        }

        private void Update()
        {
            if (enableHotkey && Input.GetKeyDown(switchKey))
            {
                ToggleMode();
            }
        }

        private void LateUpdate()
        {
            if (_remainingLegacyReassertFrames <= 0 || activeMode != HudMode.LegacyUGUI)
            {
                return;
            }

            _remainingLegacyReassertFrames--;
            ReassertLegacyHudVisible();
        }

        [ContextMenu("Refresh Targets")]
        public void RefreshTargets()
        {
            if (!autoFindTargets)
            {
                return;
            }

            if (legacyHudRoot == null || !ShouldShowLegacyRoot(legacyHudRoot))
            {
                legacyHudRoot = FindPreferredLegacyHudRoot();
            }

            legacyHudRoots.Clear();
            toolkitHiddenOverlays.Clear();
            foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform == null)
                {
                    continue;
                }

                bool isLegacyHudRoot = transform.gameObject.name == legacyHudName ||
                                       transform.gameObject.name == legacyCanvasName ||
                                       IsConfiguredLegacyCanvasName(transform.gameObject.name) ||
                                       IsConfiguredSuppressedLegacyRootName(transform.gameObject.name);
                if (isLegacyHudRoot && !legacyHudRoots.Contains(transform.gameObject))
                {
                    legacyHudRoots.Add(transform.gameObject);
                }

                if (IsConfiguredToolkitHiddenOverlay(transform.gameObject.name) &&
                    !toolkitHiddenOverlays.Contains(transform.gameObject))
                {
                    toolkitHiddenOverlays.Add(transform.gameObject);
                }
            }

            if (uiToolkitHud == null)
            {
                uiToolkitHud = FindAnyObjectByType<FaaUiToolkitHud>(FindObjectsInactive.Include);
            }

            if (legacyHudSanitizer == null)
            {
                legacyHudSanitizer = FindAnyObjectByType<FaaHudRuntimeSanitizer>(FindObjectsInactive.Include);
            }
        }

        public void SetMode(HudMode mode)
        {
            activeMode = mode;
            ApplyMode(activeMode);
        }

        [ContextMenu("Use Legacy uGUI HUD")]
        public void UseLegacyHud()
        {
            SetMode(HudMode.LegacyUGUI);
        }

        [ContextMenu("Use UI Toolkit HUD")]
        public void UseUiToolkitHud()
        {
            SetMode(HudMode.UIToolkit);
        }

        [ContextMenu("Toggle HUD Mode")]
        public void ToggleMode()
        {
            SetMode(activeMode == HudMode.LegacyUGUI ? HudMode.UIToolkit : HudMode.LegacyUGUI);
        }

        public void ApplyMode(HudMode mode)
        {
            RefreshTargets();

            bool useLegacy = mode == HudMode.LegacyUGUI;
            activeMode = mode;
            foreach (GameObject root in legacyHudRoots)
            {
                bool shouldBeActive = useLegacy && ShouldShowLegacyRoot(root);
                if (shouldBeActive)
                {
                    PrepareLegacyRootForDisplay(root);
                }

                if (root != null && root.activeSelf != shouldBeActive)
                {
                    root.SetActive(shouldBeActive);
                }
            }

            foreach (GameObject overlay in toolkitHiddenOverlays)
            {
                if (overlay != null && overlay.activeSelf != useLegacy)
                {
                    overlay.SetActive(useLegacy);
                }
            }

            bool shouldShowFallbackRoot = useLegacy && ShouldShowLegacyRoot(legacyHudRoot);
            if (shouldShowFallbackRoot)
            {
                PrepareLegacyRootForDisplay(legacyHudRoot);
            }

            if (legacyHudRoots.Count == 0 && legacyHudRoot != null && legacyHudRoot.activeSelf != shouldShowFallbackRoot)
            {
                legacyHudRoot.SetActive(shouldShowFallbackRoot);
            }

            if (legacyHudSanitizer != null)
            {
                legacyHudSanitizer.enabled = useLegacy;
                if (useLegacy)
                {
                    legacyHudSanitizer.SanitizeNow();
                }
            }

            if (uiToolkitHud != null)
            {
                uiToolkitHud.SetVisible(!useLegacy);
            }

            _remainingLegacyReassertFrames = useLegacy
                ? Mathf.Max(1, legacyStartupReassertFrames)
                : 0;
            OnModeChanged?.Invoke(mode);
        }

        private void ReassertLegacyHudVisible()
        {
            RefreshTargets();
            foreach (GameObject root in legacyHudRoots)
            {
                if (ShouldShowLegacyRoot(root))
                {
                    PrepareLegacyRootForDisplay(root);
                    if (root != null && !root.activeSelf)
                    {
                        root.SetActive(true);
                    }
                }
            }

            if (uiToolkitHud != null)
            {
                uiToolkitHud.SetVisible(false);
            }
        }

        private void PrepareLegacyRootForDisplay(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            if (root.name == legacyCanvasName || IsConfiguredLegacyCanvasName(root.name))
            {
                root.transform.localScale = Vector3.one;
            }
            else if (root.name == legacyHudName && IsPreferredScreenLegacyHud(root.transform))
            {
                root.transform.localScale = Vector3.one * 420f;
            }

            Canvas canvas = root.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = true;
                if (root.name == legacyHudName)
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 5000;
                }
            }

            UnityEngine.UI.CanvasScaler scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null)
            {
                scaler.enabled = true;
            }

            GraphicRaycaster raycaster = root.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = true;
            }
        }

        private GameObject FindPreferredLegacyHudRoot()
        {
            GameObject fallback = null;
            foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform == null || transform.gameObject.name != legacyHudName)
                {
                    continue;
                }

                fallback ??= transform.gameObject;
                if (IsPreferredScreenLegacyHud(transform))
                {
                    return transform.gameObject;
                }
            }

            return fallback;
        }

        private bool ShouldShowLegacyRoot(GameObject root)
        {
            if (root == null || IsConfiguredSuppressedLegacyRootName(root.name))
            {
                return false;
            }

            if (root.name == legacyHudName)
            {
                return IsPreferredScreenLegacyHud(root.transform);
            }

            return true;
        }

        private bool IsConfiguredLegacyCanvasName(string objectName)
        {
            if (legacyCanvasNames == null)
            {
                return false;
            }

            foreach (string legacyName in legacyCanvasNames)
            {
                if (!string.IsNullOrWhiteSpace(legacyName) && objectName == legacyName)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsConfiguredSuppressedLegacyRootName(string objectName)
        {
            if (suppressedLegacyRootNames == null)
            {
                return false;
            }

            foreach (string suppressedName in suppressedLegacyRootNames)
            {
                if (!string.IsNullOrWhiteSpace(suppressedName) && objectName == suppressedName)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsConfiguredToolkitHiddenOverlay(string objectName)
        {
            if (overlayNamesToHideInToolkitMode == null)
            {
                return false;
            }

            foreach (string overlayName in overlayNamesToHideInToolkitMode)
            {
                if (!string.IsNullOrWhiteSpace(overlayName) && objectName == overlayName)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPreferredScreenLegacyHud(Transform transform)
        {
            string path = GetHierarchyPath(transform).ToLowerInvariant();
            return path.Contains("/faasymbologycanvas/") &&
                   !path.Contains("/faasymbologycanvasworldspace/");
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            List<string> names = new List<string>();
            for (Transform current = transform; current != null; current = current.parent)
            {
                names.Add(current.name);
            }

            names.Reverse();
            return string.Join("/", names);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                RefreshTargets();
            }
        }
#endif
    }
}
