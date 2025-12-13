/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Manages map tiles
    /// </summary>
    public class TileManager
    {
        /// <summary>
        /// The maximum number simultaneously downloading tiles.
        /// </summary>
        public static int maxTileDownloads = 5;

        /// <summary>
        /// This event is used to load a tile from the cache. Use this event only if you are implementing your own caching system.
        /// </summary>
        public static Action<Tile> OnLoadFromCache;

        /// <summary>
        /// The event occurs after generating buffer and before update control to preload tiles for tileset.
        /// </summary>
        public static Action<TileManager> OnPreloadTiles;

        /// <summary>
        /// This event is used in preparation for loading a tile.
        /// </summary>
        public static Action<Tile> OnPrepareDownloadTile;

        /// <summary>
        /// An event that occurs when loading the tile. Allows you to intercept of loading tile, and load it yourself.
        /// </summary>
        public static Action<Tile> OnStartDownloadTile;

        /// <summary>
        /// This event is occurring when a tile is loaded.
        /// </summary>
        public static Action<Tile> OnTileLoaded;
        
        /// <summary>
        /// This event is occurring when a tile is created.
        /// </summary>
        public Action<Tile> OnCreateTile;

        private Tile[] downloadTiles;
        private Dictionary<ulong, Tile> _dtiles;
        private Map _map;
        private List<Tile> unusedTiles;

        /// <summary>
        /// Dictionary of tiles
        /// </summary>
        public Dictionary<ulong, Tile> dTiles => _dtiles;

        /// <summary>
        /// List of tiles
        /// </summary>
        public List<Tile> tiles { get; set; } = new List<Tile>();

        /// <summary>
        /// Reference to the map
        /// </summary>
        public Map map => _map;

        public TileManager(Map map)
        {
            _map = map;
            unusedTiles = new List<Tile>();
            _dtiles = new Dictionary<ulong, Tile>();
        }

        /// <summary>
        /// Add a tile to the manager
        /// </summary>
        /// <param name="tile">Tile</param>
        public void Add(Tile tile)
        {
            tiles.Add(tile);
            dTiles[tile.key] = tile;
            
            OnCreateTile?.Invoke(tile);
        }
        
        /// <summary>
        /// Allows you to test the connection to the Internet.
        /// </summary>
        /// <param name="callback">Function, which will return the availability of the Internet.</param>
        public void CheckServerConnection(Action<bool> callback)
        {
            Tile tempTile = map.control.CreateTile(350, 819, 11, false);
            string url = tempTile.url;
            tempTile.Dispose();

            WebRequest checkConnectionWWW = new WebRequest(url);
            checkConnectionWWW.OnComplete += www => { callback(!www.hasError); };
        }

        public void Dispose()
        {
            OnCreateTile = null;
            downloadTiles = null;
            
            foreach (Tile tile in tiles) tile.Dispose();

            _map = null;
            _dtiles = null;
            tiles = null;
        }

        /// <summary>
        /// Gets a tile for zoom, x, y.
        /// </summary>
        /// <param name="zoom">Tile zoom</param>
        /// <param name="x">Tile X</param>
        /// <param name="y">Tile Y</param>
        /// <returns>Tile or null</returns>
        public Tile GetTile(int zoom, int x, int y)
        {
            ulong key = GetTileKey(zoom, x, y);
            if (dTiles.ContainsKey(key))
            {
                Tile tile = dTiles[key];
                if (tile.status != TileStatus.disposed) return tile;
            }
            return null;
        }

        /// <summary>
        /// Gets a tile for zoom, x, y.
        /// </summary>
        /// <param name="zoom">Tile zoom</param>
        /// <param name="x">Tile X</param>
        /// <param name="y">Tile Y</param>
        /// <param name="tile">Tile</param>
        /// <returns>True - success, false - otherwise</returns>
        public bool GetTile(int zoom, int x, int y, out Tile tile)
        {
            tile = null;
            ulong key = GetTileKey(zoom, x, y);
            Tile t;
            if (dTiles.TryGetValue(key, out t))
            {
                if (t.status != TileStatus.disposed)
                {
                    tile = t;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets a tile key for zoom, x, y.
        /// </summary>
        /// <param name="zoom">Tile zoom</param>
        /// <param name="x">Tile X</param>
        /// <param name="y">Tile Y</param>
        /// <returns>Tile key</returns>
        public static ulong GetTileKey(int zoom, int x, int y) => ((ulong)zoom << 58) + ((ulong)x << 29) + (ulong)y;

        private static void OnTileWWWComplete(WebRequest www)
        {
            Tile tile = www.GetData<Tile>("tile");
            if (tile == null) return;
            
            tile.LoadFromWWW(www);
        }

        public static void OnTrafficWWWComplete(WebRequest www)
        {
            RasterTile tile = www.GetData<RasterTile>("tile");

            if (tile == null) return;
            if (tile.trafficWWW == null || !tile.trafficWWW.isDone) return;

            if (tile.status == TileStatus.disposed)
            {
                tile.trafficWWW = null;
                return;
            }

            if (www.hasError || www.bytesDownloaded <= 0)
            {
                tile.trafficWWW = null;
                return;
            }

            if (tile.map.control.resultIsTexture)
            {
                if (tile.OnLabelDownloadComplete()) tile.map.buffer.ApplyTile(tile);
            }
            else if (tile.trafficWWW != null && tile.map.traffic)
            {
                Texture2D trafficTexture = new Texture2D(256, 256, TextureFormat.ARGB32, false)
                {
                    wrapMode = TextureWrapMode.Clamp
                };
                if (tile.map.useSoftwareJPEGDecoder) RasterTile.LoadTexture(trafficTexture, www.bytes);
                else tile.trafficWWW.LoadImageIntoTexture(trafficTexture);

                TileSetControl tsControl = tile.map.control as TileSetControl;
                if (tsControl && tsControl.compressTextures) trafficTexture.Compress(true);

                tile.trafficTexture = trafficTexture;
            }

            if (Tile.OnTrafficDownloaded != null) Tile.OnTrafficDownloaded(tile);

            tile.map.Redraw();


        }

        /// <summary>
        /// Remove tile.
        /// </summary>
        /// <param name="tile">Tile</param>
        public void Remove(Tile tile)
        {
            unusedTiles.Add(tile);
            _dtiles.Remove(tile.key);
        }

        /// <summary>
        /// Reset state of tile manager and dispose all tiles
        /// </summary>
        public void Reset()
        {
            downloadTiles = null;
            foreach (Tile tile in tiles) tile.Dispose();
            tiles.Clear();
            dTiles.Clear();
        }

        /// <summary>
        /// Start next downloads (if any).
        /// </summary>
        public void StartDownloading()
        {
            if (tiles == null) return;
            float startTime = Time.realtimeSinceStartup;

            int countDownload = 0;
            int c = 0;
            
            if (downloadTiles == null) downloadTiles = new Tile[maxTileDownloads];

            lock (Tile.lockTiles)
            {
                for (int i = 0; i < tiles.Count; i++)
                {
                    Tile tile = tiles[i];
                    if (tile.status != TileStatus.loading || tile.www == null) continue;
                    
                    countDownload++;
                    if (countDownload >= maxTileDownloads) return;
                }
                
                int needDownload = maxTileDownloads - countDownload;

                for (int i = 0; i < tiles.Count; i++)
                {
                    Tile tile = tiles[i];
                    if (tile.status != TileStatus.idle) continue;

                    if (c == 0)
                    {
                        downloadTiles[0] = tile;
                        c++;
                    }
                    else
                    {
                        int index = c;
                        int index2 = index - 1;

                        while (index2 >= 0)
                        {
                            if (downloadTiles[index2].zoom <= tile.zoom) break;

                            index2--;
                            index--;
                        }

                        if (index < needDownload)
                        {
                            for (int j = needDownload - 1; j > index; j--) downloadTiles[j] = downloadTiles[j - 1];
                            downloadTiles[index] = tile;
                            if (c < needDownload) c++;
                        }
                    }
                }
            }

            for (int i = 0; i < c; i++)
            {
                if (Time.realtimeSinceStartup - startTime > 0.02) break;
                Tile tile = downloadTiles[i];

                countDownload++;
                if (countDownload > maxTileDownloads) break;

                if (OnPrepareDownloadTile != null) OnPrepareDownloadTile(tile);

                if (OnLoadFromCache != null) OnLoadFromCache(tile);
                else if (OnStartDownloadTile != null) OnStartDownloadTile(tile);
                else StartDownloadTile(tile);
            }
        }

        /// <summary>
        /// Starts downloading of specified tile.
        /// </summary>
        /// <param name="tile">Tile to be downloaded.</param>
        public static void StartDownloadTile(Tile tile)
        {
            tile.status = TileStatus.loading;
            tile.map.StartCoroutine(StartDownloadTileAsync(tile));
        }

        private static IEnumerator StartDownloadTileAsync(Tile tile)
        {
            bool loadOnline = true;

            Map map = tile.map;
            MapSource source = map.source;
            if (source != MapSource.Online)
            {
                if (source == MapSource.Resources || source == MapSource.ResourcesAndOnline)
                {
                    yield return TryLoadFromResources(tile);
                    if (tile.status == TileStatus.error) yield break;
                    if (tile.status == TileStatus.loaded) loadOnline = false;
                }
                else if (source == MapSource.StreamingAssets || source == MapSource.StreamingAssetsAndOnline)
                {
                    yield return TryLoadFromStreamingAssets(tile);
                    if (tile.status == TileStatus.error) yield break;
                    if (tile.status == TileStatus.loaded) loadOnline = false;
                }
            }

            if (loadOnline)
            {
                if (tile.www != null)
                {
                    Debug.Log("tile has www " + tile + "   " + tile.status);
                    yield break;
                }

                if (!tile.map)
                {
                    tile.MarkError();
                    yield break;
                }

                tile.www = new WebRequest(tile.url);
                tile.www["tile"] = tile;
                tile.www.OnComplete += OnTileWWWComplete;
                tile.status = TileStatus.loading;
            }

            RasterTile rTile = tile as RasterTile;

            try
            {
                if (map.traffic && !string.IsNullOrEmpty(rTile.trafficURL))
                {
                    rTile.trafficWWW = new WebRequest(rTile.trafficURL);
                    rTile.trafficWWW["tile"] = tile;
                    rTile.trafficWWW.OnComplete += OnTrafficWWWComplete;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        
        }

        private static IEnumerator TryLoadFromResources(Tile tile)
        {
            ResourceRequest resourceRequest = Resources.LoadAsync(tile.resourcesPath);
            yield return resourceRequest;

            if (!tile.map)
            {
                if (resourceRequest.asset) Resources.UnloadAsset(resourceRequest.asset);
                tile.MarkError();
                yield break;
            }

            Texture2D texture = resourceRequest.asset as Texture2D;

            if (texture)
            {
                texture.name = tile.ToString();
            
                texture.wrapMode = TextureWrapMode.Clamp;
                tile.texture = texture;
                tile.MarkLoaded();
                tile.loadedFromResources = true;
                tile.map.Redraw();
            }
            else if (tile.map.source == MapSource.Resources)
            {
                tile.MarkError();
            }
        }

        private static IEnumerator TryLoadFromStreamingAssets(Tile tile)
        {
            if (!tile.map)
            {
                tile.MarkError();
                yield break;
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            string path = Application.streamingAssetsPath + "/" + tile.streamingAssetsPath;
#if !UNITY_ANDROID || UNITY_EDITOR
            if (!System.IO.File.Exists(path))
            {
                if (tile.map.source == MapSource.StreamingAssets) tile.MarkError();
                yield break;
            }
            byte[] bytes = System.IO.File.ReadAllBytes(path);
#else
            WebRequest www = new WebRequest(path);
            yield return www;

            if (tile.map == null)
            {
                tile.MarkError();
                yield break;
            }

            if (www.hasError)
            {
                if (tile.map.source == MapSource.StreamingAssets) tile.MarkError();
                yield break;
            }
            byte[] bytes = www.bytes;
#endif

            Texture2D texture = new Texture2D(1, 1);
            texture.LoadImage(bytes);

            texture.wrapMode = TextureWrapMode.Clamp;
            tile.texture = texture;

            tile.MarkLoaded();
            tile.map.Redraw();
#else
            if (tile.map.source == MapSource.StreamingAssets) tile.MarkError();
#endif
        }

        public void UnloadUnusedTiles()
        {
            if (unusedTiles == null) return;

            for (int i = 0; i < unusedTiles.Count; i++) unusedTiles[i].Destroy();
            unusedTiles.Clear();
        }
    }
}