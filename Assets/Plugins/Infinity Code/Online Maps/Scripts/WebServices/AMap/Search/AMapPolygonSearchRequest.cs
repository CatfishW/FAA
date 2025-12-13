using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Polygon search
    /// </summary>
    public class AMapPolygonSearchRequest : AMapSearchRequestBase
    {
        /// <summary>
        /// Query keywords.<br/>
        /// Multiple keywords are separated by "|".
        /// </summary>
        public string keywords;

        /// <summary>
        /// Query POI type.<br/>
        /// Multiple types are separated by "|".
        /// </summary>
        public string types;

        /// <summary>
        /// Each page records data.<br/>
        /// The maximum number of records per page is 25. Out of range Return to the maximum value.
        /// </summary>
        public int? offset;

        /// <summary>
        /// The current page count.
        /// </summary>
        public int? page;

        /// <summary>
        /// Returns the result control.<br/>
        /// This basic default return address information; the value of all the return address information, nearby POI, road and road intersection information.
        /// </summary>
        public string extensions;

        /// <summary>
        /// Digital signature.
        /// </summary>
        public string sig;

        private GeoPoint[] polygon;

        protected override string baseurl => "https://restapi.amap.com/v3/place/around?";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="polygon">Longitude and latitude coordinate pairs.</param>
        public AMapPolygonSearchRequest(GeoPoint[] polygon)
        {
            this.polygon = polygon;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            builder.Append("&location=");
            for (int i = 0; i < polygon.Length; i++)
            {
                if (i != 0) builder.Append(",");
                AppendLocation(builder, polygon[i]);
            }

            if (!string.IsNullOrEmpty(keywords)) builder.Append("&keywords=").Append(keywords);
            if (!string.IsNullOrEmpty(types)) builder.Append("&types=").Append(types);
            if (offset.HasValue) builder.Append("&offset=").Append(offset.Value);
            if (page.HasValue) builder.Append("&page=").Append(page.Value);
            if (!string.IsNullOrEmpty(extensions)) builder.Append("&extensions=").Append(extensions);
            if (!string.IsNullOrEmpty(sig)) builder.Append("&sig=").Append(sig);
        }
    }
}