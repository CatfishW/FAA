/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// This class is used to request to Open Street Map Overpass API.<br/>
    /// You can create a new instance using OSMOverpass.Find.<br/>
    /// Open Street Map Overpass API documentation: https://wiki.openstreetmap.org/wiki/Overpass_API/Language_Guide <br/>
    /// You can test your queries using: https://overpass-turbo.eu/ 
    /// </summary>
    public class OSMOverpassRequest : TextWebService<OSMOverpassResult>
    {
        private static string osmURL = "https://overpass-api.de/api/interpreter?data=";
        
        public readonly string data;

        public OSMOverpassRequest(string data)
        {
            this.data = data;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            builder.Append(osmURL).Append(WebRequest.EscapeURL(data));
        }

        public static OSMOverpassResult Parse(string response) => OSMOverpassResult.Parse(response);
        public override OSMOverpassResult ParseResult(string response) => Parse(response);

        public static void InitOSMServer(OSMOverpassServer server)
        {
            if (server == OSMOverpassServer.main) osmURL = "https://overpass-api.de/api/interpreter?data=";
            else if (server == OSMOverpassServer.main2) osmURL = "https://z.overpass-api.de/api/interpreter?data=";
            else if (server == OSMOverpassServer.french) osmURL = "https://overpass.openstreetmap.fr/api/interpreter?data=";
            else if (server == OSMOverpassServer.taiwan) osmURL = "https://overpass.nchc.org.tw/api/interpreter?data=";
            else if (server == OSMOverpassServer.kumiSystems) osmURL = "https://overpass.kumi.systems/api/interpreter?data=";
        }
    }
}