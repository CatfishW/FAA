/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of runtime saving map state.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "SaveMapStateExample")]
    public class SaveMapStateExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// List of marker textures.
        /// </summary>
        public List<Texture2D> markerTextures;
        
        /// <summary>
        /// List of marker 3D prefabs.
        /// </summary>
        public List<GameObject> marker3DPrefabs;

        /// <summary>
        /// Key for PlayerPrefs.
        /// </summary>
        private string key = "MapSettings";

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            LoadState();
        }

        /// <summary>
        /// Load 2D markers from JSON.
        /// </summary>
        /// <param name="manager">Marker manager</param>
        /// <param name="json">JSON</param>
        private void LoadMarkerManager(Marker2DManager manager, JSONItem json)
        {
            if (json == null) return;
            
            manager.RemoveAll();
            foreach (JSONItem jitem in json)
            {
                double mx = jitem.V<double>("longitude");
                double my = jitem.V<double>("latitude");
                int textureIndex = jitem.V<int>("texture");
                Texture2D texture = null;
                if (textureIndex > -1 && textureIndex < markerTextures.Count) texture = markerTextures[textureIndex];
                string label = jitem.V<string>("label");

                Marker2D marker = manager.Create(mx, my, texture, label);
                
                marker.range = jitem.V<LimitedRange>("range");
                marker.align = (Align)jitem.V<int>("align");
                marker.rotation = jitem.V<float>("rotation");
                marker.enabled = jitem.V<bool>("enabled");
            }
        }

        /// <summary>
        /// Load 3D markers from JSON.
        /// </summary>
        /// <param name="manager">Marker manager</param>
        /// <param name="json">JSON</param>
        private void LoadMarker3DManager(Marker3DManager manager, JSONItem json)
        {
            if (manager == null || json == null) return;
            
            manager.RemoveAll();
            foreach (JSONItem jitem in json)
            {
                double mx = jitem.V<double>("longitude");
                double my = jitem.V<double>("latitude");
                int prefabIndex = jitem.V<int>("prefab");
                GameObject prefab = null;
                if (prefabIndex > -1 && prefabIndex < marker3DPrefabs.Count) prefab = marker3DPrefabs[prefabIndex];

                Marker3D marker = manager.Create(mx, my, prefab);

                marker.range = jitem.V<LimitedRange>("range");
                marker.label = jitem.V<string>("label");
                marker.rotation = jitem.V<float>("rotation");
                marker.scale = jitem.V<float>("scale");
                marker.enabled = jitem.V<bool>("enabled");
                marker.sizeType = (Marker3D.SizeType)jitem.V<int>("sizeType");
            }
        }

        /// <summary>
        /// Loading saved state.
        /// </summary>
        private void LoadState()
        {
            if (!PlayerPrefs.HasKey(key)) return;

            // Load map position and zoom
            string settings = PlayerPrefs.GetString(key);
            JSONItem json = JSON.Parse(settings);
            JSONItem jpos = json["Map/Coordinates"];
            map.view.center = jpos.Deserialize<GeoPoint>();
            map.view.zoom = json["Map/Zoom"].V<float>();

            // Load 2D and 3D markers
            LoadMarkerManager(map.marker2DManager, json["Markers"]);
            LoadMarker3DManager(map.marker3DManager, json["Markers3D"]);
        }

        private void OnGUI()
        {
            // By clicking on the button to save the current state.
            if (GUI.Button(new Rect(5, 5, 150, 30), "Save State")) SaveState();
        }

        private void SaveState()
        {
            JSONObject json = new JSONObject();

            // Save position and zoom
            JSONObject jmap = new JSONObject();
            json.Add("Map", jmap);
            jmap.Add("Coordinates", map.view.center);
            jmap.Add("Zoom", map.view.zoom);

            // Save 2D markers
            JSONArray jmarkers = new JSONArray();
            foreach (Marker2D marker in map.marker2DManager)
            {
                JSONObject jmarker = marker.ToJSON() as JSONObject;
                jmarker.Add("texture", markerTextures.IndexOf(marker.texture));
                jmarkers.Add(jmarker);
            }
            json.Add("Markers", jmarkers);

            // Save 3D markers
            if (map.marker3DManager != null)
            {
                JSONArray jmarkers3d = new JSONArray();
                foreach (Marker3D marker in Marker3DManager.instance)
                {
                    JSONObject jmarker = marker.ToJSON() as JSONObject;
                    jmarker.Add("prefab", marker3DPrefabs.IndexOf(marker.prefab));
                    jmarkers3d.Add(jmarker);
                }
                json.Add("Markers3D", jmarkers3d);
            }

            Debug.Log(json.ToString());
            
            // Save settings to PlayerPrefs
            PlayerPrefs.SetString(key, json.ToString());
        }
    }
}