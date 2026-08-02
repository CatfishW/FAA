using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;

using UnityEngine;

#pragma warning disable CS0414, CS0649 // Windows-only Archer bridge fields are used only in Windows builds.

public class ArcherInterface : MonoBehaviour
{
    public GameObject SAPrefab;

    static IntPtr archerReader;
    public static Vector3 InitialPosition { set; get; }
    public static Vector3 InitialRotation { set; get; }

    private double ticksPerMS;

    private static UdpClient udpForwarder;
    private static IPAddress SendToAddress = IPAddress.Parse("10.6.0.233");
    private static int SendToPort = 30255;
    private static uint Frame = 0;
    private static uint Magic = 0x48435241;
    private static byte Version = 1;
    private static byte Packet_Type = 0;
    //private static uint Header_Size = (2 * sizeof(uint)) + (2 * sizeof(ushort));
    private static uint Payload_Size = sizeof(long) + sizeof(uint) + (6 * sizeof(float));

    private readonly object forwardLock = new object();
    private AutoResetEvent forwardSignal;
    private Thread forwardThread;
    private volatile bool forwardWorkerRunning;
    private ArcherPose pendingPose;
    private bool shutdownComplete;

    private static float Deg2Rad(float x)
    {
        return x * MathF.PI / 180f;
    }

    [DllImport("ArcherCSharpInterface.dll", CharSet = CharSet.Ansi)]
    public static extern IntPtr CreateArcherHeadTracker();

    [DllImport("ArcherCSharpInterface.dll", CharSet = CharSet.Ansi)]
    public static extern void DestroyArcherHeadTracker(IntPtr archer);

    [DllImport("ArcherCSharpInterface.dll", CharSet = CharSet.Ansi)]
    public static extern bool IsArcherRunning(IntPtr archer);

    [DllImport("ArcherCSharpInterface.dll", CharSet = CharSet.Ansi)]
    public static extern void StartArcher(IntPtr archer,[MarshalAs(UnmanagedType.LPStr)]string portName, int baudRate);

    [DllImport("ArcherCSharpInterface.dll", CharSet = CharSet.Ansi)]
    public static extern void StopArcher(IntPtr archer);

    //[DllImport("ArcherCSharpInterface.dll", CharSet = CharSet.Ansi)]
    //public static extern IntPtr GetDataPointer();

    [DllImport("ArcherCSharpInterface.dll", CharSet = CharSet.Ansi)]
    public static extern IntPtr GetArcherState(IntPtr archer);

    [DllImport("ArcherCSharpInterface.dll", CharSet = CharSet.Ansi)]
    public static extern bool UpdateArcher(IntPtr archer);

    [DllImport("ArcherCSharpInterface.dll", CharSet = CharSet.Ansi)]
    public static extern int GetDefaultBaudRate(IntPtr archer);

    [DllImport("Kernel32.dll")]
    private static extern bool QueryPerformanceFrequency(out long lpFrequency);

    [StructLayout (LayoutKind.Sequential, Pack = sizeof(float))]
    class ArcherPose
    {
        public float PosZ;
        public float PosX;
        public float PosY;
        public float RotRoll;
        public float RotEl;
        public float RotAz;
        public ulong ReadTime;
        public ulong CalledTime;
    }

    static float Rad2Deg(double deg)
    {
        return (float)(deg * 180.0 / Math.PI);
    }
    // Start is called before the first frame update
    void Start()
    {
#if !UNITY_STANDALONE_WIN && !UNITY_EDITOR_WIN
        Debug.LogWarning("[SA147 Archer] Archer head tracker bridge is Windows-only; tracker disabled on this platform.", this);
        enabled = false;
        return;
#else
        try
        {
            if (SAPrefab == null)
            {
                throw new InvalidOperationException("SA-147 rig reference is not assigned.");
            }

            archerReader = CreateArcherHeadTracker();
            int baud = GetDefaultBaudRate(archerReader);
            StartArcher(archerReader, "COM6", baud);
            InitialPosition = SAPrefab.transform.localPosition;
            InitialRotation = SAPrefab.transform.localRotation.eulerAngles;
            long freq = 0;
            if (QueryPerformanceFrequency(out freq))
            {
                ticksPerMS = freq / 1000.0;
            }
            else ticksPerMS = 1.0;

            Application.onBeforeRender += onBeforeRender;
            udpForwarder = new UdpClient();
            udpForwarder.Connect(SendToAddress, SendToPort);
            StartForwardWorker();
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to start Archer Interface");
            Debug.LogException(ex);
        }
#endif
    }

