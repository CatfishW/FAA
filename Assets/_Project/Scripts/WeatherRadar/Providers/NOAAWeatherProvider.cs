using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;

namespace WeatherRadar
{
    /// <summary>
    /// Multi-source weather data provider for US radar tiles and WMS.
    /// Downloads a grid of tiles for coverage, composites to a single texture,
    /// and rotates to aircraft heading for display.
    /// </summary>
    public class NOAAWeatherProvider : WeatherRadarProviderBase
    {
        [Header("Service Selection")]
        [Tooltip("Which service to use for radar data")]
        [SerializeField] private RadarService preferredService = RadarService.RainViewer;

        [Tooltip("Fallback service if preferred fails")]
        [SerializeField] private RadarService fallbackService = RadarService.IEM_NEXRAD;

        [Header("Request Settings")]
        [Tooltip("Tile size (256 or 512). Some services only support 256.")]
        [SerializeField] private int tileSize = 256;
        
        [Tooltip("Request timeout in seconds")]
        [SerializeField] private float requestTimeout = 15f;

        [Header("Coverage")]
        [Tooltip("Auto-calculate zoom based on range")]
        [SerializeField] private bool autoZoomFromRange = true;

        [Tooltip("Zoom level for tile services (lower = larger area)")]
        [SerializeField] [Range(3, 15)] private int zoomLevel = 6;
        
        [Tooltip("Tile grid size (1=1x1, 2=3x3, 3=5x5, 4=7x7)")]
        [SerializeField] [Range(1, 4)] private int tileRadius = 2;

        [Header("Refresh Settings")]
        [Tooltip("How often to check for new radar data (seconds)")]
        [SerializeField] private float dataCheckInterval = 60f;

        [Header("API Keys")]
        [SerializeField] private string xweatherClientId = "";
        [SerializeField] private string xweatherClientSecret = "";
        [SerializeField] private string tomorrowApiKey = "";
        [SerializeField] private string weatherbitApiKey = "";
        [SerializeField] private string weatherOpticsApiKey = "";
        [SerializeField] private string weatherCompanyApiKey = "";

        [Header("Xweather Options")]
        [SerializeField] private string xweatherLayers = "radar";
        [SerializeField] private string xweatherOffset = "current";
        [SerializeField] private string xweatherFormat = "png";

        [Header("Tomorrow.io Options")]
        [SerializeField] private string tomorrowField = "precipitationIntensity";
        [SerializeField] private string tomorrowFormat = "png";

        [Header("Weatherbit Options")]
        [SerializeField] private string weatherbitSource = "singleband";
        [SerializeField] private string weatherbitField = "catprecipdbz";
        [SerializeField] private string weatherbitTime = "latest";

        [Header("WeatherOptics Options")]
        [SerializeField] private string weatherOpticsEndpoint = "https://api.weatheroptics.co/tiling/weather";

        [Header("Weather Company Options")]
        [SerializeField] private string weatherCompanyProductSet = "PPAcore";
        [SerializeField] private string weatherCompanyLayer = "radar";

        [Header("Custom XYZ Options")]
        [SerializeField] private string customTileUrlTemplate = "";
        [SerializeField] [Range(3, 15)] private int customMinZoom = 3;
        [SerializeField] [Range(3, 15)] private int customMaxZoom = 15;

        public override string ProviderName => "US Weather Radar (Multi-Source)";

        private bool isRequesting;
        private string lastRadarPath = "";
        private long lastRadarTimestamp = 0;
        private float lastDataCheckTime = 0;
        private RadarService lastService;
        
        // Cached composite texture from tile grid
        private Texture2D cachedComposite;
        private float cachedCenterLat;
        private float cachedCenterLon;
        private float cachedCoverageMeters;
        
        // Tile cache
        private Dictionary<string, Texture2D> tileCache = new Dictionary<string, Texture2D>();

        public enum RadarService
        {
            RainViewer = 0,      // Global tiles (US coverage)
            NOAA_RIDGE = 1,      // US WMS (opengeo.ncep.noaa.gov)
            Fallback = 2,        // Procedural
            IEM_NEXRAD = 3,      // Iowa Mesonet tiles
            XWeather = 4,        // Xweather (Aeris) tiles
            TomorrowIO = 5,      // Tomorrow.io tiles
            Weatherbit = 6,      // Weatherbit tiles
            WeatherOptics = 7,   // WeatherOptics tiles
            WeatherCompany = 8,  // The Weather Company tiles
            CustomXYZ = 9        // Custom XYZ tile template
        }

        protected override void Start()
        {
            base.Start();
            cachedCenterLat = latitude;
            cachedCenterLon = longitude;
            lastDisplayHeading = heading;
            lastDisplayLat = latitude;
            lastDisplayLon = longitude;
            lastService = preferredService;
        }
        
        // Tracking for display updates (not refetch triggers)
        private float lastDisplayHeading;
        private float lastDisplayLat;
        private float lastDisplayLon;
        private const float DISPLAY_UPDATE_THRESHOLD = 0.01f; // Very small threshold for smooth updates
        private const float NM_TO_METERS = 1852f;
        private const float EARTH_RADIUS_METERS = 6378137f;

