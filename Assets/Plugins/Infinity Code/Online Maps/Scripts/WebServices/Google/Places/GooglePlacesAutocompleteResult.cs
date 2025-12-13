/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Result of Google Maps Place Autocomplete query.
    /// </summary>
    public class GooglePlacesAutocompleteResult
    {
        /// <summary>
        /// Human-readable name for the returned result. For establishment results, this is usually the business name.
        /// </summary>
        public string description;

        /// <summary>
        /// Unique token that you can use to retrieve additional information about this place in a Place Details request. <br/>
        /// Although this token uniquely identifies the place, the converse is not true. A place may have many valid reference tokens. <br/>
        /// It's not guaranteed that the same token will be returned for any given place across different searches. <br/>
        /// Note: The reference is deprecated in favor of place_id. 
        /// </summary>
        public string reference;

        /// <summary>
        /// Unique stable identifier denoting this place. <br/>
        /// This identifier may not be used to retrieve information about this place, but can be used to consolidate data about this place, and to verify the identity of a place across separate searches. <br/>
        /// Note: The id is deprecated in favor of place_id.
        /// </summary>
        public string id;

        /// <summary>
        /// Unique identifier for a place. <br/>
        /// To retrieve information about the place, pass this identifier in the placeId field of a Places API request.<br/>
        /// </summary>
        public string place_id;

        /// <summary>
        /// Array of types that apply to this place. <br/>
        /// For example: [ "political", "locality" ] or [ "establishment", "geocode" ].
        /// </summary>
        public string[] types;

        /// <summary>
        /// Array of terms identifying each section of the returned description (a section of the description is generally terminated with a comma).
        /// </summary>
        public Term[] terms;

        /// <summary>
        /// These describe the location of the entered term in the prediction result text, so that the term can be highlighted if desired.
        /// </summary>
        public MatchedSubstring matchedSubstring;

        /// <summary>
        /// Structured formatting for the autocomplete result.
        /// </summary>
        public string structured_formatting;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public GooglePlacesAutocompleteResult()
        {
        }

        /// <summary>
        /// Constructor of FindAutocompleteResult.
        /// </summary>
        /// <param name="node">Result node from response.</param>
        public GooglePlacesAutocompleteResult(XML node)
        {
            List<Term> terms = new List<Term>();
            List<string> types = new List<string>();

            foreach (XML n in node)
            {
                if (n.name == "description") description = n.Value();
                else if (n.name == "type") types.Add(n.Value());
                else if (n.name == "id") id = n.Value();
                else if (n.name == "place_id") place_id = n.Value();
                else if (n.name == "reference") reference = n.Value();
                else if (n.name == "term") terms.Add(new Term(n));
                else if (n.name == "matched_substring") matchedSubstring = new MatchedSubstring(n);
                else if (n.name == "structured_formatting") structured_formatting = n.Value();
                else Debug.Log(n.name);
            }

            this.terms = terms.ToArray();
            this.types = types.ToArray();
        }

        public static GooglePlacesAutocompleteResult[] Parse(string response)
        {
            try
            {
                XML xml = XML.Load(response);
                string status = xml.Find<string>("//status");
                if (status != "OK") return null;

                List<GooglePlacesAutocompleteResult> results = new List<GooglePlacesAutocompleteResult>();

                XMLList resNodes = xml.FindAll("//prediction");

                foreach (XML node in resNodes)
                {
                    results.Add(new GooglePlacesAutocompleteResult(node));
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
        /// Term identifying each section of the returned description.
        /// </summary>
        public class Term
        {
            /// <summary>
            /// Term value.
            /// </summary>
            public string value;

            /// <summary>
            /// Term offset
            /// </summary>
            public int offset;

            /// <summary>
            /// Default constructor.
            /// </summary>
            public Term()
            {
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="node">Term node from response.</param>
            public Term(XML node)
            {
                try
                {
                    value = node.Get<string>("value");
                    offset = node.Get<int>("height");
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// These describe the location of the entered term in the prediction result text, so that the term can be highlighted if desired.
        /// </summary>
        public class MatchedSubstring
        {
            /// <summary>
            /// Substring offset.
            /// </summary>
            public int offset;

            /// <summary>
            /// Substring length.
            /// </summary>
            public int length;

            /// <summary>
            /// Default constructor.
            /// </summary>
            public MatchedSubstring()
            {
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="node">MatchedSubstring node from response.</param>
            public MatchedSubstring(XML node)
            {
                try
                {
                    length = node.Get<int>("length");
                    offset = node.Get<int>("height");
                }
                catch (Exception)
                {
                }
            }
        }
    }
}