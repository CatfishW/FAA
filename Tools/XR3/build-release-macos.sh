#!/bin/bash
# Build the current FAA worktree without changing its scenes or embedded packages.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
UNITY="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
WORK="$ROOT/_artifacts/xr3/$STAMP"
STAGE="$WORK/project"
OUTPUT="$ROOT/Builds/FAA-XR3-$STAMP-Windows-x64"
CESIUM_VERSION=1.25.1
CESIUM_SHA256=bde7aacd4a8b251a44984aaefa4ce44878727cab30d81f588ebf9ed146a5da3b
CACHE="$ROOT/_artifacts/xr3/vendor"
ARCHIVE="$CACHE/com.cesium.unity-$CESIUM_VERSION.tgz"

test -x "$UNITY" || { echo "Unity $VERSION not found: $UNITY" >&2; exit 1; }
test ! -e "$STAGE" && test ! -e "$OUTPUT"
mkdir -p "$STAGE" "$CACHE" "$OUTPUT"
git -C "$ROOT" status --porcelain > "$WORK/source-status.txt"
git -C "$ROOT" diff --binary > "$WORK/source-worktree.patch"

if ! test -f "$ARCHIVE"; then
  curl --fail --location --retry 2 --max-time 900 \
    "https://github.com/CesiumGS/cesium-unity/releases/download/v$CESIUM_VERSION/com.cesium.unity-$CESIUM_VERSION.tgz" \
    --output "$ARCHIVE.download"
  ACTUAL="$(shasum -a 256 "$ARCHIVE.download" | cut -d ' ' -f 1)"
  test "$ACTUAL" = "$CESIUM_SHA256" || { echo "Cesium download checksum mismatch" >&2; exit 1; }
  mv "$ARCHIVE.download" "$ARCHIVE"
fi
test "$(shasum -a 256 "$ARCHIVE" | cut -d ' ' -f 1)" = "$CESIUM_SHA256" || {
  echo "Cached Cesium package checksum mismatch" >&2; exit 1;
}

# APFS copy-on-write clones are independent files, unlike hard links.
# Close the source Unity editor before starting to ensure a consistent snapshot.
for DIRECTORY in Assets Packages ProjectSettings Library; do
  if test -d "$ROOT/$DIRECTORY"; then
    /bin/cp -cR "$ROOT/$DIRECTORY" "$STAGE/$DIRECTORY"
  fi
done
# Remove generated/private evidence only from the disposable build snapshot.
rm -rf "$STAGE/Assets/artifacts" "$STAGE/Assets/artifacts.meta" "$STAGE/Assets/_Recovery"
# Optional exact in-memory scene copy exported by the Editor without saving the source scene.
if test -n "${FAA_XR3_SCENE_COPY:-}"; then
  test -f "$FAA_XR3_SCENE_COPY"
  cp "$FAA_XR3_SCENE_COPY" "$STAGE/Assets/_Project/Scenes/ExperimentScene.unity"
fi
# Replace only the disposable build copy. Do not merge a source-generator build
# with release binaries: their native callback ABI can differ.
mv "$STAGE/Packages/com.cesium.unity" "$WORK/original-cesium-source"
mkdir -p "$STAGE/Packages/com.cesium.unity"
tar -xzf "$ARCHIVE" -C "$STAGE/Packages/com.cesium.unity" --strip-components 1

# Terrain height assets need 4097-pixel render targets during serialization.
# Null graphics caps them at 4096 and Unity can misleadingly report Succeeded
# while recording texture creation errors. Use the host GPU for the build.
"$UNITY" -batchmode -quit -projectPath "$STAGE" \
  -buildTarget Win64 \
  -executeMethod FAA.Headset.Editor.XR3ReleaseBuild.BuildWindows64 \
  -xr3Output "$OUTPUT/FAA-XR3.exe" -logFile "$WORK/unity-build.log"

test -s "$OUTPUT/FAA-XR3.exe"
cp "$ROOT/Tools/XR3/Launch-XR3.cmd" "$OUTPUT/Launch-XR3.cmd"
cp "$ROOT/Tools/XR3/Check-Terrain.cmd" "$OUTPUT/Check-Terrain.cmd"
cp "$ROOT/Tools/XR3/Start-Terrain-Tunnel.cmd" "$OUTPUT/Start-Terrain-Tunnel.cmd"
cp "$ROOT/docs/XR3_WINDOWS_BUILD.md" "$OUTPUT/README-XR3.md"
cp "$ROOT/docs/SWITCHABLE_SYMBOLOGY.md" "$OUTPUT/HUD-SYMBOLOGY.md"
cp "$ROOT/docs/HUD_TERRAIN_XR3_20260925.md" "$OUTPUT/HUD-TERRAIN-UPDATE.md"
cp "$ROOT/Tools/XPlaneTerrain/README.md" "$OUTPUT/TERRAIN-CONNECTION.md"
python3 "$ROOT/Tools/XR3/package-release.py" "$OUTPUT"
printf '\nXR-3 release: %s.zip\nBuild log: %s/unity-build.log\n' "$OUTPUT" "$WORK"
