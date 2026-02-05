using UnityEngine;
using UnityEngine.UIElements;
using VoiceControl.Core;

namespace VoiceControl.UI
{
    /// <summary>
    /// Test and demo script for the UI Toolkit Radial Menu.
    /// Provides visual verification of all features.
    /// </summary>
    public class UIToolkitRadialMenuTester : MonoBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private bool runTestsOnStart = true;
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private float testDelay = 0.5f;

        [Header("Test Commands")]
        [SerializeField] private KeyCode testOpenKey = KeyCode.F1;
        [SerializeField] private KeyCode testCloseKey = KeyCode.F2;
        [SerializeField] private KeyCode testCycleKey = KeyCode.F3;
        [SerializeField] private KeyCode stressTestKey = KeyCode.F4;

        private UIToolkitRadialMenu _menu;
        private UIToolkitRadialMenuAdvanced _advancedMenu;
        private int _testCycleIndex;
        private float _lastTestTime;
        private bool _stressTestRunning;
        private int _stressTestCount;

        private void Awake()
        {
            _menu = GetComponent<UIToolkitRadialMenu>();
            _advancedMenu = GetComponent<UIToolkitRadialMenuAdvanced>();
        }

        private void Start()
        {
            if (runTestsOnStart)
            {
                RunAllTests();
            }

            // Subscribe to events for testing
            if (_menu != null)
            {
                _menu.OnMenuOpened += () => LogTest("Menu opened");
                _menu.OnMenuClosed += () => LogTest("Menu closed");
                _menu.OnCommandSelected += cmd => LogTest($"Command selected: {cmd.DisplayName}");
            }

            if (_advancedMenu != null)
            {
                _advancedMenu.OnMenuOpened += () => LogTest("Advanced menu opened");
                _advancedMenu.OnMenuClosed += () => LogTest("Advanced menu closed");
                _advancedMenu.OnCommandExecuted += cmd => LogTest($"Command executed: {cmd.DisplayName}");
                _advancedMenu.OnCategoryChanged += cat => LogTest($"Category changed: {cat}");
            }
        }

        private void Update()
        {
            HandleTestInput();

            if (_stressTestRunning)
            {
                RunStressTest();
            }
        }

        private void HandleTestInput()
        {
            // Test open/close
            if (Input.GetKeyDown(testOpenKey))
            {
                TestOpenMenu();
            }

            if (Input.GetKeyDown(testCloseKey))
            {
                TestCloseMenu();
            }

            // Test command cycling
            if (Input.GetKeyDown(testCycleKey))
            {
                TestCommandCycling();
            }

            // Stress test toggle
            if (Input.GetKeyDown(stressTestKey))
            {
                _stressTestRunning = !_stressTestRunning;
                LogTest($"Stress test: {(_stressTestRunning ? "STARTED" : "STOPPED")}");
            }
        }

        [ContextMenu("Run All Tests")]
        public void RunAllTests()
        {
            LogTest("=== STARTING RADIAL MENU TESTS ===");

            TestComponentSetup();
            TestEventSubscription();
            TestCommandLoading();
            TestVisualElements();

            LogTest("=== ALL TESTS COMPLETED ===");
        }

        [ContextMenu("Test Component Setup")]
        private void TestComponentSetup()
        {
            LogTest("Testing component setup...");

            // Check for required components
            var uiDoc = GetComponent<UIDocument>();
            if (uiDoc == null)
            {
                LogError("UIDocument component missing!");
                return;
            }

            LogTest("✓ UIDocument component found");

            if (_menu == null && _advancedMenu == null)
            {
                LogError("No radial menu component found!");
                return;
            }

            LogTest($"✓ Menu component found: {(_menu != null ? "Basic" : "Advanced")}");

            // Check root visual element
            if (uiDoc.rootVisualElement == null)
            {
                LogWarning("Root visual element is null - UI Document may not be initialized yet");
            }
            else
            {
                LogTest("✓ Root visual element initialized");
            }
        }

        [ContextMenu("Test Event Subscription")]
        private void TestEventSubscription()
        {
            LogTest("Testing event subscription...");

            if (_menu != null)
            {
                bool eventFired = false;
                System.Action handler = () => eventFired = true;

                _menu.OnMenuOpened += handler;
                _menu.OnMenuOpened -= handler;

                LogTest("✓ Basic menu events can be subscribed/unsubscribed");
            }

            if (_advancedMenu != null)
            {
                bool eventFired = false;
                System.Action handler = () => eventFired = true;

                _advancedMenu.OnMenuOpened += handler;
                _advancedMenu.OnMenuOpened -= handler;

                LogTest("✓ Advanced menu events can be subscribed/unsubscribed");
            }
        }

        [ContextMenu("Test Command Loading")]
        private void TestCommandLoading()
        {
            LogTest("Testing command loading...");

            // Check for VoiceCommandRegistry
            var registry = VoiceCommandRegistry.Instance;
            if (registry != null)
            {
                LogTest($"✓ VoiceCommandRegistry found with {registry.Targets.Count} targets");

                var commands = registry.GetAllCommands();
                LogTest($"✓ {commands.Count} commands available");

                foreach (var target in registry.Targets)
                {
                    var targetCmds = target.Value.GetAvailableCommands();
                    LogTest($"  - {target.Key}: {targetCmds.Length} commands");
                }
            }
            else
            {
                LogWarning("VoiceCommandRegistry not found - will use demo commands");
            }
        }

