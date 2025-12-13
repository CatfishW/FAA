/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Class for geocoding requests to the Bing Maps Location API.
    /// </summary>
    public class BingMapsGeocodingRequest : BingMapsLocationRequestBase
    {
        /// <summary>
        /// The query string for the geocoding request.
        /// </summary>
        public readonly string query;
            
        /// <summary>
        /// The maximum number of results to return.
        /// </summary>
        public readonly int maxResults;
            
        /// <summary>
        /// Whether to include neighborhood information in the results.
        /// </summary>
        public readonly bool includeNeighborhood;
            
        protected override string urlToken => query;

        /// <summary>
        /// Initializes a new instance of the Geocoding class.
        /// </summary>
        /// <param name="query">The query string for the geocoding request.</param>
        /// <param name="maxResults">The maximum number of results to return. Default is 5.</param>
        /// <param name="includeNeighborhood">Whether to include neighborhood information in the results. Default is false.</param>
        public BingMapsGeocodingRequest(string query, int maxResults = 5, bool includeNeighborhood = false)
        {
            this.query = query;
            this.maxResults = maxResults;
            this.includeNeighborhood = includeNeighborhood;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);
            if (includeNeighborhood) builder.Append("&inclnb=1");
            if (maxResults > 0 && maxResults != 5) builder.Append("&maxRes=").Append(maxResults);
        }
    }
}