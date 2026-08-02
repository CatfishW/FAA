# Bug: Radial-menu visibility targets and X-Plane weather sweep

**Date Reported**: 2026-07-17  
**Date Fixed**: 2026-07-17  
**Reporter**: User  
**Assignee**: Codex debug agent  
**Severity**: High  
**Status**: Fixed

## Problems

- `Hide Weather Radar` and `Hide Traffic Radar` executed but left the live panels visible.
- `Hide HUD` left the separately hosted heading tape and uncached/custom HUD graphics visible.
- Invisible HUD layers could continue intercepting pointer input.
- The live X-Plane weather render was square-stretched and had no scan motion.

## Reproduction

1. Enter Play Mode in `ExperimentScene` and open the wheel with `Tab`.
2. Run `Hide Weather Radar` or `Hide Traffic Radar`; observe that the corresponding live X-Plane canvas remains visible.
3. Run `Hide HUD`; observe that the central HUD mostly disappears but `FAAHeadingTapeCanvas` remains visible.
4. Observe that the 724x512 X-Plane weather render is forced into a 352x352 square and remains visually static.

## Root causes

The weather and traffic voice adapters had serialized `radarRoot` references to inactive legacy objects under the old `FAASymbologyCanvas/RadarCanvas`. `ResolveRadarRoot()` returned those non-null references immediately. The actual panels live on `XPlaneWeatherRadarCanvas` and `XPlaneTrafficRadarCanvas`, so commands successfully hid objects that were already inactive. The compact control strips are siblings of the live radar roots, which also meant hiding only a corrected child root would have left the strip behind.

The symbology adapter delegated visibility to `SymbologyColorManager.Hide()`. That manager adjusts cached `Image`, TMP, and legacy `Text` colors; it does not own `RawImage`, custom graphics, or the standalone `FAAHeadingTapeCanvas`.

`WeatherRadarPanel` explicitly disabled its legacy centered 360-degree sweep whenever the provider was `XPlaneOriginalWeatherRadarProvider`. That was appropriate for the old CPU renderer, but no X-Plane-specific sector sweep replaced it. Both the panel and `XPlaneOriginalWeatherRadarDisplay` also forced the native 724x512 image into 352x352 with aspect preservation disabled.

## Fix

- Radar adapters now resolve and cache the loaded dedicated X-Plane canvas before considering legacy serialized roots.
- Radar hide/show operates on the dedicated canvas plus a root `CanvasGroup`, so the radar and its sibling control strip hide together while provider/controller GameObjects continue updating.
- HUD hide/show owns full flight-HUD canvas groups, including `FAAHeadingTapeCanvas`, while explicitly excluding weather, traffic, radar, indicator, menu, radial, and voice-control canvases.
- Hidden HUD groups set alpha to zero and disable interaction/raycast blocking; show restores their authored state.
- The X-Plane texture is aspect-fitted inside its existing panel instead of square-stretched.
- A lightweight UI shader adds a bottom-center, aspect-correct, +/-64-degree ping-pong phosphor sweep over the unchanged X-Plane image. It stops in standby or when X-Plane explicitly reports radar power off. No synthetic returns are generated.

## Regression coverage

- `RadarVoiceAdapterVisibilityTests` verifies both adapters ignore stale legacy roots, hide the live dedicated canvas without stopping its GameObject, and restore it.
- `SymbologyColorVoiceAdapterTests` verifies full HUD and heading-tape visibility, raycast release, restoration, and exclusion of independent radar/indicator/menu canvases.
- `XPlaneWeatherRadarPresentationTests` verifies native aspect fitting, sector sweep bounds/direction, and build inclusion of the sweep shader.

## Verification

- `WeatherRadar.csproj`, `Assembly-CSharp.csproj`, and `FAA.Customization.EditorTests.csproj` build with zero errors.
- Unity EditMode regression suite: 20 passed, 0 failed.
- Computer-controlled Play Mode confirmed weather hide/show, traffic hide/show, and full HUD hide/show all work from the wheel; radar strips hide with their panels and the heading tape hides with the HUD.
- Computer-controlled Play Mode visually confirmed the weather beam moving across successive frames over the live X-Plane sector image.
- The tunneled X-Plane API reported healthy live telemetry with 480 subscriptions, five multiplayer traffic targets, and X-Plane weather values.

## Prevention

Runtime UI commands should resolve the currently loaded visual owner rather than trusting serialized references to objects that may have been superseded. Full-panel visibility should be controlled at a stable canvas/group boundary, while data providers remain active. Source-rendered telemetry images should retain their native aspect ratio, and presentation effects must remain separate overlays so they cannot alter source data.

## Files modified

- `Assets/_Project/Scripts/VoiceControl/Adapters/WeatherRadarVoiceAdapter.cs`
- `Assets/_Project/Scripts/VoiceControl/Adapters/TrafficRadarVoiceAdapter.cs`
- `Assets/_Project/Scripts/VoiceControl/Adapters/SymbologyColorVoiceAdapter.cs`
- `Assets/_Project/Scripts/WeatherRadar/Display/XPlaneOriginalWeatherRadarDisplay.cs`
- `Assets/_Project/Scripts/WeatherRadar/Display/XPlaneWeatherRadarSweepOverlay.cs`
- `Assets/_Project/Scripts/WeatherRadar/Display/WeatherRadarPanel.cs`
- `Assets/_Project/Resources/Shaders/XPlaneWeatherRadarSweep.shader`
- `Assets/_Project/Scripts/Editor/FaaXPlane12BridgeSceneSetup.cs`
- `Assets/_Project/Tests/Editor/RadarVoiceAdapterVisibilityTests.cs`
- `Assets/_Project/Tests/Editor/SymbologyColorVoiceAdapterTests.cs`
- `Assets/_Project/Tests/Editor/XPlaneWeatherRadarPresentationTests.cs`
- `.agent/brain/bugs/bug-20260717-radial-visibility-and-weather-sweep.md`
