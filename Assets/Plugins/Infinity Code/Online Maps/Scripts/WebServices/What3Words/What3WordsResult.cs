/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Base class for result objects of What 3 Words.
    /// </summary>
    public abstract class What3WordsResultBase
    {
        /// <summary>
        /// The response code.
        /// </summary>
        public int code = 200;

        /// <summary>
        /// The human-readable status of the request.
        /// </summary>
        public string message = "OK";

        /// <summary>
        /// Represents the geographical bounds.
        /// </summary>
        public class Bounds
        {
            /// <summary>
            /// Left longitude
            /// </summary>
            public double left;

            /// <summary>
            /// Right longitude
            /// </summary>
            public double right;

            /// <summary>
            /// Top latitude
            /// </summary>
            public double top;

            /// <summary>
            /// Bottom latitude
            /// </summary>
            public double bottom;

            /// <summary>
            /// The southwest corner of the bounds.
            /// </summary>
            public Vector2d southwest
            {
                get => new(left, bottom);
                set
                {
                    left = value.x;
                    bottom = value.y;
                }
            }

            /// <summary>
            /// The northeast corner of the bounds.
            /// </summary>
            public Vector2d northeast
            {
                get => new(right, top);
                set
                {
                    right = value.x;
                    top = value.y;
                }
            }
        }
    }


    /// <summary>
    /// The resulting object for What 3 Words forward and reverse geocoding.
    /// </summary>
    public class What3WordsFRResult : What3WordsResultBase
    {
        /// <summary>
        /// The bounds of the result.
        /// </summary>
        public Bounds bounds;

        /// <summary>
        /// The three words representing the location.
        /// </summary>
        public string words;

        /// <summary>
        /// The map URL of the location.
        /// </summary>
        public string map;

        /// <summary>
        /// The language of the result.
        /// </summary>
        public string language;

        /// <summary>
        /// The geographical coordinates of the location.
        /// </summary>
        public Vector2d geometry;

        public static What3WordsFRResult Parse(string response)
        {
            return JSON.Deserialize<What3WordsFRResult>(response);
        }
    }

    /// <summary>
    /// The resulting object for What 3 Words AutoSuggest or StandardBlend.
    /// </summary>
    public class What3WordsSBResult : What3WordsResultBase
    {
        /// <summary>
        /// Suggestions or blends items.
        /// </summary>
        [Alias("suggestions", "blends")]
        public Item[] items;

        public static What3WordsSBResult Parse(string response)
        {
            return JSON.Deserialize<What3WordsSBResult>(response);
        }

        /// <summary>
        /// Represents a suggestion or blend item.
        /// </summary>
        public class Item
        {
            /// <summary>
            /// The country of the item.
            /// </summary>
            public string country;

            /// <summary>
            /// The distance of the item.
            /// </summary>
            public int distance;

            /// <summary>
            /// The words of the item.
            /// </summary>
            public string words;

            /// <summary>
            /// The rank of the item.
            /// </summary>
            public int rank;

            /// <summary>
            /// The geometry of the item.
            /// </summary>
            public Vector2d geometry;

            /// <summary>
            /// The place of the item.
            /// </summary>
            public string place;
        }
    }

    /// <summary>
    /// The resulting object for What 3 Words Grid.
    /// </summary>
    public class What3WordsGridResult : What3WordsResultBase
    {
        /// <summary>
        /// The lines that make up the grid.
        /// </summary>
        public Line[] lines;

        public static What3WordsGridResult Parse(string response)
        {
            return JSON.Deserialize<What3WordsGridResult>(response);
        }

        /// <summary>
        /// Represents a line in the grid.
        /// </summary>
        public class Line
        {
            /// <summary>
            /// The starting point of the line.
            /// </summary>
            public Vector2d start;

            /// <summary>
            /// The ending point of the line.
            /// </summary>
            public Vector2d end;
        }
    }

    /// <summary>
    ///  The resulting object for What 3 Words Get Languages.
    /// </summary>
    public class What3WordsLanguagesResult : What3WordsResultBase
    {
        /// <summary>
        /// The languages supported by What 3 Words.
        /// </summary>
        public Language[] languages;

        public static What3WordsLanguagesResult Parse(string response)
        {
            return JSON.Deserialize<What3WordsLanguagesResult>(response);
        }

        /// <summary>
        /// Represents a language supported by What 3 Words.
        /// </summary>
        public class Language
        {
            /// <summary>
            /// The language code.
            /// </summary>
            public string code;

            /// <summary>
            /// The name of the language.
            /// </summary>
            public string name;

            /// <summary>
            /// The native name of the language.
            /// </summary>
            public string native_name;
        }
    }
}