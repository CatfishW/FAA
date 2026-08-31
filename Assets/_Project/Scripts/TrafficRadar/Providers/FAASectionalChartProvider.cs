using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace TrafficRadar
{
    /// <summary>
    /// Basemap choices exposed by the pilot-focus traffic map.  The FAA
    /// sources use ArcGIS tile endpoints while StreetMap uses a standard
    /// XYZ template.  URLs remain serialized/configurable so an operator can
    /// point the provider at an approved mirror without changing code.
    /// </summary>
    public enum FAAChartMapSource
    {
        Sectional,
        TerminalArea,
        WorldAeronautical,
        StreetMap,
        Custom
    }

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
        private const string LegacyTerminalAreaUrl = "https://tiles.arcgis.com/tiles/ssFJjBXIUyZDrSYZ/arcgis/rest/services/VFR_Terminal_Area_Chart/MapServer";
        private const string LegacyWorldAeronauticalUrl = "https://tiles.arcgis.com/tiles/ssFJjBXIUyZDrSYZ/arcgis/rest/services/World_Aeronautical_Chart/MapServer";
        private const string DefaultTerminalAreaUrl = "https://tiles.arcgis.com/tiles/ssFJjBXIUyZDrSYZ/arcgis/rest/services/VFR_Terminal/MapServer";
        private const string DefaultWorldAeronauticalUrl = "https://services.arcgisonline.com/ArcGIS/rest/services/Specialty/World_Navigation_Charts/MapServer";

        [Header("Service Settings")]
        [Tooltip("FAA ArcGIS MapServer URL for sectional charts")]
        [SerializeField] private string tileServerUrl = "https://tiles.arcgis.com/tiles/ssFJjBXIUyZDrSYZ/arcgis/rest/services/VFR_Sectional/MapServer";

        [Header("Map Source")]
        [Tooltip("Basemap used behind the traffic symbols. Sectional is the FAA VFR sectional chart.")]
        [SerializeField] private FAAChartMapSource mapSource = FAAChartMapSource.Sectional;

        [Tooltip("FAA ArcGIS MapServer URL for terminal-area charts.")]
        [SerializeField] private string terminalAreaTileServerUrl = "https://tiles.arcgis.com/tiles/ssFJjBXIUyZDrSYZ/arcgis/rest/services/VFR_Terminal/MapServer";

        [Tooltip("FAA ArcGIS MapServer URL for world aeronautical charts.")]
        [SerializeField] private string worldAeronauticalTileServerUrl = "https://services.arcgisonline.com/ArcGIS/rest/services/Specialty/World_Navigation_Charts/MapServer";

        [Tooltip("XYZ tile URL template for the StreetMap source. Use {z}, {x}, and {y} tokens.")]
        [SerializeField] private string streetMapTileUrlTemplate = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

        [Tooltip("Optional custom XYZ/ArcGIS tile URL template. Use {z}, {x}, and {y} tokens, or an ArcGIS MapServer base URL.")]
        [SerializeField] private string customTileUrlTemplate = string.Empty;
        
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

        [Tooltip("When an FAA chart has no coverage at the aircraft position, automatically use the global World Aeronautical Chart source instead of leaving a stale/blank map.")]
        [SerializeField] private bool fallbackToWorldAeronautical = true;
        
        [SerializeField] private Color fallbackBackgroundColor = new Color(0.1f, 0.15f, 0.2f, 1f);

        // Events
        public event System.Action<Texture2D> OnChartTileLoaded;
        public event System.Action<string> OnLoadError;

        // Cache
        private Dictionary<string, CachedTile> tileCache = new Dictionary<string, CachedTile>();
        private Texture2D currentCompositeTexture;
        // Keep the chart composite at a native enough resolution for the
        // enlarged XR-3/focus view.  The previous 512px texture was routinely
        // up-scaled to an 800-1600px circular scope, making chart labels and
        // symbology appear soft even when the source tiles were available.
        // 1024px keeps the same 3x3 tile layout and memory footprint modest
        // (4 MiB for RGBA32) while providing a much sharper presentation.
        private int compositeSize = 1024;

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
        // A no-coverage fallback waits one frame before changing the source.
        // Display updates can arrive during that window; suppress duplicate
        // requests for the failed source until the fallback has resolved.
        private bool worldFallbackPending;
        private FAAChartMapSource worldFallbackPendingSource;
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

        /// <summary>
        /// Raised after the active basemap changes.  The display and controls
        /// use this to refresh a compact source label without polling.
        /// </summary>
        public event System.Action<FAAChartMapSource> OnMapSourceChanged;
        
        public int ZoomLevel
        {
            get => zoomLevel;
            set => zoomLevel = Mathf.Clamp(value, 4, 12);
        }

        /// <summary>
        /// Currently selected basemap source.
        /// </summary>
        public FAAChartMapSource MapSource
        {
            get => mapSource;
            set => SetMapSource(value);
        }

        /// <summary>
        /// Human-readable source name suitable for a small HUD label.
        /// </summary>
        public string MapSourceName => GetMapSourceDisplayName(mapSource);

        /// <summary>
        /// Attribution text for the active basemap.  Hosts should keep this
        /// visible in a map drawer/about affordance when StreetMap is used.
        /// </summary>
        public string MapSourceAttribution => GetMapSourceAttribution(mapSource);

        public int MapSourceCount => 5;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            NormalizeBuiltInSourceUrls();
            CreateCompositeTexture();
        }

        private void OnDisable()
        {
            worldFallbackPending = false;
            CancelActiveFetch(true);
        }

        private void OnDestroy()
        {
            isDestroying = true;
            worldFallbackPending = false;
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
            // BeginWorldAeronauticalFallback intentionally yields one frame so
            // the failed generation can unwind.  Ignore display-driven retries
            // for that same source during the hand-off; otherwise a moving
            // aircraft can create an unbounded stream of duplicate 404s.
            if (worldFallbackPending && mapSource == worldFallbackPendingSource)
            {
                return;
            }

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
            zoomLevel = ResolveZoomForRange(safeRange);
            
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
        /// Select a basemap and refresh the current location.  The previous
        /// composite remains visible while the replacement tiles load, so a
        /// source switch never flashes an empty panel in the headset.
        /// </summary>
        public void SetMapSource(FAAChartMapSource source)
        {
            if (mapSource == source)
            {
                return;
            }

            worldFallbackPending = false;
            mapSource = source;
            ClearCache();
            try
            {
                OnMapSourceChanged?.Invoke(mapSource);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (isActiveAndEnabled && IsValidFetchPosition(lastFetchLat, lastFetchLon))
            {
                FetchChartTiles(lastFetchLat, lastFetchLon, lastFetchRangeNM);
            }
        }

        /// <summary>
        /// Cycle to the next configured source.  Custom is included only when
        /// a custom template has been supplied; otherwise it is skipped.
        /// </summary>
        public void CycleMapSource()
        {
            int sourceCount = MapSourceCount;
            int next = (int)mapSource;
            for (int i = 0; i < sourceCount; i++)
            {
                next = (next + 1) % sourceCount;
                FAAChartMapSource candidate = (FAAChartMapSource)next;
                if (candidate != FAAChartMapSource.Custom || !string.IsNullOrWhiteSpace(customTileUrlTemplate))
                {
                    SetMapSource(candidate);
                    return;
                }
            }
        }

        /// <summary>
        /// UnityEvent-friendly source setter for generated control strips.
        /// Values outside the enum range are clamped to the nearest source.
        /// </summary>
        public void SetMapSource(int sourceIndex)
        {
            int clamped = Mathf.Clamp(sourceIndex, 0, MapSourceCount - 1);
            SetMapSource((FAAChartMapSource)clamped);
        }

        /// <summary>
        /// Configure a custom XYZ or ArcGIS tile endpoint at runtime.  The
        /// template accepts {z}, {x}, and {y}; an ArcGIS MapServer base URL is
        /// also accepted and receives /tile/{z}/{y}/{x}.
        /// </summary>
        public void SetCustomTileUrlTemplate(string template, bool selectSource = true)
        {
            string nextTemplate = template ?? string.Empty;
            bool changed = !string.Equals(customTileUrlTemplate, nextTemplate, System.StringComparison.Ordinal);
            customTileUrlTemplate = nextTemplate;
            if (selectSource)
            {
                if (mapSource != FAAChartMapSource.Custom)
                {
                    SetMapSource(FAAChartMapSource.Custom);
                }
                else if (changed)
                {
                    // SetMapSource intentionally returns for an unchanged
                    // enum value.  A custom URL can change without changing
                    // that value, so explicitly invalidate/refetch it here.
                    ClearCache();
                    try
                    {
                        OnMapSourceChanged?.Invoke(mapSource);
                    }
                    catch (System.Exception exception)
                    {
                        Debug.LogException(exception, this);
                    }

                    if (isActiveAndEnabled && IsValidFetchPosition(lastFetchLat, lastFetchLon))
                    {
                        FetchChartTiles(lastFetchLat, lastFetchLon, lastFetchRangeNM);
                    }
                }
            }
            else if (changed && mapSource == FAAChartMapSource.Custom)
            {
                ClearCache();
                if (isActiveAndEnabled && IsValidFetchPosition(lastFetchLat, lastFetchLon))
                {
                    FetchChartTiles(lastFetchLat, lastFetchLon, lastFetchRangeNM);
                }
            }
        }

        private static bool IsValidFetchPosition(float latitude, float longitude)
        {
            return !float.IsNaN(latitude) && !float.IsInfinity(latitude) &&
                   !float.IsNaN(longitude) && !float.IsInfinity(longitude) &&
                   (Mathf.Abs(latitude) > 0.00001f || Mathf.Abs(longitude) > 0.00001f);
        }

        /// <summary>
        /// Cancel the active request, preserving whichever chart texture is
        /// currently displayed.
        /// </summary>
        public void CancelFetch()
        {
            worldFallbackPending = false;
            CancelActiveFetch(true);
        }

        /// <summary>
        /// Clear the tile cache.
        /// </summary>
        public void ClearCache()
        {
            worldFallbackPending = false;
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
            int failedTileCount = 0;
            int notFoundTileCount = 0;
            string firstTileError = string.Empty;

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
                }, (error, responseCode) =>
                {
                    failedTileCount++;
                    if (responseCode == 404)
                    {
                        notFoundTileCount++;
                    }
                    if (string.IsNullOrEmpty(firstTileError))
                    {
                        firstTileError = error;
                    }
                });
            }

            if (requestGeneration != fetchGeneration)
            {
                yield break;
            }

            if (failedTileCount > 0)
            {
                string failureSummary = $"{failedTileCount}/{tilesToFetch.Count} chart tiles unavailable" +
                                         (string.IsNullOrEmpty(firstTileError) ? "." : $" ({firstTileError}).");
                // One concise diagnostic per generation is considerably more
                // useful than nine identical 404 warnings for a chart that is
                // simply outside FAA coverage.
                RecordLoadError(failureSummary, false);
                if (fetchedTiles.Count == 0)
                {
                    Debug.LogWarning($"[FAASectionalChartProvider] {failureSummary} (source {mapSource}).");
                }
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
            else if (fetchedTiles.Count == 0 &&
                     tilesToFetch.Count > 0 &&
                     failedTileCount == tilesToFetch.Count &&
                     notFoundTileCount == tilesToFetch.Count &&
                     fallbackToWorldAeronautical &&
                     (mapSource == FAAChartMapSource.Sectional || mapSource == FAAChartMapSource.TerminalArea))
            {
                // FAA sectional and terminal charts are intentionally bounded
                // to their published coverage.  X-Plane can place an aircraft
                // offshore or outside the CONUS, where every FAA tile returns
                // 404.  Switch to the worldwide aeronautical source on the
                // next frame so the pilot receives a useful map without a
                // re-entrant CancelCoroutine call from this enumerator.
                FAAChartMapSource failedSource = mapSource;
                worldFallbackPending = true;
                worldFallbackPendingSource = failedSource;
                isLoading = false;
                fetchCoroutine = null;
                StartCoroutine(BeginWorldAeronauticalFallback(
                    failedSource,
                    latitude,
                    longitude,
                    rangeNM,
                    requestGeneration));
                yield break;
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

        private IEnumerator BeginWorldAeronauticalFallback(
            FAAChartMapSource failedSource,
            float latitude,
            float longitude,
            float rangeNM,
            int failedGeneration)
        {
            // Let the failed generation finish unwinding before changing the
            // source.  This keeps Unity's coroutine bookkeeping deterministic
            // when a tile callback completes on the same frame.
            yield return null;

            if (isDestroying || failedGeneration != fetchGeneration || mapSource != failedSource)
            {
                if (worldFallbackPending && worldFallbackPendingSource == failedSource)
                {
                    worldFallbackPending = false;
                }
                yield break;
            }

            worldFallbackPending = false;
            SetMapSource(FAAChartMapSource.WorldAeronautical);
            // SetMapSource normally refetches from the last requested
            // position.  Explicitly use the captured values when a provider
            // was refreshed before its first successful request.
            if (!isLoading && isActiveAndEnabled)
            {
                FetchChartTiles(latitude, longitude, rangeNM);
            }
        }

        private IEnumerator FetchSingleTile(
            int x,
            int y,
            int requestZoom,
            int requestGeneration,
            System.Action<Texture2D> callback,
            System.Action<string, long> errorCallback)
        {
            string url = BuildTileUrl(x, y, requestZoom);

            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = Mathf.Max(1, Mathf.RoundToInt(requestTimeout));
                yield return request.SendWebRequest();

                if (requestGeneration != fetchGeneration)
                {
                    yield break;
                }

                Texture2D decodedTexture = TryDecodeTile(request);
                if (decodedTexture != null)
                {
                    callback?.Invoke(decodedTexture);
                }
                else
                {
                    string error = string.IsNullOrEmpty(request.error)
                        ? "Chart tile request failed."
                        : request.error;
                    int byteCount = request.downloadHandler != null && request.downloadHandler.data != null
                        ? request.downloadHandler.data.Length
                        : 0;
                    // Keep diagnostics useful without echoing a custom URL
                    // that may contain an access token or other query secret.
                    string detail = request.responseCode > 0
                        ? $"{error} HTTP {request.responseCode}, {byteCount} bytes"
                        : error;
                    errorCallback?.Invoke(detail, request.responseCode);
                    callback?.Invoke(null);
                }
            }
        }

        private Texture2D TryDecodeTile(UnityWebRequest request)
        {
            if (request == null)
            {
                return null;
            }

            // DownloadHandlerTexture is the fast path and preserves the old
            // behavior for FAA ArcGIS tiles. Some public XYZ/ArcGIS mirrors
            // return a generic content type or trigger Unity's native texture
            // decoder to report DataProcessingError even though the response
            // body is a valid PNG/JPEG. Fall back to LoadImage so switching
            // basemaps remains useful on those endpoints.
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(request);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"[FAASectionalChartProvider] Native tile decode failed: {exception.Message}");
                }
            }

            byte[] bytes = request.downloadHandler != null ? request.downloadHandler.data : null;
            if (bytes == null || bytes.Length < 16 ||
                (request.responseCode < 200 || request.responseCode >= 300))
            {
                return null;
            }

            Texture2D fallback = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool loaded;
            try
            {
                loaded = fallback.LoadImage(bytes, false);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[FAASectionalChartProvider] Managed tile decode failed: {exception.Message}");
                loaded = false;
            }

            if (!loaded)
            {
                Destroy(fallback);
                return null;
            }

            fallback.wrapMode = TextureWrapMode.Clamp;
            fallback.filterMode = FilterMode.Bilinear;
            return fallback;
        }

        private string BuildTileUrl(int x, int y, int zoom)
        {
            string template = ResolveMapSourceTemplate();
            if (string.IsNullOrWhiteSpace(template))
            {
                template = tileServerUrl;
            }

            template = template.Trim();
            if (template.IndexOf("{z}", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                template.IndexOf("{x}", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                template.IndexOf("{y}", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return template
                    .Replace("{z}", zoom.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Replace("{x}", x.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Replace("{y}", y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            // ArcGIS MapServer endpoints use z/y/x ordering.  Keep the old
            // default URL behavior exactly intact for existing scenes.
            return template.TrimEnd('/') + $"/tile/{zoom}/{y}/{x}";
        }

        private string ResolveMapSourceTemplate()
        {
            NormalizeBuiltInSourceUrls();
            switch (mapSource)
            {
                case FAAChartMapSource.TerminalArea:
                    return terminalAreaTileServerUrl;
                case FAAChartMapSource.WorldAeronautical:
                    return worldAeronauticalTileServerUrl;
                case FAAChartMapSource.StreetMap:
                    return streetMapTileUrlTemplate;
                case FAAChartMapSource.Custom:
                    return customTileUrlTemplate;
                case FAAChartMapSource.Sectional:
                default:
                    return tileServerUrl;
            }
        }

        private void NormalizeBuiltInSourceUrls()
        {
            // Existing scene instances may have been serialized while the
            // initial prototype used placeholder service names. Preserve
            // user-provided custom URLs, but transparently migrate those two
            // known built-in values to endpoints that serve real tiles.
            if (string.Equals(terminalAreaTileServerUrl, LegacyTerminalAreaUrl,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                terminalAreaTileServerUrl = DefaultTerminalAreaUrl;
            }

            if (string.Equals(worldAeronauticalTileServerUrl, LegacyWorldAeronauticalUrl,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                worldAeronauticalTileServerUrl = DefaultWorldAeronauticalUrl;
            }
        }

        private static string GetMapSourceDisplayName(FAAChartMapSource source)
        {
            switch (source)
            {
                case FAAChartMapSource.TerminalArea:
                    return "TERMINAL";
                case FAAChartMapSource.WorldAeronautical:
                    return "WAC";
                case FAAChartMapSource.StreetMap:
                    return "STREET";
                case FAAChartMapSource.Custom:
                    return "CUSTOM";
                case FAAChartMapSource.Sectional:
                default:
                    return "SECTIONAL";
            }
        }

        private static string GetMapSourceAttribution(FAAChartMapSource source)
        {
            switch (source)
            {
                case FAAChartMapSource.StreetMap:
                    return "© OpenStreetMap contributors";
                case FAAChartMapSource.WorldAeronautical:
                    return "Esri / NGA World Navigation Charts";
                case FAAChartMapSource.TerminalArea:
                case FAAChartMapSource.Sectional:
                case FAAChartMapSource.Custom:
                default:
                    return "FAA / ArcGIS";
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

        private int ResolveZoomForRange(float rangeNM)
        {
            int requested = GetZoomForRange(rangeNM);
            // FAA's VFR Terminal cache is published only at LOD 10-12.
            // Clamping here avoids a guaranteed HTML/404 response at the
            // wider 20-40 NM ranges used by the pilot-focus display.
            if (mapSource == FAAChartMapSource.TerminalArea)
            {
                return Mathf.Clamp(requested, 10, 12);
            }

            // FAA's VFR Sectional cache is published only at LOD 8-12.
            // Clamp the wider focus ranges to the lowest available sectional
            // level so requests resolve to chart tiles instead of 404s.
            if (mapSource == FAAChartMapSource.Sectional)
            {
                return Mathf.Clamp(requested, 8, 12);
            }

            // World Navigation Charts exposes LOD 0-10.  Keep the request in
            // that range while retaining the existing range-to-zoom mapping.
            if (mapSource == FAAChartMapSource.WorldAeronautical)
            {
                return Mathf.Clamp(requested, 4, 10);
            }

            return requested;
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
