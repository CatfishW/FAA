/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    public class GoogleSnapToRoadsRequest : GoogleRoadsRequestBase<GoogleSnapToRoadResult[]>
    {
        public bool interpolate;
        public GeoPoint[] path;

        public GoogleSnapToRoadsRequest(GeoPoint[] path, bool interpolate = false)
        {
            this.path = path;
            this.interpolate = interpolate;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            if (string.IsNullOrEmpty(key)) key = KeyManager.GoogleMaps();

            builder.Append("https://roads.googleapis.com/v1/snapToRoads?key=").Append(key);
            if (interpolate) builder.Append("&intepolate=true");
            builder.Append("&path=");

            for (int i = 0; i < path.Length; i++)
            {
                if (i > 0) builder.Append("|");

                GeoPoint p = path[i];
                AppendLocation(builder, p);
            }
        }

        public static GoogleSnapToRoadResult[] Parse(string response) => GoogleSnapToRoadResult.Parse(response);
        public override GoogleSnapToRoadResult[] ParseResult(string response) => Parse(response);
    }
}