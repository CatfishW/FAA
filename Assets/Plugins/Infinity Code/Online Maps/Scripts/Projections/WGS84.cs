/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;

namespace OnlineMaps
{
    /// <summary>
    /// Implementation of WGS84 Ellipsoid Mercator.
    /// </summary>
    public class WGS84: Projection
    {
        public override void LocationToTile(double longitude, double latitude, int zoom, out double tileX, out double tileY)
        {
            latitude = Mathd.Clamp(latitude, -85, 85);
            longitude = Mathd.Repeat(longitude, -180, 180);

            double rLon = longitude * Constants.Deg2Rad;
            double rLat = latitude * Constants.Deg2Rad;

            const double a = 6378137;
            const double d = 53.5865938 / 256;
            const double k = 0.0818191908426;

            double z = Math.Tan(Constants.PID4 + rLat / 2) / Math.Pow(Math.Tan(Constants.PID4 + Math.Asin(k * Math.Sin(rLat)) / 2), k);
            double z1 = Math.Pow(2, 23 - zoom);

        
            tileX = (20037508.342789 + a * rLon) * d / z1;
            tileY = (20037508.342789 - a * Math.Log(z)) * d / z1;
        }

        public override void LocationToMercator(double longitude, double latitude, out double mercatorX, out double mercatorY)
        {
            double rLon = longitude * Constants.Deg2Rad;
            double rLat = latitude * Constants.Deg2Rad;
            
            const double a = 6378137;
            const double k = 0.0818191908426;
            
            double z = Math.Tan(Constants.PID4 + rLat / 2) / Math.Pow(Math.Tan(Constants.PID4 + Math.Asin(k * Math.Sin(rLat)) / 2), k);
            
            mercatorX = a * rLon;
            mercatorY = a * Math.Log(z);
        }

        public override void MercatorToLocation(double mercatorX, double mercatorY, out double longitude, out double latitude)
        {
            const double a = 6378137;
            const double c1 = 0.00335655146887969;
            const double c2 = 0.00000657187271079536;
            const double c3 = 0.00000001764564338702;
            const double c4 = 0.00000000005328478445;

            double g = Math.PI / 2 - 2 * Math.Atan(1 / Math.Exp(mercatorY / a));
            double z = g + c1 * Math.Sin(2 * g) + c2 * Math.Sin(4 * g) + c3 * Math.Sin(6 * g) + c4 * Math.Sin(8 * g);

            latitude = z * Constants.Rad2Deg;
            longitude = mercatorX / a * Constants.Rad2Deg;
        }

        public override void TileToLocation(double tileX, double tileY, int zoom, out double longitude, out double latitude)
        {
            const double a = 6378137;
            const double c1 = 0.00335655146887969;
            const double c2 = 0.00000657187271079536;
            const double c3 = 0.00000001764564338702;
            const double c4 = 0.00000000005328478445;
            const double d = 256 / 53.5865938;
            double z1 = 23 - zoom;
            double mx = tileX * Math.Pow(2, z1) * d - 20037508.342789;
            double my = 20037508.342789 - tileY * Math.Pow(2, z1) * d;

            double g = Math.PI / 2 - 2 * Math.Atan(1 / Math.Exp(my / a));
            double z = g + c1 * Math.Sin(2 * g) + c2 * Math.Sin(4 * g) + c3 * Math.Sin(6 * g) + c4 * Math.Sin(8 * g);

            latitude = z * Constants.Rad2Deg;
            longitude = mx / a * Constants.Rad2Deg;
        }
    }
}