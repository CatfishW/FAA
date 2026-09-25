// Render-frame evidence; never captures images, opens camera or invokes AI/flight-control actions.
var w=FAA.Customization.FaaSpatialWorkspace.Current;
var brief=UnityEngine.Object.FindFirstObjectByType<FAA.Explanations.ExplanationAssistantPanel>();
if(!Application.isPlaying||w==null||!w.Initialized||brief==null)throw new Exception("Workspace not ready");
if(w.LaptopCamera.CameraActive)throw new Exception("Do not interrupt an active camera session");
const string key="FAA.RadarBrief.FrameProbe";
if(AppDomain.CurrentDomain.GetData(key)!=null)throw new Exception("Probe already active");
var controls=UnityEngine.Object.FindFirstObjectByType<FAA.Customization.FaaRadarControlsOverlay>();
var info=UnityEngine.Object.FindFirstObjectByType<FAA.Customization.XPlaneWeatherInfoStrip>();
var traffic=w.Panels.First(p=>p.Id=="traffic").Radar.GetComponentInChildren<TrafficRadar.TrafficRadarDisplay>(true);
var poses=w.InteractivePanels.ToDictionary(p=>p.Id,p=>p.Layout.Copy());
var scales=w.Modules.ToDictionary(m=>m.Id,m=>m.Layout.scale);
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var dirty=typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",flags);object dirtyBefore=dirty.GetValue(w);
bool persist=w.PersistChanges,open=brief.IsOpen,expanded=info!=null&&info.IsExpanded,full=traffic.IsFullscreen;
var cameraController=w.View.GetComponent<AircraftControl.Camera.AircraftCameraController>();
var rows=new System.Collections.Generic.List<object>();int start=Time.frameCount,last=-1,phase=-1;
System.Action cleanup=null;Canvas.WillRenderCanvases callback=null;
cleanup=()=>{Canvas.willRenderCanvases-=callback;w.CancelManipulation();cameraController.ResetView();
 if(traffic.IsFullscreen!=full)traffic.ToggleFullscreen();if(info!=null)info.SetExpanded(expanded,true);
 controls.SetRadarConfigurationVisible(FAA.Customization.FaaRadarKind.Weather,expanded);
 foreach(var panel in w.InteractivePanels){var p=poses[panel.Id];panel.Layout.yaw=p.yaw;panel.Layout.elevation=p.elevation;panel.Layout.distance=p.distance;panel.Layout.scale=p.scale;}
 foreach(var m in w.Modules)m.Layout.scale=scales[m.Id];brief.SetOpen(open);w.RefreshTransforms();dirty.SetValue(w,dirtyBefore);w.PersistChanges=persist;AppDomain.CurrentDomain.SetData(key,null);};
AppDomain.CurrentDomain.SetData(key,cleanup);w.PersistChanges=false;w.ReturnToForwardView();brief.SetOpen(false);w.RecallPanels();
callback=()=>
{
 if(last==Time.frameCount)return;last=Time.frameCount;int elapsed=last-start;int current=elapsed/80;
 try
 {
  if(current>=6){System.IO.Directory.CreateDirectory("artifacts/radar-brief-fix");System.IO.File.WriteAllText("artifacts/radar-brief-fix/render-frames.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));cleanup();return;}
  if(current!=phase)
  {
   phase=current;
   if(phase==1){controls.SetRadarConfigurationVisible(FAA.Customization.FaaRadarKind.Weather,true);controls.SetRadarConfigurationVisible(FAA.Customization.FaaRadarKind.Traffic,true);info?.SetExpanded(true,true);}
   if(phase==2){w.SetScale("weather",1.6f);w.SetScale("traffic",1.6f);foreach(var p in w.Panels){p.Layout.yaw=0;p.Layout.distance=.55f;}w.RefreshTransforms();}
   if(phase==3){foreach(var p in w.Panels){p.Layout.scale=.8f;p.Layout.distance=1.7f;}w.RecallPanels();w.InspectPanel("weather");}
   if(phase==4){w.InspectPanel("traffic");if(!traffic.IsFullscreen)traffic.ToggleFullscreen();}
   if(phase==5){cameraController.ResetView();if(traffic.IsFullscreen)traffic.ToggleFullscreen();w.RecallPanels();info?.SetExpanded(false,true);controls.SetRadarConfigurationVisible(FAA.Customization.FaaRadarKind.Weather,false);}
  }
  if(elapsed%80<24)return;
  var panelRows=w.Panels.Select(p=>{
   float minAngle=180;
   foreach(var g in p.Canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(false))
   {
    if(!g.isActiveAndEnabled||g.color.a<=.001f||g.GetComponentsInParent<CanvasGroup>().Any(c=>c.alpha<.02f))continue;
    // Measure only rendered clipped bounds, in canvas-local coordinates.
    if(!FAA.Customization.FaaCanvasLocalGeometry.TryMatrix(g.transform,p.Canvas.transform,out var gm))continue;
    var rect=FAA.Customization.FaaCanvasLocalGeometry.TransformRect(g.rectTransform.rect,gm);
    for(Transform parent=g.transform.parent;parent!=null&&parent!=p.Canvas.transform;parent=parent.parent)
    {
     if(parent.GetComponent<UnityEngine.UI.RectMask2D>()==null&&parent.GetComponent<UnityEngine.UI.Mask>()==null)continue;
     if(!(parent is RectTransform mask)||!FAA.Customization.FaaCanvasLocalGeometry.TryMatrix(mask,p.Canvas.transform,out var mm))continue;
     var clip=FAA.Customization.FaaCanvasLocalGeometry.TransformRect(mask.rect,mm);
     rect=Rect.MinMaxRect(Mathf.Max(rect.xMin,clip.xMin),Mathf.Max(rect.yMin,clip.yMin),Mathf.Min(rect.xMax,clip.xMax),Mathf.Min(rect.yMax,clip.yMax));
    }
    if(rect.width<=0||rect.height<=0)continue;
    for(int i=0;i<4;i++)
    {
     Vector3 world=p.Canvas.transform.TransformPoint(new Vector3((i&1)==0?rect.xMin:rect.xMax,(i&2)==0?rect.yMin:rect.yMax,0));
     minAngle=Mathf.Min(minAngle,Vector3.Angle(w.CockpitFrame.forward,world-w.CockpitFrame.position));
    }
   }
   return new {p.Id,minAngle,p.Layout.yaw,p.Layout.distance,mode=p.Canvas.renderMode.ToString()};}).ToArray();
  rows.Add(new{frame=last,phase,launcher=new[]{brief.LauncherRect.anchoredPosition.x,brief.LauncherRect.anchoredPosition.y,brief.LauncherRect.rect.width,brief.LauncherRect.rect.height},visible=brief.LauncherRect.gameObject.activeInHierarchy,
   instruments=w.Modules.Where(m=>new[]{"airspeed","altitude","nr","torque","vertical-speed"}.Contains(m.Id)).Select(m=>new{m.Id,bounds=m.TryScreenBounds(w.View,out var b)?new[]{b.x,b.y,b.width,b.height}:null}).ToArray(),panels=panelRows});
 }
 catch(Exception e){cleanup();Debug.LogException(e);}
};
Canvas.willRenderCanvases+=callback;
return "Measuring Pilot Brief and full radar groups for 480 rendered frames across six states.";
