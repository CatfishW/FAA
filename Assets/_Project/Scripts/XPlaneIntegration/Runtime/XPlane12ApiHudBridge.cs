using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AircraftControl.Core;
using AviationUI;
using FAA.Geo;
using FAA.XPlaneIntegration.Core;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using Newtonsoft.Json.Linq;
using TrafficRadar;
using TrafficRadar.Core;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using UnityEngine.UI;
using WeatherRadar;
using HUDControl.Elements;
using AircraftRuntimeState = AircraftControl.Core.AircraftState;

namespace FAA.XPlaneIntegration.Runtime
{
    [AddComponentMenu("X-Plane Integration/Runtime/X-Plane 12 API HUD Bridge")]
    public class XPlane12ApiHudBridge : MonoBehaviour
    {
        private const float MetersToFeet = 3.28084f;
        private const float MetersPerSecondToKnots = 1.94384f;
        private const float MetersPerSecondToFeetPerMinute = 196.8504f;
        private const float KnotsToFeetPerSecond = 1.68781f;
        private const string TangTunnelLiveApiBaseUrl = "http://127.0.0.1:12678";

        public enum TransportMode
        {
            HttpApi = 0,
            MqttSnapshot = 1,
            WebSocketStream = 2,
            TcpNdjsonStream = 3
        }

        public enum DataSmoothingStrategy
        {
            None = 0,
            LowLatencyAdaptive = 1
        }

        [Header("X-Plane 12 API")]
        [Tooltip("Live X-Plane 12 API reached through the local SSH forward to tang-server.")]
        [SerializeField] private string baseUrl = "http://127.0.0.1:12678";
        [SerializeField] private bool autoStartOnPlay = true;
        [SerializeField] private float pollIntervalSeconds = 0.1f;
        [SerializeField] private float requestTimeoutSeconds = 2f;
        [SerializeField] private float staleAfterSeconds = 5f;

        [Header("Transport")]
        [SerializeField] private TransportMode transportMode = TransportMode.HttpApi;
        [SerializeField] private string tcpStreamHost = "127.0.0.1";
        [SerializeField] private int tcpStreamPort = 37212;
        [SerializeField] private string webSocketUrl = "ws://127.0.0.1:37212/v1/stream/ws";
        [SerializeField] private float webSocketReconnectDelaySeconds = 0.5f;
        [SerializeField] private int webSocketReceiveBufferBytes = 262144;
        [SerializeField] private bool webSocketUseMqttFallback = false;
        [SerializeField] private bool webSocketUseHttpFallback = false;
        [SerializeField] private float webSocketFallbackAfterSeconds = 1.25f;
        [SerializeField] private string mqttBrokerHost = "127.0.0.1";
        [SerializeField] private int mqttBrokerPort = 18883;
        [SerializeField] private string mqttSnapshotTopic = "xplane12/snapshot";
        [SerializeField] private string mqttClientId = "FAA-XPlane12-Unity";
        [SerializeField] private string mqttUsername = string.Empty;
        [SerializeField] private string mqttPassword = string.Empty;
        [SerializeField] private bool mqttAutoReconnect = true;
        [SerializeField] private float mqttReconnectDelaySeconds = 2f;

        [Header("Latency")]
        [SerializeField] private DataSmoothingStrategy smoothingStrategy = DataSmoothingStrategy.LowLatencyAdaptive;
        [SerializeField] private bool compensatePacketAge = true;
        [SerializeField] private bool interpolateDisplayBetweenPackets = true;
        [SerializeField] private float maxPredictionSeconds = 0.2f;
        [SerializeField] private float smoothingResponseRate = 90f;
        [SerializeField] private float aggressiveSmoothingResponseRate = 180f;
        [SerializeField] private float attitudeSnapDegrees = 35f;
        [SerializeField] private float headingSnapDegrees = 60f;
        [SerializeField] private float airspeedSnapKnots = 50f;
        [SerializeField] private float altitudeSnapFeet = 1000f;
        [SerializeField] private float verticalSpeedSnapFpm = 2500f;
        [SerializeField] private float staleHoldSeconds = 0.75f;

        [Header("Categories")]
        [SerializeField] private bool pollAircraft = true;
        [SerializeField] private bool pollWeather = true;
        [SerializeField] private bool pollSystems = true;
        [SerializeField] private bool pollTraffic = true;
        [SerializeField] private bool pollRenderAssets = true;
        [SerializeField] private float renderAssetPollIntervalSeconds = 2f;

        [Header("Weather Radar X-Plane Datarefs")]
        [FormerlySerializedAs("synthesizeWeatherRadarTextureFromStream")]
        [SerializeField] private bool publishWeatherDatarefTextureFromStream = true;
        [SerializeField] private float streamWeatherTextureIntervalSeconds = 1f;
        [SerializeField] private int streamWeatherTextureSize = 512;

        [Header("Targets")]
        [SerializeField] private AviationUIManager uiManager;
        [SerializeField] private AviationFlightDataProvider flightDataProvider;
        [SerializeField] private AircraftController aircraftController;
        [SerializeField] private HUDControl.Core.HUDController hudController;
        [SerializeField] private TrafficRadarDataManager trafficRadarDataManager;
        [SerializeField] private TrafficRadarController trafficRadarController;
        [SerializeField] private WeatherRadarDataProvider weatherRadarDataProvider;
        [SerializeField] private WeatherRadarProviderBase weatherRadarProvider;
        [SerializeField] private XPlaneOriginalWeatherRadarDisplay xPlaneWeatherRadarDisplay;
        [SerializeField] private TrafficRadarDisplay xPlaneTrafficRadarDisplay;
        [SerializeField] private RawImage weatherImageTarget;
        [SerializeField] private RawImage trafficImageTarget;

        [Header("Apply Data")]
        [SerializeField] private bool applyToAviationHud = true;
        [SerializeField] private bool applyToLegacyHud = true;
        [SerializeField] private bool applyToAircraftController = true;
        [SerializeField] private bool applyToTrafficRadar = true;
        [SerializeField] private bool applyToWeatherRadar = true;
        [SerializeField] private bool disableUserControlWhenReceiving = true;
        [SerializeField] private bool disableTrafficApiWhenReceiving = true;
        [Tooltip("When disabled, traffic is exclusively sourced from X-Plane multiplayer datarefs.")]
        [SerializeField] private bool allowExternalTrafficFallback = false;
        [SerializeField] private bool refreshWeatherRadarTexture = false;
        [SerializeField] private bool treatFreshWeatherTextureAsRadarOn = true;
        [SerializeField] private float minimumUnityTerrainClearanceMeters = 120f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        private readonly XPlane12ApiSnapshot _snapshot = new XPlane12ApiSnapshot();
        private Coroutine _pollRoutine;
        private Coroutine _renderAssetRoutine;
        private AviationFlightData _rawFlightData;
        private AviationFlightData _targetFlightData;
        private AviationFlightData _latestFlightData;
        private AviationFlightData _smoothedFlightData;
        private bool _lastHealthyState;
        private bool _hasLoggedConnection;
        private bool _hasSmoothedFlightData;
        private bool _hasTargetFlightData;
        private bool _suppressedUserControl;
        private bool _userControlWasEnabledBeforeApi;
        private bool _suppressedAircraftControl;
        private bool _aircraftControlWasEnabledBeforeApi;
        private bool _suppressedTrafficFetching;
        private bool _trafficWasFetchingBeforeApi;
        private float _lastWeatherRefreshTime;
        private float _lastDisplayApplyRealtime;
        private float _lastFlightSnapshotRealtime = -1f;
        private bool _enginePointersClearedForStaleFeed;
        private bool _hasWeatherRadarPowerState;
        private bool _isWeatherRadarPowered;
        private int _weatherRadarMode = -1;
        private readonly object _mqttMessageLock = new object();
        private MqttFactory _mqttFactory;
        private IMqttClient _mqttClient;
        private IMqttClientOptions _mqttOptions;
        private bool _mqttTransportRunning;
        private string _pendingSnapshotJson;
        private readonly object _webSocketMessageLock = new object();
        private CancellationTokenSource _webSocketCancellation;
        private ClientWebSocket _webSocketClient;
        private bool _webSocketTransportRunning;
        private CancellationTokenSource _tcpStreamCancellation;
        private TcpClient _tcpStreamClient;
        private bool _tcpStreamTransportRunning;
        private string _pendingWebSocketSnapshotJson;
        private float _lastWebSocketSnapshotRealtime = -1f;
        private float _lastMqttSnapshotRealtime = -1f;
        private bool _usingHttpFallback;
        private bool _usingMqttFallback;
        private readonly List<TrafficRadarDataManager.AircraftData> _trafficRows = new List<TrafficRadarDataManager.AircraftData>(19);

        private struct StreamWeatherMetrics
        {
            public float Precipitation;
            public float CloudCoverage;
            public float Turbulence;
            public float WindDirection;
            public float WindSpeed;
            public float VisibilityMeters;
            public float CloudBaseMeters;
            public float TemperatureC;
            public float Intensity;
        }

        private AirspeedHUD[] _airspeedHuds = Array.Empty<AirspeedHUD>();
        private AltitudeHUD[] _altitudeHuds = Array.Empty<AltitudeHUD>();
        private VerticalSpeedHUD[] _verticalSpeedHuds = Array.Empty<VerticalSpeedHUD>();
        private HeadingHUD[] _headingHuds = Array.Empty<HeadingHUD>();
        private WindDirection[] _windIndicators = Array.Empty<WindDirection>();
        private AttitudeHUD[] _attitudeHuds = Array.Empty<AttitudeHUD>();
        private AttitudeHUDNew[] _attitudeHudNews = Array.Empty<AttitudeHUDNew>();
        private FlightPathVector[] _flightPathVectors = Array.Empty<FlightPathVector>();
        private SlipSkidHUD[] _slipSkidHuds = Array.Empty<SlipSkidHUD>();
        private AltitudeAGL[] _altitudeAglDisplays = Array.Empty<AltitudeAGL>();
        private CourseDeviation[] _courseDeviationHuds = Array.Empty<CourseDeviation>();
        private Glideslope[] _glideslopeHuds = Array.Empty<Glideslope>();
        private LocalizerElement[] _localizerElements = Array.Empty<LocalizerElement>();
        private GlidescopeElement[] _glidescopeElements = Array.Empty<GlidescopeElement>();
        private AirspeedIndicatorElement[] _airspeedIndicatorElements = Array.Empty<AirspeedIndicatorElement>();
        private AltimeterElement[] _altimeterElements = Array.Empty<AltimeterElement>();
        private TorquePanelElement[] _torquePanelElements = Array.Empty<TorquePanelElement>();
        private NRIndicatorElement[] _nrIndicatorElements = Array.Empty<NRIndicatorElement>();

