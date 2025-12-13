/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;

namespace OnlineMaps
{
    /// <summary>
    /// Represents a rectangular area in Mercator projection coordinates.
    /// </summary>
    public struct MercatorRect
    {
        /// <summary>
        /// The left boundary of the rectangle.
        /// </summary>
        public double left;

        /// <summary>
        /// The top boundary of the rectangle.
        /// </summary>
        public double top;

        /// <summary>
        /// The right boundary of the rectangle.
        /// </summary>
        public double right;

        /// <summary>
        /// The bottom boundary of the rectangle.
        /// </summary>
        public double bottom;

        /// <summary>
        /// Gets or sets the bottom-left corner of the rectangle.
        /// </summary>
        public MercatorPoint bottomLeft
        {
            get => new MercatorPoint(left, bottom);
            set
            {
                left = value.x;
                bottom = Math.Clamp(value.y, 0, 1);
            }
        }

        /// <summary>
        /// Gets or sets the bottom-right corner of the rectangle.
        /// </summary>
        public MercatorPoint bottomRight
        {
            get => new MercatorPoint(right, bottom);
            set
            {
                right = value.x;
                bottom = Math.Clamp(value.y, 0, 1);
            }
        }

        /// <summary>
        /// Gets the center point of the rectangle.
        /// </summary>
        public MercatorPoint center => new MercatorPoint((left + right) / 2, (top + bottom) / 2);

        /// <summary>
        /// Gets the height of the rectangle.
        /// </summary>
        public double height => bottom - top;

        /// <summary>
        /// Gets the size of the rectangle as a vector.
        /// </summary>
        public Vector2d size => new Vector2d(width, height);

        /// <summary>
        /// Gets or sets the top-left corner of the rectangle.
        /// </summary>
        public MercatorPoint topLeft
        {
            get => new MercatorPoint(left, top);
            set
            {
                left = value.x;
                top = Math.Clamp(value.y, 0, 1);
            }
        }

        /// <summary>
        /// Gets or sets the top-right corner of the rectangle.
        /// </summary>
        public MercatorPoint topRight
        {
            get => new MercatorPoint(right, top);
            set
            {
                right = value.x;
                top = Math.Clamp(value.y, 0, 1);
            }
        }

        /// <summary>
        /// Gets the width of the rectangle.
        /// </summary>
        public double width => right - left;

        /// <summary>
        /// Initializes a new instance of the MercatorRect struct with the specified boundaries.
        /// </summary>
        /// <param name="left">The left boundary.</param>
        /// <param name="top">The top boundary.</param>
        /// <param name="right">The right boundary.</param>
        /// <param name="bottom">The bottom boundary.</param>
        public MercatorRect(double left, double top, double right, double bottom)
        {
            this.left = left;
            this.top = top;
            this.right = right;
            this.bottom = bottom;
        }

        /// <summary>
        /// Initializes a new instance of the MercatorRect struct with the specified top-left and bottom-right points.
        /// </summary>
        /// <param name="topLeft">The top-left point.</param>
        /// <param name="bottomRight">The bottom-right point.</param>
        public MercatorRect(MercatorPoint topLeft, MercatorPoint bottomRight)
        {
            left = topLeft.x;
            top = topLeft.y;
            right = bottomRight.x;
            bottom = bottomRight.y;
        }

        /// <summary>
        /// Determines whether the specified coordinates in Mercator projection are contained within this rectangle.
        /// </summary>
        /// <param name="x">The x-coordinate to check.</param>
        /// <param name="y">The y-coordinate to check.</param>
        /// <returns>True if the coordinates are inside the rectangle; otherwise, false.</returns>
        public bool Contains(double x, double y)
        {
            return left <= x && right >= x && top <= y && bottom >= y;
        }

        /// <summary>
        /// Determines whether the specified geographic location is contained within this rectangle using the given map.
        /// </summary>
        /// <param name="location">The geographic location to check.</param>
        /// <param name="map">The map used for conversion to Mercator coordinates.</param>
        /// <returns>True if the location is inside the rectangle; otherwise, false.</returns>
        public bool Contains(GeoPoint location, Map map)
        {
            MercatorPoint p = location.ToMercator(map);
            return Contains(p.x, p.y);
        }

