# Pilot spatial workspace — XR-3 / FAA Unity

Current side-panel placement and jitter correction: see `PERIPHERAL_HUD_STABILITY.md`; multi-finger recognition is described in `MULTIFINGER_SPATIAL_STUDIO.md`. The sections below document the original implementation. Current guides supersede the original overlay placement, automatic Bring/Recall and index-only/10 Hz recognition flow.

This is a research-simulator user-interface implementation, not certified flight equipment. Native XR-3 optical calibration, pilot workload and hardware ergonomics still require on-device evaluation.

## Pilot controls

Open **LAYOUT** on the upper-right dock below the scene-cue card, or press **F9** on a keyboard. Gestures and dragging start **LOCKED**. Explicit size buttons, the size slider and RESET SELECTED work without unlocking; choose **EDIT / LOCK** only to enable gesture/drag manipulation. Closing the menu is separate from locking; lock gestures when finished. The initial module selection is airspeed, and invisible legacy modules are excluded from the picker.

With a tracked Ultraleap hand, hold an open left palm facing the headset for approximately 0.9 seconds to open the menu. This does not unlock anything. Aim the hand ray at a radar, pinch thumb and index finger, and move the hand to reposition it. Push/pull changes depth. A second pinch on the same target changes size through hand separation. Releasing either hand ends paired manipulation; release both before starting another gesture. Hands must remain tracked: turn toward a side/behind panel before manipulating it. Losing hand tracking preserves the last position and requires a new pinch.

For a non-conformal instrument, point and pinch to select it; move the pinched hand up/down to resize, or use two pinches and spread/close the hands. The menu also offers a size slider and +/- buttons. The radar grip supports mouse and standard Unity UI pointer/controller dragging; scroll over the grip changes size. Tracked XR controllers have a trigger-ray fallback, and the left controller menu button opens the layout panel.

**RECALL**, or **F10**, brings both radars near the current view. **RECENTER SEAT** re-establishes the seated tracking-space reference. **RESET SELECTED** resets only that module; **RESET LAYOUT** restores all defaults. **Escape** locks manipulation. Explicit LEFT / DOWN / RIGHT / BEHIND buttons place the selected radar around the seated user; NEARER / FARTHER changes its depth. Placement/depth controls are not enabled for non-conformal scale-only instruments.

## Radar placement and rendering

The original weather and traffic canvases become independent World Space canvases. Their existing radar image, map, header, conditions, configuration drawers and buttons remain the same objects; the implementation does not create a second telemetry source or duplicate radar. Normal map panning and radar settings continue to work when layout editing is locked. In detailed-map mode, the physical panel footprint remains pilot-controlled rather than suddenly filling the headset.

Default XR/simulator placement: weather at -55 degrees azimuth, traffic at +55 degrees; both at -28 degrees elevation and 1.2 metres from the seated reference. Default radar diameter is 0.42 metres. Layout yaw spans 360 degrees; elevation is bounded to +/-80 degrees, depth to 0.55–3 metres, and size multiplier to 0.40–1.60. These are UI usability bounds, not aircraft performance limits. Individual presets, positions and sizes are editable.

Native tracked presentation uses a captured neutral seat in the **actual camera tracking space**. Head translation and rotation do not drag the panels along. This works for a stationary mixed-reality cockpit and for an XR-origin rig attached to a virtual aircraft. The FAA camera is not assumed to be directly parented to the aircraft. Tracking-origin changes and the recenter action re-establish the reference. Desktop mouse-look preview uses the smoothed aircraft camera position as its untracked seat, avoiding a several-metre mismatch between camera smoothing and the aircraft transform.

The 3D PANELS control restores the original desktop canvas presentation when switched off. The XR routing component and HUD sanitizer respect spatial ownership; they do not force pilot-owned radars back to a head-fixed overlay. Canvas properties are restored when the workspace is disabled or its scene unloads.

## Non-conformal instruments

Ten core modules are independently sized: airspeed, altitude, vertical speed, torque, rotor/engine RPM, glideslope deviation, localizer/course deviation, bank/command references, the legacy heading panel, and the separate heading/compass tape. XR/simulator defaults use 72% of their authored scale; desktop-only defaults retain 100%. **ALL HUD +/-** changes these modules together, not the conformal layer. Hidden legacy modules remain hidden; changing their size does not enable them.

