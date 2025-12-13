/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    [Plugin("MapTiler Elevations", typeof(ControlBaseDynamicMesh), "Elevations")]
    [AddComponentMenu("Infinity Code/Online Maps/Elevations/MapTiler Elevation Manager")]
    public class MapTilerElevationManager : TerrainRGBElevationManager<MapTilerElevationManager>
    {
        public override string token => KeyManager.MapTiler();
        public override int maxZoom => 12;
        protected override string cachePrefix => "maptiler_elevation_";
        protected override int tileWidth => 512;
        protected override int tileHeight => 512;

        public override string GetUrl(Tile tile)
        {
            if (string.IsNullOrEmpty(token)) return null;
            return $"https://api.maptiler.com/tiles/terrain-rgb/{tile.zoom}/{tile.x}/{tile.y}.png?key={token}";
        }
    }
}