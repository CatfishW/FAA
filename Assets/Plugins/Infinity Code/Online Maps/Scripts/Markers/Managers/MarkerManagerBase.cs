/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;

namespace OnlineMaps
{
    /// <summary>
    /// Base class for markers managers
    /// </summary>
    /// <typeparam name="T">Subclass of MarkerManagerBase</typeparam>
    /// <typeparam name="U">Type of markers</typeparam>
    [Serializable]
    public abstract class MarkerManagerBase<T, U> : InteractiveElementManager<T, U>, ISavableAdvanced
        where T : MarkerManagerBase<T, U>
        where U : Marker
    {
        /// <summary>
        /// Called when a marker is created
        /// </summary>
        public Action<U> OnCreateItem;

        /// <summary>
        /// Scaling of 3D markers by default
        /// </summary>
        public float defaultScale = 1;

        protected SavableItem[] savableItems;

        /// <summary>
        /// Marker that dragged at the moment.
        /// </summary>
        public Marker dragMarker { get; set; }

        protected U _CreateItem(double longitude, double latitude)
        {
            U item = Activator.CreateInstance<U>();
            item.location = new GeoPoint(longitude, latitude);
            items.Add(item);
            if (OnCreateItem != null) OnCreateItem(item);
            return item;
        }

        /// <summary>
        /// Moves the marker to the location of the cursor.
        /// </summary>
        public void DragMarker()
        {
            bool hit = map.control.ScreenToLocationInternal(out GeoPoint p);

            if (!hit) return;

            Vector2d lastCursorLocation = mouseController?.lastCursorLocation ?? Vector2d.zero;
            Vector2d offset = p - lastCursorLocation;

            if (Math.Abs(offset.sqrMagnitude) < double.Epsilon) return;
            dragMarker.location += offset;
            if (dragMarker.OnDrag != null) dragMarker.OnDrag(dragMarker);
            if (dragMarker is Marker2D) map.Redraw();
        }

        public abstract SavableItem[] GetSavableItems();

        protected override void OnEnable()
        {
            base.OnEnable();

            _instance = (T)this;
        }

        /// <summary>
        /// Removes markers by their tags.
        /// </summary>
        /// <param name="tags">Array of tags to remove markers by.</param>
        public void RemoveByTag(params string[] tags)
        {
            if (tags.Length == 0) return;

            RemoveAll(m =>
            {
                for (int j = 0; j < tags.Length; j++)
                {
                    if (m.tags.Contains(tags[j]))
                    {
                        return true;
                    }
                }

                return false;
            });
        }

        /// <summary>
        /// Removes markers by their tags.
        /// </summary>
        /// <param name="tags">Array of tags to remove markers by.</param>
        public static void RemoveItemsByTag(params string[] tags)
        {
            if (instance) instance.RemoveByTag(tags);
        }

        protected virtual JSONItem SaveSettings()
        {
            JSONArray array = new JSONArray();
            foreach (U marker in items) array.Add(marker.ToJSON());
            JSONObject json = new JSONObject();
            json.Add("settings", new JSONObject());
            json.Add("items", array);
            return json;
        }

        /// <summary>
        /// Called when the script instance is being loaded.
        /// </summary>
        protected virtual void Start()
        {
        }

        /// <summary>
        /// Called every frame, if the MonoBehaviour is enabled.
        /// </summary>
        protected virtual void Update()
        {
        }
    }
}