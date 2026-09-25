#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FAA.Editor
{
    /// <summary>Optional host-specific worker copied on an explicitly requested player build; never runs at scene load.</summary>
    public sealed class FaaWebcamBuildSupport : IPreprocessBuildWithReport,IPostprocessBuildWithReport
    {
        public int callbackOrder=>100;
        public void OnPreprocessBuild(BuildReport report)
        {
            if(report.summary.platform==BuildTarget.StandaloneOSX&&string.IsNullOrWhiteSpace(PlayerSettings.macOS.cameraUsageDescription))
                PlayerSettings.macOS.cameraUsageDescription="Optional laptop-camera hand gestures resize your HUD. Images are processed locally, never recorded or uploaded.";
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            string destination;
            string prefix;
            switch(report.summary.platform)
            {
                case BuildTarget.StandaloneOSX:
                    destination=Path.Combine(report.summary.outputPath,"Contents","Resources","Data","StreamingAssets","FAA","WebcamGestures","runtime");prefix="osx-";break;
                case BuildTarget.StandaloneWindows64:
                    destination=Path.Combine(Path.GetDirectoryName(report.summary.outputPath),Path.GetFileNameWithoutExtension(report.summary.outputPath)+"_Data","StreamingAssets","FAA","WebcamGestures","runtime");prefix="windows-";break;
                case BuildTarget.StandaloneLinux64:
                    destination=Path.Combine(Path.GetDirectoryName(report.summary.outputPath),Path.GetFileNameWithoutExtension(report.summary.outputPath)+"_Data","StreamingAssets","FAA","WebcamGestures","runtime");prefix="linux-";break;
                default:return;
            }
            string source=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Library","FAAWebcam","bundles");
            bool copied=false;
            if(Directory.Exists(source))foreach(string bundle in Directory.GetDirectories(source,prefix+"*"))
            {
                CopyTree(bundle,Path.Combine(destination,Path.GetFileName(bundle)));copied=true;
            }
            if(!copied)Debug.LogWarning("[FAA webcam] Player has no bundled local hand recognizer. Build it on the target OS with Tools/WebcamGestures/setup.py --bundle. Camera controls will report unavailable, not start a broken capture.");
            else Debug.Log("[FAA webcam] Local hand worker included. Sign/notarize the complete player and helper bundle before distribution.");
        }
        private static void CopyTree(string source,string destination)
        {
            if(Application.platform==RuntimePlatform.OSXEditor)
            {
                // Preserve PyInstaller framework symlinks and executable bits. Plain File.Copy
                // is not sufficient for a relocatable, signed macOS helper.
                if(source.Contains("\"")||destination.Contains("\""))throw new BuildFailedException("Unsupported quote in worker bundle path.");
                var start=new System.Diagnostics.ProcessStartInfo("/usr/bin/ditto","\""+source+"\" \""+destination+"\""){UseShellExecute=false};
                using(var process=System.Diagnostics.Process.Start(start))
                {process.WaitForExit();if(process.ExitCode!=0)throw new BuildFailedException("Could not copy webcam helper bundle.");}
                return;
            }
            if(Application.platform==RuntimePlatform.LinuxEditor)
            {
                if(source.Contains("\"")||destination.Contains("\""))throw new BuildFailedException("Unsupported quote in worker bundle path.");
                Directory.CreateDirectory(destination);
                var start=new System.Diagnostics.ProcessStartInfo("/bin/cp","-a \""+source+"/.\" \""+destination+"\""){UseShellExecute=false};
                using(var process=System.Diagnostics.Process.Start(start))
                {process.WaitForExit();if(process.ExitCode!=0)throw new BuildFailedException("Could not copy webcam helper bundle.");}
                return;
            }
            Directory.CreateDirectory(destination);
            foreach(string file in Directory.GetFiles(source))File.Copy(file,Path.Combine(destination,Path.GetFileName(file)),true);
            foreach(string folder in Directory.GetDirectories(source))CopyTree(folder,Path.Combine(destination,Path.GetFileName(folder)));
        }
    }
}
#endif
