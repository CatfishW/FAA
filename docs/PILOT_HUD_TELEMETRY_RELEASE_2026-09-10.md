# Pilot HUD and telemetry release — 2026-09-10

## Release identity

| Item | Value |
| --- | --- |
| Unity repository | `CatfishW/FAA` |
| X-Plane API repository | `CatfishW/xplane12api` |
| Branch in both repositories | `codex/pilot-hud-telemetry-release` |
| Unity editor used for final checks | 6000.5.10f1 on macOS/Metal |
| Simulator integration host | SSH alias `4090`, X-Plane 12 local Web API + RREF |
| Release status | Research/prototype update; not certified avionics or flight guidance |

This release consolidates the accumulated pilot-facing UI and simulator work
into one reviewable branch. It covers the HUD, traffic and weather radars,
sectional-chart presentation, screen cues, click-only evidence briefs, camera
alignment, X-Plane aircraft metadata, mixed AI traffic, rain, and turbulence
telemetry.

The two repositories are deliberately separate. `FAA` owns Unity rendering,
interaction, evidence presentation, and local simulator tooling. `xplane12api`
owns native X-Plane subscriptions, host automation, recovery, and the live
snapshot service. Both branches are needed for the complete integration.

## Safety boundary

This project is a simulation and research demonstrator. It is not FAA-approved,
not an operational TCAS/ACAS or weather radar, and not a substitute for an
approved chart, aircraft flight manual, ATC instruction, or certified display.
The application can explain captured evidence, but it does not issue flight
commands or expose aircraft-control tools to the language model.

Important distinctions preserved by the implementation:

- local **DISPLAY ON/OFF** changes UI visibility; it does not assert native
  radar, TCAS, network, or transmitter state;
- traffic altitude labels are relative altitude in hundreds of feet, not MSL;
- generated `SIM WX` pixels are illustrative and are not measured storm cells;
- rain/precipitation is not proof of turbulence;
- X-Plane regional and aircraft turbulence arrays are scalar layer samples,
  not a spatial turbulence scan;
- unknown, stale, incomplete, or non-finite values remain visibly unknown;
- screen-cue and map positions use the same observed target geometry, but that
  does not turn the prototype into a collision-avoidance system.

## Integrated data flow

```text
X-Plane 12 native Web API / RREF
            │
            ├── ownship, systems, engine and autopilot state
            ├── multiplayer geometry + native TCAS ICAO byte array
            └── weather + 13 regional and 13 aircraft turbulence layers
            │
            ▼
xplane12api snapshot service
            │  freshness, category views, retry/recovery, numeric raw values
            ▼
XPlane12ApiHudBridge
            │
     ┌──────┼──────────────┬───────────────┬────────────────┐
     ▼      ▼              ▼               ▼                ▼
    HUD  traffic radar  weather radar  screen cues    Pilot Brief
            │              │               │                │
            └──── shared range/bearing/altitude evidence ───┘
```

No source is silently substituted for another. Native X-Plane traffic takes
priority when healthy. A chart keeps its last successful image during a retry,
but its state and age remain available. Briefs use a frozen, allowlisted
snapshot and cite only evidence actually returned by their read-only tools.

## Pilot-facing changes

### HUD and flight symbology

- Replaced the stretched pitch-ladder bitmap with resolution-independent vector
  linework and TextMeshPro labels.
- Pitch spacing is angular and projection-aware. Numbered references are 5°
  apart, with restrained 2.5° subdivisions around the rotorcraft attitude
  region; bank and pitch movement follow the camera projection.
- Added explicit IAS/KT, ALT/FT, torque, engine N2, rotor NR, target-bearing,
  along-track, and signed vertical-speed semantics.
- Missing telemetry renders as a dash or explicit no-data state, rather than a
  cached value or plausible-looking zero.
- The desktop look control returns smoothly to the aircraft-forward view after
  the pilot releases look input. Native tracked-pose operation is not overridden.
- Added `FaaConformalHudController`. Conformal mode is now the default primary
  HUD presentation: aircraft attitude and the camera's no-look reference are
  projected into the viewport, while manual camera yaw/pitch is deliberately
  excluded. A pilot looking through a side window therefore sees the HUD move
  off-boresight instead of following the camera.
- The separate heading tape uses the same projected reference, keeping the
  navigation scale aligned with the primary attitude symbology during a look.