        public bool IsRunning => _pollRoutine != null || _mqttTransportRunning || _webSocketTransportRunning || _tcpStreamTransportRunning;
        public bool IsFeedHealthy { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public float LastPacketAgeSeconds { get; private set; } = float.PositiveInfinity;
        public string LastSender { get; private set; } = string.Empty;
        public int TrafficCount { get; private set; }
        public AviationFlightData LatestFlightData => _latestFlightData;
        public AviationFlightData LatestRawFlightData => _rawFlightData;
        public XPlane12ApiSnapshot LatestSnapshot => _snapshot;
        private Texture2D _latestWeatherTexture;
        private Texture2D _latestTrafficTexture;
        private Texture2D _streamWeatherTexture;
        private Color32[] _streamWeatherPixels;
        private float _lastDownloadedWeatherTextureRealtime = -1f;
        private float _lastStreamWeatherTextureRealtime = -1f;

        public Texture2D LatestWeatherTexture => _latestWeatherTexture;
        public Texture2D LatestTrafficTexture => _latestTrafficTexture;
        public TransportMode CurrentTransportMode => transportMode;

        private void Awake()
        {
            // This FAA scene is intentionally X-Plane-only. Port 12678 is locally
            // forwarded over SSH to tang-server, avoiding Unity/libcurl HTTP/2 issues
            // while still consuming the remote live X-Plane API.
            baseUrl = TangTunnelLiveApiBaseUrl;
            transportMode = TransportMode.HttpApi;
            allowExternalTrafficFallback = false;
            // Weather presentation is synthesized from the live datarefs in
            // each coherent 4090 snapshot. This deliberately avoids pulling
            // the native X-Plane radar PNG into the Unity HUD.
            publishWeatherDatarefTextureFromStream = true;

            FindDependencies();
            if (!allowExternalTrafficFallback)
            {
                SuppressExternalTrafficFetching();
            }
        }

        private void Start()
        {
            if (Application.isPlaying && autoStartOnPlay)
            {
                StartBridge();
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            MaintainTrafficApiFallback();

            if (transportMode == TransportMode.MqttSnapshot || _usingMqttFallback)
            {
                ProcessPendingMqttSnapshot();
            }

            if (transportMode == TransportMode.WebSocketStream || transportMode == TransportMode.TcpNdjsonStream)
            {
                ProcessPendingWebSocketSnapshot();
                UpdateWebSocketFallbackState();
            }

            ApplyContinuousDisplayFrame();
            ClearEnginePointersWhenFeedIsStale();
        }

        private void OnDisable()
        {
            StopBridge();
        }

        private void OnDestroy()
        {
            DestroyTexture(ref _latestWeatherTexture);
            DestroyTexture(ref _latestTrafficTexture);
            DestroyTexture(ref _streamWeatherTexture);
        }

        [ContextMenu("Start X-Plane 12 API Bridge")]
        public void StartBridge()
        {
            if (IsRunning)
            {
                return;
            }

            FindDependencies();
            if (transportMode == TransportMode.WebSocketStream || transportMode == TransportMode.TcpNdjsonStream)
            {
                if (webSocketUseMqttFallback)
                {
                    StartMqttTransport();
                }
                if (transportMode == TransportMode.TcpNdjsonStream)
                {
                    StartTcpStreamTransport();
                }
                else
                {
                    StartWebSocketTransport();
                }
                if (webSocketUseHttpFallback)
                {
                    _pollRoutine = StartCoroutine(PollLoop());
                }
            }
            else if (transportMode == TransportMode.MqttSnapshot)
            {
                StartMqttTransport();
            }
            else
            {
                _pollRoutine = StartCoroutine(PollLoop());
            }

            if (pollRenderAssets && _renderAssetRoutine == null)
            {
                _renderAssetRoutine = StartCoroutine(RenderAssetLoop());
            }

            MaintainTrafficApiFallback();
        }

        [ContextMenu("Stop X-Plane 12 API Bridge")]
        public void StopBridge()
        {
            if (_pollRoutine != null)
            {
                StopCoroutine(_pollRoutine);
                _pollRoutine = null;
            }

            StopMqttTransport();
            StopWebSocketTransport();
            StopTcpStreamTransport();

            if (_renderAssetRoutine != null)
            {
                StopCoroutine(_renderAssetRoutine);
                _renderAssetRoutine = null;
            }

            RestoreSuppressedSystems();
            ClearEngineHudPointers();
            IsFeedHealthy = false;
            _pendingSnapshotJson = null;
            _pendingWebSocketSnapshotJson = null;
            _lastWebSocketSnapshotRealtime = -1f;
            _lastMqttSnapshotRealtime = -1f;
            _usingMqttFallback = false;
            _usingHttpFallback = false;
            _hasSmoothedFlightData = false;
            _smoothedFlightData = null;
            _rawFlightData = null;
            _targetFlightData = null;
            _latestFlightData = null;
            _lastDisplayApplyRealtime = 0f;
            _lastFlightSnapshotRealtime = -1f;
            _enginePointersClearedForStaleFeed = true;
            _hasTargetFlightData = false;
        }

        [ContextMenu("Transport/Use WebSocket Stream")]
        public void UseWebSocketTransport()
        {
            SetTransportMode(TransportMode.WebSocketStream);
        }

        [ContextMenu("Transport/Use TCP NDJSON Stream")]
        public void UseTcpNdjsonTransport()
        {
            SetTransportMode(TransportMode.TcpNdjsonStream);
        }

        [ContextMenu("Transport/Use MQTT Snapshot")]
        public void UseMqttTransport()
        {
            SetTransportMode(TransportMode.MqttSnapshot);
        }

        [ContextMenu("Transport/Use HTTP API Polling")]
        public void UseHttpApiTransport()
        {
            SetTransportMode(TransportMode.HttpApi);
        }

        public void SetTransportMode(TransportMode mode, bool restartIfRunning = true)
        {
            if (transportMode == mode)
            {
                return;
            }

            bool wasRunning = IsRunning;
            if (wasRunning && restartIfRunning)
            {
                StopBridge();
            }

            transportMode = mode;

            if (wasRunning && restartIfRunning && Application.isPlaying && enabled)
            {
                StartBridge();
            }
        }

        [ContextMenu("Refresh Targets")]
        public void FindDependencies()
        {
            if (uiManager == null)
            {
                uiManager = FindAnyObjectByType<AviationUIManager>(FindObjectsInactive.Include);
            }

            if (flightDataProvider == null)
            {
                flightDataProvider = FindAnyObjectByType<AviationFlightDataProvider>(FindObjectsInactive.Include);
            }

            if (flightDataProvider == null && uiManager != null)
            {
                flightDataProvider = uiManager.DataProvider;
            }

            if (aircraftController == null)
            {
                aircraftController = FindAnyObjectByType<AircraftController>(FindObjectsInactive.Include);
            }

            if (hudController == null)
            {
                hudController = FindAnyObjectByType<HUDControl.Core.HUDController>(FindObjectsInactive.Include);
            }

            if (trafficRadarDataManager == null)
            {
                trafficRadarDataManager = FindAnyObjectByType<TrafficRadarDataManager>(FindObjectsInactive.Include);
            }

            if (trafficRadarController == null)
            {
                trafficRadarController = FindAnyObjectByType<TrafficRadarController>(FindObjectsInactive.Include);
            }

            if (weatherRadarProvider == null)
            {
                weatherRadarProvider = FindAnyObjectByType<WeatherRadarProviderBase>(FindObjectsInactive.Include);
            }

            if (weatherRadarDataProvider == null)
            {
                weatherRadarDataProvider = FindAnyObjectByType<WeatherRadarDataProvider>(FindObjectsInactive.Include);
            }

            if (xPlaneWeatherRadarDisplay == null)
            {
                xPlaneWeatherRadarDisplay = FindAnyObjectByType<XPlaneOriginalWeatherRadarDisplay>(FindObjectsInactive.Include);
            }

            if (weatherImageTarget == null && xPlaneWeatherRadarDisplay != null)
            {
                weatherImageTarget = xPlaneWeatherRadarDisplay.TargetImage;
            }

            if (weatherRadarProvider is XPlaneOriginalWeatherRadarProvider originalWeatherProvider)
            {
                if (publishWeatherDatarefTextureFromStream || !originalWeatherProvider.UsesNativeTexture)
                {
                    // Keep the active FAA path dataref-only. In particular,
                    // do not repopulate the legacy raster URL while refreshing
                    // dependencies after a scene reload.
                    originalWeatherProvider.UseProceduralDatarefTexture();
                }
                else
                {
                    originalWeatherProvider.RadarTextureUrl = BuildUrl(GetWeatherTexturePath());
                }
            }

            TrafficRadarDisplay preferredTrafficDisplay = FindPreferredTrafficTextureDisplay();
            if (preferredTrafficDisplay != null &&
                (xPlaneTrafficRadarDisplay == null ||
                 xPlaneTrafficRadarDisplay != preferredTrafficDisplay ||
                 !IsUsableTrafficTextureTarget(trafficImageTarget)))
            {
                xPlaneTrafficRadarDisplay = preferredTrafficDisplay;
            }

            if (!IsUsableTrafficTextureTarget(trafficImageTarget) &&
                xPlaneTrafficRadarDisplay != null &&
                xPlaneTrafficRadarDisplay.UsesXPlaneTrafficTexture)
            {
                trafficImageTarget = xPlaneTrafficRadarDisplay.RadarImage;
            }

            CacheLegacyHudComponents();
        }

        private void CacheLegacyHudComponents()
        {
            _airspeedHuds = FindSceneObjects<AirspeedHUD>();
            _altitudeHuds = FindSceneObjects<AltitudeHUD>();
            _verticalSpeedHuds = FindSceneObjects<VerticalSpeedHUD>();
            _headingHuds = FindSceneObjects<HeadingHUD>();
            _windIndicators = FindSceneObjects<WindDirection>();
            _attitudeHuds = FindSceneObjects<AttitudeHUD>();
            _attitudeHudNews = FindSceneObjects<AttitudeHUDNew>();
            _flightPathVectors = FindSceneObjects<FlightPathVector>();
            _slipSkidHuds = FindSceneObjects<SlipSkidHUD>();
            _altitudeAglDisplays = FindSceneObjects<AltitudeAGL>();
            _courseDeviationHuds = FindSceneObjects<CourseDeviation>();
            _glideslopeHuds = FindSceneObjects<Glideslope>();
            _localizerElements = FindSceneObjects<LocalizerElement>();
            _glidescopeElements = FindSceneObjects<GlidescopeElement>();
            _airspeedIndicatorElements = Array.FindAll(
                FindSceneObjects<AirspeedIndicatorElement>(),
                element => element != null && element.enabled && element.gameObject.activeInHierarchy);
            _altimeterElements = Array.FindAll(
                FindSceneObjects<AltimeterElement>(),
                element => element != null && element.enabled && element.gameObject.activeInHierarchy);
            _torquePanelElements = Array.FindAll(
                FindSceneObjects<TorquePanelElement>(),
                element => element != null && element.enabled && element.gameObject.activeInHierarchy);
            _nrIndicatorElements = Array.FindAll(
                FindSceneObjects<NRIndicatorElement>(),
                element => element != null && element.enabled && element.gameObject.activeInHierarchy);
        }

        private static T[] FindSceneObjects<T>() where T : Component
        {
            return FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private void StartWebSocketTransport()
        {
            if (_webSocketTransportRunning)
            {
                return;
            }

            _webSocketTransportRunning = true;
            _webSocketCancellation = new CancellationTokenSource();
            CancellationToken token = _webSocketCancellation.Token;
            _ = Task.Run(() => RunWebSocketLoopAsync(token), token);
        }

        private void StopWebSocketTransport()
        {
            _webSocketTransportRunning = false;

            CancellationTokenSource cancellation = _webSocketCancellation;
            _webSocketCancellation = null;
            if (cancellation != null)
            {
                try
                {
                    cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            ClientWebSocket client = _webSocketClient;
            _webSocketClient = null;
            if (client != null)
            {
                try
                {
                    client.Abort();
                    client.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            cancellation?.Dispose();
        }

        private void StartTcpStreamTransport()
        {
            if (_tcpStreamTransportRunning)
            {
                return;
            }

            _tcpStreamTransportRunning = true;
            _tcpStreamCancellation = new CancellationTokenSource();
            CancellationToken token = _tcpStreamCancellation.Token;
            _ = Task.Run(() => RunTcpStreamLoopAsync(token), token);
        }

        private void StopTcpStreamTransport()
        {
            _tcpStreamTransportRunning = false;

            CancellationTokenSource cancellation = _tcpStreamCancellation;
            _tcpStreamCancellation = null;
            if (cancellation != null)
            {
                try
                {
                    cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            TcpClient client = _tcpStreamClient;
            _tcpStreamClient = null;
            if (client != null)
            {
                try
                {
                    client.Close();
                    client.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            cancellation?.Dispose();
        }

        private async Task RunTcpStreamLoopAsync(CancellationToken cancellationToken)
        {
            string host = string.IsNullOrWhiteSpace(tcpStreamHost) ? "127.0.0.1" : tcpStreamHost.Trim();
            int port = Mathf.Clamp(tcpStreamPort, 1, 65535);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (TcpClient client = new TcpClient())
                    {
                        client.NoDelay = true;
                        _tcpStreamClient = client;
                        await client.ConnectAsync(host, port);

                        LastSender = "tcp-ndjson";
                        LastError = string.Empty;
                        LastPacketAgeSeconds = 0f;
                        SetHealthy(true);

                        using (NetworkStream stream = client.GetStream())
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, Mathf.Clamp(webSocketReceiveBufferBytes, 4096, 1024 * 1024), leaveOpen: false))
                        {
                            while (!cancellationToken.IsCancellationRequested && client.Connected)
                            {
                                string snapshotJson = await reader.ReadLineAsync();
                                if (snapshotJson == null)
                                {
                                    break;
                                }

                                if (string.IsNullOrWhiteSpace(snapshotJson))
                                {
                                    continue;
                                }

                                lock (_webSocketMessageLock)
                                {
                                    _pendingWebSocketSnapshotJson = snapshotJson;
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!_tcpStreamTransportRunning || cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    LastError = $"TCP NDJSON stream failed: {ex.Message}";
                    SetHealthy(_hasTargetFlightData && LastPacketAgeSeconds <= staleHoldSeconds);
                    if (verboseLogging)
                    {
                        Debug.LogWarning($"[XPlane12ApiHudBridge] {LastError}");
                    }
                }
                finally
                {
                    _tcpStreamClient = null;
                }

                if (!_tcpStreamTransportRunning || cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    int delayMs = Mathf.RoundToInt(Mathf.Max(0.1f, webSocketReconnectDelaySeconds) * 1000f);
                    await Task.Delay(delayMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task RunWebSocketLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string url = string.IsNullOrWhiteSpace(webSocketUrl)
                    ? "ws://127.0.0.1:37212/v1/stream/ws"
                    : webSocketUrl.Trim();

                try
                {
                    using (ClientWebSocket client = new ClientWebSocket())
                    {
                        _webSocketClient = client;
                        client.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
                        await client.ConnectAsync(new Uri(url), cancellationToken);

                        LastSender = "websocket";
                        LastError = string.Empty;
                        LastPacketAgeSeconds = 0f;
                        SetHealthy(true);

                        await ReceiveWebSocketMessagesAsync(client, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!_webSocketTransportRunning || cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    LastError = $"WebSocket stream failed: {ex.Message}";
                    SetHealthy(_hasTargetFlightData && LastPacketAgeSeconds <= staleHoldSeconds);
                    if (verboseLogging)
                    {
                        Debug.LogWarning($"[XPlane12ApiHudBridge] {LastError}");
                    }
                }
                finally
                {
                    _webSocketClient = null;
                }

                if (!_webSocketTransportRunning || cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    int delayMs = Mathf.RoundToInt(Mathf.Max(0.1f, webSocketReconnectDelaySeconds) * 1000f);
                    await Task.Delay(delayMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ReceiveWebSocketMessagesAsync(ClientWebSocket client, CancellationToken cancellationToken)
        {
            int bufferSize = Mathf.Clamp(webSocketReceiveBufferBytes, 4096, 1024 * 1024);
            byte[] buffer = new byte[bufferSize];

            while (!cancellationToken.IsCancellationRequested && client.State == WebSocketState.Open)
            {
                using (MemoryStream messageStream = new MemoryStream(bufferSize))
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }

                        messageStream.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType != WebSocketMessageType.Text)
                    {
                        continue;
                    }

                    string snapshotJson = Encoding.UTF8.GetString(messageStream.ToArray());
                    if (string.IsNullOrWhiteSpace(snapshotJson))
                    {
                        continue;
                    }

                    lock (_webSocketMessageLock)
                    {
                        _pendingWebSocketSnapshotJson = snapshotJson;
                    }
                }
            }
        }

        private void StartMqttTransport()
        {
            if (_mqttTransportRunning)
            {
                return;
            }

            _mqttTransportRunning = true;
            EnsureMqttClient();
            _ = ConnectMqttAsync();
        }

        private void StopMqttTransport()
        {
            _mqttTransportRunning = false;

            if (_mqttClient == null)
            {
                return;
            }

            try
            {
                if (_mqttClient.IsConnected)
                {
                    _mqttClient.DisconnectAsync().GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                if (verboseLogging)
                {
                    Debug.LogWarning($"[XPlane12ApiHudBridge] MQTT disconnect failed: {ex.Message}");
                }
            }
            finally
            {
                _mqttClient.Dispose();
                _mqttClient = null;
            }
        }

        private void EnsureMqttClient()
        {
            if (_mqttClient != null)
            {
                return;
            }

            _mqttFactory = new MqttFactory();
            _mqttClient = _mqttFactory.CreateMqttClient();

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithClientId($"{mqttClientId}-{SystemInfo.deviceName}")
                .WithTcpServer(mqttBrokerHost, mqttBrokerPort)
                .WithCleanSession();

            if (!string.IsNullOrWhiteSpace(mqttUsername))
            {
                optionsBuilder.WithCredentials(mqttUsername, mqttPassword);
            }

            _mqttOptions = optionsBuilder.Build();

            _mqttClient.UseConnectedHandler(async _ =>
            {
                await _mqttClient.SubscribeAsync(new TopicFilterBuilder()
                    .WithTopic(mqttSnapshotTopic)
                    .Build());

                LastSender = "mqtt";
                LastError = string.Empty;
                LastPacketAgeSeconds = 0f;
                SetHealthy(true);

                if (verboseLogging)
                {
                    Debug.Log($"[XPlane12ApiHudBridge] Subscribed to MQTT topic {mqttSnapshotTopic} at {mqttBrokerHost}:{mqttBrokerPort}.");
                }
            });

            _mqttClient.UseDisconnectedHandler(async e =>
            {
                if (!_mqttTransportRunning)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    LastError = $"MQTT disconnected: {e.Exception.Message}";
                    SetHealthy(false);
                }

                if (!mqttAutoReconnect)
                {
                    return;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Mathf.Max(0.5f, mqttReconnectDelaySeconds)));
                    if (_mqttTransportRunning)
                    {
                        await ConnectMqttAsync();
                    }
                }
                catch (Exception reconnectError)
                {
                    LastError = $"MQTT reconnect failed: {reconnectError.Message}";
                    SetHealthy(false);
                }
            });

            _mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                if (!string.Equals(e.ApplicationMessage.Topic, mqttSnapshotTopic, StringComparison.Ordinal))
                {
                    return;
                }

                lock (_mqttMessageLock)
                {
                    _pendingSnapshotJson = Encoding.UTF8.GetString(e.ApplicationMessage.Payload ?? Array.Empty<byte>());
                }
            });
        }

        private async Task ConnectMqttAsync()
        {
            if (_mqttClient == null || _mqttOptions == null || _mqttClient.IsConnected)
            {
                return;
            }

            try
            {
                await _mqttClient.ConnectAsync(_mqttOptions, CancellationToken.None);
            }
            catch (Exception ex)
            {
                LastError = $"MQTT connect failed: {ex.Message}";
                SetHealthy(false);
                if (verboseLogging)
                {
                    Debug.LogWarning($"[XPlane12ApiHudBridge] {LastError}");
                }
            }
        }

        private void UpdateWebSocketFallbackState()
        {
            if (!webSocketUseMqttFallback && !webSocketUseHttpFallback)
            {
                _usingMqttFallback = false;
                _usingHttpFallback = false;
                return;
            }

            float now = Time.realtimeSinceStartup;
            float fallbackAfter = Mathf.Max(0.25f, webSocketFallbackAfterSeconds);
            bool webSocketRecent = _lastWebSocketSnapshotRealtime >= 0f &&
                now - _lastWebSocketSnapshotRealtime <= fallbackAfter;
            bool mqttRecent = _lastMqttSnapshotRealtime >= 0f &&
                now - _lastMqttSnapshotRealtime <= fallbackAfter;
            string expectedStreamSender = transportMode == TransportMode.TcpNdjsonStream ? "tcp-ndjson" : "websocket";
            bool shouldUseStreamFallback = !webSocketRecent &&
                (!IsFeedHealthy || LastSender != expectedStreamSender || LastPacketAgeSeconds > fallbackAfter);
            bool shouldUseMqttFallback = webSocketUseMqttFallback && shouldUseStreamFallback;
            bool shouldUseHttpFallback = webSocketUseHttpFallback && shouldUseStreamFallback && !mqttRecent;

            if (shouldUseMqttFallback == _usingMqttFallback && shouldUseHttpFallback == _usingHttpFallback)
            {
                return;
            }

            _usingMqttFallback = shouldUseMqttFallback;
            _usingHttpFallback = shouldUseHttpFallback;
            if (verboseLogging)
            {
                if (_usingHttpFallback)
                {
                    Debug.Log("[XPlane12ApiHudBridge] Stream/MQTT stale; applying HTTP snapshot fallback.");
                }
                else if (_usingMqttFallback)
                {
                    Debug.Log("[XPlane12ApiHudBridge] Stream stale; applying MQTT fallback snapshots.");
                }
                else
                {
                    Debug.Log("[XPlane12ApiHudBridge] stream snapshots recovered; fallbacks remain standby.");
                }
            }
        }

        private void ProcessPendingWebSocketSnapshot()
        {
            string snapshotJson = null;
            lock (_webSocketMessageLock)
            {
                if (!string.IsNullOrWhiteSpace(_pendingWebSocketSnapshotJson))
                {
                    snapshotJson = _pendingWebSocketSnapshotJson;
                    _pendingWebSocketSnapshotJson = null;
                }
            }

            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                return;
            }

            try
            {
                JObject snapshot = JObject.Parse(snapshotJson);
                _lastWebSocketSnapshotRealtime = Time.realtimeSinceStartup;
                _usingMqttFallback = false;
                _usingHttpFallback = false;
                ApplySnapshotEnvelope(snapshot);
            }
            catch (Exception ex)
            {
                LastError = $"WebSocket snapshot parse failed: {ex.Message}";
                SetHealthy(false);
            }
        }

        private void ProcessPendingMqttSnapshot()
        {
            string snapshotJson = null;
            lock (_mqttMessageLock)
            {
                if (!string.IsNullOrWhiteSpace(_pendingSnapshotJson))
                {
                    snapshotJson = _pendingSnapshotJson;
                    _pendingSnapshotJson = null;
                }
            }

            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                return;
            }

            try
            {
                JObject snapshot = JObject.Parse(snapshotJson);
                _lastMqttSnapshotRealtime = Time.realtimeSinceStartup;
                ApplySnapshotEnvelope(snapshot);
            }
            catch (Exception ex)
            {
                LastError = $"MQTT snapshot parse failed: {ex.Message}";
                SetHealthy(false);
            }
        }

        private IEnumerator PollLoop()
        {
            while (enabled)
            {
                if ((transportMode != TransportMode.WebSocketStream && transportMode != TransportMode.TcpNdjsonStream) || _usingHttpFallback)
                {
                    yield return PollOnce();
                }
                yield return new WaitForSeconds(Mathf.Max(0.05f, pollIntervalSeconds));
            }
        }

        private IEnumerator PollOnce()
        {
            if (transportMode == TransportMode.WebSocketStream || transportMode == TransportMode.TcpNdjsonStream)
            {
                yield return RequestJson("v1/snapshot", snapshot =>
                {
                    LastSender = "http";
                    LastPacketAgeSeconds = 0f;
                    ApplySnapshotEnvelope(snapshot);
                }, suppressFailureState: true);
                yield break;
            }

            // The local 12678 forward terminates on tang-server and reaches
            // the 4090 xplane12_data_api. Prefer its single coherent snapshot
            // envelope so IAS, altitude, engines, systems, weather, and
            // traffic all come from the same websocket-origin packet. The
            // category endpoints remain a compatibility fallback for older
            // API builds.
            bool receivedCoherentSnapshot = false;
            yield return RequestJson("v1/snapshot", snapshot =>
            {
                ApplySnapshotEnvelope(snapshot);
                receivedCoherentSnapshot = snapshot != null && _snapshot.Aircraft.Count > 0;
            }, suppressFailureState: true);
            if (receivedCoherentSnapshot)
            {
                yield break;
            }

            bool receivedAny = false;

            bool receivedHealth = false;
            yield return RequestJson("api/health", json =>
            {
                ApplyHealth(json);
                receivedHealth = true;
                receivedAny = true;
            }, suppressFailureState: true);
            if (!receivedHealth)
            {
                yield return RequestJson("health", json =>
                {
                    ApplyHealth(json);
                    receivedAny = true;
                });
            }

            if (pollAircraft)
            {
                yield return RequestValues("aircraft", values =>
                {
                    _snapshot.Aircraft = values;
                    receivedAny = true;
                });
            }

            if (pollWeather)
            {
                yield return RequestValues("weather", values =>
                {
                    _snapshot.Weather = values;
                    receivedAny = true;
                });
            }

            if (pollSystems)
            {
                yield return RequestValues("systems", values =>
                {
                    _snapshot.Systems = values;
                    ApplyWeatherRadarPowerStateToDisplay();
                    receivedAny = true;
                });
            }

            if (pollTraffic)
            {
                yield return RequestValues("traffic", values =>
                {
                    _snapshot.Traffic = values;
                    receivedAny = true;
                });
            }

            if (receivedAny && _snapshot.Aircraft.Count > 0)
            {
                ApplySnapshot();
            }
        }

        private IEnumerator RequestValues(string category, Action<Dictionary<string, float>> onSuccess)
        {
            bool receivedValues = false;
            string primaryUrl = BuildUrl($"api/data?category={category}");
            yield return RequestJson(primaryUrl, json =>
            {
                onSuccess?.Invoke(ReadValues(json));
                receivedValues = true;
                string lastError = json.Value<string>("last_error") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(lastError))
                {
                    LastError = lastError;
                }
            }, true, suppressFailureState: true);

            if (receivedValues)
            {
                yield break;
            }

            string fallbackUrl = BuildUrl($"data?category={category}");
            yield return RequestJson(fallbackUrl, json =>
            {
                onSuccess?.Invoke(ReadValues(json));
                string lastError = json.Value<string>("last_error") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(lastError))
                {
                    LastError = lastError;
                }
            }, true);
        }

        private IEnumerator RequestJson(
            string relativeOrAbsoluteUrl,
            Action<JObject> onSuccess,
            bool alreadyBuiltUrl = false,
            bool suppressFailureState = false)
        {
            string url = alreadyBuiltUrl ? relativeOrAbsoluteUrl : BuildUrl(relativeOrAbsoluteUrl);
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = Mathf.Max(1, Mathf.RoundToInt(requestTimeoutSeconds));
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (!suppressFailureState)
                    {
                        LastError = $"{url}: {request.error}";
                        SetHealthy(false);
                    }
                    yield break;
                }

                try
                {
                    JObject json = JObject.Parse(request.downloadHandler.text);
                    onSuccess?.Invoke(json);
                }
                catch (Exception ex)
                {
                    if (!suppressFailureState)
                    {
                        LastError = $"{url}: JSON parse failed: {ex.Message}";
                        SetHealthy(false);
                    }
                }
            }
        }

        private void ApplyHealth(JObject json)
        {
            string status = json.Value<string>("status") ?? string.Empty;
            LastPacketAgeSeconds = ReadFloat(json["last_packet_age_sec"], float.PositiveInfinity);
            LastSender = json.Value<string>("last_sender") ?? string.Empty;
            LastError = json.Value<string>("last_error") ?? LastError;

            bool healthy = string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase) && LastPacketAgeSeconds <= staleAfterSeconds;
            SetHealthy(healthy);
        }

        private void ApplySnapshotEnvelope(JObject snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            JObject health = snapshot["health"] as JObject;
            if (health != null)
            {
                LastPacketAgeSeconds = ReadFloat(health["last_packet_age_sec"], 0f);
                LastError = health.Value<string>("last_error") ?? string.Empty;
                LastSender = snapshot.Value<string>("source_mode") ?? LastSender;
                bool healthy = string.Equals(health.Value<string>("status"), "ok", StringComparison.OrdinalIgnoreCase) &&
                    LastPacketAgeSeconds <= staleAfterSeconds;
                SetHealthy(healthy);
            }
            else
            {
                LastSender = snapshot.Value<string>("source_mode") ?? LastSender;
                LastError = string.Empty;
                LastPacketAgeSeconds = 0f;
                SetHealthy(true);
            }

            PopulateSnapshotFromRaw(snapshot["raw"] as JObject);
            if (_snapshot.Aircraft.Count == 0)
            {
                PopulateSnapshotFromSections(snapshot);
            }
            if (!HasMultiplayerTraffic(_snapshot.Traffic))
            {
                PopulateTrafficFromSections(snapshot);
            }

            if (_snapshot.Aircraft.Count > 0)
            {
                ApplySnapshot();
            }
            else
            {
                ApplyWeatherRadarPowerStateToDisplay();
            }
        }

        private void SetHealthy(bool healthy)
        {
            IsFeedHealthy = healthy;
            if (_hasLoggedConnection && healthy == _lastHealthyState)
            {
                return;
            }

            _hasLoggedConnection = true;
            _lastHealthyState = healthy;

            if (healthy)
            {
                Debug.Log($"[XPlane12ApiHudBridge] Connected via {DescribeTransport()} from {LastSender}. Last packet age {LastPacketAgeSeconds:F1}s.");
            }
            else if (!string.IsNullOrWhiteSpace(LastError))
            {
                Debug.LogWarning($"[XPlane12ApiHudBridge] Feed unhealthy: {LastError}");
            }
        }

        private void ApplySnapshot()
        {
            _lastFlightSnapshotRealtime = Time.realtimeSinceStartup;
            _enginePointersClearedForStaleFeed = false;
            _rawFlightData = BuildFlightData(_snapshot.Aircraft, _snapshot.Weather, _snapshot.Systems);
            _targetFlightData = BuildDisplayTargetFlightData(_rawFlightData);
            _hasTargetFlightData = _targetFlightData != null;
            ApplyWeatherRadarPowerStateToDisplay();

            if (_hasTargetFlightData)
            {
                ApplyContinuousDisplayFrame(!interpolateDisplayBetweenPackets);
            }

            if (applyToTrafficRadar && _targetFlightData != null)
            {
                ApplyToTrafficRadar(_targetFlightData);
            }

            if (verboseLogging && _targetFlightData != null)
            {
                Debug.Log($"[XPlane12ApiHudBridge] HUD target IAS={_targetFlightData.indicatedAirspeed:F0} ALT={_targetFlightData.altitudeMSL:F0} HDG={_targetFlightData.heading:F0} traffic={TrafficCount}");
            }
        }

        private void ApplyContinuousDisplayFrame(bool forceSnap = false)
        {
            if (!_hasTargetFlightData || _targetFlightData == null)
            {
                return;
            }

            _latestFlightData = BuildContinuousDisplayFlightData(_targetFlightData, forceSnap);
            if (_latestFlightData == null)
            {
                return;
            }

            if (applyToAviationHud)
            {
                ApplyToAviationHud(_latestFlightData);
            }

            if (applyToAircraftController)
            {
                ApplyToAircraftController(_latestFlightData);
            }

            ApplyToHudControlStack(_latestFlightData);

            if (applyToLegacyHud)
            {
                ApplyToLegacyHud(_latestFlightData);
            }

            if (applyToWeatherRadar)
            {
                ApplyToWeatherRadar(_latestFlightData);
            }

        }

        private AviationFlightData BuildDisplayTargetFlightData(AviationFlightData rawData)
        {
            if (rawData == null)
            {
                return null;
            }

            AviationFlightData targetData = rawData.Clone();
            ApplyPacketAgeCompensation(targetData);

            return targetData;
        }

        private AviationFlightData BuildContinuousDisplayFlightData(AviationFlightData targetData, bool forceSnap)
        {
            if (targetData == null)
            {
                return null;
            }

            if (smoothingStrategy == DataSmoothingStrategy.None || forceSnap || !interpolateDisplayBetweenPackets)
            {
                ResetSmoothedFlightData(targetData);
                return targetData.Clone();
            }

            float now = Time.realtimeSinceStartup;
            float dt = _lastDisplayApplyRealtime > 0f
                ? Mathf.Clamp(now - _lastDisplayApplyRealtime, 0.001f, 0.1f)
                : Time.unscaledDeltaTime;
            _lastDisplayApplyRealtime = now;

            if (!_hasSmoothedFlightData || _smoothedFlightData == null || ShouldSnapToTarget(_smoothedFlightData, targetData))
            {
                ResetSmoothedFlightData(targetData);
                return targetData.Clone();
            }

            float responseRate = ShouldUseAggressiveSmoothing(_smoothedFlightData, targetData)
                ? Mathf.Max(smoothingResponseRate, aggressiveSmoothingResponseRate)
                : smoothingResponseRate;
            float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, responseRate) * dt);
            _smoothedFlightData = LerpFlightData(_smoothedFlightData, targetData, t);
            return _smoothedFlightData.Clone();
        }

        private void ApplyPacketAgeCompensation(AviationFlightData data)
        {
            if (!compensatePacketAge || data == null || !IsFinite(LastPacketAgeSeconds))
            {
                return;
            }

            float predictionSeconds = Mathf.Clamp(LastPacketAgeSeconds, 0f, Mathf.Max(0f, maxPredictionSeconds));
            if (predictionSeconds <= 0.001f)
            {
                return;
            }

            float altitudeDeltaFeet = data.verticalSpeed / 60f * predictionSeconds;
            data.altitudeMSL += altitudeDeltaFeet;
            data.altitudeAGL = Mathf.Max(0f, data.altitudeAGL + altitudeDeltaFeet);
        }

        private void ResetSmoothedFlightData(AviationFlightData data)
        {
            _smoothedFlightData = data?.Clone();
            _hasSmoothedFlightData = _smoothedFlightData != null;
        }

        private bool ShouldSnapToTarget(AviationFlightData current, AviationFlightData target)
        {
            if (current == null || target == null)
            {
                return true;
            }

            if (IsFinite(LastPacketAgeSeconds) && LastPacketAgeSeconds > staleAfterSeconds)
            {
                return true;
            }

            return Mathf.Abs(target.pitch - current.pitch) > attitudeSnapDegrees ||
                   Mathf.Abs(Mathf.DeltaAngle(current.roll, target.roll)) > attitudeSnapDegrees ||
                   Mathf.Abs(Mathf.DeltaAngle(current.heading, target.heading)) > headingSnapDegrees ||
                   Mathf.Abs(target.indicatedAirspeed - current.indicatedAirspeed) > airspeedSnapKnots ||
                   Mathf.Abs(target.altitudeMSL - current.altitudeMSL) > altitudeSnapFeet ||
                   Mathf.Abs(target.verticalSpeed - current.verticalSpeed) > verticalSpeedSnapFpm;
        }

        private static bool ShouldUseAggressiveSmoothing(AviationFlightData current, AviationFlightData target)
        {
            return Mathf.Abs(target.pitch - current.pitch) > 2f ||
                   Mathf.Abs(Mathf.DeltaAngle(current.roll, target.roll)) > 3f ||
                   Mathf.Abs(Mathf.DeltaAngle(current.heading, target.heading)) > 5f ||
                   Mathf.Abs(target.indicatedAirspeed - current.indicatedAirspeed) > 5f ||
                   Mathf.Abs(target.altitudeMSL - current.altitudeMSL) > 80f ||
                   Mathf.Abs(target.verticalSpeed - current.verticalSpeed) > 400f;
        }

        private static AviationFlightData LerpFlightData(AviationFlightData current, AviationFlightData target, float t)
        {
            float clampedT = Mathf.Clamp01(t);
            AviationFlightData data = AviationFlightData.Lerp(current, target, clampedT);
            data.roll = XPlaneDataRefMapper.NormalizeAngle(current.roll + Mathf.DeltaAngle(current.roll, target.roll) * clampedT);
            data.roll = XPlaneDataRefMapper.NormalizeAngle(data.roll);
            data.heading = XPlaneDataRefMapper.NormalizeHeading(data.heading);
            data.track = XPlaneDataRefMapper.NormalizeHeading(data.track);
            data.windDirection = XPlaneDataRefMapper.NormalizeHeading(data.windDirection);
            data.gpsValid = target.gpsValid;
            data.ilsValid = target.ilsValid;
            data.autopilotEngaged = target.autopilotEngaged;
            return data;
        }

        private AviationFlightData BuildFlightData(
            IDictionary<string, float> aircraft,
            IDictionary<string, float> weather,
            IDictionary<string, float> systems)
        {
            var data = new AviationFlightData();

            data.pitch = Mathf.Clamp(Get(aircraft, "sim/flightmodel/position/theta"), -90f, 90f);
            data.roll = XPlaneDataRefMapper.NormalizeAngle(Get(aircraft, "sim/flightmodel/position/phi"));
            data.heading = XPlaneDataRefMapper.NormalizeHeading(Get(aircraft, "sim/flightmodel/position/psi"));
            data.track = XPlaneDataRefMapper.NormalizeHeading(Get(aircraft, "sim/flightmodel/position/mag_psi", data.heading));
            data.magneticVariation = XPlaneDataRefMapper.NormalizeAngle(data.heading - data.track);

            data.indicatedAirspeed = Mathf.Max(0f, Get(aircraft, "sim/flightmodel/position/indicated_airspeed"));
            data.trueAirspeed = Mathf.Max(0f, Get(aircraft, "sim/flightmodel/position/true_airspeed") * MetersPerSecondToKnots);
            data.groundSpeed = Mathf.Max(0f, Get(aircraft, "sim/flightmodel/position/groundspeed") * MetersPerSecondToKnots);

            float altitudeMeters = Get(aircraft, "sim/flightmodel/position/elevation");
            float altitudeAglMeters = Get(aircraft, "sim/flightmodel/position/y_agl");
            data.altitudeMSL = altitudeMeters * MetersToFeet;
            data.altitudeAGL = altitudeAglMeters * MetersToFeet;
            data.verticalSpeed = Get(aircraft, "sim/flightmodel/position/vh_ind") * MetersPerSecondToFeetPerMinute;

            data.windSpeed = Mathf.Max(0f, GetWindSpeed(weather));
            data.windDirection = XPlaneDataRefMapper.NormalizeHeading(GetAny(weather, 0f,
                "sim/weather/aircraft/wind_now_direction_degt",
                "sim/weather/wind_direction_degt[0]",
                "sim/weather/aircraft/wind_direction_deg"));
            data.barometricSetting = GetBarometerInHg(weather);

            data.flightPathAngle = CalculateFlightPathAngle(data.verticalSpeed, data.groundSpeed);
            data.slipSkid = Mathf.Clamp(Get(aircraft, "sim/flightmodel/forces/g_side"), -1f, 1f);
            data.courseDeviation = Mathf.Clamp(GetNavigationDeviation(systems), -2.5f, 2.5f);
            data.glideslopeDeviation = Mathf.Clamp(GetGlideslopeDeviation(systems), -2.5f, 2.5f);
            data.gpsValid = true;
            data.ilsValid = GetAny(systems, 0f,
                "sim/cockpit2/radios/nav1_has_glideslope",
                "sim/cockpit/radios/nav1_CDI",
                "sim/cockpit/radios/gps_has_glideslope",
                "sim/cockpit/radios/gps2_has_glideslope",
                "sim/cockpit2/autopilot/nav_status") > 0.5f;
            data.autopilotEngaged = IsAutopilotEngaged(systems);

            ApplyEngineData(data, systems);
            return data;
        }

        private void ApplyEngineData(AviationFlightData data, IDictionary<string, float> systems)
        {
            data.engineCount = TryGetAny(
                systems,
                out float engineCount,
                "sim/aircraft/engine/acf_num_engines")
                ? Mathf.Clamp(Mathf.RoundToInt(engineCount), 0, 8)
                : 0;

            data.engine1TorqueValid = TryCalculateTorquePercent(systems, 0, out data.engine1Torque);
            data.engine2TorqueValid = data.engineCount >= 2 &&
                                      TryCalculateTorquePercent(systems, 1, out data.engine2Torque);

            data.engine1NRValid = TryReadEnginePercent(
                systems,
                "sim/cockpit2/engine/indicators/N2_percent[0]",
                110f,
                out data.engine1NR);
            data.engine2NRValid = data.engineCount >= 2 && TryReadEnginePercent(
                systems,
                "sim/cockpit2/engine/indicators/N2_percent[1]",
                110f,
                out data.engine2NR);

            data.engine1NGValid = TryReadEnginePercent(
                systems,
                "sim/cockpit2/engine/indicators/N1_percent[0]",
                120f,
                out data.engine1NG);
            data.engine2NGValid = data.engineCount >= 2 && TryReadEnginePercent(
                systems,
                "sim/cockpit2/engine/indicators/N1_percent[1]",
                120f,
                out data.engine2NG);

            data.rotorNRValid = TryCalculateRotorNrPercent(systems, 0, out data.rotorNR);
        }

        public static bool TryCalculateTorquePercent(
            IDictionary<string, float> systems,
            int engineIndex,
            out float percent)
        {
            percent = 0f;
            if (!TryGetFinite(systems, $"sim/flightmodel/engine/ENGN_driv_TRQ[{engineIndex}]", out float torqueNm) ||
                !TryGetFinite(systems, $"sim/flightmodel/engine/POINT_max_TRQ[{engineIndex}]", out float ratedTorqueNm))
            {
                return false;
            }

            ratedTorqueNm = Mathf.Abs(ratedTorqueNm);
            if (ratedTorqueNm <= 0.001f)
            {
                return false;
            }

            percent = Mathf.Clamp(Mathf.Abs(torqueNm) / ratedTorqueNm * 100f, 0f, 120f);
            return true;
        }

        public static bool TryCalculateRotorNrPercent(
            IDictionary<string, float> systems,
            int propellerIndex,
            out float percent)
        {
            percent = 0f;
            if (!TryGetFinite(
                    systems,
                    $"sim/cockpit2/engine/indicators/prop_speed_rpm[{propellerIndex}]",
                    out float propellerRpm) ||
                !TryGetFinite(systems, "sim/aircraft/controls/acf_RSC_redline_prp", out float redlineRadiansPerSecond))
            {
                return false;
            }

            float redlineRpm = Mathf.Abs(redlineRadiansPerSecond) * 60f / (2f * Mathf.PI);
            if (redlineRpm <= 0.001f)
            {
                return false;
            }

            percent = Mathf.Clamp(Mathf.Abs(propellerRpm) / redlineRpm * 100f, 0f, 110f);
            return true;
        }

        private static bool TryReadEnginePercent(
            IDictionary<string, float> systems,
            string key,
            float maximum,
            out float percent)
        {
            percent = 0f;
            if (!TryGetFinite(systems, key, out float value))
            {
                return false;
            }

            percent = Mathf.Clamp(value, 0f, maximum);
            return true;
        }

        private void ApplyToAviationHud(AviationFlightData data)
        {
            if (uiManager != null)
            {
                uiManager.UpdateFlightData(data);
            }

            if (flightDataProvider != null && (uiManager == null || uiManager.DataProvider != flightDataProvider))
            {
                flightDataProvider.UpdateFlightData(data);
            }
        }

        private void ApplyToAircraftController(AviationFlightData data)
        {
            if (aircraftController == null)
            {
                return;
            }

            if (aircraftController.State == null)
            {
                aircraftController.ResetToDefault();
            }

            SuppressLocalAircraftSimulation();

            float altitudeMeters = data.altitudeMSL / MetersToFeet;
            double latitude = Get(_snapshot.Aircraft, "sim/flightmodel/position/latitude", aircraftController.State.Latitude);
            double longitude = Get(_snapshot.Aircraft, "sim/flightmodel/position/longitude", aircraftController.State.Longitude);
            aircraftController.SetPosition(latitude, longitude, altitudeMeters, data.heading);

            AircraftRuntimeState state = aircraftController.State;
            if (state == null)
            {
                return;
            }

            state.Pitch = data.pitch;
            state.Roll = data.roll;
            state.Heading = data.heading;
            state.IndicatedAirspeedKnots = data.indicatedAirspeed;
            state.TrueAirspeedKnots = data.trueAirspeed;
            state.GroundSpeedKnots = data.groundSpeed;
            state.VerticalSpeedFpm = data.verticalSpeed;
            state.AutopilotEngaged = data.autopilotEngaged;
            state.AutopilotMode = Mathf.RoundToInt(GetAny(_snapshot.Systems, data.autopilotEngaged ? 1f : 0f,
                "sim/cockpit/autopilot/autopilot_mode",
                "sim/cockpit/autopilot/autopilot_state"));

            ApplyExternalAircraftTransform(latitude, longitude, altitudeMeters, data);
        }

        private void ApplyToHudControlStack(AviationFlightData data)
        {
            if (data != null)
            {
                ForEach(_localizerElements, element => element.SetDeviation(data.courseDeviation));
                ForEach(_glidescopeElements, element => element.SetDeviation(data.glideslopeDeviation));
                bool airspeedValid = TryGetFinite(
                    _snapshot.Aircraft,
                    "sim/flightmodel/position/indicated_airspeed",
                    out float indicatedAirspeed) &&
                    indicatedAirspeed >= 0f;
                bool altitudeValid = TryGetFinite(
                    _snapshot.Aircraft,
                    "sim/flightmodel/position/elevation",
                    out float altitudeMeters);
                ForEach(_airspeedIndicatorElements, element =>
                    element.SetAirspeedData(data.indicatedAirspeed, airspeedValid));
                ForEach(_altimeterElements, element =>
                    element.SetAltitudeData(data.altitudeMSL, altitudeValid && IsFinite(data.altitudeMSL)));
                ForEach(_torquePanelElements, element =>
                {
                    element.SetEngineCount(data.engineCount);
                    element.SetTorqueData(
                        data.engine1Torque,
                        data.engine1TorqueValid,
                        data.engine2Torque,
                        data.engine2TorqueValid);
                });
                ForEach(_nrIndicatorElements, element =>
                {
                    element.SetEngineCount(data.engineCount);
                    element.SetRPMData(
                        data.rotorNR,
                        data.rotorNRValid,
                        data.engine1NR,
                        data.engine1NRValid,
                        data.engine2NR,
                        data.engine2NRValid);
                });
            }

            if (hudController != null && aircraftController?.State != null)
            {
                hudController.InjectState(aircraftController.State);
            }
        }

        private void ClearEnginePointersWhenFeedIsStale()
        {
            if (_enginePointersClearedForStaleFeed || _lastFlightSnapshotRealtime < 0f)
            {
                return;
            }

            if (Time.realtimeSinceStartup - _lastFlightSnapshotRealtime <= Mathf.Max(0.1f, staleAfterSeconds))
            {
                return;
            }

            ClearEngineHudPointers();
            _enginePointersClearedForStaleFeed = true;
        }

        private void ClearEngineHudPointers()
        {
            ForEach(_airspeedIndicatorElements, element => element.ClearExternalData());
            ForEach(_altimeterElements, element => element.ClearExternalData());
            ForEach(_torquePanelElements, element => element.ClearExternalData());
            ForEach(_nrIndicatorElements, element => element.ClearExternalData());
        }

        private void SuppressLocalAircraftSimulation()
        {
            if (!disableUserControlWhenReceiving || aircraftController == null)
            {
                return;
            }

            if (!_suppressedAircraftControl)
            {
                _aircraftControlWasEnabledBeforeApi = aircraftController.IsEnabled;
                _suppressedAircraftControl = true;
            }
            aircraftController.SetControlEnabled(false);

            if (!_suppressedUserControl)
            {
                _userControlWasEnabledBeforeApi = aircraftController.IsUserControlled;
                _suppressedUserControl = true;
            }
            aircraftController.SetUserControlled(false);
        }

        private void ApplyExternalAircraftTransform(double latitude, double longitude, float altitudeMeters, AviationFlightData data)
        {
            if (aircraftController == null || data == null)
            {
                return;
            }

            GeoPosUnityPosProjectManager geoProjection = GeoPosUnityPosProjectManager.Instance;
            if (geoProjection != null)
            {
                float projectedAltitudeMeters = Mathf.Max(
                    altitudeMeters,
                    GeoAltitudeFromAgl(data) + Mathf.Max(0f, minimumUnityTerrainClearanceMeters));
                aircraftController.transform.position = geoProjection.GeoToUnityPosition(latitude, longitude, projectedAltitudeMeters);
            }

            aircraftController.transform.rotation = Quaternion.Euler(data.pitch, data.heading, -data.roll);
        }

        private void ApplyToTrafficRadar(AviationFlightData data)
        {
            if (trafficRadarDataManager == null)
            {
                return;
            }

            double ownLat = Get(_snapshot.Aircraft, "sim/flightmodel/position/latitude", 0d);
            double ownLon = Get(_snapshot.Aircraft, "sim/flightmodel/position/longitude", 0d);
            BuildTrafficRows(_snapshot.Traffic, ownLat, ownLon, _trafficRows);
            TrafficCount = _trafficRows.Count;

            if (_trafficRows.Count == 0)
            {
                if (allowExternalTrafficFallback)
                {
                    EnableTrafficApiFallback();
                    return;
                }

                SuppressExternalTrafficFetching();
                ClearTrafficRadarRows(ownLat, ownLon, data);
                return;
            }

            SuppressExternalTrafficFetching();

            trafficRadarDataManager.aircraftMap.Clear();
            trafficRadarDataManager.aircraftList.Clear();
            foreach (TrafficRadarDataManager.AircraftData row in _trafficRows)
            {
                trafficRadarDataManager.aircraftMap[row.icao24] = row;
                trafficRadarDataManager.aircraftList.Add(row);
            }

            if (trafficRadarController != null)
            {
                trafficRadarController.SetOwnPosition(ownLat, ownLon, data.altitudeMSL / MetersToFeet, data.heading);
            }

            trafficRadarDataManager.SetReferencePosition((float)ownLat, (float)ownLon);
            trafficRadarDataManager.onDataUpdated?.Invoke(_trafficRows);
        }

        private void MaintainTrafficApiFallback()
        {
            if (!disableTrafficApiWhenReceiving || trafficRadarDataManager == null)
            {
                return;
            }

            if (allowExternalTrafficFallback && (!IsFeedHealthy || TrafficCount == 0))
            {
                EnableTrafficApiFallback();
                return;
            }

            SuppressExternalTrafficFetching();
        }

        private void EnableTrafficApiFallback()
        {
            if (!allowExternalTrafficFallback || !disableTrafficApiWhenReceiving || trafficRadarDataManager == null)
            {
                return;
            }

            // Release the prior live-traffic suppression before starting the fallback.
            _suppressedTrafficFetching = false;
            _trafficWasFetchingBeforeApi = false;
            if (!trafficRadarDataManager.IsActive)
            {
                trafficRadarDataManager.StartFetching();
            }
        }

        private void SuppressExternalTrafficFetching()
        {
            if (!disableTrafficApiWhenReceiving || trafficRadarDataManager == null)
            {
                return;
            }

            if (!_suppressedTrafficFetching)
            {
                _trafficWasFetchingBeforeApi = trafficRadarDataManager.IsActive;
                _suppressedTrafficFetching = true;
            }

            if (trafficRadarDataManager.IsActive)
            {
                trafficRadarDataManager.StopFetching();
            }
        }

        private void ClearTrafficRadarRows(double ownLat, double ownLon, AviationFlightData data)
        {
            trafficRadarDataManager.aircraftMap.Clear();
            trafficRadarDataManager.aircraftList.Clear();
            trafficRadarDataManager.SetReferencePosition((float)ownLat, (float)ownLon);
            trafficRadarDataManager.onDataUpdated?.Invoke(_trafficRows);

            if (trafficRadarController != null)
            {
                trafficRadarController.SetOwnPosition(ownLat, ownLon, data.altitudeMSL / MetersToFeet, data.heading);
            }
        }

        private void BuildTrafficRows(
            IDictionary<string, float> traffic,
            double ownLat,
            double ownLon,
            List<TrafficRadarDataManager.AircraftData> rows)
        {
            rows.Clear();
            if (traffic == null)
            {
                return;
            }
            if (IsSyntheticSourceMode(LastSender))
            {
                return;
            }

            DateTime timestamp = DateTime.UtcNow;

            for (int i = 1; i <= 19; i++)
            {
                string prefix = $"sim/multiplayer/position/plane{i}";
                float lat = Get(traffic, prefix + "_lat");
                float lon = Get(traffic, prefix + "_lon");
                float elevationMeters = Get(traffic, prefix + "_el");
                bool hasPosition = Mathf.Abs(lat) > 0.0001f || Mathf.Abs(lon) > 0.0001f || Mathf.Abs(elevationMeters) > 1f;
                if (!hasPosition)
                {
                    continue;
                }

                float vx = Get(traffic, prefix + "_v_x");
                float vy = Get(traffic, prefix + "_v_y");
                float vz = Get(traffic, prefix + "_v_z");

                rows.Add(new TrafficRadarDataManager.AircraftData
                {
                    icao24 = $"xplmp{i:00}",
                    callsign = $"XPL-{i:00}",
                    originCountry = "X-Plane",
                    latitude = lat,
                    longitude = lon,
                    altitude = elevationMeters,
                    velocity = Mathf.Sqrt(vx * vx + vy * vy + vz * vz),
                    heading = XPlaneDataRefMapper.NormalizeHeading(Get(traffic, prefix + "_psi")),
                    verticalRate = vy,
                    onGround = elevationMeters < 5f,
                    lastUpdateTime = timestamp,
                    type = TrafficRadarDataManager.AircraftType.Unknown
                });
            }
        }

        private void ApplyToWeatherRadar(AviationFlightData data)
        {
            if (weatherRadarProvider == null)
            {
                return;
            }

            float latitude = Get(_snapshot.Aircraft, "sim/flightmodel/position/latitude");
            float longitude = Get(_snapshot.Aircraft, "sim/flightmodel/position/longitude");
            weatherRadarProvider.SetAircraftPosition(data.altitudeMSL, latitude, longitude, data.heading);
            if (weatherRadarDataProvider != null)
            {
                weatherRadarDataProvider.SetPosition(latitude, longitude, data.altitudeMSL);
                weatherRadarDataProvider.SetHeading(data.heading);
            }
            ApplyStreamWeatherTexture(data, latitude, longitude);

            if (refreshWeatherRadarTexture && Time.time - _lastWeatherRefreshTime >= Mathf.Max(1f, renderAssetPollIntervalSeconds))
            {
                _lastWeatherRefreshTime = Time.time;
                weatherRadarProvider.RefreshData();
            }
        }

        private void ApplyStreamWeatherTexture(AviationFlightData data, float latitude, float longitude)
        {
            if (!publishWeatherDatarefTextureFromStream || weatherRadarProvider == null || data == null)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            float interval = Mathf.Max(0.25f, streamWeatherTextureIntervalSeconds);
            if (_lastStreamWeatherTextureRealtime >= 0f && now - _lastStreamWeatherTextureRealtime < interval)
            {
                return;
            }

            float downloadedAge = _lastDownloadedWeatherTextureRealtime >= 0f
                ? now - _lastDownloadedWeatherTextureRealtime
                : float.PositiveInfinity;
            if (downloadedAge <= Mathf.Max(2f, renderAssetPollIntervalSeconds * 2f))
            {
                return;
            }

            StreamWeatherMetrics metrics = ReadStreamWeatherMetrics();
            Texture2D texture = BuildStreamWeatherTexture(data, metrics);
            if (texture == null)
            {
                return;
            }

            _lastStreamWeatherTextureRealtime = now;
            if (weatherRadarProvider is XPlaneOriginalWeatherRadarProvider originalProvider)
            {
                originalProvider.PublishTexture(texture, BuildStreamWeatherStatus(metrics));
                return;
            }

            MethodInfo method = weatherRadarProvider.GetType().GetMethod(
                "SimulateDataReceived",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(Texture2D) },
                null);
            method?.Invoke(weatherRadarProvider, new object[] { texture });
        }

        private Texture2D BuildStreamWeatherTexture(AviationFlightData data, StreamWeatherMetrics metrics)
        {
            int size = Mathf.Clamp(streamWeatherTextureSize, 128, 1024);
            if (_streamWeatherTexture == null || _streamWeatherTexture.width != size || _streamWeatherTexture.height != size)
            {
                DestroyTexture(ref _streamWeatherTexture);
                _streamWeatherTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "FAAProceduralWeatherRadar",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                _streamWeatherPixels = new Color32[size * size];
            }
            else if (_streamWeatherPixels == null || _streamWeatherPixels.Length != size * size)
            {
                _streamWeatherPixels = new Color32[size * size];
            }

            DrawModernWeatherRadar(_streamWeatherPixels, size, data, metrics);

            _streamWeatherTexture.SetPixels32(_streamWeatherPixels);
            _streamWeatherTexture.Apply(false);
            return _streamWeatherTexture;
        }

        private StreamWeatherMetrics ReadStreamWeatherMetrics()
        {
            float precipitation = GetNormalizedWeatherValue(
                "sim/weather/aircraft/precipitation_on_aircraft_ratio",
                "sim/weather/precipitation_on_aircraft_ratio",
                "sim/weather/region/rain_percent",
                "sim/weather/rain_percent");
            float cloudCoverage = Mathf.Max(GetCloudCoverage(0), GetCloudCoverage(1), GetCloudCoverage(2));
            float turbulence = GetMaxWeatherValue("sim/weather/region/turbulence", 13);
            float visibilityMeters = GetAny(_snapshot.Weather, 0f,
                "sim/weather/visibility_reported_m",
                "sim/weather/aircraft/visibility_reported_m");
            float visibilitySm = GetAny(_snapshot.Weather, 0f,
                "sim/weather/region/visibility_reported_sm",
                "sim/weather/aircraft/visibility_reported_sm");
            if (visibilityMeters <= 0f && visibilitySm > 0f)
            {
                visibilityMeters = visibilitySm * 1609.344f;
            }

            float cloudBaseMeters = GetAny(_snapshot.Weather, 0f,
                "sim/weather/region/cloud_base_msl_m[0]",
                "sim/weather/cloud_base_msl_m[0]",
                "sim/weather/aircraft/cloud_base_msl_m",
                "sim/weather/cloud_base_m");

            return new StreamWeatherMetrics
            {
                Precipitation = precipitation,
                CloudCoverage = cloudCoverage,
                Turbulence = turbulence,
                WindDirection = GetWeatherWindDirection(_snapshot.Weather),
                WindSpeed = GetWindSpeed(_snapshot.Weather),
                VisibilityMeters = visibilityMeters,
                CloudBaseMeters = cloudBaseMeters,
                TemperatureC = GetAny(_snapshot.Weather, 0f,
                    "sim/weather/temperature_ambient_c",
                    "sim/weather/aircraft/temperature_ambient_deg_c"),
                Intensity = Mathf.Clamp01(Mathf.Max(precipitation, cloudCoverage * 0.65f, turbulence * 0.7f))
            };
        }

        private string BuildStreamWeatherStatus(StreamWeatherMetrics metrics)
        {
            float visibilitySm = metrics.VisibilityMeters > 0f ? metrics.VisibilityMeters / 1609.344f : 0f;
            if (metrics.Precipitation > 0.02f || visibilitySm > 0f)
            {
                return $"RAIN {metrics.Precipitation * 100f:0} VIS {visibilitySm:0.0}SM";
            }

            return $"CLD {metrics.CloudCoverage * 100f:0} TURB {metrics.Turbulence * 100f:0}";
        }

        private float GetNormalizedWeatherValue(params string[] keys)
        {
            float value = GetAny(_snapshot.Weather, 0f, keys);
            if (value > 1f)
            {
                value /= 100f;
            }
            return Mathf.Clamp01(value);
        }

        private float GetMaxWeatherValue(string keyPrefix, int count)
        {
            float max = 0f;
            for (int i = 0; i < count; i++)
            {
                max = Mathf.Max(max, Get(_snapshot.Weather, $"{keyPrefix}[{i}]"));
            }
            return Mathf.Clamp01(max > 1f ? max / 100f : max);
        }

        private float GetCloudCoverage(int layer)
        {
            float coverage = GetAny(_snapshot.Weather, float.NaN,
                $"sim/weather/region/cloud_coverage_percent[{layer}]",
                $"sim/weather/cloud_coverage_percent[{layer}]");
            if (!float.IsNaN(coverage))
            {
                return NormalizeCoverageValue(coverage);
            }

            coverage = GetAny(_snapshot.Weather, float.NaN,
                $"sim/weather/cloud_coverage[{layer}]",
                $"sim/weather/region/cloud_coverage[{layer}]");
            return float.IsNaN(coverage) ? 0f : NormalizeCoverageValue(coverage);
        }

        private static float NormalizeCoverageValue(float value)
        {
            if (value <= 1f)
            {
                return Mathf.Clamp01(value);
            }
            if (value <= 4f)
            {
                return Mathf.Clamp01(value / 4f);
            }
            if (value <= 8f)
            {
                return Mathf.Clamp01(value / 8f);
            }

            return Mathf.Clamp01(value / 100f);
        }

        private static void DrawModernWeatherRadar(Color32[] pixels, int size, AviationFlightData data, StreamWeatherMetrics metrics)
        {
            int originX = size / 2;
            int originY = Mathf.RoundToInt(size * 0.12f);
            float maxRadius = size * 0.90f;
            float halfAngleDegrees = 62f;

            DrawModernRadarBackdrop(pixels, size, originX, originY, maxRadius, halfAngleDegrees);
            DrawModernWeatherReturns(pixels, size, originX, originY, maxRadius, halfAngleDegrees, data, metrics);
            DrawModernRadarGrid(pixels, size, originX, originY, maxRadius, halfAngleDegrees, data, metrics);
        }

        private static void DrawModernRadarBackdrop(Color32[] pixels, int size, int originX, int originY, float maxRadius, float halfAngleDegrees)
        {
            for (int y = 0; y < size; y++)
            {
                float vertical = y / Mathf.Max(1f, size - 1f);
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    float rangeNorm;
                    float angleDegrees;
                    bool insideSector = TryGetRadarSectorCoordinates(
                        x,
                        y,
                        originX,
                        originY,
                        maxRadius,
                        halfAngleDegrees,
                        out rangeNorm,
                        out angleDegrees);

                    float dx = (x - size * 0.5f) / size;
                    float dy = (y - size * 0.55f) / size;
                    float vignette = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 1.9f);
                    byte blue = (byte)Mathf.RoundToInt(Mathf.Lerp(6f, 22f, vignette * 0.62f + vertical * 0.12f));
                    byte green = (byte)Mathf.RoundToInt(Mathf.Lerp(1f, 7f, vignette));
                    byte red = (byte)Mathf.RoundToInt(Mathf.Lerp(0f, 3f, vignette));

                    if (insideSector)
                    {
                        float fade = Mathf.Clamp01(1f - Mathf.Abs(angleDegrees) / halfAngleDegrees);
                        blue = (byte)Mathf.Min(255, blue + Mathf.RoundToInt(Mathf.Lerp(2f, 9f, fade) * (1f - rangeNorm * 0.45f)));
                        green = (byte)Mathf.Min(255, green + Mathf.RoundToInt(Mathf.Lerp(1f, 5f, fade)));
                    }

                    pixels[row + x] = new Color32(red, green, blue, 255);
                }
            }
        }

