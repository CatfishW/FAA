/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;

namespace OnlineMaps
{
    public static class Constants
    {
        /// <summary>
        /// Arcseconds in meters.
        /// </summary>
        public const float AngleSecond = 1 / 3600f;

        /// <summary>
        /// Default size of the map.
        /// </summary>
        public const int DefaultMapSize = 1024;

        /// <summary>
        /// Degrees-to-radians conversion constant.
        /// </summary>
        public const double Deg2Rad = Math.PI / 180;

        /// <summary>
        /// Earth radius in meters.
        /// </summary>
        public const double EarthRadiusMeters = 6378137;
        
        /// <summary>
        /// The Earth radius in kilometers.
        /// </summary>
        public const double EarthRadius = EarthRadiusMeters / 1000;

        /// <summary>
        /// The minimum zoom level
        /// </summary>
        public const int MinZoom = 1;

        /// <summary>
        /// The maximum zoom level
        /// </summary>
#if ONLINEMAPS_MAXZOOM_23
        public const int MaxZoom = 23;
#elif ONLINEMAPS_MAXZOOM_22
        public const int MaxZoom = 22;
#elif ONLINEMAPS_MAXZOOM_21
        public const int MaxZoom = 21;
#else
        public const int MaxZoom = 20;
#endif

        /// <summary>
        /// The maximum zoom delta.
        /// </summary>
        public const float MaxZoomDelta = 0.999f;

        /// <summary>
        /// The maximum zoom level extended by the maximum zoom delta.
        /// </summary>
        public const float MaxZoomExt = MaxZoom + MaxZoomDelta;

        /// <summary>
        /// Maximal distance of raycast.
        /// </summary>
        public const int MaxRaycastDistance = 100000;

        /// <summary>
        /// Bytes per megabyte.
        /// </summary>
        public const int Mb = 1024 * 1024;

        /// <summary>
        /// PI * 2
        /// </summary>
        public const double PI2 = Math.PI * 2;

        /// <summary>
        /// PI * 4
        /// </summary>
        public const double PI4 = Math.PI * 4;

        /// <summary>
        /// PI / 4
        /// </summary>
        public const double PID4 = Math.PI / 4;

        /// <summary>
        /// Radians-to-degrees conversion constant.
        /// </summary>
        public const double Rad2Deg = 180 / Math.PI;

        /// <summary>
        /// tileSize squared, to accelerate the calculations.
        /// </summary>
        public const int SqrTileSize = TileSize * TileSize;

        /// <summary>
        /// Size of the tile texture in pixels.
        /// </summary>
        public const short TileSize = 256;

        /// <summary>
        /// The second in ticks.
        /// </summary>
        public const long TicksInSecond = 10000000;
    }
}