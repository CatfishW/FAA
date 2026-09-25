# Switchable non-conformal symbology: Digital and Classic Analog

## In-game use

Open the existing side-mounted **Settings** panel with **SETTINGS / F9**, then use **LOOK RIGHT** to inspect it on a laptop (look toward it in XR). Select the **SYMBOLOGY** tab and choose **DIGITAL** or **CLASSIC ANALOG**. Switching is explicit and works while gesture editing remains locked; no camera is started. Return to flight view with **FORWARD / R**.

The **INSTRUMENTS** tab controls the currently selected version's instruments. Its per-instrument slider, +/- buttons, presets and group-size controls also work on the new analog dials. Existing webcam multi-finger group sizing and tracked-hand size adjustments operate on the active version. Switching cancels a gesture in progress, preventing it from applying an old baseline to a different version.

During an explicit desktop **LOOK LEFT/RIGHT** panel inspection, the Classic instrument layer dims so its large bank arc and pitch bars do not obscure the settings buttons. Its layout, data updates and canvas projection do not move or switch. **FORWARD / R** restores full opacity immediately. Native XR continues using its normal canvas depth/order rather than this desktop inspection behavior.

Each version keeps its own instrument sizes. The existing Digital workspace profile remains compatible. The selected version, Classic sizes and presentation choices are saved separately under the existing scene/device key plus `.Symbology.v1`. Radar/utility panel coordinates remain in the original profile and never change when selecting a style. Invalid/unsupported preference documents fall back to Digital. The developer F8 renderer toggle is separate; an explicit style choice returns to the uGUI flight renderer so the chosen style is visible.

## Reference design and deliberate interpretation

Classic Analog is built from native scalable Unity UI geometry and TMP text, not a static screenshot stretched over the view. The user's supplied reference is the visual basis for the lime-green palette, large circular speed and altitude instruments, smaller torque/RPM instruments, upper bank arc, central solid/dashed pitch bars, four-dot course-deviation layout, and right-hand vertical indications. Its black background is treated as transparency so the cockpit/outside scene remains visible.

The photograph is not an interface-control specification. Its sample values (140 knots, 1400 feet and 78%) are never hardcoded into the live view. Some dial markings and C/R/P mode mappings are ambiguous in the image. The following mappings are explicit implementation choices, rather than claims about an aircraft-certified instrument:

| Element | Current source and mapping |
|---|---|
| Airspeed | Actual indicated-airspeed dataref, 0–240 knots around one clockwise revolution. Values above the scale display OFF SCALE; they do not wrap back to zero. |
| Altitude | Actual MSL altitude. One needle revolution is **1,000 feet**, dial labels 0–9 are hundreds of feet, and the window shows full feet MSL. A separate thousands indication disambiguates the cyclic pointer. This replaces the reference's ambiguous mixed labels with an explicit scale. |
| Torque | Maximum of the available expected engines, 0–150% dial. The E1/E2 values remain labelled beneath it; missing data from an expected engine invalidates the combined maximum. This is labelled MAX ENG, not falsely presented as a particular engine. |
| RPM | Actual primary **rotor** RPM percentage, 0–120% dial. Engine N2 is not silently substituted. |
| Bank/slip | Actual roll and side-force/slip telemetry. Invalid channels remove their moving pointer. |
| Vertical speed | Actual feet/minute, +/-2,000 fpm instrument range with an off-scale indication and numeric value outside that range. |
| LOC / G/S | Existing navigation-valid state and a real deviation dataref. No signal means NO DATA/NO GS and no false centered guidance diamond. A map target is not substituted for an ILS signal. |
| R/P and coupling | Read-only X-Plane active/armed mode bits and flight-director/autopilot mode. Collective-mode telemetry is not established by the available source: **C: --** remains explicitly unavailable. No flight-control commands are sent. |

AP-mode references: https://developer.x-plane.com/article/accessing-the-x-plane-autopilot-from-datarefs/ and https://developer.x-plane.com/article/flight-director-and-autothrottle-datarefs/ . These source meanings are separate from the user's visual reference.

## Attitude vs conformal scene geometry

The subsequent [reference VSI revision](REFERENCE_VSI.md) places `1`/`2` numerals inside the scale, adds rounded shoulders to the zero notch, and moves a detached rounded pointer outside the numeric column. The live range, signed readout, invalid-state behavior and existing animation remain unchanged. Recent source discovery and release status are described in the current README; the measured results below describe the original analog implementation pass.

