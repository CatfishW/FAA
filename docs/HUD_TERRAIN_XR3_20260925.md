# Classic HUD readability and terrain connection — XR-3 release update

## Changes

The Classic Analog vertical-speed scale now puts its four numeric tick labels in a dedicated left-aligned column to the right of the scale. The column begins at reference X=39, beyond both the right rail at X=15 and the pointer endpoint at X=29. Its container includes the labels, so per-instrument and gesture scaling preserve this separation. The scale, pointer and live vertical-speed values remain unchanged.

The reported missing terrain was a disconnected local SSH terrain forward, not a stopped simulator service. The simulator's read-only elevation service on port 8767 was already healthy. On the development Mac, `Tools/XPlaneTerrain/install_macos_tunnel.py --host 4090` installs a user-owned launchd agent that keeps the loopback-only 12679-to-8767 forward alive and reconnects it. Existing SSH keys/configuration are used; no credentials or scenery are copied. `--remove` removes only that agent. No simulator flight commands or remote service changes are involved.

Terrain now accepts an operator endpoint through `FAA/TerrainConnection.json` in StreamingAssets, the `FAA_TERRAIN_URL` environment variable, or `--terrain-url URL` (in increasing priority). Invalid URLs/configuration fail closed. The source status distinguishes an unavailable connection from absent scenery coverage. It never hides that problem behind fabricated terrain.

## Windows XR-3 setup

Extract the whole ZIP and start Varjo Base on the headset PC. Run `Check-Terrain.cmd`. The terrain channel is independent of the aircraft telemetry channel: connected instruments do not imply a working terrain connection.

For terrain served by the existing simulator host, run `Start-Terrain-Tunnel.cmd`, supply the Windows PC's configured SSH alias, and leave its window open. The PC must have OpenSSH Client and its own authorized SSH key/known-host configuration. The host must already run the read-only terrain service on localhost:8767. No credentials are bundled. Alternatively, set an explicitly configured trusted service endpoint in `FAA-XR3_Data/StreamingAssets/FAA/TerrainConnection.json`, `FAA_TERRAIN_URL`, or `--terrain-url`.

Run `Launch-XR3.cmd`; it reports connection readiness before launching the native Windows x64 player. A missing terrain connection is announced rather than silently treated as valid coverage. No installed X-Plane DSF data or generated height tiles are distributed in the release.

This build uses the current `ExperimentScene`, including switchable Digital/Classic Analog symbology, protected side-mounted radars and settings, and right-button/Ultraleap hand dragging. The native XR-3 hand path uses the bundled Ultraleap integration. The optional laptop-webcam feature requires a platform-matching local recognizer: the existing Mac arm64 helper is not a Windows helper and is not advertised as one.

## Measured local validation

The source Unity Editor compiled with zero script errors. The combined HUD suite passed 366 direct Editor assertions, including 13 new label-spacing and terrain-configuration cases. A separate terrain geometry suite passed 16 assertions. Play-mode checks passed 29 analog, 24 radar/drag/Pilot Brief, and 27 conformal assertions. Python terrain tests passed 16 cases. These are focused direct checks, not a full-project Test Runner certification.

The conformal runtime fixture now establishes its own full-conformal presentation and restores the prior user selection afterward. It no longer depends on whether the pilot had selected Classic Analog's intentionally non-conformal attitude display.

After restoring the tunnel, the actual live scene loaded **97/97** source elevation tiles and **810,345** vertices. Ownship coverage was true, no source error was reported, and 26 mesh bounds intersected the forward-camera frustum at the sampled pose. The camera far clip was 150 km. An actual game-view capture confirmed terrain and the corrected label layout; no image-generation model or webcam capture was used.

One simultaneous DEM-versus-simulator-ground spot check gave 223.996 m MSL from the rendered triangle and 228.271 m from simulator MSL minus AGL, a -4.275 m difference. This is a source-raster/final-mesh comparison, not a terrain-clearance or accuracy guarantee. Shading is synthetic and does not reproduce X-Plane's scenery textures, buildings, final mesh edits or airport flattening.

Windows build/package results and source commit are recorded with the release artifacts. Native XR-3 optical alignment, both-eye rendering, physical hand performance and sustained headset-PC frame rate require on-hardware acceptance. This is research/prototyping software, not certified flight equipment.
