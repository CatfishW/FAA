// Current in-game components; artificial values only on an isolated disabled renderer fixture.
var w=FAA.Customization.FaaSpatialWorkspace.Current;
if(!Application.isPlaying||w==null||!w.Initialized)throw new Exception("Workspace not ready.");
if(w.LaptopCamera.CameraActive)throw new Exception("Do not interrupt active camera use.");
var checks=new System.Collections.Generic.List<object>();Action<string,bool> check=(name,passed)=>checks.Add(new{name,passed});
var version=w.CurrentSymbology;bool persist=w.PersistChanges,editing=w.EditMode,menu=w.MenuOpen;
bool local=w.ClassicInstrumentAttitude,reduced=w.ReducedSymbologyMotion,palette=w.UsePilotSymbologyColor;
string selection=w.SelectedId;
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var dirtyField=typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",flags);object dirty=dirtyField.GetValue(w);
string preferences=w.ExportSymbologyPreferences();
var panels=w.InteractivePanels.ToDictionary(p=>p.Id,p=>p.Layout.Copy());
var layer=UnityEngine.Object.FindFirstObjectByType<FAA.Customization.FaaRotorcraftConformalLayer>();
float fpa=layer.SelectedFpaDegrees;Vector3 layerScale=layer.transform.localScale;
var canvas=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).First(c=>c.name=="FAASymbologyCanvas");
var root=canvas.transform.Find("Second Interation GUI");bool rootActive=root.gameObject.activeSelf;
var mode=canvas.renderMode;float depth=canvas.planeDistance;
w.PersistChanges=false;
try
{
    w.SetSymbologyVersion(FAA.Customization.FaaSymbologyVersion.Digital);var digital=w.Modules.ToDictionary(m=>m.Id,m=>m.Layout.scale);
    int count=UnityEngine.Object.FindObjectsByType<FAA.Customization.FaaClassicAnalogHud>(FindObjectsInactive.Include).Length;
    check("One reusable Classic Analog renderer",count==1);
    w.OpenSymbologySettings();w.SetEditMode(false);
    var button=w.SettingsCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="Classic Analog Symbology");
    button.onClick.Invoke();
    check("In-game style button works while gestures are locked",w.CurrentSymbology==FAA.Customization.FaaSymbologyVersion.ClassicAnalog&&!w.EditMode);
    check("Reference-style round dials exist",w.ClassicHud.GetComponentsInChildren<FAA.Customization.FaaClassicGaugeGraphic>().Length==4);
    check("Ten classic module bindings replace the Digital bindings",w.Modules.Count==10&&w.Modules.All(m=>m.Target.name.StartsWith("Classic ")));
    check("Classic non-conformal attitude is explicit",w.UsesClassicAttitude&&w.ClassicHud.InstrumentRoots["attitude"].gameObject.activeSelf);
    check("Original digital graphics remain hidden without stopping telemetry",root.Cast<Transform>().All(t=>t.GetComponent<CanvasGroup>().alpha==0)&&root.gameObject.activeInHierarchy);
    check("Style selection does not change flight canvas mode/depth",canvas.renderMode==mode&&canvas.planeDistance==depth);
    check("No opaque full-screen background added",!w.ClassicHud.GetComponentsInChildren<UnityEngine.UI.Image>(true).Any());
    var classicDefault=w.GetEntry("airspeed").scale;
    w.SetScale("airspeed",.8f);w.RefreshTransforms();
    check("Individual classic dial size works",Mathf.Abs(w.ClassicHud.InstrumentRoots["airspeed"].localScale.x-.8f)<.001f);
    w.SetSymbologyVersion(FAA.Customization.FaaSymbologyVersion.Digital);
    check("Switching back restores Digital instrument sizes",w.Modules.All(m=>Mathf.Abs(m.Layout.scale-digital[m.Id])<.001f));
    check("Digital presentation restored exactly once",root.Cast<Transform>().All(t=>t.GetComponent<CanvasGroup>().alpha>0));
    w.SetSymbologyVersion(FAA.Customization.FaaSymbologyVersion.ClassicAnalog);
    check("Classic size remembers prior choice",Mathf.Abs(w.GetEntry("airspeed").scale-.8f)<.001f);
    for(int i=0;i<20;i++)w.SetSymbologyVersion((FAA.Customization.FaaSymbologyVersion)(i%2));
    check("Repeated switching creates no duplicate renderers",UnityEngine.Object.FindObjectsByType<FAA.Customization.FaaClassicAnalogHud>(FindObjectsInactive.Include).Length==count);
    w.SetSymbologyVersion(FAA.Customization.FaaSymbologyVersion.ClassicAnalog);w.SetClassicInstrumentAttitude(false);w.RefreshTransforms();
    check("Scene-aligned center hides only local attitude inset",!w.UsesClassicAttitude&&!w.ClassicHud.InstrumentRoots["attitude"].gameObject.activeSelf&&layer.enabled);
    w.SetClassicInstrumentAttitude(true);
    check("Conformal FPV/FPA and reference transform preserved",layer.enabled&&layer.SelectedFpaDegrees==fpa&&layer.transform.localScale==layerScale);
    w.SetEditMode(true);var sizedDial=w.ClassicHud.InstrumentRoots["airspeed"];float beforeSize=sizedDial.rect.width*sizedDial.lossyScale.x;
    bool armed=w.BeginWebcamSizing();w.ApplyWebcamSizing(1.1f);w.RefreshTransforms();
    check("Main HUD gesture sizing visibly enlarges classic dials",armed&&w.WebcamSizing&&sizedDial.rect.width*sizedDial.lossyScale.x>beforeSize*1.03f);
    w.SetSymbologyVersion(FAA.Customization.FaaSymbologyVersion.Digital);
    check("Switch cancels active gesture without leaking its scale",!w.WebcamSizing&&w.Modules.All(m=>Mathf.Abs(m.Layout.scale-digital[m.Id])<.001f));
    w.SetSymbologyVersion(FAA.Customization.FaaSymbologyVersion.ClassicAnalog);
    root.gameObject.SetActive(false);w.ClassicHud.ApplyLayout();
    check("Existing HUD declutter also hides Classic",w.ClassicHud.GetComponent<CanvasGroup>().alpha==0);
    root.gameObject.SetActive(true);w.ClassicHud.ApplyLayout();check("HUD declutter restores Classic",w.ClassicHud.GetComponent<CanvasGroup>().alpha==1);
    var originalAltitude=root.Find("Altimeter");bool originalAltitudeActive=originalAltitude.gameObject.activeSelf;
    originalAltitude.gameObject.SetActive(false);w.RefreshTransforms();
    check("Per-instrument declutter also hides the matching classic dial",!w.ClassicHud.InstrumentRoots["altitude"].gameObject.activeSelf);
    originalAltitude.gameObject.SetActive(originalAltitudeActive);w.RefreshTransforms();
    check("Per-instrument visibility restores without resetting size",w.ClassicHud.InstrumentRoots["altitude"].gameObject.activeSelf);
    check("Profile document validates and contains both versions",FAA.Customization.FaaSymbologyPreferences.TryParse(w.ExportSymbologyPreferences(),out var saved)&&saved.classic.Count==10&&saved.digital.Count==10);
    check("Invalid profile enum rejected without changing display",!w.SetSymbologyVersion((FAA.Customization.FaaSymbologyVersion)999)&&w.CurrentSymbology==FAA.Customization.FaaSymbologyVersion.ClassicAnalog);
    check("Radar/settings positions and sizes never changed",w.InteractivePanels.All(p=>p.Layout.yaw==panels[p.Id].yaw&&p.Layout.elevation==panels[p.Id].elevation&&p.Layout.distance==panels[p.Id].distance&&p.Layout.scale==panels[p.Id].scale));
    check("Camera remained off",!w.LaptopCamera.CameraActive&&!w.LaptopCamera.Starting);
    w.InspectUtility("settings");w.RefreshTransforms();
    check("Desktop panel inspection dims without moving or reprojecting Classic",w.ClassicHud.PanelInspectionOpacity<.2f&&canvas.renderMode==mode&&canvas.planeDistance==depth);
    w.ReturnToForwardView();w.RefreshTransforms();
    check("Returning forward immediately restores Classic opacity",w.ClassicHud.PanelInspectionOpacity==1f);
    // A disabled, temporary renderer fixture receives artificial test values. The actual live display is not injected.
    var fixture=new GameObject("Analog verification fixture",typeof(RectTransform));fixture.SetActive(false);
    var gauge=fixture.AddComponent<FAA.Customization.FaaClassicGaugeGraphic>();gauge.Configure(FAA.Customization.FaaClassicGaugeGraphic.Gauge.Airspeed,Color.green);
    gauge.Present(140,true,"",Color.green);
    check("Reference 140-kt sample produces 210-degree needle",Mathf.Abs(gauge.NeedleAngle-210)<.001f);
    gauge.Present(float.NaN,true,"",Color.green);check("Invalid sample immediately removes live needle",!gauge.DataValid);
    UnityEngine.Object.Destroy(fixture);
}
finally
{
    w.LaptopCamera.CancelSizing();w.CancelManipulation();root.gameObject.SetActive(rootActive);
    FAA.Customization.FaaSymbologyPreferences.TryParse(preferences,out var saved);
    foreach(var v in new[]{FAA.Customization.FaaSymbologyVersion.Digital,FAA.Customization.FaaSymbologyVersion.ClassicAnalog})
    {
        w.SetSymbologyVersion(v);var list=v==FAA.Customization.FaaSymbologyVersion.Digital?saved.digital:saved.classic;
        foreach(var s in list)if(w.GetEntry(s.id)!=null)w.GetEntry(s.id).scale=s.scale;
    }
    w.SetClassicInstrumentAttitude(local);w.SetReducedSymbologyMotion(reduced);w.SetPilotSymbologyColor(palette);w.SetSymbologyVersion(version);
    w.SetEditMode(editing);w.Select(selection);w.SetSettingsPage(false);if(w.MenuOpen!=menu)w.ToggleMenu();w.RefreshTransforms();
    dirtyField.SetValue(w,dirty);w.PersistChanges=persist;
}
return checks;