        /// <summary>
        /// Determines whether the specified geographic location is contained within this rectangle using the given projection.
        /// </summary>
        /// <param name="location">The geographic location to check.</param>
        /// <param name="projection">The projection used for conversion to Mercator coordinates.</param>
        /// <returns>True if the location is inside the rectangle; otherwise, false.</returns>
        public bool Contains(GeoPoint location, Projection projection)
        {
            MercatorPoint p = location.ToMercator(projection);
            return Contains(p.x, p.y);
        }

        /// <summary>
        /// Determines whether the specified tile point is contained within this rectangle.
        /// </summary>
        /// <param name="point">The tile point to check.</param>
        /// <returns>True if the point is inside the rectangle; otherwise, false.</returns>
        public bool Contains(TilePoint point)
        {
            MercatorPoint p = point.ToMercator();
            return Contains(p.x, p.y);
        }

        /// <summary>
        /// Determines whether the specified vector point is contained within this rectangle.
        /// </summary>
        /// <param name="point">The vector point to check.</param>
        /// <returns>True if the point is inside the rectangle; otherwise, false.</returns>
        public bool Contains(Vector2d point)
        {
            return Contains(point.x, point.y);
        }

        /// <summary>
        /// Determines whether the specified MercatorPoint is contained within this rectangle.
        /// </summary>
        /// <param name="point">The point to check.</param>
        /// <returns>True if the point is inside the rectangle; otherwise, false.</returns>
        public bool Contains(MercatorPoint point)
        {
            return left <= point.x && right >= point.x && top <= point.y && bottom >= point.y;
        }


        /// <summary>
        /// Determines whether the specified MercatorPoint is contained within this rectangle,
        /// taking into account horizontal wrapping (e.g., for world maps that wrap at the 180° meridian).
        /// </summary>
        /// <param name="point">The point to check.</param>
        /// <returns>True if the point is inside the rectangle or its wrapped equivalent; otherwise, false.</returns>
        public bool ContainsWrapped(MercatorPoint point)
        {
            return (point.x > left && point.x < right
                    || point.x + 1 > left && point.x + 1 < right
                    || point.x - 1 > left && point.x - 1 < right)
                   && point.y > top && point.y < bottom;
        }

        /// <summary>
        /// Expands the rectangle to include the specified Mercator coordinates.
        /// </summary>
        /// <param name="x">The x-coordinate in Mercator projection.</param>
        /// <param name="y">The y-coordinate in Mercator projection.</param>
        public void Encapsulate(double x, double y)
        {
            if (x < left) left = x;
            if (x > right) right = x;
            if (y < top) top = y;
            if (y > bottom) bottom = y;
        }

        /// <summary>
        /// Expands the rectangle to include the specified geographic location using the given map.
        /// </summary>
        /// <param name="location">The geographic location to include.</param>
        /// <param name="map">The map used for conversion to Mercator coordinates.</param>
        public void Encapsulate(GeoPoint location, Map map)
        {
            MercatorPoint p = location.ToMercator(map);
            Encapsulate(p.x, p.y);
        }

        /// <summary>
        /// Expands the rectangle to include the specified geographic location using the given projection.
        /// </summary>
        /// <param name="location">The geographic location to include.</param>
        /// <param name="projection">The projection used for conversion to Mercator coordinates.</param>
        public void Encapsulate(GeoPoint location, Projection projection)
        {
            MercatorPoint p = location.ToMercator(projection);
            Encapsulate(p.x, p.y);
        }

        /// <summary>
        /// Expands the rectangle to include the specified Mercator point.
        /// </summary>
        /// <param name="point">The Mercator point to include.</param>
        public void Encapsulate(MercatorPoint point)
        {
            Encapsulate(point.x, point.y);
        }