- Kept `HeadFixed` as an explicit compatibility mode. It restores the authored
  screen anchor for desktop familiarisation and regression comparison. A
  dormant world-space duplicate is only used when a scene opts into a calibrated
  layout; legacy scenes use the screen-projection path.

### Radar controls and visual hierarchy

- Reworked radar controls into compact, touch-sized pages with plain-language
  labels and original SVG line icons instead of letter badges.
- Kept weather and traffic controls collapsed until requested, and provided
  explicit expand, collapse, fullscreen, restore, recenter, range, map source,
  chart opacity, display, ring, and target actions.
- Added modern status headers and footers that distinguish live, stale, waiting,
  standby, display-off, and known radar-power-off states.
- Reduced continuous flashing and pulsing. Motion uses short, bounded fades and
  smoothing; important state changes remain immediate.

### Traffic, altitude tags, and screen cues

- Added SVG silhouettes for airliners, general aviation, helicopters, military
  aircraft, and unknown types, plus light/moderate/heavy rain-return glyphs.
- Added signed relative-altitude tags and vertical-trend arrows to radar tracks.
- Native TCAS ICAO metadata is decoded from the 512-byte array once per update.
  Slot 0 is ownship and slots 1–19 are AI only while `override_TCAS == 0`.
- A bounded catalog maps reported ICAO codes to display categories. Callsign,
  speed, altitude, and slot order are never used to guess an aircraft type.
- On/off-screen indicators now share target bearing, range, altitude, ownship
  reference, and freshness with the radar path. They update at presentation time
  and avoid reserved UI regions through deterministic decluttering.
- The cue panel can be expanded, collapsed, or hidden so it does not cover
  instruments. Its legend explains every target and weather symbol.

### Sectional chart and map interaction

- Increased the chart composite to 1024 px for a sharper enlarged radar view.
- Corrected map drag direction and converted screen pixels to canvas/map units.
- Registered geographic scale continuously within a tile level, so decreasing
  the NM range actually zooms in rather than only changing ring labels.
- Added 2 NM close-range support, bounded tile levels, cancellation, retry,
  generation checks, last-good retention, and explicit fallback status.
- Kept chart opacity, radar linework, map source, range, orientation, panning,
  ownship recentering, and fullscreen state independent.
- Fullscreen controls reserve their own layout space; related briefing content
  moves to a free side rail instead of blocking the chart.

### Weather, range, gain, rain, and turbulence

- Weather range changes a fixed north-aligned simulated field in nautical miles;
  the same feature therefore moves radially when the selected range changes.
- Echo gain operates over −8…+8 dB and changes echo amplitude. It does not change
  X-Plane precipitation or manufacture returns in dry weather.
- The host rain profile uses X-Plane 12 regional writable DataRefs, static
  weather, warm temperatures, 6 SM visibility, and modest wind.
- The host now publishes all 13 `sim/weather/region/turbulence[i]` settings and
  all 13 read-only `sim/weather/aircraft/turbulence[i]` samples. Unity accepts
  the documented X-Plane factor scale of 0…10, not a percentage.
- The current demo setting is 0.2/10. It is loaded from a small validated JSON
  profile and re-read during the periodic weather refresh, preventing the
  previous helper from resetting turbulence to zero.
- TURB mode does not draw rain as fake turbulence. It reports the regional and
  local-layer maxima and clearly states **NO SPATIAL SCAN**. WX+T may retain rain
  but identifies the absent turbulence scan.

### Click-only Pilot Brief

- Replaced typed prompts with four fixed pilot actions: **Traffic**, **Weather**,
  **Chart**, and **Status**. Opening the dock does not send a request.
- The compact dock and result card relocate around active map/radar controls;
  both can be collapsed, restored, or hidden.
- Responses stream into bounded Picture / Context / Limit briefs. Pagination
  keeps long output accessible without covering the HUD.
- Each request captures one fresh, allowlisted snapshot. Seven read-only tools
  expose traffic, weather, chart, status, and existing visual-analysis evidence.
- Citation auditing rejects unknown evidence IDs. Images are optional, scoped to
  the relevant chart/radar source, and kept in memory only for the request.
- The credential is read from `FAA_EXPLANATIONS_API_KEY` or the documented local
  keychain entry. No API credential belongs in Git, a scene, PlayerPrefs, logs,
  or this document.

## X-Plane host update

The matching API branch contains:

- an S-76-specific hold controller with bounded collective slew, native heading
  and altitude modes, governor/throttle setup, and crash handoff to the existing
  timed recovery path;
