"""Sample actual render callbacks, never record the webcam or send flight/AI commands."""
import json
from pathlib import Path
import subprocess
import time
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'artifacts/radar-brief-fix'
def evaluate(code):
 p=subprocess.run(['unity','command','eval',code,'--format','json'],cwd=ROOT,capture_output=True,text=True,timeout=20)
 e=json.loads(p.stdout)
 if not e.get('success'):raise RuntimeError(e)
 r=e['data']['result']
 if not r.get('success'):raise RuntimeError(r)
 return r['result']
def main():
 output=OUT/'render-frames.json'
 if output.exists():output.unlink()
 try:
  print(evaluate((ROOT/'Tools/ExplanationVerification/RunRadarBriefFrameProbe.cs').read_text()),flush=True)
  deadline=time.monotonic()+90
  while not output.exists() and time.monotonic()<deadline:time.sleep(1)
  if not output.exists():raise TimeoutError('Render probe did not complete')
  rows=json.loads(output.read_text());assert len(rows)>=300
  min_angle=min(p['minAngle'] for row in rows for p in row['panels'])
  motion=[max(r['launcher'][i] for r in rows)-min(r['launcher'][i] for r in rows) for i in range(4)]
  assert min_angle>=59.9,(min_angle,'radar group blocks forward cone')
  assert max(motion)<.01,(motion,'Pilot Brief launcher drift')
  assert all(r['visible'] for r in rows),'collapsed launcher disappeared'
  report={'render_samples':len(rows),'phases':len(set(r['phase'] for r in rows)),
          'pilot_brief_xywh_peak_to_peak':motion,'minimum_rendered_radar_corner_angle':min_angle,'camera_opened':False,'passed':True}
  (OUT/'render-summary.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
 finally:
  evaluate('var cleanup=AppDomain.CurrentDomain.GetData("FAA.RadarBrief.FrameProbe") as Action;cleanup?.Invoke();return "probe removed";')
if __name__=='__main__':main()
