/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    [Plugin("TomTom Elevations", typeof(ControlBaseDynamicMesh), "Elevations")]
    [AddComponentMenu("Infinity Code/Online Maps/Elevations/TomTom Elevation Manager")]
    public class TomTomElevationManager : TerrainRGBElevationManager<TomTomElevationManager>
    {
        public override int maxZoom => 13;
        public override string token => KeyManager.TomTom();

        protected override string cachePrefix => "tomtom_elevation_";
        protected override int tileWidth => 512;
        protected override int tileHeight => 512;

        protected override bool CheckTextureSize(Texture2D texture)
        {
            return texture.width == tileWidth + 2 && texture.height == tileHeight + 2;
        }

        public override string GetUrl(Tile tile)
        {
            if (string.IsNullOrEmpty(token)) return null;
            return $"https://api.tomtom.com/map/1/tile/hill/main/{tile.zoom}/{tile.x}/{tile.y}.png?key={token}";
        }

        protected override void ParseColors(Tile tile, Color[] colors)
        {
            short max = short.MinValue;
            short min = short.MaxValue;
            
            int width = tileWidth + 2;
            int height = tileHeight + 2;

            for (int y = 1; y < height - 1; y++)
            {
                int py = (height - y - 1) * width;

                for (int x = 1; x < width - 1; x++)
                {
                    Color c = colors[py + x];

                    double h = -10000 + (c.r * 255 * 256 * 256 + c.g * 255 * 256 + c.b * 255) * 0.1;
                    short v = (short) Math.Round(h);
                    tile.elevations[x - 1, y - 1] = v;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }
            
            tile.minValue = min;
            tile.maxValue = max;
        }
    }
}