- a static rain/turbulence profile and tests;
- 12-aircraft native ATC traffic profile plus an apply-and-verify tool;
- TCAS ICAO byte-array decoding for REST and WebSocket snapshots;
- subscriptions for type metadata, `override_TCAS`, crash state, aircraft limits,
  engine instrumentation, and regional/local turbulence arrays;
- changed recovery logic that detects the native crash flag and avoids applying
  the legacy severe-weather profile to rotorcraft.

The verified AI profile is:

| Category | Models | Count |
| --- | --- | ---: |
| General aviation | C172, PA18, BE58, BE9L, SR22, C750 | 6 |
| Helicopter | S76, R22, S76 | 3 |
| Airliner | B738, MD82, A333 | 3 |

The Unity catalog also supports military silhouettes. A metadata-only F-4 copy
was tested, but the already-running simulator skipped the new model in its
aircraft catalog. It was removed from the verified live profile rather than
mislabeling another aircraft as military. The stock F-4 was not modified.

## Configuration

### Unity

1. Open `Assets/_Project/Scenes/ExperimentScene.unity` in Unity 6000.5.10f1.
2. Wait for package import and confirm compilation completes without errors.
3. Start the X-Plane API tunnel so `http://127.0.0.1:12678/v1/snapshot` is live.
4. Enter Play mode. Use radar headers for local display state, tap a radar for
   settings, and use the lower Pilot Brief dock for one-tap evidence actions.

### X-Plane host

The host repository README contains installation and systemd commands. For a
manual traffic-only update, validate before applying:

```bash
python3 xplane12/host/apply_mixed_traffic.py \
  --xplane-root "/path/to/X-Plane 12"

python3 xplane12/host/apply_mixed_traffic.py \
  --xplane-root "/path/to/X-Plane 12" --apply
```

The apply command changes only the native AI list, polls the TCAS byte array,
confirms every requested model, and verifies the ownship type is unchanged.

## Verification record

Final checks performed on 2026-09-10:

| Check | Result |
| --- | --- |
| Unity script import/recompile | Completed; no compiler errors reported |
| Direct edit-time assertion runner | 96 passed: view alignment 25 (including conformal side-look projection), screen cues 22, cue stability 17, traffic type metadata 17, turbulence mode 15 |
| Host Python unit tests | 21 passed: rotorcraft/weather 11, traffic metadata 6, WebSocket mapping 3, catalog recovery 1 |
| Native AI profile readback | 12/12 slots matched; ownship remained S76 |
| Unity live type ingestion | 6 General, 3 Helicopter, 3 Commercial |
| Turbulence telemetry | 13/13 regional + 13/13 aircraft samples, factor 0.2/10 |
| Evidence snapshot | Same two 13-layer maxima; `spatial_scan_available=false` |
| Flight health during final snapshot | Feed healthy; native crash flag zero |
| Source formatting | `git diff --check` passed for the release scope |

The direct assertion runner executes NUnit assertion methods in the connected
editor; it is not a Unity Test Runner XML report. Hardware XR, optical alignment,
full PlayMode regression, and a production build remain separate release gates.

## Known limitations and follow-up

1. **Rotorcraft stability under turbulence is not accepted.** A 90-second
   0.2/10 trial produced no crash, but 48 of 78 samples exceeded the existing
   demo envelope. Geometric altitude rose from about 9,844 to 10,935 ft and
   vertical speed reached +1,644 ft/min. The altitude/collective controller
   needs separate tuning before unattended use.
2. **No spatial turbulence scan exists.** The UI reports layer factors but does
   not invent cells, range, bearing, or severity categories.
3. **Military live-model coverage is pending.** The rendering/category path is
   tested, but the running simulator did not accept the new F-4 catalog entry.
4. **Sectional imagery can be unavailable or stale.** Coverage, provider errors,
   rate limits, and chart currency remain external constraints. Last-good image
   retention must not be confused with a fresh certified chart.
5. **Type metadata depends on native slot ownership.** When a plugin asserts
   `override_TCAS`, Unity abstains because native and displayed slot order may
   no longer correspond.
6. **Headset validation is incomplete.** Desktop Game view checks do not prove
   Varjo XR-3 or SA-147 optical alignment, latency, or multi-display behavior.
7. **The explanation model is fallible.** Evidence links show provenance for a
   captured snapshot, not regulatory approval or semantic correctness.

