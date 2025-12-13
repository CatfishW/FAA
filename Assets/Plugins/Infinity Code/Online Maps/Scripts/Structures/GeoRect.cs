/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    /// <summary>
    /// The class contains the coordinates or positions of the area boundaries.
    /// </summary>
    public struct GeoRect
    {
        /// <summary>
        /// Left longitude or position
        /// </summary>
        public double left;

        /// <summary>
        /// Right longitude or position
        /// </summary>
        public double right;

        /// <summary>
        /// Top latitude or position
        /// </summary>
        public double top;

        /// <summary>
        /// Bottom latitude or position
        /// </summary>
        public double bottom;

        /// <summary>
        /// Bottom left point
        /// </summary>
        public GeoPoint bottomLeft => new GeoPoint(left, bottom);

        /// <summary>
        /// Bottom right point
        /// </summary>
        public GeoPoint bottomRight => new GeoPoint(right, bottom);

        /// <summary>
        /// Center point
        /// </summary>
        public GeoPoint center => new GeoPoint((left + right) / 2, (top + bottom) / 2);

        /// <summary>
        /// Height of the area
        /// </summary>
        public double height => bottom - top;

        /// <summary>
        /// Fixed rectangle. This property ensures that the right boundary is always greater than the left boundary.
        /// </summary>
        public GeoRect rightFixed
        {
            get
            {
                FixRight();
                return this;
            }
        }

        /// <summary>
        /// Area size
        /// </summary>
        public Vector2d size => new Vector2d(width, height);

        /// <summary>
        /// Top left point
        /// </summary>
        public GeoPoint topLeft => new GeoPoint(left, top);

        /// <summary>
        /// Top right point
        /// </summary>
        public GeoPoint topRight => new GeoPoint(right, top);

        /// <summary>
        /// Width of the area
        /// </summary>
        public double width => right - left;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="left">Left longitude</param>
        /// <param name="top">Top latitude</param>
        /// <param name="right">Right longitude</param>
        /// <param name="bottom">Bottom latitude</param>
        public GeoRect(double left, double top, double right, double bottom)
        {
            this.left = left;
            this.top = top;
            this.right = right;
            this.bottom = bottom;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="center">Center point</param>
        public GeoRect(GeoPoint center) : this(center.x, center.y, center.x, center.y)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="topLeft">Top left point</param>
        /// <param name="bottomRight">Bottom right point</param>
        public GeoRect(GeoPoint topLeft, GeoPoint bottomRight) : this(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y)
        {
        }

        /// <summary>
        /// Checks if the specified point is within the boundaries of the GeoRect.
        /// </summary>
        /// <param name="point">The point to check.</param>
        /// <returns>True if the point is within the boundaries; otherwise, false.</returns>
        public bool Contains(GeoPoint point) => point.x >= left && point.x <= right && point.y <= top && point.y >= bottom;

        /// <summary>
        /// Checks if the specified point is within the boundaries of the GeoRect, considering wrapping around the 180th meridian.
        /// </summary>
        /// <param name="point">The point to check.</param>
        /// <returns>True if the point is within the boundaries; otherwise, false.</returns>
        public bool ContainsWrapped(GeoPoint point)
        {
            return (point.x > left && point.x < right
                    || point.x + 360 > left && point.x + 360 < right
                    || point.x - 360 > left && point.x - 360 < right)
                   && point.y < top && point.y > bottom;
        }

        /// <summary>
        /// Calculates the distances between the top left and bottom right points of the GeoRect.
        /// </summary>
        /// <returns>A Vector2d representing the distances between the top left and top right points.</returns>
        public Vector2d Distances() => GeoMath.Distances(topLeft, bottomRight);

        /// <summary>
        /// Grows the Rect to include the point.
        /// </summary>
        /// <param name="point">The point to include.</param>
        public void Encapsulate(GeoPoint point)
        {
            double rx = point.x - left;
            if (rx > 180) point.x -= 360;
            else if (rx < -180) point.x += 360;

            if (point.x < left) left = point.x;
            if (point.x > right) right = point.x;
            if (point.y > top) top = point.y;
            if (point.y < bottom) bottom = point.y;
        }

        /// <summary>
        /// Checks if the specified object is equal to this GeoRect.
        /// </summary>
        /// <param name="obj">The object to compare with.</param>
        /// <returns>True if the object is equal to this GeoRect; otherwise, false.</returns>
        public override bool Equals(object obj) => obj is GeoRect other && Equals(other);

        /// <summary>
        /// Checks if the specified GeoRect is equal to this GeoRect.
        /// </summary>
        /// <param name="other">The GeoRect to compare with.</param>
        /// <returns>True if the GeoRects are equal; otherwise, false.</returns>
        public bool Equals(GeoRect other)
        {
            return left.Equals(other.left) &&
                   right.Equals(other.right) &&
                   top.Equals(other.top) &&
                   bottom.Equals(other.bottom);
        }

        /// <summary>
        /// Returns a hash code for this GeoRect.
        /// </summary>
        /// <returns>A hash code for this GeoRect.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = left.GetHashCode();
                hashCode = (hashCode * 397) ^ right.GetHashCode();
                hashCode = (hashCode * 397) ^ top.GetHashCode();
                hashCode = (hashCode * 397) ^ bottom.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Adjusts the right boundary to ensure it is greater than the left boundary.
        /// </summary>
        /// <returns>True if the right boundary was adjusted; otherwise, false.</returns>
        public bool FixRight()
        {
            if (right >= left) return false;
            
            right += 360;
            return true;

        }

        /// <summary>
        /// Checks if the specified GeoRect intersects with this GeoRect.
        /// </summary>
        /// <param name="rect">The GeoRect to check for intersection.</param>
        /// <returns>True if the GeoRects intersect; otherwise, false.</returns>
        public bool Intersects(GeoRect rect)
        {
            return !(rect.left > right || rect.right < left || rect.top < bottom || rect.bottom > top);
        }

        /// <summary>
        /// Linearly interpolates between the left and right boundaries and the top and bottom boundaries.
        /// </summary>
        /// <param name="x">The x-coordinate to interpolate.</param>
        /// <param name="y">The y-coordinate to interpolate.</param>
        /// <returns>A GeoPoint representing the interpolated point.</returns>
        public GeoPoint Lerp(double x, double y) => new((x - left) / width, (y - top) / height);

        /// <summary>
        /// Linearly interpolates between the left and right boundaries and the top and bottom boundaries.
        /// </summary>
        /// <param name="point">The point to interpolate.</param>
        /// <returns>A GeoPoint representing the interpolated point.</returns>
        public GeoPoint Lerp(Vector2d point) => Lerp(point.x, point.y);

        /// <summary>
        /// Converts the GeoRect to a MercatorRect.
        /// </summary>
        /// <param name="map">The map to use for the conversion.</param>
        /// <returns>A MercatorRect representing the converted rectangle.</returns>
        public MercatorRect ToMercatorRect(Map map)
        {
            MercatorPoint tl = topLeft.ToMercator(map);
            MercatorPoint br = bottomRight.ToMercator(map);
            return new MercatorRect(tl, br);
        }

        /// <summary>
        /// Converts the GeoRect to a TileRect.
        /// </summary>
        /// <param name="map">The map to use for the conversion.</param>
        /// <param name="zoom">The zoom level to use for the conversion. If null, the current zoom level of the map is used.</param>
        /// <returns>A TileRect representing the converted rectangle.</returns>
        public TileRect ToTileRect(Map map, int? zoom = null)
        {
            return new TileRect(topLeft.ToTile(map, zoom), bottomRight.ToTile(map, zoom));
        }

        /// <summary>
        /// Returns a string representation of the GeoRect.
        /// </summary>
        /// <returns>A string representation of the GeoRect.</returns>
        public override string ToString()
        {
            return string.Format(Culture.numberFormat, 
                "(left: {0}, top: {1}, right: {2}, bottom: {3})", 
                left, top, right, bottom);
        }

        /// <summary>
        /// Checks if two GeoRects are equal.
        /// </summary>
        /// <param name="left">The first GeoRect.</param>
        /// <param name="right">The second GeoRect.</param>
        /// <returns>True if the GeoRects are equal; otherwise, false.</returns>
        public static bool operator ==(GeoRect left, GeoRect right) => left.Equals(right);

        /// <summary>
        /// Checks if two GeoRects are not equal
        /// </summary>
        /// <param name="left">The first GeoRect.</param>
        /// <param name="right">The second GeoRect.</param>
        /// <returns>True if the GeoRects are not equal; otherwise, false.</returns>
        public static bool operator !=(GeoRect left, GeoRect right) => !(left == right);
    }
}