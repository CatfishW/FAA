# Bug: Heading tape UI position resets in edit mode

**Date Reported**: 2026-07-17  
**Date Fixed**: 2026-07-17  
**Reporter**: User  
**Assignee**: Codex debug agent  
**Severity**: Medium  
**Status**: Fixed

## Problem

Moving `FAA Heading Tape Overlay` or its `Heading Tape Clip` child with the Rect tool or Rect Transform Inspector did not persist. The UI snapped back on the next editor update.

## Reproduction

1. Open `Assets/_Project/Scenes/ExperimentScene.unity`.
2. Select `FAAHeadingTapeCanvas/FAA Heading Tape Overlay/Heading Tape Clip`.
3. Change Pos X or Pos Y.
4. Observe the position immediately return to its previous value.

## Root cause

`FaaHeadingTapeOverlay` uses `[ExecuteAlways]`. `UpdateTape()` called `EnsureBuilt()` every editor frame, and `EnsureBuilt()` called `ApplyLayoutAndStyle()`. That method unconditionally restored the root's serialized position and size and hard-coded the clip position to `(0, -5)`.

The initial fix captured edits during `Update()`, but root RectTransform Inspector changes can invoke `OnValidate()` first. Validation still reapplied the stale serialized position before `Update()` had a chance to capture the new value.

A second runtime writer also reset the tape when Play Mode began: `FaaHudRuntimeSanitizer` repeatedly called `Configure(...)` with the hard-coded position `(-610, 430)`. That sanitizer ran in `Awake`, `OnEnable`, `Start`, and recurring `LateUpdate` scans, so it overrode the scene-authored RectTransform even after the overlay itself stopped snapping in edit mode.

## Fix

In edit mode, the generated root and clip RectTransforms are now captured as the layout source of truth before both validation and regular overlay refreshes. The duplicate component position and size fields are hidden so they cannot compete with the RectTransform Inspector. The clip position is serialized instead of hard-coded, and the component is marked dirty only when captured layout values change.

At runtime, the sanitizer now applies its default heading layout only when it has to create a missing overlay. Existing scene-authored overlays keep their RectTransform, and recurring sanitizer scans are opt-in instead of continuously rewriting UI state.

## Regression coverage

`Assets/_Project/Tests/Editor/FaaHeadingTapeOverlayTests.cs` covers editor update, validation, and the runtime sanitizer path. It verifies that root position, root size, and clip position remain unchanged after manual RectTransform edits and sanitizer setup.

## Verification

- Unity script compilation completed with zero errors.
- Initial focused EditMode regression tests: 2 passed, 0 failed. Coverage includes both `Update()` and `OnValidate()` snapping paths.
- Runtime sanitizer regression coverage was subsequently added; the runtime, weather, and test assemblies compile with zero errors (pre-existing project warnings remain outside this fix).
- The full EditMode suite was not completed because Unity requested a decision about pre-existing unsaved `ExperimentScene` changes; the reload was canceled to preserve those changes.
- Final Computer-controlled Play Mode verification showed the authored heading tape layout surviving startup while the live X-Plane HUD initialized. The user's unsaved scene positioning changes were deliberately left intact.

## Prevention

Execute-always UI generators should not overwrite user-editable RectTransform properties every editor frame. Separate generated visual updates from editor-authored layout state, or capture editor layout before applying generated styling.

## Files modified

- `Assets/_Project/Scripts/Customization/FaaHeadingTapeOverlay.cs`
- `Assets/_Project/Tests/Editor/FAA.Customization.EditorTests.asmdef`
- `Assets/_Project/Tests/Editor/FaaHeadingTapeOverlayTests.cs`
- `.agent/brain/bugs/bug-20260717-heading-tape-position-reset.md`
