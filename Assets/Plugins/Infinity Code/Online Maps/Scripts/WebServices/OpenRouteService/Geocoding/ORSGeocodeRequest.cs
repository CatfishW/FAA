/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    public class ORSGeocodeRequest : ORSGeocodeRequestBase
    {
        /// <summary>
        /// Name of location, street address or postal code.
        /// </summary>
        public readonly string text;
            
        /// <summary>
        /// Location of a focus point. Specify the focus point to order results by linear distance to this point. Works for up to 100 kilometers distance.
        /// </summary>
        public GeoPoint? focusPoint;

        /// <summary>
        /// Top-left border of rectangular boundary to narrow results.
        /// </summary>
        public GeoPoint? boundaryMin;

        /// <summary>
        /// Bottom-right border of rectangular boundary to narrow results.
        /// </summary>
        public GeoPoint? boundaryMax;

        /// <summary>
        /// Center location of circular boundary to narrow results.
        /// </summary>
        public GeoPoint? boundaryCircle;

        /// <summary>
        /// Radius of circular boundary to narrow results.
        /// </summary>
        public float boundaryCircleRadius = 50;

        protected override string service => "search";
            
        public ORSGeocodeRequest(string text)
        {
            this.text = text;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);
                
            builder.Append("&text=").Append(WebRequest.EscapeURL(text));
                
            if (focusPoint.HasValue)
            {
                builder.Append("&focus.point.lat=").Append(focusPoint.Value.y.ToString(Culture.numberFormat))
                    .Append("&focus.point.lon=").Append(focusPoint.Value.x.ToString(Culture.numberFormat));
            }

            if (boundaryMin.HasValue)
            {
                builder.Append("&boundary.rect.min_lat=").Append(boundaryMin.Value.y.ToString(Culture.numberFormat))
                    .Append("&boundary.rect.min_lon=").Append(boundaryMin.Value.x.ToString(Culture.numberFormat));
            }

            if (boundaryMax.HasValue)
            {
                builder.Append("&boundary.rect.max_lat=").Append(boundaryMax.Value.y.ToString(Culture.numberFormat))
                    .Append("&boundary.rect.max_lon=").Append(boundaryMax.Value.x.ToString(Culture.numberFormat));
            }

            if (boundaryCircle.HasValue)
            {
                builder.Append("&boundary.circle.lat=").Append(boundaryCircle.Value.y.ToString(Culture.numberFormat))
                    .Append("&boundary.circle.lon=").Append(boundaryCircle.Value.x.ToString(Culture.numberFormat))
                    .Append("&boundary.circle.radius=").Append(boundaryCircleRadius.ToString(Culture.numberFormat));
            }
        }
    }
}