        private static void DrawModernWeatherReturns(
            Color32[] pixels,
            int size,
            int originX,
            int originY,
            float maxRadius,
            float halfAngleDegrees,
            AviationFlightData data,
            StreamWeatherMetrics metrics)
        {
            float intensity = Mathf.Clamp01(Mathf.Max(metrics.Intensity, metrics.Precipitation * 0.95f, metrics.CloudCoverage * 0.55f));
            if (intensity <= 0.025f)
            {
                return;
            }

            float heading = data != null ? data.heading : 0f;
            float seed = Mathf.Repeat(heading * 0.73f + metrics.WindDirection * 1.37f + metrics.WindSpeed * 0.91f, 997f);
            float stormCenter = Mathf.Lerp(0.40f, 0.70f, Mathf.Clamp01(metrics.CloudCoverage * 0.65f + metrics.Precipitation * 0.25f));
            float threshold = Mathf.Lerp(0.80f, 0.62f, intensity);
            float windDrift = Mathf.Clamp(Mathf.DeltaAngle(heading, metrics.WindDirection) / Mathf.Max(1f, halfAngleDegrees), -1f, 1f);

            int minY = Mathf.Clamp(originY, 0, size - 1);
            for (int y = minY; y < size; y++)
            {
                float ny = y / (float)size;
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    float rangeNorm;
                    float angleDegrees;
                    if (!TryGetRadarSectorCoordinates(x, y, originX, originY, maxRadius, halfAngleDegrees, out rangeNorm, out angleDegrees))
                    {
                        continue;
                    }

                    float nx = x / (float)size;
                    float angleNorm = angleDegrees / halfAngleDegrees;
                    float broad = Mathf.PerlinNoise(nx * 4.7f + seed * 0.011f, ny * 4.7f - seed * 0.017f);
                    float cell = Mathf.PerlinNoise(nx * 17.5f + seed * 0.037f, ny * 17.5f + seed * 0.019f);
                    float fine = Mathf.PerlinNoise(nx * 58f - seed * 0.023f, ny * 58f + seed * 0.041f);
                    float streak = Mathf.PerlinNoise((rangeNorm + seed * 0.003f) * 13f, (angleNorm + windDrift * 0.35f) * 4.8f);
                    float radialBand = Mathf.Clamp01(1f - Mathf.Abs(rangeNorm - stormCenter) / 0.30f);
                    float windSide = Mathf.Clamp01(0.5f + (angleNorm + windDrift * 0.45f) * 0.5f);
                    float sectorFill = Mathf.Lerp(0.66f, 1.04f, windSide) * Mathf.Clamp01(1f - rangeNorm * 0.18f);
                    float broadMask = Mathf.SmoothStep(0.38f, 0.76f, broad);
                    float cellMask = Mathf.SmoothStep(0.44f, 0.78f, cell);
                    float filamentMask = Mathf.SmoothStep(0.50f, 0.84f, streak);
                    float nearFieldClear = Mathf.SmoothStep(0.07f, 0.20f, rangeNorm);
                    float textureMask = broadMask
                        * Mathf.Lerp(0.24f, 1f, cellMask)
                        * Mathf.Lerp(0.52f, 1.08f, filamentMask);
                    float raw = textureMask * sectorFill;
                    raw += radialBand * Mathf.Lerp(0.04f, 0.28f, intensity) * Mathf.Lerp(0.32f, 1f, cellMask);
                    raw += metrics.Turbulence * Mathf.Max(0f, fine - 0.58f) * 0.14f;
                    raw *= nearFieldClear * Mathf.Lerp(0.70f, 1.34f, intensity);

                    if (raw < threshold || fine < 0.18f || (fine > 0.92f && raw < 0.96f))
                    {
                        continue;
                    }

                    float strength = Mathf.Clamp01((raw - threshold) / Mathf.Max(0.001f, 1.30f - threshold));
                    Color32 color = ModernWeatherReturnColor(strength, metrics.Precipitation);
                    pixels[row + x] = BlendRadarReturn(pixels[row + x], color);
                }
            }
        }

