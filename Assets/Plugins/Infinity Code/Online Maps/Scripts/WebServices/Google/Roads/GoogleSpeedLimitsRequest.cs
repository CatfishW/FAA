/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    public class GoogleSpeedLimitsRequest : GoogleRoadsRequestBase<GoogleSpeedLimitResult[]>
    {
        public GeoPoint[] path;
        public Units units = Units.KPH;

        public GoogleSpeedLimitsRequest(GeoPoint[] path)
        {
            this.path = path;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            if (string.IsNullOrEmpty(key)) key = KeyManager.GoogleMaps();

            builder.Append("https://roads.googleapis.com/v1/speedLimits?key=").Append(key);
            if (units != Units.KPH) builder.Append("&units=MPH");

            builder.Append("&path=");

            for (int i = 0; i < path.Length; i++)
            {
                if (i > 0) builder.Append("|");

                GeoPoint p = path[i];
                AppendLocation(builder, p);
            }
        }

        public static GoogleSpeedLimitResult[] Parse(string response) => GoogleSpeedLimitResult.Parse(response);
        public override GoogleSpeedLimitResult[] ParseResult(string response) => Parse(response);

        /// <summary>
        /// Speed limits in kilometers or miles per hour.
        /// </summary>
        public enum Units
        {
            /// <summary>
            /// Kilometers per hour.
            /// </summary>
            KPH,

            /// <summary>
            /// Miles per hour.
            /// </summary>
            MPH
        }
    }
}