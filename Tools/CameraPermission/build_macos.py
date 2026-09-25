"""Build the small universal macOS camera-consent plugin. Does not request camera access."""
from pathlib import Path
import plistlib
import subprocess

root = Path(__file__).resolve().parents[2]
bundle = root / 'Assets/Plugins/macOS/FaaCameraPermission.bundle'
binary = bundle / 'Contents/MacOS/FaaCameraPermission'
binary.parent.mkdir(parents=True, exist_ok=True)
subprocess.run(['xcrun','clang++','-bundle','-fobjc-arc','-fblocks','-std=c++17',
                '-arch','arm64','-arch','x86_64','-mmacosx-version-min=11.0',
                '-framework','AVFoundation','-framework','Foundation',
                str(Path(__file__).with_name('FaaCameraPermission.mm')),'-o',str(binary)], check=True)
with (bundle/'Contents/Info.plist').open('wb') as output:
    plistlib.dump({'CFBundleExecutable':'FaaCameraPermission','CFBundleIdentifier':'org.faa.simulator.camera-permission',
                  'CFBundleName':'FaaCameraPermission','CFBundlePackageType':'BNDL','CFBundleVersion':'1'},output)
subprocess.run(['codesign','--force','--sign','-',str(bundle)],check=True)
print('Built universal app-local camera permission plugin:',bundle)
