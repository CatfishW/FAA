using UnityEngine;
using UnityEngine.UIElements;

namespace VoiceControl.UI
{
    /// <summary>
    /// Manages theme switching and UI customization for radial menus.
    /// </summary>
    public class RadialMenuThemeManager : MonoBehaviour
    {
        [SerializeField] private bool enableDarkMode = true;
        [SerializeField] private bool enableHighContrast = false;
        [SerializeField] private bool enableReducedMotion = false;

        private VisualElement _root;
        private static RadialMenuThemeManager _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument != null)
            {
                _root = uiDocument.rootVisualElement;
                ApplyTheme();
            }
        }

        /// <summary>
        /// Applies the current theme settings to the root element.
        /// </summary>
        public void ApplyTheme()
        {
            if (_root == null) return;

            // Remove existing theme classes
            _root.RemoveFromClassList("theme-light");
            _root.RemoveFromClassList("theme-dark");
            _root.RemoveFromClassList("high-contrast");
            _root.RemoveFromClassList("reduced-motion");

            // Apply dark/light mode
            if (!enableDarkMode)
            {
                _root.AddToClassList("theme-light");
            }

            // Apply high contrast if enabled
            if (enableHighContrast)
            {
                _root.AddToClassList("high-contrast");
            }

            // Apply reduced motion if enabled
            if (enableReducedMotion)
            {
                _root.AddToClassList("reduced-motion");
            }
        }

        /// <summary>
        /// Toggle between dark and light themes.
        /// </summary>
        public void ToggleDarkMode()
        {
            enableDarkMode = !enableDarkMode;
            ApplyTheme();
        }

        /// <summary>
        /// Set high contrast mode.
        /// </summary>
        public void SetHighContrast(bool enabled)
        {
            enableHighContrast = enabled;
            ApplyTheme();
        }

        /// <summary>
        /// Set reduced motion preference.
        /// </summary>
        public void SetReducedMotion(bool enabled)
        {
            enableReducedMotion = enabled;
            ApplyTheme();
        }

        public bool IsDarkModeEnabled => enableDarkMode;
        public bool IsHighContrastEnabled => enableHighContrast;
        public bool IsReducedMotionEnabled => enableReducedMotion;

        public static RadialMenuThemeManager Instance => _instance;
    }
}
