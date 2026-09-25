"""Observe 480 real Unity-render frames without blocking the Editor thread or capturing images."""
import json
from pathlib import Path
import subprocess
import time

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'artifacts/peripheral-stability'

def evaluate(code):
    p=subprocess.run(['unity','command','eval',code,'--format','json'],cwd=ROOT,capture_output=True,text=True,timeout=45)
    response=json.loads(p.stdout)
    if not response.get('success'):raise RuntimeError(response)
    result=response['data']['result']
    if not result.get('success'):raise RuntimeError(result)
    return result['result']

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    target=OUT/'after-render-frames.json'
    error=OUT/'frame-probe-error.txt'
    for old in [target,error]:
        if old.exists():old.unlink()
    started=False
    try:
        print(evaluate((ROOT/'Tools/ExplanationVerification/RunPeripheralFrameProbe.cs').read_text()),flush=True)
        started=True
        deadline=time.monotonic()+100
        while not target.exists() and not error.exists() and time.monotonic()<deadline:time.sleep(.25)
        if error.exists():raise RuntimeError(error.read_text())
        if not target.exists():raise TimeoutError('Render probe did not complete')
        rows=json.loads(target.read_text())
        assert len(rows)==360,len(rows)
        reports=[]
        for phase in range(6):
            phase_rows=[r for r in rows if r['phase']==phase]
            for name in ['airspeed','altitude','nr','vertical-speed','torque','glideslope']:
                samples=[next(m for m in r['modules'] if m['Id']==name) for r in phase_rows]
                assert all(m['box'] for m in samples)
                spans=[max(m['box'][i] for m in samples)-min(m['box'][i] for m in samples) for i in range(4)]
                reports.append({'phase':phase,'module':name,'xywh_peak_to_peak_pixels':spans,'passed':max(spans)<.25})
        states={tuple((c['name'],c['mode'],c['planeDistance']) for c in r['canvases']) for r in rows}
        clearance=min(p['minCornerAngle'] for r in rows for p in r['panels'])
        report={'measured_render_samples':len(rows),'observed_frames':480,'phases':6,
                'worst_geometry_motion_pixels':max(max(r['xywh_peak_to_peak_pixels']) for r in reports),
                'flight_canvas_configurations':len(states),'minimum_actual_panel_corner_angle_degrees':clearance,
                'per_phase':reports,'camera_opened':False,
                'passed':all(r['passed'] for r in reports) and len(states)==1 and clearance>=60}
        (OUT/'frame-stability-summary.json').write_text(json.dumps(report,indent=2)+'\n')
        print(json.dumps({k:v for k,v in report.items() if k!='per_phase'},indent=2))
        assert report['passed'],'Stability/clearance invariant failed; inspect frame-stability-summary.json'
    finally:
        if started:evaluate('var cleanup=AppDomain.CurrentDomain.GetData("FAA.PeripheralFrameProbe") as Action;cleanup?.Invoke();return "Probe cleanup complete";')

if __name__=='__main__':main()
