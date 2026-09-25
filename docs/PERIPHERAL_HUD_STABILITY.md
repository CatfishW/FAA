# Peripheral utility panels and stable flight instruments

## User-visible behavior

Settings and Hand Studio stay beside the cockpit when opened. They no longer automatically move toward the user's gaze. The default positions are 90 degrees right/left, 8 degrees below the neutral seat, 1.5 metres away, and 0.58 metres wide. Their complete rectangular extent is protected from entering a forward cone with a 60-degree half-angle and a 3-degree design margin. This is a UI-clearance policy, not an aviation operating limit.

Protection is applied to restored legacy positions, direct movement, presets, closer/farther actions and scale changes. A very large or close panel may move farther sideways or behind the user rather than intruding into the forward view. Legitimate saved peripheral poses are retained. Radar layouts are independent and are not subjected to this new utility-only constraint.

**SETTINGS / F9** opens or closes the right-side Settings panel without moving it into view. On a laptop, **LOOK LEFT** explicitly turns the desktop view toward Hand Studio; **LOOK RIGHT** turns it toward Settings. **FORWARD / R** returns to the flight view. These are deliberate view changes, not panel relocation. Native XR head pose is never driven by those desktop actions. Headset users turn toward the panels naturally. The top handles, size buttons, tabs, all-finger recognition, camera permission controls and saved profiles are retained.

Recovery through the old `BringUtilityHere` API now restores a safe side slot. Opening Hand Studio does not activate its camera. Existing camera stop/permission/privacy behavior is unchanged. Live camera and physical XR-3 tests were not performed for this update.

## Shake root cause and correction

`FaaUtilityFocusPresentation` changed the flight canvases from Screen Space Overlay to Screen Space Camera and repeatedly changed their depth while a utility was visible. Other HUD/XR components retained ownership of those same properties. The flight reflow also read camera-projected graphic positions and fed screen-to-world corrections back into its transforms. Those two mechanisms allowed a moving camera, render callback timing and display-mode changes to disturb non-conformal layout.

The focus-mode conversion has been removed, including its caller. Utility visibility no longer changes any flight-canvas render mode, event camera or plane distance. `FaaNonConformalReflow` now measures and arranges graphic bounds in a common canvas-local frame. `FaaCanvasLocalGeometry` composes local transforms only; it never round-trips through world coordinates or camera projection. Negligible positional corrections do not dirty the layout. This also avoids large-world-position cancellation errors.

Utility canvas positions are calculated absolutely from their cockpit-local geometry rather than integrating `desired - currentWorldCentre` every frame. Desktop panels also use the camera controller's smoothed aircraft reference, explicitly excluding its manual look offset. Using raw telemetry rotation for panels while filtering the cockpit camera made controls move under a stationary mouse during side inspection. Native XR continues to use its captured tracking-space seat. Camera-relative geometry and the conformal horizon, FPV, FPA and scene anchors retain their separate existing implementation.

Unity render-mode background: https://docs.unity.cn/6000.0/Documentation/Manual/class-Canvas.html
Render-event reference: https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/canvas/willrendercanvases

## Reproducible validation

`FaaPeripheralStabilityTests` covers complete-panel corner clearance at different scales/depths/elevations, bad saved data, idempotent constraints, camera-independent local bounds and explicit desktop inspection/reset. Existing camera-permission, multi-finger, radar, HUD and projection fixtures are also run through the direct Editor assertion harness.

`Tools/ExplanationVerification/verify_peripheral_frames.py` starts a nonblocking live-render probe and waits outside the Editor thread. It observes 480 real rendered frames and samples 360 of them across six phases: both panels open, panel visibility changes, legacy recovery, extreme panel sizing/depth, explicit side inspection, and return to the forward view. It measures the actual screen bounds and local positions of airspeed, altitude, rotor/engine RPM, vertical speed, torque and along-track/glideslope UI. It also measures actual utility world-corner angular clearance and flight-canvas mode/depth consistency. It does not open a webcam, change flight telemetry, save the scene or send simulator commands.

`verify_spatial_settings_mouse.py` uses a temporary Unity Input System mouse with the actual XR UI input module to click the side Settings panel, verifies a 72% to 77% altitude resize with gestures locked, and restores input/layout state. This is not a direct invocation of the Button callback.

The verification report and per-frame data are in `artifacts/peripheral-stability/`. The before-change diagnostic is preserved separately from after-change measurements. Previews are `Assets/artifacts/peripheral-stability/forward-view-panels-open.png` and `settings-side-inspection.png`. The forward screenshot is taken with BOTH utility panels active, not merely hidden to fake clearance.

These are Editor tests and measured rectangle-layout stability, not optical calibration, native stereo headset validation or a live-camera hand-accuracy study. The existing terrain-coverage warning is unrelated and has not been modified. No complete player build or release is produced by this change.

## Measured results

Final script compilation: zero errors. Direct Editor fixtures: 310 passed (including 18 new peripheral/local-geometry cases). Spatial utility runtime checks: 24 passed; preserved conformal runtime checks: 27 passed. The independent real-input-module mouse test resized altitude from 72% to 77% while gestures remained locked.

The live-render probe sampled 360 frames out of a 480-frame run, across all six phases. Maximum measured x/y/width/height movement within each phase across the six fixed instrument groups was 0.0 pixels; flight canvases retained one render-mode/depth configuration. The closest measured utility corner remained 73.52 degrees from forward, including the extreme close/large-panel test. This exceeds the configured 60-degree protected half-angle. These measurements do not freeze telemetry values; they measure instrument geometry, not the moving needles or changing numbers.

Both panels are active in the final forward preview but remain at their side positions. The side-inspection preview intentionally turns the desktop view toward Settings, without dragging the panel into the forward cockpit sector. The native camera was not opened. Final machine-readable evidence is `artifacts/peripheral-stability/peripheral-stability-verification.json`.
