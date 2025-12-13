/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors.Windows
{
    public class AboutWindow: EditorWindow
    {
        private string years = "2013-" + DateTime.Now.Year;

        [MenuItem(EditorUtils.MenuPath + "About", false, 300)]
        public static void OpenWindow()
        {
            AboutWindow window = GetWindow<AboutWindow>(true, "About", true);
            window.minSize = new Vector2(200, 100);
            window.maxSize = new Vector2(200, 100);
        }

        public void OnGUI()
        {
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.alignment = TextAnchor.MiddleCenter;

            GUIStyle textStyle = new GUIStyle(EditorStyles.label);
            textStyle.alignment = TextAnchor.MiddleCenter;

            GUILayout.Label("Online Maps", titleStyle);
            GUILayout.Label("version " + Map.version, textStyle);
            GUILayout.Label("created Infinity Code", textStyle);
            GUILayout.Label(years, textStyle);
        }
    }
}