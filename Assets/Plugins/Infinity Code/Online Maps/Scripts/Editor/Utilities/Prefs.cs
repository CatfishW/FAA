/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OnlineMaps.Editors
{
    [InitializeOnLoad]
    public static class Prefs
    {
        private const string prefsKey = "OM_Settings";

        static Prefs()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static bool Exists()
        {
            return EditorPrefs.HasKey(prefsKey);
        }

        public static Object GetObject(int tid)
        {
            return tid != 0 ? EditorUtility.InstanceIDToObject(tid) : null;
        }
        
        public static IEnumerable<SavableItem> GetSavableItems(ISavable savable)
        {
            if (savable is ISavableAdvanced) return (savable as ISavableAdvanced).GetSavableItems();
            
            string name = savable.GetType().Name;
            SavableItem item = new SavableItem(
                name, 
                ObjectNames.NicifyVariableName(name), 
                () => JSON.Serialize(savable)
            );
            item.loadCallback += obj =>
            {
                if (obj != null) obj.DeserializeObject(savable);
            };
            return new[] { item };
        } 

        private static void OnPlayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            
            TryLoadComponents();
            TryRemoveRefreshFlag();
        }

        public static void Save(string data)
        {
            EditorPrefs.SetString(prefsKey, data);
        }

        private static void TryLoadComponents()
        {
            if (!Exists()) return;
            
            Map map = Object.FindFirstObjectByType<Map>();
            if (!map) return;
            
            try
            {
                Utils.isPlaying = false;
                ISavable[] savableComponents = map.GetComponents<ISavable>();
                SavableItem[] savableItems = savableComponents.SelectMany(GetSavableItems).ToArray();
                if (savableItems.Length == 0) return;

                string prefs = EditorPrefs.GetString(prefsKey);
                JSONObject json = JSON.Parse(prefs) as JSONObject;
                foreach (KeyValuePair<string, JSONItem> pair in json.table)
                {
                    SavableItem item = savableItems.FirstOrDefault(s => s.name == pair.Key);
                    item?.loadCallback?.Invoke(pair.Value as JSONObject);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            EditorPrefs.DeleteKey(prefsKey);
            EditorUtility.SetDirty(map);
        }

        private static void TryRemoveRefreshFlag()
        {
            if (!EditorPrefs.HasKey("OnlineMapsRefreshAssets")) return;
            EditorPrefs.DeleteKey("OnlineMapsRefreshAssets");
            AssetDatabase.Refresh();
        }
    }
}