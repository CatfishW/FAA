using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FAA.XPlaneIntegration.Runtime
{
    /// <summary>Bounded installation/process metadata; no global filesystem search or credential collection.</summary>
    public sealed class XPlaneLocalInventory
    {
        public int RunningProcesses;
        public readonly List<string> InstallPaths=new();
        public readonly HashSet<int> UdpPorts=new(){49000};
        public readonly HashSet<int> WebPorts=new(){8086};
        public readonly HashSet<int> OutputPorts=new();
        public readonly HashSet<int> MqttPorts=new();
        public string Summary=>RunningProcesses+" X-Plane process(es), "+InstallPaths.Count+" validated installation(s)";
        public static XPlaneLocalInventory Scan(string home,string localAppData,string[] configured)
        {
            var result=new XPlaneLocalInventory();var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var simulatorPids=new HashSet<int>();var brokerPids=new HashSet<int>();
            foreach(string name in new[]{"X-Plane","X-Plane-x86_64"})
            {
                Process[] processes;
                try{processes=Process.GetProcessesByName(name);}catch(Exception){continue;}
                foreach(var process in processes.Take(8))using(process)
                {
                    result.RunningProcesses++;
                    simulatorPids.Add(process.Id);
                    try
                    {
                        string executable=process.MainModule?.FileName;
                        if(string.IsNullOrEmpty(executable))continue;
                        var dir=new DirectoryInfo(Path.GetDirectoryName(executable));
                        if(dir.Name=="MacOS"&&dir.Parent?.Name=="Contents")dir=dir.Parent.Parent?.Parent;
                        if(dir!=null)paths.Add(dir.FullName);
                    }
                    catch(Exception e) when(e is System.ComponentModel.Win32Exception||e is InvalidOperationException||e is UnauthorizedAccessException||e is NotSupportedException){ }
                }
            }
            foreach(string name in new[]{"mosquitto","emqx","nanomq"})
            {
                try{foreach(var process in Process.GetProcessesByName(name).Take(8))using(process)brokerPids.Add(process.Id);}
                catch(Exception e) when(e is InvalidOperationException||e is System.ComponentModel.Win32Exception||e is NotSupportedException){ }
            }
            XPlaneWindowsPortInventory.AddOwnedPorts(simulatorPids,brokerPids,result.UdpPorts,result.WebPorts,result.MqttPorts);
            foreach(string path in configured??Array.Empty<string>())paths.Add(path);
            foreach(string list in new[]{Path.Combine(localAppData,"x-plane_install_12.txt"),Path.Combine(home,"Library","Preferences","x-plane_install_12.txt"),Path.Combine(home,".x-plane","x-plane_install_12.txt")})
            {
                try
                {
                    if(File.Exists(list)&&new FileInfo(list).Length<16384)
                        foreach(string line in File.ReadLines(list).Take(16))if(!string.IsNullOrWhiteSpace(line)&&line.Length<1024)paths.Add(line.Trim());
                }
                catch(Exception e) when(e is IOException||e is UnauthorizedAccessException){ }
            }
            foreach(string path in paths.Take(16))
            {
                try
                {
                    if(!Directory.Exists(Path.Combine(path,"Resources"))||!Directory.Exists(Path.Combine(path,"Output")))continue;
                    if(!File.Exists(Path.Combine(path,"X-Plane.exe"))&&!File.Exists(Path.Combine(path,"X-Plane-x86_64"))&&!Directory.Exists(Path.Combine(path,"X-Plane.app")))continue;
                    result.InstallPaths.Add(Path.GetFullPath(path));
                    foreach(string name in new[]{"X-Plane.prf","X-Plane Network Settings.prf"})
                    {
                        string file=Path.Combine(path,"Output","preferences",name);
                        if(!File.Exists(file)||new FileInfo(file).Length>262144)continue;
                        foreach(string line in File.ReadLines(file).Take(6000))
                        {
                            var match=Regex.Match(line,@"^\s*(?:_?port_udp|_?udp_port)\s+(\d{2,5})\s*$");
                            if(match.Success&&int.TryParse(match.Groups[1].Value,out int port)&&port>=1024&&port<=65535&&result.UdpPorts.Count<4)result.UdpPorts.Add(port);
                            match=Regex.Match(line,@"^\s*(?:_?port_web_server|_?web_server_port)\s+(\d{2,5})\s*$");
                            if(match.Success&&int.TryParse(match.Groups[1].Value,out port)&&port>=1024&&port<=65535&&result.WebPorts.Count<4)result.WebPorts.Add(port);
                            match=Regex.Match(line,@"^\s*_?udp_DATA_port\s+(\d{2,5})\s*$");
                            if(match.Success&&int.TryParse(match.Groups[1].Value,out port)&&port>=1024&&port<=65535&&port!=49000&&result.OutputPorts.Count<4)result.OutputPorts.Add(port);
                        }
                    }
                }
                catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException){ }
            }
            return result;
        }
    }
}
