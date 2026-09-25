# Automatic X-Plane 12 data-source discovery

## What a downloaded player does

The application starts in **Automatic** mode. It looks for usable X-Plane 12 data on the local machine instead of assuming the developer's API tunnel exists. Discovery and network reads run outside the Unity render thread. A small bottom-left source badge always identifies the chosen source; clicking it opens **Settings → DATA SOURCE**, a side-mounted panel.

Candidate preference is native local Web API, native local UDP RREF, explicitly configured native endpoints, configured/listed local UDP DATA output, then semantically validated MQTT streams. A source must supply a complete, plausible flight core in at least three fresh samples spanning 0.4 seconds. A current healthy source is sticky; the system does not oscillate every frame as probes finish. A better source waits for the current source's minimum eight-second residence unless the operator explicitly pins it.

After the initial six-second local search, or three seconds without a usable selected source, Automatic mode attempts the existing configured fallback. **The fallback is visibly labelled `FALLBACK FEED - NOT LOCAL SIMULATOR`.** It may be a separate aircraft/session and must not be confused with a local simulator. If it is also unreachable, the application reports no flight data; it does not synthesize a connected simulator.

### A necessary deployment boundary

The project's existing feed is reached through `http://127.0.0.1:12678` on the development machine's authorized SSH tunnel. That address means **the downloading computer itself**, not a universally available public service. Automatic selection can try it, but it cannot supply someone else's SSH credentials or make a private endpoint reachable. On a new PC, local X-Plane normally avoids this dependency. To use the existing remote fallback, configure an authorized reachable API URL or run the packaged `Start-Fallback-Tunnel.cmd` with the user's own preconfigured SSH alias. No password or private key is included in the release and no private simulator service is exposed publicly by this update.

## Discovery mechanisms

### Local process, installation and ports

The inventory checks the `X-Plane` / `X-Plane-x86_64` process names, reads an accessible executable location, and validates candidate installation roots. It checks X-Plane 12's platform-specific installation-list files and explicitly configured installation paths. Only bounded known preference files are read; there is no recursive drive scan.

On Windows, the IP Helper owner-PID tables identify UDP ports and TCP listeners belonging to those processes. This can find a non-default simulator port without guessing a port range. The same read-only inventory recognizes local Mosquitto, EMQX and NanoMQ process listener ports. It does not inspect process memory, command-line secrets or unrelated broker traffic. A discovered port is only a **candidate** until its protocol and data validate. Permission-denied process metadata does not stop default local probes.

The built-in defaults are UDP 49000, native Web API 8086 and local MQTT 1883. Passive X-Plane beacons can reveal a changed UDP port, but automatic beacon selection is restricted to this computer's local addresses. Other LAN simulators require an explicit endpoint; the program does not silently choose a neighbour's aircraft.

### Native UDP RREF

The application opens its own ephemeral receive socket and sends indexed **read subscriptions** for canonical X-Plane datarefs. It requires X-Plane 12 version identity, the simulator running clock, and fresh position, attitude, airspeed, ground speed and vertical speed. Optional engine, navigation and weather fields are included only as available. A confirmed paused native source is labelled `[PAUSED]` rather than mistaken for a dropped connection.

The configured rate is 10 Hz. Only this client's subscriptions are cancelled, with RREF frequency zero, when its worker exits. The program never sends `DREF`, `DSEL`, control positions, autopilot commands or simulator settings changes. The existing UDP DATA path only listens to receive ports already present in known preferences or explicitly configured by the user. It decodes X-Plane 12 sets 3, 4, 17 and 20 with their documented units and never enables output sets itself.

### Native Web API

The client resolves exact dataref names through X-Plane's own catalog and obtains the IDs for the **current simulator session**. It subscribes over WebSocket and retains unchanged values within that session because the server sends deltas after the initial full update. A reconnect discards the ID/value cache and performs discovery again. It does not hardcode another user's dataref IDs.

The native API is present from X-Plane 12.1.1 onward. Network security settings can disable it; UDP is a separate candidate. The implementation uses the broadly supported API v1 read/subscription methods. It never invokes API setters, commands or flight initialization. Requests have bounded payload sizes, parsing depth, timeouts and concurrency, and do not follow redirects to an unexpected endpoint.

### MQTT topics and semantic matching

MQTT does not provide a general standard operation to list every publisher's topic or infer arbitrary units. The client therefore **observes actual messages on an authorized configured broker**. The local default uses an eight-second `#` discovery window; recognized sources are then narrowed to exact snapshot topics or their validated canonical-dataref prefix. Discovery windows repeat after 45 seconds so a publisher that starts later can be found.

Supported message forms:

| Form | Recognition rule |
|---|---|
| JSON snapshot on any topic | `raw` or `datarefs` map keyed by canonical `sim/...` names. Arrays are expanded into indexed datarefs. |
| Explicit ownship JSON | `ownship` contains unit-bearing names such as `altitude_m`, `pitch_deg`, `indicated_airspeed_kt`, `ground_speed_kt`, `vertical_speed_fpm`, latitude and longitude. |
| Scalar topic per dataref | A common publisher prefix followed by a canonical `sim/...` path; all required values must arrive inside the freshness window. |

For example, a valid JSON snapshot at `training/rig-A/flight` is discovered without requiring that particular topic name. Scalar topics at `rig-A/sim/flightmodel/position/theta`, etc., are grouped only under `rig-A`. Fields from `rig-B` never fill gaps in `rig-A`.

