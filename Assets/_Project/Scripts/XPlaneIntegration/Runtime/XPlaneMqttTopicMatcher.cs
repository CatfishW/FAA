using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FAA.XPlaneIntegration.Runtime
{
    /// <summary>Observe actual topic/payloads, infer only declared units/canonical datarefs, isolate publisher prefixes.</summary>
    public sealed class XPlaneMqttTopicMatcher
    {
        private readonly string broker;
        private readonly Dictionary<string,Dictionary<string,(double value,double at)>> groups=new(StringComparer.Ordinal);
        private readonly HashSet<string> filters=new(StringComparer.Ordinal);
        private double window;private int count,bytes;
        public bool Overloaded{get;private set;}
        public XPlaneMqttTopicMatcher(string endpoint){broker=endpoint;}
        public string[] Filters=>filters.Take(16).ToArray();
        public XPlaneDiscoveredFrame Accept(string topic,byte[] payload,bool retained,double now)
        {
            if(retained||string.IsNullOrEmpty(topic)||topic.Length>512||payload==null||payload.Length==0||payload.Length>131072||topic.StartsWith("$",StringComparison.Ordinal))return null;
            if(now-window>=1){window=now;count=bytes=0;Overloaded=false;}
            count++;bytes+=payload.Length;if(count>2000||bytes>2097152){Overloaded=true;return null;}
            string[] components=topic.Split('/');
            if(components.Any(p=>p.Equals("command",StringComparison.OrdinalIgnoreCase)||p.Equals("commands",StringComparison.OrdinalIgnoreCase)||p.Equals("set",StringComparison.OrdinalIgnoreCase)||p.Equals("request",StringComparison.OrdinalIgnoreCase)))return null;
            string text;
            try{text=new UTF8Encoding(false,true).GetString(payload);}catch(DecoderFallbackException){return null;}
            if(XPlaneDiscoveryData.DecodeJson(text,out var values,out string signature))
            {
                if(filters.Count<16)filters.Add(topic);
                return Frame(topic,values,signature,now);
            }
            int sim=topic.IndexOf("sim/",StringComparison.Ordinal);if(sim<0||sim>0&&topic[sim-1]!='/')return null;
            string prefix=topic.Substring(0,sim),path=topic.Substring(sim);
            if(path.Length>240||!XPlaneDiscoveryData.Subscriptions.Contains(path))return null;
            if(!double.TryParse(text.Trim().Trim('"'),NumberStyles.Float,CultureInfo.InvariantCulture,out double scalar)||!XPlaneDiscoveryData.Finite(scalar))return null;
            if(!groups.TryGetValue(prefix,out var fields))
            {if(groups.Count>=16)return null;fields=new Dictionary<string,(double,double)>();groups[prefix]=fields;}
            fields[path]=(scalar,now);
            var fresh=fields.Where(p=>now-p.Value.at<.75).ToDictionary(p=>p.Key,p=>p.Value.value,StringComparer.Ordinal);
            if(!XPlaneDiscoveryData.CoreValid(fresh))return null;
            string filter=prefix+"sim/#";if(filters.Count<16)filters.Add(filter);
            if(fresh.TryGetValue(XPlaneDiscoveryData.Clock,out double clock))signature="clock:"+clock.ToString("R",CultureInfo.InvariantCulture);
            // A complete JSON snapshot OR one coherent scalar prefix is the selection unit.
            return Frame(filter,fresh,signature,now);
        }
        private XPlaneDiscoveredFrame Frame(string topic,Dictionary<string,double> values,string signature,double now)=>new()
        {
            Id="mqtt:"+broker+":"+topic,Label="MQTT "+broker+" / "+XPlaneDiscoveryData.SafeLabel(topic,80),Transport="MQTT",Topic=topic,
            Priority=65,Received=now,Values=values,Signature=signature,LocalProcessVerified=false
        };
    }
}
