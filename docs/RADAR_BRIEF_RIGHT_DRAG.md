# Radar-side placement, Pilot Brief stability and right-button dragging

## Pilot controls

Weather and traffic now remain in cockpit-side world space on laptops as well as XR. The forward viewing cone is protected for the complete UI group: scope, header, configuration drawer, traffic quick menu and weather-detail cards. Collapsed/faded drawers reserve their footprint so opening them does not suddenly cover the flight HUD. Recall and the old desktop-toggle API no longer bring radars in front of the pilot. **STOW BOTH RADARS** restores their side slots without changing their size. The original radar objects and telemetry are retained.

Default scope locations are 95 degrees left/right, 32 degrees below the neutral reference, 1.7 metres away. Existing saved layouts remain readable and are constrained when rendered. Whole-group protection can move a large or close group farther to the side/back; it does not shrink instruments or change calibration. Settings and Hand Studio retain their own protected side placement. The low-profile dock has **WEATHER** and **TRAFFIC** buttons for explicit laptop side inspection; **FORWARD / R** resets the view. Tracked headset pose is never controlled by these desktop inspection buttons.

**Laptop:** right-click and hold on a visible radar/settings/Hand Studio panel, then drag. This is an explicit mouse action and works while gesture editing is locked. The press must start on the panel. Camera look is suppressed for that press until release, even after the cursor leaves the panel. A right press starting outside all panels retains the existing look-around behavior. Left click continues to operate radar settings, map selection and pan. Panel motion remains constrained away from the protected forward area.

**XR-3:** enable gesture editing, point a tracked hand at the labelled top grip, pinch and move to drag that same panel. Two pinches resize it. Buttons and sliders remain UI actions rather than being intercepted as panel movement. Tracking loss, focus loss or locking cancels ownership. A returning hand must release before starting a new drag. Laptop camera resizing remains a separate non-conformal-HUD scale action and is not treated as a 3D hand pose.

## Pilot Brief

The collapsed **PILOT BRIEF** button has one fixed bottom-centre position. It no longer inherits the moving placement chosen for the much larger expanded brief. The expanded dock retains its valid location instead of choosing a new preferred free slot every frame; after an obstruction it waits for a stable free candidate before reappearing. Flight-instrument obstacle geometry is transformed in canvas-local coordinates, and behind-camera/offscreen radar panels do not create mirrored forward obstacles. Its ordinary mouse hit path uses the existing pixel-correct raycaster.

A side-mounted traffic map's detailed/fullscreen mode no longer hides unrelated flight-HUD groups. No camera FOV, IPD, flight telemetry or conformal angular geometry is changed by these layout/input fixes.

## Validation and recovery

Focused fixtures: `FaaRadarBriefDragTests.cs`. Runtime panel/grip assertions: `RunRadarBriefRuntime.cs`. Render-frame probe: `verify_radar_brief_frames.py`. Actual Input System right-button test: `verify_panel_right_drag.py`. These input tests use temporary injected mouse/hand samples, not a physical XR-3 measurement or webcam capture. Evidence is saved in `artifacts/radar-brief-fix/`.

During development the long-running Unity Editor exhausted its native/managed I/O-selector descriptors. The build pipeline then reported completion without producing the updated test assembly; separate compiler diagnostics exposed the resource failure. Recovery files at `artifacts/radar-brief-fix/recovery/` preserve both the original on-disk scene and the current scene contents. The current scene was saved before restarting the Editor so its edits were not lost. Final evidence must come from the rebuilt code after recovery, not the older loaded assemblies.

Live hardware pinch accuracy, headset optical behavior and player builds are not established by Editor tests. This remains a research simulator interface, not certified flight equipment.

## Final measured checks

After restarting and rebuilding the resource-exhausted Editor, 318 direct Editor assertions passed, including eight new radar-footprint and pointer-ownership checks. The live scene passed 24 radar/brief/grip integration assertions and 27 conformal regressions: 369 direct assertions in total. These are focused direct assertion runs, not a full-project Test Runner report; the earlier zero-test discovery result is not counted.

An actual temporary Unity Input System mouse drove right-button drags on Settings, Hand Studio, weather and traffic. All four moved while gesture editing remained locked; the camera did not enter look-around during the captured press, and release cleared both panel and camera ownership. XR-grip routing, acquisition, movement and tracking-loss release were checked with injected tracked-hand rays for the same four panels. This does not establish physical XR-3 tracking performance.

The rendered-frame probe took 336 samples across six phases spanning 480 frames, including open configuration drawers, expanded weather details, maximum-size/near-distance requests, traffic detailed-map mode and side inspection. The collapsed Pilot Brief's x/y/width/height peak-to-peak movement was 0.0 pixels in those samples. The closest measured rendered radar-group corner remained 76.04 degrees from the forward axis, outside the protected 60-degree half-angle. Telemetry values continue to update; this measurement concerns UI geometry.

Current forward preview: `Assets/artifacts/radar-brief-fix/radars-stowed-forward.png`. The radar drawers are open off-axis in that capture. The camera was never opened for testing. No complete player build, release, commit, push or remote simulator modification was performed. Scene recovery copies remain available under the recovery directory above.
