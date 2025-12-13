/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OnlineMaps.Editors.Windows
{
    [Serializable]
    public class Wizard : EditorWindow
    {
        internal delegate void WizardDelegate(ref bool allowCreate);

        #region Variables

        #region Wizard

        private string[] control2DTitles;
        private string[] control3DTitles;
        private List<Control> controls2D;
        private List<Control> controls3D;
        private int default2DIndex = 0;
        private int default3DIndex = 0;
        private bool is2D = false;

        private List<IPlugin> plugins;
        private Vector2 scrollPosition;
        private int selectedIndex = 0;
        private List<WizardDelegate> steps;

        #endregion

        #region General

        private MapType activeMapType;
        private string customProviderURL;
        private bool labels;
        private string language = "en";
        private int providerIndex;
        private TileProvider[] providers;
        private string[] providersTitle;
        private bool showCustomProviderTokens;
        private MapSource source;
        private bool traffic;

        #endregion

        #region 3D Controls

        private Camera activeCamera;
        private Vector2 sizeInScene = new Vector2(Constants.DefaultMapSize, Constants.DefaultMapSize);

        #endregion

        #region Tileset

        private Shader defaultTilesetShader;
        private Shader drawingShader;
        private bool fixClippingPlanes = true;
        private Material markerMaterial;
        private Shader markerShader;
        private bool moveCameraToMap;
        private Material tileMaterial;
        private int tilesetHeight = Constants.DefaultMapSize;
        private Shader tilesetShader;
        private int tilesetWidth = Constants.DefaultMapSize;

        #endregion

        #region Texture

        private bool createTexture = true;
        private string textureFilename = "OnlineMaps";
        private int textureHeight = 512;
        private int textureWidth = 512;
        private GameObject uGUIParent;
#if NGUI
        private GameObject NGUIParent;
#endif

        #endregion

        #endregion

        private Control activeControl => is2D ? controls2D[selectedIndex] : controls3D[selectedIndex];

        private void CacheControl(Type type)
        {
            if (!type.IsSubclassOf(typeof(ControlBase)) || type.IsAbstract) return;
#if !NGUI
            if (type == typeof(NGUITextureControl)) return;
#endif

            string fullName = type.Name;

            int controlIndex = fullName.IndexOf("Control");
            if (controlIndex != -1) fullName = fullName.Insert(controlIndex, " ");

            Control control = new Control(fullName, type);

            if (type.IsSubclassOf(typeof(ControlBase2D))) controls2D.Add(control);
            else
            {
                if (type == typeof(TileSetControl)) default3DIndex = controls3D.Count;
                controls3D.Add(control);
            }

            InitControlSteps(type, control);
            InitControlPlugins(type, control);
        }

        private void CacheControls()
        {
            controls2D = new List<Control>();
            controls3D = new List<Control>();

            Type[] types = typeof(Map).Assembly.GetTypes();
            foreach (Type t in types)
            {
                CacheControl(t);
            }

            control2DTitles = controls2D.Select(c => c.title).ToArray();
            control3DTitles = controls3D.Select(c => c.title).ToArray();
            selectedIndex = default3DIndex;
        }

        private void CachePlugins()
        {
            plugins = new List<IPlugin>();
            Type[] types = typeof(Map).Assembly.GetTypes();
            foreach (Type t in types)
            {
                object[] attributes = t.GetCustomAttributes(typeof(PluginAttribute), true);
                if (attributes.Length == 0 || t.IsAbstract) continue;
                
                PluginAttribute p = attributes[0] as PluginAttribute;
                if (string.IsNullOrEmpty(p.group))
                {
                    plugins.Add(new Plugin(t, p));
                    continue;
                }

                PluginGroup g = plugins.FirstOrDefault(pg => pg is PluginGroup && pg.title == p.group) as PluginGroup;
                if (g == null)
                {
                    g = new PluginGroup(p.group);
                    plugins.Add(g);
                }

                g.Add(t, p);
            }
            plugins = plugins.OrderBy(p => p.title).ToList();
        }

        private float CheckCameraDistance(Camera tsCamera)
        {
            if (!tsCamera) return -1;

            Vector3 cameraPosition;
            if (moveCameraToMap)
            {
                cameraPosition = new Vector3(sizeInScene.x / -2, Mathf.Min(sizeInScene.x, sizeInScene.y), sizeInScene.y / 2);
            }
            else
            {
                cameraPosition = tsCamera.transform.position;
            }

            Vector3 mapCenter = new Vector3(sizeInScene.x / -2, 0, sizeInScene.y / 2);
            float distance = (cameraPosition - mapCenter).magnitude * 3f;

            if (distance <= tsCamera.farClipPlane) return -1;

            return distance;
        }

        private void CreateControl(GameObject go, Map map)
        {
            ControlBase control = go.AddComponent(activeControl.type) as ControlBase;
            if (activeControl.resultType == MapTarget.texture) CreateTextureControl(go, map, control as ITextureControl);
            else CreateDynamicMeshControl(map, control);
        }

        private void CreateDynamicMeshControl(Map map, Component component)
        {
            ControlBaseDynamicMesh control3D = component as ControlBaseDynamicMesh;
            control3D.SetSize(tilesetWidth, tilesetHeight);
            control3D.sizeInScene = sizeInScene;
            map.renderInThread = false;

            TileSetControl tsControl = component as TileSetControl;
            if (tsControl)
            {
                tsControl.tileMaterial = tileMaterial;
                tsControl.markerMaterial = markerMaterial;
                tsControl.tilesetShader = tilesetShader;
                tsControl.drawingShader = drawingShader;
                tsControl.markerShader = markerShader;
            }

            if (moveCameraToMap)
            {
                GameObject cameraGO = activeCamera.gameObject;
                float minSide = Mathf.Min(sizeInScene.x, sizeInScene.y);
                Vector3 pos = new Vector3(sizeInScene.x / -2, minSide, sizeInScene.y / 2);
                cameraGO.transform.position = pos;
                cameraGO.transform.rotation = Quaternion.Euler(90, 180, 0);
            }

            if (fixClippingPlanes)
            {
                float needFixCameraDistance = CheckCameraDistance(activeCamera);
                if (Math.Abs(needFixCameraDistance + 1) > float.Epsilon) activeCamera.farClipPlane = needFixCameraDistance;
            }
        }

        private void CreateMap()
        {
            Map map = CreateMapGameObject();
            GameObject go = map.gameObject;
            CreateControl(go, map);
            CreatePlugins(go);

            EditorGUIUtility.PingObject(go);
            Selection.activeGameObject = go;
        }

        private Map CreateMapGameObject()
        {
            GameObject go;
            if (activeControl.type == typeof(PlaneControl)) go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            else go = new GameObject("Map");

            Map map = go.AddComponent<Map>();

            map.source = source;
            map.mapType = activeMapType.ToString();
            map.labels = labels;
            map.customProviderURL = customProviderURL;
            map.language = language;
            map.traffic = traffic;
            map.redrawOnPlay = true;

            return map;
        }

        private void CreatePlugins(GameObject go)
        {
            IEnumerable<IPlugin> _plugins = activeControl.plugins.Concat(activeControl.thirdPartyPlugins);
            foreach (IPlugin plugin in _plugins)
            {
                if (plugin is Plugin)
                {
                    Plugin p = plugin as Plugin;
                    if (p.enabled) go.AddComponent(p.type);
                }
                else if (plugin is PluginGroup)
                {
                    PluginGroup g = plugin as PluginGroup;
                    if (g.selected > 0)
                    {
                        go.AddComponent(g.plugins.First(p => p.title == g.titles[g.selected]).type);
                    }
                }
            }
        }

        private Texture2D CreateTexture(out string texturePath)
        {
            if (!createTexture)
            {
                texturePath = string.Empty;
                return null;
            }

            texturePath = $"Assets/{textureFilename}.png";
            Texture2D texture = new Texture2D(textureWidth, textureHeight);
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            AssetDatabase.Refresh();
            TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (!textureImporter) return texture;
            
            textureImporter.mipmapEnabled = false;
            textureImporter.isReadable = true;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.maxTextureSize = Mathf.Max(textureWidth, textureHeight);

            if (activeControl.useSprite)
            {
                textureImporter.spriteImportMode = SpriteImportMode.Single;
                textureImporter.npotScale = TextureImporterNPOTScale.None;
            }
            
            textureImporter.SaveAndReimport();
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            return (Texture2D)AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D));
        }

        private void CreateTextureControl(GameObject go, Map map, ITextureControl control)
        {
            map.redrawOnPlay = true;

            string texturePath;
            Texture2D texture = CreateTexture(out texturePath);
            Sprite sprite = GetSprite(texturePath);
            
            if (control is SpriteRendererControl)
            {
                go.GetComponent<SpriteRenderer>().sprite = sprite;
                go.AddComponent<BoxCollider>();
            }
            else if (control is UIImageControl || control is UIRawImageControl)
            {
                if (!uGUIParent) uGUIParent = EditorUtils.GetCanvas().gameObject;

                RectTransform rectTransform = go.AddComponent<RectTransform>();
                rectTransform.SetParent(uGUIParent.transform as RectTransform);
                go.AddComponent<CanvasRenderer>();
                rectTransform.localPosition = Vector3.zero;
                rectTransform.anchorMax = rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = new Vector2(textureWidth, textureHeight);

                if (control is UIImageControl)
                {
                    Image image = go.AddComponent<Image>();
                    image.sprite = sprite;
                }
                else
                {
                    RawImage image = go.AddComponent<RawImage>();
                    image.texture = texture;
                }
            }
            else if (control is NGUITextureControl)
            {
#if NGUI
                go.layer = NGUIParent.layer;
                UITexture uiTexture = go.AddComponent<UITexture>();
                uiTexture.mainTexture = texture;
                uiTexture.width = textureWidth;
                uiTexture.height = textureHeight;
                go.transform.parent = NGUIParent.transform;
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one;
                go.transform.localRotation = Quaternion.Euler(Vector3.zero);
                BoxCollider boxCollider = go.AddComponent<BoxCollider>();
                boxCollider.size = new Vector3(textureWidth, textureHeight, 0);
#endif
            }
            else if (control is PlaneControl)
            {
                Renderer renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = new Material(RenderPipelineHelper.GetDefaultShader());
                renderer.sharedMaterial.mainTexture = texture;
            }
            
            control.SetTexture(texture);
        }

        private void DrawCamera(ref bool allowCreate)
        {
            EditorUtils.GroupLabel("Camera Settings");
            EditorGUIUtility.labelWidth += 100;

            Camera cam = activeCamera ? activeCamera : Camera.main;
            activeCamera = EditorGUILayout.ObjectField("Camera: ", cam, typeof(Camera), true) as Camera;
            moveCameraToMap = EditorGUILayout.Toggle("Move camera to Map", moveCameraToMap);
            fixClippingPlanes = EditorGUILayout.Toggle("Fix Camera - Clipping Planes - Far", fixClippingPlanes);
            
            EditorGUIUtility.labelWidth -= 100;
        }

        private void DrawControls(ref bool allowcreate)
        {
            if (is2D)
            {
                EditorGUILayout.HelpBox(
                    "All 2D controls have the same features.\nSelect a control, depending on the place where you want to show the map.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Tileset - a dynamic mesh. Faster, uses less memory and has many additional features. It is recommended for most applications.\nTexture (Plane) - used to display maps on the plane.",
                    MessageType.Info);
            }

            EditorUtils.GroupLabel("Select Control");

            string[] titles = is2D ? control2DTitles : control3DTitles;

            EditorGUI.BeginChangeCheck();
            selectedIndex = GUILayout.SelectionGrid(selectedIndex, titles, 1, "toggle");
            if (EditorGUI.EndChangeCheck())
            {
                if (is2D) default2DIndex = selectedIndex;
                else default3DIndex = selectedIndex;
                InitSteps();
            }
        }

        private void DrawLabels()
        {
            bool showLanguage;
            if (activeMapType.hasLabels)
            {
                labels = EditorGUILayout.Toggle("Labels: ", labels);
                showLanguage = labels;
            }
            else
            {
                showLanguage = activeMapType.labelsEnabled;
                GUILayout.Label("Labels " + (showLanguage ? "enabled" : "disabled"));
            }
            if (showLanguage && activeMapType.hasLanguage)
            {
                language = EditorGUILayout.TextField("Language: ", language);
                EditorGUILayout.HelpBox(activeMapType.provider.twoLetterLanguage ? "Use two-letter code such as: en" : "Use three-letter code such as: eng", MessageType.Info);
            }
        }

        private void DrawMapType(ref bool allowCreate)
        {
            EditorUtils.GroupLabel("Select Map Type");
            EditorGUI.BeginChangeCheck();
            is2D = GUILayout.SelectionGrid(is2D? 0: 1, new[] { "2D", "3D" }, 1, "toggle") == 0;
            if (EditorGUI.EndChangeCheck())
            {
                selectedIndex = is2D ? default2DIndex : default3DIndex;
                InitSteps();
            }
        }

        private void DrawMaterialsAndShaders(ref bool allowcreate)
        {
            EditorUtils.GroupLabel("Materials and Shaders (optional)");

            tileMaterial = EditorGUILayout.ObjectField("Tile material: ", tileMaterial, typeof(Material), false) as Material;
            markerMaterial = EditorGUILayout.ObjectField("Marker Material:", markerMaterial, typeof(Material), false) as Material;
            tilesetShader = EditorGUILayout.ObjectField("Tileset Shader:", tilesetShader, typeof(Shader), true) as Shader;
            markerShader = EditorGUILayout.ObjectField("Marker Shader:", markerShader, typeof(Shader), false) as Shader;
            drawingShader = EditorGUILayout.ObjectField("Drawing Shader:", drawingShader, typeof(Shader), false) as Shader;
        }

        private void DrawMeshSize(ref bool allowCreate)
        {
            EditorUtils.GroupLabel("Size");
            tilesetWidth = EditorGUILayout.IntField("Width (pixels): ", tilesetWidth);
            tilesetHeight = EditorGUILayout.IntField("Height (pixels): ", tilesetHeight);
            sizeInScene = EditorGUILayout.Vector2Field("Size (in scene): ", sizeInScene);

            tilesetWidth = Mathf.ClosestPowerOfTwo(tilesetWidth);
            if (tilesetWidth < 512) tilesetWidth = 512;

            tilesetHeight = Mathf.ClosestPowerOfTwo(tilesetHeight);
            if (tilesetHeight < 512) tilesetHeight = 512;
        }

        private void DrawMoreFeatures(ref bool allowcreate)
        {
            EditorUtils.GroupLabel("More Features");
            EditorGUIUtility.labelWidth += 100;
            traffic = EditorGUILayout.Toggle("Traffic: ", traffic);
            EditorGUIUtility.labelWidth -= 100;
        }

        private void DrawPlugins(ref bool allowcreate)
        {
            EditorUtils.GroupLabel("Plugins");
            EditorGUIUtility.labelWidth += 100;
            foreach (IPlugin plugin in activeControl.plugins)
            {
                if (plugin is Plugin)
                {
                    Plugin p = plugin as Plugin;
                    p.enabled = EditorGUILayout.Toggle(plugin.title, p.enabled);
                }
                else if (plugin is PluginGroup)
                {
                    PluginGroup g = plugin as PluginGroup;
                    g.selected = EditorGUILayout.Popup(g.title, g.selected, g.titles);
                }
            }
            EditorGUIUtility.labelWidth -= 100;
        }
        
        private void DrawThirdPartyPlugins(ref bool allowcreate)
        {
            EditorUtils.GroupLabel("Third-party Connectors");
            EditorGUIUtility.labelWidth += 100;
            foreach (IPlugin plugin in activeControl.thirdPartyPlugins)
            {
                if (plugin is Plugin)
                {
                    Plugin p = plugin as Plugin;
                    p.enabled = EditorGUILayout.Toggle(plugin.title, p.enabled);
                }
                else if (plugin is PluginGroup)
                {
                    PluginGroup g = plugin as PluginGroup;
                    g.selected = EditorGUILayout.Popup(g.title, g.selected, g.titles);
                }
            }
            EditorGUIUtility.labelWidth -= 100;
        }

        private void DrawProvider()
        {
            EditorGUI.BeginChangeCheck();
            providerIndex = EditorGUILayout.Popup("Provider", providerIndex, providersTitle);
            if (EditorGUI.EndChangeCheck()) activeMapType = providers[providerIndex].types[0];

            if (!activeMapType.isCustom) return;
            customProviderURL = EditorGUILayout.TextField("URL: ", customProviderURL);

            EditorGUILayout.BeginVertical(GUI.skin.box);
            showCustomProviderTokens = EditorUtils.Foldout(showCustomProviderTokens, "Available tokens");
            if (showCustomProviderTokens)
            {
                GUILayout.Label("{zoom}");
                GUILayout.Label("{x}");
                GUILayout.Label("{y}");
                GUILayout.Label("{quad}");
                GUILayout.Space(10);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSource(ref bool allowCreate)
        {
            source = (MapSource) EditorGUILayout.EnumPopup("Source: ", source);

            if (source != MapSource.Resources)
            {
                DrawProvider();

                GUIContent[] availableTypes = activeMapType.provider.types.Select(t => new GUIContent(t.title)).ToArray();
                int index = activeMapType.index;
                EditorGUI.BeginChangeCheck();
                index = EditorGUILayout.Popup(new GUIContent("Type: ", "Type of map texture"), index, availableTypes);
                if (EditorGUI.EndChangeCheck()) activeMapType = activeMapType.provider.types[index];

                DrawLabels();
            }
        }

        private void DrawTextureSize(ref bool allowCreate)
        {
            createTexture = EditorGUILayout.ToggleLeft("Create Texture", createTexture);
            EditorGUI.BeginDisabledGroup(!createTexture);

            if (!EditorUtils.availableSizes.Contains(textureWidth)) textureWidth = 512;
            if (!EditorUtils.availableSizes.Contains(textureHeight)) textureHeight = 512;

            textureWidth = EditorGUILayout.IntPopup("Width: ", textureWidth,
                EditorUtils.availableSizesStr, EditorUtils.availableSizes);
            textureHeight = EditorGUILayout.IntPopup("Height: ", textureHeight,
                EditorUtils.availableSizesStr, EditorUtils.availableSizes);

            textureFilename = EditorGUILayout.TextField("Filename: ", textureFilename);

            EditorGUI.EndDisabledGroup();
        }

        private void DrawNGUIParent(ref bool allowCreate)
        {
#if NGUI
            EditorGUILayout.HelpBox("Select the parent GameObject in the scene.", MessageType.Warning);
            NGUIParent = EditorGUILayout.ObjectField("Parent: ", NGUIParent, typeof(GameObject), true) as GameObject;
            if (NGUIParent == null) allowCreate = false;
#endif
        }

        private void DrawUGUIParent(ref bool allowCreate)
        {
            EditorGUILayout.HelpBox("Select the parent GameObject in the scene.", MessageType.Warning);
            uGUIParent = EditorGUILayout.ObjectField("Parent: ", uGUIParent, typeof(GameObject), true) as GameObject;
            if (uGUIParent != null && uGUIParent.GetComponent<CanvasRenderer>() == null && uGUIParent.GetComponent<Canvas>() == null)
            {
                EditorGUILayout.HelpBox("Selected the wrong parent. Parent must contain the Canvas or Canvas Renderer.", MessageType.Error);
                allowCreate = false;
            }
        }

        private Sprite GetSprite(string texturePath)
        {
            if (!activeControl.useSprite) return null;
            if (string.IsNullOrEmpty(texturePath)) return null;
            
            TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (!textureImporter) return null;
            
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.spriteImportMode = SpriteImportMode.Single;
            textureImporter.npotScale = TextureImporterNPOTScale.None;
            textureImporter.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath(texturePath, typeof(Sprite)) as Sprite;
        }

        private void InitControlSteps(Type type, Control control)
        {
            if (type == typeof(UIImageControl) || type == typeof(UIRawImageControl)) control.steps.Add(DrawUGUIParent);
            else if (type == typeof(NGUITextureControl)) control.steps.Add(DrawNGUIParent);
            if (type.IsSubclassOf(typeof(ControlBase3D))) control.steps.Add(DrawCamera);
            if (type.IsSubclassOf(typeof(ControlBaseDynamicMesh))) control.steps.Add(DrawMeshSize);
            if (type == typeof(TileSetControl)) control.steps.Add(DrawMaterialsAndShaders);

            object[] controlHelperAttributes = type.GetCustomAttributes(typeof(WizardControlHelperAttribute), true);
            if (controlHelperAttributes.Length > 0)
            {
                control.resultType = (controlHelperAttributes[0] as WizardControlHelperAttribute).resultType;
                if (control.resultType == MapTarget.texture)
                {
                    control.steps.Add(DrawTextureSize);
                }
            }
        }

        private void InitControlPlugins(Type type, Control control)
        {
            foreach (IPlugin plugin in plugins)
            {
                Type requiredType = null;
                bool thirdParty = false;
                
                if (plugin is Plugin)
                {
                    Plugin p = plugin as Plugin;
                    requiredType = p.attribute.requiredType;
                    thirdParty = p.thirdParty;
                }
                else if (plugin is PluginGroup)
                {
                    PluginGroup g = plugin as PluginGroup;
                    Plugin first = g.plugins[0];
                    requiredType = first.attribute.requiredType;
                    thirdParty = first.thirdParty;
                }

                if (!type.IsSubclassOf(requiredType)) continue;
                
                if (thirdParty) control.thirdPartyPlugins.Add(plugin);
                else control.plugins.Add(plugin);
            }
        }

        private void InitSRPShaders()
        {
            string[] assets = AssetDatabase.FindAssets("TilesetPBRShader");
            if (assets.Length > 0) defaultTilesetShader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(assets[0]));
            else defaultTilesetShader = Shader.Find("Infinity Code/Online Maps/Tileset Cutout");
            tilesetShader = defaultTilesetShader;

            assets = AssetDatabase.FindAssets("TilesetPBRMarkerShader");
            if (assets.Length > 0) markerShader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(assets[0]));
            else markerShader = RenderPipelineHelper.GetTransparentShader();

            assets = AssetDatabase.FindAssets("TilesetPBRDrawingElement");
            if (assets.Length > 0) drawingShader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(assets[0]));
            else drawingShader = Shader.Find("Infinity Code/Online Maps/Tileset DrawingElement");
        }

        private void InitShaders()
        {
            bool useSRP = Compatibility.GetRenderPipelineAsset();

            if (useSRP) InitSRPShaders();
            else
            {
                defaultTilesetShader = Shader.Find("Infinity Code/Online Maps/Tileset Cutout");
                tilesetShader = defaultTilesetShader;
                markerShader = RenderPipelineHelper.GetTransparentShader();
                drawingShader = Shader.Find("Infinity Code/Online Maps/Tileset DrawingElement");
            }
        }

        private void InitSteps()
        {
            steps = new List<WizardDelegate>();
            steps.Add(DrawMapType);
            steps.Add(DrawControls);
            steps.Add(DrawSource);
            steps.AddRange(activeControl.steps);
            steps.Add(DrawMoreFeatures);
            steps.Add(DrawPlugins);
            steps.Add(DrawThirdPartyPlugins);
        }

        private void OnEnable()
        {
            activeMapType = TileProvider.FindMapType("arcgis");
            providers = TileProvider.providers;
            providersTitle = TileProvider.GetProvidersTitle();
            providerIndex = activeMapType.provider.index;
            
            activeCamera = Camera.main;
            
            InitShaders();
            CachePlugins();
            CacheControls();
            InitSteps();
        }

        private void OnGUI()
        {
            if (steps == null || steps.Count == 0) InitSteps();
            bool allowCreate = true;
            float labelWidth = EditorGUIUtility.labelWidth;
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (WizardDelegate s in steps)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                s(ref allowCreate);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
            EditorGUIUtility.labelWidth = labelWidth;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUIStyle.none);

            EditorGUI.BeginDisabledGroup(!allowCreate);
            if (GUILayout.Button("Create", GUILayout.ExpandWidth(false)))
            {
                try
                {
                    CreateMap();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return;
                }
                Close();
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        [MenuItem(EditorUtils.MenuPath + "Map Wizard", false, 0)]
        public static void OpenWindow()
        {
            GetWindow<Wizard>(true, "Map Wizard", true);
        }

        internal class Control
        {
            public List<IPlugin> plugins = new List<IPlugin>();
            public List<IPlugin> thirdPartyPlugins = new List<IPlugin>();
            public MapTarget resultType;
            public List<WizardDelegate> steps = new List<WizardDelegate>();
            public string title;
            public Type type;
            public bool useSprite;

            public Control(string fullName, Type t)
            {
                title = fullName;
                type = t;

                useSprite = t == typeof(SpriteRendererControl) || t == typeof(UIImageControl);
            }
        }

        internal interface IPlugin
        {
            string title { get; }
        }

        internal class Plugin: IPlugin
        {
            public PluginAttribute attribute;
            public bool enabled;
            public Type type;
            public bool thirdParty;

            private string _title;

            public Plugin(Type type, PluginAttribute attribute)
            {
                this.type = type;
                this.attribute = attribute;
                enabled = attribute.enabledByDefault;
                _title = attribute.title;
                thirdParty = type.GetCustomAttributes(typeof(ThirdPartyPluginAttribute), true).Length > 0;
            }

            public string title { get { return _title; } }
        }

        internal class PluginGroup: IPlugin
        {
            public string title { get { return _title; } }

        
            public int selected = 0;

            public List<Plugin> plugins;
            private string _title;
            private List<string> _titles;
            private string[] _ts;

            public string[] titles
            {
                get
                {
                    if (_ts == null)
                    {
                        List<string> orderedTitles = _titles.OrderBy(t => t).ToList();
                        orderedTitles.Insert(0, "None");
                        _ts = orderedTitles.ToArray();
                    }
                    return _ts;
                }
            }

            public PluginGroup(string title)
            {
                _title = title;
                plugins = new List<Plugin>();
                _titles = new List<string>();
            }

            public void Add(Type type, PluginAttribute p)
            {
                plugins.Add(new Plugin(type, p));
                _titles.Add(p.title);
            }
        }
    }
}