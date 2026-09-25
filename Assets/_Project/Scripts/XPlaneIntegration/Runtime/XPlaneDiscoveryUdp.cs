using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FAA.XPlaneIntegration.Runtime
{
    public static class XPlaneDiscoveryUdp
    {
        public static byte[] Request(string dataref,int frequency,int id)
        {
            if(string.IsNullOrEmpty(dataref)||dataref.Length>399||!dataref.StartsWith("sim/",StringComparison.Ordinal)||frequency<0||frequency>30)throw new ArgumentException("Invalid RREF subscription");
            var packet=new byte[413];Encoding.ASCII.GetBytes("RREF").CopyTo(packet,0);
            BitConverter.GetBytes(frequency).CopyTo(packet,5);BitConverter.GetBytes(id).CopyTo(packet,9);Encoding.ASCII.GetBytes(dataref).CopyTo(packet,13);return packet;
        }
        public static bool ParseBeacon(byte[] packet,out int port,out int version)
        {
            port=version=0;if(packet==null||packet.Length<22||packet.Length>512||Encoding.ASCII.GetString(packet,0,5)!="BECN\0"||packet[5]!=1||packet[6]>2)return false;
            if(BitConverter.ToInt32(packet,7)!=1||BitConverter.ToInt32(packet,15)!=1)return false;
            version=BitConverter.ToInt32(packet,11);port=BitConverter.ToUInt16(packet,19);
            return version>=120000&&version<130000&&port>0;
        }
        public static Dictionary<string,double> ParseRref(byte[] packet,int firstIndex,string[] paths)
        {
            if(packet==null||packet.Length<13||packet.Length>65507||(packet.Length-5)%8!=0||Encoding.ASCII.GetString(packet,0,4)!="RREF")return null;
            var values=new Dictionary<string,double>();
            for(int offset=5;offset<packet.Length;offset+=8)
            {
                int index=BitConverter.ToInt32(packet,offset)-firstIndex;float value=BitConverter.ToSingle(packet,offset+4);
                if(index>=0&&index<paths.Length&&XPlaneDiscoveryData.Finite(value))values[paths[index]]=value;
            }
            return values.Count>0?values:null;
        }
        // X-Plane 12 DATA sets: 3 airspeeds, 4 Mach/VVI, 17 attitude, 20 position.
        // No DSEL command is sent: this only reads data the operator already enabled.
        public static Dictionary<string,double> ParseData(byte[] packet)
        {
            if(packet==null||packet.Length<41||packet.Length>65507||(packet.Length-5)%36!=0||Encoding.ASCII.GetString(packet,0,4)!="DATA")return null;
            var values=new Dictionary<string,double>();
            for(int offset=5;offset<packet.Length;offset+=36)
            {
                int set=BitConverter.ToInt32(packet,offset);var numbers=new double[8];
                for(int i=0;i<8;i++)numbers[i]=BitConverter.ToSingle(packet,offset+4+i*4);
                void Add(string key,int i,double factor=1){if(XPlaneDiscoveryData.Finite(numbers[i])&&numbers[i]!=-999)values[XPlaneDiscoveryData.Prefix+key]=numbers[i]*factor;}
                if(set==3){Add("indicated_airspeed",0);Add("true_airspeed",2,.5144444444);Add("groundspeed",3,.5144444444);}
                else if(set==4)Add("vh_ind",2,.00508);
                else if(set==17){Add("theta",0);Add("phi",1);Add("psi",2);}
                else if(set==20){Add("latitude",0);Add("longitude",1);Add("elevation",2,.3048);Add("y_agl",3,.3048);}
            }
            return values.Count>0?values:null;
        }
        public static async Task RunRref(IPEndPoint endpoint,Action<XPlaneDiscoveredFrame> publish,Action<string> diagnostic,CancellationToken token)
        {
            string id="udp-rref:"+endpoint;string label=(IPAddress.IsLoopback(endpoint.Address)?"LOCAL X-PLANE UDP ":"CONFIGURED X-PLANE UDP ")+endpoint;
            string[] paths=XPlaneDiscoveryData.Subscriptions;int baseIndex=10000+Math.Abs(Guid.NewGuid().GetHashCode()%10000000);
            while(!token.IsCancellationRequested)
            {
                try
                {
                    using(var socket=new UdpClient(new IPEndPoint(IPAddress.Any,0)))
                    {
                        socket.Client.ReceiveBufferSize=65536;socket.Client.ReceiveTimeout=250;
                        var values=new Dictionary<string,double>();var timestamps=new Dictionary<string,double>();
                        double started=XPlaneDiscoveryData.Now,lastBatch=0,lastPublished=0;bool subscribed=false;
                        try
                        {
                            while(!token.IsCancellationRequested)
                            {
                                double now=XPlaneDiscoveryData.Now;
                                if(now-lastBatch>5)
                                {
                                    for(int i=0;i<paths.Length;i++){byte[] request=Request(paths[i],10,baseIndex+i);socket.Send(request,request.Length,endpoint);}
                                    lastBatch=now;subscribed=true;
                                }
                                if(socket.Available==0){await Task.Delay(15,token);if(now-started>3&&values.Count==0)break;continue;}
                                IPEndPoint sender=new IPEndPoint(IPAddress.Any,0);byte[] packet=socket.Receive(ref sender);
                                if(!sender.Equals(endpoint))continue;
                                var received=ParseRref(packet,baseIndex,paths);if(received==null)continue;
                                foreach(var pair in received){values[pair.Key]=pair.Value;timestamps[pair.Key]=now;}
                                if(now-lastPublished<.05)continue;
                                var fresh=values.Where(p=>now-timestamps[p.Key]<.75).ToDictionary(p=>p.Key,p=>p.Value);
                                if(!XPlaneDiscoveryData.CoreValid(fresh)||!fresh.TryGetValue(XPlaneDiscoveryData.Version,out double version)||version<120000||version>=130000||!fresh.TryGetValue(XPlaneDiscoveryData.Clock,out double clock))continue;
                                bool paused=fresh.TryGetValue("sim/time/paused",out double pause)&&pause==1;
                                string signature=paused?"paused:"+now.ToString("R",CultureInfo.InvariantCulture):"clock:"+clock.ToString("R",CultureInfo.InvariantCulture);
                                publish(new XPlaneDiscoveredFrame{Id=id,Label=label+(paused?" [PAUSED]":""),Transport="UDP RREF",Priority=IPAddress.IsLoopback(endpoint.Address)?100:80,Received=now,Values=fresh,Signature=signature,LocalProcessVerified=true});lastPublished=now;
                            }
                        }
                        finally
                        {
                            // Frequency zero removes ONLY this client's indexed read subscriptions.
                            if(subscribed)for(int i=0;i<paths.Length;i++)try{byte[] stop=Request(paths[i],0,baseIndex+i);socket.Send(stop,stop.Length,endpoint);}catch(SocketException){break;}catch(ObjectDisposedException){break;}
                        }
                    }
                }
                catch(Exception e) when(e is SocketException||e is ObjectDisposedException||e is OperationCanceledException){if(token.IsCancellationRequested)break;diagnostic("UDP unavailable at "+endpoint);}
                try{await Task.Delay(10000,token);}catch(OperationCanceledException){break;}
            }
        }
        public static async Task RunData(int port,HashSet<string> localAddresses,Action<XPlaneDiscoveredFrame> publish,Action<string> diagnostic,CancellationToken token)
        {
            while(!token.IsCancellationRequested)
            {
                try
                {
                    using(var socket=new UdpClient(new IPEndPoint(IPAddress.Any,port)))
                    using(token.Register(()=>socket.Close()))
                    {
                        socket.Client.ReceiveBufferSize=65536;
                        var sources=new Dictionary<string,Dictionary<string,(double value,double at)>>();
                        while(!token.IsCancellationRequested)
                        {
                            var packet=await socket.ReceiveAsync();if(!localAddresses.Contains(packet.RemoteEndPoint.Address.ToString()))continue;
                            var data=ParseData(packet.Buffer);if(data==null)continue;
                            string sender=packet.RemoteEndPoint.ToString();
                            if(!sources.TryGetValue(sender,out var fields)){if(sources.Count>=4)continue;fields=new();sources[sender]=fields;}
                            double now=XPlaneDiscoveryData.Now;foreach(var item in data)fields[item.Key]=(item.Value,now);
                            var fresh=fields.Where(p=>now-p.Value.at<.5).ToDictionary(p=>p.Key,p=>p.Value.value);
                            if(XPlaneDiscoveryData.CoreValid(fresh))publish(new XPlaneDiscoveredFrame{Id="udp-data:"+sender+":"+port,Label="LOCAL UDP DATA "+sender,Transport="UDP DATA",Priority=75,Received=now,Values=fresh});
                        }
                    }
                }
                catch(Exception e) when(e is SocketException||e is ObjectDisposedException||e is OperationCanceledException){if(token.IsCancellationRequested)break;diagnostic("UDP output port busy/unavailable: "+port);}
                try{await Task.Delay(15000,token);}catch(OperationCanceledException){break;}
            }
        }
    }
}
