/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// The what3words API gives you programmatic access to convert a 3 word address to coordinates (forward geocoding), to convert coordinates to a 3 word address (reverse geocoding), to obtain suggestions based on a full or partial 3 word address (AutoSuggest and StandardBlend), to obtain a geographically bounded section of the 3m x 3m what3words grid and to determine the languages that the API currently supports. 
    /// https://docs.what3words.com/api/v3/
    /// </summary>
    public abstract class What3WordsRequestBase<T> : TextWebService<T>
    {
        public string key;

        private const string endpoint = "https://api.what3words.com/v3/";
        protected abstract string service { get; }

        protected override void GenerateUrl(StringBuilder builder)
        {
            builder.Append(endpoint).Append(service);
            builder.Append("?format=json&key=");
            if (!string.IsNullOrEmpty(key)) builder.Append(key);
            else if (KeyManager.hasWhat3Words) builder.Append(KeyManager.What3Words());
            else throw new Exception("Please specify What3Words API Key");
        }

        /// <summary>
        /// Restricts results to those within a geographical area.
        /// </summary>
        public class Clip
        {
            private double v1;
            private double v2;
            private double v3;
            private double v4;
            private int type;

            /// <summary>
            /// Clips to a radius of km kilometers from the point.
            /// </summary>
            /// <param name="lng">Longitude of the point</param>
            /// <param name="lat">Latitude of the point</param>
            /// <param name="radius">Raduis (km)</param>
            public Clip(double lng, double lat, double radius)
            {
                v1 = lng;
                v2 = lat;
                v3 = radius;
                type = 0;
            }

            /// <summary>
            /// Clips to a radius of km kilometers from the point specified by the focus parameter.
            /// </summary>
            /// <param name="km">Radius (km)</param>
            public Clip(double km)
            {
                v1 = km;
                type = 1;
            }

            /// <summary>
            /// Clips to 3 word addresses that are fully contained inside a bounding box.
            /// </summary>
            /// <param name="leftLng">Left longitude</param>
            /// <param name="topLat">Top latitude</param>
            /// <param name="rightLng">Right longitude</param>
            /// <param name="bottomLat">Bottom latitude</param>
            public Clip(double leftLng, double topLat, double rightLng, double bottomLat)
            {
                v1 = leftLng;
                v2 = topLat;
                v3 = rightLng;
                v4 = bottomLat;
                type = 2;
            }

            public void AppendURL(StringBuilder url)
            {
                if (type == 0)
                {
                    url.Append("&clip-to-circle=")
                        .Append(v2.ToString(Culture.numberFormat)).Append(",")
                        .Append(v1.ToString(Culture.numberFormat)).Append(",")
                        .Append(v3.ToString(Culture.numberFormat));
                }
                else if (type == 2)
                {
                    url.Append("&clip-to-bounding-box=")
                        .Append(v2.ToString(Culture.numberFormat)).Append(",")
                        .Append(v3.ToString(Culture.numberFormat)).Append(",")
                        .Append(v4.ToString(Culture.numberFormat)).Append(",")
                        .Append(v1.ToString(Culture.numberFormat));
                }
            }
        }

        /// <summary>
        /// Display type
        /// </summary>
        public enum Display
        {
            /// <summary>
            /// Full output
            /// </summary>
            full,

            /// <summary>
            /// Less output
            /// </summary>
            terse,

            /// <summary>
            /// The bare minimum
            /// </summary>
            minimal
        }
    }
}