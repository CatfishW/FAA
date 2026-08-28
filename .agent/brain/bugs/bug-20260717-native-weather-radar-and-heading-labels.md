# Bug: Native X-Plane weather radar was replaced by point diagnostics and heading labels were clipped

**Date Reported**: 2026-07-17  
**Date Fixed**: 2026-07-17  
**Reporter**: User  
**Assignee**: Codex debug agent  
**Severity**: High  
**Status**: Fixed

## Problems

- The weather scope showed sparse circular sample bubbles instead of the filled green/yellow return field visible on an aircraft radar.
- The scan beam extended outside the useful radar range and the reference symbology was hidden.
- N/E/W/S labels on the heading tape disappeared or were clipped, and the scrolling tape could become detached from its current-heading index in Play Mode.

## Reproduction

1. Open `ExperimentScene` and enter Play Mode with the local SSH tunnel connected to the X-Plane API.
2. Observe the lower-left weather display: isolated bubbles are rendered in a square diagnostic scope.
3. Observe the heading tape after moving its root: ticks may appear apart from the current-heading index, and the opposite cardinal is clipped at the viewport edge.

## Root causes

Unity appended `range_nm` to every `/v1/render/weather.png` request. In the X-Plane API, that query deliberately selects the UDP RADR point diagnostic renderer, which rasterizes each sample as an ellipse and returns a 700x700 debug image. The query-free route serves the current 724x512 `xplm_Tex_Radar_Pilot` artifact captured from X-Plane itself, containing the desired continuous green/yellow radar returns.

The aircraft-style `FAAReferenceOverlay` already existed, but scene setup, `WeatherRadarPanel`, and `FaaHudRuntimeSanitizer` all forced it inactive. The sweep shader masked only by sector angle, so its beam continued beyond the outer range arc.

The generated heading-tape clip had been serialized hundreds of pixels away from its parent overlay. Editor-layout preservation treated that internal detached viewport as intentional. At 1.65 pixels per degree, the opposite cardinal also landed at the exact mask edge, leaving insufficient room for its 52-pixel label. Disabled TMP labels did not have all renderer state restored when reused.

## Fix

- Native weather requests now omit `range_nm`; the point diagnostic remains available only through an explicit provider opt-out.
- Live X-Plane position and heading are fed into the weather data provider.
- The native 724x512 return texture is presented beneath a clean 55-degree aircraft sector with four range arcs, absolute heading labels, ownship symbol, mode/range/tilt data, and a bounded phosphor sweep.
- Reference symbology is enabled by default and remains controllable from the radar controls.
- The sweep shader now has an outer-radius mask.
- Implausibly detached heading-tape clip offsets self-heal to `(0, -5)` without moving the user-authored overlay root.
- Heading-tape scale is capped so N/E/W/S labels fit inside the viewport, and TMP active/enabled/cull/alpha state is restored for every displayed label.

## Regression coverage

- `FaaHeadingTapeOverlayTests` verifies detached-clip repair while preserving root layout, and verifies N/E/W/S are enabled, uncullled, opaque, and fully contained in the clip.
- `XPlaneWeatherRadarPresentationTests` verifies native aspect, no `range_nm` on the native provider route, default reference-overlay visibility, absolute heading labels, scan motion, and the shader outer-range mask.

## Verification

- The tunnel health endpoint reported `status: ok`, 480 subscriptions, a sub-second packet age, and no bridge error.
- The query-free live radar artifact was verified as a 724x512 PNG.
- `WeatherRadar.csproj`, `Assembly-CSharp.csproj`, `Assembly-CSharp-Editor.csproj`, and `FAA.Customization.EditorTests.csproj` built with zero errors.
- Unity EditMode suite: 29 passed, 0 failed.
- Computer-controlled Play Mode showed W/N/E/S simultaneously inside the heading tape, the repaired clip at local `(0, -5)`, filled native X-Plane return cells beneath four sector arcs, and the bounded scan beam at different positions in successive frames.
- The Unity console was clean after verification.

## Prevention

API query parameters that change the semantic source must not be added as ordinary presentation controls. Name source selection explicitly and test the final request URL. Generated UI should distinguish user-owned root layout from internal implementation transforms, validate internal offsets, and test complete renderer state rather than only text values.

## Files modified

- `Assets/_Project/Scripts/Customization/FaaHeadingTapeOverlay.cs`
- `Assets/_Project/Scripts/Customization/FaaHudRuntimeSanitizer.cs`
- `Assets/_Project/Scripts/Customization/FaaRadarControlsOverlay.cs`
- `Assets/_Project/Scripts/Editor/FaaXPlane12BridgeSceneSetup.cs`
- `Assets/_Project/Scripts/WeatherRadar/Providers/XPlaneOriginalWeatherRadarProvider.cs`
- `Assets/_Project/Scripts/WeatherRadar/Display/WeatherRadarPanel.cs`
- `Assets/_Project/Scripts/WeatherRadar/Display/XPlaneOriginalWeatherRadarDisplay.cs`
- `Assets/_Project/Scripts/WeatherRadar/Display/XPlaneWeatherRadarOverlay.cs`
- `Assets/_Project/Scripts/WeatherRadar/Display/XPlaneWeatherRadarSweepOverlay.cs`
- `Assets/_Project/Resources/Shaders/XPlaneWeatherRadarSweep.shader`
- `Assets/_Project/Scripts/XPlaneIntegration/Runtime/XPlane12ApiHudBridge.cs`
- `Assets/_Project/Scripts/XPlaneIntegration/Editor/RadarEvidenceCli.cs`
- `Assets/_Project/Tests/Editor/FaaHeadingTapeOverlayTests.cs`
- `Assets/_Project/Tests/Editor/XPlaneWeatherRadarPresentationTests.cs`
- `.agent/brain/bugs/bug-20260717-native-weather-radar-and-heading-labels.md`
