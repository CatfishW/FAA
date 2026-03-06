# X-Plane 11/12 UDP Output Protocol Specification

## Technical Specification for Flight Telemetry Streaming

**Document Version:** 1.0  
**Date:** March 6, 2026  
**Target Platform:** Unity C# UDP Listener  
**Latency Target:** <50ms

---

## 1. Overview

X-Plane 11 and 12 support two UDP telemetry output methods:

1. **RREF (Request DataRef)** - Modern, flexible subscription-based protocol for arbitrary DataRefs
2. **DATA (Legacy Output)** - Fixed-format broadcast packets for predefined data groups

Both protocols use **little-endian byte order** and **IEEE 754 single-precision floats** (4 bytes).

---

## 2. Network Configuration

### Port Assignments

| Direction | Port | Purpose |
|-----------|------|---------|
| **Incoming to X-Plane** | 49000 | RREF commands, DREF writes, CMND execution |
| **Outgoing from X-Plane** | User-configurable | DATA packets, RREF responses |
| **Common listener ports** | 49001-49010 | Community conventions (49009 frequently used) |

**Note:** Port 49009 is **not** an official X-Plane default—it is a community convention and the default in NASA's XPlaneConnect library. X-Plane sends responses to the **source IP/port** of incoming RREF requests.

### X-Plane Setup

1. Open X-Plane → **Settings** → **Net Connections** → **UDP Ports** tab
2. For DATA output: Set destination IP and port, enable desired data groups
3. For RREF: No configuration needed; X-Plane replies to requesting IP/port

---

## 3. RREF Protocol (Recommended)

### 3.1 Subscription Request Packet

**Sent to:** X-Plane IP, port 49000  
**Packet Size:** 413 bytes (fixed)

| Offset | Size | Type | Description |
|--------|------|------|-------------|
| 0-4 | 5 bytes | ASCII | `"RREF\0"` (null-terminated header) |
| 5-8 | 4 bytes | int32 LE | Frequency (1-99 Hz; 0 = unsubscribe) |
| 9-12 | 4 bytes | int32 LE | Index (your unique identifier, 0-9999+) |
| 13-412 | 400 bytes | char[] | DataRef path (null-terminated, zero-padded) |

