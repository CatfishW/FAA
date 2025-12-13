/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    public class QQPolygonSearchRequest : QQSearchRequestBase
    {
        public readonly GeoPoint[] points;

        protected override string service => "search_by_polygon";

        public QQPolygonSearchRequest(GeoPoint[] points)
        {
            this.points = points;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);
                
            builder.Append("&polygon=");
                
            for (int i = 0; i < points.Length; i++)
            {
                if (i > 0) builder.Append(";");
                AppendLocation(builder, points[i]);
            }
        }
    }
}