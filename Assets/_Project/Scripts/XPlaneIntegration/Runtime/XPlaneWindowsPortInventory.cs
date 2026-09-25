using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FAA.XPlaneIntegration.Runtime
{
    /// <summary>Read-only Windows owner-PID tables. No socket scan, command line, process memory or credentials.</summary>
    public static class XPlaneWindowsPortInventory
    {
        [DllImport("iphlpapi.dll",SetLastError=true)]
        private static extern uint GetExtendedTcpTable(IntPtr table,ref int bytes,bool order,uint family,int kind,uint reserved);
        [DllImport("iphlpapi.dll",SetLastError=true)]
        private static extern uint GetExtendedUdpTable(IntPtr table,ref int bytes,bool order,uint family,int kind,uint reserved);
        public static int PortFromNetworkBytes(byte high,byte low)=>(high<<8)|low;
        public static void AddOwnedPorts(HashSet<int> simulatorPids,HashSet<int> brokerPids,HashSet<int> udp,HashSet<int> web,HashSet<int> mqtt)
        {
            if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))return;
            Read(false,simulatorPids,brokerPids,udp,web,mqtt);
            Read(true,simulatorPids,brokerPids,udp,web,mqtt);
        }
        private static void Read(bool tcp,HashSet<int> simulatorPids,HashSet<int> brokerPids,HashSet<int> udp,HashSet<int> web,HashSet<int> mqtt)
        {
            IntPtr memory=IntPtr.Zero;
            try
            {
                int size=0;uint code=tcp?GetExtendedTcpTable(IntPtr.Zero,ref size,false,2,3,0):GetExtendedUdpTable(IntPtr.Zero,ref size,false,2,1,0);
                if(code!=122||size<4||size>262144)return;
                int capacity=size;memory=Marshal.AllocHGlobal(capacity);
                code=tcp?GetExtendedTcpTable(memory,ref size,false,2,3,0):GetExtendedUdpTable(memory,ref size,false,2,1,0);
                if(code!=0||size<4||size>capacity)return;
                int count=Marshal.ReadInt32(memory),stride=tcp?24:12;
                if(count<0||count>4096||4L+(long)count*stride>size)return;
                for(int i=0;i<count;i++)
                {
                    IntPtr row=IntPtr.Add(memory,4+i*stride);
                    int pid=Marshal.ReadInt32(row,tcp?20:8),offset=tcp?8:4;
                    int port=PortFromNetworkBytes(Marshal.ReadByte(row,offset),Marshal.ReadByte(row,offset+1));
                    if(port<1024)continue;
                    if(simulatorPids.Contains(pid))
                    {
                        var target=tcp?web:udp;if(target.Count<4)target.Add(port);
                    }
                    if(tcp&&brokerPids.Contains(pid)&&mqtt.Count<4)mqtt.Add(port);
                }
            }
            catch(Exception e) when(e is DllNotFoundException||e is EntryPointNotFoundException||e is BadImageFormatException||e is System.Security.SecurityException){ }
            finally{if(memory!=IntPtr.Zero)Marshal.FreeHGlobal(memory);}
        }
    }
}
