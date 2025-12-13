/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Represents a request to get elevation data for a set of geographical points.
    /// </summary>
    public class BingMapsPointsElevationRequest : BingMapsElevationRequestBase
    {
        protected GeoPoint[] points;

        protected override string urlToken => "List";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="points">An array of geographical points.</param>
        /// <param name="heights">The height reference system (default is sea level).</param>
        public BingMapsPointsElevationRequest(GeoPoint[] points, Heights heights = Heights.sealevel) : base(heights)
        {
            this.points = points;
        }

        private string EncodePoints()
        {
            long prevLatitude = 0;
            long prevLongitude = 0;

            StringBuilder builder = StaticStringBuilder.Start();

            foreach (GeoPoint p in points)
            {
                long newLatitude = (long)Math.Round(p.latitude * 100000);
                long newLongitude = (long)Math.Round(p.longitude * 100000);

                long dy = newLatitude - prevLatitude;
                long dx = newLongitude - prevLongitude;
                prevLatitude = newLatitude;
                prevLongitude = newLongitude;

                dy = (dy << 1) ^ (dy >> 31);
                dx = (dx << 1) ^ (dx >> 31);

                long index = (dy + dx) * (dy + dx + 1) / 2 + dy;

                while (index > 0)
                {
                    long rem = index & 31;
                    index = (index - rem) / 32;
                    if (index > 0) rem += 32;
                    builder.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-"[(int)rem]);
                }
            }

            return builder.ToString();
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            builder.Append("&pts=").Append(EncodePoints());
        }
    }
}