## Timeline

| Date | Milestone | Outcome |
| --- | --- | --- |
| 2026-06-26 | Live radar/headset integration | Aligned X-Plane data, HUD/radar updates, terrain/headset paths, and first remote integration fixes. |
| 2026-08-30 | Sectional-chart and focus foundation | Added multi-source draggable maps, attribution, fallback, sharper tiles, stable circular masking, radar linework, and fullscreen pilot focus. |
| 2026-08-31 | Pilot controls and navigation targets | Added contextual radar controls, explicit target preview/confirm/cancel, HUD target guidance, and reachable XR simulator controls. |
| 2026-09-01 | Engine instrumentation | Repaired, aligned, and labeled torque and rotor/engine bars with live-data semantics. |
| 2026-09-02 | Project documentation | Added the first comprehensive root README and operator/setup guidance. |
| 2026-09-07 | HUD and transport recovery | Refined the clean pilot presentation, radar status states, weather/range controls, and bounded API retry/recovery behavior. |
| 2026-09-08 | Evidence workspace | Added the streamed, source-linked explanation harness and then reduced it to the compact four-action, no-typing Pilot Brief requested for pilots. |
| 2026-09-09 | Map, cue, and briefing refinement | Improved chart zoom/pan behavior, control-panel placement, screen-cue decluttering, compact briefing layout, and fixed-height brief pagination. |
| 2026-09-10 | Geometry and simulator completion pass | Added view auto-return, exact radar/cue target geometry, native ICAO category mapping, mixed AI traffic, all-layer turbulence telemetry, truthful TURB status, tests, and release documentation. |
| 2026-09-11 | Conformal HUD presentation | Added aircraft-referenced HUD projection, independent of manual camera look, with a head-fixed compatibility mode and viewport calibration notes. |
| 2026-09-12 | Installed terrain and view stability | Streamed installed X-Plane elevation, extended coverage with distant LOD and 150 km clipping, and removed finite-anchor/camera-phase HUD jitter. |

## Repository hygiene included in this branch

The release removes previously tracked Python bytecode, local test output,
Codex/editor backups, embedded Unity recovery projects, crash artifacts, and
retired scene recovery copies. Ignore rules now cover imported XR samples,
`Assets/Temp`, `Assets/_Recovery`, embedded recovery roots, agent state, and
machine-local package settings. Authored Unity assets keep their `.meta` files.

Generated artifacts are intentionally excluded from GitHub. Reproducible tools,
source tests, deployment configuration, scene changes, package manifests, and
documentation are included.

Two serialized password fields in the Main scene were cleared before staging.
The release scan found no long API-key token, SSH/sudo password literal, or
private-key block in added/modified files. Because an earlier Git revision may
still contain a previously serialized credential, any credential ever stored in
that scene should be rotated; deleting it from the latest tree does not erase
Git history.

## Follow-up implementation notes

### 2026-09-12 — installed X-Plane terrain generation

Added a read-only DSF elevation service and bounded Unity terrain streaming.
The service reads the installed scenery-pack order on `4090`, validates DSF
rasters and source checksums, and serves geographic height tiles through a
separate loopback SSH forward. Unity uses the same geo projection and MSL
reference as ownship, with shared tile edges, longitude wrapping, origin
rebasing, source status, retry/backoff, and no invented flat fallback.

The old relocated terrain/underlay is suppressed in this mode; the artificial
120 m aircraft visual-clearance floor is bypassed. X-Plane's running flight,
weather and plugin configuration are untouched. Terrain colors are synthetic;
this is source elevation relief, **not** a copy of the final collision mesh,
airport flattening, water geometry, buildings or scenery textures.

Validation: 14 Python checks, 9 Unity terrain direct assertions and 96 existing
view/cue/type/weather assertions passed. Live rendering loaded 25/25 tiles;
outage retention, reconnection and reversible disable/re-enable were exercised.
Spot comparisons differed from simulator ground height by approximately 1.7–2.1
metres; those observations are not an accuracy guarantee or clearance approval.

See [terrain setup and limitations](../Tools/XPlaneTerrain/README.md) for the
service, tunnel, runtime settings, test commands and reversible disable steps.

### 2026-09-12 — HUD shaking and distant-terrain cutoff

