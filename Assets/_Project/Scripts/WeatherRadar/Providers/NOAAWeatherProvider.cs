using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

namespace WeatherRadar
{
    /// <summary>
    /// Weather data provider that fetches real-time radar imagery from RainViewer API.
    /// Downloads a 3x3 grid of tiles for larger coverage area.
    /// Refreshes when new radar data is available (every ~5 minutes).
    /// Supports heading rotation for aircraft-relative display.
    /// </summary>
    public class NOAAWeatherProvider : WeatherRadarProviderBase
    {
        [Header("Service Selection")]
        [Tooltip("Which service to use for radar data")]
        [SerializeField] private RadarService preferredService = RadarService.RainViewer;

        [Header("Request Settings")]
        [Tooltip("Tile size (256 or 512)")]
        [SerializeField] private int tileSize = 256;
        
        [Tooltip("Request timeout in seconds")]
        [SerializeField] private float requestTimeout = 15f;

        [Header("Coverage")]
        [Tooltip("Zoom level for tile services (lower = larger area)")]
        [SerializeField] [Range(3, 7)] private int zoomLevel = 5;
        
        [Tooltip("Tile grid size (1=1x1, 2=3x3, 3=5x5, 4=7x7)")]
        [SerializeField] [Range(1, 4)] private int tileRadius = 2;

        [Header("Refresh Settings")]
        [Tooltip("How often to check for new radar data (seconds)")]
        [SerializeField] private float dataCheckInterval = 60f;

        public override string ProviderName => "Online Weather Radar";

        private bool isRequesting;
        private string lastRadarPath = "";
        private long lastRadarTimestamp = 0;
        private float lastDataCheckTime = 0;
        
        // Cached composite texture from tile grid
        private Texture2D cachedComposite;
        private float cachedCenterLat;
        private float cachedCenterLon;
        private float cachedCoverageRadius; // in degrees
        
        // Tile cache
        private Dictionary<string, Texture2D> tileCache = new Dictionary<string, Texture2D>();

        public enum RadarService
        {
            RainViewer,      // Global, reliable, free tier
            NOAA_RIDGE,      // US only, WMS
            Fallback         // Procedural
        }

        protected override void Start()
        {
            base.Start();
            cachedCenterLat = latitude;
            cachedCenterLon = longitude;
            lastDisplayHeading = heading;
            lastDisplayLat = latitude;
            lastDisplayLon = longitude;
        }
        
