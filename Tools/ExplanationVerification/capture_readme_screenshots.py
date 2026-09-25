"""Capture actual Unity application views for README. Never opens camera or modifies flight telemetry."""
import json
from pathlib import Path
import shutil
import subprocess
import time

ROOT=Path(__file__).resolve().parents[2]
DEST=ROOT/'docs/screenshots/2026-09-25'
KEY='FAA.ReadmeScreenshotState'

def command(*args):
    p=subprocess.run(['unity','command',*args,'--format','json'],cwd=ROOT,capture_output=True,text=True,timeout=35)
    envelope=json.loads(p.stdout)
    if not envelope.get('success'):raise RuntimeError(envelope)
    result=envelope['data']['result']
    if isinstance(result,dict) and result.get('success') is False:raise RuntimeError(result)
    return result

def evaluate(code):return command('eval',code)

def capture(name):
    result=command('capture_game_view','--source','screen','--width','1920','--height','1080',
                   '--save_path','artifacts/vsi-reference-readme/'+name)
    source=Path(result['savedPath'])
    if not source.is_absolute():source=ROOT/source
    shutil.copy2(source,DEST/name)
    print(json.dumps({'path':str((DEST/name).relative_to(ROOT)),'source':'Actual Unity Game view, camera OFF','bytes':(DEST/name).stat().st_size}))

def main():
    DEST.mkdir(parents=True,exist_ok=True)
    saved=False
    try:
        evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;if(w==null||!w.Initialized||w.LaptopCamera.CameraActive||w.LaptopCamera.Starting)throw new Exception("Need ready workspace and camera off.");'
                 'if(AppDomain.CurrentDomain.GetData("'+KEY+'")!=null)throw new Exception("Existing capture context");'
                 'var field=typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);'
                 'AppDomain.CurrentDomain.SetData("'+KEY+'",new object[]{w.PersistChanges,w.EditMode,w.MenuOpen,w.LaptopCamera.PanelOpen,w.CurrentSymbology,w.SelectedId,field.GetValue(w)});'
                 'w.PersistChanges=false;w.SetEditMode(false);if(w.MenuOpen)w.ToggleMenu();if(w.LaptopCamera.PanelOpen)w.LaptopCamera.TogglePanel();w.ReturnToForwardView();return "Captured layout preferences; camera remains off";')
        saved=True
        evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;w.SetSymbologyVersion(FAA.Customization.FaaSymbologyVersion.ClassicAnalog);w.ReturnToForwardView();return "Classic";')
        time.sleep(1.2);capture('classic-terrain.png')
        evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;w.SetSymbologyVersion(FAA.Customization.FaaSymbologyVersion.Digital);return "Digital";')
        time.sleep(.7);capture('digital-terrain.png')
        evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;w.SetSymbologyVersion(FAA.Customization.FaaSymbologyVersion.ClassicAnalog);w.InspectUtility("settings");w.OpenSymbologySettings();return "Style selector side inspection";')
        time.sleep(1.2);capture('symbology-settings.png')
        evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;w.OpenDataSourceSettings();return "Data source diagnostics";')
        time.sleep(1.2);capture('data-source-discovery.png')
        evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;w.InspectUtility("camera-controls");return "Hand Studio OFF";')
        time.sleep(1.6);capture('hand-studio-off.png')
        evaluate('var w=FAA.Customization.FaaSpatialWorkspace.Current;w.InspectPanel("traffic");return "Traffic side inspection";')
        time.sleep(1.8);capture('traffic-side-panel.png')
    finally:
        if saved:
            evaluate('var c=(object[])AppDomain.CurrentDomain.GetData("'+KEY+'");var w=FAA.Customization.FaaSpatialWorkspace.Current;'
                     'w.SetSymbologyVersion((FAA.Customization.FaaSymbologyVersion)c[4]);w.Select((string)c[5]);w.SetEditMode((bool)c[1]);w.SetSettingsPage(false);'
                     'if(w.MenuOpen!=(bool)c[2])w.ToggleMenu();if(w.LaptopCamera.PanelOpen!=(bool)c[3])w.LaptopCamera.TogglePanel();w.ReturnToForwardView();'
                     'typeof(FAA.Customization.FaaSpatialWorkspace).GetField("dirty",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(w,c[6]);'
                     'w.PersistChanges=(bool)c[0];AppDomain.CurrentDomain.SetData("'+KEY+'",null);return "Preferences restored; camera remains off";')

if __name__=='__main__':main()
