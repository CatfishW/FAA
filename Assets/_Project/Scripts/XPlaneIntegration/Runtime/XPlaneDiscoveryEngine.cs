using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FAA.XPlaneIntegration.Runtime
{
    public sealed class XPlaneDiscoveryEngine:IDisposable
    {
        private readonly CancellationTokenSource cancel=new();
        private readonly Dictionary<string,XPlaneDiscoveredFrame> pending=new(StringComparer.Ordinal);
        private readonly HashSet<string> started=new(StringComparer.Ordinal);
        private readonly object sync=new();
        private readonly XPlaneSourceConfig config;
        private readonly HashSet<string> localAddresses=new(){"127.0.0.1","::1"};
        public string InventoryStatus{get;private set;}="Checking local simulator...";
        public string LastDiagnostic{get;private set;}="";
        public XPlaneLocalInventory Inventory{get;private set;}
        private bool disposed;
        public XPlaneDiscoveryEngine(XPlaneSourceConfig configuration,string home,string localAppData)
        {
            config=configuration;
            try{foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces())foreach(var address in adapter.GetIPProperties().UnicastAddresses)localAddresses.Add(address.Address.ToString());}catch(NetworkInformationException){ }
            _=Task.Run(async()=>
            {
                while(!cancel.IsCancellationRequested)
                {
                    var inventory=XPlaneLocalInventory.Scan(home,localAppData,config.installPaths);Inventory=inventory;InventoryStatus=inventory.Summary;
                    if(config.discoverLocalUdp)foreach(int port in inventory.UdpPorts)StartUdp(new IPEndPoint(IPAddress.Loopback,port));
                    if(config.discoverLocalWeb)foreach(int port in inventory.WebPorts)StartWeb("http://127.0.0.1:"+port);
                    if(config.mqttBrokers.Any(b=>XPlaneSourceConfig.IsLoopback(b.host)))foreach(int port in inventory.MqttPorts)
                    {
                        var local=new XPlaneMqttDiscoveryConfig{host="127.0.0.1",port=port};
                        Launch("mqtt:127.0.0.1:"+port,()=>XPlaneDiscoveryMqtt.Run(local,Publish,Diagnostic,cancel.Token));
                    }
                    foreach(int port in inventory.OutputPorts.Concat(config.udpListenPorts).Distinct())
                        Launch("data:"+port,()=>XPlaneDiscoveryUdp.RunData(port,localAddresses,Publish,Diagnostic,cancel.Token));
                    try{await Task.Delay(30000,cancel.Token);}catch(OperationCanceledException){break;}
                }
            });
            foreach(string target in config.udpEndpoints)if(XPlaneSourceConfig.TryUdpEndpoint(target,out var endpoint))StartUdp(endpoint);
            foreach(string endpoint in config.webEndpoints)StartWeb(endpoint.TrimEnd('/'));
            foreach(var broker in config.mqttBrokers)Launch("mqtt:"+broker.host+":"+broker.port,()=>XPlaneDiscoveryMqtt.Run(broker,Publish,Diagnostic,cancel.Token));
            if(config.observeLocalBeacons)Launch("beacon",ObserveBeacons);
        }
        private void Launch(string key,Func<Task> run)
        {
            lock(sync){if(disposed||started.Count>=24||!started.Add(key))return;}
            _=Task.Run(async()=>{try{await run();}catch(Exception e) when(!(e is OutOfMemoryException)){if(!cancel.IsCancellationRequested)Diagnostic("Discovery worker stopped: "+e.GetType().Name);}});
        }
        private void StartUdp(IPEndPoint endpoint)=>Launch("rref:"+endpoint,()=>XPlaneDiscoveryUdp.RunRref(endpoint,Publish,Diagnostic,cancel.Token));
        private void StartWeb(string endpoint)=>Launch("web:"+endpoint,()=>XPlaneDiscoveryWeb.Run(endpoint,Publish,Diagnostic,cancel.Token));
        private void Publish(XPlaneDiscoveredFrame frame)
        {lock(sync){if(!disposed&&(pending.Count<32||pending.ContainsKey(frame.Id)))pending[frame.Id]=frame;}}
        private void Diagnostic(string message){LastDiagnostic=XPlaneDiscoveryData.SafeLabel(message,180);}
        public XPlaneDiscoveredFrame[] Drain()
        {lock(sync){var frames=pending.Values.ToArray();pending.Clear();return frames;}}
        private async Task ObserveBeacons()
        {
            while(!cancel.IsCancellationRequested)
            {
                try
                {
                    using(var socket=new UdpClient())
                    using(cancel.Token.Register(()=>socket.Close()))
                    {
                        socket.ExclusiveAddressUse=false;socket.Client.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReuseAddress,true);
                        socket.Client.Bind(new IPEndPoint(IPAddress.Any,49707));socket.JoinMulticastGroup(IPAddress.Parse("239.255.1.1"));
                        while(!cancel.IsCancellationRequested)
                        {
                            var packet=await socket.ReceiveAsync();
                            // Multicast reception is passive. Unknown LAN hosts are never selected automatically.
                            if(localAddresses.Contains(packet.RemoteEndPoint.Address.ToString())&&XPlaneDiscoveryUdp.ParseBeacon(packet.Buffer,out int port,out _))
                                StartUdp(new IPEndPoint(IPAddress.Loopback,port));
                        }
                    }
                }
                catch(Exception e) when(e is SocketException||e is ObjectDisposedException||e is OperationCanceledException)
                {if(cancel.IsCancellationRequested)break;Diagnostic("Local beacon listener unavailable; direct loopback probes continue.");}
                try{await Task.Delay(15000,cancel.Token);}catch(OperationCanceledException){break;}
            }
        }
        public void Dispose()
        {
            lock(sync){if(disposed)return;disposed=true;pending.Clear();}
            cancel.Cancel();
            // Workers own disposal of sockets/clients; do not block the Unity render thread.
        }
    }
}
