using System;
using System.Collections.Generic;
using AviationUI;
using FAA.XPlaneIntegration.Runtime;
using UnityEngine;

namespace FAA.Customization
{
    /// <summary>Read-only adapter. A missing dataref is never a valid zero. No commands or subscriptions.</summary>
    public struct FaaAnalogFlightSample
    {
        public bool Fresh, AttitudeValid, SpeedValid, AltitudeValid, VerticalSpeedValid, TorqueValid, RpmValid, SlipValid, LocValid, GsValid;
        public float Pitch, Roll, Heading, Speed, Altitude, VerticalSpeed, Torque, Rpm, Slip, Localizer, Glideslope;
        public bool Engine1Valid, Engine2Valid;
        public float Engine1Torque, Engine2Torque;
        public string RollMode, PitchMode, RollArmed, PitchArmed, Coupling;

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool Try(IDictionary<string,float> source, string key, out float value)
        { value=0;return source!=null && source.TryGetValue(key,out value) && Finite(value); }
        private static bool First(IDictionary<string,float> source,out float value,params string[] keys)
        {foreach(string key in keys)if(Try(source,key,out value))return true;value=0;return false;}

        public static FaaAnalogFlightSample Capture(XPlane12ApiHudBridge bridge)
        {
            if(bridge==null)return default;
            return From(bridge.LatestRawFlightData??bridge.LatestFlightData,bridge.LatestSnapshot?.Aircraft,
                bridge.LatestSnapshot?.Systems,FaaRotorcraftCueMath.Fresh(bridge.IsFeedHealthy,bridge.LastPacketAgeSeconds,1f));
        }
        public static FaaAnalogFlightSample From(AviationFlightData data,IDictionary<string,float> aircraft,IDictionary<string,float> systems,bool fresh)
        {
            var s=new FaaAnalogFlightSample {Fresh=fresh,RollMode="--",PitchMode="--",RollArmed="",PitchArmed="",Coupling="AP --"};
            if(data==null||!fresh)return s;
            s.AttitudeValid=data.attitudeValid && Finite(data.pitch) && Mathf.Abs(data.pitch)<=90 && Finite(data.roll) && Finite(data.heading);
            s.Pitch=data.pitch;s.Roll=data.roll;s.Heading=data.heading;
            s.SpeedValid=Try(aircraft,"sim/flightmodel/position/indicated_airspeed",out float speed)&&speed>=0&&Finite(data.indicatedAirspeed);
            s.Speed=data.indicatedAirspeed;
            s.AltitudeValid=Try(aircraft,"sim/flightmodel/position/elevation",out _)&&Finite(data.altitudeMSL);
            s.Altitude=data.altitudeMSL;
            s.VerticalSpeedValid=Try(aircraft,"sim/flightmodel/position/vh_ind",out _)&&Finite(data.verticalSpeed);
            s.VerticalSpeed=data.verticalSpeed;
            s.SlipValid=Try(aircraft,"sim/flightmodel/forces/g_side",out float slip);s.Slip=Mathf.Clamp(slip,-1,1);
            s.Engine1Valid=data.engine1TorqueValid&&Finite(data.engine1Torque)&&data.engine1Torque>=0;
            s.Engine2Valid=data.engine2TorqueValid&&Finite(data.engine2Torque)&&data.engine2Torque>=0;
            s.Engine1Torque=data.engine1Torque;s.Engine2Torque=data.engine2Torque;
            s.TorqueValid=s.Engine1Valid&&(data.engineCount<2||s.Engine2Valid);
            s.Torque=data.engineCount>=2?Mathf.Max(data.engine1Torque,data.engine2Torque):data.engine1Torque;
            s.RpmValid=data.rotorNRValid&&Finite(data.rotorNR)&&data.rotorNR>=0;s.Rpm=data.rotorNR;
            s.LocValid=data.ilsValid&&First(systems,out s.Localizer,
                "sim/cockpit2/radios/indicators/hsi_hdef_dots_pilot","sim/cockpit2/radios/indicators/nav1_hdef_dots_pilot",
                "sim/cockpit2/radios/indicators/nav2_hdef_dots_pilot","sim/cockpit2/radios/indicators/gps_hdef_dots_pilot",
                "sim/cockpit/radios/nav1_hdef_dot","sim/cockpit/radios/nav2_hdef_dot","sim/cockpit/radios/gps_hdef_dot");
            s.GsValid=data.ilsValid&&First(systems,out s.Glideslope,
                "sim/cockpit2/radios/indicators/hsi_vdef_dots_pilot","sim/cockpit2/radios/indicators/nav1_vdef_dots_pilot",
                "sim/cockpit2/radios/indicators/nav2_vdef_dots_pilot","sim/cockpit/radios/nav1_vdef_dot","sim/cockpit/radios/nav2_vdef_dot","sim/cockpit/radios/gps_vdef_dot");
            if(Try(systems,"sim/cockpit/autopilot/autopilot_state",out float raw)&&raw>=0&&raw<16777216)
                DecodeModes((int)raw,out s.RollMode,out s.PitchMode,out s.RollArmed,out s.PitchArmed);
            if(First(systems,out float mode,"sim/cockpit2/autopilot/flight_director_mode","sim/cockpit/autopilot/autopilot_mode"))
                s.Coupling=mode>=2?"CPL":mode>=1?"FD":"AP OFF";
            return s;
        }
        // X-Plane documented autopilot_state bit field (read only). Collective modes are NOT inferred from pitch/throttle modes.
        public static void DecodeModes(int bits,out string roll,out string pitch,out string rollArmed,out string pitchArmed)
        {
            roll=(bits&524288)!=0?"GPSS":(bits&4194304)!=0?"TRK":(bits&1048576)!=0?"HDG HLD":(bits&512)!=0?"NAV":(bits&2)!=0?"HDG":(bits&4)!=0?"ROLL":(bits&32768)!=0?"TO/GA":"--";
            pitch=(bits&2048)!=0?"G/S":(bits&16384)!=0?"ALT":(bits&16)!=0?"VS":(bits&8388608)!=0?"FPA":(bits&262144)!=0?"VNAV":(bits&64)!=0?"FLC":(bits&8)!=0?"IAS":(bits&128)!=0?"PITCH":(bits&65536)!=0?"TO/GA":"--";
            rollArmed=(bits&256)!=0?"NAV ARM":"";
            pitchArmed=(bits&1024)!=0?"G/S ARM":(bits&32)!=0?"ALT ARM":(bits&131072)!=0?"VNAV ARM":"";
        }
    }

