"""Actual XRUIInputModule clicks on the side style selector. No camera capture, OS mouse movement or flight commands."""
import json
from pathlib import Path
import subprocess
import time
ROOT=Path(__file__).resolve().parents[2]
KEY='FAA.SymbologyMouseVerification'
def evaluate(code):
    p=subprocess.run(['unity','command','eval',code,'--format','json'],cwd=ROOT,capture_output=True,text=True,timeout=20)
    e=json.loads(p.stdout)
    if not e.get('success'):raise RuntimeError(e)
    r=e['data']['result']
    if not r.get('success'):raise RuntimeError(r)
    return r['result']
def main():
    started=False
    reports=[]
    try:
        evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;if(w==null||!w.Initialized)throw new Exception("Not ready");'
                 'if(AppDomain.CurrentDomain.GetData("'+KEY+'")!=null)throw new Exception("Existing test context");'
                 'var f=typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);'
                 'AppDomain.CurrentDomain.SetData("'+KEY+'",new object[]{w.PersistChanges,w.EditMode,w.MenuOpen,w.SelectedId,w.CurrentSymbology,f.GetValue(w),UnityEngine.InputSystem.Mouse.current,null});'
                 'w.PersistChanges=false;w.SetEditMode(false);w.SetSymbologyVersion(FAA.Customization.FaaSymbologyVersion.Digital);'
                 'w.InspectUtility("settings");w.OpenSymbologySettings();return "Saved test context";')
        started=True
        time.sleep(.9)
        evaluate('var c=(object[])AppDomain.CurrentDomain.GetData("'+KEY+'");var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("FAA Symbology Test Mouse");c[7]=mouse;mouse.MakeCurrent();return "Temporary mouse ready";')
        for name,expected in [('Classic Analog Symbology','ClassicAnalog'),('Digital Symbology','Digital')]:
            setup=evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;var b=w.SettingsCanvas.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="'+name+'");'
                           'var r=b.GetComponent<RectTransform>();var p=RectTransformUtility.WorldToScreenPoint(w.View,r.TransformPoint(r.rect.center));'
                           'return new{x=p.x,y=p.y,before=w.CurrentSymbology.ToString(),module=UnityEngine.EventSystems.EventSystem.current.currentInputModule.GetType().Name};')
            for held in [0,1,0]:
                evaluate('var c=(object[])AppDomain.CurrentDomain.GetData("'+KEY+'");var m=(UnityEngine.InputSystem.Mouse)c[7];m.MakeCurrent();'
                         f'UnityEngine.InputSystem.InputSystem.QueueStateEvent(m,new UnityEngine.InputSystem.LowLevel.MouseState{{position=new Vector2({setup["x"]}f,{setup["y"]}f),buttons={held}}});return "Queued actual mouse input";')
                time.sleep(.15)
            state=evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;return new{version=w.CurrentSymbology.ToString(),w.EditMode,classicVisible=w.ClassicHud.Visible,moduleCount=w.Modules.Count,radars=w.Panels.Select(p=>new{p.Id,p.Layout.yaw}).ToArray()};')
            report={'button':name,'setup':setup,'result':state,'passed':state['version']==expected and not state['EditMode']}
            reports.append(report)
            if not report['passed']:raise AssertionError(report)
        output=ROOT/'artifacts/analog-symbology/actual-mouse-style-switch.json';output.write_text(json.dumps(reports,indent=2)+'\n');print(json.dumps(reports,indent=2))
    finally:
        if started:
            evaluate('var c=(object[])AppDomain.CurrentDomain.GetData("'+KEY+'");var m=c[7] as UnityEngine.InputSystem.Mouse;if(m!=null)UnityEngine.InputSystem.InputSystem.RemoveDevice(m);'
                     'var original=c[6] as UnityEngine.InputSystem.Mouse;if(original!=null&&original.added)original.MakeCurrent();'
                     'var w=FAA.Customization.FaaSpatialWorkspace.Current;w.SetSymbologyVersion((FAA.Customization.FaaSymbologyVersion)c[4]);w.SetEditMode((bool)c[1]);w.Select((string)c[3]);'
                     'w.SetSettingsPage(false);if(w.MenuOpen!=(bool)c[2])w.ToggleMenu();w.ReturnToForwardView();'
                     'typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(w,c[5]);w.PersistChanges=(bool)c[0];'
                     'AppDomain.CurrentDomain.SetData("'+KEY+'",null);return "Input and selection restored";')
if __name__=='__main__':main()