**Example Request (C#):**
```csharp
byte[] packet = new byte[413];
Encoding.ASCII.GetBytes("RREF\0").CopyTo(packet, 0);
Buffer.BlockCopy(BitConverter.GetBytes(frequency), 0, packet, 5, 4);
Buffer.BlockCopy(BitConverter.GetBytes(index), 0, packet, 9, 4);
byte[] pathBytes = Encoding.ASCII.GetBytes(dataRefPath + "\0");
Buffer.BlockCopy(pathBytes, 0, packet, 13, Math.Min(pathBytes.Length, 400));
udpClient.Send(packet, packet.Length, xplaneEndPoint);
```

### 3.2 Response Packet

**Received from:** X-Plane IP, **your** bound port  
**Packet Size:** Variable (5 + N × 8 bytes, where N = number of values)

| Offset | Size | Type | Description |
|--------|------|------|-------------|
| 0-3 | 4 bytes | ASCII | `"RREF"` header |
| 4 | 1 byte | byte | Unused (typically 0x00) |
| 5+ | 8 bytes × N | (int32 + float) | Repeated index/value pairs |

**Each index/value pair (8 bytes):**
| Offset | Size | Type | Description |
|--------|------|------|-------------|
| 0-3 | 4 bytes | int32 LE | Index (matches your subscription) |
| 4-7 | 4 bytes | float32 LE | DataRef value (IEEE 754) |

**Important Notes:**
- X-Plane may batch **multiple DataRef values** in a single UDP packet
- All values are returned as **floats**, even integer DataRefs (cast as needed)
- Array DataRefs require **separate subscriptions** per element (e.g., `dataref[0]`, `dataref[1]`, ...)
- Unsubscribe by sending frequency = 0

**Example Response Parser (C#):**
```csharp
if (data.Length >= 9 && Encoding.ASCII.GetString(data, 0, 4) == "RREF")
{
    int offset = 5;
    while (offset + 8 <= data.Length)
    {
        int index = BitConverter.ToInt32(data, offset);
        float value = BitConverter.ToSingle(data, offset + 4);
        dataRefValues[index] = value;
        offset += 8;
    }
}
```

---

## 4. DATA Protocol (Legacy)

### 4.1 Packet Structure

**Sent from:** X-Plane IP, configured output port  
**Packet Size:** 41 bytes (fixed per group)

| Offset | Size | Type | Description |
|--------|------|------|-------------|
| 0-3 | 4 bytes | ASCII | `"DATA"` header |
| 4 | 1 byte | byte | Unused (typically 0x2A `*` or 0x00) |
| 5-8 | 4 bytes | int32 LE | Group index (0-133+, matches Data Output table row) |
| 9-40 | 32 bytes | float[8] LE | Eight float values for this group |

**Example Parser (C#):**
```csharp
if (data.Length == 41 && Encoding.ASCII.GetString(data, 0, 4) == "DATA")
{
    int groupIndex = BitConverter.ToInt32(data, 5);
    float[] values = new float[8];
    for (int i = 0; i < 8; i++)
        values[i] = BitConverter.ToSingle(data, 9 + i * 4);
}
```

### 4.2 Key Data Groups

| Index | Group Name | Float[0] | Float[1] | Float[2] | Float[3] | Float[4-7] |
|-------|-----------|----------|----------|----------|----------|------------|
| **3** | Speeds | IAS (kts) | EAS (kts) | TAS (kts) | GS (kts) | IAS/TAS/GS (mph), unused |
| **17** | Pitch/Roll/Heading | Pitch (deg) | Roll (deg) | Heading (deg) | varies | varies |
| **20** | Position | Lat (deg) | Lon (deg) | Alt MSL (ft) | Alt AGL (ft) | On-runway, indicated alt, bounds |
| **19** | Velocities | Vx (m/s) | Vy (m/s) | Vz (m/s) | varies | varies |
| **15** | Accelerations | Ax (g) | Ay (g) | Az (g) | varies | varies |
| **23** | Wind | Wind speed (kts) | Wind dir (deg) | varies | varies | varies |

**Note:** Group indices may vary slightly between X-Plane versions. Verify in-sim via **Settings → Data Input & Output → Data Set** tab.

---

## 5. Required DataRefs for Flight Telemetry

### 5.1 Ownship Attitude

| DataRef | Units | Description | Index Example |
|---------|-------|-------------|---------------|
| `sim/flightmodel/position/theta` | degrees | Pitch angle (positive = nose up) | 100 |
| `sim/flightmodel/position/phi` | degrees | Roll angle (positive = right wing down) | 101 |
| `sim/flightmodel/position/psi` | degrees | True heading (0-360, clockwise from North) | 102 |
| `sim/flightmodel/position/mag_psi` | degrees | Magnetic heading | 103 |

### 5.2 Airspeed

| DataRef | Units | Description | Index Example |
|---------|-------|-------------|---------------|
| `sim/flightmodel/position/indicated_airspeed` | knots | Indicated Airspeed (IAS) | 110 |
| `sim/flightmodel/position/true_airspeed` | m/s | True Airspeed (TAS) — **multiply by 1.94384 for knots** | 111 |
| `sim/flightmodel/position/groundspeed` | m/s | Ground Speed — **multiply by 1.94384 for knots** | 112 |

### 5.3 Position

| DataRef | Units | Description | Index Example |
|---------|-------|-------------|---------------|
| `sim/flightmodel/position/latitude` | degrees | Latitude (positive = North) | 120 |
| `sim/flightmodel/position/longitude` | degrees | Longitude (positive = East) | 121 |
| `sim/flightmodel/position/elevation` | meters | Altitude MSL (Mean Sea Level) | 122 |
| `sim/flightmodel/position/y_agl` | meters | Height Above Ground Level | 123 |

### 5.4 Weather (X-Plane 12 Format)

| DataRef | Units | Description | Index Example |
|---------|-------|-------------|---------------|
| `sim/weather/aircraft/wind_speed_kt` | knots | Wind speed at aircraft position | 130 |
| `sim/weather/aircraft/wind_direction_deg` | degrees | Wind direction (true, where wind is **from**) | 131 |
| `sim/weather/aircraft/ambient_temperature_c` | °C | Ambient temperature | 132 |
| `sim/weather/aircraft/barometer_sealevel_inhg` | inHg | Sea-level pressure (QNH) | 133 |

**XP11 Legacy (deprecated in XP12):**
- `sim/weather/wind_speed_kt`
- `sim/weather/wind_direction_deg`
- `sim/weather/ambient_temperature_c`

### 5.5 Traffic / TCAS

| DataRef | Units | Description | Array Size |
|---------|-------|-------------|------------|
| `sim/cockpit2/tcas/targets/modeS_id[n]` | integer | Mode S transponder code (24-bit: 0-16777215) | [64] |
| `sim/cockpit2/tcas/targets/flight_id[n]` | byte[8] | Flight ID string (8 chars per target) | [64] × 8 |
| `sim/cockpit2/tcas/targets/relative_x[n]` | meters | Relative X position (local coords) | [64] |
| `sim/cockpit2/tcas/targets/relative_y[n]` | meters | Relative Y position | [64] |
| `sim/cockpit2/tcas/targets/relative_z[n]` | meters | Relative Z position | [64] |
| `sim/cockpit2/tcas/targets/relative_distance_m[n]` | meters | Relative distance | [64] |
| `sim/cockpit2/tcas/targets/relative_bearing_deg[n]` | degrees | Relative bearing | [64] |
| `sim/cockpit2/tcas/targets/altitude_ft[n]` | feet | Target altitude | [64] |

**Note:** TCAS arrays require **individual subscriptions per element** (e.g., index 200 = `modeS_id[0]`, index 201 = `modeS_id[1]`, etc.). Targets are sorted by distance; inactive slots return 0.

---

## 6. XP11 vs XP12 Differences

### Protocol Changes

| Aspect | XP11 | XP12 | Notes |
|--------|------|------|-------|
| RREF format | Same | Same | No breaking changes |
| DATA format | Same | Same | Packet structure unchanged |
| Port usage | 49000 in, user-config out | Same | 49009 remains community choice |
| Weather DataRefs | `sim/weather/*` | `sim/weather/aircraft/*` | XP12 uses scoped paths |
| TCAS array size | [20] or [64] | [64] | XP12 expanded capacity |
| Max RREF frequency | ~100 Hz | ~100 Hz | Practical limit ~30-50 Hz for stability |

### Recommendations

- **Use XP12-style DataRefs** (`sim/weather/aircraft/*`) for forward compatibility
- **Test DataRef paths** in your specific X-Plane version using DataRefEditor
- **Monitor X-Plane.log** for deprecated DataRef warnings

---

## 7. Unity C# Implementation Example

### Complete MonoBehaviour Listener

```csharp
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;

public class XPlaneTelemetryReceiver : MonoBehaviour
{
    [Header("Connection")]
    public string xplaneIP = "127.0.0.1";
    public int receivePort = 49005;  // Local port to bind
    
    [Header("Subscription (freq 1-99 Hz)")]
    [Serializable]
    public struct DataRefSubscription
    {
        public string dataRef;
        [Range(1, 99)] public int frequency;
        public int index;
    }
    
    public DataRefSubscription[] subscriptions;
    
    // Live data access
    private Dictionary<int, float> values = new Dictionary<int, float>();
    private UdpClient udpClient;
    private IPEndPoint xplaneEP;
    private bool running = true;
    
    // Cached values for easy access
    public float Pitch => GetValue(100);
    public float Roll => GetValue(101);
    public float Heading => GetValue(102);
    public float IAS => GetValue(110);
    public float TAS => GetValue(111) * 1.94384f;  // m/s → knots
    public float GroundSpeed => GetValue(112) * 1.94384f;
    public float Latitude => GetValue(120);
    public float Longitude => GetValue(121);
    public float AltitudeMSL => GetValue(122) * 3.28084f;  // m → ft
    public float WindSpeed => GetValue(130);
    public float WindDirection => GetValue(131);
    public float Temperature => GetValue(132);
    public float QNH => GetValue(133);
    
    void Start()
    {
        try
        {
            udpClient = new UdpClient(receivePort);
            udpClient.Client.ReceiveTimeout = 1000;
            xplaneEP = new IPEndPoint(IPAddress.Parse(xplaneIP), 49000);
            
            Debug.Log($"[XPlane] Listening on {receivePort}, target {xplaneIP}:49000");
            
            BeginReceive();
            SubscribeAll();
        }
        catch (Exception e)
        {
            Debug.LogError($"[XPlane] Init failed: {e.Message}");
        }
    }
    
    void SubscribeAll()
    {
        foreach (var sub in subscriptions)
        {
            SendRREF(sub.frequency, sub.index, sub.dataRef);
            Debug.Log($"[XPlane] Subscribed: idx={sub.index} freq={sub.frequency} {sub.dataRef}");
        }
    }
    
    void SendRREF(int freq, int idx, string dataRef)
    {
        byte[] pkt = new byte[413];
        Encoding.ASCII.GetBytes("RREF\0").CopyTo(pkt, 0);
        Buffer.BlockCopy(BitConverter.GetBytes(freq), 0, pkt, 5, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(idx), 0, pkt, 9, 4);
        byte[] path = Encoding.ASCII.GetBytes(dataRef + "\0");
        Buffer.BlockCopy(path, 0, pkt, 13, Math.Min(path.Length, 400));
        udpClient.Send(pkt, pkt.Length, xplaneEP);
    }
    
    void BeginReceive()
    {
        if (!running) return;
        try
        {
            udpClient.BeginReceive(OnData, null);
        }
        catch (Exception e)
        {
            if (running) Debug.LogError($"[XPlane] Receive error: {e.Message}");
        }
    }
    
    void OnData(IAsyncResult ar)
    {
        if (!running) return;
        
        try
        {
            IPEndPoint remote = null;
            byte[] data = udpClient.EndReceive(ar, ref remote);
            
            if (data.Length >= 9)
            {
                string hdr = Encoding.ASCII.GetString(data, 0, 4);
                
                if (hdr == "RREF")
                {
                    int offset = 5;
                    while (offset + 8 <= data.Length)
                    {
                        int idx = BitConverter.ToInt32(data, offset);
                        float val = BitConverter.ToSingle(data, offset + 4);
                        lock (values) values[idx] = val;
                        offset += 8;
                    }
                }
            }
        }
        catch { }
        
        BeginReceive();
    }
    
    public float GetValue(int idx, float def = 0f)
    {
        lock (values)
            return values.TryGetValue(idx, out float v) ? v : def;
    }
    
    void OnDestroy()
    {
        running = false;
        // Unsubscribe all
        foreach (var sub in subscriptions)
            SendRREF(0, sub.index, sub.dataRef);
        udpClient?.Close();
        udpClient?.Dispose();
    }
}
```

### Usage in Unity

1. Attach `XPlaneTelemetryReceiver` to a GameObject
2. Configure subscriptions in Inspector:
   ```
   subscriptions[0]: dataRef="sim/flightmodel/position/theta", frequency=30, index=100
   subscriptions[1]: dataRef="sim/flightmodel/position/phi", frequency=30, index=101
   subscriptions[2]: dataRef="sim/flightmodel/position/psi", frequency=30, index=102
   ...
   ```
3. Access data in your scripts:
   ```csharp
   var receiver = FindObjectOfType<XPlaneTelemetryReceiver>();
   float pitch = receiver.Pitch;
   float lat = receiver.Latitude;
   ```

---

## 8. Performance Considerations

### Latency Optimization

| Factor | Recommendation |
|--------|----------------|
| Frequency | 20-30 Hz per DataRef is sufficient for most uses; 50+ Hz adds overhead |
| Batch size | X-Plane batches multiple values/packet; use contiguous indices when possible |
| Thread model | Use async receive (BeginReceive/EndReceive) to avoid blocking Unity main thread |
| Lock contention | Minimize time holding lock on values dictionary |
| Network | Use localhost (127.0.0.1) for <10ms; LAN adds 5-20ms depending on network |

### Expected Latency Budget

| Component | Typical Latency |
|-----------|-----------------|
| X-Plane simulation tick | 8-16ms (60-120 Hz sim) |
| UDP send/receive (localhost) | <1ms |
| Packet parsing | <0.5ms |
| Unity Update() access | <0.5ms |
| **Total** | **<20ms** (well under 50ms target) |

---

## 9. Troubleshooting

| Issue | Solution |
|-------|----------|
| No data received | Verify X-Plane is running, check firewall, confirm IP/port |
| Values stuck at 0 | Check DataRef path spelling; verify DataRef exists in your XP version |
| High latency | Reduce subscription frequency; use localhost; check network congestion |
| Deprecated warnings | Update to XP12-style DataRef paths (e.g., `sim/weather/aircraft/*`) |
| TCAS data empty | Ensure traffic is present; some targets require plugins (LiveTraffic, etc.) |

---

## 10. References

- XPPython3 UDP Documentation: https://xppython3.readthedocs.io/en/latest/development/udp/rref.html
- X-Plane DataRef Search: https://www.siminnovations.com/xplane/dataref
- NASA XPlaneConnect: https://github.com/nasa/XPlaneConnect
- X-Plane Developer DataRefs: https://developer.x-plane.com/datarefs/
- X-Plane Knowledge Base Data Output Table: https://www.x-plane.com/kb/data-set-output-table/
- Nuclear Projects X-Plane UDP Info: https://www.nuclearprojects.com/xplane/info.shtml

---

## Appendix A: Complete DataRef Subscription Template

```csharp
// Copy-paste template for Unity Inspector configuration
new DataRefSubscription { dataRef = "sim/flightmodel/position/theta", frequency = 30, index = 100 },   // Pitch
new DataRefSubscription { dataRef = "sim/flightmodel/position/phi", frequency = 30, index = 101 },     // Roll
new DataRefSubscription { dataRef = "sim/flightmodel/position/psi", frequency = 30, index = 102 },     // Heading (true)
new DataRefSubscription { dataRef = "sim/flightmodel/position/indicated_airspeed", frequency = 30, index = 110 },  // IAS (kts)
new DataRefSubscription { dataRef = "sim/flightmodel/position/true_airspeed", frequency = 30, index = 111 },       // TAS (m/s)
new DataRefSubscription { dataRef = "sim/flightmodel/position/groundspeed", frequency = 30, index = 112 },         // GS (m/s)
new DataRefSubscription { dataRef = "sim/flightmodel/position/latitude", frequency = 30, index = 120 },  // Lat (deg)
new DataRefSubscription { dataRef = "sim/flightmodel/position/longitude", frequency = 30, index = 121 }, // Lon (deg)
new DataRefSubscription { dataRef = "sim/flightmodel/position/elevation", frequency = 30, index = 122 }, // Alt MSL (m)
new DataRefSubscription { dataRef = "sim/weather/aircraft/wind_speed_kt", frequency = 10, index = 130 }, // Wind (kts)
new DataRefSubscription { dataRef = "sim/weather/aircraft/wind_direction_deg", frequency = 10, index = 131 }, // Wind dir (deg)
new DataRefSubscription { dataRef = "sim/weather/aircraft/ambient_temperature_c", frequency = 10, index = 132 }, // Temp (C)
new DataRefSubscription { dataRef = "sim/weather/aircraft/barometer_sealevel_inhg", frequency = 10, index = 133 }, // QNH (inHg)
```

---

**END OF SPECIFICATION**
