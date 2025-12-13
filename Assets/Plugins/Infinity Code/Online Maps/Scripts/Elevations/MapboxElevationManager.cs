/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Implements the use of elevation data from Mapbox
    /// </summary>
    [Plugin("Mapbox Elevations", typeof(ControlBaseDynamicMesh), "Elevations")]
    [AddComponentMenu("Infinity Code/Online Maps/Elevations/Mapbox Elevation Manager")]
    public class MapboxElevationManager : TerrainRGBElevationManager<MapboxElevationManager>
    {
        public override string token => KeyManager.Mapbox();
        protected override string cachePrefix => "mapbox_elevation_";
        protected override int tileWidth => 256;
        protected override int tileHeight => 256;

        public override string GetUrl(Tile tile)
        {
            if (string.IsNullOrEmpty(token)) return null;
            return $"https://api.mapbox.com/v4/mapbox.terrain-rgb/{tile.zoom}/{tile.x}/{tile.y}.pngraw?access_token={token}";
        }
    }
}