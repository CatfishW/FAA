using UnityEngine;

namespace FAA.Customization
{
    public sealed partial class FaaLaptopCameraGestures
    {
        public bool PermissionPending { get; private set; }
        public FaaCameraPermissionState PermissionState => FaaMacCameraPermission.State;
        private bool startAfterPermission;
        private float permissionRequestedAt;

        // An explicit separate control can request permission without starting recognition or video.
        public void RequestPermissionByUser()
        {
            if (CameraActive || Starting || PermissionPending) return;
            BeginPermissionRequest(false);
        }
        private bool BeginPermissionRequest(bool startWhenAllowed)
        {
            startAfterPermission = startWhenAllowed;
            PermissionPending = FaaMacCameraPermission.RequestByUser(out string message);
            permissionRequestedAt = Time.unscaledTime;
            Status = message;
            RefreshPanel();
            return PermissionPending;
        }
        private void UpdatePermissionRequest()
        {
            if (!PermissionPending) return;
            var state = PermissionState;
            if (state == FaaCameraPermissionState.NotRequested)
            {
                if (Time.unscaledTime - permissionRequestedAt > 120f)
                { PermissionPending = false; startAfterPermission = false; Status = "Camera permission still pending. Stop/retry when ready."; }
                return;
            }
            if (FaaMacCameraPermission.CanCapture(state) && startAfterPermission && !Application.isFocused) return;
            bool start = startAfterPermission && FaaMacCameraPermission.CanCapture(state);
            PermissionPending = false; startAfterPermission = false;
            Status = FaaMacCameraPermission.Description(state);
            if (start) StartCameraByUser();
            else RefreshPanel();
        }
        public void OpenCameraSettingsByUser()
        {
            StopCamera();
            FaaMacCameraPermission.OpenSettingsByUser();
            Status = "In Privacy & Security > Camera, enable Unity (Editor) or this built app, then return and press Start.";
            RefreshPanel();
        }
    }
}
