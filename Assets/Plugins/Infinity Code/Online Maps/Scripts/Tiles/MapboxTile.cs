/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    public class MapboxTile : VectorTile
    {
        private const float size = 256;

        private Material baseMaterial;
        private int renderQueueOffset;

        public override string url => $"https://b.tiles.mapbox.com/v4/mapbox.mapbox-terrain-v2,mapbox.mapbox-streets-v8/{zoom}/{x}/{y}.vector.pbf?access_token={KeyManager.Mapbox()}";

        public MapboxTile(int x, int y, int zoom, Map map, bool isMapTile = true) : base(x, y, zoom, map, isMapTile)
        {
        }

        protected override void LoadTileFromWWW(WebRequest www)
        {
            status = TileStatus.loaded;

            byte[] decompressed = ZipDecompressor.Decompress(www.bytes);
            Read(decompressed);

            MarkLoaded();
            map.Redraw();
        }

        public override Color GetPixel(float u, float v)
        {
            return default;
        }
    }
}