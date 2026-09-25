# Multi-finger gestures and spatial settings panels

## Changes

The laptop-camera worker now returns all 21 XYZ landmarks per hand, five joint-angle extension measurements, and four thumb-to-fingertip distances (index, middle, ring, little finger). The camera preview draws the full hand skeleton, clips it to the preview image and reports finger counts and the detected pinch. This uses the existing local MediaPipe hand landmarker, not an added cloud service or simulated hand device.

**Thumb + any finger** is the default gesture. Show two open hands, touch the thumb to any fingertip on each hand, hold briefly, then spread/close the hands. The selected fingertip is retained for the duration of that gesture to avoid a discontinuity from switching fingers while pinching. Release either pinch to finish.

**Open palms** is an explicitly selected alternative. Hold both palms open facing the camera, then separate or bring the palms together to resize. Curl the fingers or lower the hands to finish. At least three of the four non-thumb fingers must be extended on each hand. This mode avoids the thumb occlusion of a sustained pinch.

Resizing uses the separation of the palm centres normalized by palm size, not unstable fingertip distance between hands. Motion-adaptive filtering sets a scale target. The rendered HUD interpolates toward that target every display frame, instead of jumping only when the old 10 Hz recognition results arrived. Capture requests 30 fps, submits at most 24 recognition frames per second, and uses a maximum 480-pixel inference edge. These are configured limits, not measured live-camera throughput guarantees. One frame remains in flight at a time, so old images cannot accumulate in a queue.

A brief tracking loss freezes the size. A valid reacquisition within 0.22 seconds rebases the motion reference at the frozen factor; longer loss, a gesture release, a mode switch, locking, focus loss or camera stop requires a new gesture. Large tracking discontinuities are not applied as scale changes. The existing per-instrument size limits and proportionate group sizing are retained. Camera input changes only non-conformal instrument size, never radar placement or conformal flight-path calibration.

## Spatial panels

The large settings interface and Hand Studio are separate **World Space canvases**, not children of the main HUD overlay. The current defaults are +90 and -90 degrees, 8 degrees below the neutral seat reference, 1.5 metres away, with a 0.58-metre panel width. A whole-panel clearance constraint keeps their corners outside the protected 60-degree forward half-angle even when old saved layouts, dragging, depth changes or scaling would put them in front. They use the same cockpit/tracking origin as the radars; they do not follow head look. See `PERIPHERAL_HUD_STABILITY.md` for the current stability fix and measurements.

The settings interface has **Instruments** and **Spatial Panels** pages, with a direct **Hand Studio** button. The Instrument page provides precise size controls even while gestures are locked. The spatial page provides selectors for both radars, Settings and Hand Studio, with positioning/depth presets. Unlock gestures to drag a panel's top handle or use two tracked hands to resize. The PANEL +/- buttons resize a utility independently; STOW LEFT/RIGHT returns it outside the main forward HUD. Radio/map interactions remain on the original radar components.

A small recovery dock remains at the bottom right. **F9 / Settings** opens the side panel without relocating it. **LOOK LEFT** and **LOOK RIGHT** explicitly turn the desktop view toward Hand Studio or Settings; **FORWARD / R** returns it to the flight view. These actions never drive a tracked headset. In native XR, turn toward the side panel naturally. Recovery/stow now restores the safe side slot, not the current gaze. Opening Hand Studio does not open the camera or pull the panel into the forward view.

Opening, closing or inspecting utilities no longer changes flight-canvas render mode, camera or display depth. The previous focus-mode conversion was removed because it competed with normal HUD routing. Flight instruments are laid out in stable canvas-local coordinates, not by feeding their camera-projected positions back into their transforms. FOV, IPD, telemetry and conformal geometry remain unchanged.

## Camera permission and deployment

Camera permission, opt-in start, stop/cancel, camera selection and privacy behavior are preserved. Merely opening Hand Studio does not capture video. Closing it stops capture. The macOS native permission plugin is untouched. Webcam recognition remains a desktop feature; native XR uses the existing tracked-hand/controller input.

Worker output protocol 2 identifies `multi-finger-v2`. An old bundled worker is rejected with an explicit rebuild message before camera capture starts. Rebuild using `python3 Tools/WebcamGestures/setup.py --bundle`. The current Mac arm64 helper has been rebuilt and tested. Other platform helpers and a complete signed Unity player are not produced by this task.

## Validation boundaries

See `artifacts/multifinger-spatial/` for measured results. `FaaMultiFingerTests` checks all four fingertip pinches, open-palms activation, frame-rate-independent interpolation, stale/invalid input and occlusion recovery. `RunMultiFingerSpatialRuntime.cs` checks utility registration, world-space geometry, persistence, buttons, sliders and dragging without invoking a camera. The actual input-module mouse test uses a temporary Unity Input System mouse, not direct Button invocation. The native worker is separately tested against a public still photograph containing hands and against blank frames.

No user's camera images were captured. Still-image inference and injected gesture samples do not establish live-hand accuracy, native XR stereo usability or sustained laptop camera frame rate. These still require user/hardware trials under real lighting.

Primary reference for the hand model and returned landmarks: https://ai.google.dev/edge/mediapipe/solutions/vision/hand_landmarker/python . Runtime remains pinned to the project's tested MediaPipe 0.10.21 package.

## Measured results for this update

Unity 6000.5.10f1 compiled with zero script errors. The direct Editor fixtures passed 292 assertions, including 24 new multi-finger/filter cases; the new spatial utility UI fixture passed 24 checks, and 27 conformal-HUD runtime checks passed. Eleven Python protocol tests also passed. These are focused direct assertion runs, not a full-project Unity Test Runner XML report.

The actual active XRUIInputModule processed a temporary Input System mouse click on the world-space settings panel: altitude size changed from 72% to 77% with gestures still locked. The C# process bridge launched the rebuilt native helper, verified protocol 2, and received a real blank-image inference. Both Python and frozen workers detected two hands with complete finger arrays on the public reference photograph and zero hands on blank frames. The skeleton graphic produced 336 vertices from the real model output and cleared them when no frame was supplied.

The current-preview images show camera-OFF Hand Studio and the settings panel pulled toward the view for inspection, plus the unobstructed forward HUD after both panels are stowed. No physical webcam was opened, no user hand video was recorded, and no physical XR-3 validation or complete player release was performed. The existing terrain-coverage warning is unrelated to this interface work.