        protected override void Update()
        {
            base.Update();
            
            // Check if heading or position changed - update display from cached data
            bool needsDisplayUpdate = false;
            
            if (Mathf.Abs(heading - lastDisplayHeading) > 0.5f) // 0.5 degree heading change
            {
                needsDisplayUpdate = true;
                lastDisplayHeading = heading;
            }
            
            if (Mathf.Abs(latitude - lastDisplayLat) > DISPLAY_UPDATE_THRESHOLD ||
                Mathf.Abs(longitude - lastDisplayLon) > DISPLAY_UPDATE_THRESHOLD)
            {
                needsDisplayUpdate = true;
                lastDisplayLat = latitude;
                lastDisplayLon = longitude;
                
                // Check if we've moved outside cached coverage - need new fetch
                if (cachedComposite != null && cachedCoverageMeters > 0)
                {
                    GetDeltaMeters(latitude, longitude, cachedCenterLat, cachedCenterLon, out float dxMeters, out float dyMeters);
                    float distFromCenter = Mathf.Max(Mathf.Abs(dxMeters), Mathf.Abs(dyMeters));

                    if (distFromCenter > cachedCoverageMeters * 0.7f) // 70% threshold
                    {
                        Debug.Log("[NOAAWeatherProvider] Moved outside 70% coverage area - fetching new tiles");
                        if (!isRequesting)
                        {
                            GenerateRadarData();
                        }
                    }
                }
            }
            
            // Update display from cached data (but don't notify - wait for sweep completion)
            if (needsDisplayUpdate && cachedComposite != null)
            {
                UpdateRadarFromComposite(false); // false = don't notify, just update texture silently
            }
            
            // Periodically check for new radar data (RainViewer only)
            if (preferredService == RadarService.RainViewer &&
                Time.time - lastDataCheckTime > dataCheckInterval)
            {
                lastDataCheckTime = Time.time;
                if (!isRequesting)
                {
                    StartCoroutine(CheckForNewData());
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ClearCache();
        }

        private void ClearCache()
        {
            foreach (var tile in tileCache.Values)
            {
                if (tile != null) Destroy(tile);
            }
            tileCache.Clear();
            
            if (cachedComposite != null)
            {
                Destroy(cachedComposite);
                cachedComposite = null;
            }

            cachedCoverageMeters = 0f;
        }

        protected override void GenerateRadarData()
        {
            if (!isRequesting)
            {
                StartCoroutine(FetchRadarData());
            }
        }

        private IEnumerator CheckForNewData()
        {
            // Get latest radar timestamp from API
            string apiUrl = "https://api.rainviewer.com/public/weather-maps.json";
            
            using (var request = UnityWebRequest.Get(apiUrl))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    long newTimestamp = ParseRainViewerTimestamp(json);
                    
                    if (newTimestamp > lastRadarTimestamp)
                    {
                        Debug.Log($"[RainViewer] New radar data available (timestamp: {newTimestamp}) - will fetch on next sweep");
                        // Just flag that new data is available - don't fetch yet
                        // Data will be fetched when RefreshData() is called at sweep completion
                        pendingNewData = true;
                        pendingRadarPath = ParseRainViewerPath(json);
                        lastRadarTimestamp = newTimestamp;
                    }
                }
            }
        }
        
        // Flag for pending new data
        private bool pendingNewData = false;
        private string pendingRadarPath = "";

        private IEnumerator FetchRadarData()
        {
            isRequesting = true;
            SetStatus(ProviderStatus.Connecting);

            bool success = false;

            yield return TryFetchService(preferredService);
            success = status == ProviderStatus.Active;

            if (!success && fallbackService != preferredService)
            {
                Debug.Log($"[NOAAWeatherProvider] Preferred failed, trying fallback {fallbackService}");
                yield return TryFetchService(fallbackService);
                success = status == ProviderStatus.Active;
            }

            if (!success && preferredService != RadarService.RainViewer && fallbackService != RadarService.RainViewer)
            {
                Debug.Log("[NOAAWeatherProvider] Trying RainViewer fallback...");
                yield return TryFetchService(RadarService.RainViewer);
                success = status == ProviderStatus.Active;
            }

            if (!success)
            {
                Debug.LogWarning("[NOAAWeatherProvider] All services failed, using procedural fallback");
                GenerateProceduralFallback();
            }

            isRequesting = false;
        }

