using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;

namespace WeatherRadar
{
    /// <summary>
    /// High-performance weather data provider using Iowa Environmental Mesonet (IEM) NEXRAD tiles.
    /// 
    /// Data Source: Iowa State University Mesonet
    /// URL: https://mesonet.agron.iastate.edu/cache/tile.py/1.0.0/nexrad-n0q-900913/{z}/{x}/{y}.png
    /// 
    /// This is the BEST FREE option for real-time NEXRAD radar data:
    /// - Pre-rendered tiles (no processing needed)
    /// - Updates every ~5 minutes
    /// - Standard XYZ tile format
    /// - Covers Continental US
    /// 
    /// Position is set via base class properties (Latitude, Longitude, Altitude).
    /// 
    /// NOTE: Not for high-traffic commercial use per IEM terms.
    /// </summary>
    public class IEMWeatherProvider : WeatherRadarProviderBase
    {
        [Header("IEM Service Settings")]
        [Tooltip("Base URL for IEM NEXRAD tiles")]
        [SerializeField] private string tileUrl = "https://mesonet.agron.iastate.edu/cache/tile.py/1.0.0/nexrad-n0q-900913";

        [Header("Tile Settings")]
        [Tooltip("Zoom level (4-12 recommended, higher = more detail)")]
        [SerializeField] [Range(4, 12)] private int zoomLevel = 6;

        [Tooltip("Number of tiles to fetch in each direction from center")]
        [SerializeField] [Range(1, 3)] private int tileRadius = 1;

        [Header("Auto Zoom")]
        [Tooltip("Automatically adjust zoom based on range setting")]
        [SerializeField] private bool autoZoomFromRange = true;

        private const float NM_TO_METERS = 1852f;
        private const float EARTH_RADIUS_METERS = 6378137f;

        [Header("Performance")]
        [Tooltip("Use cached tiles when available")]
        [SerializeField] private bool useCache = true;

        [Tooltip("Cache duration in seconds")]
        [SerializeField] private float cacheDuration = 120f;

        [Tooltip("Max concurrent requests")]
        [SerializeField] private int maxConcurrentRequests = 4;

        [Header("Request Settings")]
        [SerializeField] private float requestTimeout = 15f;

        public override string ProviderName => "IEM NEXRAD (Real-time)";

        // Tile cache
        private Dictionary<string, CachedTile> tileCache = new Dictionary<string, CachedTile>();
        private int activeRequests = 0;
        private Queue<TileRequest> requestQueue = new Queue<TileRequest>();
        private bool isProcessingQueue = false;
        
        // Position tracking
        private float lastFetchLat;
        private float lastFetchLon;
        private const float MIN_POSITION_CHANGE = 0.3f; // degrees

        private class CachedTile
        {
            public Texture2D texture;
            public float timestamp;
        }

        private class TileRequest
        {
            public int x;
            public int y;
            public int z;
            public Action<Texture2D> callback;
        }

        protected override void Start()
        {
            base.Start();
            lastFetchLat = latitude;
            lastFetchLon = longitude;
        }
        
