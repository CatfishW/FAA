/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(Cache), true)]
    public class CacheEditor:FormattedEditor
    {
        private Map map;
        private Cache cache;
        private bool showPathTokens;

        private SerializedProperty pUseFileCache;
        private SerializedProperty pFileCacheLocation;
        private SerializedProperty pFileCacheCustomPath;
        private SerializedProperty pFileCacheTilePath;
        private SerializedProperty pMaxFileCacheSize;
        private SerializedProperty pFileCacheUnloadRate;

        private SerializedProperty pUseMemoryCache;
        private SerializedProperty pMaxMemoryCacheSize;
        private SerializedProperty pMemoryCacheUnloadRate;

        private SerializedProperty pMaxCustomCacheSize;
        private SerializedProperty pCustomCacheUnloadRate;

        private int? customCacheSize;
        private int? fileCacheSize;

        protected override void CacheSerializedFields()
        {
            cache = target as Cache;
            map = cache.GetComponent<Map>();

            pUseMemoryCache = serializedObject.FindProperty("useMemoryCache");
            pUseFileCache = serializedObject.FindProperty("useFileCache");
            pFileCacheLocation = serializedObject.FindProperty("fileCacheLocation");
            pFileCacheCustomPath = serializedObject.FindProperty("fileCacheCustomPath");
            pFileCacheTilePath = serializedObject.FindProperty("fileCacheTilePath");
            pMaxFileCacheSize = serializedObject.FindProperty("maxFileCacheSize");
            pFileCacheUnloadRate = serializedObject.FindProperty("fileCacheUnloadRate");

            pMaxMemoryCacheSize = serializedObject.FindProperty("maxMemoryCacheSize");
            pMemoryCacheUnloadRate = serializedObject.FindProperty("memoryCacheUnloadRate");
        
            pMaxCustomCacheSize = serializedObject.FindProperty("maxCustomCacheSize");
            pCustomCacheUnloadRate = serializedObject.FindProperty("customCacheUnloadRate");
        }

        private void CheckFileCacheSize()
        {
            if (pMaxFileCacheSize.intValue >= pMaxMemoryCacheSize.intValue * 2) return;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.HelpBox("The size of the file cache should be a minimum of twice the size of the memory cache.", MessageType.Error);
            if (GUILayout.Button("Increase size of the file cache"))
            {
                pMaxFileCacheSize.intValue = pMaxMemoryCacheSize.intValue * 2;
            }

            EditorGUILayout.EndVertical();
        }

        private void CheckMemoryCacheSize()
        {
            if (!map) return;
            
            int w = map.control.width;
            int h = map.control.height;
            w /= Constants.TileSize;
            h /= Constants.TileSize;
            w += 2;
            h += 2;
            int c = w * h;

            for (int i = 0; i < 5; i++)
            {
                c += (w * h) >> (i + 2);
            }

            int s = (int)(c * 0.2);

            if (pMaxMemoryCacheSize.intValue >= s) return;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.HelpBox($"The minimum recommended memory cache is {s} MB.", MessageType.Error);
            if (GUILayout.Button("Increase size of the memory cache"))
            {
                pMaxMemoryCacheSize.intValue = s;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAvailableTokens()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            showPathTokens = EditorUtils.Foldout(showPathTokens, "Available Tokens");
            if (showPathTokens)
            {
                GUILayout.Label("{pid} - Provider ID");
                GUILayout.Label("{mid} - MapType ID");
                GUILayout.Label("{zoom}, {z} - Tile Zoom");
                GUILayout.Label("{x} - Tile X");
                GUILayout.Label("{y} - Tile Y");
                GUILayout.Label("{quad} - Tile Quad Key");
                GUILayout.Label("{lng} - Language code");
                GUILayout.Label("{lbs} - Labels");
                GUILayout.Space(10);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawCustomCacheFolder()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(pFileCacheCustomPath, TempContent.Get("Cache Folder"));
            if (GUILayout.Button("...", GUILayout.ExpandWidth(false)))
            {
                string folder = EditorUtility.OpenFolderPanel("Cache folder", pFileCacheCustomPath.stringValue, "");
                if (!string.IsNullOrEmpty(folder)) pFileCacheCustomPath.stringValue = folder;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCustomCacheSize()
        {
            if (Utils.isPlaying || !customCacheSize.HasValue) customCacheSize = cache.GetCustomCacheSizeFast();

            float customCacheSizeMb = customCacheSize.Value / 1000000f;
            string customCacheSizeStr = customCacheSizeMb.ToString("F2");
            EditorGUILayout.LabelField("Current Size (mb)", customCacheSizeStr);
            if (GUILayout.Button("Clear"))
            {
                cache.ClearCustomCache();
                customCacheSize = null;
            }
        }

        private void DrawFileCacheSize()
        {
            if (Utils.isPlaying || !fileCacheSize.HasValue) fileCacheSize = cache.GetFileCacheSizeFast();

            float fileCacheSizeMb = fileCacheSize.Value / 1000000f;
            string fileCacheSizeStr = fileCacheSizeMb.ToString("F2");
            EditorGUILayout.LabelField("Current Size (mb)", fileCacheSizeStr);
            if (!GUILayout.Button("Clear")) return;
            
            cache.ClearFileCache();
            fileCacheSize = null;
        }

        private void DrawCustomCacheUnloadRate()
        {
            pCustomCacheUnloadRate.floatValue = EditorGUILayout.Slider("Unload (%)", Mathf.RoundToInt(pCustomCacheUnloadRate.floatValue * 100), 1, 50) / 100;
        }

        private void DrawFileCacheUnloadRate()
        {
            pFileCacheUnloadRate.floatValue = EditorGUILayout.Slider("Unload (%)", Mathf.RoundToInt(pFileCacheUnloadRate.floatValue * 100), 1, 50) / 100;
        }

        private void DrawMemoryCacheSize()
        {
            if (!Utils.isPlaying) return;
            int memoryCacheSize = (target as Cache).memoryCacheSize;
            float memoryCacheSizeMb = memoryCacheSize / 1000000f;
            string memoryCacheSizeStr = memoryCacheSizeMb < 10 ? memoryCacheSizeMb.ToString("F2") : memoryCacheSizeMb.ToString("F0");
            EditorGUILayout.LabelField("Current Size (mb)", memoryCacheSizeStr);
            if (GUILayout.Button("Clear")) (target as Cache).ClearMemoryCache();
        }

        private void DrawMemoryCacheUnloadRate()
        {
            pMemoryCacheUnloadRate.floatValue = EditorGUILayout.Slider("Unload (%)", Mathf.RoundToInt(pMemoryCacheUnloadRate.floatValue * 100), 1, 50) / 100;
        }

        protected override void GenerateLayoutItems()
        {
            base.GenerateLayoutItems();

            GenerateFileCacheLayout();
            GenerateMemoryCacheLayout();
            GenerateCustomCacheLayout();
        }

        private void GenerateCustomCacheLayout()
        {
            LayoutItem lCustomCache = rootLayoutItem.Create("CustomCache");
            lCustomCache.drawGroup = LayoutItem.Group.always;

            lCustomCache.Create("label", () => EditorGUILayout.LabelField("Custom Cache"));
            lCustomCache.Create(pMaxCustomCacheSize).content = new GUIContent("Size (mb)");
            lCustomCache.Create("unloadRate", DrawCustomCacheUnloadRate);
            lCustomCache.Create("drawSize", DrawCustomCacheSize);
        }

        private void GenerateFileCacheLayout()
        {
            LayoutItem lFileCache = rootLayoutItem.Create(pUseFileCache);
            lFileCache.drawGroup = LayoutItem.Group.valueOn;

            lFileCache.Create("checkCacheSize", CheckFileCacheSize);

            lFileCache.Create(pMaxFileCacheSize).content = new GUIContent("Size (mb)");
            lFileCache.Create("pFileCacheUnloadRate", DrawFileCacheUnloadRate);

            LayoutItem lFileCacheLocation = lFileCache.Create(pFileCacheLocation);
            lFileCacheLocation.content = new GUIContent("Cache Location");
            lFileCacheLocation.drawGroup = LayoutItem.Group.validated;
            lFileCacheLocation.drawGroupBorder = false;
            lFileCacheLocation.OnValidateDrawChildren += () => pFileCacheLocation.enumValueIndex == (int) Cache.CacheLocation.custom;
            lFileCacheLocation.OnChildChanged += item =>
            {
                fileCacheSize = null;
                serializedObject.ApplyModifiedProperties();
            };
            lFileCacheLocation.OnChanged += () =>
            {
                fileCacheSize = null;
                serializedObject.ApplyModifiedProperties();
            };
            lFileCacheLocation.Create("customCacheFolder", DrawCustomCacheFolder);

            lFileCache.Create(pFileCacheTilePath).content = new GUIContent("Tile Path");

            lFileCache.Create("availableTokens", DrawAvailableTokens);
            lFileCache.Create("drawFileCacheSize", DrawFileCacheSize);
        }

        private void GenerateMemoryCacheLayout()
        {
            LayoutItem lMemoryCache = rootLayoutItem.Create(pUseMemoryCache);
            lMemoryCache.drawGroup = LayoutItem.Group.valueOn;

            lMemoryCache.Create("checkCacheSize", CheckMemoryCacheSize);
            lMemoryCache.Create(pMaxMemoryCacheSize).content = new GUIContent("Size (mb)");
            lMemoryCache.Create("unloadRate", DrawMemoryCacheUnloadRate);
            lMemoryCache.Create("drawSize", DrawMemoryCacheSize);
        }
    }
}