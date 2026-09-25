using UnityEngine;

namespace FAA.Customization
{
    /// <summary>Image-space gesture clutch. Recognition samples set a target; rendering interpolates independently.</summary>
    public sealed class FaaMultiFingerResize
    {
        public enum GestureMode { MultiFingerPinch, OpenPalms }
        public enum Phase { ReleaseHands, Ready, Holding, Resizing, Paused }
        public GestureMode Mode { get; private set; }
        public Phase State { get; private set; }
        public bool JustStarted { get; private set; }
        public bool JustResumed { get; private set; }
        public float Factor { get; private set; } = 1;
        public float HoldProgress { get; private set; }
        public int LeftFinger { get; private set; } = -1;
        public int RightFinger { get; private set; } = -1;
        private float lastTime = -1, validAt = -1, phaseAt = -1, missingAt = -1;
        private float baseline, filteredMetric, previousMetric, resumeFactor = 1;
        private Vector2 previousLeft, previousRight;
        public void SetMode(GestureMode mode) { Mode = mode; Reset(); }
        public void Reset()
        {
            State = Phase.ReleaseHands; Factor = 1; JustStarted = JustResumed = false;
            lastTime = validAt = phaseAt = missingAt = -1; HoldProgress = 0;
            LeftFinger = RightFinger = -1; baseline = filteredMetric = previousMetric = 0; resumeFactor = 1;
        }
        public static bool Valid(FaaWebcamHand h)
        {
            if (!FaaWebcamGestureMath.ValidHand(h) || h.pinches == null || h.pinches.Length != 4 ||
                h.extensions == null || h.extensions.Length != 5 || h.landmarks == null || h.landmarks.Length != 63 ||
                !FaaWebcamGestureMath.Finite(h.palmX) || !FaaWebcamGestureMath.Finite(h.palmY) ||
                h.palmX < .02f || h.palmX > .98f || h.palmY < .02f || h.palmY > .98f) return false;
            foreach (float v in h.pinches) if (!FaaWebcamGestureMath.Finite(v) || v < 0 || v > 12) return false;
            foreach (float v in h.extensions) if (!FaaWebcamGestureMath.Finite(v) || v < 0 || v > 1) return false;
            foreach (float v in h.landmarks) if (!FaaWebcamGestureMath.Finite(v) || Mathf.Abs(v) > 4) return false;
            return true;
        }
        public static int PinchFinger(FaaWebcamHand hand)
        {
            if (!Valid(hand)) return -1;
            int best = 0; for (int i=1;i<4;i++) if (hand.pinches[i] < hand.pinches[best]) best = i;
            return hand.pinches[best] <= .38f ? best : -1;
        }
        public static bool Open(FaaWebcamHand h)
        {
            if (!Valid(h)) return false;
            int count=0; for(int i=1;i<5;i++) if(h.extensions[i]>.7f)count++;
            return count>=3;
        }
        private static bool Released(FaaWebcamHand h)
        {
            foreach(float p in h.pinches) if(p<.62f)return false;
            return true;
        }
        public bool Sample(FaaWebcamResult frame, float now, bool enabled)
        {
            JustStarted = JustResumed = false;
            if (!enabled || !FaaWebcamGestureMath.Finite(now) || lastTime >= 0 && now <= lastTime) { Reset(); return false; }
            float dt = lastTime < 0 ? .04f : now-lastTime;
            if (dt > .5f && lastTime >= 0) Reset();
            lastTime = now;
            if (frame == null || frame.kind != "frame" || frame.width<32 || frame.width>640 || frame.height<32 || frame.height>640 ||
                frame.hands == null || frame.hands.Length != 2 || !Valid(frame.hands[0]) || !Valid(frame.hands[1]))
                return Missing(now);
            var a=frame.hands[0]; var b=frame.hands[1];
            if(a.palmX>b.palmX) { var tmp=a;a=b;b=tmp; }
            Vector2 left=new(a.palmX,a.palmY*frame.height/frame.width),right=new(b.palmX,b.palmY*frame.height/frame.width);
            float palm=(a.palmSize+b.palmSize)*.5f;
            float separation=Vector2.Distance(left,right);
            if(Mathf.Max(a.palmSize,b.palmSize)>Mathf.Min(a.palmSize,b.palmSize)*2.2f || separation<Mathf.Max(.07f,palm*1.1f))return Missing(now);
            float metric=separation/palm;
            bool existing=State==Phase.Resizing||State==Phase.Paused||State==Phase.Holding;
            if(existing&&validAt>=0&&(Vector2.Distance(left,previousLeft)>.18f||Vector2.Distance(right,previousRight)>.18f))return Missing(now);
            bool clutch=Mode==GestureMode.OpenPalms ? Open(a)&&Open(b) :
                existing&&LeftFinger>=0&&RightFinger>=0 ? a.pinches[LeftFinger]<.62f&&b.pinches[RightFinger]<.62f : PinchFinger(a)>=0&&PinchFinger(b)>=0;
            if(State==Phase.ReleaseHands)
            {
                bool neutral=Mode==GestureMode.OpenPalms ? Open(a)&&Open(b) : Released(a)&&Released(b);
                if(!neutral){phaseAt=-1;return false;}
                if(phaseAt<0)phaseAt=now;
                if(now-phaseAt>=.18f){State=Phase.Ready;phaseAt=-1;}
                return false;
            }
            if(!clutch) { if(existing)Reset(); return false; }
            if(State==Phase.Ready)
            {
                LeftFinger=PinchFinger(a);RightFinger=PinchFinger(b);State=Phase.Holding;phaseAt=now;
                previousMetric=filteredMetric=metric;previousLeft=left;previousRight=right;validAt=now;return false;
            }
            if(State==Phase.Paused)
            {
                if(now-missingAt>.22f){Reset();return false;}
                // Rebase at the frozen scale after a brief occlusion; no catch-up jump.
                baseline=filteredMetric=previousMetric=metric;resumeFactor=Factor;State=Phase.Resizing;JustResumed=true;
            }
            else if(previousMetric>0&&Mathf.Abs(Mathf.Log(metric/previousMetric))>.38f)return Missing(now);
            validAt=now;missingAt=-1;previousMetric=metric;previousLeft=left;previousRight=right;
            float speed=Mathf.Abs(Mathf.Log(metric/Mathf.Max(.001f,filteredMetric)))/Mathf.Max(.01f,dt);
            float cutoff=3f+Mathf.Min(12f,speed*2f);
            filteredMetric=Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(.001f,filteredMetric)),Mathf.Log(metric),1-Mathf.Exp(-2*Mathf.PI*cutoff*dt)));
            if(State==Phase.Holding)
            {
                float hold=Mode==GestureMode.OpenPalms?.42f:.18f;
                HoldProgress=Mathf.Clamp01((now-phaseAt)/hold);
                if(HoldProgress<1)return false;
                baseline=filteredMetric;resumeFactor=Factor=1;State=Phase.Resizing;JustStarted=true;
            }
            float desired=Mathf.Clamp(resumeFactor*filteredMetric/baseline,.25f,4f);
            // Sub-percent deadband, rather than the previous 2.5% stair-step threshold.
            if(Mathf.Abs(Mathf.Log(desired/Factor))>.003f)Factor=desired;
            return true;
        }
        private bool Missing(float now)
        {
            if(State==Phase.Resizing||State==Phase.Paused)
            {
                if(missingAt<0)missingAt=now;
                if(now-missingAt<=.22f){State=Phase.Paused;return false;}
            }
            Reset();return false;
        }
        public void FreezeAt(float factor) { if(FaaWebcamGestureMath.Finite(factor)&&factor>0)Factor=factor; }
        public static float RenderStep(float current,float target,float dt,float response=16f)
        {
            if(!FaaWebcamGestureMath.Finite(current)||current<=0)current=1;
            if(!FaaWebcamGestureMath.Finite(target)||target<=0||!FaaWebcamGestureMath.Finite(dt)||dt<=0)return current;
            return Mathf.Exp(Mathf.Lerp(Mathf.Log(current),Mathf.Log(target),1-Mathf.Exp(-Mathf.Max(1,response)*Mathf.Min(.1f,dt))));
        }
    }
}
