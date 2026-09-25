"""Verify Unity's real child-process bridge across short main-thread calls. No camera access."""
import json
from pathlib import Path
import subprocess
import time

ROOT=Path(__file__).resolve().parents[2]
KEY='FAA.Webcam.VerificationProcess'

def evaluate(code):
    process=subprocess.run(['unity','command','eval',code,'--format','json'],cwd=ROOT,capture_output=True,text=True,timeout=15)
    envelope=json.loads(process.stdout)
    if not envelope.get('success'):raise RuntimeError(envelope.get('errors'))
    result=envelope['data']['result']
    if not result.get('success'):raise RuntimeError(result)
    return result['result']

def receive(seconds):
    deadline=time.monotonic()+seconds
    while time.monotonic()<deadline:
        result=evaluate('var p=(FAA.Customization.FaaWebcamWorkerProcess)AppDomain.CurrentDomain.GetData("'+KEY+'");'
                        'bool received=p.TryRead(out string line);return new{p.Running,p.Failure,received,line};')
        if result['received']:return json.loads(result['line'])
        if not result['Running'] or result['Failure']:raise RuntimeError(result)
        time.sleep(.25)
    raise TimeoutError('Unity child-process response timed out')

def main():
    started=False
    try:
        start=evaluate('if(AppDomain.CurrentDomain.GetData("'+KEY+'")!=null)throw new Exception("Existing test process; clean it first.");'
                       'var camera=FAA.Customization.FaaSpatialWorkspace.Current.LaptopCamera;'
                       'if(!camera.TryResolveWorker(out string executable,out string script,out string model))throw new Exception("Missing local worker");'
                       'var p=new FAA.Customization.FaaWebcamWorkerProcess();p.Start(executable,script,model);'
                       'AppDomain.CurrentDomain.SetData("'+KEY+'",p);return new{executable,camera.CameraActive};')
        started=True
        ready=receive(25);assert ready.get('kind')=='ready' and ready.get('protocol')==2 and ready.get('features')=='multi-finger-v2',ready
        sent=evaluate('var p=(FAA.Customization.FaaWebcamWorkerProcess)AppDomain.CurrentDomain.GetData("'+KEY+'");'
                      'return p.Send(1,320,240,new byte[320*240*3]);')
        assert sent
        result=receive(5)
        assert result['kind']=='frame' and result['seq']==1 and result['hands']==[],result
        report={'passed':True,'camera_opened':False,'ready':ready,'blank_frame':result,'launch':start}
        output=ROOT/'artifacts/webcam-gestures/unity-worker-bridge.json';output.write_text(json.dumps(report,indent=2)+'\n')
        print(json.dumps(report,indent=2))
    finally:
        if started:evaluate('var p=AppDomain.CurrentDomain.GetData("'+KEY+'") as FAA.Customization.FaaWebcamWorkerProcess;'
                            'p?.Dispose();AppDomain.CurrentDomain.SetData("'+KEY+'",null);return "Disposed owned test worker";')

if __name__=='__main__':main()
