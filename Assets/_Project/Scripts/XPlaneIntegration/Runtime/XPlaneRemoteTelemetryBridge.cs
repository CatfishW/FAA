using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using AircraftControl.Core;
using Newtonsoft.Json;
using TrafficRadar;
using TrafficRadar.Core;
using UnityEngine;
using WeatherRadar;
using AircraftRuntimeState = AircraftControl.Core.AircraftState;

namespace FAA.XPlaneIntegration.Runtime
{
    [AddComponentMenu("X-Plane Integration/Runtime/X-Plane Remote Telemetry Bridge")]
    public class XPlaneRemoteTelemetryBridge : MonoBehaviour
    {
        [Header("Remote Relay Connection")]
        [SerializeField] private string relayHost = "127.0.0.1";
        [SerializeField] private int relayPort = 37211;
        [SerializeField] private bool autoConnectOnStart = true;
        [SerializeField] private bool autoReconnect = true;
        [SerializeField] private float reconnectDelaySeconds = 2f;

        [Header("Targets")]
        [SerializeField] private AircraftController aircraftController;
        [SerializeField] private TrafficRadarDataManager trafficRadarDataManager;
        [SerializeField] private TrafficRadarController trafficRadarController;
        [SerializeField] private WeatherRadarProviderBase weatherRadarProvider;

        [Header("Integration")]
        [SerializeField] private bool disableUserControlWhenConnected = true;
        [SerializeField] private bool applyOwnshipToAircraftController = true;
        [SerializeField] private bool applyTrafficToRadar = true;
        [SerializeField] private bool applyWeatherPositionToRadar = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        private readonly ConcurrentQueue<XPlaneRemoteTelemetrySnapshot> _snapshotQueue = new ConcurrentQueue<XPlaneRemoteTelemetrySnapshot>();
        private readonly object _stateLock = new object();

        private TcpClient _client;
        private Thread _readerThread;
        private CancellationTokenSource _cts;
        private bool _connectRequested;
        private bool _disposed;
        private bool _suppressedTrafficFetching;
        private bool _trafficWasFetchingBeforeRemote;
        private bool _suppressedUserControl;
        private bool _userControlWasEnabledBeforeRemote;

        public XPlaneRemoteConnectionState CurrentState { get; private set; } = XPlaneRemoteConnectionState.Disconnected;
        public string LastError { get; private set; } = string.Empty;
        public float LastSnapshotTime { get; private set; }
        public XPlaneRemoteTelemetrySnapshot LatestSnapshot { get; private set; }

        public event Action<XPlaneRemoteConnectionState> OnConnectionStateChanged;
        public event Action<XPlaneRemoteTelemetrySnapshot> OnSnapshotApplied;
        public event Action<string> OnError;

        private void Awake()
        {
            FindDependencies();
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
            ProcessPendingSnapshots();
        }

        private void OnDestroy()
        {
            DisposeResources();
        }

        public void Connect()
        {
            if (_disposed)
            {
                return;
            }

            lock (_stateLock)
            {
                if (_readerThread != null && _readerThread.IsAlive)
                {
                    return;
                }

                _connectRequested = true;
                LastError = string.Empty;
                _cts = new CancellationTokenSource();
                _readerThread = new Thread(ReadLoop)
                {
                    Name = "XPlaneRemoteTelemetryBridge",
                    IsBackground = true
                };
                _readerThread.Start(_cts.Token);
                SetState(XPlaneRemoteConnectionState.Connecting);
            }
        }

        public void Disconnect()
        {
            _connectRequested = false;
            RestoreLocalIntegrationState();
            CancelReader();
            CloseClient();
            SetState(XPlaneRemoteConnectionState.Disconnected);
        }

        public void ProcessPendingSnapshots()
        {
            while (_snapshotQueue.TryDequeue(out XPlaneRemoteTelemetrySnapshot snapshot))
            {
                ApplySnapshot(snapshot);
            }
        }

        public void SetAircraftController(AircraftController controller)
        {
            aircraftController = controller;
        }

