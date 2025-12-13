/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    [Serializable]
    public class MapView
    {
        /// <summary>
        /// Event triggered when the location changes.
        /// </summary>
        public Action OnLocationChanged;

        /// <summary>
        /// Event triggered when the zoom level changes.
        /// </summary>
        public Action OnZoomChanged;

        [SerializeField]
        private int _countTilesX = Constants.DefaultMapSize / Constants.TileSize;

        [SerializeField]
        private int _countTilesY = Constants.DefaultMapSize / Constants.TileSize;

        [SerializeField]
        private double _latitude;

        [SerializeField]
        private double _longitude;

        [SerializeField]
        private float _zoom = 3;
        private int _maxTiles;

        private double _mercatorX;
        private double _mercatorY;
        private double _leftLongitude;
        private double _topLatitude;
        private double _rightLongitude;
        private double _bottomLatitude;

        private LocationRange _locationRange;
        private Projection _projection;
        private LimitedRange _zoomRange;
        private Map map;

        /// <summary>
        /// Gets the bottom-right geographical point of the map view.
        /// </summary>
        public GeoPoint bottomRight => new GeoPoint(_rightLongitude, _bottomLatitude);

        /// <summary>
        /// Gets the bottom-right tile point of the map view.
        /// </summary>
        public TilePoint bottomRightTile => bottomRight.ToTile(_projection, intZoom);

        /// <summary>
        /// Gets or sets the center geographical point of the map view.
        /// </summary>
        public GeoPoint center
        {
            get => new GeoPoint(_longitude, _latitude);
            set
            {
                if (Math.Abs(_longitude - value.x) < double.Epsilon &&
                    Math.Abs(_latitude - value.y) < double.Epsilon) return;

                double x = Mathd.Repeat(value.x, -180, 180);
                double y = Mathd.Clamp(value.y, -90, 90);

                SetCenter(x, y);
            }
        }

        /// <summary>
        /// Gets or sets the center point of the map view in Mercator coordinates.
        /// </summary>
        public MercatorPoint centerMercator
        {
            get => new MercatorPoint(_mercatorX, _mercatorY);
            set
            {
                if (Math.Abs(_mercatorX - value.x) < double.Epsilon &&
                    Math.Abs(_mercatorY - value.y) < double.Epsilon) return;

                SetCenterMercator(value.x, value.y);
            }
        }

        /// <summary>
        /// Gets or sets the center tile point of the map view.
        /// </summary>
        public TilePoint centerTile
        {
            get => GetCenterTile();
            set => SetCenterTile(value);
        }

        /// <summary>
        /// Gets the number of tiles in the map view.
        /// </summary>
        public Vector2Int countTiles => new Vector2Int(_countTilesX, _countTilesY);

        /// <summary>
        /// Gets the number of tiles in the X direction.
        /// </summary>
        public int countTilesX => _countTilesX;

        /// <summary>
        /// Gets the number of tiles in the Y direction.
        /// </summary>
        public int countTilesY => _countTilesY;

        /// <summary>
        /// Gets or sets the zoom level as an integer.
        /// </summary>
        public int intZoom
        {
            get => (int)_zoom;
            set => zoom = value;
        }

        /// <summary>
        /// Indicates whether the map covers the entire world.
        /// </summary>
        public bool isEntireWorld => _countTilesX == _maxTiles;

        /// <summary>
        /// Gets the latitude of the map view.
        /// </summary>
        public double latitude => _latitude;

        /// <summary>
        /// Limits the range of map coordinates.
        /// </summary>
        public LocationRange locationRange
        {
            get => _locationRange;
            set
            {
                _locationRange = value;
                if (value == null) return;

                if (value.CheckAndFix(ref _longitude, ref _latitude))
                {
                    SetCenter(_longitude, _latitude);
                }
            }
        }

        /// <summary>
        /// Gets the longitude of the map view.
        /// </summary>
        public double longitude => _longitude;

        /// <summary>
        /// Gets the maximum number of tiles in current zoom level.
        /// </summary>
        public int maxTiles => _maxTiles;

        /// <summary>
        /// Gets the Mercator rectangle of the map view.
        /// </summary>
        public MercatorRect mercatorRect => GetMercatorRect();

        /// <summary>
        /// Projection of active provider.
        /// </summary>
        public Projection projection
        {
            get
            {
                if (_projection == null) _projection = map.activeType.provider.projection;
                return _projection;
            }
            set => _projection = value;
        }

        /// <summary>
        /// Gets the geographical rectangle of the map view.
        /// </summary>
        public GeoRect rect => new(_leftLongitude, _topLatitude, _rightLongitude, _bottomLatitude);

        /// <summary>
        /// Gets the tile rectangle of the map view.
        /// </summary>
        public TileRect tileRect => GetTileRect();

        /// <summary>
        /// Gets the top-left geographical point of the map view.
        /// </summary>
        public GeoPoint topLeft => new(_leftLongitude, _topLatitude);

        /// <summary>
        /// Gets the top-left tile point of the map view.
        /// </summary>
        public TilePoint topLeftTile => topLeft.ToTile(_projection, intZoom);

        /// <summary>
        /// Gets or sets the zoom level of the map view.
        /// </summary>
        public float zoom
        {
            get => _zoom;
            set
            {
                if (Math.Abs(_zoom - value) < float.Epsilon) return;

                SetZoom(value);
                if (Application.isPlaying) UpdateBounds();
            }
        }

        /// <summary>
        /// Gets the zoom factor of the map view.
        /// </summary>
        public float zoomFactor => Mathf.Pow(2, -zoomFractional);

        /// <summary>
        /// Gets the fractional part of the zoom level.
        /// </summary>
        public float zoomFractional => _zoom - (int)_zoom;


        /// <summary>
        /// Specifies the valid range of map zoom.
        /// </summary>
        public LimitedRange zoomRange
        {
            get => _zoomRange;
            set
            {
                _zoomRange = value;
                if (value != null) _zoom = value.CheckAndFix(_zoom);
            }
        }

        /// <summary>
        /// Checks if the given geographical point is within the map view.
        /// </summary>
        /// <param name="point">The geographical point to check.</param>
        /// <returns>True if the point is within the map view, otherwise false.</returns>
        public bool Contains(GeoPoint point) => Contains(point.x, point.y);

        /// <summary>
        /// Checks if the given geographical point is within the map view.
        /// </summary>
        /// <param name="longitude">The longitude of the geographical point to check.</param>
        /// <param name="latitude">The latitude of the geographical point to check.</param>
        /// <returns>True if the point is within the map view, otherwise false.</returns>
        public bool Contains(double longitude, double latitude)
        {
            if (latitude > _topLatitude || latitude < _bottomLatitude) return false;

            double tlx = _leftLongitude;
            double brx = _rightLongitude;

            if (tlx > brx)
            {
                brx += 360;
                if (longitude < tlx) longitude += 360;
            }

            return tlx <= longitude && brx >= longitude;
        }

        /// <summary>
        /// Gets the center tile point of the map view.
        /// </summary>
        /// <param name="zoom">Optional zoom level. If not specified, the current zoom level is used.</param>
        /// <returns>The center tile point of the map view.</returns>
        public TilePoint GetCenterTile(int? zoom = null)
        {
            int z = zoom != null ? zoom.Value : (int)_zoom;

            double tx, ty;
            Projection.MercatorToTile(_mercatorX, _mercatorY, z, out tx, out ty);
            return new TilePoint(tx, ty, z);
        }

        /// <summary>
        /// Gets the Mercator rectangle of the map view.
        /// </summary>
        /// <returns>The Mercator rectangle of the map view.</returns>
        public MercatorRect GetMercatorRect()
        {
            MercatorPoint tl = topLeft.ToMercator(_projection);
            MercatorPoint br = bottomRight.ToMercator(_projection);
            return new MercatorRect(tl, br);
        }

        /// <summary>
        /// Gets the tile rectangle of the map view.
        /// </summary>
        /// <param name="zoom">Optional zoom level. If not specified, the current zoom level is used.</param>
        /// <returns>The tile rectangle of the map view.</returns>
        public TileRect GetTileRect(int? zoom = null)
        {
            int z = zoom != null ? zoom.Value : (int)_zoom;
            TilePoint tl = topLeft.ToTile(_projection, z);
            TilePoint br = bottomRight.ToTile(_projection, z);
            return new TileRect(tl, br);
        }

        /// <summary>
        /// Initializes the map view with the specified map.
        /// </summary>
        /// <param name="map">The map to initialize the view with.</param>
        public void Init(Map map)
        {
            this.map = map;
            _projection = projection;
            _projection.LocationToMercator(_longitude, _latitude, out _mercatorX, out _mercatorY);
            _maxTiles = 1 << intZoom;
        }

        /// <summary>
        /// Sets the center of the map view to the specified geographical coordinates.
        /// </summary>
        /// <param name="longitude">The longitude of the new center.</param>
        /// <param name="latitude">The latitude of the new center.</param>
        /// <param name="zoom">Optional zoom level. If not specified, the current zoom level is used.</param>
        public void SetCenter(double longitude, double latitude, float? zoom = null)
        {
            if (Application.isPlaying)
            {
                double mx, my;
                _projection.LocationToMercator(longitude, latitude, out mx, out my);
                SetCenterMercator(mx, my, zoom);
            }
            else
            {
                _longitude = Mathd.Repeat(longitude, -180, 180);
                _latitude = Mathd.Clamp(latitude, -90, 90);
                if (zoom.HasValue) _zoom = zoom.Value;
            }
        }

        /// <summary>
        /// Sets the center of the map view to the specified geographical coordinates.
        /// </summary>
        /// <param name="newCenter">The new center geographical point.</param>
        /// <param name="newZoom">Optional new zoom level. If not specified, the current zoom level is used.</param>
        public void SetCenter(GeoPoint newCenter, float? newZoom = null)
        {
            SetCenter(newCenter.x, newCenter.y, newZoom);
        }

        /// <summary>
        /// Sets the center of the map view to the specified Mercator coordinates.
        /// </summary>
        /// <param name="mercatorX">The X coordinate in Mercator projection.</param>
        /// <param name="mercatorY">The Y coordinate in Mercator projection.</param>
        /// <param name="newZoom">Optional new zoom level. If not specified, the current zoom level is used.</param>
        public void SetCenterMercator(double mercatorX, double mercatorY, float? newZoom = null)
        {
            float oldZoom = _zoom;

            double prevX = _mercatorX;
            double prevY = _mercatorY;

            _mercatorX = Mathd.Repeat(mercatorX, 1);
            _mercatorY = Mathd.Clamp01(mercatorY);

            if (newZoom.HasValue) SetZoom(newZoom.Value);
            
            _projection.MercatorToLocation(_mercatorX, _mercatorY, out _longitude, out _latitude);

            if (_locationRange != null && _locationRange.CheckAndFix(ref _longitude, ref _latitude))
            {
                _projection.LocationToMercator(_longitude, _latitude, out _mercatorX, out _mercatorY);
            }

            UpdateBounds();

            if (Math.Abs(prevX * prevX + prevY * prevY - (_mercatorX * _mercatorX + _mercatorY * _mercatorY)) > double.Epsilon)
            {
                OnLocationChanged?.Invoke();
                if (map)
                {
                    map.OnLocationChanged?.Invoke();
                    map.Redraw();
                }
            }

            if (Mathf.Abs(_zoom - oldZoom) > float.Epsilon)
            {
                OnZoomChanged?.Invoke();
                if (map)
                {
                    map.OnZoomChanged?.Invoke();
                    map.Redraw();
                }
            }
        }

        /// <summary>
        /// Sets the center of the map view to the specified Mercator coordinates.
        /// </summary>
        /// <param name="center">The new center Mercator point.</param>
        /// <param name="newZoom">Optional new zoom level. If not specified, the current zoom level is used.</param>
        public void SetCenterMercator(MercatorPoint center, float? newZoom = null)
        {
            SetCenterMercator(center.x, center.y, newZoom);
        }

        /// <summary>
        /// Sets the center of the map view to the specified tile coordinates.
        /// </summary>
        /// <param name="tileX">The X coordinate of the tile.</param>
        /// <param name="tileY">The Y coordinate of the tile.</param>
        /// <param name="tileZoom">The zoom level of the tile.</param>
        public void SetCenterTile(double tileX, double tileY, int tileZoom)
        {
            double mx, my;
            Projection.TileToMercator(tileX, tileY, tileZoom, out mx, out my);

            SetCenterMercator(mx, my, tileZoom);
        }

        /// <summary>
        /// Sets the center of the map view to the specified tile coordinates.
        /// </summary>
        /// <param name="center">The new center tile point.</param>
        public void SetCenterTile(TilePoint center)
        {
            SetCenterTile(center.x, center.y, center.zoom);
        }

        /// <summary>
        /// Sets the size of the map view in tiles based on the specified width and height in pixels.
        /// </summary>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        public void SetSize(int width, int height)
        {
            _countTilesX = width / Constants.TileSize;
            _countTilesY = height / Constants.TileSize;

            if (Application.isPlaying) UpdateBounds();
        }

        private void SetZoom(float value)
        {
            if (_zoomRange != null) value = _zoomRange.CheckAndFix(value);
            int minZoom = Mathf.Max(Constants.MinZoom, (int)value);
            int size = 1 << minZoom;
            if (_countTilesX > size)
            {
                minZoom = (int)Math.Ceiling(Math.Log(_countTilesX, 2));
                size = 1 << minZoom;
            }

            if (_countTilesY > size) minZoom = (int)Math.Ceiling(Math.Log(_countTilesY, 2));

            _zoom = Mathf.Clamp(value, minZoom, Constants.MaxZoomExt);
            _maxTiles = 1 << (int)_zoom;
            map?.Redraw();
        }

        /// <summary>
        /// Updates the bounds of the map view based on the current center and zoom level.
        /// </summary>
        public void UpdateBounds()
        {
            int iZoom = (int)_zoom;
            float factor = zoomFactor;
            int size = 1 << iZoom;

            Projection.MercatorToTile(_mercatorX, _mercatorY, iZoom, out double cx, out double cy);
            double left = Mathd.Repeat(cx - _countTilesX / 2d * factor, size);
            double right = Mathd.Repeat(cx + _countTilesX / 2d * factor, size);
            
            if (Math.Abs(left - right) < double.Epsilon)
            {
                right -= 0.0000000001;
            }

            double top = cy - _countTilesY / 2d * factor;
            double bottom = cy + _countTilesY / 2d * factor;

            bool centerChanged = false;

            if (top < 0)
            {
                top = 0;
                bottom = _countTilesY * factor;
                centerChanged = true;
            }
            else if (bottom > size)
            {
                bottom = size;
                top = size - _countTilesY * factor;
                centerChanged = true;
            }

            double centerY = (top + bottom) / 2;

            if (centerChanged)
            {
                Projection.TileToMercator(cx, centerY, iZoom, out _mercatorX, out _mercatorY);
                _projection.MercatorToLocation(_mercatorX, _mercatorY, out _longitude, out _latitude);
            }

            _projection.TileToLocation(left, top, iZoom, out _leftLongitude, out _topLatitude);
            _projection.TileToLocation(right, bottom, iZoom, out _rightLongitude, out _bottomLatitude);
        }
    }
}