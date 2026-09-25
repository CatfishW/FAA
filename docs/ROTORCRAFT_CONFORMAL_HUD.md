# Rotorcraft conformal HUD / HMD

Implemented in `/Users/zlad/Development/FAA` for the FAA / S-76D research simulator.
This is not an FAA-approved instrument, an EFVS operational approval, or a landing-safety system.

## Presentation

`FaaConformalHudController` automatically installs `FaaRotorcraftConformalLayer` in Play mode
when the presentation is **Conformal**. No scene rewrite is required. `HeadFixed` restores
the existing presentation; the controller's `useRotorcraftSceneLayer` field also preserves
the earlier group-projection compatibility path. Existing instrument readouts, bank indication,
navigation command/deviation cues, heading tape, weather, traffic and Pilot Brief remain separate.

| Reference | Implementation |
| --- | --- |
| Horizon / pitch | Earth-horizontal, scene-rendered horizon; major 5-degree pitch bars, negative-pitch dashes, hooks and central half-step ticks. |
| FPV / FPM | Circle, wings and upper stem located on the measured ground-velocity direction. It does not use heading as a substitute for track. |
| Selected FPA | Dashed selected-elevation reference, initially -3 degrees; pilot-adjustable in 0.5-degree steps from -15 to +10 degrees. A reference, not a flight-director command. |
| Aircraft waterline | Aircraft-forward reference, not a geographic location and not a head-fixed reticle. |
| Landing / helipad | Explicit selected scene point plus declared area outline. The outline dimensions must be authored; a pilot raycast creates a declared 20m box, not a surveyed helipad. |
| Waypoint / obstacle | Existing scene-transform anchors; diamond waypoint, amber obstacle triangle, optional second endpoint for an authored wire/linear obstacle. |
| Hover | Head-fixed plan-view velocity instrument, right/forward axes relative to aircraft heading. A separate selected geographic hover anchor is world-referenced. |

Angular geometry is placed on a distant, camera-centered **world-oriented** shell. This avoids
translating the pitch/FPV with a screen-space instrument group. Geographic references retain
their actual scene-relative position and depth. Heading look offsets therefore change the
view of these references without dragging the fixed data around the screen. World-space labels
use native TMP text meshes and a resource-backed overlay material. They are sized by measured
font metrics and do not depend on per-camera Canvas batch rebuilding.

## Controls and authoring

The duplicate green reference/status dock has been removed at the pilot's request. No FPA/SCENE/HOVER/MARK LZ button strip is instantiated. The public `SetSelectedFpa`, `SetFpaVisible`, `SetSceneCuesVisible`, `SetHoverVisible`, `MarkLandingReference` and `ClearLandingReference` APIs remain for integration and inspector-driven configuration.
`MarkLandingReference` uses the camera's forward ray against loaded colliders, ignores own-aircraft children,
and declines placement when no suitable geometric intersection exists. It does not load missing terrain,
check rotor clearance, measure slope suitability, detect wires, or authorize a landing.

For persistent authored references, select a real scene object and use
**FAA > HUD > Scene Reference**. Choose Landing Area, Waypoint, Obstacle or Hover Reference.
Set the identifier, validity, declared area, optional line endpoint, range and selection in the
`FaaRotorcraftCueAnchor` Inspector. Landing/hover references require selection. These commands
use Undo and do not silently save your scene. Keep anchors under the same terrain/georeference
hierarchy as their physical objects, so floating-origin shifts move them together.

The existing radar selection supplies latitude/longitude but no target altitude. It is therefore
**not automatically converted into a 3D landing point at an invented elevation**. Likewise no
unverified obstacle database or synthetic wire detections are introduced by this change.

## Data and failure handling

The bridge now distinguishes true ground track (`sim/flightmodel/position/hpath`) from magnetic
heading (`mag_psi`). It uses ground speed and vertical speed to construct the ground-velocity
vector. The remote XPlaneConnect relay source also reads actual `hpath` and `y_agl`, instead
of substituting heading, an estimated AGL, or a linear approximation to flight-path angle.
The relay source was edited locally only; its aircraft-control loop is not launched by HUD setup.

