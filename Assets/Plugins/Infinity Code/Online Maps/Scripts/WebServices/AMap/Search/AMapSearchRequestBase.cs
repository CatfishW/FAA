using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// AMap Search provides many kinds of querying POI information, including keyword search, peripheral search, polygon search and ID query.<br/>
    /// https://lbs.amap.com/api/webservice/guide/api/search/#introduce
    /// </summary>
    public abstract class AMapSearchRequestBase : TextWebService<AMapSearchResult>
    {
        /// <summary>
        /// API key for AMap services.
        /// </summary>
        public string key;


        /// <summary>
        /// The base URL for the AMap API request.
        /// </summary>
        protected abstract string baseurl { get; }

        protected override void GenerateUrl(StringBuilder builder)
        {
            if (string.IsNullOrEmpty(key)) key = KeyManager.AMap();
            builder.Append(baseurl).Append("key=").Append(key).Append("&output=JSON");
        }

        public static AMapSearchResult Parse(string response) => AMapSearchResult.Parse(response);
        public override AMapSearchResult ParseResult(string response) => Parse(response);
    }
}