/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors.Windows
{
    [InitializeOnLoad]
    public class Welcome : EditorWindow
    {
        private const string PREFSHOWATSTARTUP = "OnlineMaps.ShowWelcomeScreen";
        private string copyright = "Infinity Code 2013-" + DateTime.Now.Year;
        private static bool showAtStartup = true;
        private Vector2 scrollPosition;
        private static bool inited;
        private static GUIStyle headerStyle;
        private static Texture2D wizardTexture;
        private static Texture2D boltTexture;
        private static Texture2D playmakerTexture;
        private static Texture2D docTexture;
        private static Texture2D forumTexture;
        private static Texture2D apiTexture;
        private static Texture2D examplesTexture;
        private static Texture2D updateTexture;
        private static Texture2D supportTexture;
        private static GUIStyle copyrightStyle;
        private static Welcome wnd;
        private static Texture2D rateTexture;

        static Welcome()
        {
            EditorApplication.update -= GetShowAtStartup;
            EditorApplication.update += GetShowAtStartup;
        }

        private static bool DrawButton(Texture2D texture, string title, string body = "", int space = 10)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Space(34);
            GUILayout.Box(texture, GUIStyle.none, GUILayout.MaxWidth(48), GUILayout.MaxHeight(48));
            GUILayout.Space(10);

            GUILayout.BeginVertical();
            GUILayout.Space(1);
            GUILayout.Label(title, EditorStyles.boldLabel);
            GUILayout.Label(body);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            Rect rect = GUILayoutUtility.GetLastRect();
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            bool returnValue = Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition);
            GUILayout.Space(space);

            return returnValue;
        }

        private static void GetShowAtStartup()
        {
            EditorApplication.update -= GetShowAtStartup;
            showAtStartup = EditorPrefs.GetBool(PREFSHOWATSTARTUP, true);

            if (showAtStartup)
            {
                EditorApplication.update -= OpenAtStartup;
                EditorApplication.update += OpenAtStartup;
            }
        }

        private static bool Init()
        {
            try
            {
                headerStyle = new GUIStyle();
                headerStyle.normal.background = EditorUtils.LoadAsset<Texture2D>("Icons/Editor/Welcome/Logo.png", true);
                headerStyle.normal.textColor = Color.white;
                headerStyle.padding = new RectOffset(330, 0, 30, 0);
                headerStyle.margin = new RectOffset(0, 0, 0, 0);

                copyrightStyle = new GUIStyle();
                copyrightStyle.alignment = TextAnchor.MiddleRight;

                wizardTexture = EditorUtils.LoadAsset<Texture2D>("Icons/Editor/Welcome/Wizard.png", true);
                boltTexture = EditorUtils.LoadAsset<Texture2D>("Icons/Editor/Welcome/Bolt.png", true);
                playmakerTexture = EditorUtils.LoadAsset<Texture2D>("Icons/Editor/Welcome/Playmaker.png", true);
                docTexture = EditorUtils.LoadAsset<Texture2D>("Icons/Editor/Welcome/Docs.png", true);
                forumTexture = EditorUtils.LoadAsset<Texture2D>("Icons/Editor/Welcome/Forum.png", true);
                apiTexture = EditorUtils.LoadAsset<Texture2D>("Icons/Editor/Welcome/API.png", true);
                examplesTexture = EditorUtils.LoadAsset<Texture2D>("Icons/Editor/Welcome/Examples.png", true);
                updateTexture = EditorUtils.LoadAsset<Texture2D>("Icons/Editor/Welcome/Update.png", true);
                supportTexture = EditorUtils.LoadAsset<Texture2D>("Icons/Editor/Welcome/Support.png", true);
                rateTexture = EditorUtils.LoadAsset<Texture2D>("Icons/Editor/Welcome/Rate.png", true);

                inited = true;
            }
            catch
            {
                return false;
            }
            return true;
        }

        private void OnEnable()
        {
            wnd = this;
        }

        private void OnDestroy()
        {
            wnd = null;
            EditorPrefs.SetBool(PREFSHOWATSTARTUP, false);
        }

        private void OnGUI()
        {
            if (!inited) Init();

            GUI.Box(new Rect(0, 0, 500, 58), "v" +  Map.version, headerStyle);

            GUILayoutUtility.GetRect(position.width, 60);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.Space();

            if (DrawButton(wizardTexture, "Map Wizard", "Create your own map for a minute.")) Wizard.OpenWindow();
            if (DrawButton(boltTexture, "Import Visual Scripting Integration Kit", "and interact with the map without C# scripting.")) PackageManager.ImportVisualScriptingIntegrationKit();
            if (DrawButton(playmakerTexture, "Import Playmaker Integration Kit", "and interact with the map without C# scripting.")) PackageManager.ImportPlayMakerIntegrationKit();
            if (DrawButton(docTexture, "Documentation", "Online version of the documentation.")) Links.OpenDocumentation();
            if (DrawButton(apiTexture, "API Reference", "Online Maps API Reference.")) Links.OpenAPIReference();
            if (DrawButton(examplesTexture, "Atlas of Examples", "We made a lot of examples. That will help you get started quickly.")) Links.OpenAtlasOfExamples();
            if (DrawButton(supportTexture, "Support", "If you have any problems feel free to contact us.")) Links.OpenSupport();
            if (DrawButton(forumTexture, "Forum", "Official forum of Online Maps.")) Links.OpenHomepage();
            if (DrawButton(rateTexture, "Rate and Review", "Share your impression about the asset.")) Links.OpenReviews();
            if (DrawButton(updateTexture, "Check Updates", "Perhaps a new version is already waiting for you. Check it.")) Updater.OpenWindow();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField(copyright, copyrightStyle);
        }

        private static void OpenAtStartup()
        {
            if (!inited && !Init()) return;
            OpenWindow();
            EditorApplication.update -= OpenAtStartup;
        }

        [MenuItem(EditorUtils.MenuPath + "Welcome Screen", false, 1)]
        public static void OpenWindow()
        {
            if (wnd != null) return;

            wnd = GetWindow<Welcome>(true, "Welcome to Online Maps", true);
            wnd.maxSize = wnd.minSize = new Vector2(500, 440);
        }
    }
}