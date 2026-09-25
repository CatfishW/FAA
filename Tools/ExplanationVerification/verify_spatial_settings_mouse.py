"""Drive the actual XRUIInputModule using a temporary Input System mouse. No OS mouse movement/camera access."""
import json
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[2]
KEY = 'FAA.SpatialSettingsMouseVerification'

def evaluate(code):
    result = subprocess.run(['unity','command','eval',code,'--format','json'],cwd=ROOT,capture_output=True,text=True,timeout=15)
    envelope = json.loads(result.stdout)
    if not envelope.get('success'):
        raise RuntimeError(envelope)
    value = envelope['data']['result']
    if not value.get('success'):
        raise RuntimeError(value)
    return value['result']

def main():
    started = False
    try:
        state = evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;'
            'if(!Application.isPlaying||w==null||!w.Initialized)throw new Exception("Workspace not ready");'
            'if(AppDomain.CurrentDomain.GetData("'+KEY+'")!=null)throw new Exception("Existing test context");'
            'if(w.View.GetComponent<AircraftControl.Camera.AircraftCameraController>().IsPanelInspectionActive)throw new Exception("Finish side inspection before running this input test");'
            'var dirty=typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);'
            'var data=new object[]{w.PersistChanges,w.EditMode,w.MenuOpen,w.SelectedId,w.GetEntry("altitude").scale,dirty.GetValue(w),UnityEngine.InputSystem.Mouse.current,null,w.GetPanel("settings").Layout.Copy()};'
            'AppDomain.CurrentDomain.SetData("'+KEY+'",data);w.PersistChanges=false;w.SetEditMode(false);w.OpenMenu();w.StowUtility("settings");w.InspectUtility("settings");w.SetSettingsPage(false);w.Select("altitude");w.SetScale("altitude",.72f);'
            'return "Context saved";')
        started = True
        time.sleep(1.0)
        setup = evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;'
            'var context=(object[])AppDomain.CurrentDomain.GetData("'+KEY+'");'
            'var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("FAA Resize Verification Mouse");'
            'context[7]=mouse;mouse.MakeCurrent();'
            'var button=w.SettingsCanvas.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="Larger");'
            'var rect=button.GetComponent<RectTransform>();'
            'var p=RectTransformUtility.WorldToScreenPoint(w.View,rect.TransformPoint(rect.rect.center));'
            'UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=p});'
            'return new{x=p.x,y=p.y,before=w.GetEntry("altitude").scale,module=UnityEngine.EventSystems.EventSystem.current.currentInputModule.GetType().Name};')
        time.sleep(.2)
        for buttons in [1, 0]:
            evaluate('var context=(object[])AppDomain.CurrentDomain.GetData("'+KEY+'");'
                'var mouse=(UnityEngine.InputSystem.Mouse)context[7];mouse.MakeCurrent();'
                f'UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{{position=new Vector2({setup["x"]}f,{setup["y"]}f),buttons={buttons}}});return "queued";')
            time.sleep(.2)
        result = evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;var a=w.Modules.Single(m=>m.Id=="altitude");'
            'a.TryScreenBounds(w.View,out var b);return new{after=a.Layout.scale,w.EditMode,width=b.width,currentMouse=UnityEngine.InputSystem.Mouse.current.name};')
        report = {'input':'temporary Unity Input System mouse; actual active UI input module, no manual Button invocation',
            'setup':setup,'result':result,'passed':abs(result['after']-.77)<.001 and not result['EditMode']}
        (ROOT/'artifacts/peripheral-stability/actual-mouse-click.json').write_text(json.dumps(report,indent=2)+'\n')
        print(json.dumps(report,indent=2))
        if not report['passed']:
            raise AssertionError('Input-module click did not resize the altitude instrument')
    finally:
        if started:
            evaluate('var context=(object[])AppDomain.CurrentDomain.GetData("'+KEY+'");'
                'var mouse=context[7] as UnityEngine.InputSystem.Mouse;if(mouse!=null)UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);'
                'var original=context[6] as UnityEngine.InputSystem.Mouse;if(original!=null&&original.added)original.MakeCurrent();'
                'var w=FAA.Customization.FaaSpatialWorkspace.Current;w.ReturnToForwardView();w.SetScale("altitude",(float)context[4]);'
                'w.SetEditMode((bool)context[1]);w.Select((string)context[3]);if(w.MenuOpen!=(bool)context[2])w.ToggleMenu();'
                'typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(w,context[5]);'
                'var pose=(FAA.Customization.FaaSpatialLayoutEntry)context[8];var p=w.GetPanel("settings").Layout;p.yaw=pose.yaw;p.elevation=pose.elevation;p.distance=pose.distance;p.scale=pose.scale;w.RefreshTransforms();w.PersistChanges=(bool)context[0];AppDomain.CurrentDomain.SetData("'+KEY+'",null);return "Original input and layout restored";')

if __name__ == '__main__':
    main()
