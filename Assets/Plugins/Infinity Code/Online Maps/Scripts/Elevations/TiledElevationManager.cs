/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Implements elevation managers, which loads elevation data by tiles
    /// </summary>
    /// <typeparam name="T">Type of elevation manager</typeparam>
    public abstract class TiledElevationManager<T> : ElevationManager<T> where T : TiledElevationManager<T>
    {
        /// <summary>
        /// Called when all tiles are loaded.
        /// </summary>
        public Action<TiledElevationManager<T>> OnAllTilesLoaded;

        /// <summary>
        /// Called when data download starts.
        /// </summary>
        public Action<Tile> OnDownload;

        /// <summary>
        /// Called when data is successfully downloaded.
        /// </summary>
        public Action<Tile, WebRequest> OnDownloadSuccess;

        /// <summary>
        /// Offset of tile zoom from map zoom
        /// </summary>
        public int zoomOffset = 3;

        /// <summary>
        /// Cache elevations?
        /// </summary>
        public bool cacheElevations = true;

        /// <summary>
        /// Dictionary to store tiles with their unique keys.
        /// </summary>
        protected Dictionary<ulong, Tile> tiles;

        /// <summary>
        /// Flag indicating if the minimum and maximum elevation values need to be updated.
        /// </summary>
        protected bool needUpdateMinMax = true;

        /// <summary>
        /// Flag indicating if the tiles need to be updated.
        /// </summary>
        private bool needUpdateTiles = true;

        private int prevTileX;
        private int prevTileY;

        /// <summary>
        /// Gets the width of the tile.
        /// </summary>
        protected abstract int tileWidth { get; }

        /// <summary>
        /// Gets the height of the tile.
        /// </summary>
        protected abstract int tileHeight { get; }

        /// <summary>
        /// Gets the cache prefix.
        /// </summary>
        protected abstract string cachePrefix { get; }

        /// <summary>
        /// Gets a value indicating whether the manager has data.
        /// </summary>
        public override bool hasData => true;

        protected Tile currentDownloadTile;

        /// <summary>
        /// Checks if all tiles are loaded and triggers the OnAllTilesLoaded event if they are.
        /// </summary>
        protected void CheckAllTilesLoaded()
        {
            foreach (var pair in tiles)
            {
                if (!pair.Value.loaded) return;
            }

            if (OnAllTilesLoaded != null) OnAllTilesLoaded(this);
        }

        public override float GetElevationValue(double x, double z, float yScale, GeoRect rect)
        {
            float v = GetUnscaledElevationValue(x, z, rect);

            if (bottomMode == ElevationBottomMode.minValue) v -= minValue;
            return v * yScale * scale;
        }

        public override float GetUnscaledElevationValue(double x, double z, GeoRect rect)
        {
            if (tiles == null)
            {
                tiles = new Dictionary<ulong, Tile>();
                return 0;
            }

            x /= -sizeInScene.x;
            z /= sizeInScene.y;

            Map m = map;
            int mapZoom = m.view.intZoom;

            TileRect tileRect = rect.ToTileRect(m);

            if (tileRect.right < tileRect.left) tileRect.right += m.view.maxTiles;

            double cx = tileRect.width * x + tileRect.left;
            double cz = tileRect.height * z + tileRect.top;

            int zoom = mapZoom - zoomOffset;
            TilePoint t = new TilePoint(cx, cz, mapZoom).ToZoom(zoom);

            ulong key = TileManager.GetTileKey(zoom, (int)t.x, (int)t.y);
            Tile tile;
            bool hasTile = tiles.TryGetValue(key, out tile);
            if (hasTile && !tile.loaded) hasTile = false;

            const int maxZoomOffset = 3;

            if (!hasTile)
            {
                int nz = zoom;
                int offset = 0;

                while (!hasTile && nz < Constants.MaxZoom && offset < maxZoomOffset)
                {
                    nz++;
                    offset++;
                    TilePoint t2 = t.ToZoom(nz);
                    key = TileManager.GetTileKey(nz, (int)t2.x, (int)t2.y);
                    hasTile = tiles.TryGetValue(key, out tile) && tile.loaded;
                }
            }

            if (!hasTile)
            {
                int nz = zoom;
                int offset = 0;

                while (!hasTile && nz > 1 && offset < maxZoomOffset)
                {
                    nz--;
                    offset++;
                    TilePoint t2 = t.ToZoom(nz);
                    key = TileManager.GetTileKey(nz, (int)t2.x, (int)t2.y);
                    hasTile = tiles.TryGetValue(key, out tile) && tile.loaded;
                }
            }

            if (!hasTile) return 0;

            t = t.ToZoom(tile.zoom);
            return tile.GetElevation(t.x, t.y);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (map)
            {
                map.OnLocationChanged -= OnChangePosition;
                map.OnZoomChanged -= OnChangeZoom;
                map.OnLateUpdateBefore -= OnLateUpdateBefore;
            }
        }

        private void OnChangePosition()
        {
            if (needUpdateTiles) return;

            TilePoint t = map.view.centerTile;
            int isx = (int)t.x;
            int isy = (int)t.y;

            if (prevTileX != isx || prevTileY != isy) needUpdateTiles = true;
        }

        private void OnChangeZoom()
        {
            needUpdateTiles = true;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (tiles == null) tiles = new Dictionary<ulong, Tile>();
        }

        private void OnLateUpdateBefore()
        {
            if (!enabled) return;

            StartNextDownload();
            
            if (!needUpdateTiles)
            {
                if (needUpdateMinMax) UpdateMinMax();
                return;
            }

            needUpdateTiles = false;

            int zoom = map.view.intZoom - zoomOffset;
            if (zoom < 1) zoom = 1;
            if (!zoomRange.Contains(map.view.intZoom)) return;

            int currentOffset = map.view.intZoom - zoom;
            int divider = (1 << currentOffset) * Constants.TileSize;
            int countX = Mathf.CeilToInt(control.width / 2f / divider) * 2 + 2;
            int countY = Mathf.CeilToInt(control.height / 2f / divider) * 2 + 2;

            TilePoint centerTile = map.view.GetCenterTile(zoom);
            prevTileX = (int)centerTile.x;
            prevTileY = (int)centerTile.y;
            int isx = prevTileX - countX / 2;
            int isy = prevTileY - countY / 2;

            int max = 1 << zoom;

            foreach (KeyValuePair<ulong, Tile> pair in tiles)
            {
                pair.Value.used = false;
            }

            for (int x = isx; x < isx + countX; x++)
            {
                int cx = x;
                if (cx < 0) cx += max;
                else if (cx >= max) cx -= max;

                for (int y = Mathf.Max(isy, 0); y < Mathf.Min(isy + countY, max); y++)
                {
                    ulong key = TileManager.GetTileKey(zoom, cx, y);
                    Tile t;
                    if (tiles.TryGetValue(key, out t))
                    {
                        t.used = true;
                        continue;
                    }

                    t = new Tile
                    {
                        x = x,
                        y = y,
                        zoom = zoom,
                        width = tileWidth,
                        height = tileHeight,
                        used = true,
                        centerMercator = new TilePoint(x + 0.5, y + 0.5, zoom).ToMercator(),
                    };
                    tiles.Add(key, t);
                }
            }

            TileRect r = map.view.GetTileRect();

            int left = (int)r.left;
            int top = (int)r.top;
            int right = (int)r.right;
            int bottom = (int)r.bottom;

            List<ulong> unloadKeys = new List<ulong>();
            foreach (KeyValuePair<ulong, Tile> pair in tiles)
            {
                Tile tile = pair.Value;
                if (tile.used) continue;

                int scale = 1 << (map.view.intZoom - tile.zoom);
                int tx = tile.x * scale;
                int ty = tile.y * scale;

                if (right >= tx && left <= tx + scale && bottom >= ty && top <= ty + scale)
                {
                    if (Mathf.Abs(zoom - tile.zoom) < 3) continue;
                }

                unloadKeys.Add(pair.Key);
            }

            foreach (ulong key in unloadKeys) tiles.Remove(key);
            UpdateMinMax();
        }

        private void StartNextDownload()
        {
            if (currentDownloadTile != null) return;

            MercatorPoint centerMercator = map.view.centerMercator;

            IEnumerable<Tile> waitingLoading = tiles
                .Select(t => t.Value)
                .Where(t => !t.loaded)
                .OrderBy(t => t.zoom)
                .ThenBy(t => (t.centerMercator - centerMercator).sqrMagnitude);

            foreach (Tile tile in waitingLoading)
            {
                if (TryLoadFromCache(tile))
                {
                }
                else if (OnDownload != null) OnDownload(tile);
                else StartDownloadElevationTile(tile);

                return;
            }
        }

        /// <summary>
        /// Caches the elevation data for a tile.
        /// </summary>
        /// <param name="tile">The tile for which elevation data is being cached.</param>
        /// <param name="elevations">The elevation data to cache.</param>
        protected void SetElevationToCache(Tile tile, short[,] elevations)
        {
            if (!cacheElevations) return;

            byte[] cache = new byte[tileWidth * tileHeight * 2];
            int cacheIndex = 0;

            for (int y = 0; y < tileHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    short s = elevations[x, y];
                    cache[cacheIndex++] = (byte)(s & 255);
                    cache[cacheIndex++] = (byte)(s >> 8);
                }
            }

            Cache.Add(tile.GetCacheKey(cachePrefix), cache);
        }

        /// <summary>
        /// Sets the elevation data for a tile and updates the minimum and maximum elevation values.
        /// </summary>
        /// <param name="tile">The tile for which elevation data is being set.</param>
        /// <param name="elevations">The elevation data to set.</param>
        protected void SetElevationData(Tile tile, short[,] elevations)
        {
            tile.elevations = elevations;

            short max = short.MinValue;
            short min = short.MaxValue;

            for (int y = 0; y < tileHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    short v = elevations[x, y];
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }

            tile.minValue = min;
            tile.maxValue = max;

            tile.loaded = true;
            needUpdateMinMax = true;

            CheckAllTilesLoaded();

            map.Redraw();
        }

        protected override void Start()
        {
            map.OnLocationChanged += OnChangePosition;
            map.OnZoomChanged += OnChangeZoom;
            map.OnLateUpdateBefore += OnLateUpdateBefore;
        }

        /// <summary>
        /// Starts downloading elevation data for a tile
        /// </summary>
        /// <param name="tile">Tile</param>
        public abstract void StartDownloadElevationTile(Tile tile);

        /// <summary>
        /// Tries to load elevation data for a tile from the cache.
        /// </summary>
        /// <param name="tile">The tile for which elevation data is being loaded.</param>
        /// <returns>True if the elevation data was successfully loaded from the cache; otherwise, false.</returns>
        protected bool TryLoadFromCache(Tile tile)
        {
            if (!cacheElevations) return false;

            byte[] data = Cache.Get(tile.GetCacheKey(cachePrefix));
            if (data == null || data.Length != tileWidth * tileHeight * 2) return false;

            short[,] elevations = new short[tileWidth, tileHeight];
            int dataIndex = 0;

            for (int y = 0; y < tileHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    elevations[x, y] = (short)((data[dataIndex + 1] << 8) + data[dataIndex]);
                    dataIndex += 2;
                }
            }

            SetElevationData(tile, elevations);

            if (OnElevationUpdated != null) OnElevationUpdated();

            return true;
        }

        protected override void UpdateMinMax()
        {
            needUpdateMinMax = false;

            TileRect r = map.view.GetTileRect();

            minValue = short.MaxValue;
            maxValue = short.MinValue;

            int left = (int)r.left;
            int top = (int)r.top;
            int right = (int)r.right;
            int bottom = (int)r.bottom;

            foreach (KeyValuePair<ulong, Tile> pair in tiles)
            {
                Tile tile = pair.Value;
                if (!tile.loaded) continue;

                int scale = 1 << (map.view.intZoom - tile.zoom);
                int tx = tile.x * scale;
                int ty = tile.y * scale;

                if (right < tx || left > tx + scale) continue;
                if (bottom < ty || top > ty + scale) continue;

                if (tile.minValue < minValue) minValue = tile.minValue;
                if (tile.maxValue > maxValue) maxValue = tile.maxValue;
            }
        }

        /// <summary>
        /// Elevation tile
        /// </summary>
        public class Tile
        {
            /// <summary>
            /// Is the tile loaded?
            /// </summary>
            public bool loaded;

            /// <summary>
            /// Tile X
            /// </summary>
            public int x;

            /// <summary>
            /// Tile Y
            /// </summary>
            public int y;

            /// <summary>
            /// Tile zoom
            /// </summary>
            public int zoom;

            /// <summary>
            /// Minimum elevation value
            /// </summary>
            public short minValue;

            /// <summary>
            /// Maximum elevation value
            /// </summary>
            public short maxValue;

            /// <summary>
            /// Elevation data width
            /// </summary>
            public int width;

            /// <summary>
            /// Elevation data height
            /// </summary>
            public int height;

            /// <summary>
            /// Elevation values
            /// </summary>
            public short[,] elevations;

            /// <summary>
            /// Indicates whether the tile is used.
            /// </summary>
            public bool used;

            /// <summary>
            /// Center point of the tile in Mercator coordinates.
            /// </summary>
            public MercatorPoint centerMercator;

            /// <summary>
            /// Get elevation value from tile
            /// </summary>
            /// <param name="tx">Relative X (0-1)</param>
            /// <param name="ty">Relative Y (0-1)</param>
            /// <returns>Elevation value</returns>
            public float GetElevation(double tx, double ty)
            {
                if (!loaded) return 0;

                double rx = (tx - (int)tx) * (width - 1);
                double ry = (ty - (int)ty) * (height - 1);

                int x1 = (int)rx;
                int x2 = x1 + 1;
                int y1 = (int)ry;
                int y2 = y1 + 1;

                if (x2 >= width) x2 = width - 1;
                if (y2 >= height) y2 = height - 1;

                double dx = rx - x1;
                double dy = ry - y1;

                double v1 = elevations[x1, y1];
                double v2 = elevations[x2, y1];
                double v3 = elevations[x1, y2];
                double v4 = elevations[x2, y2];

                v1 = (v2 - v1) * dx + v1;
                v2 = (v4 - v3) * dx + v3;
                return (float)((v2 - v1) * dy + v1);
            }

            /// <summary>
            /// Gets the cache key for the tile.
            /// </summary>
            /// <param name="prefix">The prefix to use for the cache key.</param>
            /// <returns>The cache key for the tile.</returns>
            public string GetCacheKey(string prefix)
            {
                return prefix + TileManager.GetTileKey(zoom, x, y);
            }
        }
    }
}