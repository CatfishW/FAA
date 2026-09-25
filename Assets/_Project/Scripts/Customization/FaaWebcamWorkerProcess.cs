using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FAA.Customization
{
    /// <summary>One owned local child, bounded anonymous pipes, no HTTP endpoint or image files.</summary>
    public sealed class FaaWebcamWorkerProcess : IDisposable
    {
        private readonly ConcurrentQueue<string> messages = new();
        private Process process;
        private int writing, disposed, queued;
        public string Failure { get; private set; }
        public bool Writing => Volatile.Read(ref writing) != 0;
        public bool Running
        {
            get { try { return process != null && !process.HasExited; } catch (InvalidOperationException) { return false; } }
        }
        private static string Quote(string path)
        {
            if (path.IndexOfAny(new[] {'"','\r','\n'}) >= 0) throw new ArgumentException("Invalid local worker path.");
            return "\""+path+"\"";
        }
        public void Start(string executable,string script,string model)
        {
            if (process != null || disposed != 0) throw new InvalidOperationException("Worker already started/disposed.");
            if (!File.Exists(executable) || !File.Exists(model) || script != null && !File.Exists(script))
                throw new FileNotFoundException("Run Tools/WebcamGestures/setup.py first.");
            var start = new ProcessStartInfo
            {
                FileName=executable, Arguments=(script!=null?"-u "+Quote(script)+" ":"")+"--model "+Quote(model),
                UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,
                RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(model)
            };
            start.EnvironmentVariables["MPLBACKEND"]="Agg";
            start.EnvironmentVariables["GLOG_minloglevel"]="2";
            start.EnvironmentVariables["TF_CPP_MIN_LOG_LEVEL"]="3";
            process=Process.Start(start);
            Process owned=process;
            // Capture readers before queuing: stop/dispose can occur before a Task starts.
            var output=owned.StandardOutput;var error=owned.StandardError;
            Task.Run(()=>Read(output,true));
            Task.Run(()=>Read(error,false));
        }
        private void Read(StreamReader input,bool output)
        {
            try
            {
                var text=new StringBuilder(1024);
                while(Volatile.Read(ref disposed)==0)
                {
                    int ch=input.Read(); if(ch<0)break;
                    if(ch=='\n')
                    {
                        if(output && text.Length>0)
                        {
                            if(Interlocked.Increment(ref queued)>8) { Failure="Worker output overflow.";break; }
                            messages.Enqueue(text.ToString());
                        }
                        text.Clear();
                    }
                    else if(ch!='\r')
                    {
                        if(text.Length>=16384) { Failure="Worker response too large.";break; }
                        text.Append((char)ch);
                    }
                }
            }
            catch(Exception ex) when(ex is IOException || ex is ObjectDisposedException || ex is InvalidOperationException)
            { if(Volatile.Read(ref disposed)==0)Failure="Worker connection closed."; }
        }
        public bool TryRead(out string line)
        {
            if(!messages.TryDequeue(out line))return false;
            Interlocked.Decrement(ref queued);return true;
        }
        public bool Send(long sequence,int width,int height,byte[] rgb)
        {
            if(disposed!=0||!Running||rgb==null||rgb.Length!=width*height*3||width<32||height<32||width>640||height>640)return false;
            if(Interlocked.CompareExchange(ref writing,1,0)!=0)return false;
            Process owned=process;
            Task.Run(()=>
            {
                try
                {
                    byte[] header;
                    using(var buffer=new MemoryStream())
                    {
                        using(var writer=new BinaryWriter(buffer,Encoding.UTF8,true))
                        { writer.Write(new byte[]{70,87,72,49});writer.Write(sequence);writer.Write(width);writer.Write(height);writer.Write(rgb.Length); }
                        header=buffer.ToArray();
                    }
                    var stream=owned.StandardInput.BaseStream;
                    stream.Write(header,0,header.Length);stream.Write(rgb,0,rgb.Length);stream.Flush();
                }
                catch(Exception ex) when(ex is IOException || ex is ObjectDisposedException || ex is InvalidOperationException)
                { if(disposed==0)Failure="Could not send frame to local worker."; }
                finally { Interlocked.Exchange(ref writing,0); }
            });
            return true;
        }
        public void Dispose()
        {
            if(Interlocked.Exchange(ref disposed,1)!=0)return;
            var owned=process;process=null;
            if(owned==null)return;
            try { if(!owned.HasExited)owned.Kill(); }
            catch(Exception ex) when(ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception) { }
            finally { owned.Dispose(); }
            while(messages.TryDequeue(out _)) { }
        }
    }
}
