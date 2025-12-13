/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Retrieves information from Google Maps Place Autocomplete API.<br/>
    /// Place Autocomplete service is a web service that returns place predictions.<br/>
    /// The request specifies a textual search string and optional geographic bounds.<br/>
    /// The service can be used to provide autocomplete functionality for text-based geographic searches, by returning places such as businesses, addresses and points of interest as a user types.<br/>
    /// Requires Google Maps API key.<br/>
    /// https://developers.google.com/places/documentation/autocomplete
    /// </summary>
    public class GooglePlacesAutocompleteRequest: TextWebService<GooglePlacesAutocompleteResult[]>
    {
        /// <summary>
        /// Your application's API key. This key identifies your application for purposes of quota management. <br/>
        /// Visit the Google APIs Console to select an API Project and obtain your key. <br/>
        /// If empty, the key will be taken from the Key Manager.
        /// </summary>
        public string key;

        /// <summary>
        /// The text string on which to search.<br/>
        /// The Place Autocomplete service will return candidate matches based on this string and order results based on their perceived relevance.
        /// </summary>
        public string input;

        /// <summary>
        /// A random string which identifies an autocomplete session for billing purposes. If this parameter is omitted from an autocomplete request, the request is billed independently.
        /// </summary>
        public string sessionToken;

        /// <summary>
        /// The types of place results to return.
        /// </summary>
        public string types;

        /// <summary>
        /// The point around which you wish to retrieve place information.
        /// </summary>
        public GeoPoint? location;

        /// <summary>
        /// The position, in the input term, of the last character that the service uses to match predictions.<br/>
        /// For example, if the input is 'Google' and the offset is 3, the service will match on 'Goo'.<br/>
        /// The string determined by the offset is matched against the first word in the input term only.<br/>
        /// For example, if the input term is 'Google abc' and the offset is 3, the service will attempt to match against 'Goo abc'.<br/>
        /// If no offset is supplied, the service will use the whole term.<br/>
        /// The offset should generally be set to the position of the text caret.
        /// </summary>
        public int offset = -1;

        /// <summary>
        /// The distance (in meters) within which to return place results. <br/>
        /// Note that setting a radius biases results to the indicated area, but may not fully restrict results to the specified area.
        /// </summary>
        public int radius = -1;

        /// <summary>
        /// The language in which to return results.
        /// </summary>
        public string language;

        /// <summary>
        /// A grouping of places to which you would like to restrict your results. <br/>
        /// Currently, you can use components to filter by country. <br/>
        /// The country must be passed as a two character, ISO 3166-1 Alpha-2 compatible country code. <br/>
        /// For example: components=country:fr would restrict your results to places within France.
        /// </summary>
        public string components;
        
        public GooglePlacesAutocompleteRequest(string input)
        {
            this.input = input;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            builder.Append("https://maps.googleapis.com/maps/api/place/autocomplete/xml?sensor=false");
            builder.Append("&input=").Append(WebRequest.EscapeURL(input));
            
            if (!string.IsNullOrEmpty(key)) builder.Append("&key=").Append(key);
            else if (KeyManager.hasGoogleMaps) builder.Append("&key=").Append(KeyManager.GoogleMaps());
            else throw new Exception("Please specify Google API Key");

            if (!string.IsNullOrEmpty(sessionToken)) builder.Append("&sessiontoken=").Append(sessionToken);
            if (location.HasValue)
            {
                builder.Append("&location=");
                AppendLocation(builder, location.Value);
            }
            if (radius != -1) builder.Append("&radius=").Append(radius);
            if (offset != -1) builder.Append("&offset=").Append(offset);
            if (!string.IsNullOrEmpty(types)) builder.Append("&types=").Append(types);
            if (!string.IsNullOrEmpty(components)) builder.Append("&components=").Append(components);
            if (!string.IsNullOrEmpty(language)) builder.Append("&language=").Append(language);
        }

        public static GooglePlacesAutocompleteResult[] Parse(string response) => GooglePlacesAutocompleteResult.Parse(response);
        public override GooglePlacesAutocompleteResult[] ParseResult(string response) => Parse(response);
    }
}