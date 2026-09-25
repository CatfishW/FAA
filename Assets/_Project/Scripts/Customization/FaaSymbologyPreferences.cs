using System;
using System.Collections.Generic;
using UnityEngine;
namespace FAA.Customization
{
    public enum FaaSymbologyVersion { Digital=0,ClassicAnalog=1 }
    [Serializable] public sealed class FaaSymbologyScale {public string id;public float scale=1;}
    [Serializable] public sealed class FaaSymbologyPreferences
    {
        public int schema=1;
        public FaaSymbologyVersion selected=FaaSymbologyVersion.Digital;
        public bool localAttitude=true,reducedMotion,pilotColor;
        public List<FaaSymbologyScale> digital=new(),classic=new();
        public static bool TryParse(string json,out FaaSymbologyPreferences profile)
        {
            profile=null;if(string.IsNullOrWhiteSpace(json)||json.Length>32768)return false;
            try
            {
                var p=JsonUtility.FromJson<FaaSymbologyPreferences>(json);
                if(p==null||p.schema!=1||!Enum.IsDefined(typeof(FaaSymbologyVersion),p.selected)||!Validate(p.digital)||!Validate(p.classic))return false;
                profile=p;return true;
            }
            catch(ArgumentException){return false;}
        }
        private static bool Validate(List<FaaSymbologyScale> list)
        {
            if(list==null||list.Count>32)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var v in list)
            {
                if(v==null||string.IsNullOrWhiteSpace(v.id)||v.id.Length>64||!ids.Add(v.id)||!FaaAnalogFlightSample.Finite(v.scale))return false;
                v.scale=FaaSpatialLayoutMath.Scale(v.scale);
            }
            return true;
        }
    }
}
