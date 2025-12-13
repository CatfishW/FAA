/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;

namespace OnlineMaps
{
    /// <summary>
    /// Implementation of WGS84 Spherical Mercator (Web Mercator).
    /// </summary>
    public class SphericalMercator : Projection
    {
        public override void LocationToTile(double longitude, double latitude, int zoom, out double tileX, out double tileY)
        {
            double sy = Math.Sin(latitude * Constants.Deg2Rad);
            longitude = (longitude + 180) / 360;
            latitude = 0.5 - Math.Log((1 + sy) / (1 - sy)) / Constants.PI4;
            long mapSize = (long)Constants.TileSize << zoom;
            double px = longitude * mapSize;
            double py = latitude * mapSize;

            if (px < 0) px = 0;
            else if (px > mapSize) px = mapSize;
            if (py < 0) py = 0;
            else if (py > mapSize) py = mapSize;

            tileX = px / Constants.TileSize;
            tileY = py / Constants.TileSize;
        }

        public override void LocationToMercator(double longitude, double latitude, out double mercatorX, out double mercatorY)
        {
            double sy = Math.Sin(latitude * Constants.Deg2Rad);
            mercatorX = (longitude + 180) / 360;
            mercatorY = 0.5 - Math.Log((1 + sy) / (1 - sy)) / Constants.PI4;
        }

        public override void MercatorToLocation(double mercatorX, double mercatorY, out double longitude, out double latitude)
        {
            double lat = 90 - 360 * Math.Atan(Math.Exp((Mathd.Clamp01(mercatorY) - 0.5) * Constants.PI2)) / Math.PI;
            latitude = lat;
            longitude = mercatorX * 360 - 180;
        }

        public override void TileToLocation(double tileX, double tileY, int zoom, out double longitude, out double latitude)
        {
            double mapSize = (long)Constants.TileSize << zoom;
            longitude = 360 * (Mathd.Repeat(tileX * Constants.TileSize, 0, mapSize) / mapSize - 0.5);
            latitude = 90 - 360 * Math.Atan(Math.Exp((Mathd.Clamp(tileY * Constants.TileSize, mapSize) / mapSize - 0.5) * Constants.PI2)) / Math.PI;
        }
    }
}