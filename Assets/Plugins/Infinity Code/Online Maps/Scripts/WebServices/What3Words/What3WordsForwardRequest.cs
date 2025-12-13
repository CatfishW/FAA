/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    public class What3WordsForwardRequest : What3WordsRequestBase<What3WordsFRResult>
    {
        public readonly string address;
        public string language;
        public Display display;

        protected override string service => "convert-to-coordinates";

        public What3WordsForwardRequest(string address)
        {
            this.address = address;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            builder.Append("&words=").Append(address);
            if (!string.IsNullOrEmpty(language)) builder.Append("&lang=").Append(language);
            if (display != Display.full) builder.Append("&display=").Append(display);
        }

        public static What3WordsFRResult Parse(string response) => What3WordsFRResult.Parse(response);
        public override What3WordsFRResult ParseResult(string response) => Parse(response);
    }
}