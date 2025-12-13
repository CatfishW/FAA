/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// This class of buffer tile image. <br/>
    /// Please do not use it if you do not know what you're doing.<br/>
    /// Perform all operations with the map through other classes.
    /// </summary>
    public abstract class Tile: IDataContainer
    {
        #region Actions

        /// <summary>
        /// The event that occurs when all tiles are loaded.
        /// </summary>
        public static Action OnAllTilesLoaded;

        /// <summary>
        /// The event, which allows you to control the path of tile in Resources.
        /// </summary>
        public static Func<Tile, string> OnGetResourcesPath;

        /// <summary>
        /// The event, which allows you to control the path of tile in Streaming Assets.
        /// </summary>
        public static Func<Tile, string> OnGetStreamingAssetsPath;

        /// <summary>
        /// The event which allows to intercept the replacement tokens in the url.<br/>
        /// Return the value, or null - if you do not want to modify the value.
        /// </summary>
        public static Func<Tile, string, string> OnReplaceURLToken;

        /// <summary>
        /// The event which allows to intercept the replacement tokens in the traffic url.<br/>
        /// Return the value, or null - if you do not want to modify the value.
        /// </summary>
        public static Func<Tile, string, string> OnReplaceTrafficURLToken;

        /// <summary>
        /// The event, which occurs after a successful download of the tile.
        /// </summary>
        public static Action<Tile> OnTileDownloaded;

        /// <summary>
        /// The event, which occurs when a download error is occurred.
        /// </summary>
        public static Action<Tile> OnTileError;

        /// <summary>
        /// The event, which occurs after a successful download of the traffic texture.
        /// </summary>
        public static Action<Tile> OnTrafficDownloaded;

        /// <summary>
        /// This event is called when the tile is disposed.
        /// </summary>
        public Action<Tile> OnDisposed;

        #endregion

        #region Variables

        #region Static Fields

        public static object lockTiles = new object();

        /// <summary>
        /// Try again in X seconds if a download error occurred. Use 0 to repeat immediately, or -1 to not repeat at all.
        /// </summary>
        public static float tryAgainAfterSec = 10;

        /// <summary>
        /// Number of attempts to download a tile
        /// </summary>
        public static int countDownloadsAttempts = 3;

        #endregion

        #region Public Fields

        /// <summary>
        /// The coordinates of the bottom-right corner of the tile.
        /// </summary>
        public GeoPoint bottomRight;

        /// <summary>
        /// This flag indicates whether the cache is checked for a tile.
        /// </summary>
        public bool cacheChecked;

        /// <summary>
        /// Drawing elements have been changed. Used for drawing as overlay.
        /// </summary>
        public bool drawingChanged;

        /// <summary>
        /// The coordinates of the center point of the tile.
        /// </summary>
        public GeoPoint globalPosition;

        /// <summary>
        /// Tile loaded or there are parent tile colors
        /// </summary>
        public bool hasColors;

        /// <summary>
        /// Is map tile?
        /// </summary>
        public bool isMapTile;

        /// <summary>
        /// The unique tile key.
        /// </summary>
        public ulong key;

        /// <summary>
        /// Texture, which is used in the back overlay.
        /// </summary>
        public Texture2D overlayBackTexture;

        /// <summary>
        /// Back overlay transparency (0-1).
        /// </summary>
        public float overlayBackAlpha = 1;

        /// <summary>
        /// Texture, which is used in the front overlay.
        /// </summary>
        public Texture2D overlayFrontTexture;

        /// <summary>
        /// Front overlay transparency (0-1).
        /// </summary>
        public float overlayFrontAlpha = 1;

        /// <summary>
        /// The tile texture was loaded from Resources
        /// </summary>
        public bool loadedFromResources = false;

        /// <summary>
        /// Reference to parent tile.
        /// </summary>
        [NonSerialized]
        public Tile parent;

        /// <summary>
        /// Number of remaining attempts to download a tile
        /// </summary>
        public int remainDownloadsAttempts;

        /// <summary>
        /// Status of tile.
        /// </summary>
        public TileStatus status = TileStatus.idle;

        /// <summary>
        /// The coordinates of the top-left corner of the tile.
        /// </summary>
        public GeoPoint topLeft;

        /// <summary>
        /// Tile used by map
        /// </summary>
        public bool used = true;

        /// <summary>
        /// Instance of the texture loader.
        /// </summary>
        public WebRequest www;

        /// <summary>
        /// Tile X.
        /// </summary>
        public readonly int x;

        /// <summary>
        /// Tile Y.
        /// </summary>
        public readonly int y;

        /// <summary>
        /// Tile zoom.
        /// </summary>
        public readonly int zoom;

        #endregion

        #region Private Fields

        protected string _url;
        protected byte[] data;
        
        private Map _map;
        private List<object> blockers;
        private Tile[] children = new Tile[4];
        private bool hasChildren;

        #endregion

        #endregion

        #region Properties

        /// <summary>
        /// Gets custom data dictionary
        /// </summary>
        public Dictionary<string, object> customData {  get; private set; } = new Dictionary<string, object>();

        /// <summary>
        /// The tile is blocked?
        /// </summary>
        public bool isBlocked => blockers != null && blockers.Count > 0;

        /// <summary>
        /// Gets the map associated with this tile.
        /// </summary>
        public Map map => _map;

        /// <summary>
        /// Path in Resources, from where the tile will be loaded.
        /// </summary>
        public string resourcesPath
        {
            get
            {
                if (OnGetResourcesPath != null) return OnGetResourcesPath(this);
                return Regex.Replace(map.resourcesPath, @"{\w+}", CustomProviderReplaceToken);
            }
        }

        /// <summary>
        /// Path in Streaming Assets, from where the tile will be loaded.
        /// </summary>
        public string streamingAssetsPath
        {
            get
            {
                if (OnGetStreamingAssetsPath != null) return OnGetStreamingAssetsPath(this);
                return Regex.Replace(map.streamingAssetsPath, @"{\w+}", CustomProviderReplaceToken);
            }
        }

        /// <summary>
        /// Texture of tile.
        /// </summary>
        public virtual Texture2D texture { get; set; }

        /// <summary>
        /// Gets / sets custom fields value by key
        /// </summary>
        /// <param name="key">Custom field key</param>
        /// <returns>Custom field value</returns>
        public object this[string key]
        {
            get => customData.GetValueOrDefault(key);
            set => customData[key] = value;
        }

        /// <summary>
        /// URL from which will be downloaded texture.
        /// </summary>
        public abstract string url { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="x">Tile X</param>
        /// <param name="y">Tile Y</param>
        /// <param name="zoom">Tile zoom</param>
        /// <param name="map">Reference to the map</param>
        /// <param name="isMapTile">Should this tile be displayed on the map?</param>
        public Tile(int x, int y, int zoom, Map map, bool isMapTile = true)
        {
            remainDownloadsAttempts = countDownloadsAttempts;
            int maxX = 1 << zoom;
            if (x < 0) x += maxX;
            else if (x >= maxX) x -= maxX;

            this.x = x;
            this.y = y;
            this.zoom = zoom;

            _map = map;
            this.isMapTile = isMapTile;

            topLeft = map.view.projection.TileToLocation(x, y, zoom);
            bottomRight = map.view.projection.TileToLocation(x + 1, y + 1, zoom);

            globalPosition = GeoPoint.Lerp(topLeft, bottomRight, 0.5f);
            key = TileManager.GetTileKey(zoom, x, y);

            if (isMapTile) map.tileManager.Add(this);
        }

        /// <summary>
        /// Blocks the tile from disposing.
        /// </summary>
        /// <param name="blocker">The object that prohibited the disposing.</param>
        public void Block(object blocker)
        {
            if (blockers == null) blockers = new List<object>();
            blockers.Add(blocker);
        }

        protected string CustomProviderReplaceToken(Match match)
        {
            string v = match.Value.ToLower().Trim('{', '}');

            if (OnReplaceURLToken != null)
            {
                string ret = OnReplaceURLToken(this, v);
                if (ret != null) return ret;
            }

            if (v == "zoom" || v == "z") return zoom.ToString();
            if (v == "x") return x.ToString();
            if (v == "y") return y.ToString();
            if (v == "quad") return Utils.TileToQuadKey(x, y, zoom);
            return v;
        }

        /// <summary>
        /// Dispose of tile.
        /// </summary>
        public virtual void Dispose()
        {
            if (status == TileStatus.disposed) return;
            status = TileStatus.disposed;
            
            if (www != null)
            {
                www.Dispose();
                www = null;
            }

            if (isMapTile) map.tileManager.Remove(this);
            if (OnDisposed != null) OnDisposed(this);
        }

        /// <summary>
        /// Destroys the tile and releases all associated resources.
        /// </summary>
        public virtual void Destroy()
        {
            lock (lockTiles)
            {
                map.tileManager.tiles.Remove(this);
            }
            
            if (overlayBackTexture) Utils.Destroy(overlayBackTexture);
            if (overlayFrontTexture) Utils.Destroy(overlayFrontTexture);
            overlayBackTexture = null;
            overlayFrontTexture = null;
            customData = null;

            _url = null;
            data = null;
            blockers = null;

            if (hasChildren)
            {
                foreach (Tile child in children)
                {
                    if (child != null) child.parent = null;
                }
            }
            if (parent != null && parent.children != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (parent.children[i] != this) continue;
                    parent.children[i] = null;
                    break;
                }
            }
            parent = null;
            children = null;
            hasChildren = false;
            hasColors = false;
            _map = null;

            OnDisposed = null;
        }

        /// <summary>
        /// Called when the download of the tile is complete.
        /// </summary>
        public virtual void DownloadComplete()
        {
        
        }
        
        /// <summary>
        /// Gets the value from the custom data dictionary by key.
        /// </summary>
        /// <param name="key">Field key.</param>
        /// <typeparam name="T">Type of value.</typeparam>
        /// <returns>Field value.</returns>
        public T GetData<T>(string key)
        {
            object val = customData.GetValueOrDefault(key);
            return val != null? (T)val: default;
        }

        /// <summary>
        /// Gets the filtered pixel color at the normalized coordinates ( u , v ).
        /// </summary>
        /// <param name="u">The u coordinate of the pixel to get.
        /// <param name="v">The v coordinate of the pixel to get.</param>
        /// <returns>The color of the pixel at the specified coordinates.</returns>
        public abstract Color GetPixel(float u, float v);

        /// <summary>
        /// Gets rect of the tile.
        /// </summary>
        /// <returns>Rect of the tile.</returns>
        public Rect GetRect()
        {
            return new Rect((float)topLeft.x, (float)topLeft.y, (float)(bottomRight.x - topLeft.x), (float)(bottomRight.y - topLeft.y));
        }

        /// <summary>
        /// Load a tile from a WWW object
        /// </summary>
        /// <param name="www">WWW object</param>
        public void LoadFromWWW(WebRequest www)
        {
            if (status == TileStatus.disposed)
            {
                this.www = null;
                return;
            }

            if (!www.hasError && www.bytesDownloaded > 0) LoadTileFromWWW(www);
            else MarkError();
            this.www = null;
        }

        /// <summary>
        /// Loads the tile from the given WebRequest.
        /// </summary>
        /// <param name="www">The WebRequest containing the tile data.</param>
        protected abstract void LoadTileFromWWW(WebRequest www);

        /// <summary>
        /// Mark that the tile has an error
        /// </summary>
        public void MarkError()
        {
            status = TileStatus.error;
            if (OnTileError != null) OnTileError(this);
            if (tryAgainAfterSec <= 0) return;
            
            if (map && map.source != MapSource.Resources && map.source != MapSource.StreamingAssets)
            {
                remainDownloadsAttempts--;
                if (remainDownloadsAttempts > 0) map.StartCoroutine(TryDownloadAgain());
            }
        }

        /// <summary>
        /// Marks the tile loaded.
        /// </summary>
        public void MarkLoaded()
        {
            if (TileManager.OnTileLoaded != null) TileManager.OnTileLoaded(this);

            if (OnAllTilesLoaded == null) return;

            foreach (Tile tile in map.tileManager.tiles)
            {
                if (tile.status != TileStatus.loaded && tile.status != TileStatus.error)
                {
                    return;
                }
            }

            OnAllTilesLoaded();
        }

        private void SetChild(Tile tile)
        {
            if (children == null) return;
            int cx = tile.x % 2;
            int cy = tile.y % 2;
            children[cx * 2 + cy] = tile;
            hasChildren = true;
        }

        /// <summary>
        /// Set parent tile
        /// </summary>
        /// <param name="tile"></param>
        public void SetParent(Tile tile)
        {
            parent = tile;
            parent.SetChild(this);
        }

        public override string ToString()
        {
            return $"{zoom}x{x}x{y}";
        }

        private IEnumerator TryDownloadAgain()
        {
            if (tryAgainAfterSec < 0) yield break;
            if (tryAgainAfterSec > 0) yield return new WaitForSeconds(tryAgainAfterSec);

            status = TileStatus.idle;
        }

        /// <summary>
        /// Remove an object that prevents the tile from disposing.
        /// </summary>
        /// <param name="blocker">The object that prohibited the disposing.</param>
        public void Unblock(object blocker)
        {
            if (blockers == null) return;
            blockers.Remove(blocker);
        }

        #endregion
    }
}