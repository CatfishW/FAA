using UnityEngine;
using UnityEditor;

namespace VoiceControl.UI.Editor
{
    /// <summary>
    /// Custom editor for UIToolkitRadialMenuAdvanced with in-editor preview and positioning.
    /// </summary>
    [CustomEditor(typeof(UIToolkitRadialMenuAdvanced))]
    public class UIToolkitRadialMenuAdvancedEditor : UnityEditor.Editor
    {
        private UIToolkitRadialMenuAdvanced _menu;
        private bool _showPositionSettings = true;
        private bool _showVisualSettings = true;
        private bool _showPreviewControls = true;

        private void OnEnable()
        {
            _menu = (UIToolkitRadialMenuAdvanced)target;
        }

        [MenuItem("FAA/Voice Control/Show Advanced Radial Menu Preview")]
        private static void ShowAdvancedMenuPreview()
        {
            var menu = FindAdvancedMenu();
            if (menu == null)
            {
                Debug.LogWarning("[UIToolkitRadialMenuAdvanced] No advanced radial menu found in the active scene.");
                return;
            }

            menu.ApplyAviationHudPreset(false);
            menu.RefreshUI(true);
            EditorUtility.SetDirty(menu);
        }

        [MenuItem("FAA/Voice Control/Hide Advanced Radial Menu Preview")]
        private static void HideAdvancedMenuPreview()
        {
            var menu = FindAdvancedMenu();
            if (menu == null)
            {
                return;
            }

            menu.SetMenuOpen(false);
            EditorUtility.SetDirty(menu);
        }

        private static UIToolkitRadialMenuAdvanced FindAdvancedMenu()
        {
            var menus = Object.FindObjectsOfType<UIToolkitRadialMenuAdvanced>(true);
            return menus != null && menus.Length > 0 ? menus[0] : null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(5);

            // Preview Controls
            _showPreviewControls = EditorGUILayout.Foldout(_showPreviewControls, "Editor Preview", true, EditorStyles.foldoutHeader);
            if (_showPreviewControls)
            {
                EditorGUI.indentLevel++;
                DrawPreviewControls();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Position Settings
            _showPositionSettings = EditorGUILayout.Foldout(_showPositionSettings, "Position & Layout", true, EditorStyles.foldoutHeader);
            if (_showPositionSettings)
            {
                EditorGUI.indentLevel++;
                DrawPositionSettings();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Visual Settings
            _showVisualSettings = EditorGUILayout.Foldout(_showVisualSettings, "Visual Settings", true, EditorStyles.foldoutHeader);
            if (_showVisualSettings)
            {
                EditorGUI.indentLevel++;
                DrawVisualSettings();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Draw remaining default properties
            DrawPropertiesExcluding(serializedObject,
                "m_Script",
                "innerRadius", "middleRadius", "outerRadius",
                "collapsedButtonSize", "collapsedButtonPosition",
                "openDuration", "closeDuration", "subMenuExpandDuration",
                "springCurve", "bounceCurve",
                "useRippleEffect", "usePulseAnimation", "useGradientBackground",
                "menuTransparency", "ringBackgroundTransparency", "segmentTransparency", "centerTransparency");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPreviewControls()
        {
            EditorGUILayout.HelpBox(
                "Preview the menu in Edit Mode without entering Play Mode. " +
                "Changes are applied immediately.",
                MessageType.Info);

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Show Preview", GUILayout.Height(30)))
            {
                _menu.RefreshUI(true);
            }

            if (GUILayout.Button("Hide Preview", GUILayout.Height(30)))
            {
                _menu.SetMenuOpen(false);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Generate FAA Icons", GUILayout.Height(25)))
            {
                FAAStyleIconGenerator.ShowWindow();
            }
        }

        private void DrawPositionSettings()
        {
            EditorGUILayout.LabelField("Radial Dimensions", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            float innerRadius = EditorGUILayout.FloatField("Inner Radius", _menu.InnerRadius);
            float middleRadius = EditorGUILayout.FloatField("Middle Radius", _menu.MiddleRadius);
            float outerRadius = EditorGUILayout.FloatField("Outer Radius", _menu.OuterRadius);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_menu, "Change Radial Menu Position");
                _menu.SetRadialDimensions(innerRadius, middleRadius, outerRadius);
                EditorUtility.SetDirty(_menu);
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Collapsed Button", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            float buttonSize = EditorGUILayout.FloatField("Button Size", _menu.CollapsedButtonSize);
            Vector2 buttonPos = EditorGUILayout.Vector2Field("Button Position", _menu.CollapsedButtonPosition);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_menu, "Change Collapsed Button");
                _menu.SetCollapsedButton(buttonSize, buttonPos);
                EditorUtility.SetDirty(_menu);
            }
        }

        private void DrawVisualSettings()
        {
            EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            float openDuration = EditorGUILayout.FloatField("Open Duration", _menu.OpenDuration);
            float closeDuration = EditorGUILayout.FloatField("Close Duration", _menu.CloseDuration);
            float subMenuDuration = EditorGUILayout.FloatField("Sub-Menu Duration", _menu.SubMenuExpandDuration);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_menu, "Change Animation Settings");
                _menu.SetAnimationDurations(openDuration, closeDuration, subMenuDuration);
                EditorUtility.SetDirty(_menu);
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Transparency", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            float menuTrans = EditorGUILayout.Slider("Menu", _menu.MenuTransparency, 0.3f, 1f);
            float ringTrans = EditorGUILayout.Slider("Ring", _menu.RingTransparency, 0.3f, 1f);
            float segTrans = EditorGUILayout.Slider("Segments", _menu.SegmentTransparency, 0.3f, 1f);
            float centerTrans = EditorGUILayout.Slider("Center", _menu.CenterTransparency, 0.3f, 1f);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_menu, "Change Transparency");
                _menu.SetTransparency(menuTrans, ringTrans, segTrans, centerTrans);
                EditorUtility.SetDirty(_menu);
            }
        }
    }
}
