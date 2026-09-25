// Ordinary EventSystem raycasts + actual UGUI events; does not access the camera or OS permission database.
var w = FAA.Customization.FaaSpatialWorkspace.Current;
if (!Application.isPlaying || w == null || !w.Initialized) throw new Exception("Workspace must be running.");
var rows = new System.Collections.Generic.List<object>();
System.Action<string,bool> check = (name, passed) => rows.Add(new {name, passed});
var scales = w.Modules.ToDictionary(m=>m.Id,m=>m.Layout.scale);
var radar = w.Panels.ToDictionary(p=>p.Id,p=>p.Layout.Copy());
bool edit=w.EditMode, menu=w.MenuOpen, persist=w.PersistChanges;
string selected=w.SelectedId;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var dirtyField=typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",flags);
bool dirty=(bool)dirtyField.GetValue(w);
w.PersistChanges=false;
try
{
    w.SetEditMode(false); w.OpenMenu(); w.Select("altitude");
    foreach(var module in w.Modules) w.SetScale(module.Id,.72f);
    w.RefreshTransforms(); Canvas.ForceUpdateCanvases();
    var buttons=w.ControlsCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
    var larger=buttons.Single(b=>b.name=="Larger"); var smaller=buttons.Single(b=>b.name=="Smaller");
    check("Explicit +/- remain interactable while gestures are locked",larger.interactable&&smaller.interactable&&!w.EditMode);
    var c=w.ControlsCanvas;
    var raycaster=c.GetComponent<FAA.Customization.FaaCanvasPixelRaycaster>();
    check("Normal EventSystem uses canvas-pixel raycaster",raycaster!=null&&raycaster.isActiveAndEnabled);
    System.Func<UnityEngine.UI.Button,bool> click=button=>
    {
        var rect=button.GetComponent<RectTransform>();
        Camera ui=c.renderMode==RenderMode.ScreenSpaceOverlay?null:c.worldCamera;
        Vector2 pixel=RectTransformUtility.WorldToScreenPoint(ui,rect.TransformPoint(rect.rect.center));
        var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=pixel,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(data,hits);
        var hit=hits.FirstOrDefault(h=>h.gameObject==button.gameObject);
        if(hit.gameObject==null)return false;
        data.pointerCurrentRaycast=hit;data.pointerPressRaycast=hit;data.pressPosition=pixel;
        UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
        UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
        UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        return true;
    };
    var altitude=w.Modules.Single(m=>m.Id=="altitude");
    altitude.TryScreenBounds(w.View,out var before);
    bool hitPlus=click(larger);w.RefreshTransforms();altitude.TryScreenBounds(w.View,out var after);
    check("Real button hit changes saved altitude scale",hitPlus&&Mathf.Abs(altitude.Layout.scale-.77f)<.001f);
    check("Altitude graphic visibly grows, not just a percentage label",after.width>before.width+5f);
    bool hitMinus=click(smaller);w.RefreshTransforms();altitude.TryScreenBounds(w.View,out var restored);
    check("Minus button visibly restores original width",hitMinus&&Mathf.Abs(restored.width-before.width)<.01f);
    var slider=c.GetComponentInChildren<UnityEngine.UI.Slider>(true);slider.value=.9f;
    check("Size slider works while gestures remain locked",Mathf.Abs(altitude.Layout.scale-.9f)<.001f&&!w.EditMode);
    foreach(var module in w.Modules)w.SetScale(module.Id,.72f);
    bool hitAll=click(buttons.Single(b=>b.name=="All HUD Larger"));
    check("All-HUD plus resizes every registered non-conformal group",hitAll&&w.Modules.All(m=>Mathf.Abs(m.Layout.scale-.77f)<.001f));
    click(buttons.Single(b=>b.name=="All HUD Smaller"));
    check("All-HUD minus reverses enlargement",w.Modules.All(m=>Mathf.Abs(m.Layout.scale-.72f)<.001f));
    foreach(float scale in new[]{.4f,.72f,1f,1.6f})
    {
        foreach(var module in w.Modules)w.SetScale(module.Id,scale);
        for(int i=0;i<4;i++)w.RefreshTransforms();
        var ids=new[]{"altitude","glideslope","vertical-speed","nr"};
        var bounds=ids.ToDictionary(id=>id,id=>{w.Modules.Single(m=>m.Id==id).TryScreenBounds(w.View,out var r);return r;});
        check("Altitude and along-track separated at "+scale,!bounds["altitude"].Overlaps(bounds["glideslope"]));
        check("Along-track and vertical-speed separated at "+scale,!bounds["glideslope"].Overlaps(bounds["vertical-speed"]));
        check("Altitude and engine N2 separated at "+scale,!bounds["altitude"].Overlaps(bounds["nr"]));
        Vector3 position=altitude.Target.localPosition;
        for(int i=0;i<10;i++)w.RefreshTransforms();
        check("Layout does not drift across repeated updates at "+scale,Vector3.Distance(position,altitude.Target.localPosition)<.0001f);
    }
    check("Entire green status/FPA strip removed",!UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Any(x=>x.name=="FAA Rotorcraft Reference Controls"));
    var layer=UnityEngine.Object.FindFirstObjectByType<FAA.Customization.FaaRotorcraftConformalLayer>();
    check("Conformal renderer survives strip removal",layer!=null&&layer.enabled);
    check("Radar poses and sizes were not changed",w.Panels.All(p=>p.Layout.yaw==radar[p.Id].yaw&&p.Layout.elevation==radar[p.Id].elevation&&p.Layout.distance==radar[p.Id].distance&&p.Layout.scale==radar[p.Id].scale));
    check("Permission request and settings controls exist",buttons.Any(b=>b.name=="Request Camera Permission")&&buttons.Any(b=>b.name=="Camera Privacy Settings"));
    check("Verification has not started camera capture",!w.LaptopCamera.CameraActive&&!w.LaptopCamera.Starting);
    // Negative raycast: an existing CanvasGroup must still block hidden UI even in the fallback pixel space.
    var block=c.gameObject.AddComponent<CanvasGroup>();block.blocksRaycasts=false;
    try
    {
        var rect=larger.GetComponent<RectTransform>();
        var p=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=p};
        var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();raycaster.Raycast(data,hits);
        check("CanvasGroup blocking still prevents mouse hits",hits.Count==0);
    }
    finally {UnityEngine.Object.DestroyImmediate(block);}
}
catch(Exception error) { rows.Add(new{name="Fixture exception",passed=false,error=error.ToString()}); }
finally
{
    foreach(var module in w.Modules)module.Layout.scale=scales[module.Id];
    w.SetEditMode(edit);w.Select(selected);if(w.MenuOpen!=menu)w.ToggleMenu();w.RefreshTransforms();
    dirtyField.SetValue(w,dirty);w.PersistChanges=persist;
}
return rows;
