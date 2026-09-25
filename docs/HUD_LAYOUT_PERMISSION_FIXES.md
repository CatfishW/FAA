# HUD layout, desktop buttons and macOS camera consent

## Changes

The duplicate green rotorcraft reference strip (FORWARD FLIGHT/HAGL/Q1/Q2/NR, FPA/SCENE/HOVER/MARK LZ buttons) is no longer created. The underlying conformal renderer, diagnostic DataStatus and public reference-setting APIs remain. This removal does not remove the horizon, pitch ladder, flight-path vector or scene cues.

Non-conformal ALT/IAS, along-track/glideslope, VSI and engine indicators now use measured, separate layout positions. ALT is no longer drawn over the along-track axis. Engines stay below the primary readouts, and navigation and vertical-speed scales occupy separate positions to the right. The layout recomputes from the requested sizes and current viewport, with no incremental position drift. Original positions and scales are restored when the workspace releases them. Radars and calibrated conformal geometry are excluded.

Explicit size controls now work while the gesture/drag lock is on: selected +/- and slider, ALL HUD +/- and RESET SELECTED. Gesture lock still prevents accidental hand/controller/mouse-grip repositioning and webcam gestures. The initial selection is airspeed rather than an offscreen radar, and invisible legacy modules are not offered in the picker. The profile still preserves all registered module sizes.

Normal UGUI EventSystem hit testing now uses the actual overlay canvas pixel rectangle instead of Screen.width/height, which can describe the Editor window rather than a larger fixed-resolution Game view. This fixes right-side buttons receiving no mouse hit in the reported configuration. It retains Graphic/CanvasGroup/masking, display and backface rules. Native camera/world-space raycasts and XR tracked-device raycasts keep their existing path. Repeated size updates no longer reassign unchanged native Canvas render properties and invalidate UI hit depths.

## macOS camera consent

The laptop camera panel now has ALLOW CAMERA and CAMERA SETTINGS controls and displays a separate permission state. ALLOW CAMERA requests AVFoundation camera consent for the hosting Unity Editor or player, without starting video. START CAMERA first obtains authorization, then starts the already-local hand-recognition process and capture. Restricted/denied/missing-plugin cases show distinct messages; CAMERA SETTINGS opens the macOS Camera privacy pane so the user can enable the application.

The small universal arm64/x86_64 native plugin lives at `Assets/Plugins/macOS/FaaCameraPermission.bundle`; its source and reproducible build script are in `Tools/CameraPermission/`. It calls AVFoundation authorizationStatus/requestAccess inside the hosting application. It does not edit/reset TCC, change the signed Unity Editor, obtain permission for the Python helper, open a capture session, or silently grant access. The host must have NSCameraUsageDescription; the existing player build hook supplies one when building a player. A macOS consent dialog requires the user's decision.

Pending authorization does not get cancelled merely because the native consent dialog temporarily takes focus before capture begins. Closing/stopping cancels the requested continuation. A running camera still stops on focus loss. No camera capture was needed to test the UI layout or permission-policy logic.

## Verification

Current-run evidence is written to `artifacts/hud-layout-fixes/`. Pure enum/state tests exercise permission gating without requesting OS access. Runtime layout tests measure rendered graphic bounds at 40%, 72%, 100% and 160% scales. Button tests use ordinary EventSystem raycasts plus UGUI pointer events, not only direct Button.onClick invocation. Existing webcam, radar, gesture and conformal fixtures remain part of the regression pass; removed-strip tests now assert absence while testing preserved APIs directly.

A native plugin build or a passing permission-policy test is not evidence that the user has granted macOS camera access. Report actual grants only when observed, and distinguish them from implementation/testing. Physical webcam/gesture accuracy and XR-3 hardware validation remain separate from these desktop/UI checks.

Apple reference: https://developer.apple.com/documentation/avfoundation/capture_setup/requesting_authorization_to_capture_and_save_media
Apple camera settings: https://support.apple.com/guide/mac-help/control-access-to-your-camera-mchlf6d108da/mac