        private static void DrawModernRadarGrid(
            Color32[] pixels,
            int size,
            int originX,
            int originY,
            float maxRadius,
            float halfAngleDegrees,
            AviationFlightData data,
            StreamWeatherMetrics metrics)
        {
            Color32 white = new Color32(235, 245, 255, 225);
            Color32 whiteDim = new Color32(190, 216, 236, 128);
            Color32 cyan = new Color32(48, 226, 255, 190);
            Color32 green = new Color32(62, 255, 94, 210);
            Color32 magenta = new Color32(255, 62, 238, 220);

            int ringThickness = Mathf.Max(1, size / 220);
            DrawRadarArc(pixels, size, originX, originY, Mathf.RoundToInt(maxRadius * 0.25f), -halfAngleDegrees, halfAngleDegrees, white, ringThickness);
            DrawRadarArc(pixels, size, originX, originY, Mathf.RoundToInt(maxRadius * 0.45f), -halfAngleDegrees, halfAngleDegrees, white, ringThickness);
            DrawRadarArc(pixels, size, originX, originY, Mathf.RoundToInt(maxRadius * 0.68f), -halfAngleDegrees, halfAngleDegrees, white, ringThickness);
            DrawRadarArc(pixels, size, originX, originY, Mathf.RoundToInt(maxRadius * 0.92f), -halfAngleDegrees, halfAngleDegrees, white, ringThickness + 1);

            DrawRadarBearingSegment(pixels, size, originX, originY, 0f, size * 0.10f, maxRadius * 0.95f, white, ringThickness);
            DrawRadarBearingSegment(pixels, size, originX, originY, -halfAngleDegrees, maxRadius * 0.22f, maxRadius * 0.92f, whiteDim, 1);
            DrawRadarBearingSegment(pixels, size, originX, originY, halfAngleDegrees, maxRadius * 0.22f, maxRadius * 0.92f, whiteDim, 1);

            for (int a = -60; a <= 60; a += 5)
            {
                bool major = a % 10 == 0;
                float inner = maxRadius * (major ? 0.86f : 0.89f);
                float outer = maxRadius * 0.92f;
                DrawRadarBearingSegment(pixels, size, originX, originY, a, inner, outer, major ? white : whiteDim, 1);
            }

            DrawRadarBearingSegment(pixels, size, originX, originY, 0f, size * 0.19f, maxRadius * 0.88f, magenta, 1);
            DrawRadarDiamond(pixels, size, originX, originY + Mathf.RoundToInt(maxRadius * 0.45f), Mathf.Max(4, size / 80), magenta);
            DrawOwnshipTriangle(pixels, size, originX, originY + Mathf.RoundToInt(size * 0.035f), Mathf.Max(12, size / 28), white);

            int labelScale = Mathf.Max(2, size / 180);
            DrawDigitString(pixels, size, "20", originX - labelScale * 8, originY + Mathf.RoundToInt(maxRadius * 0.45f) + labelScale * 4, labelScale, white);
            DrawDigitString(pixels, size, "30", originX + Mathf.RoundToInt(size * 0.18f), originY + Mathf.RoundToInt(maxRadius * 0.68f) - labelScale * 2, labelScale, white);

            int heading = Mathf.RoundToInt(Mathf.Repeat(data != null ? data.heading : 0f, 360f));
            DrawDigitString(pixels, size, heading.ToString("000", CultureInfo.InvariantCulture), originX - labelScale * 8, size - labelScale * 16, labelScale, white);

            int metricScale = Mathf.Max(1, size / 260);
            DrawDigitString(pixels, size, Mathf.RoundToInt(metrics.Precipitation * 100f).ToString("00", CultureInfo.InvariantCulture), Mathf.RoundToInt(size * 0.08f), Mathf.RoundToInt(size * 0.91f), metricScale, green);
            DrawDigitString(pixels, size, Mathf.RoundToInt(metrics.WindSpeed).ToString("000", CultureInfo.InvariantCulture), Mathf.RoundToInt(size * 0.80f), Mathf.RoundToInt(size * 0.91f), metricScale, cyan);
        }

