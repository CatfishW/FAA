/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Implements the use of elevation data from Mapbox
    /// </summary>
    [Plugin("Here Elevations", typeof(ControlBaseDynamicMesh), "Elevations")]
    [AddComponentMenu("Infinity Code/Online Maps/Elevations/Here Elevation Manager")]
    public class HereElevationManager : TerrainRGBElevationManager<HereElevationManager>
    {
        public override string token => KeyManager.HereApiKey();
        protected override string cachePrefix => "here_elevation_";
        protected override int tileWidth => 256;
        protected override int tileHeight => 256;

        public override string GetUrl(Tile tile)
        {
            if (string.IsNullOrEmpty(token)) return null;
            return $"https://maps.hereapi.com/v3/base/mc/{tile.zoom}/{tile.x}/{tile.y}/png?size=256&style=dem&apiKey={token}";
        }
    }
}