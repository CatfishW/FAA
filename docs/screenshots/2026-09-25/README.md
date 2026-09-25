# Screenshot provenance — 2026-09-25

These are curated renders for the project README. No user webcam images are included.

- `vsi-reference-zero.png`, `vsi-reference-climb.png`, `vsi-reference-descent.png`: actual Unity native vector/TMP renders of an isolated production VSI component at explicitly injected 0, +1000 and -1000 FPM. These values are not measured flights. The black background is a capture fixture, not a new opaque in-game HUD panel.
- `classic-terrain.png`, `digital-terrain.png`: actual ExperimentScene Game views using the available live fallback simulator feed. Source provenance is visible in the bottom-left badge.
- `symbology-settings.png`: actual side-mounted version selector during deliberate desktop side inspection.
- `data-source-discovery.png`: actual source-selection page. The development Mac had no local X-Plane process/installation; its fallback was correctly labelled as non-local.
- `hand-studio-off.png`: actual Hand Studio, camera OFF; no permission or capture was activated for this screenshot.
- `traffic-side-panel.png`: deliberate side inspection of the existing traffic/map panel; the radar stays outside the protected forward view.

Reproduce the close-ups with `Tools/ExplanationVerification/CaptureReferenceVsi.cs` and the application views with `capture_readme_screenshots.py`. The capture code saves/restores UI selections and never substitutes fixture aircraft data into the live bridge.
