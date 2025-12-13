# CompassBarSystem

Lightweight, camera-independent compass bar controller for UI tapes.

## Quick setup
1. Create a UI Image (RectTransform) for your compass tape. Use a horizontally repeating sprite/tile. Set anchors/pivot to center.
2. Add `CompassBarController` to a GameObject and assign the tape RectTransform.
3. Set `Pixels Per Degree` to match your artwork. Set `Cycle Width Pixels` to the length of one full 360° pass of the tape (e.g., `360 * pixelsPerDegree` if the tape contains exactly one 0-360 cycle; larger if you stacked repeats for seamless wrap).
4. Set `Heading Mode` to `TransformYaw` and assign your aircraft/body transform to `Heading Target`. This keeps the bar stable if the camera orbits.
5. (Optional) Assign a `TMP_Text` to `Heading Readout` and tweak the format.

## Notes
- The controller ignores camera motion unless the camera is explicitly set as the heading target.
- Smoothing uses exponential interpolation scaled by frame time; set smoothing to `0` for instant snapping.
- Manual heading mode lets other systems feed a heading via `SetHeadingDegrees`.
