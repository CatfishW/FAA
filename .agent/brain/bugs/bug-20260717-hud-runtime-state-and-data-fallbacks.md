# Bug: HUD runtime state, terrain, and X-Plane data ownership

**Date Reported**: 2026-07-17  
**Date Fixed**: 2026-07-17  
**Reporter**: User  
**Assignee**: Codex debug agent  
**Severity**: High  
**Status**: Fixed

## Problems

- Radial-menu commands appeared to stop working.
- The terrain was replaced by a harsh black-and-white grid.
- Traffic and weather needed to come exclusively from X-Plane 12, even though the local TCP bridge was unavailable.
- Weather metadata was duplicated around the clipped radar panel.
- Radial submenu buttons overlapped and were difficult to click.
- A menu visibility command could be undone when the menu restored the HUD after closing.
- The bright horizon reduced HUD contrast.

## Root causes

`FaaHudRuntimeSanitizer` continuously forced HUD roots active and reset colors to opaque green. Commands were registered and invoked, but their visual state was overwritten in recurring `LateUpdate` scans.

`XPlaneMappedTerrainAnchor` replaced every authored terrain layer with a generated dark-grid layer and created a very large repeating underlay. That made the terrain look like a synthetic wire grid instead of the scene's authored surface.

The local X-Plane TCP NDJSON feed was refusing connections even though the X-Plane API on the remote machine was healthy. The working end-to-end route was the X-Plane API reverse-forwarded to `tang-server`. Unity initially used the public HTTPS route, but its libcurl client accumulated HTTP/2 `PROTOCOL_ERROR` failures on repeated radar-image downloads. A local SSH forward to `tang-server:12678` removes that HTTP/2 layer.

The initial recovery temporarily enabled a procedural weather image and a third-party traffic manager. The user clarified that both panels must be X-Plane-only, so those fallbacks were removed. The loaded scene still contained its old serialized local TCP settings; the bridge now enforces the live tunneled HTTP route during `Awake` so unsaved scene layout edits do not need to be discarded or overwritten.

The weather panel's collapsed strip and legacy inline metadata were both active. The latter extended outside the clip and repeated mode, range, tilt, and source information.

The menu hid the HUD while open, executed a visibility command, and then restored the pre-menu HUD state. For show/hide commands, that final restore reversed the requested visibility.

The radial menu packed five 144-pixel submenu buttons into a 64-degree fan, so their visual and pointer hit areas overlapped.

## Fix

- Sanitization is now a one-time startup repair by default and no longer owns user-facing HUD color, opacity, or visibility state.
- Authored terrain layers are preserved by default; the generated dark-grid material and underlay require an explicit opt-in.
- The bridge enforces HTTP polling from `http://127.0.0.1:12678`, locally forwarded by SSH to `tang-server`, whose reverse tunnel terminates at the live X-Plane 12 API.
- Traffic is populated only from X-Plane multiplayer datarefs. External traffic fetching remains stopped, and an empty X-Plane traffic set produces an empty radar instead of third-party targets.
- Weather is populated only by the X-Plane render endpoint. The procedural offline generator was removed; an unavailable feed reports `X-PLANE WX OFFLINE` and never invents returns.
- Redundant inline weather labels are disabled while the compact control strip remains readable.
- The radial menu uses a wider 126-degree submenu fan, separated hit areas, stronger contrast, aviation-themed navy/teal surfaces, and click propagation guards.
- Visibility-owning menu commands restore the temporarily hidden HUD before executing, so the requested show/hide state persists.
- A runtime-cloned, lower-exposure blue skybox improves green HUD contrast without mutating the authored skybox material.

## Regression coverage

`Assets/_Project/Tests/Editor/FaaHeadingTapeOverlayTests.cs` verifies:

- the sanitizer preserves an existing tape layout;
- menu-selected HUD colors are not overwritten;
- authored terrain layers remain installed by default;
- no synthetic weather generator exists;
- submenu fan spacing does not overlap at the configured dimensions.
- menu visibility commands are not undone when the menu closes;
- duplicate inline weather labels are suppressed.

## Verification

- `WeatherRadar.csproj`: zero errors (deprecated weather subsystems still emit pre-existing warnings).
- `Assembly-CSharp.csproj`: zero errors (pre-existing project warnings remain).
- `FAA.Customization.EditorTests.csproj`: zero warnings, zero errors.
- Computer-controlled Play Mode showed live X-Plane values (`258 KT`, `12,161 FT`), the X-Plane weather render, and X-Plane multiplayer traffic after the enforced tunneled route was compiled.
- Computer-controlled Play Mode also confirmed the darker sky, normal non-grid terrain, de-duplicated weather panel, and separated radial submenu layout.
- Local tunnel verification returned healthy telemetry with 480 subscriptions; both weather and traffic render endpoints returned valid PNG files without HTTP/2 errors.

## Files modified

- `Assets/_Project/Scripts/Customization/FaaHudRuntimeSanitizer.cs`
- `Assets/_Project/Scripts/Customization/FaaRadarControlsOverlay.cs`
- `Assets/_Project/Scripts/XPlaneIntegration/Runtime/XPlaneMappedTerrainAnchor.cs`
- `Assets/_Project/Scripts/XPlaneIntegration/Runtime/XPlane12ApiHudBridge.cs`
- `Assets/_Project/Scripts/WeatherRadar/Providers/XPlaneOriginalWeatherRadarProvider.cs`
- `Assets/_Project/Scripts/WeatherRadar/Display/XPlaneOriginalWeatherRadarDisplay.cs`
- `Assets/_Project/Scripts/VoiceControl/UI/UIToolkitRadialMenuAdvanced.cs`
- `Assets/_Project/Tests/Editor/FaaHeadingTapeOverlayTests.cs`
- `.agent/brain/bugs/bug-20260717-heading-tape-position-reset.md`
- `.agent/brain/bugs/bug-20260717-hud-runtime-state-and-data-fallbacks.md`
