/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// This class is used to search for a location by address.<br/>
    /// https://developers.google.com/maps/documentation/geocoding/intro
    /// </summary>
    public abstract class GoogleGeocodingRequestBase : TextWebService<GoogleGeocodingResult[]>
    {
        /// <summary>
        /// Your application's API key. This key identifies your application for purposes of quota management.
        /// </summary>
        public string key;

        /// <summary>
        /// The language in which to return results. 
        /// List of supported languages:
        /// https://developers.google.com/maps/faq#languagesupport
        /// </summary>
        public string language;

        /// <summary>
        /// Available to Google Maps APIs Premium Plan customers but not to holders of a previous Maps API for Business license.
        /// </summary>
        public string client;

        /// <summary>
        /// Uses instead of an API key to authenticate requests.
        /// </summary>
        public string signature;

        protected override void GenerateUrl(StringBuilder builder)
        {
            builder.Append("https://maps.googleapis.com/maps/api/geocode/xml?sensor=false");

            if (!string.IsNullOrEmpty(key)) builder.Append("&key=").Append(key);
            else if (KeyManager.hasGoogleMaps) builder.Append("&key=").Append(KeyManager.GoogleMaps());

            if (!string.IsNullOrEmpty(language)) builder.Append("&language=").Append(language);
            if (!string.IsNullOrEmpty(client)) builder.Append("&client=").Append(client);
            if (!string.IsNullOrEmpty(signature)) builder.Append("&signature=").Append(signature);
        }

        public static GoogleGeocodingResult[] Parse(string response) => GoogleGeocodingResult.Parse(response);
        public override GoogleGeocodingResult[] ParseResult(string response) => Parse(response);
    }
}