The reported shaking had a reproducible rendering component, not just network
latency. The conformal HUD ran at execution order 10020, before the final camera
pose at 11100. It also projected a point 175.6 m in front of the **aircraft** while
camera translation was independently smoothed. That relative displacement
introduced artificial parallax into an angular HUD reference.

The screen path now projects rotation-only directions through the non-jittered
camera matrix, after camera updates (12050), and refreshes before rendering.
It adds no smoothing buffer, remains aircraft-relative during side-window
looks, and takes an invalid/behind-view reference off screen instead of freezing
it. Native tracked-head pose is not overwritten; its aircraft reference is kept
current. Per-eye/optical XR latency remains separately unvalidated.

Read-only 180-frame measurements at a 3840-pixel render width:

| Registration error | Before | After, terrain loaded |
| --- | ---: | ---: |
| Mean | 4.337 px | <0.001 px |
| 95th percentile | 9.387 px | <0.001 px |
| Maximum | 12.214 px | <0.001 px |

These were separate live windows, not an identical recorded-flight replay. The
new rotation/translation and final-pose regression tests also verify the
geometric invariants directly. Network packet age still averaged about 46 ms
in the final window; this change does not claim to eliminate transport latency
or genuine aircraft/turbulence motion. Loaded-state frame time averaged 10.8 ms
(95th percentile 15.9 ms); an isolated 90.6 ms Editor frame remained. Cold terrain
loading is heavier, so these figures are not a hardware performance guarantee.

The visible terrain boundary was the old 0.5° tile window, even though the camera
could see 60 km. The replacement world-aligned quadtree spans 4° by default,
preserves near-source detail, uses coarse distant tiles and seam skirts, and
holds parents until child replacements are ready. Far clipping is now 150 km;
the cockpit near clip remains 0.3 m. Origin reprojection is spread across frames
to avoid rebuilding the entire landscape at once.

Validation: **137 focused assertions passed** (16 Python, 16 terrain, 9 new HUD,
96 existing view/cue/type/weather). Live Unity loaded **91/91** tiles / **802,227**
vertices with no terrain error; 15 terrain bounds beyond the old 60 km distance
intersected the camera frustum. A fresh source-height comparison differed from
X-Plane MSL-minus-AGL by −0.767 m at one point; this is not a clearance guarantee.

The read-only terrain service on `4090` was updated to support bounded tile
spans. Its preceding script is retained as
`terrain_server.pre-distance-20260912.py` on that host. No aircraft controls,
weather settings or simulator plugins were changed for this fix. Source and
documentation changes remain local until explicitly committed/pushed.

Reproduce the HUD trace in Play mode with
`Tools/ExplanationVerification/CaptureHudTiming.cs`; it writes an ignored
`Temp/faa-hud-timing.json`. Run the view/terrain assertion scripts outside Play.

## Rollback and recovery

- Use Git revert on this branch after review; do not replace a working tree with
  a destructive reset.
- The 4090 deployment retained dated `*.bak-mixed-types-20260910` and
  `*.bak-turbulence-20260910` source backups for emergency comparison. Those
  machine-local backups are intentionally not committed.
- Restarting `xplane12-data-api.service` reloads subscription changes.
- Restarting `xplane12-autoflight.service` reloads controller/profile code. The
  deployment restart used `--skip-air-start`, so it did not reset the active
  flight; normal future crash recovery retains its configured air-start path.
- If telemetry IDs become invalid after an X-Plane restart, the API client
  invalidates the session catalog and resolves fresh native IDs.

## Reviewer checklist

- [ ] Inspect both repositories at `codex/pilot-hud-telemetry-release`.
- [ ] Confirm no credential, `.env`, recovery tree, cache, or screenshot is staged.
- [ ] Open ExperimentScene in Unity 6000.5.10f1 and confirm a clean compile.
- [ ] Run the documented Unity EditMode and PlayMode suites and archive XML
      outside the repository.
- [ ] Verify traffic types, range, chart pan/zoom, camera return, and cue/map
      alignment against a fresh X-Plane snapshot.
- [ ] Verify Conformal mode with forward, left-window, and right-window looks;
      confirm the primary HUD leaves boresight with the outside view while
      remaining tied to aircraft attitude. Compare HeadFixed mode for regression.
- [ ] Exercise WX, WX+T, TURB, standby, stale, display-off, and unknown-power states.
- [ ] Tune and re-run the rotorcraft stability acceptance test before unattended
      turbulence demonstrations.
- [ ] Test native XR-3/SA-147 output on the intended hardware.
