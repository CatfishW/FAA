/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// This class is used to search for a location by address using Bing Maps Location API.<br/>
    /// https://msdn.microsoft.com/en-us/library/ff701715.aspx
    /// </summary>
    public abstract class BingMapsLocationRequestBase : TextWebService<BingMapsLocationResult[]>
    {
        /// <summary>
        /// The API key for Bing Maps.
        /// </summary>
        public string key;

        /// <summary>
        /// Gets the URL token for the request.
        /// </summary>
        protected abstract string urlToken { get; }

        /// <summary>
        /// Generates the URL for the request.
        /// </summary>
        /// <param name="builder">The StringBuilder to append the URL to.</param>
        protected override void GenerateUrl(StringBuilder builder)
        {
            if (string.IsNullOrEmpty(key)) key = KeyManager.BingMaps();
            builder.AppendFormat("https://dev.virtualearth.net/REST/v1/Locations/{0}?key={1}&o=xml", WebRequest.EscapeURL(urlToken), key);
        }

        public static BingMapsLocationResult[] Parse(string response) => BingMapsLocationResult.Parse(response);
        public override BingMapsLocationResult[] ParseResult(string response) => Parse(response);
    }
}