        private static bool TryGetRadarSectorCoordinates(
            int x,
            int y,
            int originX,
            int originY,
            float maxRadius,
            float halfAngleDegrees,
            out float rangeNorm,
            out float angleDegrees)
        {
            float dx = x - originX;
            float dy = y - originY;
            float range = Mathf.Sqrt(dx * dx + dy * dy);
            rangeNorm = maxRadius > 0f ? range / maxRadius : 1f;
            angleDegrees = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
            return dy >= 0f && range <= maxRadius && Mathf.Abs(angleDegrees) <= halfAngleDegrees;
        }

        private static void DrawRadarArc(Color32[] pixels, int size, int cx, int cy, int radius, float startDegrees, float endDegrees, Color32 color, int thickness)
        {
            int steps = Mathf.Max(16, Mathf.RoundToInt(radius * Mathf.Abs(endDegrees - startDegrees) * Mathf.Deg2Rad * 1.5f));
            int halfThickness = Mathf.Max(0, thickness / 2);
            for (int t = -halfThickness; t <= halfThickness; t++)
            {
                int r = Mathf.Max(0, radius + t);
                for (int i = 0; i <= steps; i++)
                {
                    float angle = Mathf.Lerp(startDegrees, endDegrees, i / (float)steps) * Mathf.Deg2Rad;
                    SetPixel(
                        pixels,
                        size,
                        Mathf.RoundToInt(cx + Mathf.Sin(angle) * r),
                        Mathf.RoundToInt(cy + Mathf.Cos(angle) * r),
                        color);
                }
            }
        }

