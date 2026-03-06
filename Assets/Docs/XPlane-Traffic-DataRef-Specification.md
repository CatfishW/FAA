# X-Plane Traffic/Multiplayer DataRef Technical Specification

**Document Version:** 1.0  
**Date:** March 6, 2026  
**Purpose:** Technical specification for XPlaneTrafficProvider DataRef integration

---

## 1. Complete List of Multiplayer/Traffic DataRefs

### 1.1 sim/multiplayer/position/plane[N]/* DataRefs (N=1-19)

The legacy multiplayer system provides **19 aircraft slots** (plane1 through plane19). Each slot exposes the following DataRefs:

#### Position Data (Global Coordinates)
| DataRef | Type | Writable | Units | Description |
|---------|------|----------|-------|-------------|
| `sim/multiplayer/position/planeN_lat` | double | No | degrees | Latitude |
| `sim/multiplayer/position/planeN_lon` | double | No | degrees | Longitude |
| `sim/multiplayer/position/planeN_el` | double | No | meters | Elevation (MSL) |

#### Position Data (Local Cartesian Coordinates)
| DataRef | Type | Writable | Units | Description |
|---------|------|----------|-------|-------------|
| `sim/multiplayer/position/planeN_x` | double | Yes | meters | Local X coordinate |
| `sim/multiplayer/position/planeN_y` | double | Yes | meters | Local Y coordinate |
| `sim/multiplayer/position/planeN_z` | double | Yes | meters | Local Z coordinate |

#### Orientation Data
| DataRef | Type | Writable | Units | Description |
|---------|------|----------|-------|-------------|
| `sim/multiplayer/position/planeN_psi` | float | Yes | degrees | Heading (yaw) |
| `sim/multiplayer/position/planeN_the` | float | Yes | degrees | Pitch angle |
| `sim/multiplayer/position/planeN_phi` | float | Yes | degrees | Bank angle (roll) |

#### Velocity Data
| DataRef | Type | Writable | Units | Description |
|---------|------|----------|-------|-------------|
| `sim/multiplayer/position/planeN_v_x` | float | Yes | m/s | Local X velocity |
| `sim/multiplayer/position/planeN_v_y` | float | Yes | m/s | Local Y velocity |
| `sim/multiplayer/position/planeN_v_z` | float | Yes | m/s | Local Z velocity |

#### Control Surface Data
| DataRef | Type | Writable | Units | Description |
|---------|------|----------|-------|-------------|
| `sim/multiplayer/position/planeN_flap_ratio` | float | Yes | ratio | Flap position (0.0-1.0) |
| `sim/multiplayer/position/planeN_slat_ratio` | float | Yes | ratio | Slat position (0.0-1.0) |
| `sim/multiplayer/position/planeN_gear_deploy` | float | Yes | ratio | Gear deployment (0.0-1.0) |
| `sim/multiplayer/position/planeN_speedbrake_ratio` | float | Yes | ratio | Speedbrake position (0.0-1.0) |
| `sim/multiplayer/position/planeN_spoiler_ratio` | float | Yes | ratio | Spoiler position (0.0-1.0) |
| `sim/multiplayer/position/planeN_wing_sweep` | float | Yes | ratio | Wing sweep ratio |
| `sim/multiplayer/position/planeN_throttle` | float | Yes | ratio | Throttle position (0.0-1.0) |

#### Control Input Data (XP11 and earlier)
| DataRef | Type | Writable | Units | Description |
|---------|------|----------|-------|-------------|
| `sim/multiplayer/position/planeN_yolk_pitch` | float | Yes | ratio | Yoke pitch input |
| `sim/multiplayer/position/planeN_yolk_roll` | float | Yes | ratio | Yoke roll input |
| `sim/multiplayer/position/planeN_yolk_yaw` | float | Yes | ratio | Yoke yaw (rudder) input |

> **Note:** In X-Plane 12, `yolk_*` DataRefs are deprecated. Use `sim/multiplayer/controls/yoke_*_ratio` instead.