        protected override void Update()
        {
            base.Update();
            
            // Check if position has changed significantly
            float latDiff = Mathf.Abs(latitude - lastFetchLat);
            float lonDiff = Mathf.Abs(longitude - lastFetchLon);
            
            if (latDiff > MIN_POSITION_CHANGE || lonDiff > MIN_POSITION_CHANGE)
            {
                Debug.Log($"[IEM] Position changed significantly: ({lastFetchLat:F2},{lastFetchLon:F2}) -> ({latitude:F2},{longitude:F2}) - clearing cache & fetching");
                
                // Update position tracking
                lastFetchLat = latitude;
                lastFetchLon = longitude;
                
                // Clear cache for new location
                ClearCache();
                
                // Trigger new data fetch immediately
                GenerateRadarData();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ClearCache();
        }

        protected override void GenerateRadarData()
        {
            if (autoZoomFromRange)
            {
                int targetZoom = CalculateZoomForRange(rangeNM, latitude);
                if (targetZoom != zoomLevel)
                {
                    zoomLevel = targetZoom;
                    ClearCache();
                }
            }

            // Check if position changed enough to warrant cache clear
            float latDiff = Mathf.Abs(latitude - lastFetchLat);
            float lonDiff = Mathf.Abs(longitude - lastFetchLon);
            
            if (latDiff > MIN_POSITION_CHANGE || lonDiff > MIN_POSITION_CHANGE)
            {
                Debug.Log($"[IEM] Position changed significantly, clearing cache");
                ClearCache();
            }
            
            lastFetchLat = latitude;
            lastFetchLon = longitude;
            
            StartCoroutine(FetchRadarTiles());
        }

        private IEnumerator FetchRadarTiles()
        {
            SetStatus(ProviderStatus.Connecting);

            // Use base class latitude/longitude (set via SetAircraftPosition or inspector)
            Debug.Log($"[IEM] Fetching tiles for location: {latitude}, {longitude} at zoom {zoomLevel}");

            // Convert lat/lon to tile coordinates
            var centerTile = LatLonToTile(latitude, longitude, zoomLevel);
            Debug.Log($"[IEM] Center tile: {centerTile.x}, {centerTile.y}");

            // Collect all tiles we need
            List<TileRequest> tilesToFetch = new List<TileRequest>();
            Texture2D[,] fetchedTiles = new Texture2D[tileRadius * 2 + 1, tileRadius * 2 + 1];
            int tilesNeeded = 0;
            int tilesReceived = 0;

            for (int dx = -tileRadius; dx <= tileRadius; dx++)
            {
                for (int dy = -tileRadius; dy <= tileRadius; dy++)
                {
                    int tx = centerTile.x + dx;
                    int ty = centerTile.y + dy;
                    
                    string cacheKey = $"{zoomLevel}/{tx}/{ty}";
                    
                    // Check cache first
                    if (useCache && tileCache.TryGetValue(cacheKey, out CachedTile cached))
                    {
                        if (Time.time - cached.timestamp < cacheDuration && cached.texture != null)
                        {
                            fetchedTiles[dx + tileRadius, dy + tileRadius] = cached.texture;
                            tilesReceived++;
                            continue;
                        }
                    }

                    tilesNeeded++;
                    int localDx = dx;
                    int localDy = dy;
                    
                    var request = new TileRequest
                    {
                        x = tx,
                        y = ty,
                        z = zoomLevel,
                        callback = (tex) =>
                        {
                            fetchedTiles[localDx + tileRadius, localDy + tileRadius] = tex;
                            tilesReceived++;
                            
                            if (tex != null && useCache)
                            {
                                string key = $"{zoomLevel}/{tx}/{ty}";
                                tileCache[key] = new CachedTile { texture = tex, timestamp = Time.time };
                            }
                        }
                    };
                    
                    tilesToFetch.Add(request);
                }
            }

            // Queue all requests
            foreach (var request in tilesToFetch)
            {
                requestQueue.Enqueue(request);
            }

            // Start processing queue
            if (!isProcessingQueue)
            {
                StartCoroutine(ProcessRequestQueue());
            }

            // Wait for all tiles
            int totalTiles = (tileRadius * 2 + 1) * (tileRadius * 2 + 1);
            float timeout = Time.time + requestTimeout * 2;
            
            while (tilesReceived < totalTiles && Time.time < timeout)
            {
                yield return null;
            }

            // Composite tiles into radar texture
            if (tilesReceived > 0)
            {
                CompositeTiles(fetchedTiles);
                SetStatus(ProviderStatus.Active);
            }
            else
            {
                Debug.LogWarning("[IEMWeatherProvider] No tiles received, using fallback");
                SetStatus(ProviderStatus.NoData);
                GenerateProceduralFallback();
            }
        }

        private IEnumerator ProcessRequestQueue()
        {
            isProcessingQueue = true;

            while (requestQueue.Count > 0)
            {
                // Wait if we have too many active requests
                while (activeRequests >= maxConcurrentRequests)
                {
                    yield return null;
                }

                if (requestQueue.Count > 0)
                {
                    var request = requestQueue.Dequeue();
                    StartCoroutine(FetchSingleTile(request));
                }
            }

            isProcessingQueue = false;
        }

        private IEnumerator FetchSingleTile(TileRequest request)
        {
            activeRequests++;

            string url = $"{tileUrl}/{request.z}/{request.x}/{request.y}.png";
            
            using (var webRequest = UnityWebRequestTexture.GetTexture(url))
            {
                webRequest.timeout = Mathf.RoundToInt(requestTimeout);
                
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(webRequest);
                    request.callback?.Invoke(tex);
                }
                else
                {
                    Debug.LogWarning($"[IEM] Tile fetch failed: {request.z}/{request.x}/{request.y} - {webRequest.error}");
                    request.callback?.Invoke(null);
                }
            }

            activeRequests--;
        }

