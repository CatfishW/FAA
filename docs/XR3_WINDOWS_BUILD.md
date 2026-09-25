# FAA Varjo XR-3 Windows release

## Run on the headset PC

Extract the entire release ZIP to a writable local folder on the Windows PC
connected to the Varjo XR-3. Keep the executable, `FAA-XR3_Data`, `UnityPlayer.dll`,
and `MonoBleedingEdge` together. Start Varjo Base, confirm that the headset is
connected and tracking, then run `Launch-XR3.cmd`. The launch script selects
native XR-3 routing and Direct3D 11 and writes diagnostics to
`%LOCALAPPDATA%\FAA-XR3\Player-XR3.log`.

Use **Varjo Base 4.14** for the XR-3. The bundled Varjo XR plugin 3.7.3 requires
Base 4.6 or newer, but XR-3 support ends at the 4.14 release family. Base 4.15
and later target XR-4 instead; do not update the XR-3 PC indiscriminately.
The headset PC needs the Varjo-compatible GPU, drivers, cabling, and tracking
setup. See the vendor's release notes and system requirements for that PC.

Run `Check-Terrain.cmd` to check the independent terrain connection. When the
source is on the simulator host, run `Start-Terrain-Tunnel.cmd` with the Windows
PC's configured SSH alias and leave that window open. The host must already run
the read-only terrain service on port 8767. No passwords or keys are bundled.
For another trusted endpoint, edit `FAA-XR3_Data/StreamingAssets/FAA/TerrainConnection.json`,
set `FAA_TERRAIN_URL`, or pass `--terrain-url URL` to the launcher. See
`TERRAIN-CONNECTION.md` and `HUD-TERRAIN-UPDATE.md`. Flight telemetry and terrain
use separate connections; a working HUD does not imply terrain is connected.

The current FAA source scene, HUD, traffic/weather interfaces and X-Plane terrain
code are included. X-Plane, terrain/data services, local datasets and network
endpoints remain external dependencies; the executable does not install them.
This release retains the project's opaque VR presentation. Passthrough/MR
composition, controllers and other optional headset functions require their
own configuration and physical validation.

This update includes the in-game Digital/Classic Analog selector, protected
side-mounted radars/settings, right-button panel dragging, and the native
Ultraleap hand-input path. The optional laptop-webcam recognizer is separately
packaged per operating system; a Mac helper does not run in this Windows build.

## Build profile and provenance

The release uses Unity **6000.5.10f1**, Windows x86-64, the Mono scripting backend,
Direct3D 11, Linear color space, and the native **Varjo XR plugin 3.7.3**. It is
not a Development Build. The existing single-pass/foveated Varjo settings are
retained. The validated FAA ExperimentScene is the only scene in this release.

The build scene processor removes both XR Interaction Simulator implementations
from the serialized player scene and disables automatic simulator activation.
It edits only the build's in-memory scene, not the source scene. Explicit
`--xr3-sim` developer functionality remains in code but is not the release default.

Legacy Post Processing Stack v1 camera effects are disabled in the build scene;
their intermediate render textures are not a validated Varjo quad-view path.
Desktop scene settings are unchanged. Depth texture declarations in the legacy
shader resources use Unity's stereo-aware declaration macros, and lightning
soft-particle depth sampling receives the correct stereo eye index. These fixes
remove Direct3D stereo shader compilation errors without claiming that the old
post-processing stack has been completely ported to native XR.

The source workspace's embedded Cesium 1.20.0 has custom Unity compatibility
changes but lacks the matching native binaries. The isolated release snapshot
therefore uses the **complete official Cesium 1.25.1 package**, which includes
Unity 6.5 compatibility and matching generated bindings/native libraries.
The original embedded package and uncommitted application changes are preserved.

`build-report.json` records Unity's actual build result. `SHA256SUMS.txt` covers
the release files; the adjacent `.zip.sha256` verifies the archive. A successful
build and binary/package validation do **not** prove physical XR-3 operation.

## Rebuild on the Mac

Close the source Unity Editor first. From the FAA project root, run:

```bash
bash Tools/XR3/build-release-macos.sh
```

Install the exact Unity version and its Windows Build Support (Mono) module.
The script uses APFS copy-on-write clones of the current Assets, Packages,
ProjectSettings and optional Library into `_artifacts/xr3/<UTC timestamp>/project`.
It pins and verifies the Cesium package, builds the release, validates x64 native
plugins and ZIP integrity, and writes `Builds/FAA-XR3-<UTC timestamp>-Windows-x64.zip`.
Logs and source-diff evidence remain in the timestamped artifacts directory.
Set `UNITY_EDITOR` to an alternate path for the same installed editor version.
No commit, push, server deployment or source-package replacement is performed.

## Physical acceptance check

Before a pilot demonstration, verify both-eye rendering, stable head pose,
eye calibration/foveated output, HUD angular placement and readability, input
behavior, tracking loss/recovery, and sustained frame timing on the actual PC.
Check the real X-Plane/data connection and stale-feed behavior separately.
This is research/prototyping software, not a certified flight instrument.

## Vendor references

- Varjo compatibility: https://developer.varjo.com/docs/unity-xr-sdk/compatibility
- Varjo Base 4.14: https://support.varjo.com/hc/en-us/release-notes-varjo-base-4.14
- XR-3 support boundary: https://support.varjo.com/hc/en-us/end-of-support-for-older-generation-models-xr-3-vr-3-and-varjo-aero
- Cesium 1.25.1: https://github.com/CesiumGS/cesium-unity/releases/tag/v1.25.1
