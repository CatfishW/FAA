using System;
using NUnit.Framework;
using UnityEngine;
namespace FAA.Customization.Tests
{
    public class FaaMultiFingerTests
    {
        private static Type Filter=>Type.GetType("FAA.Customization.FaaMultiFingerResize, Assembly-CSharp",true);
        private static Type Hand=>Type.GetType("FAA.Customization.FaaWebcamHand, Assembly-CSharp",true);
        private static Type FrameType=>Type.GetType("FAA.Customization.FaaWebcamResult, Assembly-CSharp",true);
        private static void Set(object o,string name,object value)=>o.GetType().GetField(name).SetValue(o,value);
        private static object Get(object o,string name)=>o.GetType().GetProperty(name).GetValue(o);
        private static object Frame(float sep=.4f,int finger=-1,float palm=.1f,bool open=true)
        {
            var f=Activator.CreateInstance(FrameType);Set(f,"kind","frame");Set(f,"width",480);Set(f,"height",360);
            Array hands=Array.CreateInstance(Hand,2);
            for(int i=0;i<2;i++)
            {
                var h=Activator.CreateInstance(Hand);float x=.5f+(i==0?-.5f:.5f)*sep;
                Set(h,"x",x);Set(h,"palmX",x);Set(h,"y",.5f);Set(h,"palmY",.5f);Set(h,"palmSize",palm);Set(h,"pinchRatio",finger==0?.2f:2f);
                float[] pinches={2,2,2,2};if(finger>=0)pinches[finger]=.2f;
                Set(h,"pinches",pinches);Set(h,"extensions",open?new float[]{1,1,1,1,1}:new float[5]);Set(h,"landmarks",new float[63]);hands.SetValue(h,i);
            }
            Set(f,"hands",hands);return f;
        }
        private static bool Sample(object filter,object frame,float time,bool enabled=true)=>(bool)Filter.GetMethod("Sample").Invoke(filter,new[]{frame,(object)time,enabled});
        private static object Started(int finger=0)
        {
            var f=Activator.CreateInstance(Filter);Sample(f,Frame(),1);Sample(f,Frame(),1.2f);
            Sample(f,Frame(finger:finger),1.25f);Sample(f,Frame(finger:finger),1.45f);
            Assert.That(Get(f,"State").ToString(),Is.EqualTo("Resizing"));return f;
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void ThumbToEachFingertipCanStartResize(int finger)
        {var f=Started(finger);Assert.That((int)Get(f,"LeftFinger"),Is.EqualTo(finger));Assert.That((bool)Get(f,"JustStarted"),Is.True);}
        [Test] public void CannotStartFromAnAlreadyHeldPinch()
        {
            var f=Activator.CreateInstance(Filter);for(int i=0;i<12;i++)Sample(f,Frame(finger:1),1+i*.1f);
            Assert.That(Get(f,"State").ToString(),Is.EqualTo("ReleaseHands"));
        }
        [Test] public void OpenPalmsModeNeedsNoPinch()
        {
            var f=Activator.CreateInstance(Filter);Filter.GetMethod("SetMode").Invoke(f,new[]{Enum.Parse(Filter.GetNestedType("GestureMode"),"OpenPalms")});
            foreach(float t in new[]{1f,1.2f,1.25f,1.45f,1.68f})Sample(f,Frame(),t);
            Assert.That(Get(f,"State").ToString(),Is.EqualTo("Resizing"));
            Sample(f,Frame(.45f),1.73f);Assert.That((float)Get(f,"Factor"),Is.GreaterThan(1));
            Sample(f,Frame(open:false),1.78f);Assert.That(Get(f,"State").ToString(),Is.EqualTo("ReleaseHands"));
        }
        [TestCase(.45f,true)] [TestCase(.35f,false)]
        public void SpreadAndCloseAreContinuous(float sep,bool growth)
        {var f=Started();Sample(f,Frame(sep,0),1.5f);Assert.That(growth?(float)Get(f,"Factor")>1:(float)Get(f,"Factor")<1,Is.True);}
        [Test] public void FingerChoiceDoesNotSwitchWhileHolding()
        {var f=Started(1);Sample(f,Frame(finger:2),1.5f);Assert.That(Get(f,"State").ToString(),Is.EqualTo("ReleaseHands"));}
        [Test] public void BriefMissingHandFreezesAndRebasesWithoutJump()
        {
            var f=Started();Sample(f,Frame(.45f,0),1.5f);float scale=(float)Get(f,"Factor");
            Sample(f,null,1.55f);Assert.That(Get(f,"State").ToString(),Is.EqualTo("Paused"));
            Assert.That((float)Get(f,"Factor"),Is.EqualTo(scale));Sample(f,Frame(.47f,0),1.63f);
            Assert.That(Get(f,"State").ToString(),Is.EqualTo("Resizing"));Assert.That((float)Get(f,"Factor"),Is.EqualTo(scale).Within(.0001f));
        }
        [Test] public void LongLossRequiresReleaseAgain()
        {var f=Started();Sample(f,null,1.5f);Sample(f,null,1.75f);Sample(f,Frame(finger:0),1.8f);Assert.That(Get(f,"State").ToString(),Is.EqualTo("ReleaseHands"));}
        [Test] public void NearCameraDepthChangeIsNormalized()
        {var f=Started();Sample(f,Frame(.44f,0,.11f),1.5f);Assert.That((float)Get(f,"Factor"),Is.EqualTo(1).Within(.002f));}
        [Test] public void LargeJumpIsNotApplied()
        {var f=Started();Sample(f,Frame(.9f,0),1.5f);Assert.That(Get(f,"State").ToString(),Is.EqualTo("Paused"));Assert.That((float)Get(f,"Factor"),Is.EqualTo(1));}
        [TestCase(1.45f)] [TestCase(1.4f)] [TestCase(float.NaN)]
        public void OutOfOrderAndInvalidTimeCannotResize(float time)
        {var f=Started();Sample(f,Frame(.45f,0),time);Assert.That(Get(f,"State").ToString(),Is.EqualTo("ReleaseHands"));}
        [Test] public void LockCancelsOwnership()
        {var f=Started();Sample(f,Frame(.45f,0),1.5f,false);Assert.That(Get(f,"State").ToString(),Is.EqualTo("ReleaseHands"));}
        [Test] public void MissingFingerArraysFailClosed()
        {
            var frame=Frame();var hand=((Array)FrameType.GetField("hands").GetValue(frame)).GetValue(0);Set(hand,"extensions",null);
            Assert.That((bool)Filter.GetMethod("Valid").Invoke(null,new[]{hand}),Is.False);
        }
        [Test] public void NonfiniteLandmarksFailClosed()
        {
            var frame=Frame();var hand=((Array)FrameType.GetField("hands").GetValue(frame)).GetValue(0);var a=new float[63];a[8]=float.NaN;Set(hand,"landmarks",a);
            Assert.That((bool)Filter.GetMethod("Valid").Invoke(null,new[]{hand}),Is.False);
        }
        [Test] public void RenderInterpolationContinuesBetweenCameraFrames()
        {
            var method=Filter.GetMethod("RenderStep");float current=1;int moving=0;
            for(int i=0;i<6;i++){float next=(float)method.Invoke(null,new object[]{current,1.2f,1f/60,16f});if(next>current)moving++;current=next;}
            Assert.That(moving,Is.EqualTo(6));Assert.That(current,Is.LessThan(1.2f));
        }
        [Test] public void RenderingResponseIsFrameRateIndependent()
        {
            var method=Filter.GetMethod("RenderStep");float a=1,b=1;
            for(int i=0;i<30;i++)a=(float)method.Invoke(null,new object[]{a,1.3f,1f/30,16f});
            for(int i=0;i<120;i++)b=(float)method.Invoke(null,new object[]{b,1.3f,1f/120,16f});
            Assert.That(a,Is.EqualTo(b).Within(.0001));
        }
        [TestCase(float.NaN)] [TestCase(0)] [TestCase(-1)]
        public void InvalidRenderTargetCannotCorruptScale(float target)
        {Assert.That((float)Filter.GetMethod("RenderStep").Invoke(null,new object[]{1f,target,.016f,16f}),Is.EqualTo(1));}
    }
}
