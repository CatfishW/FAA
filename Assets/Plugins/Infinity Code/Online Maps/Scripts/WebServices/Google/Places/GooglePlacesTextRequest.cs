/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Request parameters for Text Search
    /// </summary>
    public class GooglePlacesTextRequest : GooglePlacesRequestBase
    {
        /// <summary>
        /// The text string on which to search, for example: "restaurant". <br/>
        /// The Google Places service will return candidate matches based on this string and order the results based on their perceived relevance.
        /// </summary>
        public string query;

        /// <summary>
        /// The language code, indicating in which language the results should be returned, if possible. 
        /// </summary>
        public string language;

        public override string typePath => "textsearch";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="query">
        /// The text string on which to search, for example: "restaurant". <br/>
        /// The Google Places service will return candidate matches based on this string and order the results based on their perceived relevance.
        /// </param>
        public GooglePlacesTextRequest(string query)
        {
            this.query = query;
        }

        public override void AppendParams(StringBuilder builder)
        {
            if (!string.IsNullOrEmpty(query)) builder.Append("&query=").Append(WebRequest.EscapeURL(query));
            if (!string.IsNullOrEmpty(language)) builder.Append("&language=").Append(language);
        }
    }
}