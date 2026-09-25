import json
from pathlib import Path
import subprocess
import time
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'artifacts/analog-symbology'
def evaluate(code):
    p=subprocess.run(['unity','command','eval',code,'--format','json'],cwd=ROOT,capture_output=True,text=True,timeout=20)
    e=json.loads(p.stdout)
    if not e.get('success'):raise RuntimeError(e)
    r=e['data']['result']
    if not r.get('success'):raise RuntimeError(r)
    return r['result']
def main():
    result=OUT/'render-frames.json';error=OUT/'render-error.txt'
    if result.exists():result.unlink()
    if error.exists():error.unlink()
    print(evaluate((ROOT/'Tools/ExplanationVerification/RunAnalogFrameProbe.cs').read_text()))
    try:
        deadline=time.monotonic()+90
        while not result.exists() and not error.exists() and time.monotonic()<deadline:time.sleep(2)
        if error.exists():raise RuntimeError(error.read_text())
        if not result.exists():raise TimeoutError('Analog render probe did not finish')
        rows=json.loads(result.read_text());assert len(rows)==200
        metrics=[]
        for phase in range(5):
            group=[r for r in rows if r['phase']==phase];assert len(group)==40
            worst=0
            for id in ['airspeed','altitude','torque','nr']:
                boxes=[next(x['bounds'] for x in r['geometry'] if x['Id']==id) for r in group]
                assert all(b is not None for b in boxes),(phase,id)
                worst=max(worst,max(max(b[i] for b in boxes)-min(b[i] for b in boxes) for i in range(4)))
            metrics.append({'phase':phase,'version':group[0]['version'],'geometry_max_delta_px':worst})
            assert worst<.05,(phase,worst)
        telemetry=[r['telemetry'] for r in rows if r['version']=='ClassicAnalog' and r['telemetry']['Fresh']]
        summary={'render_frames':300,'settled_samples':len(rows),'phases':metrics,'fresh_analog_samples':len(telemetry),
                 'observed_live_altitude_range_ft':[min(r['Altitude'] for r in telemetry),max(r['Altitude'] for r in telemetry)] if telemetry else None,
                 'camera_opened':False,'passed':True}
        (OUT/'render-summary.json').write_text(json.dumps(summary,indent=2)+'\n');print(json.dumps(summary,indent=2))
    finally:
        evaluate('var stop=AppDomain.CurrentDomain.GetData("FAA.Analog.FrameProbe") as Action;stop?.Invoke();return "Probe released";')
if __name__=='__main__':main()