#### Lighting Data
| DataRef | Type | Writable | Units | Description |
|---------|------|----------|-------|-------------|
| `sim/multiplayer/position/planeN_beacon_lights_on` | int | Yes | boolean | Beacon light |
| `sim/multiplayer/position/planeN_landing_lights_on` | int | Yes | boolean | Landing lights |
| `sim/multiplayer/position/planeN_nav_lights_on` | int | Yes | boolean | Navigation lights |
| `sim/multiplayer/position/planeN_strobe_lights_on` | int | Yes | boolean | Strobe lights |
| `sim/multiplayer/position/planeN_taxi_light_on` | int | Yes | boolean | Taxi light |

### 1.2 sim/multiplayer/* Supporting DataRefs

| DataRef | Type | Description |
|---------|------|-------------|
| `sim/multiplayer/num_planes` | int | Current number of active multiplayer aircraft |

### 1.3 sim/multiplayer/controls/* DataRefs (XP12+)

| DataRef | Type | Description |
|---------|------|-------------|
| `sim/multiplayer/controls/yoke_pitch_ratio` | float | Yoke pitch input |
| `sim/multiplayer/controls/yoke_roll_ratio` | float | Yoke roll input |
| `sim/multiplayer/controls/yoke_heading_ratio` | float | Yoke heading (rudder) input |
| `sim/multiplayer/controls/flap_request` | float | Flap request position |
| `sim/multiplayer/controls/gear_request` | int | Gear request state |
| `sim/multiplayer/controls/speed_brake_request` | float | Speedbrake request |

---

## 2. Traffic Slot Count Limits

### 2.1 Legacy Multiplayer DataRefs
- **Maximum slots:** 19 aircraft (plane1 through plane19)
- **User aircraft:** Not counted in multiplayer slots (separate DataRefs)
- **Total visible traffic:** 19 aircraft maximum via DataRefs

### 2.2 TCAS Override (X-Plane 11.50+)
- **Maximum slots:** 63 aircraft (indices 1-63, index 0 reserved for user)
- **DataRef path:** `sim/cockpit2/tcas/targets/position/*`
- **Backward compatibility:** Lower 19 slots mirrored to `sim/multiplayer/position/*`

### 2.3 Plugin Considerations
- XPMP2 (used by LiveTraffic, xPilot, etc.) prioritizes closest 19 aircraft for legacy compatibility
- Slots are dynamically reassigned based on distance from camera
- TCAS Override allows 63 targets but legacy plugins only see 19

---

## 3. DataRef Update Frequency

### 3.1 Update Rate
- **Theoretical maximum:** Per simulation frame (20-60 Hz typical)
- **Actual rate:** Limited by:
  - X-Plane frame rate
  - Network update frequency (for remote traffic)
  - Plugin refresh rate (for injected traffic)

### 3.2 Staleness Timeout
- **TCAS Override:** Targets dropped after **10 consecutive frames** without update
- **Legacy multiplayer:** No explicit timeout, but stale data persists until overwritten
- **Practical implication:** Plugins must update positions every frame for smooth motion

### 3.3 Network Traffic Updates
- **X-Plane native multiplayer:** Update rate depends on sender's frame rate + network latency
- **VATSIM/IVAO:** Typically 1-5 Hz update rate (network bandwidth optimized)
- **ADS-B data injection:** Configurable, often 10-30 seconds at cruise altitude

---

## 4. Known Issues and Limitations

### 4.1 DataRef Gaps and Stale Data

