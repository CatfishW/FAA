/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    public class ORSReverseGeocodeRequest : ORSGeocodeRequestBase
    {
        /// <summary>
        /// Location of a focus point. Specify the focus point to order results by linear distance to this point. Works for up to 100 kilometers distance.
        /// </summary>
        public GeoPoint point;
            
        /// <summary>
        /// Restrict search to circular region around point. Value in kilometers.
        /// </summary>
        public float? boundaryCircleRadius;

        protected override string service => "reverse";
            
        public ORSReverseGeocodeRequest(GeoPoint point)
        {
            this.point = point;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            builder.Append("&point.lon=").Append(point.x.ToString(Culture.numberFormat));
            builder.Append("&point.lat=").Append(point.y.ToString(Culture.numberFormat));
            if (boundaryCircleRadius.HasValue) builder.Append("&boundary.circle.radius=").Append(boundaryCircleRadius.Value.ToString(Culture.numberFormat));
        }
    }
}