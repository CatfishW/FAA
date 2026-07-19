using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace WeatherRadar
{
    /// <summary>
    /// Publishes X-Plane 12 weather radar imagery when the bridge receives an
    /// original render texture, or a live dataref-driven weather instrument when
    /// only the NDJSON stream is available.
    /// </summary>
    [AddComponentMenu("Weather Radar/Providers/X-Plane 12 Original Weather Radar Provider")]
    public class XPlaneOriginalWeatherRadarProvider : WeatherRadarProviderBase
    {
        private const string DefaultRadarTextureUrl = "http://127.0.0.1:12678/v1/render/weather.png";

        [Header("X-Plane Original Texture")]
        [SerializeField] private string radarTextureUrl = DefaultRadarTextureUrl;
        [SerializeField] private bool preferNativePluginTexture = true;
        [SerializeField] private bool allowHttpTexturePolling = true;
        [SerializeField] private float requestTimeoutSeconds = 2f;
        [SerializeField] private bool cacheBustRequests = true;
        [SerializeField] private bool acceptAllCertificates = false;
        [SerializeField] private bool keepLastTextureOnError = true;

        [Header("Status")]
        [SerializeField] private string lastStatus = "Idle";
        [SerializeField] private int lastWidth;
        [SerializeField] private int lastHeight;
        [SerializeField] private float lastSuccessfulUpdateTime;

        public override string ProviderName => "X-Plane 12 Original Weather Radar";

        public string RadarTextureUrl
        {
            get => radarTextureUrl;
            set => radarTextureUrl = string.IsNullOrWhiteSpace(value) ? DefaultRadarTextureUrl : value.Trim();
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
                name = "XPlaneOriginalWeatherRadar"
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
                    : "Waiting for X-Plane stream data";
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

            StartCoroutine(DownloadOriginalTexture());
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

        private IEnumerator DownloadOriginalTexture()
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

                downloadedTexture.name = "XPlaneOriginalWeatherRadar";
                ReplaceRadarTexture(downloadedTexture);
                lastStatus = $"Updated {lastWidth}x{lastHeight}";
                SetStatus(ProviderStatus.Active);
                NotifyDataUpdated();
            }
        }

        private string BuildRequestUrl()
        {
            string url = string.IsNullOrWhiteSpace(radarTextureUrl) ? DefaultRadarTextureUrl : radarTextureUrl.Trim();
            // The no-query endpoint serves X-Plane's live xplm_Tex_Radar_Pilot
            // artifact. Adding range_nm selects the older UDP point diagnostic,
            // whose individual sample bubbles are not the aircraft radar image.
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
                name = "XPlaneOriginalWeatherRadar"
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
