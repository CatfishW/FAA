/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// AMap search response object.<br/>
    /// Note: Descriptions of the fields are translated from Chinese using Google Translate and can be translated incorrectly. <br/>
    /// If you confused in the description of field, please read the official AMap documentation.<br/>
    /// https://lbs.amap.com/api/webservice/guide/api/search/#introduce
    /// </summary>
    public class AMapSearchResult
    {
        /// <summary>
        /// The resulting status value. 0: Request failed, 1: The request was successful.
        /// </summary>
        public int status;

        /// <summary>
        /// Returns the status description.<br/>
        /// When status is 0, info returns the cause of the error, otherwise it returns "OK".<br/>
        /// https://lbs.amap.com/api/webservice/info/
        /// </summary>
        public string info;

        /// <summary>
        /// Returns the status code information.
        /// </summary>
        public string infocode;

        /// <summary>
        /// Number of search items (maximum 1000).
        /// </summary>
        public int count;

        /// <summary>
        /// Search POI information array.
        /// </summary>
        public POI[] pois;

        /// <summary>
        /// Suggestion information for the search result.
        /// </summary>
        public Suggestion suggestion;
        
        public static AMapSearchResult Parse(string response)
        {
            return JSON.Deserialize<AMapSearchResult>(response);
        }

        /// <summary>
        /// POI information.
        /// </summary>
        public class POI
        {
            /// <summary>
            /// The unique ID.
            /// </summary>
            public string id;

            /// <summary>
            /// The name of the POI.
            /// </summary>
            public string name;

            /// <summary>
            /// Tag associated with the POI.
            /// </summary>
            public string tag;

            /// <summary>
            /// Points of Interest.
            /// </summary>
            public string type;

            /// <summary>
            /// Points of interest type encoding.
            /// </summary>
            public string typecode;

            /// <summary>
            /// Career type.
            /// </summary>
            public string biz_type;

            /// <summary>
            /// Address.
            /// </summary>
            public string address;

            /// <summary>
            /// Latitude and longitude
            /// </summary>
            public string location;

            /// <summary>
            /// Phone
            /// </summary>
            public string tel;

            /// <summary>
            /// The postcode of the POI.
            /// </summary>
            public string postcode;

            /// <summary>
            /// The website of the POI.
            /// </summary>
            public string website;

            /// <summary>
            /// The province of POI the code.<br/>
            /// The following data is a list of poi details, extensions = all to return; extensions = base does not return.
            /// </summary>
            public string pcode;

            /// <summary>
            /// The name of POI province.
            /// </summary>
            public string pname;

            /// <summary>
            /// City code.
            /// </summary>
            public string citycode;

            /// <summary>
            /// City name.
            /// </summary>
            public string cityname;

            /// <summary>
            /// Area encoding.
            /// </summary>
            public string adcode;

            /// <summary>
            /// Area name.
            /// </summary>
            public string adname;

            /// <summary>
            /// Importance of the POI.
            /// </summary>
            public string importance;

            /// <summary>
            /// Shop ID associated with the POI.
            /// </summary>
            public string shopid;

            /// <summary>
            /// Weight of the POI.
            /// </summary>
            public string poiweight;

            /// <summary>
            /// Geography.
            /// </summary>
            public string gridcode;

            /// <summary>
            /// The distance from the center point, in meters.
            /// </summary>
            public string distance;

            /// <summary>
            /// The map number.
            /// </summary>
            public string navi_poiid;

            /// <summary>
            /// Entrance latitude and longitude.
            /// </summary>
            public string entr_location;

            /// <summary>
            /// The business district.
            /// </summary>
            public string business_area;

            /// <summary>
            /// Exit latitude and longitude.
            /// </summary>
            public string exit_location;

            /// <summary>
            /// Match status of the POI.
            /// </summary>
            public string match;

            /// <summary>
            /// Recommendation status of the POI.
            /// </summary>
            public string recommend;

            /// <summary>
            /// Timestamp of the POI data.
            /// </summary>
            public string timestamp;

            /// <summary>
            /// Type of parking. Show parking types, including: underground, ground, roadside.
            /// </summary>
            public string parking_type;

            /// <summary>
            /// Alias.
            /// </summary>
            public string alias;

            /// <summary>
            /// Are there indoor map signs.
            /// </summary>
            public string indoor_map;

            /// <summary>
            /// Indoor map of the relevant data.
            /// </summary>
            public IndoorData indoor_data;

            /// <summary>
            /// Number of group buys.
            /// </summary>
            public string groupbuy_num;

            /// <summary>
            /// Number of discounts.
            /// </summary>
            public string discount_num;

            /// <summary>
            /// Business extension information.
            /// </summary>
            public BizExt biz_ext;

            /// <summary>
            /// Event associated with the POI.
            /// </summary>
            public string @event;

            /// <summary>
            /// Array of child POIs.
            /// </summary>
            public Children[] children;

            /// <summary>
            /// Array of photos associated with the POI.
            /// </summary>
            public Photo[] photos;

            /// <summary>
            /// Gets the location coordinates
            /// </summary>
            /// <param name="lng">Longitude</param>
            /// <param name="lat">Latitude</param>
            public void GetLocation(out double lng, out double lat)
            {
                lng = 0;
                lat = 0;
                if (string.IsNullOrEmpty(location)) return;

                string[] parts = location.Split(',');
                lat = double.Parse(parts[1], Culture.numberFormat);
                lng = double.Parse(parts[0], Culture.numberFormat);
            }
            
            /// <summary>
            /// Gets the location coordinates
            /// </summary>
            /// <returns>Location coordinates</returns>
            public GeoPoint GetLocation()
            {
                double lng, lat;
                GetLocation(out lng, out lat);
                return new GeoPoint(lng, lat);
            }
        }

        /// <summary>
        /// Suggestion information for the search result.
        /// </summary>
        public class Suggestion
        {
            /// <summary>
            /// Suggested keywords.
            /// </summary>
            public string keywords;

            /// <summary>
            /// Suggested cities.
            /// </summary>
            public string cities;
        }

        /// <summary>
        /// Indoor map of the relevant data
        /// </summary>
        public class IndoorData
        {
            /// <summary>
            /// The parent POI of the current POI
            /// </summary>
            public string cpid;

            /// <summary>
            /// Floor directory
            /// </summary>
            public string floor;

            /// <summary>
            /// On the floor
            /// </summary>
            public string truefloor;

            /// <summary>
            /// CMS ID associated with the indoor data.
            /// </summary>
            public string cmsid;
        }

        /// <summary>
        /// Business extension information.
        /// </summary>
        public class BizExt
        {
            /// <summary>
            /// Rating of the POI.
            /// </summary>
            public string rating;

            /// <summary>
            /// Cost associated with the POI.
            /// </summary>
            public string cost;
        }

        /// <summary>
        /// Represents a child POI.
        /// </summary>
        public class Children
        {
            /// <summary>
            /// The unique ID of the child POI.
            /// </summary>
            public string id;

            /// <summary>
            /// The name of the child POI.
            /// </summary>
            public string name;

            /// <summary>
            /// The short name of the child POI.
            /// </summary>
            public string sname;

            /// <summary>
            /// The location of the child POI.
            /// </summary>
            public string location;

            /// <summary>
            /// The address of the child POI.
            /// </summary>
            public string address;

            /// <summary>
            /// The distance from the parent POI.
            /// </summary>
            public string distance;

            /// <summary>
            /// The subtype of the child POI.
            /// </summary>
            public string subtype;
        }

        /// <summary>
        /// Represents a photo associated with the POI.
        /// </summary>
        public class Photo
        {
            /// <summary>
            /// The title of the photo.
            /// </summary>
            public string title;

            /// <summary>
            /// The URL of the photo.
            /// </summary>
            public string url;
        }
    }
}