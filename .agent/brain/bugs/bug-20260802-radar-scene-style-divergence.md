# Radar panels retained opaque legacy scene styling

- Date: 2026-08-02
- Severity: Medium
- Area: Unity HUD / weather and traffic radar presentation

## Symptoms

The weather and traffic radars appeared as large, opaque black or gray rectangles in the lower corners of the HUD. They obscured too much of the outside view, used different visual treatments, and could return to their old 372 px and 420 px dimensions even after runtime styling code selected smaller defaults.

## Root cause

`ExperimentScene.unity` still serialized the legacy panel sizes, opaque `Image` and `RawImage` colors, and a visible traffic `Mask` plate. The runtime sanitizer and setup utilities did not fully clear those serialized fallbacks. In the editor, persisted `PlayerPrefs` could also overwrite the compact defaults during scene setup, which made generated scene content depend on workstation history. Finally, the weather texture cleanup callback restored an opaque black fallback while removing an invalid serialized runtime texture.

## Resolution

- Reduced the weather and traffic radar defaults to 280 px and 296 px.
- Replaced the opaque square plates with transparent deep-teal glass and a circular traffic presentation.
- Retired the four full-length resize edges and added eight short corner-bracket handles.
- Kept weather imagery transparent while the X-Plane feed is offline and made stale/offline states progressively less intrusive.
- Cleared the traffic mask graphic and rectangular fallback image while retaining the circular radar texture.
- Hid duplicate weather diagnostics and compacted the power, range, and tilt readouts.
- Made editor scene setup deterministic by ignoring runtime `PlayerPrefs`; runtime now migrates the two exact legacy persisted sizes.
- Added a dedicated editor command to reapply the compact radar glass without rebuilding unrelated FAA scene content.

## Verification

- Clean-room Unity 6000.4.10f1 compile completed without C# errors.
- The editor presentation suite passed 46/46 tests.
- A scene audit confirmed weather at 280x280, traffic at 296x296, no visible square traffic mask, transparent null textures, hidden duplicate diagnostics, and eight active corner-bracket resize handles.
- A Play Mode startup audit confirmed the compact glass styling and resize handles survive runtime sanitization.
- The repaired X-Plane feed supplied live airborne ownship, storm-weather, and moving traffic-target data through the Unity API tunnel.
- `git diff --check` passed.

## Prevention

Keep serialized scene defaults, runtime repair code, and editor setup utilities driven by the same presentation values. Editor setup must not read user-specific runtime preferences, and regression tests must cover panel opacity, mask visibility, resize geometry, and legacy-size migration.
