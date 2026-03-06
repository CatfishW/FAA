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

        /// <summary>
        /// X-Plane IP address. Default: 127.0.0.1 (localhost)
        /// </summary>
        public string XPlaneIp { get; set; } = "127.0.0.1";

        /// <summary>
        /// UDP port for listening to DATA packets. Default: 49009
        /// </summary>
        public int UdpPort { get; set; } = 49009;

        /// <summary>
        /// List of DataRefs to request from X-Plane.
        /// </summary>
        public List<string> RequestedDataRefs { get; } = new List<string>();

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
            if (State != newState)
            {
                State = newState;
                OnConnectionStateChanged?.Invoke(newState);
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

            if (IsConnected)
            {
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(ip))
                {
                    XPlaneIp = ip;
                }

                _udpClient = new UdpClient(UdpPort);
                _udpClient.EnableBroadcast = true;
                _udpClient.Client.ReceiveTimeout = 1000;

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
                OnError?.Invoke($"Failed to create UDP socket: {ex.Message}");
                Debug.LogError($"[XPlaneUdpListener] Socket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                SetState(ConnectionState.Error);
                OnError?.Invoke($"Connection failed: {ex.Message}");
                Debug.LogError($"[XPlaneUdpListener] Connection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Disconnects from X-Plane and stops listening.
        /// </summary>
        public void Disconnect()
        {
            if (!IsConnected && _udpClient == null)
            {
                return;
            }

            try
            {
                _cancellationTokenSource?.Cancel();
                _listenThread?.Join(2000);

                _udpClient?.Close();
                _udpClient?.Dispose();

                _udpClient = null;
                _listenThread = null;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                while (_dataQueue.TryDequeue(out _)) { }

                SetState(ConnectionState.Disconnected);
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
                byte[] rrefCommand = BuildRrefCommand(dataRef, frequency);
                var endPoint = new IPEndPoint(IPAddress.Parse(XPlaneIp), 49009);
                _udpClient.Send(rrefCommand, rrefCommand.Length, endPoint);

                if (frequency > 0 && !RequestedDataRefs.Contains(dataRef))
                {
                    RequestedDataRefs.Add(dataRef);
                }
                else if (frequency == 0)
                {
                    RequestedDataRefs.Remove(dataRef);
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
                OnDataReceived?.Invoke(data);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Main UDP listening loop. Runs on separate thread.
        /// </summary>
        private void ListenLoop(object tokenObj)
        {
            var token = (CancellationToken)tokenObj;

            try
            {
                var endPoint = new IPEndPoint(IPAddress.Any, UdpPort);

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        byte[] data = _udpClient.Receive(ref endPoint);

                        if (data != null && data.Length > 0)
                        {
                            var parsedData = ParseDataPacket(data);
                            if (parsedData != null && parsedData.Count > 0)
                            {
                                _dataQueue.Enqueue(parsedData);
                            }
                        }
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode == SocketError.TimedOut)
                        {
                            continue;
                        }
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }
                        Debug.LogError($"[XPlaneUdpListener] Socket error during receive: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }
                        Debug.LogError($"[XPlaneUdpListener] Error in listen loop: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XPlaneUdpListener] Listen loop terminated: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds an RREF command packet for X-Plane.
        /// RREF format: "RREF&lt;dataRef&gt;\0" + frequency (4 bytes, little-endian float)
        /// </summary>
        /// <param name="dataRef">DataRef path</param>
        /// <param name="frequency">Update frequency in Hz</param>
        /// <returns>Byte array RREF command</returns>
        private static byte[] BuildRrefCommand(string dataRef, int frequency)
        {
            var commandBuilder = new List<byte>();

            commandBuilder.AddRange(Encoding.ASCII.GetBytes("RREF"));
            commandBuilder.AddRange(Encoding.ASCII.GetBytes(dataRef));
            commandBuilder.Add(0);
            commandBuilder.AddRange(BitConverter.GetBytes((float)frequency));

            return commandBuilder.ToArray();
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
                if (data[0] != 'D' || data[1] != 'A' || data[2] != 'T' || data[3] != 'A')
                {
                    return null;
                }

                var result = new Dictionary<string, float>();
                int offset = 0;

                while (offset + 28 <= data.Length)
                {
                    string header = Encoding.ASCII.GetString(data, offset, 4);
                    if (header != "DATA")
                    {
                        break;
                    }
                    offset += 4;

                    int dataRefIndex = BitConverter.ToInt32(data, offset);
                    offset += 4;

                    float value = BitConverter.ToSingle(data, offset);
                    offset += 20;

                    result[$"dataref_{dataRefIndex}"] = value;
                }

                return result.Count > 0 ? result : null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XPlaneUdpListener] Parse error: {ex.Message}");
                return null;
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
                _cancellationTokenSource?.Dispose();
                _udpClient?.Dispose();
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
