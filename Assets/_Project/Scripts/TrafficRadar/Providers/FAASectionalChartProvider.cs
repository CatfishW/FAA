using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace TrafficRadar
{
    /// <summary>
    /// Current state of a sectional-chart request.  <see cref="Error"/> does
    /// not imply that the displayed texture is unusable: when available, the
    /// last successful composite remains on screen while the provider retries.
    /// </summary>
    public enum ChartLoadStatus
    {
        Idle,
        Loading,
        Ready,
        Fallback,
        Error,
        Cancelled
    }

    /// <summary>
    /// Provider for FAA VFR Sectional Chart tiles from ArcGIS services.
    /// Fetches and caches aeronautical chart tiles for background display.
    /// </summary>
    public class FAASectionalChartProvider : MonoBehaviour
    {
        [Header("Service Settings")]
        [Tooltip("FAA ArcGIS MapServer URL for sectional charts")]
        [SerializeField] private string tileServerUrl = "https://tiles.arcgis.com/tiles/ssFJjBXIUyZDrSYZ/arcgis/rest/services/VFR_Sectional/MapServer";
        
        [Tooltip("Tile request timeout in seconds")]
        [SerializeField] private float requestTimeout = 15f;
        

        [Header("Tile Settings")]
        [Tooltip("Zoom level for tiles (4-12)")]
        [Range(4, 12)]
        [SerializeField] private int zoomLevel = 8;
        
        [Tooltip("Tile size in pixels")]
        [SerializeField] private int tileSize = 256;

        [Header("Cache Settings")]
        [Tooltip("Maximum number of tiles to cache")]
        [SerializeField] private int maxCachedTiles = 50;
        
        [Tooltip("Cache expiration time in seconds")]
        [SerializeField] private float cacheExpirationSeconds = 3600f; // 1 hour

        [Header("Fallback")]
        [Tooltip("Use procedural background when tiles unavailable")]
        [SerializeField] private bool useProceduralFallback = true;
        
        [SerializeField] private Color fallbackBackgroundColor = new Color(0.1f, 0.15f, 0.2f, 1f);

        // Events
        public event System.Action<Texture2D> OnChartTileLoaded;
        public event System.Action<string> OnLoadError;

        // Cache
        private Dictionary<string, CachedTile> tileCache = new Dictionary<string, CachedTile>();
        private Texture2D currentCompositeTexture;
        private int compositeSize = 512;

        // State
        private bool isLoading;
        private float lastFetchLat;
        private float lastFetchLon;
        private float lastFetchRangeNM;
        private Coroutine fetchCoroutine;
        private int fetchGeneration;
        private bool hasLastGoodTexture;
        private bool usingProceduralFallback = true;
        private ChartLoadStatus loadStatus = ChartLoadStatus.Idle;
        private string lastError = string.Empty;
        private int lastSuccessfulTileCount;
        private float lastSuccessfulFetchRealtime = -1f;
        private System.DateTime lastSuccessfulFetchUtc = System.DateTime.MinValue;
        private float lastSuccessfulLatitude;
        private float lastSuccessfulLongitude;
        private float lastSuccessfulRangeNM;
        private bool isDestroying;

        private const float MaxMercatorLatitude = 85.05112878f;
        private const int MinimumCompositeSize = 3;
        private const int MaximumCompositeSize = 4096;

        private struct CachedTile
        {
            public Texture2D texture;
            public float timestamp;
        }

        #region Properties

        public bool IsLoading => isLoading;
        public Texture2D CurrentTexture => currentCompositeTexture;
        public ChartLoadStatus Status => loadStatus;
        public ChartLoadStatus LoadStatus => loadStatus;
        public string LastError => lastError;
        public string LastErrorMessage => lastError;
        public bool HasLastGoodTexture => hasLastGoodTexture;
        public bool IsUsingProceduralFallback => usingProceduralFallback;
        public int LastSuccessfulTileCount => lastSuccessfulTileCount;
        public float LastSuccessfulFetchTime => lastSuccessfulFetchRealtime;
        public float SecondsSinceLastSuccess => lastSuccessfulFetchRealtime < 0f
            ? -1f
            : Mathf.Max(0f, Time.realtimeSinceStartup - lastSuccessfulFetchRealtime);
        public System.DateTime LastSuccessfulFetchUtc => lastSuccessfulFetchUtc;
        public System.DateTime LastSuccessfulFetch => lastSuccessfulFetchUtc;
        public float LastSuccessfulLatitude => lastSuccessfulLatitude;
        public float LastSuccessfulLongitude => lastSuccessfulLongitude;
        public float LastSuccessfulRangeNM => lastSuccessfulRangeNM;
        public float LastRequestedLatitude => lastFetchLat;
        public float LastRequestedLongitude => lastFetchLon;
        public float LastRequestedRangeNM => lastFetchRangeNM;

        /// <summary>
        /// Raised whenever the request state changes.  Consumers can use this
        /// for a small non-blocking HUD status indicator instead of polling.
        /// </summary>
        public event System.Action<ChartLoadStatus> OnStatusChanged;
        
        public int ZoomLevel
        {
            get => zoomLevel;
            set => zoomLevel = Mathf.Clamp(value, 4, 12);
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            CreateCompositeTexture();
        }

        private void OnDisable()
        {
            CancelActiveFetch(true);
        }

        private void OnDestroy()
        {
            isDestroying = true;
            CancelActiveFetch(false);

            // Clean up textures
            foreach (var tile in tileCache.Values)
            {
                if (tile.texture != null)
                    Destroy(tile.texture);
            }
            tileCache.Clear();

            if (currentCompositeTexture != null)
                Destroy(currentCompositeTexture);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Fetch chart tiles for the specified location.
        /// </summary>
        public void FetchChartTiles(float latitude, float longitude, float rangeNM)
        {
            // Every call supersedes an in-flight request, including malformed
            // input.  Otherwise a previous request could complete later and
            // unexpectedly replace the HUD texture/status.
            CancelActiveFetch(false);

            if (float.IsNaN(latitude) || float.IsInfinity(latitude) ||
                float.IsNaN(longitude) || float.IsInfinity(longitude))
            {
                RecordLoadError("Invalid chart coordinates.", true);
                SetStatus(ChartLoadStatus.Error);
                return;
            }

            if (currentCompositeTexture == null)
            {
                CreateCompositeTexture();
            }

            // Adjust zoom based on range.  Keep metadata finite even when an
            // upstream simulator sends an uninitialised/overflowed value.
            float safeRange = float.IsNaN(rangeNM)
                ? 40f
                : float.IsPositiveInfinity(rangeNM)
                    ? 10000f
                    : float.IsNegativeInfinity(rangeNM)
                        ? 0.1f
                        : Mathf.Clamp(rangeNM, 0.1f, 10000f);
            zoomLevel = GetZoomForRange(safeRange);
            
            lastFetchLat = ClampLatitude(latitude);
            lastFetchLon = WrapLongitude(longitude);
            lastFetchRangeNM = safeRange;

            int requestGeneration = ++fetchGeneration;
            isLoading = true;
            Coroutine startedCoroutine = StartCoroutine(FetchTilesCoroutine(
                lastFetchLat,
                lastFetchLon,
                safeRange,
                zoomLevel,
                requestGeneration));
            if (requestGeneration == fetchGeneration && isLoading)
            {
                fetchCoroutine = startedCoroutine;
            }
            // Publish after retaining the coroutine handle.  A status
            // listener may immediately issue a replacement request; in that
            // case CancelActiveFetch can now stop this generation correctly.
            if (requestGeneration == fetchGeneration && isLoading)
            {
                SetStatus(ChartLoadStatus.Loading);
            }
        }

        /// <summary>
        /// Cancel the active request, preserving whichever chart texture is
        /// currently displayed.
        /// </summary>
        public void CancelFetch()
        {
            CancelActiveFetch(true);
        }

        /// <summary>
        /// Clear the tile cache.
        /// </summary>
        public void ClearCache()
        {
            CancelActiveFetch(true);
            foreach (var tile in tileCache.Values)
            {
                if (tile.texture != null)
                    Destroy(tile.texture);
            }
            tileCache.Clear();
        }

        /// <summary>
        /// Set the composite texture size.
        /// </summary>
        public void SetCompositeSize(int size)
        {
            compositeSize = Mathf.Clamp(size, MinimumCompositeSize, MaximumCompositeSize);
            CreateCompositeTexture();
        }

        #endregion

        #region Private Methods

        private void CreateCompositeTexture()
        {
            if (currentCompositeTexture != null)
                Destroy(currentCompositeTexture);

            currentCompositeTexture = new Texture2D(compositeSize, compositeSize, TextureFormat.RGBA32, false);
            currentCompositeTexture.name = "FAA Sectional Chart Composite";
            currentCompositeTexture.wrapMode = TextureWrapMode.Clamp;
            currentCompositeTexture.filterMode = FilterMode.Bilinear;

            // Fill with fallback color
            Color32[] pixels = new Color32[compositeSize * compositeSize];
            Color32 bgColor = fallbackBackgroundColor;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = bgColor;
            }
            currentCompositeTexture.SetPixels32(pixels);
            currentCompositeTexture.Apply();
            usingProceduralFallback = true;
        }

        private IEnumerator FetchTilesCoroutine(
            float latitude,
            float longitude,
            float rangeNM,
            int requestZoom,
            int requestGeneration)
        {
            // Get the center tile coordinates
            var centerTile = LatLonToTile(latitude, longitude, requestZoom);
            int tileCountPerAxis = 1 << requestZoom;
            
            // Fetch center tile and surrounding tiles (3x3 grid)
            List<(int x, int y, int offsetX, int offsetY)> tilesToFetch = new List<(int, int, int, int)>();
            
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int tileX = WrapTileX(centerTile.x + dx, tileCountPerAxis);
                    int tileY = ClampTileY(centerTile.y + dy, tileCountPerAxis);
                    tilesToFetch.Add((tileX, tileY, dx, dy));
                }
            }

            List<Texture2D> fetchedTiles = new List<Texture2D>();
            List<(int offsetX, int offsetY)> tileOffsets = new List<(int, int)>();

            foreach (var tile in tilesToFetch)
            {
                if (requestGeneration != fetchGeneration)
                {
                    yield break;
                }

                string cacheKey = $"{requestZoom}_{tile.x}_{tile.y}";
                
                // Check cache first
                if (tileCache.TryGetValue(cacheKey, out CachedTile cached))
                {
                    if (Time.time - cached.timestamp < Mathf.Max(0f, cacheExpirationSeconds) && cached.texture != null)
                    {
                        fetchedTiles.Add(cached.texture);
                        tileOffsets.Add((tile.offsetX, tile.offsetY));
                        continue;
                    }

                    // Do not leave expired textures alive when replacing a
                    // cache entry; they are no longer referenced by the cache.
                    if (cached.texture != null)
                    {
                        Destroy(cached.texture);
                    }
                    tileCache.Remove(cacheKey);
                }

                // Fetch from server
                yield return FetchSingleTile(tile.x, tile.y, requestZoom, requestGeneration, (tex) =>
                {
                    if (requestGeneration != fetchGeneration || tex == null)
                    {
                        return;
                    }

                    // Add to cache
                    tileCache[cacheKey] = new CachedTile { texture = tex, timestamp = Time.time };
                    fetchedTiles.Add(tex);
                    tileOffsets.Add((tile.offsetX, tile.offsetY));
                });
            }

            if (requestGeneration != fetchGeneration)
            {
                yield break;
            }

            // Composite tiles into single texture
            if (fetchedTiles.Count > 0)
            {
                CompositeTiles(fetchedTiles, tileOffsets);
                // Trim only after compositing so a low cache limit cannot
                // destroy a fetched tile before the composite has read it.
                TrimCache();
                hasLastGoodTexture = true;
                usingProceduralFallback = false;
                lastSuccessfulTileCount = fetchedTiles.Count;
                lastSuccessfulFetchRealtime = Time.realtimeSinceStartup;
                lastSuccessfulFetchUtc = System.DateTime.UtcNow;
                lastSuccessfulLatitude = latitude;
                lastSuccessfulLongitude = longitude;
                lastSuccessfulRangeNM = rangeNM;
                lastError = string.Empty;
                SetStatus(ChartLoadStatus.Ready);
            }
            else if (!hasLastGoodTexture && useProceduralFallback)
            {
                GenerateProceduralBackground(latitude, longitude);
                usingProceduralFallback = true;
                SetStatus(ChartLoadStatus.Fallback);
            }
            else
            {
                // Keep the last successful composite in place.  If no request
                // has ever succeeded and fallback is disabled, expose a useful
                // error state without replacing the current texture.
                SetStatus(ChartLoadStatus.Error);
            }

            // A status listener may have started a replacement request from
            // inside SetStatus.  Do not let this completed generation clear
            // the replacement's loading state or coroutine handle.
            if (requestGeneration != fetchGeneration)
            {
                yield break;
            }

            isLoading = false;
            fetchCoroutine = null;
            PublishChartTexture();
        }

        private IEnumerator FetchSingleTile(
            int x,
            int y,
            int requestZoom,
            int requestGeneration,
            System.Action<Texture2D> callback)
        {
            // FAA ArcGIS tile URL format
            string url = $"{tileServerUrl}/tile/{requestZoom}/{y}/{x}";

            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = Mathf.Max(1, Mathf.RoundToInt(requestTimeout));
                yield return request.SendWebRequest();

                if (requestGeneration != fetchGeneration)
                {
                    yield break;
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(request);
                    callback?.Invoke(tex);
                }
                else
                {
                    string error = string.IsNullOrEmpty(request.error)
                        ? "Chart tile request failed."
                        : request.error;
                    Debug.LogWarning($"[FAASectionalChartProvider] Failed to fetch tile: {error}");
                    RecordLoadError(error, true);
                    callback?.Invoke(null);
                }
            }
        }

        private void PublishChartTexture()
        {
            if (isDestroying)
            {
                return;
            }

            try
            {
                OnChartTileLoaded?.Invoke(currentCompositeTexture);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void CompositeTiles(List<Texture2D> tiles, List<(int offsetX, int offsetY)> offsets)
        {
            Color32[] compositePixels = new Color32[compositeSize * compositeSize];
            
            // Fill with fallback color first
            Color32 bgColor = fallbackBackgroundColor;
            for (int i = 0; i < compositePixels.Length; i++)
            {
                compositePixels[i] = bgColor;
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                if (i >= offsets.Count)
                {
                    break;
                }

                var offset = offsets[i];

                if (tile == null || !tile.isReadable || tile.width <= 0 || tile.height <= 0)
                {
                    continue;
                }

                // Derive each destination rectangle from integer boundaries.
                // Using compositeSize / 3 for both origin and extent leaves
                // gaps (or overruns) whenever the composite is not divisible
                // by three, e.g. the default 512x512 texture.
                int startX = TileBoundary(offset.offsetX + 1, compositeSize);
                int endX = TileBoundary(offset.offsetX + 2, compositeSize);
                int startY = TileBoundary(offset.offsetY + 1, compositeSize);
                int endY = TileBoundary(offset.offsetY + 2, compositeSize);

                if (endX <= startX || endY <= startY)
                {
                    continue;
                }

                // Sample and copy pixels
                int width = endX - startX;
                int height = endY - startY;
                for (int destY = startY; destY < endY; destY++)
                {
                    float sampleY = (destY - startY + 0.5f) / height;
                    for (int destX = startX; destX < endX; destX++)
                    {
                        // Sample from tile texture.  The destination bounds
                        // are clamped by TileBoundary, so the array index is
                        // always valid even for odd composite sizes.
                        float sampleX = (destX - startX + 0.5f) / width;
                        Color c = tile.GetPixelBilinear(sampleX, sampleY);
                        compositePixels[destY * compositeSize + destX] = c;
                    }
                }
            }

            currentCompositeTexture.SetPixels32(compositePixels);
            currentCompositeTexture.Apply();
        }

        private static int TileBoundary(int tileIndex, int size)
        {
            // tileIndex is expected to be 0, 1, 2, or 3.  Clamp defensively so
            // malformed offsets cannot produce an invalid pixel index.
            int safeIndex = Mathf.Clamp(tileIndex, 0, 3);
            return Mathf.Clamp((safeIndex * size) / 3, 0, size);
        }

        private void GenerateProceduralBackground(float latitude, float longitude)
        {
            Color32[] pixels = new Color32[compositeSize * compositeSize];
            
            // Create a subtle grid pattern
            for (int y = 0; y < compositeSize; y++)
            {
                for (int x = 0; x < compositeSize; x++)
                {
                    Color32 c = fallbackBackgroundColor;
                    
                    // Add grid lines
                    if (x % 32 == 0 || y % 32 == 0)
                    {
                        c = new Color32(
                            (byte)Mathf.Min(255, c.r + 20),
                            (byte)Mathf.Min(255, c.g + 20),
                            (byte)Mathf.Min(255, c.b + 20),
                            c.a
                        );
                    }

                    pixels[y * compositeSize + x] = c;
                }
            }

            currentCompositeTexture.SetPixels32(pixels);
            currentCompositeTexture.Apply();
            usingProceduralFallback = true;
        }

        private (int x, int y) LatLonToTile(float lat, float lon, int zoom)
        {
            int safeZoom = Mathf.Clamp(zoom, 0, 30);
            int n = 1 << safeZoom;
            float safeLat = ClampLatitude(lat);
            float safeLon = WrapLongitude(lon);

            // This form of the Web Mercator equation remains finite at the
            // latitude clamp and avoids tan/cos overflow near the poles.
            float sinLatitude = Mathf.Sin(safeLat * Mathf.Deg2Rad);
            float normalizedX = (safeLon + 180f) / 360f;
            float normalizedY = 0.5f - Mathf.Log((1f + sinLatitude) / (1f - sinLatitude)) /
                (4f * Mathf.PI);

            int x = Mathf.FloorToInt(normalizedX * n);
            int y = Mathf.FloorToInt(normalizedY * n);
            return (WrapTileX(x, n), ClampTileY(y, n));
        }

        private static float ClampLatitude(float latitude)
        {
            if (float.IsNaN(latitude))
            {
                return 0f;
            }

            if (float.IsNegativeInfinity(latitude))
            {
                return -MaxMercatorLatitude;
            }

            if (float.IsPositiveInfinity(latitude))
            {
                return MaxMercatorLatitude;
            }

            return Mathf.Clamp(latitude, -MaxMercatorLatitude, MaxMercatorLatitude);
        }

        private static float WrapLongitude(float longitude)
        {
            if (float.IsNaN(longitude) || float.IsInfinity(longitude))
            {
                return 0f;
            }

            float wrapped = longitude % 360f;
            if (wrapped >= 180f)
            {
                wrapped -= 360f;
            }
            else if (wrapped < -180f)
            {
                wrapped += 360f;
            }

            return wrapped;
        }

        private static int WrapTileX(int x, int tileCount)
        {
            if (tileCount <= 0)
            {
                return 0;
            }

            int wrapped = x % tileCount;
            return wrapped < 0 ? wrapped + tileCount : wrapped;
        }

        private static int ClampTileY(int y, int tileCount)
        {
            return Mathf.Clamp(y, 0, Mathf.Max(0, tileCount - 1));
        }

        private int GetZoomForRange(float rangeNM)
        {
            // Map range to appropriate zoom level
            if (rangeNM <= 5) return 11;
            if (rangeNM <= 10) return 10;
            if (rangeNM <= 20) return 9;
            if (rangeNM <= 40) return 8;
            if (rangeNM <= 80) return 7;
            return 6;
        }

        private void CancelActiveFetch(bool publishStatus)
        {
            // Increment the generation before stopping the coroutine so any
            // request callback that returns during disposal is ignored.
            fetchGeneration++;
            if (fetchCoroutine != null)
            {
                StopCoroutine(fetchCoroutine);
                fetchCoroutine = null;
            }

            bool wasLoading = isLoading;
            isLoading = false;
            if (publishStatus && wasLoading && isActiveAndEnabled && !isDestroying)
            {
                SetStatus(ChartLoadStatus.Cancelled);
            }
        }

        private void SetStatus(ChartLoadStatus nextStatus)
        {
            if (loadStatus == nextStatus)
            {
                return;
            }

            loadStatus = nextStatus;
            if (isDestroying)
            {
                return;
            }

            try
            {
                OnStatusChanged?.Invoke(nextStatus);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void RecordLoadError(string message, bool publishEvent)
        {
            lastError = string.IsNullOrEmpty(message)
                ? "Chart tile request failed."
                : message;

            if (!publishEvent || isDestroying)
            {
                return;
            }

            try
            {
                OnLoadError?.Invoke(lastError);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void TrimCache()
        {
            // Keep at least one tile alive so a configured value of zero cannot
            // destroy the texture that the active composite is about to read.
            int maximum = Mathf.Max(1, maxCachedTiles);
            while (tileCache.Count > maximum)
            {
                // Remove the oldest entry.  The loop (rather than a single
                // removal) also handles a cache-size reduction at runtime.
                float oldestTime = float.MaxValue;
                string oldestKey = null;
                foreach (var kvp in tileCache)
                {
                    if (kvp.Value.timestamp < oldestTime)
                    {
                        oldestTime = kvp.Value.timestamp;
                        oldestKey = kvp.Key;
                    }
                }

                if (oldestKey == null)
                {
                    break;
                }

                if (tileCache[oldestKey].texture != null)
                {
                    Destroy(tileCache[oldestKey].texture);
                }
                tileCache.Remove(oldestKey);
            }
        }

        #endregion
    }
}
