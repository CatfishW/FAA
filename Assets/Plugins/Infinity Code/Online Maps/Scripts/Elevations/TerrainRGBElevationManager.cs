/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    public abstract class TerrainRGBElevationManager<T> : TiledElevationManager<T>  where T : TerrainRGBElevationManager<T>
    {
        public virtual int maxZoom => Constants.MaxZoom;
        public abstract string token { get; }
        
        public override void CancelCurrentElevationRequest()
        {
            // TODO Implement this
        }

        protected virtual bool CheckTextureSize(Texture2D texture)
        {
            return texture.width == tileWidth && texture.height == tileHeight;
        }

        public abstract string GetUrl(Tile tile);

        protected virtual void OnTileDownloaded(Tile tile, WebRequest www)
        {
            currentDownloadTile = null;
            
            if (www.hasError)
            {
                if (OnElevationFails != null) OnElevationFails(www.error);
                Debug.Log("Download error");
                return;
            }

            if (OnDownloadSuccess != null) OnDownloadSuccess(tile, www);
        
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGB24, false);
            texture.LoadImage(www.bytes);

            SetElevationTexture(tile, texture);
            Utils.Destroy(texture);

            if (OnElevationUpdated != null) OnElevationUpdated();
        }

        protected virtual void ParseColors(Tile tile, Color[] colors)
        {
            short max = short.MinValue;
            short min = short.MaxValue;

            for (int y = 0; y < tileHeight; y++)
            {
                int py = (tileHeight - y - 1) * tileWidth;

                for (int x = 0; x < tileWidth; x++)
                {
                    Color c = colors[py + x];

                    double height = -10000 + (c.r * 255 * 256 * 256 + c.g * 255 * 256 + c.b * 255) * 0.1;
                    short v = (short) Math.Round(height);
                    tile.elevations[x, y] = v;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }
            
            tile.minValue = min;
            tile.maxValue = max;
        }

        /// <summary>
        /// Sets the elevation texture for the tile.
        /// </summary>
        /// <param name="tile">Tile</param>
        /// <param name="texture">Texture</param>
        public virtual void SetElevationTexture(Tile tile, Texture2D texture)
        {
            if (!CheckTextureSize(texture))
            {
                Debug.Log("Wrong texture size: " + texture.width + "x" + texture.height);
                return;
            }

            Color[] colors = texture.GetPixels();
            tile.elevations = new short[tile.width, tile.height];

            ParseColors(tile, colors);
            SetElevationToCache(tile, tile.elevations);

            tile.loaded = true;
            needUpdateMinMax = true;

            CheckAllTilesLoaded();

            map.Redraw();
        }
        
        protected override void Start()
        {
            base.Start();

            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning($"Missing key for {GetType().Name}. Please set a valid Api Key.");
            }
        }

        public override void StartDownloadElevationTile(Tile tile)
        {
            if (tile.zoom > maxZoom) return;
            string url = GetUrl(tile);
            if (string.IsNullOrEmpty(url)) return;

            currentDownloadTile = tile;
            WebRequest www = new WebRequest(url);
            www.OnComplete += delegate { OnTileDownloaded(tile, www); };

            if (OnElevationRequested != null) OnElevationRequested();
        }
    }
}