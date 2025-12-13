/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    public class QQSearchRequest : QQSearchRequestBase
    {
        public readonly string region;
        public bool autoExtend = true;
        public GeoPoint? location;
            
        public QQSearchRequest(string keyword, string region)
        {
            this.keyword = keyword;
            this.region = region;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            builder.Append("&boundary=region(");
            builder.Append(WebRequest.EscapeURL(region));
            builder.Append(",");
            builder.Append(autoExtend ? 1 : 2);
            if (location.HasValue) AppendLocation(builder, location.Value);
            builder.Append(")");
                
            if (getSubPois) builder.Append("&get_subpois=1");
        }
    }
}