/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using OnlineMaps;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Class that draws a rectangle on the map.
    /// </summary>
    public class Rectangle : DrawingElement
    {
        private static List<Vector2> activePoints;
        private static List<int> backTriangles;

        private Color _backgroundColor = new Color(1, 1, 1, 0);
        private Color _borderColor = Color.black;
        private float _borderWidth = 1;
        
        private double _height = 1;
        private double _width = 1;
        private double _x;
        private double _y;
        private Texture2D _backgroundTexture;

        internal override bool createBackgroundMaterial => _backgroundTexture || _backgroundColor.a > 0;

        /// <summary>
        /// Center point of the rectangle.
        /// </summary>
        public override GeoPoint center => new(_x + _width / 2, _y + _height / 2);

        /// <summary>
        /// Background color of the rectangle.
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
        /// Background texture of the rectangle. Currently, works only for Tileset. For this to work correctly, also set backgroundColor.
        /// </summary>
        public Texture2D backgroundTexture
        {
            get => _backgroundTexture;
            set
            {
                _backgroundTexture = value;
                if (manager != null) manager.map.Redraw();
            }
        }

        /// <summary>
        /// Border color of the rectangle.
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
        /// Border width of the rectangle.
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

        public override Type bufferDrawerType { get; set; } = typeof(RectangleBufferDrawer);

        protected override string defaultName => "Rect";

        public override Type dynamicMeshDrawerType { get; set; } = typeof(RectangleDynamicMeshDrawer);

        /// <summary>
        /// Gets or sets the width of the rectangle. Geographic coordinates.
        /// </summary>
        public double width
        {
            get => _width;
            set
            {
                _width = value;
                InitPoints();
                if (manager != null) manager.map.needRedraw = true;
            }
        }

        /// <summary>
        /// Gets or sets the height of the rectangle. Geographic coordinates.
        /// </summary>
        public double height
        {
            get => _height;
            set
            {
                _height = value;
                InitPoints();
                if (manager != null) manager.map.needRedraw = true;
            }
        }

        /// <summary>
        /// Gets or sets the x position of the rectangle. Geographic coordinates.
        /// </summary>
        public double x
        {
            get => _x;
            set
            {
                _x = value;
                InitPoints();
                if (manager != null) manager.map.needRedraw = true;
            }
        }

        /// <summary>
        /// Gets or sets the y position of the rectangle. Geographic coordinates.
        /// </summary>
        public double y
        {
            get => _y;
            set
            {
                _y = value;
                InitPoints();
                if (manager != null) manager.map.needRedraw = true;
            }
        }

        /// <summary>
        /// Coordinates of top-left corner.
        /// </summary>
        public GeoPoint topLeft
        {
            get => new GeoPoint(_x, _y);
            set
            {
                GeoPoint br = bottomRight;
                _x = value.x;
                _y = value.y;
                bottomRight = br;
            }
        }

        /// <summary>
        /// Coordinates of top-right corner.
        /// </summary>
        public GeoPoint topRight
        {
            get => new GeoPoint(_x + _width, _y);
            set
            {
                double b = _y + _height;
                _width = value.x - _x;
                _y = value.y;
                _height = b - _y;
                InitPoints();
                if (manager != null) manager.map.needRedraw = true;
            }
        }

        /// <summary>
        /// Coordinates of bottom-left corner.
        /// </summary>
        public GeoPoint bottomLeft
        {
            get => new GeoPoint(_x, _y + _height);
            set
            {
                double r = _x + _width;
                _x = value.x;
                _height = value.y - _y;
                _width = r - _x;
                InitPoints();
                if (manager != null) manager.map.needRedraw = true;
            }
        }

        /// <summary>
        /// Coordinates of bottom-right corner.
        /// </summary>
        public GeoPoint bottomRight
        {
            get => new GeoPoint(_x + _width, _y + _height);
            set
            {
                _width = value.x - _x;
                _height = value.y - _y;
                InitPoints();
                if (manager != null) manager.map.needRedraw = true;
            }
        }

        /// <summary>
        /// Creates a new rectangle.
        /// </summary>
        /// <param name="longitude">Longitude of top-left corner of the rectangle.</param>
        /// <param name="latitude">Latitude of top-left corner of the rectangle.</param>
        /// <param name="width">Width. Geographic coordinates.</param>
        /// <param name="height">Height. Geographic coordinates.</param>
        public Rectangle(double longitude, double latitude, double width, double height)
        {
            _y = 0;
            _x = longitude;
            _y = latitude;
            _width = width;
            _height = height;

            InitPoints();
        }

        /// <summary>
        /// Creates a new rectangle.
        /// </summary>
        /// <param name="location">The position of the rectangle. Geographic coordinates.</param>
        /// <param name="size">The size of the rectangle. Geographic coordinates.</param>
        public Rectangle(GeoPoint location, Vector2d size):this(location.x, location.y, size.x, size.y)
        {
        
        }

        /// <summary>
        /// Creates a new rectangle.
        /// </summary>
        /// <param name="rect">Rectangle. Geographic coordinates.</param>
        public Rectangle(Rect rect): this(rect.x, rect.y, rect.width, rect.height)
        {
        
        }

        /// <summary>
        /// Creates a new rectangle.
        /// </summary>
        /// <param name="x">Position X. Geographic coordinates.</param>
        /// <param name="y">Position Y. Geographic coordinates.</param>
        /// <param name="width">Width. Geographic coordinates.</param>
        /// <param name="height">Height. Geographic coordinates.</param>
        /// <param name="borderColor">Border color.</param>
        public Rectangle(double x, double y, double width, double height, Color borderColor)
            : this(x, y, width, height)
        {
            _borderColor = borderColor;
        }

        /// <summary>
        /// Creates a new rectangle.
        /// </summary>
        /// <param name="position">The location of the rectangle. Geographic coordinates.</param>
        /// <param name="size">The size of the rectangle. Geographic coordinates.</param>
        /// <param name="borderColor">Border color.</param>
        public Rectangle(GeoPoint position, Vector2d size, Color borderColor)
            : this(position.x, position.y, size.x, size.y)
        {
            _borderColor = borderColor;
        }

        /// <summary>
        /// Creates a new rectangle.
        /// </summary>
        /// <param name="rect">Rectangle. Geographic coordinates.</param>
        /// <param name="borderColor">Border color.</param>
        public Rectangle(Rect rect, Color borderColor)
            : this(rect)
        {
            _borderColor = borderColor;
        }

        /// <summary>
        /// Creates a new rectangle.
        /// </summary>
        /// <param name="x">Position X. Geographic coordinates.</param>
        /// <param name="y">Position Y. Geographic coordinates.</param>
        /// <param name="width">Width. Geographic coordinates.</param>
        /// <param name="height">Height. Geographic coordinates.</param>
        /// <param name="borderColor">Border color.</param>
        /// <param name="borderWidth">Border width.</param>
        public Rectangle(double x, double y, double width, double height, Color borderColor, float borderWidth)
            : this(x, y, width, height, borderColor)
        {
            _borderWidth = borderWidth;
        }

        /// <summary>
        /// Creates a new rectangle.
        /// </summary>
        /// <param name="location">The location of the rectangle. Geographic coordinates.</param>
        /// <param name="size">The size of the rectangle. Geographic coordinates.</param>
        /// <param name="borderColor">Border color.</param>
        /// <param name="borderWidth">Border width.</param>
        public Rectangle(GeoPoint location, Vector2d size, Color borderColor, float borderWidth)
            : this(location, size, borderColor)
        {
            _borderWidth = borderWidth;
        }

        /// <summary>
        /// Creates a new rectangle.
        /// </summary>
        /// <param name="rect">Rectangle. Geographic coordinates.</param>
        /// <param name="borderColor">Border color.</param>
        /// <param name="borderWidth">Border width.</param>
        public Rectangle(Rect rect, Color borderColor, float borderWidth)
            : this(rect, borderColor)
        {
            _borderWidth = borderWidth;
        }

        /// <summary>
        /// Creates a new rectangle.
        /// </summary>
        /// <param name="x">Position X. Geographic coordinates.</param>
        /// <param name="y">Position Y. Geographic coordinates.</param>
        /// <param name="width">Width. Geographic coordinates.</param>
        /// <param name="height">Height. Geographic coordinates.</param>
        /// <param name="borderColor">Border color.</param>
        /// <param name="borderWidth">Border width.</param>
        /// <param name="backgroundColor">Background color.</param>
        public Rectangle(double x, double y, double width, double height, Color borderColor, float borderWidth, Color backgroundColor)
            : this(x, y, width, height, borderColor, borderWidth)
        {
            _backgroundColor = backgroundColor;
        }

        /// <summary>
        /// Creates a new rectangle.
        /// </summary>
        /// <param name="location">The location of the rectangle. Geographic coordinates.</param>
        /// <param name="size">The size of the rectangle. Geographic coordinates.</param>
        /// <param name="borderColor">Border color.</param>
        /// <param name="borderWidth">Border width.</param>
        /// <param name="backgroundColor">Background color.</param>
        public Rectangle(GeoPoint location, Vector2d size, Color borderColor, float borderWidth, Color backgroundColor)
            : this(location, size, borderColor, borderWidth)
        {
            _backgroundColor = backgroundColor;
        }

        /// <summary>
        /// Creates a new rectangle.
        /// </summary>
        /// <param name="rect">Rectangle. Geographic coordinates.</param>
        /// <param name="borderColor">Border color.</param>
        /// <param name="borderWidth">Border width.</param>
        /// <param name="backgroundColor">Background color.</param>
        public Rectangle(Rect rect, Color borderColor, float borderWidth, Color backgroundColor)
            : this(rect, borderColor, borderWidth)
        {
            _backgroundColor = backgroundColor;
        }

        public override bool HitTest(GeoPoint location, int zoom)
        {
            if (location.x < x || location.x > x + width) return false;
            if (location.y < y || location.y > y + height) return false;
            return true;
        }

        private void InitPoints()
        {
            SetPoints(new[]
            {
                _x, _y,
                _x + _width, _y,
                _x + _width, _y + _height,
                _x, _y + _height
            });
        }
    }
}