        private void CompositeTiles(Texture2D[,] tiles)
        {
            if (radarTexture == null) InitializeTexture();

            int tileCount = tileRadius * 2 + 1;
            int tilePixelSize = 256;
            for (int tx = 0; tx < tileCount; tx++)
            {
                for (int ty = 0; ty < tileCount; ty++)
                {
                    if (tiles[tx, ty] != null)
                    {
                        tilePixelSize = tiles[tx, ty].width;
                        break;
                    }
                }
            }

            int compositeSize = tilePixelSize * tileCount;
            Texture2D composite = new Texture2D(compositeSize, compositeSize, TextureFormat.RGBA32, false);
            composite.filterMode = FilterMode.Bilinear;

            for (int ty = 0; ty < tileCount; ty++)
            {
                for (int tx = 0; tx < tileCount; tx++)
                {
                    Texture2D tile = tiles[tx, ty];
                    if (tile == null) continue;

                    int destY = (tileCount - 1 - ty) * tilePixelSize; // Flip Y for Unity
                    composite.SetPixels(tx * tilePixelSize, destY, tilePixelSize, tilePixelSize, tile.GetPixels());
                }
            }

            composite.Apply();
            UpdateRadarFromComposite(composite);
            Destroy(composite);
        }

        private void UpdateRadarFromComposite(Texture2D composite)
        {
            int centerX = textureSize / 2;
            int centerY = textureSize / 2;
            float radius = textureSize / 2f;

            float rangeMeters = Mathf.Max(rangeNM, 1f) * NM_TO_METERS;
            float coverageMeters = CalculateCoverageMeters(zoomLevel, tileRadius, latitude);
            float rangeScale = coverageMeters > 0f ? rangeMeters / coverageMeters : 1f;
            if (rangeScale > 1f)
            {
                Debug.LogWarning("[IEM] Range exceeds tile coverage; clamping to available data");
                rangeScale = 1f;
            }

            Color32[] pixels = new Color32[textureSize * textureSize];
            Color32 clear = new Color32(0, 0, 0, 0);

            float gainMultiplier = 1f + (gainDB / 8f);
            float compositeHalfW = composite.width / 2f;
            float compositeHalfH = composite.height / 2f;

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

                    float normX = dx / radius;
                    float normY = dy / radius;

                    float compX = compositeHalfW + (normX * rangeScale) * compositeHalfW;
                    float compY = compositeHalfH + (normY * rangeScale) * compositeHalfH;

                    int sourceX = Mathf.Clamp(Mathf.RoundToInt(compX), 0, composite.width - 1);
                    int sourceY = Mathf.Clamp(Mathf.RoundToInt(compY), 0, composite.height - 1);

                    Color sourceColor = composite.GetPixel(sourceX, sourceY);
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
            NotifyDataUpdated();
        }

