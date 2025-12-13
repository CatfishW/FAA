/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    public class What3WordsAutoSuggestRequest : What3WordsRequestBase<What3WordsSBResult>
    {
        public readonly string address;
        public string language;
        public GeoPoint? focus;
        public Clip clip;
        public int? count;
        public Display display;

        protected override string service => "autosuggest";

        public What3WordsAutoSuggestRequest(string address)
        {
            this.address = address;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            builder.Append("&input=").Append(address);
            if (!string.IsNullOrEmpty(language)) builder.Append("&language=").Append(language);
            if (focus.HasValue) AppendLocation(builder, focus.Value);
            if (clip != null) clip.AppendURL(builder);
            if (count.HasValue) builder.Append("&count=").Append(count.Value);
            if (display != Display.full) builder.Append("&display=").Append(display);
        }

        public static What3WordsSBResult Parse(string response) => What3WordsSBResult.Parse(response);
        public override What3WordsSBResult ParseResult(string response) => Parse(response);
    }
}