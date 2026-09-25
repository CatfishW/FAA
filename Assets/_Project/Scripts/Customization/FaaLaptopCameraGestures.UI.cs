using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static FAA.Customization.FaaWorkspaceUi;
namespace FAA.Customization
{
    public sealed partial class FaaLaptopCameraGestures
    {
        public Canvas CameraCanvas { get; private set; }
        private RectTransform panel;
        private RawImage previewImage;
        private FaaHandSkeletonGraphic skeleton;
        private TMP_Text statusText,deviceText,toggleText,previewPlaceholder,permissionText,fingerText,phaseText,rateText;
        private Button toggleButton,nextButton;
        private Button pinchModeButton,palmModeButton;
        private Image progress;
        private float nextPanelUpdate;
        private void BuildPanel()
        {
            if(CameraCanvas!=null)Destroy(CameraCanvas.gameObject);
            if(owner==null)return;
            CameraCanvas=FaaWorkspaceUi.Canvas("FAA Spatial Hand Studio",owner.transform,7230);
            panel=Rect("FAA Laptop Camera Gestures",CameraCanvas.transform,960,540,640,750);
            Box("Hand Studio Surface",panel,320,375,640,750,Background,true);
            Box("Camera Accent Rail",panel,4,375,4,750,new Color(.47f,.65f,1,1));
            Text("Webcam Heading",panel,"HAND STUDIO",210,60,370,42,28,true);
            Text("Hand Studio Subtitle",panel,"LOCAL CAMERA  /  ALL FIVE FINGERS",245,95,440,24,13,true).color=Muted;
            Button("Stow Camera",panel,"STOW LEFT",543,60,154,36,()=>owner.StowUtility("camera-controls"));
            Button("Camera Panel Smaller",panel,"PANEL -",462,104,100,28,()=>owner.ResizeUtility("camera-controls",-.1f));
            Button("Camera Panel Larger",panel,"PANEL +",570,104,100,28,()=>owner.ResizeUtility("camera-controls",.1f));
            Box("Preview background",panel,320,276,592,294,Card);
            previewPlaceholder=Text("Camera Off Placeholder",panel,"CAMERA OFF\n\nStart the camera to see your hands.\nNo video is recorded or uploaded.",320,270,550,160,20);
            var image=Rect("Local Camera Preview",panel,320,276,390,292);
            previewImage=image.gameObject.AddComponent<RawImage>();previewImage.raycastTarget=false;previewImage.uvRect=new Rect(1,0,-1,1);
            image.gameObject.AddComponent<RectMask2D>();
            var bones=Rect("All Finger Landmarks",image,195,146,390,292);
            bones.anchorMin=Vector2.zero;bones.anchorMax=Vector2.one;bones.offsetMin=bones.offsetMax=Vector2.zero;
            skeleton=bones.gameObject.AddComponent<FaaHandSkeletonGraphic>();skeleton.raycastTarget=false;
            phaseText=Text("Gesture Phase",panel,"CAMERA OFF",180,444,310,30,19,true);phaseText.color=Accent;
            rateText=Text("Gesture Quality",panel,"NO TRACKING",488,444,260,30,13);rateText.color=Muted;
            Box("Progress Track",panel,320,468,592,4,Card);
            progress=Box("Hold To Resize Progress",panel,24,468,592,4,Accent);progress.rectTransform.pivot=new Vector2(0,.5f);
            fingerText=Text("Finger Feedback",panel,"LEFT: --     RIGHT: --",320,493,592,32,14);
            pinchModeButton=Button("Multi Finger Mode",panel,"THUMB + ANY FINGER",174,536,294,38,()=>SetGestureMode(FaaMultiFingerResize.GestureMode.MultiFingerPinch));
            palmModeButton=Button("Open Palms Mode",panel,"OPEN PALMS",478,536,282,38,()=>SetGestureMode(FaaMultiFingerResize.GestureMode.OpenPalms));
            statusText=Text("Webcam Status",panel,Status,320,581,594,42,15);
            toggleButton=Button("Webcam Start Stop",panel,"START CAMERA",174,630,294,42,()=>
                {if(CameraActive||Starting||RecognitionReady||PermissionPending)StopCamera();else StartCameraByUser();});
            toggleText=toggleButton.GetComponentInChildren<TMP_Text>();
            Button("Webcam Edit Lock",panel,"GESTURES / LOCK",478,630,282,42,()=>owner.SetEditMode(!owner.EditMode));
            Button("Request Camera Permission",panel,"ALLOW",78,679,102,34,RequestPermissionByUser);
            Button("Camera Privacy Settings",panel,"PERMISSIONS",201,679,134,34,OpenCameraSettingsByUser);
            nextButton=Button("Webcam Next Device",panel,"NEXT CAMERA",352,679,154,34,NextCamera);
            Button("Webcam Hide",panel,"CLOSE + STOP",526,679,188,34,()=>{PanelOpen=false;StopCamera();});
            deviceText=Text("Webcam Device",panel,"",174,719,296,22,12);deviceText.color=Muted;
            permissionText=Text("Webcam Permission",panel,"",477,719,294,22,12);permissionText.color=Muted;
            owner.RegisterUtility("camera-controls",CameraCanvas,panel,-90,.58f);
            RefreshPanel();
        }
        private void RefreshPanel()
        {
            if(panel==null)return;
            bool visible=PanelOpen&&owner!=null&&owner.Initialized;
            if(panel.gameObject.activeSelf!=visible)panel.gameObject.SetActive(visible);
            if(!visible)return;
            previewImage.texture=previewTexture;previewImage.enabled=previewTexture!=null;
            previewPlaceholder.gameObject.SetActive(previewTexture==null);
            if(previewTexture!=null)
            {
                float ratio=Mathf.Min(590f/previewTexture.width,292f/previewTexture.height);
                previewImage.rectTransform.sizeDelta=new Vector2(previewTexture.width,previewTexture.height)*ratio;
            }
            skeleton.SetFrame(previewTexture!=null&&Time.unscaledTime-lastResultAt<.22f?LatestResult:null);
            progress.rectTransform.sizeDelta=new Vector2(592*(gesture.State==FaaMultiFingerResize.Phase.Resizing?1:gesture.HoldProgress),4);
            if(Time.unscaledTime<nextPanelUpdate)return;nextPanelUpdate=Time.unscaledTime+.1f;
            statusText.text=Status;
            pinchModeButton.GetComponent<Image>().color=GestureMode==FaaMultiFingerResize.GestureMode.MultiFingerPinch?new Color(.06f,.3f,.25f):Card;
            palmModeButton.GetComponent<Image>().color=GestureMode==FaaMultiFingerResize.GestureMode.OpenPalms?new Color(.06f,.3f,.25f):Card;
            phaseText.text=!RecognitionReady?"CAMERA OFF":!owner.EditMode?"GESTURES LOCKED":gesture.State.ToString().ToUpperInvariant()+"   "+Mathf.RoundToInt(displayedFactor*100)+"%";
            rateText.text=RecognitionReady?$"{SampleRate:0} samples/s   {LatestResult?.inferenceMs??0:0} ms CPU":"NO TRACKING";
            permissionText.text=FaaMacCameraPermission.Description(PermissionState);
            deviceText.text=devices.Length==0?"No camera selected":devices[deviceIndex].name;
            toggleText.text=CameraActive||Starting||RecognitionReady||PermissionPending?"STOP / CANCEL":"START CAMERA";
            toggleButton.interactable=owner!=null&&!owner.NativeXr;nextButton.interactable=!CameraActive&&!Starting&&!RecognitionReady&&!PermissionPending;
            previewPlaceholder.text=Starting||RecognitionReady?"WAITING FOR CAMERA\n\nAllow OS access when prompted.":"CAMERA OFF\n\nStart the camera to see all finger joints.\nLocal processing. No recording.";
            fingerText.text=HandSummary(0)+"       "+HandSummary(1);
        }
        private string HandSummary(int index)
        {
            string side=index==0?"LEFT":"RIGHT";
            if(LatestResult?.hands==null||LatestResult.hands.Length<=index||Time.unscaledTime-lastResultAt>.22f)return side+": --";
            var hand=LatestResult.hands[index];if(!FaaMultiFingerResize.Valid(hand))return side+": uncertain";
            string[] names={"INDEX","MIDDLE","RING","LITTLE"};int finger=FaaMultiFingerResize.PinchFinger(hand);
            return side+": "+hand.fingers+" extended / "+(finger>=0?names[finger]+" PINCH":"OPEN");
        }
    }
}
