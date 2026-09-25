// Real UI handlers with injected rays/gesture samples. Not a physical XR-3 hand-tracking test.
var w=FAA.Customization.FaaSpatialWorkspace.Current;
if(!Application.isPlaying||w==null||!w.Initialized) throw new System.Exception("Workspace not ready.");
var reports=new System.Collections.Generic.List<object>();
System.Action<string,bool> check=(name,passed)=>reports.Add(new{name,passed});
var saved=new System.Collections.Generic.Dictionary<string,FAA.Customization.FaaSpatialLayoutEntry>();
foreach(var panel in w.Panels) saved[panel.Id]=panel.Layout.Copy();
foreach(var module in w.Modules) saved[module.Id]=module.Layout.Copy();
bool editing=w.EditMode,menu=w.MenuOpen,persist=w.PersistChanges,spatial=w.SpatialPanelsEnabled;
string selection=w.SelectedId;
var dirtyField=typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
bool dirty=(bool)dirtyField.GetValue(w);
var pointer=new FAA.Customization.FaaWorkspacePointerDispatcher(w,70);
w.PersistChanges=false;
try
{
    w.SetEditMode(true);w.OpenMenu();w.Select("airspeed");
    Canvas.ForceUpdateCanvases();
    var button=w.ControlsCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name=="Larger");
    var rect=button.GetComponent<RectTransform>();
    Camera ui=w.ControlsCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:w.View;
    Vector2 pixel=RectTransformUtility.WorldToScreenPoint(ui,rect.TransformPoint(rect.rect.center));
    Ray ray=w.View.ScreenPointToRay(pixel);
    bool hit=pointer.TryHit(ray,out var result,out var at);
    check("Hand ray resolves actual layout button",hit&&result.gameObject==button.gameObject);
    float before=w.GetEntry("airspeed").scale;
    pointer.Process(true,true,result,at);pointer.Process(true,false,result,at);
    check("Pinch release dispatches one actual Button click",Mathf.Abs(w.GetEntry("airspeed").scale-before-.05f)<.001f);
    before=w.GetEntry("airspeed").scale;
    pointer.Process(true,true,result,at);pointer.Cancel();
    check("Lost/cancelled pointer never clicks",w.GetEntry("airspeed").scale==before);
    w.SetEditMode(false);
    pointer.Process(true,true,result,at);pointer.Process(true,false,result,at);
    check("Explicit UI resizing works without enabling gestures",Mathf.Abs(w.GetEntry("airspeed").scale-before-.05f)<.001f&&!w.EditMode);
    w.SetEditMode(true);
    var air=w.Modules.First(m=>m.Id=="airspeed");
    check("Non-conformal selection has finite visible bounds",air.TryScreenBounds(w.View,out Rect bounds));
    var targetRay=w.View.ScreenPointToRay(bounds.center);
    check("Instrument ray intersects its own display plane",air.TryRay(targetRay,w.View,out float distance));
    double time=Time.realtimeSinceStartupAsDouble;
    Vector3 hand=w.View.transform.position+targetRay.direction*.3f;
    w.CancelManipulation();
    w.SubmitGesture(0,targetRay,hand,true,false,time);
    bool captured=w.SubmitGesture(0,targetRay,hand,true,true,time+.01);
    check("Pinch selects the airspeed module",captured&&w.SelectedId=="airspeed");
    before=w.GetEntry("airspeed").scale;
    w.SubmitGesture(0,targetRay,hand+w.View.transform.up*.1f,true,true,time+.02);
    check("One-hand vertical pinch motion resizes non-conformal module",w.GetEntry("airspeed").scale>before);
    w.SubmitGesture(0,targetRay,hand,true,false,time+.03);
    w.SetScale("airspeed",.72f);w.RefreshTransforms();Canvas.ForceUpdateCanvases();
    air.TryScreenBounds(w.View,out bounds);targetRay=w.View.ScreenPointToRay(bounds.center);
    air.TryRay(targetRay,w.View,out distance);Vector3 target=targetRay.GetPoint(distance);
    Vector3 left=w.View.transform.position+w.View.transform.forward*.2f-w.View.transform.right*.1f;
    Vector3 right=w.View.transform.position+w.View.transform.forward*.2f+w.View.transform.right*.1f;
    Ray leftRay=new Ray(left,(target-left).normalized),rightRay=new Ray(right,(target-right).normalized);
    w.CancelManipulation();w.SubmitGesture(0,leftRay,left,true,false,time+.04);w.SubmitGesture(1,rightRay,right,true,false,time+.04);
    w.SubmitGesture(0,leftRay,left,true,true,time+.05);w.SubmitGesture(1,rightRay,right,true,true,time+.06);
    before=w.GetEntry("airspeed").scale;
    w.SubmitGesture(0,leftRay,left-w.View.transform.right*.04f,true,true,time+.07);
    w.SubmitGesture(1,rightRay,right+w.View.transform.right*.04f,true,true,time+.08);
    check("Two-hand pinch also resizes non-conformal instruments",w.GetEntry("airspeed").scale>before);
    typeof(FAA.Customization.FaaSpatialWorkspace).GetMethod("OnApplicationFocus",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(w,new object[]{false});
    check("Application focus loss locks and cancels all manipulation",!w.EditMode&&!w.IsManipulating);
    w.SetEditMode(true);w.Select("traffic");w.SetSpatialPanels(true);w.PlaceSelected(0,-10);w.RefreshTransforms();Canvas.ForceUpdateCanvases();
    var panel=w.GetPanel("traffic");
    var handle=panel.Handle.GetComponent<FAA.Customization.FaaWorkspaceDragHandle>();
    var eventData=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
    eventData.button=UnityEngine.EventSystems.PointerEventData.InputButton.Left;
    eventData.position=w.View.WorldToScreenPoint(panel.WorldCenter);
    Vector3 oldCenter=panel.WorldCenter;
    handle.OnBeginDrag(eventData);eventData.position+=new Vector2(90,30);handle.OnDrag(eventData);handle.OnEndDrag(eventData);
    check("Mouse/controller UGUI grip moves world panel",Vector3.Distance(oldCenter,panel.WorldCenter)>.01f);
    check("Ending UGUI drag releases ownership",!w.IsManipulating);
}
finally
{
    pointer.Cancel();w.CancelManipulation();
    foreach(var savedEntry in saved)
    {
        var entry=w.GetEntry(savedEntry.Key);var source=savedEntry.Value;
        entry.yaw=source.yaw;entry.elevation=source.elevation;entry.distance=source.distance;entry.scale=source.scale;
    }
    w.SetSpatialPanels(spatial);w.SetEditMode(editing);w.Select(selection);
    if(w.MenuOpen!=menu)w.ToggleMenu();w.RefreshTransforms();
    dirtyField.SetValue(w,dirty);w.PersistChanges=persist;
}
return reports;
