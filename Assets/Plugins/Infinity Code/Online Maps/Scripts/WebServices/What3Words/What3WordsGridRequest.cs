/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    public class What3WordsGridRequest : What3WordsRequestBase<What3WordsGridResult>
    {
        public readonly GeoRect bbox;

        protected override string service => "grid-section";

        public What3WordsGridRequest(GeoRect bbox)
        {
            this.bbox = bbox;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            builder.Append("&bounding-box=");
            AppendLocation(builder, bbox.topRight);
            builder.Append(",");
            AppendLocation(builder, bbox.bottomLeft);
        }

        public static What3WordsGridResult Parse(string response) => What3WordsGridResult.Parse(response);
        public override What3WordsGridResult ParseResult(string response) => Parse(response);
    }
}