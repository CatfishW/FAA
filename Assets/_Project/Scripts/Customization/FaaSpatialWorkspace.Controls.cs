using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static FAA.Customization.FaaWorkspaceUi;
namespace FAA.Customization
{
    public sealed partial class FaaSpatialWorkspace
    {
        public bool MenuOpen { get; private set; }
        public Canvas ControlsCanvas=>controlsCanvas;
        private Canvas controlsCanvas;
        private RectTransform menuRoot,selectionFrame,resizeGrip,hudPage,panelsPage;
        private TMP_Text selectedCaption,detailCaption,inputCaption,modeCaption;
        private Slider scaleSlider;
        private readonly List<string> selectionIds=new();
        private readonly List<Button> editButtons=new();
        private bool refreshingUi;
        public void ToggleMenu(){MenuOpen=!MenuOpen;RefreshControls();}
        public void OpenMenu(){MenuOpen=true;RefreshControls();}
        private void BuildControls()
        {
            controlsCanvas=FaaWorkspaceUi.Canvas("FAA Spatial Layout Controls",transform,7250);
            var scaler=controlsCanvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var dock=Rect("Workspace Recovery Dock",controlsCanvas.transform,0,0,648,38);
            dock.anchorMin=dock.anchorMax=dock.pivot=new Vector2(1,0);dock.anchoredPosition=new Vector2(-16,16);
            Box("Recovery background",dock,324,19,648,38,Background);
            Button("Menu",dock,"SETTINGS / F9",70,19,132,32,ToggleMenu);
            Button("Inspect Left",dock,"LOOK LEFT",188,19,94,32,()=>InspectUtility("camera-controls"));
            Button("Inspect Right",dock,"LOOK RIGHT",288,19,94,32,()=>InspectUtility("settings"));
            Button("Forward View",dock,"FORWARD / R",402,19,120,32,ReturnToForwardView);
            Button("Inspect Weather",dock,"WEATHER",508,19,82,32,()=>InspectPanel("weather"));
            Button("Inspect Traffic",dock,"TRAFFIC",600,19,88,32,()=>InspectPanel("traffic"));
            SettingsCanvas=FaaWorkspaceUi.Canvas("FAA Cockpit Settings Panel",transform,7220);
            menuRoot=Rect("Cockpit Settings",SettingsCanvas.transform,960,540,640,620);
            Box("Settings Surface",menuRoot,320,310,640,620,Background,true);
            Box("Accent Rail",menuRoot,4,310,4,620,Accent);
            Text("Workspace Title",menuRoot,"COCKPIT WORKSPACE",230,61,418,44,27,true);
            Text("Workspace Subtitle",menuRoot,"YOUR SPACE. YOUR SCALE.",200,94,360,24,13,true).color=Muted;
            Button("Stow Settings",menuRoot,"STOW RIGHT",548,64,148,36,()=>StowUtility("settings"));
            Button("Settings Panel Smaller",menuRoot,"PANEL -",464,104,100,28,()=>ResizeUtility("settings",-.1f));
            Button("Settings Panel Larger",menuRoot,"PANEL +",570,104,100,28,()=>ResizeUtility("settings",.1f));
            Button("HUD Tab",menuRoot,"INSTRUMENTS",80,151,114,40,()=>SetSettingsPage(false));
            Button("Panels Tab",menuRoot,"PANELS",200,151,114,40,()=>SetSettingsPage(true));
            Button("Symbology Tab",menuRoot,"SYMBOLOGY",320,151,114,40,OpenSymbologySettings);
            Button("Laptop Camera",menuRoot,"HAND STUDIO",440,151,114,40,()=>LaptopCamera?.TogglePanel());
            Button("Data Sources Tab",menuRoot,"DATA SOURCE",560,151,114,40,OpenDataSourceSettings);
            hudPage=Rect("Instrument Settings Page",menuRoot,320,352,640,354);
            selectedCaption=Text("Selected Module",hudPage,"AIRSPEED",320,38,440,40,24);
            Button("Previous Module",hudPage,"<",48,38,52,42,()=>CycleSelection(-1));
            Button("Next Module",hudPage,">",592,38,52,42,()=>CycleSelection(1));
            Button("Smaller",hudPage,"-",50,102,56,48,()=>AdjustSelectedScale(-.05f));
            Button("Larger",hudPage,"+",590,102,56,48,()=>AdjustSelectedScale(.05f));
            var track=Box("Module Size Slider",hudPage,320,102,464,12,Card,true).rectTransform;
            scaleSlider=track.gameObject.AddComponent<Slider>();scaleSlider.minValue=.4f;scaleSlider.maxValue=1.6f;
            var thumb=Box("Size Thumb",track,0,0,18,22,Accent).rectTransform;scaleSlider.handleRect=thumb;scaleSlider.targetGraphic=thumb.GetComponent<Image>();
            scaleSlider.onValueChanged.AddListener(v=>{if(!refreshingUi)SetScale(SelectedId,v);});
            Text("Scale Endpoints",hudPage,"40%                  INDIVIDUAL SIZE                  160%",320,137,490,24,12).color=Muted;
            Button("Size 60",hudPage,"COMPACT 60%",123,188,202,42,()=>SetScale(SelectedId,.6f));
            Button("Size 80",hudPage,"BALANCED 80%",335,188,202,42,()=>SetScale(SelectedId,.8f));
            Button("Size 100",hudPage,"FULL 100%",547,188,146,42,()=>SetScale(SelectedId,1));
            Button("All HUD Smaller",hudPage,"ALL HUD  -",175,246,302,44,()=>ScaleAllModules(-.05f));
            Button("All HUD Larger",hudPage,"ALL HUD  +",489,246,262,44,()=>ScaleAllModules(.05f));
            Button("Reset Selected",hudPage,"RESET THIS INSTRUMENT",320,308,596,42,ResetSelected);
            panelsPage=Rect("Spatial Panel Settings Page",menuRoot,320,352,640,354);
            string[] ids={"weather","traffic","settings","camera-controls"};string[] names={"WEATHER","TRAFFIC","SETTINGS","HAND STUDIO"};
            for(int i=0;i<4;i++){string id=ids[i];Button("Select "+id,panelsPage,names[i],88+154*i,38,144,40,()=>Select(id));}
            Text("Spatial Instruction",panelsPage,"Right-click + drag a panel to move it, even while locked.\nXR-3: unlock, then pinch the top handle; two hands resize.",320,92,592,58,17).color=Muted;
            editButtons.Add(Button("Nearer",panelsPage,"NEARER",174,157,292,42,()=>{if(EditMode)AdjustDistance(-.1f);}));
            editButtons.Add(Button("Farther",panelsPage,"FARTHER",480,157,272,42,()=>{if(EditMode)AdjustDistance(.1f);}));
            float[] headings={-75,0,75,180};string[] placements={"LEFT","DOWN","RIGHT","BEHIND"};
            for(int i=0;i<4;i++){int n=i;editButtons.Add(Button("Place "+placements[i],panelsPage,placements[i],88+154*i,212,144,40,()=>{if(EditMode)PlaceSelected(headings[n],n==1?-55:5);}));}
            Button("Seat Recenter",panelsPage,"RECENTER SEAT",174,274,292,42,RecenterSeat);
            Button("3D Panels",panelsPage,"STOW BOTH RADARS",480,274,272,42,RecallPanels);
            Button("Reset Layout",panelsPage,"RESET POSITIONS + SIZES",320,327,596,36,()=>{if(EditMode)ResetAll();});
            modeCaption=Text("Layout Mode",menuRoot,"GESTURES LOCKED",200,558,354,24,16,true);
            Button("Edit Lock",menuRoot,"UNLOCK / LOCK",535,555,166,38,()=>SetEditMode(!EditMode));
            detailCaption=Text("Layout Details",menuRoot,"",320,515,590,28,14);
            inputCaption=Text("Input Source",menuRoot,"",320,595,600,24,12);inputCaption.color=Muted;
            RegisterUtility("settings",SettingsCanvas,menuRoot,90,.58f);
            BuildSymbologyPage();
            BuildDataSourcePage();
            selectionIds.Clear();foreach(var m in modules)if(m.TryScreenBounds(View,out _))selectionIds.Add(m.Id);
            SetSettingsPage(false);
            selectionFrame=Rect("Selected HUD Bounds",controlsCanvas.transform,0,0,0,0);selectionFrame.anchorMin=selectionFrame.anchorMax=new Vector2(.5f,.5f);
            var frame=selectionFrame.gameObject.AddComponent<Image>();frame.color=new Color(.2f,.8f,.72f,.03f);frame.raycastTarget=false;
            var outline=selectionFrame.gameObject.AddComponent<Outline>();outline.effectColor=Accent;
            resizeGrip=Rect("Resize Selected HUD",selectionFrame,0,0,28,28);resizeGrip.anchorMin=resizeGrip.anchorMax=new Vector2(1,0);
            resizeGrip.gameObject.AddComponent<Image>().color=Accent;resizeGrip.gameObject.AddComponent<FaaWorkspaceDragHandle>();
        }
        public void SetSettingsPage(bool spatial){hudPage.gameObject.SetActive(!spatial);panelsPage.gameObject.SetActive(spatial);if(symbologyPage!=null)symbologyPage.gameObject.SetActive(false);if(dataSourcePage!=null)dataSourcePage.gameObject.SetActive(false);}
        private void CycleSelection(int delta){if(selectionIds.Count>0)Select(selectionIds[(selectionIds.IndexOf(SelectedId)+delta+selectionIds.Count)%selectionIds.Count]);}
        private void UpdateControlCanvasPose()
        {
            if(controlsCanvas==null||View==null)return;
            RenderMode mode=NativeXr?RenderMode.ScreenSpaceCamera:RenderMode.ScreenSpaceOverlay;
            if(controlsCanvas.renderMode!=mode)controlsCanvas.renderMode=mode;
            Camera camera=NativeXr?View:null;if(controlsCanvas.worldCamera!=camera)controlsCanvas.worldCamera=camera;
            if(NativeXr)controlsCanvas.planeDistance=Mathf.Max(.75f,View.nearClipPlane+.15f);
        }
        private void RefreshControls()
        {
            RefreshDataSourceControls();
            RefreshSymbologyControls();
            if(menuRoot==null)return;
            if(detailCaption!=null)detailCaption.gameObject.SetActive((symbologyPage==null||!symbologyPage.gameObject.activeSelf)&&(dataSourcePage==null||!dataSourcePage.gameObject.activeSelf));
            menuRoot.gameObject.SetActive(MenuOpen);refreshingUi=true;
            modeCaption.text=EditMode?"GESTURES + DRAGGING ENABLED":"GESTURES LOCKED  /  BUTTONS ACTIVE";
            modeCaption.color=EditMode?Accent:Muted;
            var entry=GetEntry(SelectedId);string caption=SelectedId.ToUpperInvariant();foreach(var m in modules)if(m.Id==SelectedId)caption=m.Caption;
            selectedCaption.text=caption+(entry!=null?"   "+Mathf.RoundToInt(entry.scale*100)+"%":"");
            if(entry!=null)scaleSlider.SetValueWithoutNotify(entry.scale);scaleSlider.interactable=entry!=null;
            foreach(var button in editButtons)button.interactable=EditMode&&GetPanel(SelectedId)!=null;
            var p=GetPanel(SelectedId);
            detailCaption.text=p==null?"Forward view protected. Settings stay beside the cockpit.":$"{p.Id}   {p.Layout.yaw:0} deg / {p.Layout.elevation:0} deg    {p.Layout.distance:0.00} m";
            inputCaption.text=ProfileStatus+"  |  "+InputStatus;refreshingUi=false;
        }
        private void UpdateSelectionFrame()
        {
            if(selectionFrame==null||controlsCanvas==null)return;bool visible=false;
            if(EditMode)foreach(var m in modules)if(m.Id==SelectedId&&m.TryScreenBounds(View,out var b))
            {
                var root=(RectTransform)controlsCanvas.transform;Camera ui=NativeXr?View:null;
                if(RectTransformUtility.ScreenPointToLocalPointInRectangle(root,b.min,ui,out var a)&&RectTransformUtility.ScreenPointToLocalPointInRectangle(root,b.max,ui,out var z))
                {selectionFrame.anchoredPosition=(a+z)*.5f;selectionFrame.sizeDelta=z-a;resizeGrip.GetComponent<FaaWorkspaceDragHandle>().Configure(this,m.Id,true);visible=true;}
            }
            selectionFrame.gameObject.SetActive(visible);
        }
        private void DestroyControls()
        {
            if(SettingsCanvas!=null)Destroy(SettingsCanvas.gameObject);SettingsCanvas=null;
            if(controlsCanvas!=null)Destroy(controlsCanvas.gameObject);controlsCanvas=null;editButtons.Clear();selectionIds.Clear();
        }
    }
}
