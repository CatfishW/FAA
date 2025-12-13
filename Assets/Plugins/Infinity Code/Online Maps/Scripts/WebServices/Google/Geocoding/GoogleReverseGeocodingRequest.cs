/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Request parameters for Reverse Geocoding
    /// </summary>
    public class GoogleReverseGeocodingRequest : GoogleGeocodingRequestBase
    {
        /// <summary>
        /// The geographical point specifying the location for which you wish to obtain the closest, human-readable address.
        /// </summary>
        public GeoPoint? location;

        /// <summary>
        /// The place ID of the place for which you wish to obtain the human-readable address. <br/>
        /// The place ID is a unique identifier that can be used with other Google APIs. <br/>
        /// For example, you can use the placeID returned by the Google Maps Roads API to get the address for a snapped point. <br/>
        /// For more information about place IDs, see the place ID overview. <br/>
        /// The place ID may only be specified if the request includes an API key or a Google Maps APIs Premium Plan client ID. 
        /// </summary>
        public string placeId;

        /// <summary>
        /// One or more address types, separated by a pipe (|). <br/>
        /// Examples of address types: country, street_address, postal_code. <br/>
        /// For a full list of allowable values, see the address types on this page:<br/>
        /// https://developers.google.com/maps/documentation/geocoding/intro?hl=en#Types <br/>
        /// Specifying a type will restrict the results to this type. <br/>
        /// If multiple types are specified, the API will return all addresses that match any of the types. <br/>
        /// Note: This parameter is available only for requests that include an API key or a client ID.
        /// </summary>
        public string result_type;

        /// <summary>
        /// One or more location types, separated by a pipe (|). <br/>
        /// https://developers.google.com/maps/documentation/geocoding/intro?hl=en#ReverseGeocoding <br/>
        /// Specifying a type will restrict the results to this type. <br/>
        /// If multiple types are specified, the API will return all addresses that match any of the types. <br/>
        /// Note: This parameter is available only for requests that include an API key or a client ID.
        /// </summary>
        public string location_type;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="longitude">The longitude value specifying the location for which you wish to obtain the closest, human-readable address. </param>
        /// <param name="latitude">The latitude value specifying the location for which you wish to obtain the closest, human-readable address. </param>
        public GoogleReverseGeocodingRequest(double longitude, double latitude)
        {
            location = new GeoPoint(longitude, latitude);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="location">The longitude and latitude values specifying the location for which you wish to obtain the closest, human-readable address. </param>
        public GoogleReverseGeocodingRequest(GeoPoint location)
        {
            this.location = location;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="placeId">The place ID of the place for which you wish to obtain the human-readable address. <br/>
        /// The place ID is a unique identifier that can be used with other Google APIs. <br/>
        /// For example, you can use the placeID returned by the Google Maps Roads API to get the address for a snapped point. <br/>
        /// For more information about place IDs, see the place ID overview. <br/>
        /// The place ID may only be specified if the request includes an API key or a Google Maps APIs Premium Plan client ID. 
        /// </param>
        public GoogleReverseGeocodingRequest(string placeId)
        {
            this.placeId = placeId;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            if (location.HasValue)
            {
                builder.Append("&latlng=");
                AppendLocation(builder, location.Value);
            }
            else if (!string.IsNullOrEmpty(placeId)) builder.Append("&placeId=").Append(placeId);
            else throw new Exception("You must specify latitude and longitude, location, or placeId.");

            if (!string.IsNullOrEmpty(result_type)) builder.Append("&result_type=").Append(result_type);
            if (!string.IsNullOrEmpty(location_type)) builder.Append("&location_type=").Append(location_type);
        }
    }
}