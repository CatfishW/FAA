# Laptop-camera gestures for the main HUD

Current multi-finger recognition and spatial panel behavior: see `MULTIFINGER_SPATIAL_STUDIO.md`. The sections below document the original implementation; the new guide supersedes the original overlay panel placement and index-only/10 Hz recognition flow.

## Use

In ExperimentScene Play mode, open **LAYOUT > LAPTOP CAMERA GESTURES**. The camera starts OFF. Opening the panel lists devices and permission status; it does not capture video. On macOS, **ALLOW CAMERA** requests the native permission without starting capture. Choose Allow in the system dialog. If access was previously denied, **CAMERA SETTINGS** opens the macOS Camera privacy pane. Select **NEXT CAMERA** if necessary, then **START CAMERA** (which also requests permission first when needed). Choose **GESTURES / LOCK** to unlock gesture resizing; explicit HUD +/- and slider controls now work while gestures remain locked.

Show both hands within the preview and open the thumb/index pinches briefly. Then pinch thumb and index finger on both hands and hold for approximately a quarter second. Spread the two pinches apart to enlarge the main HUD; bring them together to shrink it. Release either pinch to keep the size. Open both pinches before grabbing again. Green dots indicate the detected pinch centres in the mirrored local preview.

These gestures resize the ten registered non-conformal main-HUD modules together, retaining their individual size proportions. They do not change radar placement, radar size, flight data, horizon/pitch angular scale, FPV, FPA reference or geographic scene cues. The existing per-module size controls and ALL HUD +/- remain available. Individual scales stay within the existing 40–160% limits. A module at its limit can constrain proportional group scaling.

**STOP / CANCEL** releases the webcam and recognition process and cancels pending start intent. **CLOSE + STOP**, scene changes, disabled workspace, application focus loss and an active native XR session also stop capture. A pending native permission dialog may temporarily take focus before any capture; the explicit Start action can continue when permission is granted and the app is focused again. The camera-on preference is never saved; OS consent itself remains managed by macOS. Saved instrument sizes continue using the existing device-local layout profile.

## Recognition and privacy

Unity owns the actual WebCamTexture and permission prompt. Frames are downsampled to a maximum 320-pixel edge, made upright, and submitted at up to 10 frames/second. Recognition uses a pinned MediaPipe 0.10.21 CPU hand-landmarker worker through anonymous stdin/stdout pipes. The worker never opens a webcam itself, records camera images, starts an HTTP server, or uploads frames. The camera feature does not capture microphone audio.

The process and model are local. Setup downloads Python dependencies and the model before camera use; runtime does not download code/models or use a cloud inference API. MediaPipe is deliberately pinned to the validated 0.10.21 universal desktop release, not silently upgraded. Changing this dependency requires revalidation, including privacy and native-library behavior. Other project network functions are independent of this camera feature.

Detection is not a depth camera: webcam coordinates are not sent to the XR3 spatial-hand interface. Separation is normalized by measured palm size to reduce changes caused by moving toward the camera. The gesture filter has a deliberate open/hold sequence, pinch hysteresis, minimum hand separation, smoothing, a small deadband, bounded resize speed, tracking-jump rejection and a stale-sample cutoff. Missing/overlapping hands stop the gesture and require release/rearm. Manual resizing and locking cancel a webcam gesture instead of fighting it.

The operating system can report no devices, permission denial, an occupied camera or no fresh frames. These states are displayed and do not produce synthetic hand poses or false resizing. Improve lighting, keep both hands away from the image edges, and use a camera facing the user. A real-user lighting/occlusion/accuracy study is still required.

## Local setup and player builds

From the Unity project root:

```sh
python3 Tools/WebcamGestures/setup.py
python3 Tools/WebcamGestures/setup.py --bundle
```

With `uv` installed, setup uses an isolated Python 3.12 environment at `Library/FAAWebcam/venv`. Without uv, run setup with Python 3.10–3.12. The environment does not modify the system Python installation. The model is under `Assets/StreamingAssets/FAA/WebcamGestures/hand_landmarker.task`.

