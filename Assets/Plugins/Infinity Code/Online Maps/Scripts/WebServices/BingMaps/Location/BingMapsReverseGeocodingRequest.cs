/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Class for reverse geocoding requests to the Bing Maps Location API.
    /// </summary>
    public class BingMapsReverseGeocodingRequest : BingMapsLocationRequestBase
    {
        /// <summary>
        /// The geographical point for the reverse geocoding request.
        /// </summary>
        public readonly GeoPoint point;
            
        /// <summary>
        /// Whether to include neighborhood information in the results.
        /// </summary>
        public readonly bool includeNeighborhood;

        protected override string urlToken => point.y + "," + point.x;

        /// <summary>
        /// Initializes a new instance of the ReverseGeocoding class.
        /// </summary>
        /// <param name="point">The geographical point for the reverse geocoding request.</param>
        /// <param name="includeNeighborhood">Whether to include neighborhood information in the results. Default is false.</param>
        public BingMapsReverseGeocodingRequest(GeoPoint point, bool includeNeighborhood = false)
        {
            this.point = point;
            this.includeNeighborhood = includeNeighborhood;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            if (includeNeighborhood) builder.Append("&inclnb=1");
        }
    }
}