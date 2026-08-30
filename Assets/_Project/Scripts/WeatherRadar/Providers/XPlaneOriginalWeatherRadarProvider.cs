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

        [Header("Unity XR-3 Simulator Fallback")]
        [Tooltip("When enabled by the FAA XR bridge, generate a local procedural weather texture until an X-Plane dataref feed is available.")]
        [SerializeField] private bool simulatorFallbackEnabled;

        private SimulatedWeatherProvider simulatorFallbackProvider;
        private bool ownsSimulatorFallbackProvider;

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
        public bool SimulatorFallbackEnabled => simulatorFallbackEnabled;
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

        /// <summary>
        /// Enables the editor/player XR-3 simulator weather source. The same
        /// X-Plane provider remains the public provider for existing panels and
        /// controls; only its data source changes while the simulator is active.
        /// </summary>
        public void SetSimulatorFallbackEnabled(bool enabled)
        {
            if (simulatorFallbackEnabled == enabled && (!enabled || simulatorFallbackProvider != null))
            {
                if (enabled)
                {
                    SyncSimulatorFallbackSettings();
                }

                return;
            }

            simulatorFallbackEnabled = enabled;
            if (enabled)
            {
                EnsureSimulatorFallbackProvider();
                SyncSimulatorFallbackSettings();
                simulatorFallbackProvider.Activate();
                simulatorFallbackProvider.RefreshData();
            }
            else
            {
                DestroySimulatorFallbackProvider();
                if (lastSuccessfulUpdateTime <= 0f)
                {
                    lastStatus = "Waiting for X-Plane datarefs";
                    SetStatus(ProviderStatus.Connecting);
                }
            }
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
            if (simulatorFallbackEnabled)
            {
                EnsureSimulatorFallbackProvider();
                SyncSimulatorFallbackSettings();
                if (!simulatorFallbackProvider.IsActive)
                {
                    simulatorFallbackProvider.Activate();
                }

                simulatorFallbackProvider.RefreshData();
                return;
            }

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

        private void EnsureSimulatorFallbackProvider()
        {
            if (simulatorFallbackProvider != null)
            {
                return;
            }

            simulatorFallbackProvider = GetComponent<SimulatedWeatherProvider>();
            if (simulatorFallbackProvider == null)
            {
                simulatorFallbackProvider = gameObject.AddComponent<SimulatedWeatherProvider>();
                ownsSimulatorFallbackProvider = true;
            }

            simulatorFallbackProvider.OnRadarDataUpdated -= OnSimulatorFallbackDataUpdated;
            simulatorFallbackProvider.OnRadarDataUpdated += OnSimulatorFallbackDataUpdated;
            simulatorFallbackProvider.SetAutoUpdate(false);
        }

        private void SyncSimulatorFallbackSettings()
        {
            if (simulatorFallbackProvider == null)
            {
                return;
            }

            simulatorFallbackProvider.SetAircraftPosition(Altitude, Latitude, Longitude, Heading);
            simulatorFallbackProvider.RangeNM = RangeNM;
            simulatorFallbackProvider.TiltDegrees = TiltDegrees;
            simulatorFallbackProvider.GainDB = GainDB;
        }

        private void OnSimulatorFallbackDataUpdated(Texture2D texture)
        {
            if (simulatorFallbackEnabled && texture != null)
            {
                PublishTexture(texture, "XR-3 SIMULATED WEATHER");
            }
        }

        private void DestroySimulatorFallbackProvider()
        {
            if (simulatorFallbackProvider == null)
            {
                return;
            }

            simulatorFallbackProvider.OnRadarDataUpdated -= OnSimulatorFallbackDataUpdated;
            if (ownsSimulatorFallbackProvider)
            {
                if (Application.isPlaying)
                {
                    Destroy(simulatorFallbackProvider);
                }
                else
                {
                    DestroyImmediate(simulatorFallbackProvider);
                }
            }

            simulatorFallbackProvider = null;
            ownsSimulatorFallbackProvider = false;
        }

        protected override void OnDestroy()
        {
            // The child component is destroyed with this GameObject. Unhook
            // the delegate first so no stale provider callback survives a
            // scene transition or domain reload.
            if (simulatorFallbackProvider != null)
            {
                simulatorFallbackProvider.OnRadarDataUpdated -= OnSimulatorFallbackDataUpdated;
            }

            simulatorFallbackProvider = null;
            ownsSimulatorFallbackProvider = false;
            base.OnDestroy();
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
