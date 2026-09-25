#!/usr/bin/env python3
"""Validate a completed Unity Windows XR-3 release and create its portable ZIP."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import zipfile


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def require_x64(path):
    with path.open("rb") as stream:
        header = stream.read(64)
        if len(header) != 64 or header[:2] != b"MZ":
            raise ValueError("Not a Windows PE file: " + str(path))
        stream.seek(struct.unpack_from("<I", header, 60)[0])
        pe = stream.read(6)
        if len(pe) != 6 or pe[:4] != b"PE\0\0" or struct.unpack_from("<H", pe, 4)[0] != 0x8664:
            raise ValueError("Not a Windows x86-64 binary: " + str(path))


def package_release(folder):
    folder = folder.resolve()
    report = json.loads((folder / "build-report.json").read_text())
    if report.get("result") != "Succeeded" or report.get("errors") != 0:
        raise ValueError("Unity did not report a successful error-free build")
    if report.get("target") != "StandaloneWindows64" or report.get("development"):
        raise ValueError("Not a Windows x64 non-development release")
    for name in ("FAA-XR3.exe", "UnityPlayer.dll"):
        require_x64(folder / name)
    data = folder / "FAA-XR3_Data"
    for name in ("VarjoUnityXR.dll", "VarjoLib.dll", "openvr_api.dll", "CesiumForUnityNative.dll"):
        matches = list(data.rglob(name))
        if len(matches) != 1:
            raise ValueError("Expected exactly one bundled native plugin " + name)
        require_x64(matches[0])
    if not (folder / "MonoBleedingEdge").is_dir():
        raise ValueError("Mono runtime directory is missing")
    if not (data / "Managed" / "Assembly-CSharp.dll").is_file():
        raise ValueError("FAA game assembly is missing")
    for name in ("Launch-XR3.cmd", "README-XR3.md", "Check-Terrain.cmd", "Start-Terrain-Tunnel.cmd"):
        if not (folder / name).is_file():
            raise ValueError("Missing operator file: " + name)
    connection = data / "StreamingAssets" / "FAA" / "TerrainConnection.json"
    if not connection.is_file() or not json.loads(connection.read_text()).get("terrainUrl"):
        raise ValueError("Terrain runtime endpoint configuration is missing")
    # Unity may emit optional Burst symbols next to a non-development player.
    # Keep those locally, but never include DoNotShip folders in the release ZIP.
    def distributable(path):
        return path.is_file() and path.name != ".DS_Store" and not any(
            part.endswith("DoNotShip") for part in path.relative_to(folder).parts)

    inventory = []
    for path in sorted(folder.rglob("*")):
        if distributable(path) and path.name != "SHA256SUMS.txt":
            inventory.append(sha256(path) + "  " + path.relative_to(folder).as_posix())
    (folder / "SHA256SUMS.txt").write_text("\n".join(inventory) + "\n")
    archive = folder.with_name(folder.name + ".zip")
    if archive.exists():
        raise FileExistsError("Refusing to overwrite an existing release archive: " + str(archive))
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=3, allowZip64=True) as output:
        for path in sorted(folder.rglob("*")):
            if distributable(path):
                output.write(path, folder.name + "/" + path.relative_to(folder).as_posix())
    with zipfile.ZipFile(archive) as output:
        corrupt = output.testzip()
        if corrupt:
            raise ValueError("Corrupt ZIP member: " + corrupt)
    checksum = sha256(archive)
    archive.with_suffix(".zip.sha256").write_text(checksum + "  " + archive.name + "\n")
    print(json.dumps({"archive": str(archive), "bytes": archive.stat().st_size,
                      "sha256": checksum, "files": len(inventory) + 1,
                      "nativeArchitecture": "x86_64", "zipIntegrity": "passed",
                      "hardwareTested": False}, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folder", type=Path)
    package_release(parser.parse_args().folder)
