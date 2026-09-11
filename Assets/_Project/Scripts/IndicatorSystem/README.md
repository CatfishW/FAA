# On/Off Screen Indicator System

A simulator-only system for displaying in-view traffic/weather symbols and
off-screen directional cues. This is not certified TCAS, resolution-advisory
guidance, or operational weather-avoidance equipment.

## Quick Start

1. **One-Click Setup**: `Tools > Indicator System > Setup Indicator System`
2. The system auto-links to existing `TrafficRadarController` and `WeatherRadarProviderBase`
3. Enter Play mode to see indicators

## Pilot controls

The controls start as a compact **SCREEN CUES +** button. Tap it to expand;
tap **SCREEN CUES −** to hide the controls and symbol key again. This does not
change marker visibility or source connections. The reopen button stays
available, and the expanded/collapsed preference is retained between runs.

The **SCREEN CUES** panel has separate **TRAFFIC MARKERS** and **WEATHER MARKERS**
buttons. Tap a row to show or hide that source's cues. These switches do not turn
off the radar display, its data connection, or the simulated radar's power.
The switches also work through the existing indicator voice/wheel commands.

Each row reports ON/OFF, the number of visible cues, and source status. Zero
visible cues can mean no current targets in range, stale/missing data, or
decluttering; it does not mean the switch is broken. The panel reports how many
cues are in view, off screen, and suppressed to prevent overlapping readouts.
Touch visibility preferences are retained between runs.

The range button cycles **10 / 20 / 40 / 80 NM** for screen cues, independently
of the radar's zoom. For example, zooming the radar to 5 NM no longer removes
an aircraft 12 NM away from an 80 NM cue set. The range is a display filter,
not a promise that the upstream source covers that distance.

## Features

- **In-view traffic**: type-specific SVG silhouette, type label, callsign, distance in NM, and relative
  altitude explicitly labeled in feet, rounded to the nearest hundred. For
  example, `REL -2,300 FT` means below ownship, not absolute MSL altitude.
- **Off-screen traffic/weather**: a directional chevron and `OFF SCREEN` or
  `BEHIND YOU` label. A directly aft target is shown at the bottom edge, never at
  the centre of the windshield. Edge locations preserve the projected bearing.
- **Weather**: rain-cloud glyphs instead of a lightning bolt for every return.
  One, two, or three rain strokes distinguish light, moderate, and heavy returns.
  Cues detected from the procedural picture say **SIMULATED RETURN**.
  Their positions are synthetic, not measurements of real weather cells.
- **Contrast and motion**: compact dark readouts, cyan traffic and blue weather,
  scalable vector symbols and steady readout opacity. Target anchors follow
  exact projections, without decorative position lag.
  Routine traffic and simulated rain do not constantly flash or glow.
- **Decluttering**: priority/continuity/distance ordering, non-overlapping cue footprints,
  and a maximum visible count. Targets beyond that limit are actually released.
  The controls and expanded symbol key reserve space so markers cannot hide behind them.
- **Freshness**: individual traffic older than 10 seconds is omitted; loss of
  the X-Plane feed clears cues. Weather needs a fresh texture and respects known
  radar power-off state. A blank or dry picture does not create fallback storms.
- **Pooling**: generic and aircraft-specific instances return to their own pools,
  so repeated hide/show and target turnover do not exhaust the generic pool.

Tap **KEY** for the symbol legend. Airliner, general aviation, helicopter, and
military categories have distinct silhouettes. Unreported aircraft types retain
a question-mark diamond and **TYPE UNKNOWN** label. The X-Plane bridge decodes
native TCAS ICAO type bytes for AI slots 1–19 (slot 0 is ownship), only when
`override_TCAS` explicitly reports zero. A bounded ICAO catalog maps verified
model codes to categories. Missing, malformed, unsupported or plugin-remapped
metadata stays unknown; types are never guessed from callsign, altitude or speed.
Reported categories from other sources are
preserved through the traffic processing pipeline. These are category symbols,
not exact aircraft model drawings or heading commands.

Weather cue identities are matched to current nearby detections in a fixed NM
coordinate frame, then retained ahead of equal-intensity competing samples.
This prevents pixel-ranking churn from recreating labels every scan. New stronger
returns can still preempt weak ones; a clear scan, stale feed, or known power-off
clears cues rather than keeping fictitious cells alive. There is no whole-cue
pulsing or repeated entry fade in pilot style. Scans are bounded to at most 1 Hz
except for explicit range/gain changes.

