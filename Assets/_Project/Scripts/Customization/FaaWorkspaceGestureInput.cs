using System;
using Leap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using Hand = Leap.Hand;

namespace FAA.Customization
{
    /// <summary>
    /// Real Ultraleap world-space hands, with tracked XR controller fallback. No editor/mock poses are
    /// promoted to sensor input. Tracking loss releases ownership without a click, and requires re-pinch.
    /// </summary>
    [DefaultExecutionOrder(12450)]
    public sealed class FaaWorkspaceGestureInput : MonoBehaviour
    {
        private FaaSpatialWorkspace owner;
        private LeapProvider provider;
        private GameObject ownedProvider;
        private readonly FaaWorkspacePointerDispatcher[] pointers = new FaaWorkspacePointerDispatcher[4];
        private readonly bool[] pinched = new bool[4], releaseRequired = new bool[4], handPresent = new bool[2];
        private readonly bool[] uiGesture = new bool[4];
        private long lastTimestamp;
        private float lastFrameChange, nextDiscovery, palmSince = -1f;
        private bool palmLatched, menuButtonWasDown;
        private readonly LineRenderer[] rays = new LineRenderer[4];
        private Material rayMaterial;
        public bool HasHandProvider => provider != null && provider.isActiveAndEnabled;
        public bool HandsTracked => handPresent[0] || handPresent[1];
        public void Bind(FaaSpatialWorkspace workspace)
        {
            CancelPointers(); owner = workspace;
            for (int i = 0; i < pointers.Length; i++) pointers[i] = new FaaWorkspacePointerDispatcher(owner, i);
        }

        private void Update()
        {
            if (owner == null || !owner.Initialized || owner.View == null) return;
            if (owner.NativeXr && !Application.isFocused) { CancelPointers(); return; }
            if (Time.unscaledTime >= nextDiscovery)
            {
                nextDiscovery = Time.unscaledTime + 1f;
                ResolveProvider();
            }
            handPresent[0] = handPresent[1] = false;
            Frame frame = null;
            if (HasHandProvider)
            {
                frame = provider.CurrentFrame;
                if (frame != null && frame.Timestamp != lastTimestamp)
                { lastTimestamp = frame.Timestamp; lastFrameChange = Time.unscaledTime; }
                if (Time.unscaledTime - lastFrameChange > .2f) frame = null;
            }
            Hand left = null;
            if (frame != null)
                foreach (Hand hand in frame.Hands)
                {
                    int index = hand.IsLeft ? 0 : 1;
                    if (handPresent[index]) continue;
                    Vector3 pinch = hand.GetPinchPosition(); // LeapProvider has ALREADY converted to Unity world space.
                    Vector3 shoulder = owner.View.transform.TransformPoint(new Vector3(hand.IsLeft ? -.16f : .16f, -.17f, 0f));
                    Vector3 direction = pinch - shoulder;
                    if (!FaaSpatialLayoutMath.Finite(pinch) || direction.sqrMagnitude < .001f) continue;
                    handPresent[index] = true;
                    Feed(index, new Ray(shoulder, direction.normalized), pinch, true, hand.PinchStrength);
                    if (hand.IsLeft) left = hand;
                }
            for (int i = 0; i < 2; i++)
                if (!handPresent[i]) Feed(i, default, Vector3.zero, false, 0);
            CheckPalmMenu(left);
            bool controllers = FeedController(XRNode.LeftHand, 2, !handPresent[0]) |
                               FeedController(XRNode.RightHand, 3, !handPresent[1]);
            owner.InputStatus = HandsTracked ? "Ultraleap hands tracked" : controllers ? "Tracked controller" :
                HasHandProvider ? "Hands not tracked; mouse fallback" : "Mouse / controller; no hand sensor";
        }

        private void ResolveProvider()
        {
            if (provider == null)
                foreach (var candidate in FindObjectsByType<LeapProvider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (candidate.isActiveAndEnabled) { provider = candidate; break; }
            if (provider != null || !owner.NativeXr) return;
            // Varjo Base supplies the XR-3 tracking service. Do not launch an unsupported native service on Mac.
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor) return;
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.isValid || string.IsNullOrEmpty(head.name) || !head.name.Contains("XR-3", StringComparison.OrdinalIgnoreCase)) return;
            ownedProvider = new GameObject("FAA XR-3 Ultraleap Provider");
            ownedProvider.SetActive(false);
            ownedProvider.transform.SetParent(owner.View.transform, false);
            var leap = ownedProvider.AddComponent<LeapXRServiceProvider>();
            leap.mainCamera = owner.View;
            leap.deviceOffsetMode = LeapXRServiceProvider.DeviceOffsetMode.ManualHeadOffset;
            leap.deviceOffsetYAxis = -.0112f; leap.deviceOffsetZAxis = .0999f; leap.deviceTiltXAxis = 0f;
            provider = leap;
            ownedProvider.SetActive(true);
        }

        private void CheckPalmMenu(Hand hand)
        {
            bool facing = false;
            if (hand != null && !pinched[0])
            {
                Vector3 toEye = owner.View.transform.position - hand.PalmPosition;
                int extended = 0;
                foreach (var finger in hand.fingers) if (finger.IsExtended) extended++;
                facing = extended >= 4 && toEye.magnitude >= .15f && toEye.magnitude <= .8f &&
                         Vector3.Dot(hand.PalmNormal.normalized, toEye.normalized) > .75f;
            }
            if (!facing) { palmSince = -1f; palmLatched = false; return; }
            if (palmSince < 0f) palmSince = Time.unscaledTime;
            if (!palmLatched && Time.unscaledTime - palmSince >= .9f)
            {
                palmLatched = true;
                owner.OpenMenu(); // Opening does NOT unlock layout or move any instrument.
            }
        }

