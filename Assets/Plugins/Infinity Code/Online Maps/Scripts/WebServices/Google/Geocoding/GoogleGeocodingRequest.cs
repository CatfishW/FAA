/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Request parameters for Geocoding
    /// </summary>
    public class GoogleGeocodingRequest : GoogleGeocodingRequestBase
    {
        /// <summary>
        /// The street address that you want to geocode, in the format used by the national postal service of the country concerned.<br/>
        /// Additional address elements such as business names and unit, suite or floor numbers should be avoided.
        /// </summary>
        public string address;

        /// <summary>
        /// A component filter for which you wish to obtain a geocode.<br/>
        /// See Component Filtering for more information. <br/>
        /// https://developers.google.com/maps/documentation/geocoding/intro?hl=en#ComponentFiltering <br/>
        /// The components filter will also be accepted as an optional parameter if an address is provided. 
        /// </summary>
        public string components;

        /// <summary>
        /// The bounding box of the viewport within which to bias geocode results more prominently. <br/>
        /// This parameter will only influence, not fully restrict, results from the geocoder.
        /// </summary>
        public GeoRect? bounds;

        /// <summary>
        /// The region code, specified as a ccTLD ("top-level domain") two-character value. <br/>
        /// This parameter will only influence, not fully restrict, results from the geocoder.
        /// </summary>
        public string region;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="address">
        /// The street address that you want to geocode, in the format used by the national postal service of the country concerned. <br/>
        /// Additional address elements such as business names and unit, suite or floor numbers should be avoided.
        /// </param>
        public GoogleGeocodingRequest(string address)
        {
            this.address = address;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            if (!string.IsNullOrEmpty(address)) builder.Append("&address=").Append(WebRequest.EscapeURL(address));
            if (!string.IsNullOrEmpty(components)) builder.Append("&components=").Append(components);
            if (bounds.HasValue)
            {
                GeoRect b = bounds.Value;
                builder.Append("&bounds=");
                AppendLocation(builder, b.bottomLeft);
                builder.Append("|");
                AppendLocation(builder, b.topRight);
            }

            if (!string.IsNullOrEmpty(region)) builder.Append("&region=").Append(region);
        }
    }
}