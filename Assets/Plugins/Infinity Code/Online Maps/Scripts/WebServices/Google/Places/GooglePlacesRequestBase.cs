/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// The Google Places API allows you to query for place information on a variety of categories, such as: establishments, prominent points of interest, geographic locations, and more. <br/>
    /// You can search for places either by proximity or a text string. <br/>
    /// A Place Search returns a list of places along with summary information about each place.<br/>
    /// https://developers.google.com/places/web-service/search
    /// </summary>
    public abstract class GooglePlacesRequestBase : TextWebService<GooglePlacesResult[]>
    {
        public string key;
        public GeoPoint? location;

        /// <summary>
        /// Defines the distance (in meters) within which to return place results. <br/>
        /// The maximum allowed radius is 50000 meters.
        /// </summary>
        public int? radius;

        /// <summary>
        /// Restricts the results to places matching at least one of the specified types. <br/>
        /// Types should be separated with a pipe symbol (type1|type2|etc). <br/>
        /// See the list of supported types:<br/>
        /// https://developers.google.com/maps/documentation/places/supported_types
        /// </summary>
        public string types;

        /// <summary>
        /// Restricts results to only those places within the specified price level. <br/>
        /// Valid values are in the range from 0 (most affordable) to 4 (most expensive), inclusive. <br/>
        /// The exact amount indicated by a specific value will vary from region to region.
        /// </summary>
        public int? minprice;

        /// <summary>
        /// Restricts results to only those places within the specified price level. <br/>
        /// Valid values are in the range from 0 (most affordable) to 4 (most expensive), inclusive. <br/>
        /// The exact amount indicated by a specific value will vary from region to region.
        /// </summary>
        public int? maxprice;

        /// <summary>
        /// Returns only those places that are open for business at the time the query is sent. <br/>
        /// Places that do not specify opening hours in the Google Places database will not be returned if you include this parameter in your query.
        /// </summary>
        public bool? opennow;

        /// <summary>
        /// Add this parameter (just the parameter name, with no associated value) to restrict your search to locations that are Zagat selected businesses.<br/>
        /// This parameter must not include a true or false value. The zagatselected parameter is experimental, and is only available to Google Places API customers with a Premium Plan license.
        /// </summary>
        public bool? zagatselected;

        /// <summary>
        /// Returns the next 20 results from a previously run search. <br/>
        /// Setting a pagetoken parameter will execute a search with the same parameters used previously — all parameters other than pagetoken will be ignored. 
        /// </summary>
        public string pagetoken;

        public abstract string typePath { get; }
        public abstract void AppendParams(StringBuilder builder);

        protected override void GenerateUrl(StringBuilder builder)
        {
            builder.Append("https://maps.googleapis.com/maps/api/place/").Append(typePath).Append("/xml?sensor=false");
            if (!string.IsNullOrEmpty(key)) builder.Append("&key=").Append(key);
            else if (KeyManager.hasGoogleMaps) builder.Append("&key=").Append(KeyManager.GoogleMaps());
            else throw new Exception("Google Places key is not set");

            if (location.HasValue)
            {
                builder.Append("&location=");
                AppendLocation(builder, location.Value);
            }

            if (radius.HasValue) builder.Append("&radius=").Append(radius.Value);
            if (!string.IsNullOrEmpty(types)) builder.Append("&types=").Append(types);
            if (minprice.HasValue) builder.Append("&minprice=").Append(minprice.Value);
            if (maxprice.HasValue) builder.Append("&maxprice=").Append(maxprice.Value);
            if (opennow.HasValue) builder.Append("&opennow");
            if (zagatselected.HasValue && zagatselected.Value) builder.Append("&zagatselected");
            if (!string.IsNullOrEmpty(pagetoken)) builder.Append("&pagetoken=").Append(WebRequest.EscapeURL(pagetoken));

            AppendParams(builder);
        }

        public static GooglePlacesResult[] Parse(string response) => GooglePlacesResult.Parse(response);
        public override GooglePlacesResult[] ParseResult(string response) => Parse(response);

        /// <summary>
        /// Specifies the order in which results are listed.
        /// </summary>
        public enum RankBy
        {
            /// <summary>
            /// This option sorts results based on their importance. <br/>
            /// Ranking will favor prominent places within the specified area. <br/>
            /// Prominence can be affected by a place's ranking in Google's index, global popularity, and other factors. 
            /// </summary>
            prominence,

            /// <summary>
            /// This option sorts results in ascending order by their distance from the specified location. <br/>
            /// When distance is specified, one or more of keyword, name, or types is required.
            /// </summary>
            distance
        }
    }
}