        private bool FeedController(XRNode node, int index, bool allowed)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool valid = allowed && device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked &&
                head.isValid && head.TryGetFeatureValue(CommonUsages.isTracked, out bool headTracked) && headTracked;
            Vector3 local = Vector3.zero, headPosition = Vector3.zero;
            Quaternion rotation = Quaternion.identity, headRotation = Quaternion.identity;
            valid &= device.TryGetFeatureValue(CommonUsages.devicePosition, out local) &&
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation) &&
                head.TryGetFeatureValue(CommonUsages.devicePosition, out headPosition) &&
                head.TryGetFeatureValue(CommonUsages.deviceRotation, out headRotation);
            if (!valid) { Feed(index, default, Vector3.zero, false, 0); return false; }
            Quaternion originRotation = owner.View.transform.rotation * Quaternion.Inverse(headRotation);
            Vector3 origin = owner.View.transform.position - originRotation * headPosition;
            Vector3 position = origin + originRotation * local;
            Vector3 direction = originRotation * rotation * Vector3.forward;
            device.TryGetFeatureValue(CommonUsages.trigger, out float trigger);
            Feed(index, new Ray(position, direction), position, true, trigger);
            if (node == XRNode.LeftHand)
            {
                device.TryGetFeatureValue(CommonUsages.menuButton, out bool menu);
                if (menu && !menuButtonWasDown) owner.ToggleMenu();
                menuButtonWasDown = menu;
            }
            return true;
        }

        private void Feed(int index, Ray ray, Vector3 position, bool tracked, float strength)
        {
            bool previousPinch = pinched[index];
            bool held = FaaSpatialLayoutMath.Pinched(strength, pinched[index]);
            bool valid = tracked && FaaSpatialLayoutMath.Finite(strength) &&
                FaaSpatialLayoutMath.Finite(position) && FaaSpatialLayoutMath.Finite(ray.direction);
            if (!valid)
            {
                releaseRequired[index] = true; pinched[index] = false;
                uiGesture[index] = false;
                owner.SubmitGesture(index, ray, position, false, false, Time.realtimeSinceStartupAsDouble);
                pointers[index]?.Cancel(); DrawRay(index, ray, false); return;
            }
            if (!held) releaseRequired[index] = false;
            pinched[index] = held;
            if (releaseRequired[index]) { DrawRay(index, ray, false); return; }
            var pointer = pointers[index];
            bool hit = pointer.TryHit(ray, out RaycastResult result, out Vector2 pixel);
            // A continuous pinch belongs to the channel where it began. Crossing a menu
            // while holding a pinch cannot reset the layout gesture and cause a ghost grab.
            if (held && !previousPinch) uiGesture[index] = hit && pointer.ShouldUseUi(result);
            bool useUi = pointer.HasPress || uiGesture[index] || !held && hit && pointer.ShouldUseUi(result);
            if (owner.HasGestureCapture(index)) useUi = false;
            if (useUi)
            {
                // A menu click must never begin a simultaneous world-layout drag beneath it.
                owner.SubmitGesture(index, ray, position, true, false, Time.realtimeSinceStartupAsDouble);
                // Hovering onto a button while already pinching is not a fresh button press.
                bool uiHeld = held && (!previousPinch || pointer.HasPress);
                pointer.Process(true, uiHeld, result, pixel);
            }
            else
            {
                pointer.Process(true, false, result, pixel);
                owner.SubmitGesture(index, ray, position, true, held, Time.realtimeSinceStartupAsDouble);
            }
            DrawRay(index, ray, owner.EditMode || owner.MenuOpen || hit);
            if (!held) uiGesture[index] = false;
        }

        private void DrawRay(int index, Ray ray, bool visible)
        {
            if (!visible) { if (rays[index] != null) rays[index].enabled = false; return; }
            if (rays[index] == null)
            {
                if (rayMaterial == null)
                {
                    Shader shader = Resources.Load<Shader>("Shaders/FaaRotorcraftConformal");
                    if (shader == null) return;
                    rayMaterial = new Material(shader);
                }
                var go = new GameObject("FAA Layout Pointer " + index); go.transform.SetParent(transform, false);
                rays[index] = go.AddComponent<LineRenderer>();
                rays[index].sharedMaterial = rayMaterial; rays[index].positionCount = 2;
                rays[index].startWidth = .0018f; rays[index].endWidth = .001f;
                rays[index].startColor = rays[index].endColor = new Color(.4f, .95f, .85f, .75f);
                rays[index].useWorldSpace = true; rays[index].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            rays[index].enabled = true;
            rays[index].SetPosition(0, ray.origin);
            rays[index].SetPosition(1, ray.GetPoint(1.5f));
        }

        public void CancelPointers()
        {
            foreach (var pointer in pointers) pointer?.Cancel();
            for (int i = 0; i < 4; i++)
            {
                releaseRequired[i] = pinched[i]; pinched[i] = false;
                uiGesture[i] = false;
                if (rays[i] != null) rays[i].enabled = false;
            }
            owner?.CancelManipulation();
        }
        private void OnDisable() { CancelPointers(); if (ownedProvider != null) ownedProvider.SetActive(false); }
        private void OnEnable() { if (ownedProvider != null) ownedProvider.SetActive(true); }
        private void OnDestroy()
        {
            if (ownedProvider != null) Destroy(ownedProvider);
            if (rayMaterial != null) Destroy(rayMaterial);
        }
    }
}
