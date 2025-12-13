/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using OnlineMaps;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Base class for marker manager components
    /// </summary>
    [Serializable]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class Marker2DManager : MarkerManagerBase<Marker2DManager, Marker2D>
    {
        /// <summary>
        /// Texture to be used if marker texture is not specified.
        /// </summary>
        public Texture2D defaultTexture;

        /// <summary>
        /// Align for new markers
        /// </summary>
        public Align defaultAlign = Align.Bottom;

        /// <summary>
        /// Specifies whether to create a 2D marker by pressing M under the cursor.
        /// </summary>
        public bool allowAddMarkerByM = true;

        /// <summary>
        /// Create a new marker
        /// </summary>
        /// <param name="location">Location of the marker (X - longitude, Y - latitude)</param>
        /// <param name="label">Tooltip</param>
        /// <returns>Instance of the marker</returns>
        public static Marker2D CreateItem(GeoPoint location, string label)
        {
            if (instance) return instance.Create(location.x, location.y, null, label);
            return null;
        }

        /// <summary>
        /// Create a new marker
        /// </summary>
        /// <param name="location">Location of the marker (X - longitude, Y - latitude)</param>
        /// <param name="texture">Texture of the marker</param>
        /// <param name="label">Tooltip</param>
        /// <returns>Instance of the marker</returns>
        public static Marker2D CreateItem(GeoPoint location, Texture2D texture = null, string label = "")
        {
            if (instance) return instance.Create(location.x, location.y, texture, label);
            return null;
        }

        /// <summary>
        /// Create a new marker
        /// </summary>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        /// <param name="label">Tooltip</param>
        /// <returns>Instance of the marker</returns>
        public static Marker2D CreateItem(double longitude, double latitude, string label)
        {
            if (instance) return instance.Create(longitude, latitude, null, label);
            return null;
        }

        /// <summary>
        /// Create a new marker
        /// </summary>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        /// <param name="texture">Texture of the marker</param>
        /// <param name="label">Tooltip</param>
        /// <returns>Instance of the marker</returns>
        public static Marker2D CreateItem(double longitude, double latitude, Texture2D texture = null, string label = "")
        {
            if (instance != null) return instance.Create(longitude, latitude, texture, label);
            return null;
        }
    
        /// <summary>
        /// Create a new marker
        /// </summary>
        /// <param name="location">Location of the marker (X - longitude, Y - latitude)</param>
        /// <param name="label">Tooltip</param>
        /// <returns>Instance of the marker</returns>
        public Marker2D Create(GeoPoint location, string label)
        {
            if (instance != null) return Create(location.x, location.y, label);
            return null;
        } 

        /// <summary>
        /// Create a new marker
        /// </summary>
        /// <param name="location">Location of the marker (X - longitude, Y - latitude)</param>
        /// <param name="texture">Texture of the marker</param>
        /// <param name="label">Tooltip</param>
        /// <returns>Instance of the marker</returns>
        public Marker2D Create(GeoPoint location, Texture2D texture = null, string label = "")
        {
            if (instance != null) return Create(location.x, location.y, texture, label);
            return null;
        }
    
        /// <summary>
        /// Create a new marker
        /// </summary>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        /// <param name="label">Tooltip</param>
        /// <returns>Instance of the marker</returns>
        public Marker2D Create(double longitude, double latitude, string label)
        {
            return Create(longitude, latitude, null, label);
        }

        /// <summary>
        /// Create a new marker
        /// </summary>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        /// <param name="texture">Texture of the marker</param>
        /// <param name="label">Tooltip</param>
        /// <returns>Instance of the marker</returns>
        public Marker2D Create(double longitude, double latitude, Texture2D texture = null, string label = "")
        {
            if (texture == null) texture = defaultTexture;
            Marker2D marker = _CreateItem(longitude, latitude);
            marker.manager = this;
            marker.texture = texture;
            marker.label = label;
            marker.align = defaultAlign;
            marker.scale = defaultScale;
            marker.Init();
            Redraw();
            return marker;
        }

        public override SavableItem[] GetSavableItems()
        {
            if (savableItems != null) return savableItems;

            savableItems = new []
            {
                new SavableItem("markers", "2D Markers", SaveSettings)
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
            JSONItem jitems = json["items"];
            RemoveAll();
            foreach (JSONItem jitem in jitems)
            {
                Marker2D marker = new Marker2D();

                marker.location = jitem.ChildValue<GeoPoint>("location");
                marker.range = jitem.ChildValue<LimitedRange>("range");
                marker.label = jitem.ChildValue<string>("label");
                marker.texture = Utils.GetObject(jitem.ChildValue<int>("texture")) as Texture2D;
                marker.align = (Align)jitem.ChildValue<int>("align");
                marker.rotation = jitem.ChildValue<float>("rotation");
                marker.enabled = jitem.ChildValue<bool>("enabled");
                Add(marker);
            }

            JSONItem jsettings = json["settings"];
            defaultTexture = Utils.GetObject(jsettings.ChildValue<int>("defaultTexture")) as Texture2D;
            defaultAlign = (Align)jsettings.ChildValue<int>("defaultAlign");
            defaultScale = jsettings.ChildValue<float>("defaultScale");
            allowAddMarkerByM = jsettings.ChildValue<bool>("allowAddMarkerByM");
        }

        protected override JSONItem SaveSettings()
        {
            JSONItem jitem = base.SaveSettings();
            jitem["settings"].AppendObject(new
            {
                defaultTexture = defaultTexture != null? defaultTexture.GetInstanceID(): -1,
                defaultAlign = (int)defaultAlign,
                defaultScale,
                allowAddMarkerByM
            });
            return jitem;
        }

        protected override void Start()
        {
            base.Start();

            foreach (Marker2D marker in items)
            {
                marker.manager = this;
                marker.Init();
            }
        }

        protected override void Update()
        {
            base.Update();

            if (allowAddMarkerByM && InputManager.GetKeyUp(KeyCode.M))
            {
                if (map.control.ScreenToLocation(out GeoPoint p)) Create(p);
            }
        }
    }
}