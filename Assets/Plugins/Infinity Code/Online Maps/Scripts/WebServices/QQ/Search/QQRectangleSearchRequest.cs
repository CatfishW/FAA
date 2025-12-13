/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    public class QQRectangleSearchRequest : QQSearchRequestBase
    {
        public readonly GeoRect boundary;
            
        public QQRectangleSearchRequest(string keyword, GeoRect boundary)
        {
            this.keyword = keyword;
            this.boundary = boundary;
        }
            
        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);
                
            builder.Append("&boundary=rectangle(");
            AppendLocation(builder, boundary.topLeft);
            builder.Append(";");
            AppendLocation(builder, boundary.bottomRight);
            builder.Append(")");
        }
    }
}