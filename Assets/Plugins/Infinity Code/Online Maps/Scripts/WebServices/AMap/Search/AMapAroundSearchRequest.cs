using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Peripheral search
    /// </summary>
    public class AMapAroundSearchRequest : AMapSearchRequestBase
    {
        /// <summary>
        /// Query keywords.<br/>
        /// Multiple keywords are separated by "|".
        /// </summary>
        public string keywords;

        /// <summary>
        /// Query the POI type.<br/>
        /// Multiple keywords are separated by "|".<br/>
        /// https://a.amap.com/lbs/static/zip/AMap_API_Table.zip
        /// </summary>
        public string types;

        /// <summary>
        /// Check the city.<br/>
        /// Optional values: city Chinese, Chinese spelling, citycode, adcode<br/>
        /// Such as: Beijing / beijing / 010/110000
        /// </summary>
        public string city;

        /// <summary>
        ///  The radius of the query.<br/>
        /// The value ranges from 0 to 50000, in meters.
        /// </summary>
        public int? raduis;

        /// <summary>
        /// Collation.
        /// </summary>
        public string sortrule;

        /// <summary>
        /// Each page records data.<br/>
        /// The maximum number of records per page is 25. Out of range The maximum value is returned.
        /// </summary>
        public int? offset;

        /// <summary>
        /// The current page count.
        /// </summary>
        public int? page;

        /// <summary>
        /// Returns the result control.<br/>
        /// This item returns the basic address information by default; the value returns all address information, nearby POIs, roads, and road intersections.
        /// </summary>
        public string extensions;

        /// <summary>
        /// Digital signature
        /// </summary>
        public string sig;

        private GeoPoint location;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="location">Longitude and latitude coordinate pairs.</param>
        public AMapAroundSearchRequest(GeoPoint location)
        {
            this.location = location;
        }

        protected override string baseurl => "https://restapi.amap.com/v3/place/around?";

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            builder.Append("&location=")
                .Append(location.latitude.ToString("F6", Culture.numberFormat)).Append(",")
                .Append(location.longitude.ToString("F6", Culture.numberFormat));

            if (!string.IsNullOrEmpty(keywords)) builder.Append("&keywords=").Append(keywords);
            if (!string.IsNullOrEmpty(types)) builder.Append("&types=").Append(types);
            if (raduis.HasValue) builder.Append("&raduis=").Append(raduis.Value);
            if (!string.IsNullOrEmpty(sortrule)) builder.Append("&sortrule=").Append(sortrule);
            if (!string.IsNullOrEmpty(city)) builder.Append("&city=").Append(city);
            if (offset.HasValue) builder.Append("&offset=").Append(offset.Value);
            if (page.HasValue) builder.Append("&page=").Append(page.Value);
            if (!string.IsNullOrEmpty(extensions)) builder.Append("&extensions=").Append(extensions);
            if (!string.IsNullOrEmpty(sig)) builder.Append("&sig=").Append(sig);
        }
    }
}