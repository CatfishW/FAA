/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace OnlineMaps.Editors
{
    public static class EditorUtils
    {
        public const string MenuPath = "GameObject/Infinity Code/Online Maps/";
        
        private static string _assetPath;
        private static string[] _availableSizesStr;
        private static GUIStyle _groupStyle;
        private static Texture2D _helpIcon;
        private static GUIStyle _helpStyle;

        public static readonly int[] availableSizes = { 256, 512, 1024, 2048, 4096 };

        public static string[] availableSizesStr
        {
            get
            {
                if (_availableSizesStr == null) _availableSizesStr = availableSizes.Select(s => s.ToString()).ToArray();
                return _availableSizesStr;
            }
        }

        public static string assetPath
        {
            get
            {
                if (_assetPath != null) return _assetPath;
                
                string[] paths = Directory.GetFiles(Application.dataPath, "OnlineMaps.asmdef", SearchOption.AllDirectories);
                if (paths.Length != 0)
                {
                    FileInfo info = new FileInfo(paths[0]);
                    _assetPath = info.Directory.Parent.FullName.Substring(Application.dataPath.Length - 6) + "/";
                }
                else
                {
                    _assetPath = "Assets/Plugins/Infinity Code/Online Maps/";
                }
                return _assetPath;
            }
        }
        
        public static GUIStyle groupStyle
        {
            get
            {
                if (_groupStyle != null) return _groupStyle;
                
                _groupStyle = new GUIStyle(GUI.skin.box);
                _groupStyle.normal.textColor = EditorStyles.label.normal.textColor;
                _groupStyle.margin = new RectOffset(0, 0, 10, 5);
                return _groupStyle;
            }
        }

        public static Texture2D helpIcon
        {
            get
            {
                if (!_helpIcon) _helpIcon = LoadAsset<Texture2D>("Icons/HelpIcon.png");
                return _helpIcon;
            }
        }

        public static GUIStyle helpStyle
        {
            get
            {
                if (_helpStyle != null) return _helpStyle;
                
                _helpStyle = new GUIStyle();
                _helpStyle.margin = new RectOffset(0, 0, 2, 0);
                return _helpStyle;
            }
        }
        
        public static void AddCompilerDirective(string directive)
        {
            BuildTargetGroup[] targetGroups = (BuildTargetGroup[]) Enum.GetValues(typeof(BuildTargetGroup));
            foreach (BuildTargetGroup g in targetGroups)
            {
                if (g == BuildTargetGroup.Unknown) continue;
                int ig = (int) g;
                if (ig == 2 || 
                    ig == 5 || 
                    ig == 6 || 
                    ig >= 15 && ig <= 18 ||
                    ig == 20 || 
                    ig >= 22 && ig <= 24 ||
                    ig == 26) continue;

#if UNITY_2023_1_OR_NEWER
                UnityEditor.Build.NamedBuildTarget buildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(g);
                string currentDefinitions = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
#else
                string currentDefinitions = PlayerSettings.GetScriptingDefineSymbolsForGroup(g);
#endif
                List<string> directives = currentDefinitions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
                if (!directives.Contains(directive))
                {
                    directives.Add(directive);
                    currentDefinitions = string.Join(";", directives.ToArray());
#if UNITY_2023_1_OR_NEWER
                    PlayerSettings.SetScriptingDefineSymbols(buildTarget, currentDefinitions);
#else
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(g, currentDefinitions);
#endif
                }
            }
        }
        
        public static void CheckMarkerTextureImporter(SerializedProperty property)
        {
            Texture2D texture = property.objectReferenceValue as Texture2D;
            CheckMarkerTextureImporter(texture);
        }

        public static void CheckMarkerTextureImporter(Texture2D texture)
        {
            if (!texture) return;

            string textureFilename = AssetDatabase.GetAssetPath(texture.GetInstanceID());
            TextureImporter textureImporter = AssetImporter.GetAtPath(textureFilename) as TextureImporter;
            if (!textureImporter) return;

            bool needReimport = false;
            if (textureImporter.mipmapEnabled)
            {
                textureImporter.mipmapEnabled = false;
                needReimport = true;
            }
            if (!textureImporter.isReadable)
            {
                textureImporter.isReadable = true;
                needReimport = true;
            }
            if (textureImporter.textureCompression != TextureImporterCompression.Uncompressed)
            {
                textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
                needReimport = true;
            }

            if (needReimport) AssetDatabase.ImportAsset(textureFilename, ImportAssetOptions.ForceUpdate);
        }

        public static bool Foldout(bool value, string text)
        {
            return GUILayout.Toggle(value, text, EditorStyles.foldout);
        }

        /// <summary>
        /// Returns the current canvas or creates a new one
        /// </summary>
        /// <returns>Instance of Canvas</returns>
        public static Canvas GetCanvas()
        {
            Canvas canvas = Compatibility.FindObjectOfType<Canvas>();
            if (!canvas)
            {
                GameObject canvasGO = new GameObject("Canvas");
                canvasGO.layer = LayerMask.NameToLayer("UI");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            GetEventSystem();

            return canvas;
        }

        /// <summary>
        /// Returns the current event system or creates a new one
        /// </summary>
        /// <returns>Instance of event system</returns>
        public static EventSystem GetEventSystem()
        {
            EventSystem eventSystem = Compatibility.FindObjectOfType<EventSystem>();
            if (!eventSystem)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            return eventSystem;
        }

        public static void GroupLabel(string label)
        {
            GUILayout.Label(label, groupStyle, GUILayout.ExpandWidth(true));
        }

        public static void HelpButton(string help, string url = null)
        {
            if (GUILayout.Button(new GUIContent(helpIcon, help), helpStyle, GUILayout.ExpandWidth(false)) && !string.IsNullOrEmpty(url))
            {
                Process.Start(url);
            }
        }

        public static void ImportPackage(string path, Warning warning = null, string errorMessage = null)
        {
            if (warning != null && !warning.Show()) return;
            if (string.IsNullOrEmpty(assetPath))
            {
                if (!string.IsNullOrEmpty(errorMessage)) Debug.LogError(errorMessage);
                return;
            }

            string filename = assetPath + path;
            if (!File.Exists(filename))
            {
                if (!string.IsNullOrEmpty(errorMessage)) Debug.LogError(errorMessage);
                return;
            }

            AssetDatabase.ImportPackage(filename, true);
        }

        public static T LoadAsset<T>(string path, bool throwOnMissed = false) where T : Object
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                if (throwOnMissed) throw new FileNotFoundException(assetPath);
                return null;
            }
            string filename = assetPath + "/" + path;
            
            if (!File.Exists(filename))
            {
                if (throwOnMissed) throw new FileNotFoundException(assetPath);
                return null;
            }
            return (T)AssetDatabase.LoadAssetAtPath(filename, typeof(T));
        }

        public static void PropertyField(SerializedProperty sp, string help = null, string url = null)
        {
            EditorGUI.BeginChangeCheck();
            bool hasHelp = !string.IsNullOrEmpty(help);
            if (hasHelp) EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(sp);
            if (!hasHelp) return;
            
            HelpButton(help, url);
            EditorGUILayout.EndHorizontal();
        }

        public static void PropertyField(SerializedProperty sp, GUIContent content, string help = null, string url = null)
        {
            EditorGUI.BeginChangeCheck();
            bool hasHelp = !string.IsNullOrEmpty(help);
            if (hasHelp) EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(sp, content);
            if (!hasHelp) return;
            
            HelpButton(help, url);
            EditorGUILayout.EndHorizontal();
        }

        public class Warning
        {
            public string title = "Warning";
            public string message;
            public string ok = "OK";
            public string cancel = "Cancel";

            public bool Show()
            {
                return EditorUtility.DisplayDialog(title, message, ok, cancel);
            }
        }
    }
}