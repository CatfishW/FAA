/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    public class QQNearbySearchRequest : QQSearchRequestBase
    {
        public readonly GeoPoint location;
        public readonly int radius;
        public int expandRange = 1;

        public QQNearbySearchRequest(string keyword, GeoPoint location, int radius)
        {
            this.keyword = keyword;
            this.location = location;
            this.radius = radius;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);
                
            builder.Append("&boundary=nearby(");
            AppendLocation(builder, location);
            builder.Append(",");
            builder.Append(radius);
            builder.Append(",");
            builder.Append(expandRange);
            builder.Append(")");
        }
    }
}