        private static void DrawRadarBearingSegment(Color32[] pixels, int size, int cx, int cy, float angleDegrees, float startRadius, float endRadius, Color32 color, int thickness)
        {
            float angle = angleDegrees * Mathf.Deg2Rad;
            int x0 = Mathf.RoundToInt(cx + Mathf.Sin(angle) * startRadius);
            int y0 = Mathf.RoundToInt(cy + Mathf.Cos(angle) * startRadius);
            int x1 = Mathf.RoundToInt(cx + Mathf.Sin(angle) * endRadius);
            int y1 = Mathf.RoundToInt(cy + Mathf.Cos(angle) * endRadius);
            DrawThickLine(pixels, size, x0, y0, x1, y1, color, thickness);
        }

        private static void DrawOwnshipTriangle(Color32[] pixels, int size, int cx, int cy, int height, Color32 color)
        {
            int halfWidth = Mathf.Max(6, height / 2);
            int topY = cy + height / 2;
            int bottomY = cy - height / 2;
            DrawThickLine(pixels, size, cx, topY, cx - halfWidth, bottomY, color, 2);
            DrawThickLine(pixels, size, cx, topY, cx + halfWidth, bottomY, color, 2);
            DrawThickLine(pixels, size, cx - halfWidth, bottomY, cx + halfWidth, bottomY, color, 2);
            DrawLine(pixels, size, cx, bottomY - Mathf.Max(2, height / 8), cx, bottomY - Mathf.Max(8, height / 3), color);
        }

        private static void DrawRadarDiamond(Color32[] pixels, int size, int cx, int cy, int radius, Color32 color)
        {
            DrawThickLine(pixels, size, cx, cy + radius, cx + radius, cy, color, 1);
            DrawThickLine(pixels, size, cx + radius, cy, cx, cy - radius, color, 1);
            DrawThickLine(pixels, size, cx, cy - radius, cx - radius, cy, color, 1);
            DrawThickLine(pixels, size, cx - radius, cy, cx, cy + radius, color, 1);
        }

        private static Color32 ModernWeatherReturnColor(float strength, float precipitation)
        {
            strength = Mathf.Clamp01(strength);
            Color32 color;
            if (strength > 0.92f || precipitation > 0.92f && strength > 0.84f)
            {
                color = new Color32(246, 220, 62, 205);
            }
            else if (strength > 0.58f)
            {
                color = new Color32(116, 246, 58, 174);
            }
            else
            {
                color = new Color32(18, 216, 70, 146);
            }

            color.a = (byte)Mathf.RoundToInt(Mathf.Lerp(70f, color.a, strength));
            return color;
        }

