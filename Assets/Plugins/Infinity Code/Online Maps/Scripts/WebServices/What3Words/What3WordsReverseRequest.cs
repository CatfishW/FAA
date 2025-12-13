/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    public class What3WordsReverseRequest : What3WordsRequestBase<What3WordsFRResult>
    {
        public readonly GeoPoint location;
        public string language;
        public Display display;

        protected override string service => "convert-to-3wa";

        public What3WordsReverseRequest(GeoPoint location)
        {
            this.location = location;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            builder.Append("&coordinates=");
            AppendLocation(builder, location);
            if (!string.IsNullOrEmpty(language)) builder.Append("&language=").Append(language);
            if (display != Display.full) builder.Append("&display=").Append(display);
        }

        public static What3WordsFRResult Parse(string response) => What3WordsFRResult.Parse(response);
        public override What3WordsFRResult ParseResult(string response) => Parse(response);
    }
}