        private IEnumerator TryFetchService(RadarService service)
        {
            switch (service)
            {
                case RadarService.RainViewer:
                    yield return FetchRainViewerGrid();
                    yield break;
                case RadarService.NOAA_RIDGE:
                    yield return TryNOAARidge();
                    yield break;
                case RadarService.IEM_NEXRAD:
                    if (!ValidateServiceKey(service)) { yield break; }
                    yield return FetchXYZGrid(BuildIemUrl, service);
                    yield break;
                case RadarService.XWeather:
                    if (!ValidateServiceKey(service)) { yield break; }
                    yield return FetchXYZGrid(BuildXWeatherUrl, service);
                    yield break;
                case RadarService.TomorrowIO:
                    if (!ValidateServiceKey(service)) { yield break; }
                    yield return FetchTomorrowIoGrid();
                    yield break;
                case RadarService.Weatherbit:
                    if (!ValidateServiceKey(service)) { yield break; }
                    yield return FetchXYZGrid(BuildWeatherbitUrl, service);
                    yield break;
                case RadarService.WeatherOptics:
                    if (!ValidateServiceKey(service)) { yield break; }
                    yield return FetchWeatherOpticsGrid();
                    yield break;
                case RadarService.WeatherCompany:
                    if (!ValidateServiceKey(service)) { yield break; }
                    yield return FetchWeatherCompanyGrid();
                    yield break;
                case RadarService.CustomXYZ:
                    if (!ValidateServiceKey(service)) { yield break; }
                    yield return FetchXYZGrid(BuildCustomUrl, service);
                    yield break;
                case RadarService.Fallback:
                    GenerateProceduralFallback();
                    yield break;
                default:
                    yield break;
            }
        }

        private IEnumerator FetchRainViewerGrid()
        {
            // Get API info first
            string apiUrl = "https://api.rainviewer.com/public/weather-maps.json";
            
            using (var request = UnityWebRequest.Get(apiUrl))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[RainViewer] API request failed: {request.error}");
                    yield break;
                }

                string json = request.downloadHandler.text;
                string radarPath = ParseRainViewerPath(json);
                
                if (string.IsNullOrEmpty(radarPath))
                {
                    Debug.LogWarning("[RainViewer] Could not parse radar path");
                    yield break;
                }

                lastRadarPath = radarPath;
                lastRadarTimestamp = ParseRainViewerTimestamp(json);

                int effectiveZoom = GetTargetZoom(RadarService.RainViewer);
                int effectiveTileSize = GetEffectiveTileSize(RadarService.RainViewer);

                // Calculate center tile
                var centerTile = LatLonToTile(latitude, longitude, effectiveZoom);
                
                // Fetch grid of tiles
                int gridSize = tileRadius * 2 + 1;
                Texture2D[,] tiles = new Texture2D[gridSize, gridSize];
                int tilesLoaded = 0;
                int totalTiles = gridSize * gridSize;

                Debug.Log($"[RainViewer] Fetching {gridSize}x{gridSize} tile grid centered at ({latitude:F2}, {longitude:F2}) zoom {effectiveZoom}");

                for (int dy = -tileRadius; dy <= tileRadius; dy++)
                {
                    for (int dx = -tileRadius; dx <= tileRadius; dx++)
                    {
                        int tileX = centerTile.x + dx;
                        int tileY = centerTile.y + dy;
                        
                        string tileUrl = $"https://tilecache.rainviewer.com{radarPath}/{effectiveTileSize}/{effectiveZoom}/{tileX}/{tileY}/2/1_1.png";

                        using (var tileRequest = UnityWebRequestTexture.GetTexture(tileUrl))
                        {
                            tileRequest.timeout = Mathf.RoundToInt(requestTimeout);
                            yield return tileRequest.SendWebRequest();

                            if (tileRequest.result == UnityWebRequest.Result.Success)
                            {
                                Texture2D tex = DownloadHandlerTexture.GetContent(tileRequest);
                                if (tex != null)
                                {
                                    // Store tile at grid position (no flip - CompositeGridToRadar handles orientation)
                                    int gridX = dx + tileRadius;
                                    int gridY = dy + tileRadius;
                                    tiles[gridX, gridY] = tex;
                                    tilesLoaded++;
                                }
                            }
                        }
                    }
                }

                Debug.Log($"[RainViewer] Loaded {tilesLoaded}/{totalTiles} tiles");

                if (tilesLoaded > 0)
                {
                    // Composite tiles into single texture
                    CompositeGridToRadar(tiles, gridSize, effectiveTileSize);
                    
                    // Update cached coverage info
                    cachedCenterLat = latitude;
                    cachedCenterLon = longitude;
                    cachedCoverageMeters = CalculateCoverageMeters(effectiveZoom, tileRadius, latitude);
                    
                    SetStatus(ProviderStatus.Active);
                    Debug.Log($"[RainViewer] Radar composite created, coverage radius: {cachedCoverageMeters / NM_TO_METERS:F1}nm");
                }

