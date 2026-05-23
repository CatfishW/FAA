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
        [SerializeField] private string baseUrl = "https://faa.agaii.org/xplane12";
        [SerializeField] private bool autoStartOnPlay = true;
        [SerializeField] private float pollIntervalSeconds = 0.1f;
        [SerializeField] private float requestTimeoutSeconds = 2f;
        [SerializeField] private float staleAfterSeconds = 5f;

        [Header("Transport")]
        [SerializeField] private TransportMode transportMode = TransportMode.TcpNdjsonStream;
        [SerializeField] private string tcpStreamHost = "127.0.0.1";
        [SerializeField] private int tcpStreamPort = 37212;
        [SerializeField] private string webSocketUrl = "ws://127.0.0.1:37212/v1/stream/ws";
        [SerializeField] private float webSocketReconnectDelaySeconds = 0.5f;
        [SerializeField] private int webSocketReceiveBufferBytes = 262144;
        [SerializeField] private bool webSocketUseMqttFallback = true;
        [SerializeField] private bool webSocketUseHttpFallback = true;
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

        public Texture2D LatestWeatherTexture => _latestWeatherTexture;
        public Texture2D LatestTrafficTexture => _latestTrafficTexture;
        public TransportMode CurrentTransportMode => transportMode;

        private void Awake()
        {
            FindDependencies();
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
        }

        private void OnDisable()
        {
            StopBridge();
        }

        private void OnDestroy()
        {
            DestroyTexture(ref _latestWeatherTexture);
            DestroyTexture(ref _latestTrafficTexture);
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

            if (xPlaneTrafficRadarDisplay == null)
            {
                xPlaneTrafficRadarDisplay = FindAnyObjectByType<TrafficRadarDisplay>(FindObjectsInactive.Include);
            }

            if (trafficImageTarget == null && xPlaneTrafficRadarDisplay != null)
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
            data.engine1Torque = GetAny(systems, data.engine1Torque,
                "sim/cockpit2/engine/indicators/torque_percent[0]",
                "sim/flightmodel/engine/ENGN_thro[0]");
            data.engine2Torque = GetAny(systems, data.engine2Torque,
                "sim/cockpit2/engine/indicators/torque_percent[1]",
                "sim/flightmodel/engine/ENGN_thro[1]");
            data.engine1NR = GetAny(systems, data.engine1NR,
                "sim/cockpit2/engine/indicators/N1_percent[0]",
                "sim/cockpit2/engine/indicators/prop_speed_rpm[0]");
            data.engine2NR = GetAny(systems, data.engine2NR,
                "sim/cockpit2/engine/indicators/N1_percent[1]",
                "sim/cockpit2/engine/indicators/prop_speed_rpm[1]");
            data.engine1NG = GetAny(systems, data.engine1NG,
                "sim/cockpit2/engine/indicators/N2_percent[0]",
                "sim/cockpit2/engine/indicators/engine_speed_rpm[0]");
            data.engine2NG = GetAny(systems, data.engine2NG,
                "sim/cockpit2/engine/indicators/N2_percent[1]",
                "sim/cockpit2/engine/indicators/engine_speed_rpm[1]");

            if (data.engine1Torque > 0f && data.engine1Torque <= 1f)
            {
                data.engine1Torque *= 100f;
            }
            if (data.engine2Torque > 0f && data.engine2Torque <= 1f)
            {
                data.engine2Torque *= 100f;
            }
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
            }

            if (hudController != null && aircraftController?.State != null)
            {
                hudController.InjectState(aircraftController.State);
            }
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

            if (disableTrafficApiWhenReceiving && !_suppressedTrafficFetching)
            {
                _trafficWasFetchingBeforeApi = trafficRadarDataManager.IsActive;
                if (_trafficWasFetchingBeforeApi)
                {
                    trafficRadarDataManager.StopFetching();
                }
                _suppressedTrafficFetching = true;
            }

            double ownLat = Get(_snapshot.Aircraft, "sim/flightmodel/position/latitude", 0d);
            double ownLon = Get(_snapshot.Aircraft, "sim/flightmodel/position/longitude", 0d);
            BuildTrafficRows(_snapshot.Traffic, ownLat, ownLon, _trafficRows);
            TrafficCount = _trafficRows.Count;

            trafficRadarDataManager.aircraftMap.Clear();
            trafficRadarDataManager.aircraftList.Clear();
            foreach (TrafficRadarDataManager.AircraftData row in _trafficRows)
            {
                trafficRadarDataManager.aircraftMap[row.icao24] = row;
                trafficRadarDataManager.aircraftList.Add(row);
            }

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

            if (refreshWeatherRadarTexture && Time.time - _lastWeatherRefreshTime >= Mathf.Max(1f, renderAssetPollIntervalSeconds))
            {
                _lastWeatherRefreshTime = Time.time;
                weatherRadarProvider.RefreshData();
            }
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
                if (ShouldBridgeDownloadWeatherTexture())
                {
                    yield return DownloadTexture("v1/render/weather.png", texture =>
                    {
                        ReplaceTexture(ref _latestWeatherTexture, texture);
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

                if (trafficImageTarget != null)
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

            if (trafficImageTarget != null)
            {
                trafficImageTarget.texture = texture;
                trafficImageTarget.color = Color.white;
                trafficImageTarget.enabled = true;
                trafficImageTarget.raycastTarget = false;
            }

            if (xPlaneTrafficRadarDisplay != null)
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

        private bool ShouldBridgeDownloadWeatherTexture()
        {
            if (weatherRadarProvider is XPlaneOriginalWeatherRadarProvider)
            {
                return false;
            }

            return weatherImageTarget != null || xPlaneWeatherRadarDisplay != null || weatherRadarProvider != null;
        }

        private IEnumerator DownloadTexture(string relativeUrl, Action<Texture2D> onSuccess)
        {
            string separator = relativeUrl.Contains("?") ? "&" : "?";
            string url = BuildUrl(relativeUrl) + separator + "t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, true))
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
