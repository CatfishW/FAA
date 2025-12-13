/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    public class OSMNominatimReverseGeocodingRequest: OSMNominatimRequestBase
    {
        public GeoPoint location;
        public string language = "en";
        public bool addressdetails = true;

        public OSMNominatimReverseGeocodingRequest(GeoPoint location)
        {
            this.location = location;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            builder.Append("https://nominatim.openstreetmap.org/reverse?format=xml&lat=");
            builder.Append(location.y.ToString(Culture.numberFormat)).Append("&lon=").Append(location.x.ToString(Culture.numberFormat));
            if (addressdetails) builder.Append("&addressdetails=1");
            if (!string.IsNullOrEmpty(language)) builder.Append("&accept-language=").Append(language);
        }
    }
}