        /// <summary>
        /// Expands the rectangle to include the specified tile point.
        /// </summary>
        /// <param name="point">The tile point to include.</param>
        public void Encapsulate(TilePoint point)
        {
            MercatorPoint p = point.ToMercator();
            Encapsulate(p.x, p.y);
        }

        /// <summary>
        /// Expands the rectangle to include the specified vector point.
        /// </summary>
        /// <param name="point">The vector point to include.</param>
        public void Encapsulate(Vector2d point)
        {
            Encapsulate(point.x, point.y);
        }

        /// <summary>
        /// Fixes the rectangle for horizontal wrapping by adjusting the right boundary if it is less than the left boundary.
        /// </summary>
        public void FixWrap()
        {
            if (left > right) right += 1;
        }

        /// <summary>
        /// Determines whether this rectangle intersects with another rectangle.
        /// </summary>
        /// <param name="other">The other rectangle.</param>
        /// <returns><c>true</c> if the rectangles intersect; otherwise, <c>false</c>.</returns>
        public bool Intersects(MercatorRect other)
        {
            return other.left <= right && other.right >= left && other.top <= bottom && other.bottom >= top;
        }
        
        /// <summary>
        /// Determines whether this rectangle intersects with another rectangle,
        /// taking into account horizontal wrapping (e.g., for world maps that wrap at the 180° meridian).
        /// </summary>
        /// <param name="other">The other rectangle to check for intersection.</param>
        /// <returns><c>true</c> if the rectangles intersect (including wrapped cases); otherwise, <c>false</c>.</returns>
        public bool IntersectsWrapped(MercatorRect other)
        {
            if (other.top > bottom || other.bottom < top) return false;
            if (other.left <= right && other.right >= left) return true;
            if (other.left <= right + 1 && other.right >= left + 1) return true;
            return other.left <= right - 1 && other.right >= left - 1;
        }

        /// <summary>
        /// Linearly interpolates between the left and right boundaries and the top and bottom boundaries.
        /// </summary>
        /// <param name="x">The x-coordinate to interpolate.</param>
        /// <param name="y">The y-coordinate to interpolate.</param>
        /// <returns>A MercatorPoint representing the interpolated point.</returns>
        public MercatorPoint Lerp(double x, double y)
        {
            return new MercatorPoint((x - left) / width, (y - top) / height);
        }

        /// <summary>
        /// Linearly interpolates between the left and right boundaries and the top and bottom boundaries.
        /// </summary>
        /// <param name="point">The point to interpolate.</param>
        /// <returns>A MercatorPoint representing the interpolated point.</returns>
        public MercatorPoint Lerp(Vector2d point)
        {
            return Lerp(point.x, point.y);
        }

        /// <summary>
        /// Converts this Mercator rectangle to a geographic rectangle using the specified map.
        /// </summary>
        /// <param name="map">The map used for the conversion.</param>
        /// <returns>A GeoRect representing the geographic rectangle.</returns>
        public GeoRect ToGeoRect(Map map)
        {
            return new GeoRect(topLeft.ToLocation(map), bottomRight.ToLocation(map));
        }

        /// <summary>
        /// Converts this Mercator rectangle to a geographic rectangle using the specified projection.
        /// </summary>
        /// <param name="projection">The projection used for the conversion.</param>
        /// <returns>A GeoRect representing the geographic rectangle.</returns>
        public GeoRect ToGeoRect(Projection projection)
        {
            return new GeoRect(topLeft.ToLocation(projection), bottomRight.ToLocation(projection));
        }

        public override string ToString()
        {
            return string.Format(Culture.numberFormat, 
                "(left: {0}, top: {1}, right: {2}, bottom: {3})", 
                left, top, right, bottom);
        }

        /// <summary>
        /// Converts this Mercator rectangle to a tile rectangle at the specified zoom level.
        /// </summary>
        /// <param name="zoom">The zoom level.</param>
        /// <returns>A TileRect representing the rectangle in tile coordinates.</returns>
        public TileRect ToTileRect(int zoom)
        {
            return new TileRect(topLeft.ToTile(zoom), bottomRight.ToTile(zoom));
        }
    }
}