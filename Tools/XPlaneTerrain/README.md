# X-Plane → Unity terrain

Unity generates geographically fixed relief from the **elevation raster in the
installed X-Plane scenery DSFs**. It does not move an unrelated terrain underneath
the aircraft and does not depend on Cesium native libraries.

This is a research visualization, **not terrain-clearance or flight guidance**.
The source DEM is not the final simulator collision mesh: airport flattening,
explicit per-vertex height edits, mesh triangulation, water surfaces, runways,
buildings, vegetation, and scenery textures are not reproduced. Colors are
synthetic elevation/slope shading, not satellite imagery, land-use classes, or
hazard bands. There is no terrain collider controlling the simulator.

## Data path

```text
X-Plane installation (read-only)
  scenery_packs.ini → highest-priority base DSF → elevation raster
       ↓ 127.0.0.1:8767 (Python service on the simulator host)
  SSH forward → 127.0.0.1:12679 (Unity machine)
       ↓ versioned, bounded JSON height tiles
  XPlaneTerrainStreamer → GeoPosUnityPosProjectManager → Unity meshes
                              ↑
                   live ownship latitude/longitude/MSL
```

The service needs Python 3.10+ and `7z` for compressed scenery. No third-party
Python dependencies, network elevation provider, API key, or simulator restart
is required. Installed scenery stays on the user's systems. Do not commit or
redistribute DSFs, generated height payloads, or proprietary scenery art.

## Run

On the simulator host:

```sh
python3 terrain_server.py --xplane-root '/path/to/X-Plane 12' --port 8767
```

On the Unity machine, keep this tunnel running:

```sh
sh Tools/XPlaneTerrain/start_tunnel.sh 4090
curl http://127.0.0.1:12679/health
```

For a persistent Linux user service, copy `terrain_server.py` to
`$HOME/.local/share/faa-xplane-terrain/`, edit the supplied
`xplane12-terrain.service` for the actual simulator installation, and install it
under `$HOME/.config/systemd/user/`. Then run:

```sh
systemctl --user daemon-reload
systemctl --user enable --now xplane12-terrain.service
systemctl --user status xplane12-terrain.service
```

The supplied service matches the existing Steam installation on `4090`. It
binds **loopback only**, uses a read-only filesystem sandbox, and has a 512 MiB
memory limit. It does not change X-Plane's weather, aircraft, flight, or plugins.

Open **ExperimentScene** and enter Play. Its existing `XPlane12TerrainSync`
creates `XPlaneTerrainStreamer` when **Generate Installed XPlane Terrain** is
enabled (default). In another scene, configure the normal X-Plane API bridge and
terrain sync, or add **X-Plane Integration → Runtime → Installed Scenery Terrain**
to a system object. The streamer must use the same aircraft and geo projection
as the live bridge. Source availability is independent of the telemetry port.

## Runtime behavior

- Waits for a healthy live aircraft position; does not generate at an editor
  preview/default coordinate.
- Prioritizes the tile below the aircraft, then nearby tiles. The default
  world-aligned quadtree covers **4°×4°** (about 370×444 km near Atlanta), with
  increasingly coarse distant coverage. It replaces the original 0.5° window
  whose outer edge was visible well before the camera clip.
- Uses 129×129 samples per 0.1° tile: about 72×87 m spacing near Atlanta,
  comparable to the installed 1201×1201, approximately 90 m DSF DEM. Increasing
  interpolation density cannot add source detail.
- 0.2° tiles use 65×65 samples; 0.4°/0.8° distant tiles use 33×33. These are
  progressively coarser samples of the same installed DEM, not invented terrain.
  The default tested layouts stay below 1.2 million vertices including skirts.
  One request/mesh upload at a time; no full-resolution horizon-sized grid.
  Near and distant radii are bounded to 1–3. The server caches at most four source
  DEMs, not the entire world. Cold distant coverage can take longer to load.
- The main camera far clip is **150,000 m** by default; terrain haze extends
  accordingly. The near clip is unchanged to preserve cockpit rendering.
  Coverage shrinks in metres toward the poles and unavailable scenery remains
  unavailable; clip distance is not a promise of source coverage in every direction.
- Coordinates are east/+X, up/+Y, north/+Z through the existing projection;
  heights are metres MSL. Both aircraft and terrain respond to origin rebasing.
  The local projection wraps longitude at the date line.
- Shared geographic edges are sampled identically, including negative tile
  indices. No terrain position or elevation is tied to the pilot's look direction.
