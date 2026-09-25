#!/usr/bin/env python3
"""Prepare the task-scoped webcam worker. Does not request/open camera access."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import urllib.request

ROOT=Path(__file__).resolve().parents[2]
CACHE=ROOT/'Library'/'FAAWebcam'
ASSETS=ROOT/'Assets'/'StreamingAssets'/'FAA'/'WebcamGestures'
MODEL_URL='https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task'
MODEL_SHA256='fbc2a30080c3c557093b5ddfc334698132eb341044ccee322ccf8bcf3607cde1'

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--bundle',action='store_true',help='Build a self-contained worker for this machine/OS only.')
    parser.add_argument('--licenses-only',action='store_true',help='Refresh license texts for an existing local bundle without rebuilding.')
    args=parser.parse_args()
    CACHE.mkdir(parents=True,exist_ok=True)
    uv=shutil.which('uv')
    venv=CACHE/'venv'
    python=venv/('Scripts/python.exe' if sys.platform=='win32' else 'bin/python')
    if not python.exists():
        if uv:
            subprocess.run([uv,'venv','--python','3.12',str(venv)],check=True)
        else:
            if sys.version_info[:2] not in [(3,10),(3,11),(3,12)]:
                raise RuntimeError('Run with Python 3.10–3.12, or install uv for an isolated Python 3.12 runtime.')
            subprocess.run([sys.executable,'-m','venv',str(venv)],check=True)
    install=[uv,'pip','install','--python',str(python)] if uv else [str(python),'-m','pip','install']
    subprocess.run(install+['-r',str(Path(__file__).with_name('requirements.txt'))],check=True)
    model=ASSETS/'hand_landmarker.task'
    if not model.exists():
        with urllib.request.urlopen(MODEL_URL,timeout=60) as response:
            data=response.read(12000000)
        if len(data)<1000000 or len(data)>=12000000:
            raise RuntimeError('Unexpected hand model length')
        model.write_bytes(data)
    fingerprint=hashlib.sha256(model.read_bytes()).hexdigest()
    if fingerprint!=MODEL_SHA256:
        raise RuntimeError('Hand model hash mismatch; refusing to use an unverified model.')
    (CACHE/'model-provenance.json').write_text(json.dumps({'url':MODEL_URL,'sha256':fingerprint,'bytes':model.stat().st_size},indent=2)+'\n')
    if args.bundle or args.licenses_only:
        arch=platform.machine().lower()
        target=('osx' if sys.platform=='darwin' else 'windows' if sys.platform=='win32' else 'linux')+'-'+('arm64' if arch in ['arm64','aarch64'] else 'x64')
        destination=CACHE/'bundles'/target
        if args.bundle:
            subprocess.run([str(python),'-m','PyInstaller','--noconfirm','--clean','--onedir','--name','FaaWebcamWorker',
                        '--distpath',str(destination),'--workpath',str(CACHE/'build'),'--specpath',str(CACHE),
                        '--collect-all','mediapipe','--exclude-module','jax','--exclude-module','jaxlib',
                        '--exclude-module','IPython','--exclude-module','pytest',str(ASSETS/'worker.py')],check=True,cwd=ROOT)
        bundle=destination/'FaaWebcamWorker'
        if not bundle.is_dir():
            raise RuntimeError('Build the platform worker first with --bundle.')
        subprocess.run([str(python),str(Path(__file__).with_name('collect_licenses.py')),str(bundle/'licenses')],check=True)
        print('Bundle ready:',destination/'FaaWebcamWorker')
    print('Local worker Python:',python)
    print('Model SHA256:',fingerprint)
    print('Camera was not opened. Unity LAYOUT > LAPTOP CAMERA offers opt-in start.')

if __name__=='__main__':
    main()
