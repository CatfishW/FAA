/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OnlineMaps
{
    /// <summary>
    /// Class implements the basic functionality control of the map.
    /// </summary>
    [Serializable]
    [WizardControlHelper(MapTarget.texture)]
    [RequireComponent(typeof(Marker2DManager))]
    public abstract class ControlBase : MonoBehaviour, ISavableAdvanced
    {
        #region Variables

        #region Static Fields

        /// <summary>
        /// Singleton of control
        /// </summary>
        protected static ControlBase _instance;

        #endregion

        #region Actions

        /// <summary>
        /// Event that occurs when need to draw markers.
        /// </summary>
        public Action OnDrawMarkers;

        /// <summary>
        /// Event that occurs when you click on the map.
        /// </summary>
        public Action OnClick;

        /// <summary>
        /// Event that occurs when you double-click on the map.
        /// </summary>
        public Action OnDoubleClick;

        /// <summary>
        /// Event that occurs when you drag the map.
        /// </summary>
        public Action OnDrag;

        /// <summary>
        /// Event that occurs when you handle interaction.
        /// </summary>
        public Action OnHandleInteraction;

        /// <summary>
        /// Event that occurs when you long press the map.
        /// </summary>
        public Action OnLongPress;

        /// <summary>
        /// Event that occurs when you press on the map.
        /// </summary>
        public Action OnPress;

        /// <summary>
        /// Event that occurs when you release the map.
        /// </summary>
        public Action OnRelease;

        /// <summary>
        /// Event that occurs at end Update.
        /// </summary>
        public Action OnUpdateAfter;

        /// <summary>
        /// Event that occurs at start Update.
        /// </summary>
        public Action OnUpdateBefore;

        /// <summary>
        /// Event that occurs when you zoom the map.
        /// </summary>
        public Action OnZoom;

        /// <summary>
        /// Event validating that cursor is on UI element. True - cursor on UI element, false - otherwise.
        /// </summary>
        public Predicate<GameObject> OnValidateCursorOnUIElement;

        #endregion

        #region Fields
        
        private Map _map;
        private IMapDrawer _mapDrawer;
        private Marker2DDrawer _marker2DDrawer;
        private Rect _screenRect;
        private SavableItem[] savableItems;

        #endregion

        #endregion

        #region Properties

        /// <summary>
        /// Singleton instance of map control.
        /// </summary>
        public static ControlBase instance => _instance;

        /// <summary>
        /// Indicates whether it is possible to get the screen coordinates store. True - for 2D map, false - for the 3D map.
        /// </summary>
        public virtual bool allowMarkerScreenRect => false;
        
        /// <summary>
        /// Reference to drawing element manager
        /// </summary>
        public DrawingElementManager drawingElementManager { get; private set; }

        /// <summary>
        /// Height of the map in pixels.
        /// </summary>
        public abstract int height { get; }

        /// <summary>
        /// Reference to map instance.
        /// </summary>
        public Map map
        {
            get
            {
                if (!_map) _map = GetComponent<Map>();
                return _map;
            }
        }

        /// <summary>
        /// Reference to map drawer.
        /// </summary>
        public IMapDrawer mapDrawer
        {
            get => _mapDrawer;
            set
            {
                if (_mapDrawer != null) _mapDrawer.Dispose();
                _mapDrawer = value;
            }
        }

        /// <summary>
        /// Gets/sets the marker drawer.
        /// </summary>
        public Marker2DDrawer marker2DDrawer
        {
            get => _marker2DDrawer;
            set
            {
                if (_marker2DDrawer != null) _marker2DDrawer.Dispose();
                _marker2DDrawer = value;
            }
        }

        /// <summary>
        /// Reference to marker manager
        /// </summary>
        public Marker2DManager marker2DManager { get; private set; }

        /// <summary>
        /// Mipmap for tiles.
        /// </summary>
        public virtual bool mipmapForTiles
        {
            get => false;
            set => throw new Exception("This control does not support mipmap for tiles.");
        }

        /// <summary>
        /// Screen area occupied by the map.
        /// </summary>
        public virtual Rect screenRect => _screenRect;

        /// <summary>
        /// Whether the current control draws to a texture.
        /// </summary>
        public bool resultIsTexture => resultType == MapTarget.texture;

        /// <summary>
        /// The result type of the current control.
        /// </summary>
        public virtual MapTarget resultType => MapTarget.texture;

        /// <summary>
        /// Whether the current control uses raster tiles.
        /// </summary>
        public virtual bool useRasterTiles => true;

        /// <summary>
        /// UV rectangle used by the texture of the map.
        /// NGUI: uiTexture.uvRect.
        /// Other: new Rect(0, 0, 1, 1);
        /// </summary>
        public virtual Rect uvRect => new(0, 0, 1, 1);

        /// <summary>
        /// Width of the map in pixels.
        /// </summary>
        public abstract int width { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Function, which is executed after map updating.
        /// </summary>
        protected virtual void AfterUpdate()
        {
        
        }

        /// <summary>
        /// Function, which is executed before map updating.
        /// </summary>
        protected virtual void BeforeUpdate()
        {
        
        }

        /// <summary>
        /// Creates a new tile.
        /// </summary>
        /// <param name="x">Tile X</param>
        /// <param name="y">Tile Y</param>
        /// <param name="zoom">Tile Zoom</param>
        /// <param name="isMapTile">Should this tile be displayed on the map?</param>
        /// <returns>Tile</returns>
        public virtual Tile CreateTile(int x, int y, int zoom, bool isMapTile = true)
        {
            return new RasterTile(x, y, zoom, map, isMapTile);
        }

        /// <summary>
        /// Get the interactive element located at the screen position.
        /// </summary>
        /// <param name="screenPosition">Screen position</param>
        /// <returns>Interactive element</returns>
        public virtual IInteractiveElement GetInteractiveElement(Vector2 screenPosition)
        {
            if (IsCursorOnUIElement(screenPosition)) return null;

            Marker2D marker = marker2DDrawer.GetMarkerFromScreen(screenPosition);
            if (marker != null) return marker;

            DrawingElement drawingElement = map.GetDrawingElement(screenPosition);
            return drawingElement;
        }

        /// <summary>
        /// Screen area occupied by the map.
        /// </summary>
        /// <returns>Screen rectangle</returns>
        public virtual Rect GetScreenRect()
        {
            return new Rect();
        }

        public SavableItem[] GetSavableItems()
        {
            if (savableItems != null) return savableItems;

            savableItems = new[]
            {
                new SavableItem("control", "Control", SaveSettings)
                {
                    loadCallback = LoadSettings
                }
            };

            return savableItems;
        }

        /// <summary>
        /// Checks whether the cursor over the map.
        /// </summary>
        /// <returns>True - if the cursor over the map, false - if not.</returns>
        public bool HitTest()
        {
            return HitTest(InputManager.mousePosition);
        }

        /// <summary>
        /// Checks whether specified position over the map.
        /// </summary>
        /// <param name="screenPosition">Screen position</param>
        /// <returns>True - if the position over the map, false - if not.</returns>
        public virtual bool HitTest(Vector2 screenPosition)
        {
            return true;
        }

        /// <summary>
        /// Whether the screen position is on UI element
        /// </summary>
        /// <param name="position">Screen position</param>
        /// <returns>True - on UI element, False - otherwise</returns>
        public bool IsCursorOnUIElement(Vector2 position)
        {
            if (!map.notInteractUnderGUI) return false;
#if !IGUI && ((!UNITY_ANDROID && !UNITY_IOS) || UNITY_EDITOR)
            if (GUIUtility.hotControl != 0) return true;
#endif
            if (!EventSystem.current) return false;

            PointerEventData pe = new PointerEventData(EventSystem.current);
            pe.position = position;

            List<RaycastResult> hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pe, hits);
            if (hits.Count == 0) return false;

            GameObject go = hits[0].gameObject;
            if (go == gameObject) return false;
            if (go.GetComponent<MarkerInstanceBase>() || go.GetComponent<BuildingBase>()) return false;
            return OnValidateCursorOnUIElement?.Invoke(go) ?? true;
        }

        protected virtual void LoadSettings(JSONObject json)
        {
            json.DeserializeObject(this);
        }

        /// <summary>
        /// Converts geographical coordinate to position in the scene relative to the top-left corner of the map in map space.
        /// </summary>
        /// <param name="location">Geographical coordinate.</param>
        /// <returns>Scene position (in map space)</returns>
        public Vector2d LocationToLocal(GeoPoint location) => LocationToLocal(location.x, location.y);

        /// <summary>
        /// Converts geographical coordinate to position in the scene relative to the top-left corner of the map in map space.
        /// </summary>
        /// <param name="lng">Longitude</param>
        /// <param name="lat">Latitude</param>
        /// <returns>Scene position (in map space)</returns>
        public virtual Vector2d LocationToLocal(double lng, double lat)
        {
            StateProps lastState = map.buffer.lastState;
            MercatorPoint p1 = map.view.projection.LocationToMercator(lng, lat);
            MercatorPoint d = p1 - lastState.centerMercator;
            return d.ToTile(lastState.intZoom) * Constants.TileSize / lastState.zoomFactor;
        }

        /// <summary>
        /// Converts geographical coordinate to position in screen space.
        /// </summary>
        /// <param name="location">Geographical coordinate (X - longitude, Y - latitude)</param>
        /// <returns>Screen space position</returns>
        public Vector2 LocationToScreen(GeoPoint location)
        {
            return LocationToScreen(location.x, location.y);
        }

        /// <summary>
        /// Converts geographical coordinate to position in screen space.
        /// </summary>
        /// <param name="lng">Longitude</param>
        /// <param name="lat">Latitude</param>
        /// <returns>Screen space position</returns>
        public virtual Vector2 LocationToScreen(double lng, double lat)
        {
            Vector2d p = LocationToLocal(lng, lat);
            StateProps lastState = map.buffer.lastState;
            p.x /= lastState.width;
            p.y /= lastState.height;
            Rect mapRect = GetScreenRect();
            p.x = mapRect.x + mapRect.width * p.x;
            p.y = mapRect.y + mapRect.height - mapRect.height * p.y;
            return p;
        }
        
        /// <summary>
        /// Converts a Mercator point to a local position in map space relative to the top-left corner.
        /// </summary>
        /// <param name="mercatorPoint">Mercator point to convert.</param>
        /// <returns>Local position in map space.</returns>
        public virtual Vector2d MercatorToLocal(MercatorPoint mercatorPoint)
        {
            StateProps lastState = map.buffer.lastState;
            MercatorPoint p = mercatorPoint - lastState.centerMercator;
            return p.ToTile(lastState.intZoom) * Constants.TileSize / lastState.zoomFactor;
        } 

        /// <summary>
        /// Event that occurs before Awake.
        /// </summary>
        public virtual void OnAwakeBefore()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            OnClick = null;
            OnDoubleClick = null;
            OnDrag = null;
            OnLongPress = null;
            OnPress = null;
            OnRelease = null;
            OnZoom = null;
            _map = null;
            _instance = null;
            marker2DDrawer = null;
            drawingElementManager = null;

            OnDestroyLate();
        }

        /// <summary>
        /// Event is called after the control has been disposed.
        /// </summary>
        protected virtual void OnDestroyLate()
        {
        
        }

        private void OnEnable()
        {
            _instance = this;

            if (!map)
            {
                Debug.LogError("Can not find a script OnlineMaps.");
                Utils.Destroy(this);
                return;
            }

            drawingElementManager = GetComponent<DrawingElementManager>();
            if (!drawingElementManager) drawingElementManager = gameObject.AddComponent<DrawingElementManager>();

            marker2DManager = GetComponent<Marker2DManager>();
            marker2DManager.dragMarker = null;
            
            Marker2DManager.Init();
            DrawingElementManager.Init();
            if (resultIsTexture) marker2DDrawer = new MarkerBufferDrawer(this);

            OnEnableLate();
        }

        /// <summary>
        /// Function that is called after control of the map enabled.
        /// </summary>
        protected virtual void OnEnableLate()
        {
        
        }

        protected virtual JSONItem SaveSettings()
        {
            return new JSONObject();
        }

        /// <summary>
        /// Returns the geographical coordinates of the location where the cursor is.
        /// </summary>
        /// <returns>Geographical coordinates</returns>
        public GeoPoint ScreenToLocation()
        {
            return ScreenToLocation(InputManager.mousePosition);
        }

        /// <summary>
        /// Returns the geographical coordinates at the specified coordinates of the screen.
        /// </summary>
        /// <param name="screenPosition">Screen coordinates</param>
        /// <returns>Geographical coordinates</returns>
        public GeoPoint ScreenToLocation(Vector2 screenPosition)
        {
            ScreenToLocation(screenPosition, out GeoPoint point);
            return point;
        }

        /// <summary>
        /// Returns the geographical coordinates of the location where the cursor is.
        /// </summary>
        /// <param name="point">Geographical point</param>
        /// <returns>True - success, False - otherwise.</returns>
        public bool ScreenToLocation(out GeoPoint point)
        {
            return ScreenToLocation(InputManager.mousePosition, out point);
        }

        /// <summary>
        /// Returns the geographical coordinates of the location where the position is.
        /// </summary>
        /// <param name="position">Screen position</param>
        /// <param name="point">Geographical point</param>
        /// <returns>True - success, False - otherwise.</returns>
        public abstract bool ScreenToLocation(Vector2 position, out GeoPoint point);

        internal virtual bool ScreenToLocationInternal(out GeoPoint point)
        {
            return ScreenToLocation(InputManager.mousePosition, out point);
        }

        /// <summary>
        /// Gets a tile by screen position.
        /// </summary>
        /// <param name="screenPosition">Screen position</param>
        /// <param name="tilePoint">Tile point</param>
        /// <returns>Tile</returns>
        public abstract bool ScreenToTile(Vector2 screenPosition, out TilePoint tilePoint);

        internal virtual bool ScreenToTileInternal(Vector2 position, out TilePoint tilePoint)
        {
            return ScreenToTile(position, out tilePoint);
        }
        
        /// <summary>
        /// Converts a tile point to a local position in map space relative to the top-left corner.
        /// </summary>
        /// <param name="tilePoint">Tile point to convert.</param>
        /// <returns>Local position in map space.</returns>
        public virtual Vector2d TileToLocal(TilePoint tilePoint)
        {
            StateProps lastState = map.buffer.lastState;
            MercatorPoint p = tilePoint.ToMercator() - lastState.centerMercator;
            return p.ToTile(lastState.intZoom) * Constants.TileSize / lastState.zoomFactor;
        }

        protected void Update()
        {
            if (OnUpdateBefore != null) OnUpdateBefore();

            BeforeUpdate();
            _screenRect = GetScreenRect();
            if (!map.blockAllInteractions) OnHandleInteraction?.Invoke();
            AfterUpdate();

            if (OnUpdateAfter != null) OnUpdateAfter();
        }

        /// <summary>
        /// Updates the control.
        /// </summary>
        public virtual void UpdateControl()
        {
            
        }

        /// <summary>
        /// Changes the zoom keeping a specified point on same place.
        /// </summary>
        /// <param name="zoomOffset">Positive - zoom in, Negative - zoom out</param>
        /// <param name="screenPosition">Screen position</param>
        /// <returns>True - if zoom changed, False - if zoom not changed</returns>
        public bool ZoomOnPoint(float zoomOffset, Vector2 screenPosition)
        {
            float newZoom = Mathf.Clamp(map.view.zoom + zoomOffset, Constants.MinZoom, Constants.MaxZoomExt);
            if (Math.Abs(newZoom - map.view.zoom) < float.Epsilon) return false;
            
            bool hit = ScreenToTile(screenPosition, out TilePoint t1);
            if (!hit) return false;

            map.dispatchEvents = false;

            int zoom = map.view.intZoom;
            TilePoint tc = map.view.centerTile;

            map.view.zoom = newZoom;

            GeoPoint gp = ScreenToLocation(screenPosition);
            TilePoint t2 = gp.ToTile(map, zoom);
            tc += t1 - t2;
            map.view.center = tc.ToLocation(map);
            
            map.dispatchEvents = true;
            map.DispatchEvent(Events.changedPosition, Events.changedZoom);

            if (OnZoom != null) OnZoom();
            map.Redraw();
            return true;
        }

        #endregion
    }
}