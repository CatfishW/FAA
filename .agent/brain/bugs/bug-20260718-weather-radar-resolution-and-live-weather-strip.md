# Weather radar symbology blurred at 4K and lacked nearby conditions

- Date: 2026-07-18
- Severity: Medium
- Area: Unity weather radar / X-Plane 12 HUD presentation

## Symptoms

The native X-Plane weather radar was visible, but the range arcs, ticks, labels, and aircraft marker became difficult to read in the Unity Game view, especially when a 3840x2160 view was previewed at 0.35x. The radar also had no compact nearby display for temperature, aircraft-local wind, visibility, pressure, or precipitation.

## Root cause

The API was not reducing the source image: `/v1/render/weather.png` arrived as a native 724x512 PNG. The Unity reference overlay was also generated at 724x512, but it used roughly 1-pixel strokes and a 3x5 bitmap glyph raster. Unity then downsampled that already-thin overlay in the 4K editor preview, causing the visible loss of resolution.

## Resolution

- Preserve the native X-Plane weather texture and its aspect ratio.
- Render the generated reference overlay at 1448x1024 (2x supersampling).
- Scale strokes, ticks, labels, dash lengths, and the aircraft marker with the render resolution.
- Add a sibling `WeatherConditionsStrip` beside the weather radar control so it does not move or overlap when the radar menu expands.
- Draw five resolution-independent UGUI vector icons for OAT, aircraft-local wind, visibility, QNH, and precipitation.
- Populate the strip exclusively from `XPlane12ApiHudBridge` live snapshot/flight data and apply cyan/amber/red severity colors.
- Label wind as `A/C WIND` to distinguish aircraft-local wind from regional weather-layer wind.

## Verification

- Unity C# validation: no errors in the four changed scripts.
- EditMode presentation suite: 90/90 passed.
- Play-mode hierarchy: `WeatherConditionsStrip` active at `(222, 408)`, size `458x48`, directly beside `WeatherControlStrip`.
- Play-mode overlay: `TextureResolution = 1448x1024`, `RenderScale = 2`.
- Live X-Plane readout observed: `-9°C`, `265° / 144KT`, `1.5 SM`, `29.92`, `92%`.
- Visual verification performed in the 3840x2160 Game view at 0.35x preview scale.

## Prevention

Keep simulator-provided imagery separate from generated symbology, test presentation at the actual canvas resolution and reduced editor zoom, and add formatting/geometry regression tests whenever compact cockpit UI is introduced.
