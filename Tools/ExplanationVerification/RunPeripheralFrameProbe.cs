// Multi-frame LIVE render-path test. No webcam, flight commands, scene save or simulated telemetry.
var w=FAA.Customization.FaaSpatialWorkspace.Current;
if(!Application.isPlaying||w==null||!w.Initialized)throw new Exception("Workspace not ready");
if(w.LaptopCamera.CameraActive||w.LaptopCamera.Starting)throw new Exception("Do not interrupt a user's active camera");
const string key="FAA.PeripheralFrameProbe";
if(AppDomain.CurrentDomain.GetData(key)!=null)throw new Exception("Probe already running");
var flight=UnityEngine.Object.FindObjectsByType<Canvas>().Where(c=>c.name=="FAASymbologyCanvas"||c.name=="FAAHeadingTapeCanvas").ToArray();
var modules=w.Modules.Where(m=>new[]{"airspeed","altitude","nr","vertical-speed","torque","glideslope"}.Contains(m.Id)).ToArray();
var poses=w.UtilityPanels.ToDictionary(p=>p.Id,p=>p.Layout.Copy());
bool persistence=w.PersistChanges,menu=w.MenuOpen,handPanel=w.LaptopCamera.PanelOpen,editing=w.EditMode;
var dirtyField=typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
object dirty=dirtyField.GetValue(w);
var controller=w.View.GetComponent<AircraftControl.Camera.AircraftCameraController>();
if(controller!=null&&controller.IsPanelInspectionActive)throw new Exception("Finish user side inspection before running probe");
var rows=new System.Collections.Generic.List<object>();
int lastFrame=-1,tick=0;
Canvas.WillRenderCanvases callback=null;
Action cleanup=()=>{
    Canvas.willRenderCanvases-=callback;w.ReturnToForwardView();
    foreach(var p in w.UtilityPanels){var s=poses[p.Id];p.Layout.yaw=s.yaw;p.Layout.elevation=s.elevation;p.Layout.distance=s.distance;p.Layout.scale=s.scale;}
    if(w.MenuOpen!=menu)w.ToggleMenu();if(w.LaptopCamera.PanelOpen!=handPanel)w.LaptopCamera.TogglePanel();
    w.SetEditMode(editing);w.RefreshTransforms();dirtyField.SetValue(w,dirty);w.PersistChanges=persistence;
    AppDomain.CurrentDomain.SetData(key,null);
};
w.PersistChanges=false;w.SetEditMode(false);w.OpenMenu();if(!w.LaptopCamera.PanelOpen)w.LaptopCamera.TogglePanel();
callback=()=>{
    if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
    try
    {
        int phase=tick/80,within=tick%80;
        if(within==0)
        {
            if(phase==0){w.StowUtility("settings");w.StowUtility("camera-controls");}
            if(phase==1){w.ToggleMenu();}
            if(phase==2){w.OpenMenu();w.BringUtilityHere("settings");w.BringUtilityHere("camera-controls");}
            if(phase==3)foreach(var p in w.UtilityPanels){p.Layout.scale=1.6f;p.Layout.distance=.55f;p.Layout.yaw=0;}
            if(phase==4){foreach(var p in w.UtilityPanels)p.Layout.scale=1;w.StowUtility("settings");w.StowUtility("camera-controls");w.InspectUtility("camera-controls");}
            if(phase==5){w.ReturnToForwardView();w.StowUtility("settings");w.StowUtility("camera-controls");}
            w.RefreshTransforms();
        }
        if(within>=20)
        {
            rows.Add(new{
                phase,frame=Time.frameCount,
                canvases=flight.Select(c=>new{c.name,mode=c.renderMode.ToString(),c.planeDistance,c.scaleFactor,rect=((RectTransform)c.transform).rect.ToString()}).ToArray(),
                modules=modules.Select(m=>new{m.Id,position=new[]{m.Target.localPosition.x,m.Target.localPosition.y,m.Target.localPosition.z},scale=m.Layout.scale,
                    box=m.TryScreenBounds(w.View,out var b)?new[]{b.x,b.y,b.width,b.height}:null}).ToArray(),
                panels=w.UtilityPanels.Select(p=>{
                    var corners=new Vector3[4];p.Radar.GetWorldCorners(corners);
                    float clearance=corners.Min(v=>Vector3.Angle(w.CockpitFrame.forward,v-w.CockpitFrame.position));
                    return new{p.Id,p.Layout.yaw,p.Layout.distance,p.Layout.scale,minCornerAngle=clearance};
                }).ToArray()
            });
        }
        tick++;
        if(tick>=480)
        {
            System.IO.Directory.CreateDirectory("artifacts/peripheral-stability");
            System.IO.File.WriteAllText("artifacts/peripheral-stability/after-render-frames.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));
            cleanup();
        }
    }
    catch(Exception e)
    {
        cleanup();System.IO.File.WriteAllText("artifacts/peripheral-stability/frame-probe-error.txt",e.ToString());
    }
};
AppDomain.CurrentDomain.SetData(key,cleanup);Canvas.willRenderCanvases+=callback;
return "Collecting 360 measured render samples over 480 frames and six open/close/recovery/resize/side-look phases.";
