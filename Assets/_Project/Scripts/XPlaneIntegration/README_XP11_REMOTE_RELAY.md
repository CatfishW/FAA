# X-Plane 11 Remote Relay for SSH 4090 + Unity Windows

This adds the missing runtime shape for the SSH-based setup:

- **4090 machine** runs full X-Plane 11 with graphics enabled.
- A **Python relay** keeps the aircraft aloft indefinitely and publishes FAA-relevant telemetry over TCP/NDJSON.
- The **Windows Unity machine** connects through `XPlaneRemoteTelemetryBridge` and injects the feed into existing FAA systems.

## Why this path

X-Plane 11 does not expose a real production headless runtime. The stable path is:

1. run the simulator normally on the GPU host,
2. control/export state via plugin or XPlaneConnect,
3. use SSH only for process supervision and port forwarding.

## Files

- `Assets/_Project/Scripts/XPlaneIntegration/Remote/xplane_remote_relay.py`
  - 4090-side relay/controller.
  - `--mode mock` for local smoke tests.
  - `--mode xpc` for real X-Plane 11 using NASA XPlaneConnect.
- `Assets/_Project/Scripts/XPlaneIntegration/Runtime/XPlaneRemoteTelemetryBridge.cs`
  - Unity-side TCP receiver and injector.
- `Assets/_Project/Scripts/XPlaneIntegration/Editor/XPlaneRemoteTelemetrySmokeCli.cs`
  - Batchmode smoke test entry point.

## Telemetry schema

Each line is a JSON object with:

- `timestamp_utc`
- `source_mode`
- `ownship`
  - lat/lon/altitude/pitch/roll/heading
  - IAS/TAS/ground speed/vertical speed
  - autopilot state, control inputs, gear/flaps/speedbrake
- `weather`
  - wind, barometer, temperature, visibility, cloud base
- `traffic`
  - array of surrounding traffic objects
- `raw`
  - key raw datarefs for FAA/debug consumers
- `automation`
  - current envelope-keeper mode and targets

## 4090 machine runtime

### Mock mode

```bash
python3 "Assets/_Project/Scripts/XPlaneIntegration/Remote/xplane_remote_relay.py" \
  --mode mock \
  --listen-host 127.0.0.1 \
  --listen-port 37211 \
  --broadcast-hz 5
```

### Real X-Plane 11 mode

Requirements:

1. X-Plane 11 running on the 4090 host.
2. NASA XPlaneConnect plugin installed into `Resources/plugins/`.
3. Python `xpc` client importable on the 4090 host.

```bash
python3 "Assets/_Project/Scripts/XPlaneIntegration/Remote/xplane_remote_relay.py" \
  --mode xpc \
  --xplane-host 127.0.0.1 \
  --xplane-port 49009 \
  --listen-host 0.0.0.0 \
  --listen-port 37211 \
  --broadcast-hz 10 \
  --target-altitude-ft 8500 \
  --target-heading-deg 090 \
  --target-speed-kt 160
```

## Infinite-flight behavior

The relay uses a **hybrid envelope keeper** in `xpc` mode:

- normal mode: continuously nudges pitch/roll/throttle using `sendCTRL`,
- recovery mode: if altitude collapses, bank becomes extreme, or speed decays, it uses `sendPOSI` to recover to a safe cruise state.

This is intentionally pragmatic. The goal is continuous FAA telemetry, not perfect autopilot fidelity.

## Unity-side hookup

Add `XPlaneRemoteTelemetryBridge` to a GameObject and assign, or let it auto-find:

- `AircraftController`
- `TrafficRadarDataManager`
- `TrafficRadarController` (optional)
- `WeatherRadarProviderBase` (optional)

The bridge updates:

- ownship position/state into `AircraftController`,
- traffic targets into `TrafficRadarDataManager`,
- weather radar aircraft position into `WeatherRadarProviderBase`.

## SSH and port forwarding

If Unity runs on the Windows SSH machine and the relay runs on the 4090 machine, forward the relay port:

```bash
ssh -L 37211:127.0.0.1:37211 user@ssh-4090
```

Then point the Unity bridge at `127.0.0.1:37211`.

## Git-based sync for the two machines

Use the same repo remote and branch on both machines.

### After implementation on whichever machine you edited first

```bash
git status
git add Assets/_Project/Scripts/XPlaneIntegration
git commit -m "Add remote X-Plane telemetry relay smoke path"
git push origin master
```

### Windows Unity machine

```bash
git fetch origin
git checkout master
git pull --ff-only origin master
```

### 4090 X-Plane machine

```bash
git fetch origin
git checkout master
git pull --ff-only origin master
```

Run the pull on **both machines immediately before cross-machine testing** so Unity and the relay use the same code.

Recommended testing order:

1. Push the finished XPlaneIntegration changes from the machine where you implemented them.
2. Pull on the **4090 X-Plane machine** before starting the relay.
3. Pull on the **Windows Unity machine** before opening or batch-running Unity smoke tests.
4. Only then start the relay, open the SSH tunnel, and run the Unity-side test.

## Local smoke test

### 1. Start mock relay

```bash
python3 "Assets/_Project/Scripts/XPlaneIntegration/Remote/xplane_remote_relay.py" \
  --mode mock \
  --listen-port 37211 \
  --duration-seconds 10
```

### 2. Run Unity batchmode test

```bash
XPLANE_REMOTE_HOST=127.0.0.1 \
XPLANE_REMOTE_PORT=37211 \
"/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode \
  -projectPath "/Users/zladwu/Development/Projects/FAA" \
  -quit \
  -executeMethod FAA.XPlaneIntegration.Editor.XPlaneRemoteTelemetrySmokeCli.Run
```

Artifacts are written to:

- `ulw_test_results/xplane_remote_smoke/xplane-remote-smoke-report.txt`
- `ulw_test_results/xplane_remote_smoke/weather-radar.png`

## What this does not do

- It does **not** make XP11 truly headless.
- It does **not** require GUI automation over SSH.
- It does **not** replace the existing direct UDP/X-Plane integration already present in this repo.

This is the SSH-friendly companion path for remote operation.
