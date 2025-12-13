using System;
using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Keyword search
    /// </summary>
    public class AMapTextSearchRequest : AMapSearchRequestBase
    {
        /// <summary>
        /// Query keywords.<br/>
        /// Multiple keywords are separated by "|"
        /// </summary>
        public string keywords;

        /// <summary>
        /// Query POI type.<br/>
        /// Multiple types are separated by "|".<br/>
        /// https://a.amap.com/lbs/static/zip/AMap_API_Table.zip
        /// </summary>
        public string types;

        /// <summary>
        /// Check the city.<br/>
        /// Optional values: city Chinese, Chinese spelling, citycode, adcode.<br/>
        /// Such as: Beijing / beijing / 010/110000
        /// </summary>
        public string city;

        /// <summary>
        /// Returns only the specified city data.
        /// </summary>
        public bool citylimit = false;

        /// <summary>
        /// Whether the sub-POI data is displayed by hierarchy.
        /// </summary>
        public bool children = false;

        /// <summary>
        /// Each page records data.<br/>
        /// It is strongly recommended not to exceed 25, if more than 25 may cause access error.
        /// </summary>
        public int? offset;

        /// <summary>
        /// The current page count.
        /// </summary>
        public int? page;

        /// <summary>
        /// POI number of the building.<br/>
        /// After building POI is introduced, only in the building within the search.
        /// </summary>
        public string building;

        /// <summary>
        /// Search for floors.<br/>
        /// Returns the keyword search results for the current floor in the building if the building id + floor is passed in.<br/>
        /// If only the floor, the return parameters incomplete advice.<br/>
        /// If the building id + floor, the floor does not have the corresponding search results, will return to the contents of the building.
        /// </summary>
        public int? floor;

        /// <summary>
        /// Returns the result control.<br/>
        /// This item returns the basic address information by default; the value returns all address information, nearby POIs, roads, and road intersections.
        /// </summary>
        public string extensions;

        /// <summary>
        /// Digital signature
        /// </summary>
        public string sig;

        protected override string baseurl => "https://restapi.amap.com/v3/place/text?";

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            bool hasKeywords = false, hasTypes = false;
            if (!string.IsNullOrEmpty(keywords))
            {
                builder.Append("&keywords=").Append(keywords);
                hasKeywords = true;
            }

            if (!string.IsNullOrEmpty(types))
            {
                builder.Append("&types=").Append(types);
                hasTypes = true;
            }

            if (!hasKeywords && !hasTypes)
            {
                throw new Exception("You must specify the keywords or types.");
            }

            if (!string.IsNullOrEmpty(city)) builder.Append("&city=").Append(city);
            if (citylimit) builder.Append("&citylimit=true");
            if (children) builder.Append("&children=true");
            if (offset.HasValue) builder.Append("&offset=").Append(offset.Value);
            if (page.HasValue) builder.Append("&page=").Append(page.Value);
            if (!string.IsNullOrEmpty(building)) builder.Append("&building=").Append(building);
            if (floor.HasValue) builder.Append("&floor=").Append(floor.Value);
            if (!string.IsNullOrEmpty(extensions)) builder.Append("&extensions=").Append(extensions);
            if (!string.IsNullOrEmpty(sig)) builder.Append("&sig=").Append(sig);
        }
    }
}