        // Tracking for display updates (not refetch triggers)
        private float lastDisplayHeading;
        private float lastDisplayLat;
        private float lastDisplayLon;
        private const float DISPLAY_UPDATE_THRESHOLD = 0.01f; // Very small threshold for smooth updates

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
                if (cachedComposite != null && cachedCoverageRadius > 0)
                {
                    float distFromCenter = Mathf.Max(
                        Mathf.Abs(latitude - cachedCenterLat),
                        Mathf.Abs(longitude - cachedCenterLon)
                    );
                    
                    if (distFromCenter > cachedCoverageRadius * 0.7f) // 70% threshold
                    {
                        Debug.Log($"[NOAAWeatherProvider] Moved outside 70% coverage area - fetching new tiles");
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
            
            // Periodically check for new radar data
            if (Time.time - lastDataCheckTime > dataCheckInterval)
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

            if (preferredService == RadarService.RainViewer)
            {
                yield return FetchRainViewerGrid();
                success = status == ProviderStatus.Active;
            }
            else if (preferredService == RadarService.NOAA_RIDGE)
            {
                yield return TryNOAARidge();
                success = status == ProviderStatus.Active;
            }

            if (!success)
            {
                Debug.Log("[NOAAWeatherProvider] Trying RainViewer fallback...");
                yield return FetchRainViewerGrid();
                success = status == ProviderStatus.Active;
            }

            if (!success)
            {
                Debug.LogWarning("[NOAAWeatherProvider] All services failed, using procedural fallback");
                GenerateProceduralFallback();
            }

            isRequesting = false;
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

                // Calculate center tile
                var centerTile = LatLonToTile(latitude, longitude, zoomLevel);
                
                // Fetch grid of tiles
                int gridSize = tileRadius * 2 + 1;
                Texture2D[,] tiles = new Texture2D[gridSize, gridSize];
                int tilesLoaded = 0;
                int totalTiles = gridSize * gridSize;

                Debug.Log($"[RainViewer] Fetching {gridSize}x{gridSize} tile grid centered at ({latitude:F2}, {longitude:F2})");

                for (int dy = -tileRadius; dy <= tileRadius; dy++)
                {
                    for (int dx = -tileRadius; dx <= tileRadius; dx++)
                    {
                        int tileX = centerTile.x + dx;
                        int tileY = centerTile.y + dy;
                        
                        string tileUrl = $"https://tilecache.rainviewer.com{radarPath}/{tileSize}/{zoomLevel}/{tileX}/{tileY}/2/1_1.png";
                        string cacheKey = $"{zoomLevel}_{tileX}_{tileY}";

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
                    CompositeGridToRadar(tiles, gridSize);
                    
                    // Update cached coverage info
                    cachedCenterLat = latitude;
                    cachedCenterLon = longitude;
                    cachedCoverageRadius = CalculateCoverageRadius(zoomLevel, tileRadius);
                    
                    SetStatus(ProviderStatus.Active);
                    Debug.Log($"[RainViewer] Radar composite created, coverage radius: {cachedCoverageRadius:F2}°");
                }

                // Clean up individual tiles
                foreach (var tile in tiles)
                {
                    if (tile != null) Destroy(tile);
                }
            }
        }

        private void CompositeGridToRadar(Texture2D[,] tiles, int gridSize)
        {
            int compositeSize = tileSize * gridSize;
            
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
                        int destY = (gridSize - 1 - gy) * tileSize;
                        cachedComposite.SetPixels(gx * tileSize, destY, tileSize, tileSize, pixels);
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
            if (cachedCoverageRadius > 0)
            {
                offsetX = (longitude - cachedCenterLon) / (cachedCoverageRadius * 2);
                offsetY = (latitude - cachedCenterLat) / (cachedCoverageRadius * 2);
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
                    float rotX = normX * cosH - normY * sinH;
                    float rotY = normX * sinH + normY * cosH;

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

        private float CalculateCoverageRadius(int zoom, int radius)
        {
            // At zoom level Z, each tile covers 360/2^Z degrees longitude
            float degreesPerTile = 360f / (1 << zoom);
            return degreesPerTile * (radius + 0.5f);
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
            float nmToDegrees = rangeNM / 60f;
            float minLon = longitude - nmToDegrees;
            float maxLon = longitude + nmToDegrees;
            float minLat = latitude - nmToDegrees * 0.75f;
            float maxLat = latitude + nmToDegrees * 0.75f;
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

            // Update zoom based on range
            int targetZoom = CalculateZoomLevel(rangeNM);
            bool zoomChanged = targetZoom != zoomLevel;
            if (zoomChanged)
            {
                zoomLevel = targetZoom;
                Debug.Log($"[NOAAWeatherProvider] Range {rangeNM}nm -> Zoom {zoomLevel}");
            }
            
            // If new data is pending or no cache or zoom changed, fetch new tiles
            if (pendingNewData || cachedComposite == null || zoomChanged)
            {
                Debug.Log("[NOAAWeatherProvider] RefreshData - fetching new tiles");
                pendingNewData = false;
                ClearCache();
                StartCoroutine(FetchRadarData());
            }
            else
            {
                // Just re-render from cache with notify (updates display at sweep completion)
                Debug.Log("[NOAAWeatherProvider] RefreshData - using cached data");
                UpdateRadarFromComposite(true); // true = notify listeners
            }
        }

        private int CalculateZoomLevel(float range)
        {
            if (range <= 20) return 10;
            if (range <= 40) return 9;
            if (range <= 80) return 8;
            if (range <= 160) return 7;
            if (range <= 320) return 6;
            return 5;
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
