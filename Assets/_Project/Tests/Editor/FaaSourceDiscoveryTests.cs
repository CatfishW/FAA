using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace FAA.Customization.Tests
{
    public sealed class FaaSourceDiscoveryTests
    {
        private const string Namespace="FAA.XPlaneIntegration.Runtime.";
        private const string P="sim/flightmodel/position/";
        private static Type T(string name)=>Type.GetType(Namespace+name+", Assembly-CSharp",true);
        private static object Call(string type,string method,params object[] args)=>T(type).GetMethod(method).Invoke(null,args);
        private static object Invoke(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method).Invoke(obj,args);
        private static void Set(object obj,string field,object value)=>obj.GetType().GetField(field).SetValue(obj,value);
        private static object Get(object obj,string field)=>obj.GetType().GetField(field).GetValue(obj);
        private static Dictionary<string,double> Core()=>new()
        {
            [P+"latitude"]=33,[P+"longitude"]=-83,[P+"elevation"]=1200,[P+"theta"]=2,[P+"phi"]=3,[P+"psi"]=90,
            [P+"indicated_airspeed"]=100,[P+"groundspeed"]=45,[P+"vh_ind"]=1
        };
        private static string Json(Dictionary<string,double> values)=>"{\"raw\":{"+string.Join(",",values.Select(p=>"\""+p.Key+"\":"+p.Value.ToString("R",CultureInfo.InvariantCulture)))+"}}";
        private static object Frame(string id,double now,int priority=100,string signature=null)
        {
            var f=Activator.CreateInstance(T("XPlaneDiscoveredFrame"));Set(f,"Id",id);Set(f,"Label",id);Set(f,"Priority",priority);Set(f,"Received",now);Set(f,"Values",Core());Set(f,"Signature",signature);return f;
        }
        [Test] public void CompleteCanonicalSnapshotIsRecognized()
        {
            object[] args={Json(Core()),null,null};Assert.That((bool)Call("XPlaneDiscoveryData","DecodeJson",args),Is.True);
            Assert.That(((Dictionary<string,double>)args[1])[P+"latitude"],Is.EqualTo(33));
        }
        [Test] public void DeclaredOwnshipUnitsAreConvertedNotGuessed()
        {
            const string json="{\"ownship\":{\"latitude\":33,\"longitude\":-83,\"altitude_m\":1200,\"pitch_deg\":2,\"roll_deg\":3,\"heading_deg\":90,\"indicated_airspeed_kt\":100,\"ground_speed_kt\":100,\"vertical_speed_fpm\":1000}}";
            object[] args={json,null,null};Assert.That((bool)Call("XPlaneDiscoveryData","DecodeJson",args),Is.True);
            var values=(Dictionary<string,double>)args[1];Assert.That(values[P+"groundspeed"],Is.EqualTo(51.44444444).Within(.0001));Assert.That(values[P+"vh_ind"],Is.EqualTo(5.08).Within(.00001));
        }
        [TestCase("{}")] [TestCase("{\"pitch\":2,\"altitude\":1200}")] [TestCase("[]")] [TestCase("bad json")]
        public void UnrecognizedOrAmbiguousSchemaNeverBecomesFlightData(string json)
        {object[] args={json,null,null};Assert.That((bool)Call("XPlaneDiscoveryData","DecodeJson",args),Is.False);}
        [TestCase("latitude",91)] [TestCase("longitude",181)] [TestCase("elevation",100001)] [TestCase("theta",91)] [TestCase("indicated_airspeed",-1)] [TestCase("vh_ind",double.NaN)]
        public void InvalidCoreIsRejected(string field,double value)
        {var values=Core();values[P+field]=value;Assert.That((bool)Call("XPlaneDiscoveryData","CoreValid",values),Is.False);}
        [Test] public void StaleJsonTimestampIsNotTreatedAsLive()
        {
            string json=Json(Core()).TrimEnd('}')+"},\"timestamp\":\"2001-01-01T00:00:00Z\"}";
            object[] args={json,null,null};Assert.That((bool)Call("XPlaneDiscoveryData","DecodeJson",args),Is.False);
        }
        [Test] public void DefaultConfigAndCredentialFreeFallbackAreValid()
        {
            var config=Activator.CreateInstance(T("XPlaneSourceConfig"));object[] args={config,null};Assert.That((bool)Call("XPlaneSourceConfig","Validate",args),Is.True);
            Set(config,"startupGraceSeconds",double.NaN);Assert.That((bool)Call("XPlaneSourceConfig","Validate",args),Is.False);
        }
        [TestCase("127.0.0.1:49000",true)] [TestCase("10.0.0.2:49100",true)]
        [TestCase("127.0.0.1:0",false)] [TestCase("0.0.0.0:49000",false)] [TestCase("http://host",false)] [TestCase("255.255.255.255:49000",false)]
        public void ExplicitUdpEndpointsAreBounded(string endpoint,bool expected)
        {object[] args={endpoint,null};Assert.That((bool)Call("XPlaneSourceConfig","TryUdpEndpoint",args),Is.EqualTo(expected));}
        [TestCase("#",true)] [TestCase("flight/+/data",true)] [TestCase("flight/#",true)]
        [TestCase("flight/#/set",false)] [TestCase("flight+",false)] [TestCase("",false)]
        public void MqttFiltersAreValidated(string filter,bool expected)=>Assert.That((bool)Call("XPlaneSourceConfig","TopicFilterValid",filter),Is.EqualTo(expected));
        [Test] public void ReadOnlyRrefRequestHasExactProtocolLayout()
        {
            var bytes=(byte[])Call("XPlaneDiscoveryUdp","Request",P+"theta",10,732);
            Assert.That(bytes.Length,Is.EqualTo(413));Assert.That(Encoding.ASCII.GetString(bytes,0,5),Is.EqualTo("RREF\0"));
            Assert.That(BitConverter.ToInt32(bytes,5),Is.EqualTo(10));Assert.That(BitConverter.ToInt32(bytes,9),Is.EqualTo(732));
            Assert.That(Encoding.ASCII.GetString(bytes,13,400).TrimEnd('\0'),Is.EqualTo(P+"theta"));
            Assert.That(BitConverter.ToInt32((byte[])Call("XPlaneDiscoveryUdp","Request",P+"theta",0,732),5),Is.Zero);
        }
        [Test] public void RrefDecoderRejectsUnknownIdsAndTruncatedRecords()
        {
            byte[] bytes=new byte[21];Encoding.ASCII.GetBytes("RREF").CopyTo(bytes,0);BitConverter.GetBytes(50).CopyTo(bytes,5);BitConverter.GetBytes(2f).CopyTo(bytes,9);BitConverter.GetBytes(999).CopyTo(bytes,13);BitConverter.GetBytes(7f).CopyTo(bytes,17);
            var values=(Dictionary<string,double>)Call("XPlaneDiscoveryUdp","ParseRref",bytes,50,new[]{P+"theta"});Assert.That(values.Count,Is.EqualTo(1));Assert.That(values[P+"theta"],Is.EqualTo(2));
            Assert.That(Call("XPlaneDiscoveryUdp","ParseRref",bytes.Take(20).ToArray(),50,new[]{P+"theta"}),Is.Null);
        }
        [TestCase(1,120400,1,true)] [TestCase(2,120400,1,false)] [TestCase(1,110550,1,false)] [TestCase(1,120400,2,false)]
        public void BeaconRequiresXPlane12MasterIdentity(int host,int version,int role,bool expected)
        {
            var bytes=new byte[25];Encoding.ASCII.GetBytes("BECN\0").CopyTo(bytes,0);bytes[5]=1;bytes[6]=2;
            BitConverter.GetBytes(host).CopyTo(bytes,7);BitConverter.GetBytes(version).CopyTo(bytes,11);BitConverter.GetBytes(role).CopyTo(bytes,15);BitConverter.GetBytes((ushort)49000).CopyTo(bytes,19);
            object[] args={bytes,0,0};Assert.That((bool)Call("XPlaneDiscoveryUdp","ParseBeacon",args),Is.EqualTo(expected));
        }
        [Test] public void OnePacketIsInsufficientForAutomaticSelection()
        {
            var selector=Activator.CreateInstance(T("XPlaneSourceSelector"));Invoke(selector,"Observe",Frame("a",1));
            Assert.That(Invoke(selector,"Select",1d,""),Is.Null);
            Invoke(selector,"Observe",Frame("a",1.25));Invoke(selector,"Observe",Frame("a",1.5));
            Assert.That(Get(Invoke(selector,"Select",1.5d,""),"Id"),Is.EqualTo("a"));
            Assert.That(Invoke(selector,"Select",2.5d,""),Is.Null);
        }
        [Test] public void TimestampReplayDoesNotWarmUpCandidate()
        {
            var selector=Activator.CreateInstance(T("XPlaneSourceSelector"));
            for(int i=0;i<10;i++)Invoke(selector,"Observe",Frame("a",1+i*.1,100,"constant"));
            Assert.That(Invoke(selector,"Select",2d,""),Is.Null);
        }
        [Test] public void LiveSourceIsStickyAndManualPinDoesNotSilentlyFallbackToAnother()
        {
            var selector=Activator.CreateInstance(T("XPlaneSourceSelector"));
            for(int i=0;i<3;i++)Invoke(selector,"Observe",Frame("mqtt",1+i*.25,65));
            Assert.That(Get(Invoke(selector,"Select",1.5d,""),"Id"),Is.EqualTo("mqtt"));
            for(int i=0;i<3;i++){Invoke(selector,"Observe",Frame("native",1.6+i*.25,110));Invoke(selector,"Observe",Frame("mqtt",1.6+i*.25,65));}
            Assert.That(Get(Invoke(selector,"Select",2.1d,""),"Id"),Is.EqualTo("mqtt"));
            Assert.That(Get(Invoke(selector,"Select",2.1d,"native"),"Id"),Is.EqualTo("native"));
            Assert.That(Invoke(selector,"Select",2.1d,"missing"),Is.Null);
        }
        [Test] public void MqttDiscoversActualArbitrarySnapshotTopicButIgnoresRetainedAndCommands()
        {
            var matcher=Activator.CreateInstance(T("XPlaneMqttTopicMatcher"),"127.0.0.1:1883");byte[] payload=Encoding.UTF8.GetBytes(Json(Core()));
            Assert.That(Invoke(matcher,"Accept","my-lab/s76/telemetry",payload,true,1d),Is.Null);
            Assert.That(Invoke(matcher,"Accept","my-lab/commands",payload,false,1.1d),Is.Null);
            var frame=Invoke(matcher,"Accept","my-lab/s76/telemetry",payload,false,1.2d);
            Assert.That(frame,Is.Not.Null);Assert.That(Get(frame,"Topic"),Is.EqualTo("my-lab/s76/telemetry"));
            Assert.That(Get(frame,"LocalProcessVerified"),Is.EqualTo(false));
        }
        [Test] public void ScalarDatarefsFromDifferentPublishersNeverMix()
        {
            var matcher=Activator.CreateInstance(T("XPlaneMqttTopicMatcher"),"127.0.0.1:1883");int i=0;object frame=null;
            foreach(var item in Core())
            {
                string topic=(i++%2==0?"aircraft-a/":"aircraft-b/")+item.Key;
                frame=Invoke(matcher,"Accept",topic,Encoding.UTF8.GetBytes(item.Value.ToString(CultureInfo.InvariantCulture)),false,1d);
                Assert.That(frame,Is.Null);
            }
            foreach(var item in Core())frame=Invoke(matcher,"Accept","aircraft-a/"+item.Key,Encoding.UTF8.GetBytes(item.Value.ToString(CultureInfo.InvariantCulture)),false,1.1d);
            Assert.That(frame,Is.Not.Null);Assert.That(Get(frame,"Topic"),Is.EqualTo("aircraft-a/sim/#"));
        }
        [Test] public void OldScalarFieldsCannotBeRefreshedByUnrelatedTraffic()
        {
            var matcher=Activator.CreateInstance(T("XPlaneMqttTopicMatcher"),"localhost:1883");object frame=null;
            foreach(var item in Core())frame=Invoke(matcher,"Accept","cockpit/"+item.Key,Encoding.UTF8.GetBytes(item.Value.ToString(CultureInfo.InvariantCulture)),false,1d);
            Assert.That(frame,Is.Not.Null);
            Assert.That(Invoke(matcher,"Accept","cockpit/"+P+"theta",Encoding.UTF8.GetBytes("5"),false,2d),Is.Null);
        }

        [TestCase(191,104,49000)] [TestCase(31,150,8086)] [TestCase(7,91,1883)]
        public void WindowsOwnerTablePortsUseNetworkByteOrder(int high,int low,int expected)=>
            Assert.That((int)Call("XPlaneWindowsPortInventory","PortFromNetworkBytes",(byte)high,(byte)low),Is.EqualTo(expected));

        [Test] public void NativeOwnerTableQueryIsSafeOnNonWindowsHosts()
        {
            var udp=new HashSet<int>();var web=new HashSet<int>();var mqtt=new HashSet<int>();
            Call("XPlaneWindowsPortInventory","AddOwnedPorts",new HashSet<int>(),new HashSet<int>(),udp,web,mqtt);
            Assert.That(udp.Count+web.Count+mqtt.Count,Is.Zero);
        }
    }
}
