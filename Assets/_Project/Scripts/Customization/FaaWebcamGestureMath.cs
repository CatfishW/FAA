using System;
using UnityEngine;
namespace FAA.Customization
{
    [Serializable] public sealed class FaaWebcamHand
    {
        public float x, y, palmSize, pinchRatio, palmX, palmY, handednessScore;
        public string handedness;
        public int fingers;
        public float[] pinches, extensions, landmarks;
    }
    [Serializable] public sealed class FaaWebcamResult
    {
        public string kind, error, features;
        public int protocol, width, height;
        public long seq;
        public float inferenceMs;
        public FaaWebcamHand[] hands;
    }
    // Webcam image geometry is NOT an XR world-space hand pose.
    public sealed class FaaWebcamGestureMath
    {
        public enum Phase { ReleaseHands, Ready, Holding, Resizing }
        public Phase State { get; private set; } = Phase.ReleaseHands;
        public float Factor { get; private set; } = 1f;
        public bool JustStarted { get; private set; }
        private float openedAt = -1, holdAt = -1, baseline, previousMetric, lastSample = -1;
        private bool leftPinched, rightPinched;
        public void Reset()
        {
            State = Phase.ReleaseHands; Factor = 1; JustStarted = false;
            openedAt = holdAt = lastSample = -1; baseline = previousMetric = 0;
            leftPinched = rightPinched = false;
        }
        public static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        public static bool ValidHand(FaaWebcamHand h) => h != null && Finite(h.x) && Finite(h.y) &&
            h.x >= .015f && h.x <= .985f && h.y >= .015f && h.y <= .985f &&
            Finite(h.palmSize) && h.palmSize >= .025f && h.palmSize <= .7f &&
            Finite(h.pinchRatio) && h.pinchRatio >= 0 && h.pinchRatio <= 8;
        public static bool TryMetric(FaaWebcamResult f, out float metric)
        {
            metric = 0;
            if (f == null || f.kind != "frame" || f.width < 32 || f.width > 640 || f.height < 32 || f.height > 640 ||
                f.hands == null || f.hands.Length != 2 || !ValidHand(f.hands[0]) || !ValidHand(f.hands[1])) return false;
            var a = f.hands[0]; var b = f.hands[1];
            float palm = (a.palmSize + b.palmSize) * .5f;
            if (Mathf.Max(a.palmSize,b.palmSize) > Mathf.Min(a.palmSize,b.palmSize)*2.2f) return false;
            float distance = new Vector2(a.x-b.x,(a.y-b.y)*f.height/f.width).magnitude;
            if (distance < Mathf.Max(.07f,palm*1.2f)) return false;
            metric = distance / palm;
            return Finite(metric) && metric <= 25;
        }
        public bool Update(FaaWebcamResult frame, float now, bool canEdit)
        {
            JustStarted = false;
            if (!canEdit || !Finite(now) || lastSample >= 0 && (now <= lastSample || now-lastSample > .65f) ||
                !TryMetric(frame,out float metric)) { Reset(); return false; }
            float dt = lastSample < 0 ? .1f : now-lastSample; lastSample = now;
            var a = frame.hands[0]; var b = frame.hands[1];
            if (State == Phase.ReleaseHands)
            {
                if (a.pinchRatio < .55f || b.pinchRatio < .55f) { openedAt = -1; return false; }
                if (openedAt < 0) openedAt = now;
                if (now-openedAt >= .25f) State = Phase.Ready;
                return false;
            }
            leftPinched = a.pinchRatio <= (leftPinched ? .48f : .30f);
            rightPinched = b.pinchRatio <= (rightPinched ? .48f : .30f);
            if (!leftPinched || !rightPinched)
            {
                if (State == Phase.Resizing || State == Phase.Holding) Reset();
                return false;
            }
            if (State == Phase.Ready) { holdAt = now; previousMetric = metric; State = Phase.Holding; return false; }
            if (Mathf.Abs(Mathf.Log(metric/Mathf.Max(.001f,previousMetric))) > .40f) { Reset(); return false; }
            previousMetric = metric;
            if (State == Phase.Holding)
            {
                if (now-holdAt < .25f) return false;
                baseline = metric; Factor = 1; State = Phase.Resizing; JustStarted = true; return true;
            }
            float desired = Mathf.Clamp(metric/baseline,.4f,2.5f);
            if (Mathf.Abs(Mathf.Log(desired/Factor)) < .025f) return false;
            float smooth = Mathf.Lerp(Mathf.Log(Factor),Mathf.Log(desired),1-Mathf.Exp(-8*dt));
            Factor = Mathf.Exp(Mathf.MoveTowards(Mathf.Log(Factor),smooth,Mathf.Min(dt,.2f)*1.1f));
            return true;
        }
    }
}