        private static Color32 BlendRadarReturn(Color32 baseColor, Color32 overlay)
        {
            float alpha = overlay.a / 255f;
            byte r = (byte)Mathf.RoundToInt(Mathf.Lerp(baseColor.r, overlay.r, alpha));
            byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(baseColor.g, overlay.g, alpha));
            byte b = (byte)Mathf.RoundToInt(Mathf.Lerp(baseColor.b, overlay.b, alpha));
            return new Color32(r, g, b, 255);
        }

        private static void DrawDigitString(Color32[] pixels, int size, string value, int x, int y, int scale, Color32 color)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            int cursor = x;
            int step = Mathf.Max(1, scale) * 4;
            foreach (char character in value)
            {
                if (character == ' ')
                {
                    cursor += step;
                    continue;
                }

                DrawDigit(pixels, size, character, cursor, y, Mathf.Max(1, scale), color);
                cursor += step;
            }
        }

        private static void DrawDigit(Color32[] pixels, int size, char character, int x, int y, int scale, Color32 color)
        {
            string[] pattern = GetDigitPattern(character);
            if (pattern == null)
            {
                return;
            }

            for (int row = 0; row < pattern.Length; row++)
            {
                string line = pattern[row];
                for (int column = 0; column < line.Length; column++)
                {
                    if (line[column] != '1')
                    {
                        continue;
                    }

                    DrawFilledRect(
                        pixels,
                        size,
                        x + column * scale,
                        y + (pattern.Length - 1 - row) * scale,
                        scale,
                        scale,
                        color);
                }
            }
        }

        private static string[] GetDigitPattern(char character)
        {
            switch (character)
            {
                case '0': return new[] { "111", "101", "101", "101", "111" };
                case '1': return new[] { "010", "110", "010", "010", "111" };
                case '2': return new[] { "111", "001", "111", "100", "111" };
                case '3': return new[] { "111", "001", "111", "001", "111" };
                case '4': return new[] { "101", "101", "111", "001", "001" };
                case '5': return new[] { "111", "100", "111", "001", "111" };
                case '6': return new[] { "111", "100", "111", "101", "111" };
                case '7': return new[] { "111", "001", "010", "010", "010" };
                case '8': return new[] { "111", "101", "111", "101", "111" };
                case '9': return new[] { "111", "101", "111", "001", "111" };
                default: return null;
            }
        }

        private static void DrawStreamRadarGrid(Color32[] pixels, int size)
        {
            int center = size / 2;
            Color32 ring = new Color32(42, 170, 70, 70);
            Color32 axis = new Color32(58, 220, 86, 90);
            for (int r = 1; r <= 4; r++)
            {
                DrawCircle(pixels, size, center, center, Mathf.RoundToInt(center * r / 4f), ring);
            }
            DrawLine(pixels, size, center, 10, center, size - 11, axis);
            DrawLine(pixels, size, 10, center, size - 11, center, axis);
        }

        private static void DrawLiveWeatherTelemetry(Color32[] pixels, int size, StreamWeatherMetrics metrics)
        {
            float visibilitySeverity = metrics.VisibilityMeters > 0f
                ? 1f - Mathf.Clamp01(metrics.VisibilityMeters / 16093.44f)
                : 0f;
            float cloudBaseSeverity = metrics.CloudBaseMeters > 0f
                ? 1f - Mathf.Clamp01(metrics.CloudBaseMeters / 3658f)
                : 0f;

            DrawSeverityHalo(pixels, size, metrics.Intensity);
            DrawCompactMetricGauges(
                pixels,
                size,
                metrics.Precipitation,
                metrics.CloudCoverage,
                metrics.Turbulence,
                visibilitySeverity,
                cloudBaseSeverity);
        }

        private static void DrawWindVector(Color32[] pixels, int size, float relativeWindDirection, float windSpeedKnots)
        {
            int center = size / 2;
            float length = Mathf.Lerp(size * 0.10f, size * 0.35f, Mathf.Clamp01(windSpeedKnots / 120f));
            float angle = relativeWindDirection * Mathf.Deg2Rad;
            int endX = Mathf.RoundToInt(center + Mathf.Sin(angle) * length);
            int endY = Mathf.RoundToInt(center + Mathf.Cos(angle) * length);
            Color32 windColor = new Color32(84, 255, 144, 170);
            DrawLine(pixels, size, center, center, endX, endY, windColor);
            DrawCircle(pixels, size, endX, endY, Mathf.Max(4, size / 80), windColor);
        }

        private static void DrawSeverityHalo(Color32[] pixels, int size, float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            if (intensity <= 0.03f)
            {
                return;
            }

            int center = size / 2;
            int radius = Mathf.RoundToInt(size * 0.365f);
            Color32 color = WeatherReturnColor(intensity);
            color.a = (byte)Mathf.RoundToInt(Mathf.Lerp(44f, 120f, intensity));

            int dashCount = 48;
            float dashLength = Mathf.Lerp(0.025f, 0.055f, intensity) * Mathf.PI * 2f;
            for (int i = 0; i < dashCount; i++)
            {
                if ((i & 1) == 1)
                {
                    continue;
                }

                float start = (i / (float)dashCount) * Mathf.PI * 2f;
                float end = start + dashLength;
                DrawArc(pixels, size, center, center, radius, start, end, color);
                DrawArc(pixels, size, center, center, radius + Mathf.Max(2, size / 160), start, end, color);
            }
        }

        private static void DrawCompactMetricGauges(
            Color32[] pixels,
            int size,
            float precipitation,
            float cloudCoverage,
            float turbulence,
            float visibilitySeverity,
            float cloudBaseSeverity)
        {
            int gaugeCount = 5;
            int margin = Mathf.Max(18, size / 22);
            int gaugeWidth = Mathf.Max(4, size / 96);
            int gaugeHeight = Mathf.Max(24, size / 18);
            int spacing = Mathf.Max(6, size / 56);
            int totalWidth = gaugeCount * gaugeWidth + (gaugeCount - 1) * spacing;
            int startX = margin;
            int baseY = margin;
            if (startX + totalWidth > size / 2)
            {
                startX = Mathf.Max(10, size / 2 - totalWidth - margin / 2);
            }

            DrawSmallGauge(pixels, size, startX, baseY, gaugeWidth, gaugeHeight, precipitation);
            DrawSmallGauge(pixels, size, startX + (gaugeWidth + spacing), baseY, gaugeWidth, gaugeHeight, cloudCoverage);
            DrawSmallGauge(pixels, size, startX + (gaugeWidth + spacing) * 2, baseY, gaugeWidth, gaugeHeight, turbulence);
            DrawSmallGauge(pixels, size, startX + (gaugeWidth + spacing) * 3, baseY, gaugeWidth, gaugeHeight, visibilitySeverity);
            DrawSmallGauge(pixels, size, startX + (gaugeWidth + spacing) * 4, baseY, gaugeWidth, gaugeHeight, cloudBaseSeverity);
        }

        private static void DrawSmallGauge(Color32[] pixels, int size, int x, int y, int width, int height, float value)
        {
            value = Mathf.Clamp01(value);
            DrawFilledRect(pixels, size, x, y, width, height, new Color32(8, 36, 14, 62));
            DrawLine(pixels, size, x, y, x + width - 1, y, new Color32(62, 230, 78, 72));
            DrawLine(pixels, size, x, y + height - 1, x + width - 1, y + height - 1, new Color32(62, 230, 78, 72));

            int fillHeight = Mathf.RoundToInt(value * height);
            if (fillHeight <= 0)
            {
                return;
            }

            Color32 fill = WeatherReturnColor(value);
            fill.a = (byte)Mathf.RoundToInt(Mathf.Lerp(95f, 165f, value));
            DrawFilledRect(pixels, size, x, y, width, fillHeight, fill);
        }

        private static void DrawArc(Color32[] pixels, int size, int cx, int cy, int radius, float startRadians, float endRadians, Color32 color)
        {
            int steps = Mathf.Max(4, Mathf.RoundToInt(radius * Mathf.Abs(endRadians - startRadians)));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float angle = Mathf.Lerp(startRadians, endRadians, t);
                SetPixel(pixels, size, Mathf.RoundToInt(cx + Mathf.Cos(angle) * radius), Mathf.RoundToInt(cy + Mathf.Sin(angle) * radius), color);
            }
        }

        private static void DrawFilledRect(Color32[] pixels, int size, int x, int y, int width, int height, Color32 color)
        {
            int minX = Mathf.Clamp(x, 0, size - 1);
            int maxX = Mathf.Clamp(x + width - 1, 0, size - 1);
            int minY = Mathf.Clamp(y, 0, size - 1);
            int maxY = Mathf.Clamp(y + height - 1, 0, size - 1);
            for (int py = minY; py <= maxY; py++)
            {
                int row = py * size;
                for (int px = minX; px <= maxX; px++)
                {
                    pixels[row + px] = BlendAdditive(pixels[row + px], color);
                }
            }
        }

        private static Color32 WeatherReturnColor(float strength)
        {
            if (strength > 0.78f)
            {
                return new Color32(255, 174, 36, 220);
            }
            if (strength > 0.48f)
            {
                return new Color32(210, 232, 58, 205);
            }
            return new Color32(38, 220, 64, (byte)Mathf.RoundToInt(Mathf.Lerp(90f, 185f, strength)));
        }

        private static Color32 BlendAdditive(Color32 baseColor, Color32 overlay)
        {
            byte r = (byte)Mathf.Min(255, baseColor.r + overlay.r * overlay.a / 255);
            byte g = (byte)Mathf.Min(255, baseColor.g + overlay.g * overlay.a / 255);
            byte b = (byte)Mathf.Min(255, baseColor.b + overlay.b * overlay.a / 255);
            return new Color32(r, g, b, 255);
        }

        private static void DrawCircle(Color32[] pixels, int size, int cx, int cy, int radius, Color32 color)
        {
            int steps = Mathf.Max(48, radius * 8);
            for (int i = 0; i < steps; i++)
            {
                float angle = (i / (float)steps) * Mathf.PI * 2f;
                SetPixel(pixels, size, Mathf.RoundToInt(cx + Mathf.Cos(angle) * radius), Mathf.RoundToInt(cy + Mathf.Sin(angle) * radius), color);
            }
        }

        private static void DrawLine(Color32[] pixels, int size, int x0, int y0, int x1, int y1, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int steps = Mathf.Max(dx, dy);
            if (steps == 0)
            {
                SetPixel(pixels, size, x0, y0, color);
                return;
            }

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                SetPixel(pixels, size, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), color);
            }
        }

        private static void DrawThickLine(Color32[] pixels, int size, int x0, int y0, int x1, int y1, Color32 color, int thickness)
        {
            int radius = Mathf.Max(0, thickness / 2);
            if (radius == 0)
            {
                DrawLine(pixels, size, x0, y0, x1, y1, color);
                return;
            }

            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    if (offsetX * offsetX + offsetY * offsetY > radius * radius)
                    {
                        continue;
                    }

                    DrawLine(pixels, size, x0 + offsetX, y0 + offsetY, x1 + offsetX, y1 + offsetY, color);
                }
            }
        }

        private static void SetPixel(Color32[] pixels, int size, int x, int y, Color32 color)
        {
            if (x < 0 || x >= size || y < 0 || y >= size)
            {
                return;
            }

            pixels[y * size + x] = BlendAdditive(pixels[y * size + x], color);
        }

        private void ApplyWeatherRadarPowerStateToDisplay()
        {
            bool hasWeatherOn = TryGetAny(_snapshot.Systems, out float weatherOn,
                "sim/cockpit2/EFIS/EFIS_weather_on",
                "sim/cockpit2/EFIS/EFIS_weather_on_copilot");

            _hasWeatherRadarPowerState = hasWeatherOn;
            bool copilotRadarOn = Get(_snapshot.Systems, "sim/cockpit2/EFIS/EFIS_weather_on_copilot") > 0.5f;
            bool hasFreshOriginalTexture = xPlaneWeatherRadarDisplay != null && xPlaneWeatherRadarDisplay.HasUsableTexture;
            _isWeatherRadarPowered = !hasWeatherOn || weatherOn > 0.5f || copilotRadarOn ||
                (treatFreshWeatherTextureAsRadarOn && hasFreshOriginalTexture);
            _weatherRadarMode = TryGetAny(_snapshot.Systems, out float mode,
                "sim/cockpit2/EFIS/EFIS_weather_mode",
                "sim/cockpit2/EFIS/EFIS_weather_mode_copilot")
                ? Mathf.RoundToInt(mode)
                : -1;

            if (xPlaneWeatherRadarDisplay != null)
            {
                xPlaneWeatherRadarDisplay.SetRadarPowerState(
                    _hasWeatherRadarPowerState,
                    _isWeatherRadarPowered,
                    _weatherRadarMode);
            }

            if (weatherRadarProvider != null && _hasWeatherRadarPowerState)
            {
                if (_isWeatherRadarPowered && weatherRadarProvider.Status == ProviderStatus.Inactive)
                {
                    weatherRadarProvider.Activate();
                }
                else if (!_isWeatherRadarPowered && weatherRadarProvider.Status != ProviderStatus.Inactive && !hasFreshOriginalTexture)
                {
                    weatherRadarProvider.Deactivate();
                }
            }

            if (weatherRadarDataProvider != null && _hasWeatherRadarPowerState)
            {
                RadarMode currentMode = weatherRadarDataProvider.RadarData.currentMode;
                if (!_isWeatherRadarPowered && currentMode != RadarMode.STBY)
                {
                    weatherRadarDataProvider.SetMode(RadarMode.STBY);
                }
                else if (_isWeatherRadarPowered && currentMode == RadarMode.STBY)
                {
                    weatherRadarDataProvider.SetMode(RadarMode.WX);
                }
            }
        }

        private void ApplyToLegacyHud(AviationFlightData data)
        {
            float altitudeMeters = Get(_snapshot.Aircraft, "sim/flightmodel/position/elevation");
            float aglMeters = Get(_snapshot.Aircraft, "sim/flightmodel/position/y_agl");
            float relativeFlightPathPitch = data.flightPathAngle - data.pitch;
            float relativeTrack = XPlaneDataRefMapper.NormalizeAngle(data.track - data.heading);

            ForEach(_airspeedHuds, hud => hud.UpdateAirspeed(data.indicatedAirspeed));
            ForEach(_altitudeHuds, hud => hud.UpdateAltitude(data.altitudeMSL));
            ForEach(_verticalSpeedHuds, hud => hud.UpdateVerticalSpeed(data.verticalSpeed, data.altitudeAGL));
            ForEach(_headingHuds, hud =>
            {
                hud.allowManualUpdate = true;
                hud.useTransformRotation = false;
                hud.UpdateHeading(data.heading, true);
            });
            ForEach(_windIndicators, hud => hud.UpdateWind(data.windDirection, data.heading, data.windSpeed));
            ForEach(_attitudeHuds, hud =>
            {
                hud.UpdatePitch(data.pitch);
                hud.UpdateRoll(data.roll);
            });
            ForEach(_attitudeHudNews, hud => hud.UpdatePitch(data.pitch));
            ForEach(_flightPathVectors, hud => hud.UpdateFPV(relativeFlightPathPitch, relativeTrack, data.indicatedAirspeed));
            ForEach(_slipSkidHuds, hud => hud.UpdateSlip(data.slipSkid));
            ForEach(_altitudeAglDisplays, hud => hud.UpdateText(altitudeMeters, aglMeters));
            ForEach(_courseDeviationHuds, hud => hud.UpdateDeviation(0, data.courseDeviation, data.courseDeviation, data.courseDeviation));
            ForEach(_glideslopeHuds, hud => hud.UpdateGlideslope(data.glideslopeDeviation));
        }

        private IEnumerator RenderAssetLoop()
        {
            while (enabled)
            {
                // The weather panel is fed by ApplyStreamWeatherTexture from
                // the live snapshot. Native X-Plane raster downloads remain an
                // explicit fallback only and are disabled for this scene.
                if (ShouldBridgeDownloadWeatherTexture() && !publishWeatherDatarefTextureFromStream)
                {
                    yield return DownloadTexture(GetWeatherTexturePath(), texture =>
                    {
                        ReplaceTexture(ref _latestWeatherTexture, texture);
                        _lastDownloadedWeatherTextureRealtime = Time.realtimeSinceStartup;
                        if (weatherImageTarget != null)
                        {
                            weatherImageTarget.texture = _latestWeatherTexture;
                            weatherImageTarget.color = Color.white;
                            weatherImageTarget.enabled = true;
                            weatherImageTarget.raycastTarget = false;
                        }
                        if (xPlaneWeatherRadarDisplay != null)
                        {
                            xPlaneWeatherRadarDisplay.ShowTexture(_latestWeatherTexture);
                        }
                        foreach (XPlaneOriginalWeatherRadarDisplay display in
                                 FindObjectsByType<XPlaneOriginalWeatherRadarDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        {
                            if (display != null && display != xPlaneWeatherRadarDisplay)
                            {
                                display.ShowTexture(_latestWeatherTexture);
                            }
                        }
                        ApplyTextureToWeatherProvider(_latestWeatherTexture);
                    });
                }

                if (HasTrafficTextureConsumer())
                {
                    yield return DownloadTexture(GetTrafficTexturePath(), texture =>
                    {
                        ReplaceTexture(ref _latestTrafficTexture, texture);
                        ApplyTrafficTexture(_latestTrafficTexture);
                    });
                }

                yield return RequestJson("v1/render/gauges.json", json =>
                {
                    _snapshot.GaugeManifest = json.ToString(Newtonsoft.Json.Formatting.None);
                }, suppressFailureState: true);

                yield return new WaitForSeconds(Mathf.Max(0.5f, renderAssetPollIntervalSeconds));
            }
        }

        private void ApplyTrafficTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (!IsUsableTrafficTextureTarget(trafficImageTarget))
            {
                TryBindTrafficTextureTarget();
            }

            if (IsUsableTrafficTextureTarget(trafficImageTarget))
            {
                trafficImageTarget.texture = texture;
                trafficImageTarget.color = Color.white;
                trafficImageTarget.enabled = true;
                trafficImageTarget.raycastTarget = false;
            }

            if (xPlaneTrafficRadarDisplay != null && xPlaneTrafficRadarDisplay.UsesXPlaneTrafficTexture)
            {
                xPlaneTrafficRadarDisplay.ShowXPlaneTrafficTexture(texture);
            }

            foreach (TrafficRadarDisplay display in
                     FindObjectsByType<TrafficRadarDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (display != null && display != xPlaneTrafficRadarDisplay && display.UsesXPlaneTrafficTexture)
                {
                    display.ShowXPlaneTrafficTexture(texture);
                }
            }
        }

        private bool HasTrafficTextureConsumer()
        {
            if (xPlaneTrafficRadarDisplay != null &&
                xPlaneTrafficRadarDisplay.UsesXPlaneTrafficTexture &&
                IsUsableTrafficTextureTarget(xPlaneTrafficRadarDisplay.RadarImage))
            {
                trafficImageTarget = xPlaneTrafficRadarDisplay.RadarImage;
                return true;
            }

            return TryBindTrafficTextureTarget();
        }

        private bool TryBindTrafficTextureTarget()
        {
            TrafficRadarDisplay display = FindPreferredTrafficTextureDisplay();
            if (display == null)
            {
                trafficImageTarget = null;
                return false;
            }

            xPlaneTrafficRadarDisplay = display;
            trafficImageTarget = display.RadarImage;
            return IsUsableTrafficTextureTarget(trafficImageTarget);
        }

        private static bool IsUsableTrafficTextureTarget(RawImage image)
        {
            return image != null && image.gameObject.activeInHierarchy && image.enabled;
        }

        private static TrafficRadarDisplay FindPreferredTrafficTextureDisplay()
        {
            TrafficRadarDisplay best = null;
            int bestScore = int.MinValue;
            foreach (TrafficRadarDisplay display in FindObjectsByType<TrafficRadarDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (display == null || !display.UsesXPlaneTrafficTexture || !display.gameObject.activeInHierarchy)
                {
                    continue;
                }

                int score = ScoreTrafficRadarDisplay(display);
                if (score > bestScore)
                {
                    best = display;
                    bestScore = score;
                }
            }

            return best;
        }

        private static int ScoreTrafficRadarDisplay(TrafficRadarDisplay display)
        {
            if (display == null)
            {
                return int.MinValue;
            }

            string path = GetHierarchyPath(display.transform).ToLowerInvariant();
            int score = 0;
            if (path.StartsWith("xplanetrafficradarcanvas/"))
            {
                score += 5000;
            }
            if (path.Contains("/faasymbologycanvas/radarcanvas") ||
                path.Contains("faasymbologycanvasworldspace"))
            {
                score -= 2000;
            }
            if (display.gameObject.activeSelf)
            {
                score += 500;
            }
            if (display.gameObject.activeInHierarchy)
            {
                score += 500;
            }
            if (display.enabled)
            {
                score += 100;
            }

            RawImage image = display.RadarImage;
            if (image != null)
            {
                score += 100;
                if (image.gameObject.activeSelf && image.enabled)
                {
                    score += 100;
                }
                if (image.color.a > 0.01f)
                {
                    score += 100;
                }
            }

            return score;
        }

        private string GetTrafficTexturePath()
        {
            float rangeNm = 120f;
            if (trafficRadarController != null)
            {
                rangeNm = trafficRadarController.RangeNM;
            }
            else if (xPlaneTrafficRadarDisplay != null)
            {
                rangeNm = xPlaneTrafficRadarDisplay.RangeNM;
            }

            return "v1/render/traffic.png?range_nm=" + Mathf.Clamp(rangeNm, 5f, 160f).ToString("0", CultureInfo.InvariantCulture);
        }

        private string GetWeatherTexturePath()
        {
            // Retained only for an explicitly opted-in legacy fallback. The
            // configured FAA scene uses the procedural dataref texture path.
            return "v1/render/weather.png";
        }

        private bool ShouldBridgeDownloadWeatherTexture()
        {
            return !publishWeatherDatarefTextureFromStream &&
                   (weatherImageTarget != null || xPlaneWeatherRadarDisplay != null || weatherRadarProvider != null);
        }

        private IEnumerator DownloadTexture(string relativeUrl, Action<Texture2D> onSuccess)
        {
            string separator = relativeUrl.Contains("?") ? "&" : "?";
            string url = BuildUrl(relativeUrl) + separator + "t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, false))
            {
                request.timeout = Mathf.Max(1, Mathf.RoundToInt(requestTimeoutSeconds));
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (verboseLogging)
                    {
                        Debug.LogWarning($"[XPlane12ApiHudBridge] Texture request failed: {url}: {request.error}");
                    }
                    yield break;
                }

                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                if (texture != null)
                {
                    texture.name = relativeUrl;
                    onSuccess?.Invoke(texture);
                }
            }
        }

        private void ApplyTextureToWeatherProvider(Texture2D texture)
        {
            if (texture == null || weatherRadarProvider == null)
            {
                return;
            }

            if (weatherRadarProvider is XPlaneOriginalWeatherRadarProvider originalProvider)
            {
                if (!_hasWeatherRadarPowerState || _isWeatherRadarPowered)
                {
                    originalProvider.PublishTexture(texture);
                }
                return;
            }

            MethodInfo method = weatherRadarProvider.GetType().GetMethod(
                "SimulateDataReceived",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(Texture2D) },
                null);
            method?.Invoke(weatherRadarProvider, new object[] { texture });
        }

        private void RestoreSuppressedSystems()
        {
            if (_suppressedUserControl && aircraftController != null)
            {
                aircraftController.SetUserControlled(_userControlWasEnabledBeforeApi);
                _suppressedUserControl = false;
            }

            if (_suppressedAircraftControl && aircraftController != null)
            {
                aircraftController.SetControlEnabled(_aircraftControlWasEnabledBeforeApi);
                _suppressedAircraftControl = false;
            }

            if (_suppressedTrafficFetching && trafficRadarDataManager != null)
            {
                if (_trafficWasFetchingBeforeApi && !trafficRadarDataManager.IsActive)
                {
                    trafficRadarDataManager.StartFetching();
                }
                _suppressedTrafficFetching = false;
            }
        }

        private void PopulateSnapshotFromRaw(JObject rawValues)
        {
            _snapshot.Aircraft.Clear();
            _snapshot.Weather.Clear();
            _snapshot.Systems.Clear();
            _snapshot.Traffic.Clear();

            if (rawValues == null)
            {
                return;
            }

            foreach (KeyValuePair<string, JToken> entry in rawValues)
            {
                float value = ReadFloat(entry.Value, float.NaN);
                if (float.IsNaN(value))
                {
                    continue;
                }

                if (entry.Key.StartsWith("sim/flightmodel/position/", StringComparison.Ordinal) ||
                    entry.Key.StartsWith("sim/flightmodel/forces/", StringComparison.Ordinal))
                {
                    _snapshot.Aircraft[entry.Key] = value;
                }
                else if (entry.Key.StartsWith("sim/weather/", StringComparison.Ordinal))
                {
                    _snapshot.Weather[entry.Key] = value;
                }
                else if (entry.Key.StartsWith("sim/multiplayer/position/", StringComparison.Ordinal) ||
                    entry.Key.StartsWith("sim/cockpit2/tcas/targets/", StringComparison.Ordinal))
                {
                    _snapshot.Traffic[entry.Key] = value;
                }
                else
                {
                    _snapshot.Systems[entry.Key] = value;
                }
            }
        }

        private void PopulateSnapshotFromSections(JObject snapshot)
        {
            JObject ownship = snapshot["ownship"] as JObject;
            if (ownship != null)
            {
                AddSnapshotValue(_snapshot.Aircraft, "sim/flightmodel/position/latitude", ownship["latitude"]);
                AddSnapshotValue(_snapshot.Aircraft, "sim/flightmodel/position/longitude", ownship["longitude"]);
                AddSnapshotValue(_snapshot.Aircraft, "sim/flightmodel/position/elevation", ownship["altitude_m"]);
                AddSnapshotValue(_snapshot.Aircraft, "sim/flightmodel/position/y_agl", ownship["altitude_agl_m"]);
                AddSnapshotValue(_snapshot.Aircraft, "sim/flightmodel/position/theta", ownship["pitch_deg"]);
                AddSnapshotValue(_snapshot.Aircraft, "sim/flightmodel/position/phi", ownship["roll_deg"]);
                AddSnapshotValue(_snapshot.Aircraft, "sim/flightmodel/position/psi", ownship["heading_deg"]);
                AddSnapshotValue(_snapshot.Aircraft, "sim/flightmodel/position/mag_psi", ownship["track_deg"]);
                AddSnapshotValue(_snapshot.Aircraft, "sim/flightmodel/position/indicated_airspeed", ownship["indicated_airspeed_kt"]);
                AddScaledSnapshotValue(_snapshot.Aircraft, "sim/flightmodel/position/true_airspeed", ownship["true_airspeed_kt"], 1f / MetersPerSecondToKnots);
                AddScaledSnapshotValue(_snapshot.Aircraft, "sim/flightmodel/position/groundspeed", ownship["ground_speed_kt"], 1f / MetersPerSecondToKnots);
                AddScaledSnapshotValue(_snapshot.Aircraft, "sim/flightmodel/position/vh_ind", ownship["vertical_speed_fpm"], 1f / MetersPerSecondToFeetPerMinute);
                AddSnapshotValue(_snapshot.Systems, "sim/cockpit/autopilot/autopilot_mode", ownship["autopilot_mode"]);
            }

            JObject weather = snapshot["weather"] as JObject;
            if (weather != null)
            {
                AddSnapshotValue(_snapshot.Weather, "sim/weather/wind_speed_kt[0]", weather["wind_speed_kt"]);
                AddSnapshotValue(_snapshot.Weather, "sim/weather/wind_direction_degt[0]", weather["wind_direction_deg"]);
                AddSnapshotValue(_snapshot.Weather, "sim/weather/barometer_sealevel_inhg", weather["barometer_inhg"]);
                AddSnapshotValue(_snapshot.Weather, "sim/weather/temperature_ambient_c", weather["temperature_c"]);
                AddSnapshotValue(_snapshot.Weather, "sim/weather/visibility_reported_m", weather["visibility_m"]);
                AddSnapshotValue(_snapshot.Weather, "sim/weather/aircraft/precipitation_on_aircraft_ratio", weather["precipitation_on_aircraft_ratio"]);
            }
        }

        private void PopulateTrafficFromSections(JObject snapshot)
        {
            JArray traffic = snapshot?["traffic"] as JArray;
            if (traffic == null)
            {
                return;
            }

            int planeIndex = 1;
            foreach (JToken token in traffic)
            {
                if (planeIndex > 19)
                {
                    break;
                }

                JObject target = token as JObject;
                if (target == null)
                {
                    continue;
                }
                if (IsSyntheticTrafficTarget(target))
                {
                    continue;
                }

                float latitude = ReadFloat(target["latitude"], float.NaN);
                float longitude = ReadFloat(target["longitude"], float.NaN);
                float altitudeMeters = ReadFloat(target["altitude_m"], float.NaN);
                if (!IsFinite(latitude) || !IsFinite(longitude) || !IsFinite(altitudeMeters))
                {
                    continue;
                }

                float heading = ReadFloat(target["heading_deg"], 0f);
                float speed = Mathf.Max(0f, ReadFloat(target["velocity_mps"], 0f));
                float verticalRate = ReadFloat(target["vertical_rate_mps"], 0f);
                float headingRadians = heading * Mathf.Deg2Rad;
                string prefix = $"sim/multiplayer/position/plane{planeIndex}";

                _snapshot.Traffic[prefix + "_lat"] = latitude;
                _snapshot.Traffic[prefix + "_lon"] = longitude;
                _snapshot.Traffic[prefix + "_el"] = altitudeMeters;
                _snapshot.Traffic[prefix + "_psi"] = heading;
                _snapshot.Traffic[prefix + "_v_x"] = Mathf.Sin(headingRadians) * speed;
                _snapshot.Traffic[prefix + "_v_y"] = verticalRate;
                _snapshot.Traffic[prefix + "_v_z"] = Mathf.Cos(headingRadians) * speed;
                planeIndex++;
            }
        }

        private static bool HasMultiplayerTraffic(IDictionary<string, float> traffic)
        {
            if (traffic == null)
            {
                return false;
            }

            for (int i = 1; i <= 19; i++)
            {
                string prefix = $"sim/multiplayer/position/plane{i}";
                float latitude = Get(traffic, prefix + "_lat");
                float longitude = Get(traffic, prefix + "_lon");
                float elevationMeters = Get(traffic, prefix + "_el");
                if (Mathf.Abs(latitude) > 0.0001f || Mathf.Abs(longitude) > 0.0001f || Mathf.Abs(elevationMeters) > 1f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSyntheticTrafficTarget(JObject target)
        {
            string source = target.Value<string>("source") ?? string.Empty;
            if (IsSyntheticSourceMode(source))
            {
                return true;
            }

            string icao = target.Value<string>("icao24") ?? string.Empty;
            string callsign = target.Value<string>("callsign") ?? string.Empty;
            return StartsWithSyntheticPrefix(icao) || StartsWithSyntheticPrefix(callsign);
        }

        private static bool StartsWithSyntheticPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.StartsWith("MOCK", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("SYN", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("FAKE", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSyntheticSourceMode(string sourceMode)
        {
            if (string.IsNullOrWhiteSpace(sourceMode))
            {
                return false;
            }

            return sourceMode.IndexOf("mock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sourceMode.IndexOf("synthetic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sourceMode.IndexOf("generated", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sourceMode.IndexOf("sample", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sourceMode.IndexOf("demo", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string DescribeTransport()
        {
            switch (transportMode)
            {
                case TransportMode.WebSocketStream:
                    return string.IsNullOrWhiteSpace(webSocketUrl)
                        ? "WebSocket ws://127.0.0.1:37212/v1/stream/ws"
                        : $"WebSocket {webSocketUrl.Trim()}";
                case TransportMode.TcpNdjsonStream:
                    return $"TCP NDJSON {tcpStreamHost}:{tcpStreamPort}";
                case TransportMode.MqttSnapshot:
                    return $"MQTT {mqttBrokerHost}:{mqttBrokerPort} ({mqttSnapshotTopic})";
                case TransportMode.HttpApi:
                default:
                    return baseUrl;
            }
        }

        private string BuildUrl(string relativeUrl)
        {
            if (relativeUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                relativeUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return relativeUrl;
            }

            return baseUrl.TrimEnd('/') + "/" + relativeUrl.TrimStart('/');
        }

        private static Dictionary<string, float> ReadValues(JObject json)
        {
            var output = new Dictionary<string, float>(StringComparer.Ordinal);
            JObject values = json["values"] as JObject;
            if (values == null)
            {
                return output;
            }

            foreach (KeyValuePair<string, JToken> pair in values)
            {
                float value = ReadFloat(pair.Value, float.NaN);
                if (!float.IsNaN(value))
                {
                    output[pair.Key] = value;
                }
            }

            return output;
        }

        private static float ReadFloat(JToken token, float defaultValue = 0f)
        {
            if (token == null)
            {
                return defaultValue;
            }

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                return token.Value<float>();
            }

            return float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : defaultValue;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TryGetFinite(IDictionary<string, float> values, string key, out float value)
        {
            value = 0f;
            return values != null &&
                   values.TryGetValue(key, out value) &&
                   IsFinite(value);
        }

        private static void AddSnapshotValue(IDictionary<string, float> values, string key, JToken token)
        {
            float value = ReadFloat(token, float.NaN);
            if (!float.IsNaN(value))
            {
                values[key] = value;
            }
        }

        private static void AddScaledSnapshotValue(IDictionary<string, float> values, string key, JToken token, float scale)
        {
            float value = ReadFloat(token, float.NaN);
            if (!float.IsNaN(value))
            {
                values[key] = value * scale;
            }
        }

        private static float Get(IDictionary<string, float> values, string key, float defaultValue = 0f)
        {
            if (values != null && values.TryGetValue(key, out float value) && !float.IsNaN(value))
            {
                return value;
            }
            return defaultValue;
        }

        private static double Get(IDictionary<string, float> values, string key, double defaultValue)
        {
            return Get(values, key, (float)defaultValue);
        }

        private static float GetAny(IDictionary<string, float> values, float defaultValue, params string[] keys)
        {
            if (values == null)
            {
                return defaultValue;
            }

            foreach (string key in keys)
            {
                if (values.TryGetValue(key, out float value) && !float.IsNaN(value))
                {
                    return value;
                }
            }

            return defaultValue;
        }

        private static bool TryGetAny(IDictionary<string, float> values, out float value, params string[] keys)
        {
            value = 0f;
            if (values == null)
            {
                return false;
            }

            foreach (string key in keys)
            {
                if (values.TryGetValue(key, out value) && !float.IsNaN(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetWindSpeed(IDictionary<string, float> weather)
        {
            if (weather == null)
            {
                return 0f;
            }

            if (weather.TryGetValue("sim/weather/aircraft/wind_now_speed_msc", out float metersPerSecond))
            {
                return metersPerSecond * MetersPerSecondToKnots;
            }

            return GetAny(weather, 0f,
                "sim/weather/wind_speed_kt[0]",
                "sim/weather/aircraft/wind_speed_kt");
        }

        private static float GetWeatherWindDirection(IDictionary<string, float> weather)
        {
            return Mathf.Repeat(GetAny(weather, 0f,
                "sim/weather/aircraft/wind_now_direction_degt",
                "sim/weather/region/wind_direction_degt[0]",
                "sim/weather/wind_direction_degt[0]",
                "sim/weather/aircraft/wind_direction_deg"), 360f);
        }

        private static float GetBarometerInHg(IDictionary<string, float> weather)
        {
            float inHg = GetAny(weather, float.NaN,
                "sim/weather/barometer_sealevel_inhg",
                "sim/weather/aircraft/barometer_sealevel_inhg");
            if (!float.IsNaN(inHg))
            {
                return inHg;
            }

            float pascals = GetAny(weather, float.NaN,
                "sim/weather/aircraft/qnh_pas",
                "sim/weather/aircraft/barometer_current_pas");
            return !float.IsNaN(pascals) ? pascals * 0.000295300f : 29.92f;
        }

        private static float GetNavigationDeviation(IDictionary<string, float> systems)
        {
            return GetAny(systems, 0f,
                "sim/cockpit2/radios/indicators/hsi_hdef_dots_pilot",
                "sim/cockpit2/radios/indicators/nav1_hdef_dots_pilot",
                "sim/cockpit2/radios/indicators/nav2_hdef_dots_pilot",
                "sim/cockpit2/radios/indicators/gps_hdef_dots_pilot",
                "sim/cockpit/radios/nav1_hdef_dot",
                "sim/cockpit/radios/nav2_hdef_dot",
                "sim/cockpit/radios/gps_hdef_dot");
        }

        private static float GetGlideslopeDeviation(IDictionary<string, float> systems)
        {
            return GetAny(systems, 0f,
                "sim/cockpit2/radios/indicators/hsi_vdef_dots_pilot",
                "sim/cockpit2/radios/indicators/nav1_vdef_dots_pilot",
                "sim/cockpit2/radios/indicators/nav2_vdef_dots_pilot",
                "sim/cockpit/radios/nav1_vdef_dot",
                "sim/cockpit/radios/nav2_vdef_dot",
                "sim/cockpit/radios/gps_vdef_dot");
        }

        private static bool IsAutopilotEngaged(IDictionary<string, float> systems)
        {
            float state = GetAny(systems, 0f,
                "sim/cockpit/autopilot/autopilot_state",
                "sim/cockpit/autopilot/autopilot_mode");
            float headingStatus = Get(systems, "sim/cockpit2/autopilot/heading_status");
            float navStatus = Get(systems, "sim/cockpit2/autopilot/nav_status");
            float altitudeStatus = Get(systems, "sim/cockpit2/autopilot/altitude_hold_status");
            float flightDirectorMode = Get(systems, "sim/cockpit2/autopilot/flight_director_mode");
            return state > 0.5f || headingStatus > 0.5f || navStatus > 0.5f || altitudeStatus > 0.5f || flightDirectorMode > 0.5f;
        }

        private static float GeoAltitudeFromAgl(AviationFlightData data)
        {
            if (data == null)
            {
                return 0f;
            }

            return (data.altitudeMSL - Mathf.Max(0f, data.altitudeAGL)) / MetersToFeet;
        }

        private static float CalculateFlightPathAngle(float verticalSpeedFpm, float groundSpeedKnots)
        {
            float groundSpeedFps = Mathf.Max(groundSpeedKnots * KnotsToFeetPerSecond, 0.1f);
            float verticalSpeedFps = verticalSpeedFpm / 60f;
            return Mathf.Atan2(verticalSpeedFps, groundSpeedFps) * Mathf.Rad2Deg;
        }

        private static void ForEach<T>(IEnumerable<T> items, Action<T> action) where T : Component
        {
            if (items == null)
            {
                return;
            }

            foreach (T item in items)
            {
                if (item == null)
                {
                    continue;
                }

                try
                {
                    action(item);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[XPlane12ApiHudBridge] Failed to update {typeof(T).Name} on {item.name}: {ex.Message}", item);
                }
            }
        }

        private static void ReplaceTexture(ref Texture2D current, Texture2D replacement)
        {
            if (ReferenceEquals(current, replacement))
            {
                return;
            }

            DestroyTexture(ref current);
            current = replacement;
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            Destroy(texture);
            texture = null;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            Stack<string> names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }
    }

    [Serializable]
    public class XPlane12ApiSnapshot
    {
        public Dictionary<string, float> Aircraft = new Dictionary<string, float>(StringComparer.Ordinal);
        public Dictionary<string, float> Weather = new Dictionary<string, float>(StringComparer.Ordinal);
        public Dictionary<string, float> Systems = new Dictionary<string, float>(StringComparer.Ordinal);
        public Dictionary<string, float> Traffic = new Dictionary<string, float>(StringComparer.Ordinal);
        public string GaugeManifest = string.Empty;
    }
}
