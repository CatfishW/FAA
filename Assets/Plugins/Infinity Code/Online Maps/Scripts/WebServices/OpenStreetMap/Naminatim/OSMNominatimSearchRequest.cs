/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    public class OSMNominatimSearchRequest: OSMNominatimRequestBase
    {
        public string query;
        public string language = "en";
        public int limit;
        public bool addressdetails = true;

        public OSMNominatimSearchRequest(string query)
        {
            this.query = query;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            builder.Append("https://nominatim.openstreetmap.org/search?format=xml&q=").Append(WebRequest.EscapeURL(query));
            if (addressdetails) builder.Append("&addressdetails=1");
            if (limit > 0) builder.Append("&limit=").Append(limit);
            if (!string.IsNullOrEmpty(language)) builder.Append("&accept-language=").Append(language);
        }
    }
}