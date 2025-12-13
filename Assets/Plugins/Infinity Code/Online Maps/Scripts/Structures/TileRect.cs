/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Represents a rectangular area in tile coordinates.
    /// </summary>
    public struct TileRect
    {
        /// <summary>
        /// Left boundary of the rectangle.
        /// </summary>
        public double left;

        /// <summary>
        /// Top boundary of the rectangle.
        /// </summary>
        public double top;

        /// <summary>
        /// Right boundary of the rectangle.
        /// </summary>
        public double right;

        /// <summary>
        /// Bottom boundary of the rectangle.
        /// </summary>
        public double bottom;

        /// <summary>
        /// Zoom level of the rectangle.
        /// </summary>
        public int zoom;

        /// <summary>
        /// Gets the bottom-left point of the rectangle.
        /// </summary>
        public TilePoint bottomLeft => new TilePoint(left, bottom, zoom);

        /// <summary>
        /// Gets the bottom-right point of the rectangle.
        /// </summary>
        public TilePoint bottomRight => new TilePoint(right, bottom, zoom);

        /// <summary>
        /// Gets the center point of the rectangle.
        /// </summary>
        public TilePoint center => new TilePoint((left + right) / 2, (top + bottom) / 2, zoom);

        /// <summary>
        /// Gets the height of the rectangle.
        /// </summary>
        public double height => bottom - top;

        /// <summary>
        /// Gets the total number of tiles on the current zoom level (2^zoom).
        /// </summary>
        public int maxTiles => 1 << zoom;

        /// <summary>
        /// Gets the size of the rectangle.
        /// </summary>
        public Vector2d size => new Vector2d(width, height);

        /// <summary>
        /// Gets the top-left point of the rectangle.
        /// </summary>
        public TilePoint topLeft => new TilePoint(left, top, zoom);

        /// <summary>
        /// Gets the top-right point of the rectangle.
        /// </summary>
        public TilePoint topRight => new TilePoint(right, top, zoom);

        /// <summary>
        /// Gets the width of the rectangle.
        /// </summary>
        public double width => right - left;

        /// <summary>
        /// Initializes a new instance of the TileRect struct.
        /// </summary>
        /// <param name="left">Left boundary of the rectangle.</param>
        /// <param name="top">Top boundary of the rectangle.</param>
        /// <param name="right">Right boundary of the rectangle.</param>
        /// <param name="bottom">Bottom boundary of the rectangle.</param>
        /// <param name="zoom">Zoom level of the rectangle.</param>
        public TileRect(double left, double top, double right, double bottom, int zoom)
        {
            this.left = left;
            this.top = top;
            this.right = right;
            this.bottom = bottom;
            this.zoom = zoom;
        }

        /// <summary>
        /// Initializes a new instance of the TileRect struct.
        /// </summary>
        /// <param name="topLeft">Top-left point of the rectangle.</param>
        /// <param name="bottomRight">Bottom-right point of the rectangle.</param>
        public TileRect(TilePoint topLeft, TilePoint bottomRight)
        {
            if (topLeft.zoom != bottomRight.zoom) bottomRight = bottomRight.ToZoom(topLeft.zoom);
            left = topLeft.x;
            top = topLeft.y;
            right = bottomRight.x;
            bottom = bottomRight.y;
            zoom = topLeft.zoom;
        }

        /// <summary>
        /// Determines whether the specified tile coordinates are contained within this rectangle.
        /// </summary>
        /// <param name="x">The x-coordinate (tile).</param>
        /// <param name="y">The y-coordinate (tile).</param>
        /// <returns>True if the point is inside the rectangle; otherwise, false.</returns>
        public bool Contains(double x, double y)
        {
            return (int)left <= x && (int)right >= x && (int)top <= y && (int)bottom >= y;
        }
        
        /// <summary>
        /// Determines whether the specified geographic location is contained within this rectangle using the given map.
        /// </summary>
        /// <param name="location">The geographic location to check.</param>
        /// <param name="map">The map used for conversion.</param>
        /// <returns>True if the location is inside the rectangle; otherwise, false.</returns>
        public bool Contains(GeoPoint location, Map map)
        {
            TilePoint tp = location.ToTile(map);
            return Contains(tp.x, tp.y);
        }
        
        /// <summary>
        /// Determines whether the specified geographic location is contained within this rectangle using the given projection.
        /// </summary>
        /// <param name="location">The geographic location to check.</param>
        /// <param name="projection">The projection used for conversion.</param>
        /// <returns>True if the location is inside the rectangle; otherwise, false.</returns>
        public bool Contains(GeoPoint location, Projection projection)
        {
            TilePoint tp = location.ToTile(projection, zoom);
            return Contains(tp.x, tp.y);
        }
        
        /// <summary>
        /// Determines whether the specified Mercator point is contained within this rectangle.
        /// </summary>
        /// <param name="p">The Mercator point to check.</param>
        /// <returns>True if the point is inside the rectangle; otherwise, false.</returns>
        public bool Contains(MercatorPoint p)
        {
            TilePoint tp = p.ToTile(zoom);
            return Contains(tp.x, tp.y);
        }
        
        /// <summary>
        /// Determines whether the specified tile point is contained within this rectangle.
        /// </summary>
        /// <param name="p">The tile point to check.</param>
        /// <returns>True if the point is inside the rectangle; otherwise, false.</returns>
        public bool Contains(TilePoint p)
        {
            if (p.zoom != zoom) p = p.ToZoom(zoom);
            return Contains(p.x, p.y);
        }
        
        /// <summary>
        /// Determines whether the specified tile point in Vector2d is contained within this rectangle.
        /// </summary>
        /// <param name="p">The 2D vector point to check.</param>
        /// <returns>True if the point is inside the rectangle; otherwise, false.</returns>
        public bool Contains(Vector2d p)
        {
            return Contains(p.x, p.y);
        }

        /// <summary>
        /// Determines whether the specified point is contained within this rectangle.
        /// </summary>
        /// <param name="p">The point to check.</param>
        /// <returns>True if the point is inside the rectangle; otherwise, false.</returns>
        public bool Contains(Vector2Int p)
        {
            return (int)left <= p.x && (int)right >= p.x && (int)top <= p.y && (int)bottom >= p.y;
        }

        /// <summary>
        /// Determines whether the specified tile point is contained within this rectangle,
        /// taking into account horizontal wrapping (e.g., for world maps that wrap around).
        /// </summary>
        /// <param name="point">The tile point to check.</param>
        /// <returns>True if the point is inside the rectangle (with wrapping); otherwise, false.</returns>
        public bool ContainsWrapped(TilePoint point)
        {
            int max = maxTiles;
            if (point.zoom != max) point = point.ToZoom(zoom);
            
            return (point.x > left && point.x < right
                    || point.x + max > left && point.x + max < right
                    || point.x - max > left && point.x - max < right)
                   && point.y > top && point.y < bottom;
        }

        /// <summary>
        /// Expands the rectangle to include the specified tile coordinates.
        /// </summary>
        /// <param name="x">The x-coordinate (tile) to encapsulate.</param>
        /// <param name="y">The y-coordinate (tile) to encapsulate.</param>
        public void Encapsulate(double x, double y)
        {
            if (left > x) left = x;
            if (right < x) right = x;
            if (top > y) top = y;
            if (bottom < y) bottom = y;
        }
        
        /// <summary>
        /// Expands the rectangle to include the specified geographic location using the given map.
        /// </summary>
        /// <param name="location">The geographic location to encapsulate.</param>
        /// <param name="map">The map used for conversion.</param>
        public void Encapsulate(GeoPoint location, Map map)
        {
            TilePoint point = location.ToTile(map);
            Encapsulate(point.x, point.y);
        }
        
        /// <summary>
        /// Expands the rectangle to include the specified geographic location using the given projection.
        /// </summary>
        /// <param name="location">The geographic location to encapsulate.</param>
        /// <param name="projection">The projection used for conversion.</param>
        public void Encapsulate(GeoPoint location, Projection projection)
        {
            TilePoint point = location.ToTile(projection, zoom);
            Encapsulate(point.x, point.y);
        }
        
        /// <summary>
        /// Expands the rectangle to include the specified Mercator point.
        /// </summary>
        /// <param name="p">The Mercator point to encapsulate.</param>
        public void Encapsulate(MercatorPoint p)
        {
            TilePoint point = p.ToTile(zoom);
            Encapsulate(point.x, point.y);
        }
        
        /// <summary>
        /// Expands the rectangle to include the specified tile point.
        /// </summary>
        /// <param name="point">The tile point to encapsulate.</param>
        public void Encapsulate(TilePoint point)
        {
            if (point.zoom != zoom) point = point.ToZoom(zoom);
            Encapsulate(point.x, point.y);
        }
        
        /// <summary>
        /// Expands the rectangle to include the specified 2D vector point.
        /// </summary>
        /// <param name="p">The 2D vector point to encapsulate.</param>
        public void Encapsulate(Vector2d p)
        {
            Encapsulate(p.x, p.y);
        }

        /// <summary>
        /// Fixes the rectangle in case of tile wrapping (when the left boundary is greater than the right boundary).
        /// </summary>
        public void FixWrap()
        {
            if (left > right) right += maxTiles;
        }

        /// <summary>
        /// Determines whether the specified rectangle intersects with this rectangle.
        /// </summary>
        /// <param name="rect">The rectangle to check for intersection.</param>
        /// <returns>True if the rectangles intersect; otherwise, false.</returns>
        public bool Intersects(TileRect rect)
        {
            return !(rect.left > right || rect.right < left || rect.top > bottom || rect.bottom < top);
        }

        /// <summary>
        /// Linearly interpolates between the left and right boundaries and the top and bottom boundaries.
        /// </summary>
        /// <param name="x">The x-coordinate to interpolate.</param>
        /// <param name="y">The y-coordinate to interpolate.</param>
        /// <returns>A TilePoint representing the interpolated point.</returns>
        public TilePoint Lerp(double x, double y)
        {
            return new TilePoint((x - left) / width, (y - top) / height, zoom);
        }

        /// <summary>
        /// Linearly interpolates between the left and right boundaries and the top and bottom boundaries.
        /// </summary>
        /// <param name="point">The point to interpolate.</param>
        /// <returns>A TilePoint representing the interpolated point.</returns>
        public TilePoint Lerp(Vector2d point)
        {
            return Lerp(point.x, point.y);
        }

        /// <summary>
        /// Converts this tile rectangle to a geographic rectangle using the specified map.
        /// </summary>
        /// <param name="map">The map used for conversion.</param>
        /// <returns>A <see cref="GeoRect"/> representing the geographic rectangle.</returns>
        public GeoRect ToGeoRect(Map map)
        {
            return new GeoRect(topLeft.ToLocation(map), bottomRight.ToLocation(map));
        }

        /// <summary>
        /// Converts this tile rectangle to a geographic rectangle using the specified projection.
        /// </summary>
        /// <param name="projection">The projection used for conversion.</param>
        /// <returns>A <see cref="GeoRect"/> representing the geographic rectangle.</returns>
        public GeoRect ToGeoRect(Projection projection)
        {
            return new GeoRect(topLeft.ToLocation(projection), bottomRight.ToLocation(projection));
        }

        /// <summary>
        /// Converts this tile rectangle to a Mercator rectangle.
        /// </summary>
        /// <returns>A <see cref="MercatorRect"/> representing the Mercator rectangle.</returns>
        public MercatorRect ToMercatorRect()
        {
            return new MercatorRect(topLeft.ToMercator(), bottomRight.ToMercator());
        }

        public override string ToString()
        {
            return string.Format(Culture.numberFormat, 
                "(left: {0}, top: {1}, right: {2}, bottom: {3})", 
                left, top, right, bottom);
        }
        
        /// <summary>
        /// Returns a new TileRect at the specified zoom level.
        /// </summary>
        /// <param name="newZoom">The target zoom level.</param>
        /// <returns>A TileRect at the new zoom level.</returns>
        public TileRect ToZoom(int newZoom)
        {
            if (newZoom == zoom) return this;
            return new TileRect(topLeft.ToZoom(newZoom), bottomRight.ToZoom(newZoom));
        }
    }
}