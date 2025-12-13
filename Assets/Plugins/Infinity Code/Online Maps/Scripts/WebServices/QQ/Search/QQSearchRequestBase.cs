/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Class to work with QQ Search.<br/>
    /// https://lbs.qq.com/webservice_v1/guide-search.html
    /// </summary>
    public abstract class QQSearchRequestBase: TextWebService<QQSearchResult>
    {
        public string key;
            
        /// <summary>
        /// Search for keywords with a maximum length of 96 bytes. Note: Only one keyword can be retrieved.<br/>
        /// (The API adopts UTF-8 character encoding, 1 English character occupies 1 byte, and 1 Chinese character occupies 3 bytes. For details, please refer to the relevant technical information)
        /// </summary>
        public string keyword;
            
        /// <summary>
        /// Filter criteria.<br/>
        /// https://lbs.qq.com/webservice_v1/guide-search.html#filter_detail
        /// </summary>
        public string filter;
            
        /// <summary>
        /// Returns the specified standard additional field, the value supports: category_code-poi classification code
        /// </summary>
        public string addedFields;
            
        /// <summary>
        /// Whether to return to sub-locations, such as building parking lots, entrances and exits, etc.
        /// </summary>
        public bool getSubPois;

        /// <summary>
        /// Sort by.<br/>
        /// https://lbs.qq.com/webservice_v1/guide-search.html#orderby_detail
        /// </summary>
        public string orderby;

        /// <summary>
        /// The maximum number of entries per page is 20.
        /// </summary>
        public int? page_size;

        /// <summary>
        /// Page x, default page 1
        /// </summary>
        public int? page_index;
            
        protected virtual string service => "search";
            
        protected override void GenerateUrl(StringBuilder builder)
        {
            builder.Append("https://apis.map.qq.com/ws/place/v1/");
            builder.Append(service);
            builder.Append("?key=");
                
            if (!string.IsNullOrEmpty(key)) builder.Append(key);
            else if (KeyManager.hasQQ) builder.Append(KeyManager.QQ());
            else throw new Exception("Please specify QQ API Key");
                
            builder.Append("&keyword=").Append(WebRequest.EscapeURL(keyword));
            if (!string.IsNullOrEmpty(filter)) builder.Append("&filter=").Append(WebRequest.EscapeURL(filter));
            if (!string.IsNullOrEmpty(addedFields)) builder.Append("&addedFields=").Append(WebRequest.EscapeURL(addedFields));
            if (!string.IsNullOrEmpty(orderby)) builder.Append("&orderby=").Append(WebRequest.EscapeURL(orderby));
            if (getSubPois) builder.Append("&get_subpois=1");
            if (page_size.HasValue) builder.Append("&page_size=").Append(page_size);
            if (page_index.HasValue) builder.Append("&page_index=").Append(page_index);
            builder.Append("&output=json");
        }

        public static QQSearchResult Parse(string response) => QQSearchResult.Parse(response);
        public override QQSearchResult ParseResult(string response) => Parse(response);
    }
}