The conformal horizon, pitch ladder, flight-path vector, selected FPA and geographically registered scene cues are not scaled, reprojected or moved by these controls. Camera FOV, IPD and lens distortion settings are untouched. Native non-conformal camera-space canvases now default to a 1.2-metre UI depth, configurable on `XR3HeadsetCompatibility`; this changes virtual display depth, not optical calibration or conformal angular geometry.

## Persistence and input safeguards

Positions and module sizes are saved locally through PlayerPrefs using a versioned scene-specific XR/Desktop profile. Changes are debounced; lock/release saves the result. The implementation rejects malformed, oversized, duplicate-ID and unsupported-version profiles and rejects non-finite geometry. Unknown module IDs are ignored on import; no live telemetry is stored. The workspace does not save the Unity scene or write any simulator flight controls.

Layout editing is explicit. Pinch strength has separate begin/release thresholds. Missing, stale or out-of-order samples cancel ownership; reconnecting while still pinched cannot reacquire a panel or click a button. UI pointer cancellation does not emit a click. Focus loss locks manipulation. The head-fixed dock remains available to recover a misplaced radar.

## XR-3 runtime setup

XR-3 supports VR and video-passthrough mixed reality; the active mode depends on the Varjo runtime/application. This change does not automatically enable/disable passthrough. In Varjo Base, enable **Settings > Headset > Hands > Hand tracking**. XR-3 has integrated Ultraleap tracking; Varjo Base supplies its drivers. Do not install a separate Gemini driver for this integration.

The project embeds Ultraleap Tracking **7.3.0**. A Windows native XR-3 session creates a `LeapXRServiceProvider` only when no existing active provider is available. It uses Varjo's documented XR-3 manual offsets: Y -0.0112 m, Z +0.0999 m, X tilt 0 degrees. Existing active providers are reused. CurrentFrame positions are already world-space; they are not transformed a second time. No synthetic hand device is created on the Mac editor. Without hardware/provider data, the controls report hands unavailable and retain mouse/controller interaction.

The embedded SDK preserves its Apache-2.0 license. Its editor reference comparison was changed from removed `GetInstanceID()` calls to Unity object equality for Unity 6000.5 compatibility. Tracking algorithms and native binaries are unmodified. No external tracking service or remote simulator was installed/restarted.

Primary references: https://developer.varjo.com/docs/v4.14.0/unity-xr-sdk/hand-tracking-with-varjo-xr-plugin ; https://support.varjo.com/hc/en-us/using-hand-tracking ; https://github.com/ultraleap/UnityPlugin .

## Validation

Focused NUnit assertions are invoked by `Tools/ExplanationVerification/RunSpatialAssertions.cs` with setup/teardown lifecycle. Live UI/gesture fixtures are `RunSpatialRuntimeAssertions.cs` and `RunSpatialPointerAssertions.cs`. They use injected test rays/poses and restore state; they are not measurements from physical hands. Reports and Editor previews are under `artifacts/spatial-workspace/` and `Assets/artifacts/spatial-workspace/`.

Final local validation: Unity 6000.5.10f1 compiled with zero script errors. The direct Editor runner passed 225 assertions, including 34 new spatial-layout/seat-reference cases and existing radar/conformal regressions. Play-mode UI tests passed 34 spatial integration checks, 12 pointer/gesture checks, and 27 existing conformal-HUD checks: 298 focused assertions total. These are direct assertion runs, not a full-project Unity Test Runner XML report.

The pointer tests exercised actual UI button dispatch, cancellation without clicks, one/two-hand instrument sizing and the UGUI panel grip. They exposed and led to a fix for the scaled Editor Game view's render-pixel/display-pixel mismatch and freshly activated graphics with an unassigned draw depth. The fallback still honors active state, clipping and Graphic raycast filters. Native tracking-space seat tests confirmed head translation/rotation does not move a panel, while moving the tracking origin moves its seat reference.

Final Editor previews: `Assets/artifacts/spatial-workspace/spatial-layout-final.png` (forward view, airspeed-size editor) and `Assets/artifacts/spatial-workspace/spatial-radar-side-final.png` (looking toward the right-side traffic panel). No physical hand sensor was connected to this Mac. The previews retain the pre-existing terrain-coverage warning; this UI task did not alter or deploy the terrain service.

See `artifacts/spatial-workspace/verification-summary.json` for machine-readable evidence. Windows player compilation, native stereo/quad-view rendering, optical alignment, controller mapping, real pinch accuracy, hand occlusion and sustained XR-3 frame rate must still be checked on the headset. No claim of physical XR-3 validation or FAA certification is made.
