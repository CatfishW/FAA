/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections;
using System.Collections.Generic;
using OnlineMaps;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Class draws a closed polygon on the map.
    /// </summary>
    public class Polygon : DrawingElement
    {
        private static float[] internalPoints;
        private static List<int> internalIndices;
        private static int countInternalPoints;
        private static List<int> fillTriangles;

        private Color _backgroundColor = new Color(1, 1, 1, 0);
        private Color _borderColor = Color.black;
        private float _borderWidth = 1;


        /// <summary>
        /// Background color of the polygon.
        /// Note: Not supported in tileset.
        /// </summary>
        public Color backgroundColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor = value;
                if (manager != null) manager.map.Redraw();
            }
        }

        /// <summary>
        /// Border color of the polygon.
        /// </summary>
        public Color borderColor
        {
            get => _borderColor;
            set
            {
                _borderColor = value;
                if (manager != null) manager.map.Redraw();
            }
        }

        /// <summary>
        /// Border width of the polygon.
        /// </summary>
        public float borderWidth
        {
            get => _borderWidth;
            set
            {
                _borderWidth = value;
                if (manager != null) manager.map.Redraw();
            }
        }

        public override Type bufferDrawerType { get; set; } = typeof(PolygonBufferDrawer);

        protected override string defaultName => "Poly";

        public override Type dynamicMeshDrawerType { get; set; } = typeof(PolygonDynamicMeshDrawer);

        /// <summary>
        /// IEnumerable of points of the polygon. Geographic coordinates.
        /// The values can be of type: Vector2, Vector2d, GeoPoint, TilePoint, MercatorPoint, float, double.
        /// If values float or double, the value should go in pairs(longitude, latitude).
        /// </summary>
        public GeoPoint[] points
        {
            get => _points;
            set
            {
                if (value == null) throw new Exception("Points can not be null.");
                SetPoints(value);
            }
        }

        internal override bool createBackgroundMaterial => _backgroundColor != default;

        /// <summary>
        /// Center point of the polygon.
        /// </summary>
        public override GeoPoint center
        {
            get
            {
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                
                foreach (GeoPoint point in points)
                {
                    if (point.x < minX) minX = point.x;
                    if (point.x > maxX) maxX = point.x;
                    if (point.y < minY) minY = point.y;
                    if (point.y > maxY) maxY = point.y;
                }
                
                return new GeoPoint((minX + maxX) / 2, (minY + maxY) / 2);
            }
        }

        /// <summary>
        /// Creates a new polygon.
        /// </summary>
        public Polygon()
        {
            _points = Array.Empty<GeoPoint>();
        }

        /// <summary>
        /// Creates a new polygon.
        /// </summary>
        /// <param name="points">
        /// IEnumerable of points of the polygon. Geographic coordinates.
        /// The values can be of type: Vector2, Vector2d, GeoPoint, TilePoint, MercatorPoint, float, double.
        /// If values float or double, the value should go in pairs(longitude, latitude).
        /// </param>
        public Polygon(IEnumerable points) : this()
        {
            if (points == null) throw new Exception("Points can not be null.");
            SetPoints(points);
        }

        /// <summary>
        /// Creates a new polygon.
        /// </summary>
        /// <param name="points">
        /// IEnumerable of points of the polygon. Geographic coordinates.
        /// The values can be of type: Vector2, Vector2d, GeoPoint, TilePoint, MercatorPoint, float, double.
        /// If values float or double, the value should go in pairs(longitude, latitude).
        /// </param>
        /// <param name="borderColor">Border color of the polygon.</param>
        public Polygon(IEnumerable points, Color borderColor)
            : this(points)
        {
            _borderColor = borderColor;
        }

        /// <summary>
        /// Creates a new polygon.
        /// </summary>
        /// <param name="points">
        /// IEnumerable of points of the polygon. Geographic coordinates.
        /// The values can be of type: Vector2, Vector2d, GeoPoint, TilePoint, MercatorPoint, float, double.
        /// If values float or double, the value should go in pairs(longitude, latitude).
        /// </param>
        /// <param name="borderColor">Border color of the polygon.</param>
        /// <param name="borderWidth">Border width of the polygon.</param>
        public Polygon(IEnumerable points, Color borderColor, float borderWidth)
            : this(points, borderColor)
        {
            _borderWidth = borderWidth;
        }

        /// <summary>
        /// Creates a new polygon.
        /// </summary>
        /// <param name="points">
        /// IEnumerable of points of the polygon. Geographic coordinates.
        /// The values can be of type: Vector2, Vector2d, GeoPoint, TilePoint, MercatorPoint, float, double.
        /// If values float or double, the value should go in pairs(longitude, latitude).
        /// </param>
        /// <param name="borderColor">Border color of the polygon.</param>
        /// <param name="borderWidth">Border width of the polygon.</param>
        /// <param name="backgroundColor">
        /// Background color of the polygon.
        /// Note: Not supported in tileset.
        /// </param>
        public Polygon(IEnumerable points, Color borderColor, float borderWidth, Color backgroundColor)
            : this(points, borderColor, borderWidth)
        {
            _backgroundColor = backgroundColor;
        }

        protected override void DisposeLate()
        {
            base.DisposeLate();

            _points = null;
        }

        public override bool HitTest(GeoPoint location, int zoom)
        {
            if (points == null) return false;
            return Geometry.IsPointInPolygon(mercatorPoints, location.ToMercator(manager.map));
        }
    }
}