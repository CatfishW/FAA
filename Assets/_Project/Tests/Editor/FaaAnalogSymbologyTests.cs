using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace FAA.Customization.Tests
{
    public class FaaAnalogSymbologyTests
    {
        private static Type T(string name)=>Type.GetType("FAA.Customization."+name+", Assembly-CSharp",true);
        private static object Call(string name,string method,params object[] args)=>T(name).GetMethod(method).Invoke(null,args);
        private static void Set(object o,string key,object v)=>o.GetType().GetField(key).SetValue(o,v);
        private static U Field<U>(object o,string key)=>(U)o.GetType().GetField(key).GetValue(o);
        private static object Sample(float speed=50,float altitude=1400,float roll=0)
        {
            var s=Activator.CreateInstance(T("FaaAnalogFlightSample"));
            foreach(var f in new[]{"Fresh","SpeedValid","AltitudeValid","AttitudeValid","TorqueValid","RpmValid"})Set(s,f,true);
            Set(s,"Speed",speed);Set(s,"Altitude",altitude);Set(s,"Roll",roll);Set(s,"Torque",78f);Set(s,"Rpm",100f);return s;
        }
        [TestCase(0f,0f)][TestCase(60f,90f)][TestCase(120f,180f)][TestCase(140f,210f)][TestCase(180f,270f)]
        public void AirspeedDialMatchesClockwiseScale(float knots,float angle)=>Assert.That((float)Call("FaaAnalogAnimation","SpeedAngle",knots),Is.EqualTo(angle).Within(.001f));
        [TestCase(0f,0f)][TestCase(1400f,144f)][TestCase(1000f,0f)][TestCase(11999f,359.64f)][TestCase(-100f,324f)]
        public void AltitudeUsesExplicitThousandFootRevolution(float feet,float angle)=>Assert.That((float)Call("FaaAnalogAnimation","AltitudeAngle",feet),Is.EqualTo(angle).Within(.001f));
        [Test]
        public void AltitudeNeedleCrossesZeroWithoutReverseSpin()
        {
            float a=(float)Call("FaaAnalogAnimation","AltitudeAngle",999f),b=(float)Call("FaaAnalogAnimation","AltitudeAngle",1001f);
            Assert.That(Mathf.DeltaAngle(a,b),Is.EqualTo(.72f).Within(.001f));
        }
        [TestCase(-10f,150f,-135f)][TestCase(75f,150f,0f)][TestCase(200f,150f,135f)][TestCase(60f,120f,0f)]
        public void PercentageNeedlesAreBounded(float value,float max,float expected)=>Assert.That((float)Call("FaaAnalogAnimation","PercentAngle",value,max),Is.EqualTo(expected).Within(.001f));
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void AnimationIsFrameRateIndependentAndMonotone(int fps)
        {
            float value=0,previous=0;
            for(int i=0;i<fps;i++)
            {
                value=(float)Call("FaaAnalogAnimation","Damp",value,100f,1f/fps,14f);
                Assert.That(value,Is.InRange(previous,100f));previous=value;
            }
            Assert.That(value,Is.EqualTo(100f*(1f-Mathf.Exp(-14))).Within(.0001f));
        }
        [Test]
        public void RollInterpolationUsesShortestPath()
        {
            float result=(float)Call("FaaAnalogAnimation","DampAngle",179f,-179f,1f/60);
            Assert.That(result,Is.GreaterThan(179f).And.LessThan(181f));
        }
        [Test]
        public void InvalidSamplesRemoveLiveFlagsImmediatelyAndRecoverySnaps()
        {
            var type=T("FaaAnalogAnimation");var animation=Activator.CreateInstance(type);var step=type.GetMethod("Step");
            step.Invoke(animation,new[]{Sample(),(object).016f,false});
            var invalid=Sample(180);Set(invalid,"Fresh",false);Set(invalid,"SpeedValid",false);
            step.Invoke(animation,new[]{invalid,(object).016f,false});
            var display=type.GetProperty("Display").GetValue(animation);
            Assert.That(Field<bool>(display,"Fresh"),Is.False);Assert.That(Field<bool>(display,"SpeedValid"),Is.False);
            step.Invoke(animation,new[]{Sample(180),(object).016f,false});display=type.GetProperty("Display").GetValue(animation);
            Assert.That(Field<float>(display,"Speed"),Is.EqualTo(180));
        }
        [Test]
        public void ReducedMotionIsDirectAndLargeAltitudeTeleportDoesNotSweep()
        {
            var type=T("FaaAnalogAnimation");var a=Activator.CreateInstance(type);var step=type.GetMethod("Step");
            step.Invoke(a,new[]{Sample(),(object).016f,false});step.Invoke(a,new[]{Sample(130,15000),(object).016f,true});
            var d=type.GetProperty("Display").GetValue(a);Assert.That(Field<float>(d,"Speed"),Is.EqualTo(130));Assert.That(Field<float>(d,"Altitude"),Is.EqualTo(15000));
        }
        private static object Data()=>Activator.CreateInstance(Type.GetType("AviationUI.AviationFlightData, Assembly-CSharp",true));
        private static object Adapt(object data,Dictionary<string,float> aircraft=null,Dictionary<string,float> systems=null,bool fresh=true)=>Call("FaaAnalogFlightSample","From",data,aircraft,systems,fresh);
        [Test]
        public void MissingDatarefsAreNeverValidZeroReadings()
        {
            var s=Adapt(Data());foreach(string flag in new[]{"SpeedValid","AltitudeValid","VerticalSpeedValid","TorqueValid","RpmValid","LocValid","GsValid"})Assert.That(Field<bool>(s,flag),Is.False,flag);
        }
        [Test]
        public void OneValidInstrumentDoesNotRequireEveryOtherChannel()
        {
            var d=Data();Set(d,"indicatedAirspeed",60f);
            var s=Adapt(d,new Dictionary<string,float>{{"sim/flightmodel/position/indicated_airspeed",60f}});
            Assert.That(Field<bool>(s,"SpeedValid"),Is.True);Assert.That(Field<bool>(s,"AltitudeValid"),Is.False);
        }
        [Test]
        public void TorqueMaximumUsesBothEnginesAndReportsIncompleteSource()
        {
            var d=Data();Set(d,"engineCount",2);Set(d,"engine1Torque",75f);Set(d,"engine2Torque",88f);Set(d,"engine1TorqueValid",true);Set(d,"engine2TorqueValid",true);
            var s=Adapt(d);Assert.That(Field<float>(s,"Torque"),Is.EqualTo(88));Assert.That(Field<bool>(s,"TorqueValid"),Is.True);
            Set(d,"engine2TorqueValid",false);s=Adapt(d);Assert.That(Field<bool>(s,"TorqueValid"),Is.False);
        }
        [Test]
        public void NavigationDotsRequireAnActualSignalAndDeviationChannel()
        {
            var d=Data();Set(d,"ilsValid",true);var s=Adapt(d);Assert.That(Field<bool>(s,"LocValid"),Is.False);
            var channels=new Dictionary<string,float>{{"sim/cockpit2/radios/indicators/hsi_hdef_dots_pilot",1.1f}};
            s=Adapt(d,null,channels);Assert.That(Field<bool>(s,"LocValid"),Is.True);Assert.That(Field<bool>(s,"GsValid"),Is.False);
            Set(d,"ilsValid",false);s=Adapt(d,null,channels);Assert.That(Field<bool>(s,"LocValid"),Is.False);
        }
        [TestCase(18,"HDG","VS","","")][TestCase(34,"HDG","--","","ALT ARM")]
        [TestCase(768,"NAV","--","NAV ARM","")][TestCase(2050,"HDG","G/S","","")]
        public void ModeLabelsUseDocumentedReadOnlyBits(int mask,string roll,string pitch,string armedRoll,string armedPitch)
        {
            object[] args={mask,null,null,null,null};Call("FaaAnalogFlightSample","DecodeModes",args);
            Assert.That(args[1],Is.EqualTo(roll));Assert.That(args[2],Is.EqualTo(pitch));Assert.That(args[3],Is.EqualTo(armedRoll));Assert.That(args[4],Is.EqualTo(armedPitch));
        }
        [TestCase("")][TestCase("broken json")][TestCase("{\"schema\":2}")][TestCase("{\"schema\":1,\"selected\":99}")]
        [TestCase("{\"schema\":1,\"classic\":[{\"id\":\"airspeed\"},{\"id\":\"airspeed\"}]}")]
        public void MalformedStylePreferencesFailClosed(string json)
        {object[] args={json,null};Assert.That((bool)Call("FaaSymbologyPreferences","TryParse",args),Is.False);Assert.That(args[1],Is.Null);}
        [Test]
        public void StyleSizesClampAndRoundTripIndependently()
        {
            const string json="{\"schema\":1,\"selected\":1,\"localAttitude\":false,\"digital\":[{\"id\":\"airspeed\",\"scale\":0.6}],\"classic\":[{\"id\":\"airspeed\",\"scale\":4}]}";
            object[] args={json,null};Assert.That((bool)Call("FaaSymbologyPreferences","TryParse",args),Is.True);
            string saved=JsonUtility.ToJson(args[1]);Assert.That(saved,Does.Contain("1.6"));Assert.That(saved,Does.Contain("0.6"));
        }
    }
}
