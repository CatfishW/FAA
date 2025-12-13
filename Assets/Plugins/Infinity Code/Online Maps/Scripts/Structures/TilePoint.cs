/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Represents a point on a tile map with x, y coordinates and zoom level.
    /// </summary>
    [Serializable]
    public struct TilePoint
    {
        /// <summary>
        /// The x-coordinate of the tile point.
        /// </summary>
        public double x;

        /// <summary>
        /// The y-coordinate of the tile point.
        /// </summary>
        public double y;

        /// <summary>
        /// The zoom level of the tile point.
        /// </summary>
        public int zoom;

        /// <summary>
        /// Gets a TilePoint with zero coordinates and zoom level.
        /// </summary>
        public static TilePoint zero => new(0, 0, 0);

        /// <summary>
        /// Gets the fractional part of the tile point coordinates.
        /// </summary>
        public Vector2d fractionPart => new(x - (int)x, y - (int)y);

        /// <summary>
        /// Gets a value indicating whether the tile point is at the origin with zero zoom level.
        /// </summary>
        public bool isZero => x == 0 && y == 0 && zoom == 0;

        /// <summary>
        /// Gets the magnitude of the tile point.
        /// </summary>
        public double magnitude => Math.Sqrt(x * x + y * y);
        
        /// <summary>
        /// Gets the total number of tiles at the current zoom level.
        /// </summary>
        public int maxTiles => 1 << zoom;

        /// <summary>
        /// Gets the parent tile point with half the coordinates and one less zoom level.
        /// </summary>
        public TilePoint parent => new(x / 2, y / 2, zoom - 1);

        /// <summary>
        /// Gets the squared magnitude of the tile point.
        /// </summary>
        public double sqrMagnitude => x * x + y * y;

        /// <summary>
        /// Initializes a new instance of the TilePoint struct with the specified coordinates and zoom level.
        /// </summary>
        /// <param name="x">The x-coordinate.</param>
        /// <param name="y">The y-coordinate.</param>
        /// <param name="zoom">The zoom level.</param>
        public TilePoint(double x, double y, int zoom)
        {
            this.x = x;
            this.y = y;
            this.zoom = zoom;
        }

        /// <summary>
        /// Adds the specified values to the tile point coordinates.
        /// </summary>
        /// <param name="x">The value to add to the x-coordinate.</param>
        /// <param name="y">The value to add to the y-coordinate.</param>
        /// <returns>A new TilePoint with the updated coordinates.</returns>
        public TilePoint Add(double x, double y)
        {
            this.x += x;
            this.y += y;
            return this;
        }

        /// <summary>
        /// Calculates the angle between this tile point and another tile point.
        /// </summary>
        /// <param name="other">The other tile point.</param>
        /// <returns>The angle in degrees.</returns>
        public double Angle(TilePoint other)
        {
            return Geometry.Angle2D(x, y, other.x, other.y);
        }

        /// <summary>
        /// Linearly interpolates between two tile points using a Vector2d.
        /// </summary>
        /// <param name="t1">The first tile point.</param>
        /// <param name="t2">The second tile point.</param>
        /// <param name="d">The interpolation factor.</param>
        /// <returns>A new interpolated tile point.</returns>
        public static TilePoint Lerp(TilePoint t1, TilePoint t2, Vector2d d)
        {
            return new TilePoint(t1.x + (t2.x - t1.x) * d.x, t1.y + (t2.y - t1.y) * d.y, t1.zoom);
        }
        
        /// <summary>
        /// Linearly interpolates between two tile points using a single interpolation factor.
        /// </summary>
        /// <param name="t1">The first tile point.</param>
        /// <param name="t2">The second tile point.</param>
        /// <param name="d">The interpolation factor (0 returns t1, 1 returns t2).</param>
        /// <returns>A new interpolated tile point.</returns>
        public static TilePoint Lerp(TilePoint t1, TilePoint t2, double d)
        {
            return new TilePoint(t1.x + (t2.x - t1.x) * d, t1.y + (t2.y - t1.y) * d, t1.zoom);
        }

        /// <summary>
        /// Linearly interpolates between two tile points using separate x and y factors.
        /// </summary>
        /// <param name="t1">The first tile point.</param>
        /// <param name="t2">The second tile point.</param>
        /// <param name="dx">The interpolation factor for the x-coordinate.</param>
        /// <param name="dy">The interpolation factor for the y-coordinate.</param>
        /// <returns>A new interpolated tile point.</returns>
        public static TilePoint Lerp(TilePoint t1, TilePoint t2, double dx, double dy)
        {
            return new TilePoint(t1.x + (t2.x - t1.x) * dx, t1.y + (t2.y - t1.y) * dy, t1.zoom);
        }

        /// <summary>
        /// Subtracts the specified values from the tile point coordinates.
        /// </summary>
        /// <param name="x">The value to subtract from the x-coordinate.</param>
        /// <param name="y">The value to subtract from the y-coordinate.</param>
        /// <returns>A new TilePoint with the updated coordinates.</returns>
        public TilePoint Subtract(double x, double y)
        {
            this.x -= x;
            this.y -= y;
            return this;
        }

        /// <summary>
        /// Converts the tile point to a GeoPoint using the specified map and optional zoom level.
        /// </summary>
        /// <param name="map">The map to use for the conversion.</param>
        /// <returns>The corresponding GeoPoint.</returns>
        public GeoPoint ToLocation(Map map)
        {
            return ToLocation(map.view.projection);
        }
        
        /// <summary>
        /// Converts the tile point to a GeoPoint using the specified projection and zoom level.
        /// </summary>
        /// <param name="projection">The projection to use for the conversion.</param>
        /// <returns>The corresponding GeoPoint.</returns>
        public GeoPoint ToLocation(Projection projection)
        {
            projection.TileToLocation(x, y, zoom, out double lng, out double lat);
            return new GeoPoint(lng, lat);
        }

        /// <summary>
        /// Converts the tile point to a MercatorPoint.
        /// </summary>
        /// <returns>A MercatorPoint representing the tile point.</returns>
        public MercatorPoint ToMercator()
        {
            return Projection.TileToMercator(this);
        }
        
        /// <summary>
        /// Converts the Tile point to meters.
        /// </summary>
        /// <returns>A meters in Web Mercator projection.</returns>
        public Vector2d ToMeters()
        {
            return new Vector2d(x / maxTiles * Constants.EarthRadiusMeters, y / maxTiles * Constants.EarthRadiusMeters);
        }

        /// <summary>
        /// Returns a string representation of the tile point.
        /// </summary>
        /// <returns>A string representation of the tile point.</returns>
        public override string ToString()
        {
            return $"(x: {x.ToString(Culture.numberFormat)}, y: {y.ToString(Culture.numberFormat)}, zoom: {zoom})";
        }

        /// <summary>
        /// Converts the tile point to a different zoom level.
        /// </summary>
        /// <param name="zoom">The target zoom level.</param>
        /// <returns>A new tile point with the specified zoom level.</returns>
        public TilePoint ToZoom(int zoom)
        {
            int offset = this.zoom - zoom;
            if (offset == 0) return this;

            int scale = 1 << Math.Abs(offset);
            if (offset > 0) return new TilePoint(x / scale, y / scale, zoom);
            return new TilePoint(x * scale, y * scale, zoom);
        }

        /// <summary>
        /// Explicitly converts a TilePoint to a MercatorPoint.
        /// </summary>
        /// <param name="p">The TilePoint to convert.</param>
        public static explicit operator MercatorPoint(TilePoint p)
        {
            return new MercatorPoint(p.x, p.y);
        }

        /// <summary>
        /// Implicitly converts a MercatorPoint to a TilePoint.
        /// </summary>
        /// <param name="p">The MercatorPoint to convert.</param>
        public static implicit operator TilePoint(MercatorPoint p)
        {
            return new TilePoint(p.x, p.y, 0);
        }

        /// <summary>
        /// Divides a TilePoint by a scalar value.
        /// </summary>
        /// <param name="a">The TilePoint.</param>
        /// <param name="d">The scalar value.</param>
        /// <returns>A new TilePoint that is the quotient of the TilePoint and the scalar.</returns>
        public static TilePoint operator /(TilePoint a, double d)
        {
            return new TilePoint(a.x / d, a.y / d, a.zoom);
        }

        /// <summary>
        /// Multiplies a TilePoint by a scalar value.
        /// </summary>
        /// <param name="a">The TilePoint.</param>
        /// <param name="d">The scalar value.</param>
        /// <returns>A new TilePoint that is the product of the TilePoint and the scalar.</returns>
        public static TilePoint operator *(TilePoint a, double d)
        {
            return new TilePoint(a.x * d, a.y * d, a.zoom);
        }

        /// <summary>
        /// Adds two TilePoint instances.
        /// </summary>
        /// <param name="a">The first TilePoint.</param>
        /// <param name="b">The second TilePoint.</param>
        /// <returns>A new TilePoint that is the sum of the two TilePoint instances.</returns>
        public static TilePoint operator +(TilePoint a, TilePoint b)
        {
            if (a.zoom != b.zoom) b = b.ToZoom(a.zoom);
            return new TilePoint(a.x + b.x, a.y + b.y, a.zoom);
        }

        /// <summary>
        /// Adds a Vector2 to a TilePoint.
        /// </summary>
        /// <param name="a">The TilePoint.</param>
        /// <param name="b">The Vector2.</param>
        /// <returns>A new TilePoint that is the sum of the TilePoint and the Vector2.</returns>
        public static TilePoint operator +(TilePoint a, Vector2 b)
        {
            return new TilePoint(a.x + b.x, a.y + b.y, a.zoom);
        }

        /// <summary>
        /// Adds a Vector2d to a TilePoint.
        /// </summary>
        /// <param name="a">The TilePoint.</param>
        /// <param name="b">The Vector2d.</param>
        /// <returns>A new TilePoint that is the sum of the TilePoint and the Vector2d.</returns>
        public static TilePoint operator +(TilePoint a, Vector2d b)
        {
            return new TilePoint(a.x + b.x, a.y + b.y, a.zoom);
        }

        /// <summary>
        /// Adds a Vector2Int to a TilePoint.
        /// </summary>
        /// <param name="a">The TilePoint.</param>
        /// <param name="b">The Vector2Int.</param>
        /// <returns>A new TilePoint that is the sum of the TilePoint and the Vector2Int.</returns>
        public static TilePoint operator +(TilePoint a, Vector2Int b)
        {
            return new TilePoint(a.x + b.x, a.y + b.y, a.zoom);
        }

        /// <summary>
        /// Subtracts one TilePoint from another.
        /// </summary>
        /// <param name="a">The first TilePoint.</param>
        /// <param name="b">The second TilePoint.</param>
        /// <returns>A new TilePoint that is the difference between the two TilePoint instances.</returns>
        public static TilePoint operator -(TilePoint a, TilePoint b)
        {
            if (a.zoom != b.zoom) b = b.ToZoom(a.zoom);
            return new TilePoint(a.x - b.x, a.y - b.y, a.zoom);
        }

        /// <summary>
        /// Subtracts a Vector2 from a TilePoint.
        /// </summary>
        /// <param name="a">The TilePoint.</param>
        /// <param name="b">The Vector2.</param>
        /// <returns>A new TilePoint that is the difference between the TilePoint and the Vector2.</returns>
        public static TilePoint operator -(TilePoint a, Vector2 b)
        {
            return new TilePoint(a.x - b.x, a.y - b.y, a.zoom);
        }

        /// <summary>
        /// Subtracts a Vector2d from a TilePoint.
        /// </summary>
        /// <param name="a">The TilePoint.</param>
        /// <param name="b">The Vector2d.</param>
        /// <returns>A new TilePoint that is the difference between the TilePoint and the Vector2d.</returns>
        public static TilePoint operator -(TilePoint a, Vector2d b)
        {
            return new TilePoint(a.x - b.x, a.y - b.y, a.zoom);
        }

        /// <summary>
        /// Subtracts a Vector2Int from a TilePoint.
        /// </summary>
        /// <param name="a">The TilePoint.</param>
        /// <param name="b">The Vector2Int.</param>
        /// <returns>A new TilePoint that is the difference between the TilePoint and the Vector2Int.</returns>
        public static TilePoint operator -(TilePoint a, Vector2Int b)
        {
            return new TilePoint(a.x - b.x, a.y - b.y, a.zoom);
        }

        /// <summary>
        /// Implicitly converts a TilePoint to a Vector2d.
        /// </summary>
        /// <param name="p">The TilePoint to convert.</param>
        public static implicit operator Vector2d(TilePoint p)
        {
            return new Vector2d(p.x, p.y);
        }

        /// <summary>
        /// Implicitly converts a TilePoint to a Vector2Int.
        /// </summary>
        /// <param name="p">The TilePoint to convert.</param>
        public static implicit operator Vector2Int(TilePoint p)
        {
            Vector2Int v = new Vector2Int((int)p.x, (int)p.y);
            if (v.x == p.maxTiles) v.x = 0;
            if (v.y == p.maxTiles) v.y = 0;
            return v;
        }
    }
}