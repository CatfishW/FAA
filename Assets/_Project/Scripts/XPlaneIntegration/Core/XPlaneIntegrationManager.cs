using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using FAA.XPlaneIntegration;

namespace FAA.XPlaneIntegration.Core
{
    /// <summary>
    /// Master manager component that coordinates all X-Plane integration systems.
    /// Singleton pattern (DontDestroyOnLoad) providing centralized connection management,
    /// provider coordination, and global connection state.
    /// 
    /// Features:
    /// - Centralized X-Plane connection management (connect/disconnect)
    /// - Auto-connect on Start (configurable)
    /// - Auto-reconnect on failure with configurable retry logic
    /// - Global connection state and events
    /// - Provider coordination (finds and manages all XPlane*Provider components)
    /// 
    /// Usage:
    /// 1. Add this component to a persistent GameObject in your scene
    /// 2. Configure connection settings in Inspector
    /// 3. Providers are auto-discovered and coordinated
    /// 4. Subscribe to global events for connection state changes
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("X-Plane Integration/X-Plane Integration Manager")]
    public class XPlaneIntegrationManager : MonoBehaviour
    {
        #region Singleton

        private static XPlaneIntegrationManager _instance;

        /// <summary>
        /// Singleton instance. Creates one if it doesn't exist.
        /// </summary>
        public static XPlaneIntegrationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<XPlaneIntegrationManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("XPlaneIntegrationManager");
                        _instance = go.AddComponent<XPlaneIntegrationManager>();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Whether singleton instance exists
        /// </summary>
        public static bool HasInstance => _instance != null;

        #endregion

        #region Inspector Configuration

        [Header("X-Plane Connection Settings")]
        [Tooltip("X-Plane IP address (default: 127.0.0.1 for local)")]
        [SerializeField] private string xPlaneIpAddress = "127.0.0.1";

        [Tooltip("UDP port for X-Plane data (default: 49009)")]
        [SerializeField] private int udpPort = 49009;

        [Tooltip("Auto-connect to X-Plane on Start()")]
        [SerializeField] private bool autoConnectOnStart = true;

        [Header("Auto-Reconnect Settings")]
        [Tooltip("Automatically attempt to reconnect on connection loss")]
        [SerializeField] private bool autoReconnect = true;

        [Tooltip("Maximum number of reconnection attempts (0 = infinite)")]
        [SerializeField] private int maxReconnectAttempts = 5;

        [Tooltip("Delay between reconnection attempts in seconds")]
        [Range(0.5f, 30f)]
        [SerializeField] private float reconnectDelay = 2f;

        [Header("Provider Settings")]
        [Tooltip("Automatically find and coordinate XPlane*Provider components in scene")]
        [SerializeField] private bool autoDiscoverProviders = true;

        [Tooltip("Start providers when manager connects")]
        [SerializeField] private bool startProvidersOnConnect = true;

        [Tooltip("Stop providers when manager disconnects")]
        [SerializeField] private bool stopProvidersOnDisconnect = true;

        [Header("Events")]
        [Tooltip("Fired when connection is established")]
        [SerializeField] private UnityEvent onConnected;

        [Tooltip("Fired when connection is lost")]
        [SerializeField] private UnityEvent onDisconnected;

        [Tooltip("Fired when an error occurs")]
        [SerializeField] private UnityEvent<string> onError;

        #endregion

        #region Connection State

        /// <summary>
        /// Current connection state
        /// </summary>
        public enum ConnectionState
        {
            Disconnected,
            Connecting,
            Connected,
            Error
        }

        private ConnectionState _connectionState = ConnectionState.Disconnected;

        /// <summary>
        /// Current connection state
        /// </summary>
        public ConnectionState CurrentState => _connectionState;

        /// <summary>
        /// Whether currently connected to X-Plane
        /// </summary>
        public bool IsConnected => _connectionState == ConnectionState.Connected;

        /// <summary>
        /// Whether currently attempting to connect
        /// </summary>
        public bool IsConnecting => _connectionState == ConnectionState.Connecting;

        /// <summary>
        /// Last error message (empty if no error)
        /// </summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>
        /// Time when connection was established (0 if not connected)
        /// </summary>
        public float ConnectionStartTime { get; private set; }

        /// <summary>
        /// Current connection uptime in seconds (0 if not connected)
        /// </summary>
        public float ConnectionUptime => IsConnected ? Time.time - ConnectionStartTime : 0f;

        /// <summary>
        /// Number of reconnection attempts made
        /// </summary>
        public int ReconnectAttempts { get; private set; }

        /// <summary>
        /// Whether auto-reconnect is currently scheduled
        /// </summary>
        public bool IsReconnectScheduled => _reconnectScheduled;

        #endregion

        #region Events

        /// <summary>
        /// Fired when connection is established
        /// </summary>
        public event Action OnConnected;

        /// <summary>
        /// Fired when connection is lost
        /// </summary>
        public event Action OnDisconnected;

        /// <summary>
        /// Fired when connection state changes
        /// </summary>
        public event Action<ConnectionState> OnConnectionStateChanged;

        /// <summary>
        /// Fired when an error occurs
        /// </summary>
        public event Action<string> OnError;

        #endregion

        #region Provider Management

        private List<XPlaneAircraftProvider> _aircraftProviders = new List<XPlaneAircraftProvider>();
        private List<XPlaneWeatherProvider> _weatherProviders = new List<XPlaneWeatherProvider>();
        private List<XPlaneTrafficProvider> _trafficProviders = new List<XPlaneTrafficProvider>();

        /// <summary>
        /// List of all discovered aircraft providers
        /// </summary>
        public IReadOnlyList<XPlaneAircraftProvider> AircraftProviders => _aircraftProviders.AsReadOnly();

        /// <summary>
        /// List of all discovered weather providers
        /// </summary>
        public IReadOnlyList<XPlaneWeatherProvider> WeatherProviders => _weatherProviders.AsReadOnly();

        /// <summary>
        /// List of all discovered traffic providers
        /// </summary>
        public IReadOnlyList<XPlaneTrafficProvider> TrafficProviders => _trafficProviders.AsReadOnly();

        /// <summary>
        /// Total number of managed providers
        /// </summary>
        public int TotalProviderCount => _aircraftProviders.Count + _weatherProviders.Count + _trafficProviders.Count;

        #endregion

        #region Private Fields

        private XPlaneUdpListener _udpListener;
        private bool _isInitialized;
        private bool _reconnectScheduled;
        private int _currentReconnectAttempt;
        private CancellationTokenSource _reconnectCts;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[XPlaneIntegrationManager] Multiple instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeUdpListener();
            DiscoverProviders();
            _isInitialized = true;

            LogDebug("XPlaneIntegrationManager initialized");
        }

        private void Start()
        {
            if (autoConnectOnStart)
            {
                Connect();
            }
        }

        private void Update()
        {
            if (_udpListener != null && IsConnected)
            {
                _udpListener.ProcessQueuedData();
            }
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
            CancelReconnect();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            Cleanup();
        }

        #endregion

        #region Initialization

        private void InitializeUdpListener()
        {
            _udpListener = new XPlaneUdpListener(xPlaneIpAddress, udpPort);
        }

        /// <summary>
        /// Discover all XPlane*Provider components in the scene
        /// </summary>
        public void DiscoverProviders()
        {
            if (!autoDiscoverProviders)
            {
                LogDebug("Auto-discover providers disabled");
                return;
            }

            _aircraftProviders.Clear();
            _weatherProviders.Clear();
            _trafficProviders.Clear();

            var aircraftProviders = FindObjectsOfType<XPlaneAircraftProvider>();
            _aircraftProviders.AddRange(aircraftProviders);

            var weatherProviders = FindObjectsOfType<XPlaneWeatherProvider>();
            _weatherProviders.AddRange(weatherProviders);

            var trafficProviders = FindObjectsOfType<XPlaneTrafficProvider>();
            _trafficProviders.AddRange(trafficProviders);

            LogDebug($"Discovered providers: {_aircraftProviders.Count} aircraft, {_weatherProviders.Count} weather, {_trafficProviders.Count} traffic");
        }

        private void SubscribeToEvents()
        {
            if (_udpListener != null)
            {
                _udpListener.OnConnectionStateChanged += OnUdpConnectionStateChanged;
                _udpListener.OnError += OnUdpError;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_udpListener != null)
            {
                _udpListener.OnConnectionStateChanged -= OnUdpConnectionStateChanged;
                _udpListener.OnError -= OnUdpError;
            }
        }

        #endregion

        #region Connection Management

        /// <summary>
        /// Connect to X-Plane
        /// </summary>
        public void Connect()
        {
            if (_udpListener == null)
            {
                InitializeUdpListener();
            }

            if (IsConnected || IsConnecting)
            {
                LogDebug("Connect called but already connected/connecting");
                return;
            }

            SetConnectionState(ConnectionState.Connecting);
            LastError = string.Empty;
            CancelReconnect();

            try
            {
                _udpListener.Connect(xPlaneIpAddress);
                LogDebug("Connection initiated");
            }
            catch (Exception ex)
            {
                HandleConnectionError($"Connection failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Disconnect from X-Plane
        /// </summary>
        public void Disconnect()
        {
            CancelReconnect();

            if (stopProvidersOnDisconnect)
            {
                StopAllProviders();
            }

            if (_udpListener != null)
            {
                _udpListener.Disconnect();
            }

            SetConnectionState(ConnectionState.Disconnected);
            ConnectionStartTime = 0;
            LogDebug("Disconnected from X-Plane");
        }

        /// <summary>
        /// Reconnect to X-Plane (disconnect then connect)
        /// </summary>
        public void Reconnect()
        {
            Disconnect();
            Invoke(nameof(Connect), 0.5f);
        }

        /// <summary>
        /// Set X-Plane IP address (takes effect on next connect)
        /// </summary>
        public void SetIpAddress(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                Debug.LogWarning("[XPlaneIntegrationManager] Cannot set empty IP address");
                return;
            }

            xPlaneIpAddress = ip;
            if (_udpListener != null)
            {
                LogDebug($"IP address changed to {ip} - reconnect required");
            }
        }

        /// <summary>
        /// Set UDP port (takes effect on next connect)
        /// </summary>
        public void SetUdpPort(int port)
        {
            if (port < 1 || port > 65535)
            {
                Debug.LogWarning("[XPlaneIntegrationManager] Invalid UDP port");
                return;
            }

            udpPort = port;
            LogDebug($"UDP port changed to {port} - reconnect required");
        }

        #endregion

        #region Provider Coordination

        /// <summary>
        /// Start all discovered providers
        /// </summary>
        public void StartAllProviders()
        {
            foreach (var provider in _aircraftProviders)
            {
                if (provider != null && !provider.IsEnabled)
                {
                    provider.SetEnabled(true);
                }
            }

            foreach (var provider in _weatherProviders)
            {
                if (provider != null)
                {
                    provider.EnableXPlaneWeather = true;
                }
            }

            foreach (var provider in _trafficProviders)
            {
                if (provider != null)
                {
                    provider.EnableXPlaneTraffic = true;
                }
            }

            LogDebug("Started all providers");
        }

        /// <summary>
        /// Stop all discovered providers
        /// </summary>
        public void StopAllProviders()
        {
            foreach (var provider in _aircraftProviders)
            {
                if (provider != null)
                {
                    provider.SetEnabled(false);
                }
            }

            foreach (var provider in _weatherProviders)
            {
                if (provider != null)
                {
                    provider.EnableXPlaneWeather = false;
                }
            }

            foreach (var provider in _trafficProviders)
            {
                if (provider != null)
                {
                    provider.EnableXPlaneTraffic = false;
                }
            }

            LogDebug("Stopped all providers");
        }

        /// <summary>
        /// Register a provider manually (for runtime-created providers)
        /// </summary>
        public void RegisterProvider(MonoBehaviour provider)
        {
            if (provider is XPlaneAircraftProvider aircraftProvider)
            {
                if (!_aircraftProviders.Contains(aircraftProvider))
                {
                    _aircraftProviders.Add(aircraftProvider);
                    LogDebug($"Registered aircraft provider: {provider.name}");
                }
            }
            else if (provider is XPlaneWeatherProvider weatherProvider)
            {
                if (!_weatherProviders.Contains(weatherProvider))
                {
                    _weatherProviders.Add(weatherProvider);
                    LogDebug($"Registered weather provider: {provider.name}");
                }
            }
            else if (provider is XPlaneTrafficProvider trafficProvider)
            {
                if (!_trafficProviders.Contains(trafficProvider))
                {
                    _trafficProviders.Add(trafficProvider);
                    LogDebug($"Registered traffic provider: {provider.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[XPlaneIntegrationManager] Unknown provider type: {provider.GetType().Name}");
            }
        }

        /// <summary>
        /// Unregister a provider
        /// </summary>
        public void UnregisterProvider(MonoBehaviour provider)
        {
            if (provider is XPlaneAircraftProvider aircraftProvider)
            {
                _aircraftProviders.Remove(aircraftProvider);
            }
            else if (provider is XPlaneWeatherProvider weatherProvider)
            {
                _weatherProviders.Remove(weatherProvider);
            }
            else if (provider is XPlaneTrafficProvider trafficProvider)
            {
                _trafficProviders.Remove(trafficProvider);
            }
        }

        #endregion

        #region Event Handlers

        private void OnUdpConnectionStateChanged(XPlaneUdpListener.ConnectionState state)
        {
            switch (state)
            {
                case XPlaneUdpListener.ConnectionState.Connected:
                    SetConnectionState(ConnectionState.Connected);
                    ConnectionStartTime = Time.time;
                    ReconnectAttempts = 0;
                    _currentReconnectAttempt = 0;

                    if (startProvidersOnConnect)
                    {
                        StartAllProviders();
                    }

                    OnConnected?.Invoke();
                    onConnected?.Invoke();
                    LogDebug("Connected to X-Plane");
                    break;

                case XPlaneUdpListener.ConnectionState.Disconnected:
                    if (_connectionState != ConnectionState.Error)
                    {
                        SetConnectionState(ConnectionState.Disconnected);
                        ConnectionStartTime = 0;

                        if (stopProvidersOnDisconnect)
                        {
                            StopAllProviders();
                        }

                        OnDisconnected?.Invoke();
                        onDisconnected?.Invoke();
                        LogDebug("Disconnected from X-Plane");
                    }
                    break;

                case XPlaneUdpListener.ConnectionState.Error:
                    HandleConnectionError("X-Plane connection error");
                    break;
            }

            OnConnectionStateChanged?.Invoke(_connectionState);
        }

        private void OnUdpError(string errorMessage)
        {
            HandleConnectionError(errorMessage);
        }

        private void HandleConnectionError(string errorMessage)
        {
            LastError = errorMessage;
            SetConnectionState(ConnectionState.Error);
            OnError?.Invoke(errorMessage);
            onError?.Invoke(errorMessage);

            Debug.LogError($"[XPlaneIntegrationManager] Error: {errorMessage}");

            if (autoReconnect && !_reconnectScheduled)
            {
                ScheduleReconnect();
            }
        }

        private void SetConnectionState(ConnectionState newState)
        {
            if (_connectionState != newState)
            {
                _connectionState = newState;
                LogDebug($"Connection state changed: {newState}");
            }
        }

        #endregion

        #region Auto-Reconnect

        private void ScheduleReconnect()
        {
            if (maxReconnectAttempts > 0 && _currentReconnectAttempt >= maxReconnectAttempts)
            {
                Debug.LogError($"[XPlaneIntegrationManager] Max reconnect attempts ({maxReconnectAttempts}) reached. Giving up.");
                LastError = $"Max reconnect attempts reached ({maxReconnectAttempts})";
                return;
            }

            _reconnectScheduled = true;
            _currentReconnectAttempt++;
            ReconnectAttempts = _currentReconnectAttempt;

            LogDebug($"Scheduling reconnect attempt {_currentReconnectAttempt}/{(maxReconnectAttempts > 0 ? maxReconnectAttempts.ToString() : "∞")} in {reconnectDelay}s");

            _reconnectCts = new CancellationTokenSource();
            var token = _reconnectCts.Token;

            _ = ReconnectAsync(token);
        }

        private async System.Threading.Tasks.Task ReconnectAsync(System.Threading.CancellationToken token)
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(reconnectDelay), token);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                _reconnectScheduled = false;

                if (!IsConnected)
                {
                    LogDebug("Attempting reconnection...");
                    Connect();
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XPlaneIntegrationManager] Reconnect error: {ex.Message}");
                _reconnectScheduled = false;
            }
        }

        private void CancelReconnect()
        {
            if (_reconnectCts != null)
            {
                _reconnectCts.Cancel();
                _reconnectCts.Dispose();
                _reconnectCts = null;
            }
            _reconnectScheduled = false;
        }

        #endregion

        #region Cleanup

        private void Cleanup()
        {
            CancelReconnect();
            UnsubscribeFromEvents();

            if (_udpListener != null)
            {
                _udpListener.Dispose();
                _udpListener = null;
            }

            _aircraftProviders.Clear();
            _weatherProviders.Clear();
            _trafficProviders.Clear();

            LogDebug("XPlaneIntegrationManager cleaned up");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get the UDP listener instance (for advanced usage)
        /// </summary>
        public XPlaneUdpListener GetUdpListener() => _udpListener;

        /// <summary>
        /// Force provider rediscovery (useful after runtime spawning)
        /// </summary>
        public void RefreshProviders()
        {
            DiscoverProviders();
        }

        /// <summary>
        /// Get connection status summary for debugging
        /// </summary>
        public string GetStatusSummary()
        {
            return $"State: {_connectionState} | Connected: {IsConnected} | Uptime: {ConnectionUptime:F1}s | " +
                   $"Providers: {TotalProviderCount} | Reconnects: {ReconnectAttempts} | Error: {(string.IsNullOrEmpty(LastError) ? "None" : LastError)}";
        }

        #endregion

        #region Debug

        [System.Diagnostics.Conditional("DEBUG")]
        private void LogDebug(string message)
        {
            Debug.Log($"[XPlaneIntegrationManager] {message}");
        }

#if UNITY_EDITOR
        [ContextMenu("Connect")]
        private void EditorConnect()
        {
            Connect();
        }

        [ContextMenu("Disconnect")]
        private void EditorDisconnect()
        {
            Disconnect();
        }

        [ContextMenu("Reconnect")]
        private void EditorReconnect()
        {
            Reconnect();
        }

        [ContextMenu("Refresh Providers")]
        private void EditorRefreshProviders()
        {
            DiscoverProviders();
        }

        [ContextMenu("Log Status")]
        private void EditorLogStatus()
        {
            Debug.Log("=== XPlaneIntegrationManager Status ===");
            Debug.Log($"State: {_connectionState}");
            Debug.Log($"Connected: {IsConnected}");
            Debug.Log($"Uptime: {ConnectionUptime:F1}s");
            Debug.Log($"IP: {xPlaneIpAddress}:{udpPort}");
            Debug.Log($"Auto-connect: {autoConnectOnStart}");
            Debug.Log($"Auto-reconnect: {autoReconnect} (max: {maxReconnectAttempts}, delay: {reconnectDelay}s)");
            Debug.Log($"Providers: {_aircraftProviders.Count} aircraft, {_weatherProviders.Count} weather, {_trafficProviders.Count} traffic");
            Debug.Log($"Last Error: {(string.IsNullOrEmpty(LastError) ? "None" : LastError)}");
        }
#endif

        #endregion
    }
}
