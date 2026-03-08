using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace FAA.XPlaneIntegration
{
    /// <summary>
    /// Core UDP communication service for X-Plane integration.
    /// Handles RREF command sending and DATA packet parsing.
    /// Thread-safe for Unity main thread consumption.
    /// </summary>
    public class XPlaneUdpListener : IDisposable
    {
        #region Events

        /// <summary>
        /// Fired when parsed data is received. Queued for main thread safety.
        /// </summary>
        public event Action<Dictionary<string, float>> OnDataReceived;

        /// <summary>
        /// Fired when connection state changes.
        /// </summary>
        public event Action<ConnectionState> OnConnectionStateChanged;

        /// <summary>
        /// Fired when an error occurs.
        /// </summary>
        public event Action<string> OnError;

        #endregion

        #region Configuration

        private const int MaxQueueSize = 100;
        private const int ReceiveTimeoutMs = 1000;
        private const int DefaultReconnectDelayMs = 1000;
        private const int DefaultMaxReconnectAttempts = 5;
        private const int XPlaneCommandPort = 49000;
        private const int XPlaneDataRefFieldLength = 400;

        /// <summary>
        /// X-Plane IP address. Default: 127.0.0.1 (localhost)
        /// </summary>
        public string XPlaneIp { get; set; } = "127.0.0.1";

        /// <summary>
        /// UDP port for listening to DATA packets. Default: 49009
        /// </summary>
        public int UdpPort { get; set; } = 49009;

        /// <summary>
        /// Maximum reconnection attempts after connection loss. Default: 5
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = DefaultMaxReconnectAttempts;

        /// <summary>
        /// Delay between reconnection attempts in milliseconds. Default: 1000
        /// </summary>
        public int ReconnectDelayMs { get; set; } = DefaultReconnectDelayMs;

        /// <summary>
        /// List of DataRefs to request from X-Plane. Thread-safe.
        /// </summary>
        private readonly List<string> _requestedDataRefs = new List<string>();
        public IReadOnlyList<string> RequestedDataRefs => _requestedDataRefs.AsReadOnly();

        private readonly Dictionary<string, int> _dataRefToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> _indexToDataRef = new Dictionary<int, string>();
        private int _nextDataRefIndex;

        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(XPlaneIp))
            {
                throw new ArgumentException("XPlaneIp cannot be null or empty", nameof(XPlaneIp));
            }

            if (!IPAddress.TryParse(XPlaneIp, out _))
            {
                throw new ArgumentException($"Invalid IP address format: {XPlaneIp}", nameof(XPlaneIp));
            }

            if (UdpPort < 1 || UdpPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(UdpPort), UdpPort, "UDP port must be between 1 and 65535");
            }
        }

        #endregion

        #region Connection State

        /// <summary>
        /// Current connection state.
        /// </summary>
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        /// <summary>
        /// Returns true if connected and listening.
        /// </summary>
        public bool IsConnected => State == ConnectionState.Connected;

        private void SetState(ConnectionState newState)
        {
            lock (_stateLock)
            {
                if (State != newState)
                {
                    State = newState;
                }
            }
        }

        private void SafeInvokeError(string message)
        {
            try
            {
                OnError?.Invoke(message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XPlaneUdpListener] Error handler threw exception: {ex.Message}");
            }
        }

        private void SafeInvokeData(Dictionary<string, float> data)
        {
            try
            {
                OnDataReceived?.Invoke(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XPlaneUdpListener] Data handler threw exception: {ex.Message}");
            }
        }

        private void SafeInvokeState(ConnectionState state)
        {
            try
            {
                OnConnectionStateChanged?.Invoke(state);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XPlaneUdpListener] State handler threw exception: {ex.Message}");
            }
        }

        #endregion

        #region Private Fields

        private UdpClient _udpClient;
        private Thread _listenThread;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _disposed;

        /// <summary>
        /// Thread-safe queue for data received on UDP thread, consumed on main thread.
        /// </summary>
        private readonly ConcurrentQueue<Dictionary<string, float>> _dataQueue = new ConcurrentQueue<Dictionary<string, float>>();

        /// <summary>
        /// X-Plane expects RREF commands in little-endian format.
        /// </summary>
        private readonly object _lock = new object();

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new XPlaneUdpListener instance.
        /// </summary>
        public XPlaneUdpListener()
        {
        }

        /// <summary>
        /// Creates a new XPlaneUdpListener with specified IP and port.
        /// </summary>
        /// <param name="ip">X-Plane IP address</param>
        /// <param name="port">UDP port (default 49009)</param>
        public XPlaneUdpListener(string ip, int port = 49009)
        {
            XPlaneIp = ip;
            UdpPort = port;
        }

        #endregion

        #region Public Methods

        private int _connectAttempts;
        private readonly object _stateLock = new object();

        /// <summary>
        /// Connects to X-Plane and starts listening for DATA packets.
        /// </summary>
        /// <param name="ip">Optional IP override</param>
        public void Connect(string ip = null)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(XPlaneUdpListener));
            }

            lock (_stateLock)
            {
                if (IsConnected)
                {
                    return;
                }
            }

            try
            {
                if (!string.IsNullOrEmpty(ip))
                {
                    XPlaneIp = ip;
                }

                ValidateConfiguration();

                _udpClient = new UdpClient(UdpPort);
                _udpClient.EnableBroadcast = true;
                _udpClient.Client.ReceiveBufferSize = 524288;
                _udpClient.Client.ReceiveTimeout = ReceiveTimeoutMs;

                _connectAttempts = 0;
                _cancellationTokenSource = new CancellationTokenSource();
                _listenThread = new Thread(ListenLoop)
                {
                    Name = "XPlaneUdpListener",
                    IsBackground = true
                };
                _listenThread.Start(_cancellationTokenSource.Token);

                SetState(ConnectionState.Connected);
                Debug.Log($"[XPlaneUdpListener] Connected, listening on port {UdpPort}");
            }
            catch (SocketException ex)
            {
                SetState(ConnectionState.Error);
                SafeInvokeError($"Failed to create UDP socket: {ex.SocketErrorCode} - {ex.Message}");
                Debug.LogError($"[XPlaneUdpListener] Socket error: {ex.SocketErrorCode} - {ex.Message}");
            }
            catch (Exception ex)
            {
                SetState(ConnectionState.Error);
                SafeInvokeError($"Connection failed: {ex.Message}");
                Debug.LogError($"[XPlaneUdpListener] Connection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Disconnects from X-Plane and stops listening.
        /// </summary>
        public void Disconnect()
        {
            lock (_stateLock)
            {
                if (!IsConnected && _udpClient == null)
                {
                    return;
                }
            }

            try
            {
                var cts = _cancellationTokenSource;
                if (cts != null && !cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }

                _listenThread?.Join(2000);

                _udpClient?.Close();
                _udpClient?.Dispose();
                _udpClient = null;

                _listenThread = null;

                var localCts = Interlocked.Exchange(ref _cancellationTokenSource, null);
                localCts?.Dispose();

                while (_dataQueue.TryDequeue(out _)) { }
                lock (_requestedDataRefs)
                {
                    _requestedDataRefs.Clear();
                    _dataRefToIndex.Clear();
                    _indexToDataRef.Clear();
                    _nextDataRefIndex = 0;
                }

                SetState(ConnectionState.Disconnected);
                _connectAttempts = 0;
                Debug.Log("[XPlaneUdpListener] Disconnected");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XPlaneUdpListener] Disconnect error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends an RREF command to request a DataRef from X-Plane.
        /// X-Plane will respond with DATA packets at the specified frequency.
        /// </summary>
        /// <param name="dataRef">DataRef path (e.g., "sim/flightmodel/position/latitude")</param>
        /// <param name="frequency">Update frequency in Hz (0 to stop, 1-50 typical)</param>
        public void SendRrefRequest(string dataRef, int frequency)
        {
            if (!IsConnected || _udpClient == null)
            {
                Debug.LogWarning("[XPlaneUdpListener] Cannot send RREF: not connected");
                return;
            }

            try
            {
                int dataRefIndex = GetOrCreateDataRefIndex(dataRef, frequency);
                if (dataRefIndex < 0)
                {
                    return;
                }

                byte[] rrefCommand = BuildRrefCommand(dataRef, frequency, dataRefIndex);
                var endPoint = new IPEndPoint(IPAddress.Parse(XPlaneIp), XPlaneCommandPort);
                _udpClient.Send(rrefCommand, rrefCommand.Length, endPoint);

                lock (_requestedDataRefs)
                {
                    if (frequency > 0 && !_requestedDataRefs.Contains(dataRef))
                    {
                        _requestedDataRefs.Add(dataRef);
                    }
                    else if (frequency == 0)
                    {
                        _requestedDataRefs.Remove(dataRef);
                    }
                }

                Debug.Log($"[XPlaneUdpListener] RREF sent: {dataRef} @ {frequency}Hz");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"RREF send failed: {ex.Message}");
                Debug.LogError($"[XPlaneUdpListener] RREF error: {ex.Message}");
            }
        }

        /// <summary>
        /// Polls the data queue for new data. Call from Unity Update() on main thread.
        /// </summary>
        /// <returns>Dictionary of DataRef values, or null if no new data</returns>
        public Dictionary<string, float> PollData()
        {
            if (_dataQueue.TryDequeue(out var data))
            {
                return data;
            }
            return null;
        }

        /// <summary>
        /// Processes all queued data immediately, invoking OnDataReceived for each.
        /// Call from Unity Update() on main thread.
        /// </summary>
        public void ProcessQueuedData()
        {
            while (_dataQueue.TryDequeue(out var data))
            {
                SafeInvokeData(data);
            }
        }

        #endregion

        #region Private Methods

        private void ListenLoop(object tokenObj)
        {
            var token = (CancellationToken)tokenObj;
            int consecutiveTimeouts = 0;
            const int MaxTimeoutsBeforeReconnect = 30;

            try
            {
                var endPoint = new IPEndPoint(IPAddress.Any, UdpPort);

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        byte[] data = _udpClient.Receive(ref endPoint);
                        consecutiveTimeouts = 0;

                        if (data != null && data.Length > 0)
                        {
                            var parsedData = ParseDataPacket(data);
                            if (parsedData != null && parsedData.Count > 0)
                            {
                                if (_dataQueue.Count >= MaxQueueSize)
                                {
                                    while (_dataQueue.TryDequeue(out _)) { }
                                }
                                _dataQueue.Enqueue(parsedData);
                            }
                        }
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode == SocketError.TimedOut)
                        {
                            consecutiveTimeouts++;
                            if (consecutiveTimeouts >= MaxTimeoutsBeforeReconnect)
                            {
                                SetState(ConnectionState.Error);
                                SafeInvokeError($"X-Plane not responding for {consecutiveTimeouts} seconds");
                                
                                if (_connectAttempts < MaxReconnectAttempts)
                                {
                                    Thread.Sleep(ReconnectDelayMs);
                                    AttemptReconnect(ref endPoint, ref consecutiveTimeouts);
                                }
                                else
                                {
                                    Debug.LogError($"[XPlaneUdpListener] Max reconnection attempts reached");
                                    break;
                                }
                            }
                            continue;
                        }
                        
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }

                        SetState(ConnectionState.Error);
                        SafeInvokeError($"Socket error: {ex.SocketErrorCode} - {ex.Message}");
                        Debug.LogError($"[XPlaneUdpListener] Socket error: {ex.SocketErrorCode} - {ex.Message}");
                        
                        if (_connectAttempts < MaxReconnectAttempts)
                        {
                            Thread.Sleep(ReconnectDelayMs);
                            AttemptReconnect(ref endPoint, ref consecutiveTimeouts);
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }
                        
                        SafeInvokeError($"Listen loop error: {ex.Message}");
                        Debug.LogError($"[XPlaneUdpListener] Error in listen loop: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                SafeInvokeError($"Listen loop terminated: {ex.Message}");
                Debug.LogError($"[XPlaneUdpListener] Listen loop terminated: {ex.Message}");
            }
        }

        private void AttemptReconnect(ref IPEndPoint endPoint, ref int consecutiveTimeouts)
        {
            try
            {
                _connectAttempts++;
                Debug.Log($"[XPlaneUdpListener] Reconnection attempt {_connectAttempts}/{MaxReconnectAttempts}");

                _udpClient?.Close();
                _udpClient?.Dispose();
                _udpClient = null;

                _udpClient = new UdpClient(UdpPort);
                _udpClient.EnableBroadcast = true;
                _udpClient.Client.ReceiveBufferSize = 524288;
                _udpClient.Client.ReceiveTimeout = ReceiveTimeoutMs;

                endPoint = new IPEndPoint(IPAddress.Any, UdpPort);
                SetState(ConnectionState.Connected);
                SafeInvokeError($"Reconnected after {_connectAttempts} attempts");
                consecutiveTimeouts = 0;
            }
            catch (Exception ex)
            {
                SafeInvokeError($"Reconnection failed: {ex.Message}");
                Debug.LogError($"[XPlaneUdpListener] Reconnection failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds an RREF command packet for X-Plane.
        /// </summary>
        /// <param name="dataRef">DataRef path</param>
        /// <param name="frequency">Update frequency in Hz</param>
        /// <returns>Byte array RREF command</returns>
        private static byte[] BuildRrefCommand(string dataRef, int frequency, int dataRefIndex)
        {
            byte[] packet = new byte[5 + 4 + 4 + XPlaneDataRefFieldLength];
            Encoding.ASCII.GetBytes("RREF").CopyTo(packet, 0);
            packet[4] = 0;

            BitConverter.GetBytes(frequency).CopyTo(packet, 5);
            BitConverter.GetBytes(dataRefIndex).CopyTo(packet, 9);

            byte[] pathBytes = Encoding.ASCII.GetBytes(dataRef ?? string.Empty);
            int pathLength = Math.Min(pathBytes.Length, XPlaneDataRefFieldLength - 1);
            Array.Copy(pathBytes, 0, packet, 13, pathLength);

            return packet;
        }

        /// <summary>
        /// Parses a DATA packet from X-Plane.
        /// DATA format: "DATA&lt;index&gt;\0" + float[5] arrays (each array is one DataRef value + metadata)
        /// </summary>
        /// <param name="data">Raw UDP packet data</param>
        /// <returns>Dictionary mapping DataRef indices to values, or null if invalid</returns>
        private Dictionary<string, float> ParseDataPacket(byte[] data)
        {
            if (data == null || data.Length < 5)
            {
                return null;
            }

            try
            {
                if (data[0] == 'R' && data[1] == 'R' && data[2] == 'E' && data[3] == 'F')
                {
                    return ParseRrefPacket(data);
                }

                if (data[0] == 'D' && data[1] == 'A' && data[2] == 'T' && data[3] == 'A')
                {
                    return ParseDataPacketLegacy(data);
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XPlaneUdpListener] Parse error: {ex.Message}");
                return null;
            }
        }

        private Dictionary<string, float> ParseRrefPacket(byte[] data)
        {
            var result = new Dictionary<string, float>(StringComparer.Ordinal);

            for (int offset = 5; offset + 8 <= data.Length; offset += 8)
            {
                int dataRefIndex = BitConverter.ToInt32(data, offset);
                float value = BitConverter.ToSingle(data, offset + 4);

                if (TryGetDataRefByIndex(dataRefIndex, out string dataRefPath))
                {
                    result[dataRefPath] = value;
                }
                else
                {
                    result[$"dataref_{dataRefIndex}"] = value;
                }
            }

            return result.Count > 0 ? result : null;
        }

        private Dictionary<string, float> ParseDataPacketLegacy(byte[] data)
        {
            var result = new Dictionary<string, float>(StringComparer.Ordinal);
            int offset = 5;

            while (offset + 36 <= data.Length)
            {
                int dataSetIndex = BitConverter.ToInt32(data, offset);
                offset += 4;

                float firstValue = BitConverter.ToSingle(data, offset);
                offset += 32;

                result[$"dataref_{dataSetIndex}"] = firstValue;
            }

            return result.Count > 0 ? result : null;
        }

        private int GetOrCreateDataRefIndex(string dataRef, int frequency)
        {
            lock (_requestedDataRefs)
            {
                if (_dataRefToIndex.TryGetValue(dataRef, out int existingIndex))
                {
                    if (frequency == 0)
                    {
                        _dataRefToIndex.Remove(dataRef);
                        _indexToDataRef.Remove(existingIndex);
                    }
                    return existingIndex;
                }

                if (frequency == 0)
                {
                    return -1;
                }

                int newIndex = _nextDataRefIndex++;
                _dataRefToIndex[dataRef] = newIndex;
                _indexToDataRef[newIndex] = dataRef;
                return newIndex;
            }
        }

        private bool TryGetDataRefByIndex(int index, out string dataRefPath)
        {
            lock (_requestedDataRefs)
            {
                return _indexToDataRef.TryGetValue(index, out dataRefPath);
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes of the UDP listener and releases resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                Disconnect();
                var cts = _cancellationTokenSource;
                cts?.Dispose();
            }

            _disposed = true;
        }

        ~XPlaneUdpListener()
        {
            Dispose(false);
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Connection state enumeration.
        /// </summary>
        public enum ConnectionState
        {
            Disconnected,
            Connected,
            Error
        }

        #endregion
    }
}