- Downward edge skirts cover mixed-LOD cracks without altering source surface
  heights. Old parent tiles remain visible until all replacement children load;
  overlapping children are hidden during that transition, avoiding z-fighting.
  Origin rebasing moves every anchor together, then rebuilds one mesh per frame
  instead of rebuilding the whole distant landscape in a single frame.
- Retains valid existing tiles during transient failures, retries with bounded
  backoff, and discards responses superseded by an aircraft teleport. It never
  replaces missing scenery with a fabricated flat surface.
- Refreshes source tiles after five minutes, and offers **Refresh Installed
  Terrain** on the component. The server detects scenery-pack order and source
  file changes. This follows the installed files, not unsaved/unreloaded scenery
  changes inside X-Plane.
- Hides the old `XPlaneMappedTerrainAnchor` root and its fake underlay while this
  mode is active. Disabling the component restores those roots and the camera
  far clip. Existing scene assets are not deleted or rewritten.
- Uses the simulator altitude directly, without the legacy 120 m visual
  clearance offset. Ground-height discrepancies must not be “fixed” by lifting
  the aircraft; the source raster/final-mesh distinction is explicit.
- A small, non-interactive source label identifies synthetic X-Plane terrain,
  coverage/loading, retry, and stale-position states. It does not capture clicks.

## Source selection and validation

The reader honors enabled `SCENERY_PACK` priority, skips overlay-only DSFs, and
then considers demo/global scenery. An active custom **base** mesh without a
supported elevation raster returns `unavailable`; it never silently falls back
to the wrong global terrain. Areas without installed scenery also fail closed.

The reader validates the DSF cookie/version, atom bounds, MD5 footer, geographic
bounds, raster dimensions/encoding, scale/offset, post- versus area-centric
sampling, and finite elevations. Signed integer, unsigned integer, and float
rasters are supported. Null, no-data, corrupt, or unsupported rasters are not
treated as zero elevation. Coverage is limited to latitudes 85°S–85°N.

`GET /v1/terrain/tile?lat_index=337&lon_index=-829&resolution=129` returns a
0.1° tile whose southwest corner is 33.7°N, 82.9°W. Flat `heights_m` rows run
south-to-north, columns west-to-east. The response identifies MSL units, schema,
tile bounds, source DSF names, source revision checksums and raster resolution.
Unity verifies that identity before constructing a mesh. A `422` response means
coverage/format unavailable; `503` indicates an installation/read failure.

Optional `span=1|2|4|8` selects 0.1°/0.2°/0.4°/0.8° tiles while preserving the
southwest index convention. Expanded tiles use schema 2 and carry an explicit
`span`; 0.1° requests remain schema-1-compatible. Update the service on `4090`
alongside the Unity client. `/health` advertises `supported_spans`. Unity rejects
a response with a mismatched span rather than stretching a smaller tile.

