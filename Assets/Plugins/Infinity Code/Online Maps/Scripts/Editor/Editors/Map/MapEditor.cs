/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using OnlineMaps.Editors.Windows;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(Map))]
    public partial class MapEditor : Editor
    {
        private Map map;
        private ControlBase control;
        
        private bool showCreateTexture;
        private bool showCustomProviderTokens;
        private bool showResourcesTokens;
        private bool wrongInputModuleDetected;
        private GUIContent wizardIconContent;

        private SerializedProperty pSource;
        private SerializedProperty pMapType;

        private SerializedProperty pLabels;
        private SerializedProperty pCustomProviderURL;
        private SerializedProperty pResourcesPath;
        private SerializedProperty pStreamingAssetsPath;
        
        private SerializedProperty pLanguage;
        private SerializedProperty pActiveTypeSettings;

        private TileProvider[] providers;
        private string[] providersTitle;
        private MapType mapType;
        private int providerIndex;
        
        private bool isDirty;
        private bool controlIsNull;

        private void CacheSerializedProperties()
        {
            pSource = serializedObject.FindProperty("source");
            pMapType = serializedObject.FindProperty("mapType");

            pCustomProviderURL = serializedObject.FindProperty("customProviderURL");
            pResourcesPath = serializedObject.FindProperty("resourcesPath");
            pStreamingAssetsPath = serializedObject.FindProperty("streamingAssetsPath");

            pLabels = serializedObject.FindProperty("labels");
            pLanguage = serializedObject.FindProperty("language");
            
            pActiveTypeSettings = serializedObject.FindProperty("_activeTypeSettings");
            
            CacheAdvancedProperties();
            CacheRuntimeProperties();
            CacheToolbarProperties();
            CacheTroubleshootingProperties();
        }

        private void DetectWrongInputModule()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            StandaloneInputModule[] inputModules = FindObjectsByType<StandaloneInputModule>(FindObjectsSortMode.None);
            wrongInputModuleDetected = inputModules.Length > 0;
#endif
        }

        private void DrawAddControlButton()
        {
            if (!GUILayout.Button("Add Control")) return;
            
            GenericMenu menu = new GenericMenu();

            TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<ControlBase>();
            foreach (Type t in types)
            {
                if (t.IsAbstract) continue;
                string className = t.Name;
                int controlIndex = className.IndexOf("Control");
                className = className.Insert(controlIndex, " ");

                int textureIndex = className.IndexOf("Texture");
                if (textureIndex > 0) className = className.Insert(textureIndex, " ");

                menu.AddItem(new GUIContent(className), false, data =>
                {
                    Type ct = data as Type;
                    map.gameObject.AddComponent(ct);
                    Repaint();
                }, t);
            }

            menu.ShowAsContext();
        }

        private void DrawAvailableLocalTokens()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            showResourcesTokens = EditorUtils.Foldout(showResourcesTokens, "Available Tokens");
            if (showResourcesTokens)
            {
                GUILayout.Label("{zoom} - Tile zoom");
                GUILayout.Label("{x} - Tile x");
                GUILayout.Label("{y} - Tile y");
                GUILayout.Label("{quad} - Tile quad key");
                GUILayout.Space(10);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawGeneralGUI()
        {
            DrawSourceGUI();
            DrawLocationGUI();
            DrawPlaymodeButtons();
        }

        private void DrawLabelsGUI()
        {
            if (mapType.isCustom) return;

            bool showLanguage;
            if (mapType.hasLabels)
            {
                EditorUtils.PropertyField(pLabels, "Show labels?");
                showLanguage = pLabels.boolValue;
            }
            else
            {
                showLanguage = mapType.labelsEnabled;
                GUILayout.Label("Labels " + (showLanguage ? "enabled" : "disabled"));
            }
            if (showLanguage && mapType.hasLanguage)
            {
                EditorUtils.PropertyField(pLanguage, mapType.provider.twoLetterLanguage ? "Use two-letter code such as: en" : "Use three-letter code such as: eng");
            }
        }

        private void DrawLocationGUI()
        {
            GeoPoint center = map.view.center;

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            EditorGUI.BeginChangeCheck();
            center.y = EditorGUILayout.DoubleField(new GUIContent("Latitude", "Latitude of the center point of the map"), center.y);
            center.x = EditorGUILayout.DoubleField(new GUIContent("Longitude", "Longitude of the center point of the map"), center.x);

            if (EditorGUI.EndChangeCheck())
            {
                isDirty = true;
                map.view.center = center;
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.Width(16));
            GUILayout.Space(10);
            EditorUtils.HelpButton("Coordinates of the center point of the map");
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            string tooltip = "Current zoom of the map";
            float newZoom = EditorGUILayout.Slider(new GUIContent("Zoom", tooltip), map.view.zoom, Constants.MinZoom, Constants.MaxZoomExt);

            if (EditorGUI.EndChangeCheck())
            {
                map.view.zoom = newZoom;
                isDirty = true;
            }

            EditorUtils.HelpButton(tooltip);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNullControlWarning()
        {
            if (!controlIsNull) return;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.HelpBox("Problem detected:\nCan not find OnlineMaps Control component.", MessageType.Error);
            DrawAddControlButton();

            EditorGUILayout.EndVertical();
        }

        private void DrawProviderGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            string helpMessage = "Tile provider.\nImportant: all tile presets are for testing purpose only. Before using the tile provider, make sure that it suits you by the terms of use and price.";

            if (Application.isPlaying)
            {
                if (mapType != map.activeType)
                {
                    string pid = map.activeType.provider.id;
                    for (int i = 0; i < providers.Length; i++)
                    {
                        if (providers[i].id == pid)
                        {
                            providerIndex = i;
                            break;
                        }
                    }

                    mapType = map.activeType;
                }
            }
        
            providerIndex = EditorGUILayout.Popup(new GUIContent("Provider", helpMessage), providerIndex, providersTitle);
            if (EditorGUI.EndChangeCheck())
            {
                mapType = providers[providerIndex].types[0];
                pMapType.stringValue = mapType.ToString();
                pActiveTypeSettings.stringValue = "";
            }

            EditorUtils.HelpButton(helpMessage);

            EditorGUILayout.EndHorizontal();

            if (mapType.useHTTP)
            {
                EditorGUILayout.HelpBox(mapType.provider.title + " - " + mapType.title + " uses HTTP, which can cause problems in iOS9+.", MessageType.Warning);
            }
            else if (mapType.isCustom)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();
                GUILayout.Space(5);
                EditorGUILayout.PropertyField(pCustomProviderURL);
                EditorGUILayout.EndVertical();
                if (GUILayout.Button(wizardIconContent, GUILayout.ExpandWidth(false))) CustomURLWizard.OpenWindow();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginVertical(GUI.skin.box);
                showCustomProviderTokens = EditorUtils.Foldout(showCustomProviderTokens, "Available tokens");
                if (showCustomProviderTokens)
                {
                    GUILayout.Label("{zoom} or {z} - Tile zoom");
                    GUILayout.Label("{x} - Tile X");
                    GUILayout.Label("{y} - Tile Y");
                    GUILayout.Label("{quad} - Tile Quad");
                    GUILayout.Space(10);
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawProviderExtraFields(TileProvider.IExtraField[] extraFields)
        {
            if (extraFields == null) return;

            foreach (TileProvider.IExtraField field in extraFields)
            {
                if (field is TileProvider.ToggleExtraGroup) DrawProviderToggleExtraGroup(field as TileProvider.ToggleExtraGroup);
                else if (field is TileProvider.ExtraField) DrawProviderExtraField(field as TileProvider.ExtraField);
                else if (field is TileProvider.LabelField)
                {
                    EditorGUILayout.LabelField((field as TileProvider.LabelField).label);
                }
            }
        }

        private void DrawProviderExtraField(TileProvider.ExtraField field)
        {
            EditorGUI.BeginChangeCheck();
            field.value = EditorGUILayout.TextField(field.title, field.value);
            if (EditorGUI.EndChangeCheck()) isDirty = true;
        }

        private void DrawProviderToggleExtraGroup(TileProvider.ToggleExtraGroup group)
        {
            group.value = EditorGUILayout.Toggle(group.title, group.value);
            EditorGUI.BeginDisabledGroup(group.value);

            if (group.fields != null)
            {
                foreach (TileProvider.IExtraField field in group.fields)
                {
                    if (field is TileProvider.ToggleExtraGroup) DrawProviderToggleExtraGroup(field as TileProvider.ToggleExtraGroup);
                    else if (field is TileProvider.ExtraField) DrawProviderExtraField(field as TileProvider.ExtraField);
                }
            }

            EditorGUI.EndDisabledGroup();
        }

        private void DrawSourceGUI()
        {
            EditorGUI.BeginDisabledGroup(Utils.isPlaying);

            EditorUtils.PropertyField(pSource, "Source of tiles");
            
            if (pSource.enumValueIndex != (int)MapSource.Online)
            {
                if (GUILayout.Button("Fix Import Settings for Tiles")) FixImportSettings();
                if (GUILayout.Button("Import from GMapCatcher")) ImportFromGMapCatcher();

                if (pSource.enumValueIndex == (int) MapSource.Resources || pSource.enumValueIndex == (int) MapSource.ResourcesAndOnline)
                {
                    EditorUtils.PropertyField(pResourcesPath, "The path pattern inside Resources folder");
                }
                else
                {
                    EditorUtils.PropertyField(pStreamingAssetsPath, "The path pattern inside Streaming Assets folder");
#if UNITY_WEBGL
                    EditorGUILayout.HelpBox("Streaming Assets folder is not available for WebGL!", MessageType.Warning);
#endif
                }

                DrawAvailableLocalTokens();
            }

            EditorGUI.EndDisabledGroup();

            DrawProviderAndType();
        }

        private void DrawProviderType()
        {
            if (mapType.provider.types.Length < 2) return;
            
            GUIContent[] availableTypes = mapType.provider.types.Select(t => new GUIContent(t.title)).ToArray();
            int index = mapType.index;
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            string tooltip = "Type (style) of the map";
            index = EditorGUILayout.Popup(new GUIContent("Type", tooltip), index, availableTypes);
            if (EditorGUI.EndChangeCheck())
            {
                mapType = mapType.provider.types[index];
                pMapType.stringValue = mapType.ToString();
            }
            EditorUtils.HelpButton(tooltip);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProviderAndType()
        {
            if (control && !control.useRasterTiles) return;
            if (pSource.enumValueIndex == (int) MapSource.Resources) return;
            if (pSource.enumValueIndex == (int)MapSource.StreamingAssets) return;

            DrawProviderGUI();
            DrawProviderType();

            DrawProviderExtraFields(mapType.provider.extraFields);
            DrawProviderExtraFields(mapType.extraFields);
            if (mapType.fullID == "google.satellite")
            {
                if (GUILayout.Button("Detect the latest version of tiles"))
                {
                    WebClient client = new WebClient();
                    string response = client.DownloadString("https://maps.googleapis.com/maps/api/js");
                    Match match = Regex.Match(response, @"kh\?v=(\d+)");
                    if (match.Success)
                    {
                        TileProvider.ExtraField version = mapType.extraFields.FirstOrDefault(f =>
                        {
                            TileProvider.ExtraField ef = f as TileProvider.ExtraField;
                            if (ef == null) return false;
                            if (ef.token != "version") return false;
                            return true;
                        }) as TileProvider.ExtraField;
                        if (version != null)
                        {
                            version.value = match.Groups[1].Value;
                            EditorUtility.SetDirty(target);
                        }
                    }
                }
            }
            DrawLabelsGUI();
        }

        private void DrawWrongInputModuleWarning()
        {
            if (!wrongInputModuleDetected) return;
            
            const string message = "Problem detected:\nYou are using StandaloneInputModule, which uses the old InputManager. You are using the new InputSystem, nad have old InputManager disabled. StandaloneInputManager will not work. Click the button below to replace StandaloneInputModule with a InputSystemUIInputModule, which uses the new InputSystem";
            EditorGUILayout.HelpBox(message, MessageType.Error);
            GUILayout.Space(5);
            if (!GUILayout.Button("Replace with InputSystemUIInputModule")) return;
            
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Unity.InputSystem");
            if (assembly == null)
            {
                EditorUtility.DisplayDialog("InputModule replacement failed", "Unity Input System package is not installed. Please install it from the Package Manager.", "OK");
                return;
            }
                
            Type type = assembly.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            if (type == null)
            {
                EditorUtility.DisplayDialog("InputModule replacement failed", "InputSystemUIInputModule type not found. Please make sure you have the correct version of the Input System package.", "OK");
                return;
            }
            
            StandaloneInputModule[] inputModules = FindObjectsByType<StandaloneInputModule>(FindObjectsSortMode.None);
            
            Undo.SetCurrentGroupName("Replace StandaloneInputModule with InputSystemUIInputModule");
            int group = Undo.GetCurrentGroup();
                
            foreach (StandaloneInputModule inputModule in inputModules)
            {
                GameObject go = inputModule.gameObject;
                Undo.AddComponent(go, type);
                Undo.DestroyObjectImmediate(inputModule);
                EditorUtility.SetDirty(go);
            }
            
            Undo.CollapseUndoOperations(group);

            wrongInputModuleDetected = false;
            EditorUtility.DisplayDialog("InputModule replaced", "StandaloneInputModule has been replaced with InputSystemUIInputModule.", "OK");
        }

        private void FixImportSettings()
        {
            string path;
            string specialFolderName;

            if (pSource.enumValueIndex == (int) MapSource.Resources || pSource.enumValueIndex == (int) MapSource.ResourcesAndOnline)
            {
                path = pResourcesPath.stringValue;
                specialFolderName = "Resources";
            }
            else
            {
                path = pStreamingAssetsPath.stringValue;
                specialFolderName = "StreamingAssets";
            }

            int tokenIndex = path.IndexOf("{");

            string specialFolder = Path.Combine(Application.dataPath, specialFolderName);

            if (tokenIndex != -1)
            {
                if (tokenIndex > 1)
                {
                    string folder = path.Substring(0, tokenIndex - 1);
                    specialFolder = Path.Combine(specialFolder, folder);
                }
            }
            else specialFolder = Path.Combine(specialFolder, "OnlineMapsTiles");

            if (!Directory.Exists(specialFolder)) return;

            string[] tiles = Directory.GetFiles(specialFolder, "*.png", SearchOption.AllDirectories);
            float count = tiles.Length;
            for (int i = 0; i < tiles.Length; i++)
            {
                string shortPath = "Assets/" + tiles[i].Substring(Application.dataPath.Length + 1);
                FixTileImporter(shortPath, i / count);
            }

            EditorUtility.ClearProgressBar();
        }

        private static void FixTileImporter(string shortPath, float progress)
        {
            TextureImporter textureImporter = AssetImporter.GetAtPath(shortPath) as TextureImporter;
            EditorUtility.DisplayProgressBar("Update import settings for tiles", "Please wait, this may take several minutes.", progress);
            if (!textureImporter) return;
            
            textureImporter.mipmapEnabled = false;
            textureImporter.isReadable = true;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.maxTextureSize = 256;
            AssetDatabase.ImportAsset(shortPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private void ImportFromGMapCatcher()
        {
            string folder = EditorUtility.OpenFolderPanel("Select GMapCatcher tiles folder", string.Empty, "");
            if (string.IsNullOrEmpty(folder)) return;

            string[] files = Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories);
            if (files.Length == 0) return;

            string specialFolderName;

            if (pSource.enumValueIndex == (int)MapSource.Resources || pSource.enumValueIndex == (int)MapSource.ResourcesAndOnline)
            {
                specialFolderName = "Resources";
            }
            else
            {
                specialFolderName = "StreamingAssets";
            }

            string specialPath = "Assets/" + specialFolderName + "/OnlineMapsTiles";

            bool needAsk = true;
            bool overwrite = false;
            foreach (string file in files)
            {
                if (!ImportTileFromGMapCatcher(file, folder, specialPath, ref overwrite, ref needAsk)) break;
            }

            AssetDatabase.Refresh();
        }

        private static bool ImportTileFromGMapCatcher(string file, string folder, string resPath, ref bool overwrite, ref bool needAsk)
        {
            string shortPath = file.Substring(folder.Length + 1);
            shortPath = shortPath.Replace('\\', '/');
            string[] shortArr = shortPath.Split('/');
            int zoom = 17 - int.Parse(shortArr[0]);
            int x = int.Parse(shortArr[1]) * 1024 + int.Parse(shortArr[2]);
            int y = int.Parse(shortArr[3]) * 1024 + int.Parse(shortArr[4].Substring(0, shortArr[4].Length - 4));
            string dir = Path.Combine(resPath, string.Format("{0}/{1}", zoom, x));
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string destFileName = Path.Combine(dir, y + ".png");
            if (File.Exists(destFileName))
            {
                if (needAsk)
                {
                    needAsk = false;
                    int result = EditorUtility.DisplayDialogComplex("File already exists", "File already exists. Overwrite?", "Overwrite", "Skip", "Cancel");
                    if (result == 0) overwrite = true;
                    else if (result == 1)
                    {
                        overwrite = false;
                        return true;
                    }
                    else return false;
                }

                if (!overwrite) return true;
            }
            File.Copy(file, destFileName, true);
            return true;
        }

        private void OnEnable()
        {
            map = target as Map;
            control = map.GetComponent<ControlBase>();
            controlIsNull = !control;
            
            try
            {
                CacheSerializedProperties();

                providers = TileProvider.providers;
                providersTitle = TileProvider.GetProvidersTitle();
                
                wizardIconContent = new GUIContent(EditorUtils.LoadAsset<Texture2D>("Icons/WizardIcon.png"), "Wizard");

                Updater.CheckNewVersionAvailable();

                mapType = TileProvider.FindMapType(pMapType.stringValue);
                providerIndex = mapType.provider.index;

                serializedObject.ApplyModifiedProperties();
                
                DetectWrongInputModule();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public override void OnInspectorGUI()
        {
            DrawToolbar();
            
            DrawWrongInputModuleWarning();

            serializedObject.Update();

            isDirty = false;

            try
            {
                DrawGeneralGUI();

                DrawAdvancedBlock();
                DrawTroubleshootingBlock();
                DrawNullControlWarning();

                serializedObject.ApplyModifiedProperties();

                if (isDirty)
                {
                    EditorUtility.SetDirty(map);
                    if (!Utils.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    else map.Redraw();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}