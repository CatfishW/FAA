"""Exercise real CPU inference with Google's public reference image and a blank frame. Never opens a camera."""
import argparse
import json
from pathlib import Path
import queue
import subprocess
import sys
import threading
import time
import cv2
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
ASSETS=ROOT/'Assets/StreamingAssets/FAA/WebcamGestures'

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--executable');args=parser.parse_args()
    fixture=ROOT/'Library/FAAWebcam/woman_hands.jpg'
    if not fixture.exists():raise RuntimeError('Download the documented public fixture before running smoke test.')
    command=[args.executable] if args.executable else [sys.executable,'-u',str(ASSETS/'worker.py')]
    command+=['--model',str(ASSETS/'hand_landmarker.task')]
    process=subprocess.Popen(command,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    lines=queue.Queue();errors=[]
    def read():
        for line in process.stdout:lines.put(line)
    def drain():
        for line in process.stderr:
            if len(errors)<20:errors.append(line.decode(errors='replace')[:300])
    threading.Thread(target=read,daemon=True).start();threading.Thread(target=drain,daemon=True).start()
    records=[]
    try:
        ready=json.loads(lines.get(timeout=45));assert ready.get('kind')=='ready',ready
        import struct
        image=cv2.imread(str(fixture));h,w=image.shape[:2];image=cv2.resize(image,(320,round(h*320/w)))
        h,w=image.shape[:2]
        for seq,img in [(1,np.zeros_like(image)),(2,image),(3,image),(4,np.zeros_like(image))]:
            rgb=cv2.cvtColor(img,cv2.COLOR_BGR2RGB)[::-1].tobytes()
            start=time.perf_counter()
            process.stdin.write(struct.pack('<4sQIII',b'FWH1',seq,w,h,len(rgb))+rgb);process.stdin.flush()
            result=json.loads(lines.get(timeout=20));result['roundtripMs']=round((time.perf_counter()-start)*1000,3)
            assert result.get('kind')=='frame' and result['seq']==seq,result
            if seq in [1,4]:assert len(result['hands'])==0,result
            else:
                assert len(result['hands'])==2,result
                assert all(len(h['landmarks'])==63 and len(h['pinches'])==4 and len(h['extensions'])==5 for h in result['hands'])
            records.append(result)
        process.stdin.close();assert process.wait(timeout=10)==0
        report={'mode':'real local model inference on public still-image fixture; no webcam opened','bundled':bool(args.executable),
                'mediapipe':'0.10.21','records':records,'passed':True}
        output=ROOT/'artifacts/webcam-gestures';output.mkdir(parents=True,exist_ok=True)
        (output/('bundled-inference.json' if args.executable else 'python-inference.json')).write_text(json.dumps(report,indent=2)+'\n')
        print(json.dumps(report,indent=2))
    finally:
        if process.poll() is None:process.kill();process.wait(timeout=5)
        if not records:print('\n'.join(errors),file=sys.stderr)

if __name__=='__main__':main()
