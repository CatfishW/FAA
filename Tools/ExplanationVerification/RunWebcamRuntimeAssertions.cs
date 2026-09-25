// Play-mode integration with injected gestures and a blank local worker frame. No real webcam access.
var w=FAA.Customization.FaaSpatialWorkspace.Current;
if(!Application.isPlaying||w==null||!w.Initialized)throw new System.Exception("Workspace not ready in Play mode.");
var webcam=w.LaptopCamera;
if(webcam.CameraActive||webcam.Starting)throw new System.Exception("Do not interrupt a user camera session for tests.");
var reports=new System.Collections.Generic.List<object>();
System.Action<string,bool> check=(name,passed)=>reports.Add(new{name,passed});
var baseline=w.Modules.ToDictionary(m=>m.Id,m=>m.Layout.scale);
var radar=w.Panels.ToDictionary(p=>p.Id,p=>p.Layout.Copy());
var conformal=UnityEngine.Object.FindFirstObjectByType<FAA.Customization.FaaRotorcraftConformalLayer>();
Vector3 conformalScale=conformal.transform.localScale;
float fpa=conformal.SelectedFpaDegrees;
bool persist=w.PersistChanges,editing=w.EditMode,menu=w.MenuOpen,panel=webcam.PanelOpen;
string selected=w.SelectedId;
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var dirtyField=typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",flags);
bool dirty=(bool)dirtyField.GetValue(w);
w.PersistChanges=false;
try
{
    check("Camera source is bound but OFF by default",webcam!=null&&!webcam.CameraActive&&!webcam.Starting&&!webcam.RecognitionReady);
    check("Local worker/model resolve without downloads",webcam.TryResolveWorker(out string executable,out string script,out string model));
    w.OpenMenu();
    var button=w.ControlsCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name=="Laptop Camera");
    if(!webcam.PanelOpen)button.onClick.Invoke();
    check("Opening camera panel does not activate webcam",webcam.PanelOpen&&!webcam.CameraActive&&!webcam.Starting);
    check("Start/stop and device-selection controls exist",webcam.GetComponent<FAA.Customization.FaaLaptopCameraGestures>()!=null&&
        w.ControlsCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(b=>b.name=="Webcam Start Stop")&&
        w.ControlsCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(b=>b.name=="Webcam Next Device"));
    w.SetEditMode(false);check("Locked HUD rejects webcam resize",!w.BeginWebcamSizing());
    w.SetEditMode(true);
    check("Unlocked main HUD accepts gesture baseline",w.BeginWebcamSizing());
    w.ApplyWebcamSizing(1.2f);
    check("All registered non-conformal modules enlarge proportionally",w.Modules.All(m=>Mathf.Abs(m.Layout.scale-baseline[m.Id]*1.2f)<.001f));
    w.ApplyWebcamSizing(.8f);
    check("Closing hands shrinks relative to original baseline, not accumulated scale",w.Modules.All(m=>Mathf.Abs(m.Layout.scale-baseline[m.Id]*.8f)<.001f));
    check("Radars retain positions depths and sizes",w.Panels.All(p=>p.Layout.yaw==radar[p.Id].yaw&&p.Layout.elevation==radar[p.Id].elevation&&
        p.Layout.distance==radar[p.Id].distance&&p.Layout.scale==radar[p.Id].scale));
    check("Calibrated conformal geometry and FPA remain unchanged",conformal.transform.localScale==conformalScale&&conformal.SelectedFpaDegrees==fpa);
    float previous=w.GetEntry("airspeed").scale;w.ApplyWebcamSizing(float.NaN);
    check("Nonfinite gesture scale is ignored",w.GetEntry("airspeed").scale==previous);
    w.ApplyWebcamSizing(100f);check("Global enlargement respects instrument upper bounds",w.Modules.All(m=>m.Layout.scale<=1.6001f));
    w.ApplyWebcamSizing(.001f);check("Global shrink cannot hide instruments",w.Modules.All(m=>m.Layout.scale>=.3999f));
    w.EndWebcamSizing();check("Release ends resize ownership",!w.WebcamSizing);
    foreach(var m in w.Modules)w.SetScale(m.Id,baseline[m.Id]);
    w.BeginWebcamSizing();w.SetScale("airspeed",.9f);
    check("Manual size adjustment cancels camera gesture",!w.WebcamSizing);
    w.BeginWebcamSizing();w.SetEditMode(false);
    check("Lock immediately cancels active resize",!w.WebcamSizing);
    w.SetEditMode(true);w.BeginWebcamSizing();webcam.StopCamera();
    check("Stop releases camera source and resize ownership",!w.WebcamSizing&&!webcam.CameraActive&&!webcam.RecognitionReady);
    w.BeginWebcamSizing();typeof(FAA.Customization.FaaLaptopCameraGestures).GetMethod("OnApplicationFocus",flags).Invoke(webcam,new object[]{false});
    check("Focus loss cancels camera gesture without auto-resume",!w.WebcamSizing&&!webcam.CameraActive&&!webcam.Starting);
    // Worker process startup is checked separately across Editor calls, never by blocking its main thread.
    check("Worker test never activated a camera",!webcam.CameraActive&&!webcam.Starting);
}
finally
{
    webcam.StopCamera();w.EndWebcamSizing();
    foreach(var m in w.Modules)m.Layout.scale=baseline[m.Id];
    w.SetEditMode(editing);w.Select(selected);if(w.MenuOpen!=menu)w.ToggleMenu();
    if(webcam.PanelOpen!=panel)webcam.TogglePanel();w.RefreshTransforms();
    dirtyField.SetValue(w,dirty);w.PersistChanges=persist;
}
return reports;
