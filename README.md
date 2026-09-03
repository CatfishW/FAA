# FAA Symbology Unity Project

The FAA Symbology Unity Project is a Unity 6.5 cockpit-display demonstrator for
FAA-style flight symbology, real-time traffic and weather awareness, sectional
chart context, and headset output. It is the integration workspace for the
OPL/FAA display work: the same flight state can drive the desktop HUD, the
traffic and weather radars, a Cesium-backed environment, and either a Varjo
XR-3 workflow or the SA-147 multi-display output path.

> **Project status:** active integration/prototyping software. This repository is
> not a certified flight instrument, navigation database, collision-avoidance
> system, or source of operational flight data. Network feeds can be delayed,
> incomplete, unavailable, or wrong. Always cross-check against approved
> aeronautical sources, aircraft procedures, and ATC before making a flight
> decision.

## Contents

- [Capabilities](#capabilities)
- [Architecture](#architecture)
- [Requirements](#requirements)
- [Clone and open the project](#clone-and-open-the-project)
- [Scenes and hierarchy](#scenes-and-hierarchy)
- [First-run editor setup](#first-run-editor-setup)
- [Pilot/operator workflow](#pilotoperator-workflow)
- [Traffic radar](#traffic-radar)
- [FAA sectional chart provider](#faa-sectional-chart-provider)
- [Weather radar](#weather-radar)
- [X-Plane integration](#x-plane-integration)
- [XR-3 and SA-147 headset support](#xr-3-and-sa-147-headset-support)
- [Extension points](#extension-points)
- [Testing, diagnostics, and builds](#testing-diagnostics-and-builds)
- [Troubleshooting](#troubleshooting)
- [Repository layout](#repository-layout)
- [Performance and operational limits](#performance-and-operational-limits)
- [Attribution and licensing](#attribution-and-licensing)
- [Contributing](#contributing)

## Capabilities

| Area | What is implemented |
| --- | --- |
| Flight HUD | Attitude, airspeed, altitude, heading, vertical speed, torque, NR/N2, localizer, glideslope, flight-path and compass elements. The default implementation is the uGUI HUD; a UI Toolkit HUD can be enabled as a secondary presentation. |
| Traffic radar | Circular, masked radar with threat-level symbology, range rings, bearing ticks, compass labels, ownship cue, altitude labels, smooth zoom, track-up mode, animated linework, and a compact/fullscreen presentation. |
| Contextual controls | A modern radar menu opens on demand, keeps its state after an action, and closes when the radar is tapped again. Animated leader lines point from each action to the affected radar region. |
| Sectional maps | FAA VFR Sectional, Terminal Area, World Aeronautical, StreetMap, and configurable custom tile sources. Chart opacity, source, range/zoom, linework, panning, and recentering are controllable at runtime. |
| Navigation targets | A target can be created only from fullscreen map focus. The workflow previews a point, shows latitude/longitude, permits precise coordinate adjustment, and requires explicit confirmation. Cancel and clear operations leave the committed target untouched or remove it as appropriate. |
| Weather radar | Shared provider abstraction with X-Plane, NOAA, IEM, MQTT, and simulated providers. Range, tilt, gain, mode, power, and presentation controls are available. The current X-Plane bridge can synthesize a radar texture from live weather DataRefs. |
| X-Plane data | HTTP snapshot polling, WebSocket stream, TCP newline-delimited JSON, optional MQTT snapshots, and direct X-Plane UDP/RREF integration. Aircraft, weather, systems, multiplayer traffic, and render assets can be routed into the existing FAA systems. |
| XR output | Varjo XR-3 loader configuration and the XR Interaction Toolkit desktop simulator are provided for development. A separate SA-147/S compatibility adapter supports multi-display routing, Archer tracking, and headset prewarp where the vendor hardware is installed. |
| Environment | Cesium georeferencing/terrain hooks, optional terrain synchronization, aircraft position anchoring, and legacy/vendor environment content. |
| Automation and diagnostics | Editor setup wizards, hierarchy organization, missing-script diagnostics, radar evidence capture, remote-relay smoke tests, and test assemblies are included. |

## Architecture

The project keeps the data path separate from presentation so a feed can be
replaced without rewriting HUD or radar rendering:

~~~text
X-Plane 12 / remote relay / mock source / web API
                    │
                    ▼
       transport + connection health layer
  (HTTP, WebSocket, TCP NDJSON, MQTT, or UDP/RREF)
                    │
                    ▼
      normalized flight, weather, and traffic state
                    │
       ┌────────────┼─────────────┬─────────────┐
       ▼            ▼             ▼             ▼
   FAA HUD      Traffic radar  Weather radar  Terrain/XR
  (uGUI/UI      + chart tiles  + sweep/texture  + display
   Toolkit)
~~~

### First-party subsystem responsibilities

- **X-Plane integration**: transport, DataRef mapping, smoothing, stale-feed
  handling, and bridges into aircraft, weather, traffic, and HUD components.
- **Traffic radar**: fetches or receives aircraft rows, calculates distance and
  bearing relative to ownship, classifies threat level, and renders the
  pilot-facing scope.
- **Chart provider**: converts the current geographic center and range to a
  Web Mercator tile request, fetches a 3×3 tile composite, caches tiles, and
  retains the last good composite while retrying.
- **Weather radar**: exposes a common aircraft-position/settings contract so
  live, simulated, NOAA, IEM, MQTT, and X-Plane providers can be swapped.
- **Headset adapters**: route canvases and cameras to native Varjo or SA-147
  output, or instantiate the desktop XR Interaction Simulator without cloning
  the FAA data providers.

## Requirements

### Required for the Unity project

| Requirement | Version or note |
| --- | --- |
| Unity Editor | **6000.5.10f1** (Unity 6.5 tech-stream editor; changeset 3bd4f66ad299). Use this version when reproducing scenes or tests. |
| Git | Any recent Git with SSH or HTTPS access to the repository. |
| Git LFS | Required for the large binary assets tracked by .gitattributes. Run git lfs pull after cloning. |
| Desktop build module | Install the module for the target OS before building a player. The project is configured for standalone desktop development. |
| Network access | Needed for package resolution (first import), FAA/ArcGIS and OpenStreetMap tiles, and the default Airplanes.live traffic source. |

### Optional integrations

- **X-Plane 12**: a running simulator and either the local API service/tunnel,
  direct UDP output, or one of the stream transports described below.
- **Remote 4090 relay**: Python 3 on the GPU host; real X-Plane relay mode also
  needs NASA XPlaneConnect and an importable Python xpc client.
- **Varjo XR-3**: Windows, Varjo Base/runtime, the headset, and the native Varjo
  Unity runtime. The desktop simulator is useful without this hardware but is
  not a substitute for optical, tracking, or distortion validation.
- **SA-147/S**: the SA-147 prefab and Archer/vendor tracking components, plus the
  expected left/right displays. These are hardware/vendor dependencies rather
  than Unity packages supplied by this repository.
- **MQTT weather**: a reachable broker and the optional Python MqttWeather
  service. Do not commit broker credentials.
- **Cesium**: the embedded Cesium package and its platform-native binaries. A
  missing native plugin affects Cesium runtime features but is independent of
  the HUD/radar code.

## Clone and open the project

Use either the repository's SSH remote or an HTTPS URL:

~~~bash
git clone git@github.com:CatfishW/FAA.git
cd FAA
git lfs install
git lfs pull
~~~

Open the project with Unity Hub, or with the Unity CLI:

~~~bash
unity --version
unity open "$PWD" --editor-version 6000.5.10f1
~~~

If the CLI is not installed, open Unity Hub, choose **Add project**, and select
the repository directory. The first import can take a while because Unity
resolves embedded packages, imports the Cesium content, and generates the
Library cache. The Library, Temp, Logs, and UserSettings directories are
intentionally ignored and should not be copied between machines.

After the editor finishes importing:

1. Open **Assets/_Project/Scenes/ExperimentScene.unity** for the integration
   workflow described in this README.
2. Confirm the Console has no compile errors before entering Play mode.
3. Use the setup commands in [First-run editor setup](#first-run-editor-setup)
   only when a scene or component is missing; most setup commands modify and
   save the active scene.
4. Save the scene after an intentional setup change and commit its .unity and
   .meta changes together.

## Scenes and hierarchy

### Scenes

| Scene | Purpose | Build status |
| --- | --- | --- |
| Assets/_Project/Scenes/ExperimentScene.unity | Working FAA/X-Plane integration scene. It contains the live HUD, weather and traffic radar canvases, chart map, controls overlay, terrain hooks, and XR integration objects. | Exists in the project but is **not currently enabled** in EditorBuildSettings.asset. |
| Assets/_Project/Scenes/Main.unity | Primary FAA scene selected for the standalone build configuration. | The only scene currently enabled in ProjectSettings/EditorBuildSettings.asset. |
| Archive/LegacyScenes/* | Historical/recovery scenes retained for reference. They are outside the active Assets tree so Unity does not import stale scene references. | Never use as a shipping scene without review. |

The enabled build list is a project setting, not a guarantee that the scene is
ready for a particular headset or simulator. Before a release, verify the
active scene, platform, XR loader, display routing, and data source in the
target build profile.

### Active hierarchy conventions

The hierarchy organizer groups the working scene as:

~~~text
FAA_Scene
├── _Systems       managers, bridges, voice, diagnostics, geo projection
├── _Gameplay      ownship and gameplay objects
├── _UI            uGUI HUD, UI Toolkit HUD, radar and control canvases
└── _Environment   weather, maps, lighting, terrain
~~~

Run **FAA → Scene → Organize Experiment Scene Hierarchy** after a large scene
edit. Do not casually rename functional objects referenced by setup scripts:
_UI, [MANAGERS], FAASymbologyCanvas, HUDController, OwnAircraft,
WeatherVisualization3D, UniStorm System, OnlineMap, and
GeoPosUnityPosProjectManager.

## First-run editor setup

The project includes idempotent editor tools. They are preferable to manually
copying serialized components because they resolve references and preserve
Unity object IDs.

| Unity menu | Use |
| --- | --- |
| **FAA → X-Plane 12 → Configure API HUD Bridge In Experiment Scene** | Configures the current live-data scene, creates/links the API bridge, HUD, weather/traffic radar systems, indicator system, heading tape, controls overlay, and terrain hooks. |
| **FAA → X-Plane 12 → Configure Live Data Only In Experiment Scene** | Same live X-Plane path with presentation/legacy cleanup kept focused on data integration. |
| **FAA → X-Plane 12 → Apply Compact Radar Glass In Experiment Scene** | Re-applies the compact weather/traffic radar presentation and control overlay. |
| **FAA → X-Plane 12 → Repair Live Engine Bars In Experiment Scene** | Re-links live torque and NR/N2 bars when a scene was migrated. |
| **FAA → X-Plane 12 → Remove Engine Bar Scale Numbers In Scene And Prefab** | Removes obsolete fixed scale-number columns while retaining labeled live readouts. |
| **FAA → HUD → Create Or Update Secondary UI Toolkit HUD In Experiment Scene** | Creates or refreshes the optional UI Toolkit HUD; the uGUI HUD remains the default. |
| **Tools → X-Plane Integration → Setup Wizard** | Manual X-Plane manager/provider/bridge setup. |
| **Tools → X-Plane Integration → Auto-Configure Scene** | One-click legacy/general X-Plane setup. |
| **Tools → X-Plane Integration → Validate Setup** | Checks expected managers, providers, bridges, and network settings. |
| **Tools → Traffic Radar → Setup Wizard** | Creates the traffic data manager, controller, display, chart provider, and basic radar objects. |
| **Tools → Traffic Radar → One-Click Complete Setup** | Runs the complete traffic-radar setup pass. |
| **Tools → Radar UI Controls → Setup Wizard** | Adds range/filter/click handlers and the modern radar control surfaces. |
| **Tools → Radar UI Controls → One-Click Setup All** | Adds all radar UI control components. |
| **Tools → HUD Control → Setup Wizard** | Detects and wires uGUI HUD elements. |
| **Tools → HUD Control → Quick Setup (One Click)** | Runs the full HUD setup pass. |
| **Tools → Indicator System → Setup Indicator System** | Adds on-screen and off-screen traffic/weather indicators. |
| **FAA → Headset → Install XR-3 Simulator Sample** | Imports the XR Interaction Simulator sample and copies its prefab into the FAA Resources path. |
| **FAA → Headset → Configure Varjo XR-3 Provider** | Assigns the Varjo loader to Standalone XR management settings. |
| **FAA → Headset → Configure XR-3 + Simulator In FAA Scenes** | Configures both Main and Experiment scenes for Varjo/XR simulator operation. |
| **FAA → Headset → Configure SA-147S In Experiment Scene** | Adds the SA-147 compatibility component, rig, and Archer bridge to ExperimentScene. |

Most setup tools are safe to run more than once, but scene changes are still
source changes. Review the Hierarchy and Inspector, then save deliberately.

## Pilot/operator workflow

### HUD and radar presentation

The compact radar strips are designed to stay readable without covering the
flight path. Weather and traffic controls can be expanded or collapsed, and
advanced controls are available on demand. The engine readouts use explicit
labels (for example TORQUE, NR/N2, and left/right identifiers) instead of
unexplained fixed scale numbers.

The traffic radar context menu follows these rules:

1. Tap the traffic radar to open the menu.
2. Each action has a short label and an animated leader line to the affected
   scope region.
3. Applying an action does **not** close the menu, so a pilot can make several
   related adjustments.
4. Tap the radar again to close the menu.
5. When the radar is in focus/fullscreen mode, unrelated HUD panels fade out
   while the radar toolbar and menu remain available.

### Fullscreen map focus

Use the radar **FULL** control or the menu's **RADAR VIEW / MAXIMIZE** action to
enter map focus. In focus mode:

- the map receives the available display area;
- the chart and linework remain independently controllable;
- the traffic toolbar stays reachable;
- the rest of the HUD can be hidden to reduce visual competition;
- the same focus mode can be used in the XR simulator and in a native headset.

Drag the map to inspect an area. During a drag, the ownship symbol and ownship
vector are suppressed so they do not visually imply that the panned map is
still centered on the aircraft. Releasing the drag settles the pan and returns
the map to the ownship position according to the configured reset behavior.
Use **CENTER/RECENTER** to explicitly return at any time.

### Chart controls

The contextual menu and compact toolbar expose:

- **MAP SOURCE**: cycle Sectional, Terminal Area, WAC, StreetMap, and an
  optional custom source;
- **OPACITY**: fade the chart independently of radar symbols and linework;
- **RANGE/ZOOM**: select a preset range or use smooth manual zoom;
- **LINEWORK**: show/hide range rings, compass labels, and bearing ticks;
- **TRACK UP**: align the scope to heading, or leave it north-up;
- **RINGS**: adjust the number of range rings;
- **CENTER**: reset a temporary map pan.

The radar keeps a last-good chart composite while a replacement source loads.
If an FAA source has no coverage at the requested location, the provider can
fall back to the World Aeronautical source and then to a procedural background.
The status is visible through the provider API and can be surfaced in a custom
status strip.

### Navigation target workflow

A map tap is never an implicit navigation command. To create a target:

1. Enter traffic radar fullscreen/focus mode.
2. Open the radar menu and choose **SET TARGET** (or **TARGET** when a target
   already exists).
3. Tap a map point to create a **preview**, or enter latitude/longitude in the
   target dialog.
4. Adjust the coordinates with the fine-step controls (the default increment is
   0.001°) and review the displayed position.
5. Select **CONFIRM TARGET** to commit it. The target cue is then shown on the
   radar and exposed to the HUD heading/navigation guidance.
6. Select **CANCEL** to discard the preview, **CLEAR** to remove a committed
   target, or reopen the dialog to edit it.

Target creation is intentionally limited to fullscreen map focus to prevent
accidental selections in the compact radar. The context menu remains open
after confirmation or cancellation until the radar itself is tapped.

### Weather radar controls

The weather radar uses the shared provider contract:

- range: 5–320 NM;
- antenna tilt: −15° to +15°;
- gain: −8 to +8 dB;
- provider power/mode and texture refresh;
- optional sweep-triggered or interval-based updates.

When the X-Plane API bridge is healthy, its weather DataRefs drive the weather
provider and the bridge can publish a procedural weather-radar texture. When
the live feed is stale, the bridge reports unhealthy state instead of silently
presenting old flight values as current.

### Traffic symbology legend

The traffic processor uses distance and relative-altitude thresholds to
select a TCAS-style visual class:

| Class | Default threshold | Visual |
| --- | --- | --- |
| Resolution Advisory | within 1 NM and 300 ft | red filled square |
| Traffic Advisory | within 3 NM and 500 ft | amber filled circle |
| Proximate traffic | within 6 NM and 1,200 ft | cyan filled diamond |
| Other traffic | outside the proximate envelope | cyan outline diamond |

These are display classifications for the demonstrator, not an approved TCAS
implementation. Thresholds are serialized in ThreatThresholds and should be
reviewed with the intended training or evaluation protocol.

## Traffic radar

The traffic system is split into three replaceable stages:

1. TrafficRadarDataManager acquires and normalizes aircraft rows.
2. RadarDataProcessor calculates Haversine distance, bearing, relative
   altitude, and heading-relative radar coordinates.
3. TrafficRadarDisplay renders the circular scope, chart, linework, symbols,
   target preview, and focus transitions.

The TrafficRadarController coordinates the stages, publishes target events,
and applies range/auto-range settings. Current defaults are a 40 NM display
range, 10/20/40/80/150 NM range choices, up to 50 displayed targets, and a
2 Hz processing update.

### External traffic source

The default external source is Airplanes.live:

~~~text
https://api.airplanes.live/v2/point/{lat4}/{lon4}/{radiusNM1}
~~~

The manager refreshes periodically (30 seconds by default), supports a local
PlayerPrefs cache for short outages, and backs off after consecutive failures.
When a healthy X-Plane bridge supplies multiplayer traffic, the current FAA
scene disables the external fetcher to avoid duplicate or conflicting targets.
An explicitly enabled fallback can re-enable external traffic when the
simulator feed becomes unhealthy.

See [the traffic radar guide](Assets/_Project/Scripts/TrafficRadar/README.md)
for component-level inspector settings and runtime API examples.

## FAA sectional chart provider

FAASectionalChartProvider supports five map sources:

| Source | Default service |
| --- | --- |
| Sectional | https://tiles.arcgis.com/tiles/ssFJjBXIUyZDrSYZ/arcgis/rest/services/VFR_Sectional/MapServer |
| Terminal Area | https://tiles.arcgis.com/tiles/ssFJjBXIUyZDrSYZ/arcgis/rest/services/VFR_Terminal/MapServer |
| World Aeronautical | https://services.arcgisonline.com/ArcGIS/rest/services/Specialty/World_Navigation_Charts/MapServer |
| StreetMap | https://tile.openstreetmap.org/{z}/{x}/{y}.png |
| Custom | Configurable XYZ template or ArcGIS MapServer base URL |

Implementation details:

- tile zoom is clamped to levels 4–12;
- source tiles are 256 px and are combined into a 3×3 composite;
- the default composite is 1024 px so the enlarged focus/XR scope stays
  sharper than the former 512 px texture;
- up to 50 tiles are cached for one hour by default;
- requests have cancellation, timeout, retry, and generation checks so an old
  center cannot overwrite a newer drag/zoom request;
- a last successful composite remains visible while a refresh is loading or
  retrying;
- full no-coverage responses can fall back to World Aeronautical, followed by
  the procedural background when enabled;
- provider status (Idle, Loading, Ready, Fallback, Error, or Cancelled) and the
  last error are available for diagnostics.

The chart is contextual imagery, not a certified navigation source. Respect
FAA/ArcGIS, OpenStreetMap, and any custom-provider terms, attribution, cache
limits, and service availability. Do not scrape or redistribute tiles beyond
the applicable terms.

For the earlier tile/projection discussion, see the
[FAA project deck](Assets/Docs/Slides/FAA_Symbology_Project_Deck.md) and the
[traffic radar implementation guide](Assets/_Project/Scripts/TrafficRadar/README.md).

## Weather radar

The weather subsystem exposes a common WeatherRadarProviderBase contract for
position, heading, range, tilt, gain, update mode, texture, and status. Current
provider implementations include:

- XPlaneOriginalWeatherRadarProvider for native or bridge-provided X-Plane
  imagery;
- NOAAWeatherProvider and IEMWeatherProvider for network weather sources;
- MQTTWeatherProvider for an external radar/weather publisher;
- SimulatedWeatherProvider for deterministic local demonstrations.

The optional MQTT/Python path uses these default topics:

| Topic | Payload |
| --- | --- |
| NEXRADImage | Base64-encoded radar image |
| NOAAWeatherCoordinates | latitude,longitude,tilt,gain,heading |
| NOAAWeatherData | JSON weather values |

See [the MQTT weather guide](Assets/_Project/Scripts/MQTT/Weather/README.md)
for the Python service and dependency list. The older 3D weather folders are
retained as deprecated/experimental content; the current FAA scene uses the
2D/provider/bridge path unless explicitly configured otherwise.

## X-Plane integration

There are two related integration layers:

1. **XPlane12ApiHudBridge** is the current FAA scene path. It consumes a
   coherent snapshot or stream and applies it to the HUD, aircraft controller,
   weather radar, and traffic radar.
2. **XPlaneUdpListener and the providers/bridges** are the direct UDP/RREF
   path. They are useful when Unity talks to X-Plane itself rather than to the
   API service.

### Transport endpoints

| Transport | Default endpoint | Notes |
| --- | --- | --- |
| HTTP snapshot (current scene) | http://127.0.0.1:12678 | This is a local API/tunnel endpoint, not X-Plane's native UDP port. The bridge polls v1/snapshot first and uses health/category endpoints as compatibility fallbacks. |
| HTTP health | /health or /api/health | Reports status, sender, last packet age, and last error. |
| WebSocket stream | ws://127.0.0.1:37212/v1/stream/ws | Optional low-latency stream. |
| TCP NDJSON stream | 127.0.0.1:37212 | One JSON snapshot per line. |
| MQTT snapshot | 127.0.0.1:18883, topic xplane12/snapshot | Optional transport with reconnect support. |
| Direct X-Plane UDP | listen on 49009, command/RREF port 49000 | Uses XPlaneUdpListener; configure the simulator and firewall for the Unity host. |
| Legacy Python relay | 127.0.0.1:37211 | TCP NDJSON relay, useful for an SSH-forwarded X-Plane host or mock testing. |

The bridge defaults to:

- 100 ms HTTP polling;
- 2 s request timeout;
- a 5 s stale threshold;
- adaptive smoothing, interpolation, packet-age compensation, and up to 0.2 s
  prediction;
- aircraft, weather, systems, traffic, and render-asset categories enabled;
- X-Plane traffic taking priority over the external traffic API;
- weather DataRef texture synthesis enabled;
- user aircraft control and duplicate external traffic fetching suppressed
  while a healthy live bridge is driving the scene.

The bridge exposes IsFeedHealthy, LastError, LastPacketAgeSeconds, LastSender,
TrafficCount, LatestFlightData, and the latest weather/traffic textures for a
status panel or automated test.

### Snapshot contract

The preferred API envelope is conceptually:

~~~json
{
  "health": {
    "status": "ok",
    "last_packet_age_sec": 0.1,
    "last_error": ""
  },
  "source_mode": "xplane12",
  "aircraft": {},
  "weather": {},
  "systems": {},
  "traffic": [],
  "raw": {}
}
~~~

The bridge also accepts older category responses and normalizes keys into its
Aircraft, Weather, Systems, and Traffic dictionaries. Traffic can be provided
as multiplayer-slot values or as an array of target objects. Keep wire-schema
changes backward-compatible, and always provide a timestamp or health/age
signal so Unity can distinguish a live packet from a stale one.

### Direct X-Plane UDP setup

For the direct path, configure X-Plane 12 to accept incoming connections and
send the required UDP data to the Unity machine. The project listener uses:

- command/RREF requests: UDP 49000;
- incoming DATA/RREF values: UDP 49009;
- default address: 127.0.0.1 for a same-machine simulator.

For a second machine, replace the loopback address with the Unity host's
reachable address and allow the ports through both firewalls. The provider
requests aircraft attitude, airspeed, position, altitude, vertical speed,
wind, and up to 19 multiplayer slots. Relevant DataRefs include:

~~~text
sim/flightmodel/position/theta                 pitch (radians)
sim/flightmodel/position/phi                   roll (radians)
sim/flightmodel/position/psi                   heading (radians)
sim/flightmodel/position/indicated_airspeed    IAS (m/s)
sim/flightmodel/position/groundspeed           ground speed (m/s)
sim/flightmodel/position/elevation             altitude MSL (m)
sim/flightmodel/position/y_agl                 altitude AGL (m)
sim/flightmodel/position/vh_ind                vertical speed (m/s)
sim/flightmodel/position/latitude              latitude (degrees)
sim/flightmodel/position/longitude             longitude (degrees)
sim/weather/aircraft/wind_speed_kt             wind speed (kt)
sim/weather/aircraft/wind_direction_deg        wind direction (degrees)
sim/weather/aircraft/barometer_sealevel_inhg   QNH (inHg)
sim/weather/aircraft/ambient_temperature_c     temperature (°C)
sim/multiplayer/position/plane1_lat            multiplayer slot latitude
sim/multiplayer/position/plane1_lon            multiplayer slot longitude
~~~

The mapper performs the project conversions (metres to feet, metres/second to
knots and feet/minute). X-Plane multiplayer DataRefs are read-only; they do
not replace an approved traffic service.

### SSH tunnel to a remote 4090 host

The current FAA scene expects the API to be reachable locally at port 12678.
If the service runs on the remote simulator host, create the tunnel from the
Unity machine (replace the placeholder with your authorized SSH account):

~~~bash
ssh -N -L 12678:127.0.0.1:12678 <user>@ssh-4090
~~~

For the legacy Python NDJSON relay, forward port 37211 instead:

~~~bash
ssh -N -L 37211:127.0.0.1:37211 <user>@ssh-4090
~~~

Verify the tunnel before opening Unity:

~~~bash
curl -fsS http://127.0.0.1:12678/health
curl -fsS http://127.0.0.1:12678/v1/snapshot
nc -vz 127.0.0.1 37211
~~~

If the API is bound to a different remote port, change the right-hand side of
the SSH mapping and the bridge's serialized endpoint together. Do not put SSH
keys, passwords, cookies, or service tokens in the repository or in scene
assets.

### Legacy remote relay and mock feed

The relay at
Assets/_Project/Scripts/XPlaneIntegration/Remote/xplane_remote_relay.py
supports a local mock source and a real XPlaneConnect source. Mock mode is
useful for validating Unity transport and UI without a running simulator:

~~~bash
python3 Assets/_Project/Scripts/XPlaneIntegration/Remote/xplane_remote_relay.py \
  --mode mock \
  --listen-host 127.0.0.1 \
  --listen-port 37211 \
  --broadcast-hz 5
~~~

Real relay mode requires X-Plane and the NASA XPlaneConnect plugin on the
remote host:

~~~bash
python3 Assets/_Project/Scripts/XPlaneIntegration/Remote/xplane_remote_relay.py \
  --mode xpc \
  --xplane-host 127.0.0.1 \
  --xplane-port 49009 \
  --listen-host 0.0.0.0 \
  --listen-port 37211 \
  --broadcast-hz 10 \
  --target-altitude-ft 8500 \
  --target-heading-deg 090 \
  --target-speed-kt 160
~~~

The relay's envelope keeper is a telemetry/demo aid: it nudges the simulator
and can recover a mock or test aircraft, but it is not an autopilot or a
flight-safety system. See the
[remote relay guide](Assets/_Project/Scripts/XPlaneIntegration/README_XP11_REMOTE_RELAY.md)
and the [XP12 integration guide](Assets/_Project/Scripts/XPlaneIntegration/README_XP12_INTEGRATION.md)
for the longer protocol notes.

## XR-3 and SA-147 headset support

### Varjo XR-3 and Unity simulator

Package and asset support is already present:

- com.varjo.xr 3.7.3 from the Varjo Unity XR plugin;
- XR Interaction Toolkit 3.6.0;
- XR Management 4.5.3;
- AR Foundation 6.6.2 and Unity's device-simulation support;
- Varjo/XR loader and simulator settings under Assets/XR and Assets/XRI.

Use **FAA → Headset → Configure XR-3 + Simulator In FAA Scenes** to import the
XR Interaction Simulator sample, place a reusable prefab under
Assets/Resources/FAA/XR3, assign the Varjo Standalone loader, and configure
both FAA scenes. In the Unity Editor, the XR3HeadsetCompatibility component
prefers the desktop simulator when no native Varjo runtime is available. It
also keeps the FAA pointer reachable and positions the simulator input
selection panel below the heading tape.

The simulator provides desktop input and a simulated HMD pose. It does not
validate Varjo optics, eye tracking, calibration, passthrough, display timing,
or headset-specific distortion. Validate those behaviors on a Windows machine
with the Varjo runtime and physical XR-3 before presenting a hardware result.

### SA-147/S output

Use **FAA → Headset → Configure SA-147S In Experiment Scene** to add:

- SA147HeadsetCompatibility;
- the SA_147_Prefab rig;
- the Archer head-tracker bridge;
- left/right display routing and optional fullscreen resolution;
- overlay capture/prewarp routing for the FAA HUD.

The adapter can auto-enable when the expected displays are present or when the
--sa147, --hmd, or equivalent command-line flag is supplied. It is deliberately
separate from the Varjo path: do not enable both vendor output systems for the
same physical displays without validating the routing.

The native hardware path depends on the vendor prefab, tracker, display
topology, calibration, and runtime drivers. A successful Unity compile or
desktop simulator run is not evidence that the SA-147 optical output is
configured correctly.

## Extension points

The main extension surfaces are:

| File or folder | Extension surface |
| --- | --- |
| Assets/_Project/Scripts/TrafficRadar/Display/TrafficRadarDisplay.cs | Range, chart opacity/source, linework, track-up, map pan, fullscreen, navigation preview/commit/clear, and display events. |
| Assets/_Project/Scripts/TrafficRadar/Providers/FAASectionalChartProvider.cs | FAA/ArcGIS/XYZ source URLs, tile cache, composite size, fallback policy, and load-status events. |
| Assets/_Project/Scripts/Customization/TrafficRadarContextMenu.cs | Context actions, target dialog, leader-line geometry, and pilot-facing labels. |
| Assets/_Project/Scripts/Customization/FaaRadarControlsOverlay.cs | Compact/advanced weather and traffic controls, focus presentation, and persisted radar sizing. |
| Assets/_Project/Scripts/XPlaneIntegration/Runtime/XPlane12ApiHudBridge.cs | Transport selection, snapshot parsing, health/staleness, smoothing, category routing, and render-asset application. |
| Assets/_Project/Scripts/XPlaneIntegration/Core/XPlaneDataRefMapper.cs | DataRef names and unit conversions for direct UDP integration. |
| Assets/_Project/Scripts/HUDControl | Default uGUI HUD controller and element components. |
| Assets/_Project/Scripts/HUDToolkit | Optional UI Toolkit HUD and mode switching. |
| Assets/_Project/Scripts/Headset | Varjo/XR-3 and SA-147 compatibility adapters. |

Examples of the runtime radar API include:

~~~csharp
display.SetMapSource(FAAChartMapSource.Sectional);
display.SetChartOpacity(0.35f);
display.ToggleReferenceLinework();
display.SetTrackUpMode(true);
display.ResetMapPan(false);
display.ToggleFullscreen();
~~~

Navigation should use the preview/commit API rather than writing a target
directly from arbitrary pointer input:

~~~csharp
display.SetNavigationTargetFromLocalPoint(mapPoint, "MAP");
display.CommitNavigationPreview();
// Or, when the pilot cancels:
display.ClearNavigationPreview();
display.ClearNavigationTarget();
~~~

Keep scene references and event subscriptions explicit. Prefer adding a bridge
or provider over duplicating a full HUD/radar hierarchy; duplicate providers
can produce competing network requests, stale textures, or conflicting input.

## Testing, diagnostics, and builds

### Unity CLI checks

Check for a connected editor before using live editor commands:

~~~bash
unity status --format json
~~~

Run the editor tests with a reproducible report path:

~~~bash
mkdir -p _artifacts/tests
unity test "$PWD" \
  --editor-version 6000.5.10f1 \
  --mode EditMode \
  --report-format junit \
  --output _artifacts/tests/editmode.xml

unity test "$PWD" \
  --editor-version 6000.5.10f1 \
  --mode PlayMode \
  --report-format junit \
  --output _artifacts/tests/playmode.xml
~~~

The repository contains editor tests for the heading tape, weather-radar
presentation, voice/radar visibility, symbology color, and X-Plane engine HUD
bindings, plus aircraft-control editor/runtime tests. A test command that
exits with an infrastructure error (compile failure, unavailable editor,
license, crash, or timeout) is different from a completed run with failing
tests; preserve the logs and XML report when diagnosing CI.

### Headless import/compile check

On macOS, a native Unity batch check can be run as follows (adjust the editor
path for the installed platform):

~~~bash
"/Applications/Unity/Hub/Editor/6000.5.10f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode \
  -quit \
  -nographics \
  -projectPath "$PWD" \
  -logFile /tmp/FAA-batch-compile.log
~~~

Inspect the log for compiler errors and package import failures. Cesium's
native binaries may report platform-specific DllNotFoundException messages
when a headless machine lacks the native plugin; that is a runtime environment
issue to resolve for Cesium builds, not a reason to hide C# compiler errors.

### Build

Use the Unity Build Settings/Build Profiles UI for the first platform build,
confirming that the intended scene is enabled and the XR loader is assigned.
For a desktop CLI build, the Unity CLI can be used once the target module is
installed:

~~~bash
mkdir -p Build
unity build "$PWD" \
  --editor-version 6000.5.10f1 \
  --target StandaloneOSX \
  --output-path Build/FAA.app
~~~

Replace StandaloneOSX and the output path for Windows or Linux. Do not place
build output, Library data, test logs, or captured screenshots in source
directories; the repository ignore rules reserve _artifacts for local
evidence.

### Verification snapshot

The documentation pass for this branch recorded the following state on
2026-09-02:

| Check | Result |
| --- | --- |
| Traffic radar C# assembly compile | Passed; warnings only. |
| Main and editor C# assembly compile | Passed; warnings only. |
| Headless Unity import/compile invocation | Exit 0; no C# compiler errors observed. |
| Full EditMode/PlayMode suite | Not claimed as passing in this environment; a prior editor-run attempt was blocked by a Unity recovery/modal state. Reopen the project and run the commands above before release. |
| Native XR-3/SA-147 visual validation | Hardware-dependent; desktop simulator coverage is not optical/hardware certification. |

## Troubleshooting

### Unity opens Safe Mode or the CLI cannot connect

1. Read the first compiler error in the Unity Console; fix the earliest error,
   not every cascading message.
2. If unity status reports no editor, check whether Unity is still importing
   or waiting on a recovery dialog.
3. Restart the editor after fixing scripts so the Pipeline connection and
   package domain reload complete.
4. Do not hand-edit .unity, .prefab, or .asset YAML while a reachable editor has
   unsaved scene state. Use the setup menu or live editor command.

### HUD is visible but X-Plane data is frozen

1. Check the local API first:

   ~~~bash
   curl -v http://127.0.0.1:12678/health
   curl -v http://127.0.0.1:12678/v1/snapshot
   ~~~

2. If the request fails, establish the SSH tunnel and verify the remote
   service is listening on its loopback interface.
3. In the Inspector, confirm the bridge is using HTTP 127.0.0.1:12678 for
   the current FAA scene, not the legacy relay port.
4. Read IsFeedHealthy, LastPacketAgeSeconds, LastSender, and LastError. A
   packet older than the stale threshold is intentionally marked unhealthy.
5. For a direct UDP setup, verify X-Plane's incoming-connection setting,
   destination IP, UDP 49009, command port 49000, and firewall rules.
6. For a relay setup, test the 37211 TCP tunnel and make sure only one relay
   process owns the listening port.

### Traffic radar is empty or duplicates targets

- Confirm the ownship latitude/longitude is finite and non-zero.
- Check the Airplanes.live URL and HTTP status; 404/429/5xx responses can be
  expected service/coverage/rate-limit conditions.
- If X-Plane is healthy, its multiplayer rows intentionally take priority and
  the external fetcher may be suppressed.
- Use a simulated or mock feed to separate UI/rendering issues from network
  issues. Do not enable two independent traffic providers without deciding
  which one owns the target list.

### Chart is blank, low resolution, or logs HTTP 404

- 404 can mean the selected FAA layer has no coverage at the requested tile,
  not that the Unity texture code failed.
- Confirm the provider's Status, LastError, MapSource, and
  IsUsingProceduralFallback.
- Try World Aeronautical or StreetMap to distinguish coverage from transport.
- Check that the machine can reach the ArcGIS/OSM endpoint and that a proxy is
  not rewriting tile URLs.
- Keep chart opacity separate from radar/linework opacity; a fully transparent
  chart is visually identical to a missing chart.
- Clear the provider cache after changing a custom template or projection.

### Cesium reports a missing native library

Cesium's embedded package contains platform-specific native components. A
headless editor, a different CPU architecture, or an incomplete package import
can produce a native-library error even when FAA C# assemblies compile. Install
the correct Cesium/native package for the target platform and test terrain
features on that platform; the HUD/radar pipeline can be tested independently.

### XR devices do not appear in the editor

- The XR Interaction Simulator is a sample/prefab, not a physical device
  discovery list. Run the XR-3 setup menu and enter Play mode.
- Native XR-3 discovery requires the Varjo loader, Varjo runtime, Windows, and
  a connected headset.
- SA-147 output requires the vendor rig, Archer bridge, expected display count,
  and display routing. It does not appear as a generic Unity XR device.
- Avoid enabling both native Varjo and SA-147 display routing until the target
  display topology is confirmed.

### Controls overlap the radar or target selection is accidental

Re-run the relevant radar UI setup, keep the simulator input-selection panel
below the heading tape, and use fullscreen focus before setting a target. The
current target dialog deliberately separates map preview from confirmation;
avoid adding a direct onClick handler that calls a commit method.

## Repository layout

~~~text
Assets/
├── _Project/
│   ├── Scenes/                 Main and Experiment FAA scenes
│   ├── Scripts/
│   │   ├── HUDControl/         default uGUI HUD
│   │   ├── HUDToolkit/         optional UI Toolkit HUD
│   │   ├── TrafficRadar/       traffic, chart, scope, controls
│   │   ├── WeatherRadar/       providers, sweep, display, controls
│   │   ├── XPlaneIntegration/  API, UDP, bridges, remote relay
│   │   ├── Headset/            Varjo/XR-3 and SA-147 adapters
│   │   ├── IndicatorSystem/    on/off-screen traffic/weather cues
│   │   └── Editor/             setup, diagnostics, hierarchy tools
│   ├── Docs/                   project structure and integration notes
│   ├── Data/                   radar/traffic data assets
│   ├── Prefabs/                first-party reusable objects
│   ├── ScriptableObjects/      tunable configurations
│   ├── Materials, Textures/    first-party visual assets
│   └── Verification/           curated verification assets when tracked
├── XR/                         loader and simulator settings
├── XRI/                        interaction runtime/editor settings
├── ThirdParty/, Plugins/       vendor or external content
├── UniStorm 3.0/               vendor weather content
└── TextMesh Pro/               Unity package content
Packages/                       manifest and lock files
ProjectSettings/                Unity version, build scenes, XR settings
Archive/LegacyScenes/           historical scenes outside Unity import
~~~

There are 381 C# files under the first-party script tree, including editor,
runtime, and deprecated/test areas. Folders named Deprecated or Archive are
retained for migration/reference work and should not be added to the active
scene without an explicit compatibility review.

## Performance and operational limits

- Traffic processing is intentionally capped by range and maximum-target
  settings; increasing both raises CPU/UI work.
- Chart composites are generated at 1024 px by default. Larger composites or
  many simultaneous map-source changes increase memory and network load.
- Tile and traffic services have rate limits and variable latency. Use cache,
  backoff, and a mock/replay source for repeatable tests.
- Stream smoothing reduces jitter at the cost of latency. Tune the bridge's
  snap thresholds and prediction window for the simulator's update rate.
- Generated textures and linework are presentation aids. They do not guarantee
  geodetic, temporal, or sensor accuracy.
- Native headset output needs platform-specific frame timing and calibration
  testing. A desktop Game view screenshot is not equivalent to a headset
  acceptance test.
- Keep external traffic and weather credentials/configuration outside Git.
  Prefer environment variables, local ignored config, or deployment secret
  stores.

## Attribution and licensing

This repository does not currently include a root-level project license. Treat
the project code and assets as unavailable for redistribution unless the
project owners provide separate written terms. Vendor folders and packages
retain their own licenses.

The runtime can contact or display data from:

- FAA/ArcGIS VFR Sectional, Terminal Area, and World Aeronautical services;
- OpenStreetMap tile services;
- Airplanes.live traffic;
- NOAA and IEM weather services;
- MQTT publishers and the optional MqttWeather/Py-ART toolchain;
- Varjo XR, Unity XR Interaction Toolkit, Cesium, TextMesh Pro, and other
  third-party packages;
- NASA XPlaneConnect when the remote relay is used.

Keep the applicable attribution, usage, caching, and redistribution notices
with any deployment. Never embed private service credentials in a scene,
README, commit, or build artifact.

Additional implementation notes are available in:

- [project structure](Assets/_Project/Docs/PROJECT_STRUCTURE.md)
- [XP12 integration](Assets/_Project/Scripts/XPlaneIntegration/README_XP12_INTEGRATION.md)
- [remote relay](Assets/_Project/Scripts/XPlaneIntegration/README_XP11_REMOTE_RELAY.md)
- [traffic radar](Assets/_Project/Scripts/TrafficRadar/README.md)
- [compass bar](Assets/_Project/Scripts/CompassBarSystem/README.md)
- [indicator system](Assets/_Project/Scripts/IndicatorSystem/README.md)
- [MQTT weather](Assets/_Project/Scripts/MQTT/Weather/README.md)

## Contributing

1. Create a focused branch (the repository convention is codex/<topic>).
2. Keep first-party changes under Assets/_Project and preserve Unity .meta
   files and GUIDs.
3. Use Unity setup tools or a connected editor for scene/prefab changes.
4. Add or update an editor/runtime test when behavior changes.
5. Run the relevant compile, test, and visual checks; record environment
   limitations rather than hiding warnings.
6. Run git diff --check and review the staged file list.
7. Use Git LFS for large binaries and do not commit Library, Temp, Logs,
   UserSettings, builds, credentials, or local captures.
8. Commit with a focused message and push the topic branch for review.

For changes involving X-Plane, include the transport, host/port, snapshot age,
and fallback behavior used during validation. For changes involving XR, record
the editor platform, loader, simulator/native runtime, display topology, and
whether the result was desktop-only or hardware-tested.
