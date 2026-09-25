"""Actual Input System right button -> workspace drag -> camera arbitration. No OS mouse motion/webcam."""
import json
from pathlib import Path
import subprocess
import time
ROOT=Path(__file__).resolve().parents[2]
KEY='FAA.RightDrag.Verification'
def evaluate(code):
 p=subprocess.run(['unity','command','eval',code,'--format','json'],cwd=ROOT,capture_output=True,text=True,timeout=20)
 e=json.loads(p.stdout)
 if not e.get('success'):raise RuntimeError(e)
 r=e['data']['result']
 if not r.get('success'):raise RuntimeError(r)
 return r['result']
def main():
 saved=False;reports=[]
 try:
  evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;if(w==null||!w.Initialized||w.LaptopCamera.CameraActive)throw new Exception("Workspace not ready or camera in use");'
   'var dirty=typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);'
   'var data=new object[]{w.PersistChanges,w.EditMode,w.MenuOpen,w.SelectedId,dirty.GetValue(w),UnityEngine.InputSystem.Mouse.current,null,w.InteractivePanels.ToDictionary(p=>p.Id,p=>p.Layout.Copy()),UnityEditor.EditorWindow.focusedWindow};'
   'AppDomain.CurrentDomain.SetData("'+KEY+'",data);w.PersistChanges=false;w.SetEditMode(false);'
   'var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("FAA Right Drag Test");data[6]=mouse;mouse.MakeCurrent();'
   'var t=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");UnityEditor.EditorWindow.GetWindow(t).Focus();return "saved";')
  saved=True
  for panel_id in ['settings','weather','traffic','camera-controls']:
   evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;w.ProcessRightDrag(false,Vector2.zero,true);w.RecallPanels();w.InspectPanel("'+panel_id+'");return "inspect";')
   time.sleep(.9)
   setup=evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;var p=w.GetPanel("'+panel_id+'");var pos=w.View.WorldToScreenPoint(p.WorldCenter);'
    'var state=(object[])AppDomain.CurrentDomain.GetData("'+KEY+'");var mouse=(UnityEngine.InputSystem.Mouse)state[6];mouse.MakeCurrent();'
    'UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=pos});'
    'return new{x=pos.x,y=pos.y,focus=Application.isFocused,yaw=p.Layout.yaw,elevation=p.Layout.elevation};')
   time.sleep(.15)
   for dx,dy,buttons in [(0,0,2),(-110,50,2)]:
    evaluate('var state=(object[])AppDomain.CurrentDomain.GetData("'+KEY+'");var mouse=(UnityEngine.InputSystem.Mouse)state[6];mouse.MakeCurrent();'
      f'UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{{position=new Vector2({setup["x"]+dx}f,{setup["y"]+dy}f),buttons={buttons}}});return "queued";')
    time.sleep(.2)
   held=evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;var p=w.GetPanel("'+panel_id+'");var cc=w.View.GetComponent<AircraftControl.Camera.AircraftCameraController>();'
     'return new{w.RightDragActive,w.RightDragPanel,w.EditMode,cc.PanelPointerCaptured,cc.IsLookActive,p.Layout.yaw,p.Layout.elevation};')
   evaluate('var state=(object[])AppDomain.CurrentDomain.GetData("'+KEY+'");var mouse=(UnityEngine.InputSystem.Mouse)state[6];'
     f'UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{{position=new Vector2({setup["x"]-110}f,{setup["y"]+50}f),buttons=0}});return "released";')
   time.sleep(.15)
   released=evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;return new{w.RightDragActive,cameraCapture=w.View.GetComponent<AircraftControl.Camera.AircraftCameraController>().PanelPointerCaptured};')
   passed=held['RightDragActive'] and held['RightDragPanel']==panel_id and held['PanelPointerCaptured'] and not held['IsLookActive'] and not held['EditMode'] and not released['RightDragActive'] and not released['cameraCapture'] and (abs(held['yaw']-setup['yaw'])>.1 or abs(held['elevation']-setup['elevation'])>.1)
   reports.append({'panel':panel_id,'setup':setup,'held':held,'released':released,'passed':passed})
  out=ROOT/'artifacts/radar-brief-fix';out.mkdir(parents=True,exist_ok=True);(out/'actual-right-drag.json').write_text(json.dumps(reports,indent=2)+'\n');print(json.dumps(reports,indent=2))
  assert all(r['passed'] for r in reports),'Right-button drag integration failed'
 finally:
  if saved:evaluate('var d=(object[])AppDomain.CurrentDomain.GetData("'+KEY+'");var w=FAA.Customization.FaaSpatialWorkspace.Current;'
   'w.CancelManipulation();w.ReturnToForwardView();var mouse=d[6] as UnityEngine.InputSystem.Mouse;if(mouse!=null)UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);'
   'var original=d[5] as UnityEngine.InputSystem.Mouse;if(original!=null&&original.added)original.MakeCurrent();'
   'var poses=(System.Collections.Generic.Dictionary<string,FAA.Customization.FaaSpatialLayoutEntry>)d[7];'
   'foreach(var p in w.InteractivePanels){var s=poses[p.Id];p.Layout.yaw=s.yaw;p.Layout.elevation=s.elevation;p.Layout.distance=s.distance;p.Layout.scale=s.scale;}'
   'w.SetEditMode((bool)d[1]);w.Select((string)d[3]);if(w.MenuOpen!=(bool)d[2])w.ToggleMenu();'
   'typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(w,d[4]);w.PersistChanges=(bool)d[0];'
   'AppDomain.CurrentDomain.SetData("'+KEY+'",null);return "restored";')
if __name__=='__main__':main()