        private float CalculateCoverageMeters(int zoom, int radius, float latDeg)
        {
            int gridSize = radius * 2 + 1;
            float latRad = Mathf.Clamp(latDeg, -85f, 85f) * Mathf.Deg2Rad;
            float metersPerTile = (2f * Mathf.PI * EARTH_RADIUS_METERS / (1 << zoom)) * Mathf.Cos(latRad);
            return metersPerTile * (gridSize / 2f);
        }

        private (int x, int y) LatLonToTile(float lat, float lon, int zoom)
        {
            // Convert lat/lon to Web Mercator tile coordinates
            int n = 1 << zoom;
            int x = (int)((lon + 180.0f) / 360.0f * n);
            int y = (int)((1.0f - Mathf.Log(Mathf.Tan(lat * Mathf.Deg2Rad) + 
                1.0f / Mathf.Cos(lat * Mathf.Deg2Rad)) / Mathf.PI) / 2.0f * n);
            
            return (x, y);
        }

        private void GenerateProceduralFallback()
        {
            // Fallback noise when network fails
            int centerX = textureSize / 2;
            int centerY = textureSize / 2;
            float radius = textureSize / 2f;
            float noiseOffset = Time.time * 0.1f;

            for (int x = 0; x < textureSize; x++)
            {
                for (int y = 0; y < textureSize; y++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > radius)
                    {
                        radarTexture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    float noise = Mathf.PerlinNoise((x + noiseOffset) * 0.015f, (y + noiseOffset) * 0.015f);
                    
                    if (noise > 0.55f)
                    {
                        float intensity = (noise - 0.55f) * 2.2f;
                        Color color = GetRadarColor(intensity);
                        radarTexture.SetPixel(x, y, color);
                    }
                    else
                    {
                        radarTexture.SetPixel(x, y, Color.clear);
                    }
                }
            }

            radarTexture.Apply();
            NotifyDataUpdated();
        }

        private Color GetRadarColor(float intensity)
        {
            // Standard NEXRAD color scale
            if (intensity < 0.2f) return new Color(0.2f, 0.8f, 0.2f, 0.7f);  // Green - Light
            if (intensity < 0.4f) return new Color(1f, 1f, 0f, 0.8f);         // Yellow - Moderate
            if (intensity < 0.6f) return new Color(1f, 0.6f, 0f, 0.9f);       // Orange - Heavy
            if (intensity < 0.8f) return new Color(1f, 0f, 0f, 1f);           // Red - Very Heavy
            return new Color(0.8f, 0f, 0.8f, 1f);                              // Magenta - Extreme
        }

        public override void RefreshData()
        {
            if (autoZoomFromRange)
            {
                int targetZoom = CalculateZoomForRange(rangeNM, latitude);
                if (targetZoom != zoomLevel)
                {
                    zoomLevel = targetZoom;
                    ClearCache();
                }
            }

            base.RefreshData();
        }

        private int CalculateZoomForRange(float rangeNm, float latDeg)
        {
            float rangeMeters = Mathf.Max(rangeNm, 1f) * NM_TO_METERS;
            int gridSize = tileRadius * 2 + 1;
            float latRad = Mathf.Clamp(latDeg, -85f, 85f) * Mathf.Deg2Rad;
            float numerator = Mathf.Cos(latRad) * 2f * Mathf.PI * EARTH_RADIUS_METERS * (gridSize / 2f);
            if (numerator <= 0f)
            {
                return 4;
            }

            float zFloat = Mathf.Log(numerator / rangeMeters, 2f);
            int zoom = Mathf.FloorToInt(zFloat);
            return Mathf.Clamp(zoom, 4, 12);
        }

        /// <summary>
        /// Clear the tile cache
        /// </summary>
        public void ClearCache()
        {
            foreach (var cached in tileCache.Values)
            {
                if (cached.texture != null)
                {
                    Destroy(cached.texture);
                }
            }
            tileCache.Clear();
        }

        /// <summary>
        /// Set zoom level (affects detail)
        /// </summary>
        public void SetZoom(int zoom)
        {
            zoomLevel = Mathf.Clamp(zoom, 4, 12);
        }
    }
}
