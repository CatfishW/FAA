/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text.RegularExpressions;

namespace OnlineMaps
{
    /// <summary>
    /// Providers of the traffic tiles.
    /// </summary>
    public class TrafficProvider
    {
        private static TrafficProvider[] _providers;

        /// <summary>
        /// Provider ID
        /// </summary>
        public string id;

        /// <summary>
        /// Provider name
        /// </summary>
        public string title;

        /// <summary>
        /// URL of tiles
        /// </summary>
        public string url;

        /// <summary>
        /// Indicates that this is a custom provider.
        /// </summary>
        public bool isCustom;

        /// <summary>
        ///  Gets an instance of a traffic provider by ID.
        /// </summary>
        /// <param name="id">Provider ID</param>
        /// <returns>Success: Instance of provider; FAILED - First provider</returns>
        public static TrafficProvider GetByID(string id)
        {
            TrafficProvider[] providers = GetProviders();
            foreach (TrafficProvider p in providers) if (p.id == id) return p;
            return providers[0];
        }

        /// <summary>
        /// Gets array of traffic providers
        /// </summary>
        /// <returns>Array of traffic providers</returns>
        public static TrafficProvider[] GetProviders() => TrafficSources.providers;
        
        /// <summary>
        /// Gets the URL to download the traffic texture.
        /// </summary>
        /// <param name="tile">Instance of tile.</param>
        /// <returns>URL to texture</returns>
        public string GetURL(Tile tile)
        {
            return Regex.Replace(url, @"{\w+}", delegate (Match match)
            {
                string v = match.Value.ToLower().Trim('{', '}');

                if (Tile.OnReplaceTrafficURLToken != null)
                {
                    string ret = Tile.OnReplaceTrafficURLToken(tile, v);
                    if (ret != null) return ret;
                }

                if (v == "zoom") return tile.zoom.ToString();
                if (v == "z") return tile.zoom.ToString();
                if (v == "x") return tile.x.ToString();
                if (v == "y") return tile.y.ToString();
                if (v == "quad") return Utils.TileToQuadKey(tile.x, tile.y, tile.zoom);
                return v;
            });
        }
    }
}