New `AviationFlightData` validity flags distinguish missing attitude, velocity and AGL channels
from real zero-valued samples. Track interpolation recovers after an unavailable/NaN sample.
Other data producers must populate these flags before the new conformal layer treats them as valid.
Legacy and UI Toolkit FPV paths are guarded against invalid track values as well.

The conformal layer requires the bridge's healthy-feed flag and finite packet age no greater than
one second. This is a packet-level gate, not an independently certified per-sensor integrity system.
Stale/invalid attitude clears conformal geometry and displays an unavailable status. Missing ground
velocity suppresses FPV without inventing forward motion. An FPV outside the viewport is hidden
and annunciated, never clamped to a misleading edge position. Unknown engine values display `--`.
The new readout says **HAGL**, because `y_agl` does not establish a radar-altimeter sensor reading.

Hover enters below 5kt and exits at 8kt to avoid threshold flicker. Its vector supports lateral and
rearward motion and displays an off-scale indication rather than a clamped valid marker. These
thresholds, full-scale range and FPA bounds are simulator design choices, not FAA-mandated limits.

## Coordinate / XR assumptions

The default local tangent axes are Unity X east, Y up, Z true north. Assign `earthFrame` when the
scene uses a rotated tangent frame. Scene units must represent metres for anchor ranges and areas.
The render callback updates from the final camera pose; duplicate refreshes with identical frame
and pose are avoided. The dynamic line mesh and label pool are reused rather than recreated each frame.

The geometry uses native Unity world rendering with stereo shader support; it is not a mono
screen projection pasted over both eyes. The finite angular shell approximates, rather than proves,
optical collimation. Native XR controls use a world-space canvas and TrackedDeviceGraphicRaycaster;
the existing XR-3 compatibility component remains responsible for the fixed flight canvases.
The rig's XR input module/interactors, eye calibration, head-tracking latency, binocular registration,
near-field comfort and performance still require actual headset validation. No XR-3 player build or
hardware qualification is implied by a desktop Editor test.

## Validation

`Tools/ExplanationVerification/RunRotorcraftAssertions.cs` runs focused NUnit methods directly
inside the existing Editor. The final measured result is **142 passing Edit-mode assertions**,
including 37 new rotorcraft cases and 105 existing HUD/engine/view cases.
`RunRotorcraftRuntimeAssertions.cs` adds **27 passing isolated Play-mode component and UI
assertions**, including calibrated native text size and real-collider landing mark/clear interaction.
The total is **169 passing focused checks**, with a successful final Unity script compilation. These
are direct Editor assertions, not a complete Unity Test Runner/XML result or a whole-project test run.
Machine-readable final results and desktop captures are stored under `artifacts/rotorcraft-hud-20260924/`.

Desktop Play-mode checks use the existing scene and live X-Plane feed for forward-flight rendering.
Synthetic hover/stale/scene-reference cases are isolated fixtures, not claims about live flight states.
No unknown real landing areas or obstacles are inserted into the user's scene. The current scene can
report missing terrain coverage independently of this HUD; the new controls do not conceal that state.

The pre-existing dirty scene and worktree are preserved. No automatic scene save, commit, push,
remote relay deployment, simulator repositioning or release is part of this change.

## Implementation references

- X-Plane developer documentation, *Moving the Plane*: world velocity, derived speed/path values,
  and local/geographic coordinate relationships: https://developer.x-plane.com/article/movingtheplane/
- Unity XR Interaction Toolkit, *Tracked Device Graphic Raycaster*: canvas pointer interaction:
  https://docs.unity.cn/Packages/com.unity.xr.interaction.toolkit@3.1/manual/tracked-device-graphic-raycaster.html

These are implementation references, not a statement of regulatory compliance.
