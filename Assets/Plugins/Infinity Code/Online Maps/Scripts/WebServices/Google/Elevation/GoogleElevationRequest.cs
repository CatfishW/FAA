/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// The Elevation API provides elevation data for all locations on the surface of the earth, including depth locations on the ocean floor (which return negative values).<br/>
    /// In those cases where Google does not possess exact elevation measurements at the precise location you request, the service will interpolate and return an averaged value using the four nearest locations.<br/>
    /// With the Elevation API, you can develop hiking and biking applications, mobile positioning applications, or low resolution surveying applications. <br/>
    /// https://developers.google.com/maps/documentation/elevation/
    /// </summary>
    public class GoogleElevationRequest : TextWebService<GoogleElevationResult[]>
    {
        /// <summary>
        /// API key for accessing the Google Elevation API.
        /// </summary>
        public string key;

        /// <summary>
        /// Client ID for accessing the Google Elevation API.
        /// </summary>
        public string client;

        /// <summary>
        /// Signature for accessing the Google Elevation API.
        /// </summary>
        public string signature;

        /// <summary>
        /// Array of geographical points for which to retrieve elevation data.
        /// </summary>
        public readonly GeoPoint[] locations;

        /// <summary>
        /// Array of geographical points defining a path for which to retrieve elevation data.
        /// </summary>
        public readonly GeoPoint[] path;

        /// <summary>
        /// Number of samples to retrieve along the specified path.
        /// </summary>
        public readonly int samples;

        /// <summary>
        /// Initializes a new instance of the GoogleElevationRequest class with a single location.
        /// </summary>
        /// <param name="location">The geographical point for which to retrieve elevation data.</param>
        public GoogleElevationRequest(GeoPoint location)
        {
            locations = new[]
            {
                location
            };
        }

        /// <summary>
        /// Initializes a new instance of the GoogleElevationRequest class with multiple locations.
        /// </summary>
        /// <param name="locations">An array of geographical points for which to retrieve elevation data.</param>
        public GoogleElevationRequest(GeoPoint[] locations)
        {
            this.locations = locations;
        }

        /// <summary>
        /// Initializes a new instance of the GoogleElevationRequest class with a path and number of samples.
        /// </summary>
        /// <param name="path">An array of geographical points defining a path for which to retrieve elevation data.</param>
        /// <param name="samples">The number of samples to retrieve along the specified path.</param>
        public GoogleElevationRequest(GeoPoint[] path, int samples)
        {
            this.path = path;
            this.samples = samples;
        }

        private void AppendLocations(StringBuilder builder)
        {
            builder.Append("&locations=");
            for (int i = 0; i < locations.Length; i++)
            {
                if (i > 0) builder.Append("|");
                AppendLocation(builder, locations[i]);
            }
        }

        private void AppendPath(StringBuilder builder)
        {
            builder.Append("&path=");
            for (int i = 0; i < path.Length; i++)
            {
                if (i > 0) builder.Append("|");
                AppendLocation(builder, path[i]);
            }

            builder.Append("&samples=").Append(samples);
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            builder.Append("https://maps.googleapis.com/maps/api/elevation/xml?sensor=false");

            if (!string.IsNullOrEmpty(key)) builder.Append("&key=").Append(key);
            else if (KeyManager.hasGoogleMaps) builder.Append("&key=").Append(KeyManager.GoogleMaps());

            if (!string.IsNullOrEmpty(client)) builder.Append("&client=").Append(client);
            if (!string.IsNullOrEmpty(signature)) builder.Append("&signature=").Append(signature);

            if (locations != null) AppendLocations(builder);
            else if (path != null) AppendPath(builder);
        }

        public static GoogleElevationResult[] Parse(string response) => GoogleElevationResult.Parse(response);
        public override GoogleElevationResult[] ParseResult(string response) => Parse(response);
    }
}