Traffic alert colours retain the project's configured proximity classification.
They must not be interpreted as certified collision prediction or an instruction
to manoeuvre.

## View and map alignment

In desktop pointer mode, hold the **right mouse button** outside a control to
look around. Release it to return smoothly to aircraft-forward in **0.8 seconds**;
**R** recenters immediately. Return follows the aircraft's current heading,
including turns during the return. A press that starts on a map, slider, or
other UI does not capture the camera. Native headset tracking is not recentered.
Explicit XR simulator controller testing (desktop pointer preference disabled)
retains the simulated HMD pose path.

The desktop simulator no longer writes an independent, latched HMD rotation
over the aircraft camera before rendering. Screen cues project after camera
updates and again before rendering. On-screen means inside the actual view,
not inside the inset used for off-screen arrows.

Map and screen traffic use the same processed ownship sample and true-north
target bearing, range, altitude difference, and identity. Map positions use the
chart's displayed heading and range, including north-up and zoom transitions.
Screen positions use the final viewing camera, including look-around, pitch,
and bank. They are different projections of the same target, not identical
2D pixel locations. The map and cue range/decluttering limits remain independent;
a target outside one display's selected range can still appear in the other.

Focused verification: `Tools/ExplanationVerification/RunViewAlignmentAssertions.cs`
executes the alignment, screen-cue, and stability assertions in the Editor,
outside Play mode. It does not alter the X-Plane flight or weather.

Validated 2026-09-10: 63 direct NUnit checks passed (24 alignment, 22 screen-cue,
17 stability), with no compile errors. In Play mode, camera/aircraft/radar
heading matched; a 90° test look returned to zero offset. Eight shared live
traffic IDs matched exactly in bearing, NM range, and relative altitude.
Native headset ownership was regression-tested in code, not on physical hardware.

## Structure

```
IndicatorSystem/
├── Core/          - Interfaces, data structures, calculations
├── Display/       - UI components (IndicatorElement, IndicatorPool)
├── Controller/    - Main controller coordinating the system
├── Integration/   - Bridges to TrafficRadar and WeatherRadar
├── Editor/        - Setup tools and custom inspectors
└── Settings/      - IndicatorSettings.asset (auto-created)
```

## Configuration

Edit `IndicatorSettings.asset` to customize:

- Indicator sizes and colors
- Distance/altitude display options
- Animation settings
- Per-type enable/disable

`usePilotCueStyle` selects the compact vector presentation. The prior sprite
presentation is retained as an optional fallback. In pilot style, readout size
is fixed for legibility rather than shrinking with distance, and labels always
state range and relative-altitude units. Global and proximity opacity still
apply. The original primary HUD instruments and radar configuration bars are
not redesigned by this feature.

## Integration

The system uses events plus periodic reconnection/freshness checks:

- `TrafficIndicatorBridge` listens to `OnTargetsUpdated` and obtains the
  unzoomed, range-filtered marker set from `TrafficRadarController.GetIndicatorTargets`.
- `WeatherIndicatorBridge` listens to `OnRadarDataUpdated`. Procedural fan
  coordinates are inverted using `XPlaneWeatherRadarGeometry`, not a circular
  image-centre assumption. Adjacent samples are spatially separated before
  selecting cues.
- `IndicatorSystemController` owns visibility, projection, decluttering and
  lifecycle. The status panel and voice adapter use its common toggle method.

X-Plane bearings are placed in the project's north-aligned Unity frame (+Z north,
+X east), using the camera position as the local origin. Head rotation changes
the view projection; it must not rotate the target's world bearing with the head.

## Regression checks

`FAA.Customization.Tests.FaaScreenCueTests` covers signed altitude units,
sector inverse projection, camera sub-viewports, directly aft targets, pool
turnover, marker/radar range independence, and wet/dry procedural weather.
`FaaCueStabilityTests` covers SVG rendering dependencies, all category mappings,
stable weather identities, stronger-return preemption, clear scans, type metadata
propagation, and steady opacity after pool reuse.

The procedural radar uses local aircraft precipitation, not cloud coverage
alone. Unity `Mathf.SmoothStep` takes interpolation endpoints; threshold masks
first normalize their input with `InverseLerp`. Confusing those two conventions
previously suppressed all precipitation pixels even when the feed was live.