The format is implemented from Laminar's
[DSF specification](https://developer.x-plane.com/article/dsf-file-format-specification/)
and its distinction between
[source DEM and rebuilt mesh](https://developer.x-plane.com/2011/08/dsf-gets-raster-data/).
An exact live physical-mesh implementation would require the native
[terrain-probe API](https://developer.x-plane.com/sdk/XPLMScenery/) and its own
loaded-scenery/performance validation. This module does not claim that precision.

## Verification

```sh
python3 -m unittest discover -s Tools/XPlaneTerrain -v
python3 Tools/XPlaneTerrain/verify_live.py
unity command eval_file Tools/ExplanationVerification/RunTerrainAssertions.cs \
  --project-path /absolute/path/to/FAA --format json
```

The live comparison refuses degraded/stale snapshots instead of interpreting
zero-filled telemetry as a real position. It reports the difference between a
sampled Unity triangle and the simulator's MSL-minus-AGL ground reference.

The last command executes focused NUnit assertions in the open editor, outside
Play mode; it is **not** a Unity Test Runner XML result. Tests cover the wire
contract, source validation, seams, north/east orientation, upward triangles,
MSL aircraft/terrain separation, rebasing, and date-line continuity. Python
fixtures are tiny, invented DSFs, not copies of licensed scenery.

For runtime acceptance, verify forward/side-window looks, movement across tile
boundaries, origin recentering, service loss/recovery, unsupported coverage,
component disable/re-enable and source-label readability. Compare sampled ground
heights with a simultaneous X-Plane MSL-minus-AGL observation; allow and report
the DEM-versus-final-mesh difference rather than claiming exact equality.

### Initial terrain validation — 2026-09-12 (before distant LOD)

- Python parser/API assertions: **14/14 passed**, using invented DSF fixtures.
- Unity terrain direct assertions: **9/9 passed**; existing view/cue/type/weather
  regression assertions: **96/96 passed**. These are direct assertion runs, not
  full Test Runner XML reports or native XR tests.
- Unity 6000.5.10f1 C# recompile: completed, `failed: false`.
- Live ExperimentScene: **25/25 tiles**, 416,025 vertices, ownship coverage true,
  no source errors; old `FAA_OPL_Terrain` root inactive. Main camera far clip
  increased to 60 km. An independent downward-looking QA camera confirmed
  rendered relief without altering the live camera or simulator flight.
- Loaded installed `X-Plane 12 Global Scenery/+33-083.dsf`, 1201×1201 samples.
  One source-raster spot-check measured 170.013 m MSL versus approximately
  172.100 m from the captured simulator MSL/AGL values (**−2.087 m**). This one
  observation does not establish an accuracy guarantee elsewhere.
- Later the simulator Web API became unavailable (`Connection refused`) while
  the separate elevation service continued serving the installed DSFs. The feed
  subsequently recovered without intervention from this terrain implementation.
  A fresh comparison near Atlanta measured **−1.682 m** between the sampled
  Unity triangle and the simulator's ground reference. No simulator restart,
  aircraft control, or weather changes were performed.
- A runtime source-outage test retained **25/25** verified meshes and ownship
  coverage while reporting `Terrain service unavailable`. Restoring the normal
  endpoint and refreshing returned to ready without discarding valid terrain.
- Disabling the streamer restored the original **8,888 m** far clip and legacy
  root. Re-enabling loaded **25/25** tiles, restored the **60,000 m** clip, hid
  legacy terrain again, and returned to ready with no source errors.
- Temporary QA captures are ignored under `Assets/Temp/`; no installed scenery
  or generated elevation payload is included in this change.

### Distance / HUD follow-up validation — 2026-09-12

- **16/16** Python source/API assertions and **16/16** Unity terrain assertions
  passed. Added cases cover expanded wire identity, mixed-resolution shared
  samples, skirts, bounded/no-gap LOD coverage including the date line/poles,
  and parent retention during partial child replacement.
- **9/9** new HUD stability checks and **96/96** existing view/cue/type/weather
  checks passed. Unity recompiled without C# errors. These are focused direct
  NUnit assertions, not a full Test Runner or native headset validation.
- Live default configuration loaded **91/91** tiles, **802,227** vertices,
  no source error. There were 15 terrain bounds beyond 60 km intersecting the
  camera frustum. Far clip was **150,000 m**, near clip unchanged at **0.3 m**.
- Steady-state live timing: 10.8 ms average / 15.9 ms 95th-percentile frame time
  over 180 frames; an isolated Editor hitch reached 90.6 ms. Cold loading has
  higher frame costs. This is an observation, not an FPS guarantee.
- Updated loopback service active on `4090`; observed peak memory approximately
  179 MiB, under its existing 512 MiB limit. Simulator flight was not changed.
- Fresh near-source comparison: **−0.767 m** versus X-Plane MSL-minus-AGL ground
  at one point. Distant LOD deliberately has lower sampling density; no accuracy
  claim is inferred from that near-source measurement.

## Troubleshooting / stop

- **No coverage:** check `/health`, the terrain tunnel, live position, installed
  DSF coverage and the service journal. A live telemetry feed alone is not proof
  that the terrain service is connected.
- **Smooth/low-detail surface:** this is the installed elevation raster, not
  imagery. No invented high-frequency relief is added.
- **Distant terrain missing:** wait for source loading, inspect tile count and
  retry status, and confirm `/health` advertises spans 1/2/4/8. Increasing only
  the camera clip cannot create terrain beyond installed/generated coverage.
- **Wrong airport or water height:** final mesh/flattening/water surfaces are out
  of scope. This view is not approved for landing/clearance guidance.
- **Shader unavailable:** ensure `Assets/_Project/Resources/Shaders/XPlaneTerrain`
  is included. The implementation targets this project's built-in render pipeline.
- **Disable:** turn off the streamer (or the terrain-sync auto-generation option
  for future sessions), stop the tunnel, and, if desired, run
  `systemctl --user disable --now xplane12-terrain.service` on the simulator host.
  Installed X-Plane scenery remains untouched.
