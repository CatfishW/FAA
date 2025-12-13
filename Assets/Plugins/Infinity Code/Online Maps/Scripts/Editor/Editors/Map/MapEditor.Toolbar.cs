/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps.Editors.Windows;
using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    public partial class MapEditor
    {
        private GUIContent updateAvailableContent;

        private void CacheToolbarProperties()
        {
            updateAvailableContent = new GUIContent(
                $"Update Available (current {Map.version})", 
                EditorUtils.LoadAsset<Texture2D>("Icons/update_available.png"), 
                "Update Available");
        }

        private static void DrawHelp()
        {
            if (!GUILayout.Button("Help", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) return;
            
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Documentation"), false, Links.OpenDocumentation);
            menu.AddItem(new GUIContent("API Reference"), false, Links.OpenAPIReference);
            menu.AddItem(new GUIContent("Atlas of Examples"), false, Links.OpenAtlasOfExamples);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Product Page"), false, Links.OpenHomepage);
            menu.AddItem(new GUIContent("Forum"), false, Links.OpenForum);
            menu.AddItem(new GUIContent("Check Updates"), false, Updater.OpenWindow);
            menu.AddItem(new GUIContent("Support"), false, Links.OpenSupport);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Rate and Review"), false, Links.OpenReviews);
            menu.AddItem(new GUIContent("About"), false, AboutWindow.OpenWindow);
            menu.ShowAsContext();
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (Updater.hasNewVersion && updateAvailableContent != null)
            {
                Color defBackgroundColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1, 0.5f, 0.5f);
                if (GUILayout.Button(updateAvailableContent, EditorStyles.toolbarButton))
                {
                    Updater.OpenWindow();
                }
                GUI.backgroundColor = defBackgroundColor;
            }
            else GUILayout.Label($"Online Maps v{Map.version}", EditorStyles.centeredGreyMiniLabel);

            DrawHelp();

            GUILayout.EndHorizontal();
        }
    }
}