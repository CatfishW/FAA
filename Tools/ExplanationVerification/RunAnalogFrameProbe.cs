var w=FAA.Customization.FaaSpatialWorkspace.Current;
if(!Application.isPlaying||w==null||!w.Initialized)throw new Exception("Workspace not ready");
if(w.LaptopCamera.CameraActive)throw new Exception("Do not disturb active webcam use");
const string key="FAA.Analog.FrameProbe";
if(AppDomain.CurrentDomain.GetData(key)!=null)throw new Exception("Probe already active");
var rows=new System.Collections.Generic.List<object>();int last=-1,frames=0;
var saved=w.ExportSymbologyPreferences();var originalVersion=w.CurrentSymbology;
bool persist=w.PersistChanges,editing=w.EditMode,menu=w.MenuOpen;
var dirtyField=typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
object oldDirty=dirtyField.GetValue(w);w.PersistChanges=false;
Canvas.WillRenderCanvases callback=null;
Action restore=()=>
{
    Canvas.willRenderCanvases-=callback;
    FAA.Customization.FaaSymbologyPreferences.TryParse(saved,out var p);
    foreach(var version in new[]{FAA.Customization.FaaSymbologyVersion.Digital,FAA.Customization.FaaSymbologyVersion.ClassicAnalog})
    {w.SetSymbologyVersion(version);foreach(var s in version==FAA.Customization.FaaSymbologyVersion.Digital?p.digital:p.classic)if(w.GetEntry(s.id)!=null)w.GetEntry(s.id).scale=s.scale;}
    w.SetSymbologyVersion(originalVersion);w.SetEditMode(editing);if(w.MenuOpen!=menu)w.ToggleMenu();w.ReturnToForwardView();w.RefreshTransforms();
    dirtyField.SetValue(w,oldDirty);w.PersistChanges=persist;AppDomain.CurrentDomain.SetData(key,null);
};
AppDomain.CurrentDomain.SetData(key,restore);
callback=()=>
{
    if(last==Time.frameCount)return;last=Time.frameCount;
    try
    {
        int phase=frames/60,within=frames%60;
        if(within==0)
        {
            if(phase==0){w.SetSymbologyVersion(FAA.Customization.FaaSymbologyVersion.ClassicAnalog);w.ReturnToForwardView();if(w.MenuOpen)w.ToggleMenu();}
            if(phase==1){w.InspectUtility("settings");w.OpenSymbologySettings();}
            if(phase==2){w.ReturnToForwardView();w.SetScale("altitude",1.25f);}
            if(phase==3){w.SetSymbologyVersion(FAA.Customization.FaaSymbologyVersion.Digital);}
            if(phase==4){w.SetSymbologyVersion(FAA.Customization.FaaSymbologyVersion.ClassicAnalog);}
        }
        if(within>=20)
        {
            var d=w.ClassicHud.Animation.Display;
            rows.Add(new{frame=last,phase,version=w.CurrentSymbology.ToString(),classicVisible=w.ClassicHud.Visible,
                geometry=w.Modules.Where(m=>new[]{"airspeed","altitude","torque","nr"}.Contains(m.Id)).Select(m=>new{m.Id,bounds=m.TryScreenBounds(w.View,out var r)?new[]{r.x,r.y,r.width,r.height}:null}).ToArray(),
                telemetry=new{d.Fresh,d.Speed,d.Altitude,d.Rpm},radars=w.Panels.Select(p=>new{p.Id,p.Layout.yaw}).ToArray()});
        }
        frames++;
        if(frames>=300)
        {
            System.IO.Directory.CreateDirectory("artifacts/analog-symbology");
            System.IO.File.WriteAllText("artifacts/analog-symbology/render-frames.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));
            restore();
        }
    }
    catch(Exception e)
    {System.IO.File.WriteAllText("artifacts/analog-symbology/render-error.txt",e.ToString());restore();}
};
Canvas.willRenderCanvases+=callback;
return "Measuring 300 rendered frames / 200 settled samples across five style, size and side-panel phases. Camera stays off.";
