#!/usr/bin/env python3
"""Local RGB stdin to hand-landmark stdout. Never opens cameras, sockets or image recordings."""
from __future__ import annotations
import argparse
import json
import math
import os
from pathlib import Path
import struct
import sys
import time

HEADER = struct.Struct('<4sQIII')

def emit(value):
    sys.stdout.write(json.dumps(value, allow_nan=False, separators=(',', ':')) + '\n')
    sys.stdout.flush()

def read_exact(stream, count):
    data = bytearray()
    while len(data) < count:
        part = stream.read(count-len(data))
        if not part:
            if not data:
                raise EOFError
            raise ValueError('Truncated frame')
        data.extend(part)
    return bytes(data)

def read_frame(stream, previous):
    magic, seq, width, height, count = HEADER.unpack(read_exact(stream, HEADER.size))
    if magic != b'FWH1' or not 32 <= width <= 640 or not 32 <= height <= 640:
        raise ValueError('Invalid header')
    if count != width*height*3 or seq <= previous or seq > 2**53:
        raise ValueError('Invalid size or sequence')
    return seq, width, height, read_exact(stream, count)

def summarize(landmarks, width, height, world=None, handedness=None):
    if len(landmarks) != 21:
        return None
    aspect = height/width
    points = [(float(p.x), float(p.y)) for p in landmarks]
    if any(not math.isfinite(v) for p in points for v in p):
        return None
    def distance(a,b):
        return math.hypot(points[a][0]-points[b][0],(points[a][1]-points[b][1])*aspect)
    palm = (distance(0,9)+distance(5,17))/2
    if palm < .025:
        return None
    joints = [(float(p.x), float(p.y), float(getattr(p, 'z', 0))) for p in (world or landmarks)]
    if len(joints) != 21 or any(not math.isfinite(v) for p in joints for v in p):
        return None
    def angle(a,b,c):
        u=[joints[a][k]-joints[b][k] for k in range(3)]
        v=[joints[c][k]-joints[b][k] for k in range(3)]
        norm=math.sqrt(sum(x*x for x in u)*sum(x*x for x in v))
        return math.degrees(math.acos(max(-1,min(1,sum(u[k]*v[k] for k in range(3))/norm)))) if norm>1e-9 else 0
    extension=[max(0,min(1,(angle(a,b,c)-90)/70)) for a,b,c in [(1,2,4),(5,6,8),(9,10,12),(13,14,16),(17,18,20)]]
    palm_indices=(0,5,9,13,17)
    categories=handedness or []
    return {'x':(points[4][0]+points[8][0])/2, 'y':(points[4][1]+points[8][1])/2,
            'palmX':sum(points[i][0] for i in palm_indices)/5,
            'palmY':sum(points[i][1] for i in palm_indices)/5,
            'palmSize':palm, 'pinchRatio':distance(4,8)/palm,
            'pinches':[round(distance(4,i)/palm,5) for i in (8,12,16,20)],
            'extensions':[round(x,5) for x in extension],
            'landmarks':[round(v,5) for p in landmarks for v in (float(p.x),float(p.y),float(getattr(p,'z',0)))],
            'handedness':categories[0].category_name if categories else '',
            'handednessScore':float(categories[0].score) if categories else 0,
            'fingers':sum(x>.7 for x in extension)}

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--model',type=Path,required=True)
    args=parser.parse_args()
    if not args.model.is_file():
        emit({'kind':'error','error':'Hand model missing; run webcam setup.'})
        return 2
    os.environ.setdefault('GLOG_minloglevel','2')
    os.environ.setdefault('TF_CPP_MIN_LOG_LEVEL','3')
    os.environ.setdefault('MPLBACKEND','Agg')
    import numpy as np
    import mediapipe as mp
    if mp.__version__ != '0.10.21':
        emit({'kind':'error','error':'Unexpected MediaPipe version; rebuild pinned worker.'})
        return 2
    options=mp.tasks.vision.HandLandmarkerOptions(
        base_options=mp.tasks.BaseOptions(model_asset_path=str(args.model),delegate=mp.tasks.BaseOptions.Delegate.CPU),
        running_mode=mp.tasks.vision.RunningMode.VIDEO,num_hands=2,
        min_hand_detection_confidence=.6,min_hand_presence_confidence=.6,min_tracking_confidence=.5)
    with mp.tasks.vision.HandLandmarker.create_from_options(options) as detector:
        emit({'kind':'ready','protocol':2,'features':'multi-finger-v2'})
        previous=0
        epoch=time.monotonic();last_ms=0
        while True:
            try:
                seq,width,height,raw=read_frame(sys.stdin.buffer,previous)
            except EOFError:
                break
            previous=seq
            started=time.perf_counter()
            rgb=np.frombuffer(raw,dtype=np.uint8).reshape(height,width,3)[::-1].copy()
            timestamp=max(last_ms+1,int((time.monotonic()-epoch)*1000));last_ms=timestamp
            prediction=detector.detect_for_video(mp.Image(image_format=mp.ImageFormat.SRGB,data=rgb),timestamp)
            hands=[]
            for i,row in enumerate(prediction.hand_landmarks):
                h=summarize(row,width,height,prediction.hand_world_landmarks[i],prediction.handedness[i])
                if h is not None: hands.append(h)
            hands.sort(key=lambda h:h['palmX'])
            emit({'kind':'frame','seq':seq,'width':width,'height':height,'hands':hands,
                  'inferenceMs':round((time.perf_counter()-started)*1000,3)})
    return 0

if __name__=='__main__':
    try:
        raise SystemExit(main())
    except (BrokenPipeError,KeyboardInterrupt):
        pass
    except Exception as error:
        emit({'kind':'error','error':'Local hand recognition failed: '+type(error).__name__})
        raise SystemExit(1)
