/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Represents a point in Mercator projection.
    /// </summary>
    public struct MercatorPoint
    {
        /// <summary>
        /// The X coordinate.
        /// </summary>
        public double x;

        /// <summary>
        /// The Y coordinate.
        /// </summary>
        public double y;
        
        /// <summary>
        /// Gets the zero point in Mercator projection (0, 0).
        /// </summary>
        public MercatorPoint zero => new MercatorPoint(0, 0);

        /// <summary>
        /// Initializes a new instance of the MercatorPoint struct.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        public MercatorPoint(double x, double y)
        {
            this.x = Mathd.Repeat01(x);
            this.y = Mathd.Clamp01(y);
        }

        /// <summary>
        /// Adds the specified values to the coordinates.
        /// </summary>
        /// <param name="x">The value to add to the X coordinate.</param>
        /// <param name="y">The value to add to the Y coordinate.</param>
        /// <returns>A new MercatorPoint with the updated coordinates.</returns>
        public MercatorPoint Add(double x, double y)
        {
            this.x = Mathd.Repeat01(this.x + x);
            this.y = Mathd.Clamp01(this.y + y);
            return this;
        }
        
        /// <summary>
        /// Calculates the angle between this mercator point and another mercator point.
        /// </summary>
        /// <param name="other">The other mercator point.</param>
        /// <returns>The angle in degrees.</returns>
        public double Angle(TilePoint other)
        {
            return Geometry.Angle2D(x, y, other.x, other.y);
        }
        
        /// <summary>
        /// Converts the specified meters to a Mercator point.
        /// </summary>
        /// <param name="meters">The meters to convert.</param>
        /// <returns>A MercatorPoint representing the converted meters.</returns>
        public static MercatorPoint FromMeters(Vector2d meters)
        {
            return new MercatorPoint(meters.x / Constants.EarthRadiusMeters, meters.y / Constants.EarthRadiusMeters);
        }
        
        /// <summary>
        /// Converts the specified meters to a Mercator point.
        /// </summary>
        /// <param name="x">The X coordinate in meters.</param>
        /// <param name="y">The Y coordinate in meters.</param>
        /// <returns>A MercatorPoint representing the converted meters.</returns>
        public static MercatorPoint FromMeters(double x, double y)
        {
            return new MercatorPoint(x / Constants.EarthRadiusMeters, y / Constants.EarthRadiusMeters);
        }

        /// <summary>
        /// Determines whether two rectangles intersect.
        /// </summary>
        /// <param name="topLeft1">The top-left corner of the first rectangle.</param>
        /// <param name="bottomRight1">The bottom-right corner of the first rectangle.</param>
        /// <param name="topLeft2">The top-left corner of the second rectangle.</param>
        /// <param name="bottomRight2">The bottom-right corner of the second rectangle.</param>
        /// <returns><c>true</c> if the rectangles intersect; otherwise, <c>false</c>.</returns>
        public static bool Intersects(MercatorPoint topLeft1, MercatorPoint bottomRight1, MercatorPoint topLeft2, MercatorPoint bottomRight2)
        {
            return !(topLeft1.x > bottomRight2.x || bottomRight1.x < topLeft2.x || topLeft1.y < bottomRight2.y || bottomRight1.y > topLeft2.y);
        }
        
        /// <summary>
        /// Linearly interpolates between two Mercator points using a Vector2d.
        /// </summary>
        /// <param name="t1">The first Mercator point.</param>
        /// <param name="t2">The second Mercator point.</param>
        /// <param name="d">The interpolation factor.</param>
        /// <returns>A new interpolated MercatorPoint point.</returns>
        public static MercatorPoint Lerp(MercatorPoint t1, MercatorPoint t2, Vector2d d)
        {
            return new MercatorPoint(t1.x + (t2.x - t1.x) * d.x, t1.y + (t2.y - t1.y) * d.y);
        }
        
        /// <summary>
        /// Linearly interpolates between two Mercator points using a single interpolation factor.
        /// </summary>
        /// <param name="t1">The first Mercator point.</param>
        /// <param name="t2">The second Mercator point.</param>
        /// <param name="d">The interpolation factor (0.0 to 1.0).</param>
        /// <returns>A new interpolated MercatorPoint.</returns>
        public static MercatorPoint Lerp(MercatorPoint t1, MercatorPoint t2, double d)
        {
            return new MercatorPoint(t1.x + (t2.x - t1.x) * d, t1.y + (t2.y - t1.y) * d);
        }

        /// <summary>
        /// Linearly interpolates between two Mercator points using separate x and y factors.
        /// </summary>
        /// <param name="t1">The first Mercator point.</param>
        /// <param name="t2">The second Mercator point.</param>
        /// <param name="dx">The interpolation factor for the x-coordinate.</param>
        /// <param name="dy">The interpolation factor for the y-coordinate.</param>
        /// <returns>A new interpolated Mercator point.</returns>
        public static MercatorPoint Lerp(MercatorPoint t1, MercatorPoint t2, double dx, double dy)
        {
            return new MercatorPoint(t1.x + (t2.x - t1.x) * dx, t1.y + (t2.y - t1.y) * dy);
        }

        /// <summary>
        /// Subtracts the specified values from the coordinates.
        /// </summary>
        /// <param name="x">The value to subtract from the X coordinate.</param>
        /// <param name="y">The value to subtract from the Y coordinate.</param>
        /// <returns>A new MercatorPoint with the updated coordinates.</returns>
        public MercatorPoint Subtract(double x, double y)
        {
            this.x = Mathd.Repeat01(this.x - x);
            this.y = Mathd.Clamp01(this.y - y);
            return this;
        }

        /// <summary>
        /// Converts the Mercator point to geographic coordinates.
        /// </summary>
        /// <param name="map">The map to use for the conversion.</param>
        /// <returns>A GeoPoint representing the geographic coordinates.</returns>
        public GeoPoint ToLocation(Map map)
        {
            return map.view.projection.MercatorToLocation(x, y);
        }
        
        /// <summary>
        /// Converts the Mercator point to geographic coordinates using the specified projection.
        /// </summary>
        /// <param name="projection">The projection to use for the conversion.</param>
        /// <returns>A GeoPoint representing the geographic coordinates.</returns>
        public GeoPoint ToLocation(Projection projection)
        {
            return projection.MercatorToLocation(x, y);
        }

        /// <summary>
        /// Converts the Mercator point to meters.
        /// </summary>
        /// <returns>A meters in Web Mercator projection.</returns>
        public Vector2d ToMeters()
        {
            return new Vector2d(x * Constants.EarthRadiusMeters, y * Constants.EarthRadiusMeters);
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"x: {x.ToString(Culture.numberFormat)}, y: {y.ToString(Culture.numberFormat)}";
        }

        /// <summary>
        /// Converts the Mercator point to tile coordinates.
        /// </summary>
        /// <param name="zoom">The zoom level.</param>
        /// <returns>A TilePoint representing the tile coordinates.</returns>
        public TilePoint ToTile(int zoom)
        {
            return Projection.MercatorToTile(x, y, zoom);
        }

        /// <summary>
        /// Adds two Mercator points.
        /// </summary>
        /// <param name="a">The first Mercator point.</param>
        /// <param name="b">The second Mercator point.</param>
        /// <returns>A Vector2d representing the sum of the two points.</returns>
        public static Vector2d operator +(MercatorPoint a, MercatorPoint b)
        {
            return new Vector2d(a.x + b.x, a.y + b.y);
        }

        /// <summary>
        /// Subtracts one Mercator point from another.
        /// </summary>
        /// <param name="a">The first Mercator point.</param>
        /// <param name="b">The second Mercator point.</param>
        /// <returns>A Vector2d representing the difference between the two points.</returns>
        public static Vector2d operator -(MercatorPoint a, MercatorPoint b)
        {
            double dx = a.x - b.x;
            dx = (dx + 0.5) % 1.0 - 0.5;
            return new Vector2d(dx, a.y - b.y);
        }
        
        /// <summary>
        /// Divides the coordinates of the specified MercatorPoint by a scalar value.
        /// </summary>
        /// <param name="a">The MercatorPoint to divide.</param>
        /// <param name="b">The scalar value to divide by.</param>
        /// <returns>A new MercatorPoint with divided coordinates.</returns>
        public static MercatorPoint operator / (MercatorPoint a, double b)
        {
            if (b == 0) return new MercatorPoint();
            return new MercatorPoint(a.x / b, a.y / b);
        }
        
        /// <summary>
        /// Explicitly converts a MercatorPoint to a Vector2d.
        /// </summary>
        /// <param name="point">The MercatorPoint to convert.</param>
        /// <returns>A Vector2d representing the converted MercatorPoint.</returns>
        public static explicit operator Vector2d(MercatorPoint point)
        {
            return new Vector2d(point.x, point.y);
        }

        /// <summary>
        /// Implicitly converts a Vector2d to a MercatorPoint.
        /// </summary>
        /// <param name="point">The Vector2d to convert.</param>
        /// <returns>A MercatorPoint representing the converted Vector2d.</returns>
        public static implicit operator MercatorPoint(Vector2d point)
        {
            return new MercatorPoint(point.x, point.y);
        }

        /// <summary>
        /// Converts a MercatorPoint to a Vector2.
        /// </summary>
        /// <param name="point">The MercatorPoint to convert.</param>
        /// <returns>A Vector2 representing the converted MercatorPoint.</returns>
        public static implicit operator Vector2(MercatorPoint point)
        {
            return new Vector2((float)point.x, (float)point.y);
        }
        
        /// <summary>
        /// Implicitly converts a Vector2 to a MercatorPoint.
        /// </summary>
        /// <param name="point">The Vector2 to convert.</param>
        /// <returns>A MercatorPoint representing the converted Vector2.</returns>
        public static implicit operator MercatorPoint(Vector2 point)
        {
            return new MercatorPoint(point.x, point.y);
        }
    }
}