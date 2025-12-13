/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Returns a JSON formatted list of objects corresponding to the search input from Open Route Service Geocode.<br/>
    /// https://openrouteservice.org/dev/#/api-docs/geocode
    /// </summary>
    public abstract class ORSGeocodeRequestBase: TextWebService<ORSGeocodingResult>
    {
        private const string endpoint = "https://api.openrouteservice.org/geocode/";
            
        /// <summary>
        /// Open Route Service API key. If empty, the value from the Key Manager will be used.
        /// </summary>
        public string apiKey;

        /// <summary>
        /// Restrict your search to specific sources. Searches all sources by default. You can either use the normal or short name.
        /// </summary>
        public string sources;

        /// <summary>
        /// Restrict search to layers (place type). By default, all layers are searched.
        /// </summary>
        public string layers;

        /// <summary>
        /// ISO-3166 country code to narrow results.
        /// </summary>
        public string boundaryCountry;

        /// <summary>
        /// Restrict results to administrative boundary using a Pelias global id gid. gids for records can be found using either the Who’s on First Spelunker, a tool for searching Who’s on First data, or from the responses of other Pelias queries.
        /// </summary>
        public string boundaryGid;

        /// <summary>
        /// Number of returned results. By default, returns up to 10 results.
        /// </summary>
        public int? size;

        protected abstract string service { get; }
            
        protected override void GenerateUrl(StringBuilder builder)
        {
            builder.Append(endpoint).Append(service).Append("?");
                
            builder.Append("api_key=");
            if (!string.IsNullOrEmpty(apiKey)) builder.Append(apiKey);
            else if (KeyManager.hasOpenRouteService) builder.Append(KeyManager.OpenRouteService());
            else throw new Exception("Open Route Service API key is not specified.");
                
            if (!string.IsNullOrEmpty(sources)) builder.Append("&sources=").Append(sources);
            if (!string.IsNullOrEmpty(layers)) builder.Append("&layers=").Append(layers);
            if (!string.IsNullOrEmpty(boundaryCountry)) builder.Append("&boundary.country=").Append(boundaryCountry);
            if (size.HasValue) builder.Append("&size=").Append(size.Value);
            if (!string.IsNullOrEmpty(boundaryGid)) builder.Append("&boundary.gid=").Append(boundaryGid);
        }

        public static ORSGeocodingResult Parse(string response) => ORSGeocodingResult.Parse(response);
        public override ORSGeocodingResult ParseResult(string response) => Parse(response);
    }
}