Retained messages are ignored as live-source evidence. Topics denoting commands, sets or requests are excluded. Payloads and credentials are never logged. Unsupported unitless JSON such as `{"altitude":1200}` is rejected rather than interpreted as metres or feet. A local broker may relay a remote simulator: MQTT candidates are labelled as **schema-validated**, not falsely presented as proof of a local X-Plane process. Credentials and non-standard remote broker locations must be supplied by the operator; discovery does not bypass authentication.

## Operator controls

**AUTO + FALLBACK** restores ranked automatic selection. **LOCAL ONLY** means validated discovered/configured sources without the existing fallback. **FALLBACK ONLY** explicitly uses the existing API URL. **NEXT VALID SOURCE** pins another qualified candidate; a missing pinned source does not silently switch to a different aircraft. **RESCAN** reloads configuration and restarts discovery. **STOP DATA** releases the source workers and stops applying telemetry.

The active badge and Data Source page identify the transport, endpoint/topic, mapped-field count, process/install summary and validation status. Settings stay outside the forward viewing cone. None of these actions changes radar layout, optical field of view, lens calibration or simulator flight controls.

## Configuration

Shipped defaults: `FAA-XR3_Data/StreamingAssets/FAA/DataSources.json`. A file named `DataSources.json` in Unity's application-specific persistent-data folder overrides the shipped file. The code exposes its exact path as `XPlaneSourceDiscovery.ConfigurationPath`. Edit the configuration and choose **RESCAN**, or restart the player.

```json
{
  "version": 1,
  "enabled": true,
  "allowFallback": true,
  "discoverLocalUdp": true,
  "discoverLocalWeb": true,
  "observeLocalBeacons": true,
  "fallbackUrl": "http://127.0.0.1:12678",
  "localTerrainUrl": "http://127.0.0.1:8767",
  "udpEndpoints": [],
  "udpListenPorts": [],
  "webEndpoints": [],
  "mqttBrokers": [
    { "host": "127.0.0.1", "port": 1883, "tls": false, "topicFilter": "#",
      "usernameEnvironment": "", "passwordEnvironment": "" }
  ],
  "installPaths": [],
  "preferredSource": "",
  "startupGraceSeconds": 6,
  "sourceLossSeconds": 3
}
```

Explicit UDP endpoints use literal IPv4/port, for example `192.0.2.20:49000`; this example address is not a working simulator. `webEndpoints` are native X-Plane API origins such as `http://192.0.2.20:8086`. Broker credentials are referenced by environment-variable **names**, never embedded passwords. Non-loopback MQTT credentials require TLS, with ordinary certificate verification. Empty `mqttBrokers` disables MQTT discovery, including automatic known-broker listener candidates. `enabled:false` starts in explicit fallback-only mode unless a saved operator mode overrides it. A saved STOP remains stopped until the user chooses another mode.

Bounds include four endpoints per protocol, eight explicit installation paths, 32 selection candidates, 24 concurrent discovery workers, 128-KiB JSON/WebSocket payloads, and bounded MQTT message/byte rates. This is not an unrestricted LAN scanner.

## Terrain and other channels

Flight telemetry and terrain are separate services. Switching from the current fallback to a discovered simulator clears prior data and directs terrain to `localTerrainUrl` so stale terrain from another simulator is not retained. The installed X-Plane DSF elevation service must actually be available there; finding an X-Plane process does **not** automatically reproduce its textured scenery or install a terrain server. Switching back restores the configured terrain endpoint. See [terrain setup](../Tools/XPlaneTerrain/README.md).

Optional channels absent from a selected source are not copied from the fallback. A native source may have fewer traffic/weather datarefs than the project's full relay snapshot. Do not interpret an unavailable field as a valid zero, a terrain-clearance guarantee or a complete traffic picture.

## Validation and limits

Focused tests cover malformed data, replay, freshness, warmup, source stickiness, unit conversion, prefix isolation, retained messages, beacon identity, RREF packet layout and owner-table port byte order. A separate loopback fixture exercises the **actual C# UDP, native HTTP/WebSocket and MQTT clients** against isolated protocol servers. It verifies subscriptions only, discovery of an arbitrary snapshot topic, delta updates, cancellation and RREF unsubscribe. Fixture samples never enter the live flight HUD.

The development Mac actually reported zero local X-Plane processes/installations and successfully selected the existing healthy fallback with the non-local warning. This does not establish successful detection on every Windows installation. Windows process-table behavior, physical XR-3 rendering, authenticated production broker policies, unusual add-on schemas and sustained headset performance require operator/hardware validation. Build success is not certification.

Primary protocol references: [Laminar native Web API](https://developer.x-plane.com/article/x-plane-web-api/), [OASIS MQTT 3.1.1](https://docs.oasis-open.org/mqtt/mqtt/v3.1.1/os/mqtt-v3.1.1-os.html), [Microsoft TCP owner tables](https://learn.microsoft.com/en-us/windows/win32/api/iphlpapi/nf-iphlpapi-getextendedtcptable), [Microsoft UDP owner tables](https://learn.microsoft.com/en-us/windows/win32/api/iphlpapi/nf-iphlpapi-getextendedudptable). The installed X-Plane UDP specification remains the authority for native wire formats.
