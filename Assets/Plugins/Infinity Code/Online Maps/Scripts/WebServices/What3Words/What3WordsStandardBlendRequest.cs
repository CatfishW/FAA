/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    public class What3WordsStandardBlendRequest : What3WordsRequestBase<What3WordsSBResult>
    {
        public readonly string address;
        public string language;
        public GeoPoint focus;

        protected override string service => "standardblend";

        public What3WordsStandardBlendRequest(string address)
        {
            this.address = address;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            builder.Append("&addr=").Append(address);
            if (!string.IsNullOrEmpty(language)) builder.Append("&lang=").Append(language);
            if (focus != null) AppendLocation(builder, focus);
        }

        public static What3WordsSBResult Parse(string response) => What3WordsSBResult.Parse(response);
        public override What3WordsSBResult ParseResult(string response) => Parse(response);
    }
}