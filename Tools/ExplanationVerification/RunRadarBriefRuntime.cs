var w=FAA.Customization.FaaSpatialWorkspace.Current;
var brief=UnityEngine.Object.FindFirstObjectByType<FAA.Explanations.ExplanationAssistantPanel>();
if(!Application.isPlaying||w==null||!w.Initialized||brief==null)throw new Exception("Workspace not ready");
if(w.LaptopCamera.CameraActive)throw new Exception("Do not disturb a user camera session");
var reports=new System.Collections.Generic.List<object>();Action<string,bool> check=(name,passed)=>reports.Add(new{name,passed});
var saved=w.InteractivePanels.ToDictionary(p=>p.Id,p=>p.Layout.Copy());
bool persist=w.PersistChanges,edit=w.EditMode,opened=brief.IsOpen;
string selected=w.SelectedId;var rotation=w.View.transform.rotation;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var dirty=typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",flags);object oldDirty=dirty.GetValue(w);
var cc=w.View.GetComponent<AircraftControl.Camera.AircraftCameraController>();
w.PersistChanges=false;
try
{
 w.RecallPanels();w.RefreshTransforms();
 check("Both original radar canvases are world-space",w.Panels.Count==2&&w.Panels.All(p=>p.IsSpatial&&p.Canvas.renderMode==RenderMode.WorldSpace));
 check("Recall no longer points radars at current head gaze",w.Panels.All(p=>Mathf.Abs(p.Layout.yaw)>=90));
 check("Complete radar footprints exceed the scope-only size",w.Panels.All(p=>p.ProtectedPhysicalSize.x>p.PhysicalWidth&&p.ProtectedPhysicalSize.y>p.PhysicalHeight));
 w.SetSpatialPanels(false);check("Legacy desktop toggle cannot restore obstructing forward radars",w.SpatialPanelsEnabled&&w.Panels.All(p=>p.IsSpatial));
 var weather=w.GetPanel("weather");w.Select("weather");w.PlaceSelected(0,0);w.SetScale("weather",1.6f);w.SetPanelDistance("weather",-3f);w.RefreshTransforms();
 float angle=Vector3.Angle(w.CockpitFrame.forward,weather.WorldCenter-w.CockpitFrame.position);
 float radius=FAA.Customization.FaaPeripheralPanelLayout.AngularRadius(weather.ProtectedPhysicalSize.x,weather.ProtectedPhysicalSize.y,weather.Layout.scale,weather.Layout.distance);
 check("Drag/preset and maximum size cannot enter forward cone",angle-radius>=62.8f);
 w.SetScale("weather",.7f);w.RecallPanels();
 brief.SetOpen(false);var layout=typeof(FAA.Explanations.ExplanationAssistantPanel).GetMethod("ApplyLayout",flags);layout.Invoke(brief,null);
 var position=brief.LauncherRect.anchoredPosition;
 for(int i=0;i<30;i++){w.View.transform.rotation=rotation*Quaternion.Euler(0,i*4,0);w.RefreshTransforms();layout.Invoke(brief,null);}
 check("Collapsed Pilot Brief remains at a fixed screen anchor",Vector2.Distance(position,brief.LauncherRect.anchoredPosition)<.001f&&position==new Vector2(0,18));
 check("Pilot Brief has corrected pixel-space hit testing",brief.LauncherRect.GetComponentInParent<FAA.Customization.FaaCanvasPixelRaycaster>()!=null);
 w.View.transform.rotation=rotation;w.RefreshTransforms();
 // Native XR-hand routing uses the same rays and ownership as this injected pointer test.
 foreach(var p in w.InteractivePanels)
 {
  if(p.Id=="settings")w.OpenMenu();if(p.Id=="camera-controls"&&!w.LaptopCamera.PanelOpen)w.LaptopCamera.TogglePanel();
  w.SetEditMode(true);w.RefreshTransforms();
  w.View.transform.rotation=Quaternion.LookRotation(p.WorldCenter-w.View.transform.position,w.CockpitFrame.up);w.RefreshTransforms();Canvas.ForceUpdateCanvases();
  var handle=p.Handle.GetComponent<RectTransform>();Vector3 target=handle.TransformPoint(handle.rect.center);
  var ray=new Ray(w.View.transform.position,(target-w.View.transform.position).normalized);
  var pointer=new FAA.Customization.FaaWorkspacePointerDispatcher(w,95);
  bool hit=pointer.TryHit(ray,out var h,out _);
  check(p.Id+": XR grip dispatches to layout instead of a menu click",hit&&h.gameObject==p.Handle&&!pointer.ShouldUseUi(h));
  double now=Time.realtimeSinceStartupAsDouble;Vector3 hand=ray.GetPoint(.35f);w.CancelManipulation();
  w.SubmitGesture(0,ray,hand,true,false,now);bool captured=w.SubmitGesture(0,ray,hand,true,true,now+.01);
  check(p.Id+": pinch acquires panel",captured&&w.HasGestureCapture(0));
  Vector3 before=p.WorldCenter;Vector3 shift=w.View.transform.up*.08f;
  w.SubmitGesture(0,new Ray(ray.origin+shift,ray.direction),hand+shift,true,true,now+.02);w.RefreshTransforms();
  check(p.Id+": hand motion drags complete panel",Vector3.Distance(before,p.WorldCenter)>.01f);
  w.SubmitGesture(0,ray,hand,false,false,now+.03);check(p.Id+": tracking loss releases drag",!w.HasGestureCapture(0));pointer.Cancel();
 }
 check("Gesture tests did not open laptop camera",!w.LaptopCamera.CameraActive&&!w.LaptopCamera.Starting);
}
finally
{
 w.CancelManipulation();cc.ResetView();w.View.transform.rotation=rotation;
 foreach(var p in w.InteractivePanels){var s=saved[p.Id];p.Layout.yaw=s.yaw;p.Layout.elevation=s.elevation;p.Layout.distance=s.distance;p.Layout.scale=s.scale;}
 brief.SetOpen(opened);w.SetEditMode(edit);w.Select(selected);w.RefreshTransforms();dirty.SetValue(w,oldDirty);w.PersistChanges=persist;
}
return reports;
