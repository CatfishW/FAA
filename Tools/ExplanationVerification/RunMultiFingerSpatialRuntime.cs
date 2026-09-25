var w=FAA.Customization.FaaSpatialWorkspace.Current;
if(!Application.isPlaying||w==null||!w.Initialized)throw new Exception("Workspace not ready");
if(w.LaptopCamera.CameraActive)throw new Exception("Do not interrupt a user camera session");
var reports=new System.Collections.Generic.List<object>();
Action<string,bool> check=(name,passed)=>reports.Add(new{name,passed});
var saved=w.InteractivePanels.ToDictionary(p=>p.Id,p=>p.Layout.Copy());
var scales=w.Modules.ToDictionary(m=>m.Id,m=>m.Layout.scale);
bool persistence=w.PersistChanges,editing=w.EditMode,menu=w.MenuOpen,cameraPanel=w.LaptopCamera.PanelOpen,spatial=w.SpatialPanelsEnabled;
var rotation=w.View.transform.rotation;string selected=w.SelectedId;
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var dirty=typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",flags);object dirtyBefore=dirty.GetValue(w);
var pointer=new FAA.Customization.FaaWorkspacePointerDispatcher(w,70);
w.PersistChanges=false;
try
{
    check("Two independent utility panels and two original radars",w.UtilityPanels.Count==2&&w.Panels.Count==2);
    check("Settings and hand studio are real World Space canvases",w.SettingsCanvas.renderMode==RenderMode.WorldSpace&&w.LaptopCamera.CameraCanvas.renderMode==RenderMode.WorldSpace);
    check("Utilities are not descendants of the overlay HUD",!w.SettingsCanvas.transform.IsChildOf(w.ControlsCanvas.transform)&&!w.LaptopCamera.CameraCanvas.transform.IsChildOf(w.ControlsCanvas.transform));
    w.OpenMenu();if(!w.LaptopCamera.PanelOpen)w.LaptopCamera.TogglePanel();
    w.StowUtility("settings");w.StowUtility("camera-controls");
    check("Stowed utilities clear the protected forward view",Mathf.Abs(w.GetPanel("settings").Layout.yaw)>=90&&Mathf.Abs(w.GetPanel("camera-controls").Layout.yaw)>=90);
    var settings=w.GetPanel("settings");Vector3 oldCenter=settings.WorldCenter;
    w.View.transform.rotation*=Quaternion.Euler(12,70,0);w.RefreshTransforms();
    check("Head rotation does not drag settings along",Vector3.Distance(oldCenter,settings.WorldCenter)<.001f);
    w.View.transform.rotation=rotation;
    w.SetSpatialPanels(false);w.RefreshTransforms();
    check("Desktop radar toggle cannot dock settings back into HUD",settings.IsSpatial&&settings.Canvas.renderMode==RenderMode.WorldSpace);
    w.SetSpatialPanels(true);w.BringUtilityHere("settings");w.StowUtility("camera-controls");w.SetSettingsPage(false);w.SetEditMode(false);w.Select("altitude");w.SetScale("altitude",.72f);
    // Inspect the side, rather than moving a settings panel into the pilot's forward view.
    w.View.transform.rotation=Quaternion.LookRotation(settings.WorldCenter-w.View.transform.position,w.CockpitFrame.up);
    Canvas.ForceUpdateCanvases();
    var buttons=w.SettingsCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
    check("Tabbed instrument and spatial controls exist",buttons.Any(b=>b.name=="HUD Tab")&&buttons.Any(b=>b.name=="Panels Tab"));
    var bigger=buttons.Single(b=>b.name=="Larger");var rect=bigger.GetComponent<RectTransform>();
    Vector2 pixel=RectTransformUtility.WorldToScreenPoint(w.View,rect.TransformPoint(rect.rect.center));
    Ray ray=w.View.ScreenPointToRay(pixel);
    bool hit=pointer.TryHit(ray,out var result,out var at);
    check("Spatial button hit resolves through existing EventSystem",hit&&result.gameObject==bigger.gameObject);
    pointer.Process(true,true,result,at);pointer.Process(true,false,result,at);
    check("World panel button grows actual altitude while locked",Mathf.Abs(w.GetEntry("altitude").scale-.77f)<.001f&&!w.EditMode);
    float before=w.GetEntry("altitude").scale;pointer.Process(true,true,result,at);pointer.Cancel();
    check("Cancelled pinch does not click",w.GetEntry("altitude").scale==before);
    var slider=w.SettingsCanvas.GetComponentInChildren<UnityEngine.UI.Slider>(true);slider.value=.9f;
    check("World-space slider resizes selected instrument",Mathf.Abs(w.GetEntry("altitude").scale-.9f)<.001f);
    w.SetEditMode(true);w.RefreshTransforms();
    var handle=settings.Handle.GetComponent<FAA.Customization.FaaWorkspaceDragHandle>();
    var eventData=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=w.View.WorldToScreenPoint(settings.WorldCenter),button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
    oldCenter=settings.WorldCenter;handle.OnBeginDrag(eventData);eventData.position+=new Vector2(80,20);handle.OnDrag(eventData);handle.OnEndDrag(eventData);
    check("Dragging utility handle moves real 3D settings panel",Vector3.Distance(oldCenter,settings.WorldCenter)>.01f&&!w.IsManipulating);
    float width=settings.Radar.rect.width*settings.Radar.lossyScale.x;w.ResizeUtility("settings",.1f);w.RefreshTransforms();
    check("Settings physical size adjustable independently",settings.Radar.rect.width*settings.Radar.lossyScale.x>width);
    var camera=w.GetPanel("camera-controls");float yaw=camera.Layout.yaw;w.Select("settings");w.PlaceSelected(160,-40);w.RefreshTransforms();
    check("Settings can be placed behind and below the pilot",settings.Layout.yaw==160&&settings.Layout.elevation==-40&&camera.Layout.yaw==yaw);
    w.SetSettingsPage(true);check("Spatial tab hides instrument-size controls",!bigger.gameObject.activeInHierarchy);
    w.SetSettingsPage(false);check("Instrument controls return on its tab",bigger.gameObject.activeInHierarchy);
    var camButtons=w.LaptopCamera.CameraCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
    camButtons.Single(b=>b.name=="Open Palms Mode").onClick.Invoke();
    check("Open-palms recognition mode selectable without opening camera",w.LaptopCamera.GestureMode==FAA.Customization.FaaMultiFingerResize.GestureMode.OpenPalms&&!w.LaptopCamera.CameraActive);
    camButtons.Single(b=>b.name=="Multi Finger Mode").onClick.Invoke();
    check("Any-finger pinch recognition selectable",w.LaptopCamera.GestureMode==FAA.Customization.FaaMultiFingerResize.GestureMode.MultiFingerPinch);
    check("Native permission and stop controls retained",camButtons.Any(b=>b.name=="Request Camera Permission")&&camButtons.Any(b=>b.name=="Webcam Start Stop"));
    var geometry=UnityEngine.Object.FindFirstObjectByType<FAA.Customization.FaaRotorcraftConformalLayer>();var scale=geometry.transform.localScale;var fpa=geometry.SelectedFpaDegrees;
    w.BeginWebcamSizing();w.ApplyWebcamSizing(1.1f);w.RefreshTransforms();w.EndWebcamSizing();
    check("Gesture group resize leaves calibrated conformal geometry unchanged",scale==geometry.transform.localScale&&fpa==geometry.SelectedFpaDegrees);
    check("Removed green toolbar stays removed",!UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Any(c=>c.name=="FAA Rotorcraft Reference Controls"));
    string json=w.ExportProfile();check("Persisted profile contains both utility transforms",FAA.Customization.FaaSpatialLayoutMath.TryReadProfile(json,out var profile)&&profile.entries.Count==14&&profile.entries.Any(e=>e.id=="camera-controls")&&profile.entries.Any(e=>e.id=="settings"));
    w.SetEditMode(false);w.CancelManipulation();check("Lock cancels all utility gesture ownership",!w.IsManipulating);
    w.StowUtility("settings");w.StowUtility("camera-controls");w.RefreshTransforms();
    check("Camera remained off throughout verification",!w.LaptopCamera.CameraActive&&!w.LaptopCamera.Starting);
}
finally
{
    pointer.Cancel();w.CancelManipulation();w.LaptopCamera.CancelSizing();w.View.transform.rotation=rotation;
    foreach(var p in w.InteractivePanels){var s=saved[p.Id];p.Layout.yaw=s.yaw;p.Layout.elevation=s.elevation;p.Layout.distance=s.distance;p.Layout.scale=s.scale;}
    foreach(var m in w.Modules)m.Layout.scale=scales[m.Id];
    w.SetSpatialPanels(spatial);w.SetEditMode(editing);w.Select(selected);w.SetSettingsPage(false);
    if(w.MenuOpen!=menu)w.ToggleMenu();if(w.LaptopCamera.PanelOpen!=cameraPanel)w.LaptopCamera.TogglePanel();
    w.RefreshTransforms();dirty.SetValue(w,dirtyBefore);w.PersistChanges=persistence;
}
return reports;