    // Update is called once per frame
    void onBeforeRender()
    {
        if (archerReader != IntPtr.Zero && IsArcherRunning(archerReader))
        {
            var pose = new ArcherPose();
            
            Marshal.PtrToStructure(GetArcherState(archerReader), pose);
            SAPrefab.transform.localPosition = new Vector3(InitialPosition.x + pose.PosX,
                                                      InitialPosition.y + pose.PosY,
                                                      InitialPosition.z + pose.PosZ);
            SAPrefab.transform.localRotation = Quaternion.Euler(InitialRotation.x - (pose.RotEl),
                                                           InitialRotation.y + (pose.RotAz),
                                                           InitialRotation.z - (pose.RotRoll));

            QueueArcherState(pose);

            //Debug.Log($"ReadTime: {pose.ReadTime / ticksPerMS} - CalledTime: {pose.CalledTime / ticksPerMS} - Difference: {(pose.CalledTime - pose.ReadTime) / ticksPerMS}");
            //transform.rotation = Quaternion.AngleAxis(InitialRotation.y + Rad2Deg(pose.RotAz), Vector3.up) * 
            //                     Quaternion.AngleAxis(InitialRotation.x - Rad2Deg(pose.RotEl), Vector3.right) *
            //                     Quaternion.AngleAxis(InitialRotation.z - Rad2Deg(pose.RotRoll), Vector3.forward);
            //transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            //transform.Rotate(InitialRotation.x - Rad2Deg(pose.RotEl),
            //                 InitialRotation.y + Rad2Deg(pose.RotAz),
            //                 InitialRotation.z - Rad2Deg(pose.RotRoll),
            //                 Space.Self);
            //transform.RotateAround()
            //Debug.Log($"pose = Az:{Rad2Deg(pose.RotAz)}, El:{Rad2Deg(pose.RotEl)}, Roll:{Rad2Deg(pose.RotRoll)}");
            //Debug.Log($"Position = x:{pose.PosX}, y:{pose.PosY}, z:{pose.PosZ}");
            //var azobj = GameObject.FindGameObjectWithTag("AZ");
            //var elobj = GameObject.FindGameObjectWithTag("EL");
            //var rollobj = GameObject.FindGameObjectWithTag("Roll");


            //if (azobj is not null && azobj.active)
            //{
            //    azobj.GetComponent<TMPro.TextMeshProUGUI>().text = $"AZ: {Rad2Deg(pose.RotAz)}";
            //    elobj.GetComponent<TMPro.TextMeshProUGUI>().text = $"EL: {Rad2Deg(pose.RotEl)}";
            //    rollobj.GetComponent<TMPro.TextMeshProUGUI>().text = $"Roll: {Rad2Deg(pose.RotRoll)}";
            //}

        }
    }

    static void ForwardArcherState(ArcherPose pose)
    {
        if (udpForwarder != null)
        {
            try
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                long unixTimeMilliseconds = now.ToUnixTimeMilliseconds();
                var Data = new List<byte>();
                Data.AddRange(BitConverter.GetBytes(Magic));
                Data.Add(Version);
                Data.Add(Packet_Type);
                Data.AddRange(BitConverter.GetBytes(Payload_Size));
                Data.AddRange(BitConverter.GetBytes(unixTimeMilliseconds));
                Data.AddRange(BitConverter.GetBytes(Frame++));
                Data.AddRange(BitConverter.GetBytes(pose.PosX));
                Data.AddRange(BitConverter.GetBytes(pose.PosY));
                Data.AddRange(BitConverter.GetBytes(pose.PosZ));
                Data.AddRange(BitConverter.GetBytes(Deg2Rad(pose.RotRoll)));
                Data.AddRange(BitConverter.GetBytes(Deg2Rad(pose.RotEl)));
                Data.AddRange(BitConverter.GetBytes(Deg2Rad(pose.RotAz)));
                udpForwarder.Send(Data.ToArray(), Data.Count);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    private void StartForwardWorker()
    {
        forwardSignal = new AutoResetEvent(false);
        forwardWorkerRunning = true;
        forwardThread = new Thread(ForwardWorkerLoop)
        {
            IsBackground = true,
            Name = "FAA SA-147 Archer UDP Forwarder"
        };
        forwardThread.Start();
    }

    private void QueueArcherState(ArcherPose pose)
    {
        lock (forwardLock)
        {
            // Keep only the newest pose. Head tracking must remain low-latency;
            // building a backlog is worse than dropping an intermediate packet.
            pendingPose = pose;
        }

        forwardSignal?.Set();
    }

    private void ForwardWorkerLoop()
    {
        while (forwardWorkerRunning)
        {
            forwardSignal?.WaitOne(100);
            if (!forwardWorkerRunning)
            {
                break;
            }

            ArcherPose pose;
            lock (forwardLock)
            {
                pose = pendingPose;
                pendingPose = null;
            }

            if (pose != null)
            {
                ForwardArcherState(pose);
            }
        }
    }

    private void OnApplicationQuit()
    {
        Shutdown();
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private void Shutdown()
    {
        if (shutdownComplete)
        {
            return;
        }

        shutdownComplete = true;
        Application.onBeforeRender -= onBeforeRender;
        forwardWorkerRunning = false;
        forwardSignal?.Set();
        if (forwardThread != null && forwardThread.IsAlive)
        {
            forwardThread.Join(250);
        }

        forwardThread = null;
        forwardSignal?.Dispose();
        forwardSignal = null;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (archerReader == IntPtr.Zero)
        {
            CloseForwarder();
            return;
        }

        if (IsArcherRunning(archerReader))
        {
            StopArcher(archerReader);
        }

        if (archerReader != IntPtr.Zero)
        {
            DestroyArcherHeadTracker(archerReader);
            archerReader = IntPtr.Zero;
        }

        CloseForwarder();
#endif
    }

    private static void CloseForwarder()
    {
        if (udpForwarder is null)
        {
            return;
        }

        udpForwarder.Close();
        udpForwarder.Dispose();
        udpForwarder = null;
    }



}
