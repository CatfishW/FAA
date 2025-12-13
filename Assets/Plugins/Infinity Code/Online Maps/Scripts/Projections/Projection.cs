/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    /// <summary>
    /// The base class of map projection.
    /// </summary>
    public abstract class Projection
    {
        /// <summary>
        /// Converts geographic coordinates to Mercator coordinates.
        /// </summary>
        /// <param name="longitude">Longitude in degrees.</param>
        /// <param name="latitude">Latitude in degrees.</param>
        /// <param name="mercatorX">Output Mercator X coordinate.</param>
        /// <param name="mercatorY">Output Mercator Y coordinate.</param>
        public abstract void LocationToMercator(double longitude, double latitude, out double mercatorX, out double mercatorY);

        /// <summary>
        /// Converts geographic coordinates to Mercator coordinates and returns a MercatorPoint.
        /// </summary>
        /// <param name="longitude">Longitude in degrees.</param>
        /// <param name="latitude">Latitude in degrees.</param>
        /// <returns>A MercatorPoint representing the converted coordinates.</returns>
        public MercatorPoint LocationToMercator(double longitude, double latitude)
        {
            LocationToMercator(longitude, latitude, out double mx, out double my);
            return new MercatorPoint(mx, my);
        }

        /// <summary>
        /// Converts geographic coordinates to tile coordinates.
        /// </summary>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        /// <param name="zoom">Zoom</param>
        /// <param name="tileX">Tile X</param>
        /// <param name="tileY">Tile Y</param>
        public abstract void LocationToTile(double longitude, double latitude, int zoom, out double tileX, out double tileY);

        /// <summary>
        /// Converts geographic coordinates to tile coordinates and returns a TilePoint.
        /// </summary>
        /// <param name="longitude">Longitude in degrees.</param>
        /// <param name="latitude">Latitude in degrees.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <returns>A TilePoint representing the converted coordinates.</returns>
        public TilePoint LocationToTile(double longitude, double latitude, int zoom)
        {
            LocationToTile(longitude, latitude, zoom, out double tileX, out double tileY);
            return new TilePoint(tileX, tileY, zoom);
        }

        /// <summary>
        /// Converts geographic coordinates to tile coordinates and returns a TilePoint.
        /// </summary>
        /// <param name="coordinate">Geographic coordinates as a GeoPoint.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <returns>A TilePoint representing the converted coordinates.</returns>
        public TilePoint LocationToTile(GeoPoint coordinate, int zoom)
        {
            double tx, ty;
            LocationToTile(coordinate.x, coordinate.y, zoom, out tx, out ty);
            return new TilePoint(tx, ty, zoom);
        }

        /// <summary>
        /// Converts geographic coordinates to tile coordinates.
        /// </summary>
        /// <param name="coordinate">Geographic coordinates as a GeoPoint.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <param name="tileX">Output Tile X coordinate.</param>
        /// <param name="tileY">Output Tile Y coordinate.</param>
        public void LocationToTile(GeoPoint coordinate, int zoom, out double tileX, out double tileY)
        {
            LocationToTile(coordinate.x, coordinate.y, zoom, out tileX, out tileY);
        }

        /// <summary>
        /// Converts Mercator coordinates to geographic coordinates.
        /// </summary>
        /// <param name="mercatorX">Mercator X coordinate.</param>
        /// <param name="mercatorY">Mercator Y coordinate.</param>
        /// <param name="longitude">Output longitude in degrees.</param>
        /// <param name="latitude">Output latitude in degrees.</param>
        public abstract void MercatorToLocation(double mercatorX, double mercatorY, out double longitude, out double latitude);

        /// <summary>
        /// Converts Mercator coordinates to geographic coordinates and returns a GeoPoint.
        /// </summary>
        /// <param name="mercatorX">Mercator X coordinate.</param>
        /// <param name="mercatorY">Mercator Y coordinate.</param>
        /// <returns>A GeoPoint representing the converted coordinates.</returns>
        public GeoPoint MercatorToLocation(double mercatorX, double mercatorY)
        {
            MercatorToLocation(mercatorX, mercatorY, out double lng, out double lat);
            return new GeoPoint(lng, lat);
        }

        /// <summary>
        /// Converts Mercator coordinates to tile coordinates and returns a TilePoint.
        /// </summary>
        /// <param name="mercatorX">Mercator X coordinate.</param>
        /// <param name="mercatorY">Mercator Y coordinate.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <returns>A TilePoint representing the converted coordinates.</returns>
        public static TilePoint MercatorToTile(double mercatorX, double mercatorY, int zoom)
        {
            int size = 1 << zoom;
            return new TilePoint(Mathd.Repeat01(mercatorX) * size, Mathd.Clamp01(mercatorY) * size, zoom);
        }

        /// <summary>
        /// Converts Mercator coordinates to tile coordinates.
        /// </summary>
        /// <param name="mercatorX">Mercator X coordinate.</param>
        /// <param name="mercatorY">Mercator Y coordinate.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <param name="tileX">Output Tile X coordinate.</param>
        /// <param name="tileY">Output Tile Y coordinate.</param>
        public static void MercatorToTile(double mercatorX, double mercatorY, int zoom, out double tileX, out double tileY)
        {
            int size = 1 << zoom;
            tileX = Mathd.Repeat01(mercatorX) * size;
            tileY = Mathd.Clamp01(mercatorY) * size;
        }

        /// <summary>
        /// Converts tile coordinates to geographic coordinates.
        /// </summary>
        /// <param name="tileX">Tile X</param>
        /// <param name="tileY">Tile Y</param>
        /// <param name="zoom">Zoom</param>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        public abstract void TileToLocation(double tileX, double tileY, int zoom, out double longitude, out double latitude);

        /// <summary>
        /// Converts tile coordinates to geographic coordinates and returns a GeoPoint.
        /// </summary>
        /// <param name="tileX">Tile X coordinate.</param>
        /// <param name="tileY">Tile Y coordinate.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <returns>A GeoPoint representing the converted coordinates.</returns>
        public GeoPoint TileToLocation(double tileX, double tileY, int zoom)
        {
            TileToLocation(tileX, tileY, zoom, out double lng, out double lat);
            return new GeoPoint(lng, lat);
        }

        /// <summary>
        /// Converts tile coordinates to geographic coordinates and returns a GeoPoint.
        /// </summary>
        /// <param name="tile">TilePoint representing the tile coordinates.</param>
        /// <param name="zoom">Optional zoom level. If not provided, the zoom level of the TilePoint is used.</param>
        /// <returns>A GeoPoint representing the converted coordinates.</returns>
        public GeoPoint TileToLocation(TilePoint tile, int? zoom = null)
        {
            int z = zoom != null ? zoom.Value : tile.zoom;
            TileToLocation(tile.x, tile.y, z, out double lng, out double lat);
            return new GeoPoint(lng, lat);
        }

        /// <summary>
        /// Converts tile coordinates to Mercator coordinates.
        /// </summary>
        /// <param name="tileX">Tile X coordinate.</param>
        /// <param name="tileY">Tile Y coordinate.</param>
        /// <param name="zoom">Zoom level.</param>
        /// <param name="mercatorX">Output Mercator X coordinate.</param>
        /// <param name="mercatorY">Output Mercator Y coordinate.</param>
        public static void TileToMercator(double tileX, double tileY, int zoom, out double mercatorX, out double mercatorY)
        {
            int size = 1 << zoom;
            mercatorX = Mathd.Repeat(tileX, size) / size;
            mercatorY = Mathd.Clamp(tileY, size) / size;
        }

        /// <summary>
        /// Converts tile coordinates to Mercator coordinates and returns a MercatorPoint.
        /// </summary>
        /// <param name="tile">TilePoint representing the tile coordinates.</param>
        /// <returns>A MercatorPoint representing the converted coordinates.</returns>
        public static MercatorPoint TileToMercator(TilePoint tile)
        {
            int size = 1 << tile.zoom;
            return new MercatorPoint(Mathd.Repeat(tile.x, size) / size, Mathd.Clamp(tile.y, size) / size);
        }
    }
}