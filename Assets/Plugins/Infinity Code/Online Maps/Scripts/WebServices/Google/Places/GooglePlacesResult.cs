/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Result of Google Maps Places query.
    /// </summary>
    public class GooglePlacesResult
    {
        /// <summary>
        /// Coordinates of the place.
        /// </summary>
        public Vector2 location;

        /// <summary>
        /// URL of a recommended icon which may be displayed to the user when indicating this result.
        /// </summary>
        public string icon;

        /// <summary>
        /// Unique stable identifier denoting this place. <br/>
        /// This identifier may not be used to retrieve information about this place, but is guaranteed to be valid across sessions. <br/>
        /// It can be used to consolidate data about this place, and to verify the identity of a place across separate searches. <br/>
        /// Note: The id is now deprecated in favor of place_id.
        /// </summary>
        public string id;

        /// <summary>
        /// Human-readable address of this place. <br/>
        /// Often this address is equivalent to the "postal address". <br/>
        /// The formatted_address property is only returned for a Text Search.
        /// </summary>
        public string formatted_address;

        /// <summary>
        /// Human-readable name for the returned result. <br/>
        /// For establishment results, this is usually the business name.
        /// </summary>
        public string name;

        /// <summary>
        /// Unique identifier for a place.
        /// </summary>
        public string place_id;

        /// <summary>
        /// Unique token that you can use to retrieve additional information about this place in a Place Details request. <br/>
        /// Although this token uniquely identifies the place, the converse is not true. <br/>
        /// A place may have many valid reference tokens. <br/>
        /// It's not guaranteed that the same token will be returned for any given place across different searches. <br/>
        /// Note: The reference is now deprecated in favor of place_id.
        /// </summary>
        public string reference;

        /// <summary>
        /// Array of feature types describing the given result. <br/>
        /// XML responses include multiple type elements if more than one type is assigned to the result.
        /// </summary>
        public string[] types;

        /// <summary>
        /// Feature name of a nearby location. <br/>
        /// Often this feature refers to a street or neighborhood within the given results. <br/>
        /// The vicinity property is only returned for a Nearby Search.
        /// </summary>
        public string vicinity;

        /// <summary>
        /// The price level of the place, on a scale of 0 to 4.  <br/>
        /// The exact amount indicated by a specific value will vary from region to region. <br/>
        /// Price levels are interpreted as follows: <br/>
        /// -1 - Unknown <br/>
        /// 0 - Free <br/>
        /// 1 - Inexpensive <br/>
        /// 2 - Moderate <br/>
        /// 3 - Expensive <br/>
        /// 4 - Very Expensive
        /// </summary>
        public int price_level = -1;

        /// <summary>
        /// Place's rating, from 1.0 to 5.0, based on aggregated user reviews.
        /// </summary>
        public float rating;

        /// <summary>
        /// Value indicating if the place is open at the current time.
        /// </summary>
        public bool open_now;

        /// <summary>
        /// Indicates the scope of the place_id. 
        /// </summary>
        public string scope;

        /// <summary>
        /// Undocumented in Google Maps Places API.
        /// </summary>
        public string[] weekday_text;

        /// <summary>
        /// Array of photo objects, each containing a reference to an image. <br/>
        /// A Place Search will return at most one photo object. <br/>
        /// Performing a Place Details request on the place may return up to ten photos.
        /// </summary>
        public Photo[] photos;

        /// <summary>
        /// Plus code of the place.
        /// </summary>
        public PlusCode plus_code;

        /// <summary>
        /// Token for the next page of results.
        /// </summary>
        public string nextPageToken;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public GooglePlacesResult()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="node">Place node from response</param>
        public GooglePlacesResult(XML node)
        {
            List<Photo> photos = new List<Photo>();
            List<string> types = new List<string>();
            List<string> weekday_text = new List<string>();

            foreach (XML n in node)
            {
                if (n.name == "name") name = n.Value();
                else if (n.name == "id") id = n.Value();
                else if (n.name == "vicinity") vicinity = n.Value();
                else if (n.name == "type") types.Add(n.Value());
                else if (n.name == "geometry") location = XML.GetVector2FromNode(n[0]);
                else if (n.name == "rating") rating = n.Value<float>();
                else if (n.name == "icon") icon = n.Value();
                else if (n.name == "reference") reference = n.Value();
                else if (n.name == "place_id") place_id = n.Value();
                else if (n.name == "scope") scope = n.Value();
                else if (n.name == "price_level") price_level = n.Value<int>();
                else if (n.name == "formatted_address") formatted_address = n.Value();
                else if (n.name == "opening_hours")
                {
                    open_now = n.Get<string>("open_now") == "true";
                    foreach (XML wdt in n.FindAll("weekday_text")) weekday_text.Add(wdt.Value());
                }
                else if (n.name == "photo") photos.Add(new Photo(n));
                else if (n.name == "plus_code") plus_code = new PlusCode(n);
                else Debug.Log(n.name);
            }

            this.photos = photos.ToArray();
            this.types = types.ToArray();
            this.weekday_text = weekday_text.ToArray();
        }

        public static GooglePlacesResult[] Parse(string response)
        {
            try
            {
                XML xml = XML.Load(response);
                string status = xml.Find<string>("//status");
                if (status != "OK") return null;

                string nextPageToken = xml.Find<string>("//next_page_token");
                XMLList resNodes = xml.FindAll("//result");

                List<GooglePlacesResult> results = new List<GooglePlacesResult>(resNodes.count);
                foreach (XML node in resNodes)
                {
                    results.Add(new GooglePlacesResult(node)
                    {
                        nextPageToken = nextPageToken
                    });
                }
                return results.ToArray();
            }
            catch (Exception exception)
            {
                Debug.Log(exception.Message + "\n" + exception.StackTrace);
            }

            return null;
        }

        /// <summary>
        /// Photo objects, contains a reference to an image.
        /// </summary>
        public class Photo
        {
            /// <summary>
            /// The maximum width of the image.
            /// </summary>
            public int width;

            /// <summary>
            /// The maximum height of the image.
            /// </summary>
            public int height;

            /// <summary>
            /// String used to identify the photo when you perform a Photo request.
            /// </summary>
            public string photo_reference;

            /// <summary>
            /// Contains any required attributions. This field will always be present, but may be empty.
            /// </summary>
            public string[] html_attributions;

            /// <summary>
            /// Default constructor.
            /// </summary>
            public Photo()
            {
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="node">Photo node from response</param>
            public Photo(XML node)
            {
                try
                {
                    width = node.Get<int>("width");
                    height = node.Get<int>("height");
                    photo_reference = node["photo_reference"].Value();

                    List<string> html_attributions = new List<string>();
                    foreach (XML ha in node.FindAll("html_attributions")) html_attributions.Add(ha.Value());
                    this.html_attributions = html_attributions.ToArray();
                }
                catch
                {
                }
            }

            /// <summary>
            /// Download photo from Google Places.
            /// </summary>
            /// <param name="key">Google Maps API Key.</param>
            /// <param name="maxWidth">
            /// Specifies the maximum desired width, in pixels, of the image returned by the Place Photos service. <br/>
            /// If the image is smaller than the values specified, the original image will be returned. <br/>
            /// If the image is larger in either dimension, it will be scaled to match the smaller of the two dimensions, restricted to its original aspect ratio. <br/>
            /// maxWidth accept an integer between 1 and 1600.
            /// </param>
            /// <param name="maxHeight">
            /// Specifies the maximum desired height, in pixels, of the image returned by the Place Photos service. <br/>
            /// If the image is smaller than the values specified, the original image will be returned. <br/>
            /// If the image is larger in either dimension, it will be scaled to match the smaller of the two dimensions, restricted to its original aspect ratio. <br/>
            /// maxHeight accept an integer between 1 and 1600.
            /// </param>
            /// <returns>Instance of a request</returns>
            public GooglePlacePhotoRequest Download(string key, int? maxWidth = null, int? maxHeight = null)
            {
                if (!maxWidth.HasValue) maxWidth = width;
                if (!maxHeight.HasValue) maxHeight = height;
                GooglePlacePhotoRequest request = new GooglePlacePhotoRequest(photo_reference)
                {
                    key = key,
                    maxWidth = maxWidth,
                    maxHeight = maxHeight
                };
                request.Send();
                return request;
            }
        }

        /// <summary>
        /// Represents a Plus Code, which is a short code for a location.
        /// </summary>
        public class PlusCode
        {
            /// <summary>
            /// The global code of the Plus Code.
            /// </summary>
            public string global_code;

            /// <summary>
            /// The compound code of the Plus Code.
            /// </summary>
            public string compound_code;

            /// <summary>
            /// Default constructor.
            /// </summary>
            public PlusCode()
            {
            }

            /// <summary>
            /// Constructor that initializes the Plus Code from an XML node.
            /// </summary>
            /// <param name="node">The XML node containing Plus Code data.</param>
            public PlusCode(XML node)
            {
                try
                {
                    global_code = node.Get<string>("global_code");
                    compound_code = node.Get<string>("compound_code");
                }
                catch
                {
                }
            }
        }
    }
}