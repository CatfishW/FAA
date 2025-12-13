using UnityEngine;
using UnityEditor;

namespace WeatherRadar.Weather3D.Editor
{
    /// <summary>
    /// Quick setup context menu for creating 3D Weather system from right-click.
    /// </summary>
    public static class Weather3DContextMenu
    {
        [MenuItem("GameObject/Weather Radar/Create 3D Weather System", false, 10)]
        public static void CreateWeather3DSystem(MenuCommand menuCommand)
        {
            // Create root object
            GameObject root = new GameObject("Weather3D_System");
            
            // Parent to context if applicable
            GameObjectUtility.SetParentAndAlign(root, menuCommand.context as GameObject);
            
            // Register undo
            Undo.RegisterCreatedObjectUndo(root, "Create 3D Weather System");
            
            // Add core components
            var manager = root.AddComponent<Weather3DManager>();
            root.AddComponent<Weather3DRadarBridge>();
            
            // Create child renderers
            CreateChildRenderer<VolumetricCloudRenderer>(root, "CloudRenderer");
            CreateChildRenderer<PrecipitationSystem>(root, "PrecipitationRenderer");
            CreateChildRenderer<ThunderstormCellRenderer>(root, "ThunderstormRenderer");
            CreateChildRenderer<TurbulenceIndicator>(root, "TurbulenceRenderer");
            
            // Create UI container
            var uiObj = new GameObject("UI");
            uiObj.transform.SetParent(root.transform);
            uiObj.AddComponent<Weather3DControlPanel>();
            
            // Try to find and auto-assign config
            string[] guids = AssetDatabase.FindAssets("t:Weather3DConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var config = AssetDatabase.LoadAssetAtPath<Weather3DConfig>(path);
                if (config != null)
                {
                    var so = new SerializedObject(manager);
                    var configProp = so.FindProperty("config");
                    if (configProp != null)
                    {
                        configProp.objectReferenceValue = config;
                        so.ApplyModifiedProperties();
                    }
                }
            }
            
            // Try to find and link existing radar provider
            var existingProvider = Object.FindObjectOfType<WeatherRadarProviderBase>();
            if (existingProvider != null)
            {
                var bridge = root.GetComponent<Weather3DRadarBridge>();
                if (bridge != null)
                {
                    var so = new SerializedObject(bridge);
                    var providerProp = so.FindProperty("radarProvider");
                    if (providerProp != null)
                    {
                        providerProp.objectReferenceValue = existingProvider;
                        so.ApplyModifiedProperties();
                    }
                }
                Debug.Log($"[Weather3D] Auto-linked to {existingProvider.ProviderName}");
            }
            
            Selection.activeGameObject = root;
            
            Debug.Log("[Weather3D] Created complete 3D Weather System!");
        }

        private static T CreateChildRenderer<T>(GameObject parent, string name) where T : MonoBehaviour
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform);
            obj.transform.localPosition = Vector3.zero;
            return obj.AddComponent<T>();
        }

        [MenuItem("GameObject/Weather Radar/Create Weather3D Config", false, 11)]
        public static void CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<Weather3DConfig>();
            
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Weather3D Config",
                "Weather3DConfig",
                "asset",
                "Choose where to save the config asset.");
            
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = config;
                
                Debug.Log($"[Weather3D] Created config at: {path}");
            }
        }

        // Validation - enable menu only if manager doesn't exist
        [MenuItem("GameObject/Weather Radar/Create 3D Weather System", true)]
        public static bool ValidateCreateWeather3DSystem()
        {
            // Allow creation - you might want to add validation logic
            return true;
        }
    }
}
