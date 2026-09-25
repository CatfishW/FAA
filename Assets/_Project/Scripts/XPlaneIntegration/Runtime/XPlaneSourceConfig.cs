using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace FAA.XPlaneIntegration.Runtime
{
    [Serializable]
    public sealed class XPlaneMqttDiscoveryConfig
    {
        public string host="127.0.0.1";
        public int port=1883;
        public bool tls;
        public string topicFilter="#";
        // Only environment-variable NAMES belong in redistributable configuration.
        public string usernameEnvironment="";
        public string passwordEnvironment="";
    }
    [Serializable]
    public sealed class XPlaneSourceConfig
    {
        public int version=1;
        public bool enabled=true;
        public bool allowFallback=true;
        public bool discoverLocalUdp=true;
        public bool discoverLocalWeb=true;
        public bool observeLocalBeacons=true;
        public string fallbackUrl="http://127.0.0.1:12678";
        public string localTerrainUrl="http://127.0.0.1:8767";
        public string[] udpEndpoints=Array.Empty<string>();
        public int[] udpListenPorts=Array.Empty<int>();
        public string[] webEndpoints=Array.Empty<string>();
        public XPlaneMqttDiscoveryConfig[] mqttBrokers={new XPlaneMqttDiscoveryConfig()};
        public string[] installPaths=Array.Empty<string>();
        public string preferredSource="";
        public double startupGraceSeconds=6;
        public double sourceLossSeconds=3;

        public static XPlaneSourceConfig Load(string shippedPath,string overridePath,out string error)
        {
            error="";
            try
            {
                string path=File.Exists(overridePath)?overridePath:shippedPath;
                if(!File.Exists(path))return new XPlaneSourceConfig();
                if(new FileInfo(path).Length>32768)throw new InvalidDataException();
                var config=JsonConvert.DeserializeObject<XPlaneSourceConfig>(File.ReadAllText(path),new JsonSerializerSettings{MaxDepth=8});
                if(!Validate(config,out error))return null;
                return config;
            }
            catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is JsonException)
            {error="Invalid or unreadable DataSources.json; discovery did not start.";return null;}
        }
        public static bool Validate(XPlaneSourceConfig c,out string error)
        {
            error="Invalid source discovery configuration.";
            if(c==null||c.version!=1||!XPlaneDiscoveryData.Finite(c.startupGraceSeconds)||!XPlaneDiscoveryData.Finite(c.sourceLossSeconds)||c.startupGraceSeconds<1||c.startupGraceSeconds>30||c.sourceLossSeconds<1||c.sourceLossSeconds>10)return false;
            if(!XPlaneTerrainConnection.TryNormalize(c.fallbackUrl,out _)||!XPlaneTerrainConnection.TryNormalize(c.localTerrainUrl,out _))return false;
            if(c.udpEndpoints==null||c.udpEndpoints.Length>4||c.webEndpoints==null||c.webEndpoints.Length>4||c.mqttBrokers==null||c.mqttBrokers.Length>4||c.installPaths==null||c.installPaths.Length>8||c.udpListenPorts==null||c.udpListenPorts.Length>4)return false;
            foreach(string endpoint in c.udpEndpoints)if(!TryUdpEndpoint(endpoint,out _))return false;
            foreach(string endpoint in c.webEndpoints)if(!XPlaneTerrainConnection.TryNormalize(endpoint,out _))return false;
            foreach(int port in c.udpListenPorts)if(port<1024||port>65535)return false;
            foreach(var broker in c.mqttBrokers)
            {
                if(broker==null||string.IsNullOrWhiteSpace(broker.host)||broker.host.Length>253||broker.host.IndexOfAny(new[]{'/',':','@','\r','\n'})>=0||broker.port<1||broker.port>65535||string.IsNullOrWhiteSpace(broker.topicFilter)||broker.topicFilter.Length>256)return false;
                if(!TopicFilterValid(broker.topicFilter))return false;
                if(!EnvironmentName(broker.usernameEnvironment)||!EnvironmentName(broker.passwordEnvironment))return false;
                // Never send broker credentials in cleartext to a non-loopback endpoint.
                if(!broker.tls&&!IsLoopback(broker.host)&&(!string.IsNullOrEmpty(broker.usernameEnvironment)||!string.IsNullOrEmpty(broker.passwordEnvironment)))return false;
            }
            if(c.preferredSource==null||c.preferredSource.Length>512||c.installPaths.Any(p=>string.IsNullOrWhiteSpace(p)||p.Length>1024))return false;
            error="";return true;
        }
        public static bool IsLoopback(string host)=>string.Equals(host,"localhost",StringComparison.OrdinalIgnoreCase)||
            System.Net.IPAddress.TryParse(host,out var ip)&&System.Net.IPAddress.IsLoopback(ip);
        public static bool TryUdpEndpoint(string value,out System.Net.IPEndPoint endpoint)
        {
            endpoint=null;if(string.IsNullOrEmpty(value)||value.Length>80)return false;
            int split=value.LastIndexOf(':');if(split<1||!int.TryParse(value.Substring(split+1),out int port)||port<1||port>65535||!System.Net.IPAddress.TryParse(value.Substring(0,split),out var ip)||ip.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork||ip.Equals(System.Net.IPAddress.Any)||ip.Equals(System.Net.IPAddress.Broadcast))return false;
            endpoint=new System.Net.IPEndPoint(ip,port);return true;
        }
        private static bool EnvironmentName(string value)=>value!=null&&value.Length<=128&&value.All(c=>char.IsLetterOrDigit(c)||c=='_');
        public static bool TopicFilterValid(string filter)
        {
            if(string.IsNullOrEmpty(filter)||filter.Contains("\0"))return false;
            string[] parts=filter.Split('/');
            for(int i=0;i<parts.Length;i++)
                if(parts[i].Contains("#")&&(parts[i]!="#"||i!=parts.Length-1)||parts[i].Contains("+")&&parts[i]!="+")return false;
            return true;
        }
    }
}