                // Clean up individual tiles
                foreach (var tile in tiles)
                {
                    if (tile != null) Destroy(tile);
                }
            }
        }

        private IEnumerator FetchXYZGrid(Func<int, int, int, string> urlBuilder, RadarService service)
        {
            int effectiveZoom = GetTargetZoom(service);
            int effectiveTileSize = GetEffectiveTileSize(service);

            var centerTile = LatLonToTile(latitude, longitude, effectiveZoom);
            int gridSize = tileRadius * 2 + 1;
            Texture2D[,] tiles = new Texture2D[gridSize, gridSize];
            int tilesLoaded = 0;
            int totalTiles = gridSize * gridSize;

            for (int dy = -tileRadius; dy <= tileRadius; dy++)
            {
                for (int dx = -tileRadius; dx <= tileRadius; dx++)
                {
                    int tileX = centerTile.x + dx;
                    int tileY = centerTile.y + dy;
                    string tileUrl = urlBuilder(tileX, tileY, effectiveZoom);
                    if (string.IsNullOrEmpty(tileUrl)) continue;

                    using (var tileRequest = UnityWebRequestTexture.GetTexture(tileUrl))
                    {
                        tileRequest.timeout = Mathf.RoundToInt(requestTimeout);
                        yield return tileRequest.SendWebRequest();

                        if (tileRequest.result == UnityWebRequest.Result.Success)
                        {
                            Texture2D tex = DownloadHandlerTexture.GetContent(tileRequest);
                            if (tex != null)
                            {
                                int gridX = dx + tileRadius;
                                int gridY = dy + tileRadius;
                                tiles[gridX, gridY] = tex;
                                tilesLoaded++;
                            }
                        }
                    }
                }
            }

            Debug.Log($"[{service}] Loaded {tilesLoaded}/{totalTiles} tiles (zoom {effectiveZoom})");

            if (tilesLoaded > 0)
            {
                CompositeGridToRadar(tiles, gridSize, effectiveTileSize);
                cachedCenterLat = latitude;
                cachedCenterLon = longitude;
                cachedCoverageMeters = CalculateCoverageMeters(effectiveZoom, tileRadius, latitude);
                SetStatus(ProviderStatus.Active);
                Debug.Log($"[{service}] Composite coverage ~ {cachedCoverageMeters / NM_TO_METERS:F1}nm");
            }
            else
            {
                SetStatus(ProviderStatus.NoData);
            }

            foreach (var tile in tiles)
            {
                if (tile != null) Destroy(tile);
            }
        }

        private IEnumerator FetchTomorrowIoGrid()
        {
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string escapedTime = UnityWebRequest.EscapeURL(timestamp);
            string timeSegment = $"{escapedTime}.{tomorrowFormat}";

            yield return FetchXYZGrid(
                (x, y, z) => $"https://api.tomorrow.io/v4/map/tile/{z}/{x}/{y}/{tomorrowField}/{timeSegment}?apikey={tomorrowApiKey}",
                RadarService.TomorrowIO
            );
        }

        private IEnumerator FetchWeatherOpticsGrid()
        {
            string metaUrl = $"{weatherOpticsEndpoint}?token={weatherOpticsApiKey}";

            using (var request = UnityWebRequest.Get(metaUrl))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[WeatherOptics] API request failed: {request.error}");
                    yield break;
                }

                string json = request.downloadHandler.text;
                string tileTemplate = ParseWeatherOpticsTileTemplate(json);

                if (string.IsNullOrEmpty(tileTemplate))
                {
                    Debug.LogWarning("[WeatherOptics] Could not parse tile URL template");
                    yield break;
                }

                string hydratedTemplate = tileTemplate
                    .Replace("{size}", "1")
                    .Replace("{token}", weatherOpticsApiKey);

                yield return FetchXYZGrid(
                    (x, y, z) => hydratedTemplate
                        .Replace("{x}", x.ToString())
                        .Replace("{y}", y.ToString())
                        .Replace("{z}", z.ToString()),
                    RadarService.WeatherOptics
                );
            }
        }

        private IEnumerator FetchWeatherCompanyGrid()
        {
            string seriesUrl = $"https://api.weather.com/v3/TileServer/series/productSet/{weatherCompanyProductSet}?apiKey={weatherCompanyApiKey}";

            using (var request = UnityWebRequest.Get(seriesUrl))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[WeatherCompany] Series request failed: {request.error}");
                    yield break;
                }

                string json = request.downloadHandler.text;
                string ts = ParseWeatherCompanyTimestamp(json);

                if (string.IsNullOrEmpty(ts))
                {
                    Debug.LogWarning("[WeatherCompany] Could not parse series timestamp");
                    yield break;
                }

                string tileTemplate = $"https://api.weather.com/v3/TileServer/tile/{weatherCompanyLayer}?ts={ts}&xyz={{x}}:{{y}}:{{z}}&apiKey={weatherCompanyApiKey}";

                yield return FetchXYZGrid(
                    (x, y, z) => tileTemplate
                        .Replace("{x}", x.ToString())
                        .Replace("{y}", y.ToString())
                        .Replace("{z}", z.ToString()),
                    RadarService.WeatherCompany
                );
            }
        }

        private string BuildIemUrl(int x, int y, int z)
        {
            return $"https://mesonet.agron.iastate.edu/cache/tile.py/1.0.0/nexrad-n0q-900913/{z}/{x}/{y}.png";
        }

        private string BuildXWeatherUrl(int x, int y, int z)
        {
            return $"https://maps.api.xweather.com/{xweatherClientId}_{xweatherClientSecret}/{xweatherLayers}/{z}/{x}/{y}/{xweatherOffset}.{xweatherFormat}";
        }

        private string BuildWeatherbitUrl(int x, int y, int z)
        {
            return $"https://maps.weatherbit.io/v2.0/{weatherbitSource}/{weatherbitField}/{weatherbitTime}/{z}/{x}/{y}.png?key={weatherbitApiKey}";
        }

        private string BuildCustomUrl(int x, int y, int z)
        {
            if (string.IsNullOrWhiteSpace(customTileUrlTemplate))
            {
                return null;
            }

            return customTileUrlTemplate
                .Replace("{z}", z.ToString())
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString());
        }

        private void CompositeGridToRadar(Texture2D[,] tiles, int gridSize, int tilePixelSize)
        {
            int compositeSize = tilePixelSize * gridSize;
            
            if (cachedComposite == null || cachedComposite.width != compositeSize)
            {
                if (cachedComposite != null) Destroy(cachedComposite);
                cachedComposite = new Texture2D(compositeSize, compositeSize, TextureFormat.RGBA32, false);
                cachedComposite.filterMode = FilterMode.Bilinear;
            }

            // Composite all tiles
            // Web tile Y increases going south (down), but texture Y increases going up
            // So we need to flip the Y placement
            for (int gy = 0; gy < gridSize; gy++)
            {
                for (int gx = 0; gx < gridSize; gx++)
                {
                    Texture2D tile = tiles[gx, gy];
                    if (tile != null)
                    {
                        Color[] pixels = tile.GetPixels();
                        // Place tiles: X is normal, Y is flipped for correct orientation
                        int destY = (gridSize - 1 - gy) * tilePixelSize;
                        cachedComposite.SetPixels(gx * tilePixelSize, destY, tilePixelSize, tilePixelSize, pixels);
                    }
                }
            }
            cachedComposite.Apply();

            // Now crop and rotate to radar display
            UpdateRadarFromComposite();
        }

        /// <summary>
        /// Updates the radar display from cached composite, applying position offset and heading rotation
        /// </summary>
        /// <param name="notify">If true, notifies listeners that data was updated (only do this at sweep completion)</param>
        private void UpdateRadarFromComposite(bool notify = true)
        {
            if (cachedComposite == null) return;
            
            if (radarTexture == null)
            {
                InitializeTexture();
            }

            int centerX = textureSize / 2;
            int centerY = textureSize / 2;
            float radius = textureSize / 2f;
            
            // Calculate position offset in normalized coordinates (0-1)
            float offsetX = 0f, offsetY = 0f;
            if (cachedCoverageMeters > 0)
            {
                GetDeltaMeters(latitude, longitude, cachedCenterLat, cachedCenterLon, out float dxMeters, out float dyMeters);
                offsetX = dxMeters / (cachedCoverageMeters * 2f);
                offsetY = dyMeters / (cachedCoverageMeters * 2f);
            }
            
            float rangeMeters = Mathf.Max(rangeNM, 1f) * NM_TO_METERS;
            float rangeScale = cachedCoverageMeters > 0f ? rangeMeters / cachedCoverageMeters : 1f;
            if (rangeScale > 1f)
            {
                Debug.LogWarning("[NOAAWeatherProvider] Range exceeds tile coverage; clamping to available data");
                rangeScale = 1f;
            }

            // Heading rotation in radians
            float headingRad = heading * Mathf.Deg2Rad;
            float cosH = Mathf.Cos(headingRad);
            float sinH = Mathf.Sin(headingRad);

            Color32[] pixels = new Color32[textureSize * textureSize];
            Color32 clear = new Color32(0, 0, 0, 0);

            float gainMultiplier = 1f + (gainDB / 8f);
            
            // Scale factor: how much of the composite to show (centered crop)
            // The composite is (tileRadius*2+1) tiles, we want to show a centered region
            float compositeHalfW = cachedComposite.width / 2f;
            float compositeHalfH = cachedComposite.height / 2f;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float distSq = dx * dx + dy * dy;

                    if (distSq > radius * radius)
                    {
                        pixels[y * textureSize + x] = clear;
                        continue;
                    }

                    // Normalize to -1..1
                    float normX = dx / radius;
                    float normY = dy / radius;
                    
                    // Rotate by heading (north-up to heading-up)
                    float rotX = (normX * cosH - normY * sinH) * rangeScale;
                    float rotY = (normX * sinH + normY * cosH) * rangeScale;

                    // Map to composite coordinates
                    // Center of composite + rotated offset + position offset
                    float compX = compositeHalfW + (rotX - offsetX) * compositeHalfW;
                    float compY = compositeHalfH + (rotY + offsetY) * compositeHalfH;

                    // Clamp and sample
                    int sourceX = Mathf.Clamp(Mathf.RoundToInt(compX), 0, cachedComposite.width - 1);
                    int sourceY = Mathf.Clamp(Mathf.RoundToInt(compY), 0, cachedComposite.height - 1);

                    Color sourceColor = cachedComposite.GetPixel(sourceX, sourceY);

                    if (sourceColor.a > 0.1f)
                    {
                        sourceColor.r = Mathf.Clamp01(sourceColor.r * gainMultiplier);
                        sourceColor.g = Mathf.Clamp01(sourceColor.g * gainMultiplier);
                        sourceColor.b = Mathf.Clamp01(sourceColor.b * gainMultiplier);
                    }

                    pixels[y * textureSize + x] = sourceColor;
                }
            }

            radarTexture.SetPixels32(pixels);
            radarTexture.Apply();
            
            // Only notify at sweep completion, not for mid-sweep heading/position updates
            if (notify)
            {
                NotifyDataUpdated();
            }
        }

        private float CalculateCoverageMeters(int zoom, int radius, float latDeg)
        {
            int gridSize = radius * 2 + 1;
            float latRad = Mathf.Clamp(latDeg, -85f, 85f) * Mathf.Deg2Rad;
            float metersPerTile = (2f * Mathf.PI * EARTH_RADIUS_METERS / (1 << zoom)) * Mathf.Cos(latRad);
            return metersPerTile * (gridSize / 2f);
        }

        private int CalculateZoomForRange(float rangeNm, float latDeg, RadarService service)
        {
            float rangeMeters = Mathf.Max(rangeNm, 1f) * NM_TO_METERS;
            int gridSize = tileRadius * 2 + 1;
            float latRad = Mathf.Clamp(latDeg, -85f, 85f) * Mathf.Deg2Rad;
            float numerator = Mathf.Cos(latRad) * 2f * Mathf.PI * EARTH_RADIUS_METERS * (gridSize / 2f);
            if (numerator <= 0f)
            {
                return GetZoomLimits(service).min;
            }

            float zFloat = Mathf.Log(numerator / rangeMeters, 2f);
            int zoom = Mathf.FloorToInt(zFloat);
            var limits = GetZoomLimits(service);
            return Mathf.Clamp(zoom, limits.min, limits.max);
        }

        private void GetDeltaMeters(float lat, float lon, float centerLat, float centerLon, out float dxMeters, out float dyMeters)
        {
            float latRad = centerLat * Mathf.Deg2Rad;
            float metersPerDegLat = 111132.92f - 559.82f * Mathf.Cos(2f * latRad) + 1.175f * Mathf.Cos(4f * latRad);
            float metersPerDegLon = 111412.84f * Mathf.Cos(latRad) - 93.5f * Mathf.Cos(3f * latRad);

            dyMeters = (lat - centerLat) * metersPerDegLat;
            dxMeters = (lon - centerLon) * metersPerDegLon;
        }

        private bool ValidateServiceKey(RadarService service)
        {
            switch (service)
            {
                case RadarService.XWeather:
                    if (string.IsNullOrEmpty(xweatherClientId) || string.IsNullOrEmpty(xweatherClientSecret))
                    {
                        Debug.LogWarning("[Xweather] Missing client ID/secret");
                        SetStatus(ProviderStatus.Error);
                        return false;
                    }
                    return true;
                case RadarService.TomorrowIO:
                    if (string.IsNullOrEmpty(tomorrowApiKey))
                    {
                        Debug.LogWarning("[Tomorrow.io] Missing API key");
                        SetStatus(ProviderStatus.Error);
                        return false;
                    }
                    return true;
                case RadarService.Weatherbit:
                    if (string.IsNullOrEmpty(weatherbitApiKey))
                    {
                        Debug.LogWarning("[Weatherbit] Missing API key");
                        SetStatus(ProviderStatus.Error);
                        return false;
                    }
                    return true;
                case RadarService.WeatherOptics:
                    if (string.IsNullOrEmpty(weatherOpticsApiKey))
                    {
                        Debug.LogWarning("[WeatherOptics] Missing API key");
                        SetStatus(ProviderStatus.Error);
                        return false;
                    }
                    return true;
                case RadarService.WeatherCompany:
                    if (string.IsNullOrEmpty(weatherCompanyApiKey))
                    {
                        Debug.LogWarning("[WeatherCompany] Missing API key");
                        SetStatus(ProviderStatus.Error);
                        return false;
                    }
                    return true;
                case RadarService.CustomXYZ:
                    if (string.IsNullOrWhiteSpace(customTileUrlTemplate))
                    {
                        Debug.LogWarning("[CustomXYZ] Missing tile URL template");
                        SetStatus(ProviderStatus.Error);
                        return false;
                    }
                    return true;
                default:
                    return true;
            }
        }

        private bool UsesTiles(RadarService service)
        {
            return service != RadarService.NOAA_RIDGE && service != RadarService.Fallback;
        }

        private int GetTargetZoom(RadarService service)
        {
            int baseZoom = autoZoomFromRange ? CalculateZoomForRange(rangeNM, latitude, service) : zoomLevel;
            var limits = GetZoomLimits(service);
            return Mathf.Clamp(baseZoom, limits.min, limits.max);
        }

        private (int min, int max) GetZoomLimits(RadarService service)
        {
            switch (service)
            {
                case RadarService.RainViewer:
                    return (3, 7);
                case RadarService.IEM_NEXRAD:
                    return (4, 10);
                case RadarService.XWeather:
                    return (3, 12);
                case RadarService.TomorrowIO:
                    return (3, 12);
                case RadarService.Weatherbit:
                    return (3, 10);
                case RadarService.WeatherOptics:
                    return (0, 15);
                case RadarService.WeatherCompany:
                    return (3, 12);
                case RadarService.CustomXYZ:
                    int min = Mathf.Min(customMinZoom, customMaxZoom);
                    int max = Mathf.Max(customMinZoom, customMaxZoom);
                    return (min, max);
                default:
                    return (3, 10);
            }
        }

        private int GetEffectiveTileSize(RadarService service)
        {
            if (service == RadarService.RainViewer)
            {
                return tileSize >= 512 ? 512 : 256;
            }
            return 256;
        }

        private string ParseWeatherOpticsTileTemplate(string json)
        {
            try
            {
                int urlIndex = json.LastIndexOf("\"url\"");
                if (urlIndex < 0) return null;
                int colonIndex = json.IndexOf(":", urlIndex);
                int quoteStart = json.IndexOf("\"", colonIndex + 1);
                int quoteEnd = json.IndexOf("\"", quoteStart + 1);
                if (quoteStart > 0 && quoteEnd > quoteStart)
                {
                    return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                }
            }
            catch
            {
                return null;
            }
            return null;
        }

        private string ParseWeatherCompanyTimestamp(string json)
        {
            try
            {
                int radarIndex = json.IndexOf("\"radar\"");
                int tsIndex = radarIndex >= 0 ? json.IndexOf("\"ts\"", radarIndex) : json.IndexOf("\"ts\"");
                if (tsIndex < 0) return null;

                int colonIndex = json.IndexOf(":", tsIndex);
                int commaIndex = json.IndexOf(",", colonIndex);
                if (commaIndex < 0) commaIndex = json.IndexOf("}", colonIndex);

                string ts = json.Substring(colonIndex + 1, commaIndex - colonIndex - 1).Trim();
                return ts.Trim('"');
            }
            catch
            {
                return null;
            }
        }

        private long ParseRainViewerTimestamp(string json)
        {
            try
            {
                int radarIndex = json.IndexOf("\"radar\"");
                if (radarIndex < 0) return 0;

                int pastIndex = json.IndexOf("\"past\"", radarIndex);
                if (pastIndex < 0) return 0;

                int satelliteIndex = json.IndexOf("\"satellite\"", pastIndex);
                if (satelliteIndex < 0) satelliteIndex = json.Length;

                string radarSection = json.Substring(pastIndex, satelliteIndex - pastIndex);
                
                int timeIndex = radarSection.LastIndexOf("\"time\"");
                if (timeIndex < 0) return 0;

                int colonIndex = radarSection.IndexOf(":", timeIndex);
                int commaIndex = radarSection.IndexOf(",", colonIndex);
                if (commaIndex < 0) commaIndex = radarSection.IndexOf("}", colonIndex);

                string timeStr = radarSection.Substring(colonIndex + 1, commaIndex - colonIndex - 1).Trim();
                return long.Parse(timeStr);
            }
            catch
            {
                return 0;
            }
        }

        private string ParseRainViewerPath(string json)
        {
            try
            {
                int radarIndex = json.IndexOf("\"radar\"");
                if (radarIndex < 0) return null;

                int pastIndex = json.IndexOf("\"past\"", radarIndex);
                if (pastIndex < 0) return null;

                int satelliteIndex = json.IndexOf("\"satellite\"", pastIndex);
                if (satelliteIndex < 0) satelliteIndex = json.Length;

                string radarSection = json.Substring(pastIndex, satelliteIndex - pastIndex);
                
                int pathIndex = radarSection.LastIndexOf("\"path\"");
                if (pathIndex < 0) return null;

                int colonIndex = radarSection.IndexOf(":", pathIndex);
                int quoteStart = radarSection.IndexOf("\"", colonIndex + 1);
                int quoteEnd = radarSection.IndexOf("\"", quoteStart + 1);

                if (quoteStart > 0 && quoteEnd > quoteStart)
                {
                    return radarSection.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RainViewer] JSON parse error: {e.Message}");
            }
            return null;
        }

        private IEnumerator TryNOAARidge()
        {
            float latRad = Mathf.Clamp(latitude, -85f, 85f) * Mathf.Deg2Rad;
            float latDelta = rangeNM / 60f;
            float cosLat = Mathf.Max(0.1f, Mathf.Cos(latRad));
            float lonDelta = rangeNM / (60f * cosLat);

            float minLon = longitude - lonDelta;
            float maxLon = longitude + lonDelta;
            float minLat = latitude - latDelta;
            float maxLat = latitude + latDelta;
            string bbox = $"{minLat},{minLon},{maxLat},{maxLon}";

            string url = $"https://opengeo.ncep.noaa.gov/geoserver/conus/conus_bref_raw/ows?" +
                        $"service=WMS&version=1.3.0&request=GetMap" +
                        $"&layers=conus_bref_raw" +
                        $"&bbox={bbox}" +
                        $"&width={textureSize}&height={textureSize}" +
                        $"&crs=EPSG:4326&format=image/png&transparent=true";

            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = Mathf.RoundToInt(requestTimeout);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(request);
                    if (tex != null)
                    {
                        ProcessSingleTexture(tex);
                        SetStatus(ProviderStatus.Active);
                    }
                }
            }
        }

        private void ProcessSingleTexture(Texture2D source)
        {
            if (radarTexture == null) InitializeTexture();

            int centerX = textureSize / 2;
            int centerY = textureSize / 2;
            float radius = textureSize / 2f;

            Color32[] pixels = new Color32[textureSize * textureSize];
            Color32 clear = new Color32(0, 0, 0, 0);

            float scaleX = (float)source.width / textureSize;
            float scaleY = (float)source.height / textureSize;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    if (dx * dx + dy * dy > radius * radius)
                    {
                        pixels[y * textureSize + x] = clear;
                        continue;
                    }

                    int sourceX = Mathf.Clamp(Mathf.FloorToInt(x * scaleX), 0, source.width - 1);
                    int sourceY = Mathf.Clamp(Mathf.FloorToInt(y * scaleY), 0, source.height - 1);
                    pixels[y * textureSize + x] = source.GetPixel(sourceX, sourceY);
                }
            }

            radarTexture.SetPixels32(pixels);
            radarTexture.Apply();
            Destroy(source);
            NotifyDataUpdated();
        }

        private void GenerateProceduralFallback()
        {
            if (radarTexture == null) InitializeTexture();

            int centerX = textureSize / 2;
            int centerY = textureSize / 2;
            float radius = textureSize / 2f;
            float noiseOffset = Time.time * 0.1f;

            Color32[] pixels = new Color32[textureSize * textureSize];
            Color32 clear = new Color32(0, 0, 0, 0);

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    if (dx * dx + dy * dy > radius * radius)
                    {
                        pixels[y * textureSize + x] = clear;
                        continue;
                    }

                    float noise = Mathf.PerlinNoise((x + noiseOffset) * 0.015f, (y + noiseOffset) * 0.015f);
                    if (noise > 0.55f)
                    {
                        float intensity = (noise - 0.55f) * 2.2f;
                        pixels[y * textureSize + x] = GetRadarColor(intensity);
                    }
                    else
                    {
                        pixels[y * textureSize + x] = clear;
                    }
                }
            }

            radarTexture.SetPixels32(pixels);
            radarTexture.Apply();
            NotifyDataUpdated();
        }

        private Color GetRadarColor(float intensity)
        {
            if (intensity < 0.2f) return new Color(0.2f, 0.8f, 0.2f, 0.7f);
            if (intensity < 0.4f) return new Color(1f, 1f, 0f, 0.8f);
            if (intensity < 0.6f) return new Color(1f, 0.6f, 0f, 0.9f);
            if (intensity < 0.8f) return new Color(1f, 0f, 0f, 1f);
            return new Color(0.8f, 0f, 0.8f, 1f);
        }

        private (int x, int y) LatLonToTile(float lat, float lon, int zoom)
        {
            int n = 1 << zoom;
            int x = (int)((lon + 180.0f) / 360.0f * n);
            int y = (int)((1.0f - Mathf.Log(Mathf.Tan(lat * Mathf.Deg2Rad) + 
                1.0f / Mathf.Cos(lat * Mathf.Deg2Rad)) / Mathf.PI) / 2.0f * n);
            return (x, y);
        }

        public override void RefreshData()
        {
            if (isRequesting) return;

            bool serviceChanged = preferredService != lastService;
            if (serviceChanged)
            {
                ClearCache();
                lastService = preferredService;
                pendingNewData = false;
                pendingRadarPath = "";
            }

            bool usesTiles = UsesTiles(preferredService);
            bool zoomChanged = false;
            if (usesTiles)
            {
                int targetZoom = GetTargetZoom(preferredService);
                zoomChanged = targetZoom != zoomLevel;
                if (zoomChanged)
                {
                    zoomLevel = targetZoom;
                    Debug.Log($"[NOAAWeatherProvider] Range {rangeNM}nm -> Zoom {zoomLevel} ({preferredService})");
                }
            }

            bool needsFetch = !usesTiles ||
                              serviceChanged ||
                              zoomChanged ||
                              cachedComposite == null ||
                              (preferredService == RadarService.RainViewer && pendingNewData);

            if (needsFetch)
            {
                if (preferredService != RadarService.RainViewer)
                {
                    pendingNewData = false;
                    pendingRadarPath = "";
                }

                Debug.Log("[NOAAWeatherProvider] RefreshData - fetching new data");
                ClearCache();
                StartCoroutine(FetchRadarData());
            }
            else
            {
                Debug.Log("[NOAAWeatherProvider] RefreshData - using cached data");
                UpdateRadarFromComposite(true); // true = notify listeners
            }
        }

        /// <summary>
        /// Force update the radar display from cached data (for heading/position changes)
        /// </summary>
        public void UpdateDisplay()
        {
            if (cachedComposite != null)
            {
                UpdateRadarFromComposite(true);
                
            }
        }
    }
}
