using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FAA.Customization
{
    [DefaultExecutionOrder(12470),DisallowMultipleComponent]
    public sealed partial class FaaLaptopCameraGestures : MonoBehaviour
    {
        public bool CameraActive => cameraFeed != null && cameraFeed.isPlaying;
        public bool Starting { get; private set; }
        public bool RecognitionReady { get; private set; }
        public string Status { get; private set; } = "Camera OFF. Images stay on this laptop.";
        public Texture2D Preview => previewTexture;
        public FaaWebcamResult LatestResult { get; private set; }
        public FaaMultiFingerResize.Phase GesturePhase => gesture.State;
        public FaaMultiFingerResize.GestureMode GestureMode => gesture.Mode;
        public float DisplayedFactor => displayedFactor;
        public float SampleRate { get; private set; }
        private float displayedFactor=1f;
        public void SetGestureMode(FaaMultiFingerResize.GestureMode mode) { CancelSizing();gesture.SetMode(mode); }
        public bool PanelOpen { get; private set; }
        private FaaSpatialWorkspace owner;
        private FaaWebcamWorkerProcess worker;
        private WebCamTexture cameraFeed;
        private Texture2D previewTexture;
        private Color32[] sourcePixels, pixels;
        private WebCamDevice[] devices = Array.Empty<WebCamDevice>();
        private int deviceIndex, outputWidth, outputHeight;
        private long sequence, pending;
        private float startedAt,lastCameraFrame,sentAt,nextCapture,lastResultAt;
        private readonly FaaMultiFingerResize gesture = new();
        private bool disposing;

        public void Bind(FaaSpatialWorkspace workspace)
        {
            StopCamera();owner=workspace;BuildPanel();
        }
        public void TogglePanel()
        {
            PanelOpen=!PanelOpen;
            if(!PanelOpen)StopCamera();
            else RefreshDevices();
            RefreshPanel();
        }
        public void RefreshDevices()
        {
            if(CameraActive||Starting||PermissionPending)return;
            try { devices=WebCamTexture.devices; }
            catch(Exception) { devices=Array.Empty<WebCamDevice>();Status="Camera enumeration unavailable."; }
            deviceIndex=devices.Length==0?0:Mathf.Clamp(deviceIndex,0,devices.Length-1);
        }
        public void NextCamera()
        {
            if(CameraActive||Starting||PermissionPending)return;
            RefreshDevices();if(devices.Length>0)deviceIndex=(deviceIndex+1)%devices.Length;
            RefreshPanel();
        }
        public bool TryResolveWorker(out string executable,out string script,out string model)
        {
            string root=Path.Combine(Application.streamingAssetsPath,"FAA","WebcamGestures");
            model=Path.Combine(root,"hand_landmarker.task");script=null;executable=null;
            bool windows=Application.platform==RuntimePlatform.WindowsEditor||Application.platform==RuntimePlatform.WindowsPlayer;
            bool mac=Application.platform==RuntimePlatform.OSXEditor||Application.platform==RuntimePlatform.OSXPlayer;
            bool linux=Application.platform==RuntimePlatform.LinuxEditor||Application.platform==RuntimePlatform.LinuxPlayer;
            if(!windows&&!mac&&!linux)return false;
            string architecture=RuntimeInformation.ProcessArchitecture==Architecture.Arm64?"arm64":"x64";
            string target=(mac?"osx":windows?"windows":"linux")+"-"+architecture;
            string file=windows?"FaaWebcamWorker.exe":"FaaWebcamWorker";
            executable=Path.Combine(root,"runtime",target,"FaaWebcamWorker",file);
#if UNITY_EDITOR
            string project=Directory.GetParent(Application.dataPath).FullName;
            if(!File.Exists(executable))executable=Path.Combine(project,"Library","FAAWebcam","bundles",target,"FaaWebcamWorker",file);
            if(!File.Exists(executable))
            {
                executable=Path.Combine(project,"Library","FAAWebcam","venv",windows?"Scripts":"bin",windows?"python.exe":"python");
                script=Path.Combine(root,"worker.py");
            }
#endif
            return File.Exists(executable)&&File.Exists(model)&&(script==null||File.Exists(script));
        }
        // User-button entry ONLY; camera consent/on-state is deliberately never persisted.
        public void StartCameraByUser()
        {
            if(CameraActive||Starting||PermissionPending)return;
            StopCamera();PanelOpen=true;
            if(owner==null||!owner.Initialized||owner.NativeXr) { Status="Laptop camera mode is for desktop users, not an active XR headset.";return; }
            if(!FaaMacCameraPermission.CanCapture(PermissionState))
            { BeginPermissionRequest(true); return; }
            RefreshDevices();
            if(devices.Length==0) { Status="No laptop camera found. Connect/unblock a camera, then retry.";return; }
            if(!TryResolveWorker(out string executable,out string script,out string model))
            { Status="Local recognizer not installed. Run Tools/WebcamGestures/setup.py; player builds need the bundled worker.";return; }
            try
            {
                worker=new FaaWebcamWorkerProcess();worker.Start(executable,script,model);
                Starting=true;startedAt=Time.unscaledTime;Status="Starting local hand recognizer...";
            }
            catch(Exception ex) when(ex is IOException||ex is InvalidOperationException||ex is System.ComponentModel.Win32Exception||ex is ArgumentException)
            { StopCamera();Status="Could not launch local recognizer. Re-run webcam setup for this platform."; }
            RefreshPanel();
        }
        private void StartCaptureAfterReady()
        {
            if(!FaaMacCameraPermission.CanCapture(PermissionState))
            { StopCamera();Status=FaaMacCameraPermission.Description(PermissionState);return; }
            try
            {
                cameraFeed=new WebCamTexture(devices[deviceIndex].name,640,480,30);
                cameraFeed.Play();Starting=false;RecognitionReady=true;
                lastCameraFrame=lastResultAt=Time.unscaledTime;Status="Camera starting. Allow OS camera access if prompted.";
            }
            catch(Exception)
            { StopCamera();Status="Camera could not start. Check OS privacy permission and whether another app is using it."; }
        }
        private void Update()
        {
            if(owner==null)return;
            UpdatePermissionRequest();
            if((Starting||CameraActive||RecognitionReady)&&(!owner.Initialized||!owner.isActiveAndEnabled||owner.NativeXr))StopCamera();
            if(worker!=null)
            {
                while(worker.TryRead(out string line))
                {
                    try
                    {
                        var result=JsonUtility.FromJson<FaaWebcamResult>(line);
                        if(result==null)throw new ArgumentException();
                        if(result.kind=="ready"&&Starting)
                        {
                            if(result.protocol!=2||result.features!="multi-finger-v2")
                            {StopCamera();Status="Recognizer version mismatch. Rebuild the local multi-finger worker.";break;}
                            StartCaptureAfterReady();
                        }
                        else if(result.kind=="frame")Consume(result);
                        else if(result.kind=="error") { StopCamera();Status="Local hand recognizer reported an error. Restart or re-run setup.";break; }
                    }
                    catch(ArgumentException) { StopCamera();Status="Invalid response from local hand recognizer.";break; }
                    if(worker==null)break;
                }
                if(worker!=null&&(!worker.Running||worker.Failure!=null))
                { StopCamera();Status="Local hand recognizer stopped. Camera released; restart to retry."; }
            }
            if(Starting&&Time.unscaledTime-startedAt>20)
            { StopCamera();Status="Recognizer startup timed out. Camera was not opened."; }
            if(RecognitionReady)
            {
                if(Time.unscaledTime-lastCameraFrame>5)
                { StopCamera();Status="No camera frames. Check camera permission, shutter or device availability."; }
                else if(pending!=0&&Time.unscaledTime-sentAt>2)
                { StopCamera();Status="Recognition timed out. Camera released; restart to retry."; }
                else
                {
                    if(Time.unscaledTime-lastResultAt>.65f)CancelSizing();
                    Capture();
                }
            }
            if(!owner.EditMode||owner.IsManipulating)CancelSizing();
            else if(owner.WebcamSizing&&gesture.State==FaaMultiFingerResize.Phase.Resizing&&Time.unscaledTime-lastResultAt<.22f)
            {
                displayedFactor=FaaMultiFingerResize.RenderStep(displayedFactor,gesture.Factor,Time.unscaledDeltaTime);
                owner.ApplyWebcamSizing(displayedFactor);
            }
            RefreshPanel();
        }
        private void Capture()
        {
            if(cameraFeed==null||!cameraFeed.isPlaying||!cameraFeed.didUpdateThisFrame)return;
            lastCameraFrame=Time.unscaledTime;
            if(pending!=0||worker==null||worker.Writing||Time.unscaledTime<nextCapture||cameraFeed.width<=16||cameraFeed.height<=16)return;
            nextCapture=Time.unscaledTime+1f/24f;
            try
            {
                int width=cameraFeed.width,height=cameraFeed.height;
                if(width>4096||height>4096)throw new ArgumentException("Unsupported camera resolution.");
                if(sourcePixels==null||sourcePixels.Length!=width*height)sourcePixels=new Color32[width*height];
                sourcePixels=cameraFeed.GetPixels32(sourcePixels);
                int angle=cameraFeed.videoRotationAngle;
                bool swap=angle==90||angle==270;
                int uprightWidth=swap?height:width,uprightHeight=swap?width:height;
                int ow=uprightWidth>=uprightHeight?480:Mathf.Max(32,Mathf.RoundToInt(480f*uprightWidth/uprightHeight));
                int oh=uprightWidth>=uprightHeight?Mathf.Max(32,Mathf.RoundToInt(480f*uprightHeight/uprightWidth)):480;
                if(pixels==null||ow!=outputWidth||oh!=outputHeight)
                {
                    outputWidth=ow;outputHeight=oh;pixels=new Color32[ow*oh];
                    if(previewTexture!=null)Destroy(previewTexture);
                    previewTexture=new Texture2D(ow,oh,TextureFormat.RGBA32,false);
                }
                FaaWebcamFrameMath.Upright(sourcePixels,width,height,pixels,ow,oh,angle,cameraFeed.videoVerticallyMirrored);
                previewTexture.SetPixels32(pixels);previewTexture.Apply(false,false);
                var rgb=new byte[pixels.Length*3];
                for(int i=0;i<pixels.Length;i++) {rgb[i*3]=pixels[i].r;rgb[i*3+1]=pixels[i].g;rgb[i*3+2]=pixels[i].b;}
                long id=++sequence;
                if(worker.Send(id,ow,oh,rgb)) {pending=id;sentAt=Time.unscaledTime;}
            }
            catch(Exception ex) when(ex is UnityException||ex is ArgumentException||ex is InvalidOperationException)
            { StopCamera();Status="Camera frame unavailable. Check the camera and try again."; }
        }
        private void Consume(FaaWebcamResult result)
        {
            if(!RecognitionReady||pending==0||result.seq!=pending)return;
            pending=0;
            if(Time.unscaledTime-sentAt>.65f) {CancelSizing();Status="Recognition delayed; resizing paused.";return;}
            float interval=Time.unscaledTime-lastResultAt;
            if(interval>0)SampleRate=Mathf.Lerp(SampleRate,1f/interval,.2f);
            lastResultAt=Time.unscaledTime;LatestResult=result;
            bool canEdit=owner.EditMode&&!owner.NativeXr&&!owner.IsManipulating;
            gesture.Sample(result,sentAt,canEdit);
            if(gesture.JustStarted)
            {
                displayedFactor=1;
                if(!owner.BeginWebcamSizing())gesture.Reset();
            }
            if(gesture.State==FaaMultiFingerResize.Phase.Paused)gesture.FreezeAt(displayedFactor);
            if(gesture.State!=FaaMultiFingerResize.Phase.Resizing&&gesture.State!=FaaMultiFingerResize.Phase.Paused)owner.EndWebcamSizing();
            Status=!owner.EditMode?"Camera ON - layout LOCKED. Unlock to resize.":
                result.hands==null||result.hands.Length!=2?"Show BOTH hands inside the camera preview.":
                gesture.State==FaaMultiFingerResize.Phase.Paused?"Tracking briefly lost - size held steady.":
                gesture.State==FaaMultiFingerResize.Phase.ReleaseHands?"Show open hands to arm. Keep palms facing the camera.":
                gesture.State==FaaMultiFingerResize.Phase.Ready?(gesture.Mode==FaaMultiFingerResize.GestureMode.OpenPalms?"Hold both palms open briefly.":"Touch thumb to ANY fingertip on each hand."):
                gesture.State==FaaMultiFingerResize.Phase.Holding?"Hold steady - calibrating this gesture...":
                "RESIZING HUD - spread to enlarge, close to shrink. Release to keep.";
        }
        public void CancelSizing() {gesture.Reset();displayedFactor=1;owner?.EndWebcamSizing();}
        public void StopCamera()
        {
            if(disposing)return;disposing=true;
            PermissionPending=false;startAfterPermission=false;
            CancelSizing();Starting=RecognitionReady=false;pending=0;sequence=0;LatestResult=null;
            worker?.Dispose();worker=null;
            if(cameraFeed!=null) {cameraFeed.Stop();Destroy(cameraFeed);cameraFeed=null;}
            if(previewTexture!=null) {Destroy(previewTexture);previewTexture=null;}
            sourcePixels=pixels=null;Status="Camera OFF. No images recorded or uploaded.";
            disposing=false;RefreshPanel();
        }
        // The native consent dialog may take focus before any capture has started.
        private void OnApplicationFocus(bool focused) {if(!focused&&!PermissionPending)StopCamera();}
        private void OnApplicationPause(bool paused) {if(paused&&!PermissionPending)StopCamera();}
        private void OnDisable()=>StopCamera();
        private void OnDestroy() {StopCamera();if(CameraCanvas!=null)Destroy(CameraCanvas.gameObject);}
    }
}
