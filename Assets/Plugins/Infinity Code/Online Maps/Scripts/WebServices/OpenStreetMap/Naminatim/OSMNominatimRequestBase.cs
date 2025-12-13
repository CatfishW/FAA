/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// This class is used to search OSM data by name and address and to generate synthetic addresses of OSM points (reverse geocoding).<br/>
    /// https://wiki.openstreetmap.org/wiki/Nominatim
    ///  </summary>
    public abstract class OSMNominatimRequestBase: TextWebService<OSMNominatimResult[]>
    {
        public static OSMNominatimResult[] Parse(string response) => OSMNominatimResult.Parse(response);
        public override OSMNominatimResult[] ParseResult(string response) => Parse(response);
    }
}