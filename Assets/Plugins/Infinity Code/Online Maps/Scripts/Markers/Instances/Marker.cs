/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// The base class for markers.
    /// </summary>
    [Serializable]
    public abstract class Marker: IInteractiveElement, IDataContainer
    {
        /// <summary>
        /// Default event caused to draw tooltip.
        /// </summary>
        public static Action<Marker> OnMarkerDrawTooltip;

        /// <summary>
        /// Events that occur when user click on the marker.
        /// </summary>
        public Action<Marker> OnClick;

        /// <summary>
        /// Events that occur when user double-click on the marker.
        /// </summary>
        public Action<Marker> OnDoubleClick;

        /// <summary>
        /// Events that occur when user drag the marker.
        /// </summary>
        public Action<Marker> OnDrag;

        /// <summary>
        /// Event caused to draw tooltip.
        /// </summary>
        public Action<Marker> OnDrawTooltip;

        /// <summary>
        /// Event occurs when the marker enabled change.
        /// </summary>
        public Action<Marker> OnEnabledChange;

        /// <summary>
        /// Event occurs when the marker is initialized.
        /// </summary>
        public Action<Marker> OnInitComplete;

        /// <summary>
        /// Event that occurs when the marker location changed.
        /// </summary>
        public Action<Marker> OnLocationChanged;

        /// <summary>
        /// Events that occur when user long press on the marker.
        /// </summary>
        public Action<Marker> OnLongPress;

        /// <summary>
        /// Events that occur when user press on the marker.
        /// </summary>
        public Action<Marker> OnPress;

        /// <summary>
        /// Events that occur when user release on the marker.
        /// </summary>
        public Action<Marker> OnRelease;

        /// <summary>
        /// Events that occur when user roll out marker.
        /// </summary>
        public Action<Marker> OnRollOut;

        /// <summary>
        /// Events that occur when user roll over marker.
        /// </summary>
        public Action<Marker> OnRollOver;

        /// <summary>
        /// Marker label.
        /// </summary>
        public string label = "";

        /// <summary>
        /// Zoom range, in which the marker will be displayed.
        /// </summary>
        public LimitedRange range = new LimitedRange(Constants.MinZoom, Constants.MaxZoomExt);

        [SerializeField]
        protected bool _enabled = true;

        private bool _isDraggable;

        [SerializeField]
        protected GeoPoint _location;

        private IInteractiveElementManager _manager;

        [SerializeField]
        protected float _rotation;

        [SerializeField]
        protected float _scale = 1;

        [SerializeField]
        protected bool expand = true;

        /// <summary>
        /// Get custom data dictionary.
        /// </summary>
        public Dictionary<string, object> customData { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets marker enabled.
        /// </summary>
        /// <value>
        /// true if enabled, false if not.
        /// </value>
        public virtual bool enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                
                _enabled = value;
                if (OnEnabledChange != null) OnEnabledChange(this);
            }
        }

        /// <summary>
        /// Checks to display marker in current map view.
        /// </summary>
        public virtual bool inMapView
        {
            get
            {
                if (!enabled) return false;
                if (!range.Contains(map.view.intZoom)) return false;

                GeoRect r = map.view.rect;

                if (location.y < r.bottom || location.y > r.top) return false;

                bool isEntireWorld = map.view.maxTiles == map.control.width / Constants.TileSize;
                if (isEntireWorld) return true;

                if (r.left > r.right) r.right += 360;

                double x = location.x;

                if (r.left - x > 180) x += 360;
                else if (r.left - x < -180) x -= 360;

                if (x < r.left || x > r.right) return false;
                return true;
            }
        }

        /// <summary>
        /// Makes the marker draggable or un draggable
        /// </summary>
        public bool isDraggable
        {
            get => _isDraggable;
            set
            {
                if (_isDraggable == value) return;

                if (value)
                {
                    OnPress -= OnMarkerPress;
                    OnPress += OnMarkerPress;
                }
                else OnPress -= OnMarkerPress;

                _isDraggable = value;
            }
        }

        /// <summary>
        /// Gets or sets the location of the marker.
        /// </summary>
        public GeoPoint location
        {
            get => _location;
            set
            {
                if ((_location - value).sqrMagnitude < double.Epsilon) return;
                
                _location = value;
                if (OnLocationChanged != null) OnLocationChanged(this);
                if (Application.isPlaying) map?.Redraw();
            }
        }

        /// <summary>
        /// Reference to Marker Manager
        /// </summary>
        public IInteractiveElementManager manager
        {
            get => _manager != null? _manager: Marker2DManager.instance;
            set => _manager = value;
        }

        protected Map map => _manager == null ? Map.instance : _manager.map;
        
        /// <summary>
        /// Y rotation of 3D marker (degree).
        /// </summary>
        public virtual float rotation
        {
            get => _rotation;
            set
            {
                _rotation = value;
            }
        }

        /// <summary>
        /// Scale of marker.
        /// </summary>
        public virtual float scale
        {
            get => _scale;
            set => _scale = value;
        }

        /// <summary>
        /// List of tags.
        /// </summary>
        public List<string> tags { get; set; } = new List<string>();

        /// <summary>
        /// Get or set a value in the custom data dictionary by key.
        /// </summary>
        /// <param name="key">Field key.</param>
        /// <returns>Field value.</returns>
        public object this[string key]
        {
            get => customData.GetValueOrDefault(key);
            set => customData[key] = value;
        }
        
        public virtual void DestroyInstance()
        {
        
        }

        /// <summary>
        /// Disposes marker
        /// </summary>
        public virtual void Dispose()
        {
            tags = null;
            customData = null;
            _manager = null;

            OnClick = null;
            OnDoubleClick = null;
            OnDrag = null;
            OnDrawTooltip = null;
            OnEnabledChange = null;
            OnInitComplete = null;
            OnLongPress = null;
            OnPress = null;
            OnRelease = null;
            OnRollOut = null;
            OnRollOver = null;

            DestroyInstance();
        }
    
        /// <summary>
        /// Gets the value from the custom data dictionary by key.
        /// </summary>
        /// <param name="key">Field key.</param>
        /// <typeparam name="T">Type of value.</typeparam>
        /// <returns>Field value.</returns>
        public T GetData<T>(string key)
        {
            object val = customData.GetValueOrDefault(key);
            return val != null? (T)val: default;
        }

        /// <summary>
        /// Get tile position of the marker
        /// </summary>
        /// <returns>Tile position</returns>
        public TilePoint GetTilePosition()
        {
            return location.ToTile(map);
        }

        /// <summary>
        /// Get tile position of the marker
        /// </summary>
        /// <param name="zoom">Zoom</param>
        /// <returns>Tile position</returns>
        public TilePoint GetTilePosition(int zoom)
        {
            return location.ToTile(map, zoom);
        }

        /// <summary>
        /// Checks if the marker is in the current map view
        /// </summary>
        /// <returns>True - in map view. False - outside map view</returns>
        public bool InMapView()
        {
            return map.view.Contains(location);
        }

        /// <summary>
        /// Turns the marker in the direction specified coordinates
        /// </summary>
        /// <param name="location">The coordinates</param>
        /// <param name="rotationOffset">Correction of the rotation (degree)</param>
        public virtual void LookToLocation(GeoPoint location, float rotationOffset = 90)
        {
        
        }

        /// <summary>
        /// Turns the marker in the direction specified coordinates
        /// </summary>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        public void LookToLocation(double longitude, double latitude)
        {
            LookToLocation(new GeoPoint(longitude, latitude));
        }

        private void OnMarkerPress(Marker marker)
        {
            map.control.marker2DManager.dragMarker = this;
        }

        /// <summary>
        /// Makes the marker draggable or un draggable
        /// </summary>
        /// <param name="value">True - set draggable, false - unset draggable</param>
        public void SetDraggable(bool value = true)
        {
            isDraggable = value;
        }

        public virtual JSONItem ToJSON()
        {
            return JSON.Serialize(new 
            {
                location,
                range = new
                {
                    range.min,
                    range.max
                },
                label,
                scale,
                enabled
            });
        }

        /// <summary>
        /// Update of marker instance.
        /// </summary>
        public virtual void Update()
        {
        
        }

        /// <summary>
        /// Method that called when need update marker.
        /// </summary>
        /// <param name="rect">Map rect.</param>
        /// <param name="zoom">Map zoom.</param>
        public virtual void Update(GeoRect rect, int zoom)
        {

        }
    }
}