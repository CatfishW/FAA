/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Text;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Use the Elevations API to get elevation information for a set of locations, polyline or area on the Earth.<br/>
    /// https://msdn.microsoft.com/en-us/library/jj158961.aspx
    /// </summary>
    public abstract class BingMapsElevationRequestBase : TextWebService<BingMapsElevationResult>
    {
        /// <summary>
        /// The API key for Bing Maps.
        /// </summary>
        public string key;

        /// <summary>
        /// The height reference system.
        /// </summary>
        protected Heights heights;
        
        /// <summary>
        /// Gets the URL token for the request.
        /// </summary>
        protected abstract string urlToken { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="heights">The height reference system.</param>
        /// <param name="output">The output format.</param>
        protected BingMapsElevationRequestBase(Heights heights)
        {
            this.heights = heights;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            if (string.IsNullOrEmpty(key)) key = KeyManager.BingMaps();
            builder.Append("https://dev.virtualearth.net/REST/v1/Elevation/").Append(urlToken).Append("?key=").Append(key);
            if (heights == Heights.ellipsoid) builder.Append("&hts=ellipsoid");
        }

        private static int IndexOf(string str, params string[] parts)
        {
            int[] pi = new int[parts.Length];
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                for (int j = 0; j < parts.Length; j++)
                {
                    if (parts[j][pi[j]] == c)
                    {
                        pi[j]++;
                        if (pi[j] == parts[j].Length) return i + 1;
                    }
                    else pi[j] = 0;
                }
            }

            return -1;
        }

        /// <summary>
        /// Fast way get the elevation values without parsing JSON.
        /// </summary>
        /// <param name="response">Response string</param>
        /// <param name="array">
        /// Reference to an array where the values will be stored.<br/>
        /// Supports one-dimensional and two-dimensional arrays.
        /// </param>
        /// <returns>TRUE - success, FALSE - failed.</returns>
        public static bool ParseElevationArray(string response, ref Array array)
        {
            if (array == null) throw new Exception("Array can not be null.");

            int rank = array.Rank;
            if (rank > 2) throw new Exception("Supports only one-dimensional and two-dimensional arrays.");

            int l1 = array.GetLength(0);
            int l2 = 1;
            if (rank == 2) l2 = array.GetLength(1);

            Type t = array.GetType();
            Type t2 = t.GetElementType();

            try
            {
                return ParseJSONElevations(response, array, l1, l2, rank, t2);
            }
            catch
            {
                return false;
            }
        }

        private static bool ParseJSONElevations(string response, Array array, int l1, int l2, int rank, Type t2)
        {
            int startIndex = IndexOf(response, "\"elevations\":[", "\"offsets\":[");
            if (startIndex == -1) return false;

            int index = 0;
            int v = 0;
            bool isNegative = false;
            bool smallArray = false;

            int x, y;

            for (int i = startIndex; i < response.Length; i++)
            {
                char c = response[i];
                if (c == ',')
                {
                    x = index % l1;
                    y = index / l2;
                    if (isNegative) v = -v;

                    if (rank == 1)
                    {
                        if (y < l1) array.SetValue(Convert.ChangeType(v, t2), y);
                        else smallArray = true;
                    }
                    else
                    {
                        if (x < l1 && y < l2) array.SetValue(Convert.ChangeType(v, t2), x, y);
                        else smallArray = true;
                    }

                    isNegative = false;
                    v = 0;
                    index++;
                }
                else if (c == '-') isNegative = true;
                else if (c > 47 && c < 58) v = v * 10 + (c - 48);
                else break;
            }

            x = index % l1;
            y = index / l2;

            if (isNegative) v = -v;

            if (rank == 1)
            {
                if (y < l1) array.SetValue(Convert.ChangeType(v, t2), y);
                else smallArray = true;
            }
            else
            {
                if (x < l1 && y < l2) array.SetValue(Convert.ChangeType(v, t2), x, y);
                else smallArray = true;
            }

            if (smallArray)
            {
                Debug.LogWarning("Invalid array. The response contains " + (index + 1) + " elements.");
                return false;
            }

            return true;
        }

        public static BingMapsElevationResult Parse(string response) => BingMapsElevationResult.Parse(response);
        public override BingMapsElevationResult ParseResult(string response) => Parse(response);

        /// <summary>
        /// Enumeration of height reference systems.
        /// </summary>
        public enum Heights
        {
            /// <summary>
            /// Height relative to sea level.
            /// </summary>
            sealevel,

            /// <summary>
            /// Height relative to the ellipsoid.
            /// </summary>
            ellipsoid
        }
    }
}