/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// A Place Details request returns more comprehensive information about the indicated place such as its complete address, phone number, user rating and reviews.<br/>
    /// Requires Google Maps API key.<br/>
    /// https://developers.google.com/places/webservice/details
    /// </summary>
    public class GooglePlaceDetailsRequest : TextWebService<GooglePlaceDetailsResult>
    {
        /// <summary>
        /// Your application's API key. <br/>
        /// This key identifies your application for purposes of quota management and so that places added from your application are made immediately available to your app.<br/>
        /// Visit the Google Developers Console to create an API Project and obtain your key. <br/>
        /// If null, the value will be taken from the Key Manager.
        /// </summary>
        public string key;

        /// <summary>
        /// A textual identifier that uniquely identifies a place, returned from a Place Search.
        /// </summary>
        public string place_id;

        /// <summary>
        /// The language code, indicating in which language the results should be returned, if possible. <br/>
        /// Note that some fields may not be available in the requested language. See the list of supported languages and their codes. <br/>
        /// Note that we often update supported languages so this list may not be exhaustive.
        /// </summary>
        public string language;

        /// <summary>
        /// The region code, specified as a ccTLD (country code top-level domain) two-character value. <br/>
        /// Most ccTLD codes are identical to ISO 3166-1 codes, with some exceptions. <br/>
        /// This parameter will only influence, not fully restrict, results. <br/>
        /// If more relevant results exist outside of the specified region, they may be included. <br/>
        /// When this parameter is used, the country name is omitted from the resulting formatted_address for results in the specified region.
        /// </summary>
        public string region;

        /// <summary>
        /// A random string which identifies an autocomplete session for billing purposes. <br/>
        /// Use this for Place Details requests that are called following an autocomplete request in the same user session.
        /// </summary>
        public string sessiontoken;

        /// <summary>
        /// One or more fields, specifying the types of place data to return, separated by a comma.
        /// </summary>
        public string fields;

        protected override void GenerateUrl(StringBuilder builder)
        {
            builder.Append("https://maps.googleapis.com/maps/api/place/details/xml?sensor=false");
            
            if (!string.IsNullOrEmpty(key)) builder.Append("&key=").Append(key);
            else if (KeyManager.hasGoogleMaps) builder.Append("&key=").Append(KeyManager.GoogleMaps());
            else throw new Exception("Please specify Google API Key");
            
            if (!string.IsNullOrEmpty(place_id)) builder.Append("&placeid=").Append(place_id);
            if (!string.IsNullOrEmpty(language)) builder.Append("&language=").Append(language);
            if (!string.IsNullOrEmpty(region)) builder.Append("&region=").Append(region);
            if (!string.IsNullOrEmpty(sessiontoken)) builder.Append("&sessiontoken=").Append(sessiontoken);
            if (!string.IsNullOrEmpty(fields)) builder.Append("&fields=").Append(fields);
        }

        public static GooglePlaceDetailsResult Parse(string response) => GooglePlaceDetailsResult.Parse(response);
        public override GooglePlaceDetailsResult ParseResult(string response) => Parse(response);
    }
}