        public void SetTrafficRadarDataManager(TrafficRadarDataManager manager)
        {
            trafficRadarDataManager = manager;
        }

        public void SetTrafficRadarController(TrafficRadarController controller)
        {
            trafficRadarController = controller;
        }

        public void SetWeatherRadarProvider(WeatherRadarProviderBase provider)
        {
            weatherRadarProvider = provider;
        }

        private void FindDependencies()
        {
            if (aircraftController == null)
            {
                aircraftController = FindFirstObjectByType<AircraftController>();
            }

            if (trafficRadarDataManager == null)
            {
                trafficRadarDataManager = FindFirstObjectByType<TrafficRadarDataManager>();
            }

            if (trafficRadarController == null)
            {
                trafficRadarController = FindFirstObjectByType<TrafficRadarController>();
            }

            if (weatherRadarProvider == null)
            {
                weatherRadarProvider = FindFirstObjectByType<WeatherRadarProviderBase>();
            }
        }

        private void ReadLoop(object tokenObj)
        {
            var token = (CancellationToken)tokenObj;

            while (!token.IsCancellationRequested && _connectRequested)
            {
                try
                {
                    using (var client = new TcpClient())
                    {
                        client.NoDelay = true;
                        client.Connect(relayHost, relayPort);
                        lock (_stateLock)
                        {
                            _client = client;
                        }

                        SetState(XPlaneRemoteConnectionState.Connected);

                        using (NetworkStream stream = client.GetStream())
                        using (var reader = new StreamReader(stream))
                        {
                            while (!token.IsCancellationRequested)
                            {
                                string line = reader.ReadLine();
                                if (line == null)
                                {
                                    break;
                                }

                                if (string.IsNullOrWhiteSpace(line))
                                {
                                    continue;
                                }

                                XPlaneRemoteTelemetrySnapshot snapshot = JsonConvert.DeserializeObject<XPlaneRemoteTelemetrySnapshot>(line);
                                if (snapshot != null)
                                {
                                    _snapshotQueue.Enqueue(snapshot);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (IsExpectedShutdownException(ex, token))
                    {
                        break;
                    }

                    LastError = ex.Message;
                    SafeInvokeError($"Remote relay connection failed: {ex.Message}");
                    SetState(XPlaneRemoteConnectionState.Error);
                }
                finally
                {
                    CloseClient();
                }

                if (!autoReconnect || token.IsCancellationRequested || !_connectRequested)
                {
                    break;
                }

                Thread.Sleep(TimeSpan.FromSeconds(reconnectDelaySeconds));
                if (!token.IsCancellationRequested)
                {
                    SetState(XPlaneRemoteConnectionState.Connecting);
                }
            }

            if (!token.IsCancellationRequested)
            {
                RestoreLocalIntegrationState();
                SetState(XPlaneRemoteConnectionState.Disconnected);
            }
        }

        private void ApplySnapshot(XPlaneRemoteTelemetrySnapshot snapshot)
        {
            LatestSnapshot = snapshot;
            LastSnapshotTime = Time.time;

            if (snapshot?.Ownship == null)
            {
                return;
            }

            if (disableUserControlWhenConnected && aircraftController != null)
            {
                if (!_suppressedUserControl)
                {
                    _userControlWasEnabledBeforeRemote = aircraftController.IsUserControlled;
                    _suppressedUserControl = true;
                }

                aircraftController.SetUserControlled(false);
            }

            if (applyOwnshipToAircraftController)
            {
                ApplyOwnshipToAircraftController(snapshot);
            }

            if (applyTrafficToRadar)
            {
                ApplyTraffic(snapshot);
            }

            if (applyWeatherPositionToRadar)
            {
                ApplyWeatherPosition(snapshot);
            }

            if (verboseLogging)
            {
                Debug.Log($"[XPlaneRemoteTelemetryBridge] Applied snapshot {snapshot.TimestampUtc} mode={snapshot.SourceMode} traffic={snapshot.Traffic?.Count ?? 0}");
            }

            OnSnapshotApplied?.Invoke(snapshot);
        }

        private void ApplyOwnshipToAircraftController(XPlaneRemoteTelemetrySnapshot snapshot)
        {
            if (aircraftController == null || snapshot?.Ownship == null)
            {
                return;
            }

            XPlaneRemoteOwnshipState ownship = snapshot.Ownship;
            if (aircraftController.State == null)
            {
                aircraftController.ResetToDefault();
            }

            aircraftController.SetPosition(ownship.Latitude, ownship.Longitude, ownship.AltitudeMeters, ownship.HeadingDeg);

            AircraftRuntimeState state = aircraftController.State;
            if (state == null)
            {
                return;
            }

            state.Pitch = ownship.PitchDeg;
            state.Roll = ownship.RollDeg;
            state.Heading = ownship.HeadingDeg;
            state.IndicatedAirspeedKnots = ownship.IndicatedAirspeedKt;
            state.TrueAirspeedKnots = ownship.TrueAirspeedKt;
            state.GroundSpeedKnots = ownship.GroundSpeedKt;
            state.VerticalSpeedFpm = ownship.VerticalSpeedFpm;
            state.AutopilotEngaged = ownship.AutopilotEngaged;
            state.AutopilotMode = ownship.AutopilotMode;
            state.GearDown = ownship.GearDown;
            state.IsOnGround = ownship.OnGround;
            state.ThrottlePercent = ownship.ThrottleRatio * 100f;
            state.ElevatorInput = ownship.ElevatorInput;
            state.AileronInput = ownship.AileronInput;
            state.RudderInput = ownship.RudderInput;
            state.FlapsRatio = ownship.FlapsRatio;
            state.SpeedbrakeRatio = ownship.SpeedbrakeRatio;
            state.ParkingBrakeRatio = ownship.ParkingBrakeRatio;

            aircraftController.SetThrottle(ownship.ThrottleRatio);
            aircraftController.SetPitch(ownship.ElevatorInput);
            aircraftController.SetRoll(ownship.AileronInput);
        }

        private void ApplyTraffic(XPlaneRemoteTelemetrySnapshot snapshot)
        {
            if (trafficRadarDataManager == null)
            {
                return;
            }

            if (!_suppressedTrafficFetching)
            {
                _trafficWasFetchingBeforeRemote = trafficRadarDataManager.IsActive;
                if (_trafficWasFetchingBeforeRemote)
                {
                    trafficRadarDataManager.StopFetching();
                }

                _suppressedTrafficFetching = true;
            }

            trafficRadarDataManager.aircraftMap.Clear();
            trafficRadarDataManager.aircraftList.Clear();

            var aircraftDataList = new List<TrafficRadarDataManager.AircraftData>();
            if (snapshot.Traffic != null)
            {
                foreach (XPlaneRemoteTrafficTarget target in snapshot.Traffic)
                {
                    var aircraftData = new TrafficRadarDataManager.AircraftData
                    {
                        icao24 = string.IsNullOrWhiteSpace(target.Icao24) ? Guid.NewGuid().ToString("N") : target.Icao24.ToLowerInvariant(),
                        callsign = target.Callsign ?? string.Empty,
                        originCountry = string.Empty,
                        latitude = (float)target.Latitude,
                        longitude = (float)target.Longitude,
                        altitude = target.AltitudeMeters,
                        velocity = target.VelocityMps,
                        heading = target.HeadingDeg,
                        verticalRate = target.VerticalRateMps,
                        onGround = target.OnGround,
                        lastUpdateTime = ParseTimestamp(snapshot.TimestampUtc),
                        type = TrafficRadarDataManager.AircraftType.Unknown
                    };

                    trafficRadarDataManager.aircraftMap[aircraftData.icao24] = aircraftData;
                    trafficRadarDataManager.aircraftList.Add(aircraftData);
                    aircraftDataList.Add(aircraftData);
                }
            }

            if (snapshot.Ownship != null)
            {
                trafficRadarDataManager.SetReferencePosition((float)snapshot.Ownship.Latitude, (float)snapshot.Ownship.Longitude);
                if (trafficRadarController != null)
                {
                    trafficRadarController.SetOwnPosition(
                        snapshot.Ownship.Latitude,
                        snapshot.Ownship.Longitude,
                        snapshot.Ownship.AltitudeMeters,
                        snapshot.Ownship.HeadingDeg);
                }
            }

            trafficRadarDataManager.onDataUpdated?.Invoke(aircraftDataList);
        }

        private void ApplyWeatherPosition(XPlaneRemoteTelemetrySnapshot snapshot)
        {
            if (weatherRadarProvider == null || snapshot?.Ownship == null)
            {
                return;
            }

            weatherRadarProvider.SetAircraftPosition(
                snapshot.Ownship.AltitudeMeters * 3.28084f,
                (float)snapshot.Ownship.Latitude,
                (float)snapshot.Ownship.Longitude,
                snapshot.Ownship.HeadingDeg);
        }

        private DateTime ParseTimestamp(string timestampUtc)
        {
            if (DateTime.TryParse(timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime parsed))
            {
                return parsed;
            }

            return DateTime.UtcNow;
        }

        private void SetState(XPlaneRemoteConnectionState newState)
        {
            if (CurrentState == newState)
            {
                return;
            }

            CurrentState = newState;
            OnConnectionStateChanged?.Invoke(newState);
        }

        private void SafeInvokeError(string error)
        {
            Debug.LogError($"[XPlaneRemoteTelemetryBridge] {error}");
            OnError?.Invoke(error);
        }

        private bool IsExpectedShutdownException(Exception ex, CancellationToken token)
        {
            if (token.IsCancellationRequested || !_connectRequested || _disposed)
            {
                return true;
            }

            string message = ex?.ToString() ?? string.Empty;
            return message.IndexOf("Thread was being aborted", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void CloseClient()
        {
            lock (_stateLock)
            {
                TcpClient localClient = _client;
                _client = null;
                localClient?.Close();
            }
        }

        private void CancelReader()
        {
            CancellationTokenSource cts = Interlocked.Exchange(ref _cts, null);
            cts?.Cancel();
            cts?.Dispose();
        }

        private void RestoreLocalIntegrationState()
        {
            if (_suppressedTrafficFetching && trafficRadarDataManager != null)
            {
                if (_trafficWasFetchingBeforeRemote && !trafficRadarDataManager.IsActive)
                {
                    trafficRadarDataManager.StartFetching();
                }

                _suppressedTrafficFetching = false;
                _trafficWasFetchingBeforeRemote = false;
            }

            if (_suppressedUserControl && aircraftController != null)
            {
                aircraftController.SetUserControlled(_userControlWasEnabledBeforeRemote);
                _suppressedUserControl = false;
                _userControlWasEnabledBeforeRemote = false;
            }
        }

        private void DisposeResources()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _connectRequested = false;
            RestoreLocalIntegrationState();
            CancelReader();
            CloseClient();

            Thread localThread = _readerThread;
            _readerThread = null;
            if (localThread != null && localThread.IsAlive)
            {
                localThread.Join(1000);
            }
        }
    }

    public enum XPlaneRemoteConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Error
    }

    [Serializable]
    public class XPlaneRemoteTelemetrySnapshot
    {
        [JsonProperty("timestamp_utc")]
        public string TimestampUtc;

        [JsonProperty("source_mode")]
        public string SourceMode;

        [JsonProperty("ownship")]
        public XPlaneRemoteOwnshipState Ownship;

        [JsonProperty("weather")]
        public XPlaneRemoteWeatherState Weather;

        [JsonProperty("traffic")]
        public List<XPlaneRemoteTrafficTarget> Traffic = new List<XPlaneRemoteTrafficTarget>();

        [JsonProperty("raw")]
        public Dictionary<string, float> Raw = new Dictionary<string, float>();

        [JsonProperty("automation")]
        public XPlaneRemoteAutomationState Automation;
    }

    [Serializable]
    public class XPlaneRemoteOwnshipState
    {
        [JsonProperty("latitude")]
        public double Latitude;

        [JsonProperty("longitude")]
        public double Longitude;

        [JsonProperty("altitude_m")]
        public float AltitudeMeters;

        [JsonProperty("altitude_agl_m")]
        public float AltitudeAglMeters;

        [JsonProperty("pitch_deg")]
        public float PitchDeg;

        [JsonProperty("roll_deg")]
        public float RollDeg;

        [JsonProperty("heading_deg")]
        public float HeadingDeg;

        [JsonProperty("track_deg")]
        public float TrackDeg;

        [JsonProperty("flight_path_angle_deg")]
        public float FlightPathAngleDeg;

        [JsonProperty("slip_skid")]
        public float SlipSkid;

        [JsonProperty("indicated_airspeed_kt")]
        public float IndicatedAirspeedKt;

        [JsonProperty("true_airspeed_kt")]
        public float TrueAirspeedKt;

        [JsonProperty("ground_speed_kt")]
        public float GroundSpeedKt;

        [JsonProperty("vertical_speed_fpm")]
        public float VerticalSpeedFpm;

        [JsonProperty("autopilot_engaged")]
        public bool AutopilotEngaged;

        [JsonProperty("autopilot_mode")]
        public int AutopilotMode;

        [JsonProperty("gear_down")]
        public bool GearDown;

        [JsonProperty("on_ground")]
        public bool OnGround;

        [JsonProperty("gps_valid")]
        public bool GpsValid = true;

        [JsonProperty("ils_valid")]
        public bool IlsValid;

        [JsonProperty("throttle_ratio")]
        public float ThrottleRatio;

        [JsonProperty("elevator_input")]
        public float ElevatorInput;

        [JsonProperty("aileron_input")]
        public float AileronInput;

        [JsonProperty("rudder_input")]
        public float RudderInput;

        [JsonProperty("flaps_ratio")]
        public float FlapsRatio;

        [JsonProperty("speedbrake_ratio")]
        public float SpeedbrakeRatio;

        [JsonProperty("parking_brake_ratio")]
        public float ParkingBrakeRatio;
    }

    [Serializable]
    public class XPlaneRemoteWeatherState
    {
        [JsonProperty("wind_speed_kt")]
        public float WindSpeedKt;

        [JsonProperty("wind_direction_deg")]
        public float WindDirectionDeg;

        [JsonProperty("barometer_inhg")]
        public float BarometerInHg;

        [JsonProperty("temperature_c")]
        public float TemperatureC;

        [JsonProperty("visibility_m")]
        public float VisibilityM;

        [JsonProperty("cloud_base_m")]
        public float CloudBaseM;
    }

    [Serializable]
    public class XPlaneRemoteTrafficTarget
    {
        [JsonProperty("icao24")]
        public string Icao24;

        [JsonProperty("callsign")]
        public string Callsign;

        [JsonProperty("latitude")]
        public double Latitude;

        [JsonProperty("longitude")]
        public double Longitude;

        [JsonProperty("altitude_m")]
        public float AltitudeMeters;

        [JsonProperty("heading_deg")]
        public float HeadingDeg;

        [JsonProperty("velocity_mps")]
        public float VelocityMps;

        [JsonProperty("vertical_rate_mps")]
        public float VerticalRateMps;

        [JsonProperty("on_ground")]
        public bool OnGround;
    }

    [Serializable]
    public class XPlaneRemoteAutomationState
    {
        [JsonProperty("controller")]
        public string Controller;

        [JsonProperty("mode")]
        public string Mode;

        [JsonProperty("recovery_active")]
        public bool RecoveryActive;

        [JsonProperty("target_altitude_m")]
        public float TargetAltitudeMeters;

        [JsonProperty("target_heading_deg")]
        public float TargetHeadingDeg;

        [JsonProperty("target_speed_kt")]
        public float TargetSpeedKt;
    }
}
