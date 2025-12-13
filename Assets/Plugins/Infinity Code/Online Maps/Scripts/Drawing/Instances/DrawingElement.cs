/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OnlineMaps;
using UnityEngine;
using UnityEngine.Rendering;

namespace OnlineMaps
{
    /// <summary>
    /// Class implements the basic functionality of drawing on the map.
    /// </summary>
    public abstract class DrawingElement: IInteractiveElement, IDataContainer
    {
        /// <summary>
        /// Default event caused to draw tooltip.
        /// </summary>
        public static Action<DrawingElement> OnElementDrawTooltip;

        /// <summary>
        /// Events that occur when user click on the drawing element.
        /// </summary>
        public Action<DrawingElement> OnClick;

        /// <summary>
        /// Events that occur when user double-click on the drawing element.
        /// </summary>
        public Action<DrawingElement> OnDoubleClick;

        /// <summary>
        /// Event caused to draw tooltip.
        /// </summary>
        public Action<DrawingElement> OnDrawTooltip;

        /// <summary>
        /// Event that occur when tileset initializes a mesh.
        /// </summary>
        public Action<DrawingElement, Renderer> OnInitMesh;

        /// <summary>
        /// Events that occur when user long press on the drawing element.
        /// </summary>
        public Action<DrawingElement> OnLongPress;

        /// <summary>
        /// Events that occur when user press on the drawing element.
        /// </summary>
        public Action<DrawingElement> OnPress;

        /// <summary>
        /// Events that occur when user release on the drawing element.
        /// </summary>
        public Action<DrawingElement> OnRelease;

        /// <summary>
        /// Need to check the map boundaries? It allows you to make drawing element, which are active outside the map.
        /// </summary>
        public bool checkMapBoundaries = true;

        /// <summary>
        /// Zoom range, in which the drawing element will be displayed.
        /// </summary>
        public LimitedRange range;

        /// <summary>
        /// Tooltip that is displayed when user hover on the drawing element.
        /// </summary>
        public string tooltip;

        /// <summary>
        /// The local Y position for the GameObject on Tileset.
        /// </summary>
        public float yOffset = 0;

        protected bool _visible = true;

        private string _name;
        private int _renderQueueOffset;
        private IInteractiveElementManager _manager;

        protected GeoPoint[] _points;

        public MercatorPoint[] mercatorPoints { get; protected set; }

        /// <summary>
        /// Gets or sets custom data by key.
        /// </summary>
        /// <param name="key">Key</param>
        public object this[string key]
        {
            get
            {
                object val;
                return customData.TryGetValue(key, out val) ? val : null;
            }
            set => customData[key] = value;
        }
        
        /// <summary>
        /// Gets or sets the type of the buffer drawer used for this drawing element.
        /// </summary>
        public abstract Type bufferDrawerType { get; set; }

        /// <summary>
        /// Creates a background material for the drawing element.
        /// </summary>
        internal abstract bool createBackgroundMaterial { get; }

        /// <summary>
        /// Gets custom fields.
        /// </summary>
        public Dictionary<string, object> customData { get; private set; } = new Dictionary<string, object>();

        /// <summary>
        /// Center point of the drawing element.
        /// </summary>
        public virtual GeoPoint center => GeoPoint.zero;

        /// <summary>
        /// Default name of the drawing element.
        /// </summary>
        protected virtual string defaultName => "Drawing Element";
        
        /// <summary>
        /// Gets or sets the type of the dynamic mesh drawer used for this drawing element.
        /// </summary>
        public abstract Type dynamicMeshDrawerType { get; set; }
        
        [Obsolete("Use DynamicMeshDrawerBase.TryGetElementData(element) instead.")]
        public GameObject instance
        {
            get
            {
                DynamicMeshElementDrawerBase.ElementData data = DynamicMeshElementDrawerBase.TryGetElementData(this);
                return data != null ? data.gameObject : null;
            }
        }
        
        /// <summary>
        /// Reference to DrawingElementManager.
        /// </summary>
        public IInteractiveElementManager manager
        {
            get => _manager != null? _manager: DrawingElementManager.instance;
            set => _manager = value;
        }

        /// <summary>
        /// Gets or sets the name of the drawing element.
        /// </summary>
        public string name
        {
            get
            {
                if (!string.IsNullOrEmpty(_name)) return _name;
                return defaultName;
            }
            set
            {
                _name = value;
                DynamicMeshElementDrawerBase.TryGetElementData(this)?.SetName(value);
            }
        }

        /// <summary>
        /// Gets or sets the render queue offset.
        /// </summary>
        public int renderQueueOffset => _renderQueueOffset;

        /// <summary>
        /// Should the drawing element be split into pieces?
        /// </summary>
        public virtual bool splitToPieces => false;

        /// <summary>
        /// Gets or sets the visibility of the drawing element.
        /// </summary>
        public virtual bool visible
        {
            get => _visible;
            set
            {
                if (_visible == value) return;

                _visible = value;
                manager.map.Redraw();
            }
        }

        protected DrawingElement()
        {
        
        }

        public void DestroyInstance()
        {
            DynamicMeshElementDrawerBase.TryGetElementData(this)?.Destroy();
        }

        /// <summary>
        /// Dispose drawing element.
        /// </summary>
        public void Dispose()
        {
            _manager = null;
            customData = null;
            OnClick = null;
            OnDoubleClick = null;
            OnDrawTooltip = null;
            OnPress = null;
            OnRelease = null;

            DestroyInstance();
            tooltip = null;

            DisposeLate();
        }

        protected virtual void DisposeLate()
        {
        
        }

        /// <summary>
        /// Gets the data by key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <typeparam name="T">The type of the data.</typeparam>
        /// <returns>The data.</returns>
        public T GetData<T>(string key)
        {
            object val;
            customData.TryGetValue(key, out val);
            return val != null? (T)val : default;
        }

        /// <summary>
        /// Determines if the drawing element at the specified coordinates.
        /// </summary>
        /// <param name="location">Location</param>
        /// <param name="zoom">Zoom</param>
        /// <returns>True if the drawing element in position, false if not.</returns>
        public virtual bool HitTest(GeoPoint location, int zoom)
        {
            return false;
        }

        /// <summary>
        /// It marks the elements changed. It is used for the Drawing API as an overlay.
        /// </summary>
        public static void MarkChanged()
        {
            lock (Tile.lockTiles)
            {
                foreach (Tile tile in Map.instance.tileManager.tiles) tile.drawingChanged = true;
            }
        }

        /// <summary>
        /// Sets the points of the drawing element.
        /// </summary>
        /// <param name="newPoints">The collection of new points (array or list of types: Vector2, Vector2d, GeoPoint, TilePoint, MercatorPoint, float, double).</param>
        public virtual void SetPoints(IEnumerable newPoints)
        {
            if (newPoints == null)
            {
                _points = Array.Empty<GeoPoint>();
                manager.map.Redraw();
                return;
            }

            Map map = manager.map;

            _points = GeoPoint.FromEnumerable(map, newPoints);
            mercatorPoints = _points.Select(p => p.ToMercator(map)).ToArray();
            
            manager.map.Redraw();
        }
    }
}