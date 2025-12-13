/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// This component manages 3D markers.
    /// </summary>
    [Serializable]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class Marker3DManager : MarkerManagerBase<Marker3DManager, Marker3D>
    {
        /// <summary>
        /// Specifies whether to create a 3D marker by pressing N under the cursor.
        /// </summary>
        public bool allowAddMarker3DByN = true;

        /// <summary>
        /// Default 3D marker.
        /// </summary>
        public GameObject defaultPrefab;

        private Transform _container;

        /// <summary>
        /// Container for 3D markers.
        /// </summary>
        public Transform container
        {
            get
            {
                if (_container) return _container;
                
                GameObject go = new GameObject("3D Markers");
                _container = go.transform;
                _container.parent = map.transform;
                _container.localPosition = Vector3.zero;
                _container.localRotation = Quaternion.identity;
                _container.localScale = Vector3.one;

                return _container;
            }
        }

        /// <summary>
        /// Create a new 3D marker
        /// </summary>
        /// <param name="location">Location of the marker (X - longitude, Y - latitude)</param>
        /// <param name="prefab">Prefab</param>
        /// /// <param name="label">Marker label</param>
        /// <returns>Instance of the marker</returns>
        public Marker3D Create(GeoPoint location, GameObject prefab, string label = "")
        {
            return Create(location.x, location.y, prefab, label);
        }

        /// <summary>
        /// Create a new 3D marker
        /// </summary>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        /// <param name="prefab">Prefab</param>
        /// <param name="label">Marker label</param>
        /// <returns>Instance of the marker</returns>
        public Marker3D Create(double longitude, double latitude, GameObject prefab, string label = "")
        {
            Utils.doNotReinitFlag = true;
            Marker3D marker = _CreateItem(longitude, latitude);
            marker.manager = this;
            marker.scale = defaultScale;
            marker.label = label;
            marker.prefab = prefab;
            marker.Init(container);
            Redraw();
            Utils.doNotReinitFlag = false;
            return marker;
        }

        /// <summary>
        /// Creates a new 3D marker from an existing GameObject in the scene.
        /// </summary>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        /// <param name="markerGameObject">GameObject in the scene</param>
        /// <returns>Instance of the marker</returns>
        public Marker3D CreateFromExistGameObject(double longitude, double latitude, GameObject markerGameObject)
        {
            Utils.doNotReinitFlag = true;
            Marker3D marker = _CreateItem(longitude, latitude);
            marker.SetPrefab(markerGameObject, true);
            marker.control = map.control as ControlBase3D;
            marker.manager = this;
            marker.scale = defaultScale;
            markerGameObject.AddComponent<Marker3DInstance>().marker = marker;
            marker.initialized = true;

            Update();

            if (marker.OnInitComplete != null) marker.OnInitComplete(marker);
            Redraw();
            
            Utils.doNotReinitFlag = false;
            
            return marker;
        }

        /// <summary>
        /// Create a new 3D marker
        /// </summary>
        /// <param name="location">Location of the marker (X - longitude, Y - latitude)</param>
        /// <param name="prefab">Prefab</param>
        /// <returns>Instance of the marker</returns>
        public static Marker3D CreateItem(GeoPoint location, GameObject prefab)
        {
            if (instance) return instance.Create(location.x, location.y, prefab);
            return null;
        }

        /// <summary>
        /// Create a new 3D marker
        /// </summary>
        /// <param name="lng">Longitude</param>
        /// <param name="lat">Latitude</param>
        /// <param name="prefab">Prefab</param>
        /// <param name="label">Marker label</param>
        /// <returns>Instance of the marker</returns>
        public static Marker3D CreateItem(double lng, double lat, GameObject prefab, string label = "")
        {
            if (instance) return instance.Create(lng, lat, prefab, label);
            return null;
        }

        /// <summary>
        /// Creates a new 3D marker from an existing GameObject in the scene.
        /// </summary>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        /// <param name="markerGameObject">GameObject in the scene</param>
        /// <returns>Instance of the marker</returns>
        public static Marker3D CreateItemFromExistGameObject(double longitude, double latitude, GameObject markerGameObject)
        {
            if (instance) return instance.CreateFromExistGameObject(longitude, latitude, markerGameObject);
            return null;
        }

        public override SavableItem[] GetSavableItems()
        {
            if (savableItems != null) return savableItems;

            savableItems = new[]
            {
                new SavableItem("markers3D", "3D Markers", SaveSettings)
                {
                    priority = 90,
                    loadCallback = LoadSettings
                }
            };

            return savableItems;
        }

        /// <summary>
        /// Load items and component settings from JSON
        /// </summary>
        /// <param name="json">JSON item</param>
        public void LoadSettings(JSONItem json)
        {
            Utils.doNotReinitFlag = true;
            
            RemoveAll();
            foreach (JSONItem jitem in json["items"])
            {
                Marker3D marker = new Marker3D();

                marker.location = jitem.V<GeoPoint>("location");
                marker.range = jitem.V<LimitedRange>("range");
                marker.label = jitem.V<string>("label");
                marker.prefab = Utils.GetObject(jitem.V<int>("prefab")) as GameObject;
                marker.rotation = jitem.V<float>("rotation");
                marker.scale = jitem.V<float>("scale");
                marker.enabled = jitem.V<bool>("enabled");
                marker.sizeType = (Marker3D.SizeType)jitem.V<int>("sizeType");
                Add(marker);
            }

            (json["settings"] as JSONObject).DeserializeObject(this);
            
            Utils.doNotReinitFlag = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            foreach (Marker3D item in _items) item.DestroyInstance();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            ControlBase3D control = map.control as ControlBase3D;
            if (!control) return;
            if (!control.marker3DManager) control.marker3DManager = this;

            foreach (Marker3D item in _items)
            {
                bool isFirstStart = item.manager == null;
                item.manager = this;
                item.control = control;
                if (!isFirstStart) item.Update();
            }
        }

        private void ProcessShortcuts()
        {
            if (!allowAddMarker3DByN || !InputManager.GetKeyUp(KeyCode.N)) return;
            if (!map.control.ScreenToLocation(out GeoPoint p)) return;
            
            Marker3D marker3D = Create(p, defaultPrefab);
            marker3D.scale = defaultScale;
        }

        protected override JSONItem SaveSettings()
        {
            JSONItem jitem = base.SaveSettings();
            jitem["settings"].AppendObject(new
            {
                allowAddMarker3DByN,
                defaultPrefab = defaultPrefab? defaultPrefab.GetInstanceID(): -1,
                defaultScale
            });
            return jitem;
        }

        protected override void Update()
        {
            base.Update();
            ProcessShortcuts();
        }
    }
}