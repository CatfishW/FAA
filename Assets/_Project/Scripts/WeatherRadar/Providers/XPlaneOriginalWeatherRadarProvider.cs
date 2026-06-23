using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace WeatherRadar
{
    /// <summary>
    /// Downloads and publishes X-Plane 12's original weather radar render texture.
    /// This provider intentionally preserves the source image instead of remapping it
    /// into a synthetic NEXRAD-style return texture.
    /// </summary>
    [AddComponentMenu("Weather Radar/Providers/X-Plane 12 Original Weather Radar Provider")]
    public class XPlaneOriginalWeatherRadarProvider : WeatherRadarProviderBase
    {
        private const string DefaultRadarTextureUrl = "https://faa.agaii.org/xplane12/v1/render/weather.png";

        [Header("X-Plane Original Texture")]
        [SerializeField] private string radarTextureUrl = DefaultRadarTextureUrl;
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
        }

        protected override void GenerateRadarData()
        {
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
            if (texture == null)
            {
                isGenerating = false;
                return;
            }

            ReplaceRadarTexture(CopyTexture(texture));
            lastStatus = $"Received {texture.width}x{texture.height}";
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
                    lastStatus = request.error;
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
            if (!cacheBustRequests)
            {
                return url;
            }

            string separator = url.Contains("?") ? "&" : "?";
            return $"{url}{separator}t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
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
