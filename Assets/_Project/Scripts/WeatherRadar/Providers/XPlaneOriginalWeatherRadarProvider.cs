using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace WeatherRadar
{
    /// <summary>
    /// Publishes a compact dataref-driven weather instrument from the X-Plane
    /// 12 stream. A legacy HTTP raster path remains available only when an
    /// explicit caller opts into it.
    /// </summary>
    [AddComponentMenu("Weather Radar/Providers/X-Plane 12 Dataref Weather Radar Provider")]
    public class XPlaneOriginalWeatherRadarProvider : WeatherRadarProviderBase
    {
        // Kept as a compatibility endpoint only. The FAA scene publishes a
        // procedural texture from the live X-Plane datarefs instead of
        // downloading the native X-Plane raster.
        private const string LegacyRadarTextureUrl = "http://127.0.0.1:12678/v1/render/weather.png";
        private const string ProceduralTextureName = "FAAProceduralWeatherRadar";

        [Header("Dataref Weather Presentation")]
        [SerializeField] private string radarTextureUrl = string.Empty;
        [SerializeField] private bool preferNativePluginTexture = false;
        [SerializeField] private bool allowHttpTexturePolling = false;
        [SerializeField] private float requestTimeoutSeconds = 2f;
        [SerializeField] private bool cacheBustRequests = true;
        [SerializeField] private bool acceptAllCertificates = false;
        [SerializeField] private bool keepLastTextureOnError = true;

        [Header("Status")]
        [SerializeField] private string lastStatus = "Idle";
        [SerializeField] private int lastWidth;
        [SerializeField] private int lastHeight;
        [SerializeField] private float lastSuccessfulUpdateTime;

        public override string ProviderName => "X-Plane 12 Dataref Weather Radar";

        public string RadarTextureUrl
        {
            get => radarTextureUrl;
            set => radarTextureUrl = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public bool AllowHttpTexturePolling
        {
            get => allowHttpTexturePolling;
            set => allowHttpTexturePolling = value;
        }

        public string LastStatus => lastStatus;
        public int LastWidth => lastWidth;
        public int LastHeight => lastHeight;
        public float LastSuccessfulUpdateTime => lastSuccessfulUpdateTime;
        public bool PreferNativePluginTexture
        {
            get => preferNativePluginTexture;
            set => preferNativePluginTexture = value;
        }

        public bool UsesNativeTexture => preferNativePluginTexture && allowHttpTexturePolling;

        /// <summary>
        /// Forces the provider onto the dataref-backed procedural path. This
        /// is used by the FAA bridge at runtime as a guard against stale scene
        /// serialization selecting the legacy raster endpoint.
        /// </summary>
        public void UseProceduralDatarefTexture()
        {
            preferNativePluginTexture = false;
            allowHttpTexturePolling = false;
            radarTextureUrl = string.Empty;
        }

        protected override void InitializeTexture()
        {
            if (radarTexture != null)
            {
                return;
            }

            radarTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = ProceduralTextureName
            };
            radarTexture.SetPixels(new[] { Color.black, Color.black, Color.black, Color.black });
            radarTexture.Apply(false, true);
        }

        protected override void GenerateRadarData()
        {
            if (!allowHttpTexturePolling)
            {
                lastStatus = lastSuccessfulUpdateTime > 0f
                    ? lastStatus
                    : "Waiting for X-Plane datarefs";
                if (lastSuccessfulUpdateTime <= 0f)
                {
                    SetStatus(ProviderStatus.Connecting);
                }
                isGenerating = false;
                return;
            }

            if (!isActiveAndEnabled)
            {
                isGenerating = false;
                return;
            }

            StartCoroutine(DownloadLegacyNativeTexture());
        }

        public override void RefreshData()
        {
            if (isGenerating)
            {
                return;
            }

            if (status == ProviderStatus.Inactive)
            {
                Activate();
            }

            isGenerating = true;
            lastUpdateTime = Time.time;
            GenerateRadarData();
        }

        public void PublishTexture(Texture2D texture)
        {
            PublishTexture(texture, null);
        }

        public void PublishTexture(Texture2D texture, string statusOverride)
        {
            if (texture == null)
            {
                isGenerating = false;
                return;
            }

            ReplaceRadarTexture(CopyTexture(texture));
            lastStatus = string.IsNullOrWhiteSpace(statusOverride)
                ? $"Received {texture.width}x{texture.height}"
                : statusOverride.Trim();
            SetStatus(ProviderStatus.Active);
            NotifyDataUpdated();
        }

        private IEnumerator DownloadLegacyNativeTexture()
        {
            string url = BuildRequestUrl();
            lastStatus = $"Requesting {url}";
            SetStatus(ProviderStatus.Connecting);

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = Mathf.Max(1, Mathf.RoundToInt(requestTimeoutSeconds));
                if (acceptAllCertificates)
                {
                    request.certificateHandler = new AcceptAllCertificatesHandler();
                }

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    lastStatus = $"X-PLANE WX OFFLINE — {request.error}";
                    SetStatus(keepLastTextureOnError && radarTexture != null ? ProviderStatus.Active : ProviderStatus.Error);
                    isGenerating = false;
                    yield break;
                }

                Texture2D downloadedTexture = DownloadHandlerTexture.GetContent(request);
                if (downloadedTexture == null)
                {
                    lastStatus = "No texture returned";
                    SetStatus(ProviderStatus.NoData);
                    isGenerating = false;
                    yield break;
                }

                downloadedTexture.name = "LegacyNativeWeatherRadar";
                ReplaceRadarTexture(downloadedTexture);
                lastStatus = $"Updated {lastWidth}x{lastHeight}";
                SetStatus(ProviderStatus.Active);
                NotifyDataUpdated();
            }
        }

        private string BuildRequestUrl()
        {
            string url = string.IsNullOrWhiteSpace(radarTextureUrl) ? LegacyRadarTextureUrl : radarTextureUrl.Trim();
            // This endpoint is retained only as an opt-in compatibility
            // fallback. The FAA scene uses stream-derived weather metrics.
            if (!preferNativePluginTexture)
            {
                url = AppendQueryParameter(url, "range_nm", Mathf.Clamp(RangeNM, 5f, 320f).ToString("0", CultureInfo.InvariantCulture));
            }

            if (!cacheBustRequests)
            {
                return url;
            }

            string separator = url.Contains("?") ? "&" : "?";
            return $"{url}{separator}t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        }

        private static string AppendQueryParameter(string url, string key, string value)
        {
            if (url.IndexOf(key + "=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return url;
            }

            string separator = url.Contains("?") ? "&" : "?";
            return $"{url}{separator}{key}={value}";
        }

        private void ReplaceRadarTexture(Texture2D replacement)
        {
            if (replacement == null)
            {
                return;
            }

            replacement.filterMode = FilterMode.Bilinear;
            replacement.wrapMode = TextureWrapMode.Clamp;

            if (radarTexture != null && !ReferenceEquals(radarTexture, replacement))
            {
                Destroy(radarTexture);
            }

            radarTexture = replacement;
            lastWidth = radarTexture.width;
            lastHeight = radarTexture.height;
            lastSuccessfulUpdateTime = Time.realtimeSinceStartup;
        }

        private static Texture2D CopyTexture(Texture2D source)
        {
            Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = ProceduralTextureName
            };
            copy.SetPixels32(source.GetPixels32());
            copy.Apply(false);
            return copy;
        }

        private sealed class AcceptAllCertificatesHandler : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData)
            {
                return true;
            }
        }
    }
}