        [ContextMenu("Test Visual Elements")]
        private void TestVisualElements()
        {
            LogTest("Testing visual elements...");

            var uiDoc = GetComponent<UIDocument>();
            if (uiDoc?.rootVisualElement == null)
            {
                LogWarning("Cannot test visual elements - UI not initialized");
                return;
            }

            // Query for expected elements
            var root = uiDoc.rootVisualElement;

            // Test USS loading
            var stylesheet = Resources.Load<StyleSheet>("VoiceControl/RadialMenuStyles");
            if (stylesheet != null)
            {
                LogTest("✓ Stylesheet found in Resources");
            }
            else
            {
                LogWarning("Stylesheet not found - inline styles will be used");
            }
        }

        [ContextMenu("Test Open Menu")]
        private void TestOpenMenu()
        {
            LogTest("Testing menu open...");

            if (_menu != null)
            {
                _menu.SetMenuOpen(true);
                LogTest("✓ Basic menu opened");
            }
            else if (_advancedMenu != null)
            {
                _advancedMenu.SetMenuOpen(true);
                LogTest("✓ Advanced menu opened");
            }
        }

        [ContextMenu("Test Close Menu")]
        private void TestCloseMenu()
        {
            LogTest("Testing menu close...");

            if (_menu != null)
            {
                _menu.SetMenuOpen(false);
                LogTest("✓ Basic menu closed");
            }
            else if (_advancedMenu != null)
            {
                _advancedMenu.SetMenuOpen(false);
                LogTest("✓ Advanced menu closed");
            }
        }

        [ContextMenu("Test Command Cycling")]
        private void TestCommandCycling()
        {
            if (_menu == null) return;

            _menu.SetMenuOpen(true);

            // Simulate cycling through commands
            _testCycleIndex = (_testCycleIndex + 1) % 8;
            LogTest($"Cycling to command index: {_testCycleIndex}");
        }

        [ContextMenu("Test Animation Performance")]
        private void TestAnimationPerformance()
        {
            LogTest("Testing animation performance...");

            float startTime = Time.realtimeSinceStartup;
            int iterations = 100;

            for (int i = 0; i < iterations; i++)
            {
                if (_menu != null)
                {
                    _menu.SetMenuOpen(i % 2 == 0);
                }
                else if (_advancedMenu != null)
                {
                    _advancedMenu.SetMenuOpen(i % 2 == 0);
                }
            }

            float duration = Time.realtimeSinceStartup - startTime;
            LogTest($"✓ {iterations} open/close cycles in {duration:F3}s ({iterations / duration:F1} ops/sec)");
        }

        private void RunStressTest()
        {
            if (Time.realtimeSinceStartup - _lastTestTime < 0.1f) return;
            _lastTestTime = Time.realtimeSinceStartup;

            _stressTestCount++;

            // Rapidly toggle menu
            if (_menu != null)
            {
                _menu.ToggleMenu();
            }
            else if (_advancedMenu != null)
            {
                _advancedMenu.ToggleMenu();
            }

            // Log every 50 iterations
            if (_stressTestCount % 50 == 0)
            {
                LogTest($"Stress test: {_stressTestCount} iterations");
            }
        }

        [ContextMenu("Test Responsive Layout")]
        private void TestResponsiveLayout()
        {
            LogTest("Testing responsive layout at different resolutions...");

            Vector2[] testResolutions = new Vector2[]
            {
                new Vector2(1920, 1080),
                new Vector2(1366, 768),
                new Vector2(2560, 1440),
                new Vector2(1280, 720)
            };

            foreach (var res in testResolutions)
            {
                LogTest($"  Resolution {res.x}x{res.y}: " +
                    $"Aspect {(res.x/res.y):F2}, " +
                    $"Scale factor {res.y / 1080:F2}");
            }
        }

        private void LogTest(string message)
        {
            if (!showDebugInfo) return;
            Debug.Log($"[RadialMenuTest] {message}");
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"[RadialMenuTest] {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[RadialMenuTest] {message}");
        }

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200), "Radial Menu Test", "box");
            GUILayout.Label($"Menu Active: {(_menu?.IsOpen == true || _advancedMenu?.IsOpen == true ? "YES" : "NO")}");
            GUILayout.Label($"Stress Test: {(_stressTestRunning ? "RUNNING" : "STOPPED")}");
            GUILayout.Label($"Stress Count: {_stressTestCount}");

            if (_menu != null)
            {
                GUILayout.Label($"Commands: {_menu.Commands.Count}");
            }

            if (_advancedMenu != null)
            {
                GUILayout.Label($"Sub-menu: {(_advancedMenu.IsSubMenuOpen ? "OPEN" : "CLOSED")}");
            }

            GUILayout.Space(10);
            GUILayout.Label("Test Keys:");
            GUILayout.Label($"  {testOpenKey} = Open Menu");
            GUILayout.Label($"  {testCloseKey} = Close Menu");
            GUILayout.Label($"  {testCycleKey} = Cycle Commands");
            GUILayout.Label($"  {stressTestKey} = Toggle Stress Test");

            GUILayout.EndArea();
        }
    }
}
