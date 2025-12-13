using UnityEngine;

namespace HUDControl.Core
{
    /// <summary>
    /// High-performance script-based animation utilities for HUD elements.
    /// All methods are designed for zero-allocation, frame-by-frame animation.
    /// </summary>
    public static class HUDAnimator
    {
        #region Position Animation
        
        /// <summary>
        /// Smoothly interpolate RectTransform anchoredPosition
        /// </summary>
        public static void AnimatePosition(RectTransform target, Vector2 targetPosition, float smoothing)
        {
            if (target == null) return;
            target.anchoredPosition = Vector2.Lerp(target.anchoredPosition, targetPosition, smoothing);
        }
        
        /// <summary>
        /// Smoothly interpolate RectTransform anchoredPosition Y only
        /// </summary>
        public static void AnimatePositionY(RectTransform target, float targetY, float smoothing)
        {
            if (target == null) return;
            var pos = target.anchoredPosition;
            pos.y = Mathf.Lerp(pos.y, targetY, smoothing);
            target.anchoredPosition = pos;
        }
        
        /// <summary>
        /// Smoothly interpolate RectTransform anchoredPosition X only
        /// </summary>
        public static void AnimatePositionX(RectTransform target, float targetX, float smoothing)
        {
            if (target == null) return;
            var pos = target.anchoredPosition;
            pos.x = Mathf.Lerp(pos.x, targetX, smoothing);
            target.anchoredPosition = pos;
        }
        
        /// <summary>
        /// Set position immediately without smoothing
        /// </summary>
        public static void SetPosition(RectTransform target, Vector2 position)
        {
            if (target == null) return;
            target.anchoredPosition = position;
        }
        
        #endregion
        
        #region Rotation Animation
        
        /// <summary>
        /// Smoothly interpolate Z rotation (common for roll, pointers)
        /// </summary>
        public static void AnimateRotationZ(RectTransform target, float targetAngle, float smoothing)
        {
            if (target == null) return;
            var euler = target.localEulerAngles;
            euler.z = Mathf.LerpAngle(euler.z, targetAngle, smoothing);
            target.localEulerAngles = euler;
        }
        
        /// <summary>
        /// Smoothly interpolate full rotation using Quaternion slerp
        /// </summary>
        public static void AnimateRotation(RectTransform target, Quaternion targetRotation, float smoothing)
        {
            if (target == null) return;
            target.localRotation = Quaternion.Slerp(target.localRotation, targetRotation, smoothing);
        }
        
        /// <summary>
        /// Set Z rotation immediately
        /// </summary>
        public static void SetRotationZ(RectTransform target, float angle)
        {
            if (target == null) return;
            target.localEulerAngles = new Vector3(0, 0, angle);
        }
        
        #endregion
        
        #region Scale Animation
        
        /// <summary>
        /// Smoothly interpolate uniform scale
        /// </summary>
        public static void AnimateScale(RectTransform target, float targetScale, float smoothing)
        {
            if (target == null) return;
            var scale = Vector3.one * Mathf.Lerp(target.localScale.x, targetScale, smoothing);
            target.localScale = scale;
        }
        
        #endregion
        
        #region Value Interpolation
        
        /// <summary>
        /// Smooth float value with optional deadzone
        /// </summary>
        public static float SmoothValue(float current, float target, float smoothing, float deadzone = 0.001f)
        {
            if (Mathf.Abs(target - current) < deadzone) return target;
            return Mathf.Lerp(current, target, smoothing);
        }
        
        /// <summary>
        /// Smooth angle value using LerpAngle (handles 0/360 wrap)
        /// </summary>
        public static float SmoothAngle(float current, float target, float smoothing)
        {
            return Mathf.LerpAngle(current, target, smoothing);
        }
        
        /// <summary>
        /// Calculate smoothing factor for frame-rate independent animation
        /// </summary>
        /// <param name="responsiveness">Higher = faster response (0.1 to 20 typical)</param>
        public static float CalculateSmoothing(float responsiveness)
        {
            return 1f - Mathf.Exp(-responsiveness * Time.deltaTime);
        }
        
        #endregion
        
        #region Utility
        
        /// <summary>
        /// Normalize angle to 0-360 range
        /// </summary>
        public static float NormalizeAngle(float angle)
        {
            while (angle < 0) angle += 360f;
            while (angle >= 360f) angle -= 360f;
            return angle;
        }
        
        /// <summary>
        /// Normalize angle to -180 to 180 range
        /// </summary>
        public static float NormalizeAngleSigned(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle <= -180f) angle += 360f;
            return angle;
        }
        
        /// <summary>
        /// Clamp value and return whether it was clamped
        /// </summary>
        public static float ClampWithFlag(float value, float min, float max, out bool wasClamped)
        {
            wasClamped = value < min || value > max;
            return Mathf.Clamp(value, min, max);
        }
        
        /// <summary>
        /// Map value from one range to another
        /// </summary>
        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float t = Mathf.InverseLerp(fromMin, fromMax, value);
            return Mathf.Lerp(toMin, toMax, t);
        }
        
        #endregion
    }
}