| Issue | Description | Mitigation |
|-------|-------------|------------|
| **10-frame timeout** | TCAS targets disappear if not updated within 10 frames | Ensure continuous updates from traffic source |
| **Stale position data** | Network latency can cause positions to lag by seconds | Use interpolation for smooth display |
| **Missing velocity data** | Legacy DataRefs may not have accurate vx/vy/vz | Derive from position deltas if needed |
| **Deprecated yolk_* fields** | XP12 replaced yolk_pitch/roll/yaw with controls/* | Check X-Plane version, use appropriate DataRefs |

### 4.2 Slot Limitations
- **19-aircraft hard limit** for legacy DataRef readers
- **No slot persistence:** Aircraft may jump between slots as distance ranking changes
- **Plugin conflicts:** Only one plugin can control TCAS override at a time

### 4.3 Read-Only vs Write Access
- **Default state:** All `sim/multiplayer/position/*` DataRefs are read-only
- **Write access requires:**
  1. Call `XPLMAcquireAircraft()` to gain exclusive control
  2. Call `XPLMDisableAIForPlane()` to disable AI control
  3. Set `sim/operation/override/override_planepath[N]` = 1
  4. Update positions every frame (or aircraft freezes)

### 4.4 X-Plane 12 Compatibility Issues
- `planeN_yolk_*` DataRefs deprecated → use `sim/multiplayer/controls/yoke_*`
- Some plugins crash XP12 due to outdated DataRef mappings
- XPUIPC known to crash with many DataRef mappings in XP12

---

## 5. Alternative Traffic Data Sources

### 5.1 TCAS Override (Recommended for XP11.50+)

**DataRef Paths:**
```
sim/cockpit2/tcas/targets/position/x[N]           # Local X (meters)
sim/cockpit2/tcas/targets/position/y[N]           # Local Y (meters)
sim/cockpit2/tcas/targets/position/z[N]           # Local Z (meters)
sim/cockpit2/tcas/targets/position/lat[N]         # Latitude (degrees)
sim/cockpit2/tcas/targets/position/lon[N]         # Longitude (degrees)
sim/cockpit2/tcas/targets/position/ele[N]         # Elevation (meters)
sim/cockpit2/tcas/targets/position/vx[N]          # X velocity (m/s)
sim/cockpit2/tcas/targets/position/vy[N]          # Y velocity (m/s)
sim/cockpit2/tcas/targets/position/vz[N]          # Z velocity (m/s)
sim/cockpit2/tcas/targets/position/psi[N]         # Heading (degrees)
sim/cockpit2/tcas/targets/position/the[N]         # Pitch (degrees)
sim/cockpit2/tcas/targets/position/phi[N]         # Roll (degrees)
sim/cockpit2/tcas/targets/modeS_id[N]             # 24-bit ICAO address
sim/cockpit2/tcas/targets/icao_type[N]            # ICAO aircraft type
sim/cockpit2/tcas/targets/flight_id[N]            # Flight ID / tail number
```

**Advantages:**
- 63 aircraft slots (vs 19 for legacy)
- Official Laminar Research support
- Integrated with X-Plane TCAS displays
- ADS-B Out compatibility

**Requirements:**
- X-Plane 11.50 or later
- Plugin must call `XPLMAcquirePlanes()` and set `sim/operation/override/override_TCAS` = 1

### 5.2 X-Plane Connect (XPC)

**Protocol:** UDP-based (ports 49003, 49009)  
**Repository:** https://github.com/nasa/XPlaneConnect

**Usage:**
```python
# Python example
import XPlaneConnect as xpc
client = xpc.XPlaneConnect()
# Read multiple DataRefs
drefs = ['sim/multiplayer/position/plane1_lat',
         'sim/multiplayer/position/plane1_lon',
         'sim/multiplayer/position/plane1_el']
values = client.getDREFs(drefs)
```

**Advantages:**
- Network-accessible from external applications
- Supports batch DataRef reads (up to 509 bytes/packet)
- Cross-platform (Python, MATLAB, C++, Java clients)
- NASA-maintained open source

**Limitations:**
- Tied to X-Plane frame rate
- Port conflicts possible with multiple clients
- XP12 compatibility requires updates (PR #288)

### 5.3 XPUIPC

**Protocol:** TCP/IP (FSUIPC offset emulation)  
**Website:** https://www.schiratti.com/xpuipc.html

**Configuration:**
```ini
; XPUIPCOffsets.cfg example
*Dataref mp1_lat sim/multiplayer/position/plane1_lat Offset 0x7000 FLOAT64 1 r $mp1_lat
*Dataref mp1_lon sim/multiplayer/position/plane1_lon Offset 0x7008 FLOAT64 1 r $mp1_lon
*Dataref mp1_el sim/multiplayer/position/plane1_el Offset 0x7010 FLOAT64 1 r $mp1_el
```

**Advantages:**
- FSUIPC-compatible interface
- Works with existing FSUIPC tools (MobiFlight, etc.)

**Limitations:**
- **XP12 compatibility issues** (crashes with many mappings)
- Manual DataRef configuration required
- No native multiplayer mappings (user must configure)
- TCP overhead vs UDP
- Not recommended for high-frequency traffic data

### 5.4 XPMP2 Library

**Repository:** https://github.com/TwinFan/XPMP2  
**Used by:** LiveTraffic, xPilot, other multiplayer plugins

**Features:**
- Automatic TCAS Override fallback
- Manages 63 slots internally, mirrors 19 to legacy DataRefs
- Handles aircraft model loading and management
- Plugin coordination via `XPLM_MSG_RELEASE_PLANES`

**For traffic readers:** No direct API needed - read standard DataRefs

---

## 6. DataRef Field Mappings for XPlaneTrafficProvider

### 6.1 Recommended Field Mapping

```typescript
interface TrafficAircraft {
  // Position (use TCAS DataRefs if available, fallback to multiplayer)
  latitude: number;      // sim/cockpit2/tcas/targets/position/lat[N] or planeN_lat
  longitude: number;     // sim/cockpit2/tcas/targets/position/lon[N] or planeN_lon
  altitudeMeters: number; // sim/cockpit2/tcas/targets/position/ele[N] or planeN_el
  
  // Orientation
  heading: number;       // sim/cockpit2/tcas/targets/position/psi[N] or planeN_psi
  pitch: number;         // sim/cockpit2/tcas/targets/position/the[N] or planeN_the
  roll: number;          // sim/cockpit2/tcas/targets/position/phi[N] or planeN_phi
  
  // Velocity
  velocityX: number;     // sim/cockpit2/tcas/targets/position/vx[N] or planeN_v_x
  velocityY: number;     // sim/cockpit2/tcas/targets/position/vy[N] or planeN_v_y
  velocityZ: number;     // sim/cockpit2/tcas/targets/position/vz[N] or planeN_v_z
  
  // Configuration
  gearDown: number;      // sim/cockpit2/tcas/targets/position/gear_deploy[N] or planeN_gear_deploy
  flapRatio: number;     // sim/cockpit2/tcas/targets/position/flap_ratio[N] or planeN_flap_ratio
  
  // Identification (TCAS only)
  modeSId: number;       // sim/cockpit2/tcas/targets/modeS_id[N]
  icaoType: string;      // sim/cockpit2/tcas/targets/icao_type[N]
  flightId: string;      // sim/cockpit2/tcas/targets/flight_id[N]
}
```

### 6.2 DataRef Priority Order

For maximum compatibility, read DataRefs in this priority order:

1. **TCAS Override DataRefs** (`sim/cockpit2/tcas/targets/position/*`) - if available
2. **Legacy Multiplayer DataRefs** (`sim/multiplayer/position/planeN_*`) - fallback
3. **Check `sim/multiplayer/num_planes`** to determine active slot count

### 6.3 Reading Pattern

```cpp
// Pseudocode for reading traffic data
int numPlanes = XPLMGetDatai(XPLMFindDataRef("sim/multiplayer/num_planes"));
for (int i = 1; i <= min(numPlanes, 19); i++) {
    // Read position DataRefs for each slot
    double lat = XPLMGetDatad(XPLMFindDataRef(
        fmt::format("sim/multiplayer/position/plane{}_lat", i).c_str()));
    // ... read other fields
}
```

---

## 7. References

### Official Documentation
- X-Plane Developer - Moving the Plane: https://developer.x-plane.com/article/movingtheplane
- X-Plane Developer - TCAS Override: https://developer.x-plane.com/article/overriding-tcas-and-providing-traffic-information
- X-Plane SDK Documentation: https://developer.x-plane.com/sdk/
- X-Plane DataRef Search: https://www.siminnovations.com/xplane/dataref/

### Open Source Projects
- NASA X-Plane Connect: https://github.com/nasa/XPlaneConnect
- XPMP2 Library: https://github.com/TwinFan/XPMP2
- XPMP2 TCAS Documentation: https://twinfan.github.io/XPMP2/TCAS.html
- LittleXPConnect: https://github.com/albar965/littlexpconnect
- Avitab: https://github.com/fpw/avitab

### Community Resources
- X-Plane.org Forums: https://forums.x-plane.org
- XPUIPC: https://www.schiratti.com/xpuipc.html

---

## 8. Revision History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-03-06 | Initial specification based on research |
