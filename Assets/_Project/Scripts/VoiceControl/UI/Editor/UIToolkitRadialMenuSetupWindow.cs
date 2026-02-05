using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace VoiceControl.UI.Editor
{
    /// <summary>
    /// Editor window for setting up and testing the UI Toolkit Radial Menu.
    /// </summary>
    public class UIToolkitRadialMenuSetupWindow : EditorWindow
    {
        private const string MENU_PATH = "Tools/Aviation/Voice Control/UI Toolkit Radial Menu Setup";

        [SerializeField] private VisualTreeAsset uxmlTemplate;

        private UIToolkitRadialMenu _basicMenu;
        private UIToolkitRadialMenuAdvanced _advancedMenu;
        private Vector2 _scrollPos;
        private bool _showBasicSettings = true;
        private bool _showAdvancedSettings = true;
        private bool _showTesting = true;

        [MenuItem(MENU_PATH)]
        public static void ShowWindow()
        {
            var window = GetWindow<UIToolkitRadialMenuSetupWindow>();
            window.titleContent = new GUIContent("UI Toolkit Radial Menu", "Radial Menu Configuration");
            window.minSize = new Vector2(400, 600);
            window.Show();
        }

        private void OnEnable()
        {
            FindMenuComponents();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            // Header
            EditorGUILayout.LabelField("UI Toolkit Radial Menu", EditorStyles.largeLabel);
            EditorGUILayout.LabelField("Configuration and Testing", EditorStyles.miniLabel);
            EditorGUILayout.Space(10);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Find menu button
            if (GUILayout.Button("Find Menu Components in Scene"))
            {
                FindMenuComponents();
            }

            EditorGUILayout.Space(10);

            // Basic Menu Section
            _showBasicSettings = EditorGUILayout.Foldout(_showBasicSettings, "Basic Radial Menu", true, EditorStyles.foldoutHeader);
            if (_showBasicSettings)
            {
                EditorGUI.indentLevel++;
                DrawBasicMenuSettings();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Advanced Menu Section
            _showAdvancedSettings = EditorGUILayout.Foldout(_showAdvancedSettings, "Advanced Radial Menu", true, EditorStyles.foldoutHeader);
            if (_showAdvancedSettings)
            {
                EditorGUI.indentLevel++;
                DrawAdvancedMenuSettings();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Testing Section
            _showTesting = EditorGUILayout.Foldout(_showTesting, "Testing", true, EditorStyles.foldoutHeader);
            if (_showTesting)
            {
                EditorGUI.indentLevel++;
                DrawTestingControls();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Setup Actions
            EditorGUILayout.LabelField("Setup Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Basic Menu"))
            {
                CreateBasicMenu();
            }
            if (GUILayout.Button("Create Advanced Menu"))
            {
                CreateAdvancedMenu();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Create Stylesheet Asset"))
            {
                CreateStylesheetAsset();
            }

            EditorGUILayout.EndScrollView();
        }

        private void FindMenuComponents()
        {
            _basicMenu = FindObjectOfType<UIToolkitRadialMenu>();
            _advancedMenu = FindObjectOfType<UIToolkitRadialMenuAdvanced>();
        }

        private void DrawBasicMenuSettings()
        {
            if (_basicMenu == null)
            {
                EditorGUILayout.HelpBox("No Basic Radial Menu found in scene. Click 'Create Basic Menu' to add one.", MessageType.Info);
                return;
            }

            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(_basicMenu);
            editor.OnInspectorGUI();
        }

        private void DrawAdvancedMenuSettings()
        {
            if (_advancedMenu == null)
            {
                EditorGUILayout.HelpBox("No Advanced Radial Menu found in scene. Click 'Create Advanced Menu' to add one.", MessageType.Info);
                return;
            }

            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(_advancedMenu);
            editor.OnInspectorGUI();
        }

        private void DrawTestingControls()
        {
            EditorGUILayout.LabelField("Menu Control", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = _basicMenu != null || _advancedMenu != null;

            if (GUILayout.Button("Open Menu"))
            {
                if (_basicMenu != null) _basicMenu.SetMenuOpen(true);
                if (_advancedMenu != null) _advancedMenu.SetMenuOpen(true);
            }

            if (GUILayout.Button("Close Menu"))
            {
                if (_basicMenu != null) _basicMenu.SetMenuOpen(false);
                if (_advancedMenu != null) _advancedMenu.SetMenuOpen(false);
            }

            if (GUILayout.Button("Toggle Menu"))
            {
                if (_basicMenu != null) _basicMenu.ToggleMenu();
                if (_advancedMenu != null) _advancedMenu.ToggleMenu();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Status display
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");

            if (_basicMenu != null)
            {
                EditorGUILayout.LabelField("Basic Menu:", _basicMenu.IsOpen ? "OPEN" : "CLOSED");
            }
            else
            {
                EditorGUILayout.LabelField("Basic Menu:", "Not Found");
            }

            if (_advancedMenu != null)
            {
                EditorGUILayout.LabelField("Advanced Menu:", _advancedMenu.IsOpen ? "OPEN" : "CLOSED");
                if (_advancedMenu.IsOpen)
                {
                    EditorGUILayout.LabelField("  Sub-menu:", _advancedMenu.IsSubMenuOpen ? "OPEN" : "CLOSED");
                    EditorGUILayout.LabelField("  Category:", _advancedMenu.SelectedCategory.ToString());
                }
            }
            else
            {
                EditorGUILayout.LabelField("Advanced Menu:", "Not Found");
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Run All Tests"))
            {
                var tester = FindObjectOfType<UIToolkitRadialMenuTester>();
                if (tester != null)
                {
                    tester.SendMessage("RunAllTests");
                }
                else
                {
                    EditorUtility.DisplayDialog("Tester Not Found",
                        "Add UIToolkitRadialMenuTester component to test the menu.", "OK");
                }
            }
        }

        private void CreateBasicMenu()
        {
            // Check if one already exists
            if (FindObjectOfType<UIToolkitRadialMenu>() != null)
            {
                EditorUtility.DisplayDialog("Menu Exists",
                    "A Basic Radial Menu already exists in the scene.", "OK");
                return;
            }

            // Create game object
            GameObject go = new GameObject("UI Toolkit Radial Menu");
            go.AddComponent<UIDocument>();
            go.AddComponent<UIToolkitRadialMenu>();

            // Register for undo
            Undo.RegisterCreatedObjectUndo(go, "Create Basic Radial Menu");

            Selection.activeGameObject = go;
            _basicMenu = go.GetComponent<UIToolkitRadialMenu>();

            Debug.Log("[UIToolkitRadialMenu] Basic menu created. Ensure you have a Panel Settings asset assigned to the UIDocument.");
        }

        private void CreateAdvancedMenu()
        {
            // Check if one already exists
            if (FindObjectOfType<UIToolkitRadialMenuAdvanced>() != null)
            {
                EditorUtility.DisplayDialog("Menu Exists",
                    "An Advanced Radial Menu already exists in the scene.", "OK");
                return;
            }

            // Create game object
            GameObject go = new GameObject("UI Toolkit Radial Menu (Advanced)");
            go.AddComponent<UIDocument>();
            go.AddComponent<UIToolkitRadialMenuAdvanced>();

            // Register for undo
            Undo.RegisterCreatedObjectUndo(go, "Create Advanced Radial Menu");

            Selection.activeGameObject = go;
            _advancedMenu = go.GetComponent<UIToolkitRadialMenuAdvanced>();

            Debug.Log("[UIToolkitRadialMenuAdvanced] Advanced menu created. Ensure you have a Panel Settings asset assigned to the UIDocument.");
        }

        private void CreateStylesheetAsset()
        {
            string path = "Assets/Resources/VoiceControl/RadialMenuStyles.uss";

            // Check if file already exists
            if (System.IO.File.Exists(path))
            {
                EditorUtility.DisplayDialog("Stylesheet Exists",
                    $"Stylesheet already exists at:\n{path}", "OK");
                return;
            }

            // Ensure directory exists
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            // Create asset - USS files are text assets
            var content = @"/* Radial Menu Styles - UI Toolkit */

.radial-menu-container {
    position: absolute;
    left: 50%;
    top: 50%;
}

.segment-container {
    background-color: rgba(40, 45, 55, 0.9);
    border-radius: 8px;
    transition-property: scale, opacity;
    transition-duration: 0.15s;
}

.segment-container:hover {
    scale: 1.1;
    background-color: rgba(60, 70, 85, 0.95);
}

.center-panel {
    background-color: rgba(25, 30, 38, 0.95);
    border-radius: 60px;
    border-width: 2px;
    border-color: rgba(255, 255, 255, 0.15);
}";

            System.IO.File.WriteAllText(path, content);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Stylesheet Created",
                $"Stylesheet created at:\n{path}", "OK");
        }
    }
}