The second command freezes a self-contained worker for the **current operating system and architecture** under `Library/FAAWebcam/bundles/<platform>-<architecture>/FaaWebcamWorker/`. The Editor prefers this bundle and can otherwise use its isolated Python environment. A desktop player requires its matching bundled executable; it does not assume end users have Python installed.

`FaaWebcamBuildSupport` includes available target-platform bundles during an explicitly requested desktop player build. It sets an otherwise empty macOS camera-usage description before building, and preserves macOS helper executable permissions/framework symlinks while copying. Sign/notarize the complete application/helper tree before distribution. This task does not produce, sign, notarize or publish a full Unity player. Only the current Mac arm64 helper is built locally; Windows/Linux/Intel-Mac helpers must be built on the matching host/architecture. Unsupported or missing worker combinations report unavailable rather than opening a camera that cannot recognize gestures.

## Sources and model provenance

Hand Landmarker Python guide: https://developers.google.com/edge/mediapipe/solutions/vision/hand_landmarker/python

Pinned MediaPipe package: https://pypi.org/project/mediapipe/0.10.21/

Model: https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task

Model SHA-256: `fbc2a30080c3c557093b5ddfc334698132eb341044ccee322ccf8bcf3607cde1`

Public test image: https://storage.googleapis.com/mediapipe-tasks/hand_landmarker/woman_hands.jpg

Fixture SHA-256: `70cbeb38e198c9862202e0979c21a99b40ca980d3e7b250176c85b1636a40f12`

MediaPipe code/package is Apache-2.0. Retain dependency license notices when distributing the frozen helper. The public test image is used only as a local test fixture, not as a bundled camera sample or user capture.

## Reproducible checks

`Tools/WebcamGestures/test_worker.py` checks frame parsing and hand-feature normalization without requiring a camera. `smoke_worker.py` feeds the public hand image and blank images into the real CPU model; the `--executable` argument exercises the frozen helper. `RunWebcamAssertions.cs` directly runs the NUnit fixtures and existing regressions inside the Editor. `RunWebcamRuntimeAssertions.cs` tests HUD ownership and controls using injected gestures. `Tools/WebcamGestures/verify_unity_bridge.py` tests the real C# anonymous-pipe bridge with a blank frame across nonblocking Editor calls. None of these tests opens the laptop camera.

Reports live under `artifacts/webcam-gestures/`. Script compilation, real still-image inference, injected gesture behavior and physical webcam/hand accuracy are distinct evidence. Do not describe the injected gesture checks or public-image inference as a live-camera test.

## Measured validation for this change

Unity 6000.5.10f1 compiled with zero script errors. The direct Editor fixture runner passed 256 assertions, including 31 new camera gesture/orientation cases. Play-mode runs passed 19 webcam-HUD integration checks, 34 existing spatial workspace checks, 12 existing pointer/gesture checks, and 27 conformal-HUD checks. This is 348 Unity assertions, plus 11 Python protocol tests. These are focused direct assertion runs, not a full-project Unity Test Runner report.

Both Python and the frozen Mac arm64 worker detected two hands in the public hand photograph and zero hands in blank images. Unity launched the same frozen helper through its actual C# process bridge, received the protocol handshake and zero-hand inference from a blank 320x240 frame. The native helper was disposed after testing. Public still-image inference and injected gesture poses do not establish real-camera tracking accuracy.

The MacBook Pro camera was enumerated, but it was never opened during verification. The final screenshot is `Assets/artifacts/webcam-gestures/laptop-camera-controls-final.png` with the camera OFF. The scene's existing terrain-coverage warning was not changed. Camera permission behavior, live hand motion under varied lighting, sustained camera FPS and non-Mac platform helpers remain unverified. No complete player was built, signed, notarized or published. The Editor was returned to Edit mode and the existing unsaved scene was preserved.

Setup now verifies the downloaded model against the pinned SHA-256 and collects installed distribution license/notice texts into the native helper's `licenses/` folder. `setup.py --licenses-only` refreshes those notices without rebuilding the executable.
