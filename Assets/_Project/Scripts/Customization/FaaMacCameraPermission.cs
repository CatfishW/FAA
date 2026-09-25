using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FAA.Customization
{
    public enum FaaCameraPermissionState { Unavailable = -1, NotRequested = 0, Restricted = 1, Denied = 2, Authorized = 3, NotRequired = 4 }

    /// <summary>Native macOS consent for the hosting Unity app. No capture, helper-app permission or TCC edits.</summary>
    public static class FaaMacCameraPermission
    {
        public static bool IsMac => Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        [DllImport("FaaCameraPermission")] private static extern int FAA_CameraAuthorizationState();
        [DllImport("FaaCameraPermission")] private static extern int FAA_CameraUsageDeclared();
        [DllImport("FaaCameraPermission")] private static extern int FAA_RequestCameraAuthorization();
#endif
        public static FaaCameraPermissionState State
        {
            get
            {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                try { return (FaaCameraPermissionState)FAA_CameraAuthorizationState(); }
                catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is BadImageFormatException)
                { return FaaCameraPermissionState.Unavailable; }
#else
                return FaaCameraPermissionState.NotRequired;
#endif
            }
        }
        public static bool CanCapture(FaaCameraPermissionState state) =>
            state == FaaCameraPermissionState.Authorized || state == FaaCameraPermissionState.NotRequired;
        public static string Description(FaaCameraPermissionState state)
        {
            switch (state)
            {
                case FaaCameraPermissionState.Authorized: return "macOS camera permission: ALLOWED";
                case FaaCameraPermissionState.NotRequested: return "macOS camera permission: not requested";
                case FaaCameraPermissionState.Denied: return "Camera denied. Enable this app in macOS Camera settings.";
                case FaaCameraPermissionState.Restricted: return "Camera restricted by macOS policy or Screen Time.";
                case FaaCameraPermissionState.NotRequired: return "Camera access is checked when starting this device.";
                default: return "Native camera-permission plugin unavailable; rebuild the macOS plugin.";
            }
        }
        public static bool RequestByUser(out string message)
        {
            var state = State;
            message = Description(state);
            if (CanCapture(state)) return false;
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            try
            {
                if (state != FaaCameraPermissionState.NotRequested) return false;
                if (FAA_CameraUsageDeclared() != 1)
                { message = "The hosting app has no camera usage description. Rebuild the player; do not modify the signed Unity Editor."; return false; }
                int result = FAA_RequestCameraAuthorization();
                if (result == 1) { message = "Choose Allow in the macOS camera-permission dialog."; return true; }
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is BadImageFormatException)
            { message = Description(FaaCameraPermissionState.Unavailable); }
#endif
            return false;
        }
        public static void OpenSettingsByUser()
        {
            if (IsMac) Application.OpenURL("x-apple.systempreferences:com.apple.preference.security?Privacy_Camera");
        }
    }
}
