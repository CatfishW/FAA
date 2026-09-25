#!/usr/bin/env python3
"""Install a user-owned reconnecting SSH forward for the read-only terrain service.
No secrets are stored; uses the user's existing SSH configuration/known hosts.
"""
import argparse
import os
from pathlib import Path
import plistlib
import re
import subprocess
import sys

LABEL = 'org.warmpix.faa.xplane-terrain-tunnel'

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--host', default='4090', help='Existing SSH alias or user@host; no passwords or options.')
    parser.add_argument('--remove', action='store_true')
    args = parser.parse_args()
    if sys.platform != 'darwin': raise RuntimeError('This installer is for macOS only.')
    if not re.fullmatch(r'[A-Za-z0-9_][A-Za-z0-9_.@-]{0,199}', args.host): raise ValueError('Invalid SSH host alias.')
    agent = Path.home()/'Library/LaunchAgents'/f'{LABEL}.plist'
    domain = f'gui/{os.getuid()}'
    if args.remove:
        subprocess.run(['launchctl','bootout',f'{domain}/{LABEL}'],check=False,capture_output=True)
        if agent.exists(): agent.unlink()
        print('Removed only the FAA terrain tunnel agent.');return
    logs = Path.home()/'Library/Logs/FAA'; logs.mkdir(parents=True,exist_ok=True)
    spec = {'Label':LABEL,'ProgramArguments':['/usr/bin/ssh','-N','-T','-o','BatchMode=yes',
        '-o','ExitOnForwardFailure=yes','-o','ConnectTimeout=15','-o','ServerAliveInterval=15',
        '-o','ServerAliveCountMax=3','-L','127.0.0.1:12679:127.0.0.1:8767',args.host],
        'RunAtLoad':True,'KeepAlive':True,'ThrottleInterval':15,
        'StandardOutPath':str(logs/'terrain-tunnel.log'),'StandardErrorPath':str(logs/'terrain-tunnel-error.log')}
    agent.parent.mkdir(parents=True,exist_ok=True)
    subprocess.run(['launchctl','bootout',f'{domain}/{LABEL}'],check=False,capture_output=True)
    agent.write_bytes(plistlib.dumps(spec));agent.chmod(0o600)
    subprocess.run(['launchctl','bootstrap',domain,str(agent)],check=True)
    print('Installed reconnecting user tunnel:',agent)
    print('127.0.0.1:12679 -> existing SSH host -> 127.0.0.1:8767; remote service unchanged.')

if __name__=='__main__':main()
