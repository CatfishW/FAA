# Reference-style vertical-speed indicator

The current Classic Analog vertical-speed indicator follows the supplied visual reference using native Unity geometry and TextMeshPro text. It is not a bitmap, generated illustration or replacement flight-data source.

## Layout

The tall outline has a straight left rail, square outer end corners, and curved shoulders on the right rail that taper toward the left rail at the zero-rate notch. The four `1` and `2` numerals sit **inside** the outline, between the tick ends and right rail. Positive rates are above the notch and negative rates below it. Each numbered unit is 1,000 feet per minute; intermediate ticks are 500 feet per minute. The numeric readout below the scale retains the signed FPM value.

The pointer is a detached left-pointing triangle plus a filled rounded rectangular body. At zero it sits in the notch like the reference. Away from zero, its horizontal location follows the outside contour so that its body and arrow do not pass over the interior numerals. The contour and its slope are continuous at the rounded shoulders; the indicator does not step sideways at those transitions.

<p align="center">
  <img src="screenshots/2026-09-25/vsi-reference-zero.png" width="150" alt="Unity-rendered reference VSI at zero feet per minute">
  <img src="screenshots/2026-09-25/vsi-reference-climb.png" width="150" alt="Unity-rendered reference VSI at positive 1000 feet per minute">
  <img src="screenshots/2026-09-25/vsi-reference-descent.png" width="150" alt="Unity-rendered reference VSI at negative 1000 feet per minute">
</p>

These close-ups are **actual Unity renders of an isolated test instrument** at 0, +1,000 and -1,000 FPM. They are not three measured simulator flights. The black background belongs only to this inspection fixture; the in-game instrument has no opaque black panel.

## Behavior retained

The flight-data mapping is unchanged. A 1,000-FPM rate maps to 60 local units and the pointer range remains ±2,000 FPM. Beyond that range, only the pointer position is clamped; the numeric text still shows the actual rate and `OFF SCALE`. Missing, stale or non-finite data removes the live pointer and shows `NO DATA` rather than implying level flight.

The existing frame-time-based needle animation, reduced-motion option, per-version size preferences, manual size controls and multi-finger group resizing remain in place. This revision does not scale conformal scene cues or move the radar/settings panels.

## Implementation and checks

`FaaClassicVsiGeometry.cs` contains the contour, tick and pointer mesh construction. `FaaClassicDeviationGraphic.cs` binds it to live vertical speed and creates the internal labels. `FaaReferenceVsiTests` verifies calibration, curvature, finite geometry, off-scale behavior and a complete pointer sweep past all four digit rectangles. Runtime checks inspect actual rendered TMP glyph extents at 40%, 72%, 100% and 160% scale, not just nominal text-box widths.

The source-discovery revision is included in the same development cycle; see [automatic sources](AUTOMATIC_XPLANE_SOURCES.md). Verification reports identify which checks are measured, fixture-based or still hardware-dependent. This remains a research display, not a certified rotorcraft instrument.
