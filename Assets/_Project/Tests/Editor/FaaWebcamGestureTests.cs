using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace FAA.Customization.Tests
{
    public class FaaWebcamGestureTests
    {
        private static Type MathType=>Type.GetType("FAA.Customization.FaaWebcamGestureMath, Assembly-CSharp",true);
        private static Type ResultType=>Type.GetType("FAA.Customization.FaaWebcamResult, Assembly-CSharp",true);
        private static Type HandType=>Type.GetType("FAA.Customization.FaaWebcamHand, Assembly-CSharp",true);
        private static void Set(object obj,string name,object value)=>obj.GetType().GetField(name).SetValue(obj,value);
        private static T Get<T>(object obj,string name)=>(T)obj.GetType().GetProperty(name).GetValue(obj);
        private static object Frame(float separation=.4f,float palm=.1f,float pinch=.8f)
        {
            object result=Activator.CreateInstance(ResultType);Set(result,"kind","frame");Set(result,"width",320);Set(result,"height",240);
            Array hands=Array.CreateInstance(HandType,2);
            for(int i=0;i<2;i++)
            {
                var hand=Activator.CreateInstance(HandType);Set(hand,"x",.5f+(i==0?-1:1)*separation*.5f);Set(hand,"y",.5f);
                Set(hand,"palmSize",palm);Set(hand,"pinchRatio",pinch);hands.SetValue(hand,i);
            }
            Set(result,"hands",hands);return result;
        }
        private static bool Update(object state,object frame,float time,bool edit=true)=>(bool)MathType.GetMethod("Update").Invoke(state,new[]{frame,(object)time,edit});
        private static string State(object state)=>state.GetType().GetProperty("State").GetValue(state).ToString();
        private static object StartGesture()
        {
            var state=Activator.CreateInstance(MathType);
            foreach(float t in new[]{1f,1.15f,1.3f})Update(state,Frame(),t);
            foreach(float t in new[]{1.4f,1.55f,1.7f})Update(state,Frame(pinch:.15f),t);
            Assert.That(State(state),Is.EqualTo("Resizing"));return state;
        }
        [Test] public void StartingWithPinchesCannotGrab()
        {
            var state=Activator.CreateInstance(MathType);
            for(int i=0;i<20;i++)Assert.That(Update(state,Frame(pinch:.15f),1+i*.1f),Is.False);
            Assert.That(State(state),Is.EqualTo("ReleaseHands"));
        }
        [Test] public void RequiresOpenHandsAndDeliberatePinchHold()
        {
            var state=Activator.CreateInstance(MathType);
            Update(state,Frame(),1f);Update(state,Frame(),1.1f);Assert.That(State(state),Is.EqualTo("ReleaseHands"));
            Update(state,Frame(),1.3f);Assert.That(State(state),Is.EqualTo("Ready"));
            Update(state,Frame(pinch:.15f),1.4f);Assert.That(State(state),Is.EqualTo("Holding"));
            Update(state,Frame(pinch:.15f),1.5f);Assert.That(State(state),Is.EqualTo("Holding"));
            Assert.That(Update(state,Frame(pinch:.15f),1.7f),Is.True);Assert.That(Get<bool>(state,"JustStarted"),Is.True);
        }
        [TestCase(.50f,true)] [TestCase(.30f,false)]
        public void SpreadAndCloseProduceBoundedSizeChanges(float separation,bool enlarges)
        {
            var state=StartGesture();Update(state,Frame(separation,pinch:.15f),1.8f);
            float factor=Get<float>(state,"Factor");
            Assert.That(enlarges?factor>1:factor<1,Is.True);Assert.That(factor,Is.InRange(.89f,1.12f));
        }
        [Test] public void MovingTowardCameraWithoutSpreadingDoesNotResize()
        {
            var state=StartGesture();Assert.That(Update(state,Frame(.6f,.15f,.15f),1.8f),Is.False);
            Assert.That(Get<float>(state,"Factor"),Is.EqualTo(1).Within(.001f));
        }
        [Test] public void MinorTrackingJitterIsIgnored()
        {
            var state=StartGesture();Assert.That(Update(state,Frame(.403f,.1f,.15f),1.8f),Is.False);
            Assert.That(Get<float>(state,"Factor"),Is.EqualTo(1));
        }
        [Test] public void LargeTrackingJumpCancelsRatherThanScaling()
        {
            var state=StartGesture();Assert.That(Update(state,Frame(.8f,.1f,.15f),1.8f),Is.False);
            Assert.That(State(state),Is.EqualTo("ReleaseHands"));
        }
        [Test] public void ReleasingEitherHandEndsResizeAndRequiresRearm()
        {
            var state=StartGesture();var frame=Frame(pinch:.15f);
            Set(((Array)ResultType.GetField("hands").GetValue(frame)).GetValue(0),"pinchRatio",.6f);
            Update(state,frame,1.8f);Assert.That(State(state),Is.EqualTo("ReleaseHands"));
            Update(state,Frame(pinch:.15f),1.9f);Assert.That(State(state),Is.EqualTo("ReleaseHands"));
        }
        [TestCase(1.7f)] [TestCase(1.6f)] [TestCase(2.5f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void DuplicateOutOfOrderStaleOrNonfiniteTimeCancels(float timestamp)
        {
            var state=StartGesture();Assert.That(Update(state,Frame(pinch:.15f),timestamp),Is.False);
            Assert.That(State(state),Is.EqualTo("ReleaseHands"));
        }
        [Test] public void LockedLayoutCannotResize()
        {
            var state=StartGesture();Assert.That(Update(state,Frame(.5f,pinch:.15f),1.8f,false),Is.False);
            Assert.That(State(state),Is.EqualTo("ReleaseHands"));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(3)]
        public void OnlyTwoDetectedHandsAreAccepted(int count)
        {
            var state=StartGesture();var frame=Frame();Set(frame,"hands",Array.CreateInstance(HandType,count));
            Assert.That(Update(state,frame,1.8f),Is.False);Assert.That(State(state),Is.EqualTo("ReleaseHands"));
        }
        [TestCase("x",float.NaN)] [TestCase("x",-.1f)] [TestCase("y",1.1f)]
        [TestCase("palmSize",0f)] [TestCase("pinchRatio",float.PositiveInfinity)] [TestCase("pinchRatio",-1f)]
        public void InvalidLandmarksAreRejected(string field,float value)
        {
            var frame=Frame();Set(((Array)ResultType.GetField("hands").GetValue(frame)).GetValue(0),field,value);
            object[] args={frame,null};Assert.That((bool)MathType.GetMethod("TryMetric").Invoke(null,args),Is.False);
        }
        [Test] public void OverlappingHandsAreRejected()
        {
            object[] args={Frame(.05f),null};Assert.That((bool)MathType.GetMethod("TryMetric").Invoke(null,args),Is.False);
        }
        [Test] public void OneHandFartherAwayIsRejected()
        {
            var frame=Frame();Set(((Array)ResultType.GetField("hands").GetValue(frame)).GetValue(0),"palmSize",.3f);
            object[] args={frame,null};Assert.That((bool)MathType.GetMethod("TryMetric").Invoke(null,args),Is.False);
        }
        [TestCase(0,false,"1234")] [TestCase(90,false,"2413")] [TestCase(180,false,"4321")]
        [TestCase(270,false,"3142")] [TestCase(0,true,"3412")] [TestCase(360,false,"1234")]
        public void CameraPixelOrientationAndVerticalMirror(int angle,bool mirror,string expected)
        {
            Type type=Type.GetType("FAA.Customization.FaaWebcamFrameMath, Assembly-CSharp",true);
            Color32[] source={new(1,0,0,255),new(2,0,0,255),new(3,0,0,255),new(4,0,0,255)},output=new Color32[4];
            type.GetMethod("Upright").Invoke(null,new object[]{source,2,2,output,2,2,angle,mirror});
            string actual="";foreach(var pixel in output)actual+=pixel.r.ToString();Assert.That(actual,Is.EqualTo(expected));
        }
    }
}
