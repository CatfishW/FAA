// Run in Play mode against the live UI. Synthetic gesture samples below are test inputs,
// not physical hand-tracking measurements. Restore every changed UI/camera field; no flight commands.
var w = FAA.Customization.FaaSpatialWorkspace.Current;
if (!Application.isPlaying || w == null || !w.Initialized) throw new System.Exception("Workspace not ready in Play mode.");
var reports = new System.Collections.Generic.List<object>();
System.Action<string,bool> check = (name,passed) => reports.Add(new {name,passed});
var saved = new System.Collections.Generic.Dictionary<string,FAA.Customization.FaaSpatialLayoutEntry>();
foreach (var p in w.Panels) saved[p.Id] = p.Layout.Copy();
foreach (var m in w.Modules) saved[m.Id] = m.Layout.Copy();
bool persist = w.PersistChanges, spatial = w.SpatialPanelsEnabled, editing = w.EditMode, menu = w.MenuOpen;
string selected = w.SelectedId;
var camera = w.View;
Vector3 cameraPosition = camera.transform.position;
Quaternion cameraRotation = camera.transform.rotation;
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var profileField = typeof(FAA.Customization.FaaSpatialWorkspace).GetField("profileKey", flags);
var dirtyField = typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty", flags);
string oldKey = (string)profileField.GetValue(w);
bool oldDirty = (bool)dirtyField.GetValue(w);
var conformal = UnityEngine.Object.FindFirstObjectByType<FAA.Customization.FaaRotorcraftConformalLayer>();
var conformalTransformScale = conformal.transform.localScale;
var traffic = w.GetPanel("traffic"); var weather = w.GetPanel("weather");
var trafficDisplay = traffic.Radar.GetComponentInChildren<TrafficRadar.TrafficRadarDisplay>(true);
bool wasFull = trafficDisplay.IsFullscreen;
w.PersistChanges = false;
try
{
    check("Both original radars registered", w.Panels.Count == 2);
    check("All ten non-conformal modules including slash-named NR", w.Modules.Count == 10 && w.GetEntry("nr") != null);
    check("Layout initially locked", !w.EditMode);
    w.SetSpatialPanels(true); w.SetEditMode(true);
    w.Select("traffic"); w.PlaceSelected(135f, -25f); w.RefreshTransforms();
    Vector3 local = w.CockpitFrame.InverseTransformPoint(traffic.WorldCenter);
    check("Panel can be placed behind the forward hemisphere", local.z < 0 && local.x > 0);
    check("World canvas, not forward screen overlay", traffic.Canvas.renderMode == RenderMode.WorldSpace && weather.Canvas.renderMode == RenderMode.WorldSpace);
    check("Physical panel size uses metre scale", Mathf.Abs(traffic.Radar.rect.width * traffic.Radar.lossyScale.x - .42f * traffic.Layout.scale) < .001f);
    Vector3 beforeLook = traffic.WorldCenter;
    camera.transform.rotation *= Quaternion.Euler(15, 85, 0); w.RefreshTransforms();
    check("Head turn cannot move the cockpit panel", Vector3.Distance(traffic.WorldCenter, beforeLook) < .001f);
    camera.transform.rotation = cameraRotation;
    w.PlaceSelected(-60, -65); w.RefreshTransforms();
    check("Panel can be positioned below the pilot", w.CockpitFrame.InverseTransformPoint(traffic.WorldCenter).y < -.8f);
    w.SetScale("airspeed", .6f);
    var air = w.Modules.First(m => m.Id == "airspeed");
    for (int i=0; i<12; i++) w.RefreshTransforms();
    check("Per-module scale is absolute, never compounded", Vector3.Distance(air.Target.localScale, air.BaseScale * .6f) < .00001f);
    check("Conformal layer unaffected by non-conformal sizing", conformal.transform.localScale == conformalTransformScale);
    w.SetScale("nr", .65f); check("Rotor/engine RPM resizes independently", Mathf.Abs(w.GetEntry("nr").scale - .65f)<.001f);
    float previous = traffic.Layout.scale;
    w.SetScale("traffic", float.NaN); check("Invalid scale rejected without losing last size", traffic.Layout.scale == previous);
    w.SetScale("traffic", 100f); check("Maximum scale bounded", traffic.Layout.scale == 1.6f);
    w.SetScale("traffic", -100f); check("Minimum scale remains visible/nonzero", traffic.Layout.scale == .4f);
    w.SetScale("traffic", 1f);
    w.PlaceSelected(0f, -10f); w.RefreshTransforms();
    camera.transform.rotation = Quaternion.LookRotation(traffic.WorldCenter - camera.transform.position, w.CockpitFrame.up);
    w.RefreshTransforms();
    double time = Time.realtimeSinceStartupAsDouble;
    Vector3 left = camera.transform.position + camera.transform.forward * .35f - camera.transform.right * .12f;
    Vector3 right = camera.transform.position + camera.transform.forward * .35f + camera.transform.right * .12f;
    Ray leftRay = new Ray(left, (traffic.WorldCenter-left).normalized);
    Ray rightRay = new Ray(right, (traffic.WorldCenter-right).normalized);
    string picked; float depth;
    check("Ray selection resolves radar in 3D", w.TryPickLayoutTarget(leftRay,out picked,out depth) && picked == "traffic");
    w.CancelManipulation();
    w.SubmitGesture(0,leftRay,left,true,false,time);
    bool captured = w.SubmitGesture(0,leftRay,left,true,true,time+.01);
    check("Deliberate first pinch acquires panel", captured && w.HasGestureCapture(0));
    Vector3 startCenter = traffic.WorldCenter;
    Ray movedRay = new Ray(left + camera.transform.right*.08f, leftRay.direction);
    w.SubmitGesture(0,movedRay,left+camera.transform.right*.08f,true,true,time+.02);
    check("One-hand pinch moves the actual radar canvas", Vector3.Distance(startCenter,traffic.WorldCenter)>.03f);
    w.SubmitGesture(0,movedRay,left,false,false,time+.03);
    check("Tracking loss cancels gesture ownership", !w.HasGestureCapture(0));
    w.SubmitGesture(0,movedRay,left,true,true,time+.04);
    check("Tracking return while pinched cannot ghost-grab", !w.HasGestureCapture(0));
    w.SubmitGesture(0,leftRay,left,true,false,time+.05);
    w.CancelManipulation(); w.PlaceSelected(0f,-10f); w.RefreshTransforms();
    camera.transform.rotation = Quaternion.LookRotation(traffic.WorldCenter-camera.transform.position,w.CockpitFrame.up);
    left = camera.transform.position + camera.transform.forward*.35f-camera.transform.right*.12f;
    right = camera.transform.position + camera.transform.forward*.35f+camera.transform.right*.12f;
    leftRay = new Ray(left,(traffic.WorldCenter-left).normalized); rightRay = new Ray(right,(traffic.WorldCenter-right).normalized);
    w.SubmitGesture(0,leftRay,left,true,false,time+.06); w.SubmitGesture(1,rightRay,right,true,false,time+.06);
    w.SubmitGesture(0,leftRay,left,true,true,time+.07); w.SubmitGesture(1,rightRay,right,true,true,time+.08);
    float pairScale=traffic.Layout.scale;
    w.SubmitGesture(0,leftRay,left-camera.transform.right*.08f,true,true,time+.09);
    w.SubmitGesture(1,rightRay,right+camera.transform.right*.08f,true,true,time+.10);
    check("Two-hand pinch resizes the selected panel", traffic.Layout.scale>pairScale);
    w.SubmitGesture(0,leftRay,left,true,false,time+.11);
    check("Ending either hand ends paired resizing without drag jump", !w.HasGestureCapture(0)&&!w.HasGestureCapture(1));
    w.SetEditMode(false);
    var lockedPose=traffic.WorldCenter;
    w.SubmitGesture(0,leftRay,left,true,false,time+.12); w.SubmitGesture(0,leftRay,left,true,true,time+.13);
    check("Locked layout cannot be grabbed", !w.IsManipulating && Vector3.Distance(lockedPose,traffic.WorldCenter)<.001f);
    w.SetEditMode(true); w.OpenMenu(); w.Select("airspeed");
    var buttons=w.ControlsCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
    float smallerBefore=w.GetEntry("airspeed").scale;
    buttons.First(b=>b.name=="Smaller").onClick.Invoke();
    check("Visible minus button changes selected instrument", Mathf.Abs(w.GetEntry("airspeed").scale-(smallerBefore-.05f))<.001f);
    var slider=w.ControlsCanvas.GetComponentInChildren<UnityEngine.UI.Slider>(true);
    slider.value=.9f; check("Size slider updates module", Mathf.Abs(w.GetEntry("airspeed").scale-.9f)<.001f);
    w.SetEditMode(false); smallerBefore=w.GetEntry("airspeed").scale;
    buttons.First(b=>b.name=="Smaller").onClick.Invoke(); check("Explicit size button works while gestures remain locked", Mathf.Abs(w.GetEntry("airspeed").scale-smallerBefore+.05f)<.001f && !w.EditMode);
    w.SetEditMode(true); w.Select("traffic"); w.PlaceSelected(170,-20);
    buttons.First(b=>b.name=="Recall").onClick.Invoke();
    check("Recall returns misplaced panels near current gaze", Vector3.Angle(camera.transform.forward,traffic.WorldCenter-camera.transform.position)<55f);
    w.Select("traffic"); w.SetScale("traffic",.8f); w.RefreshTransforms();
    float physicalBefore=traffic.Radar.rect.width*traffic.Radar.lossyScale.x;
    if(!trafficDisplay.IsFullscreen) trafficDisplay.ToggleFullscreen();
    w.RefreshTransforms();
    check("Map focus preserves physical panel size", Mathf.Abs(traffic.Radar.rect.width*traffic.Radar.lossyScale.x-physicalBefore)<.005f);
    trafficDisplay.BeginMapDrag(); trafficDisplay.PanMap(new Vector2(20,10)); trafficDisplay.EndMapDrag();
    check("Existing traffic map pan still starts and ends", !trafficDisplay.IsMapDragging);
    if(trafficDisplay.IsFullscreen!=wasFull) trafficDisplay.ToggleFullscreen();
    w.RefreshTransforms();
    var sanitizer=UnityEngine.Object.FindFirstObjectByType<FAA.Customization.FaaHudRuntimeSanitizer>();
    if(sanitizer!=null) sanitizer.SanitizeNow(); w.RefreshTransforms();
    check("HUD sanitizer preserves pilot-owned world canvases", traffic.Canvas.renderMode==RenderMode.WorldSpace&&weather.Canvas.renderMode==RenderMode.WorldSpace);
    w.SetSpatialPanels(false);
    check("Desktop fallback restores original canvas presentation", traffic.Canvas.renderMode!=RenderMode.WorldSpace && weather.Canvas.renderMode!=RenderMode.WorldSpace);
    w.SetSpatialPanels(true);
    string json=w.ExportProfile();
    check("Live layout profile serializes all registered modules", FAA.Customization.FaaSpatialLayoutMath.TryReadProfile(json,out var profile)&&profile.entries.Count==12);
    string temporaryKey="FAA.Workspace.Verification."+System.Guid.NewGuid().ToString("N");
    try
    {
        profileField.SetValue(w,temporaryKey); w.PersistChanges=true;
        w.SetScale("heading",.85f); w.SaveNow();
        check("Layout saves through actual PlayerPrefs API", PlayerPrefs.HasKey(temporaryKey)&&PlayerPrefs.GetString(temporaryKey).Contains("heading"));
    }
    finally { PlayerPrefs.DeleteKey(temporaryKey); PlayerPrefs.Save(); profileField.SetValue(w,oldKey); w.PersistChanges=false; }
    check("Original radar telemetry components retained", trafficDisplay!=null && weather.Radar.GetComponentInChildren<WeatherRadar.XPlaneOriginalWeatherRadarDisplay>(true)!=null);
    check("No simulated hand device advertised on desktop", w.NativeXr || !w.GetComponent<FAA.Customization.FaaWorkspaceGestureInput>().HandsTracked);
}
finally
{
    w.CancelManipulation();
    camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);
    if(trafficDisplay.IsFullscreen!=wasFull) trafficDisplay.ToggleFullscreen();
    foreach(var pair in saved)
    {
        var entry=w.GetEntry(pair.Key); var source=pair.Value;
        entry.yaw=source.yaw;entry.elevation=source.elevation;entry.distance=source.distance;entry.scale=source.scale;
    }
    w.SetSpatialPanels(spatial); w.SetEditMode(editing); w.Select(selected);
    if(w.MenuOpen!=menu) w.ToggleMenu();
    w.RefreshTransforms();
    profileField.SetValue(w,oldKey); dirtyField.SetValue(w,oldDirty); w.PersistChanges=persist;
}
return reports;