The Classic center offers **ATTITUDE INSTRUMENT** or **CONFORMAL SCENE CUES**. The former is a head-fixed instrument with a moving local pitch/roll ladder, explicitly **non-conformal**; its pixels-per-degree spacing is not optical calibration. In this mode the duplicate scene horizon/boresight drawing is suppressed, but the existing calibrated FPV, selected FPA and georeferenced scene references remain independent and functional. Selecting the scene-cue center hides the local attitude inset and restores the original scene-aligned attitude rendering.

No version switch changes camera FOV, IPD, native lens distortion, flight telemetry or radar positions. Existing whole-HUD and individual instrument visibility controls are respected. The previous removed green control strip stays removed. Settings/Hand Studio remain in protected side positions, with right-button and XR grip dragging unchanged.

## Animation and responsive layout

New gauges use a frame-time-based exponential response rather than frame-count-based lerp. Roll and heading use shortest-angle interpolation. Altitude is smoothed as a continuous value before converting to a cyclic needle angle, avoiding the 359-to-0 reverse spin. First valid data, recovery after stale data and large discontinuities snap to the trustworthy sample rather than slowly sweeping from an invented zero. Invalid channels suppress their live pointers immediately. **MOTION: DIRECT / REDUCED** removes extra presentation smoothing; **SMOOTH NEEDLES** restores it.

Small dark outlines on the native vector strokes and glyphs improve contrast against the outside scene without adding an opaque panel behind the flight display. Numeric windows and long mode captions fit their allocated text areas rather than overlapping neighboring markings.

Individual scaling separates adjacent dials. Group scaling expands their composition together, with an outer fit limit that keeps instruments inside the current viewport rather than clipping them at extreme sizes. Local coordinates are used for this layout; it is not recomputed from a moving head/world camera. The supplied reference green is the default Classic palette; **COLOR: PILOT PALETTE** follows the existing pilot-selected color instead.

## Implementation boundaries

`FaaSpatialWorkspace.Symbology.cs` owns style selection, visibility gates, module bindings and independent preferences. `FaaClassicAnalogHud` owns the reusable Classic view; `FaaClassicGaugeGraphic`, `FaaClassicAttitudeGraphic`, `FaaClassicBankGraphic` and `FaaClassicDeviationGraphic` draw native vector geometry. `FaaAnalogFlightSample` is a read-only adapter over the existing bridge; `FaaAnalogAnimation` supplies presentation filtering. The view is built once, and switching reuses it instead of creating duplicate canvases or telemetry workers.

To add another style, define its instrument roots/captions and renderer, register its separate module set and persisted size list, and extend the explicit style enum/selector. Never reuse the conformal layer as a scalable screen instrument and never copy a screenshot's example data into live telemetry.

## Validation

`RunAnalogAssertions.cs` runs focused NUnit fixtures with setup/teardown. `RunAnalogRuntime.cs` checks in-game style switching, independent sizes, declutter, stale/invalid signals and preservation of radar/conformal state. `verify_symbology_mouse.py` drives the actual active UI input module with a temporary Input System mouse. `verify_analog_frames.py` records settled UI geometry over 300 rendered frames while switching versions, opening the side panel and changing size.

Reports and previews are in `artifacts/analog-symbology/` and `Assets/artifacts/analog-symbology/`. Synthetic values are confined to isolated test fixtures, never the live simulator bridge. Final measured results belong in the generated verification report. Physical XR-3 optical behavior, pilot workload, native-device performance and a complete player build are not established by these Editor checks. This is research-simulator software, not FAA-certified flight equipment.

### Measured local results

Final compilation on Unity 6000.5.10f1 reported zero script errors. The direct Editor runner passed 353 cases (35 new analog cases plus existing regressions); Play-mode runs passed 29 analog integration checks, 24 radar/Brief/drag checks and 27 conformal-HUD checks: **433 focused checks total**. These are direct assertion runs, not a full-project Unity Test Runner XML report.

Two actual Input System mouse clicks, processed by the active XRUIInputModule on the side Settings panel, switched Digital to Classic Analog and back while gestures remained locked. A 300-frame render probe collected 200 settled samples across five style/size/inspection phases. The four checked gauge groups had zero measured rectangle movement within each settled phase; 160 Classic samples had fresh flight data while altitude changed. This verifies layout stability, not a promise of a particular hardware frame rate.

Classic Analog is selected and saved for immediate inspection. The camera was never opened for testing, no simulator commands were issued, and no player package, commit or push was produced. The existing unsaved scene remains preserved. Machine-readable evidence: `artifacts/analog-symbology/analog-symbology-verification.json`.