    public sealed class FaaAnalogAnimation
    {
        private bool initialized;
        private FaaAnalogFlightSample previous;
        public FaaAnalogFlightSample Display {get;private set;}
        public static float Damp(float current,float target,float dt,float response=14f)
        {
            if(!FaaAnalogFlightSample.Finite(target))return current;
            if(!FaaAnalogFlightSample.Finite(current))return target;
            if(!FaaAnalogFlightSample.Finite(dt)||dt<=0)return current;
            return Mathf.LerpUnclamped(current,target,1f-Mathf.Exp(-Mathf.Max(.01f,response)*Mathf.Min(dt,.5f)));
        }
        public static float DampAngle(float current,float target,float dt) => current+Damp(0,Mathf.DeltaAngle(current,target),dt);
        public void Reset()=>initialized=false;
        public void Step(FaaAnalogFlightSample target,float dt,bool reducedMotion=false)
        {
            var d=target;
            if(!initialized||!target.Fresh||!previous.Fresh||reducedMotion)
            { Display=target;previous=target;initialized=true;return; }
            var old=Display;
            if(target.AttitudeValid&&previous.AttitudeValid)
            {d.Pitch=Damp(old.Pitch,target.Pitch,dt,20);d.Roll=DampAngle(old.Roll,target.Roll,dt);d.Heading=DampAngle(old.Heading,target.Heading,dt);}
            if(target.SpeedValid&&previous.SpeedValid)d.Speed=Mathf.Abs(target.Speed-old.Speed)>80?target.Speed:Damp(old.Speed,target.Speed,dt);
            if(target.AltitudeValid&&previous.AltitudeValid)d.Altitude=Mathf.Abs(target.Altitude-old.Altitude)>2000?target.Altitude:Damp(old.Altitude,target.Altitude,dt);
            if(target.VerticalSpeedValid&&previous.VerticalSpeedValid)d.VerticalSpeed=Damp(old.VerticalSpeed,target.VerticalSpeed,dt);
            if(target.TorqueValid&&previous.TorqueValid)d.Torque=Damp(old.Torque,target.Torque,dt);
            if(target.RpmValid&&previous.RpmValid)d.Rpm=Damp(old.Rpm,target.Rpm,dt);
            if(target.LocValid&&previous.LocValid)d.Localizer=Damp(old.Localizer,target.Localizer,dt);
            if(target.GsValid&&previous.GsValid)d.Glideslope=Damp(old.Glideslope,target.Glideslope,dt);
            if(target.SlipValid&&previous.SlipValid)d.Slip=Damp(old.Slip,target.Slip,dt);
            Display=d;previous=target;
        }
        public static float SpeedAngle(float knots)=>Mathf.Clamp(knots,0,239.9f)*1.5f;
        public static float AltitudeAngle(float feet)=>Mathf.Repeat(feet,1000f)*.36f;
        public static float PercentAngle(float value,float maximum)=>-135f+270f*Mathf.Clamp01(value/maximum);
    }
}
