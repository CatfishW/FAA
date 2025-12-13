# CompassBarSystem

Lightweight, camera-independent compass bar controller for UI tapes.

## Quick setup
1. Add `CompassTapeGenerator` to your tape RectTransform and click **Generate Tape** to get ticks/labels. Set pixels-per-degree and style there.
2. Add `CompassBarController` to a parent (or same object) and assign the tape RectTransform (and heading readout if desired).
3. Leave `Auto From Generator` on so the controller reads pixels-per-degree and cycle width from the generator.
4. Set `Heading Mode` to `TransformYaw` and assign your aircraft/body transform to `Heading Target` (not the orbiting camera) so the bar stays stable when the camera moves.
5. Play. The tape scrolls via anchor shifting (wrap-safe) and the readout updates from the heading source.

## Notes
- Tape generator builds repeated 360° copies for seamless scrolling and shows major/minor ticks plus labels (cardinals and degree labels every N degrees).
- Controller ignores camera motion unless the camera is the heading target. Use `Manual Degrees` mode or `SetHeadingDegrees` if you drive heading from another system.
- Smoothing uses exponential interpolation scaled by frame time; set smoothing to `0` for instant snapping.
