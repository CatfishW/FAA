/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Request parameters for Radar Search
    /// </summary>
    public class GooglePlacesRadarRequest : GooglePlacesRequestBase
    {
        /// <summary>
        /// A term to be matched against all content that Google has indexed for this place, including but not limited to name, type, and address, as well as customer reviews and other third-party content.
        /// </summary>
        public string keyword;

        /// <summary>
        /// One or more terms to be matched against the names of places, separated by a space character. <br/>
        /// Results will be restricted to those containing the passed name values. <br/>
        /// Note that a place may have additional names associated with it, beyond its listed name. <br/>
        /// The API will try to match the passed name value against all of these names. <br/>
        /// As a result, places may be returned in the results whose listed names do not match the search term, but whose associated names do.
        /// </summary>
        public string name;

        public override string typePath => "radarsearch";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="longitude">The longitude around which to retrieve place information.</param>
        /// <param name="latitude">The latitude around which to retrieve place information.</param>
        /// <param name="radius">
        /// Defines the distance (in meters) within which to return place results. <br/>
        /// The maximum allowed radius is 50000 meters.
        /// </param>
        public GooglePlacesRadarRequest(double longitude, double latitude, int radius)
        {
            location = new GeoPoint(longitude, latitude);
            this.radius = radius;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="location">The longitude/latitude around which to retrieve place information.</param>
        /// <param name="radius">
        /// Defines the distance (in meters) within which to return place results. <br/>
        /// The maximum allowed radius is 50000 meters.
        /// </param>
        public GooglePlacesRadarRequest(GeoPoint location, int radius)
        {
            this.location = location;
            this.radius = radius;
        }

        public override void AppendParams(StringBuilder builder)
        {
            if (!string.IsNullOrEmpty(keyword)) builder.Append("&keyword=").Append(keyword);
            if (!string.IsNullOrEmpty(name)) builder.Append("&name=").Append(name);
        }
    }
}